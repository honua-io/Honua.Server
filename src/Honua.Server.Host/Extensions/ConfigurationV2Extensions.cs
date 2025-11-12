// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for integrating Configuration V2 (.honua files) into the application.
/// </summary>
public static class ConfigurationV2Extensions
{
    /// <summary>
    /// Loads and registers Configuration V2 from a .honua file if present.
    /// Services are registered through the plugin system - each service must have a corresponding plugin.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaConfigurationV2(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var logger = CreateLogger();

        // Check for Configuration V2 path in environment variables or configuration
        var configPath = Environment.GetEnvironmentVariable("HONUA_CONFIG_PATH")
            ?? configuration["HONUA_CONFIG_PATH"]
            ?? configuration["Honua:ConfigPath"];

        // If no explicit path, check for common file names in order of precedence
        if (string.IsNullOrWhiteSpace(configPath))
        {
            var possiblePaths = new[]
            {
                $"honua.{environment.EnvironmentName.ToLowerInvariant()}.hcl",
                $"honua.{environment.EnvironmentName.ToLowerInvariant()}.honua",
                "honua.config.hcl",
                "honua.config.honua",
                "honua.hcl",
                "honua.honua"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    configPath = path;
                    logger.LogInformation("Found Configuration V2 file: {Path}", path);
                    break;
                }
            }
        }

        // If no configuration file found, Configuration V2 is not in use
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            logger.LogInformation(
                "No Configuration V2 file found. " +
                "To use Configuration V2, create a honua.config.hcl file or set HONUA_CONFIG_PATH.");
            return services;
        }

        try
        {
            // Load the configuration file
            logger.LogInformation("Loading Configuration V2 from: {Path}", configPath);
            var honuaConfig = HonuaConfigLoader.LoadAsync(configPath).GetAwaiter().GetResult();

            // Register the loaded configuration as a singleton
            services.AddSingleton(honuaConfig);

            // Initialize PluginLoader
            logger.LogInformation("Initializing plugin system...");
            var pluginLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PluginLoader>();
            var pluginLoader = new PluginLoader(pluginLogger, configuration, environment);

            // Load plugins asynchronously (blocking here since we're in ConfigureServices)
            logger.LogInformation("Loading plugins...");
            var pluginLoadResult = pluginLoader.LoadPluginsAsync().GetAwaiter().GetResult();

            // Log plugin loading results
            foreach (var plugin in pluginLoadResult.LoadedPlugins)
            {
                logger.LogInformation(
                    "Loaded plugin: {PluginId} v{Version} ({Type})",
                    plugin.Id,
                    plugin.Version,
                    plugin.Type);
            }

            foreach (var (pluginId, error) in pluginLoadResult.FailedPlugins)
            {
                logger.LogWarning("Failed to load plugin {PluginId}: {Error}", pluginId, error);
            }

            // Register PluginLoader as singleton for later access
            services.AddSingleton(pluginLoader);

            // Get enabled services from Configuration V2
            var enabledServices = honuaConfig.Services
                .Where(s => s.Value.Enabled)
                .Select(s => s.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            logger.LogInformation(
                "Configuration V2 has {Count} enabled services: {Services}",
                enabledServices.Count,
                string.Join(", ", enabledServices));

            // Configure services from loaded plugins
            var servicePlugins = pluginLoader.GetServicePlugins();
            var pluginServiceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var servicePlugin in servicePlugins)
            {
                try
                {
                    // Check if this service is enabled in Configuration V2
                    if (!enabledServices.Contains(servicePlugin.ServiceId))
                    {
                        logger.LogDebug(
                            "Skipping plugin {PluginId} - service '{ServiceId}' not enabled in configuration",
                            servicePlugin.Id,
                            servicePlugin.ServiceId);
                        continue;
                    }

                    // Validate plugin configuration
                    logger.LogDebug("Validating configuration for plugin: {PluginId}", servicePlugin.Id);
                    var validationResult = servicePlugin.ValidateConfiguration(configuration);

                    if (!validationResult.IsValid)
                    {
                        logger.LogError(
                            "Plugin {PluginId} configuration validation failed: {Errors}",
                            servicePlugin.Id,
                            string.Join(", ", validationResult.Errors));
                        continue;
                    }

                    if (validationResult.Warnings.Count > 0)
                    {
                        foreach (var warning in validationResult.Warnings)
                        {
                            logger.LogWarning("Plugin {PluginId} configuration warning: {Warning}",
                                servicePlugin.Id, warning);
                        }
                    }

                    // Create plugin context
                    var pluginContext = new PluginContext
                    {
                        PluginPath = string.Empty, // Will be set properly by PluginLoader
                        Configuration = configuration,
                        Environment = environment,
                        Logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger(servicePlugin.Id),
                        LoadedPlugins = pluginLoadResult.LoadedPlugins.ToDictionary(p => p.Id)
                    };

                    // Configure services for this plugin
                    logger.LogInformation(
                        "Configuring services for plugin: {PluginId} (service: {ServiceId})",
                        servicePlugin.Id,
                        servicePlugin.ServiceId);

                    servicePlugin.ConfigureServices(services, configuration, pluginContext);
                    pluginServiceIds.Add(servicePlugin.ServiceId);

                    logger.LogInformation(
                        "Successfully configured plugin: {PluginId} (service: {ServiceId})",
                        servicePlugin.Id,
                        servicePlugin.ServiceId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to configure plugin {PluginId}: {Message}",
                        servicePlugin.Id,
                        ex.Message);
                    // Continue with other plugins instead of failing startup
                }
            }

            // All service registration is now handled by plugins loaded above
            // Legacy IServiceRegistration system is no longer used

            logger.LogInformation(
                "Configuration V2 loaded successfully: {DataSources} data sources, {Services} services, {Layers} layers, {Plugins} plugins",
                honuaConfig.DataSources.Count,
                honuaConfig.Services.Count(s => s.Value.Enabled),
                honuaConfig.Layers.Count,
                pluginServiceIds.Count);

            return services;
        }
        catch (Exception ex)
        {
            // Log error but don't fail startup
            logger.LogError(ex, "Failed to load Configuration V2 from {Path}.", configPath);
            return services;
        }
    }

    /// <summary>
    /// Maps Configuration V2 service endpoints if Configuration V2 is active.
    /// All service endpoints are registered through plugins.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication MapHonuaConfigurationV2Endpoints(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var honuaConfig = app.Services.GetService<HonuaConfig>();

        if (honuaConfig == null)
        {
            // Configuration V2 not in use
            return app;
        }

        logger.LogInformation("Mapping Configuration V2 endpoints...");

        try
        {
            // Get PluginLoader if available
            var pluginLoader = app.Services.GetService<PluginLoader>();
            var pluginServiceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (pluginLoader != null)
            {
                // Map endpoints for loaded service plugins
                var servicePlugins = pluginLoader.GetServicePlugins();

                logger.LogInformation(
                    "Mapping endpoints for {Count} loaded service plugins",
                    servicePlugins.Count);

                foreach (var servicePlugin in servicePlugins)
                {
                    try
                    {
                        // Check if this service is enabled in Configuration V2
                        if (!honuaConfig.Services.TryGetValue(servicePlugin.ServiceId, out var serviceConfig)
                            || !serviceConfig.Enabled)
                        {
                            logger.LogDebug(
                                "Skipping endpoint mapping for plugin {PluginId} - service '{ServiceId}' not enabled",
                                servicePlugin.Id,
                                servicePlugin.ServiceId);
                            continue;
                        }

                        // Create plugin context with ServiceProvider
                        var pluginContext = new PluginContext
                        {
                            PluginPath = string.Empty,
                            Configuration = app.Configuration,
                            Environment = app.Environment,
                            Logger = app.Services.GetRequiredService<ILoggerFactory>()
                                .CreateLogger(servicePlugin.Id),
                            ServiceProvider = app.Services,
                            LoadedPlugins = pluginLoader.GetAllPlugins()
                                .Select(p => p.Metadata)
                                .ToDictionary(m => m.Id)
                        };

                        logger.LogInformation(
                            "Mapping endpoints for plugin: {PluginId} (service: {ServiceId})",
                            servicePlugin.Id,
                            servicePlugin.ServiceId);

                        servicePlugin.MapEndpoints(app, pluginContext);
                        pluginServiceIds.Add(servicePlugin.ServiceId);

                        logger.LogInformation(
                            "Successfully mapped endpoints for plugin: {PluginId}",
                            servicePlugin.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Failed to map endpoints for plugin {PluginId}: {Message}",
                            servicePlugin.Id,
                            ex.Message);
                        // Continue with other plugins instead of failing startup
                    }
                }

                logger.LogInformation(
                    "Mapped endpoints for {Count} plugins: {Services}",
                    pluginServiceIds.Count,
                    string.Join(", ", pluginServiceIds));
            }

            // All endpoint mapping is now handled by plugins loaded above
            // Legacy service registration system is no longer used

            logger.LogInformation("Configuration V2 endpoints mapped successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to map Configuration V2 endpoints");
        }

        return app;
    }

    /// <summary>
    /// Checks if a service is enabled via Configuration V2.
    /// Returns null if Configuration V2 is not active.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="serviceId">The service ID to check.</param>
    /// <returns>True if enabled, False if disabled, null if Configuration V2 not active.</returns>
    public static bool? IsServiceEnabledV2(this IServiceProvider services, string serviceId)
    {
        var honuaConfig = services.GetService<HonuaConfig>();
        if (honuaConfig == null)
        {
            return null; // Configuration V2 not active
        }

        if (honuaConfig.Services.TryGetValue(serviceId, out var serviceBlock))
        {
            return serviceBlock.Enabled;
        }

        return false; // Service not configured = disabled
    }

    private static ILogger CreateLogger()
    {
        return LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger("ConfigurationV2");
    }
}
