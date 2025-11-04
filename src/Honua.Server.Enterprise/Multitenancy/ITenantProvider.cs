// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Enterprise.Multitenancy;

/// <summary>
/// Service for resolving the current tenant context from an HTTP request
/// Used by endpoints and middleware to determine which tenant is making the request
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Gets the current tenant context from the HTTP request
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant context if found, null otherwise</returns>
    Task<TenantContext?> GetCurrentTenantAsync(HttpContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tenant by ID
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant context if found, null otherwise</returns>
    Task<TenantContext?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a tenant exists and is active
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if tenant exists and is active, false otherwise</returns>
    Task<bool> IsTenantActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
