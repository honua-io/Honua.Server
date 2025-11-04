# HonuaIO AI Consultant - Integration Tests

Comprehensive integration tests for the Azure AI-powered deployment consultant using Docker and Testcontainers.

## Test Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Test Runner (xUnit)                      │
│  - PatternAnalysisFunctionTests                             │
│  - PatternApprovalServiceTests                              │
│  - AzureAISearchKnowledgeStoreTests (TODO)                  │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ Testcontainers
                 │
       ┌─────────┴──────────────────────────────┐
       │                                        │
       ▼                                        ▼
┌──────────────────┐                   ┌──────────────────┐
│   PostgreSQL     │                   │ Elasticsearch    │
│  (deployment     │                   │ (simulating      │
│   telemetry)     │                   │  Azure AI Search)│
└──────────────────┘                   └──────────────────┘
       │                                        │
       │                                        │
       ▼                                        ▼
┌──────────────────┐                   ┌──────────────────┐
│      Redis       │                   │  MockServer      │
│   (caching)      │                   │  (Azure OpenAI)  │
└──────────────────┘                   └──────────────────┘
```

## Test Containers

### PostgreSQL (Testcontainers.PostgreSql)
- **Purpose**: Deployment telemetry and pattern staging
- **Image**: `postgres:15-alpine`
- **Auto-initializes**: Runs `schema.sql` on startup
- **Port**: Dynamically assigned by Testcontainers

### Elasticsearch (Testcontainers.Elasticsearch)
- **Purpose**: Simulates Azure AI Search for vector/hybrid search
- **Image**: `docker.elastic.co/elasticsearch/elasticsearch:8.11.0`
- **Security**: Disabled for testing
- **Port**: Dynamically assigned

### Redis (Testcontainers.Redis)
- **Purpose**: Caching layer for deployment scenarios
- **Image**: `redis:7-alpine`
- **Port**: Dynamically assigned

### MockServer
- **Purpose**: Mocks Azure OpenAI API responses
- **Image**: `mockserver/mockserver:latest`
- **Config**: Pre-configured expectations in `mocks/openai-expectations.json`
- **Port**: Dynamically assigned

## Running Tests

### Option 1: Local Run with Testcontainers (Recommended)

Testcontainers automatically manages Docker containers during test execution:

```bash
# Run all tests
./run-tests.sh

# Run specific tests
./run-tests.sh --filter PatternAnalysis

# From IDE (Visual Studio, Rider, VS Code)
# Just run tests normally - Testcontainers handles containers
```

**Requirements**:
- Docker Desktop running
- .NET 9.0 SDK
- No manual container management needed

**How it works**:
1. Test starts → Testcontainers starts required containers
2. Schema.sql automatically applied to PostgreSQL
3. Tests run against real databases
4. Test ends → Containers automatically cleaned up

### Option 2: Docker Compose (Full Environment)

Run tests in a containerized environment (useful for CI/CD):

```bash
# Run tests in Docker
./run-tests.sh --docker

