# Honua BI Connectors - Enterprise Features

Complete Business Intelligence integration suite for Honua Geospatial Server.

## Overview

This module provides enterprise-grade BI tool integrations for Honua, enabling seamless connectivity and visualization of geospatial data in popular BI platforms.

## Components

### 1. Tableau Web Data Connector 3.0 📊
**Path:** `Tableau/`

Connect Tableau to Honua's OGC Features API and STAC catalogs.

**Features:**
- Web-based connector (JavaScript)
- OGC Features API support
- STAC Catalog support
- Multiple authentication methods
- Automatic pagination
- WKT geometry conversion

**Deployment:**
- Host on web server
- Or package as .taco file for native integration

**Docs:** [Tableau/README.md](./Tableau/README.md)

---

### 2. Power BI Custom Connector 🔌
**Path:** `PowerBI/Connector/`

Power Query M-based connector for Power BI Desktop and Service.

**Features:**
- Native Power BI integration
- OGC Features API support
- STAC Catalog support
- Property flattening
- Query folding
- Incremental refresh
- Multiple authentication methods

**Deployment:**
- Install .mez file to Custom Connectors folder
- Enable in Power BI security settings

**Docs:** [PowerBI/Connector/README.md](./PowerBI/Connector/README.md)

---

### 3. Power BI Custom Visual (Kepler.gl) 🗺️
**Path:** `PowerBI/Visual/`

Advanced geospatial visualization using Uber's Kepler.gl.

**Features:**
- 7 layer types (Point, Hexagon, Arc, Line, GeoJSON, Heatmap, Cluster)
- 4 map styles (Dark, Light, Muted, Satellite)
- 3D visualization with elevation
- Temporal animation
- Interactive filtering
- Cross-visual filtering
- Drill-down support

**Deployment:**
- Import .pbiviz file
- Or publish to AppSource

**Docs:** [PowerBI/Visual/README.md](./PowerBI/Visual/README.md)

---

## Quick Start

### For Tableau Users

```bash
# 1. Deploy connector to web server
cd Tableau/
python -m http.server 8000

# 2. In Tableau Desktop
# - Connect → Web Data Connector
# - URL: http://localhost:8000/connector.html
# - Configure and connect
```

### For Power BI Users

```bash
# 1. Install Connector
# Copy PowerBI/Connector/bin/Honua.mez to:
# C:\Users\[Username]\Documents\Power BI Desktop\Custom Connectors\

# 2. Import Visual
# Copy PowerBI/Visual/dist/HonuaKeplerMap.pbiviz
# In Power BI: Visualizations → Import from file

# 3. Enable custom extensions
# Options → Security → Data Extensions → Allow any extension
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Honua Server                            │
│  ┌────────────────┐  ┌────────────────┐  ┌───────────────┐ │
│  │  OGC Features  │  │  STAC Catalog  │  │  Other APIs   │ │
│  │      API       │  │                │  │               │ │
│  └────────┬───────┘  └────────┬───────┘  └───────┬───────┘ │
└───────────┼──────────────────┼──────────────────┼──────────┘
            │                  │                  │
            │ HTTPS            │ HTTPS            │ HTTPS
            │                  │                  │
    ┌───────▼──────────────────▼──────────────────▼───────┐
    │              Authentication Layer                    │
    │   (Bearer Token, API Key, Basic Auth, OAuth 2.0)    │
    └────────┬─────────────────────────────────────────────┘
             │
    ┌────────▼──────────────────┬──────────────────────────┐
    │                           │                          │
┌───▼───────────┐    ┌─────────▼──────────┐    ┌─────────▼──────────┐
│   Tableau     │    │  Power BI Connector│    │  Power BI Visual   │
│ Web Connector │    │  (Data Source)     │    │  (Visualization)   │
├───────────────┤    ├────────────────────┤    ├────────────────────┤
│ connector.js  │    │   Honua.pq (M)     │    │  visual.ts (React) │
│ connector.html│    │   Query Folding    │    │  Kepler.gl         │
│ Auto-Pagination│    │   Incremental ↻    │    │  7 Layer Types     │
└───────────────┘    └────────────────────┘    └────────────────────┘
```

