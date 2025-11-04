using System.Diagnostics;
using Google.Cloud.BigQuery.V2;
using Google.Apis.Bigquery.v2.Data;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Honua.Server.Enterprise.Tests.BigQuery;

/// <summary>
/// xUnit collection fixture for BigQuery emulator using Testcontainers.
/// This fixture manages the lifecycle of the BigQuery emulator container
/// and provides a client for integration tests.
///
/// Note: BigQuery emulator support is limited. This fixture will skip tests
/// if Docker is not available or if the emulator fails to start.
/// </summary>
public sealed class BigQueryEmulatorFixture : IAsyncLifetime
{
    private IContainer? _container;
    private BigQueryClient? _client;
    private bool _dockerAvailable;
    private string? _emulatorHost;

    public const string TestProjectId = "test-project";
    public const string TestDatasetId = "test_dataset";
    public const string TestTableId = "test_features";

    public bool IsAvailable => _dockerAvailable && _container != null && _client != null;
    public BigQueryClient? Client => _client;
    public string ConnectionString => _dockerAvailable && !string.IsNullOrEmpty(_emulatorHost)
        ? $"ProjectId={TestProjectId};EmulatorHost={_emulatorHost}"
        : string.Empty;

    public async Task InitializeAsync()
    {
        // Check if Docker is available
        _dockerAvailable = await IsDockerAvailableAsync();

        if (!_dockerAvailable)
        {
            return;
        }

        try
        {
            // Build and start BigQuery emulator container
            _container = new ContainerBuilder()
                .WithImage("ghcr.io/goccy/bigquery-emulator:latest")
                .WithPortBinding(9050, 9050)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9050))
                .Build();

            await _container.StartAsync();

            // Set emulator host
            _emulatorHost = $"localhost:{_container.GetMappedPublicPort(9050)}";
            Environment.SetEnvironmentVariable("BIGQUERY_EMULATOR_HOST", _emulatorHost);

            _client = BigQueryClient.Create(TestProjectId);

            // Initialize test dataset and table
            await InitializeTestSchemaAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize BigQuery emulator: {ex.Message}");
            _dockerAvailable = false;
            _container = null;
            _client = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task InitializeTestSchemaAsync()
    {
        if (_client == null)
        {
            return;
        }

        try
        {
            // Create dataset
            var dataset = new Dataset
            {
                Location = "US"
            };
            await _client.CreateDatasetAsync(TestDatasetId, dataset);

            // Create test table with geometry
            var tableSchema = new TableSchemaBuilder
            {
                { "id", BigQueryDbType.String },
                { "name", BigQueryDbType.String },
                { "description", BigQueryDbType.String },
                { "value", BigQueryDbType.Int64 },
                { "created_at", BigQueryDbType.Timestamp },
                { "geom", BigQueryDbType.Geography }
            }.Build();

            var table = new Table
            {
                Schema = tableSchema
            };

            await _client.CreateTableAsync(TestDatasetId, TestTableId, table);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize test schema: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Inserts test data into the BigQuery test table.
    /// </summary>
    public async Task InsertTestDataAsync()
    {
        if (_client == null)
        {
            throw new InvalidOperationException("BigQuery client not initialized");
        }

        var rows = new[]
        {
            new BigQueryInsertRow
            {
                ["id"] = "feature-1",
                ["name"] = "Test Feature 1",
                ["description"] = "First test feature",
                ["value"] = 100,
                ["created_at"] = DateTime.UtcNow,
                ["geom"] = "POINT(-122.4194 37.7749)" // San Francisco
            },
            new BigQueryInsertRow
            {
                ["id"] = "feature-2",
                ["name"] = "Test Feature 2",
                ["description"] = "Second test feature",
                ["value"] = 200,
                ["created_at"] = DateTime.UtcNow.AddDays(-1),
                ["geom"] = "POINT(-118.2437 34.0522)" // Los Angeles
            },
            new BigQueryInsertRow
            {
                ["id"] = "feature-3",
                ["name"] = "Test Feature 3",
                ["description"] = "Third test feature",
                ["value"] = 300,
                ["created_at"] = DateTime.UtcNow.AddDays(-2),
                ["geom"] = "POINT(-73.9352 40.7306)" // New York
            }
        };

        await _client.InsertRowsAsync(TestProjectId, TestDatasetId, TestTableId, rows);

        // Wait for data to be available (eventual consistency)
        await Task.Delay(1000);
    }

    /// <summary>
    /// Clears all data from the test table.
    /// </summary>
    public async Task ClearTestDataAsync()
    {
        if (_client == null)
        {
            return;
        }

        var sql = $"DELETE FROM `{TestProjectId}.{TestDatasetId}.{TestTableId}` WHERE TRUE";
        await _client.ExecuteQueryAsync(sql, parameters: null);
    }

    private static async Task<bool> IsDockerAvailableAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// xUnit collection definition for BigQuery emulator tests.
/// All tests in this collection will share the same emulator instance.
/// </summary>
[CollectionDefinition("BigQueryEmulator")]
public class BigQueryEmulatorCollection : ICollectionFixture<BigQueryEmulatorFixture>
{
}