# Or manually
docker compose -f docker-compose.test.yml up --build --abort-on-container-exit test-runner
docker compose -f docker-compose.test.yml down -v
```

**Use cases**:
- CI/CD pipelines
- Consistent environment across developers
- Testing without .NET SDK installed

## Test Coverage

### PatternAnalysisFunctionTests

Tests the nightly pattern analysis Azure Function:

| Test | Description |
|------|-------------|
| `Run_WithSufficientDeployments_GeneratesPatternRecommendations` | Verifies pattern generation with 5+ deployments |
| `Run_WithHighSuccessRate_GeneratesApprovedRecommendation` | Checks quality thresholds (80% success, 80% cost accuracy) |
| `Run_WithLowSuccessRate_DoesNotGenerateRecommendation` | Ensures bad patterns are filtered out |
| `Run_WithInsufficientDeployments_DoesNotGenerateRecommendation` | Tests minimum deployment threshold (3) |
| `Run_GroupsBySimilarConfiguration` | Validates grouping by instance type |

**What it tests**:
- SQL-based pattern analysis (no LLM, pure statistics)
- Quality thresholds (success rate, cost accuracy, satisfaction)
- Grouping by cloud provider, region, instance type
- Writing to `pattern_recommendations` table with `pending_review` status

### PatternApprovalServiceTests

Tests the human approval workflow and Azure AI Search indexing:

| Test | Description |
|------|-------------|
| `GetPendingRecommendationsAsync_ReturnsPendingPatterns` | Retrieves patterns awaiting review |
| `GetPendingRecommendationsAsync_ExcludesApprovedAndRejectedPatterns` | Filters by status |
| `ApprovePatternAsync_UpdatesStatusAndIndexesInAzureAISearch` | Full approval flow with indexing |
| `ApprovePatternAsync_ThrowsIfPatternNotFound` | Error handling for invalid ID |
| `ApprovePatternAsync_ThrowsIfPatternAlreadyApproved` | Prevents double approval |
| `ApprovePatternAsync_RollsBackOnIndexingFailure` | Transaction rollback on Azure AI Search failure |
| `RejectPatternAsync_UpdatesStatusAndDoesNotIndex` | Rejection flow (no indexing) |
| `RejectPatternAsync_ThrowsIfPatternNotFound` | Error handling |

**What it tests**:
- Approval workflow (PostgreSQL status updates)
- Azure AI Search indexing (mocked)
- Rollback on indexing failure
- Telemetry tracking
- Human-in-the-loop validation

## Test Data Seeding

Tests use helper methods to seed realistic test data:

```csharp
await SeedTestDeploymentsAsync(
    deploymentCount: 5,
    cloudProvider: "aws",
    instanceType: "m6i.2xlarge",
    successRate: 0.9,  // 90% success
    avgCostAccuracy: 95.0,  // 95% cost prediction accuracy
    avgCustomerSatisfaction: 4.8  // 4.8/5.0 rating
);
```

This creates:
- 5 deployments in `deployment_history` table
- Realistic cost/performance metrics
- JSON configuration matching production data
- Timestamps within last 90 days (pattern analysis window)

## Mock Azure OpenAI API

MockServer is pre-configured with realistic responses:

### Chat Completion Response
```json
{
  "choices": [{
    "message": {
      "content": "Based on your requirements (500GB data, 1000 users), I recommend:\n- 2x m6i.2xlarge EC2\n- RDS PostgreSQL db.r6g.xlarge\n- ElastiCache Redis cache.r6g.large\n\nEstimated cost: $2,847/month"
    }
  }],
  "usage": {
    "prompt_tokens": 150,
    "completion_tokens": 80,
    "total_tokens": 230
  }
}
```

### Embedding Response
```json
{
  "data": [{
    "embedding": [0.023, -0.041, ..., 0.015]  // 3072 dimensions
  }],
  "usage": {
    "prompt_tokens": 8,
    "total_tokens": 8
  }
}
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Integration Tests

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

    - name: Run integration tests
      run: |
        cd tests/Honua.Cli.AI.Tests
        chmod +x run-tests.sh
        ./run-tests.sh

    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: test-results
        path: tests/Honua.Cli.AI.Tests/TestResults/*.trx
```

### Azure DevOps Example

```yaml
trigger:
- main
- dev

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '9.0.x'

- script: |
    cd tests/Honua.Cli.AI.Tests
    chmod +x run-tests.sh
    ./run-tests.sh
  displayName: 'Run integration tests'

- task: PublishTestResults@2
  condition: always()
  inputs:
    testResultsFormat: 'VSTest'
    testResultsFiles: '**/TestResults/*.trx'
```

## Debugging Tests

### Visual Studio / Rider

1. Set breakpoint in test
2. Right-click → Debug Test
3. Containers start automatically
4. Debugger attaches

### VS Code

1. Install C# Dev Kit extension
2. Set breakpoint
3. Run → Debug Test
4. Testcontainers handles containers

### Inspect Container Logs

```bash
# While tests are running, in another terminal:
docker ps  # Find container ID
docker logs <container-id>

# PostgreSQL logs
docker logs <postgres-container-id>

# Elasticsearch logs
docker logs <elasticsearch-container-id>
```

### Connect to Test Database

```bash
# Get connection string from test output
# Connect with psql
psql "host=localhost port=<dynamic-port> dbname=honua_test user=honua_test password=test_password_123"

# Query test data
SELECT * FROM deployment_history;
SELECT * FROM pattern_recommendations;
```

## Test Configuration

Tests use `appsettings.test.json` (overridden by Testcontainers):

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "set-by-testcontainers"
  },
  "AzureOpenAI": {
    "Endpoint": "set-by-testcontainers",
    "ApiKey": "test-key"
  },
  "DeploymentConsultant": {
    "MinDeploymentsForPattern": 3,
    "EnableTelemetry": true
  }
}
```

Connection strings are dynamically set by Testcontainers based on assigned ports.

## Performance

### Typical Test Run Times

| Test Suite | Tests | Duration |
|------------|-------|----------|
| PatternAnalysisFunctionTests | 5 | ~8s |
| PatternApprovalServiceTests | 8 | ~6s |
| **Total** | **13** | **~15s** |

*Includes container startup time (first run: ~30s, subsequent runs: ~15s due to caching)*

### Optimization Tips

1. **Reuse containers**: Testcontainers reuses containers for same configuration
2. **Parallel execution**: xUnit runs test classes in parallel
3. **Shared fixture**: `TestContainerFixture` shared across collection
4. **Fast images**: Using alpine-based images (PostgreSQL, Redis)

## Troubleshooting

### "Docker daemon not running"
```bash
# Start Docker Desktop
open -a Docker  # macOS
# Windows: Start Docker Desktop from Start menu
```

### "Port already in use"
Testcontainers uses dynamic ports, so this shouldn't happen. If it does:
```bash
# Stop all containers
docker stop $(docker ps -aq)
docker rm $(docker ps -aq)
```

### "Schema.sql not found"
Ensure you're running tests from solution root:
```bash
cd /path/to/HonuaIO
./tests/Honua.Cli.AI.Tests/run-tests.sh
```

### "Tests hang during startup"
Check Docker resource limits:
- Docker Desktop → Settings → Resources
- Increase Memory to 4GB+
- Increase CPUs to 2+

### "Elasticsearch fails to start"
```bash
# Increase vm.max_map_count (Linux/WSL)
sudo sysctl -w vm.max_map_count=262144
```

## Future Test Coverage

Tests to add:

- [ ] `AzureAISearchKnowledgeStoreTests` - Vector search integration
- [ ] `AzureOpenAILlmProviderTests` - LLM completion and embedding
- [ ] `DeploymentTelemetryTests` - End-to-end deployment tracking
- [ ] `KnownIssueIndexingTests` - Known issue workflow
- [ ] `CostEstimationTests` - Cost prediction accuracy
- [ ] `PatternVersioningTests` - Pattern update workflow
- [ ] `MultiCloudPatternTests` - AWS, Azure, GCP patterns
- [ ] Load tests with 1000+ deployments

## Contributing

When adding new tests:

1. Use `[Collection("IntegrationTests")]` attribute
2. Inject `TestContainerFixture` in constructor
3. Seed test data with helper methods
4. Clean up after test (Testcontainers handles containers)
5. Use FluentAssertions for readable assertions
6. Add test to this README

Example:
```csharp
[Collection("IntegrationTests")]
public sealed class MyNewTests
{
    private readonly TestContainerFixture _fixture;

    public MyNewTests(TestContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest_DoesAThing()
    {
        // Arrange
        using var connection = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await connection.OpenAsync();

        // Act
        var result = await DoSomething();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }
}
```

## Resources

- [Testcontainers](https://dotnet.testcontainers.org/)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [MockServer](https://www.mock-server.com/)
- [Docker Compose](https://docs.docker.com/compose/)
