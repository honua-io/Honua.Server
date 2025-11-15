# Background Jobs Infrastructure

Pluggable message queue system for background job processing, replacing PostgreSQL polling for Tier 3 deployments.

## Overview

This infrastructure provides a unified abstraction over different message queue backends, enabling:

- **Tier 1-2 deployments**: PostgreSQL polling (simple, no external dependencies)
- **Tier 3 deployments**: AWS SQS, Azure Service Bus, or RabbitMQ (scalable, event-driven)
- **Idempotency**: Redis-based deduplication to prevent duplicate processing
- **Retry logic**: Exponential backoff with Polly for transient failures
- **Observability**: OpenTelemetry metrics for monitoring

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    BackgroundJobWorkerService                │
│  (Receives messages, processes jobs, handles retries)       │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ├──> IBackgroundJobQueue (Abstraction)
                  │    ├─ PostgresBackgroundJobQueue (Tier 1-2)
                  │    ├─ AwsSqsBackgroundJobQueue (Tier 3)
                  │    ├─ AzureServiceBusBackgroundJobQueue (Future)
                  │    └─ RabbitMqBackgroundJobQueue (Future)
                  │
                  ├──> IIdempotencyStore (Redis)
                  │    └─ RedisIdempotencyStore
                  │
                  └──> BackgroundJobMetrics (OpenTelemetry)
```

## Components

### 1. IBackgroundJobQueue

Abstraction for background job queue systems.

**Methods:**
- `EnqueueAsync<TJob>`: Add job to queue
- `ReceiveAsync<TJob>`: Retrieve jobs for processing (long polling for message queues)
- `CompleteAsync`: Mark job as successfully processed
- `AbandonAsync`: Return job to queue for retry
- `GetQueueDepthAsync`: Get pending message count

### 2. PostgresBackgroundJobQueue

Database polling implementation for Tier 1-2 deployments.

**Features:**
- Uses `SELECT ... FOR UPDATE SKIP LOCKED` for atomic dequeue
- Priority-based ordering
- Visibility timeout for in-flight messages
- Automatic retry with exponential backoff

**Suitable for:**
- Low-to-medium job throughput (< 100 jobs/second)
- Deployments without cloud infrastructure
- Simple setup with no external dependencies

### 3. AwsSqsBackgroundJobQueue

AWS SQS implementation for Tier 3 deployments.

**Features:**
- Event-driven processing with long polling (WaitTimeSeconds = 20)
- Native retry and dead-letter queue support
- FIFO queues for exactly-once delivery
- Automatic message expiry

**Suitable for:**
- High job throughput (> 100 jobs/second)
- Cloud-native architectures
- Multi-region deployments

### 4. RedisIdempotencyStore

Redis-based idempotency tracking to prevent duplicate processing.

**Features:**
- Fast lookups (sub-millisecond)
- Automatic TTL-based expiry (default: 7 days)
- No manual cleanup needed
- Atomic operations

### 5. BackgroundJobMetrics

OpenTelemetry metrics for observability.

**Metrics:**
- `background_jobs.enqueued`: Total jobs enqueued
- `background_jobs.completed`: Total jobs completed
- `background_jobs.failed`: Total jobs failed
- `background_jobs.retried`: Total retry attempts
- `background_jobs.duration`: Job processing duration histogram
- `background_jobs.queue_wait_time`: Queue wait time histogram
- `background_jobs.queue_depth`: Current pending jobs gauge

## Configuration

### appsettings.json

```json
{
  "BackgroundJobs": {
    "Mode": "MessageQueue",
    "Provider": "AwsSqs",
    "MaxConcurrentJobs": 10,
    "PollIntervalSeconds": 5,
    "JobTimeoutMinutes": 30,
    "VisibilityTimeoutSeconds": 1800,
    "MaxRetries": 3,
    "EnableIdempotency": true,
    "IdempotencyTtlDays": 7,

    "AwsSqs": {
      "QueueUrl": "https://sqs.us-west-2.amazonaws.com/123456789012/honua-jobs",
      "DeadLetterQueueUrl": "https://sqs.us-west-2.amazonaws.com/123456789012/honua-jobs-dlq",
      "Region": "us-west-2",
      "WaitTimeSeconds": 20,
      "MaxNumberOfMessages": 10,
      "UseFifoQueue": false
    }
  }
}
```

### Environment Variables

```bash
# Background Jobs Mode
BACKGROUND_JOBS__MODE=MessageQueue

# Provider Selection
BACKGROUND_JOBS__PROVIDER=AwsSqs

# AWS SQS Configuration
BACKGROUND_JOBS__AWSSQS__QUEUEURL=https://sqs.us-west-2.amazonaws.com/123456789012/honua-jobs
BACKGROUND_JOBS__AWSSQS__REGION=us-west-2

# Or use IAM role instead of credentials
# AWS_REGION=us-west-2
```

## Usage

### 1. Register Services

```csharp
// In Program.cs or Startup.cs
services.AddBackgroundJobs(configuration);

// Register worker service
services.AddHostedService<BackgroundJobWorkerService>();
```

### 2. Enqueue Jobs

```csharp
public class GeoprocessingService
{
    private readonly IBackgroundJobQueue _jobQueue;

