// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Polly;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

public sealed class AzureBlobRasterSourceProvider : IRasterSourceProvider, IAsyncDisposable
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly bool _ownsClient;
    private readonly ResiliencePipeline _circuitBreaker;

    public AzureBlobRasterSourceProvider(
        BlobServiceClient blobServiceClient,
        ILogger<AzureBlobRasterSourceProvider> logger,
        bool ownsClient = false)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _ownsClient = ownsClient;
        _circuitBreaker = Caching.ExternalServiceResiliencePolicies.CreateCircuitBreakerPipeline("Azure Blob Source", logger);
    }

    public string ProviderKey => "azureblob";

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        // Azure Blob URIs: azureblob://container/blob-path
        return uri.StartsWith("azureblob://", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ParseAzureBlobUri(uri);

        return await _circuitBreaker.ExecuteAsync(async ct =>
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            try
            {
                var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct).ConfigureAwait(false);
                return response.Value.Content;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new FileNotFoundException($"Raster blob not found: {uri}", ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null, CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ParseAzureBlobUri(uri);

        return await _circuitBreaker.ExecuteAsync(async ct =>
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            try
            {
                var httpRange = length.HasValue
                    ? new HttpRange(offset, length.Value)
                    : new HttpRange(offset);

                var downloadOptions = new BlobDownloadOptions
                {
                    Range = httpRange
                };

                var response = await blobClient.DownloadStreamingAsync(downloadOptions, ct).ConfigureAwait(false);
                return response.Value.Content;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new FileNotFoundException($"Raster blob not found: {uri}", ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static (string containerName, string blobName) ParseAzureBlobUri(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) || parsedUri.Scheme != "azureblob")
        {
            throw new ArgumentException($"Invalid Azure Blob URI: {uri}", nameof(uri));
        }

        var containerName = parsedUri.Host;
        var blobName = parsedUri.AbsolutePath.TrimStart('/');

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException($"Azure Blob container not specified in URI: {uri}", nameof(uri));
        }

        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException($"Azure Blob name not specified in URI: {uri}", nameof(uri));
        }

        return (containerName, blobName);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient && _blobServiceClient is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
