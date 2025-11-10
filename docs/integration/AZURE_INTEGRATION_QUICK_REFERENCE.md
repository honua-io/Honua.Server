# Azure Integration Quick Reference

**Companion to**: [Azure Smart Cities Integration Guide](./AZURE_SMART_CITIES_INTEGRATION.md)

---

## Quick Decision Matrix

### When to Use Each Azure Service

| Your Need | Use This Azure Service | Integrate with Honua Via |
|-----------|------------------------|--------------------------|
| IoT device management | Azure IoT Hub | Event Hub → SensorThings API |
| No-code IoT solution | Azure IoT Central | Webhook → Intake API |
| Semantic digital twins | Azure Digital Twins | REST API ↔ WFS-T |
| Indoor mapping | Azure Maps Creator | Custom tilesets + feature overlay |
| Real-time dashboards | Power BI | OData connector + streaming |
| Time-series analytics | Azure Data Explorer | Export observations via Event Hub |
| Event routing | Event Grid | Publish feature updates, subscribe to ADT |
| High-volume telemetry | Event Hubs | Stream processing pipeline |

---

## Integration Patterns Cheat Sheet

### Pattern 1: IoT Hub → Honua (Most Common)

```
Devices → IoT Hub → Event Hub → Azure Function → Honua SensorThings API
```

**When**: You have IoT devices and want standards-based access  
**Complexity**: Low  
**Time to Implement**: 1-2 weeks

**Code Snippet:**
```csharp
[FunctionName("IoTToHonua")]
public static async Task Run([EventHubTrigger] EventData[] events)
{
    foreach (var e in events)
    {
        var telemetry = Parse(e.Body);
        await _honua.CreateObservationAsync(new Observation
        {
            DatastreamId = telemetry.SensorId,
            Result = telemetry.Value,
            ResultTime = telemetry.Timestamp
        });
    }
}
```

### Pattern 2: Bi-Directional ADT Sync

```
Honua ↔ Event Grid ↔ Azure Functions ↔ Azure Digital Twins
```

**When**: Need semantic modeling + spatial queries  
**Complexity**: High  
**Time to Implement**: 4-6 weeks

**Honua → ADT:**
```csharp
// On Honua feature update
await _eventGrid.PublishAsync(new FeatureUpdatedEvent(feature));

// Azure Function receives event
await _adt.UpdateDigitalTwinAsync(twinId, jsonPatch);
```

**ADT → Honua:**
```csharp
// ADT event route to Event Grid
// Azure Function receives ADT change
await _honua.UpdateFeatureViaWfstAsync(collectionId, feature);
```

### Pattern 3: Power BI Dashboards

```
Honua OData API → Power BI Desktop → Power BI Service
```

**When**: Need business intelligence dashboards  
**Complexity**: Low  
**Time to Implement**: 2-3 days

**Power BI Setup:**
1. Get Data → OData Feed
2. URL: `https://honua.example.com/odata/features/sensors`
3. Transform → Load → Create visuals

### Pattern 4: Azure Maps Visualization

```
Honua WMTS → Azure Blob Storage → Azure Maps Custom Tileset
```

**When**: Want Azure's global CDN for map tiles  
**Complexity**: Medium  
**Time to Implement**: 1 week

---

## Authentication Quick Start

### Option 1: Managed Identity (Recommended)

```bash
# Enable on App Service
az webapp identity assign --name honua-server --resource-group honua-rg

# Grant permissions
az dt role-assignment create \
  --dt-name honua-adt \
  --assignee $(az webapp identity show --name honua-server --query principalId -o tsv) \
  --role "Azure Digital Twins Data Owner"
```

```csharp
// In code (automatic)
var credential = new DefaultAzureCredential();
var client = new DigitalTwinsClient(adtUrl, credential);
```

### Option 2: Service Principal (Development)

```bash
# Create SP
az ad sp create-for-rbac --name honua-dev-sp

# Save output, then configure
export AZURE_TENANT_ID="..."
export AZURE_CLIENT_ID="..."
export AZURE_CLIENT_SECRET="..."
```

```csharp
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var client = new DigitalTwinsClient(adtUrl, credential);
```

---

## Common API Patterns

### Azure Digital Twins

**Query Twins:**
```csharp
var query = "SELECT * FROM digitaltwins WHERE IS_OF_MODEL('dtmi:honua:Building;1')";
await foreach (var twin in _client.QueryAsync<Building>(query))
{
    Console.WriteLine($"{twin.Id}: {twin.Name}");
}
```

**Update Twin:**
```csharp
var patch = new JsonPatchDocument();
patch.AppendReplace("/Temperature", 72.5);
await _client.UpdateDigitalTwinAsync("sensor-123", patch);
```

### IoT Hub

**Send Device-to-Cloud Message:**
```csharp
using var deviceClient = DeviceClient.CreateFromConnectionString(connStr);
var message = new Message(Encoding.UTF8.GetBytes(json));
await deviceClient.SendEventAsync(message);
```

**Receive Cloud-to-Device Message:**
```csharp
var message = await deviceClient.ReceiveAsync();
var data = Encoding.UTF8.GetString(message.GetBytes());
await deviceClient.CompleteAsync(message);
```

### Event Grid

**Publish Event:**
```csharp
var client = new EventGridPublisherClient(
    new Uri(topicEndpoint),
    new AzureKeyCredential(topicKey)
);

await client.SendEventAsync(new EventGridEvent(
    subject: "features/buildings/123",
    eventType: "Honua.Feature.Updated",
    dataVersion: "1.0",
    data: featureJson
));
```

