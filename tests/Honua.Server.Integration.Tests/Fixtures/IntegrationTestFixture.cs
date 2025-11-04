using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Honua.Server.Integration.Tests.Fixtures;

/// <summary>
/// Shared test fixture for integration tests that provides a PostgreSQL database in Docker.
/// Implements xUnit's IAsyncLifetime for setup and teardown.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private Respawner? _respawner;

    /// <summary>
    /// Connection string to the test PostgreSQL database.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Logger factory for test components.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; private set; } = null!;

    /// <summary>
    /// Initializes the test fixture by starting PostgreSQL container and running migrations.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Setup logging
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = LoggerFactory.CreateLogger<IntegrationTestFixture>();
        logger.LogInformation("Starting PostgreSQL container for integration tests...");

        // Start PostgreSQL container
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4-alpine")
            .WithDatabase("honua_test")
            .WithUsername("postgres")
            .WithPassword("testpass")
            .WithPortBinding(5432, true)
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();

        var host = _postgresContainer.Hostname;
        var port = _postgresContainer.GetMappedPublicPort(5432);
        ConnectionString = $"Host={host};Port={port};Database=honua_test;Username=postgres;Password=testpass";

        logger.LogInformation("PostgreSQL container started on port {Port}", port);

        // Run migrations
        await RunMigrationsAsync();
        logger.LogInformation("Database migrations completed");

        // Initialize Respawner for database cleanup between tests
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new Respawn.Graph.Table[] { "__EFMigrationsHistory" }
        });

        logger.LogInformation("Integration test fixture initialized successfully");
    }

    /// <summary>
    /// Runs database migrations to set up the schema.
    /// </summary>
    private async Task RunMigrationsAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Enable PostGIS extension
        await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS postgis;");
        await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

        // Create build queue table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS build_queue (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                customer_id VARCHAR(100) NOT NULL,
                manifest_id VARCHAR(100) NOT NULL,
                manifest_hash VARCHAR(64) NOT NULL,
                status VARCHAR(50) NOT NULL DEFAULT 'queued',
                priority INTEGER NOT NULL DEFAULT 100,
                target_id VARCHAR(100),
                output_path TEXT,
                error_message TEXT,
                retry_count INTEGER NOT NULL DEFAULT 0,
                max_retries INTEGER NOT NULL DEFAULT 3,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                started_at TIMESTAMPTZ,
                completed_at TIMESTAMPTZ,
                timeout_at TIMESTAMPTZ,
                CONSTRAINT chk_status CHECK (status IN ('queued', 'running', 'success', 'failed', 'timeout', 'cancelled'))
            );

            CREATE INDEX IF NOT EXISTS idx_build_queue_status ON build_queue(status);
            CREATE INDEX IF NOT EXISTS idx_build_queue_priority ON build_queue(priority DESC);
            CREATE INDEX IF NOT EXISTS idx_build_queue_customer ON build_queue(customer_id);
            CREATE INDEX IF NOT EXISTS idx_build_queue_manifest_hash ON build_queue(manifest_hash);
        ");

        // Create build cache registry table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS build_cache_registry (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                manifest_hash VARCHAR(64) NOT NULL UNIQUE,
                manifest_id VARCHAR(100) NOT NULL,
                target_id VARCHAR(100) NOT NULL,
                image_reference TEXT NOT NULL,
                digest VARCHAR(128),
                architecture VARCHAR(50),
                binary_size BIGINT,
                cache_hit_count INTEGER NOT NULL DEFAULT 0,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                last_accessed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_cache_manifest_hash ON build_cache_registry(manifest_hash);
            CREATE INDEX IF NOT EXISTS idx_cache_target ON build_cache_registry(target_id);
        ");

        // Create customer licenses table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS customer_licenses (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                customer_id VARCHAR(100) NOT NULL UNIQUE,
                license_tier VARCHAR(50) NOT NULL,
                status VARCHAR(50) NOT NULL DEFAULT 'active',
                issued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                expires_at TIMESTAMPTZ,
                max_concurrent_builds INTEGER NOT NULL DEFAULT 1,
                allowed_registries TEXT[] NOT NULL DEFAULT ARRAY['GitHubContainerRegistry'],
                metadata JSONB,
                CONSTRAINT chk_license_tier CHECK (license_tier IN ('Standard', 'Professional', 'Enterprise')),
                CONSTRAINT chk_license_status CHECK (status IN ('active', 'suspended', 'expired', 'revoked'))
            );

            CREATE INDEX IF NOT EXISTS idx_customer_licenses_customer ON customer_licenses(customer_id);
            CREATE INDEX IF NOT EXISTS idx_customer_licenses_status ON customer_licenses(status);
        ");

        // Create registry credentials table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS registry_credentials (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                customer_id VARCHAR(100) NOT NULL,
                registry_type VARCHAR(50) NOT NULL,
                namespace VARCHAR(255) NOT NULL,
                registry_url VARCHAR(255) NOT NULL,
                username VARCHAR(255),
                password_encrypted TEXT,
                access_token_encrypted TEXT,
                expires_at TIMESTAMPTZ,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                revoked_at TIMESTAMPTZ,
                metadata JSONB,
                CONSTRAINT chk_registry_type CHECK (registry_type IN ('GitHubContainerRegistry', 'AwsEcr', 'AzureAcr', 'GcpArtifactRegistry'))
            );

            CREATE INDEX IF NOT EXISTS idx_registry_creds_customer ON registry_credentials(customer_id);
            CREATE INDEX IF NOT EXISTS idx_registry_creds_type ON registry_credentials(registry_type);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_registry_creds_unique ON registry_credentials(customer_id, registry_type) WHERE revoked_at IS NULL;
        ");

        // Create build manifests table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS build_manifests (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                manifest_id VARCHAR(100) NOT NULL,
                customer_id VARCHAR(100) NOT NULL,
                manifest_hash VARCHAR(64) NOT NULL,
                manifest_json JSONB NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                version VARCHAR(50) NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_manifests_customer ON build_manifests(customer_id);
            CREATE INDEX IF NOT EXISTS idx_manifests_hash ON build_manifests(manifest_hash);
        ");

        // Create build metrics table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS build_metrics (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                build_id UUID NOT NULL REFERENCES build_queue(id) ON DELETE CASCADE,
                metric_name VARCHAR(100) NOT NULL,
                metric_value DECIMAL,
                unit VARCHAR(50),
                recorded_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_build_metrics_build ON build_metrics(build_id);
            CREATE INDEX IF NOT EXISTS idx_build_metrics_name ON build_metrics(metric_name);
        ");
    }

    /// <summary>
    /// Resets the database to a clean state for the next test.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner == null)
        {
            throw new InvalidOperationException("Respawner not initialized");
        }

        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(ConnectionString);
    }

    /// <summary>
    /// Seeds test data into the database.
    /// </summary>
    public async Task SeedTestDataAsync(Action<TestDataBuilder> configure)
    {
        var builder = new TestDataBuilder(ConnectionString);
        configure(builder);
        await builder.SeedAsync();
    }

    /// <summary>
    /// Disposes the test fixture by stopping the PostgreSQL container.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync();
            await _postgresContainer.DisposeAsync();
        }

        LoggerFactory?.Dispose();
    }
}

