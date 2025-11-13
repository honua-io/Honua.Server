// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Google.Cloud.Storage.V1;
using Honua.Server.Core.Attachments;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.HealthChecks;

/// <summary>
/// Health check for cloud storage providers (S3, Azure Blob, Google Cloud Storage).
/// Tests connectivity and basic operations for configured storage backends.
/// </summary>
public class StorageHealthCheck : IHealthCheck
{
    private readonly IAttachmentStoreSelector attachmentStoreSelector;
    private readonly ILogger<StorageHealthCheck> logger;
    private readonly IConfiguration configuration;

    public StorageHealthCheck(
        IAttachmentStoreSelector attachmentStoreSelector,
        ILogger<StorageHealthCheck> logger,
        IConfiguration configuration)
    {
        this.attachmentStoreSelector = attachmentStoreSelector;
        this.logger = logger;
        this.configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var storageProviders = new List<string>();

        try
        {
            // Check if S3 is configured
            var s3Configured = !string.IsNullOrWhiteSpace(_configuration["AWS:S3:BucketName"]) ||
                              !string.IsNullOrWhiteSpace(_configuration["honua:attachments:s3:bucketName"]);

            // Check if Azure Blob is configured
            var azureConfigured = !string.IsNullOrWhiteSpace(_configuration["Azure:Storage:ConnectionString"]) ||
                                 !string.IsNullOrWhiteSpace(_configuration["honua:attachments:azure:connectionString"]);

            // Check if Google Cloud Storage is configured
            var gcsConfigured = !string.IsNullOrWhiteSpace(_configuration["GCP:Storage:BucketName"]) ||
                               !string.IsNullOrWhiteSpace(_configuration["honua:attachments:gcs:bucketName"]);

            // Check if filesystem storage is configured
            var fileSystemConfigured = !string.IsNullOrWhiteSpace(_configuration["honua:attachments:fileSystem:basePath"]);

            // Test S3 if configured
            if (s3Configured)
            {
                var s3Result = await CheckS3HealthAsync(cancellationToken);
                storageProviders.Add($"S3 ({s3Result.status})");
                data["s3Status"] = s3Result.status;
                if (s3Result.details != null)
                {
                    data["s3Details"] = s3Result.details;
                }

                if (s3Result.status != "Healthy")
                {
                    this.logger.LogWarning("S3 storage health check failed: {Status}", s3Result.status);
                }
            }

            // Test Azure Blob if configured
            if (azureConfigured)
            {
                var azureResult = await CheckAzureBlobHealthAsync(cancellationToken);
                storageProviders.Add($"AzureBlob ({azureResult.status})");
                data["azureStatus"] = azureResult.status;
                if (azureResult.details != null)
                {
                    data["azureDetails"] = azureResult.details;
                }

                if (azureResult.status != "Healthy")
                {
                    this.logger.LogWarning("Azure Blob storage health check failed: {Status}", azureResult.status);
                }
            }

            // Test Google Cloud Storage if configured
            if (gcsConfigured)
            {
                var gcsResult = await CheckGcsHealthAsync(cancellationToken);
                storageProviders.Add($"GCS ({gcsResult.status})");
                data["gcsStatus"] = gcsResult.status;
                if (gcsResult.details != null)
                {
                    data["gcsDetails"] = gcsResult.details;
                }

                if (gcsResult.status != "Healthy")
                {
                    this.logger.LogWarning("Google Cloud Storage health check failed: {Status}", gcsResult.status);
                }
            }

            // Test filesystem if configured
            if (fileSystemConfigured)
            {
                var fsResult = CheckFileSystemHealth();
                storageProviders.Add($"FileSystem ({fsResult.status})");
                data["fileSystemStatus"] = fsResult.status;
                if (fsResult.details != null)
                {
                    data["fileSystemDetails"] = fsResult.details;
                }

                if (fsResult.status != "Healthy")
                {
                    this.logger.LogWarning("Filesystem storage health check failed: {Status}", fsResult.status);
                }
            }

            data["configuredProviders"] = storageProviders;

            // If no storage providers are configured, return degraded
            if (storageProviders.Count == 0)
            {
                this.logger.LogWarning("No storage providers configured");
                return HealthCheckResult.Degraded(
                    "No storage providers configured",
                    data: data);
            }

            // Check if all configured providers are healthy
            var unhealthyCount = storageProviders.Count(p => !p.Contains("(Healthy)"));
            if (unhealthyCount == 0)
            {
                return HealthCheckResult.Healthy(
                    $"All {storageProviders.Count} storage provider(s) are operational",
                    data: data);
            }
            else if (unhealthyCount < storageProviders.Count)
            {
                return HealthCheckResult.Degraded(
                    $"{unhealthyCount} of {storageProviders.Count} storage provider(s) are unavailable",
                    data: data);
            }
            else
            {
                return HealthCheckResult.Unhealthy(
                    "All configured storage providers are unavailable",
                    data: data);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Storage health check failed");
            return HealthCheckResult.Unhealthy(
                "Storage health check failed: " + ex.Message,
                exception: ex,
                data: data);
        }
    }

    private async Task<(string status, Dictionary<string, object>? details)> CheckS3HealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var bucketName = _configuration["AWS:S3:BucketName"] ?? _configuration["honua:attachments:s3:bucketName"];
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                return ("NotConfigured", null);
            }

