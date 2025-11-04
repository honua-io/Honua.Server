# Process Framework Integration Test

A comprehensive test suite for validating the Honua Process Framework implementation using Microsoft Semantic Kernel's Process Core.

## Overview

This test project verifies the correct implementation and integration of all 5 process workflows:

1. **DeploymentProcess** (8 steps) - Full Honua deployment workflow
2. **UpgradeProcess** (4 steps) - Blue-green upgrade workflow
3. **MetadataProcess** (3 steps) - Metadata extraction and STAC publishing
4. **GitOpsProcess** (3 steps) - GitOps configuration and drift monitoring
5. **BenchmarkProcess** (4 steps) - Performance benchmarking workflow

## Running the Tests

### Quick Start
```bash
cd /home/mike/projects/HonuaIO
dotnet run --project tests/ProcessFrameworkTest/ProcessFrameworkTest.csproj
```

### Build Only
```bash
dotnet build tests/ProcessFrameworkTest/ProcessFrameworkTest.csproj
```

### Run Specific Test
```bash
cd tests/ProcessFrameworkTest
dotnet run
```

## What the Test Validates

### 1. Process Builder Instantiation
- Verifies each process builder can be instantiated via `BuildProcess()`
- Confirms no exceptions during process construction
- Validates process builder returns valid `ProcessBuilder` instance

### 2. Step Count Verification
- Confirms each process has the expected number of steps:
  - DeploymentProcess: 8 steps
  - UpgradeProcess: 4 steps
  - MetadataProcess: 3 steps
  - GitOpsProcess: 3 steps
  - BenchmarkProcess: 4 steps

### 3. Dependency Injection Registration
- Validates all 22 process steps are registered in DI container
- Confirms steps can be resolved from service provider
- Checks for missing or incorrect registrations

### 4. Build Validation
- Ensures each process builds successfully using `builder.Build()`
- Validates process structure and event routing
- Confirms kernel integration works correctly

## Test Output

The test produces a detailed report including:

```
========================================
Process Framework Integration Test
========================================

Testing DeploymentProcess...
  ✓ DeploymentProcess built successfully
Testing UpgradeProcess...
  ✓ UpgradeProcess built successfully
...

========================================
Test Summary
========================================

[PASS] DeploymentProcess
      Expected Steps: 8
      Actual Steps: 8
...

Total: 5 processes tested
Passed: 5
Failed: 0

========================================
DI Registration Check
========================================

  ✓ ValidateDeploymentRequirementsStep is registered
  ✓ GenerateInfrastructureCodeStep is registered
...

Registered: 22/22
Missing: 0/22
```

## Project Structure

```
tests/ProcessFrameworkTest/
├── ProcessFrameworkTest.csproj  # Project file with dependencies
├── Program.cs                    # Test entry point
├── README.md                     # This file
└── TEST_RESULTS.md              # Detailed test results and analysis
```

## Dependencies

- **.NET 9.0** - Target framework
- **Honua.Cli.AI** - Main CLI project with Process Framework implementation
- **Microsoft.SemanticKernel** - SK Process Framework (transitive)
- **Microsoft.Extensions.DependencyInjection** - DI container (transitive)

## Integration Points

### Tested Components

1. **Process Builders** (`/src/Honua.Cli.AI/Services/Processes/`)
   - DeploymentProcess.cs
   - UpgradeProcess.cs
   - MetadataProcess.cs
   - GitOpsProcess.cs
   - BenchmarkProcess.cs

2. **Process Steps** (`/src/Honua.Cli.AI/Services/Processes/Steps/`)
   - Deployment/ (8 steps)
   - Upgrade/ (4 steps)
   - Metadata/ (3 steps)
   - GitOps/ (3 steps)
   - Benchmark/ (4 steps)

3. **DI Registration** (`/src/Honua.Cli.AI/Extensions/`)
   - AzureAIServiceCollectionExtensions.cs
   - RegisterProcessSteps() method

## Test Results

See [TEST_RESULTS.md](./TEST_RESULTS.md) for complete test results and analysis.

**Latest Test Status:** ✅ ALL TESTS PASSED

## Continuous Integration

To integrate this test into CI/CD pipelines:

```bash
# In your CI script
dotnet test tests/ProcessFrameworkTest/ProcessFrameworkTest.csproj
```

Or run directly:
```bash
dotnet run --project tests/ProcessFrameworkTest/ProcessFrameworkTest.csproj
# Check exit code: 0 = success, 1 = failure
```

## Troubleshooting

### Build Failures

If the test project fails to build:
1. Ensure .NET 9.0 SDK is installed
2. Restore packages: `dotnet restore`
3. Check that Honua.Cli.AI project builds successfully

### Test Failures

If tests fail:
1. Check that all 22 step classes exist in expected locations
2. Verify DI registrations in `AzureAIServiceCollectionExtensions.cs`
3. Ensure process builders in `/Services/Processes/` are correct
4. Review error messages in test output

### Missing Steps

If DI registration check shows missing steps:
1. Add missing step registration to `RegisterProcessSteps()` method
2. Ensure step class inherits from `KernelProcessStep<TState>`
3. Verify step has proper constructor with ILogger parameter

## Future Enhancements

Potential improvements for this test suite:

1. **Runtime Execution Testing**
   - Actually execute process workflows end-to-end
   - Validate state transitions between steps
   - Test event routing and data flow

2. **Error Scenario Testing**
   - Test failure handling and rollback
   - Validate StopProcess() behavior
   - Test error event propagation

3. **Performance Testing**
   - Measure process execution times
   - Test concurrent process execution
   - Validate resource cleanup

4. **Integration Testing**
   - Test with real dependencies (databases, cloud services)
   - Validate external service interactions
   - Test approval workflows

## Contributing

When adding new processes or steps:
1. Update the test to include new components
2. Add registration to `RegisterProcessSteps()` method
3. Update expected step counts
4. Run test to verify integration
5. Update TEST_RESULTS.md with findings

## License

Part of the HonuaIO project - see main repository for license details.
