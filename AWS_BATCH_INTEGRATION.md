# AWS Batch Integration for Geoprocessing

Production-ready AWS Batch integration for large-scale geoprocessing operations (Tier 3 - CloudBatch).

## Overview

The AWS Batch executor enables Honua to offload complex, long-running geoprocessing operations (10s-30min) to AWS Batch, leveraging:
- **S3 staging** for input/output data
- **SNS notifications** for job completion webhooks
- **CloudWatch Logs** for debugging and monitoring
- **Spot instances** for 50-90% cost savings
- **Auto-retry** on Spot interruptions

## Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Honua     │────▶│  S3 Inputs  │     │ AWS Batch   │
│   Server    │     └─────────────┘     │  Job Queue  │
│             │                         └─────────────┘
│             │                               │
│             │                               ▼
│             │                         ┌─────────────┐
│             │                         │   Compute   │
│             │◀────────────────────────│ Environment │
│             │    SNS Notification     │  (EC2/ECS)  │
└─────────────┘                         └─────────────┘
      │                                       │
      ▼                                       ▼
┌─────────────┐                         ┌─────────────┐
│ S3 Outputs  │◀────────────────────────│ CloudWatch  │
└─────────────┘                         │    Logs     │
                                        └─────────────┘
```

## Files Created/Modified

### New Files

1. **`src/Honua.Server.Enterprise/Geoprocessing/Executors/AwsBatchExecutor.cs`**
   - Production AWS Batch executor
   - S3 input staging, job submission, status tracking
   - Result retrieval, CloudWatch Logs integration

2. **`src/Honua.Server.Enterprise/Geoprocessing/Executors/AwsBatchExecutorOptions.cs`**
   - Configuration class for AWS Batch settings
   - Validation logic, sensible defaults
   - Cost optimization settings (Spot instances, lifecycle policies)

3. **`src/Honua.Server.Enterprise/Geoprocessing/Executors/AwsBatchModels.cs`**
   - SNS message models
   - BatchJobStateChange event models
   - Input/output staging data structures

4. **`src/Honua.Server.Host/Geoprocessing/AwsBatchWebhookEndpoints.cs`**
   - POST /api/geoprocessing/webhooks/batch-complete
   - Handles SNS notifications (subscription confirmation + job completion)
   - Verifies message format, calls executor completion handler

5. **`src/Honua.Server.Host/appsettings.AwsBatch.json`**
   - Comprehensive configuration template
   - Infrastructure setup guide
   - Cost optimization recommendations
   - Monitoring/alerting suggestions

### Modified Files

1. **`src/Honua.Server.Enterprise/Honua.Server.Enterprise.csproj`**
   - Added AWS SDK packages:
     - AWSSDK.Batch (4.0.3.6)
     - AWSSDK.S3 (4.0.11.3)
     - AWSSDK.SimpleNotificationService (4.0.2.5)
     - AWSSDK.CloudWatchLogs (4.0.9.3)
     - AWSSDK.Core (4.0.3.1)

2. **`src/Honua.Server.Enterprise/Geoprocessing/GeoprocessingServiceCollectionExtensions.cs`**
   - Added `AddAwsBatchExecutor(services, configuration)` extension method
   - Registers AWS SDK clients (IAM role-based credentials by default)
   - Registers AwsBatchExecutor as singleton + ICloudBatchExecutor

## Configuration

### appsettings.json

```json
{
  "Geoprocessing": {
    "AwsBatch": {
      "Region": "us-east-1",
      "InputBucket": "honua-geoprocessing-inputs",
      "OutputBucket": "honua-geoprocessing-outputs",
      "JobQueue": "honua-geoprocessing-queue",
      "JobDefinition": "honua-geoprocessing:1",
      "SnsTopicArn": "arn:aws:sns:us-east-1:123456789012:honua-batch-notifications",
      "LogGroupName": "/aws/batch/honua-geoprocessing",
      "CallbackUrl": "https://api.honua.example.com",
      "DefaultTimeoutSeconds": 1800,
      "RetryAttempts": 3,
      "EnableSpotInstances": true,
      "SpotMaxPricePercentage": 100,
      "S3RetentionDays": 7,
      "EnableCloudWatchLogs": true
    }
  }
}
```

See `appsettings.AwsBatch.json` for complete configuration reference.

### Service Registration

**Program.cs or Startup.cs:**

```csharp
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Host.Geoprocessing;

