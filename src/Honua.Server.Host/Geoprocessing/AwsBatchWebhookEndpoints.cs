// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Enterprise.Geoprocessing.Executors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Geoprocessing;

/// <summary>
/// Webhook endpoints for AWS Batch job completion notifications
/// Receives SNS notifications when batch jobs complete, fail, or change status
/// </summary>
public static class AwsBatchWebhookEndpoints
{
    public static IEndpointRouteBuilder MapAwsBatchWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/geoprocessing/webhooks")
            .WithTags("Geoprocessing Webhooks")
            .AllowAnonymous(); // SNS doesn't support authentication headers

        // POST /api/geoprocessing/webhooks/batch-complete
        // Receives SNS notifications for AWS Batch job state changes
        group.MapPost("/batch-complete", HandleBatchJobComplete)
            .WithName("HandleBatchJobComplete")
            .WithOpenApi(op =>
            {
                op.Summary = "AWS Batch job completion webhook";
                op.Description = "Receives SNS notifications when AWS Batch jobs complete, fail, or change status. This endpoint is called by AWS SNS.";
                return op;
            });

        return endpoints;
    }

    private static async Task<IResult> HandleBatchJobComplete(
        HttpContext context,
        [FromServices] ILogger<AwsBatchExecutor> logger,
        [FromServices] AwsBatchExecutor? batchExecutor)
    {
        try
        {
            // Read raw request body
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                logger.LogWarning("Received empty webhook body");
                return Results.BadRequest(new { error = "Empty request body" });
            }

            logger.LogDebug("Received AWS Batch webhook: {Body}", body);

            // Parse SNS message
            var snsMessage = JsonSerializer.Deserialize<SnsMessage>(body);
            if (snsMessage == null)
            {
                logger.LogWarning("Failed to parse SNS message");
                return Results.BadRequest(new { error = "Invalid SNS message format" });
            }

            // Handle SNS subscription confirmation
            if (snsMessage.Type == "SubscriptionConfirmation")
            {
                logger.LogInformation("Received SNS subscription confirmation");

                // In production, you would:
                // 1. Verify the signature
                // 2. Call the SubscribeURL to confirm the subscription
                // For now, just log it
                logger.LogInformation("SNS SubscribeURL: {SubscribeURL}", snsMessage.SubscribeURL);

                return Results.Ok(new { message = "Subscription confirmation received" });
            }

            // Handle notification
            if (snsMessage.Type == "Notification")
            {
                logger.LogInformation("Processing SNS notification");

                // TODO: Verify SNS message signature for security
                // This is critical in production to prevent spoofing
                // See: https://docs.aws.amazon.com/sns/latest/dg/sns-verify-signature-of-message.html

                // Parse the nested EventBridge event
                var eventJson = snsMessage.Message;
                var batchEvent = JsonSerializer.Deserialize<BatchJobStateChangeEvent>(eventJson);

                if (batchEvent == null || batchEvent.Detail == null)
                {
                    logger.LogWarning("Failed to parse Batch job state change event");
                    return Results.BadRequest(new { error = "Invalid Batch event format" });
                }

                logger.LogInformation(
                    "Batch job {JobId} status changed to {Status}",
                    batchEvent.Detail.JobId,
                    batchEvent.Detail.Status);

                // Convert to CloudBatchJobStatus
                var jobStatus = new CloudBatchJobStatus
                {
                    CloudJobId = batchEvent.Detail.JobId,
                    HonuaJobId = batchEvent.Detail.Parameters.TryGetValue("job_id", out var jobId) ? jobId : "unknown",
                    Status = batchEvent.Detail.Status,
                    Message = batchEvent.Detail.StatusReason,
                    StartedAt = batchEvent.Detail.StartedAt.HasValue
                        ? DateTimeOffset.FromUnixTimeMilliseconds(batchEvent.Detail.StartedAt.Value)
                        : null,
                    CompletedAt = batchEvent.Detail.StoppedAt.HasValue
                        ? DateTimeOffset.FromUnixTimeMilliseconds(batchEvent.Detail.StoppedAt.Value)
                        : null,
                    ErrorMessage = batchEvent.Detail.Status == "FAILED" ? batchEvent.Detail.StatusReason : null
                };

                // Extract exit code from attempts if available
                if (batchEvent.Detail.Attempts.Count > 0)
                {
                    var lastAttempt = batchEvent.Detail.Attempts[^1];
                    if (lastAttempt.Container?.ExitCode.HasValue == true)
                    {
                        jobStatus = jobStatus with { ExitCode = lastAttempt.Container.ExitCode.Value };
                    }
                }

                // Handle completion via executor
                if (batchExecutor != null)
                {
                    // Only handle terminal states (SUCCEEDED, FAILED)
                    if (batchEvent.Detail.Status is "SUCCEEDED" or "FAILED")
                    {
                        await batchExecutor.HandleCompletionNotificationAsync(
                            batchEvent.Detail.JobId,
                            jobStatus,
                            context.RequestAborted);

                        logger.LogInformation(
                            "Successfully processed completion for job {JobId}",
                            batchEvent.Detail.JobId);
                    }
                    else
                    {
                        logger.LogDebug(
                            "Ignoring non-terminal status {Status} for job {JobId}",
                            batchEvent.Detail.Status,
                            batchEvent.Detail.JobId);
                    }
                }
                else
                {
                    logger.LogWarning("AwsBatchExecutor not registered - cannot process completion");
                }

                return Results.Ok(new { message = "Notification processed successfully" });
            }

            logger.LogWarning("Unknown SNS message type: {Type}", snsMessage.Type);
            return Results.BadRequest(new { error = $"Unknown message type: {snsMessage.Type}" });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse webhook JSON");
            return Results.BadRequest(new { error = "Invalid JSON format", details = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing AWS Batch webhook");
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Webhook processing failed");
        }
    }
}
