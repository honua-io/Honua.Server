// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure.Storage.Blobs;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Cloud.Attachments;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Plugins.Storage.Azure;

/// <summary>
/// Azure Blob Storage cloud storage plugin.
/// Provides enterprise-grade object storage with SAS token support, geo-redundancy, and CDN integration.
/// </summary>
public class AzureBlobStoragePlugin : ICloudStoragePlugin
{
    private ILogger<AzureBlobStoragePlugin>? _logger;

    // IHonuaPlugin properties
    public string Id => "honua.plugins.storage.azure";
    public string Name => "Azure Blob Storage Plugin";
    public string Version => "1.0.0";
    public string Description => "Enterprise-grade Azure Blob Storage provider with SAS token support, server-side encryption, geo-redundant storage, and Azure CDN integration.";
    public string Author => "HonuaIO";
    public IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public string? MinimumHonuaVersion => "1.0.0";

    // ICloudStoragePlugin properties
    public string ProviderKey => "azureblob"; // Matches AttachmentStoreProviderKeys.AzureBlob
    public CloudProviderType CloudProvider => CloudProviderType.Azure;
    public string DisplayName => "Azure Blob Storage";

    public CloudStorageCapabilities Capabilities => new CloudStorageCapabilities
    {
        SupportsPresignedUrls = true, // SAS tokens
        SupportsEncryption = true, // Server-side encryption
        SupportsVersioning = true, // Blob versioning
        SupportsLifecyclePolicies = true, // Lifecycle management
        SupportsReplication = true, // Geo-redundant storage (GRS, RA-GRS, GZRS)
        SupportsCdn = true, // Azure CDN integration
        SupportsMetadata = true, // Custom metadata
        SupportsAcl = true, // Blob access tiers and permissions
        MaxFileSizeBytes = 190_734_863_360_000, // 190.7TB for block blobs (50,000 blocks × 4000 MiB)
        SupportsMultipartUpload = true, // Block blobs (staged blocks)
        SupportsStreamingUpload = true // Upload from stream
    };

    public Task OnLoadAsync(PluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.ServiceProvider?.GetService<ILogger<AzureBlobStoragePlugin>>();
        _logger?.LogInformation(
            "Loading Azure Blob Storage plugin: {Name} v{Version}",
            Name,
            Version
        );

        return Task.CompletedTask;
    }

    public Task OnUnloadAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Unloading Azure Blob Storage plugin");
        return Task.CompletedTask;
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration, PluginContext context)
    {
        _logger?.LogDebug("Registering Azure Blob Storage provider with key: {ProviderKey}", ProviderKey);

        // Register the Azure Blob attachment store provider as a keyed singleton
        services.AddKeyedSingleton<IAttachmentStoreProvider>(
            ProviderKey,
            (serviceProvider, key) =>
            {
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                return new AzureBlobAttachmentStoreProvider(loggerFactory);
            }
        );

        _logger?.LogInformation("Azure Blob Storage provider registered successfully");
    }

    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        // Check if Azure.Storage.Blobs package is available
        try
        {
            var azureAssembly = typeof(BlobContainerClient).Assembly;
            _logger?.LogDebug("Azure.Storage.Blobs version: {Version}", azureAssembly.GetName().Version);
        }
        catch (Exception ex)
        {
            result.AddError($"Azure.Storage.Blobs package not found: {ex.Message}");
            return result;
        }

        // Check Configuration V2 for any Azure Blob storage profiles
        var honuaConfig = configuration.Get<Core.Configuration.V2.HonuaConfig>();
        if (honuaConfig?.AttachmentStorage != null)
        {
            var azureProfiles = honuaConfig.AttachmentStorage
                .Where(profile => profile.Value.Provider?.Equals("azureblob", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (azureProfiles.Any())
            {
                _logger?.LogDebug("Found {Count} Azure Blob storage profiles in configuration", azureProfiles.Count);

                foreach (var profile in azureProfiles)
                {
                    var azureConfig = profile.Value.Azure;

                    // Validate connection string
                    if (string.IsNullOrWhiteSpace(azureConfig?.ConnectionString))
                    {
                        result.AddWarning($"Storage profile '{profile.Key}' has no connection string configured");
                    }
                    else
                    {
                        // Basic Azure connection string validation
                        if (!azureConfig.ConnectionString.Contains("AccountName=", StringComparison.OrdinalIgnoreCase) &&
                            !azureConfig.ConnectionString.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
                        {
                            result.AddWarning($"Storage profile '{profile.Key}' connection string may be invalid (missing AccountName or development storage flag)");
                        }
                    }

                    // Validate container name
                    if (string.IsNullOrWhiteSpace(azureConfig?.ContainerName))
                    {
                        result.AddInfo($"Storage profile '{profile.Key}' has no container name specified (will default to 'attachments')");
                    }
                    else if (!IsValidContainerName(azureConfig.ContainerName))
                    {
                        result.AddError($"Storage profile '{profile.Key}' has invalid container name '{azureConfig.ContainerName}'. Container names must be 3-63 characters, lowercase letters, numbers, and hyphens only, and must not start or end with a hyphen.");
                    }
                }
            }
            else
            {
                result.AddInfo("No Azure Blob storage profiles found in configuration (plugin will be available but unused)");
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

        return new AzureBlobAttachmentStoreProvider(loggerFactory);
    }

    /// <summary>
    /// Validates Azure Blob Storage container naming rules.
    /// Container names must be 3-63 characters long, contain only lowercase letters, numbers, and hyphens,
    /// and must not start or end with a hyphen. Consecutive hyphens are not allowed.
    /// </summary>
    /// <param name="containerName">The container name to validate.</param>
    /// <returns>True if the container name is valid, false otherwise.</returns>
    private static bool IsValidContainerName(string containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
            return false;

        if (containerName.Length < 3 || containerName.Length > 63)
            return false;

        if (containerName.StartsWith('-') || containerName.EndsWith('-'))
            return false;

        if (containerName.Contains("--"))
            return false;

        return containerName.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-');
    }
}
