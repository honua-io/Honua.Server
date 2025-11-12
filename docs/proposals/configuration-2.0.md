# Honua Configuration 2.0 - Design Proposal

**Status**: Draft
**Author**: Generated from user feedback and architectural analysis
**Date**: 2025-11-11
**Target Version**: Honua 2.0

---

## Executive Summary

The current Honua Server configuration system suffers from fragmentation across multiple sources (environment variables, `appsettings.json`, `metadata.json`, `Program.cs` DI registration), lacks upfront validation, and creates significant developer friction. This proposal introduces a declarative, single-source-of-truth configuration system inspired by Terraform/Pulumi that addresses these pain points while maintaining backwards compatibility during migration.

**Key Changes**:
- Single declarative configuration file format (`.honua` or HCL-style DSL)
- Configuration validation CLI tool (`honua validate`)
- Dynamic assembly loading based on declared services
- Automatic service registration from configuration
- Database introspection for metadata generation
- Migration tooling for existing configurations

---

## Problem Statement

### Current Pain Points

Based on developer feedback and observed issues:

> "We have literally been fighting with the config system for days either local spinup or docker etc."
>
> "Each time you reset context you start fighting with the same thing. Developers will do the same thing and drop the product."
>
> "There needs to be an easy super simple way to say I just need this or that and the config works easy."

#### 1. **Fragmented Configuration Sources**

Configuration is currently spread across at least four different locations:

1. **Environment Variables** - Redis connection, metadata provider, service enablement
2. **appsettings.json** - Application-level settings, CORS, logging
3. **metadata.json** - Data sources, services, layers, field definitions
4. **Program.cs** - DI registration, middleware pipeline, conditional service registration

**Impact**:
- No single source of truth
- Difficult to understand full configuration state
- Configuration drift between environments (local vs Docker vs production)
- Must check 4+ locations to troubleshoot issues

#### 2. **No Upfront Validation**

Configuration errors are discovered at runtime, often deep in the application startup:

```csharp
// Current: Validation happens at startup, after app has already tried to initialize
if (string.IsNullOrWhiteSpace(metadataProvider))
{
    validationErrors.Add("honua:metadata:provider is required");
}
```

**Impact**:
- Slow feedback loop (build → run → crash → debug → repeat)
- Docker builds take 6+ minutes only to fail on configuration
- Test failures that could have been caught immediately
- Production deployment failures

#### 3. **Poor Developer Experience**

Common developer workflows are frustrating:

- **Local Spinup**: Requires manually setting 10+ environment variables
- **Test Setup**: Manual JSON generation prone to syntax errors (missing commas, brackets)
- **Docker Setup**: Different configuration requirements than local
- **Debugging**: No tooling to validate configuration before running

**Example from recent debugging session**:
```json
// Test fixture generated invalid JSON - missing fields array
// Error only discovered after 6 minute Docker build + test startup
{
  "layers": [{
    "id": "roads-primary",
    "serviceId": "roads",
    // Missing: "fields": [...],
    "storage": {...}
  }]
}
```

#### 4. **Manual Service Registration**

Each service requires manual plumbing in `Program.cs`:

```csharp
// Must manually add each service to the pipeline
if (config.GetValue<bool>("honua:services:odata:enabled"))
{
    builder.Services.AddOData();
    app.MapODataEndpoints();
}

if (config.GetValue<bool>("honua:services:ogcapi:enabled"))
{
    builder.Services.AddOgcApi();
    app.MapOgcApiEndpoints();
}
// ... repeat for each service
```

**Impact**:
- Easy to forget one of the registration steps (e.g., `MapConditionalServiceEndpoints()`)
- No compile-time safety
- Configuration says "enabled" but service not actually registered
- Difficult to add new services

#### 5. **Metadata Generation is Manual and Error-Prone**

Creating metadata for new layers requires:
1. Manually writing JSON with exact schema
2. Ensuring all required fields present (`fields`, `storage`, `geometryField`, etc.)
3. Matching field names exactly to database columns
4. Getting data types correct (string/int/datetime/geometry)

**Impact**:
- Time-consuming for large databases
- Typos cause runtime failures
- Database schema changes require manual metadata updates
- No tooling to introspect database and generate metadata

---

## Current State Analysis

### Configuration Flow Today

