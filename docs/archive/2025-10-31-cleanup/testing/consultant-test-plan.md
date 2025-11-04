# Honua Consultant - Test Plan

## 1. Testing Strategy Overview

### Test Pyramid
```
                  ┌─────────────┐
                  │   E2E Demo  │  (5%) - Full user scenarios
                  │   Scripts   │
                  └─────────────┘
                 ┌───────────────┐
                 │  Integration  │  (20%) - Multi-component
                 │     Tests     │
                 └───────────────┘
              ┌─────────────────────┐
              │    Unit Tests       │  (75%) - Individual components
              │  (with mocks)       │
              └─────────────────────┘
```

### Test Categories
1. **Unit Tests** - Isolated component testing with mocks
2. **Integration Tests** - Multi-component workflows
3. **Snapshot Tests** - Plan consistency validation
4. **Performance Tests** - Optimization impact verification
5. **E2E Demo Scripts** - Automated user journey testing

---

## 2. Unit Testing

### 2.1 LLM Provider Tests

```csharp
// Honua.Cli.AI.Tests/Services/AI/OpenAILlmProviderTests.cs
public class OpenAILlmProviderTests
{
    [Fact]
    public async Task CompleteAsync_WithValidRequest_ShouldReturnResponse()
    {
        // Arrange
        var apiKey = "test-key";
        var provider = new OpenAILlmProvider(apiKey, "gpt-4o");
        var request = new LlmRequest("What is 2+2?");

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        response.Content.Should().NotBeNullOrEmpty();
        response.PromptTokens.Should().BeGreaterThan(0);
        response.CompletionTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompleteAsync_WithTemperature_ShouldApplySetting()
    {
        var provider = new OpenAILlmProvider(apiKey, "gpt-4o");
        var request = new LlmRequest("Test", Temperature: 0.1);

        var response = await provider.CompleteAsync(request);

        response.Should().NotBeNull();
        // Lower temperature should produce more deterministic output
    }

    [Fact]
    public async Task CompleteAsync_WithFunctionCalling_ShouldReturnFunction()
    {
        var provider = new OpenAILlmProvider(apiKey, "gpt-4o");
        var functions = new[] {
            new LlmFunction("get_weather", "Get current weather", ...)
        };
        var request = new LlmRequest("What's the weather?", Functions: functions);

        var response = await provider.CompleteAsync(request);

        response.FunctionCall.Should().Be("get_weather");
    }
}
```

### 2.2 Plugin Tests (with Mocks)

```csharp
// Honua.Cli.AI.Tests/Services/Plugins/WorkspaceAnalysisPluginTests.cs
public class WorkspaceAnalysisPluginTests
{
    private readonly Mock<IMetadataSnapshotService> _mockMetadata;
    private readonly Mock<IDataSourceAnalyzer> _mockDatasource;
    private readonly WorkspaceAnalysisPlugin _plugin;

    public WorkspaceAnalysisPluginTests()
    {
        _mockMetadata = new Mock<IMetadataSnapshotService>();
        _mockDatasource = new Mock<IDataSourceAnalyzer>();
        _plugin = new WorkspaceAnalysisPlugin(_mockMetadata.Object, _mockDatasource.Object);
    }

    [Fact]
    public async Task AnalyzeWorkspace_WithMissingIndexes_ShouldDetectIssue()
    {
        // Arrange
        var metadata = CreateTestMetadata(layers: 3);
        _mockMetadata.Setup(x => x.LoadAsync(It.IsAny<string>()))
            .ReturnsAsync(metadata);

        _mockDatasource.Setup(x => x.AnalyzePerformanceAsync(metadata))
            .ReturnsAsync(new[] {
                new PerformanceIssue("Layer1 missing spatial index"),
                new PerformanceIssue("Layer2 missing spatial index")
            });

        // Act
        var result = await _plugin.AnalyzeWorkspace("/workspace");
        var analysis = JsonSerializer.Deserialize<WorkspaceAnalysis>(result);

        // Assert
        analysis.Issues.Should().HaveCount(2);
        analysis.Issues.Should().Contain(i => i.Contains("spatial index"));
        analysis.HealthScore.Should().BeLessThan(70);
    }

    [Fact]
    public async Task GetLayerStats_ShouldReturnFeatureCount()
    {
        // Arrange
        var stats = new LayerStats { FeatureCount = 12450, AvgVertices = 800 };
        _mockDatasource.Setup(x => x.GetStatsAsync("service1", "layer1"))
            .ReturnsAsync(stats);

        // Act
        var result = await _plugin.GetLayerStats("service1", "layer1");
        var layerStats = JsonSerializer.Deserialize<dynamic>(result);

        // Assert
        ((int)layerStats.featureCount).Should().Be(12450);
        ((int)layerStats.avgGeometryComplexity).Should().Be(800);
    }
}
```

