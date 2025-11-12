// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Configuration.V2.Validation;

/// <summary>
/// Validates semantic correctness (references, consistency, etc.).
/// </summary>
public sealed class SemanticValidator : IConfigurationValidator
{
    public Task<ValidationResult> ValidateAsync(HonuaConfig config, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        if (config == null)
        {
            result.AddError("Configuration cannot be null");
            return Task.FromResult(result);
        }

        // Validate layer references
        ValidateLayerReferences(config, result);

        // Validate service references
        ValidateServiceReferences(config, result);

        // Validate cache references
        ValidateCacheReferences(config, result);

        // Validate duplicate IDs
        ValidateDuplicateIds(config, result);

        // Validate consistency
        ValidateConsistency(config, result);

        return Task.FromResult(result);
    }

    private void ValidateLayerReferences(HonuaConfig config, ValidationResult result)
    {
        foreach (var (layerId, layer) in config.Layers)
        {
            var location = $"layer.{layerId}";

            // Validate data source reference
            if (!string.IsNullOrWhiteSpace(layer.DataSource))
            {
                var dataSourceRef = ExtractReference(layer.DataSource);
                if (!config.DataSources.ContainsKey(dataSourceRef))
                {
                    result.AddError(
                        $"Layer references undefined data source '{dataSourceRef}'",
                        $"{location}.data_source",
                        $"Available data sources: {string.Join(", ", config.DataSources.Keys)}");
                }
            }

            // Validate service references
            foreach (var serviceRef in layer.Services)
            {
                var cleanServiceRef = ExtractReference(serviceRef);
                if (!config.Services.ContainsKey(cleanServiceRef))
                {
                    result.AddError(
                        $"Layer references undefined service '{cleanServiceRef}'",
                        $"{location}.services",
                        $"Available services: {string.Join(", ", config.Services.Keys)}");
                }
            }

            // Validate field consistency
            if (layer.Geometry != null && layer.Fields != null && layer.Fields.Count > 0)
            {
                // Check if geometry column is defined in fields
                if (!layer.Fields.ContainsKey(layer.Geometry.Column))
                {
                    result.AddWarning(
                        $"Geometry column '{layer.Geometry.Column}' is not defined in fields",
                        $"{location}.fields",
                        $"Add geometry field or ensure introspect_fields is true");
                }

                // Check if id_field is defined in fields
                if (!layer.Fields.ContainsKey(layer.IdField))
                {
                    result.AddWarning(
                        $"ID field '{layer.IdField}' is not defined in fields",
                        $"{location}.fields",
                        $"Add id field or ensure introspect_fields is true");
                }

                // Check if display_field is defined in fields
                if (!string.IsNullOrWhiteSpace(layer.DisplayField) && !layer.Fields.ContainsKey(layer.DisplayField))
                {
                    result.AddWarning(
                        $"Display field '{layer.DisplayField}' is not defined in fields",
                        $"{location}.fields");
                }
            }
        }
    }

    private void ValidateServiceReferences(HonuaConfig config, ValidationResult result)
    {
        // Check if services are actually used by layers
        var usedServices = new HashSet<string>();

        foreach (var layer in config.Layers.Values)
        {
            foreach (var serviceRef in layer.Services)
            {
                usedServices.Add(ExtractReference(serviceRef));
            }
        }

        foreach (var (serviceId, service) in config.Services)
        {
            if (service.Enabled && !usedServices.Contains(serviceId))
            {
                result.AddWarning(
                    $"Service '{serviceId}' is enabled but not used by any layers",
                    $"service.{serviceId}",
                    "Add layers that expose this service or disable it");
            }
        }
    }

    private void ValidateCacheReferences(HonuaConfig config, ValidationResult result)
    {
        // Validate rate limit cache reference
        if (config.RateLimit != null && !string.IsNullOrWhiteSpace(config.RateLimit.Store))
        {
            var storeType = config.RateLimit.Store.ToLowerInvariant();

            if (storeType == "redis")
            {
                // Check if a Redis cache is defined
                var hasRedisCache = config.Caches.Values.Any(c => c.Type.ToLowerInvariant() == "redis" && c.Enabled);

                if (!hasRedisCache)
                {
                    result.AddWarning(
                        "Rate limiting uses Redis but no Redis cache is defined or enabled",
                        "rate_limit.store",
                        "Define a Redis cache or use 'memory' store");
                }
            }
        }

        // Check for environment-specific cache requirements
        var currentEnv = config.Honua.Environment.ToLowerInvariant();

        foreach (var (cacheId, cache) in config.Caches)
        {
            if (cache.RequiredIn.Count > 0)
            {
                var isRequired = cache.RequiredIn.Any(env => env.ToLowerInvariant() == currentEnv);

                if (isRequired && !cache.Enabled)
                {
                    result.AddError(
                        $"Cache '{cacheId}' is required in '{currentEnv}' environment but is disabled",
                        $"cache.{cacheId}.enabled",
                        "Enable the cache or remove the environment from required_in");
                }
            }
        }
    }

