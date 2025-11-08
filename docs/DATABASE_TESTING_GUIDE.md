# Database Testing Guide

Complete guide to database testing in Honua.Server with Testcontainers and multi-provider support.

## Table of Contents

- [Overview](#overview)
- [Test Modes](#test-modes)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Supported Database Providers](#supported-database-providers)
- [CI/CD Integration](#cicd-integration)
- [Architecture](#architecture)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## Overview

Honua.Server uses a **three-tier testing strategy** to balance speed, coverage, and cost:

1. **FAST Mode** - SQLite only (default) - 2-3 minutes
2. **STANDARD Mode** - SQLite + PostgreSQL + MySQL - 5-10 minutes
3. **FULL Mode** - All providers (SQLite, PostgreSQL, MySQL, SQL Server, DuckDB) - 15-20 minutes

This approach allows:
- **Developers** to get rapid feedback with SQLite during local development
- **CI/CD** to run appropriate test suites based on the pipeline stage
- **Pre-release** validation to test all database providers comprehensively

## Test Modes

### FAST Mode (Default)

**When to use:** Local development, PR checks

**Providers:** SQLite only

**Requirements:** None (no Docker required)

**Duration:** 2-3 minutes

**Example:**
```bash
# Default mode
dotnet test

# Or explicitly
HONUA_DATABASE_TEST_MODE=fast dotnet test

# Or use the helper script
./tests/scripts/test-fast.sh
```

### STANDARD Mode

**When to use:** Integration testing, merge to main

**Providers:** SQLite, PostgreSQL, MySQL

**Requirements:** Docker must be running

**Duration:** 5-10 minutes

**Example:**
```bash
HONUA_DATABASE_TEST_MODE=standard dotnet test

# Or use the helper script
./tests/scripts/test-standard.sh
```

### FULL Mode

**When to use:** Release validation, nightly builds

**Providers:** SQLite, PostgreSQL, MySQL, SQL Server, DuckDB

**Requirements:** Docker must be running (except DuckDB which is file-based)

**Duration:** 15-20 minutes

**Example:**
```bash
HONUA_DATABASE_TEST_MODE=full dotnet test

# Or use the helper script
./tests/scripts/test-full.sh
```

## Quick Start

### Local Development (Fast)

```bash
# No Docker required - just run tests!
dotnet test --filter "Category=Integration&Database!=None"
```

### Integration Testing (Standard)

```bash
# Start Docker, then run
HONUA_DATABASE_TEST_MODE=standard dotnet test
```

### Pre-Release Validation (Full)

```bash
# Start Docker, then run
HONUA_DATABASE_TEST_MODE=full dotnet test
```

### Test a Specific Provider

```bash
# Enable just PostgreSQL
HONUA_ENABLE_POSTGRES_TESTS=1 dotnet test --filter "Database=Postgres"

# Enable just SQL Server
HONUA_ENABLE_SQLSERVER_TESTS=1 dotnet test --filter "Database=SQLServer"

# Enable just DuckDB
HONUA_ENABLE_DUCKDB_TESTS=1 dotnet test --filter "Database=DuckDB"
```

## Configuration

### Environment Variables

| Variable | Values | Default | Description |
|----------|--------|---------|-------------|
| `HONUA_DATABASE_TEST_MODE` | `fast`, `standard`, `full` | `fast` | Test mode (controls which providers are enabled) |
| `HONUA_ENABLE_POSTGRES_TESTS` | `1`, `true` | (mode-dependent) | Force enable PostgreSQL tests |
| `HONUA_ENABLE_MYSQL_TESTS` | `1`, `true` | (mode-dependent) | Force enable MySQL tests |
| `HONUA_ENABLE_SQLSERVER_TESTS` | `1`, `true` | (mode-dependent) | Force enable SQL Server tests |
| `HONUA_ENABLE_DUCKDB_TESTS` | `1`, `true` | (mode-dependent) | Force enable DuckDB tests |

### Mode vs Provider Matrix

| Provider | FAST Mode | STANDARD Mode | FULL Mode | Can Override |
|----------|-----------|---------------|-----------|--------------|
| SQLite | ✅ Always | ✅ Always | ✅ Always | No (always enabled) |
| PostgreSQL | ❌ | ✅ | ✅ | Yes (via `HONUA_ENABLE_POSTGRES_TESTS=1`) |
| MySQL | ❌ | ✅ | ✅ | Yes (via `HONUA_ENABLE_MYSQL_TESTS=1`) |
| SQL Server | ❌ | ❌ | ✅ | Yes (via `HONUA_ENABLE_SQLSERVER_TESTS=1`) |
| DuckDB | ❌ | ❌ | ✅ | Yes (via `HONUA_ENABLE_DUCKDB_TESTS=1`) |

## Supported Database Providers

### SQLite (Always Enabled)

- **Container:** None (file-based embedded database)
- **Speed:** Fastest
- **Use Cases:** Unit tests, local development, CI/CD fast checks
- **Spatial Support:** Via SpatiaLite extension (optional)
- **Test Class:** `SqliteDataStoreProviderTests`

### PostgreSQL (STANDARD+ Mode)

- **Container:** `postgis/postgis:16-3.4`
- **Testcontainers:** Yes
- **Speed:** Moderate
- **Use Cases:** Production-like integration testing
- **Spatial Support:** PostGIS
- **Test Class:** `PostgresDataStoreProviderTests`

### MySQL (STANDARD+ Mode)

- **Container:** `mysql:8.0`
- **Testcontainers:** Yes
- **Speed:** Moderate
- **Use Cases:** Production-like integration testing
- **Spatial Support:** MySQL spatial types
- **Test Class:** `MySqlDataStoreProviderTests`

### SQL Server (FULL Mode Only)

- **Container:** `mcr.microsoft.com/mssql/server:2022-latest`
- **Testcontainers:** Yes
- **Speed:** Slower (large container image)
- **Use Cases:** Enterprise environment validation
- **Spatial Support:** SQL Server geometry types
- **Test Class:** `SqlServerDataStoreProviderTests`

### DuckDB (FULL Mode Only)

- **Container:** None (file-based embedded database)
- **Testcontainers:** No
- **Speed:** Fast
- **Use Cases:** Analytics workloads, OLAP scenarios
- **Spatial Support:** DuckDB spatial extension
- **Test Class:** `DuckDBDataStoreProviderTests`

## CI/CD Integration

### Recommended Pipeline Stages

```yaml
# Example GitHub Actions workflow

# Stage 1: PR Checks (FAST Mode)
pr-checks:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - name: Run Fast Tests
      run: ./tests/scripts/test-fast.sh
      env:
        HONUA_DATABASE_TEST_MODE: fast

# Stage 2: Merge to Main (STANDARD Mode)
integration-tests:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - name: Run Standard Tests
      run: ./tests/scripts/test-standard.sh
      env:
        HONUA_DATABASE_TEST_MODE: standard

# Stage 3: Nightly/Release (FULL Mode)
comprehensive-tests:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - name: Run Full Test Suite
      run: ./tests/scripts/test-full.sh
      env:
        HONUA_DATABASE_TEST_MODE: full
```

### Timing Estimates

| Pipeline Stage | Mode | Duration | When to Run |
|----------------|------|----------|-------------|
| PR Checks | FAST | 2-3 min | Every PR commit |
| Integration Tests | STANDARD | 5-10 min | Merge to main |
| Comprehensive Tests | FULL | 15-20 min | Nightly, releases |

## Architecture

### Key Components

#### 1. DatabaseTestConfiguration

Central configuration class that determines which providers are enabled.

**Location:** `tests/Honua.Server.Core.Tests.Shared/TestConfiguration/DatabaseTestConfiguration.cs`

**Usage in Tests:**
```csharp
if (!DatabaseTestConfiguration.IsPostgresEnabled)
{
    throw new SkipException("PostgreSQL tests disabled");
}
```

#### 2. MultiProviderTestFixture

Unified fixture that initializes all enabled database providers with identical test data.

**Location:** `tests/Honua.Server.Core.Tests.Shared/TestInfrastructure/MultiProviderTestFixture.cs`

**Features:**
- Configuration-aware initialization
- Testcontainers for PostgreSQL, MySQL, SQL Server
- File-based databases (SQLite, DuckDB)
- Automatic cleanup
- Shared across test collections

#### 3. DataStoreProviderTestsBase

Abstract base class for provider-specific tests.

**Location:** `tests/Honua.Server.Core.Tests.Data/Data/DataStoreProviderTestsBase.cs`

**Eliminates duplicate test code across:**
- PostgreSQL tests
- MySQL tests
- SQL Server tests
- SQLite tests
- DuckDB tests

### Test Organization

```
tests/
├── Honua.Server.Core.Tests.Shared/
│   ├── TestConfiguration/
│   │   ├── DatabaseTestMode.cs
│   │   └── DatabaseTestConfiguration.cs
│   └── TestInfrastructure/
│       └── MultiProviderTestFixture.cs
├── Honua.Server.Core.Tests.Data/
│   └── Data/
│       ├── DataStoreProviderTestsBase.cs
│       ├── Postgres/PostgresDataStoreProviderTests.cs
│       ├── MySQL/MySqlDataStoreProviderTests.cs
│       ├── SqlServer/SqlServerDataStoreProviderTests.cs
│       ├── Sqlite/SqliteDataStoreProviderTests.cs
│       └── DuckDB/DuckDBDataStoreProviderTests.cs
└── scripts/
    ├── test-fast.sh
    ├── test-standard.sh
    └── test-full.sh
```

## Best Practices

### For Developers

1. **Use FAST mode locally** - Get rapid feedback with SQLite
2. **Test with STANDARD mode before PR** - Verify PostgreSQL/MySQL compatibility
3. **Let CI handle FULL mode** - Don't waste time running all providers locally

### For Test Authors

1. **Always check configuration** - Tests should respect `DatabaseTestConfiguration`
2. **Provide clear skip reasons** - Help users understand why tests are skipped
3. **Use MultiProviderTestFixture** - Leverage shared infrastructure
4. **Inherit from DataStoreProviderTestsBase** - Eliminate duplicate test code

### For CI/CD

1. **Fast feedback loop** - Use FAST mode for PR checks
2. **Comprehensive validation** - Use FULL mode for releases
3. **Resource optimization** - Only start containers when needed
4. **Parallel execution** - Run test classes in parallel when possible

## Troubleshooting

### Tests are Being Skipped

**Problem:** All database tests are skipped

**Solution:**
```bash
# Check current configuration
export HONUA_DATABASE_TEST_MODE=standard
dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed"
```

### Docker Containers Not Starting

**Problem:** Testcontainers failing to start

**Solution:**
1. Ensure Docker is running: `docker info`
2. Check Docker resources (memory, CPU)
3. Pull images manually: `docker pull postgis/postgis:16-3.4`
4. Check container logs in test output

### Slow Test Execution

**Problem:** Tests taking too long

**Solution:**
1. Use FAST mode for local development
2. Run specific test classes: `dotnet test --filter "FullyQualifiedName~PostgresDataStoreProviderTests"`
3. Enable parallel execution in test settings
4. Use shared fixtures (`ICollectionFixture<T>`)

### Port Conflicts

**Problem:** Testcontainers port conflicts

**Solution:**
- Testcontainers uses random ports by default
- If conflicts persist, stop other containers: `docker ps` and `docker stop <container>`

### SQL Server Container Issues

**Problem:** SQL Server container failing to start

**Solution:**
1. Ensure sufficient memory (SQL Server requires ~2GB)
2. Check Docker resource limits
3. Use explicit mode: `HONUA_ENABLE_SQLSERVER_TESTS=1`
4. Verify image: `docker pull mcr.microsoft.com/mssql/server:2022-latest`

## Migration from Docker Compose

### Before (Docker Compose)

```bash
# Start services
docker-compose -f tests/docker-compose.shared-test-env.yml up -d

# Run tests
dotnet test

# Stop services
docker-compose -f tests/docker-compose.shared-test-env.yml down
```

### After (Testcontainers)

```bash
# Just run tests - containers managed automatically!
dotnet test

# Or use mode-specific scripts
./tests/scripts/test-standard.sh
```

### Benefits of Migration

✅ **No manual setup** - Containers start/stop automatically
✅ **Better isolation** - Each test class can have its own containers
✅ **Port management** - Random ports prevent conflicts
✅ **Automatic cleanup** - Containers always cleaned up
✅ **Parallel-friendly** - Multiple test runners work simultaneously
✅ **Configuration-driven** - Easy to control which providers run

## Additional Resources

- [Testcontainers Documentation](https://dotnet.testcontainers.org/)
- [xUnit Collection Fixtures](https://xunit.net/docs/shared-context#collection-fixture)
- [GitHub Actions .NET Testing](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net)

## Summary

The Honua.Server database testing strategy provides:

- 🚀 **Fast local development** with SQLite (FAST mode)
- 🔄 **Comprehensive integration testing** with PostgreSQL/MySQL (STANDARD mode)
- ✅ **Complete validation** across all providers (FULL mode)
- 🐳 **Automatic container management** via Testcontainers
- ⚙️ **Flexible configuration** via environment variables
- 📊 **Clear test organization** with shared fixtures and base classes

Choose the right mode for your use case and let the infrastructure handle the rest!
