// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Configuration.V2.Validation;

/// <summary>
/// Validates configuration syntax and schema correctness.
/// </summary>
public sealed class SyntaxValidator : IConfigurationValidator
{
    private static readonly string[] ValidProviders = { "sqlite", "postgresql", "sqlserver", "mysql", "oracle" };
    private static readonly string[] ValidGeometryTypes = { "Point", "LineString", "Polygon", "MultiPoint", "MultiLineString", "MultiPolygon", "Geometry", "GeometryCollection" };
    private static readonly string[] ValidFieldTypes = { "int", "long", "string", "double", "float", "datetime", "date", "time", "bool", "boolean", "geometry", "binary" };
    private static readonly string[] ValidLogLevels = { "trace", "debug", "information", "warning", "error", "critical" };
    private static readonly string[] ValidCacheTypes = { "redis", "memory" };

    public Task<ValidationResult> ValidateAsync(HonuaConfig config, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        if (config == null)
        {
            result.AddError("Configuration cannot be null");
            return Task.FromResult(result);
        }

        // Validate global settings
        ValidateGlobalSettings(config.Honua, result);

        // Validate data sources
        ValidateDataSources(config.DataSources, result);

        // Validate services
        ValidateServices(config.Services, result);

        // Validate layers
        ValidateLayers(config.Layers, result);

        // Validate caches
        ValidateCaches(config.Caches, result);

        // Validate rate limiting
        if (config.RateLimit != null)
        {
            ValidateRateLimit(config.RateLimit, result);
        }

        return Task.FromResult(result);
    }

    private void ValidateGlobalSettings(HonuaGlobalSettings settings, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(settings.Version))
        {
            result.AddError("Honua version is required", "honua.version");
        }
        else if (!settings.Version.StartsWith("1."))
        {
            result.AddWarning($"Unknown version '{settings.Version}'. Expected version starting with '1.'", "honua.version");
        }

        if (string.IsNullOrWhiteSpace(settings.Environment))
        {
            result.AddError("Environment is required", "honua.environment");
        }

        if (!string.IsNullOrWhiteSpace(settings.LogLevel) && !ValidLogLevels.Contains(settings.LogLevel.ToLowerInvariant()))
        {
            result.AddError(
                $"Invalid log level '{settings.LogLevel}'",
                "honua.log_level",
                $"Valid values: {string.Join(", ", ValidLogLevels)}");
        }

