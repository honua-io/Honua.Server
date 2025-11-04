# Honua.Server.Enterprise.Tests

This test project contains comprehensive test coverage for enterprise database providers, including unit tests, integration tests, and security tests.

## Test Categories

Tests are organized into the following categories:

### 1. Unit Tests
- **SQL Generation Tests** (`QueryBuilderTests.cs`)
  - Tests SQL query building logic without database connectivity
  - Verifies parameterization, quoting, and query structure
  - Fast execution, no external dependencies

- **Capabilities Tests** (`ProviderCapabilitiesTests.cs`)
  - Tests provider capability metadata
  - Verifies feature support flags

- **Interface Tests** (`ProviderInterfaceTests.cs`)
  - Tests provider interface implementations
  - Validates API contracts

- **Security Tests** (`SqlInjectionProtectionTests.cs`)
  - Tests SQL injection protection mechanisms
  - Verifies identifier validation

### 2. Integration Tests
Integration tests verify actual database connectivity and query execution. These tests are automatically skipped when required resources are not available.

#### BigQuery Integration Tests
- **Location**: `BigQuery/BigQueryIntegrationTests.cs`
- **Requirements**: Docker (for BigQuery emulator)
- **Auto-skip**: Tests automatically skip if Docker is not available
- **Coverage**:
  - Query execution with filters, sorting, pagination
  - CRUD operations (Create, Read, Update, Delete)
  - Bulk operations
  - Geometry handling (ST_GEOGPOINT, ST_ASGEOJSON)
  - Parameterized queries and SQL injection prevention

#### Snowflake Integration Tests
- **Location**: `Snowflake/SnowflakeIntegrationTests.cs`
- **Requirements**: Snowflake account credentials
- **Auto-skip**: Tests automatically skip if credentials not provided
- **Coverage**:
  - Query execution with real Snowflake database
  - GEOGRAPHY type handling
  - Bulk operations
  - Parameterized queries

#### Redshift Integration Tests
- **Location**: `Redshift/RedshiftIntegrationTests.cs`
- **Requirements**: AWS Redshift cluster and IAM permissions
- **Auto-skip**: Tests automatically skip if credentials not provided
- **Coverage**:
  - Query execution via Redshift Data API
  - Bulk operations
  - Data API polling behavior
  - Spatial limitations documentation

## Running Tests

### Running All Tests
```bash
dotnet test
```

### Running Only Unit Tests
```bash
dotnet test --filter "Category=Unit"
```

### Running Only Integration Tests
```bash
dotnet test --filter "Category=Integration"
```

### Running Specific Provider Tests
```bash
# BigQuery only
dotnet test --filter "FullyQualifiedName~BigQuery"

# Snowflake only
dotnet test --filter "FullyQualifiedName~Snowflake"

# Redshift only
dotnet test --filter "FullyQualifiedName~Redshift"
```

## Setting Up Integration Tests

### BigQuery Emulator Setup

BigQuery integration tests use the official Google Cloud CLI emulator via Docker.

**Prerequisites:**
1. Docker installed and running
2. No additional configuration needed

**How it works:**
- Tests automatically start a BigQuery emulator container via Testcontainers
- Emulator runs at `localhost:9050`
- Tests are automatically skipped if Docker is unavailable
- Test data is automatically created and cleaned up

**Troubleshooting:**
```bash
# Check if Docker is running
docker info

# Pull the emulator image manually (optional)
docker pull gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators

# View running containers
docker ps
```

### Snowflake Setup

Snowflake integration tests require actual Snowflake credentials.

**Prerequisites:**
1. Active Snowflake account
2. Test database and schema created
3. Test table with required schema

**Environment Variables:**
```bash
export SNOWFLAKE_ACCOUNT="your-account-identifier"
export SNOWFLAKE_USER="your-username"
export SNOWFLAKE_PASSWORD="your-password"
export SNOWFLAKE_DATABASE="test_database"
export SNOWFLAKE_SCHEMA="test_schema"
export SNOWFLAKE_WAREHOUSE="your-warehouse"
export SNOWFLAKE_TEST_TABLE="test_features"  # Optional, defaults to test_features
```

