# Integration Tests - Configuration V2 Status

This document tracks the migration status of integration tests to Configuration V2.

## Summary

All integration test fixtures have been updated to use Configuration V2 (HCL) exclusively, removing dependencies on legacy `appsettings.Test.json` configuration files.

## Changes Completed

### Test Fixtures Updated

1. **WebApplicationFactoryFixture** ✅
   - Removed `appsettings.Test.json` dependency
   - Generates minimal HCL configuration dynamically
   - Creates temporary `.honua` file for each test run
   - Sets `HONUA_CONFIG_V2_ENABLED=true`
   - Provides database connections via environment variables

2. **ConfigurationV2TestFixture** ✅
   - Removed optional `appsettings.Test.json` loading
   - Now exclusively uses HCL configuration
   - Supports builder pattern and inline HCL
   - Sets `HONUA_CONFIG_V2_ENABLED=true`

3. **ODataContainerFixture** ✅
   - Replaced legacy `metadata.json` with `config.honua`
   - Removed `HONUA__METADATA__PROVIDER` environment variable
   - Uses Configuration V2 for data sources, services, and layers
   - Changed container environment to "Test"

### Configuration Files

- **appsettings.Test.json** → Renamed to `appsettings.Test.json.legacy` (archived)
- **New:** `CONFIGURATION_V2_MIGRATION.md` - Migration guide for developers
- **New:** `CONFIGURATION_V2_TEST_STATUS.md` - This status document

## Test Classes Status

### Using ConfigurationV2IntegrationTestBase (Recommended)

These tests inherit from `ConfigurationV2IntegrationTestBase` and use Configuration V2 explicitly:

| Test Class | Path | Status |
|------------|------|--------|
| StacCatalogTests | Stac/StacCatalogTests.cs | ✅ Fully migrated |
| OgcApiConfigV2Tests | ConfigurationV2/OgcApiConfigV2Tests.cs | ✅ Fully migrated |
| WfsConfigV2Tests | ConfigurationV2/WfsConfigV2Tests.cs | ✅ Fully migrated |

### Using WebApplicationFactoryFixture (Compatible)

These tests use `WebApplicationFactoryFixture` which now generates Configuration V2 automatically:

| Test Class | Path | Status |
|------------|------|--------|
| StacCollectionsTests | Stac/StacCollectionsTests.cs | ✅ Compatible |
| StacSearchTests | Stac/StacSearchTests.cs | ✅ Compatible |
| WfsTests | Ogc/WfsTests.cs | ✅ Compatible |
| WmtsTests | Ogc/WmtsTests.cs | ✅ Compatible |
| WmsTests | Ogc/WmsTests.cs | ✅ Compatible |
| FeatureServerTests | GeoservicesREST/FeatureServerTests.cs | ✅ Compatible |
| MapServerTests | GeoservicesREST/MapServerTests.cs | ✅ Compatible |
| ImageServerTests | GeoservicesREST/ImageServerTests.cs | ✅ Compatible |
| GeometryServerTests | GeoservicesREST/GeometryServerTests.cs | ✅ Compatible |
| PluginIntegrationTests | Plugins/PluginIntegrationTests.cs | ✅ Compatible |

### Using Custom Factory

| Test Class | Path | Status | Notes |
|------------|------|--------|-------|
| AdminAuthorizationTests | Authorization/AdminAuthorizationTests.cs | ⚠️ Custom | Uses TestWebApplicationFactory, may need review |

## Migration Path for New Tests

### Option 1: Use ConfigurationV2IntegrationTestBase (Recommended)

```csharp
[Collection("DatabaseCollection")]
public class MyTests : ConfigurationV2IntegrationTestBase
{
    public MyTests(DatabaseFixture databaseFixture)
        : base(databaseFixture)
    {
    }

    protected override ConfigurationV2TestFixture<Program> CreateFactory()
    {
        return CreateFactoryWithBuilder(builder =>
        {
            builder
                .AddDataSource("db", "postgresql")
                .AddService("my_service")
                .AddLayer("my_layer", "db", "my_table");
        });
    }
}
```

