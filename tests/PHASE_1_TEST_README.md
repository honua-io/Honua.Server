# Phase 1 Test Suite - Quick Reference

**Last Updated:** November 10, 2025
**Test Count:** 78 tests across 3 new test files

---

## 🚀 Quick Start

### Run All Phase 1 Tests

```bash
# From repository root
dotnet test tests/Honua.Server.Core.Tests.Data/ --filter "Phase=Phase1"
```

### Run Individual Test Files

```bash
# Geometry 3D tests (29 tests)
dotnet test --filter "FullyQualifiedName~Geometry3DServiceTests"

# Graph error handling tests (33 tests)
dotnet test --filter "FullyQualifiedName~GraphDatabaseServiceErrorTests"

# IFC import tests (16 tests)
dotnet test --filter "FullyQualifiedName~IfcImportServiceTests"
```

---

## 📋 Prerequisites

### Required for All Tests
- ✅ .NET 9.0 SDK
- ✅ Honua.Server.Core.Tests.Data project built

### Required for GraphDatabaseService Tests
- 🐘 PostgreSQL with Apache AGE extension

**Quick setup with Docker:**
```bash
docker run -d --name postgres-age \
  -p 5432:5432 \
  -e POSTGRES_PASSWORD=postgres \
  apache/age:latest
```

**Set connection string (optional):**
```bash
export POSTGRES_AGE_CONNECTION_STRING="Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres"
```

---

## 📊 Test Summary

| Test File | Tests | Requires PostgreSQL | Status |
|-----------|-------|---------------------|--------|
| `Geometry3DServiceTests.cs` | 29 | ❌ No | ✅ Ready |
| `GraphDatabaseServiceErrorTests.cs` | 33 | ✅ Yes | ✅ Ready |
| `IfcImportServiceTests.cs` | 16 | ❌ No | ✅ Ready |
| **TOTAL** | **78** | - | **✅ Ready** |

---

## 🧪 What's Tested

### Geometry3DService (29 tests)
- ✅ OBJ file import (with/without normals)
- ✅ STL file import (binary & ASCII)
- ✅ glTF file import
- ✅ File validation (invalid, empty, unsupported)
- ✅ Bounding box calculation
- ✅ Geometry retrieval & export
- ✅ Spatial search by bounding box
- ✅ Metadata management

### GraphDatabaseService Error Handling (33 tests)
- ✅ Invalid Cypher queries
- ✅ Null/invalid inputs
- ✅ Non-existent nodes/edges
- ✅ Special characters & SQL injection prevention
- ✅ Concurrent operations
- ✅ Large data handling (10KB properties, 50 properties)
- ✅ Type preservation (string, int, bool, datetime)
- ✅ Edge cases (empty results, depth limits)

### IfcImportService (16 tests)
- ✅ IFC file validation (STEP format)
- ✅ Schema detection (IFC4, IFC2x3)
- ✅ Metadata extraction (units, project info)
- ✅ Null/invalid input handling
- ✅ Large file handling
- ✅ Unicode character support
- ⏭️ Full import (skipped - requires Xbim.Essentials)

---

## 🔍 Running Specific Test Scenarios

### Test Only 3D File Imports

```bash
dotnet test --filter "FullyQualifiedName~ImportGeometry"
```

### Test Only Validation

```bash
dotnet test --filter "FullyQualifiedName~Validation"
```

### Test Only Error Handling

```bash
dotnet test --filter "FullyQualifiedName~Error"
```

---

## 📈 Expected Results

### With PostgreSQL AGE Available
```
✅ Geometry3DServiceTests: 29 passed
✅ GraphDatabaseServiceErrorTests: 33 passed
✅ IfcImportServiceTests: 14 passed (2 skipped)

Total: 76 passed, 2 skipped
Duration: ~30-45 seconds
```

### Without PostgreSQL AGE
```
✅ Geometry3DServiceTests: 29 passed
⏭️ GraphDatabaseServiceErrorTests: 33 skipped
✅ IfcImportServiceTests: 14 passed (2 skipped)

Total: 43 passed, 35 skipped
Duration: ~10-15 seconds
```

