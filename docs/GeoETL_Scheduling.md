# GeoETL Workflow Scheduling System

## Overview

The GeoETL Workflow Scheduling system enables automated, cron-based execution of workflows with comprehensive features including retry logic, concurrent execution control, and notification support.

## Features

### 1. Cron-Based Scheduling
- Standard cron expression syntax
- Timezone support (IANA timezone identifiers)
- Next run time calculation and preview
- Support for common schedules (hourly, daily, weekly, monthly)

### 2. Execution Management
- Background service checks for due schedules every minute
- Distributed locking for multi-instance deployments (PostgreSQL advisory locks)
- Concurrent execution limits per schedule
- Automatic next run time calculation

### 3. Reliability & Resilience
- Configurable retry attempts on failure
- Retry delay configuration
- Schedule expiration support
- Error state tracking
- Execution history tracking

### 4. Notifications (Extensible)
- Email notifications (configurable)
- Webhook notifications
- Slack integration
- Microsoft Teams integration
- Configurable success/failure notification preferences

### 5. Complete REST API
- Full CRUD operations for schedules
- Pause/Resume functionality
- Manual trigger (run now)
- Execution history queries
- Next run time preview

### 6. Blazor Admin UI
- Schedule management interface
- Visual cron expression builder
- Real-time status updates
- Execution history viewer
- Quick action menu (pause, resume, run now, delete)

## Architecture

### Components

1. **WorkflowSchedule** (`/src/Honua.Server.Enterprise/ETL/Scheduling/WorkflowSchedule.cs`)
   - Core model representing a workflow schedule
   - Cron expression validation and next run calculation
   - Status management (Active, Paused, Expired, Error)

2. **IWorkflowScheduleStore** (`/src/Honua.Server.Enterprise/ETL/Scheduling/IWorkflowScheduleStore.cs`)
   - Storage interface for schedules and executions
   - Distributed locking support

3. **PostgresWorkflowScheduleStore** (`/src/Honua.Server.Enterprise/ETL/Scheduling/PostgresWorkflowScheduleStore.cs`)
   - PostgreSQL implementation of schedule store
   - Uses advisory locks for distributed coordination
   - Stores schedule history and execution records

4. **ScheduleExecutor** (`/src/Honua.Server.Enterprise/ETL/Scheduling/ScheduleExecutor.cs`)
   - Background service running every minute
   - Checks for due schedules
   - Executes workflows via IWorkflowEngine
   - Handles retries and error states

5. **GeoEtlScheduleEndpoints** (`/src/Honua.Server.Host/Admin/GeoEtlScheduleEndpoints.cs`)
   - REST API endpoints for schedule management
   - Full CRUD operations
   - Control endpoints (pause, resume, run now)

6. **Database Schema** (`/src/Honua.Server.Core/Data/Migrations/018_ETL_Scheduling.sql`)
   - `geoetl_workflow_schedules` table
   - `geoetl_schedule_executions` table
   - Helper functions for statistics and cleanup

## Database Schema

### geoetl_workflow_schedules
```sql
- schedule_id: UUID (PK)
- workflow_id: UUID (FK to geoetl_workflows)
- tenant_id: UUID
- name: TEXT
- description: TEXT
- cron_expression: TEXT
- timezone: TEXT (default 'UTC')
- enabled: BOOLEAN
- status: TEXT (active, paused, expired, error)
- next_run_at: TIMESTAMPTZ
- last_run_at: TIMESTAMPTZ
- last_run_status: TEXT
- last_run_id: UUID
- parameter_values: JSONB
- max_concurrent_executions: INTEGER
- retry_attempts: INTEGER
- retry_delay_minutes: INTEGER
- expires_at: TIMESTAMPTZ
- notification_config: JSONB
- tags: TEXT[]
- created_at, updated_at, created_by, updated_by
```

### geoetl_schedule_executions
```sql
- execution_id: UUID (PK)
- schedule_id: UUID (FK to geoetl_workflow_schedules)
- workflow_run_id: UUID (FK to geoetl_workflow_runs)
- scheduled_at: TIMESTAMPTZ
- executed_at: TIMESTAMPTZ
- completed_at: TIMESTAMPTZ
- status: TEXT (pending, running, completed, failed, skipped)
- error_message: TEXT
- retry_count: INTEGER
- skipped: BOOLEAN
- skip_reason: TEXT
```

