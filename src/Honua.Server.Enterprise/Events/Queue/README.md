# Durable GeoEvent Queue Infrastructure

## Overview

The Durable GeoEvent Queue provides guaranteed delivery of geofence events with:

- **Durability**: Events survive server restarts (persisted to PostgreSQL)
- **Guaranteed Delivery**: Automatic retry with exponential backoff
- **Audit Trail**: Complete delivery history for compliance
- **Event Replay**: Time-travel queries for debugging and analytics
- **Deduplication**: SHA-256 fingerprinting prevents duplicate processing
- **FIFO Ordering**: Maintains event order per geofence
- **Dead Letter Queue**: Failed events are captured for investigation
- **Flexible Deployment**: Database-only (on-premises) or Database + Azure Service Bus (cloud)

## Architecture

### Hybrid Approach (Option C)

```
┌─────────────────┐
│   Location      │
│   Update        │
└────────┬────────┘
         │
         v
┌─────────────────────────────────┐
│  GeofenceEvaluationService      │
│  - Evaluates location           │
│  - Generates enter/exit events  │
│  - Persists to geofence_events  │
└────────┬────────────────────────┘
         │
         v
┌─────────────────────────────────┐
│  DurableEventPublisher           │
│  - Enqueues to queue table       │
│  - Generates fingerprint         │
│  - Prevents duplicates           │
└────────┬────────────────────────┘
         │
         v
┌─────────────────────────────────┐
│  geofence_event_queue (DB)      │
│  - Pending events                │
│  - Retry logic                   │
│  - FIFO per partition key        │
└────────┬────────────────────────┘
         │
         v
┌─────────────────────────────────┐
│  QueueConsumerService            │
│  - Polls pending events          │
│  - Delivers to targets           │
│  - Logs delivery attempts        │
└────────┬────────────────────────┘
         │
         ├────────────┬──────────────┐
         │            │              │
         v            v              v
  ┌──────────┐  ┌─────────┐  ┌──────────────┐
  │ SignalR  │  │ Service │  │  Webhooks    │
  │ Clients  │  │  Bus    │  │ (Phase 2)    │
  └──────────┘  └─────────┘  └──────────────┘
```

## Database Schema

### geofence_event_queue

Stores queued events awaiting delivery:

```sql
CREATE TABLE geofence_event_queue (
    id UUID PRIMARY KEY,
    geofence_event_id UUID REFERENCES geofence_events(id),
    status VARCHAR(20),  -- pending, processing, completed, failed, dlq
    partition_key VARCHAR(255),  -- For FIFO ordering
    fingerprint VARCHAR(64),  -- For deduplication
    attempt_count INT,
    max_attempts INT DEFAULT 5,
    next_attempt_at TIMESTAMPTZ,
    delivery_targets JSONB,  -- ["signalr", "servicebus", ...]
    tenant_id VARCHAR(100)
);
```

### geofence_event_delivery_log

Audit trail for all delivery attempts:

```sql
CREATE TABLE geofence_event_delivery_log (
    id UUID PRIMARY KEY,
    queue_item_id UUID REFERENCES geofence_event_queue(id),
    attempt_number INT,
    target VARCHAR(50),  -- signalr, servicebus, webhook
    status VARCHAR(20),  -- success, partial, failed, timeout
    recipient_count INT,
    latency_ms INT,
    attempted_at TIMESTAMPTZ
);
```

## Service Registration

### Basic Setup (Database-only)

```csharp
// In Program.cs or Startup.cs
services.AddGeoEventQueue(connectionString);
```

### Advanced Setup (with Azure Service Bus)

```csharp
services.AddGeoEventQueue(connectionString, configuration);
```

```json
// appsettings.json
{
  "GeoEventQueue": {
    "PollingIntervalSeconds": 5,
    "BatchSize": 10,
    "RetentionDays": 30,
    "EnableServiceBus": true,
    "ServiceBusConnectionString": "Endpoint=sb://...",
    "ServiceBusTopicName": "geofence-events"
  }
}
```

## Usage

### Publishing Events

Events are automatically published when `GeofenceEvaluationService` generates them:

```csharp
var result = await evaluationService.EvaluateLocationAsync(
    entityId: "vehicle-123",
    location: point,
    eventTime: DateTime.UtcNow
);

// Events are automatically:
// 1. Persisted to geofence_events table
// 2. Enqueued to geofence_event_queue
// 3. Delivered by background consumer
```

### Event Replay API

Retrieve historical events for time-travel queries:

```bash
POST /api/v1/geoevent/replay
{
  "entity_id": "vehicle-123",
  "start_time": "2025-11-14T00:00:00Z",
  "end_time": "2025-11-14T23:59:59Z",
  "event_types": ["enter", "exit"]
}
```

### Queue Metrics

Monitor queue health (admin only):

```bash
GET /api/v1/geoevent/replay/metrics
```

Response:
```json
{
  "pending_count": 5,
  "processing_count": 2,
  "completed_count": 1547,
  "dlq_count": 0,
  "avg_queue_depth_seconds": 2.3,
  "avg_delivery_latency_ms": 45.2,
  "success_rate_percent": 99.8,
  "oldest_pending_age_seconds": 15
}
```

### Dead Letter Queue

Retrieve failed events for investigation:

```bash
GET /api/v1/geoevent/replay/deadletter?limit=100&offset=0
```

## Features

### 1. Guaranteed Delivery

- Events are persisted before delivery
- Automatic retry with exponential backoff (5s, 10s, 20s, 40s, 80s)
- Maximum 5 attempts before moving to DLQ
- SKIP LOCKED prevents concurrent processing

### 2. Deduplication

- SHA-256 fingerprint generated from event key properties
- 1-hour deduplication window
- Prevents duplicate processing during retries

