// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Health;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Health;

/// <summary>
/// Health check for Amazon S3 connectivity and access.
/// Verifies S3 credentials and bucket access permissions.
/// </summary>
public sealed class S3HealthCheck : HealthCheckBase
{
    private readonly IAmazonS3? s3Client;
    private readonly string? testBucket;

    public S3HealthCheck(
        ILogger<S3HealthCheck> logger,
        IAmazonS3? s3Client = null,
        string? testBucket = null)
        : base(logger)
    {
        this.s3Client = s3Client;
        this.testBucket = testBucket;
    }

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_s3Client == null)
            {
                data["s3.configured"] = false;
                Logger.LogDebug("S3 client not configured - S3 storage unavailable");
                return HealthCheckResult.Healthy(
                    "S3 not configured (optional dependency)",
                    data);
            }

            data["s3.configured"] = true;

            // Test basic connectivity by listing buckets
            var listBucketsResponse = await this.s3Client.ListBucketsAsync(cancellationToken);
            data["s3.buckets_accessible"] = listBucketsResponse.Buckets.Count;
            data["s3.can_list_buckets"] = true;

            // If a test bucket is specified, verify access to it
            if (this.testBucket.HasValue())
            {
                try
                {
                    var headBucketRequest = new GetBucketLocationRequest
                    {
                        BucketName = _testBucket
                    };
                    var location = await this.s3Client.GetBucketLocationAsync(headBucketRequest, cancellationToken);

                    data["s3.test_bucket"] = _testBucket;
                    data["s3.test_bucket_accessible"] = true;
                    data["s3.test_bucket_region"] = location.Location.ToString();

                    Logger.LogDebug(
                        "S3 health check passed. Buckets: {BucketCount}, Test bucket: {TestBucket}",
                        listBucketsResponse.Buckets.Count,
                        _testBucket);
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    data["s3.test_bucket"] = _testBucket;
                    data["s3.test_bucket_accessible"] = false;
                    data["s3.error"] = "Test bucket not found";

                    Logger.LogResourceNotFound("S3 test bucket", _testBucket);

                    return HealthCheckResult.Degraded(
                        $"S3 connected but test bucket '{_testBucket}' not found",
                        data: data);
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    data["s3.test_bucket"] = _testBucket;
                    data["s3.test_bucket_accessible"] = false;
                    data["s3.error"] = "Access denied to test bucket";

                    Logger.LogWarning(
                        "S3 test bucket access denied: {TestBucket}",
                        _testBucket);

                    return HealthCheckResult.Degraded(
                        $"S3 connected but access denied to test bucket '{_testBucket}'",
                        data: data);
                }
            }

            return HealthCheckResult.Healthy(
                "S3 storage accessible",
                data);
        }
        catch (AmazonS3Exception ex)
        {
            data["s3.configured"] = true;
            data["s3.error"] = ex.Message;
            data["s3.error_code"] = ex.ErrorCode;
            data["s3.status_code"] = (int)ex.StatusCode;

            Logger.LogError(ex, "S3 service error: {ErrorCode}", ex.ErrorCode);
            return HealthCheckResult.Unhealthy($"S3 service error: {ex.ErrorCode}", ex, data);
        }
    }

    protected override HealthCheckResult CreateUnhealthyResult(Exception ex, Dictionary<string, object> data)
    {
        data["s3.configured"] = true;
        data["s3.error"] = ex.Message;

        return HealthCheckResult.Unhealthy("S3 health check failed", ex, data);
    }
}
