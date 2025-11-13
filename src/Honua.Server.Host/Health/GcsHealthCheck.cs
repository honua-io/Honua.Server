// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Health;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Health;

/// <summary>
/// Health check for Google Cloud Storage connectivity and access.
/// Verifies GCS credentials and bucket access permissions.
/// </summary>
public sealed class GcsHealthCheck : HealthCheckBase
{
    private readonly StorageClient? storageClient;
    private readonly string? testBucket;

    public GcsHealthCheck(
        ILogger<GcsHealthCheck> logger,
        StorageClient? storageClient = null,
        string? testBucket = null)
        : base(logger)
    {
        this.storageClient = storageClient;
        this.testBucket = testBucket;
    }

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_storageClient == null)
            {
                data["gcs.configured"] = false;
                Logger.LogDebug("Google Cloud Storage client not configured - GCS storage unavailable");
                return HealthCheckResult.Healthy(
                    "Google Cloud Storage not configured (optional dependency)",
                    data);
            }

            data["gcs.configured"] = true;

            // Test basic connectivity by attempting to access service
            // Note: GCS doesn't have a direct "ping" endpoint, so we test with bucket operations

            // If a test bucket is specified, verify access to it
            if (this.testBucket.HasValue())
            {
                try
                {
                    var bucket = await this.storageClient.GetBucketAsync(
                        _testBucket,
                        cancellationToken: cancellationToken);

                    data["gcs.test_bucket"] = _testBucket;
                    data["gcs.test_bucket_accessible"] = true;
                    data["gcs.test_bucket_location"] = bucket.Location;
                    data["gcs.test_bucket_storage_class"] = bucket.StorageClass;

                    Logger.LogDebug(
                        "GCS health check passed. Test bucket: {TestBucket}, Location: {Location}",
                        _testBucket,
                        bucket.Location);

                    return HealthCheckResult.Healthy(
                        "Google Cloud Storage accessible",
                        data);
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    data["gcs.test_bucket"] = _testBucket;
                    data["gcs.test_bucket_accessible"] = false;
                    data["gcs.error"] = "Test bucket not found";

                    Logger.LogResourceNotFound("GCS test bucket", _testBucket);

                    return HealthCheckResult.Degraded(
                        $"GCS connected but test bucket '{_testBucket}' not found",
                        data: data);
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    data["gcs.test_bucket"] = _testBucket;
                    data["gcs.test_bucket_accessible"] = false;
                    data["gcs.error"] = "Access denied to test bucket";

                    Logger.LogWarning(ex,
                        "GCS test bucket access denied: {TestBucket}",
                        _testBucket);

                    return HealthCheckResult.Degraded(
                        $"GCS connected but access denied to test bucket '{_testBucket}'",
                        data: data);
                }
            }
            else
            {
                // No test bucket specified - just verify we can create a client
                data["gcs.client_created"] = true;
                Logger.LogDebug("GCS health check passed. Client configured but no test bucket specified.");

                return HealthCheckResult.Healthy(
                    "Google Cloud Storage client configured",
                    data);
            }
        }
        catch (Google.GoogleApiException ex)
        {
            data["gcs.configured"] = true;
            data["gcs.error"] = ex.Message;
            data["gcs.status_code"] = (int?)ex.HttpStatusCode;
            data["gcs.error_code"] = ex.Error?.Code;

            Logger.LogError(ex, "GCS service error: {ErrorCode}", ex.Error?.Code);
            return HealthCheckResult.Unhealthy(
                $"GCS service error: {(ex.Error != null ? ex.Error.Code.ToString() : "Unknown")}", ex, data);
        }
    }

    protected override HealthCheckResult CreateUnhealthyResult(Exception ex, Dictionary<string, object> data)
    {
        data["gcs.configured"] = true;
        data["gcs.error"] = ex.Message;

        return HealthCheckResult.Unhealthy("GCS health check failed", ex, data);
    }
}
