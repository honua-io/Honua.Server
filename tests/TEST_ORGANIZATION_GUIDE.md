# Test Organization Guide

**Purpose**: Guidelines for writing, organizing, and maintaining tests in the Honua GIS Server project

## Table of Contents
- [Test Types & Languages](#test-types--languages)
- [When to Use Which Language](#when-to-use-which-language)
- [Test Organization Patterns](#test-organization-patterns)
- [Using Shared Infrastructure](#using-shared-infrastructure)
- [DRY Principles](#dry-principles)
- [Test File Size Guidelines](#test-file-size-guidelines)
- [Naming Conventions](#naming-conventions)

---

## Test Types & Languages

### C# Tests (Primary)
**Location**: `tests/Honua.*.Tests/`

**Purpose**:
- Unit tests for internal logic
- Integration tests for database operations
- Security tests
- Performance tests
- E2E tests for deployment workflows

**Frameworks**:
- xUnit 2.9.2 (test framework)
- Moq 4.20.72 (mocking)
- FluentAssertions (assertions)
- Testcontainers (Docker infrastructure)

**When to use**:
- Testing internal C# code logic
- Database layer testing (multiple providers)
- Authentication/authorization
- API endpoint behavior
- Service integration

### Python Tests
**Location**: `tests/python/`

**Purpose**:
- OGC protocol compliance using reference clients
- Interoperability testing
- Client library compatibility

**Frameworks**:
- pytest (test framework)
- OWSLib (WFS, WMS, WMTS, WCS, CSW clients)
- pystac (STAC client)
- requests (HTTP client)

**When to use**:
- Testing OGC protocol compliance (WFS, WMS, WMTS, WCS)
- STAC API compliance
- GeoServices REST API compatibility
- Testing with industry-standard clients

### Node.js Tests (Future)
**Location**: `tests/nodejs/` (planned)

**Purpose**:
- Client integration testing (Leaflet, OpenLayers, MapLibre)
- JavaScript SDK testing

**When to use**:
- Testing JavaScript client integrations
- Browser compatibility
- Client-side mapping libraries

### Load Tests
**Location**: `tests/load/`

**Tool**: k6

**Purpose**:
- Performance benchmarking
- Stress testing
- Load profiling

---

## When to Use Which Language

### Decision Tree

```
Does it test OGC protocol compliance?
├─ YES → Use Python with reference clients (OWSLib, pystac)
└─ NO → Continue...

Does it test internal C# logic or database operations?
├─ YES → Use C# unit/integration tests
└─ NO → Continue...

Does it test JavaScript client integration?
├─ YES → Use Node.js tests (when available)
└─ NO → Use C# for general integration testing
```

### Examples

#### ✅ Good: Use Python for Protocol Compliance
```python
# tests/python/test_wfs_owslib.py
def test_wfs_get_capabilities(wfs_client):
    """Test WFS GetCapabilities using OWSLib (reference client)"""
    assert wfs_client.identification.type == 'WFS'
    assert 'GetFeature' in [op.name for op in wfs_client.operations]
```

**Why**: OWSLib is the reference Python client for OGC services. Testing with it ensures real-world compatibility.

#### ✅ Good: Use C# for Internal Logic
```csharp
// tests/Honua.Server.Core.Tests.Apis/Stac/StacCatalogTests.cs
[Fact]
public async Task StacCatalog_WithInvalidJson_ShouldThrowException()
{
    // Test internal JSON parsing logic
    var service = new StacCatalogService(...);
    await Assert.ThrowsAsync<JsonException>(() =>
        service.ParseItemAsync("invalid json"));
}
```

**Why**: Testing internal C# logic directly is faster and more precise than external API testing.

#### ❌ Bad: Duplicate Testing
```csharp
// DON'T: Test WFS compliance in C# if Python tests already cover it
[Fact]
public async Task WfsGetCapabilities_ShouldReturnValidXml()
{
    // This duplicates Python test_wfs_owslib.py
}
```

**Why**: Python tests with OWSLib already validate WFS compliance. C# tests should focus on internal logic.

---

## Test Organization Patterns

### 1. Organize by Feature/Domain

**Good**:
```
tests/Honua.Server.Core.Tests.Apis/
├── Stac/
│   ├── StacCatalogTests.cs
│   ├── StacSearchTests.cs
│   └── StacEdgeCaseTests.cs
├── Ogc/
│   ├── OgcFeaturesTests.cs
│   └── OgcTilesTests.cs
└── Geoservices/
    ├── FeatureServerTests.cs
    └── MapServerTests.cs
```

**Bad**:
```
tests/Honua.Server.Tests/
├── Test1.cs
├── Test2.cs
├── AllTheTests.cs  ← 5000 lines, no organization
└── Miscellaneous.cs
```

### 2. Split Large Test Files by Concern

**When a test file exceeds 500-700 lines, split it by concern:**

**Before** (998 lines):
```csharp
// StacEdgeCaseTests.cs - 998 lines
public class StacEdgeCaseTests
{
    #region Empty Catalog Tests (50 lines)
    #region Bbox Edge Cases (200 lines)
    #region Special Characters (150 lines)
    #region Temporal Edge Cases (200 lines)
    #region Pagination Edge Cases (150 lines)
    // ... more regions
}
```

**After** (split into focused files):
```csharp
// StacEdgeCaseTests.EmptyCatalog.cs - 80 lines
public class StacEmptyCatalogEdgeCaseTests { ... }

// StacEdgeCaseTests.Bbox.cs - 250 lines
public class StacBboxEdgeCaseTests { ... }

// StacEdgeCaseTests.SpecialCharacters.cs - 180 lines
public class StacSpecialCharacterEdgeCaseTests { ... }

// StacEdgeCaseTests.Temporal.cs - 220 lines
public class StacTemporalEdgeCaseTests { ... }

// StacEdgeCaseTests.Pagination.cs - 180 lines
public class StacPaginationEdgeCaseTests { ... }
```

**Benefits**:
- Easier to navigate
- Faster test execution (parallel)
- Clearer test intent
- Easier code review

---

## Using Shared Infrastructure

### Shared Docker Test Environment

**Location**: `tests/docker-compose.shared-test-env.yml`

**Purpose**: Pre-configured, cached Honua Server instance for fast test execution

**Services**:
- Honua Server (SQLite backend) - Port 5100
- PostgreSQL + PostGIS - Port 5433
- Redis - Port 6380
- Qdrant - Port 6334

### Starting the Environment

```bash
# Start shared environment
cd tests
./start-shared-test-env.sh start

# Check status
./start-shared-test-env.sh status

# View logs
./start-shared-test-env.sh logs honua-test

# Stop (keeps data cached)
./start-shared-test-env.sh stop
```

### Using in C# Tests

```csharp
public class MyIntegrationTests : IClassFixture<HonuaTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MyIntegrationTests(HonuaTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        // Client automatically configured to use shared test environment
    }

    [Fact]
    public async Task MyTest()
    {
        var response = await _client.GetAsync("/ogc");
        response.Should().BeSuccessful();
    }
}
```

### Using in Python Tests

```python
# tests/python/test_my_feature.py
import pytest

def test_my_feature(honua_api_base_url):
    """Test uses shared environment automatically"""
    # honua_api_base_url = http://localhost:5100 (shared environment)
    response = requests.get(f"{honua_api_base_url}/ogc")
    assert response.status_code == 200
```

**Note**: Python tests automatically start the shared environment if not running. See `tests/python/conftest.py`.

---

## DRY Principles

### Use Base Test Classes

**Bad** (Repetitive):
```csharp
public class WfsTests
{
    [Fact]
    public async Task GetCapabilities_ShouldReturnValidXml()
    {
        var response = await _client.GetAsync("/wfs?request=GetCapabilities");
        response.Should().BeSuccessful();
        var xml = await ParseXmlAsync(response);
        xml.Root.Should().NotBeNull();
        // 20 more lines of common validation...
    }
}

public class WmsTests
{
    [Fact]
    public async Task GetCapabilities_ShouldReturnValidXml()
    {
        var response = await _client.GetAsync("/wms?request=GetCapabilities");
        response.Should().BeSuccessful();
        var xml = await ParseXmlAsync(response);
        xml.Root.Should().NotBeNull();
        // 20 more lines of DUPLICATE validation...
    }
}
```

**Good** (DRY with Base Class):
```csharp
// Base class in Honua.Server.Core.Tests.Shared/TestBases/
public abstract class OgcProtocolTestBase<TFactory> : IClassFixture<TFactory>
{
    protected abstract string ServiceEndpoint { get; }
    protected abstract string ServiceType { get; }

    protected async Task AssertValidGetCapabilitiesAsync()
    {
        var response = await Client.GetAsync(
            $"{ServiceEndpoint}?service={ServiceType}&request=GetCapabilities");
        response.Should().BeSuccessful();
        var xml = await ParseXmlResponseAsync(response);
        // Common validation in one place
    }
}

// Derived test classes
public class WfsTests : OgcProtocolTestBase<WfsTestFixture>
{
    protected override string ServiceEndpoint => "/wfs";
    protected override string ServiceType => "WFS";

    [Fact]
    public async Task GetCapabilities_ShouldReturnValidXml()
    {
        await AssertValidGetCapabilitiesAsync(); // Reuse base implementation
    }
}
```

### Use Test Data Builders

**Bad** (Repetitive):
```csharp
[Fact]
public async Task Test1()
{
    var item = new StacItemRecord
    {
        Id = "test-1",
        CollectionId = "col-1",
        Geometry = new Dictionary<string, object> { ["type"] = "Point", ["coordinates"] = new[] { -122.5, 37.8 } },
        Bbox = new[] { -122.5, 37.8, -122.5, 37.8 },
        Properties = new Dictionary<string, object> { ["datetime"] = DateTime.UtcNow.ToString("O") },
        Assets = new Dictionary<string, StacAsset>(),
        Links = new List<StacLink>()
    };
    // Use item...
}

[Fact]
public async Task Test2()
{
    var item = new StacItemRecord
    {
        // DUPLICATE 15 lines of setup...
    };
}
```

**Good** (Builder Pattern):
```csharp
using Honua.Server.Core.Tests.Shared.Builders;

[Fact]
public async Task Test1()
{
    var item = new StacItemBuilder()
        .WithId("test-1")
        .WithCollection("col-1")
        .WithGeometry(-122.5, 37.8)
        .WithDatetime(DateTime.UtcNow)
        .Build();
    // Use item...
}

[Fact]
public async Task Test2_DatelineCrossing()
{
    var item = new StacItemBuilder()
        .WithId("test-2")
        .CrossingDateline() // Reusable complex scenario
        .Build();
}
```

---

## Test File Size Guidelines

### Size Limits

| File Size | Status | Action Required |
|-----------|--------|----------------|
| < 300 lines | ✅ Good | None |
| 300-500 lines | ⚠️ Watch | Consider splitting if clear boundaries exist |
| 500-700 lines | ⚠️ Large | Plan to split by concern |
| > 700 lines | ❌ Too Large | **Must split** |

### When to Split

**Indicators that a file should be split**:
- Multiple `#region` blocks (each could be a file)
- Tests for unrelated concerns
- Difficult to find specific tests
- Long scroll time to navigate
- Multiple developers editing (merge conflicts)

**How to Split**:
1. Identify logical groupings (regions, concerns, scenarios)
2. Create separate test class for each group
3. Use descriptive names: `Feature_Concern_Tests.cs`
4. Keep shared setup in base class or fixture

**Example Split**:
```
Before: StacEdgeCaseTests.cs (998 lines)

After:
- StacEmptyCatalogTests.cs (80 lines)
- StacBboxEdgeCaseTests.cs (250 lines)
- StacSpecialCharacterTests.cs (180 lines)
- StacTemporalEdgeCaseTests.cs (220 lines)
- StacPaginationEdgeCaseTests.cs (180 lines)
```

---

## Naming Conventions

### C# Test Files

**Pattern**: `{Feature}{Concern}Tests.cs`

**Examples**:
- `StacCatalogTests.cs` - STAC catalog operations
- `StacSearchTests.cs` - STAC search functionality
- `StacEdgeCaseTests.cs` - STAC edge cases
- `WfsGetCapabilitiesTests.cs` - WFS GetCapabilities
- `PostgresFeatureRepositoryTests.cs` - PostgreSQL feature repo
- `AuthenticationFlowTests.cs` - Authentication flows

### Python Test Files

**Pattern**: `test_{protocol}_{client}.py`

**Examples**:
- `test_wfs_owslib.py` - WFS tests with OWSLib
- `test_stac_pystac.py` - STAC tests with pystac
- `test_wms_owslib.py` - WMS tests with OWSLib
- `test_smoke.py` - Smoke tests

### Test Methods

**C# Pattern**: `{Method}_{Scenario}_{ExpectedResult}`

**Examples**:
```csharp
[Fact]
public async Task SearchItems_WithEmptyCatalog_ShouldReturnEmptyResults()

[Fact]
public async Task GetCapabilities_WithInvalidVersion_ShouldReturnException()

[Theory]
[InlineData("valid-id")]
[InlineData("another-id")]
public async Task GetItem_WithValidId_ShouldReturnItem(string itemId)
```

**Python Pattern**: `test_{method}_{scenario}_{expected}`

**Examples**:
```python
def test_wfs_get_capabilities_returns_valid_xml(wfs_client):
    ...

def test_wfs_get_feature_with_filter_returns_filtered_results(wfs_client):
    ...
```

---

## Test Categories & Traits

### C# Traits

Use `[Trait]` attributes to categorize tests:

```csharp
[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
public class StacCatalogTests { }

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "Database")]
[Trait("Provider", "PostgreSQL")]
public class PostgresIntegrationTests { }
```

### Python Markers

Use `@pytest.mark` decorators:

```python
@pytest.mark.integration
@pytest.mark.wfs
@pytest.mark.requires_honua
def test_wfs_compliance(wfs_client):
    ...

@pytest.mark.smoke
def test_health_check(honua_api_base_url):
    ...
```

### Running Tests by Category

**C#**:
```bash
# Unit tests only
dotnet test --filter "Category=Unit"

# STAC feature tests
dotnet test --filter "Feature=STAC"

# PostgreSQL tests
dotnet test --filter "Provider=PostgreSQL"
```

**Python**:
```bash
# Integration tests
pytest -m integration

# WFS tests only
pytest -m wfs

# Exclude slow tests
pytest -m "not slow"
```

---

## Best Practices Summary

### ✅ DO

- Use appropriate language for test type (C# for logic, Python for protocols)
- Use shared Docker test infrastructure
- Use base test classes to reduce duplication
- Use test data builders for complex objects
- Split files over 700 lines
- Name tests descriptively
- Add traits/markers for categorization
- Keep test data in TestData/ directory
- Document complex test scenarios

### ❌ DON'T

- Duplicate protocol tests across languages
- Create monolithic test files (>1000 lines)
- Hardcode test data in tests
- Copy-paste test setup code
- Mix unrelated test concerns in one file
- Use generic test names (`Test1`, `Test2`)
- Skip test categorization
- Create one-off Docker configurations

---

## Examples & Templates

### Template: C# Unit Test Class

```csharp
using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.{Feature};

/// <summary>
/// Tests for {Feature} {Component}.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "{Feature}")]
public class {Feature}{Component}Tests
{
    [Fact]
    public async Task {Method}_{Scenario}_{ExpectedResult}()
    {
        // Arrange
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.{Method}Async(...);

        // Assert
        result.Should().NotBeNull();
    }

    private {Component} CreateSystemUnderTest()
    {
        return new {Component}(...);
    }
}
```

### Template: Python Protocol Test

```python
"""
Tests for {Protocol} compliance using {Client}.
"""
import pytest

pytestmark = [
    pytest.mark.integration,
    pytest.mark.{protocol},
    pytest.mark.requires_honua
]

@pytest.fixture(scope="module")
def client(honua_api_base_url):
    """Create {Protocol} client."""
    # Setup client
    return client

def test_{protocol}_{operation}_{scenario}(client):
    """Test {Protocol} {Operation} with {Scenario}."""
    # Arrange

    # Act
    result = client.{operation}(...)

    # Assert
    assert result is not None
```

---

## Questions?

For questions or clarifications about test organization:
1. Review this guide
2. Check `TEST_REFACTORING_PLAN.md` for implementation details
3. Look at example tests in each category
4. Ask in team discussions

---

**Last Updated**: 2025-11-04
**Version**: 1.0.0
