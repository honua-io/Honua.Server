# Sensor Anomaly Detection

Automated anomaly detection for smart city sensors with real-time alerting.

## Quick Start

### 1. Enable in Configuration

Add to `appsettings.json`:

```json
{
  "Honua": {
    "Enterprise": {
      "SensorThings": {
        "AnomalyDetection": {
          "Enabled": true,
          "CheckInterval": "00:05:00",
          "StaleSensorDetection": {
            "Enabled": true,
            "StaleThreshold": "01:00:00"
          },
          "UnusualReadingDetection": {
            "Enabled": true,
            "StandardDeviationThreshold": 3.0
          },
          "AlertDelivery": {
            "WebhookUrl": "https://your-server.com/api/webhooks/anomalies"
          }
        }
      }
    }
  }
}
```

### 2. Register Services

In `Program.cs`:

```csharp
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Extensions;

// Add anomaly detection services
builder.Services.AddSensorAnomalyDetection(builder.Configuration);
```

### 3. Run the Application

The background service will automatically start and begin monitoring sensors.

## Features

### Stale Sensor Detection
- Detects sensors with no recent data
- Configurable thresholds per sensor type
- Automatic offline/stale classification

### Unusual Reading Detection
- Statistical outlier detection (3σ method)
- Rolling window statistics
- Per-sensor-type sensitivity

### Alert Delivery
- Webhook integration (GeoEvent API compatible)
- Automatic retries
- Rate limiting to prevent flooding

### Multi-Tenancy
- Tenant isolation
- Per-tenant configuration support

## Architecture

```
AnomalyDetectionBackgroundService (orchestrator)
    ├── StaleSensorDetectionService
    │   └── PostgresAnomalyDetectionRepository
    ├── UnusualReadingDetectionService
    │   └── PostgresAnomalyDetectionRepository
    └── AnomalyAlertService
        └── HttpClient (webhook delivery)
```

## Configuration

See [appsettings.anomaly-detection.example.json](appsettings.anomaly-detection.example.json) for complete configuration options.

### Key Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `false` | Master switch for anomaly detection |
| `CheckInterval` | `00:05:00` | How often to check for anomalies |
| `StaleThreshold` | `01:00:00` | Default stale sensor threshold |
| `StandardDeviationThreshold` | `3.0` | Outlier detection sensitivity |
| `MaxAlertsPerDatastream` | `5` | Rate limit per sensor |

## Alert Payload

```json
{
  "alertId": "uuid",
  "eventType": "sensor.anomaly.detected",
  "timestamp": "2025-11-10T12:00:00Z",
  "anomaly": {
    "type": "StaleSensor|UnusualReading|SensorOffline",
    "severity": "Info|Warning|Critical",
    "datastreamName": "Temperature Sensor 1",
    "description": "Human-readable description",
    "metadata": { /* context-specific data */ }
  }
}
```

## Database

The service automatically creates the `sta_anomaly_alerts` table for rate limiting tracking.

No manual migration required - table is created on first use.

## Health Checks

Automatic health check registered at `/health` endpoint:

```bash
curl http://localhost:5000/health
```

## Performance

### Optimization Tips

1. **Indexes**: Ensure indexes on `sta_observations(datastream_id, phenomenon_time)`
2. **Check Interval**: Use 5-10 minutes for large deployments (> 1000 sensors)
3. **Statistical Window**: 24 hours is optimal for most use cases
4. **Rate Limiting**: Enable to prevent alert flooding

### Expected Performance

- **Stale Detection**: O(1) query per datastream
- **Unusual Detection**: O(n) where n = observations in window
- **Scalability**: Tested up to 10,000 datastreams

## Testing

### Unit Tests

```bash
dotnet test --filter "StaleSensorDetectionServiceTests"
```

### Integration Tests

```bash
export ConnectionStrings__SensorThingsDb="..."
dotnet test --filter "AnomalyDetectionIntegrationTests"
```

## Troubleshooting

### No Alerts Sent

1. Check `Enabled: true`
2. Verify webhook URL is accessible
3. Check logs for errors
4. Verify sensors are reporting data

### Too Many False Positives

1. Increase `StaleThreshold`
2. Increase `StandardDeviationThreshold`
3. Use per-sensor-type overrides
4. Increase `MinimumObservationCount`

### Rate Limited

1. Increase `MaxAlertsPerDatastream`
2. Increase `RateLimitWindow`
3. Check why alerts are being generated

## Documentation

- [Complete Feature Documentation](../../../../docs/features/SENSOR_ANOMALY_DETECTION.md)
- [Configuration Example](appsettings.anomaly-detection.example.json)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
