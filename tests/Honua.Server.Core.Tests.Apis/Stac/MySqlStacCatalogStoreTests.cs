using System.Threading.Tasks;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Testcontainers.MySql;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// MySQL-specific tests for StacCatalogStore.
/// Inherits all common tests from StacCatalogStoreTestsBase.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class MySqlStacCatalogStoreTests : StacCatalogStoreTestsBase, IAsyncLifetime
{
    private MySqlContainer? _container;
    private string? _connectionString;

    protected override IStacCatalogStore CatalogStore => new MySqlStacCatalogStore(_connectionString!);

    protected override string ConnectionString => _connectionString!;

    protected override IStacCatalogStore CreateStore(string connectionString)
    {
        return new MySqlStacCatalogStore(connectionString);
    }

    public Task InitializeAsync()
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .Build();

        return InitializeContainerAsync();

        async Task InitializeContainerAsync()
        {
            await _container!.StartAsync();
            _connectionString = _container.GetConnectionString();
        }
    }

    public Task DisposeAsync()
    {
        if (_container is null)
        {
            return Task.CompletedTask;
        }

        return DisposeContainerAsync();

        async Task DisposeContainerAsync()
        {
            await _container.DisposeAsync();
        }
    }
}