---

## 🐛 Troubleshooting

### Tests Skip with "PostgreSQL AGE not available"

**Solution:** Start PostgreSQL AGE container:
```bash
docker run -d --name postgres-age -p 5432:5432 -e POSTGRES_PASSWORD=postgres apache/age:latest

# Wait for PostgreSQL to be ready
docker logs -f postgres-age
```

### Connection Refused Error

**Check if PostgreSQL is running:**
```bash
docker ps | grep postgres-age
```

**Check port availability:**
```bash
netstat -an | grep 5432
```

**Use custom connection string:**
```bash
export POSTGRES_AGE_CONNECTION_STRING="Host=myhost;Port=5433;Username=user;Password=pass;Database=db"
```

### Tests Fail with "Xbim.Essentials" Error

**Expected Behavior:** IFC import tests with `[Fact(Skip = "Requires Xbim.Essentials")]` are designed to be skipped.

**If other tests fail:** This indicates Xbim.Essentials is referenced but not fully integrated. These tests should auto-skip.

---

## 📁 Test Data

### Generated Programmatically

All test data is **generated in-memory** within test methods:

- **OBJ files:** Simple cubes with 8 vertices, 12 faces
- **STL files:** Binary and ASCII cubes
- **glTF files:** Minimal glTF 2.0 with base64 geometry
- **IFC files:** Minimal IFC4 and IFC2x3 STEP files

**No external files required!**

### Test Data Directories (Empty by Design)

```
tests/TestData/
├── 3d-models/
│   ├── unit/          # Reserved for manual test files
│   ├── integration/   # Reserved for manual test files
│   └── invalid/       # Reserved for manual test files
└── ifc-files/
    ├── unit/          # Reserved for manual test files
    └── invalid/       # Reserved for manual test files
```

---

## 🏗️ CI/CD Integration

### GitHub Actions Example

```yaml
- name: Start PostgreSQL AGE
  run: |
    docker run -d --name postgres-age \
      -p 5432:5432 \
      -e POSTGRES_PASSWORD=postgres \
      apache/age:latest

    # Wait for PostgreSQL to be ready
    sleep 10

- name: Run Phase 1 Tests
  env:
    POSTGRES_AGE_CONNECTION_STRING: "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres"
  run: |
    dotnet test tests/Honua.Server.Core.Tests.Data/ \
      --filter "Phase=Phase1" \
      --logger "trx;LogFileName=phase1-tests.trx"

- name: Upload Test Results
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: phase1-test-results
    path: "**/*.trx"
```

---

## 📚 Related Documentation

- **[PHASE_1_TEST_IMPLEMENTATION_SUMMARY.md](./PHASE_1_TEST_IMPLEMENTATION_SUMMARY.md)** - Detailed implementation summary
- **[PHASE_1_TEST_COVERAGE_ANALYSIS.md](./PHASE_1_TEST_COVERAGE_ANALYSIS.md)** - Original test plan
- **[PHASE_1_TESTING_QUICK_START.md](./PHASE_1_TESTING_QUICK_START.md)** - Quick start guide

---

## ✅ Test File Locations

```
tests/Honua.Server.Core.Tests.Data/
├── Geometry3DServiceTests.cs          (29 tests)
├── GraphDatabaseServiceTests.cs       (13 tests - existing)
├── GraphDatabaseServiceErrorTests.cs  (33 tests - NEW)
└── IfcImportServiceTests.cs           (16 tests - NEW)
```

---

## 🎯 Success Criteria

- [x] All tests compile without errors
- [ ] All tests pass (execute `dotnet test` to verify)
- [ ] PostgreSQL AGE container running (for graph tests)
- [ ] Test coverage ≥ 95% for Phase 1 components
- [ ] CI/CD integration configured

---

**Need Help?** Check the comprehensive documentation in `PHASE_1_TEST_IMPLEMENTATION_SUMMARY.md`
