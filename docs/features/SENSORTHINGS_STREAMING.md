# SensorThings API - Real-Time WebSocket Streaming

## Overview

Honua Server provides real-time WebSocket streaming for sensor observations using SignalR. This enables smart city dashboards and IoT applications to receive live sensor data as it arrives, without polling.

## Features

- **Push-based delivery**: Observations are pushed to clients in real-time
- **Flexible subscriptions**: Subscribe to individual datastreams, things, sensors, or all observations
- **Automatic batching**: High-volume sensors are automatically batched to prevent overwhelming clients
- **Rate limiting**: Configurable rate limits protect clients from data floods
- **Multi-tenancy support**: Clients only receive data they're authorized to access
- **Reconnection handling**: Built-in support for automatic reconnection

## Configuration

### Server Configuration

Enable WebSocket streaming in `appsettings.json`:

```json
{
  "SensorThings": {
    "Enabled": true,
    "BasePath": "/sta/v1.1",
    "WebSocketStreamingEnabled": true,
    "Streaming": {
      "Enabled": true,
      "RateLimitingEnabled": true,
      "RateLimitPerSecond": 100,
      "BatchingEnabled": true,
      "BatchingThreshold": 100
    }
  }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable/disable WebSocket streaming |
| `RateLimitingEnabled` | bool | `true` | Enable rate limiting per subscription group |
| `RateLimitPerSecond` | int | `100` | Max observations per second per group |
| `BatchingEnabled` | bool | `true` | Enable automatic batching for high-volume sensors |
| `BatchingThreshold` | int | `100` | Batch observations if more than this many per second |

## Client Usage

### JavaScript/TypeScript

Install the SignalR client:

```bash
npm install @microsoft/signalr
```

#### Basic Connection

```javascript
import * as signalR from "@microsoft/signalr";

// Create connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/sensor-observations", {
        accessTokenFactory: () => getAuthToken()
    })
    .withAutomaticReconnect()
    .build();

// Handle incoming observations
connection.on("ObservationCreated", (observation) => {
    console.log("New observation:", observation);
    updateDashboard(observation);
});

// Handle batched observations
connection.on("ObservationsBatch", (batch) => {
    console.log(`Received batch of ${batch.count} observations`);
    batch.observations.forEach(obs => updateDashboard(obs));
});

// Handle subscription confirmations
connection.on("Subscribed", (info) => {
    console.log(`Subscribed to ${info.type}: ${info.id}`);
});

// Start connection
await connection.start();
console.log("Connected to sensor observation stream");
```

#### Subscribe to Datastream

```javascript
// Subscribe to a specific datastream
const datastreamId = "temperature-sensor-downtown-1";
await connection.invoke("SubscribeToDatastream", datastreamId);

// Unsubscribe when done
await connection.invoke("UnsubscribeFromDatastream", datastreamId);
```

#### Subscribe to Thing (Device)

```javascript
// Subscribe to all datastreams from a specific thing
const thingId = "weather-station-1";
await connection.invoke("SubscribeToThing", thingId);

// This will receive observations from all sensors on this device
```

#### Subscribe to Sensor Type

```javascript
// Subscribe to all datastreams using a specific sensor
const sensorId = "temperature-sensor-model-xyz";
await connection.invoke("SubscribeToSensor", sensorId);
```

#### Subscribe to All Observations (Admin Only)

```javascript
// Subscribe to all observations (requires admin role)
await connection.invoke("SubscribeToAll");
```

### TypeScript Example

```typescript
import * as signalR from "@microsoft/signalr";

interface Observation {
    observationId: string;
    datastreamId: string;
    datastreamName: string;
    thingId: string;
    sensorId: string;
    phenomenonTime: string;
    resultTime?: string;
    result: any;
    unitOfMeasurement: {
        name: string;
        symbol: string;
        definition: string;
    };
    serverTimestamp: string;
}

