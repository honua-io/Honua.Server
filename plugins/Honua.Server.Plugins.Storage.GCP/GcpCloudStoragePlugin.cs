// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Attachments;
using Honua.Server.Core.Cloud.Attachments;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Plugins.Storage.GCP;

/// <summary>
/// GCP Cloud Storage plugin.
/// Provides Google Cloud Storage (GCS) support for attachment storage with signed URLs,
/// server-side encryption, versioning, and multi-region replication capabilities.
/// </summary>
public class GcpCloudStoragePlugin : ICloudStoragePlugin
{
    private ILogger<GcpCloudStoragePlugin>? _logger;

    // IHonuaPlugin properties
    public string Id => "honua.plugins.storage.gcp";
    public string Name => "GCP Cloud Storage Plugin";
    public string Version => "1.0.0";
    public string Description => "Google Cloud Storage provider with support for signed URLs, server-side encryption, object versioning, and multi-region replication.";
    public string Author => "HonuaIO";
    public IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public string? MinimumHonuaVersion => "1.0.0";

    // ICloudStoragePlugin properties
    public string ProviderKey => AttachmentStoreProviderKeys.Gcs; // "gcs"
    public CloudProviderType CloudProvider => CloudProviderType.GCP;
    public string DisplayName => "Google Cloud Storage";

    public CloudStorageCapabilities Capabilities => new CloudStorageCapabilities
    {
        SupportsPresignedUrls = true,          // Signed URLs (v4 signing)
        SupportsEncryption = true,             // Server-side encryption (Google-managed, customer-managed, customer-supplied keys)
        SupportsVersioning = true,             // Object versioning
        SupportsLifecyclePolicies = true,      // Lifecycle management (auto-delete, archival, etc.)
        SupportsReplication = true,            // Multi-region and dual-region buckets
        SupportsCdn = true,                    // Cloud CDN integration
        SupportsMetadata = true,               // Custom metadata and object tags
        SupportsAcl = true,                    // Access Control Lists (ACLs) and IAM
        MaxFileSizeBytes = 5_497_558_138_880,  // 5TB maximum object size
        SupportsMultipartUpload = true,        // Resumable uploads and composite objects
        SupportsStreamingUpload = true         // Streaming uploads without knowing size upfront
    };

    public Task OnLoadAsync(PluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.ServiceProvider?.GetService<ILogger<GcpCloudStoragePlugin>>();
        _logger?.LogInformation(
            "Loading GCP Cloud Storage plugin: {Name} v{Version}",
            Name,
            Version
        );

        return Task.CompletedTask;
    }

