using Xunit;

namespace Honua.Cli.AI.Tests.Collections;

/// <summary>
/// Collection for process framework tests.
/// These tests validate the multi-step process orchestration framework.
/// </summary>
[CollectionDefinition("ProcessFramework")]
public class ProcessFrameworkTestsCollection : ICollectionFixture<ProcessFrameworkTestsFixture>
{
}

/// <summary>
/// Shared fixture for process framework tests.
/// </summary>
public class ProcessFrameworkTestsFixture : IDisposable
{
    public ProcessFrameworkTestsFixture()
    {
        // Initialize process framework test resources
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
