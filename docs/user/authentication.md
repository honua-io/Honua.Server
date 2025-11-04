# Honua Authentication Guide

Comprehensive guide for configuring authentication and authorization in Honua.

## Table of Contents
- [Overview](#overview)
- [Authentication Modes](#authentication-modes)
- [QuickStart Mode](#quickstart-mode)
- [Local Mode](#local-mode)
- [OIDC Mode](#oidc-mode)
- [Roles and Permissions](#roles-and-permissions)
- [API Endpoints](#api-endpoints)

## Overview

Honua supports three authentication modes:

| Mode | Best For | Security Level |
|------|----------|----------------|
| **QuickStart** | Development, demos | None - **Do not use in production** |
| **Local** | Small deployments, testing | Medium - File-based user store |
| **Oidc** | Production, enterprise | High - External identity provider |

Configure the mode in `appsettings.json`:
```json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "enforce": false
    }
  }
}
```

Or via environment variable:
```bash
HONUA__AUTHENTICATION__MODE=Local
HONUA__AUTHENTICATION__ENFORCE=true
```

## Authentication Modes

### QuickStart Mode

**No authentication required** - all requests have full access.

```json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "enforce": false,
      "quickStart": {
        "enabled": true
      }
    }
  }
}
```

**Use Cases:**
- Local development
- Internal demos
- Trusted internal networks

**Security Warning:** QuickStart mode provides **zero security**. Anyone with network access can:
- Read all data
- Modify features
- Change metadata
- Delete layers

**Never use QuickStart mode in production or on public networks.**

### Local Mode

Built-in user authentication with file-based storage and JWT tokens.

```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "local": {
        "sessionLifetimeMinutes": 480,
        "storePath": "data/users",
        "signingKeyPath": "data/signing-key.pem",
        "maxFailedLoginAttempts": 5,
        "lockoutDurationMinutes": 15
      },
      "bootstrap": {
        "adminUsername": "admin",
        "adminEmail": "admin@example.com",
        "adminPassword": null
      }
    }
  }
}
```

**Configuration Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `sessionLifetimeMinutes` | JWT token expiration | 480 (8 hours) |
| `storePath` | User database directory | `data/users` |
| `signingKeyPath` | RSA signing key path | `data/signing-key.pem` |
| `maxFailedLoginAttempts` | Lockout threshold | 5 |
| `lockoutDurationMinutes` | Account lockout duration | 15 |

**Setup Steps:**

1. **Configure Local mode:**
```bash
export HONUA__AUTHENTICATION__MODE=Local
export HONUA__AUTHENTICATION__ENFORCE=true
```

2. **Bootstrap admin user:**
```bash
./scripts/honua.sh auth bootstrap --mode Local
```

Output:
```
Admin user created successfully:
  Username: admin
  Email: admin@example.com
  Password: xK9#mP2$vL4@wQ7
  Role: administrator

Store this password securely. It cannot be recovered.
```

3. **Login via API:**
```bash
curl -X POST https://localhost:5000/api/auth/local/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"xK9#mP2$vL4@wQ7"}'
```

Response:
```json
{
  "token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-10-01T16:30:00Z",
  "user": {
    "username": "admin",
    "email": "admin@example.com",
    "role": "administrator"
  }
}
```

4. **Use token in requests:**
```bash
curl https://localhost:5000/admin/metadata/snapshots \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."
```

**User Management:**

Users are stored in JSON files under `storePath` (default: `data/users/`):
```
data/users/
├── admin.json
├── viewer1.json
└── publisher1.json
```

User file format:
```json
{
  "username": "admin",
  "email": "admin@example.com",
  "passwordHash": "$2a$11$...",
  "role": "administrator",
  "createdAt": "2025-10-01T08:00:00Z",
  "lastLoginAt": "2025-10-01T09:15:00Z",
  "failedLoginAttempts": 0,
  "lockedUntil": null
}
```

**Password Requirements:**
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character

**Security Features:**
- Bcrypt password hashing (cost factor 11)
- Account lockout after failed attempts
- JWT with RSA256 signing
- Token expiration
- Auto-generated signing key (if missing)

### OIDC Mode

Integrate with external OpenID Connect identity providers.

```json
{
  "honua": {
    "authentication": {
      "mode": "Oidc",
      "enforce": true,
      "jwt": {
        "authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
        "audience": "api://honua-production",
        "roleClaimPath": "roles",
        "requireHttpsMetadata": true
      }
    }
  }
}
```

**Configuration Options:**

| Option | Description | Required |
|--------|-------------|----------|
| `authority` | OIDC issuer URL | Yes |
| `audience` | Expected JWT audience | Yes |
| `roleClaimPath` | Path to role claim | No (default: `roles`) |
| `requireHttpsMetadata` | Require HTTPS metadata | No (default: true) |

**Supported Providers:**

#### Azure Active Directory / Entra ID

```json
"jwt": {
  "authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
  "audience": "api://honua-app-id",
  "roleClaimPath": "roles",
  "requireHttpsMetadata": true
}
```

**Azure AD Setup:**
1. Register app in Azure AD
2. Add API permissions
3. Define app roles (viewer, datapublisher, administrator)
4. Assign users to roles
5. Configure audience to match app ID

**Example app role definition:**
```json
{
  "appRoles": [
    {
      "id": "...",
      "allowedMemberTypes": ["User"],
      "description": "Administrators have full access",
      "displayName": "Administrator",
      "value": "administrator"
    },
    {
      "id": "...",
      "allowedMemberTypes": ["User"],
      "description": "Data publishers can edit features",
      "displayName": "Data Publisher",
      "value": "datapublisher"
    },
    {
      "id": "...",
      "allowedMemberTypes": ["User"],
      "description": "Viewers have read-only access",
      "displayName": "Viewer",
      "value": "viewer"
    }
  ]
}
```

#### Auth0

```json
"jwt": {
  "authority": "https://{your-tenant}.auth0.com/",
  "audience": "https://honua-api",
  "roleClaimPath": "https://honua.io/roles",
  "requireHttpsMetadata": true
}
```

**Auth0 Setup:**
1. Create API in Auth0 dashboard
2. Set identifier (audience)
3. Enable RBAC
4. Add custom claims for roles
5. Create roles and assign to users

#### Keycloak

```json
"jwt": {
  "authority": "https://keycloak.example.com/realms/{realm}",
  "audience": "honua-client",
  "roleClaimPath": "realm_access.roles",
  "requireHttpsMetadata": true
}
```

**Keycloak Setup:**
1. Create realm
2. Create client (honua-client)
3. Add client roles (viewer, datapublisher, administrator)
4. Configure token mappers
5. Assign roles to users

**Custom Role Claim Path:**

If your provider uses a custom claim for roles:
```json
"roleClaimPath": "custom.nested.roles"
```

Honua will extract roles from:
```json
{
  "custom": {
    "nested": {
      "roles": ["administrator"]
    }
  }
}
```

**Client Integration:**

Clients obtain JWT from identity provider, then include in requests:

```javascript
// Example: Azure AD with MSAL.js
const authResult = await msalInstance.acquireTokenSilent({
  scopes: ["api://honua-app-id/.default"]
});

fetch('https://honua.example.com/admin/metadata/snapshots', {
  headers: {
    'Authorization': `Bearer ${authResult.accessToken}`
  }
});
```

## Roles and Permissions

Honua uses three built-in roles with hierarchical permissions:

### Viewer

**Permissions:**
- ✓ Read features (GET requests)
- ✓ Export data (GeoJSON, KML, Shapefile, etc.)
- ✓ Query OGC/Esri APIs
- ✓ View metadata
- ✗ Create/update/delete features
- ✗ Upload data
- ✗ Modify metadata
- ✗ Administrative operations

**Use Case:** Public users, read-only dashboards, external partners

### Data Publisher

**Permissions:**
- ✓ All Viewer permissions
- ✓ Create features (POST)
- ✓ Update features (PUT/PATCH)
- ✓ Delete features (DELETE)
- ✓ Add/remove attachments
- ✓ Upload data via ingestion API
- ✗ Modify metadata
- ✗ Administrative operations

**Use Case:** Field workers, data editors, content managers

### Administrator

**Permissions:**
- ✓ All Data Publisher permissions
- ✓ Modify metadata
- ✓ Create/restore snapshots
- ✓ Manage raster cache
- ✓ Run migrations
- ✓ View system metrics
- ✓ Manage users (Local mode)

**Use Case:** System administrators, DevOps, GIS managers

### Permission Matrix

| Endpoint | Viewer | Data Publisher | Administrator |
|----------|--------|----------------|---------------|
| `GET /ogc/collections/.../items` | ✓ | ✓ | ✓ |
| `POST /ogc/collections/.../items` | ✗ | ✓ | ✓ |
| `PUT /ogc/collections/.../items/{id}` | ✗ | ✓ | ✓ |
| `DELETE /ogc/collections/.../items/{id}` | ✗ | ✓ | ✓ |
| `POST /.../addAttachment` | ✗ | ✓ | ✓ |
| `POST /admin/metadata/apply` | ✗ | ✗ | ✓ |
| `POST /admin/metadata/snapshots` | ✗ | ✗ | ✓ |
| `POST /admin/raster-cache/jobs` | ✗ | ✗ | ✓ |
| `GET /metrics` | ✗ | ✗ | ✓ |

## API Endpoints

### Local Authentication

#### Login

```http
POST /api/auth/local/login
Content-Type: application/json

{
  "username": "admin",
  "password": "secret123"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-10-01T16:30:00Z",
  "user": {
    "username": "admin",
    "email": "admin@example.com",
    "role": "administrator"
  }
}
```

**Error Responses:**

**401 Unauthorized - Invalid credentials:**
```json
{
  "error": "Invalid username or password"
}
```

**423 Locked - Account locked:**
```json
{
  "error": "Account locked due to too many failed login attempts",
  "lockedUntil": "2025-10-01T08:45:00Z"
}
```

#### Get Current User

```http
GET /api/auth/user
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "username": "admin",
  "email": "admin@example.com",
  "role": "administrator",
  "authenticated": true
}
```

#### Logout

```http
POST /api/auth/logout
Authorization: Bearer {token}
```

**Response (204 No Content)**

Note: Local mode tokens cannot be revoked server-side. Logout is client-side only.

### User Management (CLI)

Create users via CLI:

```bash
# Create viewer
./scripts/honua.sh auth user create \
  --username viewer1 \
  --email viewer1@example.com \
  --role viewer \
  --password "SecurePass123!"

# Create data publisher
./scripts/honua.sh auth user create \
  --username publisher1 \
  --email publisher1@example.com \
  --role datapublisher

# List users
./scripts/honua.sh auth user list

# Reset password
./scripts/honua.sh auth user reset-password \
  --username viewer1 \
  --password "NewPass456!"

# Delete user
./scripts/honua.sh auth user delete --username viewer1
```

## Security Best Practices

### General

1. **Always use HTTPS in production**
   ```json
   "urls": "https://0.0.0.0:443"
   ```

2. **Enable enforcement**
   ```json
   "enforce": true
   ```

3. **Use strong passwords**
   - Minimum 12 characters
   - Mix of upper, lower, numbers, symbols
   - No dictionary words

4. **Rotate signing keys periodically**
   ```bash
   # Generate new signing key
   openssl genpkey -algorithm RSA -out data/signing-key-new.pem -pkeyopt rsa_keygen_bits:2048

   # Update config
   HONUA__AUTHENTICATION__LOCAL__SIGNINGKEYPATH=data/signing-key-new.pem
   ```

5. **Monitor failed login attempts**
   - Check logs for `FailedLoginAttempt` events
   - Alert on unusual patterns

### OIDC-Specific

1. **Validate token issuer**
   ```json
   "authority": "https://login.microsoftonline.com/{specific-tenant}/v2.0"
   ```

2. **Use specific audience**
   ```json
   "audience": "api://honua-production-app-id"
   ```

3. **Require HTTPS metadata**
   ```json
   "requireHttpsMetadata": true
   ```

4. **Implement token caching**
   - Cache validated tokens (5-10 minutes)
   - Reduces identity provider load

### Network Security

1. **Restrict CORS origins**
   ```json
   "cors": {
     "allowedOrigins": ["https://maps.example.com"],
     "allowCredentials": true
   }
   ```

2. **Use reverse proxy with rate limiting**
   - nginx, Caddy, or Traefik
   - Prevent brute force attacks

3. **Implement IP whitelisting** (if applicable)
   - Firewall rules
   - Network policies

## Troubleshooting

### "Unauthorized" on every request

**Check:**
1. Token in Authorization header: `Authorization: Bearer {token}`
2. Token not expired (check `expiresAt`)
3. Mode set correctly (`Local` or `Oidc`)
4. `enforce` is `true`

### "Account locked" after failed logins

**Solution:**
1. Wait for lockout duration (default: 15 minutes)
2. Or manually unlock:
```bash
# Edit user file
nano data/users/{username}.json

# Set lockedUntil to null and failedLoginAttempts to 0
{
  "lockedUntil": null,
  "failedLoginAttempts": 0
}
```

### OIDC token validation fails

**Check:**
1. Authority URL is correct
2. Audience matches JWT `aud` claim
3. Role claim path matches provider structure
4. Clock skew (< 5 minutes)

**Debug:**
Decode JWT at [jwt.io](https://jwt.io) and verify claims.

### Bootstrap fails to create admin user

**Check:**
1. `storePath` directory exists and is writable
2. No existing user with same username
3. Password meets requirements (if specified)

## See Also

- [Configuration Reference](configuration.md) - Authentication config options
- [Administrative API](admin-api.md) - Admin endpoints requiring authentication
- [CLI Reference](../cli-reference.md) - User management commands
