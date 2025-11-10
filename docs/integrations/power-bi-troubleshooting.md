# Power BI Troubleshooting Guide

Common issues and solutions when connecting Power BI to Honua.Server.

## Connection Issues

### Error: "Unable to connect to the data source"

**Symptoms**: Cannot connect to OData feed from Power BI Desktop

**Possible Causes**:
1. Honua.Server is not running
2. Firewall blocking connection
3. Incorrect URL
4. HTTPS certificate issues

**Solutions**:

#### Check Server Status
```bash
# Test if Honua.Server is accessible
curl https://your-honua-server.com/health

# Test OData metadata endpoint
curl https://your-honua-server.com/odata/features/$metadata
```

#### Verify URL Format
Correct format:
```
https://your-honua-server.com/odata/features/{collectionId}
```

Example:
```
https://demo.honua.io/odata/features/traffic::sensors
```

#### Check Firewall
```bash
# Windows: Test connection from Power BI Desktop machine
Test-NetConnection -ComputerName your-honua-server.com -Port 443

# Linux/Mac
telnet your-honua-server.com 443
```

#### HTTPS Certificate Issues
If using self-signed certificate:

1. Power BI Desktop > **File** > **Options** > **Security**
2. Uncheck **Verify server certificate**
3. Restart Power BI Desktop

### Error: "Authentication failed"

**Symptoms**: Prompted for credentials, but login fails

**Solutions**:

#### Anonymous Access
If Honua.Server allows anonymous:
1. Choose **Anonymous** authentication
2. Apply to: **https://your-honua-server.com/**

#### Basic Authentication
```json
// In appsettings.json
{
  "Authentication": {
    "Schemes": {
      "Basic": {
        "Enabled": true,
        "Users": {
          "powerbi": "your-password"
        }
      }
    }
  }
}
```

In Power BI:
1. Choose **Basic** authentication
2. Username: `powerbi`
3. Password: `your-password`

#### Azure AD / OAuth
```json
{
  "Authentication": {
    "AzureAd": {
      "Enabled": true,
      "TenantId": "your-tenant-id",
      "ClientId": "your-app-id"
    }
  }
}
```

In Power BI:
1. Choose **Organizational account**
2. Sign in with Azure AD credentials

### Error: "The remote name could not be resolved"

**Symptoms**: DNS lookup fails

**Solutions**:

1. **Check DNS**:
   ```bash
   nslookup your-honua-server.com
   ```

2. **Use IP address** (temporary):
   ```
   https://192.168.1.100/odata/features/traffic::sensors
   ```

3. **Add to hosts file** (Windows: `C:\Windows\System32\drivers\etc\hosts`):
   ```
   192.168.1.100 your-honua-server.com
   ```

## Data Loading Issues

### Error: "Query timeout"

**Symptoms**: Query runs for a long time then fails

**Solutions**:

#### Increase Timeout
In Power Query M code:
```m
Source = OData.Feed(
    HonuaServerUrl & "/odata/features/" & CollectionId,
    null,
    [
        Timeout = #duration(0, 0, 10, 0), // 10 minutes
        Implementation = "2.0"
    ]
)
```

#### Add Filters
Reduce data volume:
```m
Query = [
    #"$filter" = "UpdatedAt ge 2024-01-01T00:00:00Z",
    #"$top" = "1000"
]
```

#### Use DirectQuery
Instead of **Import**, use **DirectQuery** mode:
1. When connecting, choose **DirectQuery**
2. Data stays in Honua.Server
3. Queries run on-demand

### Error: "Memory limit exceeded"

**Symptoms**: Power BI Desktop crashes or freezes

**Solutions**:

1. **Reduce page size**:
   ```json
   // In appsettings.json
   {
     "PowerBI": {
       "MaxODataPageSize": 1000
     }
   }
   ```

2. **Add filters**:
   ```m
   // Only load last 30 days
   #"$filter" = "UpdatedAt ge " & DateTime.ToText(
       Date.AddDays(DateTime.LocalNow(), -30),
       "yyyy-MM-ddTHH:mm:ssZ"
   )
   ```

3. **Use aggregations**:
   ```m
   // Instead of loading all rows, aggregate first
   #"$apply" = "groupby((Date), aggregate($count as Count))"
   ```

### Error: "Column 'XYZ' does not exist"

**Symptoms**: Query references column that doesn't exist

**Solutions**:

1. **Check metadata**:
   ```bash
   curl https://your-honua-server.com/odata/features/traffic::sensors?$metadata
   ```

2. **Use dynamic columns**:
   ```m
   // Instead of hardcoded columns
   ExpandedProperties = Table.ExpandRecordColumn(
       Source,
       "Properties",
       Record.FieldNames(Source[Properties]{0})
   )
   ```

3. **Handle missing columns**:
   ```m
   SafeColumn = try Source[ColumnName] otherwise null
   ```

## Streaming Issues

### Error: "Rate limit exceeded"

**Symptoms**: Streaming stops, error in logs

**Solutions**:

1. **Check rate limits**:
   - Power BI allows 10,000 rows/hour per dataset
   - 15 requests/second max

2. **Reduce batch size**:
   ```json
   {
     "PowerBI": {
       "StreamingBatchSize": 50
     }
   }
   ```

3. **Add delays**:
   ```csharp
   await Task.Delay(100); // Between batches
   ```

### Error: "Dataset not found"

**Symptoms**: Cannot push data to streaming dataset

