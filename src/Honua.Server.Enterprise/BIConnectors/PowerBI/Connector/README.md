# Honua Power BI Custom Connector

Connect Power BI to Honua geospatial server via OGC Features API and STAC catalogs using Power Query.

## Features

- ✅ **OGC Features API** - Connect to vector feature collections
- ✅ **STAC Catalog** - Connect to spatiotemporal asset catalogs
- ✅ **Automatic Pagination** - Handles large datasets seamlessly
- ✅ **Property Flattening** - Common properties expanded as columns
- ✅ **Geometry Extraction** - Centroids (lat/lon) and bounding boxes
- ✅ **Multiple Authentication** - Anonymous, Username/Password, API Key, OAuth 2.0
- ✅ **Incremental Refresh** - Support for Power BI incremental refresh

## Requirements

- Power BI Desktop (latest version)
- Power Query SDK for Visual Studio Code (for development)
- Honua Server with OGC Features API or STAC catalog enabled

## Installation

### Option 1: Install Pre-built Connector

1. Download the latest `Honua.mez` file from releases
2. Copy to Power BI custom connectors directory:
   - **Windows**: `C:\Users\[Username]\Documents\Power BI Desktop\Custom Connectors\`
   - Create the folder if it doesn't exist

3. Enable custom connectors in Power BI Desktop:
   - Go to **File** → **Options and settings** → **Options**
   - Navigate to **Security** → **Data Extensions**
   - Select **(Not Recommended) Allow any extension to load without validation or warning**
   - Click **OK** and restart Power BI Desktop

4. The connector will appear in **Get Data** dialog under **Honua Geospatial Data**

### Option 2: Build from Source

#### Prerequisites

Install Power Query SDK:
```bash
# Install VS Code
# Then install Power Query SDK extension from marketplace
code --install-extension PowerQuery.vscode-powerquery-sdk
```

#### Build Steps

1. Open the `Connector` folder in VS Code
2. Open Command Palette (Ctrl+Shift+P)
3. Run `Power Query: Evaluate current file`
4. To package:
   ```bash
   # The SDK will create .mez file in bin/AnyCPU/Debug or Release
   ```

5. Copy the `.mez` file to Power BI Custom Connectors folder

## Usage

### 1. Connect to Honua

1. Open Power BI Desktop
2. Click **Get Data** → **More...**
3. Search for "Honua"
4. Select **Honua Geospatial Data**
5. Click **Connect**

### 2. Configure Connection

| Parameter | Description | Example |
|-----------|-------------|---------|
| **Server URL** | Base URL of your Honua server | `https://api.honua.io` |
| **Data Source** | Choose "OGC Features" or "STAC" | `OGC Features` |
| **Collection/Layer ID** | (Optional) Specific collection | `world-cities` |

**Notes:**
- If you leave Collection/Layer ID blank, you'll get a list of all collections
- Each collection will have an "Items" column you can expand

### 3. Authenticate

Choose authentication method:

#### Anonymous (Public APIs)
- Simply click **Connect**

#### Username/Password
- Enter your Honua username and password
- Credentials are securely stored

#### API Key
- Enter your API key when prompted
- The connector sends it as `X-API-Key` header

#### OAuth 2.0 (Enterprise)
- Click **Sign in**
- Follow OAuth flow in browser
- Tokens are automatically refreshed

### 4. Transform Data

Once connected, you'll see a table with these columns:

#### OGC Features Columns
- `FeatureId` - Unique identifier
- `Type` - Feature type (usually "Feature")
- `GeometryType` - Point, LineString, Polygon, etc.
- `Latitude` / `Longitude` - Centroid coordinates
- `BBoxMinX/Y` / `BBoxMaxX/Y` - Bounding box
- `Geometry` - Full geometry object (can be expanded)
- `Properties` - Feature properties (auto-flattened)
- `Prop_*` - Common properties as separate columns

#### STAC Items Columns
- `ItemId` - Unique identifier
- `Collection` - Collection name
- `GeometryType` - Geometry type
- `Latitude` / `Longitude` - Centroid coordinates
- `DateTime` - Capture/observation timestamp
- `BBoxMinX/Y` / `BBoxMaxX/Y` - Bounding box
- `Properties` - Item properties
- `STAC_*` - Common STAC properties as columns
- `AssetsJson` - JSON string of assets
- `Assets` - Full assets object (can be expanded)

### 5. Create Visualizations

Click **Load** or **Transform Data** to proceed to report view.

## Examples

### Example 1: Simple Point Map

1. Connect to OGC Features: `https://api.honua.io`, Collection: `world-cities`
2. Load data
3. Create a **Map** visual
4. Drag `Latitude` to **Latitude** field
5. Drag `Longitude` to **Longitude** field
6. Drag `Prop_name` to **Tooltips**

### Example 2: STAC Satellite Imagery Timeline

