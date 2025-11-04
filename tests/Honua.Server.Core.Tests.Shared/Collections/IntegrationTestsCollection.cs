using Xunit;

namespace Honua.Server.Core.Tests.Collections;

/// <summary>
/// Collection for integration tests that test multiple components together.
/// These tests may have complex setup/teardown and run with controlled parallelism.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestsCollection : ICollectionFixture<IntegrationTestsFixture>
{
}

/// <summary>
/// Shared fixture for integration tests.
/// </summary>
public class IntegrationTestsFixture : IDisposable
{
    public IntegrationTestsFixture()
    {
        // Initialize shared integration test resources
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
