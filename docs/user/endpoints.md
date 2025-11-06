# Honua.Next API Endpoints

> Phase-0 summary of service entry points and example requests. All endpoints honor standard HTTP content negotiation and accept the ?f= query parameter where formats are supported.

## Base URLs
- OGC API Features: https://{host}/ogc
- OGC WFS (preview): https://{host}/wfs
- OGC WMS: https://{host}/wms
- OGC API Records: https://{host}/records
- STAC API: https://{host}/stac
- Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST Services Directory: https://{host}/rest/services
- Esri Geometry Service: https://{host}/rest/services/Geometry/GeometryServer
- OData (metadata query): https://{host}/odata
- Carto-compatible API: https://{host}/carto
- Catalog API: https://{host}/api/catalog
- Authentication (Local mode): https://{host}/api/auth
- Administrative API: https://{host}/admin
- Prometheus metrics (optional): https://{host}/metrics

## OGC API Features
| Operation | Endpoint | Example |
|-----------|----------|---------|
| Service landing | /ogc | curl https://localhost:5000/ogc |
| List collections | /ogc/collections | curl "https://localhost:5000/ogc/collections?limit=10" |
| Items (GeoJSON) | /ogc/collections/{collectionId}/items | curl "https://localhost:5000/ogc/collections/roads/items?f=geojson&limit=5" |
| Items (KML/KMZ) | same as above | curl -H "Accept: application/vnd.google-earth.kml+xml" "https://localhost:5000/ogc/collections/roads/items?f=kml&limit=1" |
| Vector/Raster tiles | /ogc/collections/{collectionId}/tiles/{tilesetId}/{tileMatrixSetId}/{tileMatrix}/{tileRow}/{tileCol} | curl "https://localhost:5000/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldWebMercatorQuad/0/0/0?format=png" --output tile.png |
| Vector tiles (MVT) | same as above | curl "https://localhost:5000/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldWebMercatorQuad/14/2818/6538?f=mvt" --output tile.mvt |
| TileJSON metadata | /ogc/collections/{collectionId}/tiles/{tilesetId}/tilejson | curl "https://localhost:5000/ogc/collections/roads::roads-primary/tiles/roads-imagery/tilejson?tileMatrixSet=WorldWebMercatorQuad" |
| GeoPackage export | /ogc/collections/{collectionId}/items | curl -OJ "https://localhost:5000/ogc/collections/roads/items?f=geopackage" |
| Create feature | POST /ogc/collections/{collectionId}/items | curl -X POST -H "Content-Type: application/geo+json" -d @new-feature.json "https://localhost:5000/ogc/collections/roads/items" |
| Replace feature | PUT /ogc/collections/{collectionId}/items/{featureId} | curl -X PUT -H "Content-Type: application/geo+json" -H "If-Match: \"etag-value\"" -d @feature.json "https://localhost:5000/ogc/collections/roads/items/123" |
| Patch feature | PATCH /ogc/collections/{collectionId}/items/{featureId} | curl -X PATCH -H "Content-Type: application/geo+json" -H "If-Match: \"etag-value\"" -d '{"type":"Feature","id":"123","properties":{"status":"Closed"}}' "https://localhost:5000/ogc/collections/roads/items/123" |
| Delete feature | DELETE /ogc/collections/{collectionId}/items/{featureId} | curl -X DELETE -H "If-Match: \"etag-value\"" "https://localhost:5000/ogc/collections/roads/items/123" |

> Add `f=html` (or send `Accept: text/html`) to landing, collections, items, and search requests to receive a lightweight HTML view alongside the JSON responses.

> Vector tiles: Add `f=mvt` or `f=pbf` to tile requests to receive Mapbox Vector Tiles (MVT/PBF format). For PostGIS data sources, tiles are generated using native ST_AsMVT functions. Vector tiles are currently in preview.