### Azure Maps

**Geocode Address:**
```csharp
var client = new MapsSearchClient(new AzureKeyCredential(subscriptionKey));
var result = await client.SearchAddressAsync("1 Microsoft Way, Redmond, WA");
var position = result.Value.Results.First().Position;
```

---

## Configuration Templates

### appsettings.json

```json
{
  "Azure": {
    "DigitalTwins": {
      "InstanceUrl": "https://honua-adt.api.wus2.digitaltwins.azure.net",
      "SyncEnabled": true,
      "SyncIntervalSeconds": 300
    },
    "IoTHub": {
      "EventHubEndpoint": "Endpoint=sb://...",
      "ConsumerGroup": "$Default"
    },
    "Maps": {
      "SubscriptionKey": "@Microsoft.KeyVault(SecretUri=...)",
      "EnableCustomTilesets": true
    },
    "EventGrid": {
      "TopicEndpoint": "https://honua-events.westus-1.eventgrid.azure.net/api/events",
      "TopicKey": "@Microsoft.KeyVault(SecretUri=...)"
    }
  }
}
```

### Terraform

```hcl
# Azure Digital Twins instance
resource "azurerm_digital_twins_instance" "honua" {
  name                = "honua-adt"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
}

# IoT Hub
resource "azurerm_iothub" "honua" {
  name                = "honua-iothub"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku {
    name     = "S1"
    capacity = 1
  }
}

# Event Hub for telemetry
resource "azurerm_eventhub" "telemetry" {
  name                = "honua-telemetry"
  namespace_name      = azurerm_eventhub_namespace.main.name
  resource_group_name = azurerm_resource_group.main.name
  partition_count     = 4
  message_retention   = 7
}
```

---

## Troubleshooting

### Authentication Issues

**Error**: `Unauthorized` when calling Azure Digital Twins

**Solution**:
```bash
# Check managed identity
az webapp identity show --name honua-server

# Check role assignment
az dt role-assignment list --dt-name honua-adt
```

### Event Routing Issues

**Error**: Events not reaching Honua

**Solution**:
```bash
# Check Event Hub consumer group
az eventhubs eventhub consumer-group list --namespace-name honua-eh --eventhub-name telemetry

# Monitor Event Hub metrics
az monitor metrics list --resource /subscriptions/.../eventHubs/telemetry --metric IncomingMessages
```

### Performance Issues

**Error**: Slow queries to Azure Digital Twins

**Solution**:
- Add caching layer (Redis)
- Use batch queries
- Create twin indexes

```csharp
// Cache twin queries
var cacheKey = $"adt:twin:{twinId}";
var cached = await _cache.GetStringAsync(cacheKey);
if (cached != null)
    return JsonSerializer.Deserialize<Twin>(cached);

var twin = await _client.GetDigitalTwinAsync(twinId);
await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(twin),
    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
```

---

## Cost Optimization Tips

1. **Use Managed Identity** (no secret rotation costs)
2. **Enable caching** (reduce ADT queries)
3. **Right-size IoT Hub tier** (start with Basic)
4. **Use Event Hub capture** for cold storage
5. **Leverage Azure CDN** for map tiles (90%+ cache hit)
6. **Reserved capacity** for long-term deployments (40% savings)

### Cost Calculator

**Typical Smart City (1,000 sensors):**
- IoT Hub (S1): $25/month
- Event Hub (Standard): $20/month
- Azure Digital Twins: $200/month (10K operations/sec)
- Azure Functions: $15/month (1M executions)
- Storage: $10/month
- **Total**: ~$270/month

**Enterprise Scale (10,000 sensors):**
- IoT Hub (S2): $250/month
- Event Hub (Standard): $100/month
- Azure Digital Twins: $2,000/month
- Azure Functions: $50/month
- Azure Data Explorer: $500/month
- **Total**: ~$2,900/month

---

## Sample Projects

### 1. Smart Building Starter

**GitHub**: `honua-io/azure-smart-building-sample`

**Includes:**
- Azure Digital Twins DTDL models
- IoT Hub device simulator
- Honua SensorThings integration
- Power BI dashboard template
- Terraform deployment

### 2. Traffic Management

**GitHub**: `honua-io/azure-traffic-monitoring-sample`

**Includes:**
- Stream Analytics job
- Anomaly detection
- Azure Maps visualization
- Honua geofencing integration

### 3. Environmental Monitoring

**GitHub**: `honua-io/azure-air-quality-sample`

**Includes:**
- IoT Central template
- SensorThings API mapping
- OGC API EDR implementation
- Public data portal

---

## Support Resources

### Microsoft Documentation
- [Azure Digital Twins Samples](https://github.com/Azure-Samples/digital-twins-samples)
- [IoT Hub Tutorials](https://learn.microsoft.com/en-us/azure/iot-hub/tutorial-routing)
- [Azure Maps Tutorials](https://learn.microsoft.com/en-us/azure/azure-maps/tutorial-create-store-locator)

### Honua Documentation
- [SensorThings API Guide](../features/SENSORTHINGS_INTEGRATION.md)
- [GeoEvent API Guide](../GEOEVENT_API_GUIDE.md)
- [OData Configuration](../api/README.md#odata)

### Community
- [Honua Discord](https://discord.gg/honua) - #azure-integration channel
- [Azure IoT Tech Community](https://techcommunity.microsoft.com/t5/internet-of-things-iot/ct-p/IoT)

---

**Last Updated**: 2025-11-10  
**Maintained by**: Honua Platform Team
