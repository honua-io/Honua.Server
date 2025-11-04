# Runtime Configuration API

## Overview

Honua Server provides a comprehensive runtime configuration API for controlling service availability without server restarts. This allows administrators to dynamically enable/disable API protocols at both the **global level** (master kill switch) and **service level** (per-service opt-in).

## Two-Level Control Architecture

### Global Level (Master Kill Switch)
- Configured in `appsettings.json` at startup
- **Can be toggled at runtime** via API
- When disabled, blocks ALL services from using that protocol
- Takes precedence over service-level settings

### Service Level (Per-Service Opt-in)
- Configured in `metadata.json` per service
- **Can be toggled at runtime** via API
- Only effective if global setting is enabled
- Defaults to `false` (explicit opt-in required)

### Effective State
A protocol is **active** for a service only when:
```
effective = serviceLevel.enabled AND globalLevel.enabled
```

Example:
- Global WFS: `enabled`
- Service A WFS: `enabled` → **WFS active for Service A** ✓
- Service B WFS: `disabled` → **WFS blocked for Service B** ✗
- If Global WFS is changed to `disabled` → **ALL services blocked** ✗✗

---

## API Endpoints

All endpoints require `RequireAdministrator` authorization (except in QuickStart mode).

Base path: `/admin/config`

### 1. Get Overall Configuration Status

**GET** `/admin/config/status`

Returns comprehensive view of global and service-level states with effective status.

#### Response

```json
{
  "global": {
    "wfs": true,
    "wms": true,
    "wmts": true,
    "csw": true,
    "wcs": true,
    "stac": true,
    "geometry": true,
    "rasterTiles": true,
    "note": "Global settings are configured in appsettings.json and are read-only at runtime."
  },
  "services": [
    {
      "serviceId": "demographics",
      "apis": {
        "collections": {
          "serviceLevel": true,
          "globalLevel": true,
          "effective": true
        },
        "wfs": {
          "serviceLevel": true,
          "globalLevel": true,
          "effective": true
        },
        "wms": {
          "serviceLevel": false,
          "globalLevel": true,
          "effective": false
        }
      }
    }
  ]
}
```

---

### 2. Get Global Service States

**GET** `/admin/config/services`

Lists all global protocol states.

#### Response

```json
{
  "wfs": { "enabled": true },
  "wms": { "enabled": true },
  "wmts": { "enabled": true },
  "csw": { "enabled": true },
  "wcs": { "enabled": true },
  "stac": { "enabled": true },
  "geometry": { "enabled": true },
  "rasterTiles": { "enabled": true },
  "note": "These global settings can be toggled at runtime. When disabled, the protocol is disabled for ALL services regardless of service-level settings."
}
```

---

### 3. Toggle Global Protocol (Master Kill Switch)

**PATCH** `/admin/config/services/{protocol}`

Enable or disable a protocol globally. This cascades to ALL services.

#### Parameters

- **Path**: `protocol` - One of: `wfs`, `wms`, `wmts`, `csw`, `wcs`, `stac`, `geometry`, `rasterTiles`

#### Request Body

```json
{
  "enabled": false
}
```

#### Response (Disabling)

```json
{
  "status": "updated",
  "protocol": "wfs",
  "enabled": false,
  "message": "WFS globally disabled. This will block 3 service(s) that have wfs enabled at service-level.",
  "affectedServices": ["demographics", "infrastructure", "parcels"],
  "note": "The protocol is now disabled globally. Service-level flags remain unchanged but are overridden by this global setting."
}
```

#### Response (Enabling)

```json
{
  "status": "updated",
  "protocol": "wfs",
  "enabled": true,
  "message": "WFS globally enabled.",
  "note": "Services with this protocol enabled at service-level can now serve requests."
}
```

---

### 4. Get Service-Level API Configuration

**GET** `/admin/config/services/{serviceId}`

Get API configuration for a specific service.

#### Response

```json
{
  "serviceId": "demographics",
  "apis": {
    "collections": {
      "enabled": true,
      "effective": true,
      "note": "Collections (OGC API Features) don't have a global toggle"
    },
    "wfs": {
      "enabled": true,
      "globalEnabled": true,
      "effective": true
    },
    "wms": {
      "enabled": false,
      "globalEnabled": true,
      "effective": false
    },
    "wmts": {
      "enabled": false,
      "globalEnabled": true,
      "effective": false
    },
    "csw": {
      "enabled": true,
      "globalEnabled": true,
      "effective": true
    },
    "wcs": {
      "enabled": false,
      "globalEnabled": true,
      "effective": false
    }
  }
}
```

---

### 5. Toggle Service-Level Protocol

**PATCH** `/admin/config/services/{serviceId}/{protocol}`

Enable or disable a protocol for a specific service.

#### Parameters

