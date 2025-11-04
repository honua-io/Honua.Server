using Xunit;

namespace Honua.Server.Host.Tests.Collections;

/// <summary>
/// Collection for host-level tests including health checks and middleware.
/// </summary>
[CollectionDefinition("HostTests")]
public class HostTestsCollection : ICollectionFixture<HostTestsFixture>
{
}

/// <summary>
/// Shared fixture for host tests.
/// </summary>
public class HostTestsFixture : IDisposable
{
    public HostTestsFixture()
    {
        // Initialize host test resources
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
