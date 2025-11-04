// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// Service for Just-in-Time (JIT) user provisioning from SAML assertions
/// </summary>
public interface ISamlUserProvisioningService
{
    /// <summary>
    /// Provisions a user from a SAML assertion, creating the user if they don't exist
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="idpConfigurationId">IdP configuration ID</param>
    /// <param name="assertionResult">Validated SAML assertion</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Provisioned user information</returns>
    Task<SamlProvisionedUser> ProvisionUserAsync(
        Guid tenantId,
        Guid idpConfigurationId,
        SamlAssertionResult assertionResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets existing user mapping from SAML NameID
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="idpConfigurationId">IdP configuration ID</param>
    /// <param name="nameId">SAML NameID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User mapping if exists</returns>
    Task<SamlUserMapping?> GetUserMappingAsync(
        Guid tenantId,
        Guid idpConfigurationId,
        string nameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates last login timestamp for a SAML user
    /// </summary>
    Task UpdateLastLoginAsync(
        Guid mappingId,
        string? sessionIndex,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// SAML user mapping record
/// </summary>
public class SamlUserMapping
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid IdpConfigurationId { get; set; }
    public string NameId { get; set; } = string.Empty;
    public string? SessionIndex { get; set; }
    public DateTimeOffset LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Result of SAML user provisioning
/// </summary>
public class SamlProvisionedUser
{
    /// <summary>
    /// User ID (existing or newly created)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User mapping ID
    /// </summary>
    public Guid MappingId { get; set; }

    /// <summary>
    /// Whether this was a new user (JIT provisioned)
    /// </summary>
    public bool IsNewUser { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's display name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Assigned role
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// SAML NameID
    /// </summary>
    public string NameId { get; set; } = string.Empty;
}
