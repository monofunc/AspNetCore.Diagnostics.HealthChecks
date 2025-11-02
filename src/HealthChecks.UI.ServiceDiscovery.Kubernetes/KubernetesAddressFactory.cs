using k8s.Models;

namespace HealthChecks.UI.ServiceDiscovery.Kubernetes;

/// <summary>
/// Constructs health check URLs for Kubernetes services based on service type and annotations.
/// </summary>
/// <remarks>
/// Supports ClusterIP, LoadBalancer, NodePort, and ExternalName service types. Handles IPv6 addresses
/// with bracket notation and respects per-service annotation overrides for path, port, and scheme.
/// </remarks>
internal class KubernetesAddressFactory
{
    private readonly KubernetesDiscoverySettings _settings;

    public KubernetesAddressFactory(KubernetesDiscoverySettings discoveryOptions)
    {
        _settings = discoveryOptions;
    }

    /// <summary>
    /// Constructs the full health check URL for a Kubernetes service.
    /// </summary>
    /// <param name="service">The Kubernetes service to construct a URL for.</param>
    /// <returns>The fully-qualified health check URL.</returns>
    /// <remarks>
    /// <para>
    /// Service type handling:
    /// - <b>ClusterIP</b>: Uses <c>spec.clusterIP</c>
    /// - <b>LoadBalancer</b>: Uses first ingress IP or hostname from <c>status.loadBalancer.ingress</c>, falls back to clusterIP
    /// - <b>NodePort</b>: Uses first ingress IP or hostname from <c>status.loadBalancer.ingress</c> with <c>nodePort</c>, falls back to clusterIP (note: clusterIP with nodePort is only accessible within the cluster)
    /// - <b>ExternalName</b>: Uses <c>spec.externalName</c>
    /// </para>
    /// <para>
    /// Annotation overrides (configured via <see cref="KubernetesDiscoverySettings"/>):
    /// - Path: <c>HealthChecksPath</c> annotation overrides default
    /// - Port: <c>HealthChecksPort</c> annotation (port number or name) overrides default
    /// - Scheme: <c>HealthChecksScheme</c> annotation (http/https) overrides default http
    /// </para>
    /// <para>
    /// IPv6 addresses are automatically wrapped in brackets: <c>http://[::1]:8080/health</c>
    /// </para>
    /// </remarks>
    /// <example>
    /// Service with annotation overrides:
    /// <code>
    /// metadata:
    ///   annotations:
    ///     HealthChecksPath: "api/health"
    ///     HealthChecksPort: "8080"
    ///     HealthChecksScheme: "https"
    /// </code>
    /// Result: <c>https://10.96.0.1:8080/api/health</c>
    /// </example>
    public string CreateAddress(V1Service service)
    {
        ArgumentNullException.ThrowIfNull(service);

        string address = string.Empty;

        var port = GetServicePortValue(service);

        switch (service.Spec.Type)
        {
            case ServiceType.LOAD_BALANCER:
            case ServiceType.NODE_PORT:
                address = GetLoadBalancerAddress(service);
                break;
            case ServiceType.CLUSTER_IP:
                address = service.Spec.ClusterIP;
                break;
            case ServiceType.EXTERNAL_NAME:
                address = service.Spec.ExternalName;
                break;
        }

        string healthPath = _settings.HealthPath;
        if (!string.IsNullOrEmpty(_settings.ServicesPathAnnotation) && (service.Metadata.Annotations?.ContainsKey(_settings.ServicesPathAnnotation) ?? false))
        {
            healthPath = service.Metadata.Annotations![_settings.ServicesPathAnnotation]!;
        }
        healthPath = healthPath.TrimStart('/');

        string healthScheme = "http";
        if (!string.IsNullOrEmpty(_settings.ServicesSchemeAnnotation) && (service.Metadata.Annotations?.ContainsKey(_settings.ServicesSchemeAnnotation) ?? false))
        {
            healthScheme = service.Metadata.Annotations![_settings.ServicesSchemeAnnotation]!.ToLower();
        }

        // Support IPv6 address hosts
        return address.Contains(':')
            ? $"{healthScheme}://[{address}]{port}/{healthPath}"
            : $"{healthScheme}://{address}{port}/{healthPath}";
    }

    /// <summary>
    /// Extracts the address from a LoadBalancer service, falling back to ClusterIP if no ingress is available.
    /// </summary>
    private static string GetLoadBalancerAddress(V1Service service)
    {
        var firstIngress = service.Status?.LoadBalancer?.Ingress?.FirstOrDefault();
        if (firstIngress is V1LoadBalancerIngress ingress)
        {
            return string.IsNullOrEmpty(ingress.Ip) ? ingress.Hostname : ingress.Ip;
        }

        return service.Spec.ClusterIP;
    }

    /// <summary>
    /// Determines the port to use for the service URL, formatted as <c>:port</c> or empty string.
    /// </summary>
    private string GetServicePortValue(V1Service service)
    {
        int? port;
        switch (service.Spec.Type)
        {
            case ServiceType.LOAD_BALANCER:
            case ServiceType.CLUSTER_IP:
                port = GetServicePort(service)?.Port;
                break;
            case ServiceType.NODE_PORT:
                port = GetServicePort(service)?.NodePort;
                break;
            case ServiceType.EXTERNAL_NAME:
                port = GetServicePortAnnotation(service) is string servicePortAnnotation && int.TryParse(servicePortAnnotation, out var servicePort)
                    ? servicePort
                    : null;
                break;
            default:
                port = null;
                break;
        }

        return port is null ? string.Empty : $":{port.Value}";
    }

    /// <summary>
    /// Resolves the service port from annotations or defaults to the first port.
    /// </summary>
    private V1ServicePort? GetServicePort(V1Service service)
    {
        if (GetServicePortAnnotation(service) is string portAnnotationValue)
        {
            if (int.TryParse(portAnnotationValue, out var portAnnotationIntValue))
            {
                return service.Spec?.Ports?.Where(p => p.Port == portAnnotationIntValue)?.FirstOrDefault();
            }
            else
            {
                return service.Spec?.Ports?.Where(p => p.Name == portAnnotationValue)?.FirstOrDefault();
            }
        }
        else
        {
            return service.Spec?.Ports?.FirstOrDefault();
        }
    }

    /// <summary>
    /// Retrieves the port annotation value from the service metadata.
    /// </summary>
    private string? GetServicePortAnnotation(V1Service service)
    {
        if (!string.IsNullOrEmpty(_settings.ServicesPortAnnotation) && (service.Metadata.Annotations?.ContainsKey(_settings.ServicesPortAnnotation) ?? false))
        {
            return service.Metadata.Annotations![_settings.ServicesPortAnnotation]!;
        }
        return null;
    }
}
