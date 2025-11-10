// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Honua.Server.Enterprise.BIConnectors.PowerBI.Configuration;
using Honua.Server.Enterprise.BIConnectors.PowerBI.Services;

namespace Honua.Server.Enterprise.BIConnectors.PowerBI.Extensions;

/// <summary>
/// Extension methods for registering Power BI services
/// </summary>
public static class PowerBIServiceCollectionExtensions
{
    /// <summary>
    /// Adds Power BI integration to the service collection
    /// </summary>
    public static IServiceCollection AddPowerBIIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        var powerBIOptions = configuration.GetSection(PowerBIOptions.SectionName).Get<PowerBIOptions>()
            ?? new PowerBIOptions();
        services.AddSingleton(powerBIOptions);

        // Register Power BI services for server-side push
        // Note: OData feeds are already available through Honua.Server.Host at /odata/{collection}
        // This package adds Push Datasets and REST API integration only
        services.AddSingleton<IPowerBIDatasetService, PowerBIDatasetService>();
        services.AddSingleton<IPowerBIStreamingService, PowerBIStreamingService>();

        // Add HTTP client with retry policy
        services.AddHttpClient("PowerBI")
            .AddPolicyHandler(GetRetryPolicy());

        return services;
    }

    /// <summary>
    /// Creates a retry policy for HTTP requests to Power BI API
    /// </summary>
    private static Polly.IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return Polly.Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                          r.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    // Log retry attempt
                });
    }
}
