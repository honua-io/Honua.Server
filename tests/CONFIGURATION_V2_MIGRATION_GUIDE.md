# Configuration V2 Migration Guide for Tests

**Copyright (c) 2025 HonuaIO**
**Licensed under the Elastic License 2.0**

**Last Updated:** 2025-11-11
**Version:** 2.0

---

## Table of Contents

1. [Overview](#overview)
2. [Why Migrate to Configuration V2](#why-migrate-to-configuration-v2)
3. [Migration Strategy](#migration-strategy)
4. [Step-by-Step Migration](#step-by-step-migration)
5. [Before and After Examples](#before-and-after-examples)
6. [Configuration Patterns](#configuration-patterns)
7. [Benefits and Tradeoffs](#benefits-and-tradeoffs)
8. [Troubleshooting](#troubleshooting)

---

## Overview

**IMPORTANT: Configuration V2 is now the only supported configuration system. Legacy configuration has been removed.**

Configuration V2 uses **declarative HCL-based configuration** for Honua.Server. This guide helps you migrate existing tests to use Configuration V2.

### What is Configuration V2?

Configuration V2 uses HCL (HashiCorp Configuration Language) to define:
- Data sources (PostgreSQL, MySQL, DuckDB, SQLite)
- Services (WFS, WMS, WMTS, OGC API Features, STAC, etc.)
- Layers (spatial data layers)
- Caches (Redis, in-memory)

### Migration Status

- **Legacy configuration system:** REMOVED
- **Configuration V2:** MANDATORY for all tests
- **All tests must use:** ConfigurationV2TestFixture or TestConfigurationBuilder

---

## Why Migrate to Configuration V2

### 1. Declarative Configuration

**Old (Imperative):**
```csharp
services.AddWfs(options =>
{
    options.Version = "2.0.0";
    options.MaxFeatures = 10000;
});
```

**New (Declarative):**
```hcl
service "wfs" {
  enabled = true
  version = "2.0.0"
  max_features = 10000
}
```

### 2. Production Parity

Tests using Configuration V2 match production configuration format, making tests more realistic and catching configuration issues early.

### 3. Easier Configuration Management

**Builder Pattern:**
```csharp
builder
    .AddDataSource("db", "postgresql")
    .AddService("wfs", new() { ["version"] = "2.0.0" })
    .AddLayer("features", "db", "features_table");
```

### 4. Better Test Isolation

Each test gets its own isolated configuration, preventing test interference.

### 5. Full Service Integration

Configuration V2 tests verify that services correctly parse and apply HCL configuration, testing the entire configuration pipeline.

---

## Migration Strategy

### When to Migrate

**Migrate Now:**
- New tests (always use Configuration V2)
- Tests that are actively being modified
- Tests that are failing frequently
- Integration tests for Configuration V2-enabled features

**Migrate Later:**
- Stable, passing unit tests
- Tests that don't involve configuration
- Tests scheduled for refactoring

### Migration Approach

**Option 1: Gradual Migration**
- Migrate one test file at a time
- Keep old and new tests side-by-side
- Verify new tests pass before removing old ones

**Option 2: Parallel Tests**
- Create new ConfigV2 test classes alongside existing tests
- Run both until confident
- Remove old tests when ready

**Recommended:** Option 2 (Parallel Tests)

---

## Step-by-Step Migration

### Step 1: Identify Test to Migrate

Look for tests using:
- `WebApplicationFactoryFixture<Program>` without Configuration V2
- Manual service configuration in `ConfigureTestServices`
- `appsettings.Test.json` overrides

**Example Old Test:**
```csharp
public class WfsTests : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task GetCapabilities_ReturnsXml()
    {
        using var factory = new WebApplicationFactoryFixture<Program>(_db);
        var client = factory.CreateClient();
        // ...
    }
}
```

### Step 2: Create ConfigV2 Test File

Create a new test file with `ConfigV2` suffix:

```
Before: Ogc/WfsTests.cs
After:  ConfigurationV2/WfsConfigV2Tests.cs
```

### Step 3: Update Class Declaration

**Before:**
```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "WFS")]
public class WfsTests : IClassFixture<DatabaseFixture>
{
}
```

**After:**
```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "ConfigurationV2")]  // Add this trait
[Trait("Endpoint", "WFS")]         // Use "Endpoint" instead of "API"
public class WfsConfigV2Tests : IClassFixture<DatabaseFixture>
{
}
```

### Step 4: Replace WebApplicationFactoryFixture

**Before:**
```csharp
using var factory = new WebApplicationFactoryFixture<Program>(_db);
var client = factory.CreateClient();
```

**After (Builder Pattern):**
```csharp
using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
{
    builder
        .AddDataSource("db", "postgresql")
        .AddService("wfs", new() { ["version"] = "2.0.0" })
        .AddLayer("features", "db", "features_table");
});
var client = factory.CreateClient();
```

**After (Inline HCL):**
```csharp
var hcl = @"
honua {
  version = ""1.0""
  environment = ""test""
}

data_source ""db"" {
  provider = ""postgresql""
  connection = env(""DATABASE_URL"")
}

service ""wfs"" {
  enabled = true
  version = ""2.0.0""
}
";

using var factory = new ConfigurationV2TestFixture<Program>(_db, hcl);
var client = factory.CreateClient();
```

### Step 5: Add Configuration Assertions

Verify configuration loaded correctly:

```csharp
// After making HTTP request
factory.LoadedConfig.Should().NotBeNull();
factory.LoadedConfig!.Services["wfs"].Enabled.Should().BeTrue();
```

### Step 6: Run and Verify

```bash
# Run new ConfigV2 test
dotnet test --filter "FullyQualifiedName~WfsConfigV2Tests"

# Verify it passes
dotnet test --filter "API=ConfigurationV2"
```

### Step 7: Mark Old Test (Optional)

If keeping old test temporarily:

```csharp
[Fact(Skip = "Migrated to WfsConfigV2Tests")]
public async Task OldTest()
{
    // Old implementation
}
```

Or delete immediately if confident.

---

## Before and After Examples

### Example 1: Simple WFS Test

**Before:**
```csharp
// tests/Honua.Server.Integration.Tests/Ogc/WfsTests.cs

[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "WFS")]
public class WfsTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public WfsTests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task GetCapabilities_ReturnsValidXml()
    {
        // Uses WebApplicationFactoryFixture with appsettings.Test.json
        using var factory = new WebApplicationFactoryFixture<Program>(_db);
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/wfs?service=WFS&version=2.0.0&request=GetCapabilities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var xml = await response.Content.ReadAsStringAsync();
        xml.Should().Contain("WFS_Capabilities");
    }
}
```

**After:**
```csharp
// tests/Honua.Server.Integration.Tests/ConfigurationV2/WfsConfigV2Tests.cs

[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "ConfigurationV2")]
[Trait("Endpoint", "WFS")]
public class WfsConfigV2Tests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public WfsConfigV2Tests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task GetCapabilities_WFS20_ReturnsValidXml()
    {
        // Uses ConfigurationV2TestFixture with HCL configuration
        using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
        {
            builder
                .AddDataSource("gis_db", "postgresql")
                .AddService("wfs", new()
                {
                    ["version"] = "2.0.0",
                    ["max_features"] = 10000
                })
                .AddLayer("test_features", "gis_db", "test_table");
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/wfs?service=WFS&version=2.0.0&request=GetCapabilities");

        // Verify response
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var xml = await response.Content.ReadAsStringAsync();
        xml.Should().Contain("WFS_Capabilities");

        // Verify configuration loaded correctly
        factory.LoadedConfig.Should().NotBeNull();
        factory.LoadedConfig!.Services["wfs"].Enabled.Should().BeTrue();
    }
}
```

### Example 2: Multiple Layers Test

**Before:**
```csharp
[Fact]
public async Task GetFeature_WithMultipleLayers_ReturnsAllFeatures()
{
    using var factory = new WebApplicationFactoryFixture<Program>(_db);
    var client = factory.CreateClient();

    // Configuration comes from appsettings.Test.json
    var response = await client.GetAsync("/ogc/features/collections");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

**After:**
```csharp
[Fact]
public async Task GetFeature_WithMultipleLayers_ReturnsAllFeatures()
{
    // Explicit configuration in test
    var hcl = @"
honua {
  version = ""1.0""
  environment = ""test""
}

data_source ""spatial_db"" {
  provider = ""postgresql""
  connection = env(""DATABASE_URL"")

  pool {
    min_size = 2
    max_size = 10
  }
}

service ""ogc_api"" {
  enabled = true
  item_limit = 1000
}

layer ""roads"" {
  title = ""Road Network""
  data_source = data_source.spatial_db
  table = ""public.roads""
  id_field = ""id""
  introspect_fields = true

  geometry {
    column = ""geom""
    type = ""LineString""
    srid = 4326
  }

  services = [service.ogc_api]
}

layer ""buildings"" {
  title = ""Buildings""
  data_source = data_source.spatial_db
  table = ""public.buildings""
  id_field = ""id""
  introspect_fields = true

  geometry {
    column = ""geom""
    type = ""Polygon""
    srid = 4326
  }

  services = [service.ogc_api]
}
";

    using var factory = new ConfigurationV2TestFixture<Program>(_db, hcl);
    var client = factory.CreateClient();

    var response = await client.GetAsync("/ogc/features/collections");

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    // Verify both layers configured
    factory.LoadedConfig!.Layers.Should().HaveCount(2);
    factory.LoadedConfig.Layers.Should().ContainKey("roads");
    factory.LoadedConfig.Layers.Should().ContainKey("buildings");
}
```

### Example 3: Service Disabled Test

**Before:**
```csharp
[Fact]
public async Task WfsDisabled_ReturnsNotFound()
{
    // Complex service override in WebApplicationFactory
    using var factory = new CustomWebAppFactory(_db, services =>
    {
        services.Configure<WfsOptions>(opt => opt.Enabled = false);
    });

    var client = factory.CreateClient();
    var response = await client.GetAsync("/wfs?request=GetCapabilities");

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

**After:**
```csharp
[Fact]
public async Task WfsService_DisabledInConfig_ReturnsNotFound()
{
    // Simple, declarative configuration
    using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
    {
        builder
            .AddDataSource("db", "postgresql")
            .AddRaw(@"
service ""wfs"" {
  enabled = false  # Explicitly disabled
}
");
    });

    var client = factory.CreateClient();
    var response = await client.GetAsync("/wfs?request=GetCapabilities");

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);

    // Verify service is disabled in configuration
    factory.LoadedConfig!.Services["wfs"].Enabled.Should().BeFalse();
}
```

---

## Configuration Patterns

### Pattern 1: Builder Pattern (Recommended for Simple Tests)

**Use When:**
- Simple configurations
- Standard settings
- Quick test setup

**Example:**
```csharp
using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
{
    builder
        .AddDataSource("db", "postgresql")
        .AddService("wfs", new()
        {
            ["version"] = "2.0.0",
            ["max_features"] = 10000,
            ["default_count"] = 100
        })
        .AddLayer("parcels", "db", "parcels", "geom", "Polygon", 4326)
        .AddRedisCache("cache", "REDIS_URL");
});
```

### Pattern 2: Inline HCL (Recommended for Complex Tests)

**Use When:**
- Complex configurations
- Testing configuration parsing
- Matching production configs
- Advanced features (pools, caching, etc.)

**Example:**
```csharp
var hcl = @"
honua {
  version = ""1.0""
  environment = ""test""
  log_level = ""debug""
}

data_source ""main_db"" {
  provider = ""postgresql""
  connection = env(""DATABASE_URL"")

  pool {
    min_size = 2
    max_size = 10
    idle_timeout = 300
    connection_lifetime = 1800
  }
}

cache ""redis"" {
  enabled = true
  connection = env(""REDIS_URL"")
  prefix = ""test:""
  ttl = 3600
}

service ""wfs"" {
  enabled = true
  version = ""2.0.0""
  max_features = 10000
  default_count = 100
  enable_complexity_check = true
  max_transaction_features = 1000
}

layer ""features"" {
  title = ""Test Features""
  description = ""Features for testing""
  data_source = data_source.main_db
  table = ""public.features""
  id_field = ""gid""
  introspect_fields = true

  geometry {
    column = ""geom""
    type = ""Point""
    srid = 4326
  }

  services = [service.wfs]

  cache {
    enabled = true
    cache_ref = cache.redis
    ttl = 300
  }
}
";

using var factory = new ConfigurationV2TestFixture<Program>(_db, hcl);
```

### Pattern 3: Hybrid (Builder + Raw HCL)

**Use When:**
- Need builder convenience + custom HCL
- Testing edge cases

**Example:**
```csharp
using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
{
    builder
        .AddDataSource("db", "postgresql")
        .AddLayer("basic", "db", "basic_table")
        .AddRaw(@"
service ""wfs"" {
  enabled = true
  # Custom advanced settings
  enable_streaming_transaction_parser = true
  max_complexity_score = 500
}
");
});
```

---

## Benefits and Tradeoffs

### Benefits

#### 1. Production Parity
Tests use same configuration format as production, catching configuration issues early.

#### 2. Better Test Documentation
Configuration is self-documenting in tests:

```csharp
// Clear what's being tested
builder
    .AddDataSource("spatial_db", "postgresql")
    .AddService("wfs", new() { ["max_features"] = 100 })  // Testing limit
    .AddLayer("test", "spatial_db", "test_table");
```

#### 3. Full Integration Testing
Tests verify entire configuration pipeline:
- HCL parsing
- Validation
- Service registration
- Runtime behavior

#### 4. Easier Debugging
When tests fail, you can see exact configuration used:

```csharp
// Inspect configuration
factory.LoadedConfig.Should().NotBeNull();
var wfs = factory.LoadedConfig!.Services["wfs"];
Console.WriteLine($"WFS max_features: {wfs.Settings["max_features"]}");
```

#### 5. Isolated Configuration
Each test has own configuration, preventing interference:

```csharp
[Fact]
public async Task Test1()
{
    using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
    {
        builder.AddService("wfs", new() { ["max_features"] = 100 });
    });
    // Test with max_features = 100
}

[Fact]
public async Task Test2()
{
    using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
    {
        builder.AddService("wfs", new() { ["max_features"] = 1000 });
    });
    // Test with max_features = 1000
}
```

### Tradeoffs

#### 1. Slightly More Verbose

**Old:**
```csharp
using var factory = new WebApplicationFactoryFixture<Program>(_db);
```

**New:**
```csharp
using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
{
    builder.AddDataSource("db", "postgresql");
});
```

**Mitigation:** Builder pattern keeps it concise for simple tests.

#### 2. Learning Curve

Developers need to learn:
- HCL syntax
- Configuration V2 concepts
- TestConfigurationBuilder API

**Mitigation:** This guide and comprehensive examples.

#### 3. Temporary File Creation

ConfigurationV2TestFixture creates temporary .honua files.

**Impact:** Minimal, automatically cleaned up.

#### 4. Migration Effort

Migrating existing tests takes time.

**Mitigation:** Gradual migration, parallel tests approach.

---

## Troubleshooting

### Issue: Configuration not loading

**Symptoms:**
```
factory.LoadedConfig is null
```

**Solutions:**
1. Check HCL syntax (quotes, braces, etc.)
2. Verify connection string interpolation
3. Check for validation errors in test output

**Debug:**
```csharp
try
{
    using var factory = new ConfigurationV2TestFixture<Program>(_db, hcl);
}
catch (Exception ex)
{
    Console.WriteLine($"Config error: {ex.Message}");
    throw;
}
```

### Issue: Connection strings not interpolated

**Symptoms:**
```
connection = "env("DATABASE_URL")" (literal string instead of actual connection)
```

**Solutions:**
Use correct syntax:
```hcl
# Correct
connection = env("DATABASE_URL")
connection = ${env:DATABASE_URL}

# Wrong
connection = "env("DATABASE_URL")"
connection = $DATABASE_URL
```

### Issue: Service not registered

**Symptoms:**
```
404 Not Found when calling API endpoint
```

**Solutions:**
1. Verify service enabled:
   ```csharp
   factory.LoadedConfig!.Services["wfs"].Enabled.Should().BeTrue();
   ```

2. Add service to layer:
   ```hcl
   layer "test" {
     services = [service.wfs]  # Don't forget this!
   }
   ```

3. Check service registration (may need implementation):
   ```csharp
   // In ConfigurationV2TestFixture.ConfigureTestServices
   services.AddHonuaFromConfiguration(LoadedConfig);
   ```

### Issue: Test slower than expected

**Symptoms:**
Tests with Configuration V2 run slower than old tests.

**Cause:**
- Creating temporary config files
- Parsing HCL
- Full service registration

**Mitigation:**
1. Use builder pattern (faster than inline HCL)
2. Share fixtures across tests
3. Profile slow tests:
   ```bash
   dotnet test --logger "console;verbosity=detailed" --filter "FullyQualifiedName~SlowTest"
   ```

### Issue: Old and new tests conflict

**Symptoms:**
Running old and new tests together causes failures.

**Solutions:**
1. Use different collections:
   ```csharp
   [Collection("OldTests")]
   public class WfsTests { }

   [Collection("ConfigV2Tests")]
   public class WfsConfigV2Tests { }
   ```

2. Run separately:
   ```bash
   # Old tests
   dotnet test --filter "Category=Integration&API=WFS"

   # New tests
   dotnet test --filter "API=ConfigurationV2"
   ```

---

## Migration Checklist

Use this checklist when migrating tests:

### Pre-Migration
- [ ] Identify test to migrate
- [ ] Understand current test behavior
- [ ] Note any special configuration
- [ ] Check for dependencies on appsettings.Test.json

### Migration
- [ ] Create new ConfigV2 test file
- [ ] Update class declaration with traits
- [ ] Replace WebApplicationFactoryFixture
- [ ] Convert configuration to builder or HCL
- [ ] Add configuration assertions
- [ ] Update test method names if needed

### Verification
- [ ] New test passes
- [ ] New test covers same scenarios as old
- [ ] Configuration loads correctly
- [ ] API endpoints respond as expected
- [ ] No regressions in test coverage

### Cleanup
- [ ] Mark or delete old test
- [ ] Update documentation
- [ ] Commit changes
- [ ] Update CI/CD if needed

---

## Quick Reference

### Builder Methods

```csharp
builder
    .AddDataSource(id, provider, connectionEnvVar)
    .AddService(serviceId, settings)
    .AddLayer(id, dataSourceRef, table, geometryColumn, geometryType, srid)
    .AddRedisCache(id, connectionEnvVar)
    .AddRaw(hclConfig)
```

### Common HCL Blocks

**Data Source:**
```hcl
data_source "db" {
  provider = "postgresql"
  connection = env("DATABASE_URL")
  pool { min_size = 2 max_size = 10 }
}
```

**Service:**
```hcl
service "wfs" {
  enabled = true
  version = "2.0.0"
  max_features = 10000
}
```

**Layer:**
```hcl
layer "features" {
  title = "Features"
  data_source = data_source.db
  table = "features"
  id_field = "id"
  geometry { column = "geom" type = "Point" srid = 4326 }
  services = [service.wfs]
}
```

### Traits

```csharp
[Trait("Category", "Integration")]
[Trait("API", "ConfigurationV2")]
[Trait("Endpoint", "WFS")]
```

### Filter Commands

```bash
# All ConfigV2 tests
dotnet test --filter "API=ConfigurationV2"

# Specific endpoint
dotnet test --filter "Endpoint=WFS&API=ConfigurationV2"
```

---

## Next Steps

1. Start with simplest tests
2. Migrate one test file at a time
3. Run both old and new tests until confident
4. Share learnings with team
5. Update CI/CD pipelines as needed

**Resources:**
- [TEST_INFRASTRUCTURE.md](./TEST_INFRASTRUCTURE.md) - Infrastructure overview
- [WRITING_TESTS.md](./WRITING_TESTS.md) - Writing guide
- [TEST_PERFORMANCE.md](./TEST_PERFORMANCE.md) - Performance tips

---

**Last Updated:** 2025-11-11
**Maintained by:** Honua.Server Team
