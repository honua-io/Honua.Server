# Test Project Split - Quick Reference Guide

## Building Projects

```bash
# Build shared infrastructure
dotnet build tests/Honua.Server.Core.Tests.Shared/

# Build individual test projects
dotnet build tests/Honua.Server.Core.Tests.Raster/
dotnet build tests/Honua.Server.Core.Tests.Data/
dotnet build tests/Honua.Server.Core.Tests.OgcProtocols/
dotnet build tests/Honua.Server.Core.Tests.Apis/
dotnet build tests/Honua.Server.Core.Tests.Security/
dotnet build tests/Honua.Server.Core.Tests.DataOperations/
dotnet build tests/Honua.Server.Core.Tests.Infrastructure/
dotnet build tests/Honua.Server.Core.Tests.Integration/

# Build all test projects
dotnet build tests/Honua.Server.Core.Tests.*/
```

## Running Tests

```bash
# Run specific project tests
dotnet test tests/Honua.Server.Core.Tests.Raster/
dotnet test tests/Honua.Server.Core.Tests.Data/

# Run all test projects in parallel (once working)
dotnet test tests/ --parallel
```

## Project Structure

```
tests/
├── Honua.Server.Core.Tests.Shared/      # Shared infrastructure (36 files) ✅
├── Honua.Server.Core.Tests.Raster/      # Raster tests (48 files) ✅
├── Honua.Server.Core.Tests.Data/        # Data access tests (35 files) ⚠️
├── Honua.Server.Core.Tests.OgcProtocols/# OGC protocols (50 files) ⚠️
├── Honua.Server.Core.Tests.Apis/        # Modern APIs (49 files) ⚠️
├── Honua.Server.Core.Tests.Security/    # Security tests (29 files) ⚠️
├── Honua.Server.Core.Tests.DataOperations/# CRUD operations (37 files) ⚠️
├── Honua.Server.Core.Tests.Infrastructure/# Infrastructure (35 files) ⚠️
└── Honua.Server.Core.Tests.Integration/ # Integration tests (14 files) ⚠️
```

Legend: ✅ Builds successfully | ⚠️ Has compilation errors

## Quick Fixes for Compilation Errors

### Fix 1: Add InternalsVisibleTo Attributes

Create `src/Honua.Server.Core/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.Data")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.OgcProtocols")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.Security")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.DataOperations")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.Infrastructure")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.Integration")]
```

Create `src/Honua.Server.Host/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.OgcProtocols")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.Apis")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.Security")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.Infrastructure")]
```

### Fix 2: Update Namespace References

Find and replace in test files:

```bash
# Fix Stubs namespace
find tests/Honua.Server.Core.Tests.*/ -name "*.cs" -exec sed -i 's/using Honua\.Server\.Core\.Tests\.Shared\.Stubs;/using Honua.Server.Core.Tests.Shared;/g' {} \;

# Fix HonuaWebApplicationFactory reference
find tests/Honua.Server.Core.Tests.*/ -name "*.cs" -exec sed -i 's/HonuaWebApplicationFactory/HonuaTestWebApplicationFactory/g' {} \;
```

## Common Import Patterns

In new test files, use:

```csharp
using Honua.Server.Core.Tests.Shared; // For test infrastructure
using Xunit;                          // Already included via global using

// Test fixtures are in Shared project:
// - HonuaTestWebApplicationFactory
// - SharedPostgresFixture
// - RedisContainerFixture
// - GeometryTestData
// - MockBuilders
// - All stubs (StubFeatureRepository, etc.)
```

## Status Summary

**Working Projects (2/9):**
- ✅ Honua.Server.Core.Tests.Shared - Builds cleanly
- ✅ Honua.Server.Core.Tests.Raster - Builds cleanly

**Projects with Errors (7/9):**
- ⚠️ Data (60 errors) - Needs InternalsVisibleTo
- ⚠️ OgcProtocols (4 errors) - Needs namespace fixes
- ⚠️ Apis (38 errors) - Needs namespace fixes
- ⚠️ Security (5 errors) - Needs InternalsVisibleTo + namespace fixes
- ⚠️ DataOperations (3 errors) - Needs InternalsVisibleTo
- ⚠️ Infrastructure (24 errors) - Needs namespace fixes
- ⚠️ Integration (29 errors) - Needs InternalsVisibleTo

## Next Actions

1. Apply Fix 1 (InternalsVisibleTo) - Will resolve ~50% of errors
2. Apply Fix 2 (namespace fixes) - Will resolve remaining errors
3. Build and test each project
4. Update CI/CD for parallel execution

## Resources

- Full report: `docs/TEST_SPLIT_IMPLEMENTATION_REPORT.md`
- Original plan: `docs/test-splitting-plan.md`
- Original project: `tests/Honua.Server.Core.Tests/` (preserved as backup)
