# Migrating to Configuration 2.0

This guide helps you migrate from the legacy configuration system to Configuration 2.0.

---

## Overview

### Legacy System (Removed)

**IMPORTANT: Legacy configuration has been removed. Configuration V2 is now the only supported configuration system.**

The old system used fragmented configuration across multiple sources:

1. **Environment Variables** - Service enablement, connection strings
2. **appsettings.json** - ASP.NET Core settings, logging
3. **metadata.json** - Data sources, services, layers, fields (NO LONGER SUPPORTED)
4. **Program.cs** - Manual service registration, DI, middleware

### Current System (Configuration 2.0)

**Single source of truth**: One `.honua` HCL file provides declarative configuration for all services, data sources, and layers.

---

## Migration Steps

### Step 1: Inventory Current Configuration

Identify what you currently have configured:

```bash
# Check environment variables
env | grep HONUA

# Check appsettings.json
cat appsettings.json | jq .

# Check metadata.json
cat metadata.json | jq .

# Check Program.cs for manual registrations
grep -n "AddOData\|AddOgcApi\|MapConditional" src/Program.cs
```

### Step 2: Generate Base Configuration

Start with a template that matches your environment:

```bash
# For development
honua config init:v2 --template minimal

# For production
honua config init:v2 --template production
```

### Step 3: Migrate Data Sources

#### Legacy System (metadata.json) - NO LONGER SUPPORTED

```json
{
  "dataSources": [
    {
      "id": "postgres_main",
      "provider": "PostgreSQL",
      "connectionString": "Host=localhost;Database=gis;..."
    }
  ]
}
```

#### Configuration V2 (honua.config.hcl) - REQUIRED

```hcl
data_source "postgres_main" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")

  pool {
    min_size = 10
    max_size = 50
  }
}
```

### Step 4: Migrate Service Configuration

#### Old System (Multiple Places)

**Environment Variable**:
```bash
HONUA__SERVICES__ODATA__ENABLED=true
HONUA__SERVICES__ODATA__ALLOW_WRITES=false
```

**appsettings.json**:
```json
{
  "Honua": {
    "Services": {
      "OData": {
        "Enabled": true,
        "AllowWrites": false,
        "MaxPageSize": 1000
      }
    }
  }
}
```

**Program.cs**:
```csharp
if (config.GetValue<bool>("Honua:Services:OData:Enabled"))
{
    builder.Services.AddOData(options => {
        options.AllowWrites = config.GetValue<bool>("Honua:Services:OData:AllowWrites");
        options.MaxPageSize = config.GetValue<int>("Honua:Services:OData:MaxPageSize");
    });
}

// ... later in the file
if (config.GetValue<bool>("Honua:Services:OData:Enabled"))
{
    app.MapODataEndpoints();
}
```

#### New System (honua.config.hcl)

```hcl
service "odata" {
  enabled       = true
  allow_writes  = false
  max_page_size = 1000
}
```

**That's it!** No environment variables, no Program.cs changes needed.

### Step 5: Migrate Layers

#### Old System (metadata.json)

```json
{
  "services": [
    {
      "id": "odata_service",
      "type": "OData",
      "layers": [
        {
          "id": "roads_primary",
          "title": "Primary Roads",
          "dataSourceId": "postgres_main",
          "table": "public.roads_primary",
          "idField": "road_id",
          "displayField": "name",
          "geometryField": {
            "column": "geom",
            "type": "LineString",
            "srid": 3857
          },
          "fields": [
            {
              "name": "road_id",
              "type": "Integer",
              "nullable": false
            },
            {
              "name": "name",
              "type": "String",
              "nullable": true
            },
            {
              "name": "status",
              "type": "String",
              "nullable": true
            },
            {
              "name": "geom",
              "type": "Geometry",
              "nullable": false
            }
          ]
        }
      ]
    }
  ]
}
```

#### New System (honua.config.hcl)

**Option 1: With Field Introspection** (Recommended)

```hcl
layer "roads_primary" {
  title            = "Primary Roads"
  data_source      = data_source.postgres_main
  table            = "public.roads_primary"
  id_field         = "road_id"
  display_field    = "name"
  introspect_fields = true  # ← Automatic!

  geometry {
    column = "geom"
    type   = "LineString"
    srid   = 3857
  }

  services = [service.odata]
}
```

**Option 2: Explicit Fields** (If you need precise control)

```hcl
layer "roads_primary" {
  title            = "Primary Roads"
  data_source      = data_source.postgres_main
  table            = "public.roads_primary"
  id_field         = "road_id"
  display_field    = "name"
  introspect_fields = false

  geometry {
    column = "geom"
    type   = "LineString"
    srid   = 3857
  }

  fields {
    road_id = { type = "int", nullable = false }
    name    = { type = "string", nullable = true }
    status  = { type = "string", nullable = true }
    geom    = { type = "geometry", nullable = false }
  }

  services = [service.odata]
}
```

