// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// Store for managing SAML Identity Provider configurations per tenant
/// </summary>
public interface ISamlIdentityProviderStore
{
    /// <summary>
    /// Gets IdP configuration by ID
    /// </summary>
    Task<SamlIdentityProviderConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets IdP configuration for a specific tenant
    /// </summary>
    Task<SamlIdentityProviderConfiguration?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all IdP configurations for a tenant
    /// </summary>
    Task<List<SamlIdentityProviderConfiguration>> GetAllByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new IdP configuration
    /// </summary>
    Task<SamlIdentityProviderConfiguration> CreateAsync(SamlIdentityProviderConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing IdP configuration
    /// </summary>
    Task<SamlIdentityProviderConfiguration> UpdateAsync(SamlIdentityProviderConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an IdP configuration
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets enabled IdP configuration for a tenant
    /// </summary>
    Task<SamlIdentityProviderConfiguration?> GetEnabledByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