interface ObservationBatch {
    datastreamId: string;
    thingId: string;
    sensorId: string;
    count: number;
    observations: Observation[];
}

class SensorDataClient {
    private connection: signalR.HubConnection;
    private handlers: Map<string, (obs: Observation) => void> = new Map();

    constructor(authTokenProvider: () => string) {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/sensor-observations", {
                accessTokenFactory: authTokenProvider
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: (retryContext) => {
                    // Exponential backoff
                    return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                }
            })
            .build();

        this.setupHandlers();
    }

    private setupHandlers(): void {
        this.connection.on("ObservationCreated", (obs: Observation) => {
            const handler = this.handlers.get(obs.datastreamId);
            if (handler) {
                handler(obs);
            }
        });

        this.connection.on("ObservationsBatch", (batch: ObservationBatch) => {
            const handler = this.handlers.get(batch.datastreamId);
            if (handler) {
                batch.observations.forEach(handler);
            }
        });

        this.connection.onreconnecting(() => {
            console.log("Reconnecting to sensor stream...");
        });

        this.connection.onreconnected(async () => {
            console.log("Reconnected! Re-subscribing...");
            // Re-subscribe to all datastreams
            for (const datastreamId of this.handlers.keys()) {
                await this.connection.invoke("SubscribeToDatastream", datastreamId);
            }
        });

        this.connection.onclose(() => {
            console.error("Connection closed");
        });
    }

    async connect(): Promise<void> {
        await this.connection.start();
    }

    async subscribeToDatastream(
        datastreamId: string,
        handler: (obs: Observation) => void
    ): Promise<void> {
        this.handlers.set(datastreamId, handler);
        await this.connection.invoke("SubscribeToDatastream", datastreamId);
    }

    async unsubscribeFromDatastream(datastreamId: string): Promise<void> {
        this.handlers.delete(datastreamId);
        await this.connection.invoke("UnsubscribeFromDatastream", datastreamId);
    }

    async disconnect(): Promise<void> {
        await this.connection.stop();
    }
}

// Usage
const client = new SensorDataClient(() => localStorage.getItem("authToken")!);

await client.connect();

await client.subscribeToDatastream("temp-sensor-1", (observation) => {
    console.log(`Temperature: ${observation.result}°C at ${observation.phenomenonTime}`);
    updateChart(observation);
});
```

### React Hook Example

```typescript
import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

interface Observation {
    observationId: string;
    datastreamId: string;
    result: any;
    phenomenonTime: string;
    unitOfMeasurement: {
        symbol: string;
    };
}

export function useSensorStream(datastreamId: string, authToken: string) {
    const [observation, setObservation] = useState<Observation | null>(null);
    const [isConnected, setIsConnected] = useState(false);
    const [error, setError] = useState<Error | null>(null);

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/sensor-observations', {
                accessTokenFactory: () => authToken
            })
            .withAutomaticReconnect()
            .build();

        connection.on('ObservationCreated', (obs: Observation) => {
            if (obs.datastreamId === datastreamId) {
                setObservation(obs);
            }
        });

        connection.onreconnecting(() => setIsConnected(false));
        connection.onreconnected(() => setIsConnected(true));
        connection.onclose(() => setIsConnected(false));

        async function start() {
            try {
                await connection.start();
                await connection.invoke('SubscribeToDatastream', datastreamId);
                setIsConnected(true);
            } catch (err) {
                setError(err as Error);
            }
        }

        start();

        return () => {
            connection.stop();
        };
    }, [datastreamId, authToken]);

    return { observation, isConnected, error };
}

// Usage in component
function TemperatureDashboard() {
    const { observation, isConnected } = useSensorStream(
        'temperature-sensor-1',
        getAuthToken()
    );

    return (
        <div>
            <div>Status: {isConnected ? 'Connected' : 'Disconnected'}</div>
            {observation && (
                <div>
                    Temperature: {observation.result}{observation.unitOfMeasurement.symbol}
                    <br />
                    Time: {new Date(observation.phenomenonTime).toLocaleString()}
                </div>
            )}
        </div>
    );
}
```

### Python Client Example

```python
from signalrcore.hub_connection_builder import HubConnectionBuilder
import logging