**Test Table Schema:**
```sql
CREATE TABLE test_features (
    id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255),
    description TEXT,
    value INTEGER,
    created_at TIMESTAMP,
    geom GEOGRAPHY
);

-- Insert sample data
INSERT INTO test_features (id, name, description, value, created_at, geom)
VALUES
    ('feature-1', 'Test Feature 1', 'First test', 100, CURRENT_TIMESTAMP(), ST_GEOGPOINT(-122.4194, 37.7749)),
    ('feature-2', 'Test Feature 2', 'Second test', 200, CURRENT_TIMESTAMP(), ST_GEOGPOINT(-118.2437, 34.0522)),
    ('feature-3', 'Test Feature 3', 'Third test', 300, CURRENT_TIMESTAMP(), ST_GEOGPOINT(-73.9352, 40.7306));
```

**Running Tests:**
```bash
# Set environment variables and run tests
dotnet test --filter "FullyQualifiedName~Snowflake"
```

### Redshift Setup

Redshift integration tests use the AWS Redshift Data API.

**Prerequisites:**
1. AWS Redshift cluster running
2. IAM permissions for Redshift Data API:
   - `redshift-data:ExecuteStatement`
   - `redshift-data:DescribeStatement`
   - `redshift-data:GetStatementResult`
3. Test table created

**Environment Variables:**
```bash
export REDSHIFT_CLUSTER_IDENTIFIER="your-cluster-id"
export REDSHIFT_DATABASE="test_db"
export REDSHIFT_DB_USER="your-db-user"
export REDSHIFT_TEST_TABLE="test_features"  # Optional, defaults to test_features
export AWS_REGION="us-east-1"  # Optional, defaults to us-east-1

# AWS credentials (via standard AWS credential chain)
export AWS_ACCESS_KEY_ID="your-access-key"
export AWS_SECRET_ACCESS_KEY="your-secret-key"
```

**Test Table Schema:**
```sql
CREATE TABLE test_features (
    id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255),
    description TEXT,
    value INTEGER,
    created_at TIMESTAMP
);

-- Insert sample data
INSERT INTO test_features (id, name, description, value, created_at)
VALUES
    ('feature-1', 'Test Feature 1', 'First test', 100, CURRENT_TIMESTAMP),
    ('feature-2', 'Test Feature 2', 'Second test', 200, CURRENT_TIMESTAMP),
    ('feature-3', 'Test Feature 3', 'Third test', 300, CURRENT_TIMESTAMP);
```

**Note:** Redshift has limited spatial support. Some tests document these limitations.

**Running Tests:**
```bash
# Set environment variables and run tests
dotnet test --filter "FullyQualifiedName~Redshift"
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Enterprise Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run Unit Tests
        run: dotnet test --filter "Category=Unit"

  bigquery-integration:
    runs-on: ubuntu-latest
    services:
      docker:
        image: docker:dind
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run BigQuery Tests
        run: dotnet test --filter "FullyQualifiedName~BigQuery&Category=Integration"

  snowflake-integration:
    runs-on: ubuntu-latest
    # Only run on main branch or when manually triggered
    if: github.ref == 'refs/heads/main' || github.event_name == 'workflow_dispatch'
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run Snowflake Tests
        env:
          SNOWFLAKE_ACCOUNT: ${{ secrets.SNOWFLAKE_ACCOUNT }}
          SNOWFLAKE_USER: ${{ secrets.SNOWFLAKE_USER }}
          SNOWFLAKE_PASSWORD: ${{ secrets.SNOWFLAKE_PASSWORD }}
          SNOWFLAKE_DATABASE: ${{ secrets.SNOWFLAKE_DATABASE }}
          SNOWFLAKE_SCHEMA: ${{ secrets.SNOWFLAKE_SCHEMA }}
          SNOWFLAKE_WAREHOUSE: ${{ secrets.SNOWFLAKE_WAREHOUSE }}
        run: dotnet test --filter "FullyQualifiedName~Snowflake&Category=Integration"

  redshift-integration:
    runs-on: ubuntu-latest
    # Only run on main branch or when manually triggered
    if: github.ref == 'refs/heads/main' || github.event_name == 'workflow_dispatch'
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Configure AWS Credentials
        uses: aws-actions/configure-aws-credentials@v2
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: us-east-1
      - name: Run Redshift Tests
        env:
          REDSHIFT_CLUSTER_IDENTIFIER: ${{ secrets.REDSHIFT_CLUSTER_IDENTIFIER }}
          REDSHIFT_DATABASE: ${{ secrets.REDSHIFT_DATABASE }}
          REDSHIFT_DB_USER: ${{ secrets.REDSHIFT_DB_USER }}
        run: dotnet test --filter "FullyQualifiedName~Redshift&Category=Integration"
```

