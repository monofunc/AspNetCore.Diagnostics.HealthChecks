using System.Net;
using HealthChecks.UI.Data;
using HealthChecks.UI.ServiceDiscovery.Kubernetes.Extensions;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthChecks.UI.ServiceDiscovery.Kubernetes;

/// <summary>
/// Background service that discovers Kubernetes services with health check endpoints and registers them automatically.
/// </summary>
/// <remarks>
/// <para>
/// Implements poll-based discovery, querying the Kubernetes API every <see cref="KubernetesDiscoverySettings.RefreshTimeInSeconds"/>
/// for services with a matching label. Discovered services are validated by calling their health endpoints before registration.
/// Services responding with HTTP 200 (OK) or 503 (Service Unavailable) are considered valid and will be registered.
/// </para>
/// <para>
/// The service initializes the Kubernetes client automatically: in-cluster configuration is tried first, then kubeconfig file,
/// or explicit <see cref="KubernetesDiscoverySettings.ClusterHost"/> and <see cref="KubernetesDiscoverySettings.Token"/> if provided.
/// </para>
/// <para>
/// <b>Security Note:</b> When connecting to external clusters using <see cref="KubernetesDiscoverySettings.ClusterHost"/>
/// and <see cref="KubernetesDiscoverySettings.Token"/>, TLS certificate validation is disabled to support cloud providers
/// (like Azure AKS) that use self-signed certificates. In-cluster and kubeconfig connections use standard validation.
/// </para>
/// </remarks>
internal sealed class KubernetesDiscoveryHostedService : IHostedService, IDisposable
{
    private readonly KubernetesDiscoverySettings _discoveryOptions;
    private readonly ILogger<KubernetesDiscoveryHostedService> _logger;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly IServiceProvider _serviceProvider;
    private IKubernetes? _discoveryClient;
    private readonly HttpClient _clusterServiceClient;
    private readonly KubernetesAddressFactory _addressFactory;

    private Task? _executingTask;
    private bool _disposed;

    public KubernetesDiscoveryHostedService(
        IServiceProvider serviceProvider,
        IOptions<KubernetesDiscoverySettings> discoveryOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<KubernetesDiscoveryHostedService> logger,
        IHostApplicationLifetime hostLifetime)
    {
        _serviceProvider = serviceProvider;
        _discoveryOptions = discoveryOptions.Value;
        _logger = logger;
        _hostLifetime = hostLifetime;
        _clusterServiceClient = httpClientFactory.CreateClient(KubernetesDiscoveryConstants.K8S_CLUSTER_SERVICE_HTTP_CLIENT_NAME);
        _addressFactory = new KubernetesAddressFactory(_discoveryOptions);
    }

    /// <summary>
    /// Starts the discovery service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown.</param>
    /// <returns>A task that completes immediately after registering the discovery callback.</returns>
    /// <remarks>
    /// The actual discovery loop is scheduled to run on <see cref="IHostApplicationLifetime.ApplicationStarted"/>
    /// to ensure the application is fully initialized before polling begins. This method returns immediately
    /// per the <see cref="IHostedService"/> pattern.
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executingTask = ExecuteAsync(cancellationToken);

