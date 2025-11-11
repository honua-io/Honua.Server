# Authorization Policy Matrix

**Generated**: 2025-11-11

This document provides a complete matrix of all authorization policies and their usage across the Honua Server codebase.

## Policy Definitions

| Policy | File | Line | Roles Required |
|--------|------|------|----------------|
| RequireAdministrator | AuthenticationExtensions.cs | 80-84 (enforced) | administrator |
| RequireAdministrator | AuthenticationExtensions.cs | 114-120 (quickstart) | administrator |
| RequireEditor | AuthenticationExtensions.cs | 85-89 (enforced) | administrator, editor |
| RequireEditor | AuthenticationExtensions.cs | 122-129 (quickstart) | administrator, editor |
| RequireDataPublisher | AuthenticationExtensions.cs | 90-94 (enforced) | administrator, datapublisher |
| RequireDataPublisher | AuthenticationExtensions.cs | 131-138 (quickstart) | administrator, datapublisher |
| RequireViewer | AuthenticationExtensions.cs | 95-99 (enforced) | administrator, datapublisher, viewer |
| RequireViewer | AuthenticationExtensions.cs | 140-148 (quickstart) | administrator, datapublisher, viewer |

## Policy Usage by Endpoint

### RequireAdministrator Endpoints (23 endpoint groups)

| File | Endpoint Path | Line |
|------|---------------|------|
| AlertAdministrationEndpoints.cs | `/admin/alerts/*` | 35 |
| AuditLogEndpoints.cs | `/api/admin/audit/*` | 27 |
| DegradationStatusEndpoints.cs | `/admin/degradation/*` | 30 |
| FeatureFlagEndpoints.cs | `/admin/feature-flags` | 28 |
| GeoEtlAiEndpoints.cs | `/admin/api/geoetl/ai/*` | 21 |
| GeoEtlExecutionEndpoints.cs | `/admin/api/geoetl/executions/*` | 20 |
| GeoEtlResilienceEndpoints.cs | `/admin/api/geoetl/resilience/*` | 25 |
| GeoEtlScheduleEndpoints.cs | `/admin/api/geoetl/schedules/*` | 21 |
| GeoEtlTemplateEndpoints.cs | `/admin/api/geoetl/templates/*` | 20 |
| GeoEtlWorkflowEndpoints.cs | `/admin/api/geoetl/workflows/*` | 20 |
| GeofenceAlertAdministrationEndpoints.cs | `/admin/geofence-alerts/*` | 27 |
| LayerGroupAdministrationEndpoints.cs | `/admin/layer-groups/*` | 32 |
| LoggingEndpointRouteBuilderExtensions.cs | `/admin/logging/*` | 63 |
| MapConfigurationEndpoints.cs | `/admin/maps/*` | 16 |
| MetadataAdministrationEndpoints.cs | `/admin/metadata/*` | 34 |
| RasterTileCacheEndpointRouteBuilderExtensions.cs | `/admin/api/tiles/raster/cache/*` | 58 |
| RasterTileCacheQuotaEndpoints.cs | `/admin/api/tiles/raster/cache/quota/*` | 23 |
| RasterTileCacheStatisticsEndpoints.cs | `/admin/api/tiles/raster/cache/stats/admin/*` | 44 |
| RbacEndpoints.cs | `/admin/rbac/*` | 20 |
| RuntimeConfigurationEndpointRouteBuilderExtensions.cs | `/admin/runtime-config/*` | 76 |
| ServerAdministrationEndpoints.cs | `/admin/server/*` | 29 |
| TokenRevocationEndpoints.cs | `/admin/tokens/*` | 46 |
| TracingEndpointRouteBuilderExtensions.cs | `/admin/tracing/*` | 65 |
| VectorTilePreseedEndpoints.cs | `/admin/api/tiles/vector/preseed` | 55 |

### RequireEditor Endpoints (3 controllers)

| File | Endpoint Path | Line |
|------|---------------|------|
| IfcImportController.cs | `/api/ifc/*` | 25 |
| GraphController.cs | `/api/v{version}/graph/*` | 19 |
| Geometry3DController.cs | `/api/v{version}/geometry/3d/*` | 19 |

### RequireDataPublisher Endpoints (2 endpoint groups)

| File | Endpoint Path | Line |
|------|---------------|------|
| DataIngestionEndpointRouteBuilderExtensions.cs | `/admin/api/ingest/*` | 61 |
| MigrationEndpointRouteBuilderExtensions.cs | `/admin/migrations/*` | 55 |

### RequireViewer Endpoints (1 endpoint group)

