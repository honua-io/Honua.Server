// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.ErrorHandling;
using Honua.Integration.Azure.Health;
using Honua.Integration.Azure.Mapping;
using Honua.Integration.Azure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Integration.Azure.Extensions;

/// <summary>
/// Extension methods for registering Azure IoT Hub integration services
/// </summary>
public static class AzureIoTHubServiceExtensions
{
    /// <summary>
    /// Add Azure IoT Hub integration services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAzureIoTHubIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<AzureIoTHubOptions>(
            configuration.GetSection(AzureIoTHubOptions.SectionName));

        // Register core services
        services.AddSingleton<IIoTHubMessageParser, IoTHubMessageParser>();
        services.AddSingleton<IDeviceMappingService, DeviceMappingService>();
        services.AddScoped<ISensorThingsMapper, SensorThingsMapper>();

        // Register error handling
        services.AddSingleton<IDeadLetterQueueService, InMemoryDeadLetterQueueService>();

        // Register background consumer service
        services.AddHostedService<AzureIoTHubConsumerService>();

        // Register health check
        services.AddHealthChecks()
            .AddCheck<AzureIoTHubHealthCheck>(
                "azure_iot_hub",
                tags: new[] { "iot", "azure", "integration" });

        return services;
    }

    /// <summary>
    /// Add Azure IoT Hub integration with custom configuration action
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAzureIoTHubIntegration(
        this IServiceCollection services,
        Action<AzureIoTHubOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddSingleton<IIoTHubMessageParser, IoTHubMessageParser>();
        services.AddSingleton<IDeviceMappingService, DeviceMappingService>();
        services.AddScoped<ISensorThingsMapper, SensorThingsMapper>();
        services.AddSingleton<IDeadLetterQueueService, InMemoryDeadLetterQueueService>();
        services.AddHostedService<AzureIoTHubConsumerService>();

        services.AddHealthChecks()
            .AddCheck<AzureIoTHubHealthCheck>(
                "azure_iot_hub",
                tags: new[] { "iot", "azure", "integration" });

        return services;
    }
}
