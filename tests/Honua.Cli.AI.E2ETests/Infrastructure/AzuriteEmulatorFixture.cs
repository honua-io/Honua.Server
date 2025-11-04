using System;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Testcontainers.Azurite;
using Xunit;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

/// <summary>
/// Provides an Azurite storage emulator instance for integration tests.
/// </summary>
[CollectionDefinition("AzuriteEmulator")]
public sealed class AzuriteEmulatorCollection : ICollectionFixture<AzuriteEmulatorFixture>
{
}

public sealed class AzuriteEmulatorFixture : IAsyncLifetime
{
    private AzuriteContainer? _container;

    public bool IsDockerAvailable { get; private set; }

    public string ConnectionString { get; private set; } = string.Empty;

    public BlobServiceClient CreateBlobServiceClient()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            throw new InvalidOperationException("Azurite container not initialised.");
        }

        var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2021_10_04)
        {
            Diagnostics =
            {
                IsLoggingContentEnabled = false,
                IsTelemetryEnabled = false
            }
        };

        return new BlobServiceClient(ConnectionString, options);
    }

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            return;
        }

        IsDockerAvailable = await DeploymentEmulatorFixture.IsDockerRunningAsync();
        if (!IsDockerAvailable)
        {
            Console.WriteLine("Docker is unavailable. Azurite emulator tests will be skipped.");
            return;
        }

        _container = new AzuriteBuilder().Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        await WaitForAzuriteAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
            ConnectionString = string.Empty;
        }
    }

    private async Task WaitForAzuriteAsync()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            return;
        }

        var client = CreateBlobServiceClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await client.GetPropertiesAsync();
                return;
            }
            catch (Exception ex) when (attempt < 4)
            {
                Console.WriteLine($"Azurite readiness probe failed (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        throw new InvalidOperationException("Azurite blob service did not respond within the readiness window.");
    }
}
