# BI Connectors Deployment Guide

Complete step-by-step deployment guide for Honua BI integrations.

## Prerequisites

### General
- Honua Server v1.0+ running and accessible
- Server URL (e.g., `https://api.honua.io`)
- Valid credentials (API key, JWT token, or username/password)
- At least one collection configured with data

### For Tableau
- Tableau Desktop 2022.3 or later
- Web server for hosting (optional, for web deployment)
- OR ability to install .taco files (for packaged deployment)

### For Power BI
- Power BI Desktop (latest version)
- Admin rights to modify security settings
- For development: Visual Studio Code + Power Query SDK

---

## Deployment Steps

## 1. Tableau Web Data Connector

### Method A: Web Hosted (Recommended for Teams)

#### Step 1: Deploy to Web Server

```bash
# Using AWS S3 + CloudFront (recommended)
cd src/Honua.Server.Enterprise/BIConnectors/Tableau

# Sync to S3
aws s3 sync . s3://your-bucket/tableau-connector/ \
  --exclude "*.md" \
  --exclude "node_modules/*" \
  --exclude "package*.json"

# Create CloudFront distribution
# Point to S3 bucket
# Enable HTTPS
# Get distribution URL: https://d1234567890.cloudfront.net
```

```bash
# OR using traditional web server
scp -r * user@webserver:/var/www/html/tableau-connector/
```

#### Step 2: Configure CORS (if needed)

If hosting on separate domain from Honua:

```nginx
# nginx config
location /tableau-connector {
    add_header Access-Control-Allow-Origin "https://tableau.honua.io";
    add_header Access-Control-Allow-Methods "GET, POST, OPTIONS";
    add_header Access-Control-Allow-Headers "Content-Type, Authorization";
}
```

#### Step 3: Test Connection

1. Open browser to connector URL
2. Fill in test configuration:
   - Server URL: Your Honua server
   - Data Source: OGC Features
   - Collection: test-collection
   - Auth: None (or your method)
3. Click "Connect to Honua"
4. Verify data appears in browser console (F12)

#### Step 4: Distribute to Users

Share connector URL: `https://your-domain.com/tableau-connector/connector.html`

Users connect via:
1. Tableau Desktop → Connect → Web Data Connector
2. Enter connector URL
3. Configure connection
4. Done!

---

### Method B: Packaged .taco (Recommended for Enterprise)

#### Step 1: Install Tableau Connector SDK

```bash
npm install -g @tableau/taco-toolkit
```

#### Step 2: Package Connector

```bash
cd src/Honua.Server.Enterprise/BIConnectors/Tableau
taco pack manifest.json
```

This creates: `honua-ogc-features.taco`

#### Step 3: Distribute .taco File

```bash
# Copy to shared drive or send via email
# Recipients install to:
# Windows: C:\Users\[Username]\Documents\My Tableau Repository\Connectors
# Mac: /Users/[Username]/Documents/My Tableau Repository/Connectors
```

#### Step 4: User Installation