### 3. FIFO Ordering

- Partition key = `geofence_id`
- Events for the same geofence are processed in order
- Prevents out-of-order delivery

### 4. Event Replay

- Query historical events by entity, geofence, or time range
- Supports filtering by event type
- Up to 90-day lookback period
- Use cases:
  - Audit trail for compliance
  - Debug geofencing behavior
  - Generate analytics reports
  - Reconstruct entity movement timeline

### 5. Monitoring & Observability

- Real-time queue metrics
- Delivery success rate tracking
- Latency monitoring
- Health checks with degradation thresholds
- Dead letter queue for failed events

### 6. Optional Azure Service Bus

For enterprise cloud deployments:

- Publish events to Azure Service Bus topics
- Built-in duplicate detection
- Automatic dead lettering
- Supports subscriptions for multiple consumers
- High availability and disaster recovery

## Performance Characteristics

### Database Queue (Default)

- **Throughput**: 100-500 events/second
- **Latency**: P50 < 50ms, P95 < 100ms, P99 < 200ms
- **Scalability**: Suitable for 1-10k events/hour
- **Deployment**: Works on-premises, no cloud dependencies

### Database + Azure Service Bus (Enterprise)

- **Throughput**: 1,000+ events/second
- **Latency**: P50 < 20ms, P95 < 50ms
- **Scalability**: Handles 100k+ events/hour
- **Deployment**: Requires Azure infrastructure

## Deployment Options

### Option 1: On-Premises (Database Only)

Best for:
- On-premises deployments
- Air-gapped environments
- Low to moderate event volume
- No cloud infrastructure available

Configuration:
```json
{
  "GeoEventQueue": {
    "EnableServiceBus": false
  }
}
```

### Option 2: Azure Cloud (Database + Service Bus)

Best for:
- Azure deployments
- High event volume (100k+ events/hour)
- Multiple downstream consumers
- Enterprise SLA requirements

Configuration:
```json
{
  "GeoEventQueue": {
    "EnableServiceBus": true,
    "ServiceBusConnectionString": "Endpoint=sb://...",
    "ServiceBusTopicName": "geofence-events"
  }
}
```

### Option 3: Hybrid (Database + Optional Service Bus)

Best for:
- Gradual migration to cloud
- Multi-deployment support
- Audit trail always in database
- Optional cloud distribution

## Maintenance

### Cleanup Completed Events

Automatically remove old completed events:

```sql
-- Manual cleanup
SELECT honua_cleanup_completed_queue_items(30);  -- 30 days retention

-- Or schedule via cron/background job
*/
0 2 * * * psql -c "SELECT honua_cleanup_completed_queue_items(30);"
```

### Monitor Dead Letter Queue

Check for failed events:

```sql
SELECT
    id,
    geofence_event_id,
    last_error,
    attempt_count,
    created_at
FROM geofence_event_queue
WHERE status = 'dlq'
ORDER BY created_at DESC
LIMIT 100;
```

### Requeue Failed Events

After fixing root cause, requeue events from DLQ:

```sql
UPDATE geofence_event_queue
SET
    status = 'pending',
    attempt_count = 0,
    next_attempt_at = NOW()
WHERE id = 'failed-event-id';
```

## Health Checks

The queue includes built-in health checks:

```csharp
services.AddHealthChecks()
    .AddGeoEventQueueHealthCheck();
```

Health check thresholds:
- **Unhealthy**: DLQ count > 100
- **Degraded**: Pending count > 1000
- **Degraded**: Oldest pending > 5 minutes
- **Degraded**: Success rate < 95%
- **Healthy**: All metrics within thresholds

## Migration

To enable durable queue on existing deployments:

1. Run migration `018_GeoEventQueue.sql`
2. Update service registration to include `AddGeoEventQueue()`
3. Deploy updated code
4. Monitor queue metrics
5. Optionally enable Azure Service Bus

## Troubleshooting

### High Queue Depth

**Symptom**: `pending_count` is increasing

**Possible Causes**:
- Consumer service not running
- High event volume exceeds processing capacity
- SignalR delivery errors

**Solutions**:
- Check consumer service logs
- Increase `BatchSize` in configuration
- Add more consumer instances (horizontal scaling)
- Enable Azure Service Bus for higher throughput

### High DLQ Count

**Symptom**: `dlq_count` is increasing

**Possible Causes**:
- SignalR hub context errors
- Database connection issues
- Invalid event data

**Solutions**:
- Check delivery logs: `SELECT * FROM geofence_event_delivery_log WHERE status = 'failed'`
- Fix root cause
- Requeue events from DLQ

### Low Success Rate

**Symptom**: `success_rate_percent < 95%`

**Possible Causes**:
- Network issues
- SignalR connection failures
- Resource constraints

**Solutions**:
- Check system resources (CPU, memory, network)
- Review delivery logs for error patterns
- Consider rate limiting or backpressure

## Future Enhancements

### Phase 2
- Webhook delivery target
- Batch message publishing to Service Bus
- Compression for large event payloads
- Partitioning by tenant for multi-tenancy

### Phase 3
- Complex Event Processing (CEP) integration
- Stream Analytics integration
- Real-time dashboards
- Machine learning pipeline integration

## References

- Migration: `/src/Honua.Server.Core/Data/Migrations/018_GeoEventQueue.sql`
- Models: `/src/Honua.Server.Enterprise/Events/Queue/Models/`
- Services: `/src/Honua.Server.Enterprise/Events/Queue/Services/`
- API: `/src/Honua.Server.Host/GeoEvent/GeoEventReplayController.cs`
- Health Checks: `/src/Honua.Server.Enterprise/Events/Queue/Health/`