### 2.3 Secrets Manager Tests

```csharp
// Honua.Cli.AI.Tests/Services/Secrets/KeychainSecretsManagerTests.cs
[SkippableFact] // Skip on platforms without keychain
public async Task SetAndGetSecret_ShouldRoundTrip()
{
    Skip.IfNot(OperatingSystem.IsMacOS() || OperatingSystem.IsWindows());

    var manager = new KeychainSecretsManager();
    var key = $"test-{Guid.NewGuid()}";
    var value = "secret-value";

    try
    {
        await manager.SetSecretAsync(key, value);
        var retrieved = await manager.GetSecretAsync(key);

        retrieved.Should().Be(value);
    }
    finally
    {
        await manager.DeleteSecretAsync(key);
    }
}
```

---

## 3. Integration Testing

### 3.1 End-to-End Assistant Workflow

```csharp
// Honua.Cli.AI.Tests/Workflows/AssistantWorkflowIntegrationTests.cs
public class AssistantWorkflowIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    [Fact]
    public async Task AssistantWorkflow_WithOptimizationRequest_ShouldGenerateAndExecutePlan()
    {
        // Arrange
        using var workspace = await _fixture.CreateTestWorkspaceAsync(
            layers: 3,
            features: 10000,
            complexity: GeometryComplexity.High
        );

        var request = new AssistantRequest(
            Prompt: "optimize my database performance",
            WorkspacePath: workspace.Path
        );

        var workflow = _fixture.CreateWorkflow();

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Plan.Should().NotBeNull();
        result.Plan.Steps.Should().Contain(s =>
            s.Skill == "PerformanceAdvisor" && s.Action.Contains("Index"));
    }

    [Fact]
    public async Task AssistantWorkflow_WithRollback_ShouldRestoreState()
    {
        // Arrange
        using var workspace = await _fixture.CreateTestWorkspaceAsync();
        var snapshotBefore = await _fixture.CreateSnapshotAsync(workspace);

        var request = new AssistantRequest(
            Prompt: "apply risky configuration",
            WorkspacePath: workspace.Path
        );

        var workflow = _fixture.CreateWorkflow(simulateFailure: true);

        // Act
        var result = await workflow.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeFalse();

        // Verify rollback occurred
        var snapshotAfter = await _fixture.CreateSnapshotAsync(workspace);
        snapshotAfter.Should().BeEquivalentTo(snapshotBefore);
    }
}
```

### 3.2 LLM Provider Resilience

```csharp
[Fact]
public async Task ResilientLlmProvider_WhenPrimaryFails_ShouldUseFallback()
{
    // Arrange
    var mockPrimary = new Mock<ILlmProvider>();
    mockPrimary.Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new HttpRequestException("API unavailable"));

    var mockFallback = new Mock<ILlmProvider>();
    mockFallback.Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new LlmResponse("Fallback response"));

    var resilient = new ResilientLlmProvider(
        mockPrimary.Object,
        mockFallback.Object,
        NullLogger<ResilientLlmProvider>.Instance
    );

    // Act
    var result = await resilient.CompleteAsync(new LlmRequest("test"));

    // Assert
    result.Content.Should().Be("Fallback response");
    mockFallback.Verify(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
        Times.Once);
}
```

---

## 4. Snapshot Testing

### 4.1 Plan Generation Consistency

