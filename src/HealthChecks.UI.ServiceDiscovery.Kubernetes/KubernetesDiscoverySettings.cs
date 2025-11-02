namespace HealthChecks.UI.ServiceDiscovery.Kubernetes;

/// <summary>
/// Configuration settings for Kubernetes service discovery in HealthChecks UI.
/// </summary>
/// <remarks>
/// The discovery service periodically polls Kubernetes for services with a matching label
/// and automatically registers them as health check endpoints. Configure via
/// <c>appsettings.json</c> under <c>HealthChecksUI:KubernetesDiscoveryService</c>.
/// </remarks>
/// <example>
/// <code>
/// "HealthChecksUI": {
///   "KubernetesDiscoveryService": {
///     "Enabled": true,
///     "ServicesLabel": "HealthChecks",
///     "Namespaces": ["production", "staging"],
///     "RefreshTimeInSeconds": 300
///   }
/// }
/// </code>
/// </example>
public class KubernetesDiscoverySettings
{
    /// <summary>
    /// Gets or sets a value indicating whether Kubernetes discovery is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the Kubernetes cluster host URL for external cluster connections.
    /// </summary>
    /// <remarks>
    /// If both <see cref="ClusterHost"/> and <see cref="Token"/> are provided, uses explicit
    /// configuration. Otherwise, attempts in-cluster config first, then falls back to kubeconfig.
    /// If <see langword="null"/>, auto-detects from in-cluster configuration or kubeconfig file.
    /// </remarks>
    public string? ClusterHost { get; set; }

    /// <summary>
    /// Gets or sets the default health check path for discovered services.
    /// </summary>
    /// <remarks>
    /// Can be overridden per-service using the <see cref="ServicesPathAnnotation"/> annotation.
    /// </remarks>
    public string HealthPath { get; set; } = KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_PATH;

    /// <summary>
    /// Gets or sets the Kubernetes label selector for identifying health check services.
    /// </summary>
    /// <remarks>
    /// Only services with this label will be discovered and registered. The value is a standard
    /// Kubernetes label selector expression. For simple label existence checks, use <c>"HealthChecks"</c>
    /// (services with any value for this label). For specific values, use <c>"HealthChecks=true"</c>.
    /// </remarks>
    public string ServicesLabel { get; set; } = KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_LABEL;

    /// <summary>
    /// Gets or sets the annotation name used to override the health check path for a service.
    /// </summary>
    public string ServicesPathAnnotation { get; set; } = KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PATH_ANNOTATION;

    /// <summary>
    /// Gets or sets the annotation name used to override the port for a service.
    /// </summary>
    /// <remarks>
    /// Annotation value can be a port number (<c>"8080"</c>) or port name (<c>"http"</c>).
    /// </remarks>
    public string ServicesPortAnnotation { get; set; } = KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PORT_ANNOTATION;

    /// <summary>
    /// Gets or sets the annotation name used to override the scheme (http/https) for a service.
    /// </summary>
    public string ServicesSchemeAnnotation { get; set; } = KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_SCHEME_ANNOTATION;

    /// <summary>
    /// Gets or sets the authentication token for external cluster connections.
    /// </summary>
    /// <remarks>
    /// Used in conjunction with <see cref="ClusterHost"/>. If either is <see langword="null"/>,
    /// auto-detection is used instead.
    /// </remarks>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets the discovery polling interval in seconds.
    /// </summary>
    public int RefreshTimeInSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the list of namespaces to search for services.
    /// </summary>
    /// <remarks>
    /// Empty list searches all accessible namespaces.
    /// </remarks>
    public List<string> Namespaces { get; set; } = new List<string>();
}
