using System;
using System.Threading.Tasks;
using Amazon.S3;
using Azure.Storage.Blobs;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.Azurite;
using Testcontainers.Minio;
using Xunit;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Shared fixture for storage container infrastructure (MinIO, Azurite).
/// Provides reusable container instances across multiple test classes.
/// </summary>
public sealed class StorageContainerFixture : IAsyncLifetime
{
    private MinioContainer? _minioContainer;
    private AzuriteContainer? _azuriteContainer;

    public IAmazonS3? MinioClient { get; private set; }
    public BlobServiceClient? AzuriteClient { get; private set; }

    public string? MinioEndpoint { get; private set; }
    public string? MinioAccessKey { get; private set; }
    public string? MinioSecretKey { get; private set; }
    public string? MinioRegion { get; private set; }

    public string? AzuriteConnectionString { get; private set; }
    public string? AzuriteBlobEndpoint { get; private set; }

    public bool IsDockerAvailable { get; private set; }
    public bool MinioAvailable { get; private set; }
    public bool AzuriteAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        // Check if Docker is available
        IsDockerAvailable = await IsDockerRunningAsync();

        if (!IsDockerAvailable)
        {
            Console.WriteLine("Docker is not available. Storage container tests will be skipped.");
            return;
        }

        // Initialize MinIO
        try
        {
            await InitializeMinioAsync();
            MinioAvailable = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MinIO container initialization failed: {ex.Message}");
            MinioAvailable = false;
        }

        // Initialize Azurite
        try
        {
            await InitializeAzuriteAsync();
            AzuriteAvailable = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Azurite container initialization failed: {ex.Message}");
            AzuriteAvailable = false;
        }
    }

    private async Task InitializeMinioAsync()
    {
        _minioContainer = new MinioBuilder()
            .WithImage("minio/minio:latest")
            .Build();

        await _minioContainer.StartAsync();

        MinioEndpoint = _minioContainer.GetConnectionString();
        MinioAccessKey = "minioadmin";
        MinioSecretKey = "minioadmin";
        MinioRegion = "us-east-1";

        var config = new AmazonS3Config
        {
            ServiceURL = MinioEndpoint,
            ForcePathStyle = true,
            AuthenticationRegion = MinioRegion
        };

        MinioClient = new AmazonS3Client(MinioAccessKey, MinioSecretKey, config);
    }

    private async Task InitializeAzuriteAsync()
    {
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();

        await _azuriteContainer.StartAsync();

        AzuriteConnectionString = _azuriteContainer.GetConnectionString();
        AzuriteBlobEndpoint = $"http://localhost:{_azuriteContainer.GetMappedPublicPort(10000)}";

        AzuriteClient = new BlobServiceClient(AzuriteConnectionString);
    }

    public async Task DisposeAsync()
    {
        MinioClient?.Dispose();

        if (_minioContainer != null)
        {
            await _minioContainer.DisposeAsync();
        }

        if (_azuriteContainer != null)
        {
            await _azuriteContainer.DisposeAsync();
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
/// Collection definition for storage container tests.
/// All tests using this collection will share the same container instances.
/// </summary>
[CollectionDefinition("StorageContainers")]
public class StorageContainerCollection : ICollectionFixture<StorageContainerFixture>
{
}
