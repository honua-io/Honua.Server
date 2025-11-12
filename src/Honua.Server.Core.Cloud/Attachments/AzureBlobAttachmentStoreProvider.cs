// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using Azure.Storage.Blobs;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
using Honua.Server.Core.Attachments;
namespace Honua.Server.Core.Cloud.Attachments;

public sealed class AzureBlobAttachmentStoreProvider : IAttachmentStoreProvider, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, BlobContainerClient> _clientCache = new(StringComparer.OrdinalIgnoreCase);

    public AzureBlobAttachmentStoreProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public string ProviderKey => AttachmentStoreProviderKeys.AzureBlob;

    public IAttachmentStore Create(string profileId, AttachmentStorageProfileOptions profileConfiguration)
    {
        Guard.NotNull(profileConfiguration);
        var azureConfig = profileConfiguration.Azure ?? new AttachmentAzureBlobStorageOptions();

        if (string.IsNullOrWhiteSpace(azureConfig.ConnectionString))
        {
            throw new InvalidOperationException($"Azure Blob attachment profile '{profileId}' must specify azure.connectionString.");
        }

        var containerName = string.IsNullOrWhiteSpace(azureConfig.ContainerName)
            ? "attachments"
            : azureConfig.ContainerName;

        var containerClient = _clientCache.GetOrAdd(profileId, _ =>
        {
            var client = new BlobContainerClient(azureConfig.ConnectionString, containerName);

            // Optionally ensure container exists
            if (azureConfig.EnsureContainer)
            {
                client.CreateIfNotExists();
            }

            return client;
        });

        // Provider owns the cached container client, but individual stores don't
        return new AzureBlobAttachmentStore(containerClient, azureConfig.Prefix, ownsContainer: false);
    }

    public void Dispose()
    {
        // Dispose all cached BlobContainerClient instances
        foreach (var client in _clientCache.Values)
        {
            if (client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _clientCache.Clear();
    }
}
