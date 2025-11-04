# Honua Build Orchestrator Test Suite

Comprehensive unit tests for the Honua Build Orchestrator system - a cross-repository build system with AOT compilation support and multi-cloud registry management.

## Test Coverage Overview

### Test Files Created

1. **ManifestHasherTests.cs** (17 tests)
   - Deterministic hash generation for build configurations
   - Module ordering normalization
   - Customer ID exclusion from hashes
   - Tier determination logic
   - Image tag generation

2. **RegistryCacheCheckerTests.cs** (16 tests)
   - Cache hit/miss detection across registries
   - GitHub Container Registry integration
   - AWS ECR integration
   - Azure ACR integration
   - Multi-registry fallback
   - Rate limiting and retry logic
   - Multi-architecture manifest support

3. **RegistryProvisionerTests.cs** (15 tests)
   - GitHub namespace provisioning
   - AWS ECR repository and IAM user creation
   - Azure ACR and Service Principal management
   - Credential encryption and storage
   - Multi-cloud provisioning
   - Resource cleanup

4. **BuildDeliveryServiceTests.cs** (13 tests)
   - Cache-based image copying
   - Build-from-source fallback
   - Cross-cloud image transfer
   - Multi-architecture deployments
   - Image tagging strategies
   - Performance metrics

5. **BuildOrchestratorTests.cs** (14 tests)
   - End-to-end orchestration workflow
   - Git repository cloning (public/private)
   - Solution generation
   - AOT compilation with CPU optimizations
   - Cache-aware builds
   - Error handling and cleanup
   - Parallel multi-target builds

**Total: 75 comprehensive tests**

## Test Patterns and Technologies

### Testing Stack
- **xUnit 2.9.2** - Test framework
- **Moq 4.20.72** - Mocking framework
- **FluentAssertions 6.12.2** - Fluent assertion library
- **RichardSzalay.MockHttp 7.0.0** - HTTP mocking
- **AWSSDK.* packages** - AWS service mocking

### Test Architecture
```
BuildOrchestratorTests
├── ManifestHasherTests          - Pure logic, no I/O
├── RegistryCacheCheckerTests    - HTTP mocking
├── RegistryProvisionerTests     - Cloud SDK mocking
├── BuildDeliveryServiceTests    - Service composition
└── BuildOrchestratorTests       - End-to-end integration
```

## Running Tests

### Run All Tests
```bash
cd tests/Honua.Build.Orchestrator.Tests
dotnet test
```

