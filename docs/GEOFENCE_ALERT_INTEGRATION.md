# Geofence-Alert System Integration

## Overview

This document describes the integration between the Honua GeoEvent geofencing system and the Alert system, enabling automated alert generation when entities enter, exit, or dwell in geofenced areas.

## Architecture

### Components

1. **GeofenceToAlertBridgeService** - Core service that converts geofence events into generic alerts
2. **GeofenceAlertRepository** - Database repository for managing alert rules, silencing rules, and correlations
3. **Database Tables** - Three new tables for correlation tracking, alert rules, and silencing rules
4. **API Endpoints** - RESTful admin endpoints for managing rules and querying alerts
5. **Integration Hook** - Automatic event processing in `GeofenceEvaluationService`

### Data Flow

```
Geofence Event Generated
    ↓
GeofenceEvaluationService
    ↓
GeofenceToAlertBridgeService.ProcessGeofenceEventAsync()
    ↓
1. Check silencing rules
2. Find matching alert rules
3. Apply deduplication
4. Convert to GenericAlert
5. Send to AlertReceiver
6. Track correlation
```

## Database Schema

### Migration: 031_GeofenceAlertIntegration.sql

Three new tables:

#### 1. geofence_alert_correlation
Tracks correlation between geofence events and generated alerts.

**Key Fields:**
- `geofence_event_id` (UUID, PK) - Links to geofence_events
- `alert_fingerprint` (VARCHAR) - Unique alert identifier
- `alert_history_id` (BIGINT) - Soft reference to alert_history
- `alert_severity` (VARCHAR) - critical, high, medium, low, info
- `alert_status` (VARCHAR) - active, resolved, silenced, acknowledged
- `was_silenced` (BOOLEAN) - Whether silencing rule was applied

#### 2. geofence_alert_rules
Advanced matching rules for geofence events.

**Key Fields:**
- `geofence_id` (UUID) - Specific geofence (optional)
- `geofence_name_pattern` (VARCHAR) - Regex pattern for geofence names
- `event_types` (VARCHAR[]) - Array: enter, exit, dwell, approach
- `entity_id_pattern` (VARCHAR) - Regex pattern for entity IDs
- `min_dwell_time_seconds` (INT) - Minimum dwell time threshold
- `max_dwell_time_seconds` (INT) - Maximum dwell time threshold
- `alert_name_template` (VARCHAR) - Template with placeholders
- `alert_description_template` (TEXT) - Detailed description template
- `notification_channel_ids` (JSONB) - Array of channel IDs
- `deduplication_window_minutes` (INT) - Prevent duplicate alerts

**Template Placeholders:**
- `{entity_id}` - Entity identifier
- `{geofence_name}` - Geofence name
- `{geofence_id}` - Geofence UUID
- `{event_type}` - enter/exit/dwell/approach
- `{entity_type}` - Entity type
- `{dwell_time}` - Dwell time in seconds
- `{dwell_time_minutes}` - Dwell time in minutes
- `{event_time}` - Event timestamp

#### 3. geofence_alert_silencing
Silencing rules for maintenance windows and scheduled downtime.

**Key Fields:**
- `start_time` / `end_time` (TIMESTAMPTZ) - Fixed time window
- `recurring_schedule` (JSONB) - Recurring schedule configuration
  - `days`: Array of day-of-week (0=Sunday, 6=Saturday)
  - `start_hour`: Hour of day (0-23)
  - `end_hour`: Hour of day (0-23)

### Database Functions

#### honua_should_silence_geofence_alert()
Checks if an alert should be silenced based on active silencing rules.

**Parameters:**
- `p_geofence_id` (UUID)
- `p_geofence_name` (VARCHAR)
- `p_entity_id` (VARCHAR)
- `p_event_type` (VARCHAR)
- `p_event_time` (TIMESTAMPTZ)
- `p_tenant_id` (VARCHAR, optional)

**Returns:** BOOLEAN

#### honua_find_matching_geofence_alert_rules()
Finds all alert rules that match a given geofence event.

**Parameters:**
- All geofence event attributes

**Returns:** TABLE of matching rules

## API Endpoints

All endpoints are under `/admin/geofence-alerts` and require authentication.

### Alert Rules

#### List Rules
```
GET /admin/geofence-alerts/rules?tenantId=xxx&enabledOnly=true
```

#### Get Rule
```
GET /admin/geofence-alerts/rules/{id}
```

