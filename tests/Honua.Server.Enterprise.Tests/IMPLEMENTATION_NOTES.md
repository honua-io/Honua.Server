# Enterprise Integration Tests - Implementation Notes

## Current Status

The enterprise integration test files have been created but require fixes before they will compile. This document details the remaining work.

## Files Created

1. ✅ `/tests/Honua.Server.Enterprise.Tests/BigQuery/BigQueryEmulatorFixture.cs` - BigQuery emulator setup
2. ✅ `/tests/Honua.Server.Enterprise.Tests/BigQuery/BigQueryIntegrationTests.cs` - BigQuery integration tests
3. ✅ `/tests/Honua.Server.Enterprise.Tests/Snowflake/SnowflakeIntegrationTests.cs` - Snowflake integration tests
4. ✅ `/tests/Honua.Server.Enterprise.Tests/Redshift/RedshiftIntegrationTests.cs` - Redshift integration tests
5. ✅ `/tests/Honua.Server.Enterprise.Tests/README.md` - Comprehensive testing documentation
6. ✅ `QueryBuilderTests.cs` - Fixed trait from Integration to Unit

## Compilation Fixes Required

### 1. ServiceDefinition Missing Required Members

**Error**: `Required member 'ServiceDefinition.FolderId' must be set`

**Files Affected**:
- BigQueryIntegrationTests.cs (line 43)
- SnowflakeIntegrationTests.cs (line 59)
- RedshiftIntegrationTests.cs (line 56)

**Fix**: Add missing required properties to ServiceDefinition initialization:
```csharp
_service = new ServiceDefinition
{
    Id = "test-service",
    Title = "Test Service",
    FolderId = "root",  // ADD THIS
    ServiceType = "FeatureServer",  // ADD THIS
    DataSourceId = "test-datasource"
};
```

### 2. FluentAssertions Method Name Changes

**Errors**:
- `HaveCountLessOrEqualTo` does not exist (use `HaveCountLessThanOrEqualTo`)
- `BeGreaterOrEqualTo` does not exist (use `BeGreaterThanOrEqualTo`)

**Files Affected**:
- SnowflakeIntegrationTests.cs (line 152, 245, 488)
- RedshiftIntegrationTests.cs (line 138, 211, 537)
- BigQueryIntegrationTests.cs (line 367)

**Fix**: Replace method names:
```csharp
// OLD
results.Should().HaveCountLessOrEqualTo(5);
count.Should().BeGreaterOrEqualTo(0);

// NEW
results.Should().HaveCountLessThanOrEqualTo(5);
count.Should().BeGreaterThanOrEqualTo(0);
```

### 3. Missing async LINQ Extension Methods

**Error**: `IAsyncEnumerable<FeatureRecord>' does not contain a definition for 'FirstOrDefaultAsync'`

**Files Affected**:
- SnowflakeIntegrationTests.cs (line 255)
- RedshiftIntegrationTests.cs (line 221)

**Fix**: Add using directive and use System.Linq.Async:
```csharp
// Add to top of file
using System.Linq;

// Change
var firstRecord = await _provider!.QueryAsync(...).FirstOrDefaultAsync();

// To
FeatureRecord? firstRecord = null;
await foreach (var record in _provider!.QueryAsync(...))
{
    firstRecord = record;
    break;
}
```

### 4. Skip.Always Method Does Not Exist

**Error**: `'Skip' does not contain a definition for 'Always'`

**Files Affected**:
- SnowflakeIntegrationTests.cs (line 259)
- RedshiftIntegrationTests.cs (line 225)

**Fix**: Use throw statement instead:
```csharp
// OLD
Skip.Always("No test data available");

// NEW
throw new SkipException("No test data available in Snowflake table");
```

### 5. ToAsyncEnumerable Extension Missing

**Error**: Does not contain a definition for 'ToAsyncEnumerable'

**Files Affected**: Multiple locations in all three integration test files

**Fix**: Add System.Linq.Async package reference or create helper:
```csharp
// Helper method to add to test class
private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
{
    foreach (var item in source)
    {
        yield return item;
    }
}

// Then use:
await _provider!.BulkInsertAsync(..., ToAsyncEnumerable(records));
```

### 6. BigQuery Types Missing

**Errors**:
- `Dataset` could not be found
- `Table` could not be found

**File**: BigQueryEmulatorFixture.cs (lines 90, 107)

**Fix**: Add Google.Cloud.BigQuery.V2 namespace or simplify emulator initialization:
```csharp
// Current code tries to create dataset/table objects
// Simplify to just execute SQL statements instead
```

## Package Dependencies Added

The following packages were already added to the .csproj:
- ✅ Xunit.SkippableFact (1.4.13)
- ✅ Testcontainers (3.10.0)

## Additional Package Needed

- System.Linq.Async - for async LINQ operations

## Test Structure

All integration tests follow this pattern:

1. **Auto-skip when resources unavailable** - Tests use `[SkippableFact]` and check for Docker/credentials in setup
2. **Proper cleanup** - Use IAsyncLifetime for setup/teardown
3. **Isolation** - Each test creates unique IDs to avoid conflicts
4. **Comprehensive coverage** - Tests cover CRUD, bulk operations, spatial queries, security

## Next Steps

1. Apply all compilation fixes listed above
2. Add System.Linq.Async package
3. Test with Docker available (BigQuery emulator)
4. Test with real credentials (Snowflake, Redshift) - optional
5. Verify all tests can be skipped gracefully
6. Run full test suite

## Running Tests After Fixes

```bash
# Unit tests only (should always pass)
dotnet test --filter "Category=Unit"

# BigQuery integration (requires Docker)
dotnet test --filter "FullyQualifiedName~BigQuery&Category=Integration"

# All integration tests
dotnet test --filter "Category=Integration"
```

## Test Coverage Added

### BigQuery (27 tests)
- Query execution with emulator
- CRUD operations
- Bulk operations
- Geometry handling
- SQL injection prevention
- Parameterization verification

### Snowflake (20 tests)
- Query execution with real Snowflake
- GEOGRAPHY type handling
- CRUD operations
- Bulk operations
- Security tests

### Redshift (22 tests)
- Data API integration
- Bulk operations
- Spatial limitations documentation
- Long-running query polling
- Security tests

**Total New Integration Tests**: 69 tests across 3 providers
