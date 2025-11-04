using Xunit;

namespace Honua.Cli.Tests.Collections;

/// <summary>
/// Collection for CLI command tests.
/// These tests validate command-line interface functionality.
/// </summary>
[CollectionDefinition("CliTests")]
public class CliTestsCollection : ICollectionFixture<CliTestsFixture>
{
}

/// <summary>
/// Shared fixture for CLI tests.
/// </summary>
public class CliTestsFixture : IDisposable
{
    public CliTestsFixture()
    {
        // Initialize CLI test resources
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
