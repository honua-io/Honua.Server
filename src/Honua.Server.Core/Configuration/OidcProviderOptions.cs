// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for a generic OpenID Connect provider.
/// </summary>
public sealed class OidcProviderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether this OIDC provider is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the authority URL for the OIDC provider.
    /// This is the base URL where the provider's discovery document can be found.
    /// Example: https://login.microsoftonline.com/{tenant-id}/v2.0
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Gets or sets the client ID registered with the OIDC provider.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret for the application.
    /// SECURITY: This should be stored securely (Azure Key Vault, AWS Secrets Manager, etc.)
    /// and loaded via environment variables or secure configuration providers.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the response type for the OIDC flow.
    /// Default is "code" (Authorization Code Flow).
    /// </summary>
    public string ResponseType { get; set; } = "code";

    /// <summary>
    /// Gets or sets a value indicating whether to save tokens in the authentication properties.
    /// When true, access tokens and refresh tokens are available in the HttpContext.
    /// </summary>
    public bool SaveTokens { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to retrieve additional user claims from the UserInfo endpoint.
    /// </summary>
    public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets the scopes to request from the OIDC provider.
    /// Default includes: openid, profile, email.
    /// </summary>
    public string[] Scopes { get; set; } = new[] { "openid", "profile", "email" };

    /// <summary>
    /// Gets or sets the allowed issuers for token validation.
    /// If not specified, the Authority will be used as the valid issuer.
    /// </summary>
    public string[]? AllowedIssuers { get; set; }

    /// <summary>
    /// Gets or sets the claim type to use for roles.
    /// Default is "roles", but some providers use "role" or custom claim paths.
    /// </summary>
    public string? RoleClaimType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether HTTPS metadata is required.
    /// Should be true in production environments.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets the callback path for the OIDC authentication flow.
    /// Default is "/signin-oidc".
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>
    /// Gets or sets the signout callback path.
    /// Default is "/signout-callback-oidc".
    /// </summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>
    /// Gets or sets a value indicating whether to use Proof Key for Code Exchange (PKCE).
    /// PKCE is a security extension to OAuth 2.0 for public clients.
    /// Recommended for enhanced security.
    /// </summary>
    public bool UsePkce { get; set; } = true;

    /// <summary>
    /// Gets or sets the authentication method for token endpoint.
    /// Options: "client_secret_post", "client_secret_basic", "private_key_jwt".
    /// Default is "client_secret_post".
    /// </summary>
    public string TokenEndpointAuthMethod { get; set; } = "client_secret_post";

    /// <summary>
    /// Gets or sets additional metadata parameters for the provider.
    /// Used for provider-specific configuration.
    /// </summary>
    public Dictionary<string, string>? MetadataParameters { get; set; }
}

/// <summary>
/// Configuration options for Azure Active Directory / Microsoft Entra ID authentication.
/// </summary>
public sealed class AzureAdOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Azure AD authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD tenant ID.
    /// Can be a tenant ID (GUID), domain name (contoso.onmicrosoft.com), or "common" for multi-tenant.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD application (client) ID.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD application client secret.
    /// SECURITY: Store securely in Azure Key Vault or environment variables.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD instance.
    /// Default is "https://login.microsoftonline.com/".
    /// Use "https://login.microsoftonline.us/" for US Government cloud.
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>
    /// Gets or sets the scopes to request.
    /// Default includes: openid, profile, email.
    /// </summary>
    public string[] Scopes { get; set; } = new[] { "openid", "profile", "email" };

    /// <summary>
    /// Gets or sets the callback path.
    /// Default is "/signin-azuread".
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-azuread";

    /// <summary>
    /// Gets or sets a value indicating whether to validate the issuer.
    /// Should be true in production.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Gets or sets the claim type that contains user roles from Azure AD.
    /// Default is "roles" which maps to App Roles in Azure AD.
    /// </summary>
    public string RoleClaimType { get; set; } = "roles";
}

/// <summary>
/// Configuration options for Google OAuth 2.0 authentication.
/// </summary>
public sealed class GoogleOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Google authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the Google OAuth 2.0 client ID.
    /// Obtained from Google Cloud Console.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the Google OAuth 2.0 client secret.
    /// SECURITY: Store securely in secret management service.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the scopes to request from Google.
    /// Default includes: openid, profile, email.
    /// </summary>
    public string[] Scopes { get; set; } = new[] { "openid", "profile", "email" };

    /// <summary>
    /// Gets or sets the callback path.
    /// Default is "/signin-google".
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-google";

    /// <summary>
    /// Gets or sets additional parameters to include in the authorization request.
    /// Example: { "prompt", "consent" } to always show consent screen.
    /// </summary>
    public Dictionary<string, string>? AuthorizationParameters { get; set; }
}
