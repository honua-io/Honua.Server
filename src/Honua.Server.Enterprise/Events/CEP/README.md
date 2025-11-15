# Complex Event Processing (CEP) for Geofence Events

## Quick Start

### 1. Run Database Migration

```bash
psql -U postgres -d honua -f src/Honua.Server.Core/Data/Migrations/035_ComplexEventProcessing.sql
```

Verify tables were created:
```sql
SELECT tablename FROM pg_tables WHERE tablename LIKE '%pattern%';
-- Expected:
-- geofence_event_patterns
-- pattern_match_state
-- pattern_match_history
-- tumbling_window_state
```

### 2. Register CEP Services

The CEP engine integrates automatically with the event queue consumer. No additional configuration required for basic usage.

**Optional**: Enable CEP explicitly in appsettings.json:

```json
{
  "GeoEventCEP": {
    "Enabled": true,
    "StateCleanupIntervalMinutes": 60,
    "StateRetentionHours": 24
  }
}
```

### 3. Create Your First Pattern

Use the example patterns in `Examples/pattern-examples.json`:

```bash
# Route Deviation Detection
curl -X POST https://your-server/api/v1/cep/patterns \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "name": "Route Deviation Detection",
    "patternType": "sequence",
    "windowDurationSeconds": 1800,
    "windowType": "sliding",
    "conditions": [
      {
        "conditionId": "step1",
        "eventType": "exit",
        "geofenceNamePattern": "Warehouse-.*"
      },
      {
        "conditionId": "step2",
        "eventType": "enter",
        "geofenceNamePattern": "Unauthorized-.*",
        "previousConditionId": "step1",
        "maxTimeSincePreviousSeconds": 1800
      }
    ],
    "alertName": "Route Deviation Detected",
    "alertSeverity": "critical",
    "alertDescription": "Vehicle {entity_id} entered unauthorized zone after leaving warehouse",
    "priority": 10
  }'
```

### 4. Monitor Pattern Matches

```bash
# Get all patterns
curl https://your-server/api/v1/cep/patterns

# Get pattern statistics
curl https://your-server/api/v1/cep/patterns/{pattern-id}/stats

# Get active pattern states (in-progress matches)
curl https://your-server/api/v1/cep/patterns/{pattern-id}/active-states

# Get match history
curl https://your-server/api/v1/cep/matches
```

### 5. Test Pattern Against Historical Data

```bash
curl -X POST https://your-server/api/v1/cep/patterns/{pattern-id}/test \
  -H "Content-Type: application/json" \
  -d '{
    "startTime": "2025-01-01T00:00:00Z",
    "endTime": "2025-01-14T23:59:59Z",
    "limit": 100
  }'
```

## Pattern Types

### Sequence Pattern
Ordered events (A then B within time window)

**Example:** Vehicle leaves warehouse, then enters unauthorized zone

```json
{
  "patternType": "sequence",
  "conditions": [
    {"conditionId": "step1", "eventType": "exit", "geofenceNamePattern": "Warehouse-.*"},
    {"conditionId": "step2", "eventType": "enter", "geofenceNamePattern": "Unauthorized-.*", "previousConditionId": "step1"}
  ]
}
```

### Count Pattern
N occurrences within window

**Example:** Entity enters/exits same geofence 3+ times in 1 hour

```json
{
  "patternType": "count",
  "conditions": [
    {"eventType": "exit", "minOccurrences": 3}
  ]
}
```

### Correlation Pattern
Multiple entities performing same action

**Example:** 3+ vehicles enter same geofence within 5 minutes

```json
{
  "patternType": "correlation",
  "conditions": [
    {"eventType": "enter", "minOccurrences": 3, "uniqueEntities": true}
  ]
}
```

### Absence Pattern
Expected event didn't occur

**Example:** Vehicle left checkpoint A but didn't reach checkpoint B within 2 hours

```json
{
  "patternType": "absence",
  "conditions": [
    {"conditionId": "step1", "eventType": "exit", "geofenceNamePattern": "Checkpoint-A", "expected": true},
    {"conditionId": "step2", "eventType": "enter", "geofenceNamePattern": "Checkpoint-B", "expected": false}
  ]
}
```

## Window Types

### Sliding Window
Continuous window that slides with each event. Use for real-time pattern detection.

```json
{
  "windowType": "sliding",
  "windowDurationSeconds": 3600
}
```

### Tumbling Window
Fixed, non-overlapping windows aligned to wall clock. Use for periodic aggregations.

```json
{
  "windowType": "tumbling",
  "windowDurationSeconds": 3600
}
```

### Session Window
Activity-based windows with gap timeout. Use for grouping related events.

```json
{
  "windowType": "session",
  "windowDurationSeconds": 3600,
  "sessionGapSeconds": 300
}
```

## Event Condition Matchers

### Event Type
```json
{"eventType": "enter"}  // or "exit", "dwell", "approach"
```

### Geofence Matching
```json
{"geofenceId": "123e4567-e89b-12d3-a456-426614174000"}
{"geofenceNamePattern": "Warehouse-.*"}
```

### Entity Matching
```json
{"entityId": "vehicle-123"}
{"entityIdPattern": "fleet-A-.*"}
{"entityType": "vehicle"}
```

