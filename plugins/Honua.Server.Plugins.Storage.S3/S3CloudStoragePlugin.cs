// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Amazon.S3;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Cloud.Attachments;
using Honua.Server.Core.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Plugins.Storage.S3;

/// <summary>
/// AWS S3 cloud storage plugin.
/// Provides highly scalable object storage with support for presigned URLs, encryption, versioning, and CDN integration.
/// </summary>
public class S3CloudStoragePlugin : ICloudStoragePlugin
{
    private ILogger<S3CloudStoragePlugin>? _logger;

    // IHonuaPlugin properties
    public string Id => "honua.plugins.storage.s3";
    public string Name => "AWS S3 Cloud Storage Plugin";
    public string Version => "1.0.0";
    public string Description => "AWS S3 object storage provider with support for presigned URLs, server-side encryption, versioning, lifecycle policies, and CloudFront CDN integration.";
    public string Author => "HonuaIO";
    public IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public string? MinimumHonuaVersion => "1.0.0";

    // ICloudStoragePlugin properties
    public string ProviderKey => AttachmentStoreProviderKeys.S3; // "s3"
    public CloudProviderType CloudProvider => CloudProviderType.AWS;
    public string DisplayName => "Amazon S3";

    public CloudStorageCapabilities Capabilities => new()
    {
        SupportsPresignedUrls = true,
        SupportsEncryption = true,
        SupportsVersioning = true,
        SupportsLifecyclePolicies = true,
        SupportsReplication = true,
        SupportsCdn = true, // CloudFront integration
        SupportsMetadata = true,
        SupportsAcl = true,
        MaxFileSizeBytes = 5_497_558_138_880, // 5 TB
        SupportsMultipartUpload = true,
        SupportsStreamingUpload = true
    };

    public Task OnLoadAsync(PluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.ServiceProvider?.GetService<ILogger<S3CloudStoragePlugin>>();
        _logger?.LogInformation(
            "Loading AWS S3 cloud storage plugin: {Name} v{Version}",
            Name,
            Version
        );

        return Task.CompletedTask;
    }

    public Task OnUnloadAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Unloading AWS S3 cloud storage plugin");
        return Task.CompletedTask;
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration, PluginContext context)
    {
        _logger?.LogDebug("Registering AWS S3 storage provider with key: {ProviderKey}", ProviderKey);

        // Register the S3 attachment store provider as a keyed singleton
        services.AddKeyedSingleton<IAttachmentStoreProvider>(
            ProviderKey,
            (serviceProvider, key) =>
            {
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                return new S3AttachmentStoreProvider(loggerFactory);
            }
        );

        _logger?.LogInformation("AWS S3 storage provider registered successfully");
    }

    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        // Check if AWS SDK packages are available
        try
        {
            var s3Assembly = typeof(AmazonS3Client).Assembly;
            _logger?.LogDebug("AWS SDK S3 version: {Version}", s3Assembly.GetName().Version);
        }
        catch (Exception ex)
        {
            result.AddError($"AWSSDK.S3 package not found: {ex.Message}");
            return result;
        }

        try
        {
            var coreAssembly = typeof(Amazon.Runtime.AWSCredentials).Assembly;
            _logger?.LogDebug("AWS SDK Core version: {Version}", coreAssembly.GetName().Version);
        }
        catch (Exception ex)
        {
            result.AddError($"AWSSDK.Core package not found: {ex.Message}");
            return result;
        }

        // Check for S3 attachment storage profiles in configuration
        // Try V2 configuration first (if AttachmentStorage is defined)
        var honuaConfig = configuration.Get<Core.Configuration.V2.HonuaConfig>();
        var foundProfiles = false;

        // Use reflection to check if AttachmentStorage property exists (for forward compatibility)
        var attachmentStorageProperty = honuaConfig?.GetType().GetProperty("AttachmentStorage");
        if (attachmentStorageProperty != null)
        {
            var attachmentStorage = attachmentStorageProperty.GetValue(honuaConfig) as IDictionary<string, object>;
            if (attachmentStorage != null && attachmentStorage.Count > 0)
            {
                foundProfiles = true;
                _logger?.LogDebug("Found attachment storage configuration in V2 config");
            }
        }

        // Fall back to V1 configuration format
        if (!foundProfiles)
        {
            var attachmentConfig = configuration.GetSection("Honua:Attachments");
            var profiles = attachmentConfig.GetSection("Profiles").GetChildren();

            foreach (var profile in profiles)
            {
                var provider = profile.GetValue<string>("Provider");
                if (string.Equals(provider, "s3", StringComparison.OrdinalIgnoreCase))
                {
                    foundProfiles = true;
                    var profileKey = profile.Key;

                    _logger?.LogDebug("Found S3 attachment storage profile: {ProfileKey}", profileKey);

                    // Validate bucket name
                    var bucketName = profile.GetValue<string>("S3:BucketName");
                    if (string.IsNullOrWhiteSpace(bucketName))
                    {
                        result.AddError($"S3 storage profile '{profileKey}' is missing required 'S3:BucketName' configuration");
                    }
                    else
                    {
                        // Validate bucket name format (AWS naming rules)
                        if (bucketName.Length < 3 || bucketName.Length > 63)
                        {
                            result.AddWarning($"S3 storage profile '{profileKey}' has invalid bucket name length (must be 3-63 characters): {bucketName}");
                        }
                        else if (!System.Text.RegularExpressions.Regex.IsMatch(bucketName, "^[a-z0-9][a-z0-9.-]*[a-z0-9]$"))
                        {
                            result.AddWarning($"S3 storage profile '{profileKey}' has invalid bucket name format (must be lowercase letters, numbers, dots, and hyphens): {bucketName}");
                        }
                    }

                    // Validate credentials configuration
                    var accessKeyId = profile.GetValue<string>("S3:AccessKeyId");
                    var secretAccessKey = profile.GetValue<string>("S3:SecretAccessKey");
                    var useInstanceProfile = profile.GetValue<bool?>("S3:UseInstanceProfile") ?? true;

                    var hasAccessKeys = !string.IsNullOrWhiteSpace(accessKeyId) && !string.IsNullOrWhiteSpace(secretAccessKey);

                    if (!hasAccessKeys && !useInstanceProfile)
                    {
                        result.AddWarning($"S3 storage profile '{profileKey}' has no credentials configured (neither access keys nor instance profile enabled)");
                    }

                    if (hasAccessKeys && useInstanceProfile)
                    {
                        result.AddInfo($"S3 storage profile '{profileKey}' has both access keys and instance profile enabled. Access keys will take precedence.");
                    }

                    // Validate region or service URL is provided
                    var region = profile.GetValue<string>("S3:Region");
                    var serviceUrl = profile.GetValue<string>("S3:ServiceUrl");

                    if (string.IsNullOrWhiteSpace(region) && string.IsNullOrWhiteSpace(serviceUrl))
                    {
                        result.AddWarning($"S3 storage profile '{profileKey}' has neither region nor service_url configured. AWS will attempt to determine region automatically.");
                    }
                }
            }
        }

        if (!foundProfiles)
        {
            result.AddInfo("No S3 attachment storage profiles found in configuration (plugin will be available but unused)");
        }

        return result;
    }

    public IAttachmentStoreProvider CreateProvider(IConfiguration configuration)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        return new S3AttachmentStoreProvider(loggerFactory);
    }
}
