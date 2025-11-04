// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates security configuration on application startup.
/// </summary>
public sealed class SecurityValidationHostedService : IHostedService
{
    private readonly IHonuaConfigurationService _configurationService;
    private readonly ISecurityConfigurationValidator _validator;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SecurityValidationHostedService> _logger;

    public SecurityValidationHostedService(
        IHonuaConfigurationService configurationService,
        ISecurityConfigurationValidator validator,
        IHostEnvironment environment,
        ILogger<SecurityValidationHostedService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating security configuration...");

        var isProduction = _environment.IsProduction();
        var result = _validator.Validate(_configurationService.Current, isProduction);

        if (result.Errors.Any())
        {
            _logger.LogError("Security configuration validation FAILED with {ErrorCount} error(s):", result.Errors.Count);
            foreach (var error in result.Errors)
            {
                _logger.LogError("  [{Category}] {Message}", error.Category, error.Message);
            }

            if (isProduction)
            {
                throw new InvalidOperationException(
                    $"Security configuration validation failed with {result.Errors.Count} error(s). " +
                    "Application cannot start with insecure configuration in production. " +
                    "Review the logs above for details.");
            }
            else
            {
                _logger.LogWarning("Continuing startup despite errors (not production environment).");
            }
        }
        else if (result.Warnings.Any())
        {
            _logger.LogWarning("Security configuration validation completed with {WarningCount} warning(s):", result.Warnings.Count);
            foreach (var warning in result.Warnings)
            {
                _logger.LogWarning("  [{Category}] {Message}", warning.Category, warning.Message);
            }
        }
        else
        {
            _logger.LogInformation("Security configuration validation PASSED with no issues.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