## OGC WFS (preview)
| Operation | Endpoint | Example | Notes |
|-----------|----------|---------|-------|
| GetCapabilities | /wfs?service=WFS&request=GetCapabilities | curl "https://localhost:5000/wfs?service=WFS&request=GetCapabilities" | Returns an XML capabilities document enumerating current collections. |
| DescribeFeatureType | /wfs?service=WFS&request=DescribeFeatureType&typeNames={serviceId}:{layerId} | curl "https://localhost:5000/wfs?service=WFS&request=DescribeFeatureType&typeNames=roads:roads-primary" | Produces an XML schema describing feature attributes and geometry. |
| GetFeature | /wfs?service=WFS&request=GetFeature&typeNames={serviceId}:{layerId}&bbox=-180,-90,180,90 | curl "https://localhost:5000/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary&outputFormat=application/geo+json" | Supports bbox, CQL/XML filters, paging, and GeoJSON or GML output. |
| GetFeatureWithLock | /wfs?service=WFS&request=GetFeatureWithLock&typeNames={serviceId}:{layerId} | curl "https://localhost:5000/wfs?service=WFS&request=GetFeatureWithLock&typeNames=roads:roads-primary" | Returns GML 3.2 responses with a `lockId` attribute for follow-up transactions. |
| LockFeature | /wfs?service=WFS&request=LockFeature&typeNames={serviceId}:{layerId}&lockAction=ALL | curl "https://localhost:5000/wfs?service=WFS&request=LockFeature&typeNames=roads:roads-primary" | Acquires transactional locks; use `lockAction=SOME` to tolerate conflicts and inspect `<FeaturesNotLocked>`. |
| Transaction (WFS-T) | POST /wfs?service=WFS&request=Transaction | curl -X POST -H "Content-Type: application/xml" --data-binary @transaction.xml "https://localhost:5000/wfs?service=WFS&request=Transaction" | Accepts Insert/Update/Delete operations; include `lockId` (and optional `releaseAction`) when editing locked features. |

> Enablement: set `"honua:services:wfs:enabled"` to `true` in appsettings to expose the WFS endpoint (defaults to enabled).

> WFS gaps: only the first entry in `typeNames` is evaluated (multi-type joins and ad-hoc stored queries are not implemented yet). Supported output formats are GeoJSON (`application/geo+json`) and GML 3.2 (`application/gml+xml; version=3.2`); other encodings and `GetPropertyValue` are not exposed. Locking and transactions require the target layer to publish a stable `idField` value. Bounding boxes and `srsName` arguments accept plain `EPSG:{code}` strings or full OGC URNs; responses advertise normalized URN identifiers (`http://www.opengis.net/def/crs/...`).

## Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST Feature Services
| Operation | Endpoint | Example |
|-----------|----------|---------|
| Service directories | /rest/services | curl "https://localhost:5000/rest/services?f=pjson" |
| Layer metadata | /rest/services/{folder}/{service}/FeatureServer?f=json | curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0?f=json" |
| Query results | /rest/services/{folder}/{service}/FeatureServer/{layerId}/query | curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=1=1&outFields=*&f=geojson" |
| KML/KMZ | same query endpoint | curl -OJ "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?f=kmz" |
| queryRelatedRecords | /rest/services/{folder}/{service}/FeatureServer/{layerId}/queryRelatedRecords | curl "https://localhost:5000/rest/services/transportation/roads/FeatureServer/0/queryRelatedRecords?relationshipId=1&objectIds=1&outFields=inspection_id,status" |
| MapServer find | /rest/services/{folder}/{service}/MapServer/find | curl "https://localhost:5000/rest/services/transportation/roads/MapServer/find?f=json&searchText=Sunset&searchFields=name&layers=visible" |
| Image export | /rest/services/{folder}/{service}/ImageServer/exportImage | curl "https://localhost:5000/rest/services/transportation/roads/ImageServer/exportImage?bbox=-122.6,45.5,-122.3,45.7&size=1024,512&format=png" --output imagery.png |
| applyEdits | /rest/services/{folder}/{service}/FeatureServer/{layerId}/applyEdits | curl -X POST -H "Content-Type: application/json" -d '{"adds":[{"attributes":{"name":"New"}}]}' "https://localhost:5000/rest/services/transportation/roads/FeatureServer/0/applyEdits?f=json" |

