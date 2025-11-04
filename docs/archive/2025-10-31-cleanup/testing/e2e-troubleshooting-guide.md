# E2E Testing Troubleshooting Guide

This document captures common issues encountered during E2E testing of Honua deployments and their solutions. This knowledge is designed to help the Consultant diagnose and fix deployment issues automatically.

## Quick Reference: Common Issues

| Symptom | Root Cause | Solution | Doc Section |
|---------|-----------|----------|-------------|
| HTTP 401 Unauthorized | QuickStart auth not configured correctly | Set `ENFORCE=false` and conditional policies | [Auth Issues](#authentication-issues) |
| HTTP 500 on features endpoint | Invalid SQLite connection string | Remove `Version=3;Pooling=false` keywords | [Database Issues](#database-connection-issues) |
| Container starts but tests timeout | Wait loops using fixed sleep | Use active polling with health checks | [Test Timeouts](#test-timeout-issues) |
| Build changes not reflected | Docker using cached binaries | Add pre-build step before Docker | [Build Issues](#build-and-caching-issues) |
| SQL Server health check fails | Wrong sqlcmd path for 2022+ | Use `/opt/mssql-tools18/bin/sqlcmd` | [Database Issues](#database-connection-issues) |

---

## Authentication Issues

### QuickStart Mode with ENFORCE=false Not Working

**Symptom**: HTTP 401 responses even when `HONUA__AUTHENTICATION__ENFORCE=false`

**Root Cause**: Authorization policies always require authenticated users with specific roles, regardless of the Enforce setting.

**Solution**: Implement conditional authorization policies based on the Enforce flag.

**Code Location**: `src/Honua.Server.Host/Hosting/HonuaHostConfigurationExtensions.cs:204-250`

**Implementation**:
```csharp
private static void ConfigureAuthentication(WebApplicationBuilder builder)
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer();

    builder.Services.AddAuthorization(options =>
    {
        var authConfig = builder.Configuration.GetSection(HonuaAuthenticationOptions.SectionName)
            .Get<HonuaAuthenticationOptions>();
        var enforceAuth = authConfig?.Enforce ?? false;

        if (enforceAuth)
        {
            // Require authenticated users with specific roles
            options.AddPolicy("RequireViewer", policy =>
                policy.RequireRole("administrator", "datapublisher", "viewer"));
        }
        else
        {
            // Allow anonymous access when enforcement is disabled
            options.AddPolicy("RequireViewer", policy =>
                policy.RequireAssertion(context =>
                    !context.User.Identity?.IsAuthenticated == true ||
                    context.User.IsInRole("administrator") ||
                    context.User.IsInRole("datapublisher") ||
                    context.User.IsInRole("viewer")));
        }
    });
}
```

**Environment Variables**:
```bash
HONUA__AUTHENTICATION__MODE=QuickStart
HONUA__AUTHENTICATION__ENFORCE=false
```

**Testing**:
```bash
# Should return HTTP 200 (not 401)
curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/ogc
```

---

## Database Connection Issues

### SQLite Invalid Connection String Keywords

**Symptom**: HTTP 500 error on `/ogc/collections/{id}/items` endpoint

**Error Message**: `Connection string keyword 'version' is not supported`

**Root Cause**: SQLite connection strings in `metadata.json` contain invalid keywords like `Version=3;Pooling=false`

**Solution**: Use minimal connection string with absolute path for Docker compatibility.

**File Location**: `samples/ogc/metadata.json:32`

**Before**:
```json
{
  "connectionString": "Data Source=./samples/ogc/ogc-sample.db;Version=3;Pooling=false;"
}
```

**After**:
```json
{
  "connectionString": "Data Source=/app/samples/ogc/ogc-sample.db"
}
```

**Key Points**:
- Use **absolute paths** for Docker containers (e.g., `/app/samples/...`)
- Remove `Version=3` - not supported by modern SQLite providers
- Remove `Pooling=false` - not a valid SQLite keyword
- Only `Data Source` is required

### SQL Server 2022 Health Check Failures

**Symptom**: SQL Server container unhealthy, tests cannot connect

**Root Cause**: SQL Server 2022 moved `sqlcmd` to `/opt/mssql-tools18/bin/`

**Solution**: Update health check to use correct path and add `-C` flag for certificate trust.

**File Locations**:
- `tests/e2e-assistant/docker-compose/test-sqlserver.yml:13`
- `scripts/deploy-instant-ssl.sh:138`

**Correct Health Check**:
```yaml
healthcheck:
  test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "HonuaSecure123!" -Q "SELECT 1" -b -C
  interval: 10s
  timeout: 5s
  retries: 10
  start_period: 30s
```

**Key Points**:
- Use `/opt/mssql-tools18/bin/sqlcmd` (not `/opt/mssql-tools/bin/sqlcmd`)
- Add `-C` flag to trust server certificate
- Set `start_period: 30s` for SQL Server initialization time
- Increase retries to 10+ for slower systems

### MySQL Connection String Format

**Correct Format**:
```bash
Server=mysql;Database=honua;User=honua_user;Password=honua_password;
```

**Key Points**:
- Use `Server=` (not `Host=`)
- Use `User=` (not `Username=`)
- Semicolon-delimited (not space-delimited)

---

## Test Timeout Issues

### Wait Loops Timing Out on Slow Systems

**Symptom**: Tests timeout waiting for containers to become healthy, even though containers eventually start successfully.

**Root Cause**: Fixed `sleep` intervals don't account for variable startup times. Tests give up before containers are ready.

**Solution**: Replace fixed sleep loops with active polling that checks actual service health.

**File Location**: All scripts in `tests/e2e-assistant/scripts/test-*.sh`

**Before** (Fixed Sleep):
```bash
echo "Waiting 60 seconds for services to be ready..."
sleep 60

echo "Checking service health..."
curl -f http://localhost:18080/ogc || exit 1
```

**After** (Active Polling):
```bash
echo "Waiting for services to be healthy..."
MAX_WAIT=180  # 3 minutes total
ELAPSED=0
INTERVAL=5

while [ $ELAPSED -lt $MAX_WAIT ]; do
  if docker-compose ps | grep -q "unhealthy"; then
    echo "Service unhealthy at ${ELAPSED}s, waiting..."
  elif docker-compose ps | grep -q "Up"; then
    HTTP_CODE=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:18080/ogc)
    if echo "$HTTP_CODE" | grep -qE "^(200|401)$"; then
      echo "✓ Services ready after ${ELAPSED}s (HTTP $HTTP_CODE)"
      break
    fi
    echo "Service responded with HTTP $HTTP_CODE, waiting..."
  fi

  sleep $INTERVAL
  ELAPSED=$((ELAPSED + INTERVAL))
done

if [ $ELAPSED -ge $MAX_WAIT ]; then
  echo "❌ Timeout after ${MAX_WAIT}s"
  docker-compose logs
  exit 1
fi
```

**Key Points**:
- Use `docker-compose ps` to check actual container status
- Accept both HTTP 200 and 401 (401 means server is up but auth required)
- Remove `-f` flag from curl (it causes failures on 401 responses)
- Show elapsed time for debugging
- Dump logs on timeout for troubleshooting
- Adjust `MAX_WAIT` based on database type (SQLite: 60s, PostGIS: 120s, SQL Server: 180s)

### HTTP Status Code Validation

**Problem**: Using `curl -f` fails on HTTP 401, even though 401 means the server is working.

**Solution**: Accept both 200 and 401 as success indicators.

```bash
# Wrong - fails on 401
curl -f http://localhost:5000/ogc

# Right - accepts 200 or 401
HTTP_CODE=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/ogc)
echo "$HTTP_CODE" | grep -qE "^(200|401)$"
```

**Healthcheck Example**:
```yaml
healthcheck:
  test: ["CMD", "sh", "-c", "curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/ogc | grep -E '^(200|401)$'"]
  interval: 10s
  timeout: 5s
  retries: 15
```

---

## Build and Caching Issues

### Docker Containers Using Stale Binaries

**Symptom**: Code changes don't appear to take effect in Docker containers, even after rebuild.

**Root Cause**: Docker containers mount source code but use pre-built binaries from previous builds. The `--no-build` flag prevents rebuilding.

**Solution**: Add explicit build step before starting Docker containers.

**File Location**: `tests/e2e-assistant/scripts/run-all-tests.sh`

**Implementation**:
```bash
#!/bin/bash
set -e

echo "Building Honua.Server.Host in Release mode..."
dotnet build src/Honua.Server.Host -c Release

echo "Exporting PROJECT_ROOT..."
export PROJECT_ROOT=$(pwd)

echo "Running PostGIS test..."
cd tests/e2e-assistant/scripts
./test-docker-postgis.sh

# ... other tests
```

**Key Points**:
- Always build on host before starting Docker tests
- Use `dotnet build -c Release` to match Docker configuration
- Export `PROJECT_ROOT` so Docker Compose can find it
- Consider adding `--force` flag to rebuild dependencies

### Missing PROJECT_ROOT Environment Variable

**Symptom**: Docker Compose cannot find source code to mount.

**Error Message**: `ERROR: Couldn't find env file`

**Solution**: Export PROJECT_ROOT before running docker-compose.

```bash
export PROJECT_ROOT=$(pwd)
docker-compose -f test-postgis.yml up -d
```

**Docker Compose Usage**:
```yaml
volumes:
  - ${PROJECT_ROOT}:/app
```

---

## LocalStack and AWS Service Emulation

### LocalStack Timeout Issues

**Symptom**: Tests timeout at 20 seconds when using LocalStack for S3 emulation.

**Root Cause**: LocalStack + Honua + database initialization takes longer than 20s on some systems.

**Solution**: Increase timeout and add staged health checks.

**File Location**: `tests/e2e-assistant/scripts/test-localstack-s3.sh`

**Recommendations**:
1. Increase `MAX_WAIT` to 120-180 seconds
2. Add separate health check for LocalStack before starting Honua
3. Pre-create S3 bucket before starting Honua
4. Use `awslocal` CLI to verify bucket exists

**Example**:
```bash
# Wait for LocalStack first
echo "Waiting for LocalStack..."
until curl -s http://localhost:4566/_localstack/health | grep -q '"s3": "available"'; do
  sleep 2
done

# Create bucket
awslocal s3 mb s3://honua-tiles-e2e

# Now start Honua
docker-compose up -d honua
```

---

## Performance and Optimization Knowledge

### Reverse Proxy Configuration

**Nginx vs Caddy vs Traefik**:

| Feature | Nginx | Caddy | Traefik |
|---------|-------|-------|---------|
| Automatic HTTPS | No | Yes | Yes |
| Configuration | Complex | Simple | Labels |
| Let's Encrypt | Manual | Automatic | Automatic |
| Best For | Production (manual) | Quick deploy | Kubernetes |

**When to Use Each**:
- **Nginx**: Production environments with manual SSL management, requires `nginx.conf`
- **Caddy**: Fastest deployment with automatic HTTPS (nip.io compatible)
- **Traefik**: Kubernetes deployments with label-based configuration

**Port Assignments for Tests** (to avoid conflicts):
- PostGIS + Nginx: 18080
- SQL Server + Caddy: 19080
- MySQL + Traefik: 20080, 20081 (dashboard)
- LocalStack: 4566, 6000 (Honua)

### Database Performance Tuning

**Health Check Timing by Database**:
```yaml
# SQLite - Fast startup
healthcheck:
  interval: 10s
  retries: 5
  start_period: 10s

# PostGIS - Medium startup
healthcheck:
  interval: 10s
  retries: 10
  start_period: 20s

# SQL Server - Slow startup
healthcheck:
  interval: 10s
  retries: 15
  start_period: 30s
```

---

## DNS and SSL Deployment Knowledge

See also: `docs/deployment/dns-ssl-quickstart.md`

### Magic DNS for Instant Deployment

**nip.io Pattern**:
- `honua.<IP>.nip.io` resolves to `<IP>`
- Works with public and private IPs
- Zero DNS configuration required
- Compatible with Let's Encrypt for automatic SSL

**Example**:
```bash
PUBLIC_IP=$(curl -s ifconfig.me)
DOMAIN="honua.$PUBLIC_IP.nip.io"

# Caddy automatically gets SSL certificate for this domain
echo "$DOMAIN {
  reverse_proxy honua:5000
}" > Caddyfile
```

### Automatic HTTPS with Caddy

**Minimal Caddyfile**:
```
{
  email admin@example.com
}

honua.192.168.1.100.nip.io {
  reverse_proxy honua:5000
  encode gzip zstd
}
```

**Deployment Time**: ~2 minutes including SSL certificate issuance

**Key Points**:
- Caddy automatically requests Let's Encrypt certificates
- First request may take 30-60 seconds while certificate is issued
- Certificates auto-renew before expiration
- Works with nip.io, sslip.io, or custom domains

---

## Testing Best Practices

### Test Isolation

**Rule**: Each test script should use unique ports to allow parallel execution.

**Port Allocation**:
- 18000-18999: PostGIS tests
- 19000-19999: SQL Server tests
- 20000-20999: MySQL tests
- 21000-21999: SQLite tests
- 22000-22999: Kubernetes tests

### Cleanup After Tests

```bash
#!/bin/bash
set -e

# Run test
./test-something.sh

# Always cleanup (even on failure)
trap "docker-compose -f test-config.yml down -v" EXIT
```

### Logging and Debugging

**Capture Logs on Failure**:
```bash
if [ $? -ne 0 ]; then
  echo "❌ Test failed, dumping logs:"
  docker-compose logs honua
  docker-compose logs postgis
  exit 1
fi
```

**Useful Debug Commands**:
```bash
# Check container status
docker-compose ps

# Follow all logs
docker-compose logs -f

# Check specific service logs
docker-compose logs honua --tail=50

# Inspect container
docker-compose exec honua env

# Test health check manually
docker-compose exec honua curl http://localhost:5000/ogc
```

---

## Decision Trees for Consultant

### Diagnosis Flow for HTTP 401 Errors

```
HTTP 401 Unauthorized
├─ Check: Is QuickStart mode enabled?
│  ├─ No → Expected behavior, auth is enforced
│  └─ Yes → Check ENFORCE setting
│     ├─ ENFORCE=true → Expected behavior
│     └─ ENFORCE=false → BUG
│        └─ Fix: Update ConfigureAuthentication() with conditional policies
│           Location: HonuaHostConfigurationExtensions.cs:204-250
```

### Diagnosis Flow for Test Timeouts

```
Test Timeout (container not ready)
├─ Check: Are containers starting?
│  ├─ No → Docker/resource issue
│  │  └─ Check: docker-compose ps
│  └─ Yes → Check: Are they becoming healthy?
│     ├─ No → Health check failing
│     │  ├─ SQL Server → Check sqlcmd path (use tools18)
│     │  ├─ PostGIS → Check pg_isready command
│     │  └─ Application → Check logs for startup errors
│     └─ Yes (but slow) → Increase timeout
│        └─ Fix: Increase MAX_WAIT, use active polling
```

### Diagnosis Flow for Database Connection Errors

```
HTTP 500 on /ogc/collections/{id}/items
├─ Check logs for connection error
│  ├─ "keyword 'version' is not supported"
│  │  └─ SQLite connection string issue
│  │     └─ Fix: Remove Version=3;Pooling=false from metadata.json
│  ├─ "Login failed for user 'sa'"
│  │  └─ SQL Server authentication issue
│  │     └─ Fix: Check connection string, verify password
│  └─ "could not connect to server"
│     └─ PostGIS connection issue
│        └─ Fix: Verify host, port, credentials
```

---

## Environment Variable Reference

### Core Configuration
```bash
# Metadata
HONUA__METADATA__PROVIDER=json                    # or yaml
HONUA__METADATA__PATH=/app/samples/ogc/metadata.json

# Authentication
HONUA__AUTHENTICATION__MODE=QuickStart
HONUA__AUTHENTICATION__ENFORCE=false              # Critical for E2E tests

# Database - PostGIS
HONUA__DATABASE__PROVIDER=postgis
HONUA__DATABASE__CONNECTIONSTRING=Host=postgis;Database=honua;Username=honua_user;Password=honua_password

# Database - SQL Server
HONUA__DATABASE__PROVIDER=sqlserver
HONUA__DATABASE__CONNECTIONSTRING=Server=sqlserver;Database=honua;User Id=sa;Password=HonuaSecure123!;TrustServerCertificate=True

# Database - MySQL
HONUA__DATABASE__PROVIDER=mysql
HONUA__DATABASE__CONNECTIONSTRING=Server=mysql;Database=honua;User=honua_user;Password=honua_password

# Database - SQLite
HONUA__DATABASE__PROVIDER=sqlite
HONUA__DATABASE__CONNECTIONSTRING=Data Source=/data/honua.db

# Redis (optional)
HONUA__SERVICES__REDIS__ENABLED=true
HONUA__SERVICES__REDIS__CONNECTIONSTRING=redis:6379

# S3/LocalStack (optional)
HONUA__SERVICES__RASTERTILES__ENABLED=true
HONUA__SERVICES__RASTERTILES__PROVIDER=s3
HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME=honua-tiles
HONUA__SERVICES__RASTERTILES__S3__REGION=us-east-1
HONUA__SERVICES__RASTERTILES__S3__SERVICEURL=http://localhost:4566
HONUA__SERVICES__RASTERTILES__S3__ACCESSKEYID=test
HONUA__SERVICES__RASTERTILES__S3__SECRETACCESSKEY=test
HONUA__SERVICES__RASTERTILES__S3__FORCEPATHSTYLE=true
```

---

## Commit History and Fixes

This section tracks major fixes applied during E2E testing development:

1. **Conditional Authorization Policies** (commit: TBD)
   - File: `HonuaHostConfigurationExtensions.cs`
   - Issue: HTTP 401 with QuickStart + ENFORCE=false
   - Fix: Conditional policies based on Enforce flag

2. **SQLite Connection String Fix** (commit: TBD)
   - File: `samples/ogc/metadata.json`
   - Issue: HTTP 500 "keyword 'version' is not supported"
   - Fix: Removed invalid keywords, use absolute path

3. **Active Wait Loops** (commit: TBD)
   - Files: All `test-*.sh` scripts
   - Issue: Tests timing out on slow systems
   - Fix: Replace sleep with active polling

4. **SQL Server 2022 Health Check** (commit: TBD)
   - Files: `test-sqlserver.yml`, `deploy-instant-ssl.sh`
   - Issue: Health check failing
   - Fix: Use `/opt/mssql-tools18/bin/sqlcmd`

5. **PROJECT_ROOT Export** (commit: TBD)
   - Files: `run-all-tests.sh`, individual test scripts
   - Issue: Docker can't find source code
   - Fix: Export PROJECT_ROOT before docker-compose

6. **Pre-build Step** (commit: TBD)
   - File: `run-all-tests.sh`
   - Issue: Docker using stale binaries
   - Fix: Run `dotnet build` before tests

---

## Future Enhancements

### Planned Improvements
- [ ] Parallel test execution with port isolation
- [ ] Automated performance benchmarking after each test
- [ ] Integration with GitHub Actions for CI/CD
- [ ] Snapshot testing for API response validation
- [ ] Chaos engineering tests (random container kills)
- [ ] Multi-region deployment tests
- [ ] Load testing with k6 or wrk

### Known Limitations
- LocalStack tests may be flaky on systems with <4GB RAM
- SQL Server requires >2GB RAM for reliable tests
- Certificate generation with Let's Encrypt rate-limited to 5/week per domain

---

**Document Version**: 1.0
**Last Updated**: 2025-10-04
**Maintained By**: Honua Development Team

This document should be updated whenever new deployment patterns, troubleshooting techniques, or testing strategies are discovered.
