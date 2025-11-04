# Build.Orchestrator - Future Feature (Not Implemented)

## Status: ⚠️ TDD Stub Project

The `Honua.Build.Orchestrator` is a **future feature** with tests written first (Test-Driven Development approach). It's not implemented yet and intentionally fails.

## What Is It?

A cross-repository build system designed for:
- AOT compilation with CPU optimizations (Graviton NEON, Intel AVX512)
- Multi-cloud registry management (GitHub, AWS ECR, Azure ACR)
- Build caching and intelligent delivery
- Multi-architecture image builds

## Current Status

### Tests: ✅ Written (75 tests)
Located in: `tests/Honua.Build.Orchestrator.Tests/`

**Test Coverage:**
- ManifestHasherTests (17 tests) - Hash generation logic
- RegistryCacheCheckerTests (16 tests) - Cache hit/miss detection
- RegistryProvisionerTests (15 tests) - Registry provisioning
- BuildDeliveryServiceTests (13 tests) - Build delivery
- BuildOrchestratorTests (14 tests) - End-to-end orchestration

### Implementation: ❌ Not Implemented

**Evidence:**
```xml
<!-- From tests/Honua.Build.Orchestrator.Tests/Honua.Build.Orchestrator.Tests.csproj -->
<!-- Placeholder reference - will be created later -->
<!-- <ItemGroup>
  <ProjectReference Include="../../tools/Honua.Build.Orchestrator/Honua.Build.Orchestrator.csproj" />
</ItemGroup> -->
```

**Test Error:**
```
System.NotImplementedException : To be implemented in Honua.Build.Orchestrator project
```

## Why Tests Fail

The tests are **intentionally written first** (TDD) as:
1. Design documentation
2. Implementation specification
3. Regression prevention (once implemented)

The implementation is TODO.

## Current AOT/ReadyToRun Status

### What Exists: Minimal
Only in `src/Honua.Server.Host/Honua.Server.Host.csproj`:
```xml
<PublishReadyToRun>true</PublishReadyToRun>
<PublishTrimmed Condition="'$(EnableOData)' == 'false'">true</PublishTrimmed>
```

### What's Missing: Modern .NET 9 Features
- ❌ Native AOT (`<PublishAot>true</PublishAot>`)
- ❌ InvariantGlobalization for AOT
- ❌ TrimMode configuration
- ❌ CPU-specific optimizations
- ❌ Multi-architecture build support

## The Evolution of AOT

### .NET 7-9 Improvements:
- **ReadyToRun (old)**: Pre-JIT some code, keep JIT for flexibility
- **Native AOT (new)**: Full ahead-of-time compilation, no JIT
  - Faster startup (no JIT compilation)
  - Smaller size (trimmed dependencies)
  - Better for containers/serverless
  - Lower memory footprint

### Modern Best Practice:
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
  <TrimMode>full</TrimMode>
  <OptimizationPreference>Speed</OptimizationPreference>
</PropertyGroup>
```

## Workaround for Tests

**Problem**: Build.Orchestrator.Tests fail and stop test execution.

**Solution**: Exclude this test project from test runs:

### Option 1: Filter by name
```bash
./scripts/test-all.sh --filter "FullyQualifiedName!~Build.Orchestrator"
```

### Option 2: Exclude from project list
Edit `scripts/run-tests-csharp-parallel.sh` to exclude this project from auto-discovery.

### Option 3: Skip file (temporary)
Create `.skip` file:
```bash
touch tests/Honua.Build.Orchestrator.Tests/.skip
```

## Future Implementation

When ready to implement, follow the test specifications in:
- `tests/Honua.Build.Orchestrator.Tests/README.md`
- `tests/Honua.Build.Orchestrator.Tests/IMPLEMENTATION_SUMMARY.md`
- `tests/Honua.Build.Orchestrator.Tests/TEST_COVERAGE_SUMMARY.md`

Estimated effort: 6-8 weeks for full implementation.

## Recommendation

**For now:**
1. Exclude Build.Orchestrator.Tests from test runs
2. Focus on real test failures in implemented features
3. Document AOT modernization as a separate future task

**For later:**
1. Create separate epic for Build.Orchestrator implementation
2. Create separate epic for Native AOT migration (.NET 9)
3. These are independent improvements

## Impact

**Test Suite Impact:**
- **Before**: Test suite fails immediately on stub tests
- **After**: Test suite runs real tests, ignores future features
- **Result**: ~75 fewer "failures" (they're not real failures)

**AOT Impact:**
- **Current**: Basic ReadyToRun (some pre-compilation)
- **Potential**: Native AOT (full compilation, faster, smaller)
- **Benefit**: 30-50% faster cold starts, 20-40% smaller images

## Files to Note

### Stub Tests (skip these):
- `tests/Honua.Build.Orchestrator.Tests/*.cs`

### Partial Implementation:
- `tools/Honua.Build.Orchestrator/Program.cs` - Basic scaffold only
- `tools/Honua.Build.Orchestrator/ManifestHasher.cs` - Empty stub
- `tools/Honua.Build.Orchestrator/BuildOrchestrator.cs` - Empty stub

### AOT Configuration:
- `src/Honua.Server.Host/Honua.Server.Host.csproj` - ReadyToRun only

## Conclusion

This is **expected and documented**. The Build.Orchestrator tests are not failures - they're future feature specifications written as tests.

**Action:** Exclude from test runs and focus on real test failures.