// Register AWS Batch executor
builder.Services.AddAwsBatchExecutor(builder.Configuration);

// Register webhook endpoints
app.MapAwsBatchWebhookEndpoints();
```

## Job Submission Flow

1. **Admission Control**: Control plane validates request, checks quotas
2. **Input Staging**: AwsBatchExecutor uploads job inputs to S3
   - `s3://bucket/jobs/{jobId}/inputs.json`
3. **Job Submission**: Submits job to AWS Batch with parameters:
   - `job_id`, `input_s3_bucket`, `input_s3_key`
   - `output_s3_bucket`, `output_s3_key`
   - `operation`, `tenant_id`, `user_id`, `callback_url`
4. **Status Tracking**: Job tracked in-memory, persisted to database
5. **SNS Notification**: AWS sends webhook when job completes
6. **Result Retrieval**: Executor downloads output from S3
7. **Completion**: Control plane marks job as completed/failed

## Result Retrieval Flow

### Via SNS Webhook (Recommended)

1. AWS Batch job completes/fails
2. EventBridge rule detects state change
3. SNS topic publishes notification
4. Webhook endpoint receives POST request
5. AwsBatchExecutor.HandleCompletionNotificationAsync() called
6. Downloads result from S3, updates control plane

### Via Polling (Fallback)

1. Worker service polls GetJobStatusAsync() periodically
2. When status is SUCCEEDED/FAILED:
   - Downloads result from S3
   - Updates control plane

## Error Handling

### S3 Upload Failures
- Job marked as FAILED immediately
- Error details logged, returned to user
- No Batch job submitted

### Batch Submission Failures
- AmazonBatchException caught, logged
- Job marked as FAILED
- User receives error message

### Timeout Scenarios
- Batch job timeout: Configured via `DefaultTimeoutSeconds`
- After timeout, job marked as FAILED
- Status reason: "Job exceeded timeout limit"

### Spot Instance Interruptions
- Auto-retry enabled via `RetryStrategy.EvaluateOnExit`
- Matches status reason: "Host EC2*"
- Automatically requeues job (up to `RetryAttempts` times)
- No manual intervention required

## Cost Optimization

### Spot Instances
- **Enable**: Set `EnableSpotInstances: true`
- **Savings**: 50-90% vs On-Demand
- **Trade-off**: Jobs may be interrupted, but auto-retry handles this
- **Best for**: Fault-tolerant, idempotent operations

### S3 Lifecycle Policies
- **Retention**: Set `S3RetentionDays` (default: 7 days)
- **Tags**: Jobs tagged with `retention-days` and `job-id`
- **Cleanup**: Use S3 lifecycle rules to auto-delete or transition to Glacier

### Right-Sizing
- **Monitor**: CloudWatch metrics (CPU, memory utilization)
- **Adjust**: `VCpus`, `MemoryMB` per operation type
- **Example**: Simple buffer → 2 vCPUs, complex union → 8 vCPUs

### Reserved Capacity
- For predictable workloads, purchase EC2 Savings Plans or Reserved Instances
- Can reduce costs by additional 30-50% on top of Spot savings

## AWS Infrastructure Setup

### 1. S3 Buckets

```bash
# Create input bucket
aws s3 mb s3://honua-geoprocessing-inputs --region us-east-1

# Create output bucket
aws s3 mb s3://honua-geoprocessing-outputs --region us-east-1

# Enable versioning (recommended)
aws s3api put-bucket-versioning \
  --bucket honua-geoprocessing-inputs \
  --versioning-configuration Status=Enabled

# Configure lifecycle policy (delete after 7 days)
cat > lifecycle-policy.json <<EOF
{
  "Rules": [{
    "Id": "DeleteOldJobs",
    "Status": "Enabled",
    "Filter": {
      "Tag": { "Key": "retention-days", "Value": "7" }
    },
    "Expiration": { "Days": 7 }
  }]
}
EOF

aws s3api put-bucket-lifecycle-configuration \
  --bucket honua-geoprocessing-inputs \
  --lifecycle-configuration file://lifecycle-policy.json
```

### 2. AWS Batch

