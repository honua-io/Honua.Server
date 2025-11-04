# PostgreSQL Optimization Tests - Quick Reference

## One-Line Commands

```bash
# Run ALL tests (unit + integration)
./tests/run-postgres-optimization-tests.sh

# Run ALL tests + benchmarks
./tests/run-postgres-optimization-tests.sh --benchmarks

# Run tests and cleanup containers
./tests/run-postgres-optimization-tests.sh --cleanup

# Unit tests only (fast, no database)
dotnet test tests/Honua.Server.Core.Tests/ --filter "FullyQualifiedName~PostgresOptimization"

# Integration tests only (requires database)
export TEST_DATABASE_URL="Host=localhost;Port=5433;Database=honua_test;Username=postgres;Password=test"
dotnet test tests/Honua.Server.Integration.Tests/ --filter "Category=PostgresOptimizations"

# Benchmarks only
export BENCHMARK_DATABASE_URL="Host=localhost;Port=5433;Database=honua_test;Username=postgres;Password=test"
cd tests/Honua.Server.Benchmarks && dotnet run -c Release --filter "*PostgresOptimization*"
```

## Docker Commands

```bash
# Start database
docker-compose -f tests/docker-compose.postgres-optimization-tests.yml up -d

# Check status
docker-compose -f tests/docker-compose.postgres-optimization-tests.yml ps

# View logs
docker logs honua-postgres-optimization-test

# Connect to database
docker exec -it honua-postgres-optimization-test psql -U postgres -d honua_test

# Stop and cleanup
docker-compose -f tests/docker-compose.postgres-optimization-tests.yml down -v
```

## Database Commands

```bash
# Check if functions exist
docker exec honua-postgres-optimization-test psql -U postgres -d honua_test -c \
  "SELECT COUNT(*) FROM pg_proc WHERE proname LIKE 'honua_%';"

# View test tables
docker exec honua-postgres-optimization-test psql -U postgres -d honua_test -c \
  "SELECT schemaname, tablename, n_live_tup FROM pg_stat_user_tables WHERE schemaname = 'test_optimizations';"

# Re-run migration
docker exec -i honua-postgres-optimization-test psql -U postgres -d honua_test \
  < src/Honua.Server.Core/Data/Migrations/014_PostgresOptimizations.sql

# Re-load test data
docker exec -i honua-postgres-optimization-test psql -U postgres -d honua_test \
  < tests/Honua.Server.Integration.Tests/Data/TestData_PostgresOptimizations.sql
```

## File Locations

```
tests/
├── Honua.Server.Core.Tests/Data/Postgres/
│   ├── PostgresFunctionRepositoryTests.cs                    # Unit tests for repository
│   └── OptimizedPostgresFeatureOperationsTests.cs            # Unit tests for optimization layer
│
├── Honua.Server.Integration.Tests/Data/
│   ├── PostgresOptimizationsIntegrationTests.cs              # Integration tests
│   └── TestData_PostgresOptimizations.sql                    # Test data generator
│
├── Honua.Server.Benchmarks/
│   └── PostgresOptimizationBenchmarks.cs                     # Performance benchmarks
│
├── docker-compose.postgres-optimization-tests.yml            # Docker environment
├── run-postgres-optimization-tests.sh                        # Test runner
├── POSTGRES_OPTIMIZATION_TESTS.md                            # Full documentation
└── POSTGRES_OPTIMIZATION_TESTS_QUICKREF.md                   # This file
```

## Test Coverage

| Component | Test Type | File | Test Count |
|-----------|-----------|------|------------|
| PostgresFunctionRepository | Unit | PostgresFunctionRepositoryTests.cs | 16 tests |
| OptimizedPostgresFeatureOperations | Unit | OptimizedPostgresFeatureOperationsTests.cs | 12 tests |
| All Functions | Integration | PostgresOptimizationsIntegrationTests.cs | 25+ tests |
| Performance | Benchmark | PostgresOptimizationBenchmarks.cs | 10+ scenarios |

