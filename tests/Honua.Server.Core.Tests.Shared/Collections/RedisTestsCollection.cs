using Xunit;

namespace Honua.Server.Core.Tests.Collections;

/// <summary>
/// Collection for tests that use Redis.
/// Tests share a Redis instance but run sequentially to avoid conflicts.
/// </summary>
[CollectionDefinition("Redis", DisableParallelization = false)]
public class RedisTestsCollection : ICollectionFixture<RedisTestsFixture>
{
}

/// <summary>
/// Shared fixture for Redis tests.
/// Can be extended to manage a shared Redis container if needed.
/// </summary>
public class RedisTestsFixture : IDisposable
{
    public RedisTestsFixture()
    {
        // Redis fixture can manage shared Redis container
        // Currently individual tests manage their own containers
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
