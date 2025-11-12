# Integration Tests - Configuration V2 Migration

This document describes the migration of integration tests from legacy JSON configuration to Configuration V2 (HCL).

## Changes Made

### 1. WebApplicationFactoryFixture

**Before:** Required `appsettings.Test.json` file with ConnectionStrings and Features settings.

**After:** Creates minimal HCL configuration dynamically:
- Generates temporary `.honua` configuration file
- Provides Configuration V2 via `HONUA_CONFIG_PATH` environment variable
- Sets `HONUA_CONFIG_V2_ENABLED=true`
- Environment variables for connection strings (`DATABASE_URL`, `MYSQL_URL`, `REDIS_URL`)

**Minimal Configuration:**
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

### 2. ConfigurationV2TestFixture

**Before:** Loaded `appsettings.Test.json` as optional fallback.

**After:** Relies exclusively on HCL configuration:
- Removed `appsettings.Test.json` dependency
- Sets `HONUA_CONFIG_V2_ENABLED=true` flag
- Provides environment variables for database connections
- Maintains legacy fallback connection strings for compatibility

**Usage Example:**
```csharp
using var factory = new ConfigurationV2TestFixture<Program>(DatabaseFixture, builder =>
{
    builder
        .AddDataSource("test_db", "postgresql", "DATABASE_URL")
        .AddService("ogc_api", new() { ["item_limit"] = 1000 })
        .AddLayer("test_features", "test_db", "test_table");
});
```

### 3. ODataContainerFixture

**Before:** Used legacy `metadata.json` with JsonMetadataProvider.

**After:** Uses Configuration V2 HCL:
- Replaced `metadata.json` with `config.honua`
- Removed `HONUA__METADATA__PROVIDER` environment variable
- Sets `HONUA_CONFIG_PATH` pointing to HCL file
- Container environment changed from "Development" to "Test"

**HCL Configuration:**
```hcl
honua {
    version     = "2.0"
    environment = "test"
}

data_source "sqlite_primary" {
    provider   = "sqlite"
    connection = env("DATABASE_URL")
}

service "odata" {
    enabled = true
}

layer "roads_primary" {
    title       = "Primary Roads"
    data_source = data_source.sqlite_primary
    table       = "roads_primary"
    id_field    = "road_id"

    geometry {
        column = "geom"
        type   = "LineString"
        srid   = 4326
    }

    services = [service.odata, service.ogc_api]
}
```

## Test Base Classes

### ConfigurationV2IntegrationTestBase

All new integration tests should inherit from `ConfigurationV2IntegrationTestBase`:

```csharp
[Collection("DatabaseCollection")]
public class MyIntegrationTests : ConfigurationV2IntegrationTestBase
{
    public MyIntegrationTests(DatabaseFixture databaseFixture)
        : base(databaseFixture)
    {
    }

    protected override ConfigurationV2TestFixture<Program> CreateFactory()
    {
        return CreateFactoryWithHcl(CreateOgcApiConfiguration());
    }

    [Fact]
    public async Task MyTest_Should_Work()
    {
        // Test implementation
    }
}
```

### Helper Methods Available

- `CreateStacConfiguration()` - Minimal STAC service configuration
- `CreateOgcApiConfiguration()` - OGC API Features configuration
- `CreateWfsConfiguration()` - WFS service configuration
- `CreateFactoryWithBuilder()` - Use TestConfigurationBuilder
- `CreateFactoryWithHcl()` - Inline HCL string

## Environment Variables

Tests use these environment variables for configuration:

| Variable | Purpose | Example |
|----------|---------|---------|
| `HONUA_CONFIG_PATH` | Path to .honua config file | `/tmp/test-abc123.honua` |
| `HONUA_CONFIG_V2_ENABLED` | Enable Configuration V2 | `true` |
| `DATABASE_URL` | PostgreSQL connection | `Host=localhost;Port=5432;...` |
| `MYSQL_URL` | MySQL connection | `Server=localhost;Port=3306;...` |
| `REDIS_URL` | Redis connection | `localhost:6379` |

## Legacy Configuration

The old `appsettings.Test.json` file has been renamed to `appsettings.Test.json.legacy` for reference.

**Do not use this file in new tests.** It is kept only for documentation purposes.

## Migration Checklist

When migrating existing tests to Configuration V2:

- [ ] Change test base class to `ConfigurationV2IntegrationTestBase`
- [ ] Replace `WebApplicationFactoryFixture<Program>` with `ConfigurationV2TestFixture<Program>`
- [ ] Implement `CreateFactory()` method with HCL configuration
- [ ] Remove any references to `appsettings.Test.json`
- [ ] Use environment variables for connection strings
- [ ] Test with `HONUA_CONFIG_V2_ENABLED=true`

## Example: Complete Test Class

```csharp
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Xunit;

namespace Honua.Server.Integration.Tests.MyFeature;

[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
public class MyFeatureTests : ConfigurationV2IntegrationTestBase
{
    public MyFeatureTests(DatabaseFixture databaseFixture)
        : base(databaseFixture)
    {
    }

    protected override ConfigurationV2TestFixture<Program> CreateFactory()
    {
        // Option 1: Use builder pattern
        return CreateFactoryWithBuilder(builder =>
        {
            builder
                .AddDataSource("db", "postgresql")
                .AddService("my_service", new() { ["enabled"] = true })
                .AddLayer("my_layer", "db", "my_table");
        });

        // Option 2: Use inline HCL
        var hclConfig = """
        honua {
            version = "2.0"
            environment = "test"
        }

        data_source "db" {
            provider = "postgresql"
            connection = env("DATABASE_URL")
        }

        service "my_service" {
            enabled = true
        }
        """;

        return CreateFactoryWithHcl(hclConfig);
    }

    [Fact]
    public async Task MyEndpoint_Should_Return_Success()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/my-endpoint");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

## Benefits of Configuration V2

1. **Type Safety**: HCL configuration is validated at load time
2. **Composability**: Easy to build complex configurations programmatically
3. **Environment Variables**: Native support for `env()` function
4. **No JSON Drift**: Single source of truth for configuration
5. **Better Testing**: Configuration is explicit and testable
6. **Declarative**: Clear intent of what services/layers are configured

## Troubleshooting

### Configuration Not Loading

Check that:
- `HONUA_CONFIG_V2_ENABLED=true` is set
- `HONUA_CONFIG_PATH` points to valid `.honua` file
- Environment variables (`DATABASE_URL`, etc.) are set correctly

### Tests Failing with 404

Ensure:
- Services are marked as `enabled = true` in HCL
- Layers reference the correct services: `services = [service.my_service]`
- Data sources are properly configured with connection strings

### Legacy Configuration Conflicts

If you see warnings about legacy configuration:
- Remove any `config.AddJsonFile("appsettings.Test.json")` calls
- Use only Configuration V2 fixtures
- Verify no tests reference `appsettings.Test.json`

## Contact

For questions about Configuration V2 migration, see:
- `/tests/CONFIGURATION_V2_MIGRATION_GUIDE.md`
- `/docs/configuration-v2.md`
