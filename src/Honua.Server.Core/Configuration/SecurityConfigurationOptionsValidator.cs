// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates security configuration options and enforces critical security requirements.
/// Integrated with ASP.NET Core options validation to fail fast on startup.
///
/// Validation Strategy:
/// - ERRORS: Critical security misconfigurations that must block application startup
/// - WARNINGS: Logged but don't block startup (handled by SecurityConfigurationValidator)
///
/// This validator is invoked automatically during options resolution via IValidateOptions.
/// </summary>
public sealed class SecurityConfigurationOptionsValidator : IValidateOptions<HonuaConfiguration>
{
    public ValidateOptionsResult Validate(string? name, HonuaConfiguration options)
    {
        var failures = new List<string>();

        // Validate critical security requirements
        ValidateMetadataSecurity(options.Metadata, failures);
        ValidateServicesSecurity(options.Services, failures);
        ValidateODataSecurity(options.Services?.OData, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    /// <summary>
    /// Validates metadata configuration security.
    /// ENFORCES: Metadata must be configured (required for authorization/feature access).
    /// </summary>
    private static void ValidateMetadataSecurity(MetadataConfiguration? metadata, List<string> failures)
    {
        if (metadata is null)
        {
            failures.Add("SECURITY: Metadata configuration is missing. Metadata is required for proper authorization and feature access control. Set 'honua:metadata' in appsettings.json.");
            return;
        }

        if (string.IsNullOrWhiteSpace(metadata.Path))
        {
            failures.Add("SECURITY: Metadata path is not configured. This prevents the application from loading authorization rules and feature configurations. Set 'honua:metadata:path' to a valid path.");
        }

        if (string.IsNullOrWhiteSpace(metadata.Provider))
        {
            failures.Add("SECURITY: Metadata provider is not configured. The application cannot load security metadata without a provider. Set 'honua:metadata:provider' to 'json' or 'yaml'.");
        }
    }

    /// <summary>
    /// Validates services configuration security.
    /// ENFORCES: DoS prevention limits for geometry and OData services.
    /// </summary>
    private static void ValidateServicesSecurity(ServicesConfiguration? services, List<string> failures)
    {
        if (services is null)
        {
            return; // Services configuration is optional
        }

        // ENFORCE: Geometry service DoS limits
        if (services.Geometry?.Enabled == true)
        {
            if (services.Geometry.MaxGeometries > 10000)
            {
                failures.Add($"SECURITY: Geometry service MaxGeometries ({services.Geometry.MaxGeometries}) exceeds secure limit of 10000. This exposes the application to DoS attacks via excessive geometry processing. Reduce 'honua:services:geometry:maxGeometries' to <= 10000.");
            }

            if (services.Geometry.MaxCoordinateCount > 1_000_000)
            {
                failures.Add($"SECURITY: Geometry service MaxCoordinateCount ({services.Geometry.MaxCoordinateCount}) exceeds secure limit of 1000000. This can cause memory exhaustion and application crashes. Reduce 'honua:services:geometry:maxCoordinateCount' to <= 1000000.");
            }

            // Basic validation
            if (services.Geometry.MaxGeometries <= 0)
            {
                failures.Add($"SECURITY: Geometry MaxGeometries must be > 0. Current: {services.Geometry.MaxGeometries}. Set 'honua:services:geometry:maxGeometries' to a positive value.");
            }

            if (services.Geometry.MaxCoordinateCount <= 0)
            {
                failures.Add($"SECURITY: Geometry MaxCoordinateCount must be > 0. Current: {services.Geometry.MaxCoordinateCount}. Set 'honua:services:geometry:maxCoordinateCount' to a positive value.");
            }
        }

        // STAC configuration - validate provider if enabled (not security-critical, but prevents runtime errors)
        if (services.Stac is not null && !string.IsNullOrWhiteSpace(services.Stac.Provider))
        {
            var validProviders = new[] { "sqlite", "postgres", "sqlserver" };
            if (!Array.Exists(validProviders, p => p.Equals(services.Stac.Provider, StringComparison.OrdinalIgnoreCase)))
            {
                failures.Add($"SECURITY: STAC provider '{services.Stac.Provider}' is invalid. Invalid providers can lead to runtime failures. Valid values: {string.Join(", ", validProviders)}.");
            }
        }
    }

    /// <summary>
    /// Validates OData configuration security.
    /// ENFORCES: Page size limits to prevent memory exhaustion DoS attacks.
    /// </summary>
    private static void ValidateODataSecurity(ODataConfiguration? odata, List<string> failures)
    {
        if (odata is null || !odata.Enabled)
        {
            return;
        }

        // ENFORCE: OData page size limits to prevent DoS
        if (odata.MaxPageSize > 5000)
        {
            failures.Add($"SECURITY: OData MaxPageSize ({odata.MaxPageSize}) exceeds secure limit of 5000. Large page sizes can cause memory exhaustion and enable DoS attacks. Reduce 'honua:odata:maxPageSize' to <= 5000.");
        }

        // ENFORCE: Logical consistency
        if (odata.DefaultPageSize > odata.MaxPageSize)
        {
            failures.Add($"SECURITY: OData DefaultPageSize ({odata.DefaultPageSize}) cannot exceed MaxPageSize ({odata.MaxPageSize}). This configuration is invalid and will cause runtime errors. Adjust 'honua:odata:defaultPageSize' to be <= 'honua:odata:maxPageSize'.");
        }

        // ENFORCE: Basic validation
        if (odata.DefaultPageSize <= 0)
        {
            failures.Add($"SECURITY: OData DefaultPageSize must be > 0. Current: {odata.DefaultPageSize}. Set 'honua:odata:defaultPageSize' to a positive value.");
        }

        if (odata.MaxPageSize <= 0)
        {
            failures.Add($"SECURITY: OData MaxPageSize must be > 0. Current: {odata.MaxPageSize}. Set 'honua:odata:maxPageSize' to a positive value.");
        }
    }
}