```bash
# Create compute environment (Spot instances)
aws batch create-compute-environment \
  --compute-environment-name honua-geoprocessing-spot \
  --type MANAGED \
  --state ENABLED \
  --compute-resources \
    type=SPOT,\
    allocationStrategy=SPOT_CAPACITY_OPTIMIZED,\
    minvCpus=0,\
    maxvCpus=256,\
    desiredvCpus=0,\
    instanceTypes=optimal,\
    subnets=subnet-xxx,subnet-yyy,\
    securityGroupIds=sg-zzz,\
    instanceRole=arn:aws:iam::123456789012:instance-profile/ecsInstanceRole,\
    bidPercentage=100

# Create job queue
aws batch create-job-queue \
  --job-queue-name honua-geoprocessing-queue \
  --state ENABLED \
  --priority 10 \
  --compute-environment-order order=1,computeEnvironment=honua-geoprocessing-spot

# Register job definition (requires pre-built Docker image)
aws batch register-job-definition \
  --job-definition-name honua-geoprocessing \
  --type container \
  --container-properties \
    image=123456789012.dkr.ecr.us-east-1.amazonaws.com/honua-geoprocessing:latest,\
    vcpus=2,\
    memory=4096,\
    jobRoleArn=arn:aws:iam::123456789012:role/HonuaBatchJobRole,\
    logConfiguration="{logDriver=awslogs,options={awslogs-group=/aws/batch/honua-geoprocessing,awslogs-region=us-east-1}}"
```

### 3. SNS Topic

```bash
# Create SNS topic
aws sns create-topic \
  --name honua-batch-notifications \
  --region us-east-1

# Subscribe webhook endpoint
aws sns subscribe \
  --topic-arn arn:aws:sns:us-east-1:123456789012:honua-batch-notifications \
  --protocol https \
  --notification-endpoint https://api.honua.example.com/api/geoprocessing/webhooks/batch-complete
```

### 4. EventBridge Rule

```bash
# Create rule to match Batch job state changes
aws events put-rule \
  --name honua-batch-job-state-change \
  --event-pattern '{
    "source": ["aws.batch"],
    "detail-type": ["Batch Job State Change"],
    "detail": {
      "jobQueue": ["honua-geoprocessing-queue"]
    }
  }'

# Add SNS topic as target
aws events put-targets \
  --rule honua-batch-job-state-change \
  --targets Id=1,Arn=arn:aws:sns:us-east-1:123456789012:honua-batch-notifications
```

### 5. IAM Permissions

**Honua Server Role (ECS Task Role or EC2 Instance Profile):**

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "batch:SubmitJob",
        "batch:DescribeJobs",
        "batch:TerminateJob"
      ],
      "Resource": "*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "s3:PutObject",
        "s3:PutObjectTagging"
      ],
      "Resource": "arn:aws:s3:::honua-geoprocessing-inputs/*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject"
      ],
      "Resource": "arn:aws:s3:::honua-geoprocessing-outputs/*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "logs:GetLogEvents"
      ],
      "Resource": "arn:aws:logs:*:*:log-group:/aws/batch/honua-geoprocessing:*"
    }
  ]
}
```

**Batch Job Role:**

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject"
      ],
      "Resource": "arn:aws:s3:::honua-geoprocessing-inputs/*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "s3:PutObject"
      ],
      "Resource": "arn:aws:s3:::honua-geoprocessing-outputs/*"
    }
  ]
}
```

## Container Image Requirements

The AWS Batch job definition must reference a Docker image with:

1. **Geoprocessing Tools**: GDAL, GEOS, PROJ, PostGIS client libraries
2. **Runtime**: .NET 9.0 or Python 3.11+ (depending on implementation)
3. **Entrypoint Script**: Reads inputs from S3, executes operation, writes output to S3

**Example Dockerfile:**

```dockerfile
FROM osgeo/gdal:ubuntu-full-latest

# Install .NET or Python runtime
RUN apt-get update && apt-get install -y dotnet-runtime-9.0

# Copy geoprocessing worker binary
COPY worker /app/worker

# Entrypoint
ENTRYPOINT ["/app/worker"]
```

**Example entrypoint logic:**

