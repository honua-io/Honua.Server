# Build Queue Background Service

This directory contains the asynchronous build queue processing system for Honua Server custom builds.

## Overview

The build queue system processes customer build requests asynchronously, allowing customers to request custom Honua Server builds that are processed in the background with real-time progress updates and email notifications.

## Architecture

### Components

1. **BuildQueueProcessor** - Main background service (hosted service)
   - Continuously polls the `build_queue` table for pending builds
   - Manages concurrent build execution with configurable limits
   - Handles build timeouts and graceful shutdown
   - Implements retry logic with exponential backoff

2. **BuildQueueManager** - Database operations and queue management
   - Enqueues new build jobs
   - Retrieves next pending build (priority + FIFO)
   - Updates build status and progress
   - Provides queue statistics and metrics

3. **BuildNotificationService** - Customer email notifications
   - Sends emails when builds are queued, started, completed, or failed
   - Uses professional HTML email templates
   - Implements retry logic for email delivery
   - Includes download links and deployment instructions

## Database Schema

The system uses a PostgreSQL `build_queue` table with the following key fields:

- **Identification**: id, customer_id, customer_name, customer_email
- **Configuration**: manifest_path, configuration_name, tier, architecture, cloud_provider
- **Status**: status (pending/building/success/failed/cancelled/timedout), priority (0-3)
- **Progress**: progress_percent (0-100), current_step
- **Results**: output_path, image_url, download_url, error_message
- **Retry**: retry_count
- **Timestamps**: enqueued_at, started_at, completed_at, updated_at
- **Metrics**: build_duration_seconds

### Indexes

- `idx_build_queue_status` - For filtering by status
- `idx_build_queue_customer` - For customer lookups
- `idx_build_queue_priority_enqueued` - For efficient queue polling (priority DESC, enqueued_at ASC)
- `idx_build_queue_enqueued_at` - For time-based queries
- `idx_build_queue_completed_at` - For completed builds

## Configuration

### appsettings.json Example

```json
{
  "BuildQueue": {
    "MaxConcurrentBuilds": 2,
    "BuildTimeoutMinutes": 60,
    "MaxRetryAttempts": 3,
    "PollIntervalSeconds": 5,
    "RetryDelayMinutes": 5,
    "UseExponentialBackoff": true,
    "CompletedBuildRetentionDays": 30,
    "EnableNotifications": true,
    "ConnectionString": "Host=localhost;Database=honua;Username=honua;Password=***",
    "WorkspaceDirectory": "/var/honua/builds",
    "OutputDirectory": "/var/honua/output",
    "DownloadBaseUrl": "https://builds.honua.io",
    "CleanupWorkspaceAfterBuild": true,
    "EnableGracefulShutdown": true,
    "GracefulShutdownTimeoutSeconds": 300
  },
  "Email": {
    "Enabled": true,
    "SmtpServer": "smtp.example.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "SmtpUsername": "builds@honua.io",
    "SmtpPassword": "***",
    "FromAddress": "builds@honua.io",
    "FromName": "Honua Server",
    "TimeoutSeconds": 30
  }
}
```

## Usage

### Registering Services

In your `Program.cs` or `Startup.cs`:

```csharp
// Option 1: Using IConfiguration
builder.Services.AddBuildQueueServices(
    builder.Configuration,
    connectionString: "Host=localhost;Database=honua;Username=honua;Password=***"
);

// Option 2: Using explicit configuration
builder.Services.AddBuildQueueServices(
    configureBuildQueue: options =>
    {
        options.MaxConcurrentBuilds = 2;
        options.BuildTimeoutMinutes = 60;
        options.MaxRetryAttempts = 3;
        options.PollIntervalSeconds = 5;
        options.WorkspaceDirectory = "/var/honua/builds";
        options.OutputDirectory = "/var/honua/output";
    },
    configureEmail: options =>
    {
        options.Enabled = true;
        options.SmtpServer = "smtp.example.com";
        options.SmtpPort = 587;
        options.FromAddress = "builds@honua.io";
    },
    connectionString: "Host=localhost;Database=honua;Username=honua;Password=***"
);
```

### Enqueuing a Build

```csharp
public class BuildController : ControllerBase
{
    private readonly IBuildQueueManager _queueManager;

    [HttpPost("builds")]
    public async Task<IActionResult> EnqueueBuild([FromBody] BuildRequest request)
    {
        var job = new BuildJob
        {
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            ManifestPath = request.ManifestPath,
            ConfigurationName = request.ConfigurationName,
            Tier = request.Tier,
            Architecture = request.Architecture,
            CloudProvider = request.CloudProvider,
            Priority = BuildPriority.Normal
        };

        var jobId = await _queueManager.EnqueueBuildAsync(job);

        return Accepted(new { jobId, message = "Build queued successfully" });
    }
}
```

### Checking Build Status

