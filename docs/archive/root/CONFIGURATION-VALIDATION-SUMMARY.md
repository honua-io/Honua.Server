# Configuration Validation Implementation Summary

## Overview

Added comprehensive startup configuration validation to prevent production deployment failures. The application now fails fast with clear, actionable error messages when critical configuration is missing or insecure.

## Changes Made

### 1. Main Server Configuration Validation
**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Program.cs`

**Before**: 14 lines, no validation
**After**: 103 lines with comprehensive validation

#### Validations Added:

1. **Redis Connection String**
   - Ensures Redis connection is configured
   - Prevents localhost connections in Production
   - Error: `ConnectionStrings:Redis is required but not configured`
   - Error: `ConnectionStrings:Redis points to localhost in Production environment`

2. **Metadata Configuration**
   - Validates provider is set (json, postgres, s3)
   - Validates path is configured
   - Error: `honua:metadata:provider is required`
   - Error: `honua:metadata:path is required`

3. **AllowedHosts Security**
   - Prevents wildcard (*) in Production
   - Error: `AllowedHosts must not be '*' in Production. Specify actual domains.`

4. **CORS Security**
   - Prevents allowAnyOrigin in Production
   - Error: `CORS allowAnyOrigin must be false in Production`

5. **Service Registration Validation**
   - Validates IMetadataRegistry is registered
   - Validates IDataStoreProviderFactory is registered
   - Catches service configuration issues early

#### Error Reporting:

The application now:
- Logs all validation errors with CRITICAL level
- Groups all errors into a single, readable output
- Throws InvalidOperationException with error count
- Exits before attempting to start with invalid config

Example output:
```
CONFIGURATION VALIDATION FAILED:
  - ConnectionStrings:Redis is required but not configured
  - honua:metadata:provider is required
  - honua:metadata:path is required
  - AllowedHosts must not be '*' in Production. Specify actual domains.

Application cannot start. Fix configuration and try again.
```

### 2. Alert Receiver Configuration Validation
**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.AlertReceiver/Program.cs`

**Enhanced Error Messages:**

#### Database Connection (Lines 27-35)
- **Before**: Generic error message
- **After**: Specific error with:
  - What's wrong: Missing AlertHistory connection string
  - How to fix: Environment variable name (`ConnectionStrings__AlertHistory`)
  - Example value: `Host=localhost;Database=alerts;Username=user;Password=pass`

#### JWT Secret (Lines 123-140)
- **Enhanced missing secret error** with:
  - Configuration key needed
  - Environment variable format
  - How to generate: `openssl rand -base64 32`

- **Enhanced length validation** with:
  - Current length shown
  - Minimum requirement (32 characters)
  - Generation command

### 3. Production Configuration Template
**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/appsettings.Production.json`

**Enhanced with:**

1. **JSON Schema Reference**
   - Added `$schema` for IDE intellisense

2. **Inline Documentation**
   - `_Note` fields explaining required vs optional settings
   - Example values for each configuration section
   - Security warnings for critical settings

3. **Structured Organization**
   - Required fields at top (AllowedHosts, ConnectionStrings, metadata)
   - Security settings grouped together
   - Clear section comments

4. **Configuration Hints**
   - CORS: "allowAnyOrigin MUST be false in production"
   - Redis: Example connection string format
   - Metadata: Supported provider types
   - TrustedProxies: When and how to configure

### 4. Configuration Documentation
**File**: `/home/mike/projects/HonuaIO/CONFIGURATION.md`

**Comprehensive guide including:**

1. **Required Environment Variables**
   - Core server configuration
   - Alert receiver configuration
   - Formatted as copy-paste ready bash exports

2. **Configuration Validation Details**
   - What validations are performed
   - When they run
   - What happens on failure

3. **Testing Configuration**
   - How to test before deployment
   - Examples of intentionally bad config
   - Expected error outputs

4. **Environment Variable Syntax**
   - JSON to environment variable mapping
   - Array configuration examples
   - Hierarchy notation explanation

5. **Docker/Container Configuration**
   - Docker Compose examples
   - Kubernetes/Swarm secret management
   - Production deployment patterns

6. **Security Best Practices**
   - Secret generation commands
   - TLS configuration
   - CORS security
   - Rate limiting setup

7. **Troubleshooting Guide**
   - Common issues and solutions
   - How to read validation errors
   - Database/Redis connection issues
   - CORS debugging

8. **Production Checklist**
   - Pre-deployment verification steps
   - Security validation items
   - Configuration completeness check

## Benefits

### 1. Fail Fast
- Application won't start with invalid configuration
- Prevents runtime failures on first request
- Reduces MTTR (Mean Time To Repair)

### 2. Clear Error Messages
Operators get:
- Exact configuration key that's wrong
- Why it's wrong
- How to fix it (environment variable format)
- Example values where applicable

### 3. Security Enforcement
Prevents common security issues:
- Wildcard AllowedHosts in production
- CORS allowAnyOrigin in production
- Localhost connections in production
- Weak JWT secrets (< 32 characters)

### 4. Better Operator Experience
- No guessing what's wrong
- No digging through code to find config keys
- Copy-paste ready commands for secret generation
- Environment-specific validation (stricter in Production)

### 5. Comprehensive Documentation
- Single source of truth for configuration
- Examples for all deployment scenarios
- Troubleshooting guide for common issues
- Security best practices included

## Validation Flow

```
1. Application starts
   ↓
