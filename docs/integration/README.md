# Azure Integration Documentation

**Welcome to Honua.Server's Azure integration documentation.**

This directory contains comprehensive guides for integrating Honua.Server with Microsoft Azure's smart city and digital twin services.

---

## Documentation Index

### 📘 [Azure Smart Cities Integration Guide](./AZURE_SMART_CITIES_INTEGRATION.md)
**55KB | 1,800 lines | Comprehensive**

The complete technical reference for Azure integration, covering:

**Services Covered:**
- Azure Digital Twins (DTDL, twins, relationships, queries)
- Azure IoT Hub (device connectivity, telemetry, device twins)
- Azure IoT Central (SaaS IoT platform, smart city templates)
- Azure Maps (geospatial services, indoor maps)
- Azure Event Hubs & Event Grid (event streaming)
- Power BI (dashboards, real-time datasets)
- Azure Time Series Insights (sensor data analytics)

**Integration Patterns:**
- Bi-directional sync (Honua ↔ Azure Digital Twins)
- IoT Hub telemetry ingestion
- Event-driven microservices
- Federated queries across platforms

**Includes:**
- Architecture diagrams
- Code samples (C#, SQL, Terraform)
- Authentication setup (Managed Identity, Azure AD)
- 30-week implementation roadmap
- Real-world use cases (smart buildings, traffic, environmental monitoring)
- Cost estimates by deployment size

**Start here if**: You need comprehensive technical details for implementation

---

### ⚡ [Azure Integration Quick Reference](./AZURE_INTEGRATION_QUICK_REFERENCE.md)
**10KB | 400 lines | Practical**

Quick-start guide with copy-paste examples:

**Contains:**
- Decision matrix (which Azure service for which need)
- Integration patterns cheat sheet
- Authentication quick start
- Common API code snippets
- Configuration templates (appsettings.json, Terraform)
- Troubleshooting guide
- Cost optimization tips
- Sample project links

**Start here if**: You want to get started quickly with working examples

---

### 📊 [Azure vs Honua Comparison](./AZURE_COMPARISON.md)
**15KB | 450 lines | Strategic**

Service-by-service comparison and positioning guide:

**Compares:**
- Geospatial services (Honua vs Azure Maps)
- IoT & sensor data (SensorThings vs IoT Hub)
- Digital twins (Honua features vs Azure Digital Twins)
- Analytics (MapSDK vs Power BI vs Synapse)

**Includes:**
- Technical differentiator matrices
- Cost comparison by deployment size
- Customer scenario recommendations
- Integration strategies (when to use which)
- Migration paths (ADT→Honua, Honua→ADT)

**Start here if**: You need to justify architecture decisions or understand positioning

---

## Quick Navigation

### By Role

**For Developers**:
1. Start: [Quick Reference](./AZURE_INTEGRATION_QUICK_REFERENCE.md) → Try code samples
2. Deep Dive: [Integration Guide](./AZURE_SMART_CITIES_INTEGRATION.md) → Full patterns
3. Reference: Search integration guide for specific APIs

**For Architects**:
1. Start: [Comparison Guide](./AZURE_COMPARISON.md) → Understand positioning
2. Design: [Integration Guide](./AZURE_SMART_CITIES_INTEGRATION.md) → Architecture patterns
3. Plan: Review implementation roadmap (Phase 1-7)

**For Product Managers**:
1. Start: [Comparison Guide](./AZURE_COMPARISON.md) → Customer scenarios
2. Costs: Review cost comparison tables
3. Roadmap: [Integration Guide](./AZURE_SMART_CITIES_INTEGRATION.md) → Feature timeline

**For Sales Engineers**:
1. Start: [Comparison Guide](./AZURE_COMPARISON.md) → Differentiation
2. Demo: [Quick Reference](./AZURE_INTEGRATION_QUICK_REFERENCE.md) → Working examples
3. Proposal: Use cost estimates from integration guide

---

## Common Scenarios

### Scenario: "We use Azure IoT Hub for device management"

**Answer**: 
✅ Keep Azure IoT Hub for device lifecycle management  
✅ Add Honua SensorThings API for standards-based data access  
📖 See: [Integration Guide § Azure IoT Hub](./AZURE_SMART_CITIES_INTEGRATION.md#azure-iot-hub)

**Pattern**: IoT Hub → Event Hub → Honua Intake → SensorThings API  
**Benefits**: Device management + OGC compliance

---

### Scenario: "We need Power BI dashboards"

**Answer**:
✅ Connect Power BI to Honua via OData connector  
✅ Or stream observations to Power BI datasets  
📖 See: [Quick Reference § Power BI](./AZURE_INTEGRATION_QUICK_REFERENCE.md#pattern-3-power-bi-dashboards)

**Setup Time**: 2-3 days  
**Complexity**: Low

---

### Scenario: "Azure Digital Twins vs Honua - which to use?"

**Answer**:
- **Use ADT** for semantic modeling (enterprise asset relationships)
- **Use Honua** for spatial modeling (geometry, CQL queries)
- **Use Both** for comprehensive digital twin (spatial + semantic)

📖 See: [Comparison § Digital Twins](./AZURE_COMPARISON.md#digital-twins)

---

### Scenario: "How much does Azure integration cost?"

**Answer**:
- **Small (100 sensors)**: ~$270/month Azure services
- **Medium (1,000 sensors)**: ~$650/month (Honua) vs ~$2,700/month (Azure only)
- **Enterprise (10,000 sensors)**: ~$7,500/month (hybrid) vs ~$25,000/month (Azure only)

📖 See: [Comparison § Cost Comparison](./AZURE_COMPARISON.md#licensing--cost-comparison)

---

## Integration Patterns Summary

### Pattern 1: Azure-Centric with Honua Gateway
**When**: Azure is source of truth, need OGC compliance  
**Flow**: `Azure Services → Honua → OGC APIs`  
**Complexity**: Medium

### Pattern 2: Honua-Centric with Azure Enhancement
**When**: Honua is source of truth, want Azure analytics  
**Flow**: `Honua → Azure Analytics → Insights`  
**Complexity**: Low

### Pattern 3: Federated Query
**When**: No data duplication, query both systems  
**Flow**: `Client → Honua Gateway → [Honua DB + Azure ADT]`  
**Complexity**: High

### Pattern 4: Event-Driven Microservices
**When**: Large-scale, distributed architecture  
**Flow**: `Devices → IoT Hub → Event Hub → [Multiple Services]`  
**Complexity**: High

📖 See: [Integration Guide § Integration Architecture](./AZURE_SMART_CITIES_INTEGRATION.md#integration-architecture)

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-4)
- Azure AD setup
- Managed Identity configuration
- Basic connectivity testing

### Phase 2: IoT Hub Integration (Weeks 5-8)
- Telemetry ingestion to SensorThings
- Device mapping workflows

### Phase 3: Azure Digital Twins Sync (Weeks 9-14)
- Bi-directional sync implementation
- DTDL model design

### Phase 4: Event Streaming (Weeks 15-18)
- Event Grid configuration
- Real-time event publishing

### Phase 5: Azure Maps (Weeks 19-22)
- Custom tileset integration
- Indoor maps visualization

### Phase 6: Power BI (Weeks 23-26)
- OData connector optimization
- Dashboard templates

### Phase 7: Production Hardening (Weeks 27-30)
- Security audit
- Performance benchmarking
- Documentation completion

📖 See: [Integration Guide § Implementation Roadmap](./AZURE_SMART_CITIES_INTEGRATION.md#implementation-roadmap)

---

## Key Technologies

### Azure Services
- **Azure Digital Twins** - Semantic digital twin platform
- **Azure IoT Hub** - Device connectivity and management
- **Azure IoT Central** - No-code IoT SaaS
- **Azure Maps** - Geospatial and indoor mapping
- **Event Hubs** - High-throughput event streaming
- **Event Grid** - Event-driven pub/sub
- **Power BI** - Business intelligence dashboards
- **Azure Data Explorer** - Time-series analytics (TSI replacement)

### Honua.Server Capabilities
- **OGC API Features** - Vector feature access
- **WFS/WMS/WMTS** - Traditional OGC services
- **SensorThings API** - IoT sensor data standard
- **MapSDK** - Blazor mapping components
- **3D Features** - Elevation and building heights
- **Anomaly Detection** - Automated sensor monitoring
- **SignalR Streaming** - Real-time WebSocket updates

---

## Authentication Methods

### Recommended: Managed Identity
- No credentials to manage
- Automatic token rotation
- Best for production

```csharp
var credential = new DefaultAzureCredential();
var client = new DigitalTwinsClient(adtUrl, credential);
```

### Alternative: Service Principal
- For development/testing
- Requires credential management

```csharp
var credential = new ClientSecretCredential(tenantId, clientId, secret);
```

📖 See: [Quick Reference § Authentication](./AZURE_INTEGRATION_QUICK_REFERENCE.md#authentication-quick-start)

---

## Sample Code

### Query Azure Digital Twins from Honua
```csharp
public async Task<FeatureCollection> GetBuildingsWithOccupancy()
{
    var buildings = await _honua.GetFeaturesAsync("buildings");
    
    foreach (var building in buildings.Features)
    {
        var twinId = building.Properties["adt_twin_id"];
        var twin = await _adt.GetDigitalTwinAsync<BuildingTwin>(twinId);
        building.Properties["occupancy"] = twin.CurrentOccupancy;
    }
    
    return buildings;
}
```

### Sync Honua Updates to Azure Digital Twins
```csharp
[HttpPatch("features/{collectionId}/{featureId}")]
public async Task<IActionResult> UpdateFeature(string featureId, Feature feature)
{
    await _honua.UpdateFeatureAsync(collectionId, featureId, feature);
    
    // Publish to Event Grid
    await _eventGrid.PublishAsync(new EventGridEvent(
        subject: $"features/{collectionId}/{featureId}",
        eventType: "Honua.Feature.Updated",
        data: feature
    ));
    
    return Ok();
}
```

📖 See: [Quick Reference § Common API Patterns](./AZURE_INTEGRATION_QUICK_REFERENCE.md#common-api-patterns)

---

## Support Resources

### Documentation
- 📘 [Azure Digital Twins Docs](https://learn.microsoft.com/en-us/azure/digital-twins/)
- 📘 [Azure IoT Hub Docs](https://learn.microsoft.com/en-us/azure/iot-hub/)
- 📘 [Azure Maps Docs](https://learn.microsoft.com/en-us/azure/azure-maps/)
- 📘 [Honua SensorThings Guide](../features/SENSORTHINGS_INTEGRATION.md)

### Sample Projects
- [Smart Building Starter](https://github.com/honua-io/azure-smart-building-sample) (Coming Soon)
- [Traffic Monitoring](https://github.com/honua-io/azure-traffic-monitoring-sample) (Coming Soon)
- [Environmental Monitoring](https://github.com/honua-io/azure-air-quality-sample) (Coming Soon)

### Community
- 💬 [Honua Discord](https://discord.gg/honua) - #azure-integration channel
- 💬 [Azure IoT Tech Community](https://techcommunity.microsoft.com/t5/internet-of-things-iot/ct-p/IoT)

### Commercial Support
- 📧 Email: support@honua.io
- 📞 Priority Support: Available with Enterprise tier

---

## FAQ

**Q: Can I use Honua without Azure?**  
A: Yes! Honua works standalone or with AWS, GCP, on-premises. Azure integration is optional.

**Q: Does Azure integration require Enterprise tier?**  
A: No. Azure integration works with all Honua tiers (Free/Professional/Enterprise).

**Q: Can I migrate from Azure Digital Twins to Honua?**  
A: Yes. See migration guide in [Comparison § Migration Paths](./AZURE_COMPARISON.md#migration-paths).

**Q: What's the minimum Azure cost?**  
A: Start with Azure IoT Hub Basic ($10/month) + Event Hub Basic ($20/month) = ~$30/month.

**Q: Is Honua vendor-locked to Azure?**  
A: No. Honua is multi-cloud and works with AWS, GCP, or Azure equally.

---

## Contributing

Found an issue or want to contribute an example?

1. File an issue: [Honua.Server Issues](https://github.com/honua-io/Honua.Server/issues)
2. Submit a PR with improvements
3. Share your integration story in Discussions

---

## Document Status

| Document | Status | Last Updated | Maintainer |
|----------|--------|--------------|------------|
| Integration Guide | ✅ Complete | 2025-11-10 | Platform Team |
| Quick Reference | ✅ Complete | 2025-11-10 | Platform Team |
| Comparison Guide | ✅ Complete | 2025-11-10 | Platform Team |

**Roadmap**:
- [ ] Add Terraform modules for common patterns
- [ ] Create video tutorials
- [ ] Build interactive architecture decision tool
- [ ] Add more code samples (Python, JavaScript)

---

**Questions?** Start a discussion: [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