#### Create Rule
```
POST /admin/geofence-alerts/rules
Content-Type: application/json

{
  "name": "Restricted Zone Entry Alert",
  "description": "Alert when unauthorized vehicles enter restricted zones",
  "enabled": true,
  "geofenceNamePattern": "Restricted.*",
  "eventTypes": ["enter"],
  "entityIdPattern": "vehicle-.*",
  "alertSeverity": "critical",
  "alertNameTemplate": "Unauthorized Entry: {entity_id} entered {geofence_name}",
  "alertDescriptionTemplate": "Vehicle {entity_id} entered restricted geofence {geofence_name} at {event_time}",
  "notificationChannelIds": [1, 2, 3],
  "deduplicationWindowMinutes": 60
}
```

#### Update Rule
```
PUT /admin/geofence-alerts/rules/{id}
```

#### Delete Rule
```
DELETE /admin/geofence-alerts/rules/{id}
```

### Silencing Rules

#### List Silencing Rules
```
GET /admin/geofence-alerts/silencing?tenantId=xxx&enabledOnly=true
```

#### Create Silencing Rule
```
POST /admin/geofence-alerts/silencing
Content-Type: application/json

{
  "name": "Maintenance Window - Weekdays 9am-5pm",
  "enabled": true,
  "geofenceNamePattern": "Construction.*",
  "eventTypes": ["enter", "exit"],
  "recurringSchedule": {
    "days": [1, 2, 3, 4, 5],
    "start_hour": 9,
    "end_hour": 17
  }
}
```

### Active Alerts & Correlations

#### Get Active Alerts
```
GET /admin/geofence-alerts/active?tenantId=xxx
```

Returns all currently active alerts generated from geofence events.

#### Get Correlation
```
GET /admin/geofence-alerts/correlation/{geofenceEventId}
```

Returns alert correlation for a specific geofence event.

## Configuration

### Enable Alert Integration

In your application startup (e.g., `Program.cs` or DI configuration):

```csharp
// Add GeoEvent services with alert integration
services.AddGeoEventServices(
    connectionString: Configuration.GetConnectionString("DefaultConnection"),
    configuration: Configuration,
    alertReceiverBaseUrl: "http://localhost:5001" // Alert receiver service URL
);
```

### Configuration Options

**alertReceiverBaseUrl** (optional)
- If provided: Enables geofence-to-alert integration
- If null: Geofence events are processed normally without alert generation

### Alert Receiver Configuration

Ensure the AlertReceiver service is running and accessible at the configured URL.

Default endpoint: `POST /api/alerts`

## Usage Examples

### Example 1: Alert on Restricted Zone Entry

**Scenario:** Alert security team when any vehicle enters a restricted zone.

```json
{
  "name": "Restricted Zone Entry",
  "enabled": true,
  "geofenceNamePattern": "Restricted.*",
  "eventTypes": ["enter"],
  "entityType": "vehicle",
  "alertSeverity": "critical",
  "alertNameTemplate": "Security Alert: {entity_id} entered {geofence_name}",
  "notificationChannelIds": [1, 2], // Slack + PagerDuty
  "deduplicationWindowMinutes": 30
}
```

### Example 2: Alert on Long Dwell Time

**Scenario:** Alert if a delivery truck stays in a loading zone for more than 30 minutes.

```json
{
  "name": "Loading Zone Overstay",
  "enabled": true,
  "geofenceNamePattern": "Loading Zone.*",
  "eventTypes": ["exit"],
  "entityIdPattern": "truck-.*",
  "minDwellTimeSeconds": 1800,
  "alertSeverity": "medium",
  "alertNameTemplate": "{entity_id} overstayed in {geofence_name} for {dwell_time_minutes} minutes",
  "notificationChannelIds": [3], // Email
  "deduplicationWindowMinutes": 120
}
```

### Example 3: Silence Alerts During Maintenance

**Scenario:** Silence all geofence alerts for a specific area during a maintenance window.

```json
{
  "name": "Maintenance Window - Zone A",
  "enabled": true,
  "geofenceNamePattern": "Zone-A-.*",
  "eventTypes": ["enter", "exit"],
  "startTime": "2025-11-10T00:00:00Z",
  "endTime": "2025-11-10T08:00:00Z"
}
```

### Example 4: Recurring Silencing (Business Hours)

**Scenario:** Silence parking alerts during business hours (9am-5pm weekdays).

