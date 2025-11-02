using HealthChecks.UI;
using HealthChecks.UI.Configuration;
using HealthChecks.UI.Core;
using HealthChecks.UI.Core.HostedService;
using HealthChecks.UI.Core.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static HealthChecksUIBuilder AddHealthChecksUI(this IServiceCollection services,
        Action<Settings>? setupSettings = null)
    {
        services
            .AddOptions<Settings>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.BindUISettings(settings);
                setupSettings?.Invoke(settings);
            });

        services.TryAddSingleton<ServerAddressesService>();
        services.TryAddScoped<IHealthCheckFailureNotifier, WebHookFailureNotifier>();
        services.TryAddScoped<IHealthCheckReportCollector, HealthCheckReportCollector>();

        services
            .AddHostedService<UIInitializationHostedService>()
            .AddHostedService<HealthCheckCollectorHostedService>()
            .AddApiEndpointHttpClient()
            .AddWebhooksEndpointHttpClient();

        return new HealthChecksUIBuilder(services);
    }

    public static IServiceCollection AddApiEndpointHttpClient(this IServiceCollection services)
    {
        return services.AddHttpClient(Keys.HEALTH_CHECK_HTTP_CLIENT_NAME, (sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<Settings>>();
                settings.Value.ApiEndpointHttpClientConfig?.Invoke(sp, client);
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<Settings>>();
                return settings.Value.ApiEndpointHttpHandler?.Invoke(sp) ?? new HttpClientHandler();
            })
            .ConfigureAdditionalHttpMessageHandlers((handlerList, serviceProvider) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<Settings>>();

                foreach (var handlerType in settings.Value.ApiEndpointDelegatingHandlerTypes.Values)
                {
                    handlerList.Add((DelegatingHandler)serviceProvider.GetRequiredService(handlerType));
                }
            })
            .Services;
    }

    public static IServiceCollection AddWebhooksEndpointHttpClient(this IServiceCollection services)
    {
        return services.AddHttpClient(Keys.HEALTH_CHECK_WEBHOOK_HTTP_CLIENT_NAME, (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<Settings>>();
            settings.Value.WebHooksEndpointHttpClientConfig?.Invoke(sp, client);
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
         {
             var settings = sp.GetRequiredService<IOptions<Settings>>();
             return settings.Value.WebHooksEndpointHttpHandler?.Invoke(sp) ?? new HttpClientHandler();
         })
        .ConfigureAdditionalHttpMessageHandlers((handlersList, serviceProvider) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<Settings>>();

            foreach (var handlerType in settings.Value.WebHooksEndpointDelegatingHandlerTypes.Values)
            {
                handlersList.Add((DelegatingHandler)serviceProvider.GetRequiredService(handlerType));
            }
        })
        .Services;
    }
}