class SensorStreamClient:
    def __init__(self, url, auth_token):
        self.connection = HubConnectionBuilder() \
            .with_url(url,
                     options={
                         "access_token_factory": lambda: auth_token,
                     }) \
            .configure_logging(logging.INFO) \
            .with_automatic_reconnect({
                "type": "interval",
                "intervals": [1, 2, 5, 10, 30]
            }) \
            .build()

        self.connection.on_open(lambda: print("Connected to sensor stream"))
        self.connection.on_close(lambda: print("Disconnected"))
        self.connection.on("ObservationCreated", self.on_observation)
        self.connection.on("ObservationsBatch", self.on_batch)

    def on_observation(self, observation):
        print(f"New observation: {observation['result']} at {observation['phenomenonTime']}")

    def on_batch(self, batch):
        print(f"Received batch of {batch['count']} observations")
        for obs in batch['observations']:
            self.on_observation(obs)

    def start(self):
        self.connection.start()

    def subscribe_to_datastream(self, datastream_id):
        self.connection.send("SubscribeToDatastream", [datastream_id])

    def unsubscribe_from_datastream(self, datastream_id):
        self.connection.send("UnsubscribeFromDatastream", [datastream_id])

    def stop(self):
        self.connection.stop()

# Usage
client = SensorStreamClient(
    "http://localhost:5000/hubs/sensor-observations",
    "your-auth-token"
)

client.start()
client.subscribe_to_datastream("temperature-sensor-1")

# Keep running
import time
time.sleep(60)

client.stop()
```

## Observation Payload

### Single Observation Event

```json
{
  "observationId": "550e8400-e29b-41d4-a716-446655440000",
  "datastreamId": "temperature-downtown-1",
  "datastreamName": "Downtown Temperature Sensor",
  "thingId": "weather-station-1",
  "sensorId": "temp-sensor-model-xyz",
  "observedPropertyId": "temperature",
  "phenomenonTime": "2025-01-15T10:30:00Z",
  "resultTime": "2025-01-15T10:30:01Z",
  "result": 23.5,
  "resultQuality": "good",
  "parameters": {
    "battery": 95,
    "signalStrength": -65
  },
  "unitOfMeasurement": {
    "name": "Degree Celsius",
    "symbol": "°C",
    "definition": "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
  },
  "serverTimestamp": "2025-01-15T10:30:01.234Z"
}
```

### Batch Observation Event

```json
{
  "datastreamId": "temperature-downtown-1",
  "thingId": "weather-station-1",
  "sensorId": "temp-sensor-model-xyz",
  "count": 150,
  "observations": [
    {
      "observationId": "...",
      "phenomenonTime": "2025-01-15T10:30:00Z",
      "result": 23.5,
      ...
    },
    ...
  ]
}
```

## Use Cases

### Smart City Dashboard

```typescript
// Real-time air quality monitoring
class AirQualityDashboard {
    private client: SensorDataClient;
    private map: Map;

    async initialize() {
        this.client = new SensorDataClient(() => this.getToken());
        await this.client.connect();

        // Subscribe to all air quality sensors in the city
        const sensors = await this.getAirQualitySensors();
        for (const sensor of sensors) {
            await this.client.subscribeToDatastream(
                sensor.datastreamId,
                (obs) => this.updateSensorMarker(sensor.location, obs)
            );
        }
    }