| File | Endpoint Path | Line |
|------|---------------|------|
| RasterTileCacheStatisticsEndpoints.cs | `/admin/api/tiles/raster/cache/stats/*` | 24 |

## Endpoint Count by Policy

| Policy | Endpoint Groups | Percentage |
|--------|----------------|------------|
| RequireAdministrator | 24 | 80.0% |
| RequireEditor | 3 | 10.0% |
| RequireDataPublisher | 2 | 6.7% |
| RequireViewer | 1 | 3.3% |
| **TOTAL** | **30** | **100%** |

## Role Access Matrix

| Endpoint Path | Administrator | Editor | DataPublisher | Viewer |
|--------------|---------------|--------|---------------|--------|
| `/admin/alerts/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/server/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/feature-flags` | ✅ | ❌ | ❌ | ❌ |
| `/api/admin/audit/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/api/geoetl/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/tokens/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/degradation/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/tracing/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/runtime-config/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/logging/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/metadata/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/geofence-alerts/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/layer-groups/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/maps/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/rbac/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/api/tiles/raster/cache/quota/*` | ✅ | ❌ | ❌ | ❌ |
| `/admin/api/tiles/raster/cache/*` (clear) | ✅ | ❌ | ❌ | ❌ |
| `/admin/api/tiles/vector/preseed` | ✅ | ❌ | ❌ | ❌ |
| `/api/ifc/*` | ✅ | ✅ | ❌ | ❌ |
| `/api/v{version}/graph/*` | ✅ | ✅ | ❌ | ❌ |
| `/api/v{version}/geometry/3d/*` | ✅ | ✅ | ❌ | ❌ |
| `/admin/api/ingest/*` | ✅ | ❌ | ✅ | ❌ |
| `/admin/migrations/*` | ✅ | ❌ | ✅ | ❌ |
| `/admin/api/tiles/raster/cache/stats/*` | ✅ | ❌ | ✅ | ✅ |

## Role Capability Summary

### Administrator
- **Total Access**: 30/30 endpoint groups (100%)
- **Unique Access**: 24 endpoint groups
- **Inherited Access**: 6 endpoint groups (Editor: 3, DataPublisher: 2, Viewer: 1)

### Editor
- **Total Access**: 3/30 endpoint groups (10%)
- **Unique Access**: 3 endpoint groups (IFC, Graph, Geometry3D)
- **Independent Role**: Does NOT inherit from other roles

### DataPublisher
- **Total Access**: 3/30 endpoint groups (10%)
- **Unique Access**: 2 endpoint groups (Ingest, Migrations)
- **Inherited Access**: 1 endpoint group (Viewer: Cache Stats)

### Viewer
- **Total Access**: 1/30 endpoint groups (3.3%)
- **Unique Access**: 1 endpoint group (Cache Stats - read only)
- **Lowest Privilege**: Read-only access

## Policy Distribution Visualization

```
RequireAdministrator ████████████████████████████████████████████ 80%
RequireEditor        █████ 10%
RequireDataPublisher ███ 6.7%
RequireViewer        █ 3.3%
```

## Security Notes

1. **80% of endpoints require Administrator role**
   - Most administrative functions require full admin access
   - This is appropriate for server management operations

2. **Editor role is independent**
   - Designed for data editing (IFC, 3D, Graphs)
   - Does not grant admin or publishing privileges
   - Good separation of concerns

3. **DataPublisher includes Viewer**
   - Makes sense: publishers need to view what they publish
   - Follows principle of cumulative permissions

4. **Viewer is most restricted**
   - Read-only access to monitoring/statistics
   - Cannot modify any configuration
   - Appropriate for monitoring teams

## Compliance

✅ **All admin endpoints have authorization**
- No unsecured admin endpoints found
- All 30 endpoint groups require authentication (when enforced)

✅ **Policies follow least privilege**
- Most restrictive policies on most sensitive endpoints
- Clear role hierarchy
- No privilege escalation paths

✅ **Consistent policy application**
- All endpoint groups in `/admin/*` require authorization
- All API controllers with sensitive operations require authorization
- Consistent use of `.RequireAuthorization()` pattern

## Testing Coverage

| Policy | Test Methods | Status |
|--------|--------------|--------|
| RequireAdministrator | 4 | ✅ |
| RequireEditor | 4 | ✅ |
| RequireDataPublisher | 3 | ✅ |
| RequireViewer | 3 | ✅ |
| QuickStart Mode | 2 | ✅ |
| Role Hierarchy | 4 | ✅ |
| **TOTAL** | **20** | **✅** |

All policies have comprehensive integration test coverage.

---

**Last Updated**: 2025-11-11
**Verified By**: Claude Code Agent
