# Code Coverage

This document describes Honua's code coverage requirements, tools, and best practices.

## Overview

Honua maintains strict code coverage requirements to ensure code quality, reliability, and maintainability. Code coverage is automatically measured and enforced in the CI/CD pipeline, with per-project thresholds tailored to each component's criticality.

## Coverage Thresholds

### Project-Specific Requirements

| Project | Minimum Coverage | Rationale |
|---------|-----------------|-----------|
| **Honua.Server.Core** | 65% | Core business logic, data providers, export engines, and critical functionality |
| **Honua.Server.Host** | 60% | API endpoints, middleware, authentication, and request handling |
| **Honua.Cli.AI** | 55% | AI agents, process framework, and integration-heavy components |
| **Honua.Cli** | 50% | CLI commands, user interface, and interactive components |
| **Overall Project** | 60% | Aggregate coverage across all production code |

### Why These Thresholds?

- **Honua.Server.Core (65%)**: Contains critical business logic that directly impacts data integrity and API compliance. High coverage ensures reliability.
- **Honua.Server.Host (60%)**: API endpoints and middleware require thorough testing but include framework code that's harder to test.
- **Honua.Cli.AI (55%)**: AI agents involve complex integrations with external LLM providers, making some code paths difficult to test without live services.
- **Honua.Cli (50%)**: CLI commands involve user interaction and terminal I/O, which are inherently difficult to test comprehensively.

## Tools and Setup

### Required Tools

- **.NET Test SDK**: Built into .NET 9.0 SDK
- **Coverlet**: Cross-platform code coverage library (included with .NET test SDK)
- **ReportGenerator**: Generates human-readable coverage reports

### Installation

ReportGenerator is automatically installed in CI. For local development:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

### Configuration Files

#### coverlet.runsettings

Located at the project root, this file configures coverage collection:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>opencover,cobertura</Format>
          <Exclude>
            [*Tests]*,
            [*Benchmarks]*,
            [*]*.Migrations.*,
            [*]*.DTO,
            [*]*.DTOs.*
          </Exclude>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

#### .codecov.yml

Configures Codecov integration for badge generation and PR comments.

## Running Coverage Locally

### Quick Coverage Check

Use the convenience script:

```bash
./scripts/check-coverage.sh
```

This script will:
1. Run all tests with coverage collection
2. Generate HTML and JSON reports
3. Check coverage thresholds
4. Optionally open the HTML report in your browser

### Manual Coverage Analysis

#### Step 1: Run Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory ./TestResults
```

#### Step 2: Generate Report

```bash
reportgenerator \
  -reports:"./TestResults/**/coverage.opencover.xml" \
  -targetdir:"./CoverageReport" \
  -reporttypes:"Html;JsonSummary;Badges"
```

#### Step 3: View Report

Open `./CoverageReport/index.html` in your browser.

### Coverage for Specific Projects

```bash
# Test a single project with coverage
dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --settings coverlet.runsettings
```

### Threshold-Only Check

To quickly check if current code meets thresholds without running tests:

```bash
./scripts/check-coverage.sh --threshold-only
```

## CI/CD Integration

### GitHub Actions Workflow

Coverage is automatically checked in the `ci.yml` workflow:

1. **Test Execution**: All tests run with coverage collection
2. **Report Generation**: ReportGenerator creates HTML and JSON reports
3. **Threshold Enforcement**: Custom script validates per-project and overall coverage
4. **Artifact Upload**: HTML reports uploaded as CI artifacts
5. **PR Comments**: Coverage summary posted to pull requests
6. **Codecov Upload**: Coverage data sent to Codecov for badge generation

### CI Failure Conditions

The CI pipeline will fail if:

- **Overall coverage** drops below 60%
- **Any project** falls below its specific threshold
- **Coverage data missing** for a core project
- **Tests fail** (coverage not calculated)

### Viewing Coverage in CI

#### GitHub Actions

1. Go to the Actions tab
2. Select a workflow run
3. View the "Check coverage thresholds" step for summary
4. Download the "coverage-report" artifact for detailed HTML report

#### Codecov

1. Visit https://codecov.io/gh/honua/honua.next
2. Browse coverage by file and commit
3. View coverage trend over time

## Exclusions

The following code is automatically excluded from coverage analysis:

### By Assembly

- `*.Tests` - All test projects
- `*.Benchmarks` - Benchmark projects
- `DataSeeder` - Sample data generation tool
- `ProcessFrameworkTest` - Process framework test harness

### By Class Pattern

- `*.Migrations.*` - Database migrations
- `*.DTO` / `*.DTOs.*` - Data transfer objects
- `*.Models.Generated.*` - Auto-generated models
- `*.Contracts.*` - Interface/contract definitions
- `*GlobalUsings` - Global using statements

### By Attribute

- `[ExcludeFromCodeCoverage]` - Explicit exclusion
- `[GeneratedCode]` - Auto-generated code
- `[Obsolete]` - Deprecated code
- `[CompilerGenerated]` - Compiler-generated code

### By File

- `**/Migrations/**/*.cs` - Migration files
- `**/*Designer.cs` - Designer files
- `**/obj/**/*.cs` - Build artifacts
- `**/GlobalUsings.g.cs` - Generated global usings

## Best Practices

### What to Test

**High Priority:**
- Public APIs and interfaces
- Business logic and algorithms
- Data transformation and validation
- Error handling and edge cases
- Security-sensitive code
- Configuration parsing

**Medium Priority:**
- Protected methods called by public APIs
- Internal utility methods
- Complex private methods
- State management

**Low Priority (Can Skip):**
- Simple property getters/setters
- DTOs with no logic
- Auto-implemented properties
- Trivial constructors

### Testing Strategies

#### Unit Tests

Focus on isolated, fast tests:

```csharp
[Fact]
public void ParseCoordinates_ValidInput_ReturnsPoint()
{
    // Arrange
    var parser = new CoordinateParser();
    var input = "45.0,-122.0";

    // Act
    var result = parser.Parse(input);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(45.0, result.Y, precision: 6);
    Assert.Equal(-122.0, result.X, precision: 6);
}
```

#### Integration Tests

Use for components requiring external dependencies:

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task GetFeatures_WithPostGIS_ReturnsGeoJson()
{
    // Arrange
    using var container = new PostgresContainer();
    await container.StartAsync();
    var provider = new PostgresDataProvider(container.ConnectionString);

    // Act
    var features = await provider.GetFeaturesAsync("test_layer");

    // Assert
    Assert.NotEmpty(features);
}
```