## Installation & Setup

### 1. Service Registration

Add to your `Program.cs` or startup configuration:

```csharp
// Register GeoETL core services
services.AddGeoEtl(connectionString);

// Register GeoETL scheduling (includes background executor)
services.AddGeoEtlScheduling(connectionString);
```

To disable the background executor (for testing or read-only instances):

```csharp
services.AddGeoEtlScheduling(connectionString, enableScheduleExecutor: false);
```

### 2. Database Migration

Run migration `018_ETL_Scheduling.sql`:

```bash
psql -h localhost -U postgres -d honua -f src/Honua.Server.Core/Data/Migrations/018_ETL_Scheduling.sql
```

### 3. Endpoint Registration

Endpoints are automatically registered in `EndpointExtensions.cs`:

```csharp
app.MapGeoEtlScheduleEndpoints();
```

## Usage

### Creating a Schedule via API

```bash
POST /admin/api/geoetl/schedules
Content-Type: application/json

{
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "workflowId": "workflow-guid-here",
  "createdBy": "user-guid-here",
  "name": "Daily Data Processing",
  "description": "Process incoming geospatial data daily",
  "cronExpression": "0 0 * * *",
  "timezone": "America/New_York",
  "enabled": true,
  "maxConcurrentExecutions": 1,
  "retryAttempts": 3,
  "retryDelayMinutes": 5,
  "parameterValues": {
    "inputPath": "/data/input",
    "outputFormat": "GeoPackage"
  },
  "notificationConfig": {
    "notifyOnSuccess": false,
    "notifyOnFailure": true,
    "emailAddresses": ["admin@example.com"]
  }
}
```

### Common Cron Expressions

| Expression | Description |
|------------|-------------|
| `*/15 * * * *` | Every 15 minutes |
| `0 * * * *` | Every hour at :00 |
| `0 0 * * *` | Daily at midnight |
| `0 9 * * MON-FRI` | Weekdays at 9:00 AM |
| `0 0 * * 0` | Weekly on Sunday at midnight |
| `0 0 1 * *` | Monthly on the 1st at midnight |
| `0 6,12,18 * * *` | Three times daily (6 AM, 12 PM, 6 PM) |

### Cron Expression Format

```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of week (0 - 6) (Sunday to Saturday)
│ │ │ │ │
* * * * *
```

## API Endpoints

### Schedule Management

- `GET /admin/api/geoetl/schedules?tenantId={id}` - List schedules
- `GET /admin/api/geoetl/schedules/{id}` - Get schedule
- `POST /admin/api/geoetl/schedules` - Create schedule
- `PUT /admin/api/geoetl/schedules/{id}` - Update schedule
- `DELETE /admin/api/geoetl/schedules/{id}` - Delete schedule

### Schedule Control

- `POST /admin/api/geoetl/schedules/{id}/pause` - Pause schedule
- `POST /admin/api/geoetl/schedules/{id}/resume` - Resume schedule
- `POST /admin/api/geoetl/schedules/{id}/run-now?userId={id}` - Trigger immediate run

### Schedule Information

- `GET /admin/api/geoetl/schedules/{id}/history?limit={n}` - Get execution history
- `GET /admin/api/geoetl/schedules/{id}/next-runs?count={n}` - Preview next executions
- `GET /admin/api/geoetl/schedules/workflow/{workflowId}` - List schedules for workflow

## Blazor UI

Access the schedule management UI at:
```
/geoetl/schedules
```

Features:
- Schedule creation wizard with cron expression builder
- Quick action buttons for common schedules
- Real-time status indicators
- Execution history viewer
- Next run time preview
- Advanced configuration (retries, concurrency, notifications)

## Distributed Deployment

The scheduling system uses PostgreSQL advisory locks to ensure only one instance executes a schedule at a time. This makes it safe to run multiple instances of Honua.Server with scheduling enabled.

### How It Works

1. ScheduleExecutor runs on all instances checking for due schedules
2. Before executing, instance tries to acquire advisory lock
3. Only one instance gets the lock and executes the workflow
4. Other instances skip the schedule and move on
5. Lock is automatically released when execution completes

## Best Practices