```csharp
// Honua.Cli.AI.Tests/Snapshots/PlanSnapshotTests.cs
public class PlanSnapshotTests
{
    private readonly MockLlmProvider _llm;
    private readonly SemanticAssistantPlanner _planner;

    public PlanSnapshotTests()
    {
        _llm = new MockLlmProvider();
        _planner = new SemanticAssistantPlanner(_llm, ...);
    }

    [Theory]
    [InlineData("postgis-auth", "deploy postgis with authentication")]
    [InlineData("optimize-performance", "optimize database performance")]
    [InlineData("arcgis-migration", "migrate from arcgis")]
    public async Task PlanGeneration_ShouldMatchSnapshot(string snapshotName, string prompt)
    {
        // Arrange
        var request = new AssistantRequest(prompt);
        var snapshotPath = $"Snapshots/{snapshotName}.json";

        // Act
        var plan = await _planner.CreatePlanAsync(request);

        // Assert - Compare with golden snapshot
        if (File.Exists(snapshotPath))
        {
            var expected = await File.ReadAllTextAsync(snapshotPath);
            var expectedPlan = JsonSerializer.Deserialize<AssistantPlan>(expected);

            plan.Should().BeEquivalentTo(expectedPlan, options => options
                .Excluding(p => p.SessionId)
                .Excluding(p => p.Timestamp)
                .WithStrictOrdering());
        }
        else
        {
            // Create snapshot if it doesn't exist (for initial run)
            var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(snapshotPath, json);

            throw new Exception($"Snapshot created: {snapshotPath}. Re-run test to validate.");
        }
    }
}
```

### 4.2 Snapshot Files

```json
// Honua.Cli.AI.Tests/Snapshots/postgis-auth.json
{
  "steps": [
    {
      "skill": "SafetySkill",
      "action": "CreateSnapshot",
      "inputs": {
        "label": "pre-auth-setup",
        "workspace": "{workspace}"
      },
      "description": "Create safety snapshot before changes"
    },
    {
      "skill": "DatasourceSkill",
      "action": "ConnectPostgis",
      "inputs": {
        "workspace": "{workspace}",
        "dryRun": "false"
      },
      "description": "Set up PostGIS database connection"
    },
    {
      "skill": "SecuritySkill",
      "action": "ConfigureJwtAuth",
      "inputs": {
        "workspace": "{workspace}",
        "issuer": "https://honua.example.com",
        "audience": "honua-api"
      },
      "description": "Configure JWT authentication"
    },
    {
      "skill": "DeploymentSkill",
      "action": "RunDeployment",
      "inputs": {
        "mode": "apply"
      },
      "description": "Apply deployment configuration"
    }
  ]
}
```

---

## 5. Performance Testing

### 5.1 Optimization Impact Validation

```csharp
// Honua.Cli.AI.Tests/Performance/OptimizationImpactTests.cs
public class OptimizationImpactTests : IClassFixture<PerformanceTestFixture>
{
    [Fact]
    public async Task GeometrySimplification_ShouldReduceSizeBy60Percent()
    {
        // Arrange
        using var layer = await _fixture.CreateLayerAsync(
            features: 10000,
            geometryType: "Polygon",
            avgVertices: 2000
        );

        var optimizer = new GeometryOptimizationPlugin();

        var beforeSize = await MeasureLayerSize(layer);
        var beforeLatency = await MeasureQueryLatency(layer, iterations: 100);

        // Act
        await optimizer.OptimizeGeometries(layer.Metadata);

        var afterSize = await MeasureLayerSize(layer);
        var afterLatency = await MeasureQueryLatency(layer, iterations: 100);

        // Assert
        var sizeReduction = (beforeSize - afterSize) / (double)beforeSize;
        sizeReduction.Should().BeGreaterThan(0.6, "should reduce by >60%");

        var latencyImprovement = (beforeLatency - afterLatency) / beforeLatency;
        latencyImprovement.Should().BeGreaterThan(0.3, "should improve latency by >30%");
    }

    [Fact]
    public async Task SpatialIndexCreation_ShouldImproveQuerySpeedBy10x()
    {
        // Arrange
        using var layer = await _fixture.CreateLayerAsync(features: 50000);

        var before = await BenchmarkBboxQuery(layer, iterations: 50);

        // Act
        await ExecuteSql(layer, $"CREATE INDEX idx_geom ON {layer.Table} USING GIST(geometry)");

        var after = await BenchmarkBboxQuery(layer, iterations: 50);

        // Assert
        var speedup = before.P95 / after.P95;
        speedup.Should().BeGreaterThan(10, "spatial index should provide 10x speedup");
    }
}
```

### 5.2 Telemetry Performance

