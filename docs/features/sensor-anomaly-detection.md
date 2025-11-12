# Sensor Anomaly Detection

## Overview

The Sensor Anomaly Detection service automatically monitors sensor data streams for anomalies and sends real-time alerts when issues are detected. This feature is essential for smart city IoT deployments where sensors may fail, go offline, or report unusual readings.

## Features

### 1. Stale Sensor Detection

Detects sensors that have stopped reporting data within expected timeframes.

**Use Cases:**
- Monitor environmental sensors for connectivity issues
- Detect traffic counters that have gone offline
- Alert when air quality sensors stop reporting

**How It Works:**
- Periodically checks all datastreams for recent observations
- Compares time since last observation against configurable thresholds
- Supports per-sensor-type threshold overrides
- Distinguishes between "stale" (recent data missing) and "offline" (never reported)

**Severity Levels:**
- **Info**: Just past threshold (1.0x - 1.5x)
- **Warning**: Moderately overdue (1.5x - 2.0x)
- **Critical**: Severely overdue (> 2.0x threshold)

### 2. Unusual Reading Detection

Identifies observations that are statistical outliers using the standard deviation method.

**Use Cases:**
- Detect faulty temperature sensors reporting impossible values
- Identify traffic counting errors or anomalies
- Alert on air quality spikes that deviate from normal patterns

**How It Works:**
- Calculates mean and standard deviation over a rolling time window
- Flags readings that exceed N standard deviations from the mean (default: 3σ)
- Requires minimum observation count for statistical validity
- Supports per-sensor-type threshold and minimum count overrides

**Statistical Method:**
```
For each observation value V:
  deviation = |V - mean| / std_dev
  if deviation >= threshold:
    flag as anomaly
```

**Severity Levels:**
- **Info**: 1.0x - 1.5x threshold (e.g., 3.0σ - 4.5σ)
- **Warning**: 1.5x - 2.0x threshold (e.g., 4.5σ - 6.0σ)
- **Critical**: > 2.0x threshold (e.g., > 6.0σ)

### 3. Alert Delivery

Sends alerts via webhooks when anomalies are detected.

**Features:**
- Primary and backup webhook URLs
- Automatic retries with configurable backoff
- Rate limiting to prevent alert flooding
- Integration with GeoEvent API for event processing

**Alert Payload Structure:**
```json
{
  "alertId": "uuid",
  "eventType": "sensor.anomaly.detected",
  "timestamp": "2025-11-10T12:00:00Z",
  "anomaly": {
    "id": "uuid",
    "type": "StaleSensor",
    "severity": "Warning",
    "datastreamId": "ds-123",
    "datastreamName": "Temperature Sensor 1",
    "thingId": "thing-456",
    "thingName": "Weather Station A",
    "sensorId": "sensor-789",
    "sensorName": "DHT22",
    "observedPropertyName": "temperature",
    "description": "Sensor has not reported data for 2.5 hours",
    "lastObservationTime": "2025-11-10T09:30:00Z",
    "timeSinceLastObservation": "02:30:00",
    "metadata": {
      "threshold": "01:00:00",
      "expected_interval": 60.0,
      "actual_interval": 150.0
    }
  },
  "tenantId": "acme-corp",
  "context": {
    "source": "honua.sensor.anomaly_detection",
    "version": "1.0"
  }
}
```

### 4. Rate Limiting

Prevents overwhelming alert systems with duplicate or excessive alerts.

**Features:**
- Per-datastream rate limiting
- Global rate limiting across all sensors
- Configurable time windows and alert counts
- Automatic suppression of duplicate alerts

**Configuration:**
```json
{
  "rateLimit": {
    "enabled": true,
    "maxAlertsPerDatastream": 5,
    "rateLimitWindow": "01:00:00",
    "maxTotalAlerts": 100
  }
}
```

## Configuration

### Basic Configuration

Add to `appsettings.json`:

```json
{
  "Honua": {
    "Enterprise": {
      "SensorThings": {
        "AnomalyDetection": {
          "Enabled": true,
          "CheckInterval": "00:05:00"
        }
      }
    }
  }
}
```

### Complete Configuration Example

See [appsettings.anomaly-detection.example.json](../../src/Honua.Server.Enterprise/Sensors/AnomalyDetection/appsettings.anomaly-detection.example.json) for all configuration options.

### Environment-Specific Configuration

**Development:**
```json
{
  "AnomalyDetection": {
    "Enabled": true,
    "CheckInterval": "00:01:00",
    "AlertDelivery": {
      "WebhookUrl": "http://localhost:5000/api/v1/webhooks/test"
    }
  }
}
```

**Production:**
```json
{
  "AnomalyDetection": {
    "Enabled": true,
    "CheckInterval": "00:05:00",
    "AlertDelivery": {
      "WebhookUrl": "https://prod-server.com/api/v1/geoevent/webhooks/anomalies",
      "AdditionalWebhooks": [
        "https://backup.com/webhooks/alerts"
      ],
      "EnableRetries": true,
      "MaxRetries": 3
    },
    "RateLimit": {
      "Enabled": true,
      "MaxAlertsPerDatastream": 5,
      "RateLimitWindow": "01:00:00"
    }
  }
}
```

## Integration

### Service Registration

Add to `Program.cs` or `Startup.cs`:

```csharp
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Extensions;

// In ConfigureServices or builder.Services:
services.AddSensorAnomalyDetection(configuration);
```

### Health Checks

The service automatically registers a health check at `/health`:

```bash
curl http://localhost:5000/health
```

