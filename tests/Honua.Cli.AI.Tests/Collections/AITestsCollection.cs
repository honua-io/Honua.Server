using Xunit;

namespace Honua.Cli.AI.Tests.Collections;

/// <summary>
/// Collection for AI agent tests that share a mock LLM service.
/// These tests use mock services and can run in parallel.
/// </summary>
[CollectionDefinition("AITests")]
public class AITestsCollection : ICollectionFixture<AITestsFixture>
{
}

/// <summary>
/// Shared fixture for AI agent tests.
/// Provides mock LLM services and shared test infrastructure.
/// </summary>
public class AITestsFixture : IDisposable
{
    public AITestsFixture()
    {
        // Initialize mock AI services
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
