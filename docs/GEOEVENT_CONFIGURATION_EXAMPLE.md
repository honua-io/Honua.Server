# GeoEvent Configuration Examples

This document provides configuration examples for Month 3-4 features: Azure Stream Analytics integration, output connectors (webhooks/email), and SignalR real-time streaming.

## appsettings.json Configuration

Add this section to your `appsettings.json` or `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=honua;Username=postgres;Password=your-password"
  },

  "GeoEvent": {
    "Notifications": {
      "Webhook": {
        "Enabled": true,
        "Urls": [
          "https://your-api.com/webhooks/geofence-events",
          "https://backup-api.com/geofence-events"
        ],
        "BatchUrl": "https://your-api.com/webhooks/geofence-events/batch",
        "UseBatchEndpoint": true,
        "TimeoutSeconds": 30,
        "RetryAttempts": 3,
        "AuthenticationHeader": "Bearer your-webhook-secret-token"
      },

      "Email": {
        "Enabled": true,
        "SmtpHost": "smtp.office365.com",
        "SmtpPort": 587,
        "UseSsl": true,
        "SmtpUsername": "geoevent@yourcompany.com",
        "SmtpPassword": "your-smtp-password",
        "From": "geoevent@yourcompany.com",
        "FromName": "Honua GeoEvent Alerts",
        "To": [
          "ops@yourcompany.com",
          "dispatch@yourcompany.com"
        ],
        "Cc": [
          "manager@yourcompany.com"
        ],
        "UseHtml": true
      }
    }
  }
}
```

## Webhook Configuration

### Basic Webhook Setup

**Minimal configuration** (single webhook URL):
```json
{
  "GeoEvent": {
    "Notifications": {
      "Webhook": {
        "Enabled": true,
        "Urls": [
          "https://your-api.com/geofence-events"
        ]
      }
    }
  }
}
```

**What your webhook receives**:
```json
POST /geofence-events
Content-Type: application/json

{
  "event_id": "e1f2g3h4-i5j6-7890-abcd-ef1234567890",
  "event_type": "Enter",
  "event_time": "2025-11-05T10:30:00Z",
  "entity_id": "vehicle-123",
  "entity_type": "delivery_truck",
  "geofence": {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "name": "Downtown Delivery Zone",
    "description": "Main downtown area",
    "properties": {
      "zone_type": "delivery",
      "priority": "high"
    }
  },
  "location": {
    "type": "Point",
    "coordinates": [-122.4144, 37.7799]
  },
  "properties": {
    "speed": 35.5,
    "heading": 180
  },
  "dwell_time_seconds": null,
  "tenant_id": null,
  "created_at": "2025-11-05T10:30:00.123Z"
}
```

### Multiple Webhooks

Send events to multiple endpoints:
```json
{
  "GeoEvent": {
    "Notifications": {
      "Webhook": {
        "Enabled": true,
        "Urls": [
          "https://primary-api.com/events",
          "https://analytics-system.com/geofence-data",
          "https://logging-service.com/events"
        ]
      }
    }
  }
}
```

### Batch Webhooks

For high-volume scenarios, use batch endpoint:
```json
{
  "GeoEvent": {
    "Notifications": {
      "Webhook": {
        "Enabled": true,
        "BatchUrl": "https://your-api.com/batch-events",
        "UseBatchEndpoint": true
      }
    }
  }
}
```

**Batch payload format**:
```json
{
  "event_count": 25,
  "timestamp": "2025-11-05T10:35:00Z",
  "events": [
    { /* event 1 */ },
    { /* event 2 */ },
    ...
  ]
}
```

### Authenticated Webhooks

Add authentication header:
```json
{
  "GeoEvent": {
    "Notifications": {
      "Webhook": {
        "Enabled": true,
        "Urls": ["https://your-api.com/events"],
        "AuthenticationHeader": "Bearer your-secret-token-here"
      }
    }
  }
}
```

## Email Configuration

### Office 365 / Outlook.com

```json
{
  "GeoEvent": {
    "Notifications": {
      "Email": {
        "Enabled": true,
        "SmtpHost": "smtp.office365.com",
        "SmtpPort": 587,
        "UseSsl": true,
        "SmtpUsername": "your-email@company.com",
        "SmtpPassword": "your-password",
        "From": "geoevent@company.com",
        "FromName": "GeoFence Alerts",
        "To": ["recipient@company.com"],
        "UseHtml": true
      }
    }
  }
}
```

### Gmail

