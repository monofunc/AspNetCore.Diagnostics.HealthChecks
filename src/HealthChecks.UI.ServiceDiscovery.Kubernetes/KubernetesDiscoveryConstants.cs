namespace HealthChecks.UI.ServiceDiscovery.Kubernetes;

/// <summary>
/// Constants used for Kubernetes service discovery configuration.
/// </summary>
public static class KubernetesDiscoveryConstants
{
    /// <summary>
    /// Default health check path used when services don't specify a custom path annotation.
    /// </summary>
    public const string HEALTHCHECKS_DEFAULT_PATH = "hc";

    /// <summary>
    /// Default Kubernetes label used to identify services that expose health checks.
    /// </summary>
    public const string HEALTHCHECKS_DEFAULT_DISCOVERY_LABEL = "HealthChecks";

    /// <summary>
    /// Default annotation name for overriding the health check path on a per-service basis.
    /// </summary>
    public const string HEALTHCHECKS_DEFAULT_DISCOVERY_PATH_ANNOTATION = "HealthChecksPath";

    /// <summary>
    /// Default annotation name for overriding the health check port on a per-service basis.
    /// </summary>
    public const string HEALTHCHECKS_DEFAULT_DISCOVERY_PORT_ANNOTATION = "HealthChecksPort";

    /// <summary>
    /// Default annotation name for overriding the health check scheme (http/https) on a per-service basis.
    /// </summary>
    public const string HEALTHCHECKS_DEFAULT_DISCOVERY_SCHEME_ANNOTATION = "HealthChecksScheme";

    /// <summary>
    /// Named HttpClient used for calling discovered Kubernetes services' health check endpoints.
    /// </summary>
    public const string K8S_CLUSTER_SERVICE_HTTP_CLIENT_NAME = "k8s-cluster-service";
}
