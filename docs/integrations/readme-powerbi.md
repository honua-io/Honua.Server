# Power BI Integration for Honua.Server

## Table of Contents

1. [Overview](#overview)
2. [What's Included](#whats-included)
3. [Quick Start](#quick-start)
4. [Dashboard Templates](#dashboard-templates)
5. [Advanced Topics](#advanced-topics)
6. [Troubleshooting](#troubleshooting)
7. [API Reference](#api-reference)

## Overview

The Power BI integration enables municipalities to create professional, real-time smart city dashboards using data from Honua.Server. This integration provides:

- **OData v4 feeds** for direct Power BI Desktop connectivity
- **Push Datasets** for real-time streaming
- **Pre-built templates** for 5 common smart city use cases
- **Programmatic API** for managing datasets and reports
- **Embedding support** for web applications

### Architecture

```
Power BI Desktop → OData v4 → Honua.Server → OGC Features API
                                            → SensorThings API

Power BI Service ← REST API ← Honua.Server
(Streaming)         (Push)
```

## What's Included

### Source Code
- **Project**: `/src/Honua.Integration.PowerBI/`
  - 8 C# implementation files
  - 4 Power Query M templates
  - Example configuration
  - Complete README

### Documentation
- **Quick Start Guide**: 10-minute setup
- **Incremental Refresh Guide**: Large dataset optimization
- **Embedding Guide**: Integrate reports in web apps
- **Troubleshooting Guide**: Common issues and solutions

### Templates
Five pre-built Power Query templates:
1. Traffic Monitoring Dashboard
2. Air Quality Dashboard
3. 311 Service Requests Dashboard
4. Asset Management Dashboard (schema)
5. Building Occupancy Dashboard (schema)

## Quick Start

### Prerequisites

- Power BI Desktop (latest version)
- Honua.Server instance
- (Optional) Azure AD Service Principal for streaming

### 1. Configure Honua.Server

Add to `appsettings.json`:

```json
{
  "PowerBI": {
    "EnableODataFeeds": true,
    "HonuaServerBaseUrl": "https://your-honua-server.com",
    "MaxODataPageSize": 5000
  }
}
```

### 2. Enable in Program.cs

```csharp
using Honua.Integration.PowerBI.Extensions;

builder.Services.AddPowerBIIntegration(builder.Configuration);
app.MapControllers();
```

### 3. Connect Power BI Desktop

**Option A: OData Feed (Easiest)**
1. Get Data → OData Feed
2. URL: `https://your-honua-server.com/odata/features/traffic::sensors`
3. Click OK → Load

**Option B: Power Query M Code (Recommended)**
1. Get Data → Blank Query → Advanced Editor
2. Copy code from `/Templates/TrafficDashboard.pq`
3. Update `HonuaServerUrl` parameter
4. Click Done → Close & Apply

### 4. Create Visualizations

**Traffic Heatmap**:
- Visualization: Map
- Latitude → Latitude
- Longitude → Longitude
- VehicleCount → Size
- CongestionLevel → Legend

**Air Quality Gauge**:
- Visualization: Gauge
- AQI → Value
- Set ranges: Good (0-50), Moderate (51-100), Unhealthy (101+)

**311 Time Series**:
- Visualization: Line Chart
- CreatedAt → X-axis
- COUNT(Id) → Y-axis
- Status → Legend

## Dashboard Templates

### Traffic Monitoring Dashboard

**Data Sources**:
- Collection: `traffic::sensors`
- Update Frequency: Every 15 minutes

**Key Metrics**:
- Vehicle count by location
- Average speed
- Congestion levels (Low/Medium/High)
- Traffic flow direction

**Visualizations**:
- Congestion heatmap (geographic)
- Vehicle count time series
- Average speed gauge
- Top 10 congested locations

**Power Query M**: `/Templates/TrafficDashboard.pq`

### Air Quality Dashboard

**Data Sources**:
- Collection: `environment::air_quality`
- Update Frequency: Hourly

**Key Metrics**:
- PM2.5, PM10 levels
- NO2, O3 concentrations
- Air Quality Index (AQI)
- Compliance vs. EPA standards

**Visualizations**:
- AQI heatmap by location
- PM2.5 trend line
- Compliance scorecard
- Sensor health status

**Power Query M**: `/Templates/AirQualityDashboard.pq`

### 311 Service Requests Dashboard

**Data Sources**:
- Collection: `civic::service_requests`
- Update Frequency: Every 6 hours
- Incremental Refresh: Enabled

**Key Metrics**:
- Open requests by type
- Average time to resolution
- Request volume by district
- Resolution rate

**Visualizations**:
- Request heatmap
- Time-to-resolution histogram
- Status breakdown (pie chart)
- Top 10 request types

**Power Query M**: `/Templates/311RequestsDashboard.pq`

### Real-Time Streaming

For real-time dashboards, use Push Datasets:

```csharp
// Create streaming dataset
var schema = new Table
{
    Name = "SensorReadings",
    Columns = new List<Column>
    {
        new Column { Name = "SensorId", DataType = "string" },
        new Column { Name = "Value", DataType = "double" },
        new Column { Name = "Timestamp", DataType = "datetime" }
    }
};

var (datasetId, pushUrl) = await _datasetService
    .CreateStreamingDatasetAsync("Live Sensors", schema);

// Configure auto-streaming in appsettings.json
{
  "PowerBI": {
    "StreamingDatasets": [{
      "Name": "Live Sensors",
      "DatasetId": "{datasetId}",
      "SourceType": "Observations",
      "AutoStream": true
    }]
  }
}
```

## Advanced Topics

### Incremental Refresh

For datasets with 100K+ rows, enable incremental refresh:

1. Create `RangeStart` and `RangeEnd` parameters (Date/Time)
2. Add filter to query:
   ```m
   #"$filter" = "UpdatedAt ge " & DateTime.ToText(RangeStart)
              & " and UpdatedAt lt " & DateTime.ToText(RangeEnd)
   ```
3. Configure in Power BI: Modeling → Incremental Refresh
4. Set archive window: 5 years
5. Set refresh window: 7 days
6. Enable change detection on `UpdatedAt` column

**Detailed Guide**: [power-bi-incremental-refresh.md](./power-bi-incremental-refresh.md)

### Embedding Reports

Embed Power BI reports in your municipal website:

**Backend (C#)**:
```csharp
[HttpGet("embed-token")]
public async Task<IActionResult> GetEmbedToken(string reportId, string datasetId)
{
    var token = await _datasetService.GenerateEmbedTokenAsync(reportId, datasetId);
    return Ok(new { token });
}
```

**Frontend (JavaScript)**:
```javascript
const { token } = await fetch('/api/powerbi/embed-token?reportId=...').then(r => r.json());

const config = {
    type: 'report',
    tokenType: models.TokenType.Embed,
    accessToken: token,
    embedUrl: `https://app.powerbi.com/reportEmbed?reportId=${reportId}`,
    settings: { filterPaneEnabled: true }
};

powerbi.embed(document.getElementById('reportContainer'), config);
```

**Detailed Guide**: [power-bi-embedding.md](./power-bi-embedding.md)

### Row-Level Security (RLS)

Show users only their district's data:

1. **In Power BI Desktop**:
   - Modeling → Manage Roles → Create "CityUser"
   - Add filter: `[District] = USERNAME()`

2. **In Backend**:
   ```csharp
   var token = await _datasetService.GenerateEmbedTokenWithRLSAsync(
       reportId, datasetId,
       username: "North District",
       roles: new[] { "CityUser" });
   ```

## Troubleshooting

### Cannot Connect to OData Feed

**Check**:
1. Is Honua.Server running? `curl https://your-server.com/health`
2. Is OData enabled? `curl https://your-server.com/odata/features/$metadata`
3. Firewall blocking? `Test-NetConnection -Port 443`

**Solution**:
```json
{
  "PowerBI": { "EnableODataFeeds": true }
}
```

### Query Timeout

**Solutions**:
1. Add filters: `#"$filter" = "UpdatedAt ge 2024-01-01T00:00:00Z"`
2. Reduce page size: `"MaxODataPageSize": 1000`
3. Use incremental refresh for large datasets

### Streaming Not Working

**Check**:
1. Service Principal permissions
2. Dataset ID in configuration
3. Rate limits (10K rows/hour)

**Detailed Guide**: [power-bi-troubleshooting.md](./power-bi-troubleshooting.md)

## API Reference

### OData Endpoints

**Get Features**:
```
GET /odata/features/{collectionId}
    ?$filter=UpdatedAt ge 2024-01-01T00:00:00Z
    &$orderby=UpdatedAt desc
    &$top=100
    &$skip=0
    &$count=true
```

**Get Metadata**:
```
GET /odata/features/$metadata
```

### Power BI Dataset Service

```csharp
// Create dataset
var datasetId = await _datasetService.CreateOrUpdateDatasetAsync(
    "Traffic",
    new[] { "traffic::sensors" });

// Create streaming dataset
var (id, pushUrl) = await _datasetService.CreateStreamingDatasetAsync(
    "Live Sensors",
    tableSchema);

// Push data
await _datasetService.PushRowsAsync(
    datasetId,
    "Observations",
    rows);

// Refresh dataset
await _datasetService.RefreshDatasetAsync(datasetId);

// Generate embed token
var token = await _datasetService.GenerateEmbedTokenAsync(reportId, datasetId);
```

### Configuration Options

```json
{
  "PowerBI": {
    // Azure AD
    "TenantId": "...",
    "ClientId": "...",
    "ClientSecret": "...",

    // Power BI
    "WorkspaceId": "...",
    "ApiUrl": "https://api.powerbi.com",

    // Features
    "EnableODataFeeds": true,
    "EnablePushDatasets": true,
    "EnableDatasetRefresh": true,

    // Performance
    "MaxODataPageSize": 5000,
    "PushDatasetRateLimitPerHour": 10000,
    "StreamingBatchSize": 100,

    // Datasets
    "Datasets": [{
      "Name": "Traffic Dashboard",
      "Type": "Traffic",
      "CollectionIds": ["traffic::sensors"],
      "EnableIncrementalRefresh": true,
      "IncrementalRefreshColumn": "UpdatedAt",
      "RefreshSchedule": "0 */6 * * *"
    }],

    // Streaming
    "StreamingDatasets": [{
      "Name": "Live Sensors",
      "SourceType": "Observations",
      "DatastreamIds": ["sensor-1", "sensor-2"],
      "AutoStream": true
    }]
  }
}
```

## Support & Resources

- **Quick Start**: [power-bi-quickstart.md](./power-bi-quickstart.md)
- **Incremental Refresh**: [power-bi-incremental-refresh.md](./power-bi-incremental-refresh.md)
- **Embedding**: [power-bi-embedding.md](./power-bi-embedding.md)
- **Troubleshooting**: [power-bi-troubleshooting.md](./power-bi-troubleshooting.md)
- **Project README**: `/src/Honua.Integration.PowerBI/README.md`
- **Example Config**: `/src/Honua.Integration.PowerBI/appsettings.powerbi.example.json`

## Performance Benchmarks

| Dataset Size | Import (No Refresh) | Incremental Refresh | DirectQuery |
|-------------|-------------------|-------------------|-------------|
| 10K rows    | 5 sec             | 5 sec            | N/A         |
| 100K rows   | 45 sec            | 5 sec            | ~1 sec/query|
| 1M rows     | 8 min             | 5 sec            | ~2 sec/query|
| 10M rows    | 90 min            | 10 sec           | ~5 sec/query|

## Best Practices

1. **Use OData for historical data** (faster initial load)
2. **Use Streaming Datasets for real-time** (live updates)
3. **Enable incremental refresh for 100K+ rows** (faster refreshes)
4. **Add indexes to datetime columns** (better query performance)
5. **Use DirectQuery for very large datasets** (no import needed)
6. **Implement RLS for multi-tenant scenarios** (data isolation)
7. **Monitor rate limits** (10K rows/hour for streaming)
8. **Cache embed tokens** (reduce API calls)

## License

Copyright (c) 2025 HonuaIO. Licensed under the Elastic License 2.0.

## Getting Help

- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Community Forum: https://community.honua.io/c/power-bi
- Email: support@honua.io
