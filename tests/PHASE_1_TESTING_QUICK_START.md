# Phase 1 Testing - Quick Start Guide

**Quick Reference for Phase 1 Test Implementation**

## TL;DR - Current Status

| Component | Tests | Status | Risk Level |
|-----------|-------|--------|------------|
| **Graph Database (AGE)** | 13 | ✅ Good | 🟢 LOW |
| **3D Geometry** | 0 | ❌ None | 🔴 CRITICAL |
| **IFC Import** | 0 | ⚠️ POC Only | 🔴 CRITICAL |
| **API Controllers** | 0 | ❌ None | 🔴 HIGH |

## Critical Gaps Summary

### 🔴 Critical (Fix Immediately)

1. **Geometry3DService has ZERO tests**
   - File import untested (OBJ, STL, glTF, FBX)
   - Validation untested
   - Format conversion untested
   - **Risk:** Production failures, data corruption

2. **IFC Service is a stub**
   - Only returns mock data
   - Xbim.Essentials not integrated
   - **Blocker:** Must implement before testing

3. **No API tests**
   - 27 endpoints completely untested
   - No request validation tests
   - No authorization tests

### 🟡 Important (Address Soon)

1. **Graph Database missing:**
   - Error handling tests
   - Concurrent access tests
   - Performance benchmarks

2. **No integration tests**
   - No end-to-end workflows
   - No multi-service tests

## Quick Start - First Week Plan

### Day 1-2: Infrastructure Setup (8 hours)

```bash
# 1. Create test data directories
mkdir -p tests/TestData/3d-models/{unit,integration,performance,invalid}
mkdir -p tests/TestData/ifc-files/{unit,integration,performance,invalid}

# 2. Create test class files
touch tests/Honua.Server.Core.Tests.Data/Geometry3DServiceTests.cs
touch tests/Honua.Server.Core.Tests.Data/GraphDatabaseServiceErrorTests.cs
touch tests/Honua.Server.Host.Tests/API/GraphControllerTests.cs
touch tests/Honua.Server.Host.Tests/API/Geometry3DControllerTests.cs
```

### Day 3-4: Core 3D Geometry Tests (16 hours)

**Priority 1: Basic Import Tests**

```csharp
[Fact]
public async Task ImportGeometry_WithValidObjFile_ShouldSucceed()
{
    // See detailed example in main doc
}

[Fact]
public async Task ImportGeometry_WithInvalidFormat_ShouldReturnError()
{
    // See detailed example in main doc
}
```

**Test all formats:**
- ✅ OBJ (text format)
- ✅ STL (binary and ASCII)
- ✅ glTF (JSON + binary)
- ✅ Invalid/malformed files

### Day 5: Graph Error Handling (8 hours)

```csharp
[Fact]
public async Task ExecuteCypherQuery_WithInvalidSyntax_ShouldThrowException()
{
    var invalidQuery = "INVALID CYPHER SYNTAX";
    Func<Task> act = async () => await _service!.ExecuteCypherQueryAsync(invalidQuery);
    await act.Should().ThrowAsync<Exception>();
}
```

## Running Tests Locally

```bash
# Fast unit tests only (< 30 seconds)
dotnet test --filter "Category=Unit"

# Integration tests (requires Docker)
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres apache/age:latest
dotnet test --filter "Category=Integration"

# Specific component tests
dotnet test --filter "FullyQualifiedName~Geometry3D"
dotnet test --filter "FullyQualifiedName~GraphDatabase"

# All Phase 1 tests
dotnet test --filter "FullyQualifiedName~Phase1"
```

## Test Data Setup

### Generate Simple Test Files

**Minimal OBJ Cube (8 vertices):**
```bash
cat > tests/TestData/3d-models/unit/simple-cube.obj << 'EOF'
# Unit cube
v -1.0 -1.0 -1.0
v  1.0 -1.0 -1.0
v  1.0  1.0 -1.0
v -1.0  1.0 -1.0
v -1.0 -1.0  1.0
v  1.0 -1.0  1.0
v  1.0  1.0  1.0
v -1.0  1.0  1.0
f 1 2 3 4
f 5 6 7 8
EOF
```

**Minimal IFC File:**
```bash
cat > tests/TestData/ifc-files/unit/simple-wall.ifc << 'EOF'
ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('simple-wall.ifc','2025-11-10T00:00:00',(''),(''),'','','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('2O_4lshd58OwrYp4Y2yhH7',$,'Test Project',$,$,$,$,(#2),#3);
#2=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#4,$);
#3=IFCUNITASSIGNMENT((#5));
#4=IFCAXIS2PLACEMENT3D(#6,$,$);
#5=IFCSIUNIT(*,.LENGTHUNIT.,.METRE.);
#6=IFCCARTESIANPOINT((0.,0.,0.));
ENDSEC;
END-ISO-10303-21;
EOF
```

## Priority Test Implementation Order

### Week 1 (40 hours) - Critical Foundation

| Priority | Component | Tests | Effort |
|----------|-----------|-------|--------|
| **P0-1** | Geometry3DService - OBJ Import | 5 tests | 4h |
| **P0-2** | Geometry3DService - STL Import | 3 tests | 2h |
| **P0-3** | Geometry3DService - Validation | 4 tests | 3h |
| **P0-4** | Geometry3DController API | 7 tests | 4h |
| **P0-5** | GraphDatabaseService Errors | 5 tests | 4h |
| **P0-6** | GraphController API | 8 tests | 4h |
| **P0-7** | IFC Validation (basic) | 4 tests | 4h |
| **P0-8** | Test Infrastructure | Setup | 8h |

### Week 2-3 (40 hours) - Comprehensive

