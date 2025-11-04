using System;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Metadata.Providers;
using Honua.Server.Core.Tests.Shared;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Apis.Metadata.Providers;

[Collection("SharedPostgres")]
[Trait("Category", "Integration")]
[Trait("Category", "Metadata")]
public sealed class PostgresMetadataProviderIntegrationTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<PostgresMetadataProvider> _logger;
    private string? _schemaName;
    private PostgresMetadataOptions? _options;
    private PostgresMetadataProvider? _provider;

    public PostgresMetadataProviderIntegrationTests(SharedPostgresFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = new TestLogger<PostgresMetadataProvider>(output);
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            throw new SkipException("PostgreSQL test container is not available.");
        }

        _schemaName = "metadata_test_" + Guid.NewGuid().ToString("N");
        var options = new PostgresMetadataOptions
        {
            SchemaName = _schemaName,
            EnableNotifications = true
        };

        _options = options;
        _provider = new PostgresMetadataProvider(_fixture.ConnectionString, options, _logger);

        // Warm schema and listener
        await _provider.SaveAsync(CreateSnapshot(), CancellationToken.None);
        await WaitForNotificationConnectionAsync(_provider, TimeSpan.FromSeconds(15));
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (_schemaName is not null && _fixture.IsAvailable)
        {
            await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS {QuoteIdentifier(_schemaName)} CASCADE;";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [RequiresDockerFact]
    public async Task NotificationListener_ReconnectsAfterConnectionDrop()
    {
        var provider = _provider ?? throw new InvalidOperationException("Provider not initialized.");
        var options = _options ?? throw new InvalidOperationException("Provider options not initialized.");

        await ForceListenerDisconnectAsync(provider);
        await WaitForNotificationConnectionAsync(provider, TimeSpan.FromSeconds(30));

        var tcs = new TaskCompletionSource<MetadataChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? _, MetadataChangedEventArgs args) => tcs.TrySetResult(args);

        provider.MetadataChanged += Handler;
        try
        {
            await PublishManualNotificationAsync(options.NotificationChannel, _fixture.ConnectionString);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            completedTask.Should().Be(tcs.Task, "the provider should reconnect and process notifications after a disconnect");

            var result = await tcs.Task;
            result.Should().NotBeNull();
            result.Source.Should().Be("postgres-reload");
        }
        finally
        {
            provider.MetadataChanged -= Handler;
        }
    }

    private static async Task ForceListenerDisconnectAsync(PostgresMetadataProvider provider)
    {
        var field = typeof(PostgresMetadataProvider).GetField("_notificationConnection", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field is null)
        {
            throw new InvalidOperationException("Unable to access notification connection field for testing.");
        }

        var connection = (NpgsqlConnection?)field.GetValue(provider);
        if (connection is null)
        {
            throw new InvalidOperationException("Notification listener has not started. Did initialization complete?");
        }

        await connection.CloseAsync();
        await connection.DisposeAsync();
    }

    private static async Task WaitForNotificationConnectionAsync(PostgresMetadataProvider provider, TimeSpan timeout)
    {
        var field = typeof(PostgresMetadataProvider).GetField("_notificationConnection", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field is null)
        {
            throw new InvalidOperationException("Unable to access notification connection field for testing.");
        }

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var connection = (NpgsqlConnection?)field.GetValue(provider);
            if (connection is not null && connection.FullState == ConnectionState.Open)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException("PostgreSQL notification listener did not reach an open state within the allotted time.");
    }

    private static async Task PublishManualNotificationAsync(string channel, string connectionString)
    {
        var payload = JsonSerializer.Serialize(new
        {
            instance_id = "integration-test",
            version_id = Guid.NewGuid().ToString("N"),
            change_version = 1,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"NOTIFY {QuoteIdentifier(channel)}, @payload";
        command.Parameters.AddWithValue("@payload", payload);
        await command.ExecuteNonQueryAsync();
    }

    private static MetadataSnapshot CreateSnapshot()
    {
        var catalog = new CatalogDefinition
        {
            Id = "integration-catalog",
            Title = "Integration Catalog"
        };

        var folders = new[]
        {
            new FolderDefinition { Id = "folder", Title = "Folder" }
        };

        var dataSources = new[]
        {
            new DataSourceDefinition
            {
                Id = "datasource",
                Provider = "Postgres",
                ConnectionString = "Host=localhost;Database=test"
            }
        };

        var services = new[]
        {
            new ServiceDefinition
            {
                Id = "service",
                Title = "Service",
                FolderId = "folder",
                ServiceType = "feature",
                DataSourceId = "datasource"
            }
        };

        return new MetadataSnapshot(
            catalog,
            folders,
            dataSources,
            services,
            Array.Empty<LayerDefinition>(),
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>(),
            ServerDefinition.Default);
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException("Identifier must be provided", nameof(identifier));
        }

        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}
