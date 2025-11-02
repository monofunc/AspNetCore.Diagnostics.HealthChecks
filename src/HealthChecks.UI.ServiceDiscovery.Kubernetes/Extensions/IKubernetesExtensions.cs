using k8s;
using k8s.Models;

namespace HealthChecks.UI.ServiceDiscovery.Kubernetes.Extensions;

/// <summary>
/// Extension methods for Kubernetes client operations.
/// </summary>
internal static class IKubernetesExtensions
{
    /// <summary>
    /// Retrieves all services matching the specified label selector, optionally filtered by namespaces.
    /// </summary>
    /// <param name="client">The Kubernetes client.</param>
    /// <param name="label">The label selector to filter services (e.g., <c>"HealthChecks"</c> for label existence, or <c>"HealthChecks=true"</c> for specific value).</param>
    /// <param name="k8sNamespaces">Optional list of namespaces to search. If <see langword="null"/> or empty, searches all namespaces.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <returns>A task containing the aggregated list of matching services.</returns>
    /// <remarks>
    /// When namespaces are specified, queries each namespace concurrently and aggregates results.
    /// When no namespaces are specified, performs a single cluster-wide query.
    /// </remarks>
    internal static async Task<V1ServiceList> GetServicesAsync(this IKubernetes client, string label, List<string> k8sNamespaces, CancellationToken cancellationToken)
    {
        if (k8sNamespaces is null || k8sNamespaces.Count == 0)
        {
            return await client.CoreV1.ListServiceForAllNamespacesAsync(labelSelector: label, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var responses = await Task.WhenAll(k8sNamespaces.Select(k8sNamespace => client.CoreV1.ListNamespacedServiceAsync(k8sNamespace, labelSelector: label, cancellationToken: cancellationToken))).ConfigureAwait(false);

            return new V1ServiceList()
            {
                Items = responses.SelectMany(r => r.Items).ToList()
            };
        }
    }
}
