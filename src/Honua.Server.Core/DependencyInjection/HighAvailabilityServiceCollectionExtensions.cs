// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Configuration.V2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering high availability and configuration watching services.
/// Provides support for distributed configuration change notifications and file watching.
/// </summary>
public static class HighAvailabilityServiceCollectionExtensions
{
    /// <summary>
    /// Adds high availability support for configuration change notifications.
    /// When HA is enabled, uses Redis for distributed notifications across server instances.
    /// When HA is disabled, uses local in-process notifications.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when HA is enabled but Redis connection is not configured.</exception>
    public static IServiceCollection AddHighAvailability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Register HA options
        services.Configure<HonuaHighAvailabilityOptions>(
            configuration.GetSection(HonuaHighAvailabilityOptions.SectionName));

        // Register appropriate notifier based on HA configuration
        services.AddSingleton<IConfigurationChangeNotifier>(sp =>
        {
            var haOptions = sp.GetRequiredService<IOptions<HonuaHighAvailabilityOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<IConfigurationChangeNotifier>>();

            if (haOptions.Enabled)
            {
                logger.LogInformation(
                    "High Availability mode enabled - using Redis for distributed configuration change notifications");

                // Validate Redis connection string
                if (string.IsNullOrWhiteSpace(haOptions.RedisConnectionString))
                {
                    throw new InvalidOperationException(
                        "High Availability is enabled but Redis connection string is not configured. " +
                        $"Please set '{HonuaHighAvailabilityOptions.SectionName}:RedisConnectionString' in configuration.");
                }

                // Get Redis connection multiplexer
                var redis = sp.GetService<StackExchange.Redis.IConnectionMultiplexer>();
                if (redis == null)
                {
                    throw new InvalidOperationException(
                        "High Availability is enabled but Redis IConnectionMultiplexer is not registered. " +
                        "Ensure AddHonuaCaching is called before AddHighAvailability.");
                }

                var redisLogger = sp.GetRequiredService<ILogger<RedisConfigurationChangeNotifier>>();
                var options = sp.GetRequiredService<IOptions<HonuaHighAvailabilityOptions>>();
                return new RedisConfigurationChangeNotifier(redis, redisLogger, options);
            }
            else
            {
                logger.LogInformation(
                    "High Availability mode disabled - using local in-process configuration change notifications");

                var localLogger = sp.GetRequiredService<ILogger<LocalConfigurationChangeNotifier>>();
                return new LocalConfigurationChangeNotifier(localLogger);
            }
        });

