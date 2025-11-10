# 13. Hybrid COG + Zarr Raster Architecture

Date: 2025-10-17

Status: Accepted

## Context

Honua 2.0 added raster data processing for climate, weather, and satellite imagery. These datasets come in various formats:
- **NetCDF**: Climate model outputs, oceanographic data
- **HDF5**: NASA satellite data (MODIS, Landsat)
- **GRIB2**: Weather forecast models
- **GeoTIFF**: General raster imagery

**Use Cases:**
1. **Spatial queries**: "What's the temperature at this location?"
2. **Tile serving**: Render map tiles for visualization
3. **Time-series analysis**: "Temperature trend over 30 days"
4. **Analytics**: NDVI, terrain analysis, zonal statistics

**Challenges:**
- Source formats not cloud-optimized (slow HTTP access)
- Time-series data requires efficient temporal queries
- Large datasets (GB to TB) don't fit in memory
- Need both spatial and temporal indexing

## Decision

Implement **hybrid raster architecture** using Cloud Optimized GeoTIFF (COG) for spatial access and Zarr for time-series analysis.

**Strategy:**
```
Source Data (NetCDF/HDF5/GRIB2)
  ├─ Spatial Use Cases → Convert to COG → S3/Azure/GCS
  └─ Time-Series Use Cases → Convert to Zarr → S3/Azure/GCS
```

**COG for Spatial:**
- Single-time rasters
- Tile serving
- Spatial analytics
- HTTP range requests

**Zarr for Temporal:**
- Multi-temporal datasets
- Time-series queries
- Climate model outputs
- Chunked array access

## Consequences

### Positive

- **Optimized Access**: Right format for each use case
- **Cloud-Native**: Both COG and Zarr support HTTP range requests
- **Performance**: 10-50x faster than source formats
- **Scalability**: Stream data without loading entirely
- **Standards**: COG is OGC-recognized, Zarr is widely adopted

### Negative

- **Storage Overhead**: 2x storage (source + optimized)
- **Conversion Time**: Initial conversion required
- **Two Formats**: More complexity than single format
- **Cache Invalidation**: Must detect source changes

### Neutral

- GDAL still needed for format conversion
- Cache management strategies needed

## Alternatives Considered

**GeoTIFF Only**: Rejected - poor for time-series
**Zarr Only**: Rejected - poor tile serving support
**Keep Source Formats**: Rejected - too slow for cloud access
**PostGIS Raster**: Rejected - doesn't scale for large rasters

## Implementation

See `/docs/RASTER_STORAGE_ARCHITECTURE.md` for detailed architecture.

**Code Reference:**
- COG Reader: `/src/Honua.Server.Core/Raster/Readers/LibTiffCogReader.cs`
- Zarr Reader: `/src/Honua.Server.Core/Raster/Readers/HttpZarrReader.cs`
- Router: `/src/Honua.Server.Core/Raster/Cache/RasterStorageRouter.cs`
- Cache Service: `/src/Honua.Server.Core/Raster/Cache/GdalCogCacheService.cs`

## References

- [Cloud Optimized GeoTIFF](https://www.cogeo.org/)
- [Zarr Format Specification](https://zarr.readthedocs.io/)
- Architecture Doc: `/docs/RASTER_STORAGE_ARCHITECTURE.md`

## Notes

This hybrid approach provides 95% of needed functionality with 20% of the complexity of a full raster database. The decision balances performance, cost, and implementation effort.
