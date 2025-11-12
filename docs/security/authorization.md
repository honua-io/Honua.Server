# Honua Server Authorization Model

## Overview

Honua Server implements a role-based access control (RBAC) system using ASP.NET Core's authorization policies. The authorization system supports two modes:

1. **Enforced Mode (Production)**: Requires authentication and enforces role-based access control
2. **QuickStart Mode (Development)**: Allows anonymous access but still validates roles for authenticated users

## Authorization Policies

The following authorization policies are configured in `/src/Honua.Server.Host/Extensions/AuthenticationExtensions.cs`:

### RequireAdministrator

**Purpose**: Grants full administrative access to all server management functions.

**Required Role**: `administrator`

**Accessible Endpoints**:
- Server administration (`/admin/server/*`)
- Alert management (`/admin/alerts/*`)
- Audit log access (`/api/admin/audit/*`)
- Feature flags (`/admin/feature-flags`)
- GeoETL workflow management (`/admin/api/geoetl/*`)
- Token revocation (`/admin/tokens/*`)
- Degradation status (`/admin/degradation/*`)
- Tracing configuration (`/admin/tracing/*`)
- Runtime configuration (`/admin/runtime-config/*`)
- Logging configuration (`/admin/logging/*`)
- Metadata administration (`/admin/metadata/*`)
- Layer group administration (`/admin/layer-groups/*`)
- Map configuration (`/admin/maps/*`)
- Geofence alerts (`/admin/geofence-alerts/*`)
- Tile cache management (`/admin/api/tiles/raster/cache/*`)
- Vector tile preseed (`/admin/api/tiles/vector/preseed`)
- RBAC management (`/admin/rbac/*`)

**Role Hierarchy**: Administrator role grants access to ALL policies (Administrator, Editor, DataPublisher, and Viewer endpoints).

### RequireEditor

**Purpose**: Grants permission to import and edit 3D/BIM data (IFC files, graphs, geometries).

**Required Roles**: `administrator` OR `editor`

**Accessible Endpoints**:
- IFC file import (`/api/ifc/*` - except `/versions` which is anonymous)
- Graph database operations (`/api/v{version}/graph/*`)
- 3D geometry operations (`/api/v{version}/geometry/3d/*`)

**Use Cases**:
- Importing Building Information Modeling (BIM) files
- Creating and managing graph relationships
- Working with 3D geometry data

**Role Hierarchy**: Administrator has full editor privileges. Editor role is independent and does NOT grant access to administrative, data publishing, or viewer endpoints.

### RequireDataPublisher

**Purpose**: Grants permission to publish and manage geospatial data.

**Required Roles**: `administrator` OR `datapublisher`

**Accessible Endpoints**:
- Data ingestion (`/admin/api/ingest/*`)
- Database migrations (`/admin/migrations/*`)

**Use Cases**:
- Publishing new datasets
- Ingesting geospatial data
- Managing database schema migrations

**Role Hierarchy**:
- Administrator has full data publisher privileges
- DataPublisher role ALSO includes Viewer privileges (can access RequireViewer endpoints)

### RequireViewer

**Purpose**: Grants read-only access to monitoring and statistics.

**Required Roles**: `administrator` OR `datapublisher` OR `viewer`

**Accessible Endpoints**:
- Tile cache statistics (`/admin/api/tiles/raster/cache/stats/*`)
- Read-only monitoring endpoints

**Use Cases**:
- Viewing system performance metrics
- Monitoring tile cache usage
- Accessing read-only administrative data

**Role Hierarchy**: This is the most permissive policy, allowing administrator, datapublisher, AND viewer roles.

## Role Hierarchy Summary

```
administrator
    ├── Full access to ALL endpoints
    ├── RequireAdministrator ✓
    ├── RequireEditor ✓
    ├── RequireDataPublisher ✓
    └── RequireViewer ✓

editor (independent)
    ├── RequireEditor ✓
    ├── RequireAdministrator ✗
    ├── RequireDataPublisher ✗
    └── RequireViewer ✗

datapublisher
    ├── RequireDataPublisher ✓
    ├── RequireViewer ✓
    ├── RequireAdministrator ✗
    └── RequireEditor ✗

viewer (lowest privileges)
    ├── RequireViewer ✓
    ├── RequireAdministrator ✗
    ├── RequireDataPublisher ✗
    └── RequireEditor ✗
```

## Authentication Modes

### Enforced Mode (Production)

When `honua:authentication:enforce` is set to `true`:

- All requests MUST be authenticated
- JWT Bearer or Local Basic authentication required
- Roles are strictly enforced
- Unauthorized requests return HTTP 401
- Forbidden requests return HTTP 403

**Configuration**:
```json
{
  "honua": {
    "authentication": {
      "enforce": true,
      "mode": "OIDC"
    }
  }
}
```

### QuickStart Mode (Development)

When `honua:authentication:enforce` is set to `false`:

- Anonymous requests are ALLOWED (bypass authentication)
- If a user IS authenticated, roles are still checked
- Useful for development and testing
- NOT recommended for production

**Configuration**:
```json
{
  "honua": {
    "authentication": {
      "enforce": false,
      "mode": "QuickStart"
    }
  }
}
```

**Important Security Note**: In QuickStart mode, authenticated users with insufficient roles will still receive HTTP 403. This prevents privilege escalation if someone authenticates with a low-privilege account.

## Configuring User Roles

### Using OIDC (Recommended for Production)

Roles are provided via JWT tokens from your identity provider:

```json
{
  "sub": "user@example.com",
  "roles": ["administrator"],
  "email": "user@example.com"
}
```

Configure your identity provider to include role claims in JWT tokens.

