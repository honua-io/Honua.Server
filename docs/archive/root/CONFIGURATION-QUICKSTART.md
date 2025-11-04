# Configuration Quick Start

## Minimum Required Configuration for Production

Copy and customize these environment variables:

```bash
# Core Settings (REQUIRED)
export ASPNETCORE_ENVIRONMENT=Production
export AllowedHosts="yourdomain.com,*.yourdomain.com"

# Redis Cache (REQUIRED)
export ConnectionStrings__Redis="your-redis-server:6379,password=YOUR_REDIS_PASSWORD"

# Metadata (REQUIRED)
export honua__metadata__provider="json"
export honua__metadata__path="./metadata/production-metadata.json"

# CORS (REQUIRED for browser access)
export honua__cors__allowAnyOrigin="false"
export honua__cors__allowedOrigins__0="https://app.yourdomain.com"
export honua__cors__allowedOrigins__1="https://www.yourdomain.com"

# Trusted Proxies (REQUIRED if behind load balancer)
export TrustedProxies__0="10.0.0.0/8"
export TrustedProxies__1="172.16.0.0/12"
```

## Alert Receiver (Optional Service)

If using the alert receiver:

```bash
# Database (REQUIRED)
export ConnectionStrings__AlertHistory="Host=your-postgres;Database=alerts;Username=honua;Password=YOUR_DB_PASSWORD"

# Authentication (REQUIRED)
export Authentication__JwtSigningKeys__0__KeyId="current"
export Authentication__JwtSigningKeys__0__Key="YOUR_JWT_SECRET_HERE_MIN_32_CHARS"
export Authentication__JwtSigningKeys__0__Active=true

# Generate JWT Secret:
openssl rand -base64 32
```

## Quick Test

```bash
# 1. Set all required environment variables above

# 2. Test the application starts
dotnet run --project src/Honua.Server.Host/Honua.Server.Host.csproj

# 3. If you see validation errors, fix them following the error messages

# 4. Application should start and log "Application started"
```

## Common Validation Errors

### "ConnectionStrings:Redis is required but not configured"
**Fix**: Set `ConnectionStrings__Redis` environment variable

### "AllowedHosts must not be '*' in Production"
**Fix**: Set `AllowedHosts` to your actual domain(s)

### "CORS allowAnyOrigin must be false in Production"
**Fix**: Set `honua__cors__allowAnyOrigin="false"` and configure `allowedOrigins`

### "honua:metadata:provider is required"
**Fix**: Set `honua__metadata__provider` to "json", "postgres", or "s3"

### "honua:metadata:path is required"
**Fix**: Set `honua__metadata__path` to your metadata file location

## Docker Compose Example

```yaml
version: '3.8'

services:
  honua-server:
    image: honua-server:latest
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      AllowedHosts: "yourdomain.com,*.yourdomain.com"
      ConnectionStrings__Redis: "redis:6379,password=${REDIS_PASSWORD}"
      honua__metadata__provider: "json"
      honua__metadata__path: "./metadata/metadata.json"
      honua__cors__allowAnyOrigin: "false"
      honua__cors__allowedOrigins__0: "https://app.yourdomain.com"
      TrustedProxies__0: "10.0.0.0/8"
    volumes:
      - ./metadata:/app/metadata:ro
    depends_on:
      - redis

  redis:
    image: redis:7-alpine
    command: redis-server --requirepass ${REDIS_PASSWORD}
```

## Next Steps

1. See [CONFIGURATION.md](./CONFIGURATION.md) for comprehensive documentation
2. See [CONFIGURATION-VALIDATION-SUMMARY.md](./CONFIGURATION-VALIDATION-SUMMARY.md) for technical details
3. Review [appsettings.Production.json](./src/Honua.Server.Host/appsettings.Production.json) for all available settings