/// <summary>
/// Helper class for building test data.
/// </summary>
public class TestDataBuilder
{
    private readonly string _connectionString;
    private readonly List<Func<NpgsqlConnection, Task>> _seedActions = new();

    public TestDataBuilder(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Adds a customer license to the test data.
    /// </summary>
    public TestDataBuilder WithCustomerLicense(
        string customerId,
        string licenseTier = "Professional",
        string status = "active",
        DateTimeOffset? expiresAt = null,
        int maxConcurrentBuilds = 2,
        string[]? allowedRegistries = null)
    {
        _seedActions.Add(async connection =>
        {
            await connection.ExecuteAsync(@"
                INSERT INTO customer_licenses (customer_id, license_tier, status, expires_at, max_concurrent_builds, allowed_registries)
                VALUES (@CustomerId, @LicenseTier, @Status, @ExpiresAt, @MaxConcurrentBuilds, @AllowedRegistries)
                ON CONFLICT (customer_id) DO UPDATE
                SET license_tier = EXCLUDED.license_tier,
                    status = EXCLUDED.status,
                    expires_at = EXCLUDED.expires_at,
                    max_concurrent_builds = EXCLUDED.max_concurrent_builds,
                    allowed_registries = EXCLUDED.allowed_registries
            ", new
            {
                CustomerId = customerId,
                LicenseTier = licenseTier,
                Status = status,
                ExpiresAt = expiresAt,
                MaxConcurrentBuilds = maxConcurrentBuilds,
                AllowedRegistries = allowedRegistries ?? new[] { "GitHubContainerRegistry", "AwsEcr", "AzureAcr" }
            });
        });
        return this;
    }

    /// <summary>
    /// Adds a build cache entry to the test data.
    /// </summary>
    public TestDataBuilder WithCacheEntry(
        string manifestHash,
        string manifestId,
        string targetId,
        string imageReference,
        string architecture = "linux-arm64",
        long binarySize = 50_000_000)
    {
        _seedActions.Add(async connection =>
        {
            await connection.ExecuteAsync(@"
                INSERT INTO build_cache_registry (manifest_hash, manifest_id, target_id, image_reference, architecture, binary_size)
                VALUES (@ManifestHash, @ManifestId, @TargetId, @ImageReference, @Architecture, @BinarySize)
                ON CONFLICT (manifest_hash) DO UPDATE
                SET cache_hit_count = build_cache_registry.cache_hit_count + 1,
                    last_accessed_at = NOW()
            ", new
            {
                ManifestHash = manifestHash,
                ManifestId = manifestId,
                TargetId = targetId,
                ImageReference = imageReference,
                Architecture = architecture,
                BinarySize = binarySize
            });
        });
        return this;
    }

    /// <summary>
    /// Adds a build queue entry to the test data.
    /// </summary>
    public TestDataBuilder WithBuildInQueue(
        string customerId,
        string manifestId,
        string manifestHash,
        string status = "queued",
        int priority = 100,
        string? targetId = null)
    {
        _seedActions.Add(async connection =>
        {
            await connection.ExecuteAsync(@"
                INSERT INTO build_queue (customer_id, manifest_id, manifest_hash, status, priority, target_id)
                VALUES (@CustomerId, @ManifestId, @ManifestHash, @Status, @Priority, @TargetId)
            ", new
            {
                CustomerId = customerId,
                ManifestId = manifestId,
                ManifestHash = manifestHash,
                Status = status,
                Priority = priority,
                TargetId = targetId
            });
        });
        return this;
    }

    /// <summary>
    /// Adds registry credentials to the test data.
    /// </summary>
    public TestDataBuilder WithRegistryCredentials(
        string customerId,
        string registryType,
        string namespace_,
        string registryUrl,
        DateTimeOffset? expiresAt = null)
    {
        _seedActions.Add(async connection =>
        {
            await connection.ExecuteAsync(@"
                INSERT INTO registry_credentials (customer_id, registry_type, namespace, registry_url, username, expires_at)
                VALUES (@CustomerId, @RegistryType, @Namespace, @RegistryUrl, @Username, @ExpiresAt)
            ", new
            {
                CustomerId = customerId,
                RegistryType = registryType,
                Namespace = namespace_,
                RegistryUrl = registryUrl,
                Username = $"{customerId}-token",
                ExpiresAt = expiresAt
            });
        });
        return this;
    }

    /// <summary>
    /// Executes all seed actions.
    /// </summary>
    public async Task SeedAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var action in _seedActions)
        {
            await action(connection);
        }
    }
}

/// <summary>
/// Collection definition for integration tests that share the same fixture.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
