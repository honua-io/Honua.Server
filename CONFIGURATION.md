# Production Configuration Guide

## Required Environment Variables

### Core Configuration

```bash
# Connection Strings
ConnectionStrings__Redis="server:6379,password=SECRET,connectTimeout=5000"

# Metadata
honua__metadata__provider="json"  # or postgres, s3
honua__metadata__path="./metadata/metadata.json"

# Security
honua__security__apiKey__enabled="true"

# CORS (set actual frontend domains)
honua__cors__allowedOrigins__0="https://app.yourdomain.com"
honua__cors__allowedOrigins__1="https://www.yourdomain.com"

# Allowed Hosts
AllowedHosts="yourdomain.com,*.yourdomain.com"

# Trusted Proxies (if behind load balancer)
TrustedProxies__0="10.0.0.0/8"
TrustedProxies__1="172.16.0.0/12"
```

### Alert Receiver (if used)

```bash
# Database
ConnectionStrings__AlertHistory="Host=db;Database=alerts;Username=user;Password=SECRET"

# Authentication
Authentication__JwtSecret="[32+ character secret from: openssl rand -base64 32]"

# Alert Integrations (optional - configure as needed)
Alerting__Opsgenie__ApiKey="SECRET"
Alerting__PagerDuty__ApiKey="SECRET"
Alerts__Slack__CriticalWebhookUrl="https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
```

## Configuration Validation

The application validates configuration at startup and will fail fast with clear error messages if:
- Required settings are missing
- Security settings are insecure for production
- Connection strings point to localhost in production environment

### Main Server Validations

The main Honua server (`Honua.Server.Host`) performs these validations:

1. **Redis Connection**
   - Must be configured
   - Cannot point to localhost in Production environment

2. **Metadata Configuration**
   - Provider must be specified (json, postgres, or s3)
   - Path must be configured

3. **AllowedHosts**
   - Cannot be "*" in Production environment
   - Must specify actual domain names

4. **CORS Configuration**
   - `allowAnyOrigin` must be false in Production
   - Specific origins must be configured

5. **Service Registration**
   - Validates that critical services are registered
   - Checks for IMetadataRegistry and IDataStoreProviderFactory

### Alert Receiver Validations

The alert receiver (`Honua.Server.AlertReceiver`) validates:

1. **Database Connection**
   - AlertHistory connection string must be configured

2. **JWT Secret**
   - Must be configured
   - Must be at least 32 characters long

## Testing Configuration

Test your configuration before deploying:

```bash
# Set environment variables
export ConnectionStrings__Redis="localhost:6379"
export honua__metadata__provider="json"
export honua__metadata__path="./metadata/metadata.json"
export AllowedHosts="localhost"
# ... other vars ...

# Run with Production environment
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/Honua.Server.Host

# Should see specific validation errors or successful startup
```

### Testing with Intentionally Bad Config

To verify validation is working:

```bash
# Missing Redis - should fail with clear error
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/Honua.Server.Host

# Expected error:
# CONFIGURATION VALIDATION FAILED:
#   - ConnectionStrings:Redis is required but not configured
#   - honua:metadata:provider is required
#   - honua:metadata:path is required
```

```bash
# Production with insecure settings - should fail
export AllowedHosts="*"
export honua__cors__allowAnyOrigin="true"
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/Honua.Server.Host

# Expected error:
# CONFIGURATION VALIDATION FAILED:
#   - AllowedHosts must not be '*' in Production. Specify actual domains.
#   - CORS allowAnyOrigin must be false in Production
```

## Configuration Methods

ASP.NET Core supports multiple configuration sources (later sources override earlier ones):

1. **appsettings.json** (base)
2. **appsettings.{Environment}.json** (environment-specific)
3. **Environment variables** (recommended for secrets)
4. **Command line arguments**

### Environment Variable Syntax

Environment variables use double underscores (`__`) to represent JSON hierarchy:

```bash
# JSON: { "honua": { "metadata": { "provider": "json" } } }
# Env:  honua__metadata__provider="json"

# JSON: { "ConnectionStrings": { "Redis": "..." } }
# Env:  ConnectionStrings__Redis="..."

# Arrays use index numbers:
# JSON: { "honua": { "cors": { "allowedOrigins": ["https://a.com", "https://b.com"] } } }
# Env:  honua__cors__allowedOrigins__0="https://a.com"
#       honua__cors__allowedOrigins__1="https://b.com"
```

## Docker Configuration

### Using Environment Variables in Docker Compose

```yaml
services:
  honua-server:
    image: honua-server:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Redis=redis:6379,password=${REDIS_PASSWORD}
      - honua__metadata__provider=postgres
      - honua__metadata__path=Host=postgres;Database=honua;Username=honua;Password=${DB_PASSWORD}
      - AllowedHosts=yourdomain.com,*.yourdomain.com
      - honua__cors__allowedOrigins__0=https://app.yourdomain.com
```

### Using Secrets in Docker Swarm/Kubernetes

For production deployments, use secret management:

```yaml
# Docker Swarm secrets
secrets:
  redis_password:
    external: true
  db_password:
    external: true

services:
  honua-server:
    secrets:
      - redis_password
      - db_password
    environment:
      - ConnectionStrings__Redis=redis:6379,password=/run/secrets/redis_password
```

## Security Best Practices

1. **Never commit secrets to version control**
   - Use environment variables or secret managers
   - Keep appsettings.Production.json minimal with empty strings for secrets

2. **Generate strong secrets**
   ```bash
   # Generate JWT secret (Alert Receiver)
   openssl rand -base64 32

   # Generate API key
   openssl rand -hex 32
   ```

3. **Use TLS in production**
   - Configure HTTPS in Kestrel or use a reverse proxy
   - Set HSTS headers via reverse proxy

4. **Restrict CORS**
   - Never use `allowAnyOrigin: true` in production
   - Specify exact frontend domains

5. **Configure AllowedHosts**
   - Prevents host header injection attacks
   - List actual domains only

6. **Rate Limiting**
   - Configure appropriate limits for your use case
   - Set TrustedProxies if behind load balancer for accurate IP detection

## Troubleshooting

### Application Won't Start

Check logs for specific validation errors:

```
CONFIGURATION VALIDATION FAILED:
  - ConnectionStrings:Redis is required but not configured
```

**Solution**: Set the missing configuration value via environment variable or appsettings file.

### "Configuration validation failed with N error(s)"

The application logs each validation error. Review the output and fix all listed issues.

### Database Connection Issues

**Symptom**: Application starts but fails on first database operation

**Check**:
- Connection string format is correct
- Database server is accessible
- Credentials are valid
- Database exists

### Redis Connection Issues

**Symptom**: Caching or session features don't work

**Check**:
- Redis server is running and accessible
- Password is correct (if required)
- Network connectivity from server to Redis

### CORS Errors in Browser

**Symptom**: Frontend gets CORS errors even with CORS configured

**Check**:
- Frontend domain is in `allowedOrigins` list
- Protocol matches (http vs https)
- Port is included if non-standard
- `allowCredentials` is true if using cookies

## Configuration Checklist for Production

Before deploying to production, verify:

- [ ] AllowedHosts set to actual domain(s)
- [ ] CORS allowAnyOrigin is false
- [ ] CORS allowedOrigins lists actual frontend domains
- [ ] Redis connection string points to production instance (not localhost)
- [ ] Metadata provider and path configured
- [ ] TrustedProxies configured if behind load balancer
- [ ] Authentication settings appropriate for environment
- [ ] Secrets managed via environment variables (not in appsettings.json)
- [ ] Rate limiting configured appropriately
- [ ] Logging level appropriate (Information, not Debug)
- [ ] Application starts successfully with production config
- [ ] Health checks return healthy status

## Additional Resources

- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Environment Variables in Docker](https://docs.docker.com/compose/environment-variables/)
- [Kubernetes Secrets](https://kubernetes.io/docs/concepts/configuration/secret/)
