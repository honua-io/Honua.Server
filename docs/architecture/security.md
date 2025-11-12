# Security Guide

Comprehensive security guidelines for deploying and operating Honua Server.

## Security Model

Honua implements defense-in-depth security:

1. **Authentication** - Verify user identity
2. **Authorization** - Role-based access control (RBAC)
3. **Network Security** - TLS, rate limiting, CORS
4. **Input Validation** - SQL injection prevention, XSS protection
5. **Secure Configuration** - Minimal privileges, secure defaults
6. **Audit Logging** - Track security events

## Authentication Modes

### QuickStart Mode (Development Only)

**⚠️ NEVER use in production!**

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

- Bypasses all authentication
- Requires `HONUA_ALLOW_QUICKSTART=true` environment variable
- Automatically disabled in production environments
- Used for local development and testing only

### Local Authentication (Recommended for Small Deployments)

Uses database-backed user accounts with password hashing:

```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "local": {
        "enabled": true,
        "tokenExpirationMinutes": 60,
        "requirePasswordComplexity": true
      }
    }
  }
}
```

**Password Requirements:**
- Minimum 12 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character
- Passwords hashed using PBKDF2 with 100,000 iterations

**Creating Users:**
```bash
# Via API (requires Administrator role)
POST /auth/users
{
  "username": "alice",
  "password": "StrongPassword123!",
  "role": "datapublisher"
}
```

### OIDC Authentication (Recommended for Enterprise)

Integrate with identity providers (Azure AD, Auth0, Keycloak):

```json
{
  "honua": {
    "authentication": {
      "mode": "OIDC",
      "enforce": true,
      "oidc": {
        "authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
        "clientId": "your-client-id",
        "clientSecret": "your-client-secret",
        "scope": "openid profile email",
        "roleClaim": "roles"
      }
    }
  }
}
```

**Role Mapping:**
Map OIDC claims to Honua roles in `roleClaim` configuration.

## Role-Based Access Control (RBAC)

### Roles

| Role | Permissions |
|------|-------------|
| **Viewer** | Read-only access to data, metadata, and maps |
| **DataPublisher** | Read/write data, manage metadata, publish services |
| **Administrator** | Full access including runtime configuration, user management |

### Endpoint Authorization

```
# Public (no authentication required)
GET /ogc                           # Landing page
GET /healthz/*                     # Health checks
GET /swagger                       # API documentation

# Viewer Role Required
GET /odata/*                       # OData queries
GET /wms, /wfs, /wmts, /wcs, /csw  # OGC services
GET /stac/*                        # STAC catalog

# DataPublisher Role Required
POST /odata/*                      # Create data
PUT /odata/*                       # Update data
DELETE /odata/*                    # Delete data
POST /wfs (Transaction)            # WFS-T operations
POST /admin/metadata/*             # Metadata management
POST /admin/ingestion/*            # Data ingestion

# Administrator Role Required
GET /admin/config/*                # Runtime configuration
PATCH /admin/config/*              # Change configuration
GET /admin/logging/*               # Logging configuration
PATCH /admin/logging/*             # Change log levels
POST /auth/users                   # User management
```

## Network Security

### TLS/HTTPS

**Production Requirement:** Always use TLS in production.

#### Option 1: Reverse Proxy (Recommended)

Use Nginx, Traefik, or load balancer for TLS termination:

```nginx
server {
    listen 443 ssl http2;
    server_name honua.example.com;

    ssl_certificate /etc/ssl/certs/honua.crt;
    ssl_certificate_key /etc/ssl/private/honua.key;

    # Strong SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers 'ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256';
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    location / {
        proxy_pass http://localhost:5000;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Host $host;
    }
}
```

#### Option 2: Kestrel TLS

Configure TLS directly in Kestrel (development/simple deployments):

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:5001",
        "Certificate": {
          "Path": "/app/certs/certificate.pfx",
          "Password": "your-certificate-password"
        }
      }
    }
  }
}
```

### CORS Policy

Configure per-service in metadata.json:

```json
{
  "services": [
    {
      "id": "my-service",
      "cors": {
        "allowedOrigins": [
          "https://app.example.com",
          "https://map.example.com"
        ],
        "allowedMethods": ["GET", "POST"],
        "allowedHeaders": ["Authorization", "Content-Type"],
        "allowCredentials": true,
        "maxAge": 3600
      }
    }
  ]
}
```

**Security Best Practices:**
- Never use `*` for `allowedOrigins` in production
- Only allow necessary methods
- Set `allowCredentials: true` only if required
- Use specific origins, not wildcards

### Rate Limiting

Protect against DoS attacks:

```json
{
  "RateLimiting": {
    "PermitLimit": 100,
    "Window": "00:01:00",
    "QueueLimit": 10
  }
}
```

**Per-IP rate limiting** is automatically applied to admin endpoints.

## Input Validation

### SQL Injection Prevention

Honua uses **parameterized queries** exclusively:

```csharp
// Safe - parameterized
command.CommandText = "SELECT * FROM features WHERE id = @id";
command.Parameters.AddWithValue("@id", userInput);

// NEVER directly concatenate user input (prevented by design)
```

### XSS Prevention

- All output is properly encoded
- Content-Security-Policy headers set
- User input sanitized before storage

### Path Traversal Prevention

File operations validate paths:

```csharp
// Validates path is within allowed directory
var safePath = Path.GetFullPath(Path.Combine(baseDir, userPath));
if (!safePath.StartsWith(baseDir))
    throw new SecurityException("Invalid path");
