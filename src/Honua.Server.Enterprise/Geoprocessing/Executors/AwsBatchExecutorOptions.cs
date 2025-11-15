// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Enterprise.Geoprocessing.Executors;

/// <summary>
/// Configuration options for AWS Batch integration
/// </summary>
public class AwsBatchExecutorOptions
{
    /// <summary>
    /// AWS region (e.g., us-east-1, us-west-2)
    /// If null, uses default AWS SDK credential chain region
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// S3 bucket for input staging
    /// </summary>
    public required string InputBucket { get; set; }

    /// <summary>
    /// S3 bucket for output results
    /// </summary>
    public required string OutputBucket { get; set; }

    /// <summary>
    /// AWS Batch job queue name
    /// </summary>
    public required string JobQueue { get; set; }

    /// <summary>
    /// AWS Batch job definition ARN or name
    /// Format: arn:aws:batch:region:account-id:job-definition/name:revision
    /// Or just: name:revision (will use latest if only name provided)
    /// </summary>
    public required string JobDefinition { get; set; }

    /// <summary>
    /// SNS topic ARN for job completion notifications
    /// Optional - if not provided, polling will be used
    /// </summary>
    public string? SnsTopicArn { get; set; }

    /// <summary>
    /// CloudWatch log group name for job logs
    /// </summary>
    public string? LogGroupName { get; set; }

    /// <summary>
    /// Default job timeout in seconds (default: 1800 = 30 minutes)
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 1800;

    /// <summary>
    /// Number of retry attempts for failed jobs (default: 3)
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Enable Spot instances for cost optimization (default: true)
    /// </summary>
    public bool EnableSpotInstances { get; set; } = true;

    /// <summary>
    /// Maximum Spot instance price as percentage of On-Demand price (default: 100)
    /// </summary>
    public int SpotMaxPricePercentage { get; set; } = 100;

    /// <summary>
    /// Callback URL base for SNS webhooks
    /// Example: https://honua.example.com
    /// The webhook endpoint will be: {CallbackUrl}/api/geoprocessing/webhooks/batch-complete
    /// </summary>
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Job status polling interval in seconds (default: 10)
    /// Only used if SNS notifications are not configured
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum polling duration in seconds (default: 3600 = 1 hour)
    /// After this time, job will be marked as timeout
    /// </summary>
    public int MaxPollingDurationSeconds { get; set; } = 3600;

    /// <summary>
    /// S3 lifecycle policy - days to keep job data (default: 7)
    /// Set to 0 to disable automatic cleanup
    /// </summary>
    public int S3RetentionDays { get; set; } = 7;

    /// <summary>
    /// Container image override (optional)
    /// If specified, overrides the image defined in the job definition
    /// </summary>
    public string? ContainerImageOverride { get; set; }

    /// <summary>
    /// vCPU allocation per job (optional)
    /// If not specified, uses job definition defaults
    /// </summary>
    public int? VCpus { get; set; }

    /// <summary>
    /// Memory allocation in MB per job (optional)
    /// If not specified, uses job definition defaults
    /// </summary>
    public int? MemoryMB { get; set; }

    /// <summary>
    /// GPU count per job (optional, for GPU-accelerated operations)
    /// </summary>
    public int? Gpus { get; set; }

    /// <summary>
    /// Additional environment variables to pass to batch jobs
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Tags to apply to batch jobs for cost allocation and tracking
    /// </summary>
    public Dictionary<string, string> JobTags { get; set; } = new();

    /// <summary>
    /// Enable CloudWatch Logs streaming (default: true)
    /// </summary>
    public bool EnableCloudWatchLogs { get; set; } = true;

    /// <summary>
    /// Enable detailed CloudWatch metrics (default: false)
    /// May incur additional costs
    /// </summary>
    public bool EnableDetailedMetrics { get; set; } = false;

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InputBucket))
            throw new InvalidOperationException("InputBucket is required");

        if (string.IsNullOrWhiteSpace(OutputBucket))
            throw new InvalidOperationException("OutputBucket is required");

        if (string.IsNullOrWhiteSpace(JobQueue))
            throw new InvalidOperationException("JobQueue is required");

        if (string.IsNullOrWhiteSpace(JobDefinition))
            throw new InvalidOperationException("JobDefinition is required");

        if (DefaultTimeoutSeconds <= 0)
            throw new InvalidOperationException("DefaultTimeoutSeconds must be greater than 0");

        if (RetryAttempts < 0)
            throw new InvalidOperationException("RetryAttempts cannot be negative");

        if (SpotMaxPricePercentage <= 0 || SpotMaxPricePercentage > 100)
            throw new InvalidOperationException("SpotMaxPricePercentage must be between 1 and 100");

        if (PollingIntervalSeconds <= 0)
            throw new InvalidOperationException("PollingIntervalSeconds must be greater than 0");

        if (MaxPollingDurationSeconds <= 0)
            throw new InvalidOperationException("MaxPollingDurationSeconds must be greater than 0");

        if (S3RetentionDays < 0)
            throw new InvalidOperationException("S3RetentionDays cannot be negative");

        if (VCpus.HasValue && VCpus.Value <= 0)
            throw new InvalidOperationException("VCpus must be greater than 0 if specified");

        if (MemoryMB.HasValue && MemoryMB.Value <= 0)
            throw new InvalidOperationException("MemoryMB must be greater than 0 if specified");

        if (Gpus.HasValue && Gpus.Value < 0)
            throw new InvalidOperationException("Gpus cannot be negative");
    }
}
