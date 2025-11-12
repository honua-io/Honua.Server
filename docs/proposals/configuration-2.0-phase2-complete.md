// Configuration 2.0 - Phase 2 Complete

**Date**: 2025-11-11
**Status**: ✅ Phase 2 Completed
**Implementation Time**: ~1 hour (cumulative: ~3 hours)

---

## Summary

Phase 2 (Validation Engine) of the Configuration 2.0 initiative has been successfully implemented. This phase adds comprehensive validation to catch configuration errors before runtime, providing fast feedback and clear error messages.

## Deliverables ✅

All Phase 2 deliverables from the [Configuration 2.0 proposal](./configuration-2.0.md) have been completed:

### 1. Validation Result Types ✅

**Location**: `src/Honua.Server.Core/Configuration/V2/Validation/ValidationResult.cs`

Comprehensive validation result types:

- `ValidationResult` - Contains errors and warnings, with `IsValid` property
- `ValidationError` - Blocking issue with message, location, and suggestion
- `ValidationWarning` - Non-blocking issue with message, location, and suggestion
- Formatted output with `GetSummary()` method
- Helper methods: `Success()`, `Error()`, `Merge()`

### 2. Validator Interfaces ✅

**Location**: `src/Honua.Server.Core/Configuration/V2/Validation/IConfigurationValidator.cs`

Clean validator interface:

```csharp
public interface IConfigurationValidator
{
    Task<ValidationResult> ValidateAsync(HonuaConfig config, CancellationToken cancellationToken = default);
}
```

Validation options:

- `ValidationOptions.Default` - Syntax + semantics (fast)
- `ValidationOptions.SyntaxOnly` - Syntax only (fastest)
- `ValidationOptions.Full` - All validations including runtime checks (slow)

### 3. Syntax Validator ✅

**Location**: `src/Honua.Server.Core/Configuration/V2/Validation/SyntaxValidator.cs`

Validates schema correctness:

**Global Settings Validation:**
- Version format
- Environment name
- Log level values
- CORS configuration (allow_any_origin + credentials conflict)
- CORS origins when not allow_any_origin

**Data Source Validation:**
- Required fields (ID, provider, connection)
- Valid provider types (sqlite, postgresql, sqlserver, mysql, oracle)
- Connection pool settings (min/max size, timeout)
- Pool consistency (min_size <= max_size)

**Service Validation:**
- Required fields (ID, type)
- Service-specific settings validation
- OData: max_page_size, default_page_size
- OGC API: item_limit

**Layer Validation:**
- Required fields (ID, title, data_source, table, id_field)
- Geometry settings (column, type, SRID)
- Valid geometry types (Point, LineString, Polygon, Multi*, etc.)
- Field definitions when introspect_fields = false
- Valid field types (int, string, double, datetime, geometry, etc.)
- Service exposure warnings

**Cache Validation:**
- Required fields (ID, type)
- Valid cache types (redis, memory)
- Redis connection requirement

**Rate Limit Validation:**
- Store configuration
- Rule definitions
- Time window format validation (e.g., "1m", "1h", "1d")

### 4. Semantic Validator ✅

**Location**: `src/Honua.Server.Core/Configuration/V2/Validation/SemanticValidator.cs`

Validates references and consistency:

**Reference Validation:**
- Layer → Data Source references
- Layer → Service references
- Rate Limit → Cache references
- Availability suggestions for undefined references

**Consistency Validation:**
- Duplicate IDs across blocks
- Unused services (enabled but not used by layers)
- Unused data sources
- Layers with no enabled services (orphaned layers)
- Field consistency (geometry column, id field, display field in explicit fields)

**Environment-Specific Validation:**
- Production: CORS allow_any_origin warning
- Production: In-memory cache warning
- Production: Rate limiting not enabled warning
- Cache required_in enforcement

### 5. Runtime Validator ✅

**Location**: `src/Honua.Server.Core/Configuration/V2/Validation/RuntimeValidator.cs`

Validates runtime aspects (optional, slower):

**Database Connectivity:**
- Connection testing with timeout
- Health check query execution
- Clear error messages for connection failures

**Table and Column Validation:**
- Table existence checks
- Column existence for geometry, id_field, display_field
- Explicit field validation against actual schema
- Multi-database support (PostgreSQL, SQLite, SQL Server, MySQL)