1. Download `honua-ogc-features.taco`
2. Copy to Tableau Connectors folder (create if doesn't exist)
3. Restart Tableau Desktop
4. Connector appears in native connector list under "Honua Geospatial Data"

---

## 2. Power BI Custom Connector

### Step 1: Build Connector (if not using pre-built)

```bash
cd src/Honua.Server.Enterprise/BIConnectors/PowerBI/Connector

# Open in VS Code with Power Query SDK extension
code .

# In VS Code:
# 1. Open Command Palette (Ctrl+Shift+P)
# 2. Run: Power Query: Set credential
# 3. Run: Power Query: Evaluate current file
# 4. Build creates .mez in bin/AnyCPU/Debug or Release
```

### Step 2: Distribute .mez File

```bash
# Copy .mez file to distribution location
cp bin/AnyCPU/Release/Honua.mez /path/to/distribution/

# Or create installer script
```

### Step 3: User Installation

Users perform these steps:

#### A. Create Custom Connectors Folder
```
Windows: C:\Users\[Username]\Documents\Power BI Desktop\Custom Connectors\
```

#### B. Copy .mez File
```bash
# Copy Honua.mez to Custom Connectors folder
```

#### C. Enable Custom Connectors

1. Open Power BI Desktop
2. Go to: **File** → **Options and settings** → **Options**
3. Navigate to: **Security** → **Data Extensions**
4. Select: **(Not Recommended) Allow any extension to load without validation or warning**
5. Click **OK**
6. **Restart Power BI Desktop**

#### D. Verify Installation

1. Click **Get Data** → **More...**
2. Search for "Honua"
3. Should see "Honua Geospatial Data"
4. Select and click **Connect**

### Step 4: First Connection

1. Enter connection parameters:
   - Server URL: `https://api.honua.io`
   - Data Source: `OGC Features`
   - Collection/Layer ID: `world-cities` (or leave blank)

2. Select authentication method
3. Click **Connect**
4. Data appears in Power Query Editor

---

## 3. Power BI Custom Visual (Kepler.gl)

### Step 1: Build Visual (if not using pre-built)

```bash
cd src/Honua.Server.Enterprise/BIConnectors/PowerBI/Visual

# Install dependencies
npm install

# Install development certificate (first time only)
pbiviz --install-cert

# Package visual
npm run package

# Creates: dist/HonuaKeplerMap.pbiviz
```

### Step 2A: Import into Single Report

For individual users or testing:

1. Open Power BI Desktop
2. Open or create a report
3. Go to **Visualizations** pane
4. Click **...** (more options)
5. Select **Import a visual from a file**
6. Browse to `HonuaKeplerMap.pbiviz`
7. Click **OK**
8. Visual appears in Visualizations pane

### Step 2B: Add to Organization Visuals

For enterprise-wide deployment:

#### Prerequisites
- Power BI Admin role
- Access to Power BI Admin Portal

#### Steps

1. Package visual (as in Step 1)

2. Upload to tenant:
   - Go to: https://app.powerbi.com
   - Click ⚙️ (Settings) → **Admin portal**
   - Navigate to: **Tenant settings** → **Organization visuals**
   - Click **Add visual**
   - Select **Upload a .pbiviz file**
   - Browse to `HonuaKeplerMap.pbiviz`
   - Fill in details:
     - Display name: Honua Kepler.gl Map
     - Description: Advanced geospatial visualization
     - Icon: Upload icon.png
   - Click **Add**

3. Enable for organization:
   - In **Organization visuals** list
   - Toggle **Enabled** to ON

4. Users see visual:
   - Visualizations pane → **...** → **Get more visuals**
   - Tab: **My organization**
   - See "Honua Kepler.gl Map"
   - Click **Add**

### Step 2C: Publish to AppSource (Public)

For public distribution:

1. Meet prerequisites:
   - Microsoft Partner Network account
   - Code signing certificate
   - Terms acceptance

2. Submit to Microsoft:
   - Go to: https://appsource.microsoft.com/partners
   - Click **Publish an app**
   - Select **Power BI visual**
   - Upload `HonuaKeplerMap.pbiviz`
   - Fill in listing details
   - Submit for review

3. Microsoft reviews (2-4 weeks)

4. Once approved:
   - Available globally in AppSource
   - Users find in "From AppSource" tab

---

## 4. Configuration & Testing

### A. Honua Server Configuration

Ensure these endpoints are accessible:

```bash
# Test OGC Features API
curl https://api.honua.io/ogc/features/v1/collections

# Test STAC API
curl https://api.honua.io/stac/collections

# Test authentication
curl -H "Authorization: Bearer YOUR_TOKEN" \
  https://api.honua.io/ogc/features/v1/collections
```

### B. CORS Configuration

If BI tools run on different domain:

```json
// appsettings.json
{
  "honua": {
    "cors": {
      "allowAnyOrigin": false,
      "allowedOrigins": [
        "https://tableau.honua.io",
        "https://powerbi.com",
        "https://app.powerbi.com"
      ],
      "allowCredentials": true
    }
  }
}
```

### C. Rate Limiting

Configure appropriate limits:

```json
{
  "RateLimiting": {
    "Enabled": true,
    "DefaultRequestsPerMinute": 100,
    "EndpointLimits": {
      "/ogc": {
        "RequestsPerMinute": 200,
        "BurstSize": 50
      },
      "/stac": {
        "RequestsPerMinute": 200,
        "BurstSize": 50
      }
    }
  }
}
```

### D. Monitoring

Enable detailed logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Honua.Server.Host.Ogc": "Debug",
      "Honua.Server.Host.Stac": "Debug"
    }
  },
  "observability": {
    "metrics": {
      "enabled": true
    }
  }
}
```

---

## 5. User Training

### Training Materials

Create these materials for users:

#### 1. Quick Start Guide (1-page PDF)
- How to install connectors
- How to connect to Honua
- Basic visualization examples

#### 2. Video Tutorials (5-10 minutes each)
- Installing Tableau connector
- Installing Power BI connector + visual
- Creating first visualization
- Advanced features

#### 3. Sample Reports
- Tableau workbook (.twbx)
- Power BI template (.pbit)
- Pre-configured with example data

### Training Sessions

Schedule hands-on training:

**Session 1: Installation (30 min)**
- Installing connectors
- Configuring authentication
- First connection

**Session 2: Basic Visualizations (1 hour)**
- Point maps
- Property filtering
- Time-based analysis

**Session 3: Advanced Features (1 hour)**
- Kepler.gl visual features
- 3D visualization
- Temporal animation
- Cross-filtering

---

## 6. Rollout Strategy

### Phase 1: Pilot (2 weeks)
- Select 5-10 power users
- Install all components
- Gather feedback
- Fix issues

### Phase 2: Department Rollout (1 month)
- Deploy to specific department
- Provide training
- Create sample reports
- Document use cases

### Phase 3: Organization-Wide (2 months)
- Deploy to all users
- Self-service installation
- Help desk support
- Regular office hours

---

## 7. Maintenance

### Monthly Tasks
- Check for connector updates
- Review usage metrics
- Update documentation
- Address user questions

### Quarterly Tasks
- Security review
- Performance optimization
- Feature enhancement
- User survey

### Annually
- Major version upgrade
- Comprehensive training refresh
- Architecture review

---

## 8. Troubleshooting

### Issue: Tableau connector not loading

**Symptoms:** Blank page when accessing connector URL

**Solutions:**
1. Check browser console for errors (F12)
2. Verify web server is serving files correctly
3. Check CORS headers
4. Test with simple HTML page first

---

### Issue: Power BI connector not appearing

**Symptoms:** Can't find "Honua" in Get Data dialog

**Solutions:**
1. Verify .mez file is in correct folder
2. Check security settings allow custom connectors
3. Restart Power BI Desktop
4. Check .mez file isn't corrupted (re-download)

---

### Issue: Authentication fails

**Symptoms:** 401 Unauthorized errors

**Solutions:**
1. Verify credentials are correct
2. Check token hasn't expired
3. Test authentication with curl first
4. Verify user has permissions

---

### Issue: No data appears

**Symptoms:** Empty table or "No rows"

**Solutions:**
1. Verify collection ID is correct and spelled correctly
2. Check collection has data (test with curl)
3. Review any filters applied
4. Check user permissions for collection

---

### Issue: Slow performance

**Symptoms:** Long load times, timeouts

**Solutions:**
1. Add filters to reduce data volume
2. Check network latency
3. Verify Honua server performance
4. Consider caching on server side
5. Use incremental refresh (Power BI)

---

## 9. Security Checklist

Before deployment, verify:

- [ ] HTTPS enabled on Honua server
- [ ] SSL certificates valid
- [ ] Authentication configured
- [ ] User permissions set correctly
- [ ] Rate limiting enabled
- [ ] CORS configured appropriately
- [ ] Credentials stored securely
- [ ] Audit logging enabled
- [ ] Firewall rules configured
- [ ] API keys rotated regularly

---

## 10. Success Metrics

Track these KPIs:

- **Adoption**: # of users using connectors
- **Usage**: # of connections per day
- **Performance**: Average response time
- **Errors**: Error rate < 1%
- **Satisfaction**: User survey scores > 4/5
- **Support**: # of support tickets
- **Business Impact**: # of reports created

---

## Support

**Installation Issues:**
- Email: support@honua.io
- Slack: #bi-connectors

**Usage Questions:**
- Documentation: https://docs.honua.io/bi-connectors
- Community: https://community.honua.io

**Enterprise Support:**
- Email: enterprise@honua.io
- Phone: +1-XXX-XXX-XXXX
- SLA: 4-hour response time

---

## Appendix: Automated Deployment Scripts

### A. Tableau WDC Deployment Script

```bash
#!/bin/bash
# deploy-tableau-wdc.sh

