using HealthChecks.UI.ServiceDiscovery.Kubernetes;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Kubernetes service discovery.
/// </summary>
public static class KubernetesServiceDiscoveryExtensions
{
    /// <summary>
    /// Adds Kubernetes service discovery to the HealthChecks UI.
    /// </summary>
    /// <param name="builder">The <see cref="HealthChecksUIBuilder"/> to add services to.</param>
    /// <param name="configuration">The configuration instance to bind settings from.</param>
    /// <returns>The <see cref="HealthChecksUIBuilder"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// This method registers the Kubernetes discovery hosted service which periodically polls
    /// the Kubernetes API for services with matching labels and automatically registers them
    /// as health check endpoints in the HealthChecks UI database.
    /// </para>
    /// <para>
    /// Configuration is read from the <c>HealthChecksUI:KubernetesDiscoveryService</c> section.
    /// Ensure your appsettings.json includes the required settings with <c>Enabled: true</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services
    ///     .AddHealthChecksUI()
    ///     .AddSqlServerStorage("connection-string")
    ///     .AddKubernetesServiceDiscovery(configuration);
    /// </code>
    /// </example>
    public static HealthChecksUIBuilder AddKubernetesServiceDiscovery(
        this HealthChecksUIBuilder builder,
        IConfiguration configuration)
    {
        builder.Services
            .AddOptions<KubernetesDiscoverySettings>()
            .Bind(configuration.GetSection("HealthChecksUI:KubernetesDiscoveryService"));

        builder.Services.AddHostedService<KubernetesDiscoveryHostedService>();

        builder.Services.AddHttpClient(KubernetesDiscoveryConstants.K8S_CLUSTER_SERVICE_HTTP_CLIENT_NAME);

        return builder;
    }
}
