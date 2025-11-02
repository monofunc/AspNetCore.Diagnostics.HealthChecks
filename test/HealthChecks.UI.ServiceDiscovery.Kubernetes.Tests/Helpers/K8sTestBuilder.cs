using k8s.Models;

namespace HealthChecks.UI.ServiceDiscovery.Kubernetes.Tests.Helpers;

/// <summary>
/// Test data builder for creating Kubernetes V1Service and V1ServiceList objects with sensible defaults.
/// </summary>
internal static class K8sTestBuilder
{
    public static V1Service CreateService(
        string name = "test-service",
        string namespaceName = "default",
        string serviceType = ServiceType.CLUSTER_IP,
        string? clusterIP = "10.0.0.1",
        int port = 80,
        int? nodePort = null,
        string? portName = null,
        Dictionary<string, string>? labels = null,
        Dictionary<string, string>? annotations = null,
        string? externalName = null,
        string? loadBalancerIP = null,
        string? loadBalancerHostname = null)
    {
        var service = new V1Service
        {
            Metadata = new V1ObjectMeta
            {
                Name = name,
                NamespaceProperty = namespaceName,
                Uid = Guid.NewGuid().ToString(),
                Labels = labels ?? new Dictionary<string, string>(),
                Annotations = annotations ?? new Dictionary<string, string>()
            },
            Spec = new V1ServiceSpec
            {
                Type = serviceType,
                ClusterIP = clusterIP,
                ExternalName = externalName,
                Selector = new Dictionary<string, string> { ["app"] = name },
                Ports = new List<V1ServicePort>
                {
                    new()
                    {
                        Port = port,
                        TargetPort = port,
                        Name = portName ?? "http",
                        NodePort = nodePort
                    }
                }
            },
            Status = new V1ServiceStatus()
        };

        // Add LoadBalancer ingress if specified
        if (serviceType == ServiceType.LOAD_BALANCER && (loadBalancerIP != null || loadBalancerHostname != null))
        {
            service.Status.LoadBalancer = new V1LoadBalancerStatus
            {
                Ingress = new List<V1LoadBalancerIngress>
                {
                    new()
                    {
                        Ip = loadBalancerIP,
                        Hostname = loadBalancerHostname
                    }
                }
            };
        }

        return service;
    }

    public static V1Service CreateClusterIPService(
        string name = "cluster-ip-service",
        string clusterIP = "10.96.0.1",
        int port = 80,
        Dictionary<string, string>? labels = null,
        Dictionary<string, string>? annotations = null)
    {
        return CreateService(
            name: name,
            serviceType: ServiceType.CLUSTER_IP,
            clusterIP: clusterIP,
            port: port,
            labels: labels,
            annotations: annotations);
    }

    public static V1Service CreateLoadBalancerServiceWithIP(
        string name = "loadbalancer-service",
        string loadBalancerIP = "203.0.113.10",
        int port = 80,
        Dictionary<string, string>? labels = null,
        Dictionary<string, string>? annotations = null)
    {
        return CreateService(
            name: name,
            serviceType: ServiceType.LOAD_BALANCER,
            clusterIP: "10.96.0.10",
            port: port,
            labels: labels,
            annotations: annotations,
            loadBalancerIP: loadBalancerIP);
    }

    public static V1Service CreateLoadBalancerServiceWithHostname(
        string name = "loadbalancer-service",
        string loadBalancerHostname = "lb.example.com",
        int port = 80,
        Dictionary<string, string>? labels = null,
        Dictionary<string, string>? annotations = null)
    {
        return CreateService(
            name: name,
            serviceType: ServiceType.LOAD_BALANCER,
            clusterIP: "10.96.0.10",
            port: port,
            labels: labels,
            annotations: annotations,
            loadBalancerHostname: loadBalancerHostname);
    }

    public static V1Service CreateNodePortService(
        string name = "nodeport-service",
        int port = 80,
        int nodePort = 30080,
        Dictionary<string, string>? labels = null,
        Dictionary<string, string>? annotations = null)
    {
        return CreateService(
            name: name,
            serviceType: ServiceType.NODE_PORT,
            clusterIP: "10.96.0.20",
            port: port,
            nodePort: nodePort,
            labels: labels,
            annotations: annotations);
    }

    public static V1Service CreateExternalNameService(
        string name = "external-service",
        string externalName = "example.com",
        Dictionary<string, string>? labels = null,
        Dictionary<string, string>? annotations = null)
    {
        return CreateService(
            name: name,
            serviceType: ServiceType.EXTERNAL_NAME,
            clusterIP: null,
            port: 443,
            labels: labels,
            annotations: annotations,
            externalName: externalName);
    }

    public static V1Service CreateServiceWithMultiplePorts(
        string name = "multi-port-service",
        params (int port, string name, int? nodePort)[] ports)
    {
        var service = CreateService(name: name);
        service.Spec.Ports = ports.Select(p => new V1ServicePort
        {
            Port = p.port,
            TargetPort = p.port,
            Name = p.name,
            NodePort = p.nodePort
        }).ToList();

        return service;
    }

    public static V1Service CreateIPv6Service(
        string name = "ipv6-service",
        string ipv6Address = "2001:db8:85a3::8a2e:370:7334",
        int port = 80,
        Dictionary<string, string>? labels = null,
        Dictionary<string, string>? annotations = null)
    {
        return CreateService(
            name: name,
            serviceType: ServiceType.CLUSTER_IP,
            clusterIP: ipv6Address,
            port: port,
            labels: labels,
            annotations: annotations);
    }
}
