using Honua.Server.Core.Tests.Shared;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Collections;

/// <summary>
/// Collection definition for storage container tests in the Raster test project.
/// All tests using this collection will share the same container instances.
/// This definition must be in the same assembly as the tests that use it.
/// </summary>
[CollectionDefinition("StorageContainers")]
public class StorageContainerCollection : ICollectionFixture<StorageContainerFixture>
{
}