```csharp
[Fact]
public async Task Telemetry_ShouldNotImpactLatency()
{
    var workflow = CreateWorkflow(telemetryEnabled: false);
    var withTelemetry = CreateWorkflow(telemetryEnabled: true);

    var baseline = await BenchmarkWorkflow(workflow, iterations: 100);
    var withMetrics = await BenchmarkWorkflow(withTelemetry, iterations: 100);

    // Telemetry should add <5% overhead
    var overhead = (withMetrics.Mean - baseline.Mean) / baseline.Mean;
    overhead.Should().BeLessThan(0.05);
}
```

---

## 6. Mock Infrastructure

### 6.1 Mock LLM Provider

```csharp
// Honua.Cli.AI.Tests/Mocks/MockLlmProvider.cs
public class MockLlmProvider : ILlmProvider
{
    private readonly Dictionary<string, string> _responses = new();
    private readonly Dictionary<string, Func<LlmRequest, LlmResponse>> _handlers = new();

    public void SetupResponse(string pattern, string response)
    {
        _responses[pattern] = response;
    }

    public void SetupHandler(string pattern, Func<LlmRequest, LlmResponse> handler)
    {
        _handlers[pattern] = handler;
    }

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        // Check handlers first
        foreach (var (pattern, handler) in _handlers)
        {
            if (request.Prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(handler(request));
            }
        }

        // Then check simple responses
        foreach (var (pattern, response) in _responses)
        {
            if (request.Prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new LlmResponse(
                    Content: response,
                    PromptTokens: request.Prompt.Length / 4,
                    CompletionTokens: response.Length / 4
                ));
            }
        }

        // Default response
        return Task.FromResult(new LlmResponse($"Mock response for: {request.Prompt}"));
    }

    public bool SupportsVision => false;
    public bool SupportsFunctionCalling => true;
    public int MaxTokens => 4096;
}
```

### 6.2 Integration Test Fixture

```csharp
// Honua.Cli.AI.Tests/Fixtures/IntegrationTestFixture.cs
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly List<TemporaryDirectory> _workspaces = new();
    private IServiceProvider _services;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        // Mock LLM
        var mockLlm = new MockLlmProvider();
        mockLlm.SetupResponse("optimize", "Create indexes, simplify geometries");
        mockLlm.SetupResponse("deploy", "Set up database, configure API, deploy");
        services.AddSingleton<ILlmProvider>(mockLlm);

        // Real implementations
        services.AddSingleton<IMetadataSchemaValidator, MetadataSchemaValidator>();
        services.AddSingleton<IAssistantPlanner, SemanticAssistantPlanner>();
        services.AddSingleton<IAssistantWorkflow, AssistantWorkflow>();

        _services = services.BuildServiceProvider();
    }

    public async Task<TestWorkspace> CreateTestWorkspaceAsync(
        int layers = 1,
        int features = 1000,
        GeometryComplexity complexity = GeometryComplexity.Medium)
    {
        var workspace = new TemporaryDirectory();
        _workspaces.Add(workspace);

        // Create test metadata
        var metadata = new {
            services = new[] {
                new {
                    id = "test-service",
                    layers = Enumerable.Range(0, layers).Select(i => new {
                        id = $"layer{i}",
                        geometryType = "Polygon",
                        geometryField = "geometry",
                        fields = new[] {
                            new { name = "id", dataType = "int" },
                            new { name = "name", dataType = "string" }
                        }
                    })
                }
            }
        };

        await File.WriteAllTextAsync(
            Path.Combine(workspace.Path, "metadata.json"),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true })
        );

        return new TestWorkspace(workspace.Path, metadata);
    }

    public Task DisposeAsync()
    {
        foreach (var workspace in _workspaces)
        {
            workspace.Dispose();
        }
        return Task.CompletedTask;
    }
}
```

---

## 7. Automated Demo Script Testing

### 7.1 Demo Test Runner

