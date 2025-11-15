# OAuth 2.0 and OpenID Connect Authentication Guide

This guide explains how to configure OAuth 2.0 and OpenID Connect (OIDC) authentication for Honua Server, enabling Single Sign-On (SSO) with Azure AD, Google, and other OIDC providers.

## Table of Contents

- [Overview](#overview)
- [Supported Providers](#supported-providers)
- [Configuration](#configuration)
  - [Azure Active Directory](#azure-active-directory)
  - [Google OAuth 2.0](#google-oauth-20)
  - [Generic OIDC Provider](#generic-oidc-provider)
- [Security Best Practices](#security-best-practices)
- [API Endpoints](#api-endpoints)
- [Claims and Role Mapping](#claims-and-role-mapping)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)

## Overview

Honua Server supports multiple authentication methods:

1. **Local Authentication** - Username/password stored in local database
2. **JWT Bearer** - API tokens for programmatic access
3. **OpenID Connect (OIDC)** - SSO with external identity providers
4. **Basic Authentication** - HTTP Basic auth for legacy clients

This guide focuses on **OpenID Connect** authentication, which enables:

- Single Sign-On (SSO) with corporate identity providers
- Centralized user management
- Multi-factor authentication (MFA) enforcement
- Role-based access control (RBAC) via external claims

## Supported Providers

Honua Server includes built-in support for:

- **Azure Active Directory (Microsoft Entra ID)** - Enterprise identity platform
- **Google OAuth 2.0** - Google Workspace or personal Google accounts
- **Generic OIDC** - Any OpenID Connect compliant provider (Okta, Auth0, Keycloak, etc.)

Multiple providers can be enabled simultaneously, allowing users to choose their preferred sign-in method.

## Configuration

### Authentication Mode

Set the authentication mode to `Oidc` in your `appsettings.json`:

```json
{
  "honua": {
    "authentication": {
      "mode": "Oidc",
      "enforce": true
    }
  }
}
```

### Azure Active Directory

#### Prerequisites

1. Register an application in Azure AD (Azure Portal > App Registrations)
2. Configure redirect URIs:
   - `https://your-domain.com/signin-azuread`
3. Create App Roles (optional, for role-based access):
   - `administrator`
   - `editor`
   - `datapublisher`
   - `viewer`
4. Generate a client secret (Certificates & secrets)

#### Configuration

```json
{
  "honua": {
    "authentication": {
      "mode": "Oidc",
      "enforce": true,
      "azureAd": {
        "enabled": true,
        "tenantId": "12345678-1234-1234-1234-123456789012",
        "clientId": "your-app-client-id",
        "clientSecret": "***USE_ENVIRONMENT_VARIABLE***",
        "instance": "https://login.microsoftonline.com/",
        "scopes": ["openid", "profile", "email"],
        "callbackPath": "/signin-azuread",
        "validateIssuer": true,
        "roleClaimType": "roles"
      }
    }
  }
}
```

#### Environment Variables

**Production**: Use environment variables or Azure Key Vault:

```bash
# Linux/macOS
export honua__authentication__azureAd__clientSecret="your-client-secret"

# Windows PowerShell
$env:honua__authentication__azureAd__clientSecret="your-client-secret"

# Docker
docker run -e honua__authentication__azureAd__clientSecret="your-secret" ...
```

#### Azure AD App Roles

Configure App Roles in Azure AD for role-based access:

1. Go to App Registrations > Your App > App roles
2. Create roles matching Honua's role names:
   - `administrator` - Full system access
   - `editor` - Edit data and metadata
   - `datapublisher` - Publish datasets
   - `viewer` - Read-only access
3. Assign users/groups to roles in Enterprise Applications

### Google OAuth 2.0

#### Prerequisites

1. Create OAuth 2.0 credentials in Google Cloud Console
2. Configure authorized redirect URIs:
   - `https://your-domain.com/signin-google`
3. Enable Google+ API (for profile information)

#### Configuration

```json
{
  "honua": {
    "authentication": {
      "mode": "Oidc",
      "enforce": true,
      "google": {
        "enabled": true,
        "clientId": "your-client-id.apps.googleusercontent.com",
        "clientSecret": "***USE_ENVIRONMENT_VARIABLE***",
        "scopes": ["openid", "profile", "email"],
        "callbackPath": "/signin-google"
      }
    }
  }
}
```

#### Role Mapping

Google doesn't provide built-in role claims. Honua maps roles based on:

1. **Email domain** - Grant roles to users from specific domains
2. **Default role** - All authenticated Google users get `viewer` role by default

Customize role mapping in `OidcClaimsTransformation.cs`:

```csharp
// Grant administrator role to specific email domain
if (email.EndsWith("@yourdomain.com", StringComparison.OrdinalIgnoreCase))
{
    claimsToAdd.Add(new Claim(ClaimTypes.Role, "administrator"));
}
```

### Generic OIDC Provider

For Okta, Auth0, Keycloak, or other OIDC providers:

#### Prerequisites

1. Register an application in your OIDC provider
2. Configure redirect URIs:
   - `https://your-domain.com/signin-oidc`
   - `https://your-domain.com/signout-callback-oidc`
3. Note the authority URL (discovery endpoint)
4. Configure role/group claims in your provider

#### Configuration

```json
{
  "honua": {
    "authentication": {
      "mode": "Oidc",
      "enforce": true,
      "oidc": {
        "enabled": true,
        "authority": "https://auth.example.com",
        "clientId": "honua-server-client",
        "clientSecret": "***USE_ENVIRONMENT_VARIABLE***",
        "responseType": "code",
        "saveTokens": true,
        "getClaimsFromUserInfoEndpoint": true,
        "scopes": ["openid", "profile", "email", "roles"],
        "allowedIssuers": ["https://auth.example.com"],
        "roleClaimType": "roles",
        "requireHttpsMetadata": true,
        "usePkce": true
      }
    }
  }
}
```

#### Provider-Specific Examples

**Okta:**
```json
{
  "oidc": {
    "authority": "https://dev-12345.okta.com/oauth2/default",
    "roleClaimType": "groups"
  }
}
```

**Auth0:**
```json
{
  "oidc": {
    "authority": "https://yourtenant.auth0.com/",
    "roleClaimType": "https://yourdomain.com/roles"
  }
}
```

**Keycloak:**
```json
{
  "oidc": {
    "authority": "https://keycloak.example.com/realms/honua",
    "roleClaimType": "realm_access.roles"
  }
}
```

## Security Best Practices

### 1. Never Commit Secrets

**NEVER** commit client secrets to source control. Use:

- **Environment variables** (development/testing)
- **Azure Key Vault** (Azure deployments)
- **AWS Secrets Manager** (AWS deployments)
- **Kubernetes Secrets** (Kubernetes deployments)

### 2. HTTPS Only

- Set `requireHttpsMetadata: true` in production
- Use HTTPS for all redirect URIs
- Configure TLS/SSL certificates properly

### 3. PKCE (Proof Key for Code Exchange)

- Always use PKCE: `"usePkce": true`
- PKCE protects against authorization code interception attacks
- Enabled by default in Honua Server

### 4. Redirect URI Validation

- Configure exact redirect URIs in your OIDC provider
- Honua Server validates redirect URIs to prevent open redirect attacks
- Use HTTPS URLs only (e.g., `https://your-domain.com/signin-oidc`)

### 5. Token Storage

- Set `"saveTokens": true` to enable token refresh
- Tokens are stored in encrypted cookies
- Configure cookie settings for your environment

### 6. Nonce Validation

- Honua Server automatically validates nonce in ID tokens
- This prevents replay attacks
- No configuration required

### 7. Session Management

- Configure session timeouts appropriately
- Implement logout endpoints
- Clear tokens on sign-out

## API Endpoints

Honua Server provides the following authentication endpoints:

### Login Endpoint

Initiates the OIDC login flow:

```
GET /auth/login/{provider}?returnUrl=/dashboard
```

**Parameters:**
- `provider` - Authentication provider: `oidc`, `azuread`, or `google`
- `returnUrl` (optional) - URL to redirect after successful login

**Example:**
```bash
# Azure AD login
curl -L https://your-domain.com/auth/login/azuread

# Google login
curl -L https://your-domain.com/auth/login/google
```

### Logout Endpoint

Signs out the current user:

```
GET /auth/logout?returnUrl=/
```

**Parameters:**
- `returnUrl` (optional) - URL to redirect after logout

**Example:**
```bash
curl -L https://your-domain.com/auth/logout
```

### User Info Endpoint

Returns information about the currently authenticated user:

```
GET /auth/user
```

**Response:**
```json
{
  "isAuthenticated": true,
  "name": "John Doe",
  "authenticationType": "azuread",
  "claims": [
    { "type": "name", "value": "John Doe" },
    { "type": "email", "value": "john@example.com" },
    { "type": "role", "value": "administrator" }
  ]
}
```

## Claims and Role Mapping

### Honua Roles

Honua Server uses four standard roles:

| Role | Permissions |
|------|------------|
| `administrator` | Full system access - manage users, data, configuration |
| `editor` | Edit existing data and metadata |
| `datapublisher` | Publish new datasets, manage data |
| `viewer` | Read-only access to data and metadata |

### Claim Transformation

Honua Server automatically transforms OIDC provider claims to internal roles:

#### Azure AD
- Reads `roles` claim from App Roles
- Maps directly to Honua roles

#### Google
- Grants `viewer` role by default
- Customize in `OidcClaimsTransformation.cs`

#### Generic OIDC
- Reads `roles` or `groups` claim
- Maps external role names to Honua roles

### Custom Role Mapping

Edit `src/Honua.Server.Host/Authentication/OidcClaimsTransformation.cs`:

```csharp
private string? MapToHonuaRole(string externalRole)
{
    var normalized = externalRole.Trim().ToLowerInvariant();

    return normalized switch
    {
        // Your organization's role names
        "honua-admin" => "administrator",
        "honua-power-user" => "editor",
        "honua-publisher" => "datapublisher",
        "honua-user" => "viewer",

        // Standard mappings
        "administrator" or "admin" => "administrator",
        "editor" => "editor",
        "publisher" => "datapublisher",
        "viewer" or "reader" => "viewer",

        _ => null // No mapping
    };
}
```

## Testing

### 1. Test with Postman

**Step 1:** Initiate login flow
```
GET https://your-domain.com/auth/login/azuread
```

**Step 2:** Complete authentication in browser

**Step 3:** Verify user information
```
GET https://your-domain.com/auth/user
```

### 2. Test with curl

```bash
# Login (will redirect to browser)
curl -L -c cookies.txt https://your-domain.com/auth/login/google

# After login, check user info
curl -b cookies.txt https://your-domain.com/auth/user

# Logout
curl -b cookies.txt https://your-domain.com/auth/logout
```

### 3. Test Token Refresh

Honua Server automatically refreshes expired access tokens using refresh tokens when:
- `saveTokens: true` is configured
- The OIDC provider supports refresh tokens
- The user hasn't revoked access

### 4. Test Role-Based Access

```bash
# Try accessing admin endpoint (requires administrator role)
curl -b cookies.txt https://your-domain.com/admin/users

# Try accessing viewer endpoint (requires any authenticated user)
curl -b cookies.txt https://your-domain.com/api/datasets
```

## Troubleshooting

### Common Issues

#### 1. "Invalid redirect URI"

**Cause:** Redirect URI not configured in OIDC provider

**Solution:**
- Add exact redirect URI to your provider's configuration
- Ensure HTTPS is used (not HTTP)
- Check for trailing slashes

**Azure AD:** App Registrations > Authentication > Redirect URIs
**Google:** Cloud Console > Credentials > Authorized redirect URIs

#### 2. "No roles assigned to user"

**Cause:** User doesn't have roles configured in OIDC provider

**Solution:**
- **Azure AD:** Assign App Roles in Enterprise Applications
- **Google:** Customize `OidcClaimsTransformation.cs` for domain-based roles
- **Generic OIDC:** Configure groups/roles in your provider

#### 3. "Authority validation failed"

**Cause:** HTTPS metadata endpoint unreachable or invalid

**Solution:**
- Verify `authority` URL is correct
- Test discovery endpoint: `{authority}/.well-known/openid-configuration`
- Ensure firewall allows outbound HTTPS to authority
- For development only: set `requireHttpsMetadata: false`

#### 4. "Client secret is invalid"

**Cause:** Client secret expired or incorrect

**Solution:**
- Regenerate client secret in provider
- Update environment variable with new secret
- Restart Honua Server

#### 5. "PKCE validation failed"

**Cause:** OIDC provider doesn't support PKCE

**Solution:**
- Set `"usePkce": false` in configuration (not recommended)
- Consider switching to a provider that supports PKCE

### Debug Logging

Enable debug logging for authentication:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore.Authentication": "Debug",
        "Honua.Server.Host.Authentication": "Debug"
      }
    }
  }
}
```

### Validation Checklist

- [ ] Redirect URIs configured in OIDC provider
- [ ] Client secret stored securely (not in appsettings.json)
- [ ] Authority URL is correct and accessible
- [ ] HTTPS is used for all redirect URIs
- [ ] Roles/groups configured in OIDC provider
- [ ] Claims transformation mapping is correct
- [ ] Firewall allows outbound HTTPS to authority
- [ ] Certificate validation is working (not disabled in production)

## Advanced Configuration

### Multiple Providers

Enable multiple providers simultaneously:

```json
{
  "honua": {
    "authentication": {
      "mode": "Oidc",
      "azureAd": { "enabled": true, ... },
      "google": { "enabled": true, ... },
      "oidc": { "enabled": true, ... }
    }
  }
}
```

Users can choose their preferred provider at login.

### Custom Scopes

Request additional scopes from providers:

```json
{
  "azureAd": {
    "scopes": [
      "openid",
      "profile",
      "email",
      "User.Read",
      "Group.Read.All"
    ]
  }
}
```

### Token Caching

Configure token caching with Redis:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

Tokens are automatically cached for improved performance.

## Security Considerations

### Production Deployment

1. **Use HTTPS** - All communication must be encrypted
2. **Store secrets securely** - Azure Key Vault, AWS Secrets Manager, etc.
3. **Enable PKCE** - Protection against authorization code interception
4. **Validate issuers** - Prevent token substitution attacks
5. **Configure CORS** - Restrict allowed origins
6. **Enable rate limiting** - Prevent brute force attacks
7. **Monitor logs** - Detect suspicious authentication attempts
8. **Implement MFA** - Enable multi-factor authentication in provider
9. **Regular secret rotation** - Rotate client secrets periodically
10. **Audit access** - Review authentication logs regularly

### Compliance

Honua Server's OIDC implementation supports:

- **GDPR** - User consent and data minimization
- **SOC 2** - Secure authentication and authorization
- **HIPAA** - Encrypted communication and audit logging
- **ISO 27001** - Information security best practices

## Additional Resources

- [OpenID Connect Specification](https://openid.net/specs/openid-connect-core-1_0.html)
- [OAuth 2.0 RFC 6749](https://tools.ietf.org/html/rfc6749)
- [PKCE RFC 7636](https://tools.ietf.org/html/rfc7636)
- [Azure AD Documentation](https://docs.microsoft.com/azure/active-directory/)
- [Google OAuth 2.0 Documentation](https://developers.google.com/identity/protocols/oauth2)

## Support

For issues or questions:

1. Check the [Troubleshooting](#troubleshooting) section
2. Enable debug logging
3. Review server logs
4. Create an issue on GitHub with logs and configuration (redact secrets!)
