// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Honua.Integration.PowerBI.Configuration;
using Honua.Integration.PowerBI.Models;
using Honua.Integration.PowerBI.Services;

namespace Honua.Integration.PowerBI.Extensions;

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

        // Register services
        services.AddSingleton<IPowerBIDatasetService, PowerBIDatasetService>();
        services.AddSingleton<IPowerBIStreamingService, PowerBIStreamingService>();

        // Add OData support for Features API
        if (powerBIOptions.EnableODataFeeds)
        {
            services.AddControllers().AddOData(options =>
            {
                options.Select().Filter().OrderBy().Expand().Count().SetMaxTop(powerBIOptions.MaxODataPageSize);
                options.AddRouteComponents("odata/features", GetEdmModel());
                options.TimeZone = TimeZoneInfo.Utc;
            });
        }

        // Add HTTP client with retry policy
        services.AddHttpClient("PowerBI")
            .AddPolicyHandler(GetRetryPolicy());

        return services;
    }

    /// <summary>
    /// Builds the EDM model for OData endpoints
    /// </summary>
    private static IEdmModel GetEdmModel()
    {
        var builder = new ODataConventionModelBuilder();

        // Define entity sets
        var featuresSet = builder.EntitySet<PowerBIFeature>("Features");
        featuresSet.EntityType.HasKey(f => f.Id);

        // Configure navigation properties and actions if needed

        return builder.GetEdmModel();
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
