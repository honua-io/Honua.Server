// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates security-related configuration to prevent common misconfigurations.
///
/// This validator uses a WARNING-based approach for recommendations and best practices.
/// Critical security enforcement is handled by SecurityConfigurationOptionsValidator (IValidateOptions).
///
/// Use this validator for:
/// - Runtime validation checks
/// - Environment-specific validations (development vs production)
/// - Security recommendations that shouldn't block startup
///
/// For critical security requirements that must block application startup, use SecurityConfigurationOptionsValidator.
/// </summary>
public interface ISecurityConfigurationValidator
{
    SecurityValidationResult Validate(HonuaConfiguration configuration, bool isProduction);
}

public sealed record SecurityValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues)
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

public sealed class SecurityConfigurationValidator : ISecurityConfigurationValidator
{
    private readonly ILogger<SecurityConfigurationValidator> _logger;

    public SecurityConfigurationValidator(ILogger<SecurityConfigurationValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates security configuration and returns warnings/errors.
    ///
    /// NOTE: Critical security validations are enforced by SecurityConfigurationOptionsValidator.
    /// This method focuses on recommendations and environment-specific checks.
    /// </summary>
    public SecurityValidationResult Validate(HonuaConfiguration configuration, bool isProduction)
    {
        var issues = new List<ValidationIssue>();

        ValidateMetadata(configuration.Metadata, issues);
        ValidateServices(configuration.Services, issues);
        ValidateOData(configuration.Services.OData, issues);

        if (issues.Any())
        {
            foreach (var issue in issues)
            {
                var logLevel = issue.Severity == ValidationSeverity.Error ? LogLevel.Error : LogLevel.Warning;
                _logger.Log(logLevel, "Security configuration issue [{Category}]: {Message}", issue.Category, issue.Message);
            }
        }

        return new SecurityValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    private static void ValidateMetadata(MetadataConfiguration? metadata, List<ValidationIssue> issues)
    {
        if (metadata is null)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Metadata",
                "Metadata configuration is missing."));
            return;
        }

        if (string.IsNullOrWhiteSpace(metadata.Path))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Metadata",
                "Metadata path is not configured."));
        }

        if (string.IsNullOrWhiteSpace(metadata.Provider))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Metadata",
                "Metadata provider is not configured."));
        }
    }

    private static void ValidateServices(ServicesConfiguration? services, List<ValidationIssue> issues)
    {
        if (services is null)
        {
            return; // Services configuration is optional
        }

        // Validate STAC configuration if present
        if (services.Stac is not null)
        {
            if (string.IsNullOrWhiteSpace(services.Stac.Provider))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Services",
                    "STAC provider is not configured. Set 'honua:services:stac:provider' to a valid provider (e.g., 'sqlite', 'postgres', 'sqlserver')."));
            }
        }

        // Validate RasterTiles configuration if enabled
        if (services.RasterTiles?.Enabled == true)
        {
            if (string.IsNullOrWhiteSpace(services.RasterTiles.Provider))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Services",
                    "RasterTiles cache is enabled but provider is not configured. Set 'honua:services:rasterTiles:provider' to 'filesystem', 's3', or 'azure'."));
            }
        }

        // CRITICAL SECURITY: Geometry service limits prevent DoS attacks
        // These are enforced as ERRORS because they're essential DoS protections
        if (services.Geometry?.Enabled == true)
        {
            if (services.Geometry.MaxGeometries > 10000)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Services",
                    $"CRITICAL: Geometry service MaxGeometries ({services.Geometry.MaxGeometries}) exceeds secure limit of 10000. Set 'honua:services:geometry:maxGeometries' to <= 10000 to prevent DoS attacks."));
            }

            if (services.Geometry.MaxCoordinateCount > 1_000_000)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Services",
                    $"CRITICAL: Geometry service MaxCoordinateCount ({services.Geometry.MaxCoordinateCount}) exceeds secure limit of 1000000. Set 'honua:services:geometry:maxCoordinateCount' to <= 1000000 to prevent memory exhaustion."));
            }

            if (services.Geometry.MaxGeometries <= 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Services",
                    $"CRITICAL: Geometry MaxGeometries must be > 0. Current: {services.Geometry.MaxGeometries}. Set 'honua:services:geometry:maxGeometries' to a positive value."));
            }

            if (services.Geometry.MaxCoordinateCount <= 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Services",
                    $"CRITICAL: Geometry MaxCoordinateCount must be > 0. Current: {services.Geometry.MaxCoordinateCount}. Set 'honua:services:geometry:maxCoordinateCount' to a positive value."));
            }
        }
    }

    private static void ValidateOData(ODataConfiguration? odata, List<ValidationIssue> issues)
    {
        if (odata is null || !odata.Enabled)
        {
            return;
        }

        // CRITICAL SECURITY: Page size limits prevent DoS via large queries
        // These are enforced as ERRORS (not warnings) because they're critical security controls
        if (odata.MaxPageSize > 5000)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "OData",
                $"CRITICAL: MaxPageSize ({odata.MaxPageSize}) exceeds secure limit of 5000. Set 'honua:odata:maxPageSize' to <= 5000 to prevent memory exhaustion. Large page sizes enable DoS attacks."));
        }

        if (odata.DefaultPageSize > odata.MaxPageSize)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "OData",
                $"CRITICAL: DefaultPageSize ({odata.DefaultPageSize}) cannot exceed MaxPageSize ({odata.MaxPageSize}). Adjust 'honua:odata:defaultPageSize' to be <= 'honua:odata:maxPageSize'."));
        }

        if (odata.DefaultPageSize <= 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "OData",
                $"CRITICAL: DefaultPageSize must be greater than 0. Current value: {odata.DefaultPageSize}."));
        }

        if (odata.MaxPageSize <= 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "OData",
                $"CRITICAL: MaxPageSize must be greater than 0. Current value: {odata.MaxPageSize}."));
        }
    }
}
