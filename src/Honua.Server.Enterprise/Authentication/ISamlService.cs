// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// Service for SAML 2.0 protocol operations
/// </summary>
public interface ISamlService
{
    /// <summary>
    /// Generates a SAML authentication request for the specified tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="relayState">Optional relay state (return URL)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SAML request result with redirect URL</returns>
    Task<SamlAuthenticationRequest> CreateAuthenticationRequestAsync(
        Guid tenantId,
        string? relayState = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a SAML response from the identity provider
    /// </summary>
    /// <param name="samlResponse">Base64-encoded SAML response</param>
    /// <param name="relayState">Relay state from the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Assertion result with user claims</returns>
    Task<SamlAssertionResult> ValidateResponseAsync(
        string samlResponse,
        string? relayState = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates service provider metadata XML
    /// </summary>
    /// <param name="tenantId">Tenant ID (optional, for tenant-specific metadata)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SP metadata XML</returns>
    Task<string> GenerateServiceProviderMetadataAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports IdP metadata from XML
    /// </summary>
    /// <param name="metadataXml">IdP metadata XML</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed IdP configuration</returns>
    Task<SamlIdentityProviderConfiguration> ImportIdpMetadataAsync(
        string metadataXml,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a SAML logout request
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="nameId">User NameID</param>
    /// <param name="sessionIndex">Session index from authentication</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Logout request result</returns>
    Task<SamlLogoutRequest> CreateLogoutRequestAsync(
        Guid tenantId,
        string nameId,
        string? sessionIndex = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a SAML logout response
    /// </summary>
    /// <param name="samlResponse">Base64-encoded SAML logout response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Whether logout was successful</returns>
    Task<bool> ValidateLogoutResponseAsync(
        string samlResponse,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// SAML authentication request result
/// </summary>
public class SamlAuthenticationRequest
{
    /// <summary>
    /// Request ID
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// URL to redirect user to for authentication
    /// </summary>
    public string RedirectUrl { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded SAML request (for POST binding)
    /// </summary>
    public string? SamlRequest { get; set; }

    /// <summary>
    /// Relay state
    /// </summary>
    public string? RelayState { get; set; }

    /// <summary>
    /// Binding type used
    /// </summary>
    public SamlBindingType BindingType { get; set; }

    /// <summary>
    /// Request expiration timestamp
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// SAML logout request result
/// </summary>
public class SamlLogoutRequest
{
    /// <summary>
    /// Request ID
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// URL to redirect user to for logout
    /// </summary>
    public string RedirectUrl { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded SAML logout request
    /// </summary>
    public string? SamlRequest { get; set; }

    /// <summary>
    /// Binding type used
    /// </summary>
    public SamlBindingType BindingType { get; set; }
}
