# Honua.Integration.Azure

Azure IoT Hub integration for Honua.Server that ingests device telemetry into the OGC SensorThings API.

## Features

- **Event Hub Consumer**: Background service that consumes messages from Azure IoT Hub's Event Hub-compatible endpoint
- **Auto-Entity Creation**: Automatically creates Things, Sensors, ObservedProperties, and Datastreams for new devices and telemetry fields
- **Flexible Mapping**: Configure custom device-to-entity mappings via JSON configuration
- **Multi-Tenancy Support**: Map devices to tenants based on device ID patterns or message properties
- **High-Volume Processing**: Handles 1,000+ messages/second with batch processing
- **Error Handling**: Dead letter queue, retry policies, and comprehensive error logging
- **Health Monitoring**: Built-in health checks and metrics
- **Managed Identity Support**: Use Azure Managed Identity or connection strings

## Architecture

```
Azure IoT Hub → Event Hub Endpoint → EventProcessorClient
                                           ↓
                                   Message Parser
                                           ↓
                                   Device Mapper
                                           ↓
                                SensorThings Mapper
                                           ↓
                              SensorThings API Repository
                                           ↓
                                   PostgreSQL Database
```

## Quick Start

### 1. Install Package

Add project reference to `Honua.Integration.Azure`:

```xml
<ProjectReference Include="../Honua.Integration.Azure/Honua.Integration.Azure.csproj" />
```

### 2. Configure Services

In your `Program.cs` or `Startup.cs`:

```csharp
using Honua.Integration.Azure.Extensions;

// Add SensorThings API services (prerequisite)
builder.Services.AddSensorThingsApi(builder.Configuration);

// Add Azure IoT Hub integration
builder.Services.AddAzureIoTHubIntegration(builder.Configuration);
```

### 3. Configure Settings

Add to your `appsettings.json`:

```json
{
  "AzureIoTHub": {
    "Enabled": true,
    "EventHubConnectionString": "Endpoint=sb://your-hub.servicebus.windows.net/;SharedAccessKeyName=service;SharedAccessKey=...;EntityPath=your-hub-name",
    "ConsumerGroup": "$Default",
    "CheckpointStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    "CheckpointContainerName": "iot-hub-checkpoints",
    "MaxBatchSize": 100,
    "MaxWaitTime": "00:00:05"
  }
}
```

### 4. Run

Start your application. The background service will automatically connect to IoT Hub and start ingesting telemetry.

## Configuration

### Connection Options

#### Option 1: Connection String (Development)

```json
{
  "AzureIoTHub": {
    "EventHubConnectionString": "Endpoint=sb://...;EntityPath=...",
    "CheckpointStorageConnectionString": "DefaultEndpointsProtocol=https;..."
  }
}
```

#### Option 2: Managed Identity (Production)

```json
{
  "AzureIoTHub": {
    "EventHubNamespace": "your-hub.servicebus.windows.net",
    "EventHubName": "your-hub-name",
    "CheckpointStorageConnectionString": "https://youraccount.blob.core.windows.net"
  }
}
```

### Device Mapping Configuration

Create a `device-mapping.json` file:

```json
{
  "defaults": {
    "autoCreateThings": true,
    "autoCreateSensors": true,
    "autoCreateObservedProperties": true,
    "autoCreateDatastreams": true,
    "thingNameTemplate": "IoT Device: {deviceId}",
    "thingDescriptionTemplate": "Device {deviceId} connected via Azure IoT Hub",
    "defaultUnit": {
      "name": "unitless",
      "symbol": "",
      "definition": "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#Unitless"
    }
  },
  "deviceMappings": {
    "temp-sensor-*": {
      "thingNameTemplate": "Temperature Sensor {deviceId}",
      "telemetryMappings": {
        "temperature": {
          "name": "Temperature",
          "description": "Ambient temperature measurement",
          "unitOfMeasurement": {
            "name": "Degree Celsius",
            "symbol": "°C",
            "definition": "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
          },
          "observedPropertyName": "Air Temperature",
          "observedPropertyDefinition": "http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#Temperature"
        }
      }
    }
  },
  "tenantMappings": [
    {
      "deviceIdPattern": "tenant1-*",
      "tenantId": "tenant-001",
      "priority": 10
    },
    {
      "propertyPath": "properties.tenantId",
      "tenantId": "{propertyValue}",
      "priority": 5
    }
  ]
}
```

Reference the file in `appsettings.json`:

```json
{
  "AzureIoTHub": {
    "MappingConfigurationPath": "/config/device-mapping.json"
  }
}
```

## Message Format

### Expected IoT Hub Message

```json
{
  "temperature": 25.5,
  "humidity": 60,
  "pressure": 1013.25,
  "timestamp": "2025-01-15T10:30:00Z"
}
```

### Nested Properties (Flattened)

```json
{
  "sensor": {
    "temp": 25.5,
    "unit": "C"
  }
}
```

This becomes:
- Observation 1: `sensor.temp` = 25.5
- Observation 2: `sensor.unit` = "C"

### Message Properties

Application properties can be used for tenant mapping:

```csharp
// In your IoT device code
message.Properties.Add("tenantId", "tenant-001");
```

## Multi-Tenancy

Devices can be mapped to tenants in three ways:

### 1. Device ID Pattern

```json
{
  "tenantMappings": [
    {
      "deviceIdPattern": "acme-*",
      "tenantId": "acme-corp"
    }
  ]
}
```

### 2. Message Property

```json
{
  "tenantMappings": [
    {
      "propertyPath": "properties.tenantId",
      "tenantId": "{propertyValue}"
    }
  ]
}
```

### 3. Device-Specific Mapping

