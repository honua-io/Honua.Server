// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Configuration;

/// <summary>
/// Validates runtime security configuration (rate limiting, etc.) on application startup.
/// </summary>
public sealed class RuntimeSecurityValidationHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IRuntimeSecurityConfigurationValidator _validator;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<RuntimeSecurityValidationHostedService> _logger;

    public RuntimeSecurityValidationHostedService(
        IConfiguration configuration,
        IRuntimeSecurityConfigurationValidator validator,
        IHostEnvironment environment,
        ILogger<RuntimeSecurityValidationHostedService> logger)
    {
        _configuration = Guard.NotNull(configuration);
        _validator = Guard.NotNull(validator);
        _environment = Guard.NotNull(environment);
        _logger = Guard.NotNull(logger);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating runtime security configuration...");

        var isProduction = _environment.IsProduction();
        var result = _validator.Validate(_configuration, isProduction);

        if (result.Errors.Any())
        {
            _logger.LogError("Runtime security configuration validation FAILED with {ErrorCount} error(s):", result.Errors.Count);
            foreach (var error in result.Errors)
            {
                _logger.LogError("  [{Category}] {Message}", error.Category, error.Message);
            }

            if (isProduction)
            {
                throw new InvalidOperationException(
                    $"Runtime security configuration validation failed with {result.Errors.Count} error(s). " +
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
            _logger.LogWarning("Runtime security configuration validation completed with {WarningCount} warning(s):", result.Warnings.Count);
            foreach (var warning in result.Warnings)
            {
                _logger.LogWarning("  [{Category}] {Message}", warning.Category, warning.Message);
            }
        }
        else
        {
            _logger.LogInformation("Runtime security configuration validation PASSED with no issues.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
