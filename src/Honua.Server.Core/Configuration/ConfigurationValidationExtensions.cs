// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Caching;
using Honua.Server.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Extension methods for registering configuration validators.
/// </summary>
public static class ConfigurationValidationExtensions
{
    /// <summary>
    /// Adds all configuration validators and validation hosted service.
    /// This ensures configuration is validated at startup and the application fails fast if invalid.
    /// </summary>
    public static IServiceCollection AddConfigurationValidation(this IServiceCollection services)
    {
        // Register validators for each configuration section
        services.AddSingleton<IValidateOptions<HonuaConfiguration>, HonuaConfigurationValidator>();
        services.AddSingleton<IValidateOptions<HonuaConfiguration>, SecurityConfigurationOptionsValidator>();
        services.AddSingleton<IValidateOptions<HonuaAuthenticationOptions>, HonuaAuthenticationOptionsValidator>();
        services.AddSingleton<IValidateOptions<OpenRosaOptions>, OpenRosaOptionsValidator>();
        services.AddSingleton<IValidateOptions<ConnectionStringOptions>, ConnectionStringOptionsValidator>();

        // Register validators for data and caching options
        services.AddSingleton<IValidateOptions<GraphDatabaseOptions>, GraphDatabaseOptionsValidator>();
        services.AddSingleton<IValidateOptions<CacheInvalidationOptions>, CacheInvalidationOptionsValidator>();
        services.AddSingleton<IValidateOptions<CacheSizeLimitOptions>, CacheSizeLimitOptionsValidator>();
        services.AddSingleton<IValidateOptions<DataIngestionOptions>, DataIngestionOptionsValidator>();
        services.AddSingleton<IValidateOptions<DataAccessOptions>, DataAccessOptionsValidator>();

        // Register the hosted service that validates on startup
        services.AddHostedService<ConfigurationValidationHostedService>();

        return services;
    }
}
