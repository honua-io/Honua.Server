// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Cloud.Attachments;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Cloud.DependencyInjection;

/// <summary>
/// Extension methods for registering cloud-based attachment store providers (AWS, Azure, GCP).
/// </summary>
public static class CloudServiceCollectionExtensions
{
    /// <summary>
    /// Registers cloud-based attachment store providers (S3, Azure Blob, GCS).
    /// </summary>
    public static IServiceCollection AddCloudAttachmentStoreProviders(
        this IServiceCollection services,
        AttachmentConfigurationOptions? attachmentConfig)
    {
        var attachmentProfiles = (attachmentConfig?.Profiles?.Values as System.Collections.Generic.IEnumerable<AttachmentStorageProfileOptions>)
                                 ?? Enumerable.Empty<AttachmentStorageProfileOptions>();

        if (attachmentProfiles.Any(profile => profile != null && string.Equals(profile.Provider, AttachmentStoreProviderKeys.S3, StringComparison.OrdinalIgnoreCase)))
        {
            services.AddSingleton<IAttachmentStoreProvider>(sp => new S3AttachmentStoreProvider(sp.GetRequiredService<ILoggerFactory>()));
        }

        if (attachmentProfiles.Any(profile => profile != null && string.Equals(profile.Provider, AttachmentStoreProviderKeys.AzureBlob, StringComparison.OrdinalIgnoreCase)))
        {
            services.AddSingleton<IAttachmentStoreProvider>(sp => new AzureBlobAttachmentStoreProvider(sp.GetRequiredService<ILoggerFactory>()));
        }

        if (attachmentProfiles.Any(profile => profile != null && string.Equals(profile.Provider, AttachmentStoreProviderKeys.Gcs, StringComparison.OrdinalIgnoreCase)))
        {
            services.AddSingleton<IAttachmentStoreProvider>(sp => new GcsAttachmentStoreProvider(sp.GetRequiredService<ILoggerFactory>()));
        }

        return services;
    }
}
