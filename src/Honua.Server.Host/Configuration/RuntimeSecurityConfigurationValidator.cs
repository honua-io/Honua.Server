// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Configuration;

/// <summary>
/// Validates runtime security configuration (rate limiting, CORS via metadata).
/// This complements SecurityConfigurationValidator for ASP.NET Core-specific settings.
/// </summary>
public interface IRuntimeSecurityConfigurationValidator
{
    ValidationResult Validate(IConfiguration configuration, bool isProduction);
}

public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues)
{
    public IReadOnlyList<ValidationIssue> Errors => Issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
    public IReadOnlyList<ValidationIssue> Warnings => Issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();
}

public sealed record ValidationIssue(ValidationSeverity Severity, string Category, string Message);

public enum ValidationSeverity
{
    Warning,
    Error
}

public sealed class RuntimeSecurityConfigurationValidator : IRuntimeSecurityConfigurationValidator
{
    private readonly ILogger<RuntimeSecurityConfigurationValidator> _logger;

    public RuntimeSecurityConfigurationValidator(ILogger<RuntimeSecurityConfigurationValidator> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public ValidationResult Validate(IConfiguration configuration, bool isProduction)
    {
        var issues = new List<ValidationIssue>();

        ValidateRateLimiting(configuration, issues, isProduction);

        if (issues.Any())
        {
            foreach (var issue in issues)
            {
                var logLevel = issue.Severity == ValidationSeverity.Error ? LogLevel.Error : LogLevel.Warning;
                _logger.Log(logLevel, "Runtime security configuration issue [{Category}]: {Message}", issue.Category, issue.Message);
            }
        }

        return new ValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    private static void ValidateRateLimiting(IConfiguration configuration, List<ValidationIssue> issues, bool isProduction)
    {
        var rateLimitingEnabled = configuration.GetValue("RateLimiting:Enabled", true);

        if (isProduction && !rateLimitingEnabled)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "RateLimiting",
                "Rate limiting must be enabled in production to prevent DoS attacks."));
        }

        // Validate default policy
        var defaultPermitLimit = configuration.GetValue("RateLimiting:Default:PermitLimit", 100);
        if (defaultPermitLimit > 500)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                "RateLimiting",
                $"Default rate limit ({defaultPermitLimit}/min) may be too permissive. Recommended: 100-200/min. High limits increase DoS risk."));
        }

        if (defaultPermitLimit <= 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "RateLimiting",
                $"Default rate limit ({defaultPermitLimit}) must be greater than 0."));
        }

        // Validate OGC API policy (read-heavy workloads)
        var ogcApiPermitLimit = configuration.GetValue("RateLimiting:OgcApi:PermitLimit", 200);
        if (ogcApiPermitLimit > 1000)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                "RateLimiting",
                $"OGC API rate limit ({ogcApiPermitLimit}/min) may be too permissive. Recommended: 200-500/min."));
        }

        // Validate OpenRosa policy (write-heavy workloads)
        var openRosaPermitLimit = configuration.GetValue("RateLimiting:OpenRosa:PermitLimit", 50);
        if (openRosaPermitLimit > 200)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                "RateLimiting",
                $"OpenRosa rate limit ({openRosaPermitLimit}/min) may be too permissive for write operations. Recommended: 50-100/min."));
        }

        // Validate window duration
        var defaultWindowMinutes = configuration.GetValue("RateLimiting:Default:WindowMinutes", 1);
        if (defaultWindowMinutes > 60)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                "RateLimiting",
                $"Rate limiting window ({defaultWindowMinutes} minutes) is very long. Recommended: 1-5 minutes for responsive rate limiting."));
        }

        if (defaultWindowMinutes <= 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "RateLimiting",
                $"Rate limiting window ({defaultWindowMinutes}) must be greater than 0."));
        }
    }
}