```csharp
[HttpGet("builds/{jobId}")]
public async Task<IActionResult> GetBuildStatus(Guid jobId)
{
    var job = await _queueManager.GetBuildJobAsync(jobId);

    if (job == null)
        return NotFound();

    return Ok(new
    {
        job.Id,
        job.Status,
        job.ProgressPercent,
        job.CurrentStep,
        job.OutputPath,
        job.ImageUrl,
        job.DownloadUrl,
        job.ErrorMessage,
        job.EnqueuedAt,
        job.StartedAt,
        job.CompletedAt,
        job.BuildDurationSeconds
    });
}
```

### Getting Queue Statistics

```csharp
[HttpGet("builds/statistics")]
public async Task<IActionResult> GetQueueStatistics()
{
    var stats = await _queueManager.GetQueueStatisticsAsync();

    return Ok(new
    {
        stats.PendingCount,
        stats.BuildingCount,
        stats.CompletedToday,
        stats.FailedToday,
        stats.AverageBuildTimeSeconds,
        stats.SuccessRate,
        stats.OldestPendingBuild
    });
}
```

## Build Process Flow

1. **Queue** - Customer submits build request → Build job created with `pending` status
2. **Start** - Processor acquires job → Status updated to `building` → "Build Started" email sent
3. **Progress** - Build executes with progress callbacks → Database updated in real-time
4. **Complete** - Build finishes → Status updated to `success` or `failed` → Notification email sent
5. **Retry** (if failed) - If retries remain → Status reset to `pending` → Retry after delay
6. **Cleanup** - Workspace cleaned up (if configured)

## Build Priority Levels

- **Critical (3)** - Emergency/support builds (processed first)
- **High (2)** - Urgent customer builds
- **Normal (1)** - Standard customer builds (default)
- **Low (0)** - Batch/scheduled builds

Builds are processed in priority order (highest first), then FIFO within the same priority.

## Retry Logic

Failed builds are automatically retried with configurable:
- Max retry attempts (default: 3)
- Retry delay (default: 5 minutes)
- Exponential backoff (optional)

After exhausting retries, a final failure notification is sent.

## Email Notifications

The system sends professional HTML emails for:

1. **Build Queued** - Confirmation that build was queued
2. **Build Started** - When build begins processing
3. **Build Completed** - Success notification with download links and deployment instructions
4. **Build Failed** - Error notification with retry information

### Email Templates

All emails use responsive HTML templates with:
- Professional branding (Honua Server)
- Styled tables for build details
- Syntax-highlighted code blocks
- Clear call-to-action links
- Footer disclaimers

## Concurrency Control

The system uses a `SemaphoreSlim` to limit concurrent builds (default: 2).

- Prevents resource exhaustion
- Ensures consistent build performance
- Configurable per environment

## Graceful Shutdown

When the service is stopped:

1. New builds are not started
2. In-progress builds are allowed to complete (configurable timeout)
3. If timeout is reached, builds are forcibly stopped
4. Status is preserved in database

## Health Checks

The build queue can be monitored via:

- Queue statistics endpoint
- Build status queries
- Database table inspection

## Performance Considerations

- **Polling Interval**: Adjust based on expected load (default: 5 seconds)
- **Concurrent Builds**: Balance between throughput and resource usage
- **Build Timeout**: Set based on typical build duration (default: 60 minutes)
- **Database Indexes**: Optimized for queue polling and status queries

## Monitoring and Metrics

Key metrics to monitor:

- Pending queue depth
- Average build time
- Success rate (last 24 hours)
- Failed build count
- Oldest pending build age
- Active concurrent builds

## Database Maintenance

Regularly archive or delete old completed builds:

```sql
-- Archive builds older than 30 days
DELETE FROM build_queue
WHERE completed_at < NOW() - INTERVAL '30 days'
  AND status IN ('success', 'failed', 'cancelled');
```

## Security Considerations

- Connection strings should be stored securely (Azure Key Vault, AWS Secrets Manager, etc.)
- Email credentials should use app-specific passwords or OAuth
- Build manifests should be validated before processing
- Customer data should be encrypted at rest
- Access to build outputs should be restricted

## Troubleshooting

### Builds stuck in pending state
- Check processor is running: `ps aux | grep BuildQueueProcessor`
- Verify database connectivity
- Check for exceptions in logs
- Ensure concurrency limit not exceeded

### Emails not sending
- Verify SMTP configuration
- Check email credentials
- Review firewall/network rules
- Check spam filters

### High memory usage
- Reduce concurrent builds
- Enable workspace cleanup
- Archive old completed builds
- Monitor build artifact sizes

## Future Enhancements

- [ ] Web-based build queue dashboard
- [ ] Real-time progress via SignalR/WebSockets
- [ ] Build artifact caching
- [ ] Distributed build processing (multiple workers)
- [ ] Build cost estimation
- [ ] Customer build quotas/limits
- [ ] Build scheduling (cron-like)
- [ ] Slack/Teams notifications