#### Mocking External Dependencies

Use Moq for external services:

```csharp
[Fact]
public async Task UploadToS3_Success_LogsMetrics()
{
    // Arrange
    var mockS3 = new Mock<IAmazonS3>();
    mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
          .ReturnsAsync(new PutObjectResponse());

    var uploader = new S3Uploader(mockS3.Object);

    // Act
    await uploader.UploadAsync("test.tif", Stream.Null);

    // Assert
    mockS3.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
}
```

### Improving Coverage

#### 1. Identify Gaps

Run coverage report and review the HTML report:

```bash
./scripts/check-coverage.sh
```

Open `./CoverageReport/index.html` and navigate to low-coverage files.

#### 2. Add Missing Tests

Focus on:
- Red (uncovered) lines in the report
- Complex methods with multiple branches
- Error handling paths

#### 3. Refactor for Testability

If code is hard to test, consider:
- Dependency injection
- Extracting interfaces
- Breaking up large methods
- Reducing coupling

#### 4. Use Code Coverage as a Guide

Coverage is a tool, not a goal:
- ✅ Use coverage to find untested code
- ✅ Write tests for critical functionality
- ❌ Don't write tests just to hit a number
- ❌ Don't test trivial code

## Coverage Reports

### HTML Report Structure

```
CoverageReport/
├── index.html              # Main entry point
├── Summary.json            # Machine-readable summary
├── SummaryGithub.md        # Markdown summary for GitHub
├── badge_linecoverage.svg  # Coverage badge
├── badge_branchcoverage.svg
└── src_Honua.Server.Core/  # Per-project drill-down
    └── ...
```

### Reading the Report

#### Overall Summary

- **Line Coverage**: Percentage of executable lines executed
- **Branch Coverage**: Percentage of decision branches taken
- **Method Coverage**: Percentage of methods executed

#### Color Coding

- 🟢 **Green**: High coverage (≥80%)
- 🟡 **Yellow**: Medium coverage (60-79%)
- 🔴 **Red**: Low coverage (<60%)

#### Drill-Down

Click on project names, namespaces, or classes to see line-by-line coverage.

## Troubleshooting

### No Coverage Data Generated

**Problem**: Tests run but no `coverage.opencover.xml` files are generated.

**Solution**:
1. Ensure `coverlet.runsettings` is present
2. Verify `--settings` flag points to the file
3. Check that `--collect:"XPlat Code Coverage"` is specified

### Coverage Lower Than Expected

**Problem**: Coverage percentage seems too low.

**Possible Causes**:
1. Tests not running (check test output)
2. Code excluded by filters
3. Integration tests skipped (missing Docker, etc.)

**Solution**:
1. Run tests with `--logger "console;verbosity=detailed"`
2. Review exclusion filters in `coverlet.runsettings`
3. Check test categories and filters

### Threshold Failing in CI

**Problem**: CI fails with "coverage below threshold" but local passes.

**Possible Causes**:
1. Different test categories running (CI may skip integration tests)
2. Environment-specific code paths
3. Cached coverage data locally

**Solution**:
1. Run `./scripts/check-coverage.sh` to match CI behavior
2. Check CI logs for which tests ran
3. Clear local coverage: `rm -rf TestResults CoverageReport`

### Report Generation Fails

**Problem**: `reportgenerator` command not found.

**Solution**:
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
export PATH="$PATH:$HOME/.dotnet/tools"
```

## References

### Documentation

- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator Documentation](https://github.com/danielpalme/ReportGenerator)
- [.NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Codecov Documentation](https://docs.codecov.com/)

### Internal Docs

- [Testing Guide](TESTING.md)
- [Contributing Guide](../CONTRIBUTING.md)
- [CI/CD Documentation](CI_CD.md)

## Support

If you have questions or encounter issues:

1. Check this documentation
2. Review the [Contributing Guide](../CONTRIBUTING.md)
3. Search existing [GitHub Issues](https://github.com/honua/honua.next/issues)
4. Create a new issue with the `testing` label

---

**Last Updated**: 2025-10-18