### Dwell Time (for exit events)
```json
{"minDwellTimeSeconds": 300}
{"maxDwellTimeSeconds": 3600}
```

### Sequence Constraints
```json
{
  "previousConditionId": "step1",
  "maxTimeSincePreviousSeconds": 1800
}
```

### Correlation Constraints
```json
{
  "minOccurrences": 3,
  "uniqueEntities": true
}
```

## Alert Template Variables

Use these placeholders in `alertName` and `alertDescription`:

- `{entity_id}` - Entity identifier
- `{geofence_name}` - Geofence name
- `{geofence_id}` - Geofence identifier
- `{event_type}` - Event type (enter/exit)
- `{entity_type}` - Entity type
- `{dwell_time}` - Dwell time in seconds
- `{dwell_time_minutes}` - Dwell time in minutes
- `{event_time}` - Event timestamp
- `{count}` - Event count (for count patterns)
- `{entity_count}` - Unique entity count (for correlation patterns)

## Architecture

```
GeofenceEvaluationService
  ↓
DurableEventPublisher (Queue)
  ↓
GeofenceEventQueueConsumer
  ├─→ SignalR (Real-time delivery)
  └─→ PatternMatchingEngine (CEP)
       ├─→ Get Active Patterns
       ├─→ Evaluate Event
       ├─→ Update Pattern State
       └─→ Generate Alert (if pattern completes)
```

## Database Tables

- `geofence_event_patterns` - Pattern definitions
- `pattern_match_state` - Active partial matches
- `tumbling_window_state` - Tumbling window aggregations
- `pattern_match_history` - Completed matches (audit trail)

## Performance Tips

1. **Use Specific Patterns**: More specific patterns (geofenceId vs pattern) are faster
2. **Limit Active Patterns**: Keep to <100 active patterns for best performance
3. **Set Appropriate Windows**: Shorter windows = less state to track
4. **Use Pattern Priority**: High-priority patterns evaluated first
5. **Enable Pattern Caching**: Reduces DB lookups for pattern definitions
6. **Monitor State Count**: Run cleanup job regularly

## Monitoring Queries

```sql
-- Count active patterns
SELECT COUNT(*) FROM geofence_event_patterns WHERE enabled = true;

-- Count active states
SELECT COUNT(*) FROM pattern_match_state WHERE window_end > NOW();

-- Pattern performance (last 24 hours)
SELECT * FROM v_cep_pattern_performance;

-- Recent matches
SELECT * FROM pattern_match_history
WHERE created_at > NOW() - INTERVAL '24 hours'
ORDER BY created_at DESC
LIMIT 100;

-- Pattern with most matches
SELECT pattern_name, COUNT(*) as match_count
FROM pattern_match_history
WHERE created_at > NOW() - INTERVAL '7 days'
GROUP BY pattern_id, pattern_name
ORDER BY match_count DESC;
```

## Cleanup

```sql
-- Manual cleanup of expired states
SELECT * FROM honua_cep_cleanup_expired_states(24);

-- Cleanup old match history (>90 days)
DELETE FROM pattern_match_history
WHERE created_at < NOW() - INTERVAL '90 days';
```

## Example Scenarios

### Fleet Management
- Route compliance monitoring
- Delivery time tracking
- Unauthorized zone detection

### Security
- After-hours access detection
- Tailgating detection
- Loitering detection

### Logistics
- Loading dock efficiency
- Checkpoint monitoring
- Convoy detection

### Compliance
- Regulatory zone monitoring
- Dwell time enforcement
- Route auditing

## Troubleshooting

### Pattern Not Matching

1. Check pattern is enabled:
   ```sql
   SELECT * FROM geofence_event_patterns WHERE id = 'pattern-id';
   ```

2. Check event conditions match:
   ```sql
   SELECT * FROM geofence_events WHERE entity_id = 'entity-id' ORDER BY event_time DESC LIMIT 10;
   ```

3. Check active states:
   ```sql
   SELECT * FROM pattern_match_state WHERE pattern_id = 'pattern-id';
   ```

### High Memory Usage

1. Reduce state retention:
   ```sql
   SELECT honua_cep_cleanup_expired_states(12); -- 12 hours instead of 24
   ```

2. Check state count:
   ```sql
   SELECT COUNT(*) FROM pattern_match_state;
   SELECT COUNT(*) FROM tumbling_window_state;
   ```

### Slow Performance

1. Check pattern count:
   ```sql
   SELECT COUNT(*) FROM geofence_event_patterns WHERE enabled = true;
   ```

2. Add indexes if needed:
   ```sql
   CREATE INDEX IF NOT EXISTS idx_custom ON pattern_match_state(custom_field);
   ```

3. Enable pattern caching in configuration

## Next Steps

1. Review full documentation: `/CEP_IMPLEMENTATION_SUMMARY.md`
2. Explore example patterns: `Examples/pattern-examples.json`
3. Create custom patterns for your use case
4. Monitor pattern performance
5. Tune cleanup and retention settings

## Support

For issues or questions:
- Check logs: `PatternMatchingEngine` logger
- Review match history: `pattern_match_history` table
- Test pattern: Use `/api/v1/cep/patterns/{id}/test` endpoint
- Monitor metrics: Use `v_cep_pattern_performance` view
