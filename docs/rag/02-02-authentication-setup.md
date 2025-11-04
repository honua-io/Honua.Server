---
tags: [authentication, security, jwt, oidc, oauth, api-keys, local-auth, rbac, authorization]
category: configuration
difficulty: intermediate
version: 1.0.0
last_updated: 2025-10-15
---

# Authentication and Authorization Complete Setup Guide

Comprehensive guide to configuring all authentication modes in Honua: Local, JWT, OIDC, and API Keys with complete RBAC setup.

## Table of Contents
- [Overview](#overview)
- [Authentication Modes](#authentication-modes)
- [Local Authentication](#local-authentication)
- [JWT Authentication](#jwt-authentication)
- [OIDC / OAuth 2.0](#oidc--oauth-20)
- [API Key Authentication](#api-key-authentication)
- [QuickStart Mode](#quickstart-mode)
- [Role-Based Access Control (RBAC)](#role-based-access-control-rbac)
- [Multi-Mode Configuration](#multi-mode-configuration)
- [Security Best Practices](#security-best-practices)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

## Overview

Honua provides flexible authentication supporting multiple identity providers and auth mechanisms.

### Supported Authentication Modes

| Mode | Use Case | Complexity | Production Ready |
|------|----------|------------|------------------|
| **Local** | Self-hosted, internal users | Low | Yes |
| **JWT** | Service-to-service, custom auth | Medium | Yes |
| **OIDC** | Enterprise SSO, third-party IdP | Medium | Yes |
| **API Keys** | Programmatic access | Low | Yes |
| **QuickStart** | Development only | Minimal | No |

### Authorization Roles

Honua implements three standard roles:

- **Viewer**: Read-only access to data and maps
- **Editor**: View + create/update/delete features
- **Admin**: Full system access including configuration

## Authentication Modes

### Configuration Location

Authentication is configured in `appsettings.json`:

```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true
    }
  }
}
```

### Available Modes

```json
"mode": "Local"      // Local user database
"mode": "Jwt"        // JWT token validation
"mode": "QuickStart" // No authentication (dev only)
"mode": "None"       // Disable authentication (not recommended)
```

## Local Authentication

Local authentication uses a built-in user database with password hashing and session management.

### Configuration

**appsettings.json:**
```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "local": {
        "sessionLifetime": "08:00:00",
        "requireEmailVerification": false,
        "minPasswordLength": 12,
        "requireUppercase": true,
        "requireLowercase": true,
        "requireDigit": true,
        "requireSpecialCharacter": true,
        "maxFailedAccessAttempts": 5,
        "lockoutDurationMinutes": 15
      }
    }
  }
}
```

### Password Requirements

**Default Policy:**
- Minimum 12 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character
- No dictionary words (optional)

**Custom Policy Example:**
```json
{
  "local": {
    "minPasswordLength": 16,
    "requireUppercase": true,
    "requireLowercase": true,
    "requireDigit": true,
    "requireSpecialCharacter": true,
    "requireNonAlphanumeric": true
  }
}
```

### Creating Users

**Using CLI:**
```bash
# Create admin user
honua auth create-user \
  --username admin \
  --password "SecureP@ssw0rd123" \
  --email admin@example.com \
  --role Admin

# Create editor user
honua auth create-user \
  --username editor \
  --password "EditorP@ss456" \
  --email editor@example.com \
  --role Editor

# Create viewer user
honua auth create-user \
  --username viewer \
  --password "ViewerP@ss789" \
  --email viewer@example.com \
  --role Viewer
```

**Bootstrap Admin (First Time Setup):**
```bash
honua auth bootstrap \
  --username admin \
  --password "InitialAdminP@ssw0rd123" \
  --email admin@example.com
```

This creates the first admin user when no users exist.

### Login Flow

**1. User Login:**
```bash
curl -X POST http://localhost:5000/api/auth/local/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "SecureP@ssw0rd123"
  }'
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "roles": ["Admin"]
}
```

**2. Use Token:**
```bash
curl -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  http://localhost:5000/ogc/collections
```

### Session Management

**Session Duration:**
```json
{
  "local": {
    "sessionLifetime": "08:00:00"  // 8 hours
  }
}
```

**Logout:**
```bash
curl -X POST http://localhost:5000/api/auth/local/logout \
  -H "Authorization: Bearer eyJhbGciOi..."
```

### Account Lockout

After N failed login attempts, accounts are locked:

```json
{
  "local": {
    "maxFailedAccessAttempts": 5,
    "lockoutDurationMinutes": 15
  }
}
```

**Lockout Response:**
```json
{
  "error": "account_locked",
  "lockedUntil": "2025-10-15T14:30:00Z"
}
```

**Unlock Account (Admin):**
```bash
honua auth unlock --username lockeduser
```

### Password Reset

**Request Reset:**
```bash
curl -X POST http://localhost:5000/api/auth/local/request-password-reset \
  -H "Content-Type: application/json" \
  -d '{"email": "user@example.com"}'
```

**Complete Reset:**
```bash
curl -X POST http://localhost:5000/api/auth/local/reset-password \
  -H "Content-Type: application/json" \
  -d '{
    "token": "reset-token-here",
    "newPassword": "NewSecureP@ssw0rd"
  }'
```

## JWT Authentication

JWT mode validates tokens from external identity providers or custom auth systems.

### Configuration

**appsettings.json:**
```json
{
  "honua": {
    "authentication": {
      "mode": "Jwt",
      "enforce": true,
      "jwt": {
        "issuer": "https://your-auth-server.com",
        "audience": "honua-api",
        "signingKey": "${JWT_SIGNING_KEY}",
        "requireHttpsMetadata": true,
        "validateIssuer": true,
        "validateAudience": true,
        "validateLifetime": true,
        "expirationMinutes": 60,
        "clockSkewMinutes": 5
      }
    }
  }
}
```

### Environment Variables

**Set Signing Key:**
```bash
export JWT_SIGNING_KEY="your-secret-signing-key-min-256-bits"
```

**Or in Docker:**
```yaml
services:
  honua:
    environment:
      - JWT_SIGNING_KEY=your-secret-signing-key-min-256-bits
```

### Token Generation (Example)

If you're generating tokens yourself:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

var claims = new[]
{
    new Claim(ClaimTypes.Name, "user@example.com"),
    new Claim(ClaimTypes.Role, "Editor"),
    new Claim("sub", "user-id-123")
};

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

var token = new JwtSecurityToken(
    issuer: "https://your-auth-server.com",
    audience: "honua-api",
    claims: claims,
    expires: DateTime.UtcNow.AddHours(1),
    signingCredentials: credentials
);

var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
```

### Using JWT Tokens

**Request with Bearer Token:**
```bash
curl -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
  http://localhost:5000/ogc/collections
```

### Role Mapping

Map JWT claims to Honua roles:

```json
{
  "jwt": {
    "roleClaimType": "role",
    "roleMappings": {
      "system-admin": "Admin",
      "data-editor": "Editor",
      "public-viewer": "Viewer"
    }
  }
}
```

## OIDC / OAuth 2.0

OpenID Connect integration for enterprise SSO with providers like Auth0, Okta, Azure AD, Keycloak.

### Configuration

**appsettings.json:**
```json
{
  "honua": {
    "authentication": {
      "mode": "Jwt",
      "enforce": true,
      "oidc": {
        "authority": "https://your-idp.com",
        "clientId": "honua-server",
        "clientSecret": "${OIDC_CLIENT_SECRET}",
        "responseType": "code",
        "scope": "openid profile email",
        "callbackPath": "/signin-oidc",
        "requireHttpsMetadata": true,
        "validateIssuer": true
      },
      "jwt": {
        "issuer": "https://your-idp.com",
        "audience": "honua-server",
        "requireHttpsMetadata": true
      }
    }
  }
}
```

### Provider-Specific Examples

#### Auth0

**Configuration:**
```json
{
  "oidc": {
    "authority": "https://your-tenant.auth0.com",
    "clientId": "your-auth0-client-id",
    "clientSecret": "${AUTH0_CLIENT_SECRET}",
    "scope": "openid profile email"
  },
  "jwt": {
    "issuer": "https://your-tenant.auth0.com/",
    "audience": "your-auth0-api-identifier"
  }
}
```

**Auth0 Application Setup:**
1. Create Application (Regular Web Application)
2. Configure Callback URLs: `https://your-honua.com/signin-oidc`
3. Configure Logout URLs: `https://your-honua.com/signout-callback-oidc`
4. Add API Identifier as Audience
5. Copy Client ID and Secret

#### Azure AD

**Configuration:**
```json
{
  "oidc": {
    "authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
    "clientId": "{application-id}",
    "clientSecret": "${AZURE_CLIENT_SECRET}",
    "scope": "openid profile email"
  },
  "jwt": {
    "issuer": "https://login.microsoftonline.com/{tenant-id}/v2.0",
    "audience": "{application-id}"
  }
}
```

**Azure AD App Registration:**
1. Register Application in Azure Portal
2. Add Redirect URI: `https://your-honua.com/signin-oidc`
3. Add Logout URL: `https://your-honua.com/signout-callback-oidc`
4. Create Client Secret
5. Note Application (client) ID and Directory (tenant) ID

#### Keycloak

**Configuration:**
```json
{
  "oidc": {
    "authority": "https://your-keycloak.com/realms/your-realm",
    "clientId": "honua-server",
    "clientSecret": "${KEYCLOAK_CLIENT_SECRET}",
    "scope": "openid profile email"
  },
  "jwt": {
    "issuer": "https://your-keycloak.com/realms/your-realm",
    "audience": "honua-server"
  }
}
```

**Keycloak Client Setup:**
1. Create Client in Realm
2. Access Type: Confidential
3. Valid Redirect URIs: `https://your-honua.com/*`
4. Copy Client Secret from Credentials tab

#### Okta

**Configuration:**
```json
{
  "oidc": {
    "authority": "https://your-org.okta.com/oauth2/default",
    "clientId": "okta-client-id",
    "clientSecret": "${OKTA_CLIENT_SECRET}",
    "scope": "openid profile email"
  },
  "jwt": {
    "issuer": "https://your-org.okta.com/oauth2/default",
    "audience": "api://default"
  }
}
```

### OIDC Login Flow

**1. Initiate Login:**
```
GET https://your-honua.com/api/auth/oidc/login
```

**2. User redirected to IdP for authentication**

**3. Callback with Authorization Code:**
```
GET https://your-honua.com/signin-oidc?code=auth-code&state=...
```

**4. Token Exchange (automatic)**

**5. Access Honua with Token**

### Role Mapping from OIDC Claims

```json
{
  "oidc": {
    "roleClaimType": "groups",
    "roleMappings": {
      "honua-admins": "Admin",
      "honua-editors": "Editor",
      "honua-viewers": "Viewer"
    }
  }
}
```

## API Key Authentication

API keys provide simple, revocable access for programmatic clients.

### Configuration

**Enable API Keys:**
```json
{
  "honua": {
    "authentication": {
      "apiKeys": {
        "enabled": true,
        "headerName": "X-API-Key"
      }
    }
  }
}
```

### Generating API Keys

**Using CLI:**
```bash
# Create API key for a user
honua auth create-api-key \
  --username admin \
  --name "CI/CD Pipeline" \
  --expires "2026-12-31"

# Output:
# API Key: hk_live_abc123def456ghi789jkl012mno345
```

**Using Admin API:**
```bash
curl -X POST http://localhost:5000/api/admin/api-keys \
  -H "Authorization: Bearer admin-jwt-token" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "name": "Mobile App",
    "expiresAt": "2026-12-31T23:59:59Z",
    "scopes": ["read:features", "write:features"]
  }'
```

### Using API Keys

**In Request Header:**
```bash
curl -H "X-API-Key: hk_live_abc123def456ghi789jkl012mno345" \
  http://localhost:5000/ogc/collections
```

**In Query Parameter (not recommended):**
```bash
curl "http://localhost:5000/ogc/collections?api_key=hk_live_abc123def456ghi789jkl012mno345"
```

### API Key Management

**List Keys:**
```bash
honua auth list-api-keys --username admin
```

**Revoke Key:**
```bash
honua auth revoke-api-key --key-id abc123
```

**Rotate Key:**
```bash
honua auth rotate-api-key --key-id abc123
```

### API Key Scopes

Limit what operations an API key can perform:

```json
{
  "scopes": [
    "read:features",
    "write:features",
    "read:rasters",
    "admin:config"
  ]
}
```

## QuickStart Mode

**Development only** - disables authentication for rapid prototyping.

### Configuration

```json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "quickStart": {
        "enabled": true
      }
    }
  }
}
```

### Warning

**Never use QuickStart in production!**
- No authentication required
- All users have Admin role
- No audit logging
- No rate limiting

## Role-Based Access Control (RBAC)

### Role Definitions

| Role | Permissions |
|------|-------------|
| **Viewer** | Read features, query data, view maps |
| **Editor** | Viewer + create/update/delete features |
| **Admin** | Editor + configuration, user management |

### Policy Configuration

**Define custom policies in code:**
```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("RequireViewer", policy =>
        policy.RequireRole("Viewer", "Editor", "Admin"));

    options.AddPolicy("RequireEditor", policy =>
        policy.RequireRole("Editor", "Admin"));

    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Admin"));
});
```

### Endpoint Protection

**OGC API Features:**
- GET `/ogc/collections`: Viewer+
- POST `/ogc/collections/{id}/items`: Editor+
- DELETE `/ogc/collections/{id}/items/{featureId}`: Editor+

**Admin API:**
- `/api/admin/*`: Admin only

**WFS Transactions:**
- Transaction operations: Editor+

### Custom Role Mapping

**Map external roles to Honua roles:**
```json
{
  "authentication": {
    "roleMappings": {
      "external-role": "HonuaRole",
      "org-admin": "Admin",
      "org-user": "Editor",
      "guest": "Viewer"
    }
  }
}
```

## Multi-Mode Configuration

Run multiple authentication methods simultaneously.

### Combined Example

**Local + API Keys:**
```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "local": {
        "sessionLifetime": "08:00:00"
      },
      "apiKeys": {
        "enabled": true,
        "headerName": "X-API-Key"
      }
    }
  }
}
```

Users can authenticate via:
- Local username/password → JWT
- API Key header

**OIDC + API Keys:**
```json
{
  "honua": {
    "authentication": {
      "mode": "Jwt",
      "enforce": true,
      "oidc": {
        "authority": "https://idp.example.com",
        "clientId": "honua"
      },
      "jwt": {
        "issuer": "https://idp.example.com",
        "audience": "honua"
      },
      "apiKeys": {
        "enabled": true
      }
    }
  }
}
```

Users can authenticate via:
- OIDC login → JWT
- API Key

## Security Best Practices

### Production Checklist

- [ ] Use HTTPS only (`requireHttpsMetadata: true`)
- [ ] Strong JWT signing keys (256+ bits)
- [ ] Enable token expiration validation
- [ ] Implement rate limiting
- [ ] Enable account lockout
- [ ] Audit logging enabled
- [ ] Regular security updates
- [ ] Rotate API keys periodically
- [ ] Use secrets management (not hardcoded)

### Secrets Management

**Environment Variables:**
```bash
export JWT_SIGNING_KEY="$(openssl rand -base64 32)"
export OIDC_CLIENT_SECRET="your-secret"
```

**Docker Secrets:**
```yaml
services:
  honua:
    secrets:
      - jwt_signing_key
      - oidc_client_secret
    environment:
      - JWT_SIGNING_KEY_FILE=/run/secrets/jwt_signing_key
      - OIDC_CLIENT_SECRET_FILE=/run/secrets/oidc_client_secret

secrets:
  jwt_signing_key:
    external: true
  oidc_client_secret:
    external: true
```

**Kubernetes Secrets:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: honua-auth-secrets
type: Opaque
stringData:
  JWT_SIGNING_KEY: your-secret-key
  OIDC_CLIENT_SECRET: your-oidc-secret
```

### Password Hashing

Honua uses **Argon2id** for password hashing (OWASP recommended):
- Memory-hard algorithm
- Resistant to GPU attacks
- Configurable work factors

### Token Security

**JWT Best Practices:**
- Short expiration times (1-2 hours)
- Include `nbf` (not before) claim
- Validate issuer and audience
- Use strong signing algorithms (HS256, RS256)

**API Key Best Practices:**
- Prefix for identification (`hk_live_`, `hk_test_`)
- Store hashed (not plaintext)
- Include creation/expiration dates
- Support revocation

## Troubleshooting

### Issue: 401 Unauthorized

**Symptoms:** All requests return 401.

**Solutions:**
1. Verify authentication mode is correct
2. Check token is being sent in header
3. Validate token hasn't expired
4. Verify JWT signing key matches

```bash
# Debug token
curl -v http://localhost:5000/ogc/collections \
  -H "Authorization: Bearer YOUR_TOKEN"

# Check authentication config
honua config show | grep authentication
```

### Issue: OIDC Login Fails

**Symptoms:** Redirect to IdP fails or callback error.

**Solutions:**
1. Verify `authority` URL is correct
2. Check callback URL is registered with IdP
3. Verify client ID and secret
4. Check HTTPS is enabled (required by most IdPs)

```bash
# Test OIDC discovery
curl https://your-idp.com/.well-known/openid-configuration

# Check Honua logs
docker logs honua-server 2>&1 | grep -i oidc
```

### Issue: API Key Not Working

**Symptoms:** API key returns 401 or 403.

**Solutions:**
1. Verify API keys are enabled in config
2. Check header name matches (`X-API-Key`)
3. Verify key hasn't expired
4. Check key exists and is active

```bash
# List API keys
honua auth list-api-keys --username yourusername

# Test with verbose output
curl -v -H "X-API-Key: your-key" http://localhost:5000/ogc/collections
```

### Issue: Role Not Applied

**Symptoms:** User has wrong permissions.

**Solutions:**
1. Check role claim in JWT
2. Verify role mapping configuration
3. Check policy definitions
4. Validate user roles in database

```bash
# Decode JWT to see claims
echo "JWT_TOKEN" | cut -d. -f2 | base64 -d 2>/dev/null | jq .

# Check user roles
honua auth get-user --username yourusername
```

### Issue: Account Locked

**Symptoms:** User can't login, sees "account_locked".

**Solutions:**
1. Wait for lockout duration to expire
2. Admin unlocks account
3. Reset failed login counter

```bash
# Unlock account
honua auth unlock --username lockeduser

# Check lockout status
honua auth get-user --username lockeduser | grep -i lock
```

## Related Documentation

- [Configuration Reference](./02-01-configuration-reference.md) - Full config options
- [Architecture Overview](./01-01-architecture-overview.md) - System design
- [Docker Deployment](./04-01-docker-deployment.md) - Deployment with auth
- [Common Issues](./05-02-common-issues.md) - Troubleshooting guide

---

**Last Updated**: 2025-10-15
**Honua Version**: 1.0.0-rc1
**Security**: OWASP ASVS Level 2 Compliant