    updateSensorMarker(location, observation) {
        const marker = this.map.getMarker(location);
        marker.setColor(this.getColorForAQI(observation.result));
        marker.setPopup(`AQI: ${observation.result}`);
    }
}
```

### IoT Device Monitoring

```typescript
// Monitor device health in real-time
async function monitorDeviceHealth(thingId: string) {
    const client = new SensorDataClient(() => getToken());
    await client.connect();

    // Subscribe to all sensors on this device
    await client.connection.invoke("SubscribeToThing", thingId);

    const metrics = new Map();

    client.connection.on("ObservationCreated", (obs) => {
        metrics.set(obs.datastreamId, obs.result);

        // Check for alerts
        if (obs.datastreamName.includes("Battery") && obs.result < 10) {
            sendAlert(`Low battery on ${thingId}: ${obs.result}%`);
        }

        if (obs.datastreamName.includes("Temperature") && obs.result > 80) {
            sendAlert(`High temperature on ${thingId}: ${obs.result}°C`);
        }

        updateDeviceDashboard(thingId, metrics);
    });
}
```

## Performance Considerations

### Rate Limiting

The server automatically rate-limits observations to prevent overwhelming clients:

- Default: 100 observations/second per subscription group
- Configurable via `RateLimitPerSecond`
- Applies per datastream, thing, and sensor subscription

### Batching

For high-volume sensors (>100 obs/sec by default):

- Observations are automatically batched
- Reduces network overhead
- Clients receive `ObservationsBatch` events instead of individual `ObservationCreated` events
- Batching threshold is configurable

### Connection Management

- Use `withAutomaticReconnect()` for resilient connections
- Implement re-subscription logic in `onreconnected` handler
- Consider exponential backoff for retries

## Security

### Authentication

WebSocket connections require authentication:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/sensor-observations", {
        accessTokenFactory: () => localStorage.getItem("authToken")
    })
    .build();
```

### Multi-Tenancy

- Clients only receive observations from their authorized tenant
- Enforced at the hub level via user claims
- Admin role required for `SubscribeToAll`

## Troubleshooting

### Connection Issues

```javascript
connection.onclose((error) => {
    if (error) {
        console.error("Connection closed with error:", error);
        // Implement custom reconnection logic
    }
});
```

### Not Receiving Observations

1. Check that WebSocket streaming is enabled in server configuration
2. Verify authentication token is valid
3. Confirm you're subscribed to the correct datastream/thing/sensor
4. Check server logs for rate limiting or errors

### High Latency

1. Check network connection
2. Verify server is not rate-limiting your subscription
3. Consider subscribing to fewer datastreams
4. Monitor server resources

## API Reference

### Hub Methods

| Method | Parameters | Description |
|--------|-----------|-------------|
| `SubscribeToDatastream` | `datastreamId: string` | Subscribe to observations from a datastream |
| `UnsubscribeFromDatastream` | `datastreamId: string` | Unsubscribe from a datastream |
| `SubscribeToThing` | `thingId: string` | Subscribe to all datastreams from a thing |
| `UnsubscribeFromThing` | `thingId: string` | Unsubscribe from a thing |
| `SubscribeToSensor` | `sensorId: string` | Subscribe to all datastreams using a sensor |
| `UnsubscribeFromSensor` | `sensorId: string` | Unsubscribe from a sensor |
| `SubscribeToAll` | none | Subscribe to all observations (admin only) |
| `UnsubscribeFromAll` | none | Unsubscribe from all observations |

### Client Events

| Event | Payload Type | Description |
|-------|-------------|-------------|
| `ObservationCreated` | `Observation` | New observation received |
| `ObservationsBatch` | `ObservationBatch` | Batch of observations received |
| `Subscribed` | `SubscriptionInfo` | Subscription confirmed |
| `Unsubscribed` | `SubscriptionInfo` | Unsubscription confirmed |
| `Error` | `ErrorInfo` | Error occurred |

## Related Documentation

- [SensorThings API Integration Guide](./SENSORTHINGS_INTEGRATION.md)
- [Advanced Filtering Guide](./ADVANCED_FILTERING_GUIDE.md)
- [Authentication Guide](../user/authentication.md)
