using System.Text.Json;
using HealthChecks.UI.ServiceDiscovery.Kubernetes.Tests.Helpers;
using k8s.Models;

namespace HealthChecks.UI.ServiceDiscovery.Kubernetes.Tests;

public class kubernetes_address_factory_should
{
    private readonly KubernetesDiscoverySettings _settings = new()
    {
        HealthPath = "healthz",
        ServicesPathAnnotation = KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PATH_ANNOTATION,
        ServicesPortAnnotation = KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PORT_ANNOTATION,
        ServicesSchemeAnnotation = KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_SCHEME_ANNOTATION
    };

    [Fact]
    public void parse_properly_the_k8s_api_discovered_services_for_a_local_cluster()
    {
        var apiResponse = File.ReadAllText("SampleData/local-cluster-discovery-sample.json");

        var services = JsonSerializer.Deserialize<V1ServiceList>(apiResponse);

        var addressFactory = new KubernetesAddressFactory(_settings);

        IReadOnlyList<string> serviceAddresses = services!.Items
            .Select(service => addressFactory.CreateAddress(service))
            .ToList();

        serviceAddresses[0].ShouldBe("http://localhost:10000/healthz");
        serviceAddresses[1].ShouldBe("http://localhost:9000/healthz");
        serviceAddresses[2].ShouldBe("http://localhost:30000/healthz");
        serviceAddresses[3].ShouldBe("http://10.97.1.153:80/healthz");
        serviceAddresses[4].ShouldBe("http://[2001:0db8:85a3:0000:0000:8a2e:0370:7334]:7070/custom/health/path");
        serviceAddresses[5].ShouldBe("https://some.external-site.com:443/custom/health/path");
    }

    [Fact]
    public void parse_properly_the_k8s_api_discovered_services_for_a_remote_cluster()
    {
        var apiResponse = File.ReadAllText("SampleData/remote-cluster-discovery-sample.json");

        var services = JsonSerializer.Deserialize<V1ServiceList>(apiResponse);

        var addressFactory = new KubernetesAddressFactory(_settings);

        IReadOnlyList<string> serviceAddresses = services!.Items
            .Select(service => addressFactory.CreateAddress(service))
            .ToList();

        serviceAddresses[0].ShouldBe("http://13.73.139.23:80/healthz");
        serviceAddresses[1].ShouldBe("http://13.80.181.10:51000/healthz");
        serviceAddresses[2].ShouldBe("http://12.0.0.190:5672/healthz");
        serviceAddresses[3].ShouldBe("http://12.0.0.168:30478/healthz");
        serviceAddresses[4].ShouldBe("https://10.152.183.35:8080/custom/health/path");
    }

    [Fact]
    public void create_clusterip_service_address_with_default_port()
    {
        var service = K8sTestBuilder.CreateClusterIPService(
            name: "api-service",
            clusterIP: "10.96.0.5",
            port: 8080);

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.5:8080/healthz");
    }

    [Fact]
    public void create_clusterip_service_address_without_port()
    {
        var service = K8sTestBuilder.CreateClusterIPService(
            name: "api-service",
            clusterIP: "10.96.0.5",
            port: 80);

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.5:80/healthz");
    }

    [Fact]
    public void create_loadbalancer_service_address_with_ingress_hostname()
    {
        var service = K8sTestBuilder.CreateLoadBalancerServiceWithHostname(
            name: "web-service",
            loadBalancerHostname: "lb.example.com",
            port: 443);

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://lb.example.com:443/healthz");
    }

    [Fact]
    public void create_loadbalancer_service_address_with_ingress_ip()
    {
        var service = K8sTestBuilder.CreateLoadBalancerServiceWithIP(
            name: "web-service",
            loadBalancerIP: "203.0.113.42",
            port: 80);

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://203.0.113.42:80/healthz");
    }

    [Fact]
    public void fallback_to_clusterip_when_loadbalancer_has_no_ingress()
    {
        var service = K8sTestBuilder.CreateService(
            name: "web-service",
            serviceType: ServiceType.LOAD_BALANCER,
            clusterIP: "10.96.0.15",
            port: 80);

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.15:80/healthz");
    }

    [Fact]
    public void create_nodeport_service_address_with_nodeport()
    {
        var service = K8sTestBuilder.CreateNodePortService(
            name: "api-service",
            port: 80,
            nodePort: 30080);

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.20:30080/healthz");
    }

    [Fact]
    public void create_externalname_service_address()
    {
        var service = K8sTestBuilder.CreateExternalNameService(
            name: "external-api",
            externalName: "api.external.com");

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://api.external.com/healthz");
    }

