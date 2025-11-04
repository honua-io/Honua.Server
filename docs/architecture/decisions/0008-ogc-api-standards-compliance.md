# 8. OGC API Standards Compliance

Date: 2025-10-17

Status: Accepted

## Context

Honua is a geospatial server that must interoperate with GIS clients, web mapping tools, and data consumers. The geospatial industry has standardized APIs defined by the Open Geospatial Consortium (OGC).

**Standards Compliance Requirements:**
- Industry interoperability (QGIS, ArcGIS Pro, Leaflet, OpenLayers)
- Government procurement (many require OGC compliance)
- Data portability between systems
- Future-proof API design
- Proven patterns for geospatial operations

**OGC Standards in Honua:**
- **OGC API - Features 1.0**: Modern REST API for vector features
- **WFS 2.0**: Legacy XML-based feature service (backward compatibility)
- **WMS 1.3**: Raster map rendering service
- **STAC 1.0**: SpatioTemporal Asset Catalog (not OGC, but de facto standard)

## Decision

Honua will **fully implement OGC API Standards** as the primary API design, with legacy OGC services (WFS, WMS) for backward compatibility.

**Primary:** OGC API - Features 1.0 (modern JSON/GeoJSON)
**Legacy:** WFS 2.0, WMS 1.3 (XML-based, for compatibility)
**Additional:** Esri REST API (for ArcGIS ecosystem integration)

**Standards Compliance:**
- OpenAPI 3.0 specification for API documentation
- GeoJSON as primary feature representation
- CQL2 (Common Query Language 2) for filtering
- HTTP-based with RESTful principles
- Pagination via links (RFC 5988)
- Bounding box queries with standard parameter names

## Consequences

### Positive

- **Interoperability**: Works with all OGC-compliant clients
- **Proven Design**: Standards-based API patterns
- **Documentation**: Extensive OGC specifications and examples
- **Ecosystem**: Large tooling ecosystem (QGIS, GeoServer, etc.)
- **Longevity**: Standards evolve but maintain compatibility
- **Government Sales**: Required for many public sector contracts
- **Testing**: OGC conformance test suites available

### Negative

- **Complexity**: OGC standards are comprehensive and detailed
- **Legacy Support**: Must maintain WFS/WMS for old clients
- **XML Processing**: WFS requires XML parsing/generation
- **Strict Validation**: Standards conformance requires exact compliance
- **Documentation Overhead**: Must document all OGC endpoints

### Neutral

- Some clients prefer Esri REST API over OGC
- Standards evolve slowly (both pro and con)

## Alternatives Considered

### 1. Custom REST API Only
**Verdict:** Rejected - reinventing the wheel, poor interoperability

### 2. Esri REST API Only
**Verdict:** Rejected - vendor lock-in, limited to Esri ecosystem

### 3. GraphQL API
**Verdict:** Rejected - not standard in GIS, poor client support

## Implementation

**OGC API - Features Endpoints:**
```
GET /ogc                          # Landing page
GET /ogc/conformance              # Conformance declaration
GET /ogc/collections              # Collections list
GET /ogc/collections/{id}         # Collection metadata
GET /ogc/collections/{id}/items   # Feature query
GET /ogc/collections/{id}/items/{featureId}  # Single feature
```

**Code Reference:**
- OGC endpoints: `/src/Honua.Server.Host/Features/OgcApi/`
- WFS endpoints: `/src/Honua.Server.Host/Features/Wfs/`
- WMS endpoints: `/src/Honua.Server.Host/Features/Wms/`

## References

- [OGC API - Features Specification](https://ogcapi.ogc.org/features/)
- [WFS 2.0 Specification](https://www.ogc.org/standards/wfs)
- [WMS 1.3 Specification](https://www.ogc.org/standards/wms)

## Notes

OGC compliance is non-negotiable for a serious geospatial server. The investment in implementing standards pays off through broad ecosystem compatibility and government/enterprise sales opportunities.