- **Path**: `serviceId` - Service identifier
- **Path**: `protocol` - One of: `collections`, `wfs`, `wms`, `wmts`, `csw`, `wcs`

#### Request Body

```json
{
  "enabled": true
}
```

#### Response (Success)

```json
{
  "status": "updated",
  "serviceId": "demographics",
  "protocol": "wfs",
  "enabled": true,
  "message": "WFS enabled for service 'demographics'.",
  "note": "This change is in-memory only. To persist, update metadata.json and reload or restart."
}
```

#### Response (Error - Global Disabled)

```json
{
  "error": "Cannot enable wfs for service 'demographics'. WFS is disabled globally in appsettings.json.",
  "details": [
    "Service 'demographics': WFS is enabled at service level but disabled globally..."
  ],
  "note": "To enable this protocol, update appsettings.json (honua:services:wfs:enabled: true) and restart the server."
}
```

---

## Usage Examples

### Example 1: Emergency Shutdown of WFS Across All Services

```bash
# Disable WFS globally (blocks all services)
curl -X PATCH http://localhost:5000/admin/config/services/wfs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"enabled": false}'
```

Response:
```json
{
  "status": "updated",
  "protocol": "wfs",
  "enabled": false,
  "message": "WFS globally disabled. This will block 15 service(s) that have wfs enabled at service-level.",
  "affectedServices": ["demographics", "parcels", "infrastructure", ...],
  "note": "The protocol is now disabled globally. Service-level flags remain unchanged but are overridden by this global setting."
}
```

### Example 2: Enable WMS for a Single Service

```bash
# Enable WMS for the 'imagery' service
curl -X PATCH http://localhost:5000/admin/config/services/imagery/wms \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'
```

Response:
```json
{
  "status": "updated",
  "serviceId": "imagery",
  "protocol": "wms",
  "enabled": true,
  "message": "WMS enabled for service 'imagery'.",
  "note": "This change is in-memory only. To persist, update metadata.json and reload or restart."
}
```

### Example 3: Check Configuration Status

```bash
# Get overall status
curl http://localhost:5000/admin/config/status \
  -H "Authorization: Bearer $TOKEN"
```

### Example 4: Progressive Rollout

```bash
# 1. Start with global WCS enabled but all services disabled (default)
# 2. Enable WCS for pilot service
curl -X PATCH http://localhost:5000/admin/config/services/test-rasters/wcs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'

# 3. Test and validate
# 4. Enable for more services incrementally
curl -X PATCH http://localhost:5000/admin/config/services/production-rasters/wcs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'
```

---

## Persistence

### In-Memory vs Persistent Changes

**Global Settings**:
- Runtime changes are **in-memory only**
- To persist, update `appsettings.json` or use environment variables
- Changes lost on server restart

**Service-Level Settings**:
- Runtime changes are **in-memory only**
- To persist, update `metadata.json` and reload with `/admin/metadata/reload`
- Changes lost on metadata reload or server restart

### Making Changes Persistent

#### Option 1: Update Configuration Files

**Global (appsettings.json)**:
```json
{
  "honua": {
    "services": {
      "wfs": { "enabled": false },
      "wms": { "enabled": true }
    }
  }
}
```

**Service-Level (metadata.json)**:
```json
{
  "services": [
    {
      "id": "demographics",
      "ogc": {
        "wfsEnabled": true,
        "wmsEnabled": false
      }
    }
  ]
}
```

Then:
- Global: Restart server
- Service-level: `POST /admin/metadata/reload`

#### Option 2: Environment Variables (Global Only)

```bash
export HONUA__SERVICES__WFS__ENABLED=false
export HONUA__SERVICES__WMS__ENABLED=true
```

Restart server for changes to take effect.

---

## Authorization

All endpoints require the `RequireAdministrator` policy:
- User must be authenticated
- User must have the `administrator` role

In **QuickStart mode**, these endpoints are disabled:
```json
{
  "error": "Service configuration changes are disabled in QuickStart mode."
}
```

---

## Use Cases

### 1. Emergency Protocol Shutdown
Immediately disable a protocol across all services (e.g., security issue discovered in WFS implementation):

```bash
PATCH /admin/config/services/wfs
{"enabled": false}
```

### 2. Gradual Service Rollout
Enable new protocol for services incrementally:
1. Global CSW: enabled
2. Service A CSW: enabled (pilot)
3. Test and validate
4. Service B, C, D CSW: enabled (production)

### 3. Maintenance Windows
Temporarily disable specific protocols:
```bash
# Before maintenance
PATCH /admin/config/services/wms {"enabled": false}

# Perform WMS backend maintenance

# After maintenance
PATCH /admin/config/services/wms {"enabled": true}
```

### 4. Load Management
Disable resource-intensive protocols during peak usage:
```bash
# Disable raster tile caching during business hours
PATCH /admin/config/services/rasterTiles {"enabled": false}
```

