// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Multitenancy;

/// <summary>
/// Resolves tenant context from PostgreSQL database
/// Includes caching for performance
/// </summary>
public partial class PostgresTenantResolver : ITenantResolver
{
    private readonly string _connectionString;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PostgresTenantResolver> _logger;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum allowed length for tenant ID
    /// </summary>
    private const int MaxTenantIdLength = 100;

    /// <summary>
    /// Regex pattern for valid tenant ID format (alphanumeric, hyphens, underscores)
    /// </summary>
    private static readonly Regex TenantIdPattern = TenantIdRegex();

    public PostgresTenantResolver(
        string connectionString,
        IMemoryCache cache,
        ILogger<PostgresTenantResolver> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TenantContext?> ResolveTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        // Validate tenant ID to prevent cache poisoning and SQL injection
        if (!ValidateTenantId(tenantId))
        {
            _logger.LogWarning(
                "Invalid tenant ID format attempted: {TenantId}. This may be a cache poisoning or SQL injection attempt.",
                tenantId);
            return null;
        }

        // Sanitize tenant ID for cache key to prevent cache poisoning
        var sanitizedTenantId = SanitizeTenantId(tenantId);

        // Check cache first
        var cacheKey = $"tenant:{sanitizedTenantId}";
        if (_cache.TryGetValue<TenantContext>(cacheKey, out var cachedContext))
        {
            _logger.LogDebug("Tenant {TenantId} resolved from cache", sanitizedTenantId);
            return cachedContext;
        }

        // Query database
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT
                    c.customer_id,
                    c.organization_name,
                    c.tier,
                    c.subscription_status,
                    l.trial_expires_at,
                    l.features,
                    l.max_builds_per_month,
                    l.max_concurrent_builds,
                    l.max_registries
                FROM customers c
                LEFT JOIN licenses l ON c.customer_id = l.customer_id
                WHERE c.customer_id = @TenantId
                  AND c.deleted_at IS NULL
                  AND (l.status IS NULL OR l.status IN ('active', 'trial'))
                LIMIT 1";

            var result = await connection.QueryFirstOrDefaultAsync<TenantRecord>(
                sql,
                new { TenantId = sanitizedTenantId });

            if (result == null)
            {
                _logger.LogWarning("Tenant {TenantId} not found in database", sanitizedTenantId);
                return null;
            }

            // Parse features from JSONB
            var features = ParseFeatures(result.Features, result);

            var tenantContext = TenantContext.FromCustomer(
                tenantId: result.CustomerId,
                customerId: result.CustomerId,
                organizationName: result.OrganizationName,
                tier: result.Tier,
                subscriptionStatus: result.SubscriptionStatus,
                trialExpiresAt: result.TrialExpiresAt,
                features: features
            );

            // Cache for 5 minutes
            _cache.Set(cacheKey, tenantContext, _cacheDuration);

            _logger.LogInformation("Tenant {TenantId} resolved from database. Org: {OrgName}, Tier: {Tier}",
                sanitizedTenantId, result.OrganizationName, result.Tier);

            return tenantContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving tenant {TenantId}", sanitizedTenantId);
            return null;
        }
    }

    public async Task<bool> IsTenantIdAvailableAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        // Validate tenant ID to prevent SQL injection
        if (!ValidateTenantId(tenantId))
        {
            _logger.LogWarning(
                "Invalid tenant ID format attempted in availability check: {TenantId}",
                tenantId);
            return false;
        }

        // Sanitize tenant ID before database query
        var sanitizedTenantId = SanitizeTenantId(tenantId);

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT COUNT(1)
                FROM customers
                WHERE customer_id = @TenantId
                  AND deleted_at IS NULL";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { TenantId = sanitizedTenantId });

            return count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking tenant ID availability: {TenantId}", sanitizedTenantId);
            return false;
        }
    }

    private TenantFeatures ParseFeatures(string? featuresJson, TenantRecord record)
    {
        try
        {
            if (!string.IsNullOrEmpty(featuresJson))
            {
                var features = JsonSerializer.Deserialize<TenantFeaturesJson>(featuresJson);
                if (features != null)
                {
                    return new TenantFeatures
                    {
                        AiIntake = features.ai_intake ?? true,
                        CustomModules = features.custom_modules ?? true,
                        PriorityBuilds = features.priority_builds ?? false,
                        DedicatedCache = features.dedicated_cache ?? false,
                        SlaGuarantee = features.sla_guarantee ?? false,
                        RasterProcessing = features.raster_processing ?? true,
                        VectorProcessing = features.vector_processing ?? true,
                        ODataApi = features.odata_api ?? true,
                        StacApi = features.stac_api ?? true,
                        MaxBuildsPerMonth = record.MaxBuildsPerMonth,
                        MaxConcurrentBuilds = record.MaxConcurrentBuilds ?? 1,
                        MaxRegistries = record.MaxRegistries ?? 3
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing features JSON for tenant {TenantId}", record.CustomerId);
        }

        // Return defaults
        return new TenantFeatures
        {
            MaxBuildsPerMonth = record.MaxBuildsPerMonth,
            MaxConcurrentBuilds = record.MaxConcurrentBuilds ?? 1,
            MaxRegistries = record.MaxRegistries ?? 3
        };
    }

    // Database record structure
    private class TenantRecord
    {
        public string CustomerId { get; set; } = string.Empty;
        public string? OrganizationName { get; set; }
        public string? Tier { get; set; }
        public string? SubscriptionStatus { get; set; }
        public DateTimeOffset? TrialExpiresAt { get; set; }
        public string? Features { get; set; }
        public int? MaxBuildsPerMonth { get; set; }
        public int? MaxConcurrentBuilds { get; set; }
        public int? MaxRegistries { get; set; }
    }

    // JSON structure for features column
    private class TenantFeaturesJson
    {
        public bool? ai_intake { get; set; }
        public bool? custom_modules { get; set; }
        public bool? priority_builds { get; set; }
        public bool? dedicated_cache { get; set; }
        public bool? sla_guarantee { get; set; }
        public bool? raster_processing { get; set; }
        public bool? vector_processing { get; set; }
        public bool? odata_api { get; set; }
        public bool? stac_api { get; set; }
    }

    /// <summary>
    /// Validates tenant ID format to prevent cache poisoning and SQL injection
    /// </summary>
    /// <param name="tenantId">The tenant ID to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private bool ValidateTenantId(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        if (tenantId.Length > MaxTenantIdLength)
        {
            _logger.LogWarning(
                "Tenant ID exceeds maximum length of {MaxLength} characters: {TenantId}",
                MaxTenantIdLength, tenantId);
            return false;
        }

        if (!TenantIdPattern.IsMatch(tenantId))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sanitizes tenant ID by removing any characters that are not alphanumeric, hyphens, or underscores
    /// </summary>
    /// <param name="tenantId">The tenant ID to sanitize</param>
    /// <returns>Sanitized tenant ID</returns>
    private string SanitizeTenantId(string tenantId)
    {
        // Only return characters that match the pattern
        // This is a belt-and-suspenders approach - we already validated above
        return Regex.Replace(tenantId, @"[^a-zA-Z0-9\-_]", "");
    }

    /// <summary>
    /// Compiled regex for tenant ID validation (alphanumeric, hyphens, underscores only)
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled)]
    private static partial Regex TenantIdRegex();
}