Response:
```json
{
  "status": "Healthy",
  "checks": {
    "anomaly_detection": {
      "status": "Healthy",
      "description": "Anomaly detection is running normally",
      "data": {
        "last_check": "2025-11-10T12:05:00Z",
        "last_successful_check": "2025-11-10T12:05:00Z",
        "time_since_last_check_seconds": 30.5
      }
    }
  }
}
```

### Webhook Integration

To receive alerts, implement a webhook endpoint:

```csharp
[ApiController]
[Route("api/v1/webhooks")]
public class AnomalyWebhookController : ControllerBase
{
    [HttpPost("anomalies")]
    public async Task<IActionResult> ReceiveAnomaly([FromBody] AnomalyAlert alert)
    {
        _logger.LogWarning(
            "Anomaly detected: {Type} - {Description}",
            alert.Anomaly.Type,
            alert.Anomaly.Description);

        // Process the alert (e.g., send email, SMS, update dashboard)
        await ProcessAnomalyAsync(alert);

        return Ok();
    }
}
```

## Database Schema

The service automatically creates the `sta_anomaly_alerts` table for rate limiting:

```sql
CREATE TABLE sta_anomaly_alerts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    datastream_id UUID NOT NULL,
    anomaly_type VARCHAR(50) NOT NULL,
    tenant_id VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_sta_anomaly_alerts_datastream
    ON sta_anomaly_alerts(datastream_id, created_at DESC);

CREATE INDEX idx_sta_anomaly_alerts_tenant
    ON sta_anomaly_alerts(tenant_id, created_at DESC);
```

## Performance Considerations

### Query Optimization

The service uses efficient database queries with indexes on:
- `phenomenon_time` for recent observation lookups
- `datastream_id` for statistics calculation
- `organization_id` for multi-tenancy filtering

### Recommended Settings

For large deployments (> 1000 sensors):
- Set `CheckInterval` to 5-10 minutes
- Enable rate limiting
- Use per-sensor-type thresholds to reduce false positives
- Consider horizontal scaling with multiple instances

### Database Performance

- The statistics calculation uses window functions for efficiency
- Indexes on `sta_observations(datastream_id, phenomenon_time)` are critical
- Consider partitioning `sta_observations` by time for large datasets

## Multi-Tenancy

The service fully supports multi-tenancy:

```csharp
// Detect anomalies for specific tenant
var anomalies = await staleSensorService.DetectStaleDatastreamsAsync(
    tenantId: "acme-corp",
    ct: cancellationToken);
```

Alerts include `tenantId` for routing and isolation.

## Monitoring and Troubleshooting

### Logging

The service provides structured logging at multiple levels:

```
[Information] Starting anomaly detection cycle
[Information] Found 3 stale sensor anomalies
[Warning] Stale sensor detected: TempSensor1 (Temperature) - last observation 2.5 hours ago
[Information] Sending 3 anomaly alerts
[Information] Alert delivery completed: 3 sent, 0 skipped, 0 failed
```

### Common Issues

**No alerts are being sent:**
1. Check that `Enabled: true` in configuration
2. Verify webhook URL is accessible
3. Check logs for errors
4. Ensure sensors are actually reporting data
5. Verify rate limits are not exceeded

**Too many false positives:**
1. Adjust `StaleThreshold` to be more lenient
2. Use per-sensor-type threshold overrides
3. Increase `StandardDeviationThreshold` for unusual readings
4. Increase `MinimumObservationCount` to require more data

**Alerts are rate limited:**
1. Increase `MaxAlertsPerDatastream` or `MaxTotalAlerts`
2. Increase `RateLimitWindow` duration
3. Review why so many alerts are being generated

## Testing

### Unit Tests

```bash
dotnet test --filter "Category=AnomalyDetection"
```

### Integration Tests

Requires PostgreSQL database:

```bash
export ConnectionStrings__SensorThingsDb="Host=localhost;Database=honua_test;..."
dotnet test Honua.Server.Integration.Tests --filter "AnomalyDetection"
```

### Manual Testing

1. Start the service with anomaly detection enabled
2. Create test sensors and datastreams
3. Simulate stale sensors by not sending observations
4. Simulate unusual readings by sending outlier values
5. Monitor webhook endpoint for alerts

## API Reference

### Services

- `IStaleSensorDetectionService` - Detects stale sensors
- `IUnusualReadingDetectionService` - Detects statistical outliers
- `IAnomalyAlertService` - Delivers alerts via webhooks
- `IAnomalyDetectionRepository` - Data access layer

### Models

- `SensorAnomaly` - Detected anomaly details
- `AnomalyAlert` - Alert payload sent to webhooks
- `DatastreamStatistics` - Statistical summary for outlier detection
- `AlertDeliveryResult` - Result of alert delivery operation

## Future Enhancements

Potential improvements for future versions:

1. **Machine Learning Models**: Replace simple statistical methods with ML models
2. **Adaptive Thresholds**: Automatically adjust thresholds based on historical patterns
3. **Trend Analysis**: Detect gradual sensor degradation over time
4. **Correlation Analysis**: Identify groups of related sensor failures
5. **Predictive Maintenance**: Predict sensor failures before they occur
6. **Dashboard Integration**: Real-time anomaly visualization
7. **Notification Channels**: Email, SMS, Slack, Teams integrations
8. **Alert Acknowledgment**: Manual ack/dismiss of alerts

## Support

For issues or questions:
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Documentation: https://docs.honua.io
- Email: support@honua.io

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