2. Load configuration from:
   - appsettings.json
   - appsettings.{Environment}.json
   - Environment variables
   ↓
3. VALIDATE CONFIGURATION
   ├─ Redis connection configured?
   ├─ Metadata provider/path set?
   ├─ AllowedHosts secure for environment?
   ├─ CORS secure for environment?
   └─ Any errors?
      ├─ YES → Log all errors, throw exception, EXIT
      └─ NO  → Continue
   ↓
4. Build application (register services)
   ↓
5. VALIDATE SERVICE REGISTRATION
   ├─ IMetadataRegistry registered?
   ├─ IDataStoreProviderFactory registered?
   └─ Any errors?
      ├─ YES → Throw exception, EXIT
      └─ NO  → Continue
   ↓
6. Configure middleware pipeline
   ↓
7. Start accepting requests
```

## Testing

### Manual Testing

```bash
# Test 1: Missing configuration
ASPNETCORE_ENVIRONMENT=Production \
dotnet run --project src/Honua.Server.Host

# Expected: Validation error listing missing configs

# Test 2: Insecure production config
ASPNETCORE_ENVIRONMENT=Production \
AllowedHosts="*" \
honua__cors__allowAnyOrigin="true" \
ConnectionStrings__Redis="localhost:6379" \
dotnet run --project src/Honua.Server.Host

# Expected: Validation errors for insecure settings

# Test 3: Valid configuration
ASPNETCORE_ENVIRONMENT=Production \
AllowedHosts="yourdomain.com" \
honua__cors__allowAnyOrigin="false" \
ConnectionStrings__Redis="prod-redis:6379,password=secret" \
honua__metadata__provider="json" \
honua__metadata__path="./metadata.json" \
dotnet run --project src/Honua.Server.Host

# Expected: Application starts successfully
```

### Automated Testing

Test script created: `/home/mike/projects/HonuaIO/test-config-validation.sh`

## Error Message Examples

### Missing Configuration
```
CONFIGURATION VALIDATION FAILED:
  - ConnectionStrings:Redis is required but not configured
  - honua:metadata:provider is required
  - honua:metadata:path is required

Application cannot start. Fix configuration and try again.
```

### Insecure Production Settings
```
CONFIGURATION VALIDATION FAILED:
  - ConnectionStrings:Redis points to localhost in Production environment
  - AllowedHosts must not be '*' in Production. Specify actual domains.
  - CORS allowAnyOrigin must be false in Production

Application cannot start. Fix configuration and try again.
```

### Alert Receiver - Missing JWT signing keys
```
CONFIGURATION ERROR: Authentication:JwtSecret (legacy) or Authentication:JwtSigningKeys are required.
Set via appsettings.json or environment variables, e.g.:
  - Authentication__JwtSigningKeys__0__KeyId=current
  - Authentication__JwtSigningKeys__0__Key=openssl rand -base64 32
  - Authentication__JwtSigningKeys__0__Active=true
```

### Alert Receiver - Weak JWT signing key
```
CONFIGURATION ERROR: Authentication:JwtSigningKeys[*].key must be at least 32 characters. Generate: openssl rand -base64 32
```

## Migration Guide for Operators

If you're upgrading from a version without validation:

1. **Review current configuration**
   - Check your appsettings.Production.json
   - Check your environment variables
   - Check your container orchestration configs

2. **Run validation test**
   ```bash
   ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/Honua.Server.Host
   ```

3. **Fix reported errors**
   - Follow the specific guidance in each error message
   - Refer to CONFIGURATION.md for detailed examples

4. **Verify services start**
   - Check logs for "Configuration validation passed" (if we add that)
   - Or successful application startup

5. **Update deployment automation**
   - Add configuration validation as deployment gate
   - Fail deployments if validation fails

## Notes

- Validation runs on every application start
- No performance impact (runs once at startup)
- Environment-aware (stricter in Production)
- Extensible (easy to add more validations)
- Does not validate all possible configurations (only critical ones)

## Future Enhancements

Potential additions:
1. Warning-level validations for recommended settings
2. Configuration schema validation (JSON schema)
3. Health check endpoints returning config validation status
4. Configuration test CLI command
5. Validation of database connectivity at startup
6. Validation of Redis connectivity at startup
7. Configuration diff tool for deployments
