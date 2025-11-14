// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Security;

/// <summary>
/// Service for managing tenant context and enforcing tenant isolation.
/// This resolves TODO-003 by providing proper tenant isolation and preventing cross-tenant data leakage.
/// </summary>
public interface ITenantContextService
{
    /// <summary>
    /// Gets the current tenant ID from the authentication context.
    /// </summary>
    /// <returns>The tenant ID, or the default tenant ID if multi-tenancy is not enabled.</returns>
    string GetCurrentTenantId();

    /// <summary>
    /// Gets the current tenant ID from the authentication context, or throws an exception if not found.
    /// </summary>
    /// <returns>The tenant ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown if tenant ID cannot be determined.</exception>
    string GetRequiredTenantId();

    /// <summary>
    /// Validates that the specified resource belongs to the current tenant.
    /// </summary>
    /// <param name="resourceTenantId">The tenant ID associated with the resource.</param>
    /// <exception cref="UnauthorizedAccessException">Thrown if the resource belongs to a different tenant.</exception>
    void ValidateTenantAccess(string resourceTenantId);

    /// <summary>
    /// Validates that the specified resource belongs to the current tenant asynchronously.
    /// </summary>
    /// <param name="resourceTenantId">The tenant ID associated with the resource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="UnauthorizedAccessException">Thrown if the resource belongs to a different tenant.</exception>
    Task ValidateTenantAccessAsync(string resourceTenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if multi-tenancy is enabled in the current configuration.
    /// </summary>
    /// <returns>True if multi-tenancy is enabled, false otherwise.</returns>
    bool IsMultiTenancyEnabled();
}

/// <summary>
/// Default implementation of ITenantContextService.
/// </summary>
public sealed class TenantContextService : ITenantContextService
{
    private readonly IUserIdentityService _userIdentityService;
    private readonly ILogger<TenantContextService> _logger;
    private const string DefaultTenantId = "default";

    public TenantContextService(
        IUserIdentityService userIdentityService,
        ILogger<TenantContextService> logger)
    {
        _userIdentityService = userIdentityService ?? throw new ArgumentNullException(nameof(userIdentityService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string GetCurrentTenantId()
    {
        // Try to get tenant ID from claims
        var tenantId = _userIdentityService.GetCurrentTenantId();

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            return tenantId;
        }

        // If multi-tenancy is not enabled or no tenant claim exists, use default tenant
        // In production, you might want to make this configurable or enforce tenant claims
        _logger.LogDebug("No tenant ID found in claims, using default tenant");
        return DefaultTenantId;
    }

    public string GetRequiredTenantId()
    {
        var tenantId = _userIdentityService.GetCurrentTenantId();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            var userId = _userIdentityService.GetCurrentUserId();
            _logger.LogError("Tenant ID is required but not found in claims for user {UserId}", userId ?? "unknown");
            throw new InvalidOperationException(
                "Tenant ID is required but not found in authentication context. " +
                "Ensure that your authentication provider includes a 'tenant_id' claim in the JWT token.");
        }

        return tenantId;
    }

    public void ValidateTenantAccess(string resourceTenantId)
    {
        if (string.IsNullOrWhiteSpace(resourceTenantId))
        {
            throw new ArgumentException("Resource tenant ID cannot be null or empty", nameof(resourceTenantId));
        }

        var currentTenantId = GetCurrentTenantId();

        if (!string.Equals(currentTenantId, resourceTenantId, StringComparison.Ordinal))
        {
            var userId = _userIdentityService.GetCurrentUserId() ?? "unknown";
            _logger.LogWarning(
                "Tenant isolation violation: User {UserId} from tenant {CurrentTenant} attempted to access resource from tenant {ResourceTenant}",
                userId, currentTenantId, resourceTenantId);

            throw new UnauthorizedAccessException(
                "Access denied. You do not have permission to access resources from a different tenant.");
        }
    }

    public Task ValidateTenantAccessAsync(string resourceTenantId, CancellationToken cancellationToken = default)
    {
        ValidateTenantAccess(resourceTenantId);
        return Task.CompletedTask;
    }

    public bool IsMultiTenancyEnabled()
    {
        // Multi-tenancy is considered enabled if the current user has a tenant claim
        var tenantId = _userIdentityService.GetCurrentTenantId();
        return !string.IsNullOrWhiteSpace(tenantId);
    }
}
