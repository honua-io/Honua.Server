# QuickStart Mode Security Warning

## ⚠️ CRITICAL SECURITY NOTICE

**QuickStart mode is ONLY for development and testing environments. NEVER use QuickStart mode in production.**

## What is QuickStart Mode?

QuickStart mode is a special authentication mode designed to make local development and testing easier by completely bypassing authentication and authorization checks.

## Security Implications

When QuickStart mode is enabled, your Honua server has **ZERO SECURITY**:

- ❌ **No authentication required** - Anyone can access the server
- ❌ **No authorization checks** - All users have administrator privileges
- ❌ **No access control** - All endpoints are publicly accessible
- ❌ **No audit logging** - User actions are not tracked
- ❌ **No rate limiting for auth** - Susceptible to abuse
- ❌ **No CORS enforcement** - Any origin can access the API

### What Attackers Can Do

With QuickStart mode enabled on a production server, an attacker can:

1. **Read all geospatial data** - Download your entire database
2. **Modify or delete data** - Corrupt or destroy features and layers
3. **Change metadata** - Alter service configurations
4. **Upload malicious files** - Inject harmful content
5. **Execute administrative operations** - Full system control
6. **Denial of Service** - Overload the server with requests

## Protection Mechanisms

Honua includes multiple safety mechanisms to prevent accidental production use:

### 1. Environment Validation

QuickStart mode is **automatically disabled** in production environments:

```csharp
if (app.Environment.IsProduction() && quickStartActive)
{
    throw new InvalidOperationException(
        "QuickStart authentication mode is DISABLED in production environments."
    );
}
```

### 2. Explicit Allow Flag Required

Even in non-production environments, QuickStart requires explicit opt-in:

```json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "allowQuickStart": true
    }
  }
}
```

Or via environment variable:
```bash
HONUA_ALLOW_QUICKSTART=true
```

### 3. Startup Warnings

When QuickStart mode is active, Honua logs prominent warnings:

```
[WARN] QuickStart authentication mode is ACTIVE.
       This mode bypasses authentication and should ONLY be used for development/testing.
```

## Proper Configuration for Production

### ✅ Use Local Authentication Mode

For smaller deployments or internal services:

```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "quickStart": {
        "enabled": false
      },
      "local": {
        "sessionLifetime": "08:00:00",
        "minPasswordLength": 16,
        "maxFailedAttempts": 5,
        "lockoutDuration": "00:15:00"
      }
    }
  }
}
```

### ✅ Use OIDC Authentication Mode

For production deployments with external identity providers:

```json
{
  "honua": {
    "authentication": {
      "mode": "Oidc",
      "enforce": true,
      "quickStart": {
        "enabled": false
      },
      "jwt": {
        "authority": "https://your-identity-provider.com",
        "audience": "honua-api",
        "requireHttpsMetadata": true
      }
    }
  }
}
```

## How to Verify QuickStart is Disabled

### Check Configuration Files

```bash
# Production configuration should have QuickStart disabled
grep -i "quickstart" src/Honua.Server.Host/appsettings.Production.json

# Should show:
# "enabled": false
```

### Check Docker Compose

```bash
# Production docker-compose should NOT have QuickStart enabled
grep -i "quickstart" docker/docker-compose.full.yml

# Should show:
# honua__authentication__quickStart__enabled: "false"
```

### Check Cloud Deployment Manifests

```bash
# Kubernetes/Cloud Run manifests should have proper auth configured
grep -A 5 "authentication" deploy/gcp/cloudrun-service.yaml

# Should show mode: "Local" or "Oidc", NOT "QuickStart"
```

### Runtime Verification

Test your production endpoint:

```bash
# This should return 401 Unauthorized (not 200 OK)
curl -X GET https://your-production-server.com/admin/metadata

# This should also return 401 (not a list of users)
curl -X GET https://your-production-server.com/auth/users
```

## Migration Checklist

If you're currently using QuickStart mode and need to move to production:

- [ ] Choose authentication mode (Local or OIDC)
- [ ] Configure authentication settings in appsettings.Production.json
- [ ] Set `enforce: true` to require authentication
- [ ] Set `quickStart.enabled: false` explicitly
- [ ] Create admin user account with strong password
- [ ] Test authentication endpoints return 401 without valid credentials
- [ ] Update application clients to use authentication tokens
- [ ] Remove `HONUA_ALLOW_QUICKSTART` environment variable
- [ ] Verify `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Review production deployment checklist

## References

- [Authentication Guide](../user/authentication.md)
- [Production Deployment Checklist](PRODUCTION_DEPLOYMENT_CHECKLIST.md)
- [Security Architecture](SECURITY_ARCHITECTURE.md)
- [OWASP Top 10 Assessment](OWASP_TOP_10_ASSESSMENT.md)

## Support

If you have questions about securing your Honua deployment:

1. Review the [Security Architecture documentation](SECURITY_ARCHITECTURE.md)
2. Check the [Production Deployment Checklist](PRODUCTION_DEPLOYMENT_CHECKLIST.md)
3. Consult the [Authentication Guide](../user/authentication.md)

---

**Remember: Security is not optional. Never compromise on authentication in production.**
