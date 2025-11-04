// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Hosted service that validates all configuration on startup and fails fast if invalid.
/// </summary>
public sealed class ConfigurationValidationHostedService : IHostedService
{
    private readonly ILogger<ConfigurationValidationHostedService> _logger;
    private readonly IOptionsMonitor<HonuaConfiguration> _honuaConfig;
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _authConfig;
    private readonly IOptionsMonitor<OpenRosaOptions> _openRosaConfig;
    private readonly IOptionsMonitor<ConnectionStringOptions> _connectionStrings;

    public ConfigurationValidationHostedService(
        ILogger<ConfigurationValidationHostedService> logger,
        IOptionsMonitor<HonuaConfiguration> honuaConfig,
        IOptionsMonitor<HonuaAuthenticationOptions> authConfig,
        IOptionsMonitor<OpenRosaOptions> openRosaConfig,
        IOptionsMonitor<ConnectionStringOptions> connectionStrings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _honuaConfig = honuaConfig ?? throw new ArgumentNullException(nameof(honuaConfig));
        _authConfig = authConfig ?? throw new ArgumentNullException(nameof(authConfig));
        _openRosaConfig = openRosaConfig ?? throw new ArgumentNullException(nameof(openRosaConfig));
        _connectionStrings = connectionStrings ?? throw new ArgumentNullException(nameof(connectionStrings));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating application configuration...");

        try
        {
            // Access each configuration to trigger validation
            // IValidateOptions implementations will be called automatically
            _ = _honuaConfig.CurrentValue;
            _logger.LogDebug("Honua configuration validated successfully");

            _ = _authConfig.CurrentValue;
            _logger.LogDebug("Authentication configuration validated successfully");

            _ = _openRosaConfig.CurrentValue;
            _logger.LogDebug("OpenRosa configuration validated successfully");

            _ = _connectionStrings.CurrentValue;
            _logger.LogDebug("Connection strings validated successfully");

            _logger.LogInformation("All configuration validated successfully");
        }
        catch (OptionsValidationException ex)
        {
            _logger.LogCritical(ex, "Configuration validation failed. Application will not start.");
            _logger.LogError("Validation errors:");
            foreach (var failure in ex.Failures)
            {
                _logger.LogError("  - {Failure}", failure);
            }

            // Re-throw to prevent application startup
            throw new InvalidOperationException(
                "Application configuration is invalid. See log for details. Fix configuration errors and restart the application.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unexpected error during configuration validation");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