### Run with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~ManifestHasherTests"
```

### Run with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Coverage Analysis

### Expected Coverage by Component

#### ManifestHasher (Target: 95%+)
- ✅ Hash computation (deterministic)
- ✅ Module sorting normalization
- ✅ Customer ID exclusion
- ✅ Tier determination
- ✅ Image tag generation
- ✅ Edge cases and validation

**Uncovered Scenarios:**
- SHA256 implementation (framework-level, no need to test)
- Serialization (covered by integration tests)

#### RegistryCacheChecker (Target: 85%+)
- ✅ GitHub registry checks
- ✅ AWS ECR checks
- ✅ Azure ACR checks
- ✅ Google GCR checks
- ✅ Multi-registry fallback
- ✅ Retry logic
- ✅ Rate limiting
- ✅ Multi-arch manifests

**Uncovered Scenarios:**
- Actual AWS SDK calls (requires AWS credentials)
- Network timeouts (integration test level)

#### RegistryProvisioner (Target: 85%+)
- ✅ GitHub namespace creation
- ✅ AWS ECR + IAM provisioning
- ✅ Azure ACR + SP provisioning
- ✅ Policy attachment
- ✅ Credential storage
- ✅ Multi-cloud provisioning
- ✅ Resource cleanup

**Uncovered Scenarios:**
- Encryption implementation (crypto library, tested separately)
- Database persistence (integration test level)

#### BuildDeliveryService (Target: 90%+)
- ✅ Cache hit flows
- ✅ Cache miss + build
- ✅ Image copying
- ✅ Cross-cloud auth
- ✅ Multi-architecture
- ✅ Tagging strategies
- ✅ Fallback mechanisms
- ✅ Performance tracking

**Uncovered Scenarios:**
- crane CLI execution (process execution, integration test)
- Actual image transfers (requires real registries)

#### BuildOrchestrator (Target: 85%+)
- ✅ End-to-end workflow
- ✅ Git cloning (public/private)
- ✅ Solution generation
- ✅ AOT compilation
- ✅ CPU optimizations (Graviton/Intel)
- ✅ Cache-aware builds
- ✅ Error handling
- ✅ Workspace cleanup
- ✅ Multi-target builds

**Uncovered Scenarios:**
- Git CLI execution (process execution)
- dotnet CLI execution (process execution)
- File system operations (mocked in unit tests, covered in integration)

### Overall Expected Coverage: **87%**

## Key Test Scenarios

### Deterministic Hashing
```csharp
// Ensures same configuration always produces same hash
// Critical for cache lookup accuracy
var hash1 = _hasher.ComputeHash(manifest1, target);
var hash2 = _hasher.ComputeHash(manifest2, target);
hash1.Should().Be(hash2);
```

### Cache Hit Optimization
```csharp
// Verifies builds are skipped when cache exists
// Saves ~2-5 minutes per build
result.CacheHit.Should().BeTrue();
_mockBuildExecutor.Verify(x => x.BuildImageAsync(...), Times.Never);
```

### AOT CPU Optimizations
```csharp
// Tests Graviton NEON and Intel AVX512 optimizations
// Critical for cloud-native performance
_mockDotNetBuilder.Verify(
    x => x.PublishAsync(...,
        It.Is<BuildOptions>(opts => opts.CpuOptimization == "neon")),
    Times.Once
);
```

### Multi-Cloud Provisioning
```csharp
// Ensures customers can deploy to any cloud provider
var results = await _provisioner.ProvisionMultipleAsync(
    customerId,
    new[] { RegistryProvider.GitHub, RegistryProvider.AwsEcr, RegistryProvider.AzureAcr }
);
results.Should().HaveCount(3);
results.Should().OnlyContain(r => r.Success);
```

## Test Data Patterns

### BuildManifest Factory
```csharp
private static BuildManifest CreateTestManifest(
    string? tier = "Pro",
    string version = "1.0.0",
    string[]? modules = null,
    string? customerId = null)
{
    return new BuildManifest
    {
        Tier = tier,
        Version = version,
        Modules = modules?.ToList() ?? new List<string> { "Core", "Ogc" },
        CustomerId = customerId,
        EnableAot = true,
        EnableTrimming = true
    };
}
```

### BuildTarget Factory
```csharp
private static BuildTarget CreateBuildTarget(
    string architecture,
    string cpuModel,
    string cloudProvider)
{
    return new BuildTarget
    {
        Architecture = architecture,
        CpuModel = cpuModel,
        CloudProvider = cloudProvider,
        OptimizationLevel = cpuModel switch
        {
            "graviton2" or "graviton3" => "neon",
            "cascade-lake" => "avx512",
            "skylake" => "avx2",
            _ => "generic"
        }
    };
}
```

## Mock Verification Patterns

### Strict Ordering Verification
```csharp
// Ensures steps execute in correct order
var sequence = new MockSequence();
_mockGitService.InSequence(sequence).Setup(...);
_mockSolutionGenerator.InSequence(sequence).Setup(...);
_mockDotNetBuilder.InSequence(sequence).Setup(...);
```

### Call Count Verification
```csharp
// Ensures operations aren't duplicated
_mockCacheChecker.Verify(
    x => x.CheckCacheAsync(...),
    Times.Once
);
```

### Never Called Verification
```csharp
// Ensures optimizations work (build skipped on cache hit)
_mockBuildExecutor.Verify(
    x => x.BuildImageAsync(...),
    Times.Never
);
```

## Implementation Roadmap

### Phase 1: Core Infrastructure (Week 1-2)
1. Implement ManifestHasher
   - SHA256 hashing
   - Module sorting
   - Tier determination

2. Implement RegistryCacheChecker
   - GitHub registry client
   - AWS ECR client
   - Azure ACR client

### Phase 2: Provisioning (Week 3-4)
3. Implement RegistryProvisioner
   - GitHub API integration
   - AWS SDK integration
   - Azure SDK integration
   - Credential encryption

4. Implement BuildDeliveryService
   - crane wrapper for image copying
   - Build orchestration
   - Multi-registry support

### Phase 3: Orchestration (Week 5-6)
5. Implement BuildOrchestrator
   - Git repository service
   - Solution generator
   - dotnet CLI wrapper
   - End-to-end workflow

### Phase 4: Optimization (Week 7-8)
6. Performance optimization
   - Parallel builds
   - Cache warming
   - Registry connection pooling

7. Observability
   - OpenTelemetry integration
   - Build metrics
   - Performance tracking

## Benefits of This Test Suite

### 1. **Test-Driven Development Ready**
All tests define expected behavior before implementation. Implementation can follow the test specifications.

### 2. **Comprehensive Edge Case Coverage**
- Cache hits and misses
- Network failures and retries
- Multi-cloud authentication
- Error handling and cleanup
- Parallel execution

### 3. **Regression Prevention**
75 tests ensure changes don't break existing functionality. Particularly important for:
- Hash determinism (cache accuracy depends on it)
- Multi-cloud compatibility
- AOT compilation flags

### 4. **Documentation Through Tests**
Tests serve as executable documentation:
```csharp
[Fact]
public async Task DeliverBuild_CacheHit_CopiesFromCache()
{
    // This test documents the cache-hit optimization flow
    // Expected: Image copy, no build
}
```

### 5. **CI/CD Integration**
Tests designed for automated pipelines:
- Fast execution (no external dependencies)
- Deterministic results (mocked I/O)
- Clear failure messages

## Performance Benchmarks

### Expected Test Execution Times
- ManifestHasherTests: ~0.5s (pure logic)
- RegistryCacheCheckerTests: ~1.5s (HTTP mocking)
- RegistryProvisionerTests: ~2.0s (SDK mocking)
- BuildDeliveryServiceTests: ~1.8s (service composition)
- BuildOrchestratorTests: ~2.5s (complex mocks)

**Total: ~8.3 seconds for full suite**

## Next Steps

### To Implement
1. Create `tools/Honua.Build.Orchestrator/` project
2. Implement services to make tests pass (TDD approach)
3. Add integration tests for actual cloud operations
4. Set up CI/CD pipeline with test coverage reporting
5. Add performance benchmarks for critical paths

### To Extend
1. Add tests for Docker multi-stage builds
2. Add tests for Kubernetes deployment manifests
3. Add tests for SBOM generation
4. Add tests for security scanning integration
5. Add chaos engineering tests (network failures, etc.)

## Maintenance

### Adding New Tests
```csharp
[Fact]
public async Task NewFeature_ExpectedBehavior_CorrectResult()
{
    // Arrange - Set up test data and mocks

    // Act - Execute the system under test

    // Assert - Verify expected outcomes
}
```

### Naming Convention
`MethodName_Scenario_ExpectedOutcome`

Example: `DeliverBuild_CacheHit_CopiesFromCache`

### Test Organization
- Group related tests in nested classes for better organization
- Use `[Theory]` for parameterized tests
- Use factories for complex test data

## References

- [xUnit Documentation](https://xunit.net/)
- [Moq Quickstart](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [.NET AOT Compilation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

## License

Tests follow the same license as the main Honua project.
