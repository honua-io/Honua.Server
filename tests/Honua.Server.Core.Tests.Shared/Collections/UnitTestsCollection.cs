using Xunit;

namespace Honua.Server.Core.Tests.Collections;

/// <summary>
/// Collection for pure unit tests that have no external dependencies.
/// These tests can run fully in parallel with maximum concurrency.
/// </summary>
[CollectionDefinition("UnitTests")]
public class UnitTestsCollection
{
    // This class is never instantiated. It exists only to define the collection.
}
