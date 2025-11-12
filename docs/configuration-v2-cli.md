# Configuration 2.0 - CLI Reference

Complete reference for all Configuration 2.0 CLI commands.

---

## Table of Contents

1. [Installation](#installation)
2. [Global Options](#global-options)
3. [Commands](#commands)
   - [config validate](#config-validate)
   - [config plan](#config-plan)
   - [config init:v2](#config-initv2)
   - [config introspect](#config-introspect)
4. [Examples](#examples)
5. [Exit Codes](#exit-codes)

---

## Installation

The `honua` CLI tool is included with Honua Server:

```bash
# Install as global tool
dotnet tool install -g honua-cli

# Or use directly
dotnet run --project src/Honua.Cli -- config validate
```

---

## Global Options

Available for all commands:

| Option      | Description                | Example                      |
|-------------|----------------------------|------------------------------|
| `--help`    | Show help information      | `honua config validate --help` |
| `--version` | Show version information   | `honua --version`            |

---

## Commands

### config validate

Validates a Honua configuration file.

#### Syntax

```bash
honua config validate [path] [options]
```

#### Arguments

| Argument | Description                               | Required | Default            |
|----------|-------------------------------------------|----------|--------------------|
| `path`   | Path to configuration file                | No       | `honua.config.hcl` |

#### Options

| Option            | Description                                      | Default |
|-------------------|--------------------------------------------------|---------|
| `--syntax-only`   | Validate syntax only (fast, ~1 sec)              | false   |
| `--full`          | Full validation including runtime checks         | false   |
| `--timeout <SEC>` | Timeout for runtime validation checks (seconds)  | 10      |
| `--verbose`       | Show detailed validation information             | false   |

#### Validation Levels

1. **Default** - Syntax + semantic validation (~2 seconds)
2. **Syntax Only** (`--syntax-only`) - Schema validation only (~1 second)
3. **Full** (`--full`) - Includes database connectivity checks (~10 seconds)

#### Examples

```bash
# Validate with defaults
honua config validate

# Validate specific file
honua config validate config/honua.production.hcl

# Syntax validation only (fast)
honua config validate --syntax-only

# Full validation including database checks
honua config validate --full

# Full validation with custom timeout
honua config validate --full --timeout 30

# Verbose output
honua config validate --verbose
```

#### Output

**Success**:
```
✓ Configuration is valid
```

**With Warnings**:
```
⚠ 2 warning(s)

WARNING at honua.config.hcl:42
No caching configured. Consider enabling Redis for production.

WARNING at honua.config.hcl:56
Rate limiting disabled. Enable for production environments.

✓ Configuration is valid (with warnings)
```

**Failure**:
```
✗ Validation failed with 2 error(s)

ERROR at honua.config.hcl:15
layer "roads" references undefined data_source "missing_db"
→ Suggestion: Define data_source "missing_db" or update reference

ERROR at honua.config.hcl:28
geometry.type must be one of: Point, LineString, Polygon, ...

Fix the errors above and try again.
```

#### Exit Codes

- `0` - Validation successful
- `1` - Validation failed

---

### config plan

Previews what would be configured from a configuration file.

#### Syntax

```bash
honua config plan [path] [options]
```

#### Arguments

| Argument | Description                | Required | Default            |
|----------|----------------------------|----------|--------------------|
| `path`   | Path to configuration file | No       | `honua.config.hcl` |

#### Options

| Option             | Description                                  | Default |
|--------------------|----------------------------------------------|---------|
| `--validate`       | Validate configuration before showing plan   | false   |
| `--show-endpoints` | Show HTTP endpoints that would be mapped     | false   |

#### Examples

```bash
# Preview configuration
honua config plan

# Preview specific file
honua config plan config/honua.production.hcl

# Validate first, then show plan
honua config plan --validate

# Show endpoints
honua config plan --show-endpoints
```

#### Output

```
Planning configuration from: honua.config.hcl

╭─ Global Settings ────────────────────╮
│ Environment  │ development           │
│ Log Level    │ information           │
│ CORS         │ Allow Any Origin: true│
╰──────────────────────────────────────╯

╭─ Data Sources (1 configured) ────────────────╮
│ ID           │ Provider   │ Connection      │
│ postgres_main│ postgresql │ env(DATABASE_URL│
╰──────────────────────────────────────────────╯

╭─ Services (2 enabled) ───────────────────────╮
│ Service ID │ Display Name      │ Status     │
│ odata      │ OData v4          │ ✓ Available│
│ ogc_api    │ OGC API Features  │ ✓ Available│
╰──────────────────────────────────────────────╯

╭─ Layers (3 configured) ──────────────────────╮
│ Layer ID │ Title    │ Data Source   │ Geometry│
│ roads    │ Roads    │ postgres_main │ LineSt..│
│ parcels  │ Parcels  │ postgres_main │ Polygon │
│ buildings│ Buildings│ postgres_main │ Polygon │
╰──────────────────────────────────────────────╯

╭─ Summary ────────────────╮
│ Data Sources: 1          │
│ Services:     2 enabled  │
│ Layers:       3          │
│ Rate Limiting: Disabled  │
╰──────────────────────────╯

✓ Configuration plan complete
```

#### Exit Codes

- `0` - Plan generated successfully
- `1` - Configuration file not found or invalid

---

### config init:v2

Initializes a new Honua Configuration 2.0 file from a template.

#### Syntax

```bash
honua config init:v2 [options]
```

#### Options

| Option                  | Description                           | Default            |
|-------------------------|---------------------------------------|--------------------|
| `--template <NAME>`, `-t` | Configuration template to use       | `minimal`          |
| `--output <PATH>`, `-o`   | Output file path                    | `honua.config.hcl` |
| `--force`, `-f`           | Overwrite existing file             | false              |

#### Templates

| Template        | Description                                      | Use Case        |
|-----------------|--------------------------------------------------|-----------------|
| `minimal`       | Simple SQLite setup                              | Local dev       |
| `production`    | PostgreSQL with Redis, rate limiting             | Production      |
| `test`          | In-memory database for testing                   | Unit tests      |
| `multi-service` | Multiple OGC services enabled                    | Full deployment |

#### Examples

```bash
# Initialize with default template
honua config init:v2

# Initialize with production template
honua config init:v2 --template production

# Custom output path
honua config init:v2 --template test --output config/test.honua

# Force overwrite
honua config init:v2 --force
```

#### Output

```
Initializing configuration with template: minimal

✓ Created configuration file: honua.config.hcl

╭─ Configuration Created Successfully ─────────────╮
│                                                  │
│ Next Steps:                                      │
│                                                  │
│ 1. Edit the configuration file to match your    │
│    database schema: honua.config.hcl             │
│                                                  │
│ 2. Validate your configuration:                 │
│    honua config validate honua.config.hcl       │
│                                                  │
│ 3. Preview what would be configured:            │
│    honua config plan honua.config.hcl           │
│                                                  │
│ 4. Generate configuration from your database:   │
│    honua config introspect "<connection>"       │
│                                                  │
│ 5. Start your Honua server                      │
│                                                  │
╰──────────────────────────────────────────────────╯
```

#### Exit Codes

- `0` - Configuration created successfully
- `1` - File already exists (use `--force` to overwrite)

---

### config introspect

Generates Honua configuration from a database schema.

#### Syntax

```bash
honua config introspect <connection-string> [options]
```

#### Arguments

| Argument            | Description                  | Required |
|---------------------|------------------------------|----------|
| `connection-string` | Database connection string   | Yes      |

#### Options

| Option                      | Description                                          | Default        |
|-----------------------------|------------------------------------------------------|----------------|
| `--provider <NAME>`, `-p`   | Database provider (auto-detected if not specified)   | auto-detect    |
| `--output <PATH>`, `-o`     | Output file path                                     | `layers.hcl`   |
| `--data-source-id <ID>`     | Data source ID in generated config                   | `db`           |
| `--table-pattern <PATTERN>` | SQL LIKE pattern to filter tables                    | -              |
| `--schema-name <SCHEMA>`    | Filter tables by schema name (PostgreSQL)            | -              |
| `--include-system-tables`   | Include system tables                                | false          |
| `--no-row-counts`           | Skip counting rows (faster for large databases)      | false          |
| `--include-views`           | Include views in addition to tables                  | false          |
| `--max-tables <COUNT>`      | Maximum number of tables to introspect               | unlimited      |
| `--layers-only`             | Generate only layer blocks (no data source/services) | false          |
| `--include-services`        | Include service blocks                               | true           |
| `--services <LIST>`         | Comma-separated list of services                     | `odata`        |
| `--use-env-var`             | Use environment variable for connection string       | true           |
| `--env-var-name <NAME>`     | Environment variable name                            | `DATABASE_URL` |
| `--include-connection-pool` | Include connection pool configuration                | true           |
| `--explicit-fields`         | Generate explicit field definitions                  | false          |
| `--force`, `-f`             | Overwrite existing file                              | false          |

#### Supported Providers

- `postgresql` - PostgreSQL (auto-detected)
- `sqlite` - SQLite (auto-detected)
- `sqlserver` - SQL Server (future)
- `mysql` - MySQL/MariaDB (future)

#### Examples

```bash
# Introspect PostgreSQL database
honua config introspect "Host=localhost;Database=gis;Username=user;Password=pass"

# Introspect SQLite database
honua config introspect "Data Source=./data/mydb.db"

# Custom output file
honua config introspect "$DB_URL" --output layers.hcl

# Filter tables by pattern
honua config introspect "$DB_URL" --table-pattern "roads%"

# Filter by schema
honua config introspect "$DB_URL" --schema-name public

# Multiple services
honua config introspect "$DB_URL" --services odata,ogc_api,wfs

# Skip row counts (faster)
honua config introspect "$DB_URL" --no-row-counts

# Generate only layers (no data source block)
honua config introspect "$DB_URL" --layers-only

# Explicit field definitions
honua config introspect "$DB_URL" --explicit-fields

# Limit number of tables
honua config introspect "$DB_URL" --max-tables 10
```

#### Output

```
Introspecting database: postgresql

✓ Database connection successful

Introspecting database schema...

╭─ Schema Summary ────────────────────╮
│ Database          │ gis_production  │
│ Provider          │ postgresql      │
│ Tables Found      │ 15              │
│ Tables w/ Geometry│ 12              │
╰─────────────────────────────────────╯

╭─ Tables ──────────────────────────────────────╮
│ Table           │ Rows    │ Columns │ Geometry│
│ public.roads    │ 45,832  │ 8       │ LineSt..│
│ public.parcels  │ 128,456 │ 12      │ Polygon │
│ public.buildings│ 89,234  │ 10      │ Polygon │
│ ...             │ ...     │ ...     │ ...     │
╰───────────────────────────────────────────────╯

✓ Configuration generated: layers.hcl

╭─ Next Steps ──────────────────────────────────╮
│                                                │
│ 1. Review the generated configuration:        │
│    layers.hcl                                  │
│                                                │
│ 2. Edit as needed (layer titles, etc.)        │
│                                                │
│ 3. Validate the configuration:                │
│    honua config validate layers.hcl           │
│                                                │
│ 4. Preview what would be configured:          │
│    honua config plan layers.hcl               │
│                                                │
│ 5. Integrate into your main config            │
│                                                │
╰────────────────────────────────────────────────╯
```

#### Exit Codes

- `0` - Introspection successful
- `1` - Connection failed or introspection error

---

## Examples

### Development Workflow

```bash
# 1. Initialize configuration
honua config init:v2 --template minimal

# 2. Introspect database
honua config introspect "Data Source=./dev.db" --output layers.hcl

# 3. Merge configurations
cat layers.hcl >> honua.config.hcl

# 4. Validate
honua config validate honua.config.hcl

# 5. Preview
honua config plan honua.config.hcl --show-endpoints

# 6. Run server
dotnet run
```

### Production Deployment

```bash
# 1. Create production config
honua config init:v2 --template production --output honua.production.hcl

# 2. Introspect production database (read-only user recommended)
honua config introspect "$PROD_DB_URL" \
  --output prod-layers.hcl \
  --services odata,ogc_api,wfs \
  --no-row-counts

# 3. Merge
cat prod-layers.hcl >> honua.production.hcl

# 4. Validate (including database checks)
honua config validate honua.production.hcl --full

# 5. Preview
honua config plan honua.production.hcl

# 6. Deploy
docker build -t honua-server .
docker run -e DATABASE_URL="$PROD_DB_URL" honua-server
```

### CI/CD Integration

```bash
# Validate in CI pipeline
honua config validate honua.production.hcl --syntax-only || exit 1

# Generate preview as artifact
honua config plan honua.production.hcl > config-plan.txt
```

### Rapid Iteration

```bash
# Make changes
vim honua.config.hcl

# Validate (1-2 seconds!)
honua config validate honua.config.hcl

# Preview
honua config plan honua.config.hcl

# Test
dotnet run
```

---

## Exit Codes

All commands use consistent exit codes:

| Code | Description                  |
|------|------------------------------|
| `0`  | Success                      |
| `1`  | Error or validation failed   |

### Usage in Scripts

```bash
#!/bin/bash

# Validate configuration before deployment
if honua config validate config/honua.production.hcl --full; then
    echo "✓ Configuration valid, deploying..."
    kubectl apply -f deployment.yaml
else
    echo "✗ Configuration validation failed, aborting deployment"
    exit 1
fi
```

---

## Environment Variables

| Variable        | Description                          | Default            |
|-----------------|--------------------------------------|--------------------|
| `HONUA_CONFIG`  | Path to configuration file           | `honua.config.hcl` |
| `LOG_LEVEL`     | CLI log level                        | `information`      |
| `NO_COLOR`      | Disable colored output               | not set            |

### Examples

```bash
# Use custom config path
export HONUA_CONFIG=config/honua.production.hcl
honua config validate

# Disable colors for CI
export NO_COLOR=1
honua config validate
```

---

## Troubleshooting

### Command Not Found

```bash
# Ensure CLI is installed
dotnet tool list -g | grep honua

# Reinstall if needed
dotnet tool install -g honua-cli
```

### Validation Errors

```bash
# Use verbose mode for details
honua config validate --verbose

# Check specific sections
honua config plan
```

### Connection Issues

```bash
# Test database connection first
honua config validate --full --timeout 30

# Or test manually
psql "$DATABASE_URL" -c "SELECT 1"
```

---

## Getting Help

```bash
# General help
honua --help

# Command-specific help
honua config validate --help
honua config plan --help
honua config init:v2 --help
honua config introspect --help
```

---

## See Also

- [Configuration Reference](./configuration-v2-reference.md)
- [Quick Start Guide](./configuration-v2-quickstart.md)
- [Best Practices](./configuration-v2-best-practices.md)
- [Migration Guide](./configuration-v2-migration.md)