---

## Use Cases

### 1. Executive Dashboard (Power BI)
**Components:** Connector + Visual

**Scenario:** C-suite wants to see real-time asset locations globally

**Implementation:**
1. Use Power BI Connector to import location data from OGC Features
2. Add Honua Kepler.gl Visual to show assets on 3D globe
3. Add slicers for filtering by region, asset type, status
4. Enable drill-down from region → country → city
5. Publish to Power BI Service for sharing

**Benefits:**
- Interactive 3D visualization
- Real-time updates via scheduled refresh
- Mobile-friendly
- Role-based access control

---

### 2. Satellite Imagery Analysis (Power BI + STAC)
**Components:** Connector + Visual

**Scenario:** Analyze satellite coverage and cloud cover trends

**Implementation:**
1. Connect to Honua STAC catalog (Sentinel-2)
2. Load items with `DateTime`, `eo:cloud_cover`, location
3. Use Kepler.gl Visual with temporal animation
4. Create line chart showing cloud cover trends
5. Add map showing recent acquisitions

**Benefits:**
- Visualize millions of satellite scenes
- Temporal playback
- Identify coverage gaps
- Filter by cloud cover threshold

---

### 3. Sales Territory Analysis (Tableau)
**Components:** Tableau WDC

**Scenario:** Visualize sales by geographic territory

**Implementation:**
1. Load territory polygons from OGC Features
2. Join with sales data from database
3. Create filled map in Tableau
4. Add tooltips showing metrics
5. Create dashboard with filters

**Benefits:**
- Native Tableau experience
- Combine geospatial and business data
- Advanced filtering and calculations
- Publish to Tableau Server

---

### 4. IoT Sensor Network (Power BI)
**Components:** Connector + Visual

**Scenario:** Monitor thousands of IoT sensors in real-time

**Implementation:**
1. Sensors send data to Honua OGC Features API
2. Power BI Connector loads sensor locations and latest readings
3. Kepler.gl Visual shows sensors as hexagon heatmap
4. Color by temperature, size by activity
5. Set up auto-refresh every 5 minutes

**Benefits:**
- Handle 100K+ sensors
- Hexagon aggregation for performance
- Identify hotspots and anomalies
- Drill into individual sensors

---

### 5. Historical Trend Analysis (Tableau + STAC)
**Components:** Tableau WDC

**Scenario:** Analyze environmental changes over 10 years

**Implementation:**
1. Connect Tableau to Honua STAC (Landsat)
2. Load items for area of interest
3. Extract temporal and spectral properties
4. Create timeline showing NDVI trends
5. Add map showing extent

**Benefits:**
- Long-term trend analysis
- Multiple spectral bands
- Temporal filtering
- Scientific visualization

---

## Comparison Matrix

| Feature | Tableau WDC | Power BI Connector | Power BI Visual |
|---------|-------------|-------------------|-----------------|
| **Primary Use** | Data Import | Data Import | Visualization |
| **Data Source** | OGC, STAC | OGC, STAC | Any (via PBI dataset) |
| **Max Rows** | ~10M | ~1M (Import) | ~100K (optimal) |
| **Refresh** | Extract/Live | Scheduled/Incremental | Auto (follows dataset) |
| **Authentication** | ✅ 4 types | ✅ 4 types | N/A (uses dataset) |
| **Pagination** | ✅ Auto | ✅ Auto | N/A |
| **Point Data** | ✅ | ✅ | ✅ |
| **Polygon Data** | ✅ (as WKT) | ✅ (as JSON) | ✅ (as GeoJSON) |
| **3D Visualization** | ❌ | ❌ | ✅ |
| **Temporal Animation** | ❌ | ❌ | ✅ |
| **Hexagon Aggregation** | ❌ | ❌ | ✅ |
| **Deployment** | Web Server | .mez file | .pbiviz file |
| **Enterprise Ready** | ✅ | ✅ | ✅ |