### 1. Timezone Configuration
- Always specify explicit timezones for schedules
- Use IANA timezone identifiers (e.g., "America/New_York")
- Consider daylight saving time changes
- UTC is the safest default for global deployments

### 2. Concurrent Execution Limits
- Set `maxConcurrentExecutions = 1` for most workflows
- Use higher values only for truly parallel-safe workflows
- Consider workflow duration when setting limits

### 3. Retry Configuration
- Set retries for network-dependent workflows
- Use appropriate retry delays (5-15 minutes typical)
- Monitor retry patterns to identify systemic issues

### 4. Schedule Expiration
- Set expiration dates for temporary schedules
- Regularly review and clean up expired schedules
- Use the cleanup function: `SELECT honua_geoetl_cleanup_old_executions(90);`

### 5. Monitoring
- Monitor execution history regularly
- Set up notifications for failed executions
- Track schedule statistics with built-in functions:
  - `SELECT * FROM honua_geoetl_get_schedule_stats(schedule_id, 30);`
  - `SELECT * FROM honua_geoetl_get_tenant_schedule_stats(tenant_id, 30);`

## Troubleshooting

### Schedule Not Executing

1. Check schedule is enabled: `enabled = true`
2. Check schedule status: `status = 'active'`
3. Verify `next_run_at` is set and in the past
4. Check ScheduleExecutor is running (background service)
5. Review logs for error messages

### Skipped Executions

Executions may be skipped if:
- Previous execution still running (concurrent limit reached)
- Schedule locked by another instance
- Schedule disabled or paused

Check `geoetl_schedule_executions` table for skip reasons:
```sql
SELECT * FROM geoetl_schedule_executions
WHERE schedule_id = 'your-schedule-id'
AND skipped = true;
```

### Timezone Issues

- Verify timezone string is valid IANA identifier
- Check for DST transition periods
- Test with `SELECT * FROM pg_timezone_names WHERE name = 'America/New_York';`

### Performance Optimization

For high-volume scheduling:
- Increase check interval (modify `_checkInterval` in ScheduleExecutor)
- Use database connection pooling
- Monitor advisory lock contention
- Consider separate instance for scheduling only

## Maintenance

### Cleanup Old Executions

Run periodically to clean up old execution history:

```sql
-- Delete executions older than 90 days
SELECT honua_geoetl_cleanup_old_executions(90);
```

### Expire Old Schedules

Automatically expire schedules past their expiration date:

```sql
SELECT honua_geoetl_expire_schedules();
```

### Monitor Schedule Health

```sql
-- Get schedule statistics
SELECT * FROM honua_geoetl_get_schedule_stats('schedule-id', 30);

-- Get tenant-wide statistics
SELECT * FROM honua_geoetl_get_tenant_schedule_stats('tenant-id', 30);

-- Find schedules with high failure rates
SELECT s.schedule_id, s.name,
       COUNT(*) FILTER (WHERE e.status = 'failed') AS failures,
       COUNT(*) AS total_executions
FROM geoetl_workflow_schedules s
LEFT JOIN geoetl_schedule_executions e ON e.schedule_id = s.schedule_id
WHERE e.scheduled_at >= NOW() - INTERVAL '30 days'
GROUP BY s.schedule_id, s.name
HAVING COUNT(*) FILTER (WHERE e.status = 'failed') > COUNT(*) * 0.1
ORDER BY failures DESC;
```

## Testing

Run unit tests:

```bash
dotnet test tests/Honua.Server.Enterprise.Tests/ETL/WorkflowScheduleTests.cs
```

Tests cover:
- Cron expression validation
- Next run calculation
- Schedule status logic
- Timezone handling
- Edge cases (DST, invalid cron, etc.)

## Future Enhancements

Potential improvements:
- [ ] Calendar-based scheduling (specific dates)
- [ ] Dependency-based scheduling (workflow chains)
- [ ] Schedule templates (predefined common schedules)
- [ ] Advanced notification integrations (PagerDuty, etc.)
- [ ] Schedule analytics dashboard
- [ ] Smart retry with exponential backoff
- [ ] Schedule versioning and rollback
- [ ] Execution time windows (only run between hours)

## Support

For issues or questions:
- Check logs in `/var/log/honua` or via Serilog sinks
- Review execution history in database
- Monitor ScheduleExecutor service health
- Enable verbose logging for debugging

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