> MapServer `find` accepts `layers` values of `all`, `visible`, `top`, or an explicit comma-separated list (e.g., `layers=0,2`). When `visible` metadata is not published, Honua defaults to all service layers; `top` targets the highest drawing order.

## OData v4 API
| Operation | Endpoint | Example | Notes |
|-----------|----------|---------|-------|
| Service document | /odata | curl "https://localhost:5000/odata" | OData service root |
| Metadata document | /odata/$metadata | curl "https://localhost:5000/odata/\$metadata" | Entity model (EDMX) |
| Query features | /odata/{LayerName} | curl "https://localhost:5000/odata/Roads?\$top=10&\$select=name,length" | OData query options |
| Filter features | /odata/{LayerName} | curl "https://localhost:5000/odata/Roads?\$filter=length gt 1000" | Standard OData filters |
| Spatial filter | /odata/{LayerName} | curl "https://localhost:5000/odata/Roads?\$filter=geo.intersects(geometry,geography'POLYGON((...))') | Spatial functions |
| Order results | /odata/{LayerName} | curl "https://localhost:5000/odata/Roads?\$orderby=name desc" | Sorting |
| Expand relations | /odata/{LayerName} | curl "https://localhost:5000/odata/Roads?\$expand=Inspections" | Related records |
| Count | /odata/{LayerName}/$count | curl "https://localhost:5000/odata/Roads/\$count" | Total record count |

