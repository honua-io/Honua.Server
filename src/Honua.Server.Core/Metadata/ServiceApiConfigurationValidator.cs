// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Configuration;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates that service-level API configurations don't violate global settings.
/// Ensures APIs can only be enabled at service level if enabled globally.
/// </summary>
public static class ServiceApiConfigurationValidator
{
    public sealed record ValidationResult
    {
        public bool IsValid { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Validates all services against global API configuration.
    /// </summary>
    public static ValidationResult ValidateServices(
        IReadOnlyList<ServiceDefinition> services,
        ServicesConfiguration globalConfig)
    {
        Guard.NotNull(services);
        Guard.NotNull(globalConfig);

        var errors = new List<string>();

        foreach (var service in services)
        {
            if (service == null) continue;

            ValidateService(service, globalConfig, errors);
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Validates a single service's API configuration.
    /// </summary>
    public static ValidationResult ValidateService(
        ServiceDefinition service,
        ServicesConfiguration globalConfig)
    {
        Guard.NotNull(service);
        Guard.NotNull(globalConfig);

        var errors = new List<string>();
        ValidateService(service, globalConfig, errors);

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static void ValidateService(
        ServiceDefinition service,
        ServicesConfiguration globalConfig,
        List<string> errors)
    {
        var ogc = service.Ogc;
        if (ogc == null) return;

        // Validate OGC API Features (Collections)
        if (ogc.CollectionsEnabled)
        {
            // OGC API Features doesn't have a global disable - it's always available
            // No validation needed
        }

        // Validate WFS
        if (ogc.WfsEnabled && !(globalConfig.Wfs?.Enabled ?? true))
        {
            errors.Add($"Service '{service.Id}': WFS is enabled at service level but disabled globally. " +
                      "Either enable WFS globally (honua:services:wfs:enabled) or disable it for this service (ogc.wfsEnabled: false).");
        }

        // Validate WMS
        if (ogc.WmsEnabled && !(globalConfig.Wms?.Enabled ?? true))
        {
            errors.Add($"Service '{service.Id}': WMS is enabled at service level but disabled globally. " +
                      "Either enable WMS globally (honua:services:wms:enabled) or disable it for this service (ogc.wmsEnabled: false).");
        }

        // Validate WMTS
        if (ogc.WmtsEnabled && !(globalConfig.Wmts?.Enabled ?? true))
        {
            errors.Add($"Service '{service.Id}': WMTS is enabled at service level but disabled globally. " +
                      "Either enable WMTS globally (honua:services:wmts:enabled) or disable it for this service (ogc.wmtsEnabled: false).");
        }

        // Validate CSW
        if (ogc.CswEnabled && !(globalConfig.Csw?.Enabled ?? true))
        {
            errors.Add($"Service '{service.Id}': CSW is enabled at service level but disabled globally. " +
                      "Either enable CSW globally (honua:services:csw:enabled) or disable it for this service (ogc.cswEnabled: false).");
        }

        // Validate WCS
        if (ogc.WcsEnabled && !(globalConfig.Wcs?.Enabled ?? true))
        {
            errors.Add($"Service '{service.Id}': WCS is enabled at service level but disabled globally. " +
                      "Either enable WCS globally (honua:services:wcs:enabled) or disable it for this service (ogc.wcsEnabled: false).");
        }
    }

    /// <summary>
    /// Determines if an API is enabled for a specific service, checking both global and service-level flags.
    /// </summary>
    public static bool IsApiEnabled(
        ServiceDefinition service,
        string apiType,
        ServicesConfiguration globalConfig)
    {
        Guard.NotNull(service);
        Guard.NotNull(apiType);
        Guard.NotNull(globalConfig);

        var ogc = service.Ogc;
        if (ogc == null) return false;

        return apiType.ToLowerInvariant() switch
        {
            "ogc-api-features" or "collections" => ogc.CollectionsEnabled,
            "wfs" => ogc.WfsEnabled && (globalConfig.Wfs?.Enabled ?? true),
            "wms" => ogc.WmsEnabled && (globalConfig.Wms?.Enabled ?? true),
            "wmts" => ogc.WmtsEnabled && (globalConfig.Wmts?.Enabled ?? true),
            "csw" => ogc.CswEnabled && (globalConfig.Csw?.Enabled ?? true),
            "wcs" => ogc.WcsEnabled && (globalConfig.Wcs?.Enabled ?? true),
            _ => false
        };
    }
}