            // Try to get S3 client from DI (if registered)
            // This is a lightweight check - we just verify we can list objects
            var s3Client = new AmazonS3Client();
            var request = new ListObjectsV2Request
            {
                BucketName = bucketName,
                MaxKeys = 1
            };

            var response = await s3Client.ListObjectsV2Async(request, cancellationToken);

            var details = new Dictionary<string, object>
            {
                ["bucketName"] = bucketName,
                ["region"] = s3Client.Config.RegionEndpoint?.SystemName ?? "unknown"
            };

            return ("Healthy", details);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // Bucket exists but we don't have permissions - still considered connected
            return ("Degraded", new Dictionary<string, object> { ["error"] = "Permission denied" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "S3 health check failed");
            return ("Unhealthy", new Dictionary<string, object> { ["error"] = ex.Message });
        }
    }

    private async Task<(string status, Dictionary<string, object>? details)> CheckAzureBlobHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = _configuration["Azure:Storage:ConnectionString"] ??
                                  _configuration["honua:attachments:azure:connectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return ("NotConfigured", null);
            }

            var containerName = _configuration["Azure:Storage:ContainerName"] ??
                               _configuration["honua:attachments:azure:containerName"] ??
                               "attachments";

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Check if container exists
            var exists = await containerClient.ExistsAsync(cancellationToken);

            var details = new Dictionary<string, object>
            {
                ["containerName"] = containerName,
                ["exists"] = exists.Value
            };

            return (exists.Value ? "Healthy" : "Degraded", details);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Azure Blob health check failed");
            return ("Unhealthy", new Dictionary<string, object> { ["error"] = ex.Message });
        }
    }

    private async Task<(string status, Dictionary<string, object>? details)> CheckGcsHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var bucketName = _configuration["GCP:Storage:BucketName"] ??
                            _configuration["honua:attachments:gcs:bucketName"];
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                return ("NotConfigured", null);
            }

            var client = await StorageClient.CreateAsync();

            // Try to get bucket metadata (lightweight operation)
            var bucket = await client.GetBucketAsync(bucketName, cancellationToken: cancellationToken);

            var details = new Dictionary<string, object>
            {
                ["bucketName"] = bucketName,
                ["location"] = bucket.Location ?? "unknown"
            };

            return ("Healthy", details);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return ("Degraded", new Dictionary<string, object> { ["error"] = "Permission denied" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Google Cloud Storage health check failed");
            return ("Unhealthy", new Dictionary<string, object> { ["error"] = ex.Message });
        }
    }

    private (string status, Dictionary<string, object>? details) CheckFileSystemHealth()
    {
        try
        {
            var basePath = _configuration["honua:attachments:fileSystem:basePath"];
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return ("NotConfigured", null);
            }

            // Check if directory exists and is writable
            var directoryInfo = new DirectoryInfo(basePath);
            if (!directoryInfo.Exists)
            {
                return ("Degraded", new Dictionary<string, object>
                {
                    ["basePath"] = basePath,
                    ["error"] = "Directory does not exist"
                });
            }

            // Try to write a test file
            var testFile = Path.Combine(basePath, $".healthcheck-{Guid.NewGuid()}.tmp");
            try
            {
                File.WriteAllText(testFile, "healthcheck");
                File.Delete(testFile);

                var details = new Dictionary<string, object>
                {
                    ["basePath"] = basePath,
                    ["writable"] = true
                };

                return ("Healthy", details);
            }
            catch (UnauthorizedAccessException)
            {
                return ("Degraded", new Dictionary<string, object>
                {
                    ["basePath"] = basePath,
                    ["error"] = "Directory is not writable"
                });
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Filesystem health check failed");
            return ("Unhealthy", new Dictionary<string, object> { ["error"] = ex.Message });
        }
    }
}