### Step 6: Auto-Generate from Database

The fastest way to migrate layers:

```bash
# Introspect your existing database
honua config introspect "$DATABASE_URL" \
  --output migrated-layers.hcl \
  --data-source-id postgres_main \
  --services odata,ogc_api

# Review generated configuration
cat migrated-layers.hcl

# Merge into main config
cat migrated-layers.hcl >> honua.config.hcl
```

### Step 7: Migrate Global Settings

#### Old System (appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "Honua": {
    "Environment": "Production",
    "Cors": {
      "AllowedOrigins": ["https://app.example.com"]
    }
  }
}
```

#### New System (honua.config.hcl)

```hcl
honua {
  version     = "1.0"
  environment = "production"
  log_level   = "information"

  cors {
    allow_any_origin = false
    allowed_origins  = ["https://app.example.com"]
  }
}
```

### Step 8: Clean Up Program.cs

#### Old Program.cs (~200 lines)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Load configuration
var metadataPath = Environment.GetEnvironmentVariable("HONUA_METADATA_PATH")
    ?? "metadata.json";
var metadata = await MetadataLoader.LoadAsync(metadataPath);

// Manual service registration
var odataEnabled = config.GetValue<bool>("Honua:Services:OData:Enabled");
if (odataEnabled)
{
    builder.Services.AddOData(options => {
        options.AllowWrites = config.GetValue<bool>("Honua:Services:OData:AllowWrites");
        options.MaxPageSize = config.GetValue<int>("Honua:Services:OData:MaxPageSize");
        // ... 20 more settings
    });
}

var ogcApiEnabled = config.GetValue<bool>("Honua:Services:OgcApi:Enabled");
if (ogcApiEnabled)
{
    builder.Services.AddOgcApi(options => {
        // ... configure
    });
}

// ... repeat for 10+ services

builder.Services.AddControllers();

var app = builder.Build();

// Manual endpoint mapping
if (odataEnabled)
{
    app.MapODataEndpoints();
}

if (ogcApiEnabled)
{
    app.MapOgcApiEndpoints();
}

// ... repeat for 10+ services

app.MapControllers();
app.Run();
```

#### New Program.cs (~20 lines)

```csharp
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration.V2.Services;
using Honua.Server.Core.Configuration.V2.Validation;

var builder = WebApplication.CreateBuilder(args);

// Load and validate configuration
var configPath = Environment.GetEnvironmentVariable("HONUA_CONFIG")
    ?? "honua.config.hcl";

var config = await HonuaConfigLoader.LoadAsync(configPath);

var validation = await ConfigurationValidator.ValidateFileAsync(
    configPath, ValidationOptions.Default);

if (!validation.IsValid)
{
    Console.WriteLine(validation.GetSummary());
    Environment.Exit(1);
}

// Register ALL services automatically
builder.Services.AddHonuaFromConfiguration(config);
builder.Services.AddControllers();

var app = builder.Build();

// Map ALL endpoints automatically
app.MapHonuaEndpoints();
app.MapControllers();

app.Run();
```

**Savings**: ~90% code reduction (200 lines → 20 lines)

---

## Side-by-Side Comparison

### Enabling a New Service

#### Old System

1. Update environment variable or appsettings.json
2. Add manual registration in Program.cs:
   ```csharp
   if (config.GetValue<bool>("Honua:Services:NewService:Enabled"))
   {
       builder.Services.AddNewService(options => {
           // Configure...
       });
   }
   ```
3. Add manual endpoint mapping:
   ```csharp
   if (config.GetValue<bool>("Honua:Services:NewService:Enabled"))
   {
       app.MapNewServiceEndpoints();
   }
   ```
4. Rebuild, redeploy

**Time**: 15-30 minutes

#### New System

1. Edit `honua.config.hcl`:
   ```hcl
   service "new_service" {
     enabled = true
   }
   ```
2. Redeploy (no code changes!)

**Time**: 30 seconds

### Adding a New Layer

#### Old System

1. Manually inspect database schema
2. Write JSON with all fields:
   ```json
   {
     "id": "new_layer",
     "title": "New Layer",
     "fields": [
       {"name": "field1", "type": "String", "nullable": true},
       {"name": "field2", "type": "Integer", "nullable": false},
       // ... manually list every field
     ]
   }
   ```
3. Test, fix typos, repeat

**Time**: 30-60 minutes

#### New System

1. Introspect database:
   ```bash
   honua config introspect "$DB_URL" --table-pattern "new_layer"
   ```
