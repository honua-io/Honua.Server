# Azure Digital Twins Implementation Summary

This document summarizes the Azure Digital Twins bi-directional synchronization implementation for Honua.Server.

## Implementation Date
November 10, 2025

## Overview

Complete bi-directional synchronization between Honua.Server geospatial features and Azure Digital Twins, enabling smart city and digital twin scenarios with real-time data exchange.

## Components Implemented

### 1. Core Services

#### DTDL Model Mapper (`Services/DtdlModelMapper.cs`)
- **Purpose**: Maps Honua layer schemas to Azure Digital Twins DTDL models
- **Key Features**:
  - Auto-generates DTDL v3 models from Honua metadata
  - Supports ETSI NGSI-LD ontology
  - Type mapping (string, integer, double, boolean, datetime, geometry)
  - Property name sanitization for DTMI compliance
  - Sync metadata injection (honuaServiceId, honuaLayerId, etc.)
  - Bidirectional property mapping with custom mappings

**Example Usage**:
```csharp
var model = await modelMapper.GenerateModelFromLayerAsync(
    "smart-city",
    "traffic-sensors",
    layerSchema);
```

#### Azure Digital Twins Client Wrapper (`Services/AzureDigitalTwinsClientWrapper.cs`)
- **Purpose**: Production-ready wrapper around Azure SDK
- **Key Features**:
  - Managed Identity authentication
  - Service Principal authentication fallback
  - Comprehensive error handling
  - Rate limit detection and logging
  - Retry logic via Azure SDK
  - ETag support for optimistic concurrency

**Authentication Options**:
- Managed Identity (recommended for production)
- Service Principal (ClientId/ClientSecret/TenantId)
- Default Azure Credential chain

#### Twin Synchronization Service (`Services/TwinSynchronizationService.cs`)
- **Purpose**: Bi-directional sync between Honua features and ADT twins
- **Key Features**:
  - Create/Update/Delete twins based on Honua features
  - Batch synchronization with configurable concurrency
  - Relationship mapping (foreign keys → ADT relationships)
  - Conflict detection and resolution (4 strategies)
  - Optimistic concurrency with ETags
  - Comprehensive sync statistics

**Sync Operations**:
- `SyncFeatureToTwinAsync()`: Single feature sync
- `SyncFeaturesToTwinsAsync()`: Batch sync with parallelism
- `SyncTwinToFeatureAsync()`: Reverse sync (ADT → Honua)
- `DeleteTwinAsync()`: Delete twin with cascade relationship deletion
- `SyncRelationshipsAsync()`: Foreign key → relationship sync

**Conflict Resolution Strategies**:
1. **LastWriteWins**: Most recent timestamp wins
2. **HonuaAuthoritative**: Honua always wins
3. **AdtAuthoritative**: ADT always wins
4. **Manual**: Flag for manual resolution

### 2. Event-Driven Sync

#### Event Grid Publisher (`Events/EventGridPublisher.cs`)
- **Purpose**: Publish Honua feature changes to Azure Event Grid
- **Events Published**:
  - `Honua.Features.FeatureCreated`
  - `Honua.Features.FeatureUpdated`
  - `Honua.Features.FeatureDeleted`
- **Features**:
  - Automatic event metadata (timestamp, event ID)
  - Subject formatting: `honua/{serviceId}/{layerId}/{featureId}`
  - Error handling and logging

#### ADT Event Handler (`Events/AdtEventHandler.cs`)
- **Purpose**: Handle Azure Digital Twins lifecycle events
- **Events Handled**:
  - `Microsoft.DigitalTwins.Twin.Create`
  - `Microsoft.DigitalTwins.Twin.Update`
  - `Microsoft.DigitalTwins.Twin.Delete`
  - `Microsoft.DigitalTwins.Relationship.Create/Update/Delete`
- **Features**:
  - Circular sync prevention (checks for Honua metadata)
  - Batch event processing
  - Automatic sync to Honua on ADT changes

### 3. API Endpoints (`Api/AzureDigitalTwinsEndpoints.cs`)

All endpoints are under `/api/azure/digital-twins`:

#### Sync Endpoints
- **POST** `/sync/layer/{serviceId}/{layerId}` - Batch sync entire layer
- **POST** `/sync/feature/{serviceId}/{layerId}/{featureId}` - Sync single feature
- **DELETE** `/sync/feature/{serviceId}/{layerId}/{featureId}` - Delete twin

#### Model Endpoints
- **GET** `/models` - List all DTDL models
- **GET** `/models/{modelId}` - Get specific model
- **POST** `/models/from-layer/{serviceId}/{layerId}` - Generate and upload model from layer

#### Query Endpoints
- **POST** `/twins/query` - ADT query language support
- **GET** `/twins/{twinId}` - Get twin by ID (proxy to ADT)

### 4. Configuration (`Configuration/AzureDigitalTwinsOptions.cs`)

Comprehensive configuration model with validation:

