# Quick Start Testing Guide

**For Developers**: This guide shows you how to quickly run the comprehensive test suite for Honua GIS Server.

## Prerequisites

- Docker Desktop installed and running
- .NET 9.0 SDK installed
- Bash shell (Git Bash on Windows, native on Linux/macOS)

## Quick Test Commands

### 1. Run All Unit Tests (Fast, ~30 seconds)

```bash
# From the tests/ directory
dotnet test Honua.Server.Core.Tests/ --logger "console;verbosity=normal"
```

### 2. Run PostgreSQL Optimization Tests (Complete Suite)

```bash
# From the tests/ directory
chmod +x run-postgres-optimization-tests.sh
./run-postgres-optimization-tests.sh
```

This will:
- Start PostgreSQL with PostGIS
- Run migrations
- Load 10,000+ test features
- Run unit tests
- Run integration tests
- Keep database running for inspection

**Options:**
```bash
# Run with benchmarks
./run-postgres-optimization-tests.sh --benchmarks

# Skip setup (if already running)
./run-postgres-optimization-tests.sh --skip-setup

# Cleanup after tests
./run-postgres-optimization-tests.sh --cleanup

# All together
./run-postgres-optimization-tests.sh --benchmarks --cleanup
```

### 3. Run Discovery Tests

```bash
# Unit tests only
dotnet test Honua.Server.Core.Tests/ --filter "FullyQualifiedName~Discovery"

# Integration tests (requires Docker)
cd Honua.Server.Integration.Tests/Discovery
docker-compose -f docker-compose.discovery-tests.yml up -d
dotnet test --filter "Category=Discovery"
docker-compose -f docker-compose.discovery-tests.yml down
```

### 4. Run Startup Optimization Tests

```bash
# All startup-related tests
dotnet test Honua.Server.Core.Tests/ --filter "FullyQualifiedName~Lazy|FullyQualifiedName~Warmup|FullyQualifiedName~Startup"
```

### 5. Run Integration Tests (All)

```bash
# Requires Docker for Testcontainers
dotnet test Honua.Server.Integration.Tests/ --logger "console;verbosity=normal"
```

### 6. Run Specific Test Categories

```bash
# PostgreSQL optimizations only
dotnet test --filter "Category=PostgresOptimizations"

# Discovery only
dotnet test --filter "Category=Discovery"

# Unit tests only (fast)
dotnet test --filter "Category=Unit"

# Integration tests only (slower, requires Docker)
dotnet test --filter "Category=Integration"
```

## Verify Test Setup

```bash
# Check if Testcontainers is properly configured
./verify-testcontainers.sh
```

## Inspect Test Database

After running the optimization tests, the PostgreSQL container stays running:

```bash
# Connect with psql
psql "Host=localhost;Port=5433;Database=honua_test;Username=postgres;Password=test"

# Or use pgAdmin (if started with debug profile)
docker-compose -f docker-compose.postgres-optimization-tests.yml --profile debug up -d pgadmin
# Navigate to http://localhost:5050
# Login: admin@honua.io / admin
```

## Cleanup

```bash
# Stop PostgreSQL optimization test containers
docker-compose -f docker-compose.postgres-optimization-tests.yml down

# Stop discovery test containers
cd Honua.Server.Integration.Tests/Discovery
docker-compose down

# Remove all test volumes (clean slate)
docker-compose -f docker-compose.postgres-optimization-tests.yml down -v
```

## Test File Locations

### Unit Tests
```
tests/Honua.Server.Core.Tests/
├── Data/
│   ├── ConnectionPoolWarmupServiceTests.cs
│   └── Postgres/
│       ├── PostgresFunctionRepositoryTests.cs
│       └── OptimizedPostgresFeatureOperationsTests.cs
├── Discovery/
│   ├── PostGisTableDiscoveryServiceTests.cs
│   ├── CachedTableDiscoveryServiceTests.cs
│   ├── DynamicODataModelProviderTests.cs
│   └── DynamicOgcCollectionProviderTests.cs
├── DependencyInjection/
│   └── LazyServiceExtensionsTests.cs
├── Hosting/
│   ├── LazyRedisInitializerTests.cs
│   └── StartupProfilerTests.cs
├── HealthChecks/
│   └── WarmupHealthCheckTests.cs
└── Configuration/
    └── ConnectionPoolWarmupOptionsTests.cs
```