```

## Secure Configuration

### Secrets Management

**DO NOT** commit secrets to version control.

#### Option 1: Environment Variables

```bash
export POSTGRES_PASSWORD="strong-random-password"
export OIDC_CLIENT_SECRET="oidc-secret"
```

#### Option 2: Docker Secrets

```yaml
services:
  honua:
    secrets:
      - postgres_password
      - oidc_client_secret
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/postgres_password

secrets:
  postgres_password:
    external: true
  oidc_client_secret:
    external: true
```

#### Option 3: Azure Key Vault / AWS Secrets Manager

```json
{
  "KeyVault": {
    "VaultUri": "https://your-vault.vault.azure.net/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
  }
}
```

### Minimal Privileges

#### Database User

```sql
-- Create dedicated user with minimal permissions
CREATE USER honua_app WITH PASSWORD 'strong-password';

-- Grant only necessary permissions
GRANT CONNECT ON DATABASE honua TO honua_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO honua_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO honua_app;

-- Revoke dangerous permissions
REVOKE CREATE ON SCHEMA public FROM honua_app;
REVOKE DROP ON ALL TABLES IN SCHEMA public FROM honua_app;
```

#### Container User

Dockerfile uses non-root user:

```dockerfile
USER app
```

Never run containers as root in production.

### Security Headers

Automatically applied by `SecureHeaders` middleware:

```
Strict-Transport-Security: max-age=31536000; includeSubDomains
X-Frame-Options: SAMEORIGIN
X-Content-Type-Options: nosniff
X-XSS-Protection: 1; mode=block
Content-Security-Policy: default-src 'self'
Referrer-Policy: strict-origin-when-cross-origin
```

## Audit Logging

Security-relevant events are logged:

```
[Information] User 'alice' authenticated successfully (role: DataPublisher)
[Warning] Failed login attempt for user 'admin' from 192.168.1.100
[Information] User 'bob' created layer 'parcels' (service: cadastral)
[Warning] Rate limit exceeded for IP 10.0.0.50
[Critical] QuickStart mode attempted in production environment (blocked)
```

**Log Retention:**
- Retain security logs for minimum 90 days
- Consider longer retention for compliance

## Vulnerability Management

### Keeping Dependencies Updated

```bash
# Check for outdated packages
dotnet list package --outdated

# Check for vulnerable packages
dotnet list package --vulnerable --include-transitive

# Update packages
dotnet add package <PackageName> --version <LatestVersion>
```

### Security Scanning

#### Container Image Scanning

```bash
# Scan with Trivy
trivy image honua-server:latest

# Scan with Docker Scout
docker scout cves honua-server:latest
```

#### SAST (Static Application Security Testing)

```bash
# Using Security Code Scan
dotnet add package SecurityCodeScan.VS2019
dotnet build

# Using SonarQube
dotnet sonarscanner begin /k:"honua-server"
dotnet build
dotnet sonarscanner end
```

### Responsible Disclosure

Report security vulnerabilities to: security@honua.io (placeholder)

**DO NOT** create public GitHub issues for security vulnerabilities.

## Incident Response

### Suspected Breach Checklist

1. **Isolate affected systems**
   ```bash
   docker compose down
   ```

2. **Collect logs**
   ```bash
   docker compose logs > incident-logs.txt
   ```

3. **Check for unauthorized access**
   ```sql
   SELECT * FROM auth_log WHERE timestamp > NOW() - INTERVAL '24 hours';
   ```

4. **Rotate credentials**
   - Change database passwords
   - Revoke and reissue JWT signing keys
   - Rotate API keys/secrets

5. **Update and patch**
   - Apply security updates
   - Scan for vulnerabilities
   - Restart with hardened configuration

6. **Post-incident review**
   - Document timeline
   - Identify root cause
   - Implement preventive measures

## Compliance

### GDPR Considerations

- User data is encrypted at rest (database encryption)
- User data is encrypted in transit (TLS)
- Support for data export (OData, CSV, GeoPackage)
- Support for data deletion (DELETE endpoints)
- Audit logs track data access

### HIPAA/PCI-DSS

Honua is not HIPAA/PCI-DSS certified. Additional controls required:
- Encrypt database at rest
- Implement network segmentation
- Add intrusion detection
- Enhanced audit logging
- Regular penetration testing

## Security Checklist

Before deploying to production:

- [ ] Disable QuickStart authentication mode
- [ ] Configure Local or OIDC authentication
- [ ] Set `enforce: true` in authentication config
- [ ] Change all default passwords
- [ ] Enable TLS/HTTPS
- [ ] Configure restrictive CORS policies
- [ ] Set appropriate rate limits
- [ ] Use minimal database permissions
- [ ] Run containers as non-root user
- [ ] Scan for vulnerable dependencies
- [ ] Enable security headers
- [ ] Configure audit logging
- [ ] Set up log aggregation and monitoring
- [ ] Implement backup and recovery procedures
- [ ] Document incident response procedures
- [ ] Perform security testing
- [ ] Review and approve security configuration

## Next Steps

- [Deployment Guide](./DEPLOYMENT.md)
- [Resilience Patterns](./RESILIENCE.md)
- [API Documentation](./API_DOCUMENTATION.md)
