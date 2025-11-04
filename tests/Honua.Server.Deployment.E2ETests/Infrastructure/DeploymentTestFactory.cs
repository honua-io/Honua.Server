using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Honua.Server.Deployment.E2ETests.Infrastructure;

/// <summary>
/// Web application factory for end-to-end deployment testing with Testcontainers.
/// </summary>
public sealed class DeploymentTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    private readonly string _metadataPath;
    private readonly string _rootPath;
    private string? _authStorePath;
    private bool _quickStartMode;

    public string PostgresConnectionString => _postgresContainer.GetConnectionString();
    public string RedisConnectionString => _redisContainer.GetConnectionString();
    public string MetadataPath => _metadataPath;

    public DeploymentTestFactory()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:17-3.5")
            .WithDatabase("honua_test")
            .WithUsername("honua")
            .WithPassword("test_password")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        _rootPath = Path.Combine(Path.GetTempPath(), $"honua-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
        _metadataPath = Path.Combine(_rootPath, "metadata.json");
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();

        // Initialize STAC tables in the test database
        await InitializeStacTablesAsync();
    }

    private async Task InitializeStacTablesAsync()
    {
        await using var connection = new Npgsql.NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();

        // Create STAC tables
        var createCollectionsTable = @"
            CREATE TABLE IF NOT EXISTS stac_collections (
                id TEXT PRIMARY KEY,
                title TEXT,
                description TEXT,
                license TEXT,
                version TEXT,
                keywords_json TEXT NOT NULL,
                extent_json TEXT NOT NULL,
                properties_json TEXT,
                links_json TEXT NOT NULL,
                extensions_json TEXT NOT NULL,
                conforms_to TEXT,
                data_source_id TEXT,
                service_id TEXT,
                layer_id TEXT,
                etag TEXT,
                created_at TIMESTAMPTZ NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL
            );";

        var createItemsTable = @"
            CREATE TABLE IF NOT EXISTS stac_items (
                collection_id TEXT NOT NULL,
                id TEXT NOT NULL,
                title TEXT,
                description TEXT,
                properties_json TEXT,
                assets_json TEXT NOT NULL,
                links_json TEXT NOT NULL,
                extensions_json TEXT NOT NULL,
                bbox_json TEXT,
                geometry_json TEXT,
                datetime TIMESTAMPTZ,
                start_datetime TIMESTAMPTZ,
                end_datetime TIMESTAMPTZ,
                raster_dataset_id TEXT,
                etag TEXT,
                created_at TIMESTAMPTZ NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL,
                PRIMARY KEY (collection_id, id),
                FOREIGN KEY (collection_id) REFERENCES stac_collections(id) ON DELETE CASCADE
            );";

        var createIndexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_stac_items_collection ON stac_items(collection_id);",
            "CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_start ON stac_items(collection_id, COALESCE(start_datetime, datetime));",
            "CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_end ON stac_items(collection_id, COALESCE(end_datetime, datetime));",
            "CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_range ON stac_items(collection_id, COALESCE(start_datetime, datetime), COALESCE(end_datetime, datetime), id);",
            "CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_point ON stac_items(collection_id, datetime) WHERE datetime IS NOT NULL AND start_datetime IS NULL AND end_datetime IS NULL;",
            "CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_range ON stac_items(collection_id, start_datetime, end_datetime) WHERE start_datetime IS NOT NULL AND end_datetime IS NOT NULL;"
        };

        await using var cmd = connection.CreateCommand();

        // Create collections table
        cmd.CommandText = createCollectionsTable;
        await cmd.ExecuteNonQueryAsync();

        // Create items table
        cmd.CommandText = createItemsTable;
        await cmd.ExecuteNonQueryAsync();

        // Create indexes
        foreach (var indexSql in createIndexes)
        {
            cmd.CommandText = indexSql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();

        if (!string.IsNullOrWhiteSpace(_rootPath) && Directory.Exists(_rootPath))
        {
            try
            {
                Directory.Delete(_rootPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }

        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Test to skip configuration validation in Program.cs
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Don't clear sources - we want to keep base appsettings
            // Instead, add our test configuration with high priority (added last = highest priority)

            var configuration = new Dictionary<string, string?>
            {
                // Core metadata configuration
                ["honua:metadata:provider"] = "json",
                ["honua:metadata:path"] = _metadataPath,

                // Data store configuration
                ["honua:dataStores:default:provider"] = "postgres",
                ["honua:dataStores:default:connectionString"] = PostgresConnectionString,

                // Redis configuration (required for rate limiting)
                ["ConnectionStrings:Redis"] = RedisConnectionString,
                ["honua:caching:redis:enabled"] = "true",
                ["honua:caching:redis:connectionString"] = RedisConnectionString,

                // STAC configuration
                ["honua:services:stac:enabled"] = "true",
                ["honua:services:stac:provider"] = "postgres",
                ["honua:services:stac:connectionString"] = PostgresConnectionString,

                // CORS configuration
                ["honua:cors:allowAnyOrigin"] = "false",

                // AllowedHosts (not critical in test environment)
                ["AllowedHosts"] = "localhost;127.0.0.1"
            };

            if (_quickStartMode)
            {
                configuration["honua:authentication:mode"] = "QuickStart";
                configuration["honua:authentication:enforce"] = "false";
                configuration["honua:authentication:allowQuickStart"] = "true";
                configuration["honua:authentication:quickStart:enabled"] = "true";
            }
            else
            {
                _authStorePath = Path.Combine(_rootPath, "auth.db");
                configuration["honua:authentication:mode"] = "Local";
                configuration["honua:authentication:enforce"] = "true";
                configuration["honua:authentication:quickStart:enabled"] = "false";
                configuration["honua:authentication:local:storePath"] = _authStorePath;
                configuration["honua:authentication:bootstrap:adminUsername"] = "admin";
                configuration["honua:authentication:bootstrap:adminPassword"] = "TestAdmin123!";
            }

            config.AddInMemoryCollection(configuration);
        });

        builder.ConfigureTestServices(services =>
        {
            // Additional test-specific service configuration can go here
        });
    }

    /// <summary>
    /// Configure the factory to use QuickStart authentication mode.
    /// </summary>
    public DeploymentTestFactory UseQuickStartAuth()
    {
        _quickStartMode = true;
        return this;
    }

    /// <summary>
    /// Configure the factory to use Local authentication mode.
    /// </summary>
    public DeploymentTestFactory UseLocalAuth()
    {
        _quickStartMode = false;
        return this;
    }

    /// <summary>
    /// Write metadata to the configured metadata path.
    /// </summary>
    public void WriteMetadata(string metadataJson)
    {
        File.WriteAllText(_metadataPath, metadataJson);
    }

    /// <summary>
    /// Get an authenticated HTTP client for testing.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();

        if (!_quickStartMode)
        {
            // Authenticate with Local mode
            var loginResponse = await client.PostAsJsonAsync("/auth/login", new
            {
                username = "admin",
                password = "TestAdmin123!"
            });

            loginResponse.EnsureSuccessStatusCode();
            var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse!.Token);
        }

        return client;
    }

    private record TokenResponse(string Token, string TokenType, int ExpiresIn);
}
