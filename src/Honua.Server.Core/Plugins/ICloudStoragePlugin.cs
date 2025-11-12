// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Attachments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Plugins;

/// <summary>
/// Plugin interface for cloud storage providers.
/// Enables extensible cloud storage support through the plugin system.
/// </summary>
public interface ICloudStoragePlugin : IHonuaPlugin
{
    /// <summary>
    /// The unique provider key for this cloud storage provider (e.g., "s3", "azureblob", "gcs").
    /// Must match the provider key used in Configuration V2 attachment_storage blocks.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// The cloud provider type.
    /// </summary>
    CloudProviderType CloudProvider { get; }

    /// <summary>
    /// Display name for the cloud storage provider (e.g., "AWS S3", "Azure Blob Storage", "Google Cloud Storage").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the capabilities supported by this cloud storage provider.
    /// </summary>
    CloudStorageCapabilities Capabilities { get; }

    /// <summary>
    /// Registers the cloud storage provider with the dependency injection container.
    /// The provider should be registered as a keyed singleton using the ProviderKey.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="context">The plugin context.</param>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration, PluginContext context);

    /// <summary>
    /// Validates that the plugin's configuration is correct and the cloud provider SDK is available.
    /// Should check for required NuGet packages, credentials, bucket/container names, etc.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    PluginValidationResult ValidateConfiguration(IConfiguration configuration);

    /// <summary>
    /// Creates an instance of the attachment store provider.
    /// This is called by the AttachmentStoreSelector when a storage profile is requested.
    /// </summary>
    /// <param name="configuration">The configuration for this storage profile.</param>
    /// <returns>A new instance of the IAttachmentStoreProvider.</returns>
    IAttachmentStoreProvider CreateProvider(IConfiguration configuration);
}

/// <summary>
/// Cloud provider types for categorization and filtering.
/// </summary>
public enum CloudProviderType
{
    /// <summary>Amazon Web Services (AWS)</summary>
    AWS,

    /// <summary>Microsoft Azure</summary>
    Azure,

    /// <summary>Google Cloud Platform (GCP)</summary>
    GCP,

    /// <summary>Multi-cloud or cloud-agnostic providers</summary>
    MultiCloud,

    /// <summary>Self-hosted or on-premises storage</summary>
    SelfHosted,

    /// <summary>Other cloud providers (DigitalOcean, Cloudflare, etc.)</summary>
    Other
}

/// <summary>
/// Describes the capabilities of a cloud storage provider.
/// </summary>
public class CloudStorageCapabilities
{
    /// <summary>
    /// Whether the provider supports presigned URLs for direct client uploads/downloads.
    /// </summary>
    public bool SupportsPresignedUrls { get; init; }

    /// <summary>
    /// Whether the provider supports server-side encryption.
    /// </summary>
    public bool SupportsEncryption { get; init; }

    /// <summary>
    /// Whether the provider supports versioning of objects.
    /// </summary>
    public bool SupportsVersioning { get; init; }

    /// <summary>
    /// Whether the provider supports lifecycle policies (auto-delete, tiering, etc.).
    /// </summary>
    public bool SupportsLifecyclePolicies { get; init; }

    /// <summary>
    /// Whether the provider supports cross-region replication.
    /// </summary>
    public bool SupportsReplication { get; init; }

    /// <summary>
    /// Whether the provider supports CDN integration.
    /// </summary>
    public bool SupportsCdn { get; init; }

    /// <summary>
    /// Whether the provider supports object metadata/tags.
    /// </summary>
    public bool SupportsMetadata { get; init; }

    /// <summary>
    /// Whether the provider supports access control lists (ACLs).
    /// </summary>
    public bool SupportsAcl { get; init; }

    /// <summary>
    /// Maximum file size supported (in bytes). Null for unlimited.
    /// </summary>
    public long? MaxFileSizeBytes { get; init; }

    /// <summary>
    /// Whether the provider supports multipart uploads for large files.
    /// </summary>
    public bool SupportsMultipartUpload { get; init; }

    /// <summary>
    /// Whether the provider supports streaming uploads (no need to know size upfront).
    /// </summary>
    public bool SupportsStreamingUpload { get; init; }
}