## Test Coverage Summary

### BigQuery Provider
- ✅ Query execution with emulator
- ✅ CRUD operations
- ✅ Bulk insert/update/delete
- ✅ Geometry handling (ST_GEOGPOINT vs ST_GEOMPOINT)
- ✅ Parameterization
- ✅ SQL injection prevention
- ✅ Pagination and sorting
- ✅ Spatial filters (bbox)
- ✅ Connectivity testing

### Snowflake Provider
- ✅ Query execution with real Snowflake
- ✅ CRUD operations
- ✅ Bulk operations
- ✅ GEOGRAPHY type handling
- ✅ Parameterization
- ✅ SQL injection prevention
- ✅ Pagination and sorting
- ✅ Spatial filters (bbox)
- ✅ Connectivity testing

### Redshift Provider
- ✅ Query execution via Data API
- ✅ CRUD operations
- ✅ Bulk operations
- ✅ Data API polling behavior
- ✅ Parameterization
- ✅ SQL injection prevention
- ✅ Pagination and sorting
- ✅ Connectivity testing
- ✅ Spatial limitations documentation

## Troubleshooting

### Tests Not Running
```bash
# Verify test discovery
dotnet test --list-tests

# Check if tests are being skipped
dotnet test -v normal
```

### BigQuery Emulator Issues
```bash
# Check Docker daemon
systemctl status docker  # Linux
docker info              # All platforms

# Check if port 9050 is available
netstat -an | grep 9050

# View emulator logs
docker logs <container-id>
```

### Snowflake Connection Issues
```bash
# Test Snowflake connectivity
snowsql -a your-account -u your-user

# Verify environment variables
printenv | grep SNOWFLAKE
```

### Redshift Connection Issues
```bash
# Test AWS credentials
aws sts get-caller-identity

# Test Redshift Data API access
aws redshift-data execute-statement \
  --cluster-identifier your-cluster \
  --database test_db \
  --db-user your-user \
  --sql "SELECT 1"

# Verify environment variables
printenv | grep REDSHIFT
```

## Development Guidelines

### Adding New Integration Tests

1. Use `[SkippableFact]` for tests that require external resources
2. Check for credential/resource availability in test setup
3. Use `Skip.IfNot()` to skip tests gracefully
4. Include proper cleanup in test teardown
5. Add comprehensive test documentation

### Example Test Structure
```csharp
[SkippableFact]
public async Task NewFeature_WithValidInput_WorksCorrectly()
{
    Skip.IfNot(_credentialsAvailable, "Credentials not provided");

    // Arrange
    var testData = CreateTestData();

    try
    {
        // Act
        var result = await _provider.DoSomething(testData);

        // Assert
        result.Should().NotBeNull();
    }
    finally
    {
        // Cleanup
        await CleanupTestData();
    }
}
```

## Additional Resources

- [BigQuery Emulator Documentation](https://cloud.google.com/bigquery/docs/emulator)
- [Snowflake .NET Driver](https://docs.snowflake.com/en/developer-guide/dotnet/dotnet-driver)
- [AWS Redshift Data API](https://docs.aws.amazon.com/redshift/latest/mgmt/data-api.html)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
