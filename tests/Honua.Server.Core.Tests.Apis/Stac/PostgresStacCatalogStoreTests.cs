using System.Threading.Tasks;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Honua.Server.Core.Tests.Shared;
using Npgsql;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// PostgreSQL-specific tests for StacCatalogStore.
/// Inherits all common tests from StacCatalogStoreTestsBase.
/// </summary>
[Collection("SharedPostgres")]
[Trait("Category", "Integration")]
public sealed class PostgresStacCatalogStoreTests : StacCatalogStoreTestsBase, IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public PostgresStacCatalogStoreTests(SharedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    protected override IStacCatalogStore CatalogStore => new PostgresStacCatalogStore(_fixture.ConnectionString);

    protected override string ConnectionString => _fixture.ConnectionString;

    protected override IStacCatalogStore CreateStore(string connectionString)
    {
        return new PostgresStacCatalogStore(connectionString);
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            throw new SkipException("PostgreSQL test container is not available (Docker required).");
        }
        (_connection, _transaction) = await _fixture.CreateTransactionScopeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}
