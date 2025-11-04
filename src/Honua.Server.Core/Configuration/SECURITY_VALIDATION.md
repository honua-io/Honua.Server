# Security Configuration Validation

## Overview

Honua implements a comprehensive security configuration validation system that prevents insecure configurations from reaching production. The system uses a two-tier approach:

1. **Critical Security Enforcement** (`SecurityConfigurationOptionsValidator`) - Blocks application startup
2. **Security Recommendations** (`SecurityConfigurationValidator`) - Logs warnings but allows startup

## Architecture

### SecurityConfigurationOptionsValidator (IValidateOptions)

**Purpose**: Enforces critical security requirements that **MUST** be met before the application starts.

**When it runs**: Automatically invoked by ASP.NET Core options framework during configuration resolution.

**Failure behavior**: Throws `OptionsValidationException`, preventing application startup.

**Use for**:
- DoS prevention limits (page sizes, geometry limits)
- Required security metadata configuration
- Invalid configuration that would cause runtime security failures

### SecurityConfigurationValidator (ISecurityConfigurationValidator)

**Purpose**: Validates security best practices and provides recommendations.

**When it runs**: Invoked by `SecurityValidationHostedService` during application startup.

**Failure behavior**:
- Development: Logs errors/warnings, continues startup
- Production: Logs errors, throws exception if critical errors found

**Use for**:
- Environment-specific validations
- Security recommendations
- Non-critical security improvements

## Validation Matrix

### Critical Validations (ERRORS - Block Startup)

| Category | Validation | Limit | Rationale |
|----------|-----------|-------|-----------|
| **Metadata** | Provider configured | Required | Authorization rules depend on metadata |
| **Metadata** | Path configured | Required | Application cannot load security config without path |
| **OData** | MaxPageSize | ≤ 5000 | Prevents memory exhaustion DoS attacks |
| **OData** | DefaultPageSize | > 0 | Invalid configuration |
| **OData** | DefaultPageSize ≤ MaxPageSize | Must be consistent | Prevents runtime errors |
| **Geometry** | MaxGeometries | ≤ 10000 | Prevents DoS via excessive geometry processing |
| **Geometry** | MaxCoordinateCount | ≤ 1,000,000 | Prevents memory exhaustion |
| **Geometry** | Values > 0 | Required | Invalid configuration |
| **STAC** | Valid provider | sqlite, postgres, sqlserver | Prevents runtime failures |

### Warning Validations (Logged - Don't Block Startup)

Currently, all security validations are treated as errors. Future warnings may include:
- Suboptimal but valid configurations
- Performance recommendations
- Deprecated settings

## Integration

### Automatic Validation

The validation system is automatically enabled via `ConfigurationValidationExtensions`:

```csharp
services.AddConfigurationValidation();
```

This registers:
1. `SecurityConfigurationOptionsValidator` (IValidateOptions)
2. `HonuaConfigurationValidator` (IValidateOptions)
3. Other configuration validators
4. `ConfigurationValidationHostedService` (validates on startup)

### Validation Flow

```
Application Startup
    ↓
IOptionsMonitor<HonuaConfiguration>.CurrentValue accessed
    ↓
All IValidateOptions<HonuaConfiguration> validators invoked
    ↓
SecurityConfigurationOptionsValidator runs
    ↓
If validation fails → OptionsValidationException thrown
    ↓
Application startup aborted
    ↓
Detailed error messages logged
```

### ConfigurationValidationHostedService

The hosted service validates all configuration on startup:

```csharp
public Task StartAsync(CancellationToken cancellationToken)
{
    // Access configuration to trigger validation
    _ = _honuaConfig.CurrentValue;  // Invokes all IValidateOptions validators
    _ = _authConfig.CurrentValue;
    _ = _openRosaConfig.CurrentValue;

    // If validation fails, OptionsValidationException is thrown
    // Application startup is prevented
}
```

## Error Messages

### Format

All error messages follow a consistent format:

```
SECURITY: {Component} {Issue}. {Impact}. {Resolution}.
```

**Examples**:

```
SECURITY: OData MaxPageSize (10000) exceeds secure limit of 5000.
Large page sizes can cause memory exhaustion and enable DoS attacks.
Reduce 'honua:odata:maxPageSize' to <= 5000.

SECURITY: Metadata configuration is missing.
Metadata is required for proper authorization and feature access control.
Set 'honua:metadata' in appsettings.json.

SECURITY: Geometry service MaxGeometries (15000) exceeds secure limit of 10000.
This exposes the application to DoS attacks via excessive geometry processing.
Reduce 'honua:services:geometry:maxGeometries' to <= 10000.
```

### Message Components

1. **SECURITY** prefix - Immediately identifies security issues
2. **Component** - What configuration is affected (OData, Metadata, Geometry)
3. **Issue** - What's wrong with the current value
4. **Impact** - Why this is a security concern (DoS, memory exhaustion, etc.)
5. **Resolution** - Exact configuration key and recommended value

## Testing

### Unit Tests

Comprehensive unit tests in `SecurityConfigurationOptionsValidatorTests.cs`:

- ✅ Valid configuration scenarios (10+ tests)
- ✅ Metadata security (6+ tests)
- ✅ Geometry service limits (8+ tests)
- ✅ OData security (8+ tests)
- ✅ STAC provider validation (3+ tests)
- ✅ Multiple simultaneous failures
- ✅ Edge cases (disabled services, exact limits, null values)

**Total: 35+ unit tests**

### Integration Testing

To test validation in a running application:

```bash
# Test with invalid configuration
dotnet run --environment Production

# Expected: Application fails to start with detailed error messages
```

