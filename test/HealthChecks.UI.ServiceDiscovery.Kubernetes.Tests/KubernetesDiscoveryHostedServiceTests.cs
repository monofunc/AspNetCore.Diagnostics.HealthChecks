using System.Net;
using HealthChecks.UI.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthChecks.UI.ServiceDiscovery.Kubernetes.Tests;

public class kubernetes_discovery_hosted_service_should
{
    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.BadGateway, false)]
    [InlineData(HttpStatusCode.GatewayTimeout, false)]
    [InlineData(HttpStatusCode.Created, false)]
    [InlineData(HttpStatusCode.NoContent, false)]
    public void validate_healthcheck_status_codes_correctly(HttpStatusCode code, bool expectedResult)
    {
        // Act
        var result = KubernetesDiscoveryHostedService.IsValidHealthChecksStatusCode(code);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public void detect_when_service_already_registered_in_database()
    {
        // Arrange
        using var context = CreateInMemoryDatabase();

        context.Configurations.Add(new HealthCheckConfiguration
        {
            Name = "existing-service",
            Uri = "http://existing.service.com/health"
        });

        context.SaveChanges();

        // Act
        var result = KubernetesDiscoveryHostedService.IsLivenessRegistered(
            context,
            "http://existing.service.com/health");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void return_false_when_service_not_in_database()
    {
        // Arrange
        using var dbContext = CreateInMemoryDatabase();
        dbContext.Configurations.Add(new HealthCheckConfiguration
        {
            Name = "other-service",
            Uri = "http://other.service.com/health"
        });
        dbContext.SaveChanges();

        // Act
        var result = KubernetesDiscoveryHostedService.IsLivenessRegistered(
            dbContext,
            "http://new.service.com/health");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void match_exact_uri_for_registration_check()
    {
        // Arrange
        using var dbContext = CreateInMemoryDatabase();

        dbContext.Configurations.Add(new HealthCheckConfiguration
        {
            Name = "service",
            Uri = "http://service.com/health"
        });

        dbContext.SaveChanges();

        // Act - Different path
        var result1 = KubernetesDiscoveryHostedService.IsLivenessRegistered(
            dbContext,
            "http://service.com/healthz");

        // Act - Different scheme
        var result2 = KubernetesDiscoveryHostedService.IsLivenessRegistered(
            dbContext,
            "https://service.com/health");

        // Act - Different port
        var result3 = KubernetesDiscoveryHostedService.IsLivenessRegistered(
            dbContext,
            "http://service.com:8080/health");

        // Assert
        result1.ShouldBeFalse();
        result2.ShouldBeFalse();
        result3.ShouldBeFalse();
    }

    [Fact]
    public void return_false_when_database_is_empty()
    {
        // Arrange
        using var dbContext = CreateInMemoryDatabase();

        // Act
        var result = KubernetesDiscoveryHostedService.IsLivenessRegistered(
            dbContext,
            "http://service.com/health");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void handle_multiple_configurations_correctly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDatabase();

        dbContext.Configurations.AddRange(
            new HealthCheckConfiguration { Name = "service1", Uri = "http://service1.com/health" },
            new HealthCheckConfiguration { Name = "service2", Uri = "http://service2.com/health" },
            new HealthCheckConfiguration { Name = "service3", Uri = "http://service3.com/health" });

        dbContext.SaveChanges();

        // Act
        var exists = KubernetesDiscoveryHostedService.IsLivenessRegistered(
            dbContext,
            "http://service2.com/health");

        var notExists = KubernetesDiscoveryHostedService.IsLivenessRegistered(
            dbContext,
            "http://service4.com/health");

        // Assert
        exists.ShouldBeTrue();
        notExists.ShouldBeFalse();
    }

    private static HealthChecksDb CreateInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<HealthChecksDb>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HealthChecksDb(options);
    }
}