> Supports standard OData query options: `$filter`, `$select`, `$orderby`, `$top`, `$skip`, `$count`, `$expand`. Spatial functions include `geo.intersects`, `geo.distance`, `geo.length`. See [OData documentation](https://www.odata.org/) for full query syntax.

## Carto API
| Operation | Endpoint | Example |
|-----------|----------|---------|
| Landing | /carto | curl "https://localhost:5000/carto" |
| List datasets | /carto/api/v3/datasets | curl "https://localhost:5000/carto/api/v3/datasets" |
| Dataset detail | /carto/api/v3/datasets/{datasetId} | curl "https://localhost:5000/carto/api/v3/datasets/roads.roads-primary" |
| Dataset schema | /carto/api/v3/datasets/{datasetId}/schema | curl "https://localhost:5000/carto/api/v3/datasets/roads.roads-primary/schema" |
| SQL query (GET) | /carto/api/v3/sql?q=SELECT+%2A+FROM+roads.roads-primary+LIMIT+5 | curl "https://localhost:5000/carto/api/v3/sql?q=SELECT+%2A+FROM+roads.roads-primary+LIMIT+5" |
| SQL query (POST) | /carto/api/v3/sql | curl -X POST -H "Content-Type: application/json" -d '{"q":"SELECT name, COUNT(*) AS total FROM roads.roads-primary GROUP BY name ORDER BY total DESC LIMIT 5"}' https://localhost:5000/carto/api/v3/sql |

> The SQL endpoint supports `SELECT` statements (including grouped aggregates such as `COUNT(*)`, `SUM`, `AVG`, `MIN`, and `MAX`) with optional `WHERE` filters (including `IN`/`LIKE`), `GROUP BY`, `ORDER BY`, `LIMIT`, and `OFFSET` clauses across a single dataset.

## WMS
| Operation | Endpoint | Example |
|-----------|----------|---------|
| Capabilities | /wms?service=WMS&request=GetCapabilities | curl "https://localhost:5000/wms?service=WMS&request=GetCapabilities" |
| GetMap | /wms | curl "https://localhost:5000/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=1024&height=512&format=image/png&crs=EPSG:4326" --output map.png |
| GetFeatureInfo | /wms | curl "https://localhost:5000/wms?service=WMS&request=GetFeatureInfo&layers=roads:roads-imagery&query_layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=512&height=512&i=256&j=256&info_format=application/json" |
| DescribeLayer | /wms | curl "https://localhost:5000/wms?service=WMS&request=DescribeLayer&layers=roads:roads-imagery" |
| GetLegendGraphic | /wms | curl "https://localhost:5000/wms?service=WMS&request=GetLegendGraphic&layer=roads:roads-imagery&width=20&height=20" --output legend.png |

> WMS layer names follow the `{serviceId}:{datasetId}` pattern. Dataset identifiers are defined in the `rasterDatasets` section of your metadata. You can request multiple layers by comma-separating names (`layers=roads:roads-imagery,roads:roads-imagery-alt`) and providing matching `STYLES` tokens (blank entries fall back to the default style). GetMap currently serves PNG or JPEG imagery, and GetFeatureInfo supports `application/json`, `application/geo+json`, `application/xml`, `text/html`, and `text/plain` responses. DescribeLayer returns XML describing layer schemas and WFS associations. GetLegendGraphic generates PNG legend icons (basic implementation). Operations such as TIME/ELEVATION dimensions and external SLD/STYLE overrides are not yet available. CRS inputs (`crs` or `srs`) accept `CRS:84`, `EPSG:{code}`, or full OGC URNs (for example `http://www.opengis.net/def/crs/EPSG/0/3857`); requests default to `EPSG:4326` when the parameter is omitted.

## OGC API Records
| Operation | Endpoint | Example |
|-----------|----------|---------|
| Landing | /records | curl "https://localhost:5000/records" |
| List collections | /records/collections | curl "https://localhost:5000/records/collections" |
| Collection summary | /records/collections/{collectionId} | curl "https://localhost:5000/records/collections/transportation" |
| Records within a collection | /records/collections/{collectionId}/items | curl "https://localhost:5000/records/collections/transportation/items?limit=5" |
| Single record | /records/collections/{collectionId}/items/{recordId} | curl "https://localhost:5000/records/collections/transportation/items/roads%3Aroads-primary" |

## STAC API
| Operation | Endpoint | Example | Notes |
|-----------|----------|---------|-------|
| Landing page | /stac | curl "https://localhost:5000/stac" | STAC catalog root |
| Conformance | /stac/conformance | curl "https://localhost:5000/stac/conformance" | Supported conformance classes |
| Collections | /stac/collections | curl "https://localhost:5000/stac/collections" | List STAC collections |
| Collection detail | /stac/collections/{collectionId} | curl "https://localhost:5000/stac/collections/imagery" | Collection metadata |
| Items | /stac/collections/{collectionId}/items | curl "https://localhost:5000/stac/collections/imagery/items?limit=10" | Items in collection |
| Item detail | /stac/collections/{collectionId}/items/{itemId} | curl "https://localhost:5000/stac/collections/imagery/items/scene-2025-10-01" | Single STAC item |
| Search (GET) | /stac/search | curl "https://localhost:5000/stac/search?bbox=-122,45,-121,46&datetime=2025-01-01/2025-12-31" | Search items by bbox, datetime, collections |
| Search (POST) | /stac/search | curl -X POST -H "Content-Type: application/json" -d '{"bbox":[-122,45,-121,46],"limit":10}' "https://localhost:5000/stac/search" | Advanced search with filters |

> Enable STAC via `honua:services:stac:enabled=true`. Requires STAC catalog configured in metadata or external provider.

## Catalog / Discovery API
| Operation | Endpoint | Example | Notes |
|-----------|----------|---------|-------|
| Search catalog | /api/catalog | curl "https://localhost:5000/api/catalog?q=roads&group=Transportation" | Search by query and group |
| Get catalog record | /api/catalog/{serviceId}/{layerId} | curl "https://localhost:5000/api/catalog/transportation/roads-primary" | Detailed catalog metadata |

## Geometry Service
| Operation | Endpoint | Example | Notes |
|-----------|----------|---------|-------|
| Service metadata | /rest/services/Geometry/GeometryServer | curl "https://localhost:5000/rest/services/Geometry/GeometryServer?f=json" | Esri-compatible geometry service |
| Project | /rest/services/Geometry/GeometryServer/project | curl -X POST -d '{"geometries":[...],"inSR":4326,"outSR":3857}' "https://localhost:5000/rest/services/Geometry/GeometryServer/project?f=json" | Reproject geometries |
| Buffer | /rest/services/Geometry/GeometryServer/buffer | curl -X POST -d '{"geometries":[...],"distances":[100],"unit":"meters"}' "https://localhost:5000/rest/services/Geometry/GeometryServer/buffer?f=json" | Buffer geometries |
| Simplify | /rest/services/Geometry/GeometryServer/simplify | curl -X POST -d '{"geometries":[...]}' "https://localhost:5000/rest/services/Geometry/GeometryServer/simplify?f=json" | Simplify geometries |
| Union | /rest/services/Geometry/GeometryServer/union | curl -X POST -d '{"geometries":[...]}' "https://localhost:5000/rest/services/Geometry/GeometryServer/union?f=json" | Union geometries |
| Intersect | /rest/services/Geometry/GeometryServer/intersect | curl -X POST -d '{"geometries":[...],"geometry":{...}}' "https://localhost:5000/rest/services/Geometry/GeometryServer/intersect?f=json" | Intersect geometries |
| Difference | /rest/services/Geometry/GeometryServer/difference | curl -X POST -d '{"geometries":[...],"geometry":{...}}' "https://localhost:5000/rest/services/Geometry/GeometryServer/difference?f=json" | Difference operation |
| Distance | /rest/services/Geometry/GeometryServer/distance | curl -X POST -d '{"geometry1":{...},"geometry2":{...},"distanceUnit":"meters"}' "https://localhost:5000/rest/services/Geometry/GeometryServer/distance?f=json" | Calculate distance |
| Areas and Lengths | /rest/services/Geometry/GeometryServer/areasAndLengths | curl -X POST -d '{"polygons":[...]}' "https://localhost:5000/rest/services/Geometry/GeometryServer/areasAndLengths?f=json" | Calculate areas and perimeters |

> Enable geometry service via `honua:services:geometry:enabled=true`. See [Geometry Service Guide](geometry-service.md) for details.

## Attachments
| Operation | Endpoint | Example | Notes |
|-----------|----------|---------|-------|
| Query attachments | /rest/services/{folder}/{service}/FeatureServer/{layer}/{objectId}/queryAttachments | curl "https://localhost:5000/rest/services/Public/Inspections/FeatureServer/0/123/queryAttachments?f=json" | List attachments for feature |
| Add attachment | /rest/services/{folder}/{service}/FeatureServer/{layer}/{objectId}/addAttachment | curl -X POST -F "attachment=@photo.jpg" "https://localhost:5000/rest/services/Public/Inspections/FeatureServer/0/123/addAttachment?f=json" | Upload attachment (multipart/form-data) |
| Update attachment | /rest/services/{folder}/{service}/FeatureServer/{layer}/{objectId}/updateAttachment | curl -X POST -F "attachment=@photo-updated.jpg" "https://localhost:5000/rest/services/Public/Inspections/FeatureServer/0/123/updateAttachment?attachmentId=5&f=json" | Update existing attachment |
| Delete attachment | /rest/services/{folder}/{service}/FeatureServer/{layer}/deleteAttachments | curl -X POST -d '{"objectIds":[123],"attachmentIds":[5]}' "https://localhost:5000/rest/services/Public/Inspections/FeatureServer/0/deleteAttachments?f=json" | Delete attachment |
| Download attachment | /rest/services/{folder}/{service}/FeatureServer/{layer}/{objectId}/attachments/{attachmentId} | curl -OJ "https://localhost:5000/rest/services/Public/Inspections/FeatureServer/0/123/attachments/5" | Download attachment file |

> Attachments must be enabled per-layer in metadata. See [Attachments Guide](attachments.md) for configuration.

## Administrative API
| Operation | Endpoint | Example | Notes |
|-----------|----------|---------|-------|
| Reload metadata | POST /admin/metadata/reload | curl -X POST -H "Authorization: Bearer {token}" "https://localhost:5000/admin/metadata/reload" | Reload metadata from disk |
| Validate metadata | POST /admin/metadata/validate | curl -X POST -H "Authorization: Bearer {token}" -H "Content-Type: application/json" -d @metadata.json "https://localhost:5000/admin/metadata/validate" | Validate metadata schema |
| Apply metadata | POST /admin/metadata/apply | curl -X POST -H "Authorization: Bearer {token}" -H "Content-Type: application/json" -d @metadata.json "https://localhost:5000/admin/metadata/apply" | Apply new metadata |
| Diff metadata | POST /admin/metadata/diff | curl -X POST -H "Authorization: Bearer {token}" -H "Content-Type: application/json" -d @metadata.json "https://localhost:5000/admin/metadata/diff" | Compare metadata changes |
| List snapshots | GET /admin/metadata/snapshots | curl -H "Authorization: Bearer {token}" "https://localhost:5000/admin/metadata/snapshots" | List metadata backups |
| Create snapshot | POST /admin/metadata/snapshots | curl -X POST -H "Authorization: Bearer {token}" -d '{"label":"backup-2025-10-01","notes":"Pre-migration"}' "https://localhost:5000/admin/metadata/snapshots" | Create metadata backup |
| Restore snapshot | POST /admin/metadata/snapshots/{label}/restore | curl -X POST -H "Authorization: Bearer {token}" "https://localhost:5000/admin/metadata/snapshots/backup-2025-10-01/restore" | Restore from backup |
| Create migration job | POST /admin/migrations/jobs | curl -X POST -H "Authorization: Bearer {token}" -d '{"sourceUrl":"https://services.arcgis.com/.../FeatureServer","targetProvider":"postgis",...}' "https://localhost:5000/admin/migrations/jobs" | Migrate from ArcGIS Server |
| List migrations | GET /admin/migrations/jobs | curl -H "Authorization: Bearer {token}" "https://localhost:5000/admin/migrations/jobs" | List migration jobs |
| Get migration status | GET /admin/migrations/jobs/{jobId} | curl -H "Authorization: Bearer {token}" "https://localhost:5000/admin/migrations/jobs/migration-abc123" | Migration job details |
| Cancel migration | DELETE /admin/migrations/jobs/{jobId} | curl -X DELETE -H "Authorization: Bearer {token}" "https://localhost:5000/admin/migrations/jobs/migration-abc123" | Cancel running migration |
| Create preseed job | POST /admin/raster-cache/jobs | curl -X POST -H "Authorization: Bearer {token}" -d '{"datasetIds":["imagery"],"maxZoom":14}' "https://localhost:5000/admin/raster-cache/jobs" | Pre-generate raster tiles |
| List preseed jobs | GET /admin/raster-cache/jobs | curl -H "Authorization: Bearer {token}" "https://localhost:5000/admin/raster-cache/jobs" | List tile generation jobs |
| Purge cache | POST /admin/raster-cache/datasets/purge | curl -X POST -H "Authorization: Bearer {token}" -d '{"datasetIds":["imagery"]}' "https://localhost:5000/admin/raster-cache/datasets/purge" | Delete cached tiles |

> All admin endpoints require `administrator` role. See [Administrative API Guide](admin-api.md) for details.

## Authentication
| Operation | Endpoint | Example | Notes |
|-----------|----------|---------|-------|
| Login (Local mode) | POST /api/auth/local/login | curl -X POST -H "Content-Type: application/json" -d '{"username":"admin","password":"secret"}' "https://localhost:5000/api/auth/local/login" | Returns JWT token |
| Get current user | GET /api/auth/user | curl -H "Authorization: Bearer {token}" "https://localhost:5000/api/auth/user" | Current user info |
| Logout | POST /api/auth/logout | curl -X POST -H "Authorization: Bearer {token}" "https://localhost:5000/api/auth/logout" | Client-side only |

> See [Authentication Guide](authentication.md) for OIDC and user management.

## Observability
| Operation | Endpoint | Example | Notes |
|-----------|----------|---------|-------|
| Prometheus scrape | /metrics (configurable) | curl "https://localhost:5000/metrics" | Enable by setting `observability.metrics.enabled=true`. Update `observability.metrics.endpoint` for a custom path. |

See [Format Matrix](format-matrix.md) for MIME types and additional notes.
