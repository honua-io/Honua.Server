# GitHub Actions CI/CD Setup

This document describes the GitHub Actions workflows configured for the Honua project to ensure reliable test execution and continuous integration.

## Workflow Overview

### 1. Continuous Integration (`ci.yml`)

**Triggers:**
- Push to `master`, `main`, or `develop` branches
- Pull requests to `master` or `main` branches

**Jobs:**
- **Build & Validate**: Builds the solution and validates compilation
- **Unit Tests (Fast)**: Runs fast unit tests excluding infrastructure dependencies
- **Infrastructure Tests**: Runs infrastructure-dependent tests (conditional)
- **Integration Tests**: Runs integration tests (on main branches or with label)
- **Performance Tests**: Runs performance tests (on main branches or with label)
- **Test Summary**: Aggregates and publishes test results

**Features:**
- GDAL dependency installation
- NuGet package caching
- Parallel test execution
- Test result publishing
- Conditional test execution based on branch/labels

### 2. Nightly Comprehensive Tests (`nightly-tests.yml`)

**Triggers:**
- Scheduled daily at 2 AM UTC
- Manual workflow dispatch with options

**Features:**
- Runs all test categories including stress and long-running tests
- Code coverage collection and reporting
- Memory leak detection
- Extended timeout (120 minutes)
- Coverage upload to Codecov

### 3. Docker Container Tests (`docker-tests.yml`)

**Triggers:**
- Changes to Docker-related files
- Manual workflow dispatch
- Pull requests with `docker-tests` label

**Features:**
- Tests containerized test execution
- Docker image security scanning with Trivy
- Isolated test environment validation

### 4. Performance Monitoring (`performance-monitoring.yml`)

**Triggers:**
- Weekly schedule (Sundays at 3 AM UTC)
- Manual workflow dispatch with configurable duration

**Features:**
- Performance benchmark execution
- Memory usage monitoring
- Automatic issue creation on performance regressions
- Long-term performance trend tracking

## Test Categories and Filtering

### Available Test Categories

- **Unit**: Standard unit tests
- **Integration**: Tests requiring external dependencies
- **Performance**: Performance and benchmark tests
- **Infrastructure**: Tests requiring specific infrastructure (GDAL, databases)
- **MemoryLeak**: Memory leak detection tests
- **Stress**: High-load stress tests
- **LongRunning**: Tests that take significant time to complete

### Filtering Examples

```bash
# Fast unit tests only
--filter "TestCategory=Unit&TestCategory!=Infrastructure&TestCategory!=LongRunning&TestCategory!=Stress"

# Infrastructure tests only
--filter "TestCategory=Infrastructure"

# All integration tests except long-running ones
--filter "TestCategory=Integration&TestCategory!=LongRunning"

# Performance tests excluding stress tests
--filter "TestCategory=Performance&TestCategory!=Stress"
```

## Branch Strategy and Test Execution

### Pull Requests
- **Always Run**: Fast unit tests, build validation
- **Conditional**: Infrastructure tests, integration tests, performance tests
- **Labels**: Use PR labels to trigger specific test suites
  - `run-infrastructure-tests`: Run infrastructure-dependent tests
  - `run-integration-tests`: Run integration test suite
  - `run-performance-tests`: Run performance benchmarks
  - `docker-tests`: Run Docker container tests

### Main Branches (master/main)
- **Always Run**: All test categories except stress and long-running
- **Scheduled**: Comprehensive nightly tests including all categories

### Development Branch
- **Always Run**: Fast unit tests, build validation
- **Manual**: All other test categories via workflow dispatch

## Optimization Features

### Caching
- **NuGet packages**: Cached based on project file hashes
- **Build artifacts**: Shared between jobs to avoid rebuilding

### Parallel Execution
- Multiple test jobs run in parallel where possible
- Test categories are separated for faster feedback

### Conditional Execution
- Expensive test suites only run when necessary
- Branch-based and label-based triggers

### Resource Management
- Appropriate timeouts for different test types
- Memory-efficient test execution

## Configuration Files

### `test.runsettings`
- Test execution configuration
- Timeout settings
- Parallel execution settings
- Code coverage configuration

### PowerShell Scripts
- `scripts/run-unit-tests.ps1`: Unit test execution with filtering
- `scripts/run-integration-tests.ps1`: Integration test execution
- `scripts/run-performance-tests.ps1`: Performance test execution
- `scripts/run-all-tests.ps1`: Comprehensive test runner

## Monitoring and Reporting

### Test Results
- Published to GitHub Actions UI
- TRX format for Visual Studio integration
- Downloadable artifacts for offline analysis

### Code Coverage
- Collected during test execution
- Uploaded to Codecov for trend analysis
- HTML reports in workflow artifacts

### Performance Monitoring
- Weekly performance benchmarks
- Automatic issue creation for regressions
- Long-term performance trend tracking

## Troubleshooting

### Common Issues

#### GDAL Dependencies
If GDAL-related tests fail:
1. Check GDAL installation step in workflow
2. Verify environment variables are set correctly
3. Ensure proj.db is accessible

#### Test Timeouts
For timeout issues:
1. Check if tests are categorized correctly
2. Consider excluding long-running tests from fast CI
3. Adjust timeout values in `test.runsettings`

#### Memory Issues
For memory-related failures:
1. Check memory leak test results
2. Monitor resource usage in workflow logs
3. Consider running fewer tests in parallel

### Debugging

#### Local Testing
```bash
# Test the same filters used in CI
./scripts/run-unit-tests.ps1 -Fast -ExcludeInfrastructure

# Test Docker environment locally
docker-compose -f docker-compose.test.yml up honua-tests
```

#### Workflow Debugging
- Enable debug logging by setting repository secret `ACTIONS_STEP_DEBUG` to `true`
- Use `workflow_dispatch` triggers for manual testing
- Download artifacts for detailed analysis

## Best Practices

### Test Organization
1. Categorize tests appropriately
2. Keep fast tests separate from slow ones
3. Ensure infrastructure tests can run in isolation

### Performance
1. Use test filters to run only necessary tests
2. Cache dependencies appropriately
3. Run expensive tests only when needed

### Maintenance
1. Review and update test categories regularly
2. Monitor test execution times
3. Update dependencies and tools periodically
4. Review and optimize workflow performance monthly