### 5. Feature Flags
Use service-level toggles as feature flags for A/B testing or canary deployments.

---

## Validation Rules

### Global to Service Validation

When toggling service-level protocols:
- **Cannot enable** a protocol if globally disabled
- Returns `422 Unprocessable Entity` with error details

### Service Constraints

When changing service-level settings:
- Service must exist
- Protocol must be valid for service type (e.g., `wcs` only for raster services)

---

## Change Notifications

The `IMetadataRegistry` provides change tokens via `GetChangeToken()`. Consumers can subscribe to configuration changes:

```csharp
var changeToken = metadataRegistry.GetChangeToken();
ChangeToken.OnChange(
    () => metadataRegistry.GetChangeToken(),
    () => {
        // Metadata changed - reload caches, update routing, etc.
    });
```

When runtime configuration changes are made:
- `MetadataRegistry.Update()` is called
- Change token is signaled
- Consumers (like OData cache) are notified

---

## Security Considerations

1. **Administrator-Only**: All endpoints require administrator role
2. **Audit Logging**: Configuration changes should be audited (future enhancement)
3. **QuickStart Protection**: Runtime changes disabled in QuickStart mode to prevent accidental misconfigurations
4. **Validation**: Cannot violate global/service hierarchy rules

---

## Error Responses

### 400 Bad Request
```json
{
  "error": "Unknown protocol: xyz. Valid values: wfs, wms, wmts, csw, wcs, stac, geometry, rasterTiles"
}
```

### 403 Forbidden
```json
{
  "error": "Service configuration changes are disabled in QuickStart mode."
}
```

### 404 Not Found
```json
{
  "error": "Service 'nonexistent' not found."
}
```

### 422 Unprocessable Entity
```json
{
  "error": "Cannot enable wfs for service 'demographics'. WFS is disabled globally in appsettings.json.",
  "details": ["Service 'demographics': WFS is enabled at service level but disabled globally..."],
  "note": "To enable this protocol, update appsettings.json (honua:services:wfs:enabled: true) and restart the server."
}
```

---

## Implementation Details

### Architecture

- **`HonuaConfigurationService`**: Manages global configuration state
  - `Update(HonuaConfiguration)` - Updates global settings in-memory
  - `GetChangeToken()` - Provides change notifications

- **`IMetadataRegistry`**: Manages service metadata
  - `Update(MetadataSnapshot)` - Updates service-level settings in-memory
  - `GetChangeToken()` - Provides change notifications

- **`ServiceApiConfigurationValidator`**: Validates configuration changes
  - Ensures service-level settings don't violate global constraints
  - Returns validation errors with actionable messages

### Endpoint Implementation

Located in: `src/Honua.Server.Host/Admin/RuntimeConfigurationEndpointRouteBuilderExtensions.cs`

- **GET** `/status` - Computes effective state from global + service configs
- **GET** `/services` - Returns global config
- **PATCH** `/services/{protocol}` - Updates global config
- **GET** `/services/{serviceId}` - Returns service config with effective states
- **PATCH** `/services/{serviceId}/{protocol}` - Updates service config with validation

---

## Future Enhancements

1. **Audit Logging**: Track who changed what and when
2. **Scheduled Changes**: Schedule protocol enables/disables for future times
3. **Configuration Snapshots**: Save/restore configuration states
4. **Persistence API**: Directly write changes to appsettings.json and metadata.json
5. **Webhook Notifications**: Notify external systems of configuration changes
6. **Bulk Operations**: Enable/disable multiple protocols or services at once
7. **Configuration Templates**: Pre-defined configuration profiles (maintenance mode, high-load mode, etc.)

---

## Related Documentation

- [Per-Service API Configuration](PER_SERVICE_API_CONFIGURATION.md) - Service-level API flags in metadata
- [Metadata Administration API](../src/Honua.Server.Host/Metadata/README.md) - Metadata reload endpoints
- [Authentication](../docs/authentication/README.md) - Authorization policies

---

## Quick Reference

| Action | Method | Endpoint |
|--------|--------|----------|
| Get all status | GET | `/admin/config/status` |
| Get global settings | GET | `/admin/config/services` |
| Toggle global protocol | PATCH | `/admin/config/services/{protocol}` |
| Get service config | GET | `/admin/config/services/{serviceId}` |
| Toggle service protocol | PATCH | `/admin/config/services/{serviceId}/{protocol}` |

**Valid Protocols (Global)**:
`wfs`, `wms`, `wmts`, `csw`, `wcs`, `stac`, `geometry`, `rasterTiles`

**Valid Protocols (Service)**:
`collections`, `wfs`, `wms`, `wmts`, `csw`, `wcs`

**Authorization**: Administrator role required (except QuickStart mode)

**Persistence**: In-memory only - update config files for persistence