```json
{
  "AzureDigitalTwins": {
    "InstanceUrl": "https://{instance}.api.{region}.digitaltwins.azure.net",
    "UseManagedIdentity": true,
    "DefaultNamespace": "dtmi:com:honua",
    "UseNgsiLdOntology": true,
    "MaxBatchSize": 100,
    "RequestTimeoutSeconds": 30,
    "MaxRetryAttempts": 3,
    "Sync": {
      "Enabled": true,
      "Direction": "Bidirectional",
      "ConflictStrategy": "LastWriteWins",
      "EnableRealTimeSync": true,
      "BatchSyncIntervalMinutes": 60,
      "SyncDeletions": true,
      "SyncRelationships": true
    },
    "EventGrid": {
      "TopicEndpoint": "...",
      "SubscribedEventTypes": [...],
      "EnableDeadLetter": true
    },
    "LayerMappings": [...]
  }
}
```

**Configuration Classes**:
- `AzureDigitalTwinsOptions`: Root configuration
- `SyncOptions`: Sync behavior and conflict resolution
- `EventGridOptions`: Event Grid integration settings
- `LayerModelMapping`: Layer-to-model mapping rules
- `RelationshipMapping`: Foreign key relationship configuration

### 5. Dependency Injection (`AzureDigitalTwinsServiceExtensions.cs`)

Easy integration into ASP.NET Core:

```csharp
// In Program.cs
builder.Services.AddAzureDigitalTwins(builder.Configuration);

// In endpoint mapping
app.MapAzureDigitalTwinsEndpoints();
```

**Services Registered**:
- `IAzureDigitalTwinsClient` (Singleton)
- `IDtdlModelMapper` (Singleton)
- `ITwinSynchronizationService` (Singleton)
- `IEventGridPublisher` (Singleton)
- `IAdtEventHandler` (Singleton)
- `BatchSyncBackgroundService` (Hosted Service, conditional)

**Background Service**:
Automatically runs batch sync at configured intervals when `BatchSyncIntervalMinutes > 0`.

### 6. Models (`Models/DtdlModels.cs`)

Complete DTDL model representation:

- `DtdlModel`: Interface definition
- `DtdlContent`: Base for properties/telemetry/relationships
- `DtdlProperty`: Property with schema and writability
- `DtdlTelemetry`: Telemetry fields
- `DtdlRelationship`: Relationships with multiplicity
- `DtdlComponent`: Nested components
- `TwinSyncMetadata`: Sync tracking metadata
- `TwinSyncResult`: Sync operation result
- `BatchSyncStatistics`: Batch operation stats
- `ConflictInfo`: Conflict details

### 7. Tests

#### Unit Tests (`tests/Honua.Integration.Azure.Tests/`)

**DtdlModelMapperTests** (`Services/DtdlModelMapperTests.cs`):
- Model generation from layer schema
- JSON serialization
- Model validation
- Property mapping (feature ↔ twin)
- DTMI component sanitization
- NGSI-LD support

**TwinSynchronizationServiceTests** (`Services/TwinSynchronizationServiceTests.cs`):
- Create new twins
- Update existing twins
- Delete twins with relationships
- Batch sync
- Conflict handling
- Layer mapping validation

**Mock Client** (`Mocks/MockAzureDigitalTwinsClient.cs`):
- Full in-memory implementation
- Support for all CRUD operations
- Relationship management
- Model management
- Pageable results

### 8. Documentation

#### Main Documentation (`README.md`)
- Installation guide
- Configuration examples
- DTDL mapping patterns
- API endpoint documentation
- Sync patterns (real-time, batch, bidirectional)
- Conflict resolution strategies
- Smart city use cases
- Authentication options
- Performance tuning
- Monitoring and telemetry

#### Smart City Example (`Examples/SmartCityExample.md`)
- Complete smart city scenario
- Layer schema definitions
- DTDL model examples
- Configuration walkthrough
- API usage examples
- Query examples
- Azure service integration
- Cost optimization tips

## Generated DTDL Model Example

Input (Honua Layer Schema):
```json
{
  "title": "Traffic Sensors",
  "properties": {
    "sensor_id": {"type": "string"},
    "temperature": {"type": "double"},
    "vehicle_count": {"type": "integer"},
    "is_active": {"type": "boolean"},
    "last_reading": {"type": "datetime"}
  }
}
```

Output (DTDL Model):
```json
{
  "@id": "dtmi:com:honua:smart_city:traffic_sensors;1",
  "@context": "dtmi:dtdl:context;3",
  "@type": "Interface",
  "displayName": "Traffic Sensors",
  "contents": [
    {"@type": "Property", "name": "type", "schema": "string"},
    {"@type": "Property", "name": "location", "schema": {...}},
    {"@type": "Property", "name": "sensor_id", "schema": "string", "writable": true},
    {"@type": "Property", "name": "temperature", "schema": "double", "writable": true},
    {"@type": "Property", "name": "vehicle_count", "schema": "integer", "writable": true},
    {"@type": "Property", "name": "is_active", "schema": "boolean", "writable": true},
    {"@type": "Property", "name": "last_reading", "schema": "dateTime", "writable": true},
    {"@type": "Property", "name": "geometry", "schema": "string"},
    {"@type": "Property", "name": "honuaServiceId", "schema": "string"},
    {"@type": "Property", "name": "honuaLayerId", "schema": "string"},
    {"@type": "Property", "name": "honuaFeatureId", "schema": "string"},
    {"@type": "Property", "name": "lastSyncTime", "schema": "dateTime"},
    {"@type": "Property", "name": "syncVersion", "schema": "long"}
  ]
}
```

