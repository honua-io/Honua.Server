// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Concurrent;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
using Honua.Server.Core.Attachments;
namespace Honua.Server.Core.Cloud.Attachments;

public sealed class S3AttachmentStoreProvider : IAttachmentStoreProvider, IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, IAmazonS3> _clientCache = new(StringComparer.OrdinalIgnoreCase);

    public S3AttachmentStoreProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public string ProviderKey => AttachmentStoreProviderKeys.S3;

    public IAttachmentStore Create(string profileId, AttachmentStorageProfileOptions profileConfiguration)
    {
        Guard.NotNull(profileConfiguration);
        var s3Config = profileConfiguration.S3 ?? new AttachmentS3StorageOptions();
        if (string.IsNullOrWhiteSpace(s3Config.BucketName))
        {
            throw new InvalidOperationException($"S3 attachment profile '{profileId}' must specify s3.bucketName.");
        }

        var client = _clientCache.GetOrAdd(profileId, _ => CreateClient(s3Config));
        // Provider owns the clients, stores don't
        return new S3AttachmentStore(client, s3Config.BucketName!, s3Config.Prefix, ownsClient: false);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clientCache.Values)
        {
            if (client is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _clientCache.Clear();
    }

    private static IAmazonS3 CreateClient(AttachmentS3StorageOptions configuration)
    {
        var clientConfig = new AmazonS3Config
        {
            ForcePathStyle = configuration.ForcePathStyle
        };

        if (!string.IsNullOrWhiteSpace(configuration.ServiceUrl))
        {
            clientConfig.ServiceURL = configuration.ServiceUrl.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(configuration.Region))
        {
            clientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(configuration.Region.Trim());
        }

        AWSCredentials credentials;
        if (!string.IsNullOrWhiteSpace(configuration.AccessKeyId) && !string.IsNullOrWhiteSpace(configuration.SecretAccessKey))
        {
            credentials = new BasicAWSCredentials(configuration.AccessKeyId.Trim(), configuration.SecretAccessKey.Trim());
        }
        else if (configuration.UseInstanceProfile)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            credentials = FallbackCredentialsFactory.GetCredentials();
#pragma warning restore CS0618 // Type or member is obsolete
        }
        else
        {
            throw new InvalidOperationException("S3 attachment profile must supply access keys or enable useInstanceProfile.");
        }

        return new AmazonS3Client(credentials, clientConfig);
    }
}
