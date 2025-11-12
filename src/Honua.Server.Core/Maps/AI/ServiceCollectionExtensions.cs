// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Maps.AI;

/// <summary>
/// Extension methods for registering AI Map Generation services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers AI-powered map generation services (optional)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration containing OpenAI settings</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddMapGenerationAi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Read OpenAI/Map AI configuration from appsettings
        // Can use same OpenAI config or separate MapAI section
        var config = new MapAiConfiguration();

        // Try MapAI section first, fallback to OpenAI section
        var mapAiSection = configuration.GetSection("MapAI");
        if (mapAiSection.Exists())
        {
            mapAiSection.Bind(config);
        }
        else
        {
            configuration.GetSection("OpenAI").Bind(config);
        }

        // Only register if API key is configured
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            services.AddSingleton(config);
            services.AddHttpClient<IMapGenerationAiService, OpenAiMapGenerationService>();

            // Also register as singleton without HttpClient for DI
            services.AddSingleton<IMapGenerationAiService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(OpenAiMapGenerationService));
                var logger = sp.GetRequiredService<ILogger<OpenAiMapGenerationService>>();
                return new OpenAiMapGenerationService(httpClient, config, logger);
            });
        }
        else
        {
            // Register null service when not configured (graceful degradation)
            services.AddSingleton<IMapGenerationAiService?>(sp => null);
        }

        return services;
    }

    /// <summary>
    /// Registers AI services with custom configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="config">Map AI configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddMapGenerationAi(
        this IServiceCollection services,
        MapAiConfiguration config)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            services.AddSingleton<IMapGenerationAiService?>(sp => null);
            return services;
        }

        services.AddSingleton(config);
        services.AddHttpClient<IMapGenerationAiService, OpenAiMapGenerationService>();

        services.AddSingleton<IMapGenerationAiService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(OpenAiMapGenerationService));
            var logger = sp.GetRequiredService<ILogger<OpenAiMapGenerationService>>();
            return new OpenAiMapGenerationService(httpClient, config, logger);
        });

        return services;
    }
}