### Example Test

```csharp
[Fact]
public void Validate_WithODataMaxPageSizeExceedingLimit_ReturnsFail()
{
    var validator = new SecurityConfigurationOptionsValidator();
    var config = CreateValidConfiguration();
    config = config with
    {
        OData = new ODataConfiguration
        {
            Enabled = true,
            MaxPageSize = 10000 // Exceeds limit of 5000
        }
    };

    var result = validator.Validate(null, config);

    Assert.True(result.Failed);
    Assert.Contains(result.Failures, f =>
        f.Contains("SECURITY") &&
        f.Contains("MaxPageSize") &&
        f.Contains("10000"));
}
```

## Fail-Fast Behavior

### Development Environment

```
[Development] Configuration validation enabled
[Development] Validating application configuration...
[ERROR] Configuration validation failed. Application will not start.
[ERROR] Validation errors:
  - SECURITY: OData MaxPageSize (10000) exceeds secure limit of 5000...
  - SECURITY: Geometry MaxGeometries (15000) exceeds secure limit of 10000...
Application startup aborted.
```

### Production Environment

```
[Production] Configuration validation enabled
[Production] Validating application configuration...
[CRITICAL] Configuration validation failed. Application will not start.
[CRITICAL] SECURITY VIOLATION DETECTED
[ERROR] Validation errors:
  - SECURITY: OData MaxPageSize (10000) exceeds secure limit of 5000...
Application cannot start with insecure configuration in production.
Application terminated.
```

## Configuration Examples

### ✅ Secure Configuration

```json
{
  "honua": {
    "metadata": {
      "provider": "json",
      "path": "/app/metadata"
    },
    "odata": {
      "enabled": true,
      "defaultPageSize": 100,
      "maxPageSize": 1000
    },
    "services": {
      "geometry": {
        "enabled": true,
        "maxGeometries": 5000,
        "maxCoordinateCount": 500000
      }
    }
  }
}
```

### ❌ Insecure Configuration (Will Fail Validation)

```json
{
  "honua": {
    "metadata": null,  // FAIL: Metadata required
    "odata": {
      "enabled": true,
      "defaultPageSize": 100,
      "maxPageSize": 10000  // FAIL: Exceeds 5000 limit
    },
    "services": {
      "geometry": {
        "enabled": true,
        "maxGeometries": 50000,  // FAIL: Exceeds 10000 limit
        "maxCoordinateCount": 5000000  // FAIL: Exceeds 1000000 limit
      }
    }
  }
}
```

## Extending Validation

### Adding New Security Validations

1. **Determine severity**: Is this critical (blocks startup) or a recommendation?

2. **Critical validations**: Add to `SecurityConfigurationOptionsValidator`

```csharp
private static void ValidateNewFeature(FeatureConfiguration? feature, List<string> failures)
{
    if (feature?.DangerousSetting > SAFE_LIMIT)
    {
        failures.Add($"SECURITY: Feature DangerousSetting ({feature.DangerousSetting}) " +
                    $"exceeds secure limit of {SAFE_LIMIT}. " +
                    $"This can lead to [security impact]. " +
                    $"Set 'honua:feature:dangerousSetting' to <= {SAFE_LIMIT}.");
    }
}
```

3. **Recommendation validations**: Add to `SecurityConfigurationValidator`

```csharp
private static void ValidateFeatureRecommendations(
    FeatureConfiguration? feature,
    List<ValidationIssue> issues)
{
    if (feature?.SuboptimalSetting > RECOMMENDED_LIMIT)
    {
        issues.Add(new ValidationIssue(
            ValidationSeverity.Warning,
            "Feature",
            $"Feature SuboptimalSetting ({feature.SuboptimalSetting}) exceeds " +
            $"recommended value. Consider reducing for better performance."));
    }
}
```

4. **Add unit tests** for all scenarios (valid, invalid, edge cases)

5. **Update this documentation** with new validation rules

## Best Practices

1. **Always use SECURITY prefix** in critical validation messages
2. **Provide exact configuration paths** in error messages
3. **Explain the security impact** (DoS, memory exhaustion, etc.)
4. **Suggest specific values** in resolution guidance
5. **Test both valid and invalid** configurations
6. **Document the rationale** for each limit in code comments
7. **Version control limits** as constants for easy adjustment

## Troubleshooting

### Q: Application won't start, validation errors shown

**A**: Check the error messages for specific configuration issues. Each message includes:
- The problematic configuration value
- The secure limit
- The configuration key to fix
- The recommended value

### Q: How do I temporarily disable validation for testing?

**A**: Don't. Validation exists to protect against security vulnerabilities. Instead:
1. Fix the configuration to meet security requirements
2. Use appropriate values for your environment
3. If limits are truly too restrictive, submit an issue with justification

### Q: Can I override validation limits?

**A**: Validation limits are hardcoded for security. If you need different limits:
1. Review the security rationale in code comments
2. Understand the DoS/security implications
3. Modify the validator source code (not recommended)
4. Submit a PR with security analysis for different limits

### Q: Validation passes in development but fails in production

**A**: Some validations are environment-specific. Review:
- `SecurityValidationHostedService` environment checks
- `RuntimeSecurityConfigurationValidator` production-specific rules
- Ensure production configuration matches development testing

## References

- [ASP.NET Core Options Validation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)
- [Options Pattern in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [OWASP DoS Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Denial_of_Service_Cheat_Sheet.html)

## Version History

- **v1.0** (2025-10-18): Initial implementation with OData, Geometry, and Metadata validations