```json
{
  "name": "Business Hours Silencing",
  "enabled": true,
  "geofenceNamePattern": "Parking-Lot-.*",
  "eventTypes": ["enter"],
  "recurringSchedule": {
    "days": [1, 2, 3, 4, 5],
    "start_hour": 9,
    "end_hour": 17
  }
}
```

## Deduplication

The bridge service implements two levels of deduplication:

1. **Rule-level deduplication** - Per-rule deduplication window (default: 60 minutes)
   - Prevents the same entity/geofence/rule combination from triggering multiple alerts
   - Configurable per rule via `deduplicationWindowMinutes`

2. **Fingerprint-based deduplication** - Global deduplication across all events
   - Fingerprint format: `gf-{sha256(geofence:entity:rule:event_type)}`
   - Cached in-memory for 24 hours

## Performance Considerations

1. **Async Processing** - Alert generation runs in background tasks to avoid blocking geofence evaluation
2. **In-Memory Caching** - Recent alerts cached to prevent duplicate processing
3. **Database Indexes** - All query patterns are indexed for fast lookups
4. **Regex Performance** - Use specific patterns instead of `.*` where possible

## Monitoring

### Key Metrics

- Alert generation rate
- Silenced event count
- Average processing time
- Failed alert submissions
- Active alert count

### Logs

The bridge service logs at various levels:

- **Debug**: Event processing details
- **Info**: Alert creation, rule matching
- **Warning**: Failed alert submissions, queue failures
- **Error**: Processing exceptions

### Example Log Messages

```
[INFO] Found 2 matching alert rules for geofence event {EventId}
[INFO] Successfully created alert {Fingerprint} for geofence event {EventId} using rule {RuleName}
[WARN] Alert fingerprint {Fingerprint} is within deduplication window, skipping
[INFO] Geofence event {EventId} silenced by silencing rules
```

## Troubleshooting

### Alerts Not Being Generated

1. **Check if bridge service is configured**
   - Verify `alertReceiverBaseUrl` is set in DI configuration
   - Check logs for "Processing geofence event" messages

2. **Verify alert rules**
   - Ensure rules are enabled
   - Check regex patterns match expected values
   - Verify event types are included in rule

3. **Check silencing rules**
   - Disable all silencing rules temporarily
   - Check if event is within recurring schedule window

4. **Check deduplication**
   - Verify deduplication window has passed
   - Check fingerprint cache

### Alerts Not Reaching Notification Channels

This is typically an AlertReceiver issue, not a geofence integration issue.

1. Check AlertReceiver logs
2. Verify notification channel configuration
3. Test notification channels independently

### High Alert Volume (Alert Storm)

1. **Increase deduplication window** - Set longer deduplication periods
2. **Add silencing rules** - Silence noisy geofences or entities
3. **Adjust event types** - Disable dwell events if not needed
4. **Use regex patterns** - Filter to specific entities or geofences

## Migration Guide

### Applying the Migration

```bash
cd /home/user/Honua.Server/src/Honua.Server.Core/Data/Migrations
psql -U postgres -d honua -f 031_GeofenceAlertIntegration.sql
```

### Verification

```sql
-- Verify tables exist
SELECT table_name FROM information_schema.tables
WHERE table_name IN ('geofence_alert_correlation', 'geofence_alert_rules', 'geofence_alert_silencing');

-- Verify functions exist
SELECT routine_name FROM information_schema.routines
WHERE routine_name LIKE 'honua_%geofence_alert%';

-- Verify view exists
SELECT table_name FROM information_schema.views
WHERE table_name = 'v_active_geofence_alerts';
```

## Security Considerations

1. **Authentication** - All admin endpoints require authentication
2. **Multi-tenancy** - Rules support tenant isolation via `tenant_id`
3. **Input Validation** - Regex patterns are validated before execution
4. **SQL Injection** - All queries use parameterized statements

## Future Enhancements

### Phase 2
- Geofence approach events (early warning)
- Dwell time tracking (real-time)
- Alert aggregation (multiple events → single alert)
- ML-based anomaly detection

### Phase 3
- Dashboard widgets for active alerts
- Alert history analytics
- Custom webhook payloads
- Conditional silencing (e.g., "silence if speed < 10 km/h")

## Related Documentation

- [GeoEvent Geofencing](../src/Honua.Server.Core/Data/Migrations/017_GeoEventGeofencing.sql)
- [Alert System](../src/Honua.Server.AlertReceiver/README.md)
- [Notification Channels](../src/Honua.Server.Core/Services/AlertConfigurationService.cs)

## Support

For questions or issues, please contact the Honua development team or open an issue on GitHub.
