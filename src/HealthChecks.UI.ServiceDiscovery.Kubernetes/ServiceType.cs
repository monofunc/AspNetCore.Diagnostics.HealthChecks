namespace HealthChecks.UI.ServiceDiscovery.Kubernetes;

/// <summary>
/// Kubernetes service type constants.
/// </summary>
internal static class ServiceType
{
    /// <summary>
    /// LoadBalancer service type exposes services externally using a cloud provider's load balancer.
    /// </summary>
    public const string LOAD_BALANCER = "LoadBalancer";

    /// <summary>
    /// NodePort service type exposes services on each node's IP at a static port.
    /// </summary>
    public const string NODE_PORT = "NodePort";

    /// <summary>
    /// ClusterIP service type exposes services on a cluster-internal IP (default type).
    /// </summary>
    public const string CLUSTER_IP = "ClusterIP";

    /// <summary>
    /// ExternalName service type maps services to external DNS names.
    /// </summary>
    public const string EXTERNAL_NAME = "ExternalName";
}