```
┌─────────────────────────────────────────────────────────────────┐
│ Developer Intent: "I want OData enabled on this PostgreSQL DB"  │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 v
    ┌────────────────────────────┐
    │ 1. Set Environment Variable │
    │    HONUA__SERVICES__ODATA   │
    │    __ENABLED=true           │
    └────────────┬───────────────┘
                 │
                 v
    ┌────────────────────────────┐
    │ 2. Edit appsettings.json   │
    │    Add connection string   │
    └────────────┬───────────────┘
                 │
                 v
    ┌────────────────────────────┐
    │ 3. Manually create         │
    │    metadata.json with:     │
    │    - Data source config    │
    │    - Service definition    │
    │    - Layer definitions     │
    │    - Field schemas         │
    └────────────┬───────────────┘
                 │
                 v
    ┌────────────────────────────┐
    │ 4. Ensure Program.cs has   │
    │    MapConditionalService   │
    │    Endpoints() call        │
    └────────────┬───────────────┘
                 │
                 v
    ┌────────────────────────────┐
    │ 5. Build and run           │
    │    (6+ min Docker build)   │
    └────────────┬───────────────┘
                 │
                 v
         ┌──────┴──────┐
         │             │
         v             v
    ┌────────┐    ┌────────────┐
    │Success │    │ Fail with  │
    │        │    │ unclear    │
    └────────┘    │ error msg  │
                  └─────┬──────┘
                        │
                        v
              ┌──────────────────┐
              │ Check logs       │
              │ across 4 sources │
              │ Repeat steps 1-5 │
              └──────────────────┘
```

### Why This is a Blocking Issue

From the user's perspective:

1. **Context Reset Problem**: Every time context resets (new terminal, new dev, new machine), the configuration fight starts over. There's no quick "here's what you need" script.

2. **Adoption Barrier**: New developers trying Honua will hit this configuration wall immediately and abandon the product.

3. **Velocity Killer**: Senior developers spending days fighting configuration instead of building features.

4. **Testing Friction**: Writing tests requires manually generating complex JSON structures, leading to brittle tests.

---

## Proposed Solution

### Goals

1. **Single Source of Truth**: One configuration file that declares everything
2. **Declarative**: Express intent ("I want OData on this database"), not implementation
3. **Validated Upfront**: CLI tool validates configuration before any build/run
4. **Introspectable**: Tools that read database and generate configuration
5. **Developer-Friendly**: Easy local spinup, clear error messages, guided workflows
6. **Production-Ready**: Supports environment-specific overrides, secrets management

### Architecture Overview

```
┌────────────────────────────────────────────────────────┐
│ Developer Intent: "I want OData on this PostgreSQL DB" │
└──────────────────┬─────────────────────────────────────┘
                   │
                   v
         ┌─────────────────────┐
         │ honua.config.hcl    │  ← Single source of truth
         │                     │
         │ data_source "db" {  │
         │   type = "postgres" │
         │   connection = ...  │
         │ }                   │
         │                     │
         │ service "odata" {   │
         │   enabled = true    │
         │   data_source = db  │
         │ }                   │
         └──────────┬──────────┘
                    │
                    v
         ┌──────────────────────┐
         │ honua validate       │  ← CLI validation
         │                      │
         │ ✓ Config valid       │
         │ ✓ DB accessible      │
         │ ✓ All fields present │
         └──────────┬───────────┘
                    │
                    v
         ┌──────────────────────┐
         │ Honua Runtime        │
         │                      │
         │ • Parse config       │
         │ • Load assemblies    │
         │ • Register services  │
         │ • Start server       │
         └──────────┬───────────┘
                    │
                    v
              ┌─────────┐
              │ Success │  ← Fast feedback loop
              │ (20 sec)│
              └─────────┘
```

### Key Components

#### 1. Configuration DSL (HCL-style)

Single `.honua` or `.hcl` file with declarative syntax:

```hcl
# honua.config.hcl
# Single source of truth for all configuration

honua {
  version = "1.0"

  # Global settings
  environment = "development"
  log_level   = "information"

  # CORS settings
  cors {
    allow_any_origin = false
    allowed_origins  = ["http://localhost:3000"]
  }
}

# Define data sources
data_source "sqlite-test" {
  provider   = "sqlite"
  connection = "Data Source=./data/test.db"

  # Optional: Health check query
  health_check = "SELECT 1"
}

data_source "postgres-prod" {
  provider   = "postgresql"
  connection = env("POSTGRES_CONNECTION")  # Reference env var for secrets

  # Optional: Connection pooling
  pool {
    min_size = 5
    max_size = 20
  }
}

# Define services
service "odata" {
  enabled       = true
  allow_writes  = true
  max_page_size = 1000

  # OData-specific settings
  expose_navigation_properties = true
  enable_case_insensitive      = true
}

service "ogc_api" {
  enabled = true

  # OGC API Features conformance classes
  conformance = ["core", "geojson", "crs", "filter"]
}

service "wfs" {
  enabled = true
  version = "2.0.0"
}

# Define layers with automatic database introspection
layer "roads_primary" {
  title         = "Primary Roads"
  data_source   = data_source.sqlite-test
  table         = "roads_primary"

  # Geometry configuration
  geometry {
    column = "geom"
    type   = "LineString"
    srid   = 4326
  }

  # Primary key
  id_field = "road_id"

  # Display field for labels
  display_field = "name"

  # OPTIONAL: Explicit field definitions (if not using introspection)
  # fields {
  #   road_id     = { type = "int", nullable = false }
  #   name        = { type = "string", nullable = true }
  #   status      = { type = "string", nullable = true }
  #   created_at  = { type = "datetime", nullable = false }
  #   geom        = { type = "geometry", nullable = false }
  # }

  # Automatically introspect fields from database if not specified
  introspect_fields = true

  # Expose via which services
  services = [
    service.odata,
    service.ogc_api,
    service.wfs
  ]
}

# Layer with explicit field definitions (no introspection)
layer "sensors" {
  title         = "IoT Sensors"
  data_source   = data_source.postgres-prod
  table         = "sensors"

  geometry {
    column = "location"
    type   = "Point"
    srid   = 4326
  }

  id_field      = "sensor_id"
  display_field = "name"

  # Explicitly define fields (no introspection)
  introspect_fields = false
  fields {
    sensor_id   = { type = "int", nullable = false }
    name        = { type = "string", nullable = false }
    model       = { type = "string", nullable = true }
    installed   = { type = "datetime", nullable = false }
    location    = { type = "geometry", nullable = false }
  }

  services = [service.ogc_api]
}

# Redis for distributed scenarios (optional)
cache "redis" {
  enabled    = var.use_redis  # Can reference variables
  connection = env("REDIS_CONNECTION")

  # Only required in production
  required_in = ["production"]
}

# Rate limiting
rate_limit {
  enabled = true

  # Use Redis if available, otherwise in-memory
  store = cache.redis.enabled ? "redis" : "memory"

  rules {
    default = {
      requests = 1000
      window   = "1m"
    }

    authenticated = {
      requests = 5000
      window   = "1m"
    }
  }
}
```

#### 2. Configuration Validation CLI

```bash
# Validate configuration file
honua validate honua.config.hcl

# Output:
✓ Configuration syntax valid
✓ Data source 'sqlite-test' accessible
✓ Table 'roads_primary' exists with expected schema
✓ Geometry column 'geom' found (type: LineString)
✗ ERROR: Data source 'postgres-prod' - connection refused
  → Check POSTGRES_CONNECTION environment variable
  → Ensure database is running on localhost:5432

Configuration validation failed with 1 error

# Introspect database and generate configuration
honua introspect "Data Source=./test.db" --output layers.hcl

# Output: layers.hcl with auto-generated layer definitions

# Dry-run: Show what would be registered
honua plan honua.config.hcl

# Output:
Services to be registered:
  • OData v4 (read/write enabled)
  • OGC API Features (conformance: core, geojson, crs, filter)
  • WFS 2.0.0

Data Sources:
  • sqlite-test: ./data/test.db (healthy)
  • postgres-prod: localhost:5432/honua (healthy)

Layers to be exposed:
  • roads_primary (LineString)
    → via: OData, OGC API, WFS
  • sensors (Point)
    → via: OGC API

Endpoints:
  • /odata
  • /collections
  • /wfs

# Check what configuration will be used in different environments
honua plan honua.config.hcl --env production

# Validate without connecting to databases (syntax only)
honua validate honua.config.hcl --syntax-only
```

#### 3. Dynamic Assembly Loading

Based on declared services in configuration, dynamically load only required assemblies:

```csharp
// New: Configuration-driven assembly loading
var config = HonuaConfigLoader.Load("honua.config.hcl");

// Parse configuration to determine required services
var requiredServices = config.GetEnabledServices();
// Result: ["Honua.Server.Core.OData", "Honua.Server.Core.OgcApi", "Honua.Server.Core.Wfs"]

// Dynamically load assemblies
var serviceLoader = new ServiceAssemblyLoader();
foreach (var serviceName in requiredServices)
{
    var assembly = serviceLoader.LoadService(serviceName);

    // Each assembly exposes IServiceRegistration interface
    var registration = assembly.GetType($"{serviceName}.ServiceRegistration")
        .GetInstance() as IServiceRegistration;

    // Automatic registration
    registration.Register(builder.Services, config.GetServiceConfig(serviceName));
}

// No more manual if/else chains in Program.cs
```

#### 4. Simplified Program.cs

```csharp
// New Program.cs - Configuration-driven startup
using Honua.Server.Core.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Load and validate configuration
var configPath = Environment.GetEnvironmentVariable("HONUA_CONFIG")
    ?? Path.Combine(builder.Environment.ContentRootPath, "honua.config.hcl");

var honuaConfig = HonuaConfigLoader.Load(configPath);

// Validate configuration (fail fast)
var validationResult = honuaConfig.Validate();
if (!validationResult.IsValid)
{
    foreach (var error in validationResult.Errors)
    {
        Console.WriteLine($"❌ {error}");
    }
    throw new InvalidOperationException("Configuration validation failed");
}

// Single call to configure everything based on config file
builder.ConfigureFromHonuaConfig(honuaConfig);

var app = builder.Build();

// Single call to map all endpoints based on config
app.MapHonuaEndpoints(honuaConfig);

app.Run();
```

#### 5. Environment-Specific Overrides

Support environment-specific configuration:

```bash
# Base configuration
honua.config.hcl

# Environment overrides
honua.development.hcl
honua.production.hcl

# Loading priority:
# 1. honua.config.hcl (base)
# 2. honua.{ENVIRONMENT}.hcl (overlay)
# 3. Environment variables (final override)
```

Example override:

```hcl
# honua.production.hcl
# Overrides for production environment

honua {
  log_level = "warning"
}

data_source "postgres-prod" {
  # Override connection for production
  connection = env("PROD_POSTGRES_CONNECTION")

  pool {
    min_size = 10
    max_size = 50
  }
}

cache "redis" {
  enabled    = true  # Override: Redis required in prod
  connection = env("PROD_REDIS_CONNECTION")
}

rate_limit {
  rules {
    default = {
      requests = 500  # Stricter in production
      window   = "1m"
    }
  }
}
```

---

## Implementation Plan

### Phase 1: Configuration Parser (2-3 weeks)

**Goal**: Parse HCL-style configuration files into strongly-typed C# objects