```csharp
// Honua.Cli.AI.Tests/Demos/DemoScriptTests.cs
public class DemoScriptTests
{
    [Fact]
    public async Task Demo1_FirstTimeSetup_ShouldComplete()
    {
        // Simulate user inputs
        var inputs = new Queue<string>(new[] {
            "1",          // PostGIS
            "n",          // Don't have PostGIS
            "2",          // GeoPackage
            "./test.gpkg", // Path
            "y",          // Proceed
            "n"           // No next steps
        });

        var console = new TestConsole();
        console.Input.PushText(inputs);

        var workflow = CreateWorkflow(console);
        var request = new AssistantRequest("I'm new to Honua, help me get started");

        // Act
        var result = await workflow.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        console.Output.Should().Contain("Your service is now available");
        console.Output.Should().Contain("http://localhost:5000/ogc/collections");
    }

    [Fact]
    public async Task Demo2_PerformanceOptimization_ShouldShow98PercentImprovement()
    {
        // Arrange
        using var workspace = await CreateWorkspaceWithSlowQueries();
        var console = new TestConsole();
        console.Input.PushText("y\n"); // Approve plan

        var workflow = CreateWorkflow(console);
        var request = new AssistantRequest("My parcels layer is slow",
            WorkspacePath: workspace.Path);

        // Act
        var result = await workflow.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        console.Output.Should().Contain("98% improvement");
        console.Output.Should().Contain("P95 48ms");
    }
}
```

### 7.2 Demo Script Files

```bash
# tests/Honua.Cli.AI.Tests/DemoScripts/demo1-first-time-setup.sh
#!/bin/bash
set -e

echo "=== Demo 1: First-Time Setup ==="

# Simulate user running assistant for first time
honua assistant --prompt "I'm new to Honua, help me get started" <<EOF
1
n
2
./tests/fixtures/parcels.gpkg
y
n
EOF

# Verify service is running
curl -f http://localhost:5000/ogc/collections/parcels

# Verify metadata
curl -f http://localhost:5000/ogc/collections/parcels | jq '.id' | grep "parcels"

echo "✓ Demo 1 passed"
```

---

## 8. CI/CD Integration

### 8.1 GitHub Actions Workflow

```yaml
# .github/workflows/consultant-tests.yml
name: Consultant Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Run unit tests
        run: dotnet test tests/Honua.Cli.AI.Tests --filter Category=Unit

      - name: Run integration tests
        run: dotnet test tests/Honua.Cli.AI.Tests --filter Category=Integration
        env:
          MOCK_LLM: true  # Use mock LLM in CI

      - name: Run snapshot tests
        run: dotnet test tests/Honua.Cli.AI.Tests --filter Category=Snapshot

      - name: Performance benchmarks
        run: dotnet test tests/Honua.Cli.AI.Tests --filter Category=Performance

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: TestResults/
```

### 8.2 Demo Script CI

```yaml
  demo-tests:
    runs-on: ubuntu-latest
    needs: test

    steps:
      - uses: actions/checkout@v4

      - name: Build CLI
        run: dotnet build -c Release

      - name: Install honua CLI
        run: dotnet tool install --global --add-source ./nupkg honua

      - name: Run Demo 1
        run: bash tests/Honua.Cli.AI.Tests/DemoScripts/demo1-first-time-setup.sh

      - name: Run Demo 2
        run: bash tests/Honua.Cli.AI.Tests/DemoScripts/demo2-performance-optimization.sh

      - name: Verify demo outputs
        run: |
          test -f /tmp/demo1-output.log
          grep "98% improvement" /tmp/demo2-output.log
```

---

## 9. Test Coverage Goals

### Coverage Targets
- **Unit Tests**: >80% code coverage
- **Integration Tests**: All major user workflows
- **Snapshot Tests**: All plan types
- **Performance Tests**: All optimization claims validated
- **Demo Scripts**: 5 complete end-to-end scenarios

### Coverage Report

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage -reporttypes:Html

# Should show:
# - Honua.Cli.AI.Services: >85% coverage
# - Honua.Cli.AI.Plugins: >80% coverage
# - Honua.Cli.AI.Planners: >75% coverage
```

---

## 10. Manual Testing Checklist

### Pre-Release Testing
- [ ] Test with real OpenAI API
- [ ] Test with real Anthropic API
- [ ] Test Ollama local model
- [ ] Test Azure OpenAI
- [ ] Test all secrets backends (keychain, Azure KV, AWS)
- [ ] Test telemetry opt-in/opt-out
- [ ] Test all 5 demo scenarios manually
- [ ] Test rollback on failures
- [ ] Test with various workspace sizes (small, medium, large)
- [ ] Test error handling and user feedback

### Platform Testing
- [ ] macOS (Keychain, Ollama)
- [ ] Windows (Credential Manager)
- [ ] Linux (libsecret, Ollama)
- [ ] Docker containers
- [ ] WSL2

---

*Test Plan Version: 1.0*
*Last Updated: 2025-01-10*
