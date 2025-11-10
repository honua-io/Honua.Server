# Azure IoT Hub Integration - Quick Start Guide

Get started with Azure IoT Hub integration in under 10 minutes.

## Prerequisites

- Azure subscription
- Honua.Server deployed
- .NET 9.0 SDK (for device simulator)

## Step 1: Create Azure Resources

### 1.1 Create IoT Hub

```bash
# Create resource group
az group create --name honua-iot-rg --location eastus

# Create IoT Hub (S1 tier for production, F1 free tier for testing)
az iot hub create \
  --name honua-iot-hub \
  --resource-group honua-iot-rg \
  --sku S1 \
  --partition-count 4

# Create consumer group for Honua
az iot hub consumer-group create \
  --hub-name honua-iot-hub \
  --name honua
```

### 1.2 Create Storage Account for Checkpoints

```bash
# Create storage account
az storage account create \
  --name honuacheckpoints \
  --resource-group honua-iot-rg \
  --location eastus \
  --sku Standard_LRS

# Create container
az storage container create \
  --name iot-hub-checkpoints \
  --account-name honuacheckpoints
```

### 1.3 Get Connection Strings

```bash
# IoT Hub Event Hub-compatible connection string
az iot hub connection-string show \
  --hub-name honua-iot-hub \
  --policy-name service \
  --key primary

# Storage account connection string
az storage account show-connection-string \
  --name honuacheckpoints \
  --resource-group honua-iot-rg
```

## Step 2: Configure Honua.Server

### 2.1 Update appsettings.json

```json
{
  "AzureIoTHub": {
    "Enabled": true,
    "EventHubConnectionString": "Endpoint=sb://honua-iot-hub.servicebus.windows.net/;SharedAccessKeyName=service;SharedAccessKey=YOUR_KEY;EntityPath=honua-iot-hub",
    "ConsumerGroup": "honua",
    "CheckpointStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=honuacheckpoints;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net",
    "CheckpointContainerName": "iot-hub-checkpoints",
    "MaxBatchSize": 100,
    "MaxWaitTime": "00:00:05"
  }
}
```

### 2.2 Register Services (if not already done)

In `Program.cs`:

```csharp
using Honua.Integration.Azure.Extensions;

// Add Azure IoT Hub integration
builder.Services.AddAzureIoTHubIntegration(builder.Configuration);
```

### 2.3 Restart Honua.Server

```bash
dotnet run --project src/Honua.Server.Host
```

## Step 3: Create Test Device

### 3.1 Register Device in IoT Hub

```bash
# Create a test device
az iot hub device-identity create \
  --hub-name honua-iot-hub \
  --device-id temp-sensor-001

# Get device connection string
az iot hub device-identity connection-string show \
  --hub-name honua-iot-hub \
  --device-id temp-sensor-001
```

### 3.2 Send Test Message

Using Azure CLI:

```bash
az iot device send-d2c-message \
  --hub-name honua-iot-hub \
  --device-id temp-sensor-001 \
  --data '{"temperature":25.5,"humidity":60}'
```

Or using the device simulator:

```bash
# Set environment variable
export DEVICE_CONNECTION_STRING="HostName=honua-iot-hub.azure-devices.net;DeviceId=temp-sensor-001;SharedAccessKey=YOUR_KEY"

# Run simulator (see DeviceSimulator.cs)
dotnet run
```

## Step 4: Verify Integration

### 4.1 Check Health Endpoint

```bash
curl http://localhost:5000/health
```

Expected output:

```json
{
  "status": "Healthy",
  "checks": {
    "azure_iot_hub": {
      "status": "Healthy",
      "data": {
        "isHealthy": true,
        "totalMessagesReceived": 1,
        "totalMessagesProcessed": 1,
        "successRate": "100%"
      }
    }
  }
}
```

### 4.2 Query SensorThings API

```bash
# Get Things (devices)
curl http://localhost:5000/api/v1.1/Things

# Get Observations
curl http://localhost:5000/api/v1.1/Observations
```

Expected Thing:

```json
{
  "value": [
    {
      "@iot.id": "...",
      "name": "IoT Device: temp-sensor-001",
      "description": "Device temp-sensor-001 connected via Azure IoT Hub",
      "properties": {
        "deviceId": "temp-sensor-001",
        "source": "Azure IoT Hub"
      }
    }
  ]
}
```

Expected Observation:

```json
{
  "value": [
    {
      "@iot.id": "...",
      "phenomenonTime": "2025-01-15T10:30:00Z",
      "result": 25.5,
      "parameters": {
        "iotHub_deviceId": "temp-sensor-001",
        "iotHub_enqueuedTime": "2025-01-15T10:30:01Z"
      }
    }
  ]
}
```

## Step 5: Configure Custom Mappings (Optional)

Create `/config/device-mapping.json`:

```json
{
  "defaults": {
    "autoCreateThings": true,
    "thingNameTemplate": "IoT Device: {deviceId}"
  },
  "deviceMappings": {
    "temp-sensor-*": {
      "thingNameTemplate": "Temperature Sensor {deviceId}",
      "telemetryMappings": {
        "temperature": {
          "unitOfMeasurement": {
            "name": "Degree Celsius",
            "symbol": "°C",
            "definition": "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
          }
        }
      }
    }
  }
}
```

Update `appsettings.json`:

```json
{
  "AzureIoTHub": {
    "MappingConfigurationPath": "/config/device-mapping.json"
  }
}
```

## Troubleshooting

### No messages received

1. Check IoT Hub metrics in Azure Portal
2. Verify consumer group exists
3. Check Honua.Server logs: `docker logs -f honua-server`
4. Verify device is sending messages: `az iot hub monitor-events --hub-name honua-iot-hub`

### Messages failing

1. Check logs for errors
2. Query dead letter queue via API (if exposed)
3. Verify database connectivity
4. Check SensorThings API is accessible

### High latency

1. Increase `MaxBatchSize` in configuration
2. Use dedicated consumer group
3. Optimize database indexes
4. Enable connection pooling

## Next Steps

- Configure multi-tenancy with tenant mappings
- Set up custom device mappings for your sensors
- Enable authentication and authorization
- Set up monitoring and alerts
- Deploy to production with Managed Identity

## Resources

- [Azure IoT Hub Documentation](https://docs.microsoft.com/azure/iot-hub/)
- [OGC SensorThings API Specification](https://docs.ogc.org/is/18-088/18-088.html)
- [Honua.Server Documentation](../../docs/)

## Support

For issues or questions:
- GitHub Issues: https://github.com/honua-io/honua-server/issues
- Documentation: See main README.md
