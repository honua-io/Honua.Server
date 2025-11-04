# Honua OAuth and Authentication Setup Guide

**Keywords**: OAuth, authentication, OIDC, OpenID Connect, Azure AD, Auth0, Okta, JWT, bearer tokens, security, identity provider

**Related Topics**: [Environment Variables](environment-variables.md), [Troubleshooting](../04-operations/troubleshooting.md), [Docker Deployment](../02-deployment/docker-deployment.md)

---

## Overview

Honua supports three authentication modes for securing your geospatial API:

1. **QuickStart** - No authentication (development only, never use in production)
2. **Local** - Built-in authentication with Honua-managed users and JWT tokens
3. **Oidc** (OAuth/OpenID Connect) - External identity providers (Azure AD, Okta, Auth0, Keycloak)

This guide focuses on **production-ready OAuth/OIDC configuration** for enterprise deployments.

---

## Table of Contents

1. [Authentication Modes Overview](#authentication-modes-overview)
2. [Honua Local Authentication](#honua-local-authentication)
3. [OAuth/OIDC Configuration](#oauthoiddic-configuration)
4. [Azure Active Directory Setup](#azure-active-directory-setup)
5. [Auth0 Setup](#auth0-setup)
6. [Okta Setup](#okta-setup)
7. [Keycloak Setup](#keycloak-setup)
8. [Role Mapping](#role-mapping)
9. [Testing Authentication](#testing-authentication)
10. [Troubleshooting](#troubleshooting)

---

## Authentication Modes Overview

Honua's authentication is configured via `JwtBearerOptionsConfigurator` (see `src/Honua.Server.Core/Authentication/JwtBearerOptionsConfigurator.cs:32-43`).

### QuickStart Mode (Development Only)

**Use Case**: Local development, demos, testing

```bash
# Environment variables
export HONUA__AUTHENTICATION__MODE=QuickStart
export HONUA__AUTHENTICATION__ENFORCE=false

# OR appsettings.json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "enforce": false
    }
  }
}
```

**Security**: No authentication required. **Never use in production!**

### Local Mode (Built-in Authentication)

**Use Case**: Small deployments, internal tools, simple security requirements

```bash
# Environment variables
export HONUA__AUTHENTICATION__MODE=Local
export HONUA__AUTHENTICATION__ENFORCE=true

# Bootstrap authentication system
honua auth bootstrap --mode Local

# Create admin user
honua auth create-user --username admin --password *** --role administrator
```

**Features**:
- Honua-managed user database (SQLite)
- JWT token generation with configurable expiration
- Role-based access control (administrator, datapublisher, viewer)
- No external dependencies

**Token Details**:
- **Issuer**: `honua-local`
- **Audience**: `honua-api`
- **Expiration**: 1 hour (configurable)
- **Signing Key**: Symmetric key stored in `data/auth/signing-key`

### Oidc Mode (OAuth/OpenID Connect)

**Use Case**: Enterprise deployments, external identity management, SSO

```bash
# Environment variables
export HONUA__AUTHENTICATION__MODE=Oidc
export HONUA__AUTHENTICATION__JWT__AUTHORITY=https://login.microsoftonline.com/{tenant-id}/v2.0
export HONUA__AUTHENTICATION__JWT__AUDIENCE=api://honua-server
export HONUA__AUTHENTICATION__JWT__REQUIREHTTPSMETADATA=true
export HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH=roles
```

**Features**:
- External identity provider (Azure AD, Okta, Auth0, Keycloak)
- Single Sign-On (SSO) support
- Centralized user management
- Advanced security features (MFA, conditional access)

---

## Honua Local Authentication

### Setup Workflow

```bash
# 1. Configure Honua for Local authentication
export HONUA__AUTHENTICATION__MODE=Local
export HONUA__AUTHENTICATION__ENFORCE=true

# 2. Bootstrap the authentication system (creates database and signing key)
honua auth bootstrap --mode Local

# 3. Create administrator user
honua auth create-user \
  --username admin \
  --password "SecurePassword123!" \
  --role administrator

# 4. Create data publisher user
honua auth create-user \
  --username publisher \
  --password "AnotherSecurePass!" \
  --role datapublisher

# 5. Create viewer user
honua auth create-user \
  --username viewer \
  --password "ViewerPass123!" \
  --role viewer

# 6. Login to get JWT token
TOKEN=$(honua auth login --username admin --password "SecurePassword123!" --json | jq -r .token)

# 7. Test authenticated request
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/collections
```

### Roles and Permissions

| Role | Permissions | Use Case |
|------|------------|----------|
| `administrator` | Full access: read, write, delete, admin operations | System administrators |
| `datapublisher` | Read and write data, manage collections | Data managers, GIS analysts |
| `viewer` | Read-only access to data | End users, data consumers |

### Token Management

**Login (Get Token)**:

```bash
# Get token as JSON
honua auth login --username admin --password *** --json

# Output:
# {
#   "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
#   "expiresAt": "2025-10-04T11:30:00Z",
#   "user": "admin",
#   "roles": ["administrator"]
# }

# Extract token for API calls
TOKEN=$(honua auth login --username admin --password *** --json | jq -r .token)
```

**Validate Token**:

```bash
# Decode and validate token
honua auth validate-token --token $TOKEN

# Manual decode (base64)
echo $TOKEN | cut -d. -f2 | base64 -d | jq .

# Expected payload:
# {
#   "sub": "admin",
#   "role": "administrator",
#   "aud": "honua-api",
#   "iss": "honua-local",
#   "exp": 1735646400
# }
```

**Refresh Token** (when expired):

```bash
# Tokens expire after 1 hour by default
# Simply re-login to get a new token
TOKEN=$(honua auth login --username admin --password *** --json | jq -r .token)
```

### User Management

```bash
# List all users
honua auth list-users

# Assign role to existing user
honua auth assign-role --user publisher --role administrator

# Change password
honua auth change-password --username publisher --new-password "NewSecurePass!"

# Delete user
honua auth delete-user --username olduser
```

---

## OAuth/OIDC Configuration

### General OIDC Setup

Honua implements standard OpenID Connect (OIDC) with JWT Bearer token authentication.

**Required Environment Variables**:

```bash
# Authentication mode
HONUA__AUTHENTICATION__MODE=Oidc

# OIDC Authority (OpenID Connect discovery endpoint)
HONUA__AUTHENTICATION__JWT__AUTHORITY=https://your-idp.com/.well-known/openid-configuration

# Audience (API identifier in your IdP)
HONUA__AUTHENTICATION__JWT__AUDIENCE=api://honua-server

# Require HTTPS for metadata discovery (true for production)
HONUA__AUTHENTICATION__JWT__REQUIREHTTPSMETADATA=true

# Role claim path in JWT (where roles are stored)
HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH=roles
```

**appsettings.json Configuration**:

```json
{
  "honua": {
    "authentication": {
      "mode": "Oidc",
      "enforce": true,
      "jwt": {
        "authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
        "audience": "api://honua-server",
        "requireHttpsMetadata": true,
        "roleClaimPath": "roles"
      }
    }
  }
}
```

### How Honua Validates JWT Tokens

**Token Validation Flow** (from `JwtBearerOptionsConfigurator.cs:83-102`):

1. **Authority Discovery**: Fetch OIDC metadata from `{authority}/.well-known/openid-configuration`
2. **Issuer Validation**: Verify `iss` claim matches authority
3. **Audience Validation**: Verify `aud` claim matches configured audience
4. **Signature Validation**: Verify JWT signature using public keys from IdP
5. **Expiration Check**: Verify `exp` claim is in the future
6. **Role Extraction**: Extract roles from claim specified by `roleClaimPath`

**Token Requirements**:

- **Algorithm**: RS256 (RSA + SHA256)
- **Type**: Bearer token in `Authorization` header
- **Claims**:
  - `iss` (issuer): Must match authority
  - `aud` (audience): Must match configured audience
  - `sub` (subject): User identifier
  - `exp` (expiration): Unix timestamp
  - `{roleClaimPath}` (roles): User roles (e.g., `["administrator"]`)

---

## Azure Active Directory Setup

### Step 1: Register Application in Azure AD

1. Navigate to [Azure Portal](https://portal.azure.com) → **Azure Active Directory** → **App registrations**
2. Click **New registration**
3. Configure:
   - **Name**: `Honua Server API`
   - **Supported account types**: `Accounts in this organizational directory only`
   - **Redirect URI**: Leave blank (server-to-server)
4. Click **Register**

### Step 2: Configure API Permissions

1. In your app registration, go to **Expose an API**
2. Click **Add a scope**:
   - **Application ID URI**: `api://honua-server`
   - **Scope name**: `access_as_user`
   - **Who can consent**: `Admins and users`
   - **Display name**: `Access Honua API`
3. Click **Add scope**

### Step 3: Create App Roles

1. Go to **App roles** → **Create app role**
2. Create three roles:

**Administrator Role**:
```json
{
  "displayName": "Administrator",
  "id": "a1b2c3d4-...",
  "isEnabled": true,
  "description": "Full access to Honua API",
  "value": "administrator",
  "allowedMemberTypes": ["User"]
}
```

**Data Publisher Role**:
```json
{
  "displayName": "Data Publisher",
  "id": "e5f6g7h8-...",
  "isEnabled": true,
  "description": "Read and write access to Honua data",
  "value": "datapublisher",
  "allowedMemberTypes": ["User"]
}
```

**Viewer Role**:
```json
{
  "displayName": "Viewer",
  "id": "i9j0k1l2-...",
  "isEnabled": true,
  "description": "Read-only access to Honua API",
  "value": "viewer",
  "allowedMemberTypes": ["User"]
}
```

### Step 4: Assign Users to Roles

1. Go to **Enterprise applications** → **Honua Server API**
2. Click **Users and groups** → **Add user/group**
3. Select user and assign role (administrator, datapublisher, or viewer)

### Step 5: Configure Honua

```bash
# Get your Azure AD tenant ID from portal
TENANT_ID="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"

# Configure Honua
export HONUA__AUTHENTICATION__MODE=Oidc
export HONUA__AUTHENTICATION__JWT__AUTHORITY="https://login.microsoftonline.com/$TENANT_ID/v2.0"
export HONUA__AUTHENTICATION__JWT__AUDIENCE="api://honua-server"
export HONUA__AUTHENTICATION__JWT__REQUIREHTTPSMETADATA=true
export HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH="roles"
```

**Docker Compose Example**:

```yaml
services:
  honua:
    image: honua:latest
    environment:
      HONUA__AUTHENTICATION__MODE: Oidc
      HONUA__AUTHENTICATION__JWT__AUTHORITY: "https://login.microsoftonline.com/${AZURE_TENANT_ID}/v2.0"
      HONUA__AUTHENTICATION__JWT__AUDIENCE: "api://honua-server"
      HONUA__AUTHENTICATION__JWT__REQUIREHTTPSMETADATA: "true"
      HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH: "roles"
```

### Step 6: Get Access Token

**Using Azure CLI**:

```bash
# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Login
az login

# Get access token for Honua API
az account get-access-token --resource api://honua-server --query accessToken -o tsv
```

**Using curl (client credentials flow)**:

```bash
# Get client credentials from Azure portal
CLIENT_ID="your-client-id"
CLIENT_SECRET="your-client-secret"
TENANT_ID="your-tenant-id"

# Request token
curl -X POST "https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "scope=api://honua-server/.default"

# Extract access token
TOKEN=$(curl -s -X POST "https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=$CLIENT_ID&client_secret=$CLIENT_SECRET&scope=api://honua-server/.default" \
  | jq -r .access_token)

# Use token with Honua
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/collections
```

---

## Auth0 Setup

### Step 1: Create Auth0 API

1. Navigate to [Auth0 Dashboard](https://manage.auth0.com)
2. Go to **Applications** → **APIs** → **Create API**
3. Configure:
   - **Name**: `Honua Server API`
   - **Identifier**: `https://honua-api.example.com` (this is your audience)
   - **Signing Algorithm**: `RS256`
4. Click **Create**

### Step 2: Configure Permissions (Scopes)

1. In your API, go to **Permissions**
2. Add scopes:
   - `read:data` - Read geospatial data
   - `write:data` - Write geospatial data
   - `admin:server` - Administrative access

### Step 3: Create Auth0 Roles

1. Go to **User Management** → **Roles** → **Create Role**
2. Create three roles:
   - **Administrator** with permissions: `read:data`, `write:data`, `admin:server`
   - **Data Publisher** with permissions: `read:data`, `write:data`
   - **Viewer** with permissions: `read:data`

### Step 4: Create Custom Claim for Roles

Auth0 doesn't include roles in tokens by default. Create an **Action** to add roles:

1. Go to **Actions** → **Flows** → **Login**
2. Click **Custom** → **Create Action**
3. Name: `Add Roles to Token`
4. Code:

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://honua-api.example.com';

  if (event.authorization) {
    // Add roles to access token
    api.accessToken.setCustomClaim(`${namespace}/roles`, event.authorization.roles);

    // Add roles to ID token
    api.idToken.setCustomClaim(`${namespace}/roles`, event.authorization.roles);
  }
};
```

5. Click **Deploy** → Add to **Login** flow

### Step 5: Assign Users to Roles

1. Go to **User Management** → **Users**
2. Select a user → **Roles** → **Assign Roles**
3. Assign appropriate role (Administrator, Data Publisher, or Viewer)

### Step 6: Configure Honua

```bash
# Get your Auth0 domain (e.g., "dev-abc123.us.auth0.com")
AUTH0_DOMAIN="your-domain.auth0.com"
AUTH0_AUDIENCE="https://honua-api.example.com"

# Configure Honua
export HONUA__AUTHENTICATION__MODE=Oidc
export HONUA__AUTHENTICATION__JWT__AUTHORITY="https://$AUTH0_DOMAIN/"
export HONUA__AUTHENTICATION__JWT__AUDIENCE="$AUTH0_AUDIENCE"
export HONUA__AUTHENTICATION__JWT__REQUIREHTTPSMETADATA=true
export HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH="https://honua-api.example.com/roles"
```

**appsettings.json**:

```json
{
  "honua": {
    "authentication": {
      "mode": "Oidc",
      "jwt": {
        "authority": "https://your-domain.auth0.com/",
        "audience": "https://honua-api.example.com",
        "requireHttpsMetadata": true,
        "roleClaimPath": "https://honua-api.example.com/roles"
      }
    }
  }
}
```

### Step 7: Get Access Token

```bash
# Create Machine-to-Machine application in Auth0
# Get credentials from Auth0 dashboard

AUTH0_DOMAIN="your-domain.auth0.com"
CLIENT_ID="your-client-id"
CLIENT_SECRET="your-client-secret"
AUDIENCE="https://honua-api.example.com"

# Request token
curl -X POST "https://$AUTH0_DOMAIN/oauth/token" \
  -H "Content-Type: application/json" \
  -d "{
    \"client_id\": \"$CLIENT_ID\",
    \"client_secret\": \"$CLIENT_SECRET\",
    \"audience\": \"$AUDIENCE\",
    \"grant_type\": \"client_credentials\"
  }"

# Extract token
TOKEN=$(curl -s -X POST "https://$AUTH0_DOMAIN/oauth/token" \
  -H "Content-Type: application/json" \
  -d "{\"client_id\":\"$CLIENT_ID\",\"client_secret\":\"$CLIENT_SECRET\",\"audience\":\"$AUDIENCE\",\"grant_type\":\"client_credentials\"}" \
  | jq -r .access_token)

# Use with Honua
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/collections
```

---

## Okta Setup

### Step 1: Create Authorization Server

1. Navigate to [Okta Admin Console](https://your-domain.okta.com/admin)
2. Go to **Security** → **API** → **Authorization Servers**
3. Click **Add Authorization Server**:
   - **Name**: `Honua API`
   - **Audience**: `api://honua-server`
   - **Description**: `Authorization server for Honua geospatial API`
4. Click **Save**

### Step 2: Add Scopes

1. In your authorization server, go to **Scopes** → **Add Scope**
2. Create scopes:
   - `honua:read` - Read geospatial data
   - `honua:write` - Write geospatial data
   - `honua:admin` - Administrative access

### Step 3: Create Groups for Roles

1. Go to **Directory** → **Groups** → **Add Group**
2. Create three groups:
   - **Honua Administrators**
   - **Honua Data Publishers**
   - **Honua Viewers**

### Step 4: Add Custom Claim for Roles

1. In authorization server, go to **Claims** → **Add Claim**
2. Configure:
   - **Name**: `roles`
   - **Include in token type**: `Access Token`
   - **Value type**: `Groups`
   - **Filter**: `Starts with` `Honua`
   - **Disable claim**: Unchecked
   - **Include in**: `Any scope`

### Step 5: Assign Users to Groups

1. Go to **Directory** → **People** → Select user
2. Click **Groups** → **Assign to Groups**
3. Assign to appropriate group (Administrators, Data Publishers, or Viewers)

### Step 6: Configure Honua

```bash
# Get Okta domain and authorization server ID
OKTA_DOMAIN="your-domain.okta.com"
AUTH_SERVER_ID="default"  # Or your custom authorization server ID

# Configure Honua
export HONUA__AUTHENTICATION__MODE=Oidc
export HONUA__AUTHENTICATION__JWT__AUTHORITY="https://$OKTA_DOMAIN/oauth2/$AUTH_SERVER_ID"
export HONUA__AUTHENTICATION__JWT__AUDIENCE="api://honua-server"
export HONUA__AUTHENTICATION__JWT__REQUIREHTTPSMETADATA=true
export HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH="roles"
```

### Step 7: Get Access Token

```bash
# Create OAuth 2.0 application in Okta (Service app)
OKTA_DOMAIN="your-domain.okta.com"
CLIENT_ID="your-client-id"
CLIENT_SECRET="your-client-secret"

# Request token
curl -X POST "https://$OKTA_DOMAIN/oauth2/default/v1/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "scope=honua:read honua:write"

# Extract token
TOKEN=$(curl -s -X POST "https://$OKTA_DOMAIN/oauth2/default/v1/token" \
  -d "grant_type=client_credentials&client_id=$CLIENT_ID&client_secret=$CLIENT_SECRET&scope=honua:read honua:write" \
  | jq -r .access_token)

# Use with Honua
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/collections
```

---

## Keycloak Setup

### Step 1: Create Realm

1. Navigate to Keycloak Admin Console
2. Click **Add Realm**
3. Name: `honua`
4. Click **Create**

### Step 2: Create Client

1. Go to **Clients** → **Create**
2. Configure:
   - **Client ID**: `honua-api`
   - **Client Protocol**: `openid-connect`
   - **Access Type**: `bearer-only`
3. Click **Save**

### Step 3: Create Roles

1. Go to **Roles** → **Add Role**
2. Create three roles:
   - `administrator`
   - `datapublisher`
   - `viewer`

### Step 4: Create Client Scope for Roles

1. Go to **Client Scopes** → **Create**
2. Name: `roles`
3. Protocol: `openid-connect`
4. Click **Save**
5. Go to **Mappers** → **Create**:
   - **Name**: `roles`
   - **Mapper Type**: `User Realm Role`
   - **Token Claim Name**: `roles`
   - **Claim JSON Type**: `String`
   - **Add to ID token**: `ON`
   - **Add to access token**: `ON`

### Step 5: Assign Roles to Users

1. Go to **Users** → Select user
2. Click **Role Mappings**
3. Assign appropriate role

### Step 6: Configure Honua

```bash
# Keycloak configuration
KEYCLOAK_URL="https://keycloak.example.com"
REALM="honua"

# Configure Honua
export HONUA__AUTHENTICATION__MODE=Oidc
export HONUA__AUTHENTICATION__JWT__AUTHORITY="$KEYCLOAK_URL/realms/$REALM"
export HONUA__AUTHENTICATION__JWT__AUDIENCE="honua-api"
export HONUA__AUTHENTICATION__JWT__REQUIREHTTPSMETADATA=true
export HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH="roles"
```

---

## Role Mapping

### Honua Authorization Policies

Honua defines three authorization policies (see `HonuaHostConfigurationExtensions.cs:214-219`):

```csharp
options.AddPolicy("RequireAdministrator", policy => policy.RequireRole("administrator"));
options.AddPolicy("RequireDataPublisher", policy => policy.RequireRole("administrator", "datapublisher"));
options.AddPolicy("RequireViewer", policy => policy.RequireRole("administrator", "datapublisher", "viewer"));
```

### Role Claim Path

The `roleClaimPath` configuration tells Honua where to find roles in the JWT token.

**Example JWT Payload**:

```json
{
  "iss": "https://login.microsoftonline.com/{tenant}/v2.0",
  "aud": "api://honua-server",
  "sub": "user@example.com",
  "roles": ["administrator"],
  "exp": 1735646400
}
```

**Configuration**:

```bash
HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH="roles"
```

**For Nested Claims** (e.g., Auth0 custom namespace):

```json
{
  "https://honua-api.example.com/roles": ["datapublisher"]
}
```

```bash
HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH="https://honua-api.example.com/roles"
```

---

## Testing Authentication

### Test Unauthenticated Request (Should Fail)

```bash
# Should return 401 Unauthorized
curl -v http://localhost:5000/collections

# Expected response:
# HTTP/1.1 401 Unauthorized
# WWW-Authenticate: Bearer
```

### Test Authenticated Request

```bash
# Get token (method depends on IdP)
TOKEN="eyJhbGciOiJSUzI1NiIs..."

# Make authenticated request
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/collections

# Expected response:
# HTTP/1.1 200 OK
# {
#   "collections": [...]
# }
```

### Verify Token Contents

```bash
# Decode JWT (without verification)
echo $TOKEN | cut -d. -f2 | base64 -d | jq .

# Expected output:
# {
#   "iss": "https://your-idp.com",
#   "aud": "api://honua-server",
#   "sub": "user@example.com",
#   "roles": ["administrator"],
#   "exp": 1735646400
# }

# Verify required claims:
# ✓ iss matches HONUA__AUTHENTICATION__JWT__AUTHORITY
# ✓ aud matches HONUA__AUTHENTICATION__JWT__AUDIENCE
# ✓ roles contains valid role (administrator, datapublisher, or viewer)
# ✓ exp is in the future
```

### Test Role-Based Access

```bash
# Test viewer role (read-only)
curl -H "Authorization: Bearer $VIEWER_TOKEN" http://localhost:5000/collections
# Should succeed (200 OK)

curl -H "Authorization: Bearer $VIEWER_TOKEN" -X POST http://localhost:5000/collections/parcels/items
# Should fail (403 Forbidden)

# Test datapublisher role (read-write)
curl -H "Authorization: Bearer $PUBLISHER_TOKEN" -X POST http://localhost:5000/collections/parcels/items -d '{...}'
# Should succeed (201 Created)

# Test administrator role (full access)
curl -H "Authorization: Bearer $ADMIN_TOKEN" -X DELETE http://localhost:5000/collections/parcels/items/123
# Should succeed (204 No Content)
```

---

## Troubleshooting

### Error: 401 Unauthorized

**Symptoms**: All requests return 401

**Diagnosis**:

```bash
# Check authentication mode
echo $HONUA__AUTHENTICATION__MODE  # Should be "Oidc"

# Check Honua logs
docker logs honua-server | grep -i authentication

# Verify token is being sent
curl -v -H "Authorization: Bearer $TOKEN" http://localhost:5000/collections
# Look for "Authorization: Bearer" in request headers
```

**Common Causes**:

1. **Missing Authorization Header**
   - Solution: Include `Authorization: Bearer {token}` header

2. **Malformed Token**
   - Solution: Verify token is valid JWT (has 3 parts separated by dots)

3. **Wrong Authentication Mode**
   - Solution: Set `HONUA__AUTHENTICATION__MODE=Oidc`

### Error: 401 Unauthorized (with token)

**Diagnosis**:

```bash
# Decode token to check claims
echo $TOKEN | cut -d. -f2 | base64 -d | jq .

# Check critical claims:
# 1. iss (issuer) matches authority
# 2. aud (audience) matches configured audience
# 3. exp (expiration) is in future (Unix timestamp)
```

**Common Causes**:

1. **Audience Mismatch**
   ```bash
   # Token aud: "api://wrong-audience"
   # Honua expects: "api://honua-server"
   # Solution: Fix audience in IdP or Honua config
   ```

2. **Authority Mismatch**
   ```bash
   # Token iss: "https://other-idp.com"
   # Honua expects: "https://login.microsoftonline.com/{tenant}/v2.0"
   # Solution: Fix authority in Honua config
   ```

3. **Expired Token**
   ```bash
   # Check exp claim
   date -d @1735646400  # Convert Unix timestamp
   # If in past, get new token
   ```

### Error: 403 Forbidden

**Symptoms**: Authenticated but access denied

**Diagnosis**:

```bash
# Check roles in token
echo $TOKEN | cut -d. -f2 | base64 -d | jq '.roles'

# Check role claim path configuration
echo $HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH

# Verify endpoint requirements
# GET /collections → Requires "viewer" or higher
# POST /collections/{id}/items → Requires "datapublisher" or higher
# DELETE /collections → Requires "administrator"
```

**Common Causes**:

1. **Missing Roles Claim**
   - Token doesn't include roles
   - Solution: Configure custom claim in IdP (see IdP-specific sections)

2. **Wrong Role Claim Path**
   - Honua looking for roles in wrong location
   - Solution: Set `HONUA__AUTHENTICATION__JWT__ROLECLAIMPATH` to match token structure

3. **Insufficient Permissions**
   - User has "viewer" role but trying to write data
   - Solution: Assign appropriate role in IdP

### Error: Unable to obtain configuration from authority

**Symptoms**: Honua fails to start with OIDC metadata error

**Diagnosis**:

```bash
# Test OIDC discovery endpoint
curl https://your-idp.com/.well-known/openid-configuration

# Should return JSON with keys: issuer, authorization_endpoint, token_endpoint, jwks_uri
```

**Common Causes**:

1. **Wrong Authority URL**
   - Solution: Verify authority URL in IdP documentation

2. **HTTPS Required but Using HTTP**
   - Solution: Use HTTPS or set `HONUA__AUTHENTICATION__JWT__REQUIREHTTPSMETADATA=false` (dev only)

3. **Network/Firewall Issues**
   - Solution: Verify Honua can reach IdP (check firewall, DNS)

### Enable Debug Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.Authentication": "Debug",
      "Microsoft.AspNetCore.Authorization": "Debug"
    }
  }
}
```

---

## Production Checklist

- [ ] Authentication mode set to `Oidc` or `Local` (never `QuickStart`)
- [ ] HTTPS enforced (`HONUA__AUTHENTICATION__JWT__REQUIREHTTPSMETADATA=true`)
- [ ] Audience configured and matches IdP
- [ ] Authority configured and matches IdP
- [ ] Roles mapped correctly (administrator, datapublisher, viewer)
- [ ] Role claim path configured (matches JWT structure)
- [ ] Test tokens obtained and validated
- [ ] Role-based access tested (viewer, datapublisher, administrator)
- [ ] Authentication logs reviewed (no errors)
- [ ] Token expiration configured appropriately (1 hour recommended)

---

**Last Updated**: 2025-10-04
**Honua Version**: 1.0+
**Related Documentation**: [Environment Variables](environment-variables.md), [Troubleshooting](../04-operations/troubleshooting.md)
