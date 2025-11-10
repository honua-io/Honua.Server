# Phase 1 Test Coverage Analysis & Implementation Plan

**Document Version:** 1.0
**Date:** November 10, 2025
**Author:** Claude Code Assessment

---

## Executive Summary

This document provides a comprehensive analysis of test coverage for Phase 1 implementations (Apache AGE Graph Database, Complex 3D Geometry, and IFC Import) and presents a detailed testing strategy with implementation tasks.

### Current State
- **Phase 1.1 (Apache AGE):** 13 unit tests ✅ **GOOD COVERAGE**
- **Phase 1.2 (Complex 3D Geometry):** 0 tests ❌ **NO COVERAGE**
- **Phase 1.3 (IFC Import):** 0 tests ❌ **NO COVERAGE**

### Critical Findings
1. **Graph Database (AGE) has solid foundation** - 13 comprehensive tests covering core operations
2. **3D Geometry Service is completely untested** - high risk for production
3. **IFC Import Service is a proof-of-concept** - needs full implementation + tests
4. **No integration tests** exist for Phase 1 workflows
5. **No API endpoint tests** for any of the three controllers
6. **No test data** - no sample 3D files or IFC files in test fixtures

---

## Table of Contents

1. [Current Test Coverage Analysis](#1-current-test-coverage-analysis)
2. [Gap Analysis](#2-gap-analysis)
3. [Test Strategy](#3-test-strategy)
4. [Test Implementation Plan](#4-test-implementation-plan)
5. [Sample Test Code](#5-sample-test-code)
6. [Test Data Requirements](#6-test-data-requirements)
7. [Effort Estimates](#7-effort-estimates)
8. [CI/CD Integration](#8-cicd-integration)

---

## 1. Current Test Coverage Analysis

### 1.1 Phase 1.1: Apache AGE Graph Database

**Location:** `/home/user/Honua.Server/tests/Honua.Server.Core.Tests.Data/GraphDatabaseServiceTests.cs`

**Coverage Summary:**

| Category | Tests | Status |
|----------|-------|--------|
| Graph Management | 1 | ✅ CreateGraph |
| Node Operations | 6 | ✅ Create, Get, Find, Update, Delete, Batch |
| Edge Operations | 3 | ✅ Create, Get Relationships, Delete |
| Cypher Queries | 1 | ✅ Execute Query |
| Graph Traversal | 2 | ✅ Traverse, Shortest Path |
| **TOTAL** | **13** | **Good Foundation** |

**Test Quality:**
- ✅ Uses xUnit with async/await patterns
- ✅ Proper setup/teardown with IAsyncLifetime
- ✅ Test isolation (unique graph per test run)
- ✅ Graceful skip when PostgreSQL AGE unavailable
- ✅ Good coverage of happy paths
- ⚠️ Missing negative test cases (invalid data, errors)
- ⚠️ Missing performance tests (bulk operations)
- ⚠️ Missing concurrent access tests

**What's Covered:**
```csharp
✅ CreateGraph - Creates a new graph successfully
✅ CreateNode - Returns node with ID and properties
✅ GetNodeById - Retrieves correct node
✅ FindNodes - Returns matching nodes by label
✅ UpdateNode - Modifies properties correctly
✅ DeleteNode - Removes node and relationships
✅ CreateEdge - Creates relationship between nodes
✅ GetNodeRelationships - Returns edges by direction
✅ ExecuteCypherQuery - Executes queries and returns results
✅ CreateNodesBatch - Creates multiple nodes efficiently
✅ TraverseGraph - Finds connected nodes with depth limit
```

**What's Missing:**
```csharp
❌ Error handling (invalid queries, connection failures)
❌ Edge cases (null/empty data, special characters in properties)
❌ Transaction boundaries and rollback
❌ Performance under load (1000+ nodes/edges)
❌ Concurrent modifications
❌ Schema validation
❌ Query timeouts and cancellation
❌ Pagination for large result sets
```

### 1.2 Phase 1.2: Complex 3D Geometry Support

**Location:** `/home/user/Honua.Server/src/Honua.Server.Core/Services/Geometry3D/Geometry3DService.cs`

**Coverage Summary:**

| Category | Tests | Status |
|----------|-------|--------|
| File Import | 0 | ❌ NO TESTS |
| File Export | 0 | ❌ NO TESTS |
| Geometry Validation | 0 | ❌ NO TESTS |
| Bounding Box Calculation | 0 | ❌ NO TESTS |
| Format Conversion | 0 | ❌ NO TESTS |
| Metadata Management | 0 | ❌ NO TESTS |
| **TOTAL** | **0** | **CRITICAL GAP** |

**Implementation Status:**
- ✅ Service implemented with AssimpNet
- ✅ Supports OBJ, STL, glTF, FBX, PLY formats
- ✅ In-memory storage (POC - not production-ready)
- ⚠️ No database persistence
- ⚠️ No blob storage integration
- ❌ Completely untested

**Critical Risks:**
1. **Unknown file parsing reliability** - no validation of Assimp integration
2. **No format validation** - could crash on malformed files
3. **Memory leaks possible** - large files not tested
4. **Checksum calculation untested** - data integrity at risk
5. **Bounding box calculation untested** - spatial queries could fail

### 1.3 Phase 1.3: IFC Import Support

**Location:** `/home/user/Honua.Server/src/Honua.Server.Core/Services/IfcImportService.cs`

**Coverage Summary:**

| Category | Tests | Status |
|----------|-------|--------|
| File Validation | 0 | ❌ NO TESTS |
| Metadata Extraction | 0 | ❌ NO TESTS |
| Entity Import | 0 | ❌ NO TESTS |
| Relationship Mapping | 0 | ❌ NO TESTS |
| Graph Integration | 0 | ❌ NO TESTS |
| **TOTAL** | **0** | **PROOF-OF-CONCEPT ONLY** |

**Implementation Status:**
- ⚠️ **Stub implementation** - Xbim.Essentials not integrated
- ⚠️ Basic file format detection only
- ⚠️ All actual IFC parsing is commented out
- ❌ No feature creation
- ❌ No graph relationship creation
- ❌ Returns mock data

**Critical Note:**
This service is **NOT production-ready**. The entire implementation consists of commented-out TODO code. Full implementation required before any testing can be meaningful.

### 1.4 API Controllers

**Locations:**
- `/home/user/Honua.Server/src/Honua.Server.Host/API/GraphController.cs`
- `/home/user/Honua.Server/src/Honua.Server.Host/API/Geometry3DController.cs`
- `/home/user/Honua.Server/src/Honua.Server.Host/API/IfcImportController.cs`

**Coverage Summary:**

| Controller | Endpoints | Tests | Status |
|------------|-----------|-------|--------|
| GraphController | 16 | 0 | ❌ NO TESTS |
| Geometry3DController | 7 | 0 | ❌ NO TESTS |
| IfcImportController | 4 | 0 | ❌ NO TESTS |
| **TOTAL** | **27** | **0** | **CRITICAL GAP** |

**Missing API Tests:**
- ❌ Request validation
- ❌ Response serialization
- ❌ HTTP status codes
- ❌ Error handling and problem details
- ❌ Authorization policies
- ❌ File upload limits
- ❌ Content-Type handling
- ❌ Multipart form data parsing

---

## 2. Gap Analysis

### 2.1 Critical Gaps (P0 - Must Have)

#### Graph Database
1. **Error Handling Tests**
   - Connection failures
   - Invalid Cypher queries
   - Constraint violations
   - Transaction failures

2. **API Integration Tests**
   - GraphController endpoints
   - Request/response validation
   - Error responses
   - Authorization

#### Complex 3D Geometry
1. **Unit Tests (Geometry3DService)**
   - File import (all supported formats)
   - File validation
   - Mesh conversion
   - Bounding box calculation
   - Checksum verification

2. **Integration Tests**
   - File upload workflow
   - Geometry storage and retrieval
   - Format conversion
   - API endpoints

3. **Edge Case Tests**
   - Large files (>50MB)
   - Malformed files
   - Unsupported formats
   - Empty meshes
   - Non-triangulated meshes

#### IFC Import
1. **Service Implementation** (prerequisite for testing)
   - Integrate Xbim.Essentials library
   - Implement actual parsing
   - Implement feature creation
   - Implement graph integration

2. **Unit Tests** (after implementation)
   - File validation
   - Metadata extraction
   - Entity parsing
   - Property extraction
   - Relationship mapping

3. **Integration Tests**
   - Full import workflow
   - Graph relationship creation
   - Feature storage
   - API endpoints

### 2.2 Important Gaps (P1 - Should Have)

1. **Performance Tests**
   - Large graph queries (1000+ nodes)
   - Large 3D files (100+ MB)
   - Large IFC files (10,000+ entities)
   - Concurrent operations
   - Batch processing efficiency

2. **End-to-End Workflows**
   - IFC import → Graph creation → 3D visualization
   - 3D file upload → Storage → Retrieval → Export
   - Graph traversal → Feature retrieval → Geometry loading

3. **Data Integrity Tests**
   - Checksum validation
   - Referential integrity (graph edges)
   - Transaction atomicity
   - Concurrent modification conflicts

### 2.3 Nice-to-Have Gaps (P2 - Could Have)

1. **Load Tests**
   - Multiple concurrent users
   - Sustained throughput
   - Memory usage under load
   - Connection pool saturation

2. **Compatibility Tests**
   - Different IFC schema versions
   - Various 3D file variations
   - Different coordinate systems
   - Unit conversions

3. **Regression Tests**
   - Known bug scenarios
   - Edge cases from production issues

---

## 3. Test Strategy

### 3.1 Testing Framework Stack

**Current Infrastructure:**
```yaml
Test Framework: xUnit 2.9.2
Assertion Library: FluentAssertions (recommended) or xUnit Assert
Mocking: Moq 4.20.72
Integration: Testcontainers 3.10.0
  - PostgreSQL (with AGE extension)
  - Redis (for caching tests if needed)
Test Data: Custom fixtures in TestData directory
```

**Additional Tools Needed:**
```yaml
API Testing:
  - Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory)
  - Alba or Refit for HTTP client testing

Performance Testing:
  - BenchmarkDotNet (already in project)

Test Data Generation:
  - Bogus (for fake data)
  - AutoFixture (for object creation)
```

### 3.2 Test Organization

**Directory Structure:**
```
tests/
├── Honua.Server.Core.Tests.Data/
│   ├── GraphDatabaseServiceTests.cs (✅ exists)
│   ├── Geometry3DServiceTests.cs (❌ NEW)
│   ├── IfcImportServiceTests.cs (❌ NEW)
│   └── TestData/
│       ├── 3d-models/
│       │   ├── simple-cube.obj
│       │   ├── simple-cube.stl
│       │   ├── simple-cube.gltf
│       │   └── complex-building.glb
│       └── ifc-files/
│           ├── simple-building.ifc
│           ├── building-with-relationships.ifc
│           └── multi-story-building.ifc
│
├── Honua.Server.Host.Tests/
│   ├── API/
│   │   ├── GraphControllerTests.cs (❌ NEW)
│   │   ├── Geometry3DControllerTests.cs (❌ NEW)
│   │   └── IfcImportControllerTests.cs (❌ NEW)
│   └── Integration/
│       ├── GraphApiIntegrationTests.cs (❌ NEW)
│       ├── Geometry3DWorkflowTests.cs (❌ NEW)
│       └── IfcImportWorkflowTests.cs (❌ NEW)
│
└── Honua.Server.Benchmarks/
    ├── GraphDatabaseBenchmarks.cs (❌ NEW)
    ├── Geometry3DProcessingBenchmarks.cs (❌ NEW)
    └── IfcImportBenchmarks.cs (❌ NEW)
```

### 3.3 Test Types & Priorities

#### Unit Tests (P0)
**Target:** Service layer logic, data validation, calculations

**For Graph Database:**
- ✅ Existing tests are good
- ➕ Add error handling tests
- ➕ Add edge case tests

**For 3D Geometry:**
- File parsing and validation
- Mesh conversion accuracy
- Bounding box calculation
- Checksum generation
- Format detection

**For IFC Import:**
- File validation logic
- Metadata extraction
- Entity type filtering
- Property mapping

#### Integration Tests (P0)
**Target:** API endpoints, database interactions, external dependencies

**Test Patterns:**
- Use WebApplicationFactory for in-process hosting
- Use Testcontainers for PostgreSQL with AGE
- Mock external blob storage initially
- Test full request/response cycle

**Scenarios:**
- File upload workflows
- Query execution with result validation
- Error responses and problem details
- Authorization enforcement

#### End-to-End Tests (P1)
**Target:** Complete user workflows across multiple services

**Scenarios:**
1. IFC Import to Graph Visualization
   - Upload IFC file
   - Verify features created
   - Verify graph relationships
   - Query relationships
   - Retrieve geometry

2. 3D Model Management Workflow
   - Upload OBJ file
   - Retrieve metadata
   - Export to different format
   - Verify data integrity

#### Performance Tests (P1)
**Target:** Scalability, throughput, resource usage

**Metrics:**
- Response time (p50, p95, p99)
- Throughput (requests/second)
- Memory usage
- Connection pool utilization

**Scenarios:**
- 1000 nodes + 5000 edges traversal
- 100MB STL file import
- 10,000 entity IFC import
- 100 concurrent graph queries

### 3.4 Test Data Strategy

**Sample Files Required:**

1. **3D Models (Small - for unit tests):**
   - `simple-cube.obj` (< 1KB, 8 vertices, 12 triangles)
   - `simple-cube.stl` (binary and ASCII versions)
   - `simple-cube.gltf` (with embedded geometry)

2. **3D Models (Medium - for integration tests):**
   - `simple-building.obj` (10-50KB, few hundred vertices)
   - `building-section.glb` (with materials and textures)

3. **3D Models (Large - for performance tests):**
   - `complex-building.glb` (5-10MB, 100K+ vertices)
   - `city-block.obj` (50+ MB, millions of triangles)

4. **IFC Files (Small - for unit tests):**
   - `simple-wall.ifc` (IFC4, single IfcWall)
   - `basic-structure.ifc` (IFC2x3, 10-20 entities)

5. **IFC Files (Medium - for integration tests):**
   - `simple-building.ifc` (500-1000 entities, basic hierarchy)
   - `building-with-properties.ifc` (includes property sets)

6. **IFC Files (Large - for performance tests):**
   - `multi-story-building.ifc` (10,000+ entities)
   - `campus-layout.ifc` (multiple buildings, site context)

**Test Data Generation:**
- For simple cases: Programmatically generate in code
- For realistic cases: Use open-source BIM samples
- For graph data: Generate using Bogus/AutoFixture

### 3.5 Continuous Integration Strategy

**Test Execution Tiers:**

**Tier 1: Fast Unit Tests (< 30 seconds)**
- Run on every commit
- No external dependencies
- In-memory databases
- Skip Testcontainers tests

**Tier 2: Integration Tests (< 5 minutes)**
- Run on pull requests
- Use Testcontainers
- Real PostgreSQL with AGE
- File I/O tests

**Tier 3: E2E & Performance Tests (< 30 minutes)**
- Run on merge to main
- Run nightly
- Full workflows
- Large files
- Stress tests

**CI Configuration Example:**
```yaml
# .github/workflows/tests.yml
name: Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Unit Tests
        run: dotnet test --filter "Category!=Integration&Category!=E2E"
        timeout-minutes: 5

  integration-tests:
    runs-on: ubuntu-latest
    services:
      docker:
        image: docker:dind
    steps:
      - uses: actions/checkout@v4
      - name: Run Integration Tests
        run: dotnet test --filter "Category=Integration"
        timeout-minutes: 15

  e2e-tests:
    runs-on: ubuntu-latest
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
      - name: Run E2E Tests
        run: dotnet test --filter "Category=E2E"
        timeout-minutes: 30
```

---

## 4. Test Implementation Plan

### Phase 1: Critical Foundation (Week 1-2) - 40 hours

#### Priority 0: Infrastructure Setup (8 hours)

**Task 1.1: Create Test Projects and Structure**
```bash
# Already exists:
# - Honua.Server.Core.Tests.Data

# Create new test classes
# - Geometry3DServiceTests.cs
# - GraphControllerTests.cs
# - Geometry3DControllerTests.cs
```

**Task 1.2: Set Up Test Data Directory** (2 hours)
- Create `/tests/TestData/3d-models/` directory
- Create `/tests/TestData/ifc-files/` directory
- Download/generate simple test files
- Document test data in README

**Task 1.3: Create Test Utilities** (6 hours)
- Create `Geometry3DTestHelpers.cs` class
- Create simple 3D file generators
- Create test fixture classes
- Create assertion helpers for 3D data

#### Priority 1: Graph Database Tests (8 hours)

**Task 2.1: Add Error Handling Tests** (4 hours)
- Test connection failures
- Test invalid Cypher syntax
- Test malformed data
- Test constraint violations
- Test transaction rollback

**Task 2.2: Add GraphController API Tests** (4 hours)
- Test all endpoints (16 total)
- Test request validation
- Test authorization
- Test error responses

#### Priority 2: 3D Geometry Service Tests (16 hours)

**Task 3.1: Core Unit Tests** (8 hours)
- Test OBJ file import
- Test STL file import
- Test glTF file import
- Test file validation
- Test bounding box calculation
- Test checksum generation
- Test format detection

**Task 3.2: Geometry3DController API Tests** (4 hours)
- Test file upload endpoint
- Test get geometry endpoint
- Test export endpoint
- Test delete endpoint
- Test search by bounding box
- Test file size limits
- Test invalid format handling

**Task 3.3: Integration Tests** (4 hours)
- Test full upload workflow
- Test geometry retrieval
- Test format conversion
- Test concurrent uploads

#### Priority 3: Basic IFC Tests (8 hours)

**Note:** IFC service needs implementation first

**Task 4.1: IFC Validation Tests** (4 hours)
- Test file format detection
- Test schema version detection
- Test basic file structure validation

**Task 4.2: IfcImportController API Tests** (4 hours)
- Test validation endpoint
- Test metadata extraction endpoint
- Test error handling
- Test file size limits

### Phase 2: Comprehensive Coverage (Week 3-4) - 40 hours

#### Priority 4: Advanced Graph Tests (12 hours)

**Task 5.1: Performance Tests** (6 hours)
- Benchmark 1000 node creation
- Benchmark complex traversals
- Benchmark bulk operations
- Test concurrent queries

**Task 5.2: Edge Cases & Error Scenarios** (6 hours)
- Test special characters in properties
- Test very deep graph traversals
- Test circular relationships
- Test orphaned nodes cleanup

#### Priority 5: Advanced 3D Geometry Tests (16 hours)

**Task 6.1: Format-Specific Tests** (8 hours)
- Test all OBJ variations (with/without normals, UVs)
- Test binary vs ASCII STL
- Test glTF with textures
- Test FBX import
- Test PLY import
- Test malformed files

**Task 6.2: Large File Tests** (4 hours)
- Test 10MB file import
- Test 50MB file import
- Test 100MB file limit
- Test memory usage
- Test streaming behavior

**Task 6.3: Geometry Validation Tests** (4 hours)
- Test non-triangulated meshes
- Test empty meshes
- Test degenerate triangles
- Test coordinate transformations

#### Priority 6: IFC Import Tests (12 hours)

**Note:** Requires full IFC service implementation

**Task 7.1: Entity Parsing Tests** (6 hours)
- Test IfcWall parsing
- Test IfcDoor parsing
- Test IfcWindow parsing
- Test IfcSlab parsing
- Test property set extraction
- Test relationship mapping

**Task 7.2: Integration with Graph** (6 hours)
- Test graph node creation from IFC entities
- Test graph edge creation from relationships
- Test spatial hierarchy
- Test full workflow

### Phase 3: End-to-End & Performance (Week 5) - 24 hours

#### Priority 7: E2E Workflows (12 hours)

**Task 8.1: IFC to Graph to 3D Workflow** (6 hours)
- Upload IFC → verify features → query graph → retrieve geometry
- Test with small, medium, large files
- Verify data consistency throughout

**Task 8.2: 3D Model Lifecycle** (6 hours)
- Upload → metadata → export → verify integrity
- Test format conversions
- Test concurrent operations

#### Priority 8: Performance & Load Tests (12 hours)

**Task 9.1: Create BenchmarkDotNet Tests** (6 hours)
- Graph query benchmarks
- 3D import benchmarks
- IFC parsing benchmarks
- Export format benchmarks

**Task 9.2: Stress Tests** (6 hours)
- 100 concurrent users
- Large dataset operations
- Memory leak detection
- Connection pool testing

---

## 5. Sample Test Code

### 5.1 Graph Database - Error Handling Tests

```csharp
// File: Honua.Server.Core.Tests.Data/GraphDatabaseServiceErrorTests.cs

using System;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Models.Graph;
using Honua.Server.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using FluentAssertions;

namespace Honua.Server.Core.Tests.Data;

[Trait("Category", "Unit")]
public class GraphDatabaseServiceErrorTests : IAsyncLifetime
{
    private readonly ILogger<GraphDatabaseService> _logger;
    private GraphDatabaseService? _service;
    private readonly string _testGraphName = $"test_errors_{Guid.NewGuid():N}";
    private bool _skipTests = false;

    public GraphDatabaseServiceErrorTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        _logger = loggerFactory.CreateLogger<GraphDatabaseService>();
    }

    public async Task InitializeAsync()
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_AGE_CONNECTION_STRING")
                ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

            var options = Options.Create(new GraphDatabaseOptions
            {
                Enabled = true,
                ConnectionString = connectionString,
                DefaultGraphName = _testGraphName,
                AutoCreateGraph = true
            });

            _service = new GraphDatabaseService(options, _logger);
            await _service.GraphExistsAsync(_testGraphName);
        }
        catch (Exception)
        {
            _skipTests = true;
        }
    }

    public async Task DisposeAsync()
    {
        if (_service != null && !_skipTests)
        {
            try
            {
                var exists = await _service.GraphExistsAsync(_testGraphName);
                if (exists)
                {
                    await _service.DropGraphAsync(_testGraphName);
                }
                await _service.DisposeAsync();
            }
            catch { }
        }
    }

    [Fact]
    public async Task ExecuteCypherQuery_WithInvalidSyntax_ShouldThrowException()
    {
        Skip.If(_skipTests, "PostgreSQL AGE not available");

        // Arrange
        var invalidQuery = "INVALID CYPHER SYNTAX HERE";

        // Act
        Func<Task> act = async () => await _service!.ExecuteCypherQueryAsync(invalidQuery);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*syntax*", "because invalid Cypher should produce syntax error");
    }

    [Fact]
    public async Task CreateNode_WithNullLabel_ShouldThrowArgumentException()
    {
        Skip.If(_skipTests, "PostgreSQL AGE not available");

        // Arrange
        var node = new GraphNode(null!)
        {
            Properties = new Dictionary<string, object> { ["test"] = "value" }
        };

        // Act
        Func<Task> act = async () => await _service!.CreateNodeAsync(node);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("label");
    }

    [Fact]
    public async Task GetNodeById_WithNonExistentId_ShouldReturnNull()
    {
        Skip.If(_skipTests, "PostgreSQL AGE not available");

        // Arrange
        long nonExistentId = 999999999;

        // Act
        var result = await _service!.GetNodeByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull("because the node ID does not exist");
    }

    [Fact]
    public async Task CreateEdge_WithNonExistentNodes_ShouldThrowException()
    {
        Skip.If(_skipTests, "PostgreSQL AGE not available");

        // Arrange
        var edge = new GraphEdge
        {
            Type = "LINKS_TO",
            StartNodeId = 999999998,
            EndNodeId = 999999999
        };

        // Act
        Func<Task> act = async () => await _service!.CreateEdgeAsync(edge);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateNode_WithSpecialCharactersInProperties_ShouldEscapeCorrectly()
    {
        Skip.If(_skipTests, "PostgreSQL AGE not available");

        // Arrange
        var node = new GraphNode("TestNode")
        {
            Properties = new Dictionary<string, object>
            {
                ["name"] = "O'Reilly",  // Single quote
                ["description"] = "Test \"quoted\" value",  // Double quotes
                ["path"] = "C:\\Users\\Test"  // Backslashes
            }
        };

        // Act
        var createdNode = await _service!.CreateNodeAsync(node);

        // Assert
        createdNode.Should().NotBeNull();
        createdNode.Properties["name"].Should().Be("O'Reilly");
        createdNode.Properties["description"].Should().Be("Test \"quoted\" value");
        createdNode.Properties["path"].Should().Be("C:\\Users\\Test");
    }
}
```

### 5.2 3D Geometry Service - Core Tests

```csharp
// File: Honua.Server.Core.Tests.Data/Geometry3DServiceTests.cs

using System;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Core.Models.Geometry3D;
using Honua.Server.Core.Services.Geometry3D;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;

namespace Honua.Server.Core.Tests.Data;

[Trait("Category", "Unit")]
public class Geometry3DServiceTests
{
    private readonly ILogger<Geometry3DService> _logger;
    private readonly Geometry3DService _service;

    public Geometry3DServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        _logger = loggerFactory.CreateLogger<Geometry3DService>();
        _service = new Geometry3DService(_logger);
    }

    [Fact]
    public async Task ImportGeometry_WithValidObjFile_ShouldSucceed()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);

        var request = new UploadGeometry3DRequest
        {
            FeatureId = Guid.NewGuid(),
            Format = "obj"
        };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.GeometryId.Should().NotBeEmpty();
        response.VertexCount.Should().Be(8, "a cube has 8 vertices");
        response.FaceCount.Should().Be(12, "a cube has 12 triangular faces");
        response.Type.Should().Be(GeometryType3D.TriangleMesh);
        response.BoundingBox.Should().NotBeNull();
    }

    [Fact]
    public async Task ImportGeometry_WithInvalidFormat_ShouldReturnError()
    {
        // Arrange
        var invalidContent = System.Text.Encoding.UTF8.GetBytes("This is not a valid 3D file");
        using var stream = new MemoryStream(invalidContent);

        var request = new UploadGeometry3DRequest { Format = "obj" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "invalid.obj", request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ImportGeometry_WithBinaryStlFile_ShouldSucceed()
    {
        // Arrange
        var stlContent = GenerateSimpleCubeBinaryStl();
        using var stream = new MemoryStream(stlContent);

        var request = new UploadGeometry3DRequest { Format = "stl" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "cube.stl", request);

        // Assert
        response.Success.Should().BeTrue();
        response.VertexCount.Should().BeGreaterThan(0);
        response.FaceCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetGeometry_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.GetGeometryAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGeometry_WithIncludeMeshData_ShouldReturnMesh()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Act
        var geometry = await _service.GetGeometryAsync(uploadResponse.GeometryId, includeMeshData: true);

        // Assert
        geometry.Should().NotBeNull();
        geometry!.Mesh.Should().NotBeNull();
        geometry.Mesh!.Vertices.Should().NotBeEmpty();
        geometry.Mesh.Indices.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetGeometry_WithoutIncludeMeshData_ShouldNotReturnMesh()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Act
        var geometry = await _service.GetGeometryAsync(uploadResponse.GeometryId, includeMeshData: false);

        // Assert
        geometry.Should().NotBeNull();
        geometry!.Mesh.Should().BeNull();
        geometry.GeometryData.Should().BeNull();
    }

    [Fact]
    public async Task ExportGeometry_ToStl_ShouldSucceed()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var importStream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(importStream, "cube.obj", request);

        var exportOptions = new ExportGeometry3DOptions
        {
            Format = "stl",
            BinaryFormat = true
        };

        // Act
        var exportStream = await _service.ExportGeometryAsync(uploadResponse.GeometryId, exportOptions);

        // Assert
        exportStream.Should().NotBeNull();
        exportStream.Length.Should().BeGreaterThan(0);

        // Verify it's valid STL by reading header
        exportStream.Position = 0;
        var header = new byte[80];
        await exportStream.ReadAsync(header);
        // Binary STL has 80-byte header
    }

    [Fact]
    public async Task DeleteGeometry_ShouldRemoveFromStorage()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Act
        await _service.DeleteGeometryAsync(uploadResponse.GeometryId);
        var deletedGeometry = await _service.GetGeometryAsync(uploadResponse.GeometryId);

        // Assert
        deletedGeometry.Should().BeNull();
    }

    [Fact]
    public async Task FindGeometriesByBoundingBox_ShouldReturnIntersecting()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest { FeatureId = Guid.NewGuid() };

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        var searchBox = new BoundingBox3D
        {
            MinX = -2, MinY = -2, MinZ = -2,
            MaxX = 2, MaxY = 2, MaxZ = 2
        };

        // Act
        var results = await _service.FindGeometriesByBoundingBoxAsync(searchBox);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(g => g.Id == uploadResponse.GeometryId);
    }

    [Fact]
    public async Task UpdateGeometryMetadata_ShouldPersistChanges()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        var newMetadata = new Dictionary<string, object>
        {
            ["author"] = "Test User",
            ["version"] = 2,
            ["lastModified"] = DateTime.UtcNow
        };

        // Act
        await _service.UpdateGeometryMetadataAsync(uploadResponse.GeometryId, newMetadata);
        var updatedGeometry = await _service.GetGeometryAsync(uploadResponse.GeometryId);

        // Assert
        updatedGeometry.Should().NotBeNull();
        updatedGeometry!.Metadata["author"].Should().Be("Test User");
        updatedGeometry.Metadata["version"].Should().Be(2);
    }

    #region Test Data Generators

    private static byte[] GenerateSimpleCubeObj()
    {
        var obj = @"# Simple cube
v -1.0 -1.0 -1.0
v -1.0 -1.0  1.0
v -1.0  1.0 -1.0
v -1.0  1.0  1.0
v  1.0 -1.0 -1.0
v  1.0 -1.0  1.0
v  1.0  1.0 -1.0
v  1.0  1.0  1.0

f 1 2 3
f 2 4 3
f 5 6 7
f 6 8 7
f 1 5 2
f 5 6 2
f 3 7 4
f 7 8 4
f 1 5 3
f 5 7 3
f 2 6 4
f 6 8 4
";
        return System.Text.Encoding.UTF8.GetBytes(obj);
    }

    private static byte[] GenerateSimpleCubeBinaryStl()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 80-byte header
        writer.Write(new byte[80]);

        // Number of triangles (12 for a cube)
        writer.Write((uint)12);

        // For each triangle, write:
        // - normal (3 floats)
        // - vertex 1 (3 floats)
        // - vertex 2 (3 floats)
        // - vertex 3 (3 floats)
        // - attribute byte count (ushort)

        // Simplified - just write one triangle as example
        for (int i = 0; i < 12; i++)
        {
            // Normal
            writer.Write(0.0f);
            writer.Write(0.0f);
            writer.Write(1.0f);

            // Vertex 1
            writer.Write(-1.0f);
            writer.Write(-1.0f);
            writer.Write(1.0f);

            // Vertex 2
            writer.Write(1.0f);
            writer.Write(-1.0f);
            writer.Write(1.0f);

            // Vertex 3
            writer.Write(-1.0f);
            writer.Write(1.0f);
            writer.Write(1.0f);

            // Attribute byte count
            writer.Write((ushort)0);
        }

        return ms.ToArray();
    }

    #endregion
}
```

### 5.3 Geometry3D Controller - API Tests

```csharp
// File: Honua.Server.Host.Tests/API/Geometry3DControllerTests.cs

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Honua.Server.Core.Models.Geometry3D;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace Honua.Server.Host.Tests.API;

[Trait("Category", "Integration")]
public class Geometry3DControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public Geometry3DControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadGeometry_WithValidObjFile_ReturnsOkWithGeometryId()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(objContent);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("model/obj");
        content.Add(fileContent, "file", "cube.obj");

        // Act
        var response = await _client.PostAsync("/api/geometry/3d/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UploadGeometry3DResponse>(responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.GeometryId.Should().NotBeEmpty();
        result.VertexCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UploadGeometry_WithUnsupportedFormat_ReturnsBadRequest()
    {
        // Arrange
        var invalidContent = System.Text.Encoding.UTF8.GetBytes("invalid");
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(invalidContent);
        content.Add(fileContent, "file", "file.invalid");

        // Act
        var response = await _client.PostAsync("/api/geometry/3d/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadGeometry_WithFileTooLarge_ReturnsBadRequest()
    {
        // Arrange - Create a file larger than 100MB limit
        var largeContent = new byte[101 * 1024 * 1024]; // 101 MB
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(largeContent);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("model/obj");
        content.Add(fileContent, "file", "large.obj");

        // Act
        var response = await _client.PostAsync("/api/geometry/3d/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetGeometry_WithValidId_ReturnsGeometry()
    {
        // Arrange - First upload a geometry
        var objContent = GenerateSimpleCubeObj();
        using var uploadContent = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(objContent);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("model/obj");
        uploadContent.Add(fileContent, "file", "cube.obj");

        var uploadResponse = await _client.PostAsync("/api/geometry/3d/upload", uploadContent);
        var uploadResult = JsonSerializer.Deserialize<UploadGeometry3DResponse>(
            await uploadResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Act
        var response = await _client.GetAsync($"/api/geometry/3d/{uploadResult!.GeometryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var geometry = JsonSerializer.Deserialize<ComplexGeometry3D>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        geometry.Should().NotBeNull();
        geometry!.Id.Should().Be(uploadResult.GeometryId);
    }

    [Fact]
    public async Task GetGeometry_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/geometry/3d/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportGeometry_ToStl_ReturnsStlFile()
    {
        // Arrange - Upload geometry
        var objContent = GenerateSimpleCubeObj();
        using var uploadContent = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(objContent);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("model/obj");
        uploadContent.Add(fileContent, "file", "cube.obj");

        var uploadResponse = await _client.PostAsync("/api/geometry/3d/upload", uploadContent);
        var uploadResult = JsonSerializer.Deserialize<UploadGeometry3DResponse>(
            await uploadResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Act
        var response = await _client.GetAsync(
            $"/api/geometry/3d/{uploadResult!.GeometryId}/export?format=stl&binary=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("model/stl");

        var fileContent = await response.Content.ReadAsByteArrayAsync();
        fileContent.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteGeometry_WithValidId_ReturnsNoContent()
    {
        // Arrange - Upload geometry
        var objContent = GenerateSimpleCubeObj();
        using var uploadContent = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(objContent);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("model/obj");
        uploadContent.Add(fileContent, "file", "cube.obj");

        var uploadResponse = await _client.PostAsync("/api/geometry/3d/upload", uploadContent);
        var uploadResult = JsonSerializer.Deserialize<UploadGeometry3DResponse>(
            await uploadResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Act
        var response = await _client.DeleteAsync($"/api/geometry/3d/{uploadResult!.GeometryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var getResponse = await _client.GetAsync($"/api/geometry/3d/{uploadResult.GeometryId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchByBoundingBox_ReturnsMatchingGeometries()
    {
        // Arrange - Upload geometry
        var objContent = GenerateSimpleCubeObj();
        using var uploadContent = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(objContent);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("model/obj");
        uploadContent.Add(fileContent, "file", "cube.obj");

        await _client.PostAsync("/api/geometry/3d/upload", uploadContent);

        // Act
        var response = await _client.GetAsync(
            "/api/geometry/3d/search/bbox?minX=-2&minY=-2&minZ=-2&maxX=2&maxY=2&maxZ=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var geometries = JsonSerializer.Deserialize<ComplexGeometry3D[]>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        geometries.Should().NotBeEmpty();
    }

    private static byte[] GenerateSimpleCubeObj()
    {
        var obj = @"# Simple cube
v -1.0 -1.0 -1.0
v  1.0 -1.0 -1.0
v  1.0  1.0 -1.0
v -1.0  1.0 -1.0
f 1 2 3
f 1 3 4
";
        return System.Text.Encoding.UTF8.GetBytes(obj);
    }
}
```

### 5.4 IFC Import - Validation Tests

```csharp
// File: Honua.Server.Core.Tests.Data/IfcImportServiceTests.cs

using System;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Core.Models.Ifc;
using Honua.Server.Core.Services;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;

namespace Honua.Server.Core.Tests.Data;

[Trait("Category", "Unit")]
public class IfcImportServiceTests
{
    private readonly ILogger<IfcImportService> _logger;
    private readonly IfcImportService _service;

    public IfcImportServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        _logger = loggerFactory.CreateLogger<IfcImportService>();
        _service = new IfcImportService(_logger);
    }

    [Fact]
    public async Task ValidateIfc_WithValidStepFile_ShouldReturnValid()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile();
        using var stream = new MemoryStream(ifcContent);

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.FileFormat.Should().Be("STEP");
        result.SchemaVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateIfc_WithInvalidFile_ShouldReturnInvalid()
    {
        // Arrange
        var invalidContent = System.Text.Encoding.UTF8.GetBytes("This is not an IFC file");
        using var stream = new MemoryStream(invalidContent);

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExtractMetadata_WithValidIfc_ShouldReturnMetadata()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile();
        using var stream = new MemoryStream(ifcContent);

        // Act
        var metadata = await _service.ExtractMetadataAsync(stream);

        // Assert
        metadata.Should().NotBeNull();
        metadata.SchemaVersion.Should().NotBeNullOrEmpty();
        metadata.LengthUnit.Should().Be("METRE");
    }

    [Fact]
    public void GetSupportedSchemaVersions_ShouldReturnVersionList()
    {
        // Act
        var versions = _service.GetSupportedSchemaVersions();

        // Assert
        versions.Should().NotBeEmpty();
        versions.Should().Contain("IFC4");
        versions.Should().Contain("IFC2x3");
    }

    [Fact]
    public async Task ImportIfcFile_WithNullStream_ShouldThrowArgumentNullException()
    {
        // Arrange
        Stream? nullStream = null;
        var options = new IfcImportOptions
        {
            TargetServiceId = "test-service",
            TargetLayerId = "test-layer"
        };

        // Act
        Func<Task> act = async () => await _service.ImportIfcFileAsync(nullStream!, options);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ImportIfcFile_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile();
        using var stream = new MemoryStream(ifcContent);

        // Act
        Func<Task> act = async () => await _service.ImportIfcFileAsync(stream, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // Note: Full import tests will require Xbim.Essentials integration
    // The following test demonstrates the structure for future implementation

    [Fact(Skip = "Requires Xbim.Essentials integration")]
    public async Task ImportIfcFile_WithValidFile_ShouldCreateFeatures()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile();
        using var stream = new MemoryStream(ifcContent);

        var options = new IfcImportOptions
        {
            TargetServiceId = "test-service",
            TargetLayerId = "test-layer",
            ImportGeometry = true,
            ImportProperties = true,
            ImportRelationships = true
        };

        // Act
        var result = await _service.ImportIfcFileAsync(stream, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.FeaturesCreated.Should().BeGreaterThan(0);
        result.EntityTypeCounts.Should().NotBeEmpty();
    }

    #region Test Data Generators

    private static byte[] GenerateSimpleIfcStepFile()
    {
        // Minimal valid IFC STEP file
        var ifc = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('','2025-11-10T00:00:00',(''),(''),'','','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('3MD_HkJ6X2EhY9W5t6mVFX',$,'Test Project',$,$,$,$,(#2),#3);
#2=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#4,$);
#3=IFCUNITASSIGNMENT((#5));
#4=IFCAXIS2PLACEMENT3D(#6,$,$);
#5=IFCSIUNIT(*,.LENGTHUNIT.,.METRE.);
#6=IFCCARTESIANPOINT((0.,0.,0.));
ENDSEC;
END-ISO-10303-21;
";
        return System.Text.Encoding.UTF8.GetBytes(ifc);
    }

    #endregion
}
```

### 5.5 End-to-End Workflow Test

```csharp
// File: Honua.Server.Host.Tests/Integration/Phase1WorkflowTests.cs

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Honua.Server.Core.Models.Geometry3D;
using Honua.Server.Core.Models.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FluentAssertions;
using System.Text.Json;
using System.Collections.Generic;

namespace Honua.Server.Host.Tests.Integration;

[Trait("Category", "E2E")]
[Collection("E2E Tests")]
public class Phase1WorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public Phase1WorkflowTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteWorkflow_Upload3DModel_CreateGraphNodes_TraverseGraph()
    {
        // ============================================================
        // STEP 1: Upload a 3D geometry file
        // ============================================================

        var objContent = GenerateSimpleCubeObj();
        using var uploadContent = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(objContent);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("model/obj");
        uploadContent.Add(fileContent, "file", "test-cube.obj");

        var uploadResponse = await _client.PostAsync("/api/geometry/3d/upload", uploadContent);
        uploadResponse.EnsureSuccessStatusCode();

        var geometry = JsonSerializer.Deserialize<UploadGeometry3DResponse>(
            await uploadResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        geometry.Should().NotBeNull();
        var geometryId = geometry!.GeometryId;

        // ============================================================
        // STEP 2: Create graph nodes representing the 3D model
        // ============================================================

        // Create a "Building" node
        var buildingNode = new GraphNode("Building")
        {
            Properties = new Dictionary<string, object>
            {
                ["name"] = "Test Building",
                ["geometry_id"] = geometryId.ToString()
            }
        };

        var createNodeResponse = await _client.PostAsJsonAsync("/api/graph/nodes", buildingNode);
        createNodeResponse.EnsureSuccessStatusCode();

        var createdBuilding = JsonSerializer.Deserialize<GraphNode>(
            await createNodeResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        createdBuilding.Should().NotBeNull();
        createdBuilding!.Id.Should().NotBeNull();

        // Create a "Floor" node
        var floorNode = new GraphNode("Floor")
        {
            Properties = new Dictionary<string, object>
            {
                ["level"] = 1,
                ["name"] = "First Floor"
            }
        };

        var createFloorResponse = await _client.PostAsJsonAsync("/api/graph/nodes", floorNode);
        createFloorResponse.EnsureSuccessStatusCode();

        var createdFloor = JsonSerializer.Deserialize<GraphNode>(
            await createFloorResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // ============================================================
        // STEP 3: Create relationship between building and floor
        // ============================================================

        var relationship = new
        {
            RelationshipType = "CONTAINS",
            SourceNodeId = createdBuilding.Id,
            TargetNodeId = createdFloor!.Id,
            Properties = new Dictionary<string, object>
            {
                ["created_at"] = DateTime.UtcNow.ToString("O")
            }
        };

        var createRelResponse = await _client.PostAsJsonAsync("/api/graph/relationships", relationship);
        createRelResponse.EnsureSuccessStatusCode();

        // ============================================================
        // STEP 4: Traverse graph to find connected nodes
        // ============================================================

        var traversalRequest = new
        {
            StartNodeId = createdBuilding.Id,
            RelationshipTypes = new[] { "CONTAINS" },
            Direction = "Outgoing",
            MaxDepth = 2
        };

        var traverseResponse = await _client.PostAsJsonAsync("/api/graph/traverse", traversalRequest);
        traverseResponse.EnsureSuccessStatusCode();

        var traversalResult = JsonSerializer.Deserialize<GraphQueryResult>(
            await traverseResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        traversalResult.Should().NotBeNull();
        traversalResult!.Nodes.Should().Contain(n => n.Label == "Floor");

        // ============================================================
        // STEP 5: Retrieve 3D geometry associated with building
        // ============================================================

        var geometryResponse = await _client.GetAsync($"/api/geometry/3d/{geometryId}");
        geometryResponse.EnsureSuccessStatusCode();

        var retrievedGeometry = JsonSerializer.Deserialize<ComplexGeometry3D>(
            await geometryResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        retrievedGeometry.Should().NotBeNull();
        retrievedGeometry!.Id.Should().Be(geometryId);
        retrievedGeometry.VertexCount.Should().BeGreaterThan(0);

        // ============================================================
        // STEP 6: Clean up - delete nodes and geometry
        // ============================================================

        await _client.DeleteAsync($"/api/graph/nodes/{createdBuilding.Id}");
        await _client.DeleteAsync($"/api/graph/nodes/{createdFloor.Id}");
        await _client.DeleteAsync($"/api/geometry/3d/{geometryId}");
    }

    private static byte[] GenerateSimpleCubeObj()
    {
        var obj = @"# Simple cube
v -1.0 -1.0 -1.0
v  1.0 -1.0 -1.0
v  1.0  1.0 -1.0
v -1.0  1.0 -1.0
f 1 2 3
f 1 3 4
";
        return System.Text.Encoding.UTF8.GetBytes(obj);
    }
}
```

---

## 6. Test Data Requirements

### 6.1 3D Model Test Files

#### Small Files (for unit tests - < 10KB)

**simple-cube.obj** - 8 vertices, 12 triangles
```obj
# Unit cube centered at origin
v -1.0 -1.0 -1.0
v -1.0 -1.0  1.0
v -1.0  1.0 -1.0
v -1.0  1.0  1.0
v  1.0 -1.0 -1.0
v  1.0 -1.0  1.0
v  1.0  1.0 -1.0
v  1.0  1.0  1.0

vn  0.0  0.0  1.0
vn  0.0  0.0 -1.0
vn  0.0  1.0  0.0
vn  0.0 -1.0  0.0
vn  1.0  0.0  0.0
vn -1.0  0.0  0.0

f 1//1 2//1 4//1
f 1//1 4//1 3//1
f 5//2 8//2 6//2
f 5//2 7//2 8//2
```

**simple-cube.stl** (binary) - Generate programmatically in test code

**simple-cube.gltf** - Minimal glTF 2.0 file
```json
{
  "asset": {"version": "2.0"},
  "scenes": [{"nodes": [0]}],
  "nodes": [{"mesh": 0}],
  "meshes": [{
    "primitives": [{
      "attributes": {"POSITION": 0},
      "indices": 1
    }]
  }],
  "accessors": [
    {"bufferView": 0, "componentType": 5126, "count": 8, "type": "VEC3"},
    {"bufferView": 1, "componentType": 5123, "count": 36, "type": "SCALAR"}
  ],
  "bufferViews": [
    {"buffer": 0, "byteOffset": 0, "byteLength": 96},
    {"buffer": 0, "byteOffset": 96, "byteLength": 72}
  ],
  "buffers": [{"byteLength": 168, "uri": "data:application/octet-stream;base64,..."}]
}
```

#### Medium Files (for integration tests - 10-500KB)

**building-section.obj** - Realistic building section
- 500-1000 vertices
- Basic materials
- Texture coordinates
- Source: OpenBuildingModels or generate with Blender

**furniture.glb** - GLB with textures
- 1000-2000 vertices
- Embedded textures
- PBR materials
- Source: Sketchfab Free Models

#### Large Files (for performance tests - 5-100MB)

**complex-building.obj** - High-detail building
- 100,000+ vertices
- Multiple materials
- Source: OpenBuildingModels or BuildingSMART sample files

**city-block.stl** - Large mesh
- 500,000+ triangles
- Binary STL format
- Use for memory/performance testing

### 6.2 IFC Test Files

#### Small Files (for unit tests)

**simple-wall.ifc** (IFC4)
```ifc
ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('simple-wall.ifc','2025-11-10T00:00:00',('Test Author'),('Test Org'),'','','');
FILE_SCHEMA(('IFC4'));
ENDSEC;

DATA;
#1=IFCPROJECT('2O_4lshd58OwrYp4Y2yhH7',$,'Simple Wall Project',$,$,$,$,(#2),#3);
#2=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#4,$);
#3=IFCUNITASSIGNMENT((#5));
#4=IFCAXIS2PLACEMENT3D(#6,$,$);
#5=IFCSIUNIT(*,.LENGTHUNIT.,.METRE.);
#6=IFCCARTESIANPOINT((0.,0.,0.));
#10=IFCWALL('1kJkXB9qP0CRYp4Z2yhK9L',$,'TestWall',$,$,#11,$,$);
#11=IFCLOCALPLACEMENT($,#4);
ENDSEC;
END-ISO-10303-21;
```

**basic-structure.ifc** (IFC2x3)
- 10-20 entities
- Single building with walls, floors, roof
- No geometry (properties only)
- Source: Generate with IfcOpenShell or use BuildingSMART samples

#### Medium Files (for integration tests)

**simple-building.ifc**
- 500-1000 entities
- Building with floors, walls, doors, windows
- Spatial hierarchy
- Property sets
- Relationships
- Source: BuildingSMART IFC Sample Files

**building-with-properties.ifc**
- Include custom property sets
- Material definitions
- Quantity takeoffs
- Multiple buildings

#### Large Files (for performance tests)

**multi-story-building.ifc**
- 10,000+ entities
- 5-10 stories
- Complete MEP systems
- Source: Open IFC Model Repository

**campus-layout.ifc**
- 50,000+ entities
- Multiple buildings
- Site context with geolocation
- Infrastructure elements

### 6.3 Test Data Sources

**Free 3D Model Sources:**
1. **Smithsonian 3D Digitization** - Public domain models
2. **Sketchfab** - CC-BY licensed models
3. **Blend Swap** - Creative Commons models
4. **OpenBuildingModels** - AEC-specific models

**IFC Sample Files:**
1. **BuildingSMART IFC Sample Files** - https://www.buildingsmart.org
2. **Open IFC Model Repository** - GitHub repositories
3. **IfcOpenShell Test Files** - Unit test fixtures
4. **Autodesk Sample Files** - Educational IFC samples

**Programmatic Generation:**
```csharp
// For simple test cases, generate in code
public static class TestDataGenerators
{
    public static byte[] GenerateCube(double size = 1.0) { /* ... */ }
    public static byte[] GenerateStlCube(bool binary = true) { /* ... */ }
    public static byte[] GenerateGltfCube() { /* ... */ }
    public static byte[] GenerateSimpleIfcWall() { /* ... */ }
}
```

### 6.4 Test Data Organization

```
tests/TestData/
├── 3d-models/
│   ├── unit/                      # Small files for unit tests
│   │   ├── simple-cube.obj
│   │   ├── simple-cube.stl
│   │   ├── simple-cube-binary.stl
│   │   ├── simple-cube.gltf
│   │   ├── simple-cube.glb
│   │   ├── simple-cube.ply
│   │   └── README.md
│   ├── integration/               # Medium files for integration tests
│   │   ├── building-section.obj
│   │   ├── furniture.glb
│   │   ├── room-with-materials.obj
│   │   └── README.md
│   ├── performance/               # Large files for performance tests
│   │   ├── complex-building.obj
│   │   ├── city-block.stl
│   │   └── README.md
│   └── invalid/                   # Malformed files for error testing
│       ├── empty.obj
│       ├── malformed-syntax.obj
│       ├── corrupted.stl
│       └── README.md
│
├── ifc-files/
│   ├── unit/                      # Small IFC files
│   │   ├── simple-wall.ifc        (IFC4, ~10 entities)
│   │   ├── basic-structure.ifc    (IFC2x3, ~20 entities)
│   │   ├── ifc4x3-sample.ifc      (IFC4x3, latest standard)
│   │   └── README.md
│   ├── integration/               # Medium IFC files
│   │   ├── simple-building.ifc    (500-1000 entities)
│   │   ├── building-with-props.ifc
│   │   ├── multi-building-site.ifc
│   │   └── README.md
│   ├── performance/               # Large IFC files
│   │   ├── multi-story-building.ifc (10,000+ entities)
│   │   ├── campus-layout.ifc        (50,000+ entities)
│   │   └── README.md
│   └── invalid/                   # Invalid IFC files
│       ├── corrupted.ifc
│       ├── invalid-schema.ifc
│       ├── missing-required-entities.ifc
│       └── README.md
│
└── README.md                      # Master test data documentation
```

---

## 7. Effort Estimates

### 7.1 Summary by Phase

| Phase | Description | Effort (hours) | Duration (days) |
|-------|-------------|----------------|-----------------|
| Phase 1 | Critical Foundation | 40 | 5 (1 week) |
| Phase 2 | Comprehensive Coverage | 40 | 5 (1 week) |
| Phase 3 | E2E & Performance | 24 | 3 (3 days) |
| **TOTAL** | **Full Implementation** | **104** | **13 days** |

### 7.2 Detailed Breakdown

#### Phase 1: Critical Foundation (40 hours)

| Task | Component | Effort | Priority |
|------|-----------|--------|----------|
| Test infrastructure setup | All | 8h | P0 |
| Graph database error tests | GraphDatabaseService | 4h | P0 |
| Graph API controller tests | GraphController | 4h | P0 |
| 3D geometry core unit tests | Geometry3DService | 8h | P0 |
| 3D geometry API tests | Geometry3DController | 4h | P0 |
| 3D geometry integration tests | Full workflow | 4h | P0 |
| IFC validation tests | IfcImportService | 4h | P0 |
| IFC API controller tests | IfcImportController | 4h | P0 |

#### Phase 2: Comprehensive Coverage (40 hours)

| Task | Component | Effort | Priority |
|------|-----------|--------|----------|
| Graph performance tests | GraphDatabaseService | 6h | P1 |
| Graph edge cases | GraphDatabaseService | 6h | P1 |
| 3D format-specific tests | Geometry3DService | 8h | P1 |
| 3D large file tests | Geometry3DService | 4h | P1 |
| 3D geometry validation tests | Geometry3DService | 4h | P1 |
| IFC entity parsing tests | IfcImportService | 6h | P1 |
| IFC graph integration tests | IfcImportService | 6h | P1 |

**Note:** IFC tests in Phase 2 require full Xbim.Essentials integration first (estimated 16-24 hours of development work).

#### Phase 3: End-to-End & Performance (24 hours)

| Task | Component | Effort | Priority |
|------|-----------|--------|----------|
| IFC→Graph→3D workflow | Full system | 6h | P1 |
| 3D model lifecycle workflow | Full system | 6h | P1 |
| BenchmarkDotNet tests | All services | 6h | P1 |
| Stress tests | All services | 6h | P2 |

### 7.3 Prerequisites

Before starting testing work:

1. **IFC Service Implementation** (16-24 hours)
   - Integrate Xbim.Essentials NuGet package
   - Implement actual IFC parsing
   - Implement feature creation in Honua
   - Implement graph relationship creation

2. **Test Data Acquisition** (4-6 hours)
   - Download sample 3D files
   - Download sample IFC files
   - Create test data directory structure
   - Document file sources and licenses

3. **Development Environment Setup** (2 hours)
   - Install Docker for Testcontainers
   - Configure PostgreSQL with AGE extension
   - Set up test database connection strings
   - Verify all dependencies

### 7.4 Resource Requirements

**Developer Skillset:**
- C# / .NET experience (required)
- xUnit testing framework (required)
- 3D geometry concepts (helpful)
- IFC / BIM domain knowledge (helpful for IFC tests)
- SQL / graph databases (helpful)

**Infrastructure:**
- Docker Desktop or Docker Engine
- PostgreSQL 12+ with Apache AGE extension
- 16GB RAM recommended (for large file tests)
- 10GB disk space (for test files)

---

## 8. CI/CD Integration

### 8.1 Test Categorization

**Use xUnit traits to categorize tests:**

```csharp
[Trait("Category", "Unit")]          // Fast, no dependencies
[Trait("Category", "Integration")]   // Requires Docker/DB
[Trait("Category", "E2E")]           // Full workflows
[Trait("Category", "Performance")]   // Benchmarks
[Trait("Category", "SkipCI")]        // Skip in CI (manual only)
```

### 8.2 GitHub Actions Workflow (Recommended)

```yaml
# .github/workflows/phase1-tests.yml

name: Phase 1 Tests

on:
  push:
    branches: [main, develop]
    paths:
      - 'src/Honua.Server.Core/Services/Graph*/**'
      - 'src/Honua.Server.Core/Services/Geometry3D/**'
      - 'src/Honua.Server.Core/Services/*Ifc*/**'
      - 'src/Honua.Server.Host/API/Graph*'
      - 'src/Honua.Server.Host/API/Geometry3D*'
      - 'src/Honua.Server.Host/API/Ifc*'
      - 'tests/**'
  pull_request:
    branches: [main, develop]

env:
  DOTNET_VERSION: '9.0'

jobs:
  # ================================================================
  # TIER 1: Fast Unit Tests (< 2 minutes)
  # Runs on every commit
  # ================================================================
  unit-tests:
    name: Unit Tests
    runs-on: ubuntu-latest
    timeout-minutes: 10

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore

      - name: Run unit tests
        run: |
          dotnet test \
            --no-restore \
            --verbosity normal \
            --filter "Category=Unit" \
            --logger "trx;LogFileName=unit-tests.trx" \
            --results-directory ./test-results

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: unit-test-results
          path: ./test-results/*.trx

  # ================================================================
  # TIER 2: Integration Tests (< 10 minutes)
  # Runs on pull requests
  # ================================================================
  integration-tests:
    name: Integration Tests
    runs-on: ubuntu-latest
    timeout-minutes: 20
    if: github.event_name == 'pull_request'

    services:
      # PostgreSQL with Apache AGE for graph database tests
      postgres-age:
        image: apache/age:latest
        env:
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: testdb
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Setup Docker for Testcontainers
        run: |
          docker --version
          docker ps

      - name: Restore dependencies
        run: dotnet restore

      - name: Run integration tests
        env:
          POSTGRES_AGE_CONNECTION_STRING: "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=testdb"
          DOCKER_HOST: "unix:///var/run/docker.sock"
        run: |
          dotnet test \
            --no-restore \
            --verbosity normal \
            --filter "Category=Integration" \
            --logger "trx;LogFileName=integration-tests.trx" \
            --results-directory ./test-results

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: integration-test-results
          path: ./test-results/*.trx

      - name: Upload test logs
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: integration-test-logs
          path: ./test-results/**/*.log

  # ================================================================
  # TIER 3: E2E & Performance Tests (< 30 minutes)
  # Runs on merge to main or manually
  # ================================================================
  e2e-tests:
    name: End-to-End Tests
    runs-on: ubuntu-latest
    timeout-minutes: 45
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'

    services:
      postgres-age:
        image: apache/age:latest
        env:
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: testdb
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Setup Docker for Testcontainers
        run: |
          docker --version
          docker ps

      - name: Download test data
        run: |
          # Download or generate test 3D models and IFC files
          mkdir -p tests/TestData/3d-models
          mkdir -p tests/TestData/ifc-files
          # TODO: Add script to download test files

      - name: Restore dependencies
        run: dotnet restore

      - name: Run E2E tests
        env:
          POSTGRES_AGE_CONNECTION_STRING: "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=testdb"
          DOCKER_HOST: "unix:///var/run/docker.sock"
        run: |
          dotnet test \
            --no-restore \
            --verbosity normal \
            --filter "Category=E2E" \
            --logger "trx;LogFileName=e2e-tests.trx" \
            --results-directory ./test-results

      - name: Run performance tests
        env:
          POSTGRES_AGE_CONNECTION_STRING: "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=testdb"
        run: |
          dotnet run \
            --project tests/Honua.Server.Benchmarks/Honua.Server.Benchmarks.csproj \
            --configuration Release \
            -- --filter "*Phase1*"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: e2e-test-results
          path: ./test-results/*.trx

      - name: Upload benchmark results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: benchmark-results
          path: ./BenchmarkDotNet.Artifacts/**/*

  # ================================================================
  # Test Summary Report
  # ================================================================
  test-summary:
    name: Test Summary
    runs-on: ubuntu-latest
    needs: [unit-tests, integration-tests]
    if: always()

    steps:
      - name: Download all test results
        uses: actions/download-artifact@v4
        with:
          path: ./all-test-results

      - name: Publish test summary
        uses: test-summary/action@v2
        with:
          paths: "./all-test-results/**/*.trx"

      - name: Comment PR with test results
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const summary = `## Test Results

            ✅ Unit Tests: See artifacts
            ✅ Integration Tests: See artifacts

            [View detailed test results](https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }})
            `;

            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: summary
            });
```

### 8.3 Test Execution Strategy

**Local Development:**
```bash
# Fast feedback loop - unit tests only
dotnet test --filter "Category=Unit"

# Before committing - all tests
dotnet test

# Specific phase tests
dotnet test --filter "FullyQualifiedName~GraphDatabase"
dotnet test --filter "FullyQualifiedName~Geometry3D"
dotnet test --filter "FullyQualifiedName~Ifc"
```

**Pull Request:**
- Unit tests (required, must pass)
- Integration tests (required, must pass)
- Code coverage report
- Performance comparison (informational)

**Main Branch:**
- All tests from PR
- E2E workflow tests
- Performance benchmarks
- Publish results to dashboard

**Nightly:**
- All tests
- Long-running stress tests
- Memory leak detection
- Large file tests (100MB+ files)

### 8.4 Test Coverage Goals

**Minimum Coverage Targets:**

| Component | Line Coverage | Branch Coverage | Priority |
|-----------|---------------|-----------------|----------|
| GraphDatabaseService | 90% | 80% | P0 |
| Geometry3DService | 85% | 75% | P0 |
| IfcImportService | 80% | 70% | P1 |
| GraphController | 85% | 75% | P0 |
| Geometry3DController | 85% | 75% | P0 |
| IfcImportController | 80% | 70% | P1 |

**Coverage Enforcement:**
```yaml
# In CI workflow
- name: Check code coverage
  run: |
    dotnet test \
      --collect:"XPlat Code Coverage" \
      --results-directory ./coverage

    dotnet tool install -g dotnet-reportgenerator-globaltool

    reportgenerator \
      -reports:"./coverage/**/coverage.cobertura.xml" \
      -targetdir:"./coverage/report" \
      -reporttypes:"HtmlInline;Cobertura;Badges"

    # Fail if coverage below 80%
    dotnet tool install -g dotnet-coverage
    dotnet-coverage merge ./coverage/**/coverage.cobertura.xml -f cobertura -o merged.xml
    # Parse and check thresholds
```

### 8.5 Performance Regression Detection

**BenchmarkDotNet Integration:**

```csharp
// tests/Honua.Server.Benchmarks/GraphDatabaseBenchmarks.cs

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class GraphDatabaseBenchmarks
{
    private GraphDatabaseService _service = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        // Initialize service with test graph
    }

    [Benchmark]
    public async Task CreateNode_SingleNode()
    {
        var node = new GraphNode("Benchmark") { Properties = new() { ["value"] = 123 } };
        await _service.CreateNodeAsync(node);
    }

    [Benchmark]
    public async Task CreateNodes_Batch100()
    {
        var nodes = Enumerable.Range(0, 100)
            .Select(i => new GraphNode("Benchmark") { Properties = new() { ["index"] = i } })
            .ToList();

        await _service.CreateNodesAsync(nodes);
    }

    [Benchmark]
    public async Task TraverseGraph_Depth5()
    {
        // Start from root node, traverse 5 levels deep
        await _service.TraverseGraphAsync(rootNodeId, maxDepth: 5);
    }
}
```

**CI Integration:**
```yaml
- name: Run benchmarks
  run: dotnet run --project Honua.Server.Benchmarks --configuration Release

- name: Compare with baseline
  run: |
    # Compare current run with baseline
    # Fail if >10% performance regression
    ./scripts/compare-benchmarks.sh
```

---

## 9. Appendix

### 9.1 Testing Checklist

Use this checklist to track testing progress:

#### Graph Database (Apache AGE)
- [x] Basic CRUD operations (existing 13 tests)
- [ ] Error handling and edge cases
- [ ] API controller endpoints
- [ ] Performance benchmarks
- [ ] Concurrent access tests
- [ ] Transaction handling

#### 3D Geometry Service
- [ ] OBJ file import
- [ ] STL file import (binary and ASCII)
- [ ] glTF/GLB file import
- [ ] FBX file import
- [ ] PLY file import
- [ ] File validation
- [ ] Bounding box calculation
- [ ] Checksum verification
- [ ] Format conversion
- [ ] Large file handling (>50MB)
- [ ] Malformed file handling
- [ ] API endpoints
- [ ] Upload workflow
- [ ] Export workflow
- [ ] Storage and retrieval

#### IFC Import Service
- [ ] **PREREQUISITE:** Integrate Xbim.Essentials
- [ ] File format detection
- [ ] Schema version detection
- [ ] Metadata extraction
- [ ] Entity parsing (walls, doors, windows, etc.)
- [ ] Property set extraction
- [ ] Relationship mapping
- [ ] Graph integration
- [ ] Feature creation
- [ ] Full import workflow
- [ ] API endpoints
- [ ] Large file handling (>10K entities)

#### End-to-End Workflows
- [ ] 3D model upload → storage → retrieval → export
- [ ] IFC import → graph creation → 3D visualization
- [ ] Graph query → feature retrieval → geometry loading
- [ ] Multi-user concurrent operations

#### CI/CD
- [ ] GitHub Actions workflow configured
- [ ] Unit tests running on every commit
- [ ] Integration tests running on PR
- [ ] E2E tests running on main merge
- [ ] Code coverage reporting
- [ ] Performance regression detection
- [ ] Test result notifications

### 9.2 Success Criteria

**Phase 1 is considered complete when:**

1. ✅ All critical tests passing (P0)
2. ✅ Code coverage ≥80% for all Phase 1 services
3. ✅ All API endpoints have integration tests
4. ✅ CI/CD pipeline running all tests
5. ✅ Test data repository established
6. ✅ Documentation updated

**Phase 2 is considered complete when:**

1. ✅ Comprehensive test coverage (P1)
2. ✅ Performance benchmarks established
3. ✅ Large file tests passing
4. ✅ Edge cases covered
5. ✅ IFC service fully implemented and tested

**Phase 3 is considered complete when:**

1. ✅ E2E workflows verified
2. ✅ Performance baselines established
3. ✅ Stress tests passing
4. ✅ Production-ready test suite

### 9.3 Risk Assessment

**High Risks:**
1. **IFC service not implemented** - Blocks all IFC testing
   - **Mitigation:** Prioritize Xbim.Essentials integration
2. **Large file tests timeout** - May require infrastructure changes
   - **Mitigation:** Use timeout settings, optimize parsing
3. **Apache AGE availability** - Tests skip if not available
   - **Mitigation:** Use Testcontainers, provide Docker compose

**Medium Risks:**
1. **Test data licensing** - Some 3D models may have restrictions
   - **Mitigation:** Use CC-BY or public domain models
2. **Performance test variability** - CI runners may have inconsistent performance
   - **Mitigation:** Use relative benchmarks, allow variance tolerance
3. **Test maintenance** - Tests may become outdated
   - **Mitigation:** Review tests during code reviews

**Low Risks:**
1. **Test flakiness** - Intermittent failures
   - **Mitigation:** Use retry policies, investigate and fix
2. **Dependency updates** - Breaking changes in AssimpNet or Xbim
   - **Mitigation:** Pin versions, test before upgrading

### 9.4 Resources & References

**Testing Frameworks:**
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)

**3D Graphics & BIM:**
- [AssimpNet Documentation](https://bitbucket.org/Starnick/assimpnet)
- [Xbim.Essentials Documentation](https://docs.xbim.net/)
- [BuildingSMART IFC Standards](https://www.buildingsmart.org/standards/bsi-standards/industry-foundation-classes/)
- [Apache AGE Documentation](https://age.apache.org/)

**Test Data Sources:**
- [BuildingSMART Sample IFC Files](https://www.buildingsmart.org/sample-bim-files/)
- [Open IFC Model Repository](https://github.com/buildingSMART/Sample-Test-Files)
- [Sketchfab 3D Models](https://sketchfab.com/features/free-3d-models)

**Best Practices:**
- [Microsoft .NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Testing Pyramid](https://martinfowler.com/articles/practical-test-pyramid.html)
- [Integration Testing in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests)

---

## 10. Next Steps

### Immediate Actions (This Week)

1. **Review and Approve Plan** (1 day)
   - Review this document with team
   - Prioritize work based on project needs
   - Assign resources

2. **Set Up Test Infrastructure** (2 days)
   - Create test data directories
   - Download/generate sample files
   - Set up Docker for Testcontainers
   - Configure PostgreSQL with AGE

3. **Start Phase 1 Testing** (2 days)
   - Implement graph database error tests
   - Implement 3D geometry core tests
   - Set up CI workflow

### Week 2-3

1. **Complete Phase 1**
   - Finish all P0 tests
   - Achieve 80% coverage on core services
   - Deploy CI/CD pipeline

2. **Begin IFC Implementation**
   - Integrate Xbim.Essentials
   - Implement basic parsing
   - Write first IFC tests

### Week 4-5

1. **Complete Phase 2**
   - Performance tests
   - Edge cases
   - Advanced scenarios

2. **IFC Integration**
   - Full IFC service implementation
   - Comprehensive IFC tests

### Week 6

1. **Complete Phase 3**
   - E2E workflows
   - Stress tests
   - Documentation

2. **Production Readiness**
   - Final review
   - Performance tuning
   - Go/no-go decision

---

**Document End**

For questions or clarifications, contact the development team or refer to the linked resources.