        // Validate CORS settings
        if (settings.Cors != null)
        {
            if (settings.Cors.AllowAnyOrigin && settings.Cors.AllowCredentials)
            {
                result.AddError(
                    "CORS cannot allow credentials when allow_any_origin is true",
                    "honua.cors",
                    "Set allow_any_origin to false and specify explicit allowed_origins");
            }

            if (!settings.Cors.AllowAnyOrigin && (settings.Cors.AllowedOrigins == null || settings.Cors.AllowedOrigins.Count == 0))
            {
                result.AddWarning(
                    "No CORS origins configured. API will reject browser requests.",
                    "honua.cors.allowed_origins",
                    "Add allowed origins or set allow_any_origin to true (not recommended for production)");
            }
        }
    }

    private void ValidateDataSources(System.Collections.Generic.Dictionary<string, DataSourceBlock> dataSources, ValidationResult result)
    {
        if (dataSources.Count == 0)
        {
            result.AddWarning("No data sources defined", suggestion: "Add at least one data source");
        }

        foreach (var (key, dataSource) in dataSources)
        {
            var location = $"data_source.{key}";

            if (string.IsNullOrWhiteSpace(dataSource.Id))
            {
                result.AddError("Data source ID is required", location);
            }

            if (string.IsNullOrWhiteSpace(dataSource.Provider))
            {
                result.AddError("Data source provider is required", $"{location}.provider");
            }
            else if (!ValidProviders.Contains(dataSource.Provider.ToLowerInvariant()))
            {
                result.AddWarning(
                    $"Unknown provider '{dataSource.Provider}'",
                    $"{location}.provider",
                    $"Expected one of: {string.Join(", ", ValidProviders)}");
            }

            if (string.IsNullOrWhiteSpace(dataSource.Connection))
            {
                result.AddError("Connection string is required", $"{location}.connection");
            }

            // Validate pool settings
            if (dataSource.Pool != null)
            {
                if (dataSource.Pool.MinSize < 0)
                {
                    result.AddError("Pool min_size must be >= 0", $"{location}.pool.min_size");
                }

                if (dataSource.Pool.MaxSize < 1)
                {
                    result.AddError("Pool max_size must be >= 1", $"{location}.pool.max_size");
                }

                if (dataSource.Pool.MinSize > dataSource.Pool.MaxSize)
                {
                    result.AddError(
                        $"Pool min_size ({dataSource.Pool.MinSize}) cannot be greater than max_size ({dataSource.Pool.MaxSize})",
                        $"{location}.pool");
                }

                if (dataSource.Pool.Timeout < 0)
                {
                    result.AddError("Pool timeout must be >= 0", $"{location}.pool.timeout");
                }
            }
        }
    }

    private void ValidateServices(System.Collections.Generic.Dictionary<string, ServiceBlock> services, ValidationResult result)
    {
        if (services.Count == 0)
        {
            result.AddWarning("No services defined", suggestion: "Add at least one service (odata, ogc_api, wfs, etc.)");
        }

        foreach (var (key, service) in services)
        {
            var location = $"service.{key}";

            if (string.IsNullOrWhiteSpace(service.Id))
            {
                result.AddError("Service ID is required", location);
            }

            if (string.IsNullOrWhiteSpace(service.Type))
            {
                result.AddError("Service type is required", $"{location}.type");
            }

            // Validate service-specific settings
            ValidateServiceSettings(service, location, result);
        }
    }

    private void ValidateServiceSettings(ServiceBlock service, string location, ValidationResult result)
    {
        // Validate OData-specific settings
        if (service.Type?.ToLowerInvariant() == "odata")
        {
            if (service.Settings.TryGetValue("max_page_size", out var maxPageSize))
            {
                if (maxPageSize is int size && size < 1)
                {
                    result.AddError("max_page_size must be >= 1", $"{location}.max_page_size");
                }
            }

            if (service.Settings.TryGetValue("default_page_size", out var defaultPageSize))
            {
                if (defaultPageSize is int size && size < 1)
                {
                    result.AddError("default_page_size must be >= 1", $"{location}.default_page_size");
                }
            }
        }

        // Validate OGC API-specific settings
        if (service.Type?.ToLowerInvariant() == "ogc_api")
        {
            if (service.Settings.TryGetValue("item_limit", out var itemLimit))
            {
                if (itemLimit is int limit && limit < 1)
                {
                    result.AddError("item_limit must be >= 1", $"{location}.item_limit");
                }
            }
        }
    }

    private void ValidateLayers(System.Collections.Generic.Dictionary<string, LayerBlock> layers, ValidationResult result)
    {
        if (layers.Count == 0)
        {
            result.AddWarning("No layers defined", suggestion: "Add at least one layer");
        }

        foreach (var (key, layer) in layers)
        {
            var location = $"layer.{key}";

            if (string.IsNullOrWhiteSpace(layer.Id))
            {
                result.AddError("Layer ID is required", location);
            }

            if (string.IsNullOrWhiteSpace(layer.Title))
            {
                result.AddError("Layer title is required", $"{location}.title");
            }

            if (string.IsNullOrWhiteSpace(layer.DataSource))
            {
                result.AddError("Layer data source is required", $"{location}.data_source");
            }

            if (string.IsNullOrWhiteSpace(layer.Table))
            {
                result.AddError("Layer table is required", $"{location}.table");
            }

            if (string.IsNullOrWhiteSpace(layer.IdField))
            {
                result.AddError("Layer id_field is required", $"{location}.id_field");
            }

            // Validate geometry settings
            if (layer.Geometry != null)
            {
                ValidateGeometrySettings(layer.Geometry, $"{location}.geometry", result);
            }

            // Validate explicit fields
            if (!layer.IntrospectFields && (layer.Fields == null || layer.Fields.Count == 0))
            {
                result.AddError(
                    "When introspect_fields is false, explicit fields must be defined",
                    $"{location}.fields",
                    "Either set introspect_fields to true or define fields explicitly");
            }

            if (layer.Fields != null)
            {
                foreach (var (fieldName, field) in layer.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Type))
                    {
                        result.AddError($"Field type is required", $"{location}.fields.{fieldName}.type");
                    }
                    else if (!ValidFieldTypes.Contains(field.Type.ToLowerInvariant()))
                    {
                        result.AddWarning(
                            $"Unknown field type '{field.Type}'",
                            $"{location}.fields.{fieldName}.type",
                            $"Expected one of: {string.Join(", ", ValidFieldTypes)}");
                    }
                }
            }

            // Validate services list
            if (layer.Services.Count == 0)
            {
                result.AddWarning($"Layer is not exposed through any services", $"{location}.services");
            }
        }
    }

    private void ValidateGeometrySettings(GeometrySettings geometry, string location, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(geometry.Column))
        {
            result.AddError("Geometry column is required", $"{location}.column");
        }

        if (string.IsNullOrWhiteSpace(geometry.Type))
        {
            result.AddError("Geometry type is required", $"{location}.type");
        }
        else if (!ValidGeometryTypes.Contains(geometry.Type))
        {
            result.AddError(
                $"Invalid geometry type '{geometry.Type}'",
                $"{location}.type",
                $"Valid types: {string.Join(", ", ValidGeometryTypes)}");
        }

        if (geometry.Srid < 0)
        {
            result.AddError("SRID must be >= 0", $"{location}.srid");
        }

        if (geometry.Srid == 0)
        {
            result.AddWarning("SRID is 0 (undefined). Consider setting to 4326 (WGS84) or appropriate SRID", $"{location}.srid");
        }
    }

    private void ValidateCaches(System.Collections.Generic.Dictionary<string, CacheBlock> caches, ValidationResult result)
    {
        foreach (var (key, cache) in caches)
        {
            var location = $"cache.{key}";

            if (string.IsNullOrWhiteSpace(cache.Id))
            {
                result.AddError("Cache ID is required", location);
            }

            if (string.IsNullOrWhiteSpace(cache.Type))
            {
                result.AddError("Cache type is required", $"{location}.type");
            }
            else if (!ValidCacheTypes.Contains(cache.Type.ToLowerInvariant()))
            {
                result.AddWarning(
                    $"Unknown cache type '{cache.Type}'",
                    $"{location}.type",
                    $"Expected one of: {string.Join(", ", ValidCacheTypes)}");
            }

            if (cache.Type?.ToLowerInvariant() == "redis" && string.IsNullOrWhiteSpace(cache.Connection))
            {
                result.AddError("Redis cache requires a connection string", $"{location}.connection");
            }
        }
    }

    private void ValidateRateLimit(RateLimitBlock rateLimit, ValidationResult result)
    {
        const string location = "rate_limit";

        if (string.IsNullOrWhiteSpace(rateLimit.Store))
        {
            result.AddError("Rate limit store is required", $"{location}.store");
        }

        if (rateLimit.Rules.Count == 0)
        {
            result.AddWarning("No rate limit rules defined", $"{location}.rules");
        }

        foreach (var (ruleName, rule) in rateLimit.Rules)
        {
            if (rule.Requests < 1)
            {
                result.AddError($"Rate limit requests must be >= 1", $"{location}.rules.{ruleName}.requests");
            }

            if (string.IsNullOrWhiteSpace(rule.Window))
            {
                result.AddError($"Rate limit window is required", $"{location}.rules.{ruleName}.window");
            }
            else if (!IsValidTimeWindow(rule.Window))
            {
                result.AddWarning(
                    $"Invalid time window format '{rule.Window}'",
                    $"{location}.rules.{ruleName}.window",
                    "Expected format: <number><unit>, e.g., '1m', '1h', '1d'");
            }
        }
    }

    private static bool IsValidTimeWindow(string window)
    {
        if (string.IsNullOrWhiteSpace(window) || window.Length < 2)
        {
            return false;
        }

        var unit = window[^1];
        var numberPart = window[..^1];

        return (unit == 's' || unit == 'm' || unit == 'h' || unit == 'd') &&
               int.TryParse(numberPart, out var number) &&
               number > 0;
    }
}