    [Fact]
    public void create_externalname_service_address_with_port_annotation()
    {
        var service = K8sTestBuilder.CreateExternalNameService(
            name: "external-api",
            externalName: "api.external.com",
            annotations: new Dictionary<string, string>
            {
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PORT_ANNOTATION] = "8443"
            });

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://api.external.com:8443/healthz");
    }

    [Fact]
    public void use_custom_path_from_annotation()
    {
        var service = K8sTestBuilder.CreateClusterIPService(
            name: "api-service",
            clusterIP: "10.96.0.5",
            port: 80,
            annotations: new Dictionary<string, string>
            {
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PATH_ANNOTATION] = "custom/health/check"
            });

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.5:80/custom/health/check");
    }

    [Fact]
    public void use_custom_path_from_annotation_with_leading_slash()
    {
        var service = K8sTestBuilder.CreateClusterIPService(
            name: "api-service",
            clusterIP: "10.96.0.5",
            port: 80,
            annotations: new Dictionary<string, string>
            {
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PATH_ANNOTATION] = "/health"
            });

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.5:80/health");
    }

    [Fact]
    public void use_custom_port_from_annotation_as_number()
    {
        var service = K8sTestBuilder.CreateServiceWithMultiplePorts(
            name: "api-service",
            (80, "http", null),
            (8080, "http-alt", null),
            (9090, "metrics", null));

        service.Spec.ClusterIP = "10.96.0.5";
        service.Metadata.Annotations = new Dictionary<string, string>
        {
            [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PORT_ANNOTATION] = "8080"
        };

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.5:8080/healthz");
    }

    [Fact]
    public void use_custom_port_from_annotation_as_name()
    {
        var service = K8sTestBuilder.CreateServiceWithMultiplePorts(
            name: "api-service",
            (80, "http", null),
            (8080, "healthcheck", null),
            (9090, "metrics", null));

        service.Spec.ClusterIP = "10.96.0.5";
        service.Metadata.Annotations = new Dictionary<string, string>
        {
            [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PORT_ANNOTATION] = "healthcheck"
        };

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.5:8080/healthz");
    }

    [Fact]
    public void use_custom_scheme_from_annotation()
    {
        var service = K8sTestBuilder.CreateClusterIPService(
            name: "api-service",
            clusterIP: "10.96.0.5",
            port: 443,
            annotations: new Dictionary<string, string>
            {
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_SCHEME_ANNOTATION] = "https"
            });

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("https://10.96.0.5:443/healthz");
    }

    [Fact]
    public void use_custom_scheme_from_annotation_case_insensitive()
    {
        var service = K8sTestBuilder.CreateClusterIPService(
            name: "api-service",
            clusterIP: "10.96.0.5",
            port: 443,
            annotations: new Dictionary<string, string>
            {
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_SCHEME_ANNOTATION] = "HTTPS"
            });

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("https://10.96.0.5:443/healthz");
    }

    [Fact]
    public void combine_all_custom_annotations()
    {
        var service = K8sTestBuilder.CreateClusterIPService(
            name: "api-service",
            clusterIP: "10.96.0.5",
            port: 8443,
            annotations: new Dictionary<string, string>
            {
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PATH_ANNOTATION] = "api/health",
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PORT_ANNOTATION] = "8443",
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_SCHEME_ANNOTATION] = "https"
            });

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("https://10.96.0.5:8443/api/health");
    }

    [Fact]
    public void wrap_ipv6_address_in_brackets()
    {
        var service = K8sTestBuilder.CreateIPv6Service(
            name: "ipv6-service",
            ipv6Address: "2001:db8:85a3::8a2e:370:7334",
            port: 8080);

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://[2001:db8:85a3::8a2e:370:7334]:8080/healthz");
    }

    [Fact]
    public void wrap_ipv6_address_in_brackets_with_annotations()
    {
        var service = K8sTestBuilder.CreateIPv6Service(
            name: "ipv6-service",
            ipv6Address: "2001:db8::1",
            port: 443,
            annotations: new Dictionary<string, string>
            {
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_SCHEME_ANNOTATION] = "https",
                [KubernetesDiscoveryConstants.HEALTHCHECKS_DEFAULT_DISCOVERY_PATH_ANNOTATION] = "health/ready"
            });

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("https://[2001:db8::1]:443/health/ready");
    }

    [Fact]
    public void handle_service_with_no_annotations()
    {
        var service = K8sTestBuilder.CreateClusterIPService(
            name: "simple-service",
            clusterIP: "10.96.0.1",
            port: 80);

        service.Metadata.Annotations = null;

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.1:80/healthz");
    }

    [Fact]
    public void handle_service_with_empty_annotations()
    {
        var service = K8sTestBuilder.CreateClusterIPService(
            name: "simple-service",
            clusterIP: "10.96.0.1",
            port: 80,
            annotations: new Dictionary<string, string>());

        var factory = new KubernetesAddressFactory(_settings);

        var address = factory.CreateAddress(service);

        address.ShouldBe("http://10.96.0.1:80/healthz");
    }
}
