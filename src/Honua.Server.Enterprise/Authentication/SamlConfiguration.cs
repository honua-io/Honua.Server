// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// SAML Identity Provider configuration for a tenant
/// </summary>
public class SamlIdentityProviderConfiguration
{
    /// <summary>
    /// Unique identifier for this IdP configuration
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant ID this configuration belongs to
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Name of the identity provider (e.g., "Okta", "Azure AD")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IdP entity ID (issuer)
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Single Sign-On service URL (where to send SAML requests)
    /// </summary>
    public string SingleSignOnServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Single Logout service URL (optional)
    /// </summary>
    public string? SingleLogoutServiceUrl { get; set; }

    /// <summary>
    /// X509 certificate for validating SAML assertions (PEM format)
    /// </summary>
    public string SigningCertificate { get; set; } = string.Empty;

    /// <summary>
    /// Whether to sign authentication requests
    /// </summary>
    public bool SignAuthenticationRequests { get; set; } = true;

    /// <summary>
    /// Whether assertions must be signed
    /// </summary>
    public bool WantAssertionsSigned { get; set; } = true;

    /// <summary>
    /// SAML binding type (HTTP-POST or HTTP-Redirect)
    /// </summary>
    public SamlBindingType BindingType { get; set; } = SamlBindingType.HttpPost;

    /// <summary>
    /// Attribute mappings from SAML to user claims
    /// </summary>
    public Dictionary<string, string> AttributeMappings { get; set; } = new()
    {
        { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "email" },
        { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname", "firstName" },
        { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname", "lastName" },
        { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", "displayName" }
    };

    /// <summary>
    /// Whether to enable Just-in-Time (JIT) user provisioning
    /// </summary>
    public bool EnableJitProvisioning { get; set; } = true;

    /// <summary>
    /// Default role for JIT-provisioned users
    /// </summary>
    public string DefaultRole { get; set; } = "User";

    /// <summary>
    /// Whether this IdP configuration is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Configuration created timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Configuration last updated timestamp
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// IdP metadata XML (full metadata document)
    /// </summary>
    public string? MetadataXml { get; set; }

    /// <summary>
    /// Whether to allow unsolicited responses (IdP-initiated SSO)
    /// </summary>
    public bool AllowUnsolicitedAuthnResponse { get; set; } = false;

    /// <summary>
    /// NameID format to request
    /// </summary>
    public string NameIdFormat { get; set; } = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress";
}

/// <summary>
/// SAML binding types
/// </summary>
public enum SamlBindingType
{
    /// <summary>
    /// HTTP-POST binding
    /// </summary>
    HttpPost,

    /// <summary>
    /// HTTP-Redirect binding
    /// </summary>
    HttpRedirect
}

/// <summary>
/// Service Provider configuration for SAML
/// </summary>
public class SamlServiceProviderConfiguration
{
    /// <summary>
    /// Service Provider entity ID (typically the application URL)
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the service provider
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Assertion Consumer Service (ACS) path
    /// </summary>
    public string AssertionConsumerServicePath { get; set; } = "/auth/saml/acs";

    /// <summary>
    /// Metadata path
    /// </summary>
    public string MetadataPath { get; set; } = "/auth/saml/metadata";

    /// <summary>
    /// Single Logout service path
    /// </summary>
    public string SingleLogoutServicePath { get; set; } = "/auth/saml/logout";

    /// <summary>
    /// X509 certificate for signing requests (PEM format)
    /// </summary>
    public string? SigningCertificate { get; set; }

    /// <summary>
    /// Private key for signing certificate (PEM format)
    /// </summary>
    public string? SigningPrivateKey { get; set; }

    /// <summary>
    /// Authentication request validity period (in minutes)
    /// </summary>
    public int AuthnRequestValidityMinutes { get; set; } = 5;

    /// <summary>
    /// Whether to sign metadata
    /// </summary>
    public bool SignMetadata { get; set; } = true;

    /// <summary>
    /// Organization name for metadata
    /// </summary>
    public string? OrganizationName { get; set; }

    /// <summary>
    /// Organization display name for metadata
    /// </summary>
    public string? OrganizationDisplayName { get; set; }

    /// <summary>
    /// Organization URL for metadata
    /// </summary>
    public string? OrganizationUrl { get; set; }

    /// <summary>
    /// Technical contact email for metadata
    /// </summary>
    public string? TechnicalContactEmail { get; set; }

    /// <summary>
    /// Support contact email for metadata
    /// </summary>
    public string? SupportContactEmail { get; set; }
}

/// <summary>
/// SAML authentication session
/// </summary>
public class SamlAuthenticationSession
{
    /// <summary>
    /// Session ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// IdP configuration ID
    /// </summary>
    public Guid IdpConfigurationId { get; set; }

    /// <summary>
    /// SAML request ID
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Relay state (return URL)
    /// </summary>
    public string? RelayState { get; set; }

    /// <summary>
    /// Session created timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Session expiration timestamp
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Whether this session has been consumed
    /// </summary>
    public bool Consumed { get; set; }
}

/// <summary>
/// SAML assertion result after validation
/// </summary>
public class SamlAssertionResult
{
    /// <summary>
    /// Whether the assertion is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// NameID from the assertion
    /// </summary>
    public string? NameId { get; set; }

    /// <summary>
    /// User attributes from the assertion
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new();

    /// <summary>
    /// Session index for logout
    /// </summary>
    public string? SessionIndex { get; set; }

    /// <summary>
    /// Session expiration from assertion
    /// </summary>
    public DateTimeOffset? SessionNotOnOrAfter { get; set; }

    /// <summary>
    /// Validation errors (if any)
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Options for SAML SSO feature
/// </summary>
public class SamlSsoOptions
{
    /// <summary>
    /// Whether SAML SSO is enabled globally
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Service provider configuration
    /// </summary>
    public SamlServiceProviderConfiguration ServiceProvider { get; set; } = new();

    /// <summary>
    /// Session timeout in minutes
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum clock skew for time validation (in minutes)
    /// </summary>
    public int MaximumClockSkewMinutes { get; set; } = 5;

    /// <summary>
    /// Whether to enable detailed logging for troubleshooting
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;
}
