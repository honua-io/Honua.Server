// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Enterprise.Multitenancy;

/// <summary>
/// Represents the current tenant context for a request
/// Enterprise feature for SaaS/multitenant deployments
/// </summary>
public class TenantContext
{
    /// <summary>
    /// Unique tenant identifier (e.g., "acme", "demo-123")
    /// </summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// Customer ID from database (links to customers table)
    /// </summary>
    public string? CustomerId { get; init; }

    /// <summary>
    /// Organization name
    /// </summary>
    public string? OrganizationName { get; init; }

    /// <summary>
    /// Subscription tier (trial, core, pro, enterprise, asp)
    /// </summary>
    public string? Tier { get; init; }

    /// <summary>
    /// Subscription status (trial, active, suspended, cancelled)
    /// </summary>
    public string? SubscriptionStatus { get; init; }

    /// <summary>
    /// Whether this is a trial account
    /// </summary>
    public bool IsTrial => SubscriptionStatus?.Equals("trial", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Trial expiration date (if applicable)
    /// </summary>
    public DateTimeOffset? TrialExpiresAt { get; init; }

    /// <summary>
    /// Whether the trial has expired
    /// </summary>
    public bool IsTrialExpired => IsTrial && TrialExpiresAt.HasValue && TrialExpiresAt.Value < DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether this tenant is active and not expired
    /// </summary>
    public bool IsActive =>
        SubscriptionStatus?.Equals("active", StringComparison.OrdinalIgnoreCase) == true ||
        (IsTrial && !IsTrialExpired);

    /// <summary>
    /// Feature flags for this tenant
    /// </summary>
    public TenantFeatures Features { get; init; } = new();

    /// <summary>
    /// Creates a tenant context with minimal information
    /// </summary>
    public static TenantContext Create(string tenantId) => new() { TenantId = tenantId };

    /// <summary>
    /// Creates a tenant context from customer database record
    /// </summary>
    public static TenantContext FromCustomer(
        string tenantId,
        string customerId,
        string? organizationName,
        string? tier,
        string? subscriptionStatus,
        DateTimeOffset? trialExpiresAt,
        TenantFeatures? features = null)
    {
        return new TenantContext
        {
            TenantId = tenantId,
            CustomerId = customerId,
            OrganizationName = organizationName,
            Tier = tier,
            SubscriptionStatus = subscriptionStatus,
            TrialExpiresAt = trialExpiresAt,
            Features = features ?? new TenantFeatures()
        };
    }
}

/// <summary>
/// Feature flags for a tenant
/// </summary>
public class TenantFeatures
{
    public bool AiIntake { get; init; } = true;
    public bool CustomModules { get; init; } = true;
    public bool PriorityBuilds { get; init; } = false;
    public bool DedicatedCache { get; init; } = false;
    public bool SlaGuarantee { get; init; } = false;
    public bool RasterProcessing { get; init; } = true;
    public bool VectorProcessing { get; init; } = true;
    public bool ODataApi { get; init; } = true;
    public bool StacApi { get; init; } = true;

    /// <summary>
    /// Maximum builds per month (null = unlimited)
    /// </summary>
    public int? MaxBuildsPerMonth { get; init; } = 100;

    /// <summary>
    /// Maximum concurrent builds
    /// </summary>
    public int MaxConcurrentBuilds { get; init; } = 1;

    /// <summary>
    /// Maximum registries
    /// </summary>
    public int MaxRegistries { get; init; } = 3;
}