    public Task OnUnloadAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Unloading GCP Cloud Storage plugin");
        return Task.CompletedTask;
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration, PluginContext context)
    {
        _logger?.LogDebug("Registering GCP Cloud Storage provider with key: {ProviderKey}", ProviderKey);

        // Register the GCS attachment store provider as a keyed singleton
        services.AddKeyedSingleton<IAttachmentStoreProvider>(
            ProviderKey,
            (serviceProvider, key) =>
            {
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                return new GcsAttachmentStoreProvider(loggerFactory);
            }
        );

        _logger?.LogInformation("GCP Cloud Storage provider registered successfully");
    }

    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        // Check if Google.Cloud.Storage.V1 package is available
        try
        {
            var gcsAssembly = typeof(Google.Cloud.Storage.V1.StorageClient).Assembly;
            _logger?.LogDebug("Google.Cloud.Storage.V1 version: {Version}", gcsAssembly.GetName().Version);
        }
        catch (Exception ex)
        {
            result.AddError($"Google.Cloud.Storage.V1 package not found: {ex.Message}");
            return result;
        }

        // Check if Google.Apis.Auth.OAuth2 package is available (for credentials)
        try
        {
            var authAssembly = typeof(Google.Apis.Auth.OAuth2.GoogleCredential).Assembly;
            _logger?.LogDebug("Google.Apis.Auth.OAuth2 version: {Version}", authAssembly.GetName().Version);
        }
        catch (Exception ex)
        {
            result.AddError($"Google.Apis.Auth.OAuth2 package not found: {ex.Message}");
            return result;
        }

        // Check Configuration V2 for any GCS attachment storage profiles
        var honuaConfig = configuration.Get<Core.Configuration.V2.HonuaConfig>();
        if (honuaConfig?.AttachmentStorage != null)
        {
            var gcsProfiles = honuaConfig.AttachmentStorage
                .Where(storage => storage.Value.Provider?.Equals("gcs", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (gcsProfiles.Any())
            {
                _logger?.LogDebug("Found {Count} GCS attachment storage profiles in configuration", gcsProfiles.Count);

                foreach (var profile in gcsProfiles)
                {
                    var gcsConfig = profile.Value.Gcs;

                    // Validate bucket name
                    if (string.IsNullOrWhiteSpace(gcsConfig?.BucketName))
                    {
                        result.AddError($"GCS storage profile '{profile.Key}' has no bucket name configured");
                        continue;
                    }

                    // Validate bucket name format (GCS naming requirements)
                    if (!IsValidBucketName(gcsConfig.BucketName))
                    {
                        result.AddWarning($"GCS storage profile '{profile.Key}' bucket name '{gcsConfig.BucketName}' may be invalid. " +
                            "Bucket names must be 3-63 characters, contain only lowercase letters, numbers, hyphens, and underscores.");
                    }

                    // Validate project ID format if provided
                    if (!string.IsNullOrWhiteSpace(gcsConfig.ProjectId) && !IsValidProjectId(gcsConfig.ProjectId))
                    {
                        result.AddWarning($"GCS storage profile '{profile.Key}' project ID '{gcsConfig.ProjectId}' may be invalid. " +
                            "Project IDs must be 6-30 characters, contain only lowercase letters, numbers, and hyphens, and start with a letter.");
                    }

                    // Validate credentials configuration
                    if (!string.IsNullOrWhiteSpace(gcsConfig.CredentialsPath))
                    {
                        if (!File.Exists(gcsConfig.CredentialsPath))
                        {
                            result.AddWarning($"GCS storage profile '{profile.Key}' credentials file not found: {gcsConfig.CredentialsPath}");
                        }
                    }
                    else if (!gcsConfig.UseApplicationDefaultCredentials)
                    {
                        result.AddWarning($"GCS storage profile '{profile.Key}' has no credentials configured. " +
                            "Either provide credentialsPath or enable useApplicationDefaultCredentials.");
                    }
                    else
                    {
                        // Check for Application Default Credentials environment variable
                        var adcPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
                        if (string.IsNullOrWhiteSpace(adcPath))
                        {
                            result.AddInfo($"GCS storage profile '{profile.Key}' is using Application Default Credentials, " +
                                "but GOOGLE_APPLICATION_CREDENTIALS environment variable is not set. " +
                                "Will attempt to use gcloud CLI credentials or GCE/GKE metadata server.");
                        }
                        else if (!File.Exists(adcPath))
                        {
                            result.AddWarning($"GCS storage profile '{profile.Key}' GOOGLE_APPLICATION_CREDENTIALS points to non-existent file: {adcPath}");
                        }
                    }
                }
            }
            else
            {
                result.AddInfo("No GCS attachment storage profiles found in configuration (plugin will be available but unused)");
            }
        }

        return result;
    }

    public IAttachmentStoreProvider CreateProvider(IConfiguration configuration)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        return new GcsAttachmentStoreProvider(loggerFactory);
    }

    /// <summary>
    /// Validates a GCS bucket name according to Google Cloud Storage naming requirements.
    /// </summary>
    /// <param name="bucketName">The bucket name to validate.</param>
    /// <returns>True if the bucket name is valid, false otherwise.</returns>
    /// <remarks>
    /// Bucket naming requirements:
    /// - Must be between 3 and 63 characters long
    /// - Can contain lowercase letters, numbers, hyphens (-), and underscores (_)
    /// - Must start and end with a letter or number
    /// - Cannot contain consecutive periods (..)
    /// - Cannot be formatted as an IP address (e.g., 192.168.5.4)
    /// </remarks>
    private static bool IsValidBucketName(string bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            return false;

        if (bucketName.Length < 3 || bucketName.Length > 63)
            return false;

        // Must contain only lowercase letters, numbers, hyphens, underscores, and dots
        if (!System.Text.RegularExpressions.Regex.IsMatch(bucketName, @"^[a-z0-9._-]+$"))
            return false;

        // Must start and end with a letter or number
        if (!char.IsLetterOrDigit(bucketName[0]) || !char.IsLetterOrDigit(bucketName[^1]))
            return false;

        // Cannot contain consecutive periods
        if (bucketName.Contains(".."))
            return false;

        // Cannot be formatted as an IP address
        if (System.Text.RegularExpressions.Regex.IsMatch(bucketName, @"^\d+\.\d+\.\d+\.\d+$"))
            return false;

        return true;
    }

    /// <summary>
    /// Validates a GCP project ID according to Google Cloud Platform naming requirements.
    /// </summary>
    /// <param name="projectId">The project ID to validate.</param>
    /// <returns>True if the project ID is valid, false otherwise.</returns>
    /// <remarks>
    /// Project ID naming requirements:
    /// - Must be between 6 and 30 characters long
    /// - Can contain lowercase letters, numbers, and hyphens
    /// - Must start with a letter
    /// - Cannot end with a hyphen
    /// </remarks>
    private static bool IsValidProjectId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return false;

        if (projectId.Length < 6 || projectId.Length > 30)
            return false;

        // Must contain only lowercase letters, numbers, and hyphens
        if (!System.Text.RegularExpressions.Regex.IsMatch(projectId, @"^[a-z][a-z0-9-]*$"))
            return false;

        // Cannot end with a hyphen
        if (projectId.EndsWith('-'))
            return false;

        return true;
    }
}
