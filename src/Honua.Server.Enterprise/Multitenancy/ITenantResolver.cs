// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Multitenancy;

/// <summary>
/// Interface for resolving tenant context from tenant ID
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Resolves tenant context from the database by tenant ID
    /// </summary>
    /// <param name="tenantId">Tenant identifier (subdomain)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant context if found, null otherwise</returns>
    Task<TenantContext?> ResolveTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a tenant ID is available for registration
    /// </summary>
    /// <param name="tenantId">Desired tenant identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if available, false if already taken</returns>
    Task<bool> IsTenantIdAvailableAsync(string tenantId, CancellationToken cancellationToken = default);
}
