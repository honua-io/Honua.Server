# Per-Service API Configuration

Honua supports fine-grained control over which API protocols are enabled for each service. This allows you to:
- Expose different services through different protocols
- Reduce attack surface by disabling unused APIs
- Optimize performance by only enabling necessary protocols

## Architecture

### Two-Level Control

API access is controlled at **two levels**:

1. **Global Level** (`appsettings.json`)
   - Controls whether an API is available server-wide
   - Acts as a master switch
   - Located in `honua:services` configuration

2. **Service Level** (`metadata.json`)
   - Controls whether an API is enabled for a specific service
   - Only effective if globally enabled
   - Located in `services[].ogc` metadata block

### Validation Rules

- ✅ **Allowed**: Global enabled + Service enabled
- ✅ **Allowed**: Global enabled + Service disabled (service opts out)
- ❌ **Blocked**: Global disabled + Service enabled (validation error at startup)
- ✅ **Allowed**: Global disabled + Service disabled

## Global Configuration

In `appsettings.json` or `appsettings.Production.json`:

```json
{
  "honua": {
    "services": {
      "wfs": { "enabled": true },
      "wms": { "enabled": true },
      "wmts": { "enabled": true },
      "csw": { "enabled": true },
      "wcs": { "enabled": true }
    }
  }
}
```

**Note**: OGC API Features (Collections) doesn't have a global disable - it's always available.

## Service-Level Configuration

In `metadata.json`:

```json
{
  "services": [
    {
      "id": "parcels",
      "title": "Parcels Service",
      "serviceType": "FeatureServer",
      "dataSourceId": "main-db",
      "folderId": "cadastral",
      "ogc": {
        "collectionsEnabled": true,
        "wfsEnabled": true,
        "wmsEnabled": true,
        "wmtsEnabled": false,
        "cswEnabled": true,
        "wcsEnabled": false
      }
    }
  ]
}
```

### API Flags

| Flag | Description | Default | Applies To |
|------|-------------|---------|------------|
| `collectionsEnabled` | Enable OGC API Features | `false` | Vector services |
| `wfsEnabled` | Enable WFS 2.0 | `false` | Vector services |
| `wmsEnabled` | Enable WMS 1.3.0 | `false` | Vector services |
| `wmtsEnabled` | Enable WMTS | `false` | Vector services |
| `cswEnabled` | Enable CSW 2.0.2 | `false` | Vector services |
| `wcsEnabled` | Enable WCS 2.0 | `false` | Raster services |

**Important**: All flags default to `false` - you must explicitly enable each API per service.

## Examples

### Example 1: Public Read-Only Service

```json
{
  "id": "public-parcels",
  "title": "Public Parcels",
  "serviceType": "FeatureServer",
  "dataSourceId": "public-db",
  "folderId": "public",
  "ogc": {
    "collectionsEnabled": true,
    "wfsEnabled": false,
    "wmsEnabled": true,
    "wmtsEnabled": true,
    "cswEnabled": true,
    "wcsEnabled": false
  }
}
```
**Result**: Accessible via OGC API Features, WMS, WMTS, and CSW. WFS is disabled (no editing).

### Example 2: Internal Editing Service

```json
{
  "id": "internal-parcels",
  "title": "Internal Parcels (Editing)",
  "serviceType": "FeatureServer",
  "dataSourceId": "internal-db",
  "folderId": "internal",
  "ogc": {
    "collectionsEnabled": true,
    "wfsEnabled": true,
    "wmsEnabled": false,
    "wmtsEnabled": false,
    "cswEnabled": false,
    "wcsEnabled": false
  }
}
```
**Result**: Accessible via OGC API Features and WFS (editing). No WMS/WMTS (no rendering). No CSW (not in catalog).

### Example 3: Raster Service

```json
{
  "id": "elevation-dem",
  "title": "Elevation DEM",
  "serviceType": "ImageServer",
  "dataSourceId": "elevation-raster",
  "folderId": "elevation",
  "ogc": {
    "collectionsEnabled": false,
    "wfsEnabled": false,
    "wmsEnabled": true,
    "wmtsEnabled": true,
    "cswEnabled": true,
    "wcsEnabled": true
  }
}
```
**Result**: Accessible via WMS, WMTS, CSW, and WCS for raster data access.

### Example 4: Catalog-Only Service

```json
{
  "id": "metadata-only",
  "title": "Metadata Repository",
  "serviceType": "FeatureServer",
  "dataSourceId": "catalog-db",
  "folderId": "metadata",
  "ogc": {
    "collectionsEnabled": false,
    "wfsEnabled": false,
    "wmsEnabled": false,
    "wmtsEnabled": false,
    "cswEnabled": true,
    "wcsEnabled": false
  }
}
```
**Result**: Only accessible via CSW for metadata discovery.

## Validation

### Startup Validation

Honua validates service API configuration at startup via the `ServiceApiValidationHostedService`:

```
[ERROR] Service 'parcels': WFS is enabled at service level but disabled globally.
Either enable WFS globally (honua:services:wfs:enabled) or disable it for this service (ogc.wfsEnabled: false).
```

### Validation Logic

```csharp
// Check if an API is enabled for a service
bool isEnabled = ServiceApiConfigurationValidator.IsApiEnabled(
    service,
    "wfs",
    globalConfig.Services
);
```

The validator ensures:
1. Service-level flags don't violate global settings
2. Clear error messages guide configuration fixes
3. Validation happens before any requests are processed

## Migration from Legacy Systems

### From Existing Honua (Pre-Per-Service API)

**Before** (all APIs always enabled):
```json
{
  "services": [
    {
      "id": "parcels",
      "enabled": true
    }
  ]
}
```

**After** (explicit opt-in):
```json
{
  "services": [
    {
      "id": "parcels",
      "enabled": true,
      "ogc": {
        "collectionsEnabled": true,
        "wfsEnabled": true,
        "wmsEnabled": true
      }
    }
  ]
}
```

### From ArcGIS Server

ArcGIS Server uses capabilities strings - map these to Honua flags:

| ArcGIS Capability | Honua Flag |
|-------------------|------------|
| `Query` | `collectionsEnabled: true` |
| `Create,Update,Delete` | `wfsEnabled: true` |
| Not applicable | `wmsEnabled: true` |
| Not applicable | `wmtsEnabled: true` |

## Security Considerations

1. **Principle of Least Privilege**: Only enable APIs that are actively used
2. **Attack Surface Reduction**: Disabled APIs cannot be exploited
3. **Audit Trail**: Service-level flags are part of metadata versioning
4. **Defense in Depth**: Global flags provide server-wide controls

## Troubleshooting

### Service Not Accessible

**Symptom**: 404 or service not found

**Check**:
1. Is the service enabled? (`"enabled": true`)
2. Is the API globally enabled? (check `appsettings.json`)
3. Is the API enabled for this service? (check `ogc.*Enabled`)

### Validation Error at Startup

**Symptom**: Server fails to start with API configuration error

**Solution**:
1. Read the error message - it tells you which service and API
2. Either enable the API globally OR disable it for that service
3. Never try to enable at service level when globally disabled

### API Returns Empty Results

**Symptom**: Service accessible but no data

**This is NOT an API configuration issue** - check:
1. Data source connection
2. Layer configuration
3. CRS/projection settings
4. Query filters

## Related Documentation

- [Metadata Schema Reference](./metadata/METADATA_SCHEMA.md)
- [Security Configuration](../SECURITY.md)
- [API Protocol Documentation](./protocols/)