## Common Issues

| Problem | Solution |
|---------|----------|
| Port 5433 in use | `docker-compose down` or edit port in docker-compose.yml |
| Function not found | Re-run migration: `docker exec -i ... < 014_PostgresOptimizations.sql` |
| No test data | Re-run: `docker exec -i ... < TestData_PostgresOptimizations.sql` |
| Connection timeout | Wait for PostgreSQL: `docker exec ... pg_isready -U postgres -d honua_test` |
| Build errors | `dotnet restore && dotnet build` |

## CI/CD

**GitHub Actions Workflow:** `.github/workflows/postgres-optimization-tests.yml`

**Triggers:**
- Push to main/master/develop/dev
- Pull requests
- Manual dispatch (with benchmark option)

**Jobs:**
1. Unit Tests (~30s)
2. Integration Tests (~2-3min)
3. Benchmarks (optional, ~5-10min)

## Expected Performance

| Operation | Improvement | Baseline |
|-----------|-------------|----------|
| Feature Query (100) | 2.5x faster | Traditional: 45ms → Optimized: 18ms |
| Feature Query (1000) | 6x faster | Traditional: 320ms → Optimized: 52ms |
| Count Query | 3.5x faster | Traditional: 28ms → Optimized: 8ms |
| MVT Tile | 12x faster | Traditional: 180ms → Optimized: 15ms |
| Aggregation | 20x faster | Traditional: 140ms → Optimized: 7ms |
| Fast Count (estimate) | 93x faster | Traditional: 28ms → Optimized: 0.3ms |

## Debug Checklist

- [ ] PostgreSQL container running? `docker ps`
- [ ] Database ready? `docker exec ... pg_isready`
- [ ] Functions created? `SELECT COUNT(*) FROM pg_proc WHERE proname LIKE 'honua_%'`
- [ ] Test data loaded? `SELECT COUNT(*) FROM test_optimizations.test_cities`
- [ ] Connection string correct? `echo $TEST_DATABASE_URL`
- [ ] Dependencies restored? `dotnet restore`
- [ ] Latest code? `git pull && dotnet build`

## Quick Test Verification

```bash
# Verify everything works
cd tests

# 1. Start database
docker-compose -f docker-compose.postgres-optimization-tests.yml up -d
sleep 10  # Wait for startup

# 2. Load migrations and data
docker exec -i honua-postgres-optimization-test psql -U postgres -d honua_test \
  < ../src/Honua.Server.Core/Data/Migrations/014_PostgresOptimizations.sql
docker exec -i honua-postgres-optimization-test psql -U postgres -d honua_test \
  < Honua.Server.Integration.Tests/Data/TestData_PostgresOptimizations.sql

# 3. Verify functions
docker exec honua-postgres-optimization-test psql -U postgres -d honua_test \
  -c "SELECT proname FROM pg_proc WHERE proname LIKE 'honua_%' ORDER BY proname;"

# 4. Run a simple test
export TEST_DATABASE_URL="Host=localhost;Port=5433;Database=honua_test;Username=postgres;Password=test"
dotnet test Honua.Server.Integration.Tests/ \
  --filter "FullyQualifiedName~PostgresOptimizationsIntegrationTests.AllOptimizationFunctions_ShouldExist"

# 5. Cleanup
docker-compose -f docker-compose.postgres-optimization-tests.yml down -v
```

## Resources

- Full Documentation: `tests/POSTGRES_OPTIMIZATION_TESTS.md`
- Migration SQL: `src/Honua.Server.Core/Data/Migrations/014_PostgresOptimizations.sql`
- Repository Code: `src/Honua.Server.Core/Data/Postgres/PostgresFunctionRepository.cs`
- Optimization Layer: `src/Honua.Server.Core/Data/Postgres/OptimizedPostgresFeatureOperations.cs`
