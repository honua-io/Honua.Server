# Honua Configuration 2.0

This directory contains the implementation of Honua's declarative configuration system (Configuration 2.0), as outlined in the [Configuration 2.0 proposal](../../../../docs/proposals/configuration-2.0.md).

## Overview

Configuration 2.0 replaces the fragmented configuration system (environment variables + appsettings.json + metadata.json + Program.cs) with a single, declarative configuration file format inspired by Terraform/Pulumi.

### Key Features

- **Single source of truth**: One `.honua` or `.hcl` file defines everything
- **Environment variable interpolation**: `${env:VAR_NAME}` or `env("VAR_NAME")` syntax
- **Variable support**: Define and reference variables with `var.variable_name`
- **Type-safe**: Strongly-typed C# models with validation
- **Human-readable**: HCL-style syntax with comments and clear structure
- **Validatable**: Can be validated before runtime (future: CLI tool)

## Architecture

### Components

1. **HonuaConfig.cs** - Core configuration schema (C# models)
2. **HclParser.cs** - Parses `.hcl` and `.honua` files
3. **ConfigurationProcessor.cs** - Processes interpolations (env vars, variables)
4. **HonuaConfigLoader.cs** - High-level API for loading configurations

### Data Flow

```
.honua file
    ↓
HclParser → Raw HonuaConfig
    ↓
ConfigurationProcessor → Processed HonuaConfig (env vars expanded)
    ↓
Application (validated & ready to use)
```

## Usage

### Basic Example

```csharp
using Honua.Server.Core.Configuration.V2;

// Load configuration from file
var config = HonuaConfigLoader.Load("honua.config.hcl");

// Or load asynchronously
var config = await HonuaConfigLoader.LoadAsync("honua.config.hcl");

// Access configuration
Console.WriteLine($"Environment: {config.Honua.Environment}");
Console.WriteLine($"Data Sources: {config.DataSources.Count}");
Console.WriteLine($"Services: {config.Services.Count}");
Console.WriteLine($"Layers: {config.Layers.Count}");
```

### Configuration File Format

```hcl
# honua.config.hcl

# Global settings
honua {
    version = "1.0"
    environment = "development"
    log_level = "information"

    cors = {
        allow_any_origin = false
        allowed_origins = ["http://localhost:3000"]
    }
}

# Data source definition
data_source "sqlite-test" {
    provider = "sqlite"
    connection = "Data Source=./data/test.db"
    health_check = "SELECT 1"
}

# Service definition
service "odata" {
    type = "odata"
    enabled = true
    allow_writes = true
    max_page_size = 1000
}

# Layer definition
layer "roads-primary" {
    title = "Primary Roads"
    data_source = "sqlite-test"
    table = "roads_primary"

    geometry = {
        column = "geom"
        type = "LineString"
        srid = 4326
    }

    id_field = "road_id"
    display_field = "name"
    introspect_fields = true

    services = ["odata"]
}

# Cache configuration
cache "redis" {
    type = "redis"
    enabled = true
    connection = "${env:REDIS_URL}"
}

# Rate limiting
rate_limit {
    enabled = true
    store = "memory"

    rules = {
        default = {
            requests = 1000
            window = "1m"
        }
    }
}
```

### Environment Variable Interpolation

Configuration 2.0 supports two syntaxes for environment variables:

1. **Dollar-brace syntax**: `${env:VAR_NAME}`
2. **Function syntax**: `env("VAR_NAME")`

Example:

```hcl
data_source "postgres-prod" {
    provider = "postgresql"
    connection = "${env:DATABASE_URL}"  # Or: env("DATABASE_URL")
}
```

At runtime, the environment variable is resolved:

```bash
export DATABASE_URL="Server=localhost;Database=honua;User=postgres;Password=secret"
```

If the environment variable is not set, loading will throw an `InvalidOperationException` with a clear error message.

### Variable Support

Define variables in your configuration and reference them:

```hcl
variable "db_name" = "honua_dev"

data_source "local" {
    provider = "sqlite"
    connection = "Data Source=./var.db_name.db"
}
```

Reference variables using `var.variable_name` syntax.

### Multiple Data Sources

You can define multiple data sources:

```hcl
data_source "sqlite-local" {
    provider = "sqlite"
    connection = "Data Source=./local.db"
}

data_source "postgres-prod" {
    provider = "postgresql"
    connection = "${env:DATABASE_URL}"

    pool = {
        min_size = 10
        max_size = 50
        timeout = 30
    }
}

data_source "sqlserver-analytics" {
    provider = "sqlserver"
    connection = "${env:ANALYTICS_DB_URL}"
}
```

### Layer Field Introspection

Layers support automatic field introspection from the database:

```hcl
layer "auto-introspected" {
    title = "Auto-Introspected Layer"
    data_source = "postgres-prod"
    table = "my_table"
    id_field = "id"
    introspect_fields = true  # Fields will be auto-detected from DB schema

    geometry = {
        column = "geom"
        type = "Point"
        srid = 4326
    }

    services = ["odata", "ogc_api"]
}
```

Or explicitly define fields:

```hcl
layer "explicit-fields" {
    title = "Explicitly Defined Fields"
    data_source = "postgres-prod"
    table = "sensors"
    id_field = "sensor_id"
    introspect_fields = false  # Must define fields explicitly

    fields = {
        sensor_id = {
            type = "int"
            nullable = false
        }
        name = {
            type = "string"
            nullable = false
        }
        temperature = {
            type = "double"
            nullable = true
        }
        last_reading = {
            type = "datetime"
            nullable = true
        }
    }

    geometry = {
        column = "location"
        type = "Point"
        srid = 4326
    }

    services = ["ogc_api"]
}
```

### Environment-Specific Configurations

You can create environment-specific configuration files:

```
honua.config.hcl          # Base configuration
honua.development.hcl     # Development overrides
honua.production.hcl      # Production overrides
```

Load the appropriate configuration based on your environment:

```csharp
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
var configPath = $"honua.{environment.ToLower()}.hcl";

if (!File.Exists(configPath))
{
    configPath = "honua.config.hcl"; // Fallback to base configuration
}

var config = HonuaConfigLoader.Load(configPath);
```

## Configuration Schema

### HonuaConfig

Root configuration object.

**Properties:**
- `Honua` (HonuaGlobalSettings) - Global settings
- `DataSources` (Dictionary<string, DataSourceBlock>) - Data source definitions
- `Services` (Dictionary<string, ServiceBlock>) - Service definitions
- `Layers` (Dictionary<string, LayerBlock>) - Layer definitions
- `Caches` (Dictionary<string, CacheBlock>) - Cache definitions
- `RateLimit` (RateLimitBlock?) - Rate limiting configuration
- `Variables` (Dictionary<string, object?>) - Variables

### HonuaGlobalSettings

Global Honua server settings.

**Properties:**
- `Version` (string) - Configuration schema version (e.g., "1.0")
- `Environment` (string) - Environment name (development, staging, production)
- `LogLevel` (string) - Logging level (trace, debug, information, warning, error, critical)
- `Cors` (CorsSettings?) - CORS configuration

### DataSourceBlock

Defines a database or data connection.

**Properties:**
- `Id` (string, required) - Data source identifier
- `Provider` (string, required) - Provider type (sqlite, postgresql, sqlserver, mysql)
- `Connection` (string, required) - Connection string (supports env var interpolation)
- `HealthCheck` (string?) - Optional health check query
- `Pool` (PoolSettings?) - Connection pool settings

### ServiceBlock

Defines a service (OData, OGC API, WFS, etc.).

**Properties:**
- `Id` (string, required) - Service identifier
- `Type` (string, required) - Service type (odata, ogc_api, wfs, wms, etc.)
- `Enabled` (bool) - Whether the service is enabled (default: true)
- `Settings` (Dictionary<string, object?>) - Service-specific settings

### LayerBlock

Defines a feature layer or raster dataset.

**Properties:**
- `Id` (string, required) - Layer identifier
- `Title` (string, required) - Human-readable title
- `DataSource` (string, required) - Reference to data source
- `Table` (string, required) - Table or view name
- `Description` (string?) - Optional description
- `Geometry` (GeometrySettings?) - Geometry configuration
- `IdField` (string, required) - Primary key field name
- `DisplayField` (string?) - Display field name for labels
- `IntrospectFields` (bool) - Auto-introspect fields from database (default: true)
- `Fields` (Dictionary<string, FieldDefinition>?) - Explicit field definitions
- `Services` (List<string>) - List of service IDs to expose this layer

### CacheBlock

Defines a cache (Redis, in-memory).

**Properties:**
- `Id` (string, required) - Cache identifier
- `Type` (string, required) - Cache type (redis, memory)
- `Enabled` (bool) - Whether the cache is enabled (default: true)
- `Connection` (string?) - Connection string for distributed caches
- `RequiredIn` (List<string>) - Environments where this cache is required

### RateLimitBlock

Rate limiting configuration.

**Properties:**
- `Enabled` (bool) - Whether rate limiting is enabled (default: true)
- `Store` (string) - Storage backend (redis, memory)
- `Rules` (Dictionary<string, RateLimitRule>) - Rate limit rules

## Examples

See the `examples/config-v2/` directory for complete examples:

- **minimal.honua** - Simple development configuration
- **production.honua** - Production configuration with environment variables

## Testing

Comprehensive unit tests are available in:

- `tests/Honua.Server.Core.Tests/Configuration/V2/HclParserTests.cs`
- `tests/Honua.Server.Core.Tests/Configuration/V2/ConfigurationProcessorTests.cs`
- `tests/Honua.Server.Core.Tests/Configuration/V2/HonuaConfigLoaderTests.cs`

Run tests:

```bash
dotnet test --filter "FullyQualifiedName~V2"
```

## Future Enhancements

Phase 1 (Configuration Parser) is now complete. Future phases include:

- **Phase 2**: Validation Engine - CLI tool for validation (`honua validate`)
- **Phase 3**: Dynamic Service Loader - Load service assemblies dynamically
- **Phase 4**: CLI Tooling - `honua introspect`, `honua plan`, `honua init`
- **Phase 5**: Database Introspection - Generate configuration from DB schemas
- **Phase 6**: Migration Tooling - Migrate from old config format
- **Phase 7**: Documentation & Examples

## Migration from Legacy Configuration

The new Configuration 2.0 system is designed to eventually replace the current fragmented configuration system. During the transition period (v2.x), both formats will be supported.

For migration guidance, see the [Configuration 2.0 proposal](../../../../docs/proposals/configuration-2.0.md#migration-strategy).

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
