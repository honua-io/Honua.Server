# Anomaly Detection - Quick Start Guide

## 5-Minute Setup

### 1. Enable the Service

Add to `appsettings.json`:

```json
{
  "Honua": {
    "Enterprise": {
      "SensorThings": {
        "AnomalyDetection": {
          "Enabled": true
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

builder.Services.AddSensorAnomalyDetection(builder.Configuration);
```

### 3. Configure Webhook (Optional)

```json
{
  "AlertDelivery": {
    "WebhookUrl": "https://your-server.com/api/webhooks/anomalies"
  }
}
```

### 4. Run the Application

```bash
dotnet run
```

The service will:
- Start automatically as a background service
- Check for anomalies every 5 minutes
- Send alerts to your webhook (if configured)
- Log to console if no webhook configured

## Webhook Handler Example

```csharp
[ApiController]
[Route("api/webhooks")]
public class WebhookController : ControllerBase
{
    [HttpPost("anomalies")]
    public IActionResult ReceiveAnomaly([FromBody] AnomalyAlert alert)
    {
        var anomaly = alert.Anomaly;

        _logger.LogWarning(
            "ALERT: {Type} on {Sensor} - {Description}",
            anomaly.Type,
            anomaly.SensorName,
            anomaly.Description);

        // Send email, SMS, update dashboard, etc.

        return Ok();
    }
}
```

## Common Configurations

### Smart City Temperature Monitoring

```json
{
  "AnomalyDetection": {
    "Enabled": true,
    "CheckInterval": "00:05:00",
    "StaleSensorDetection": {
      "StaleThreshold": "02:00:00",
      "ThresholdOverrides": {
        "temperature": "02:00:00",
        "humidity": "02:00:00"
      }
    },
    "UnusualReadingDetection": {
      "StandardDeviationThreshold": 2.5,
      "ThresholdOverrides": {
        "temperature": 2.5
      }
    }
  }
}
```

### Traffic Monitoring

```json
{
  "AnomalyDetection": {
    "Enabled": true,
    "CheckInterval": "00:03:00",
    "StaleSensorDetection": {
      "StaleThreshold": "00:30:00",
      "ThresholdOverrides": {
        "traffic_count": "00:30:00",
        "vehicle_speed": "00:30:00"
      }
    },
    "UnusualReadingDetection": {
      "StandardDeviationThreshold": 3.0,
      "MinimumObservationCount": 20
    }
  }
}
```

### Environmental Monitoring

```json
{
  "AnomalyDetection": {
    "Enabled": true,
    "CheckInterval": "00:10:00",
    "StaleSensorDetection": {
      "StaleThreshold": "01:00:00",
      "ThresholdOverrides": {
        "air_quality": "00:15:00",
        "noise_level": "00:15:00",
        "water_quality": "01:00:00"
      }
    },
    "UnusualReadingDetection": {
      "StandardDeviationThreshold": 3.0,
      "ThresholdOverrides": {
        "air_quality": 2.0,
        "noise_level": 3.5
      }
    },
    "RateLimit": {
      "MaxAlertsPerDatastream": 10
    }
  }
}
```

## Testing

### Manual Test - Stale Sensor

1. Create a sensor and datastream
2. Send a few observations
3. Wait for `StaleThreshold` duration
4. The service will detect it as stale and send alert

### Manual Test - Unusual Reading

1. Create a sensor with numeric observations
2. Send 20 observations with values around 20-25
3. Send one observation with value 100
4. The service will detect it as an outlier

### Check Health

```bash
curl http://localhost:5000/health
```

### View Logs

```bash
# Look for anomaly detection logs
grep "anomaly" /var/log/honua.log

# Or in console
docker-compose logs -f honua-server | grep -i anomaly
```

## Next Steps

1. **Configure per-sensor thresholds** based on your sensor types
2. **Set up webhook handler** for alert processing
3. **Enable rate limiting** in production
4. **Monitor health checks** for service status
5. **Review documentation** for advanced features

## Documentation

- Complete Guide: [SENSOR_ANOMALY_DETECTION.md](../../../../docs/features/SENSOR_ANOMALY_DETECTION.md)
- Configuration Reference: [appsettings.anomaly-detection.example.json](appsettings.anomaly-detection.example.json)
- Database Schema: [002_AnomalyDetection.sql](../Data/Migrations/002_AnomalyDetection.sql)

## Need Help?

- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Email: support@honua.io
