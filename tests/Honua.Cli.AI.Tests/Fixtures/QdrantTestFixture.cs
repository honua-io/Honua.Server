using System;
using System.Net.Http;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.Qdrant;
using Xunit;

namespace Honua.Cli.AI.Tests.Fixtures;

/// <summary>
/// Shared fixture for Qdrant vector database container.
/// Provides a real vector search instance for testing instead of mocking Azure AI Search.
/// </summary>
public sealed class QdrantTestFixture : IAsyncLifetime
{
    private QdrantContainer? _container;

    public string? Endpoint { get; private set; }
    public int? GrpcPort { get; private set; }
    public string? ApiKey { get; private set; }
    public bool IsDockerAvailable { get; private set; }
    public bool QdrantAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        // Check if Docker is available
        IsDockerAvailable = await IsDockerRunningAsync();

        if (!IsDockerAvailable)
        {
            Console.WriteLine("Docker is not available. Qdrant container tests will be skipped.");
            return;
        }

        // Initialize Qdrant container
        try
        {
            _container = new QdrantBuilder()
                .WithImage("qdrant/qdrant:v1.7.4")
                .Build();

            await _container.StartAsync();

            // Get connection details
            var httpPort = _container.GetMappedPublicPort(6333);
            var grpcPort = _container.GetMappedPublicPort(6334);

            Endpoint = $"http://localhost:{httpPort}";
            GrpcPort = grpcPort;
            ApiKey = null; // Qdrant in docker doesn't require auth by default

            // Verify Qdrant is responsive
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"{Endpoint}/");
            response.EnsureSuccessStatusCode();

            QdrantAvailable = true;
            Console.WriteLine($"Qdrant container started successfully at {Endpoint}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Qdrant container initialization failed: {ex.Message}");
            QdrantAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    private static async Task<bool> IsDockerRunningAsync()
    {
        try
        {
            var container = new ContainerBuilder()
                .WithImage("alpine:latest")
                .WithCommand("echo", "test")
                .WithWaitStrategy(Wait.ForUnixContainer())
                .Build();

            await container.StartAsync();
            await container.DisposeAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Collection definition for Qdrant vector database tests.
/// All tests using this collection will share the same Qdrant instance.
/// </summary>
[CollectionDefinition("QdrantContainer")]
public class QdrantContainerCollection : ICollectionFixture<QdrantTestFixture>
{
}