```csharp
// Read job parameters from environment variables
var jobId = Environment.GetEnvironmentVariable("JOB_ID");
var inputBucket = Environment.GetEnvironmentVariable("INPUT_S3_BUCKET");
var inputKey = Environment.GetEnvironmentVariable("INPUT_S3_KEY");
var outputBucket = Environment.GetEnvironmentVariable("OUTPUT_S3_BUCKET");
var outputKey = Environment.GetEnvironmentVariable("OUTPUT_S3_KEY");

// Download inputs from S3
var input = await DownloadInputFromS3(inputBucket, inputKey);

// Execute operation
var result = await ExecuteOperation(input);

// Upload output to S3
await UploadOutputToS3(outputBucket, outputKey, result);
```

## Monitoring & Alerting

### CloudWatch Metrics

- **JobsFailed**: Alert if >5% failure rate
- **JobDuration**: Alert if P95 exceeds expected duration + buffer
- **QueueDepth**: Alert if >100 pending jobs (capacity issue)

### CloudWatch Alarms

```bash
# Alert on high failure rate
aws cloudwatch put-metric-alarm \
  --alarm-name honua-batch-high-failure-rate \
  --metric-name FailedJobs \
  --namespace AWS/Batch \
  --statistic Sum \
  --period 300 \
  --evaluation-periods 2 \
  --threshold 5 \
  --comparison-operator GreaterThanThreshold

# Alert on queue backlog
aws cloudwatch put-metric-alarm \
  --alarm-name honua-batch-queue-backlog \
  --metric-name PendingJobs \
  --namespace AWS/Batch \
  --statistic Average \
  --period 300 \
  --evaluation-periods 1 \
  --threshold 100 \
  --comparison-operator GreaterThanThreshold
```

### Cost Anomaly Detection

Enable AWS Cost Anomaly Detection for the Batch service to catch unexpected spending spikes.

## Testing

### Local Development

For local development without AWS:
1. Use simulated CloudBatchExecutor (existing implementation)
2. Or mock IAmazonBatch, IAmazonS3, IAmazonCloudWatchLogs interfaces

### Integration Testing

```csharp
// Use AWS SDK mocks or LocalStack
services.AddAwsBatchExecutor(configuration,
    new BasicAWSCredentials("test-key", "test-secret"));
```

## Security Considerations

### SNS Message Signature Verification

**IMPORTANT**: The webhook endpoint currently does NOT verify SNS message signatures. This is a security risk in production.

**To implement:**

```csharp
// In AwsBatchWebhookEndpoints.cs
using Amazon.SimpleNotificationService.Util;

// Verify signature before processing
var isValid = Message.IsMessageSignatureValid(snsMessage);
if (!isValid)
{
    logger.LogWarning("SNS message signature verification failed");
    return Results.Unauthorized();
}
```

See: https://docs.aws.amazon.com/sns/latest/dg/sns-verify-signature-of-message.html

### IAM Best Practices

1. **Use IAM roles** (EC2/ECS instance profiles) instead of access keys
2. **Principle of least privilege**: Grant only required permissions
3. **Separate roles**: Different roles for Honua server vs Batch jobs
4. **Audit logs**: Enable CloudTrail for API call auditing

## Troubleshooting

### Jobs stuck in PENDING
- Check compute environment is ENABLED
- Verify sufficient capacity (maxvCpus)
- Check IAM permissions for Batch service role

### Jobs fail immediately
- Check container image exists and is accessible
- Verify job definition parameters
- Review CloudWatch Logs for container errors

### No SNS notifications received
- Verify EventBridge rule is enabled
- Check SNS subscription is confirmed
- Ensure webhook endpoint is publicly accessible
- Review SNS delivery logs in CloudWatch

### High costs
- Verify Spot instances are enabled
- Check S3 lifecycle policies are active
- Monitor CloudWatch metrics for over-provisioning
- Review right-sizing recommendations

## References

- [AWS Batch Documentation](https://docs.aws.amazon.com/batch/)
- [S3 Lifecycle Policies](https://docs.aws.amazon.com/AmazonS3/latest/userguide/object-lifecycle-mgmt.html)
- [SNS Message Verification](https://docs.aws.amazon.com/sns/latest/dg/sns-verify-signature-of-message.html)
- [Spot Instance Best Practices](https://docs.aws.amazon.com/batch/latest/userguide/spot_fleet_IAM_role.html)
- [CloudWatch Logs Insights](https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/AnalyzingLogData.html)