## File Structure

```
src/Honua.Integration.Azure/
├── Api/
│   └── AzureDigitalTwinsEndpoints.cs
├── Configuration/
│   └── AzureDigitalTwinsOptions.cs
├── Events/
│   ├── AdtEventHandler.cs
│   └── EventGridPublisher.cs
├── Examples/
│   └── SmartCityExample.md
├── Models/
│   └── DtdlModels.cs
├── Services/
│   ├── AzureDigitalTwinsClientWrapper.cs
│   ├── DtdlModelMapper.cs
│   ├── IAzureDigitalTwinsClient.cs
│   └── TwinSynchronizationService.cs
├── AzureDigitalTwinsServiceExtensions.cs
├── Honua.Integration.Azure.csproj
├── README.md
└── appsettings.azure-digital-twins.json

tests/Honua.Integration.Azure.Tests/
├── Mocks/
│   └── MockAzureDigitalTwinsClient.cs
├── Services/
│   ├── DtdlModelMapperTests.cs
│   └── TwinSynchronizationServiceTests.cs
└── Honua.Integration.Azure.Tests.csproj
```

## Key Design Decisions

1. **Abstraction Layer**: `IAzureDigitalTwinsClient` interface enables testing without Azure
2. **DTDL v3**: Latest version with geospatial support
3. **NGSI-LD Support**: Optional compliance with smart city standards
4. **Metadata Tracking**: Every twin tracks its Honua origin (serviceId, layerId, featureId)
5. **Version Control**: `syncVersion` property for conflict resolution
6. **Batch Concurrency**: Configurable `MaxBatchSize` with semaphore control
7. **Event Loop Prevention**: Checks for Honua metadata to avoid circular sync
8. **Relationship Cascade**: Deletes relationships before deleting twins
9. **ETag Support**: Optimistic concurrency control
10. **Retry Logic**: Built into Azure SDK client configuration

## Smart City Ontology Support

The implementation supports standard smart city ontologies:

- **ETSI NGSI-LD**: Optional base properties (type, location, observedAt)
- **DTDL Geospatial**: Point, LineString, Polygon, Multi* types
- **Smart Cities**: Traffic, parking, building, infrastructure models
- **Custom Namespaces**: Configurable DTMI namespace prefix

## Performance Characteristics

- **Batch Sync**: 100 twins/batch by default (configurable)
- **Concurrent Operations**: Semaphore-controlled parallelism
- **Rate Limiting**: Automatic detection and logging
- **Retry Policy**: Exponential backoff (3 attempts default)
- **Request Timeout**: 30 seconds default
- **Event Grid**: Asynchronous, decoupled event publishing

## Security

- **Managed Identity**: Recommended for production (no credentials in config)
- **Service Principal**: Supported with Client ID/Secret
- **RBAC**: Requires appropriate ADT roles (Digital Twins Data Owner/Reader)
- **Event Grid**: Access key authentication for topic publishing
- **Data Protection**: All sensitive config values should be in Azure Key Vault

## Integration Points

### Honua → ADT
1. Feature created/updated/deleted in Honua
2. Event published to Event Grid (optional)
3. Twin created/updated/deleted in ADT
4. Relationships synced based on foreign keys

### ADT → Honua
1. Twin modified in ADT (via ADT Explorer, API, etc.)
2. Event published to Event Grid
3. Event Grid webhook calls Honua endpoint
4. `AdtEventHandler` processes event
5. Feature updated in Honua (via editing API)

## Production Checklist

- [ ] Configure Azure Digital Twins instance
- [ ] Set up Managed Identity or Service Principal
- [ ] Configure Event Grid topic and subscription
- [ ] Define layer mappings in appsettings
- [ ] Generate and upload DTDL models
- [ ] Perform initial batch sync
- [ ] Set up Event Grid webhook endpoint
- [ ] Configure monitoring and alerts
- [ ] Test conflict resolution scenarios
- [ ] Validate relationship mappings
- [ ] Load test batch sync operations
- [ ] Document custom DTDL models
- [ ] Train operators on conflict resolution

## Future Enhancements

- [ ] Delta sync (track changes since last sync)
- [ ] Streaming sync for high-frequency updates
- [ ] Advanced conflict resolution (merge strategies)
- [ ] Multi-region ADT support
- [ ] Telemetry aggregation (average, sum, etc.)
- [ ] Custom DTDL component generation
- [ ] ADT model versioning support
- [ ] Sync status dashboard
- [ ] Performance metrics and dashboards
- [ ] Cost tracking and optimization

## Support

For issues or questions, please refer to:
- Main documentation: `/src/Honua.Integration.Azure/README.md`
- Smart city example: `/src/Honua.Integration.Azure/Examples/SmartCityExample.md`
- Unit tests for usage examples
- Azure Digital Twins documentation: https://learn.microsoft.com/azure/digital-twins/

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