---

## Authentication

All components support multiple authentication methods:

### 1. None (Public APIs)
No credentials required. Use for public datasets.

### 2. Bearer Token (JWT)
Most common for enterprise. Token passed in `Authorization: Bearer` header.

**Example:**
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 3. API Key
Simple key-based auth. Passed in `X-API-Key` header.

**Example:**
```
X-API-Key: sk_live_abc123def456ghi789jkl
```

### 4. Basic Auth
Username and password. Base64 encoded in `Authorization` header.

**Example:**
```
Authorization: Basic dXNlcm5hbWU6cGFzc3dvcmQ=
```

### 5. OAuth 2.0 (Power BI Connector only)
Full OAuth flow with automatic token refresh.

---

## Performance Guidelines

### Data Volume

| Rows | Tableau WDC | Power BI Connector | Power BI Visual |
|------|-------------|-------------------|-----------------|
| < 10K | ⚡ Instant | ⚡ Instant | ⚡ Instant |
| 10K - 100K | ✅ Fast | ✅ Fast | ✅ Fast |
| 100K - 1M | ⚠️ Moderate | ⚠️ Moderate | ⚠️ Use Hexagon |
| 1M - 10M | ⚠️ Slow | ❌ Limit reached | ❌ Not recommended |
| > 10M | ⚠️ Extract only | ❌ Use filters | ❌ Not recommended |

### Best Practices

1. **Filter Early**: Apply filters at data source level
2. **Select Columns**: Only load needed columns
3. **Aggregate**: Use Hexagon/Cluster for dense data
4. **Incremental Refresh**: For time-series data
5. **Cache**: Enable caching on Honua server
6. **Simplify Geometries**: Reduce polygon complexity

---

## Security Considerations

### 1. Credentials
- Store credentials securely (Tableau: Extract, Power BI: Credential Manager)
- Never commit credentials to source control
- Use environment variables for tokens
- Rotate keys regularly

### 2. HTTPS
- Always use HTTPS in production
- Validate SSL certificates
- Don't disable certificate validation

### 3. Network
- Whitelist BI tool IPs if using firewall
- Use VPN for internal Honua servers
- Consider API gateway for additional security layer

### 4. Authorization
- Use least-privilege principle
- Grant read-only access to BI users
- Use row-level security for sensitive data
- Audit connector usage

---

## Deployment Guide

### Development Environment

```bash
# Clone repository
git clone https://github.com/HonuaIO/honua-server.git
cd src/Honua.Server.Enterprise/BIConnectors

# Tableau WDC
cd Tableau
npm install
npm run serve

# Power BI Connector
cd PowerBI/Connector
# Open in VS Code with Power Query SDK
code .

# Power BI Visual
cd PowerBI/Visual
npm install
pbiviz --install-cert
npm start
```

### Testing Environment

```bash
# Tableau WDC
# Deploy to test web server
scp -r Tableau/* user@test-server:/var/www/html/tableau-connector/

# Power BI Connector
# Copy .mez to test machine
# Test with sample data

# Power BI Visual
# Package and test locally
pbiviz package
# Import into Power BI Desktop
```

### Production Deployment

#### Tableau WDC

**Option A: Web Server**
```bash
# Deploy to production web server (CDN recommended)
aws s3 cp Tableau/ s3://cdn.yourdomain.com/tableau-connector/ --recursive
# Users connect via: https://cdn.yourdomain.com/tableau-connector/connector.html
```

**Option B: .taco Package**
```bash
# Package connector
taco pack manifest.json

# Distribute .taco file
# Users install to: My Tableau Repository/Connectors/
```

#### Power BI Connector

```bash
# Build release .mez
# Distribute to users via company portal or email
# Users copy to: Documents/Power BI Desktop/Custom Connectors/

# For Power BI Service:
# Contact Microsoft to certify connector for organization
# Or use Gateway in personal mode
```

