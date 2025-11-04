# Honua Tableau Web Data Connector

Connect Tableau to Honua geospatial server via OGC Features API and STAC catalogs.

## Features

- ✅ **OGC Features API** - Connect to vector feature collections
- ✅ **STAC Catalog** - Connect to spatiotemporal asset catalogs
- ✅ **Multiple Authentication** - Support for Bearer Token, API Key, Basic Auth, and None
- ✅ **Automatic Pagination** - Fetches all pages of data automatically
- ✅ **Geometry Support** - Extracts centroids (lat/lon) and WKT representations
- ✅ **Bounding Boxes** - Includes bbox coordinates for spatial filtering

## Requirements

- Tableau Desktop 2022.3 or later
- Honua Server with OGC Features API or STAC catalog enabled
- Web server to host the connector (for testing)

## Installation

### Option 1: Deploy to Web Server

1. Copy all files to a web server accessible by Tableau:
   ```bash
   # Upload to your web server
   scp -r * user@yourserver:/var/www/html/tableau-connector/
   ```

2. In Tableau Desktop, go to **Web Data Connector**
3. Enter the URL: `https://yourserver.com/tableau-connector/connector.html`

### Option 2: Local Testing

1. Start a local web server:
   ```bash
   # Using Python
   python -m http.server 8000

   # Or using Node.js
   npx http-server -p 8000 -c-1
   ```

2. In Tableau Desktop:
   - Go to **Connect** → **Web Data Connector**
   - Enter URL: `http://localhost:8000/connector.html`

### Option 3: Package as .taco (Recommended for Enterprise)

To create a packaged connector that appears in Tableau's native connector list:

1. Install Tableau Connector SDK:
   ```bash
   npm install -g @tableau/taco-toolkit
   ```

2. Package the connector:
   ```bash
   taco pack manifest.json
   ```

3. Install the .taco file:
   - Copy `honua-ogc-features.taco` to Tableau's connector directory:
     - **Windows**: `C:\Users\[Username]\Documents\My Tableau Repository\Connectors`
     - **Mac**: `/Users/[Username]/Documents/My Tableau Repository/Connectors`

4. Restart Tableau Desktop

## Usage

### 1. Configure Connection

When the connector opens, provide:

| Field | Description | Example |
|-------|-------------|---------|
| **Server URL** | Base URL of your Honua server | `https://api.honua.io` |
| **Data Source** | Choose OGC Features or STAC | `OGC Features API` |
| **Collection/Layer ID** | Specific collection or layer | `world-cities` |
| **Authentication Type** | How to authenticate | `Bearer Token` |
| **Credentials** | Token, API key, or username/password | Your JWT token |

### 2. Click "Connect to Honua"

The connector will:
1. Validate your configuration
2. Fetch data from the API
3. Handle pagination automatically
4. Load data into Tableau

### 3. Build Visualizations

Once connected, you'll see tables:

#### OGC Features Table
- `feature_id` - Unique feature identifier
- `geometry_type` - Point, LineString, Polygon, etc.
- `geometry_wkt` - Well-Known Text representation
- `latitude` / `longitude` - Centroid coordinates for mapping
- `bbox_minx` / `bbox_miny` / `bbox_maxx` / `bbox_maxy` - Bounding box
- `properties` - JSON string of all properties
- `collection_id` - Collection identifier

#### STAC Items Table
- `item_id` - Unique item identifier
- `collection` - Collection identifier
- `geometry_type` - Geometry type
- `geometry_wkt` - Well-Known Text representation
- `latitude` / `longitude` - Centroid coordinates
- `datetime` - Capture/observation date
- `bbox_*` - Bounding box coordinates
- `properties` - JSON string of properties
- `assets` - JSON string of asset links

### Example Visualizations

#### 1. Map View
Drag `longitude` to **Columns** and `latitude` to **Rows**, then set to **Map**

#### 2. Temporal Analysis (STAC)
Use `datetime` field to create time-series visualizations

#### 3. Property Analysis
Parse the `properties` JSON field using calculated fields:
```
// Extract a property
JSON_VALUE([properties], '$.name')
```

## Authentication Examples

### Bearer Token (JWT)
```
Authentication Type: Bearer Token
Token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### API Key
```
Authentication Type: API Key
API Key: sk_live_abc123def456...
```

### Basic Auth
```
Authentication Type: Basic Auth
Username: user@example.com
Password: ••••••••
```

### No Authentication
```
Authentication Type: None (Public API)
```

## Troubleshooting

### Connection Fails
- Verify the server URL is correct and accessible
- Check that the collection/layer ID exists
- Ensure your credentials are valid
- Look at browser console (F12) for errors

### No Data Appears
- Check that the collection has data
- Verify your user has read permissions
- Try with a different collection

### Slow Performance
- Large collections may take time to fetch
- Consider using filters in Honua metadata to limit results
- Use incremental refresh in Tableau

### HTTPS Required Error
If Tableau requires HTTPS:
1. Deploy connector to HTTPS server
2. Or configure Tableau to allow HTTP (not recommended for production)

## API Endpoints Used

### OGC Features API
```
GET {serverUrl}/ogc/features/v1/collections/{collectionId}/items
```

Parameters:
- `limit` - Number of items per page (default: 1000)
- Automatic pagination via `next` links

### STAC Catalog
```
GET {serverUrl}/stac/collections/{collectionId}/items
```

Parameters:
- `limit` - Number of items per page (default: 1000)
- Automatic pagination via `next` links

## Advanced Usage

### Custom Filters
To add custom filters (e.g., spatial, temporal), modify `connector.js`:

```javascript
// Add bbox filter
apiUrl += `&bbox=-180,-90,180,90`;

// Add datetime filter (STAC)
apiUrl += `&datetime=2023-01-01/2023-12-31`;
```

### Additional Collections
To fetch multiple collections, modify the schema in `connector.js` to create multiple tables.

### Property Flattening
For better performance, flatten common properties into separate columns by modifying `processFeature()`.

## Support

- **Documentation**: https://docs.honua.io
- **Issues**: https://github.com/HonuaIO/enterprise/issues
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
- Geometry centroid extraction
- WKT conversion