        return services;
    }

    /// <summary>
    /// Adds configuration file watching support with automatic reload.
    /// When file watching is enabled, monitors the configuration file for changes and triggers reloads.
    /// When file watching is disabled, configuration changes require application restart.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configPath">The path to the configuration file to watch.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services, configPath, or configuration is null.</exception>
    /// <exception cref="ArgumentException">Thrown when configPath is empty or whitespace.</exception>
    public static IServiceCollection AddConfigurationWatcher(
        this IServiceCollection services,
        string configPath,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentNullException.ThrowIfNull(configuration);

        // Register watcher options
        services.Configure<ConfigurationWatcherOptions>(
            configuration.GetSection(ConfigurationWatcherOptions.SectionName));

        // Register configuration watcher as singleton for injection
        services.AddSingleton<HclConfigurationWatcher>(sp =>
        {
            var watcherOptions = sp.GetRequiredService<IOptions<ConfigurationWatcherOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<HclConfigurationWatcher>>();

            var debounceDelay = TimeSpan.FromMilliseconds(watcherOptions.DebounceMilliseconds);
            var watcher = new HclConfigurationWatcher(configPath, logger, debounceDelay);

            if (watcherOptions.LogChangeEvents)
            {
                logger.LogInformation(
                    "Configuration watcher initialized for: {ConfigPath} (debounce: {DebounceMs}ms)",
                    configPath,
                    watcherOptions.DebounceMilliseconds);
            }

            return watcher;
        });

        // Register as hosted service only if file watching is enabled
        services.AddSingleton<IHostedService>(sp =>
        {
            var watcherOptions = sp.GetRequiredService<IOptions<ConfigurationWatcherOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<HclConfigurationWatcherHostedService>>();

            if (watcherOptions.EnableFileWatching)
            {
                logger.LogInformation(
                    "File watching enabled - configuration changes will be automatically detected and reloaded");

                var watcher = sp.GetRequiredService<HclConfigurationWatcher>();
                var notifier = sp.GetRequiredService<IConfigurationChangeNotifier>();

                return new HclConfigurationWatcherHostedService(
                    watcher,
                    notifier,
                    configPath,
                    logger,
                    watcherOptions);
            }
            else
            {
                logger.LogInformation(
                    "File watching disabled - configuration changes will require application restart");

                // Return a no-op hosted service that does nothing
                return new NoOpHostedService();
            }
        });

        return services;
    }

    /// <summary>
    /// No-op hosted service that does nothing.
    /// Used when file watching is disabled.
    /// </summary>
    private sealed class NoOpHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// Hosted service that manages the configuration watcher lifecycle and integrates with the notifier.
    /// Starts/stops the file watcher and publishes change notifications when configuration changes are detected.
    /// </summary>
    private sealed class HclConfigurationWatcherHostedService : IHostedService
    {
        private readonly HclConfigurationWatcher _watcher;
        private readonly IConfigurationChangeNotifier _notifier;
        private readonly string _configPath;
        private readonly ILogger<HclConfigurationWatcherHostedService> _logger;
        private readonly ConfigurationWatcherOptions _options;
        private IDisposable? _changeTokenRegistration;

        public HclConfigurationWatcherHostedService(
            HclConfigurationWatcher watcher,
            IConfigurationChangeNotifier notifier,
            string configPath,
            ILogger<HclConfigurationWatcherHostedService> logger,
            ConfigurationWatcherOptions options)
        {
            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Verify configuration file exists
                if (!File.Exists(_configPath))
                {
                    _logger.LogWarning(
                        "Configuration file not found at startup: {ConfigPath}. " +
                        "File watching will be enabled but no changes will be detected until the file is created.",
                        _configPath);
                    return;
                }

                // Start the watcher
                await _watcher.StartAsync(cancellationToken);

                // Subscribe to change notifications from the watcher
                _changeTokenRegistration = Microsoft.Extensions.Primitives.ChangeToken.OnChange(
                    () => _watcher.CurrentChangeToken,
                    async () => await OnConfigurationChanged());

                _logger.LogInformation(
                    "Configuration watcher hosted service started successfully for: {ConfigPath}",
                    _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to start configuration watcher hosted service for: {ConfigPath}",
                    _configPath);
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _changeTokenRegistration?.Dispose();
                _changeTokenRegistration = null;

                await _watcher.StopAsync(cancellationToken);

                _logger.LogInformation(
                    "Configuration watcher hosted service stopped for: {ConfigPath}",
                    _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error stopping configuration watcher hosted service for: {ConfigPath}",
                    _configPath);
            }
        }

        private async Task OnConfigurationChanged()
        {
            try
            {
                if (_options.LogChangeEvents)
                {
                    _logger.LogInformation(
                        "Configuration file change detected: {ConfigPath}. Notifying all server instances...",
                        _configPath);
                }

                // Notify all subscribers (local or distributed via Redis)
                await _notifier.NotifyConfigurationChangedAsync(_configPath, CancellationToken.None);

                if (_options.LogChangeEvents)
                {
                    _logger.LogInformation(
                        "Configuration change notification sent successfully for: {ConfigPath}",
                        _configPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error handling configuration change for: {ConfigPath}",
                    _configPath);
            }
        }
    }
}
