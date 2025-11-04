using Xunit;

namespace Honua.Server.Core.Tests.Collections;

/// <summary>
/// Collection for tests that use database connections (SQLite, PostgreSQL, SQL Server, MySQL).
/// Tests in this collection share connection pool resources to prevent connection exhaustion.
/// They can run in parallel but with controlled concurrency.
/// </summary>
[CollectionDefinition("DatabaseTests")]
public class DatabaseTestsCollection : ICollectionFixture<DatabaseTestsFixture>
{
}

/// <summary>
/// Shared fixture for database tests.
/// Manages connection pooling and cleanup.
/// </summary>
public class DatabaseTestsFixture : IDisposable
{
    public DatabaseTestsFixture()
    {
        // Initialize shared database resources if needed
        // For example, set connection pool limits
    }

    public void Dispose()
    {
        // Cleanup shared resources
        GC.SuppressFinalize(this);
    }
}