        return _executingTask.IsCompleted
            ? _executingTask
            : Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the Kubernetes client and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _discoveryClient?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Registers the discovery callback to run when the application starts.
    /// </summary>
    private Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _hostLifetime.ApplicationStarted.Register(async () =>
        {
            if (_discoveryOptions.Enabled)
            {
                try
                {
#pragma warning disable IDISP003 // Dispose previous before re-assigning
                    _discoveryClient = InitializeKubernetesClient();
#pragma warning restore IDISP003 // Dispose previous before re-assigning
                    await StartK8sServiceAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // We are halting, task cancellation is expected.
                }
            }

        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the discovery service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown timeout.</param>
    /// <returns>A task that waits for the executing task to complete or the cancellation token to be signaled.</returns>
    /// <remarks>
    /// Note that <c>_executingTask</c> completes immediately after scheduling the background loop,
    /// so this method primarily serves to honor the <see cref="IHostedService"/> contract.
    /// The actual polling loop is stopped via cancellation token propagation.
    /// </remarks>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask != null)
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the periodic polling loop that discovers and registers health check services.
    /// </summary>
    private async Task StartK8sServiceAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Starting kubernetes service discovery");

            using var scope = _serviceProvider.CreateScope();

            var livenessDbContext = scope.ServiceProvider.GetRequiredService<HealthChecksDb>();

            try
            {
                var services = await _discoveryClient!.GetServicesAsync(_discoveryOptions.ServicesLabel, _discoveryOptions.Namespaces, cancellationToken).ConfigureAwait(false);

                if (services != null)
                {
                    foreach (var item in services.Items)
                    {
                        try
                        {
                            var serviceAddress = _addressFactory.CreateAddress(item);

                            if (serviceAddress != null && !IsLivenessRegistered(livenessDbContext, serviceAddress))
                            {
                                var statusCode = await CallClusterServiceAsync(serviceAddress).ConfigureAwait(false);
                                if (IsValidHealthChecksStatusCode(statusCode))
                                {
                                    await RegisterDiscoveredLiveness(livenessDbContext, serviceAddress, item.Metadata.Name).ConfigureAwait(false);
                                    _logger.LogInformation($"Registered discovered liveness on {serviceAddress} with name {item.Metadata.Name}");
                                }
                            }
                        }
                        catch (Exception)
                        {
                            _logger.LogError($"Error discovering service {item.Metadata.Name}. It might not be visible");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred on kubernetes service discovery");
            }

            await Task.Delay(_discoveryOptions.RefreshTimeInSeconds * 1000).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Checks whether a service URI is already registered in the database.
    /// </summary>
    internal static bool IsLivenessRegistered(HealthChecksDb livenessDb, string host)
    {
        return livenessDb.Configurations
            .Any(lc => lc.Uri == host);
    }

    /// <summary>
    /// Determines whether an HTTP status code represents a valid health check response.
    /// </summary>
    internal static bool IsValidHealthChecksStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.ServiceUnavailable;
    }

    /// <summary>
    /// Calls a service's health check endpoint and returns the HTTP status code.
    /// </summary>
    private async Task<HttpStatusCode> CallClusterServiceAsync(string host)
    {
        using var response = await _clusterServiceClient.GetAsync(host, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        return response.StatusCode;
    }

    /// <summary>
    /// Registers a discovered service in the database.
    /// </summary>
    private Task<int> RegisterDiscoveredLiveness(HealthChecksDb livenessDb, string host, string name)
    {
        livenessDb.Configurations.Add(new HealthCheckConfiguration
        {
            Name = name,
            Uri = host,
            DiscoveryService = "kubernetes"
        });

        return livenessDb.SaveChangesAsync();
    }

    /// <summary>
    /// Initializes the Kubernetes client using in-cluster config, kubeconfig file, or explicit settings.
    /// </summary>
    private IKubernetes InitializeKubernetesClient()
    {
        KubernetesClientConfiguration kubernetesConfig;

        if (!string.IsNullOrEmpty(_discoveryOptions.ClusterHost) && !string.IsNullOrEmpty(_discoveryOptions.Token))
        {
            kubernetesConfig = new KubernetesClientConfiguration
            {
                Host = _discoveryOptions.ClusterHost,
                AccessToken = _discoveryOptions.Token,
                // Some cloud services like Azure AKS use self-signed certificates not valid for httpclient.
                // With this method we allow invalid certificates
                SkipTlsVerify = true
            };
        }
        else if (KubernetesClientConfiguration.IsInCluster())
        {
            kubernetesConfig = KubernetesClientConfiguration.InClusterConfig();
        }
        else
        {
            kubernetesConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        }

        return new k8s.Kubernetes(kubernetesConfig);
    }
}
