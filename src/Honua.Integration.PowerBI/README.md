# Honua.Integration.PowerBI

Power BI connector for Honua.Server that enables municipalities to create real-time smart city dashboards.

## Features

- **OData v4 Feed**: Exposes OGC Features API and SensorThings API as OData endpoints for Power BI
- **Push Datasets**: Real-time streaming of sensor observations and alerts to Power BI
- **Pre-built Templates**: Ready-to-use Power Query M code for common smart city dashboards
- **Incremental Refresh**: Support for large datasets with datetime-based incremental refresh
- **Programmatic Management**: Full Power BI REST API integration for dataset/report management
- **Embed Tokens**: Support for embedding Power BI reports in web applications

## Quick Start

### 1. Configuration

Add Power BI settings to `appsettings.json`:

```json
{
  "PowerBI": {
    "TenantId": "your-azure-ad-tenant-id",
    "ClientId": "your-service-principal-app-id",
    "ClientSecret": "your-service-principal-secret",
    "WorkspaceId": "your-powerbi-workspace-id",
    "ApiUrl": "https://api.powerbi.com",
    "EnableODataFeeds": true,
    "EnablePushDatasets": true,
    "MaxODataPageSize": 5000,
    "HonuaServerBaseUrl": "https://your-honua-server.com",
    "Datasets": [
      {
        "Name": "Traffic Monitoring",
        "Type": "Traffic",
        "CollectionIds": ["traffic::sensors", "traffic::incidents"],
        "EnableIncrementalRefresh": true,
        "IncrementalRefreshColumn": "UpdatedAt"
      }
    ],
    "StreamingDatasets": [
      {
        "Name": "Real-Time Observations",
        "SourceType": "Observations",
        "DatastreamIds": ["datastream-1", "datastream-2"],
        "AutoStream": true,
        "RetentionPolicy": 200000
      }
    ]
  }
}
```

### 2. Register Services

In `Program.cs`:

```csharp
using Honua.Integration.PowerBI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Power BI integration
builder.Services.AddPowerBIIntegration(builder.Configuration);

var app = builder.Build();

// Map OData endpoints
app.MapControllers();

app.Run();
```

### 3. Access OData Feeds

OData endpoints are available at:

```
https://your-honua-server.com/odata/features/{collectionId}
```

Example:
```
https://your-honua-server.com/odata/features/traffic::sensors
```

### 4. Create Datasets Programmatically

```csharp
using Honua.Integration.PowerBI.Services;

public class DashboardSetupService
{
    private readonly IPowerBIDatasetService _datasetService;

    public async Task SetupTrafficDashboardAsync()
    {
        var datasetId = await _datasetService.CreateOrUpdateDatasetAsync(
            "Traffic",
            new[] { "traffic::sensors", "traffic::incidents" });

        Console.WriteLine($"Created dataset: {datasetId}");
    }

    public async Task SetupStreamingDatasetAsync()
    {
        var schema = new Table
        {
            Name = "Observations",
            Columns = new List<Column>
            {
                new Column { Name = "DatastreamId", DataType = "string" },
                new Column { Name = "Result", DataType = "double" },
                new Column { Name = "ResultTime", DataType = "datetime" }
            }
        };

        var (datasetId, pushUrl) = await _datasetService.CreateStreamingDatasetAsync(
            "Real-Time Sensors",
            schema);

        Console.WriteLine($"Streaming dataset: {datasetId}");
        Console.WriteLine($"Push URL: {pushUrl}");
    }
}
```

## Power BI Desktop Connection

### Method 1: OData Feed (For Historical Data)

1. Open Power BI Desktop
2. **Get Data** > **OData Feed**
3. Enter URL: `https://your-honua-server.com/odata/features/traffic::sensors`
4. Click **OK**
5. Choose **Basic** or **Organizational account** authentication
6. Select the table and click **Load**

### Method 2: Power Query M Code (Recommended)

1. Open Power BI Desktop
2. **Get Data** > **Blank Query**
3. Click **Advanced Editor**
4. Paste the M code from `/Templates/TrafficDashboard.pq`
5. Update the parameters section with your server URL
6. Click **Done**

### Method 3: Streaming Dataset (For Real-Time Data)

1. Create the streaming dataset using `IPowerBIDatasetService`
2. In Power BI Service (web), create a new dashboard
3. **Add tile** > **REAL-TIME DATA** > **Custom Streaming Data**
4. Select your streaming dataset
5. Choose visualization and fields

## Available Templates

The `/Templates` directory contains pre-built Power Query M code:

- **TrafficDashboard.pq**: Traffic sensor monitoring with congestion heatmaps
- **AirQualityDashboard.pq**: Air quality sensor readings with AQI compliance
- **311RequestsDashboard.pq**: Service request tracking with time-to-resolution
- **StreamingObservations.pq**: Real-time sensor observation streaming

## Incremental Refresh Setup

For large datasets, configure incremental refresh in Power BI Desktop:

1. **Home** > **Transform Data**
2. **Manage Parameters** > **New Parameter**
   - Name: `RangeStart`, Type: Date/Time
   - Name: `RangeEnd`, Type: Date/Time
3. In your query, add filter:
   ```m
   #"$filter" = "UpdatedAt ge " & DateTime.ToText(RangeStart)
              & " and UpdatedAt lt " & DateTime.ToText(RangeEnd)
   ```
4. **Modeling** > **Incremental Refresh**
5. Configure:
   - Archive data starting: 5 years before refresh date
   - Incrementally refresh: 7 days before refresh date
   - Detect data changes: Yes (column: UpdatedAt)

## Embedding Power BI Reports

```csharp
using Honua.Integration.PowerBI.Services;

public class ReportController : ControllerBase
{
    private readonly IPowerBIDatasetService _datasetService;

    [HttpGet("embed-token")]
    public async Task<IActionResult> GetEmbedToken(
        string reportId,
        string datasetId)
    {
        var token = await _datasetService.GenerateEmbedTokenAsync(
            reportId,
            datasetId);

        return Ok(new { token });
    }
}
```

HTML/JavaScript:
```html
<div id="reportContainer" style="height: 600px;"></div>

<script src="https://cdn.jsdelivr.net/npm/powerbi-client@2.22.0/dist/powerbi.min.js"></script>
<script>
    const reportContainer = document.getElementById('reportContainer');
    const embedUrl = 'https://app.powerbi.com/reportEmbed';
    const reportId = 'your-report-id';

    fetch('/api/embed-token?reportId=' + reportId + '&datasetId=' + datasetId)
        .then(res => res.json())
        .then(data => {
            const config = {
                type: 'report',
                tokenType: models.TokenType.Embed,
                accessToken: data.token,
                embedUrl: embedUrl + '?reportId=' + reportId,
                settings: {
                    filterPaneEnabled: true,
                    navContentPaneEnabled: true
                }
            };

            powerbi.embed(reportContainer, config);
        });
</script>
```

## Architecture

```
┌─────────────────┐
│   Power BI      │
│   Desktop       │
└────────┬────────┘
         │ OData v4
         ├─────────────────────┐
         │                     │
         ↓                     ↓
┌─────────────────┐   ┌──────────────────┐
│ OData Endpoint  │   │ Power BI Service │
│ (Features API)  │   │  (Streaming)     │
└────────┬────────┘   └────────┬─────────┘
         │                     │
         │                     │ REST API
         ↓                     ↓
┌──────────────────────────────────────┐
│       Honua.Integration.PowerBI       │
│  ┌─────────────┐  ┌─────────────────┐│
│  │   OData     │  │   Streaming     ││
│  │ Controller  │  │    Service      ││
│  └─────────────┘  └─────────────────┘│
└──────────────┬───────────────────────┘
               │
               ↓
┌──────────────────────────────────────┐
│         Honua.Server.Core             │
│  ┌─────────────┐  ┌─────────────────┐│
│  │  Features   │  │  SensorThings   ││
│  │     API     │  │      API        ││
│  └─────────────┘  └─────────────────┘│
└──────────────────────────────────────┘
```

## Performance Considerations

- **OData Page Size**: Default 1000, max 5000 rows per page
- **Streaming Rate Limit**: 10,000 rows per hour per dataset
- **Batch Size**: 100 rows per push request
- **Incremental Refresh**: Recommended for datasets > 100,000 rows
- **Caching**: Power BI caches OData responses for ~1 hour

## Troubleshooting

### "Unable to connect to the data source"

- Verify Honua.Server URL is accessible from Power BI Desktop
- Check firewall rules allow outbound HTTPS connections
- Ensure authentication credentials are correct

### "Query timeout"

- Reduce OData page size in configuration
- Add filters to limit data volume
- Enable incremental refresh for large datasets

### "Rate limit exceeded"

- Streaming datasets limited to 10,000 rows/hour
- Reduce streaming batch size
- Increase delay between batches

### "Authentication failed"

- Verify Service Principal has Power BI admin permissions
- Check Azure AD tenant ID is correct
- Ensure client secret hasn't expired

## License

Copyright (c) 2025 HonuaIO. Licensed under the Elastic License 2.0.
