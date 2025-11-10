// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Cloud.EventGrid.Configuration;
using Honua.Server.Core.Cloud.EventGrid.Hooks;
using Honua.Server.Core.Cloud.EventGrid.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Cloud.EventGrid.DependencyInjection;

/// <summary>
/// Extension methods for adding Azure Event Grid services to the DI container.
/// </summary>
public static class EventGridServiceCollectionExtensions
{
    /// <summary>
    /// Add Azure Event Grid publisher services to the application.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddEventGridPublisher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<EventGridOptions>(configuration.GetSection(EventGridOptions.SectionName));

        // Validate configuration on startup
        services.AddSingleton<IValidateOptions<EventGridOptions>, EventGridOptionsValidator>();

        // Register core publisher as singleton (it manages its own internal state)
        services.AddSingleton<IEventGridPublisher, EventGridPublisher>();

        // Register background service for batch flushing
        services.AddHostedService<EventGridBackgroundPublisher>();

        // Register event publishers
        services.AddSingleton<IFeatureEventPublisher, FeatureEventPublisher>();
        services.AddSingleton<ISensorThingsEventPublisher, SensorThingsEventPublisher>();
        services.AddSingleton<IGeoEventPublisher, GeoEventPublisher>();

        return services;
    }

    /// <summary>
    /// Add Azure Event Grid publisher services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddEventGridPublisher(
        this IServiceCollection services,
        Action<EventGridOptions> configureOptions)
    {
        // Register and configure options
        services.Configure(configureOptions);

        // Validate configuration on startup
        services.AddSingleton<IValidateOptions<EventGridOptions>, EventGridOptionsValidator>();

        // Register core publisher as singleton
        services.AddSingleton<IEventGridPublisher, EventGridPublisher>();

        // Register background service
        services.AddHostedService<EventGridBackgroundPublisher>();

        // Register event publishers
        services.AddSingleton<IFeatureEventPublisher, FeatureEventPublisher>();
        services.AddSingleton<ISensorThingsEventPublisher, SensorThingsEventPublisher>();
        services.AddSingleton<IGeoEventPublisher, GeoEventPublisher>();

        return services;
    }
}

/// <summary>
/// Validates EventGridOptions on startup.
/// </summary>
internal class EventGridOptionsValidator : IValidateOptions<EventGridOptions>
{
    public ValidateOptionsResult Validate(string? name, EventGridOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
