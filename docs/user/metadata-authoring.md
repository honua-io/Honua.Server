# Metadata Authoring Guide (Phase 0)

Honua.Next reads configuration from JSON metadata files (typically under metadata/). Use this guide to describe services, layers, providers, and styling for the MVP.

Need to load data into an existing layer? See the `docs/user/data-ingestion.md` guide for the upload workflow.

## Directory Layout
`	ext
metadata/
  services.json            # service + layer definitions
  providers/
    postgis.json           # connection + dataset mapping
    sqlite.json
    sqlserver.json
  styling.json             # optional style catalog
`

## Example Layer Definition
`json
{
  "services": [
    {
      "id": "public",
      "title": "Public Data",
      "type": "feature",
      "provider": "postgis",
      "layers": [
        {
          "id": "roads",
          "title": "Road Centerlines",
          "dataSource": {
            "provider": "postgis",
            "schema": "transport",
            "table": "roads",
            "geometryColumn": "geom",
            "identityColumn": "road_id",
            "connectionSecret": "env:HONUA_POSTGIS_CONN"
          },
          "schemaMapping": {
            "columns": [
              { "sourceName": "road_id", "friendlyName": "Road ID", "visible": true, "queryable": true },
              { "sourceName": "road_name", "friendlyName": "Road Name", "visible": true, "queryable": true },
              { "sourceName": "geom", "friendlyName": "Geometry", "visible": false, "queryable": false }
            ]
          },
          "catalog": {
            "group": "Vector Services",
            "summary": "Official centerline network",
            "thumbnail": "/media/catalog/roads.png"
          },
          "formats": ["geojson", "kml", "mvt", "geopackage"]
        }
      ]
    }
  ]
}
`

## Provider Guidance
| Provider | Key Fields | Notes |
|----------|------------|-------|
| PostGIS | schema, table, geometryColumn, identityColumn, connectionSecret, storage.crs | Use environment variables (env:) for secrets; set `storage.crs` or `storage.srid` when not 4326. |
| SQLite | path, geometryColumn, identityColumn (optional), storage.crs | Paths are relative to the host working directory; ensure the file ships with the server. Declare a storage CRS so CLI validation and reprojection behave consistently. |
| SQL Server | schema, table, geometryColumn or geographyColumn, DefaultSrid, storage.crs | Spatial indexes are operator-managed; metadata can reference preferred index names. Providing `storage.crs` keeps CLI validation quiet and improves diagnostics. |

Honua CLI (`honua metadata validate`) emits warnings when a layer lacks both `storage.srid` and `storage.crs`, or when the two values disagree. Add at least one of the fields per layer so CRS negotiation always has a canonical storage reference.


## Styling Links
- Layers may reference entries in styling.json via the `styles` array and `defaultStyleId`.
- Define reusable style definitions in `metadata/styles.json` using the following shape:

```json
{
  "styles": [
    {
      "id": "primary-roads-line",
      "title": "Primary Roads",
      "format": "mvp-style",
      "renderer": "simple",
      "geometryType": "line",
      "rules": [
        {
          "id": "default",
          "default": true,
          "symbolizer": {
            "symbolType": "line",
            "strokeColor": "#FF8800FF",
            "strokeWidth": 2.0
          }
        }
      ]
    }
  ]
}
```

- Each rule may declare optional `filter`, `minScale`, and `maxScale` values to control visibility.
- MVP styling supports simple point/line/polygon symbolizers—see `design/phases/mvp/styling-symbology.md`.
- Set `geometryType` carefully: use `"raster"` for pure imagery styles, but specify `"point"`, `"line"`, or `"polygon"` when the raster render should include vector overlays (e.g., grid outlines or annotations). Honua only fetches overlay geometries when the style declares a non-raster geometry type.
- Layers may also declare top-level `minScale`/`maxScale` values (scale denominators) to drive GeoServices REST clients and scale-aware queries; Honua will hide the layer outside the configured range when callers supply map extent/image display parameters.

## Validation Workflow
1. Edit metadata files.
2. Run schema validation (placeholder CLI): dotnet run --project tools/MetadataValidator or the schema mapping CLI when it becomes available.
3. Apply metadata: curl -X POST https://localhost:5000/admin/metadata/apply -d @metadata/services.json (requires authentication).
4. Verify /ogc/collections and /rest/services expose expected layers. Use the support CLI for additional automated checks.

## Best Practices
- Keep credentials out of source control—use connectionSecret references.
- Update schemaMapping.pendingChanges after running the schema diff CLI so reviewers can see drift.
- Populate catalog hints (group, summary, thumbnail) to keep the services directory organized.
- Document supported formats in the 
ormats array for each layer to aid documentation and tooling.

Refer to design/phases/mvp/schema-mapping-metadata.md for deeper schema mapping detail and docs/user/support/README.md for automated diagnostics.
