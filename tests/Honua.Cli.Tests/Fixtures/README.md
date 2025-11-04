# Test Fixtures for Honua.Cli.Tests

This directory contains shared test fixtures for integration testing with cloud emulators.

## Available Fixtures

### OllamaTestFixture

**Purpose:** Provides a local LLM (Large Language Model) instance for testing AI/ML workflows without API costs.

**Replaces:** Conditional `USE_REAL_LLM=true` tests in `RealLlmConsultantIntegrationTests.cs`

**Usage:**
```csharp
[Collection("OllamaContainer")]
public class MyLlmTests
{
    private readonly OllamaTestFixture _fixture;

    public MyLlmTests(OllamaTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TestLlmCompletion()
    {
        if (!_fixture.OllamaAvailable || !_fixture.ModelPulled)
        {
            return; // Skip if Ollama not available
        }

        var response = await _fixture.GenerateChatCompletionAsync(
            systemPrompt: "You are a helpful assistant.",
            userPrompt: "What is the capital of France?",
            CancellationToken.None);

        response.Should().NotBeNullOrEmpty();
    }
}
```

**Benefits:**
- Zero API costs (saves ~$0.50-2.00 per test run)
- No API keys required
- Works offline
- Deterministic results (same model version)
- Faster iteration (no network latency)

**Requirements:**
- Docker running
- 4GB+ RAM allocated to Docker (8GB recommended)
- 2+ CPU cores recommended
- First run downloads model (~2.3GB for phi3:mini)

**Model Pull Time:**
- First run: 5-10 minutes (downloads model)
- Subsequent runs: ~3 seconds (model cached)

## Integration Tests Using Fixtures

### LLM Tests

**File:** `Consultant/OllamaConsultantIntegrationTests.cs`

**What it tests:**
- Deployment guidance generation
- Security recommendations
- Data migration advice
- Performance optimization tips
- Simple completions

**Example test:**
```csharp
[Fact]
public async Task Ollama_ShouldGenerateDeploymentGuidance()
{
    if (!_fixture.OllamaAvailable || !_fixture.ModelPulled)
    {
        return;
    }

    var systemPrompt = "You are a geospatial infrastructure consultant.";
    var userPrompt = "I need to deploy a GIS server to AWS. What are the key steps?";

    var response = await _fixture.GenerateChatCompletionAsync(
        systemPrompt,
        userPrompt,
        CancellationToken.None);

    response.Should().NotBeNullOrEmpty();
    response.Should().ContainAny(new[] { "AWS", "deploy", "server", "step" });
}
```

## Running Tests

### Run all Ollama tests:
```bash
dotnet test --filter "Collection=OllamaContainer"
```

### Run specific test:
```bash
dotnet test --filter "FullyQualifiedName~OllamaConsultantIntegrationTests"
```

### Skip if Docker unavailable:
Tests automatically skip if Docker is not running or model pull fails.

## API Cost Comparison

### Before (Real APIs):
- OpenAI: $0.002-0.02 per test
- Anthropic: $0.001-0.015 per test
- **Total per test run:** $0.20-2.00

### After (Ollama):
- **Cost per test:** $0
- **Annual savings:** ~$18,000-27,000 (assuming 50 test runs/day)

## Troubleshooting

### Model pull timeout or failure

**Problem:** Ollama model download times out

**Solution:**
```bash
# Pre-pull model manually
docker run -v ollama:/root/.ollama -p 11434:11434 ollama/ollama
# In another terminal:
docker exec -it <container_id> ollama pull phi3:mini

# Or increase Docker resources (Settings > Resources)
# - RAM: 4GB minimum, 8GB recommended
# - CPU: 2+ cores
```

### Use smaller model

Edit `OllamaTestFixture.cs`:
```csharp
public string ModelName { get; private set; } = "tinyllama"; // Instead of "phi3:mini"
```

**Model sizes:**
- `tinyllama`: 637MB (fastest, lower quality)
- `llama3.2:1b`: 1.3GB (good quality, reasonable size)
- `phi3:mini`: 2.3GB (best quality for tests)

### Container won't start

1. Ensure Docker is running: `docker ps`
2. Check Docker resources: Settings > Resources
3. Verify port availability: `lsof -i:11434`

### Slow completions

- Ensure sufficient CPU cores allocated to Docker (2+ recommended)
- Use smaller model (tinyllama)
- Reduce `num_predict` in completion requests

## Performance

**First run:**
- Container start: ~10 seconds
- Model pull: 5-10 minutes (one-time)
- Total: 5-10 minutes

**Subsequent runs:**
- Container start: ~3 seconds
- Model already cached: instant

**Memory usage:**
- Container: ~500MB
- Model in memory: ~2-4GB

**Completion speed:**
- phi3:mini: ~5-10 tokens/second
- tinyllama: ~15-20 tokens/second

## Real API Tests

The original `RealLlmConsultantIntegrationTests.cs` still exists with API cost warnings:

```csharp
/// WARNING: These tests make real API calls and will incur costs!
/// - OpenAI API costs: ~$0.002-0.02 per test (depending on model)
/// - Anthropic API costs: ~$0.001-0.015 per test
///
/// For cost-free testing, use OllamaConsultantIntegrationTests instead.
```

These should only be used for final validation before release.

## See Also

- [TESTCONTAINERS_GUIDE.md](../../TESTCONTAINERS_GUIDE.md) - Complete guide to all emulators
- [Ollama Documentation](https://github.com/ollama/ollama)
- [Ollama Models](https://ollama.com/library)
- [Testcontainers .NET](https://dotnet.testcontainers.org/)