**Solutions**:

1. **Verify dataset exists**:
   ```csharp
   var datasets = await _datasetService.GetDatasetsAsync();
   foreach (var ds in datasets)
   {
       Console.WriteLine($"{ds.Name}: {ds.Id}");
   }
   ```

2. **Recreate dataset**:
   ```csharp
   await _datasetService.DeleteDatasetAsync(oldDatasetId);
   var (newId, pushUrl) = await _datasetService.CreateStreamingDatasetAsync(...);
   ```

3. **Check configuration**:
   ```json
   {
     "PowerBI": {
       "StreamingDatasets": [
         {
           "Name": "Real-Time Sensors",
           "DatasetId": "actual-dataset-id-from-power-bi"
         }
       ]
     }
   }
   ```

## Incremental Refresh Issues

### Error: "Incremental refresh is only supported for Power BI Premium"

**Solutions**:

1. Upgrade to Power BI Premium or Premium Per User (PPU)
2. Or disable incremental refresh and use regular refresh

### Data not refreshing incrementally

**Symptoms**: Full refresh happens every time

**Solutions**:

1. **Verify parameters exist**:
   - `RangeStart` (Date/Time)
   - `RangeEnd` (Date/Time)

2. **Check filter logic**:
   ```m
   // Must use RangeStart and RangeEnd parameters
   #"$filter" = "UpdatedAt ge " & DateTime.ToText(RangeStart) &
                " and UpdatedAt lt " & DateTime.ToText(RangeEnd)
   ```

3. **Enable change detection**:
   - Right-click table > Incremental refresh
   - Detect data changes: **Yes**
   - Column: **UpdatedAt**

## Performance Issues

### Slow queries

**Solutions**:

1. **Add database indexes**:
   ```sql
   CREATE INDEX idx_features_updated_at ON features(updated_at);
   CREATE INDEX idx_features_geometry ON features USING GIST(geometry);
   ```

2. **Enable caching** in Honua.Server:
   ```json
   {
     "Caching": {
       "ODataCacheDurationSeconds": 300
     }
   }
   ```

3. **Use query folding**:
   - Power BI translates queries to OData automatically
   - Check **View Native Query** in Power Query to verify

### Refresh takes too long

**Solutions**:

1. **Use incremental refresh** (see guide)
2. **Partition large tables**:
   - By date: Daily, weekly, monthly
   - By region: North, South, East, West
3. **Schedule during off-peak hours**:
   - 2:00 AM - 6:00 AM

## Authorization Issues

### Error: "Forbidden" or "403"

**Symptoms**: User not authorized to access data

**Solutions**:

1. **Check Service Principal permissions**:
   ```bash
   # Azure CLI
   az ad sp show --id {client-id} --query "appRoles"
   ```

2. **Grant Power BI permissions**:
   - Power BI Admin Portal > Tenant settings
   - Enable "Service principals can use Power BI APIs"
   - Add your app to allowed list

3. **Check Honua.Server roles**:
   ```json
   {
     "Authorization": {
       "Policies": {
         "ODataRead": {
           "Requirements": ["PowerBIUser"]
         }
       }
     }
   }
   ```

## Data Type Issues

### Geometry not displaying

**Symptoms**: Lat/Lon columns exist but map doesn't work

**Solutions**:

1. **Check data types**:
   ```m
   Table.TransformColumnTypes(Source, {
       {"Latitude", type number},
       {"Longitude", type number}
   })
   ```

2. **Verify range**:
   - Latitude: -90 to 90
   - Longitude: -180 to 180

3. **Use Map visual** (not Globe):
   - Globe requires specific format
   - Map is more flexible

### Dates not parsing

**Symptoms**: Date columns show as text

**Solutions**:

```m
// ISO 8601 format
Table.TransformColumnTypes(Source, {
    {"CreatedAt", type datetimezone}
})

// Custom format
Table.TransformColumns(Source, {
    {"CustomDate", each DateTime.FromText(_, "en-US")}
})
```

## Diagnostic Commands

### Check Honua.Server Logs

```bash
# Docker
docker logs honua-server --tail 100 --follow

# Direct
journalctl -u honua-server -f
```

### Test OData Endpoint

```bash
# Get metadata
curl https://your-honua-server.com/odata/features/$metadata

# Get data
curl "https://your-honua-server.com/odata/features/traffic::sensors?\$top=5"

# Test filter
curl "https://your-honua-server.com/odata/features/traffic::sensors?\$filter=UpdatedAt ge 2024-01-01T00:00:00Z&\$top=5"
```

### Power BI Diagnostics

1. **Enable detailed logging**:
   - Power BI Desktop > File > Options > Diagnostics
   - Enable tracing

2. **Capture network traffic**:
   - Use Fiddler or Wireshark
   - Filter by hostname: `your-honua-server.com`

3. **Check query folding**:
   - Right-click step in Power Query
   - **View Native Query**
   - Should show OData query, not "This step doesn't fold"

## Getting Help

If issue persists:

1. **Collect diagnostics**:
   - Power BI error screenshot
   - Honua.Server logs
   - OData query that fails
   - Network trace

2. **File GitHub issue**:
   - Repository: https://github.com/honua-io/Honua.Server/issues
   - Label: `power-bi`

3. **Community forum**:
   - https://community.honua.io/c/power-bi

4. **Email support**:
   - support@honua.io
   - Include: version, error message, steps to reproduce
