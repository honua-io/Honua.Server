// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using Google.Cloud.Storage.V1;
using Google.Apis.Auth.OAuth2;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
using Honua.Server.Core.Attachments;
namespace Honua.Server.Core.Cloud.Attachments;

public sealed class GcsAttachmentStoreProvider : IAttachmentStoreProvider, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, StorageClient> _clientCache = new(StringComparer.OrdinalIgnoreCase);

    public GcsAttachmentStoreProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public string ProviderKey => AttachmentStoreProviderKeys.Gcs;

    public IAttachmentStore Create(string profileId, AttachmentStorageProfileOptions profileConfiguration)
    {
        Guard.NotNull(profileConfiguration);
        var gcsConfig = profileConfiguration.Gcs ?? new AttachmentGcsStorageOptions();

        if (string.IsNullOrWhiteSpace(gcsConfig.BucketName))
        {
            throw new InvalidOperationException($"GCS attachment profile '{profileId}' must specify gcs.bucketName.");
        }

        var client = _clientCache.GetOrAdd(profileId, _ => CreateClient(gcsConfig));

        // Provider owns the cached client, but individual stores don't
        return new GcsAttachmentStore(
            client,
            gcsConfig.BucketName!,
            gcsConfig.Prefix ?? "attachments/",
            ownsClient: false);
    }

    public void Dispose()
    {
        foreach (var client in _clientCache.Values)
        {
            client.Dispose();
        }

        _clientCache.Clear();
    }

    private static StorageClient CreateClient(AttachmentGcsStorageOptions configuration)
    {
        GoogleCredential? credential = null;

        if (!string.IsNullOrWhiteSpace(configuration.CredentialsPath))
        {
            // Use service account credentials from file
            credential = GoogleCredential.FromFile(configuration.CredentialsPath.Trim());
        }
        else if (configuration.UseApplicationDefaultCredentials)
        {
            // Use Application Default Credentials (ADC) - supports:
            // - GOOGLE_APPLICATION_CREDENTIALS environment variable
            // - gcloud CLI credentials
            // - GCE/GKE/Cloud Run metadata server
            credential = GoogleCredential.GetApplicationDefault();
        }
        else
        {
            throw new InvalidOperationException(
                "GCS attachment profile must supply credentialsPath or enable useApplicationDefaultCredentials.");
        }

        // Ensure the credential has the required scopes for Cloud Storage
        if (credential.IsCreateScopedRequired)
        {
            credential = credential.CreateScoped(new[] { "https://www.googleapis.com/auth/cloud-platform" });
        }

        return StorageClient.Create(credential);
    }
}
