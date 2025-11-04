// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Health;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Health;

/// <summary>
/// Health check for Azure Blob Storage connectivity and access.
/// Verifies Azure credentials and container access permissions.
/// </summary>
public sealed class AzureBlobHealthCheck : HealthCheckBase
{
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly string? _testContainer;

    public AzureBlobHealthCheck(
        ILogger<AzureBlobHealthCheck> logger,
        BlobServiceClient? blobServiceClient = null,
        string? testContainer = null)
        : base(logger)
    {
        _blobServiceClient = blobServiceClient;
        _testContainer = testContainer;
    }

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_blobServiceClient == null)
            {
                data["azure_blob.configured"] = false;
                Logger.LogDebug("Azure Blob Storage client not configured - Azure storage unavailable");
                return HealthCheckResult.Healthy(
                    "Azure Blob Storage not configured (optional dependency)",
                    data);
            }

            data["azure_blob.configured"] = true;
            data["azure_blob.account_name"] = _blobServiceClient.AccountName;

            // Test basic connectivity by getting account info
            var accountInfo = await _blobServiceClient.GetAccountInfoAsync(cancellationToken);
            data["azure_blob.account_kind"] = accountInfo.Value.AccountKind.ToString();
            data["azure_blob.sku_name"] = accountInfo.Value.SkuName.ToString();
            data["azure_blob.can_access_account"] = true;

            // If a test container is specified, verify access to it
            if (_testContainer.HasValue())
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(_testContainer);
                    var exists = await containerClient.ExistsAsync(cancellationToken);

                    data["azure_blob.test_container"] = _testContainer;
                    data["azure_blob.test_container_exists"] = exists.Value;

                    if (exists.Value)
                    {
                        var properties = await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                        data["azure_blob.test_container_accessible"] = true;
                        data["azure_blob.test_container_public_access"] = properties.Value.PublicAccess.ToString();

                        Logger.LogDebug(
                            "Azure Blob health check passed. Account: {AccountName}, Test container: {TestContainer}",
                            _blobServiceClient.AccountName,
                            _testContainer);
                    }
                    else
                    {
                        data["azure_blob.test_container_accessible"] = false;
                        Logger.LogWarning(
                            "Azure Blob test container does not exist: {TestContainer}",
                            _testContainer);

                        return HealthCheckResult.Degraded(
                            $"Azure Blob connected but test container '{_testContainer}' does not exist",
                            data: data);
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 403)
                {
                    data["azure_blob.test_container"] = _testContainer;
                    data["azure_blob.test_container_accessible"] = false;
                    data["azure_blob.error"] = "Access denied to test container";

                    Logger.LogWarning(ex,
                        "Azure Blob test container access denied: {TestContainer}",
                        _testContainer);

                    return HealthCheckResult.Degraded(
                        $"Azure Blob connected but access denied to test container '{_testContainer}'",
                        data: data);
                }
            }

            return HealthCheckResult.Healthy(
                "Azure Blob Storage accessible",
                data);
        }
        catch (RequestFailedException ex)
        {
            data["azure_blob.configured"] = true;
            data["azure_blob.error"] = ex.Message;
            data["azure_blob.status_code"] = ex.Status;
            data["azure_blob.error_code"] = ex.ErrorCode;

            Logger.LogExternalServiceFailure(ex, "Azure Blob Storage", "health check");

            return HealthCheckResult.Unhealthy(
                $"Azure Blob service error: {ex.ErrorCode}",
                ex,
                data);
        }
    }

    protected override HealthCheckResult CreateUnhealthyResult(Exception ex, Dictionary<string, object> data)
    {
        data["azure_blob.configured"] = true;
        data["azure_blob.error"] = ex.Message;

        Logger.LogOperationFailure(ex, "Azure Blob health check");

        return HealthCheckResult.Unhealthy(
            "Azure Blob health check failed",
            ex,
            data);
    }
}