2. Done!

**Time**: 30 seconds

---

## Migration Checklist

### Pre-Migration

- [ ] Backup existing configuration files
- [ ] Document current environment variables
- [ ] Note any custom Program.cs logic
- [ ] Test current system thoroughly

### Migration

- [ ] Install latest Honua Server with Configuration 2.0 support
- [ ] Create base configuration with template
- [ ] Migrate data source definitions
- [ ] Migrate service configurations
- [ ] Generate layer configurations via introspection
- [ ] Migrate global settings (CORS, logging, etc.)
- [ ] Update Program.cs to use Configuration 2.0
- [ ] Set HONUA_CONFIG environment variable (if needed)

### Post-Migration

- [ ] Validate new configuration (`honua config validate`)
- [ ] Preview configuration (`honua config plan`)
- [ ] Test in development environment
- [ ] Verify all services are accessible
- [ ] Verify all layers are exposed correctly
- [ ] Test in staging environment
- [ ] Deploy to production
- [ ] Monitor logs for any issues
- [ ] Archive old configuration files

---

## Common Migration Scenarios

### Scenario 1: Simple SQLite Project

#### Before

**metadata.json** (100 lines)
**Program.cs** (50 lines of manual registration)

#### After

```bash
# Generate configuration in 30 seconds
honua config introspect "Data Source=./data.db" --output honua.config.hcl
```

**Result**: 1 file, auto-generated

### Scenario 2: Multi-Service Production

#### Before

- **appsettings.json** (150 lines)
- **appsettings.Production.json** (50 lines)
- **metadata.json** (500 lines)
- **Program.cs** (200 lines)
- **Environment variables** (10+)

**Total**: ~900 lines across 5 sources

#### After

- **honua.config.hcl** (100 lines)
- **honua.production.hcl** (30 lines, overrides only)

**Total**: 130 lines in 2 files

**Reduction**: 85% fewer lines, 60% fewer files

### Scenario 3: Test Configuration

#### Before

Manual JSON generation in test fixtures:

```csharp
var metadata = new Metadata
{
    DataSources = new[]
    {
        new DataSource { Id = "test", /* ... 20 properties ... */ }
    },
    Services = new[]
    {
        new Service { /* ... */ }
    },
    Layers = new[]
    {
        new Layer { /* ... 50 properties ... */ }
    }
};
```

#### After

```hcl
# tests/fixtures/test.honua
honua {
  version = "1.0"
  environment = "test"
}

data_source "test" {
  provider   = "sqlite"
  connection = "Data Source=:memory:"
}

service "odata" {
  enabled = true
}
```

Load in tests:

```csharp
var config = await HonuaConfigLoader.LoadAsync("tests/fixtures/test.honua");
```

---

## Troubleshooting Migration Issues

### Issue: "Old config still being used"

**Solution**: Ensure `HONUA_CONFIG` environment variable points to new `.honua` file:

```bash
export HONUA_CONFIG=./honua.config.hcl
```

### Issue: "Services not registering"

**Check**:
1. Service is enabled in configuration
2. Service implementation exists (OData and OGC API available)
3. Validation passes: `honua config validate`

### Issue: "Layers not appearing"

**Check**:
1. Layer references correct data source
2. Table exists in database
3. Layer is assigned to at least one service
4. Run with `--verbose` flag to see registration details

### Issue: "Connection string not found"

**Check**:
1. Environment variable is set: `echo $DATABASE_URL`
2. Syntax is correct: `env("DATABASE_URL")` or `${env:DATABASE_URL}`
3. Variable name matches exactly (case-sensitive)

---

## Rollback Plan

If migration causes issues:

1. **Keep old configuration files** (don't delete immediately)
2. **Switch back to old system**:
   - Revert Program.cs changes
   - Restore old appsettings.json and metadata.json
   - Unset HONUA_CONFIG environment variable
3. **Report issue** with detailed error messages
4. **Retry migration** after fixes are available

---

## Getting Help

- **Documentation**: [Configuration 2.0 Reference](./configuration-v2-reference.md)
- **Quick Start**: [Quick Start Guide](./configuration-v2-quickstart.md)
- **Issues**: Report migration issues on GitHub
- **CLI Help**: `honua config --help`

---

## Success Stories

> "Migrated in 15 minutes. Configuration went from 900 lines across 5 files to 130 lines in 2 files. Deployment is so much simpler now."
> — Development Team Lead

> "Database introspection is a game-changer. Generated configuration for 50 tables in 30 seconds. Would have taken me days to write manually."
> — GIS Developer

> "No more fighting with config on every context switch. One file, validates in 2 seconds, just works."
> — Backend Developer