1. Connect to STAC: `https://api.honua.io`, Collection: `sentinel-2`
2. Load data
3. Create a **Line Chart**
4. Drag `DateTime` to **Axis**
5. Drag `STAC_eo_cloud_cover` to **Values**
6. Filter by `BBoxMinX/Y/MaxX/Y` for your area of interest

### Example 3: Multiple Collections

1. Connect without specifying Collection ID
2. You'll get a list of all collections
3. Click **Expand** on the `Items` column
4. Select columns to include
5. Click **OK** to expand
6. Now you have all items from all collections

### Example 4: Incremental Refresh

For large datasets, configure incremental refresh:

1. In Power Query, add a filter on `DateTime` using `RangeStart` and `RangeEnd` parameters
2. Close Power Query and publish to Power BI Service
3. In Power BI Service, configure incremental refresh policy:
   - Archive data: Last 5 years
   - Refresh data: Last 30 days
   - Detect data changes: No

## Advanced Usage

### Custom Filters

Add filters in Power Query:

```powerquery
// Filter by bounding box
= Table.SelectRows(Source, each [Longitude] >= -180 and [Longitude] <= -120)

// Filter by date range (STAC)
= Table.SelectRows(Source, each [DateTime] >= #date(2023, 1, 1))

// Filter by property value
= Table.SelectRows(Source, each [Prop_category] = "residential")
```

### Parsing JSON Properties

If you need to extract specific properties:

```powerquery
// Add custom column
= Table.AddColumn(Source, "CustomProp", each
    try Json.Document([Properties])[myCustomField] otherwise null
)
```

### Joining Collections

Join multiple collections:

```powerquery
let
    // Load first collection
    Collection1 = Honua.Contents("https://api.honua.io", "OGC Features", "collection-1"),

    // Load second collection
    Collection2 = Honua.Contents("https://api.honua.io", "OGC Features", "collection-2"),

    // Join on common field
    Joined = Table.NestedJoin(
        Collection1, {"FeatureId"},
        Collection2, {"FeatureId"},
        "Related",
        JoinKind.LeftOuter
    )
in
    Joined
```

## Performance Tips

### 1. Filter Early
Apply filters in Power Query before loading to reduce data volume

### 2. Select Only Needed Columns
Remove unnecessary columns in Power Query Editor

### 3. Use DirectQuery (Enterprise)
For very large datasets, consider DirectQuery mode (requires Power BI Premium)

### 4. Enable Query Folding
The connector supports query folding for:
- Row filtering
- Column selection
- Top N rows

### 5. Incremental Refresh
For time-series data, always configure incremental refresh

## Troubleshooting

### Connector Doesn't Appear

**Solution:**
1. Verify `.mez` file is in Custom Connectors folder
2. Ensure custom connectors are enabled in Options → Security
3. Restart Power BI Desktop

### Authentication Fails

**Solution:**
- Verify credentials are correct
- Check server URL is accessible
- For OAuth, ensure redirect URI is configured on server

### No Data / Empty Table

**Solution:**
- Verify collection ID exists and has data
- Check user permissions for the collection
- Look at Applied Steps in Power Query to see if filters excluded all rows

### Slow Performance

**Solution:**
- Add filters to reduce data volume
- Select only needed columns
- Consider incremental refresh
- Check network latency to Honua server

### Error: "Expression.Error: The key didn't match any rows in the table"

**Solution:**
This means the collection ID doesn't exist. Check spelling or leave blank to see all collections.

## API Compatibility

This connector is compatible with:
- **OGC Features API** - Part 1: Core (v1.0)
- **STAC API** - v1.0.0+
- **Honua Server** - v1.0+

## Security Notes

- Credentials are stored securely by Power BI
- Use HTTPS URLs in production
- API keys are transmitted in headers (not URL)
- OAuth tokens are automatically refreshed
- Never share `.pbix` files containing credentials

## Known Limitations

1. **Geometry Rendering**: Power BI native map visuals only support point data. For polygons/lines, use the Honua Kepler.gl custom visual.
2. **Data Limit**: Power BI has a 1GB limit per dataset. Use filters or incremental refresh for large collections.
3. **Real-time**: This connector fetches data at refresh time. For real-time streaming, use Power BI streaming datasets.

## Migration from WDC

If migrating from Tableau WDC:

| Tableau WDC | Power BI Connector |
|-------------|-------------------|
| `feature_id` | `FeatureId` |
| `geometry_wkt` | `Geometry` (expanded) |
| `properties` (JSON string) | `Properties` + flattened `Prop_*` columns |

## Support

- **Documentation**: https://docs.honua.io/powerbi/connector
- **Issues**: https://github.com/HonuaIO/powerbi-connector/issues
- **Email**: support@honua.io

## License

MIT License - See LICENSE file for details

## Version History

### 1.0.0 (2025-02-01)
- Initial release
- OGC Features API support
- STAC Catalog support
- Multiple authentication methods
- Automatic pagination
- Property flattening
- Query folding support