**Features:**
- Configurable timeout
- Graceful failure handling
- Connection pooling
- Schema-qualified table names support

### 6. Composite Validator ✅

**Location**: `src/Honua.Server.Core/Configuration/V2/Validation/ConfigurationValidator.cs`

Orchestrates all validation phases:

**Features:**
- Sequential validation (syntax → semantic → runtime)
- Early exit on errors (doesn't proceed to next phase if previous failed)
- Configurable validation options
- Static helpers for file validation
- Async and sync API

**Usage:**

```csharp
// Validate in-memory config
var validator = new ConfigurationValidator(ValidationOptions.Default);
var result = await validator.ValidateAsync(config);

// Validate file (most common)
var result = await ConfigurationValidator.ValidateFileAsync(
    "honua.config.hcl",
    ValidationOptions.Full);

if (!result.IsValid)
{
    Console.WriteLine(result.GetSummary());
}
```

### 7. CLI Validation Tool ✅

**Location**: `src/Honua.Cli/Commands/ConfigValidateCommand.cs`

Command-line tool for validation:

**Command:**
```bash
honua config validate [path] [options]
```

**Options:**
- `[path]` - Configuration file path (default: honua.config.hcl)
- `--syntax-only` - Fast validation (syntax only)
- `--full` - Full validation including database checks
- `--timeout <seconds>` - Timeout for runtime checks (default: 10)
- `--verbose` - Detailed validation output

**Features:**
- Auto-discovery of configuration files
- Beautiful terminal output with Spectre.Console
- Colored panels for errors and warnings
- Location information for issues
- Actionable suggestions
- Exit code 0 for success, 1 for failure (CI/CD friendly)

**Example Output:**
```
Validating configuration: honua.config.hcl

╭─ ERROR at layer.roads.data_source ─────────────────╮
│ Layer references undefined data source 'prod-db'   │
│ → Suggestion: Available data sources: local-db     │
╰────────────────────────────────────────────────────╯

╭─ WARNING at service.wfs ───────────────────────────╮
│ Service 'wfs' is enabled but not used by any layers│
│ → Suggestion: Add layers or disable service        │
╰────────────────────────────────────────────────────╯

Fix the errors above and try again.
```

### 8. Comprehensive Unit Tests ✅

**Locations:**
- `tests/Honua.Server.Core.Tests/Configuration/V2/Validation/SyntaxValidatorTests.cs` - 20 tests
- `tests/Honua.Server.Core.Tests/Configuration/V2/Validation/SemanticValidatorTests.cs` - 14 tests

**Total**: 34 comprehensive unit tests

**Coverage:**
- Syntax Validator: All validation rules tested
- Semantic Validator: All reference and consistency checks tested
- Error cases and edge cases
- Warning generation
- Valid configuration scenarios

## Key Features Delivered

✅ **Three-level validation** (syntax, semantic, runtime)
✅ **Clear error messages** with location and suggestions
✅ **Fast feedback** (syntax + semantic in milliseconds)
✅ **Optional runtime validation** (database checks)
✅ **CLI tool** with beautiful output
✅ **Comprehensive test coverage** (34 tests)
✅ **CI/CD friendly** (exit codes, structured output)
✅ **Production-ready** (environment-specific warnings)

## Example Usage

### Programmatic Validation

```csharp
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration.V2.Validation;

// Load and validate config
var config = HonuaConfigLoader.Load("honua.config.hcl");
var validator = new ConfigurationValidator(ValidationOptions.Default);
var result = await validator.ValidateAsync(config);

if (!result.IsValid)
{
    Console.WriteLine("Configuration errors:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error.Message} at {error.Location}");
        if (error.Suggestion != null)
        {
            Console.WriteLine($"    Suggestion: {error.Suggestion}");
        }
    }
    Environment.Exit(1);
}

Console.WriteLine("✓ Configuration is valid");
```

### CLI Validation

```bash
# Quick validation (syntax + semantics only)
honua config validate honua.config.hcl

# Syntax-only validation (fastest)
honua config validate --syntax-only

# Full validation including database checks
honua config validate --full --timeout 30

# Auto-discover config file
honua config validate
```

### In CI/CD Pipelines

```yaml
# .github/workflows/validate-config.yml
name: Validate Configuration

on: [push, pull_request]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2

      - name: Validate configuration
        run: |
          honua config validate --syntax-only

      # Exit code 0 = success, 1 = failure
```

## Files Created

**Core Validation:**
- `src/Honua.Server.Core/Configuration/V2/Validation/ValidationResult.cs`
- `src/Honua.Server.Core/Configuration/V2/Validation/IConfigurationValidator.cs`
- `src/Honua.Server.Core/Configuration/V2/Validation/SyntaxValidator.cs`
- `src/Honua.Server.Core/Configuration/V2/Validation/SemanticValidator.cs`
- `src/Honua.Server.Core/Configuration/V2/Validation/RuntimeValidator.cs`
- `src/Honua.Server.Core/Configuration/V2/Validation/ConfigurationValidator.cs`

**CLI Tool:**
- `src/Honua.Cli/Commands/ConfigValidateCommand.cs`

**Tests:**
- `tests/Honua.Server.Core.Tests/Configuration/V2/Validation/SyntaxValidatorTests.cs`
- `tests/Honua.Server.Core.Tests/Configuration/V2/Validation/SemanticValidatorTests.cs`

**Documentation:**
- `docs/proposals/configuration-2.0-phase2-complete.md` (this document)

## Benefits Realized

### 1. Fast Feedback Loop
- Configuration errors caught in seconds, not minutes
- No need to wait for Docker builds or application startup
- Immediate feedback during development

### 2. Clear Error Messages
- Precise location information (e.g., "layer.roads.data_source")
- Actionable suggestions for fixing issues
- Available alternatives listed

### 3. Multiple Validation Levels
- **Syntax**: Fast schema validation (milliseconds)
- **Semantic**: Reference and consistency checks (milliseconds)
- **Runtime**: Database connectivity and table validation (seconds)

### 4. Production Safety
- Environment-specific warnings (production best practices)
- Security warnings (CORS, rate limiting)
- Performance warnings (caching strategy)

### 5. Developer Experience
- Beautiful CLI output with colors and panels
- Auto-discovery of configuration files
- Verbose mode for detailed information

### 6. CI/CD Integration
- Exit codes for pipeline automation
- Structured output
- Fast validation (no database required for default mode)

## Next Steps

### Phase 3: Dynamic Service Loader (3 weeks)
- `IServiceRegistration` interface for services
- Service assembly discovery and loading
- Automatic DI registration from configuration
- Automatic endpoint mapping from configuration
- Refactor existing services (OData, OGC API, WFS, etc.)
- Integration tests

### Phase 4: CLI Tooling (2 weeks)
- `honua config introspect` - Generate config from database
- `honua config plan` - Show what would be configured
- `honua config init` - Initialize new configuration with templates
- `honua config migrate` - Migrate from old format

### Phase 5: Database Introspection (2 weeks)
- Database schema readers for all providers
- Geometry column detection (PostGIS, SpatiaLite, SQL Server Spatial)
- Field type mapping
- Primary key detection
- Generate `.hcl` from existing databases

### Phase 6: Migration Tooling (1 week)
- Convert metadata.json → .honua
- Convert appsettings.json → .honua
- Analyze environment variables
- Generate equivalent configuration
- Verification tool

### Phase 7: Documentation & Examples (1 week)
- Configuration reference guide
- Validation guide
- Migration guide
- Video tutorials
- Update all documentation

## Testing

Run validation tests:

```bash
cd /home/mike/projects/Honua.Server
dotnet test --filter "FullyQualifiedName~Validation"
```

## Conclusion

Phase 2 of Configuration 2.0 has been successfully completed, adding a comprehensive validation engine with three levels of validation, a beautiful CLI tool, and extensive test coverage. The system now provides:

- ✅ Fast feedback (syntax + semantic validation in milliseconds)
- ✅ Clear, actionable error messages
- ✅ Optional runtime validation for database checks
- ✅ Beautiful CLI tool with Spectre.Console
- ✅ 34 comprehensive unit tests
- ✅ CI/CD integration support
- ✅ Production safety checks

The validation engine catches errors early, provides clear guidance, and significantly improves the developer experience.

---

**Cumulative Progress**:
- Phase 1: Configuration Parser ✅ Complete
- Phase 2: Validation Engine ✅ Complete
- Phase 3-7: Remaining

**Estimated Effort for Remaining Phases**: 9 weeks
**Priority**: High - Move to Phase 3 (Dynamic Service Loader)