### Integration Tests
```
tests/Honua.Server.Integration.Tests/
├── Data/
│   ├── PostgresOptimizationsIntegrationTests.cs
│   └── TestData_PostgresOptimizations.sql
└── Discovery/
    ├── PostGisDiscoveryIntegrationTests.cs
    ├── ZeroConfigDemoE2ETests.cs
    ├── docker-compose.discovery-tests.yml
    └── test-data/
        └── 01-create-test-tables.sql
```

### Infrastructure
```
tests/
├── docker-compose.postgres-optimization-tests.yml
├── run-postgres-optimization-tests.sh
└── verify-testcontainers.sh
```

## Test Coverage

| Area | Unit Tests | Integration Tests | Total Tests |
|------|-----------|-------------------|-------------|
| PostgreSQL Optimizations | 40 | 15+ | 55+ |
| Auto-Discovery | 51+ | 20+ | 71+ |
| Startup Optimizations | 75 | - | 75 |
| **TOTAL** | **166+** | **35+** | **201+** |

## Troubleshooting

### "Docker is not running"
```bash
# Start Docker Desktop
# On Linux: sudo systemctl start docker
```

### "Port already in use (5433)"
```bash
# Find what's using the port
lsof -i :5433  # Linux/macOS
netstat -ano | findstr :5433  # Windows

# Stop conflicting container
docker ps
docker stop <container-id>
```

### "Testcontainers not working"
```bash
# Run verification script
./verify-testcontainers.sh

# Check Docker permissions (Linux)
sudo usermod -aG docker $USER
# Log out and log back in
```

### "Tests timeout"
```bash
# Increase timeout in test
# Default is usually 30s, may need more for Testcontainers startup

# Or skip integration tests and run unit tests only
dotnet test --filter "Category=Unit"
```

### "Migration not found"
```bash
# Ensure you're in the tests/ directory
cd tests/

# Check migration file exists
ls -l ../src/Honua.Server.Core/Data/Migrations/014_PostgresOptimizations.sql
```

## Performance Benchmarks

To run performance benchmarks comparing optimized vs non-optimized functions:

```bash
./run-postgres-optimization-tests.sh --benchmarks

# Or manually
cd Honua.Server.Benchmarks
dotnet run -c Release --filter "*PostgresOptimization*"

# Results in: BenchmarkDotNet.Artifacts/results/
```

## CI/CD Integration

These tests are designed to run in CI/CD pipelines:

```yaml
# Example GitHub Actions
- name: Run Unit Tests
  run: dotnet test tests/Honua.Server.Core.Tests/ --logger "trx;LogFileName=unit-tests.trx"

- name: Run Integration Tests
  run: |
    cd tests
    ./run-postgres-optimization-tests.sh --cleanup
```

## Watch Mode (Development)

Run tests in watch mode for rapid feedback during development:

```bash
# Watch all tests
dotnet watch test --project Honua.Server.Core.Tests/

# Watch specific tests
dotnet watch test --project Honua.Server.Core.Tests/ --filter "FullyQualifiedName~PostgresFunction"
```

## Code Coverage

Generate code coverage reports:

```bash
# Install coverlet
dotnet add package coverlet.msbuild

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Generate HTML report (requires ReportGenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report
```

## Further Reading

- [TEST_INFRASTRUCTURE_COMPLETE_SUMMARY.md](TEST_INFRASTRUCTURE_COMPLETE_SUMMARY.md) - Complete implementation details
- [POSTGRES_OPTIMIZATION_TESTS.md](POSTGRES_OPTIMIZATION_TESTS.md) - PostgreSQL optimization test specs
- [TESTCONTAINERS_GUIDE.md](TESTCONTAINERS_GUIDE.md) - Testcontainers usage guide
- [TEST_CATEGORIZATION_GUIDE.md](TEST_CATEGORIZATION_GUIDE.md) - How to categorize tests

---

**Quick Reference Card**

```bash
# ✅ Most Common Commands

# All unit tests (fast)
dotnet test Honua.Server.Core.Tests/

# PostgreSQL optimization suite
./run-postgres-optimization-tests.sh

# Integration tests (slower)
dotnet test Honua.Server.Integration.Tests/

# Specific category
dotnet test --filter "Category=PostgresOptimizations"

# Cleanup
docker-compose -f docker-compose.postgres-optimization-tests.yml down -v
```

---

**Last Updated**: 2025-11-02
