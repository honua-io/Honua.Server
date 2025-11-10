# Power BI Quick Start Guide

This guide walks you through connecting Power BI to Honua.Server to create smart city dashboards in under 10 minutes.

## Prerequisites

- Power BI Desktop (latest version)
- Honua.Server instance running and accessible
- Basic understanding of Power BI concepts

## Step 1: Configure Honua.Server

### 1.1 Azure AD Setup (Optional for Streaming)

If you want real-time streaming, create an Azure AD Service Principal:

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations** > **New registration**
3. Name: "Honua Power BI Integration"
4. Click **Register**
5. Note the **Application (client) ID** and **Directory (tenant) ID**
6. Go to **Certificates & secrets** > **New client secret**
7. Note the **secret value** (you won't see it again!)
8. Go to **API permissions** > **Add a permission** > **Power BI Service**
9. Add **Dataset.ReadWrite.All** (Application permission)
10. Click **Grant admin consent**

### 1.2 Configure appsettings.json

Add to your Honua.Server `appsettings.json`:

```json
{
  "PowerBI": {
    "TenantId": "your-tenant-id-from-step-5",
    "ClientId": "your-client-id-from-step-5",
    "ClientSecret": "your-secret-from-step-7",
    "WorkspaceId": "your-powerbi-workspace-id",
    "EnableODataFeeds": true,
    "EnablePushDatasets": true,
    "HonuaServerBaseUrl": "https://your-honua-server.com"
  }
}
```

To find your Workspace ID:
1. Go to [Power BI Service](https://app.powerbi.com)
2. Open a workspace
3. Look at the URL: `https://app.powerbi.com/groups/{workspace-id}/...`

### 1.3 Enable Power BI in Program.cs

```csharp
using Honua.Integration.PowerBI.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPowerBIIntegration(builder.Configuration);
var app = builder.Build();
app.MapControllers();
app.Run();
```

### 1.4 Restart Honua.Server

```bash
dotnet run
```

Verify OData endpoint is accessible:
```bash
curl https://your-honua-server.com/odata/features/$metadata
```

## Step 2: Create Your First Dashboard in Power BI Desktop

### Option A: Using OData Feed (Easiest)

1. Open **Power BI Desktop**
2. Click **Get Data** > **OData Feed**
3. Enter URL:
   ```
   https://your-honua-server.com/odata/features/traffic::sensors
   ```
   (Replace `traffic::sensors` with your collection ID)
4. Click **OK**
5. Choose authentication:
   - **Anonymous** (if Honua.Server allows)
   - **Basic** (username/password)
   - **Organizational account** (Azure AD)
6. Click **Load**

You should see the data appear in the Fields pane!

### Option B: Using Power Query M Code (Recommended)

1. Open **Power BI Desktop**
2. Click **Get Data** > **Blank Query**
3. In the Power Query Editor, click **Advanced Editor**
4. Copy the code from one of these templates:
   - Traffic Monitoring: `/src/Honua.Integration.PowerBI/Templates/TrafficDashboard.pq`
   - Air Quality: `/src/Honua.Integration.PowerBI/Templates/AirQualityDashboard.pq`
   - 311 Requests: `/src/Honua.Integration.PowerBI/Templates/311RequestsDashboard.pq`
5. Update the parameters:
   ```m
   HonuaServerUrl = "https://your-honua-server.com",
   CollectionId = "your-service::your-layer",
   ```
6. Click **Done**
7. Click **Close & Apply**

## Step 3: Create Visualizations

### Example: Traffic Congestion Map

1. In the **Visualizations** pane, click **Map**
2. Drag fields:
   - **Latitude** → Latitude
   - **Longitude** → Longitude
   - **VehicleCount** → Size
   - **CongestionLevel** → Legend
3. Format the map:
   - **Data colors** > Set colors:
     - Low = Green
     - Medium = Orange
     - High = Red

### Example: Air Quality Gauge

1. Click **Gauge** visualization
2. Drag fields:
   - **AQI** → Value
3. Set gauge ranges:
   - Good: 0-50 (Green)
   - Moderate: 51-100 (Yellow)
   - Unhealthy: 101-150 (Orange)
   - Very Unhealthy: 151+ (Red)

### Example: 311 Requests Time Series

1. Click **Line Chart**
2. Drag fields:
   - **CreatedAt** → X-axis
   - **COUNT(Id)** → Y-axis
   - **Status** → Legend

## Step 4: Configure Auto-Refresh

### For Imported Data

1. Click **File** > **Options and settings** > **Data source settings**
2. Select your OData source
3. Click **Edit Permissions** > **Credentials** > **Edit**
4. Set refresh frequency:
   - In Power BI Service: **Dataset settings** > **Scheduled refresh**
   - Frequency: Hourly, Daily, or Weekly

### For DirectQuery (Real-time)

1. When connecting to OData, choose **DirectQuery** instead of **Import**
2. Data updates automatically when users view the report
3. Note: DirectQuery has query limitations and may be slower

### For Streaming Data

See [Real-Time Streaming Guide](#step-5-set-up-real-time-streaming) below.

## Step 5: Set Up Real-Time Streaming

### 5.1 Create Streaming Dataset (via Code)

```csharp
using Honua.Integration.PowerBI.Services;
using Microsoft.PowerBI.Api.Models;

public class StreamingSetup
{
    private readonly IPowerBIDatasetService _datasetService;

    public async Task CreateStreamingDatasetAsync()
    {
        var schema = new Table
        {
            Name = "SensorReadings",
            Columns = new List<Column>
            {
                new Column { Name = "SensorId", DataType = "string" },
                new Column { Name = "Value", DataType = "double" },
                new Column { Name = "Timestamp", DataType = "datetime" },
                new Column { Name = "Location", DataType = "string" }
            }
        };

        var (datasetId, pushUrl) = await _datasetService
            .CreateStreamingDatasetAsync("Real-Time Sensors", schema);

        Console.WriteLine($"Dataset ID: {datasetId}");
        Console.WriteLine($"Push URL: {pushUrl}");
    }
}
```

### 5.2 Configure Auto-Streaming

In `appsettings.json`:

```json
{
  "PowerBI": {
    "StreamingDatasets": [
      {
        "Name": "Real-Time Sensors",
        "SourceType": "Observations",
        "DatastreamIds": ["temp-sensor-1", "humidity-sensor-2"],
        "AutoStream": true,
        "RetentionPolicy": 200000
      }
    ]
  }
}
```

### 5.3 Create Streaming Tile in Power BI Service

1. Go to [Power BI Service](https://app.powerbi.com)
2. Create a new **Dashboard**
3. Click **Add tile** > **CUSTOM STREAMING DATA**
4. Select your streaming dataset
5. Choose visualization type:
   - **Line Chart**: For time series
   - **Card**: For latest value
   - **Gauge**: For thresholds
6. Configure fields and click **Apply**

Data now flows automatically from Honua.Server to Power BI!

## Step 6: Publish to Power BI Service

1. In Power BI Desktop, click **Publish**
2. Sign in to Power BI Service
3. Choose a workspace
4. Click **Select**
5. Once published, click **Open in Power BI**

## Step 7: Share Your Dashboard

1. In Power BI Service, open your report
2. Click **Share** or **File** > **Embed report** > **Website or portal**
3. Options:
   - **Share with colleagues**: Enter email addresses
   - **Publish to web**: Get public embed code (⚠️ data becomes public)
   - **Embed in app**: Use embed tokens (see Embedding Guide)

## Next Steps

- [Configure Incremental Refresh](./power-bi-incremental-refresh.md) for large datasets
- [Embedding Guide](./power-bi-embedding.md) to embed reports in your web app
- [Troubleshooting](./power-bi-troubleshooting.md) common issues
- [Advanced Scenarios](./power-bi-advanced.md) for custom visualizations

## Common Collection IDs

Assuming standard Honua.Server setup:

- Traffic sensors: `traffic::sensors`
- Air quality: `environment::air_quality`
- 311 requests: `civic::service_requests`
- Asset management: `assets::field_equipment`
- Building occupancy: `facilities::occupancy`

List all collections:
```bash
curl https://your-honua-server.com/ogc/collections
```

## Example Dashboard Screenshots

(Screenshots would go here in production)

## Support

- [GitHub Issues](https://github.com/honua-io/Honua.Server/issues)
- [Community Forum](https://community.honua.io)
- Email: support@honua.io
