using Xunit;

namespace Honua.Server.Core.Tests.Collections;

/// <summary>
/// Collection for API endpoint integration tests.
/// These tests create test servers and can run in parallel with some coordination.
/// </summary>
[CollectionDefinition("EndpointTests")]
public class EndpointTestsCollection : ICollectionFixture<EndpointTestsFixture>
{
}

/// <summary>
/// Shared fixture for endpoint tests.
/// Manages test server resources and cleanup.
/// </summary>
public class EndpointTestsFixture : IDisposable
{
    public EndpointTestsFixture()
    {
        // Initialize shared resources for endpoint tests
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