```json
{
  "deviceMappings": {
    "special-device-001": {
      "tenantId": "special-tenant"
    }
  }
}
```

## SensorThings API Mapping

### Thing Creation

Each IoT device becomes a Thing:

```json
{
  "@iot.id": "guid",
  "name": "IoT Device: device-001",
  "description": "Device device-001 connected via Azure IoT Hub",
  "properties": {
    "deviceId": "device-001",
    "source": "Azure IoT Hub",
    "tenantId": "tenant-001"
  }
}
```

### Datastream Creation

Each telemetry field becomes a Datastream with associated Sensor and ObservedProperty.

### Observation Creation

Each telemetry value becomes an Observation:

```json
{
  "@iot.id": "guid",
  "phenomenonTime": "2025-01-15T10:30:00Z",
  "resultTime": "2025-01-15T10:30:01Z",
  "result": 25.5,
  "parameters": {
    "iotHub_deviceId": "device-001",
    "iotHub_enqueuedTime": "2025-01-15T10:30:01Z",
    "iotHub_messageId": "abc123",
    "iotHub_systemProperties": { ... },
    "iotHub_applicationProperties": { ... }
  }
}
```

## Error Handling

### Dead Letter Queue

Failed messages are stored in the dead letter queue:

```csharp
// Access via DI
public class MyService
{
    private readonly IDeadLetterQueueService _deadLetterQueue;

    public async Task GetFailedMessages()
    {
        var messages = await _deadLetterQueue.GetDeadLetterMessagesAsync(limit: 100);
        foreach (var msg in messages)
        {
            Console.WriteLine($"Device: {msg.OriginalMessage.DeviceId}, Error: {msg.Error.Message}");
        }
    }
}
```

### Retry Policy

Configure retry behavior:

```json
{
  "AzureIoTHub": {
    "RetryPolicy": {
      "maxRetries": 3,
      "initialDelay": "00:00:01",
      "maxDelay": "00:01:00",
      "backoffMultiplier": 2.0
    }
  }
}
```

## Health Checks

Access health status at `/health` endpoint:

```json
{
  "status": "Healthy",
  "checks": {
    "azure_iot_hub": {
      "status": "Healthy",
      "data": {
        "isHealthy": true,
        "totalMessagesReceived": 15420,
        "totalMessagesProcessed": 15415,
        "totalMessagesFailed": 5,
        "successRate": "99.97%",
        "lastMessageTime": "2025-01-15T10:35:00Z"
      }
    }
  }
}
```

## Performance Tuning

### Batch Size

Larger batches = higher throughput, but more memory usage:

```json
{
  "AzureIoTHub": {
    "MaxBatchSize": 1000
  }
}
```

### Concurrent Partitions

Process multiple partitions concurrently:

```json
{
  "AzureIoTHub": {
    "MaxConcurrentPartitions": 4
  }
}
```

### Consumer Groups

Use dedicated consumer groups for Honua:

```bash
# Azure CLI
az iot hub consumer-group create --name honua --hub-name your-hub-name
```

```json
{
  "AzureIoTHub": {
    "ConsumerGroup": "honua"
  }
}
```

## Monitoring

### Metrics

The service tracks:
- Total messages received
- Total messages processed
- Total messages failed
- Success rate
- Observations created
- Things created
- Datastreams created

### Logging

Configure log levels in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Honua.Integration.Azure": "Information",
      "Honua.Integration.Azure.Services.AzureIoTHubConsumerService": "Debug"
    }
  }
}
```

## Troubleshooting

### No Messages Received

1. Check IoT Hub connection string
2. Verify consumer group exists
3. Check checkpoint storage connectivity
4. Verify devices are sending to IoT Hub

### Messages Failing

1. Check dead letter queue: `IDeadLetterQueueService.GetDeadLetterMessagesAsync()`
2. Review logs for error details
3. Verify SensorThings API repository is accessible
4. Check database connectivity

### High Memory Usage

1. Reduce `MaxBatchSize`
2. Reduce `MaxConcurrentPartitions`
3. Enable checkpoint pruning

### Slow Processing

1. Increase `MaxBatchSize`
2. Increase `MaxConcurrentPartitions`
3. Use dedicated consumer group
4. Optimize database (indexes, connection pooling)

## Production Deployment

### Recommended Configuration

```json
{
  "AzureIoTHub": {
    "Enabled": true,
    "EventHubNamespace": "prod-hub.servicebus.windows.net",
    "EventHubName": "prod-hub",
    "ConsumerGroup": "honua-prod",
    "CheckpointStorageConnectionString": "https://prodstorage.blob.core.windows.net",
    "CheckpointContainerName": "checkpoints",
    "MaxBatchSize": 500,
    "MaxWaitTime": "00:00:03",
    "MaxConcurrentPartitions": 0,
    "ErrorHandling": {
      "enableDeadLetterQueue": true,
      "continueOnError": true,
      "maxConsecutiveErrors": 10
    },
    "RetryPolicy": {
      "maxRetries": 3,
      "initialDelay": "00:00:02",
      "maxDelay": "00:02:00"
    }
  }
}
```

### Azure Resources Required

1. **IoT Hub** (Standard tier or higher)
2. **Storage Account** (for checkpoints)
3. **Managed Identity** (optional, recommended for production)
4. **Consumer Group** (dedicated for Honua)

### Security

- Use Managed Identity instead of connection strings
- Store secrets in Azure Key Vault
- Enable TLS 1.2+
- Restrict network access with firewall rules

## Examples

See the `/examples` directory for:
- Sample IoT device simulators
- Custom mapping configurations
- Multi-tenant scenarios
- High-volume testing

## License

Copyright (c) 2025 HonuaIO. Licensed under the Elastic License 2.0.