| Priority | Component | Tests | Effort |
|----------|-----------|-------|--------|
| **P1-1** | All 3D formats (glTF, FBX, PLY) | 10 tests | 8h |
| **P1-2** | Large file handling | 5 tests | 4h |
| **P1-3** | Graph performance | 6 tests | 6h |
| **P1-4** | Graph edge cases | 8 tests | 6h |
| **P1-5** | IFC entity parsing* | 10 tests | 12h |
| **P1-6** | Integration workflows | 4 tests | 4h |

*Requires IFC service implementation first

### Week 3 (24 hours) - E2E & Performance

| Priority | Component | Tests | Effort |
|----------|-----------|-------|--------|
| **P2-1** | E2E: IFC→Graph→3D | 3 tests | 6h |
| **P2-2** | E2E: 3D Lifecycle | 2 tests | 4h |
| **P2-3** | BenchmarkDotNet tests | 6 benchmarks | 6h |
| **P2-4** | Stress tests | 4 tests | 6h |

## CI/CD Quick Setup

### Add to `.github/workflows/tests.yml`

```yaml
name: Phase 1 Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0'

      - name: Run Unit Tests
        run: dotnet test --filter "Category=Unit" --logger "trx"

  integration-tests:
    runs-on: ubuntu-latest
    services:
      postgres-age:
        image: apache/age:latest
        env:
          POSTGRES_PASSWORD: postgres
        ports:
          - 5432:5432

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0'

      - name: Run Integration Tests
        env:
          POSTGRES_AGE_CONNECTION_STRING: "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres"
        run: dotnet test --filter "Category=Integration" --logger "trx"
```

## Coverage Goals

| Component | Target | Current |
|-----------|--------|---------|
| GraphDatabaseService | 90% | ~70% (13 tests) |
| Geometry3DService | 85% | 0% ❌ |
| IfcImportService | 80% | 0% ❌ |
| Graph API | 85% | 0% ❌ |
| Geometry3D API | 85% | 0% ❌ |
| IFC API | 80% | 0% ❌ |

## Test Template Examples

### Unit Test Template

```csharp
using Xunit;
using FluentAssertions;

namespace Honua.Server.Core.Tests.Data;

[Trait("Category", "Unit")]
public class Geometry3DServiceTests
{
    private readonly Geometry3DService _service;

    public Geometry3DServiceTests()
    {
        var logger = LoggerFactory.Create(b => b.AddDebug())
            .CreateLogger<Geometry3DService>();
        _service = new Geometry3DService(logger);
    }

    [Fact]
    public async Task ImportGeometry_WithValidObjFile_ShouldSucceed()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest { Format = "obj" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.VertexCount.Should().Be(8);
    }

    private static byte[] GenerateSimpleCubeObj()
    {
        var obj = "v -1 -1 -1\nv 1 -1 -1\nv 1 1 -1\nv -1 1 -1\nf 1 2 3 4\n";
        return System.Text.Encoding.UTF8.GetBytes(obj);
    }
}
```

### Integration Test Template

```csharp
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Honua.Server.Host.Tests.API;

[Trait("Category", "Integration")]
public class Geometry3DControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public Geometry3DControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadGeometry_WithValidFile_ReturnsOk()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(GenerateTestFile());
        content.Add(fileContent, "file", "test.obj");

        // Act
        var response = await _client.PostAsync("/api/geometry/3d/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

## Common Pitfalls to Avoid

1. **❌ Don't test implementation details**
   - Test behavior, not internals
   - Focus on public API

2. **❌ Don't use Thread.Sleep**
   - Use async/await properly
   - Use CancellationToken for timeouts

3. **❌ Don't share state between tests**
   - Each test should be isolated
   - Use fixtures properly

4. **❌ Don't skip cleanup**
   - Implement IAsyncLifetime
   - Clean up test data

5. **❌ Don't hardcode paths**
   - Use Path.Combine
   - Use relative paths from project root

## Success Metrics

### Week 1 Goals
- [ ] 20+ new tests written
- [ ] All 3D import formats tested
- [ ] Graph error handling tested
- [ ] CI pipeline running
- [ ] Coverage >50% on new code

### Week 2 Goals
- [ ] 40+ total new tests
- [ ] All API endpoints tested
- [ ] Integration tests passing
- [ ] Coverage >70% on new code

### Week 3 Goals
- [ ] 60+ total new tests
- [ ] E2E workflows passing
- [ ] Performance benchmarks established
- [ ] Coverage >80% on new code
- [ ] Production ready

## Resources

**Full Documentation:** See `PHASE_1_TEST_COVERAGE_ANALYSIS.md`

**Key Files:**
- `/tests/Honua.Server.Core.Tests.Data/GraphDatabaseServiceTests.cs` - Example test structure
- `/tests/TESTCONTAINERS_GUIDE.md` - Docker integration
- `/tests/TEST_ORGANIZATION_GUIDE.md` - Best practices

**External Resources:**
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

## Quick Commands Reference

```bash
# Create all test files at once
mkdir -p tests/TestData/3d-models/{unit,integration,performance,invalid}
mkdir -p tests/TestData/ifc-files/{unit,integration,performance,invalid}

# Run specific test class
dotnet test --filter "FullyQualifiedName~Geometry3DServiceTests"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Watch mode (re-run on file changes)
dotnet watch test --filter "Category=Unit"

# List all tests without running
dotnet test --list-tests

# Run tests in parallel (faster)
dotnet test --parallel

# Generate coverage report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport"
```

## Getting Help

- **Questions:** Review full documentation in `PHASE_1_TEST_COVERAGE_ANALYSIS.md`
- **Issues:** Check existing tests in `GraphDatabaseServiceTests.cs` for patterns
- **CI Problems:** See `.github/workflows/` examples

---

**Last Updated:** November 10, 2025

**Next Review:** After Week 1 completion