### Using Local Authentication (Development)

For local development, you can create users with the Local Authentication API:

**Create Administrator User**:
```bash
curl -X POST https://localhost:5001/api/auth/local/users \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin@honua.local",
    "password": "SecurePassword123!",
    "roles": ["administrator"]
  }'
```

**Create Editor User**:
```bash
curl -X POST https://localhost:5001/api/auth/local/users \
  -H "Content-Type: application/json" \
  -d '{
    "username": "editor@honua.local",
    "password": "SecurePassword123!",
    "roles": ["editor"]
  }'
```

**Create Data Publisher User**:
```bash
curl -X POST https://localhost:5001/api/auth/local/users \
  -H "Content-Type: application/json" \
  -d '{
    "username": "publisher@honua.local",
    "password": "SecurePassword123!",
    "roles": ["datapublisher"]
  }'
```

**Create Viewer User**:
```bash
curl -X POST https://localhost:5001/api/auth/local/users \
  -H "Content-Type: application/json" \
  -d '{
    "username": "viewer@honua.local",
    "password": "SecurePassword123!",
    "roles": ["viewer"]
  }'
```

**Multiple Roles**:
Users can have multiple roles for combined permissions:
```bash
curl -X POST https://localhost:5001/api/auth/local/users \
  -H "Content-Type: application/json" \
  -d '{
    "username": "poweruser@honua.local",
    "password": "SecurePassword123!",
    "roles": ["editor", "datapublisher", "viewer"]
  }'
```

## Authentication Methods

### JWT Bearer Authentication (Production)

**Header**:
```
Authorization: Bearer <jwt-token>
```

**Configuration**:
```json
{
  "honua": {
    "authentication": {
      "mode": "OIDC",
      "authority": "https://your-idp.com",
      "audience": "honua-api",
      "enforce": true
    }
  }
}
```

### Local Basic Authentication (Development)

**Header**:
```
Authorization: Basic <base64(username:password)>
```

**Example**:
```bash
# Create base64 encoded credentials
echo -n "admin@honua.local:SecurePassword123!" | base64
# Result: YWRtaW5AaG9udWEubG9jYWw6U2VjdXJlUGFzc3dvcmQxMjMh

# Use in request
curl https://localhost:5001/admin/feature-flags \
  -H "Authorization: Basic YWRtaW5AaG9udWEubG9jYWw6U2VjdXJlUGFzc3dvcmQxMjMh"
```

## Testing Authorization

Integration tests are available in `/tests/Honua.Server.Integration.Tests/Authorization/AdminAuthorizationTests.cs`.

**Run tests**:
```bash
cd tests/Honua.Server.Integration.Tests
dotnet test --filter "FullyQualifiedName~AdminAuthorizationTests"
```

**Test Coverage**:
- ✅ RequireAdministrator policy enforcement
- ✅ RequireEditor policy enforcement
- ✅ RequireDataPublisher policy enforcement
- ✅ RequireViewer policy enforcement
- ✅ QuickStart mode behavior
- ✅ Role hierarchy verification
- ✅ Unauthorized access returns 401
- ✅ Insufficient roles return 403

## Security Best Practices

### Production Deployment

1. **Always enable authentication enforcement**:
   ```json
   {
     "honua": {
       "authentication": {
         "enforce": true
       }
     }
   }
   ```

2. **Use OIDC with a trusted identity provider**:
   - Auth0
   - Azure AD
   - Okta
   - Keycloak

3. **Never use Local Authentication in production**:
   - Local auth is for development only
   - Use proper identity management

4. **Implement least privilege**:
   - Grant users only the roles they need
   - Use `viewer` for read-only access
   - Reserve `administrator` for ops team

5. **Audit admin actions**:
   - All admin endpoints are logged
   - Review audit logs regularly at `/api/admin/audit/query`

### Development

1. **Use QuickStart mode for local development**:
   ```json
   {
     "honua": {
       "authentication": {
         "enforce": false,
         "mode": "QuickStart"
       }
     }
   }
   ```

2. **Test with different roles**:
   - Create test users with each role
   - Verify authorization before deployment

3. **Run integration tests**:
   ```bash
   dotnet test --filter "FullyQualifiedName~Authorization"
   ```

## Troubleshooting

### 401 Unauthorized

**Symptom**: Requests return HTTP 401

**Causes**:
- No authentication header provided
- Invalid JWT token
- Expired JWT token
- Local user credentials are incorrect

**Solutions**:
- Verify authentication header is present
- Check JWT token validity
- Regenerate authentication token
- Verify local user credentials

### 403 Forbidden

**Symptom**: Requests return HTTP 403

**Causes**:
- User is authenticated but lacks required role
- Role claim is missing from JWT token
- Role name mismatch (case-sensitive)

**Solutions**:
- Verify user has correct role in identity provider
- Check JWT token contains role claim
- Verify role name matches exactly (lowercase)
- Grant appropriate role to user

### QuickStart Mode Not Working

**Symptom**: Anonymous requests return 401 in development

**Causes**:
- `honua:authentication:enforce` is set to `true`
- Configuration not being loaded

**Solutions**:
- Set `enforce: false` in appsettings.Development.json
- Verify configuration section name is correct
- Check environment variables don't override config

## Related Documentation

- [Authentication Configuration](./authentication.md)
- [User Management API](../api/auth-api.md)
- [Security Best Practices](./security-best-practices.md)
- [Audit Logging](./audit-logging.md)

## Change History

| Date | Version | Changes |
|------|---------|---------|
| 2025-11-11 | 1.0 | Initial documentation of authorization model |
| 2025-11-11 | 1.0 | Added RequireEditor policy and integration tests |
