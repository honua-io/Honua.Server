// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Server.Enterprise.Geoprocessing.Executors;

/// <summary>
/// SNS message wrapper for AWS notifications
/// </summary>
public class SnsMessage
{
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("MessageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("TopicArn")]
    public string? TopicArn { get; set; }

    [JsonPropertyName("Subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("Timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("SignatureVersion")]
    public string? SignatureVersion { get; set; }

    [JsonPropertyName("Signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("SigningCertURL")]
    public string? SigningCertURL { get; set; }

    [JsonPropertyName("UnsubscribeURL")]
    public string? UnsubscribeURL { get; set; }

    [JsonPropertyName("Token")]
    public string? Token { get; set; }

    [JsonPropertyName("SubscribeURL")]
    public string? SubscribeURL { get; set; }
}

/// <summary>
/// AWS Batch Job State Change event
/// EventBridge event sent when batch job status changes
/// </summary>
public class BatchJobStateChangeEvent
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("detail-type")]
    public string? DetailType { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("account")]
    public string? Account { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("resources")]
    public List<string> Resources { get; set; } = new();

    [JsonPropertyName("detail")]
    public required BatchJobStateChangeDetail Detail { get; set; }
}

/// <summary>
/// Details of the batch job state change
/// </summary>
public class BatchJobStateChangeDetail
{
    [JsonPropertyName("jobArn")]
    public string? JobArn { get; set; }

    [JsonPropertyName("jobName")]
    public string? JobName { get; set; }

    [JsonPropertyName("jobId")]
    public required string JobId { get; set; }

    [JsonPropertyName("jobQueue")]
    public string? JobQueue { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("statusReason")]
    public string? StatusReason { get; set; }

    [JsonPropertyName("createdAt")]
    public long? CreatedAt { get; set; }

    [JsonPropertyName("startedAt")]
    public long? StartedAt { get; set; }

    [JsonPropertyName("stoppedAt")]
    public long? StoppedAt { get; set; }

    [JsonPropertyName("attempts")]
    public List<BatchJobAttempt> Attempts { get; set; } = new();

    [JsonPropertyName("container")]
    public BatchJobContainer? Container { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>
/// Batch job attempt details
/// </summary>
public class BatchJobAttempt
{
    [JsonPropertyName("container")]
    public BatchJobAttemptContainer? Container { get; set; }

    [JsonPropertyName("startedAt")]
    public long? StartedAt { get; set; }

    [JsonPropertyName("stoppedAt")]
    public long? StoppedAt { get; set; }

    [JsonPropertyName("statusReason")]
    public string? StatusReason { get; set; }
}

/// <summary>
/// Container details from job attempt
/// </summary>
public class BatchJobAttemptContainer
{
    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("logStreamName")]
    public string? LogStreamName { get; set; }

    [JsonPropertyName("containerInstanceArn")]
    public string? ContainerInstanceArn { get; set; }

    [JsonPropertyName("taskArn")]
    public string? TaskArn { get; set; }

    [JsonPropertyName("networkInterfaces")]
    public List<NetworkInterface> NetworkInterfaces { get; set; } = new();
}

/// <summary>
/// Container configuration from job definition
/// </summary>
public class BatchJobContainer
{
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("vcpus")]
    public int? Vcpus { get; set; }

    [JsonPropertyName("memory")]
    public int? Memory { get; set; }

    [JsonPropertyName("command")]
    public List<string> Command { get; set; } = new();

    [JsonPropertyName("environment")]
    public List<BatchEnvironmentVariable> Environment { get; set; } = new();

    [JsonPropertyName("logStreamName")]
    public string? LogStreamName { get; set; }

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// Environment variable for batch job
/// </summary>
public class BatchEnvironmentVariable
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

/// <summary>
/// Network interface details
/// </summary>
public class NetworkInterface
{
    [JsonPropertyName("attachmentId")]
    public string? AttachmentId { get; set; }

    [JsonPropertyName("ipv6Address")]
    public string? Ipv6Address { get; set; }

    [JsonPropertyName("privateIpv4Address")]
    public string? PrivateIpv4Address { get; set; }
}

/// <summary>
/// Job input/output data structure for S3 staging
/// </summary>
public class BatchJobInput
{
    [JsonPropertyName("jobId")]
    public required string JobId { get; set; }

    [JsonPropertyName("processId")]
    public required string ProcessId { get; set; }

    [JsonPropertyName("tenantId")]
    public required Guid TenantId { get; set; }

    [JsonPropertyName("userId")]
    public required Guid UserId { get; set; }

    [JsonPropertyName("inputs")]
    public required Dictionary<string, object> Inputs { get; set; }

    [JsonPropertyName("responseFormat")]
    public string ResponseFormat { get; set; } = "geojson";

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Job output data structure from S3
/// </summary>
public class BatchJobOutput
{
    [JsonPropertyName("jobId")]
    public required string JobId { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("output")]
    public Dictionary<string, object>? Output { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("errorDetails")]
    public string? ErrorDetails { get; set; }

    [JsonPropertyName("featuresProcessed")]
    public long? FeaturesProcessed { get; set; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}