    private void ValidateDuplicateIds(HonuaConfig config, ValidationResult result)
    {
        // Check for duplicate IDs across different block types
        var allIds = new Dictionary<string, string>();

        foreach (var id in config.DataSources.Keys)
        {
            allIds[id] = "data_source";
        }

        foreach (var id in config.Services.Keys)
        {
            if (allIds.ContainsKey(id))
            {
                result.AddError(
                    $"Duplicate ID '{id}' found in service (already used in {allIds[id]})",
                    $"service.{id}",
                    "Use unique IDs across all configuration blocks");
            }
            else
            {
                allIds[id] = "service";
            }
        }

        foreach (var id in config.Layers.Keys)
        {
            if (allIds.ContainsKey(id))
            {
                result.AddError(
                    $"Duplicate ID '{id}' found in layer (already used in {allIds[id]})",
                    $"layer.{id}",
                    "Use unique IDs across all configuration blocks");
            }
            else
            {
                allIds[id] = "layer";
            }
        }

        foreach (var id in config.Caches.Keys)
        {
            if (allIds.ContainsKey(id))
            {
                result.AddError(
                    $"Duplicate ID '{id}' found in cache (already used in {allIds[id]})",
                    $"cache.{id}",
                    "Use unique IDs across all configuration blocks");
            }
            else
            {
                allIds[id] = "cache";
            }
        }
    }

    private void ValidateConsistency(HonuaConfig config, ValidationResult result)
    {
        // Check for orphaned layers (layers with no enabled services)
        foreach (var (layerId, layer) in config.Layers)
        {
            var hasEnabledService = layer.Services
                .Select(ExtractReference)
                .Any(serviceRef => config.Services.TryGetValue(serviceRef, out var service) && service.Enabled);

            if (!hasEnabledService)
            {
                result.AddWarning(
                    $"Layer '{layerId}' has no enabled services and will not be accessible",
                    $"layer.{layerId}.services",
                    "Enable at least one service or add more service references");
            }
        }

        // Check for production-specific issues
        if (config.Honua.Environment.ToLowerInvariant() == "production")
        {
            // Check for allow_any_origin in production
            if (config.Honua.Cors?.AllowAnyOrigin == true)
            {
                result.AddWarning(
                    "CORS allow_any_origin is enabled in production environment",
                    "honua.cors.allow_any_origin",
                    "Set to false and specify explicit allowed_origins for better security");
            }

            // Check for in-memory cache in production
            var hasOnlyMemoryCache = config.Caches.Values.All(c => c.Type.ToLowerInvariant() == "memory");
            if (hasOnlyMemoryCache && config.Caches.Count > 0)
            {
                result.AddWarning(
                    "Only in-memory caching is configured in production environment",
                    "cache",
                    "Consider using Redis for distributed caching in production");
            }

            // Check for rate limiting in production
            if (config.RateLimit == null || !config.RateLimit.Enabled)
            {
                result.AddWarning(
                    "Rate limiting is not enabled in production environment",
                    "rate_limit",
                    "Enable rate limiting to protect against abuse");
            }
        }

        // Check for multiple data sources without clear usage
        if (config.DataSources.Count > 1)
        {
            var dataSourceUsage = new Dictionary<string, int>();
            foreach (var ds in config.DataSources.Keys)
            {
                dataSourceUsage[ds] = 0;
            }

            foreach (var layer in config.Layers.Values)
            {
                var dsRef = ExtractReference(layer.DataSource);
                if (dataSourceUsage.ContainsKey(dsRef))
                {
                    dataSourceUsage[dsRef]++;
                }
            }

            foreach (var (dsId, count) in dataSourceUsage)
            {
                if (count == 0)
                {
                    result.AddWarning(
                        $"Data source '{dsId}' is defined but not used by any layers",
                        $"data_source.{dsId}",
                        "Remove unused data source or add layers that use it");
                }
            }
        }
    }

    /// <summary>
    /// Extract clean reference from reference syntax.
    /// Examples: "data_source.sqlite-test" -> "sqlite-test", "odata" -> "odata"
    /// </summary>
    private static string ExtractReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return reference;
        }

        var parts = reference.Split('.');
        return parts.Length > 1 ? parts[^1] : reference;
    }
}