```json
{
  "GeoEvent": {
    "Notifications": {
      "Email": {
        "Enabled": true,
        "SmtpHost": "smtp.gmail.com",
        "SmtpPort": 587,
        "UseSsl": true,
        "SmtpUsername": "your-email@gmail.com",
        "SmtpPassword": "your-app-specific-password",
        "From": "your-email@gmail.com",
        "To": ["recipient@gmail.com"]
      }
    }
  }
}
```

**Note**: For Gmail, use an [App-Specific Password](https://support.google.com/accounts/answer/185833), not your regular password.

### SendGrid

```json
{
  "GeoEvent": {
    "Notifications": {
      "Email": {
        "Enabled": true,
        "SmtpHost": "smtp.sendgrid.net",
        "SmtpPort": 587,
        "UseSsl": true,
        "SmtpUsername": "apikey",
        "SmtpPassword": "your-sendgrid-api-key",
        "From": "geoevent@yourcompany.com",
        "To": ["alerts@yourcompany.com"]
      }
    }
  }
}
```

### Multiple Recipients

```json
{
  "GeoEvent": {
    "Notifications": {
      "Email": {
        "Enabled": true,
        "To": [
          "dispatch@company.com",
          "ops@company.com",
          "manager@company.com"
        ],
        "Cc": [
          "notifications@company.com"
        ]
      }
    }
  }
}
```

### Plain Text Emails

Disable HTML formatting:
```json
{
  "GeoEvent": {
    "Notifications": {
      "Email": {
        "Enabled": true,
        "UseHtml": false,
        ...
      }
    }
  }
}
```

## Azure Stream Analytics Integration

### Step 1: Configure ASA Output

In your Azure Stream Analytics job, add HTTP output:

- **Output alias**: `honua-geoevent`
- **URL**: `https://your-honua-server.com/api/v1/azure-sa/webhook`
- **Authentication**: `Bearer`
- **Bearer token**: Your API token
- **Batch size**: `100` - `1000` (optimal)

### Step 2: ASA Query Example

**Simple location forwarding**:
```sql
SELECT
    deviceId as entity_id,
    'iot_device' as entity_type,
    location.lon as longitude,
    location.lat as latitude,
    EventEnqueuedUtcTime as event_time,
    temperature,
    speed,
    battery
INTO [honua-geoevent]
FROM [iothub-input]
WHERE location.lon IS NOT NULL
  AND location.lat IS NOT NULL
```

**With filtering and enrichment**:
```sql
SELECT
    deviceId as entity_id,
    deviceType as entity_type,
    location.lon as longitude,
    location.lat as latitude,
    System.Timestamp() as event_time,
    {
        'temperature': temperature,
        'speed': speed,
        'heading': heading,
        'altitude': altitude,
        'accuracy': location.accuracy
    } as properties
INTO [honua-geoevent]
FROM [iothub-input]
WHERE location.lon IS NOT NULL
  AND location.lat IS NOT NULL
  AND speed > 0  -- Only send when moving
```

**Aggregation example** (batched locations):
```sql
SELECT
    deviceId as entity_id,
    'vehicle' as entity_type,
    AVG(location.lon) as longitude,
    AVG(location.lat) as latitude,
    System.Timestamp() as event_time,
    {
        'avg_speed': AVG(speed),
        'max_speed': MAX(speed),
        'sample_count': COUNT(*)
    } as properties
INTO [honua-geoevent]
FROM [iothub-input]
WHERE location.lon IS NOT NULL
GROUP BY deviceId, TumblingWindow(second, 30)
```

## SignalR Real-Time Streaming

### Client Connection (JavaScript)

```html
<!DOCTYPE html>
<html>
<head>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
</head>
<body>
    <h1>Real-Time Geofence Events</h1>
    <div id="events"></div>

    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/geoevent", {
                accessTokenFactory: () => "your-auth-token-here"
            })
            .withAutomaticReconnect()
            .build();

        // Listen for geofence events
        connection.on("GeofenceEvent", (event) => {
            console.log("Geofence event received:", event);

            const eventDiv = document.createElement("div");
            eventDiv.innerHTML = `
                <strong>${event.eventType}</strong> -
                ${event.entityId} @ ${event.geofenceName}
                (${new Date(event.eventTime).toLocaleTimeString()})
            `;
            document.getElementById("events").prepend(eventDiv);
        });

        // Connect and subscribe
        connection.start()
            .then(() => {
                console.log("Connected to GeoEvent hub");

                // Subscribe to specific entity
                return connection.invoke("SubscribeToEntity", "vehicle-123");
            })
            .then(() => {
                console.log("Subscribed to vehicle-123");
            })
            .catch(err => console.error(err));
    </script>
</body>
</html>
```

### Subscribe to Specific Geofence

```javascript
// Subscribe to events for a specific geofence
await connection.invoke("SubscribeToGeofence", "geofence-uuid-here");

connection.on("GeofenceEvent", (event) => {
    if (event.geofenceId === "geofence-uuid-here") {
        console.log("Event in my geofence:", event);
    }
});
```

### Subscribe to All Events (Admin)

```javascript
// Requires Admin role
await connection.invoke("SubscribeToAll");

connection.on("GeofenceEvent", (event) => {
    // Receive ALL geofence events across all entities and geofences
    console.log("Global event:", event);
});
```

### Event Payload

SignalR broadcasts this payload:
```json
{
  "eventId": "uuid",
  "eventType": "Enter",
  "eventTime": "2025-11-05T10:30:00Z",
  "entityId": "vehicle-123",
  "entityType": "delivery_truck",
  "geofenceId": "uuid",
  "geofenceName": "Downtown Zone",
  "geofenceProperties": { ... },
  "location": {
    "latitude": 37.7749,
    "longitude": -122.4194
  },
  "properties": { ... },
  "dwellTimeSeconds": null,
  "tenantId": null
}
```

## Disable Notifications

To disable webhooks or email:
```json
{
  "GeoEvent": {
    "Notifications": {
      "Webhook": {
        "Enabled": false
      },
      "Email": {
        "Enabled": false
      }
    }
  }
}
```

## Environment Variables

You can also configure via environment variables:
```bash
# Connection string
export ConnectionStrings__Postgres="Host=localhost;Database=honua;..."

# Webhook
export GeoEvent__Notifications__Webhook__Enabled=true
export GeoEvent__Notifications__Webhook__Urls__0="https://api.example.com/events"

# Email
export GeoEvent__Notifications__Email__Enabled=true
export GeoEvent__Notifications__Email__SmtpHost="smtp.gmail.com"
export GeoEvent__Notifications__Email__SmtpUsername="your-email@gmail.com"
```

## Docker Compose Example

```yaml
version: '3.8'
services:
  honua:
    image: honua-server:latest
    ports:
      - "5000:8080"
    environment:
      - ConnectionStrings__Postgres=Host=postgres;Database=honua;Username=postgres;Password=postgres
      - GeoEvent__Notifications__Webhook__Enabled=true
      - GeoEvent__Notifications__Webhook__Urls__0=https://webhook.site/your-unique-url
      - GeoEvent__Notifications__Email__Enabled=false
    depends_on:
      - postgres

  postgres:
    image: postgis/postgis:16-3.4
    environment:
      - POSTGRES_DB=honua
      - POSTGRES_PASSWORD=postgres
    ports:
      - "5432:5432"
```

## Troubleshooting

### Webhooks Not Firing

1. Check webhook is enabled: `"Enabled": true`
2. Verify URL is accessible from server
3. Check logs for HTTP errors
4. Test webhook manually with curl:
   ```bash
   curl -X POST https://your-webhook-url \
     -H "Content-Type: application/json" \
     -d '{"test": "event"}'
   ```

### Email Not Sending

1. Verify SMTP credentials
2. Check firewall allows outbound port 587/465
3. For Gmail: ensure "Less secure app access" is enabled OR use App-Specific Password
4. Check spam folder
5. Review server logs for SMTP errors

### SignalR Connection Issues

1. Ensure hub is registered: `/hubs/geoevent`
2. Check authentication token is valid
3. Verify CORS allows SignalR websockets
4. Check browser console for connection errors

## Security Best Practices

1. **Store secrets securely**: Use Azure Key Vault, AWS Secrets Manager, or environment variables
2. **Webhook authentication**: Always use `AuthenticationHeader` for webhooks
3. **Email credentials**: Use app-specific passwords, not main account passwords
4. **SignalR auth**: Always require authentication (`[Authorize]` attribute)
5. **HTTPS only**: Use HTTPS for all webhook URLs and server endpoints

## Performance Tuning

**Webhook timeout**:
```json
{
  "TimeoutSeconds": 10  // Reduce for faster failure detection
}
```

**Batch processing**:
```json
{
  "UseBatchEndpoint": true,  // Reduce HTTP overhead
  "BatchUrl": "https://your-api.com/batch"
}
```

**Email batching**:
- Email notifier automatically batches when multiple events occur rapidly
- Configure aggregation window in future updates

## Next Steps

- Monitor notification delivery rates
- Set up retry logic for failed webhooks
- Configure notification routing rules (Phase 2)
- Add SMS notifications (Phase 2)