#### Power BI Visual

**Option A: Organization Distribution**
```bash
# Package visual
pbiviz package

# Upload to organization's Power BI tenant
# Admin approves for organization use
# Users see in "My organization" visuals
```

**Option B: AppSource (Public)**
```bash
# Submit to AppSource
# Microsoft reviews and approves
# Available globally in Power BI
```

---

## Monitoring & Maintenance

### Logging

Enable logging on Honua server:
```json
{
  "Logging": {
    "LogLevel": {
      "Honua.Server.Host.Ogc": "Information",
      "Honua.Server.Host.Stac": "Information"
    }
  }
}
```

### Metrics

Track these metrics:
- API requests from BI tools
- Authentication failures
- Average response time
- Data volume transferred
- Error rates

### Updates

**Tableau WDC:**
- Update version in `manifest.json`
- Re-deploy to web server
- Users refresh connector

**Power BI Connector:**
- Update version in `.pq` file
- Rebuild `.mez`
- Distribute new file
- Users replace old version

**Power BI Visual:**
- Update version in `pbiviz.json`
- Rebuild `.pbiviz`
- Upload to org visuals or AppSource
- Users get auto-update

---

## Troubleshooting

### Common Issues

#### Connection Timeouts
**Cause:** Large dataset or slow network
**Solution:** Add filters, increase timeout, use caching

#### Authentication Errors
**Cause:** Invalid/expired credentials
**Solution:** Refresh credentials, verify token validity

#### Empty Results
**Cause:** Incorrect collection ID, missing permissions
**Solution:** Verify collection exists, check user permissions

#### Performance Issues
**Cause:** Too much data, complex geometries
**Solution:** Apply filters, simplify geometries, use aggregation

### Support Channels

- **GitHub Issues**: https://github.com/HonuaIO/honua-server/issues
- **Community Forum**: https://community.honua.io
- **Email**: support@honua.io
- **Enterprise Support**: enterprise@honua.io

---

## License

MIT License - See LICENSE file for details

**Enterprise License Available:**
- Priority support
- Custom development
- SLA guarantees
- Professional services

Contact: enterprise@honua.io

---

## Roadmap

### Q2 2025
- ✅ Tableau Web Data Connector 3.0
- ✅ Power BI Custom Connector
- ✅ Power BI Kepler.gl Visual
- ⏳ Qlik Sense Extension
- ⏳ Looker Studio Connector

### Q3 2025
- ⏳ Excel Add-in
- ⏳ Google Sheets Add-on
- ⏳ Jupyter Notebook Integration
- ⏳ R/RStudio Connector

### Q4 2025
- ⏳ ArcGIS Pro Add-in
- ⏳ QGIS Plugin
- ⏳ Python SDK
- ⏳ REST API v2 with GraphQL

---

## Contributing

We welcome contributions! See [CONTRIBUTING.md](../../../CONTRIBUTING.md)

**Areas for Contribution:**
- Additional BI tool connectors
- Enhanced visualizations
- Performance optimizations
- Documentation improvements
- Bug fixes

---

## Acknowledgments

- **Tableau** - For Web Data Connector SDK
- **Microsoft** - For Power BI Visuals API and Power Query SDK
- **Uber** - For Kepler.gl visualization library
- **Honua Community** - For feedback and testing

---

## Version

**Current Version:** 1.0.0 (February 2025)

**Compatibility:**
- Honua Server: v1.0+
- Tableau Desktop: 2022.3+
- Power BI Desktop: Latest
- OGC Features API: v1.0
- STAC API: v1.0.0+

---

## Next Steps

1. **Choose Your Tool**: Tableau or Power BI?
2. **Install Components**: Follow installation guides
3. **Connect to Honua**: Configure your server URL
4. **Build Visualizations**: Start with simple point maps
5. **Share Insights**: Publish to Tableau Server or Power BI Service

**Need Help?** Start with the Quick Start guides in each component's README.

---

**Built with ❤️ by HonuaIO**