    public async Task<string> EnqueueJobAsync(ProcessRun job)
    {
        // Basic enqueue
        var messageId = await _jobQueue.EnqueueAsync(job);

        // Or with options
        var messageId = await _jobQueue.EnqueueAsync(job, new EnqueueOptions
        {
            Priority = 10,
            DelaySeconds = TimeSpan.FromSeconds(30),
            MessageGroupId = "tenant-123" // For FIFO queues
        });

        return messageId;
    }
}
```

### 3. Monitor Queue

```csharp
public class MonitoringService
{
    private readonly IBackgroundJobQueue _jobQueue;

    public async Task<long> GetPendingJobsAsync()
    {
        return await _jobQueue.GetQueueDepthAsync();
    }
}
```

## Database Migration

Run the migration to create the `background_jobs` table:

```sql
-- See: src/Honua.Server.Core/Data/Migrations/034_BackgroundJobs.sql
```

## Testing

### Unit Tests

```bash
dotnet test --filter "FullyQualifiedName~BackgroundJobs"
```

### Integration Tests

Requires:
- PostgreSQL database (for PostgresBackgroundJobQueue tests)
- Redis instance (for RedisIdempotencyStore tests)

```bash
# Set test connection strings
export TEST_DATABASE_CONNECTION="Host=localhost;Database=honua_test;Username=postgres;Password=postgres"
export TEST_REDIS_CONNECTION="localhost:6379"

# Run integration tests
dotnet test --filter "Category=Integration"
```

## Performance Considerations

### PostgreSQL Polling

**Pros:**
- Simple setup
- No external dependencies
- Transactional guarantees

**Cons:**
- Polling overhead (CPU/network on database)
- Higher latency (poll interval)
- Less scalable

**Tuning:**
- Adjust `PollIntervalSeconds` (default: 5)
- Tune `MaxConcurrentJobs` based on worker capacity
- Create indexes on `background_jobs` table

### AWS SQS

**Pros:**
- Event-driven (no polling overhead)
- Highly scalable
- Native retry/DLQ support

**Cons:**
- External dependency
- Cost (per message)
- Network latency to AWS region

**Tuning:**
- Use long polling (`WaitTimeSeconds = 20`)
- Batch receive (`MaxNumberOfMessages = 10`)
- Use FIFO queues for ordered processing
- Configure DLQ for failed messages

### Redis Idempotency

**Performance:**
- Sub-millisecond lookups
- Memory-based (fast but limited by instance size)
- Automatic expiry (no manual cleanup)

**Tuning:**
- Adjust `IdempotencyTtlDays` based on requirements
- Monitor Redis memory usage
- Use Redis Cluster for HA

## Monitoring & Alerts

### Prometheus Queries

```promql
# Queue depth
background_jobs_queue_depth

# Job processing rate
rate(background_jobs_completed_total[5m])

# Job failure rate
rate(background_jobs_failed_total[5m])

# P95 job duration
histogram_quantile(0.95, rate(background_jobs_duration_bucket[5m]))

# P95 queue wait time
histogram_quantile(0.95, rate(background_jobs_queue_wait_time_bucket[5m]))
```

### Grafana Dashboard

Import the dashboard from `dashboards/background-jobs.json` (create separately).

### Alerts

```yaml
# Example Prometheus alerts
groups:
  - name: background_jobs
    rules:
      - alert: HighQueueDepth
        expr: background_jobs_queue_depth > 1000
        for: 5m
        annotations:
          summary: "Background job queue depth is high"

      - alert: HighJobFailureRate
        expr: rate(background_jobs_failed_total[5m]) > 0.1
        for: 5m
        annotations:
          summary: "Background job failure rate is high"
```

## Migration from Old Worker Service

### Before (PostgreSQL Polling)

```csharp
services.AddHostedService<GeoprocessingWorkerService>();
```

### After (Pluggable Queue)

```csharp
// Register infrastructure
services.AddBackgroundJobs(configuration);

// Register new worker
services.AddHostedService<BackgroundJobWorkerService>();
```

### Configuration Changes

```json
{
  "BackgroundJobs": {
    "Mode": "Polling", // Keep existing behavior
    "MaxConcurrentJobs": 5,
    "PollIntervalSeconds": 5
  }
}
```

### Gradual Migration

1. **Phase 1**: Deploy with `Mode = "Polling"` (no behavior change)
2. **Phase 2**: Test with SQS in staging environment
3. **Phase 3**: Switch to `Mode = "MessageQueue"` in production
4. **Phase 4**: Remove old `GeoprocessingWorkerService`

## Troubleshooting

### Jobs stuck in processing state

**Cause:** Worker crash before completing job

**Solution:** Run recovery function
```sql
SELECT * FROM recover_stuck_background_jobs(60); -- 60 minutes threshold
```

### High queue depth

**Cause:** Processing slower than enqueue rate

**Solutions:**
- Scale up workers (increase `MaxConcurrentJobs`)
- Add more worker instances
- Optimize job processing logic
- Check for blocking operations

### Duplicate job processing

**Cause:** Idempotency disabled or Redis unavailable

**Solutions:**
- Ensure `EnableIdempotency = true`
- Verify Redis connectivity
- Check idempotency store metrics

### SQS visibility timeout issues

**Cause:** `VisibilityTimeoutSeconds` < `JobTimeoutMinutes`

**Solution:** Ensure `VisibilityTimeoutSeconds >= JobTimeoutMinutes * 60`

## Future Enhancements

- [ ] Azure Service Bus implementation
- [ ] RabbitMQ implementation
- [ ] Dead-letter queue handling UI
- [ ] Job priority queues (separate high/low priority)
- [ ] Job scheduling (cron-like)
- [ ] Job chaining/workflows
- [ ] Distributed tracing integration