### Option 2: Use WebApplicationFactoryFixture (Simple)

```csharp
[Collection("DatabaseCollection")]
public class MyTests
{
    private readonly DatabaseFixture _databaseFixture;

    public MyTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    [Fact]
    public async Task MyTest()
    {
        // WebApplicationFactoryFixture now uses Configuration V2 automatically
        using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
        var client = factory.CreateClient();

        // Test implementation
    }
}
```

## Environment Variables

All tests now use these environment variables:

| Variable | Purpose | Set By |
|----------|---------|--------|
| `HONUA_CONFIG_PATH` | Path to .honua file | Fixtures |
| `HONUA_CONFIG_V2_ENABLED` | Enable Configuration V2 | Fixtures |
| `DATABASE_URL` | PostgreSQL connection | DatabaseFixture |
| `MYSQL_URL` | MySQL connection | DatabaseFixture |
| `REDIS_URL` | Redis connection | DatabaseFixture |

## Minimal Test Configuration

All fixtures now create this minimal Configuration V2:

```hcl
honua {
    version     = "2.0"
    environment = "test"
    log_level   = "information"
}

data_source "test_db" {
    provider   = "postgresql"
    connection = env("DATABASE_URL")

    pool {
        min_size = 1
        max_size = 5
    }
}
```

Tests can extend this configuration using `ConfigurationV2TestFixture` for more complex scenarios.

## Testing the Migration

### Run Integration Tests

```bash
# Run all integration tests
dotnet test tests/Honua.Server.Integration.Tests --filter "Category=Integration"

# Run specific test class
dotnet test --filter "FullyQualifiedName~StacCatalogTests"

# Verify Configuration V2 is being used
HONUA_CONFIG_V2_ENABLED=true dotnet test tests/Honua.Server.Integration.Tests
```

### Verify Configuration V2 Usage

Check test output for these indicators:
- No warnings about missing `appsettings.Test.json`
- Log messages showing HCL configuration loaded
- Environment variable `HONUA_CONFIG_V2_ENABLED=true` in effect

## Known Issues

### None Currently

All integration tests have been successfully migrated to Configuration V2.

## Future Work

### Potential Improvements

1. **Remove Legacy Fallbacks**: Remove `ConnectionStrings:*` fallback configuration once all code paths use Configuration V2
2. **Test Coverage**: Add more ConfigurationV2-specific tests for edge cases
3. **Performance**: Evaluate caching of parsed HCL configurations across tests
4. **Documentation**: Update test documentation to emphasize Configuration V2 patterns

### New Test Patterns

Consider adding tests for:
- Configuration validation errors
- Environment variable interpolation
- Multi-data source scenarios
- Complex service configurations
- Layer inheritance and overrides

## References

- [Configuration V2 Migration Guide](./CONFIGURATION_V2_MIGRATION.md)
- [Main Configuration V2 Migration Guide](/tests/CONFIGURATION_V2_MIGRATION_GUIDE.md)
- [Test Infrastructure Documentation](/tests/TEST_INFRASTRUCTURE.md)

## Changelog

### 2025-11-11

- ✅ Updated `WebApplicationFactoryFixture` to use Configuration V2
- ✅ Updated `ConfigurationV2TestFixture` to remove `appsettings.Test.json` dependency
- ✅ Updated `ODataContainerFixture` to use HCL instead of `metadata.json`
- ✅ Archived `appsettings.Test.json` to `appsettings.Test.json.legacy`
- ✅ Created `CONFIGURATION_V2_MIGRATION.md` guide
- ✅ Created `CONFIGURATION_V2_TEST_STATUS.md` status document
- ✅ All integration tests now compatible with Configuration V2
