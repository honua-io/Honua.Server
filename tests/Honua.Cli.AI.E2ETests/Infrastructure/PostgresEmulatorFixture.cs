using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

public sealed class PostgresEmulatorFixture : IAsyncLifetime
{
    private const string DefaultDatabase = "honua";
    private const string DefaultUsername = "honua";
    private const string DefaultPassword = "honua";

    private PostgreSqlContainer? _container;

    public bool IsDockerAvailable { get; private set; }

    public string ConnectionString => _container?.GetConnectionString() ??
        throw new InvalidOperationException("PostgreSQL container not initialised");

    public string Endpoint => _container is null
        ? throw new InvalidOperationException("PostgreSQL container not initialised")
        : $"{_container.Hostname}:{_container.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort)}";

    public string DatabaseName => _container is null
        ? throw new InvalidOperationException("PostgreSQL container not initialised")
        : DefaultDatabase;

    public string Username => _container is null
        ? throw new InvalidOperationException("PostgreSQL container not initialised")
        : DefaultUsername;

    public string Password => _container is null
        ? throw new InvalidOperationException("PostgreSQL container not initialised")
        : DefaultPassword;

    public async Task InitializeAsync()
    {
        IsDockerAvailable = await DeploymentEmulatorFixture.IsDockerRunningAsync();
        if (!IsDockerAvailable)
        {
            Console.WriteLine("Docker is unavailable. Postgres emulator tests will be skipped.");
            return;
        }

        _container = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .WithDatabase(DefaultDatabase)
            .WithUsername(DefaultUsername)
            .WithPassword(DefaultPassword)
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS postgis;", connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition("PostgresEmulator")]
public sealed class PostgresEmulatorCollection : ICollectionFixture<PostgresEmulatorFixture>
{
}