# Configuration
S3_BUCKET="your-bucket"
CLOUDFRONT_ID="E1234567890ABC"

# Deploy to S3
echo "Deploying Tableau WDC to S3..."
cd src/Honua.Server.Enterprise/BIConnectors/Tableau
aws s3 sync . s3://${S3_BUCKET}/tableau-connector/ \
  --exclude "*.md" \
  --exclude "node_modules/*" \
  --delete

# Invalidate CloudFront cache
echo "Invalidating CloudFront cache..."
aws cloudfront create-invalidation \
  --distribution-id ${CLOUDFRONT_ID} \
  --paths "/tableau-connector/*"

echo "Deployment complete!"
echo "Connector URL: https://cdn.yourdomain.com/tableau-connector/connector.html"
```

### B. Power BI Connector Distribution Script

```bash
#!/bin/bash
# distribute-powerbi-connector.sh

# Build connector
echo "Building Power BI connector..."
cd src/Honua.Server.Enterprise/BIConnectors/PowerBI/Connector
# Build using Power Query SDK
msbuild Honua.mproj /t:BuildExtension /p:Configuration=Release

# Copy to distribution folder
echo "Copying to distribution..."
mkdir -p dist
cp bin/AnyCPU/Release/Honua.mez dist/

# Create installation package
echo "Creating installation package..."
cd dist
zip Honua-PowerBI-Connector.zip Honua.mez ../README.md

echo "Distribution package ready: dist/Honua-PowerBI-Connector.zip"
```

### C. Power BI Visual Deployment Script

```bash
#!/bin/bash
# build-powerbi-visual.sh

# Build visual
echo "Building Power BI visual..."
cd src/Honua.Server.Enterprise/BIConnectors/PowerBI/Visual
npm install
pbiviz package

echo "Visual built: dist/HonuaKeplerMap.pbiviz"
echo "Upload to Power BI Admin Portal for organization-wide distribution"
```

---

**Deployment guide version:** 1.0.0
**Last updated:** February 2025
**Maintained by:** HonuaIO Enterprise Team