**Deliverables**:
- [ ] Define configuration schema (C# classes)
- [ ] HCL parser (consider using HCL2 library or build custom)
- [ ] Environment variable interpolation (`env("VAR_NAME")`)
- [ ] Variable support (`var.use_redis`)
- [ ] Reference resolution (`data_source.sqlite-test`)
- [ ] Unit tests for parser

**Example API**:
```csharp
var config = HonuaConfigLoader.Load("honua.config.hcl");
var dataSources = config.GetDataSources();
var layers = config.GetLayers();
var services = config.GetEnabledServices();
```

### Phase 2: Validation Engine (2 weeks)

**Goal**: Validate configuration before any runtime execution

**Deliverables**:
- [ ] Syntax validation (schema correctness)
- [ ] Semantic validation (references exist, types match)
- [ ] Runtime validation (databases accessible, tables exist)
- [ ] Detailed error messages with line numbers
- [ ] CLI tool: `honua validate <file>`
- [ ] Unit tests for validation rules

**Example validation errors**:
```
honua.config.hcl:42:3: ERROR: layer "roads_primary" references undefined data_source "sqlite-test"
honua.config.hcl:45:5: ERROR: geometry.type must be one of: Point, LineString, Polygon, MultiPoint, MultiLineString, MultiPolygon
honua.config.hcl:12:5: WARNING: data_source "postgres-prod" connection failed: could not connect to server
```

### Phase 3: Dynamic Service Loader (3 weeks)

**Goal**: Load service assemblies dynamically based on configuration

**Deliverables**:
- [ ] `IServiceRegistration` interface for services
- [ ] Service assembly discovery and loading
- [ ] Automatic DI registration from configuration
- [ ] Automatic endpoint mapping from configuration
- [ ] Refactor existing services (OData, OGC API, WFS) to implement interface
- [ ] Integration tests

**Service Registration Interface**:
```csharp
public interface IServiceRegistration
{
    string ServiceName { get; }
    void Register(IServiceCollection services, ServiceConfig config);
    void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceConfig config);
}

// Example implementation for OData
public class ODataServiceRegistration : IServiceRegistration
{
    public string ServiceName => "odata";

    public void Register(IServiceCollection services, ServiceConfig config)
    {
        var odataConfig = config.As<ODataServiceConfig>();
        services.AddOData(options =>
        {
            options.AllowWrites = odataConfig.AllowWrites;
            options.MaxPageSize = odataConfig.MaxPageSize;
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceConfig config)
    {
        endpoints.MapODataEndpoints();
    }
}
```

### Phase 4: CLI Tooling (2 weeks)

**Goal**: Developer-friendly command-line tools

**Deliverables**:
- [ ] `honua validate` - Validate configuration
- [ ] `honua introspect` - Generate config from database
- [ ] `honua plan` - Show what would be configured
- [ ] `honua init` - Initialize new configuration
- [ ] `honua migrate` - Migrate from old config format
- [ ] Man pages and help documentation

**Example workflows**:
```bash
# Initialize new project
honua init --template basic

# Introspect existing database
honua introspect "Server=localhost;Database=gis;User=postgres" \
  --output layers.hcl \
  --include-tables "roads*,buildings*"

# Validate before deployment
honua validate honua.config.hcl --env production

# Preview configuration
honua plan honua.config.hcl --show-endpoints
```

### Phase 5: Database Introspection (2 weeks)

**Goal**: Automatically generate layer configurations from database schemas

**Deliverables**:
- [ ] Database schema readers (SQLite, PostgreSQL, SQL Server)
- [ ] Geometry column detection (PostGIS, SpatiaLite, SQL Server Spatial)
- [ ] Field type mapping (database types → Honua types)
- [ ] Primary key detection
- [ ] Generate `.hcl` configuration from schema
- [ ] Support for filtering tables/schemas

**Example generated configuration**:
```hcl
# Auto-generated from database: gis.db
# Date: 2025-11-11T10:30:00Z

layer "roads_primary" {
  title         = "Primary Roads"
  data_source   = data_source.gis
  table         = "roads_primary"

  geometry {
    column = "geom"
    type   = "LineString"
    srid   = 4326
  }

  id_field      = "road_id"
  display_field = "name"

  # Fields auto-detected from database schema
  fields {
    road_id    = { type = "int", nullable = false }      # INTEGER PRIMARY KEY
    name       = { type = "string", nullable = true }    # TEXT
    status     = { type = "string", nullable = true }    # TEXT
    length_km  = { type = "double", nullable = true }    # REAL
    created_at = { type = "datetime", nullable = false } # TEXT (ISO8601)
    geom       = { type = "geometry", nullable = false } # LINESTRING
  }
}
```

### Phase 6: Migration Tooling (1 week)

**Goal**: Smooth migration from current configuration to new system

**Deliverables**:
- [ ] `honua migrate` command
- [ ] Read current `metadata.json` + `appsettings.json` + env vars
- [ ] Generate equivalent `.hcl` configuration
- [ ] Migration guide documentation
- [ ] Backwards compatibility shim (read old config format temporarily)

**Example migration**:
```bash
# Analyze current configuration
honua migrate analyze \
  --metadata metadata.json \
  --appsettings appsettings.json \
  --output migration-report.md

# Generate new configuration from current setup
honua migrate convert \
  --metadata metadata.json \
  --appsettings appsettings.json \
  --output honua.config.hcl

# Validate generated configuration
honua validate honua.config.hcl

# Run side-by-side comparison
honua migrate verify \
  --old-config metadata.json \
  --new-config honua.config.hcl
```

### Phase 7: Documentation & Examples (1 week)

**Deliverables**:
- [ ] Configuration reference documentation
- [ ] Example configurations for common scenarios
- [ ] Migration guide
- [ ] Video tutorials
- [ ] Update all existing documentation

---

## Migration Strategy

### Backwards Compatibility

Support both old and new configuration formats during transition period (v2.0-v3.0):

```csharp
// Detect configuration format
if (File.Exists("honua.config.hcl"))
{
    config = HonuaConfigLoader.Load("honua.config.hcl");
}
else if (File.Exists("metadata.json"))
{
    // Legacy format - show deprecation warning
    logger.LogWarning(
        "Using legacy metadata.json configuration. " +
        "This format will be deprecated in v3.0. " +
        "Run 'honua migrate convert' to upgrade.");
    config = LegacyConfigLoader.LoadFromMetadataJson("metadata.json");
}
else
{
    throw new FileNotFoundException("No configuration file found");
}
```

### Phased Rollout

1. **v2.0**: New configuration format available, old format still supported
2. **v2.1-v2.5**: Deprecation warnings for old format
3. **v3.0**: Remove support for old format

### Migration Steps for Users

1. **Install new CLI**: `dotnet tool install -g honua-cli`
2. **Analyze current config**: `honua migrate analyze`
3. **Convert to new format**: `honua migrate convert`
4. **Validate**: `honua validate honua.config.hcl`
5. **Test locally**: Run application with new config
6. **Deploy**: Replace old config with new config

---

## Benefits

### Developer Experience

- **Fast Feedback**: Configuration errors caught in seconds, not minutes
- **Easy Onboarding**: Single file to understand entire system configuration
- **Tooling Support**: CLI validates and generates configuration
- **Clear Errors**: Detailed error messages with line numbers

### Operations

- **Declarative**: Configuration as code, version controlled
- **Reproducible**: Same config file produces identical deployments
- **Environment Parity**: Same configuration structure across dev/staging/prod
- **Secrets Management**: Clear separation of config and secrets via `env()`

### Testing

- **Test Fixture Generation**: No more manual JSON
- **Configuration Reuse**: Share test configurations across test suites
- **Fast Tests**: Skip Docker builds by using validated configs

### Maintainability

- **Single Source of Truth**: All configuration in one place
- **Type Safety**: Strongly-typed configuration objects
- **Validation**: Catch errors before deployment
- **Documentation**: Configuration is self-documenting

---

## Trade-offs and Considerations

### Complexity

**Trade-off**: New DSL adds learning curve
**Mitigation**:
- Provide extensive examples
- IDE plugins for syntax highlighting
- Migration tooling for existing configs
- `honua init` templates for common scenarios

### Breaking Changes

**Trade-off**: Requires migration from existing format
**Mitigation**:
- Support old format during transition (v2.x)
- Automated migration tool
- Detailed migration guide
- Phased deprecation timeline

### HCL vs JSON vs YAML

**Decision**: Use HCL-style syntax
**Rationale**:
- More expressive than JSON (comments, variables, references)
- More structured than YAML (strong typing)
- Familiar to Terraform/Pulumi users
- Supports complex expressions and functions

**Alternative**: If HCL parsing is too complex, use JSON with JSON Schema validation as fallback

### Native AOT Compatibility

**Consideration**: Dynamic assembly loading conflicts with AOT
**Resolution**:
- Keep AOT compilation as optional optimization
- For AOT builds, use configuration to generate static registration code
- `honua codegen` command generates `Program.cs` for AOT scenarios

---

## Success Metrics

How we'll measure if this solves the problem:

1. **Time to First Run**:
   - Current: 30+ minutes (including debugging config issues)
   - Target: <5 minutes from clone to running server

2. **Configuration Errors**:
   - Current: 80% caught at runtime after long build/startup
   - Target: 95% caught at validation time (pre-build)

3. **Developer Satisfaction**:
   - Survey question: "How easy is Honua configuration?" (1-5 scale)
   - Current baseline: TBD
   - Target: 4.0+ average

4. **Support Issues**:
   - Current: ~40% of support issues related to configuration
   - Target: <10% of support issues related to configuration

5. **Test Reliability**:
   - Current: Test failures due to config issues
   - Target: Zero test failures due to configuration

---

## Next Steps

1. **Review & Feedback**: Get stakeholder input on this proposal
2. **Prototype**: Build proof-of-concept configuration parser (Phase 1)
3. **RFC Period**: Open RFC for community feedback (2 weeks)
4. **Implementation**: Begin Phase 1 once design approved
5. **Iteration**: Regular check-ins during implementation

---

## Appendix: Example Configurations

### Minimal Development Setup

```hcl
# honua.config.hcl - Minimal development config

honua {
  version     = "1.0"
  environment = "development"
}

data_source "local_sqlite" {
  provider   = "sqlite"
  connection = "Data Source=./dev.db"
}

service "odata" {
  enabled = true
}

layer "test_features" {
  title            = "Test Features"
  data_source      = data_source.local_sqlite
  table            = "features"
  id_field         = "id"
  introspect_fields = true

  geometry {
    column = "geom"
    type   = "Point"
    srid   = 4326
  }

  services = [service.odata]
}
```

### Production Multi-Service Setup

```hcl
# honua.config.hcl - Production configuration

honua {
  version     = "1.0"
  environment = "production"
  log_level   = "warning"

  cors {
    allow_any_origin = false
    allowed_origins  = ["https://app.example.com"]
  }
}

data_source "postgres_primary" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")

  pool {
    min_size = 10
    max_size = 50
    timeout  = 30
  }

  health_check = "SELECT 1"
}

cache "redis" {
  enabled    = true
  connection = env("REDIS_URL")
}

service "odata" {
  enabled       = true
  allow_writes  = false  # Read-only in production
  max_page_size = 500
}

service "ogc_api" {
  enabled     = true
  conformance = ["core", "geojson", "crs", "filter"]
}

service "wfs" {
  enabled = true
  version = "2.0.0"
}

# Multiple layers with shared data source
layer "buildings" {
  title            = "Buildings"
  data_source      = data_source.postgres_primary
  table            = "public.buildings"
  id_field         = "building_id"
  display_field    = "name"
  introspect_fields = true

  geometry {
    column = "geom"
    type   = "Polygon"
    srid   = 3857
  }

  services = [service.odata, service.ogc_api, service.wfs]
}

layer "roads" {
  title            = "Roads Network"
  data_source      = data_source.postgres_primary
  table            = "public.roads"
  id_field         = "road_id"
  display_field    = "name"
  introspect_fields = true

  geometry {
    column = "geom"
    type   = "LineString"
    srid   = 3857
  }

  services = [service.odata, service.ogc_api]
}

rate_limit {
  enabled = true
  store   = "redis"

  rules {
    default = {
      requests = 1000
      window   = "1m"
    }

    authenticated = {
      requests = 10000
      window   = "1m"
    }
  }
}
```

### Test Configuration

```hcl
# honua.test.hcl - Test suite configuration

honua {
  version     = "1.0"
  environment = "test"
  log_level   = "debug"
}

data_source "test_db" {
  provider   = "sqlite"
  connection = "Data Source=:memory:"  # In-memory for tests
}

service "odata" {
  enabled      = true
  allow_writes = true
}

layer "test_roads" {
  title            = "Test Roads"
  data_source      = data_source.test_db
  table            = "roads_primary"
  id_field         = "road_id"
  display_field    = "name"
  introspect_fields = false

  geometry {
    column = "geom"
    type   = "LineString"
    srid   = 4326
  }

  # Explicit fields for test predictability
  fields {
    road_id    = { type = "int", nullable = false }
    name       = { type = "string", nullable = true }
    status     = { type = "string", nullable = true }
    created_at = { type = "datetime", nullable = false }
    geom       = { type = "geometry", nullable = false }
  }

  services = [service.odata]
}
```

---

## Questions for Review

1. **DSL Syntax**: Is HCL-style the right choice, or prefer JSON/YAML?
2. **Introspection**: Should field introspection be default (opt-out) or opt-in?
3. **Migration Timeline**: Is 3-version deprecation (v2.0 → v3.0) reasonable?
4. **AOT Support**: How critical is Native AOT vs dynamic loading?
5. **Tooling**: Which CLI commands are most valuable for initial release?

---

## Conclusion

The current configuration system is a significant barrier to adoption and developer productivity. This proposal provides a path forward to a declarative, validated, developer-friendly configuration system that addresses the root causes of configuration pain.

The proposed solution:
- Reduces configuration from 4+ sources to 1 source of truth
- Provides fast feedback via CLI validation (seconds not minutes)
- Eliminates manual JSON generation with database introspection
- Simplifies Program.cs from manual service registration to config-driven startup
- Maintains backwards compatibility during transition

**Estimated Effort**: 12-14 weeks for full implementation
**Priority**: High - This is blocking developer adoption

**Recommendation**: Proceed with Phase 1 (Configuration Parser) as proof-of-concept while gathering feedback on this design document.
