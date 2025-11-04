# NetCDF Support Implementation Design

## Overview
NetCDF (Network Common Data Form) is a critical format for:
- **Climate/Weather data** (NOAA, NASA, NCAR)
- **Oceanography** (sea surface temperature, salinity)
- **Atmospheric science** (temperature, pressure, wind)
- **Multi-dimensional arrays** with metadata

## Why NetCDF Matters
- Standard format for scientific data (NASA, NOAA, ECMWF)
- Self-describing (includes metadata, units, coordinates)
- Efficient storage for time-series and 3D/4D data
- CF (Climate and Forecast) conventions support

## Architecture Design

### 1. NuGet Package
```xml
<PackageReference Include="SDSLite" Version="3.0.1" />
```
**Note**: Requires native `netcdf.dll` from Unidata

### 2. New NetCDF Source Provider

**File**: `src/Honua.Server.Core/Raster/Sources/NetCdfRasterSourceProvider.cs`

```csharp
using Microsoft.Research.Science.Data;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Raster.Sources;

/// <summary>
/// Provider for loading NetCDF raster data (.nc, .nc4)
/// </summary>
public sealed class NetCdfRasterSourceProvider : IRasterSourceProvider
{
    public string ProviderKey => "netcdf";

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        return uri.EndsWith(".nc", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith(".nc4", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith(".netcdf", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        // NetCDF files need special handling - can't return raw stream
        // Need to extract specific variable as raster band
        throw new NotSupportedException(
            "NetCDF files require variable name. Use OpenReadRangeAsync with variable metadata.");
    }

    public Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null,
        CancellationToken cancellationToken = default)
    {
        // For COG-like access to specific time slices or layers
        return Task.FromResult<Stream>(OpenNetCdfVariable(uri, offset, length));
    }

    private Stream OpenNetCdfVariable(string uri, long timeIndex, long? bandIndex)
    {
        // Open NetCDF and extract specific variable/time slice
        using var dataset = DataSet.Open($"msds:nc?file={uri}&openMode=readOnly");

        // Get variable (e.g., "temperature", "precipitation")
        var variableName = ExtractVariableFromUri(uri);
        var data = dataset.GetData<float[,,]>(variableName); // [time, lat, lon]

        // Extract specific time slice
        var slice = ExtractTimeSlice(data, (int)timeIndex);

        // Convert to GeoTIFF in memory for compatibility
        return ConvertToGeoTiff(slice, dataset);
    }

    private string ExtractVariableFromUri(string uri)
    {
        // Parse URI like: file.nc?variable=temperature&time=0
        var query = new Uri(uri).Query;
        // ... parse logic
        return "temperature"; // default
    }

    private float[,] ExtractTimeSlice(float[,,] data, int timeIndex)
    {
        int latCount = data.GetLength(1);
        int lonCount = data.GetLength(2);
        var slice = new float[latCount, lonCount];

        for (int lat = 0; lat < latCount; lat++)
        {
            for (int lon = 0; lon < lonCount; lon++)
            {
                slice[lat, lon] = data[timeIndex, lat, lon];
            }
        }

        return slice;
    }

    private Stream ConvertToGeoTiff(float[,] data, DataSet metadata)
    {
        // Extract coordinate info from NetCDF
        var lat = metadata.GetData<float[]>("lat");
        var lon = metadata.GetData<float[]>("lon");

        // Create in-memory GeoTIFF
        var memoryStream = new MemoryStream();
        using (var tiff = BitMiracle.LibTiff.Classic.Tiff.ClientOpen(
            "memory", "w", memoryStream, new TiffStream()))
        {
            int width = data.GetLength(1);
            int height = data.GetLength(0);

            tiff.SetField(TiffTag.IMAGEWIDTH, width);
            tiff.SetField(TiffTag.IMAGELENGTH, height);
            tiff.SetField(TiffTag.BITSPERSAMPLE, 32);
            tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
            tiff.SetField(TiffTag.SAMPLEFORMAT, SampleFormat.IEEEFP); // Float
            tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);

            // Write geospatial metadata
            WriteGeoTiffTags(tiff, lat, lon);

            // Write pixel data
            for (int row = 0; row < height; row++)
            {
                var scanline = new byte[width * 4]; // 4 bytes per float
                Buffer.BlockCopy(GetRow(data, row), 0, scanline, 0, scanline.Length);
                tiff.WriteScanline(scanline, row);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private void WriteGeoTiffTags(Tiff tiff, float[] lat, float[] lon)
    {
        // Calculate extent from coordinate arrays
        double minLon = lon.Min();
        double maxLon = lon.Max();
        double minLat = lat.Min();
        double maxLat = lat.Max();

        // GeoTIFF tags for georeferencing
        // ModelPixelScaleTag
        double pixelScaleX = (maxLon - minLon) / lon.Length;
        double pixelScaleY = (maxLat - minLat) / lat.Length;

        // ModelTiepointTag
        // ... GeoTIFF tag writing logic
    }

    private float[] GetRow(float[,] data, int row)
    {
        int cols = data.GetLength(1);
        var result = new float[cols];
        for (int col = 0; col < cols; col++)
        {
            result[col] = data[row, col];
        }
        return result;
    }
}
```

### 3. Metadata Definition Extension

**File**: `src/Honua.Server.Core/Metadata/RasterDatasetDefinition.cs` (modify)

```csharp
public sealed class RasterDatasetDefinition
{
    // ... existing properties

    /// <summary>
    /// NetCDF-specific configuration
    /// </summary>
    public NetCdfConfiguration? NetCdfConfig { get; init; }
}

public sealed class NetCdfConfiguration
{
    /// <summary>
    /// Variable name to extract (e.g., "temperature", "precipitation")
    /// </summary>
    public string VariableName { get; init; } = "data";

    /// <summary>
    /// Time index to extract (for time-series data)
    /// </summary>
    public int? TimeIndex { get; init; }

    /// <summary>
    /// Time value to extract (ISO 8601 timestamp)
    /// </summary>
    public DateTime? TimeValue { get; init; }

    /// <summary>
    /// Vertical level to extract (for 3D data, e.g., pressure level)
    /// </summary>
    public double? Level { get; init; }

    /// <summary>
    /// Standard name from CF conventions (e.g., "air_temperature")
    /// </summary>
    public string? StandardName { get; init; }
}
```

### 4. Configuration Example

```yaml
raster_datasets:
  - id: "noaa-sst"
    title: "NOAA Sea Surface Temperature"
    source:
      type: "netcdf"
      uri: "s3://noaa-data/sst/20250114.nc"
      netcdf_config:
        variable_name: "sst"
        time_index: 0
        standard_name: "sea_surface_temperature"
    extent:
      bbox: [[-180, -90, 180, 90]]
      crs: "EPSG:4326"

  - id: "nasa-temperature"
    title: "NASA GISS Temperature Anomaly"
    source:
      type: "netcdf"
      uri: "/data/climate/gistemp_1200km_v4.nc"
      netcdf_config:
        variable_name: "tempanomaly"
        time_value: "2025-01-01T00:00:00Z"
    extent:
      bbox: [[-180, -90, 180, 90]]
      crs: "EPSG:4326"
```

### 5. API Usage

**Calculate statistics on NetCDF data:**
```bash
curl -X POST https://honua.io/raster/analytics/statistics \
  -H "Content-Type: application/json" \
  -d '{
    "datasetId": "noaa-sst",
    "boundingBox": [-125, 30, -115, 40],
    "bandIndex": 0
  }'
```

**NDVI on NetCDF bands:**
```bash
curl -X POST https://honua.io/raster/analytics/algebra \
  -H "Content-Type: application/json" \
  -d '{
    "datasetIds": ["modis-nir-netcdf", "modis-red-netcdf"],
    "expression": "ndvi",
    "boundingBox": [-120, 35, -119, 36],
    "width": 1024,
    "height": 1024
  }'
```

## Implementation Checklist

### Phase 1: Basic Support
- [ ] Add SDSLite NuGet package
- [ ] Create NetCdfRasterSourceProvider
- [ ] Add NetCdfConfiguration to metadata
- [ ] Implement variable extraction
- [ ] Convert NetCDF slices to in-memory GeoTIFF
- [ ] Unit tests with sample NetCDF files

### Phase 2: Advanced Features
- [ ] Time-series support (extract by timestamp)
- [ ] 3D/4D data support (vertical levels)
- [ ] CF conventions metadata parsing
- [ ] Automatic coordinate detection (lat/lon/time)
- [ ] Multi-variable support
- [ ] Caching of converted GeoTIFF slices

### Phase 3: Optimization
- [ ] Stream processing for large files
- [ ] Parallel time-slice extraction
- [ ] OPeNDAP remote access support
- [ ] THREDDS Data Server integration
- [ ] Zarr format support (cloud-optimized NetCDF)

## Challenges

### 1. **Native Dependencies**
NetCDF requires `netcdf.dll` (Windows) or `libnetcdf.so` (Linux)
- **Solution**: Docker images, deployment docs, pre-compiled binaries

### 2. **Multi-dimensional Data**
NetCDF is [time, level, lat, lon] - need to flatten to 2D
- **Solution**: Metadata config specifies which dimensions to extract

### 3. **Performance**
Large NetCDF files (100GB+) can be slow
- **Solution**: Cache converted GeoTIFF slices, use COG for output

### 4. **Coordinate Systems**
NetCDF uses various conventions (CF, COARDS)
- **Solution**: Auto-detect conventions, fallback to manual config

## Testing Strategy

### Sample NetCDF Files
```bash
# Download sample data
wget ftp://ftp.cdc.noaa.gov/Datasets/ncep.reanalysis/surface/air.2m.mon.mean.nc

# Test datasets
- NOAA NCEP Reanalysis (temperature, pressure)
- NASA MODIS (vegetation indices)
- ECMWF ERA5 (climate reanalysis)
```

### Unit Tests
```csharp
[Fact]
public async Task NetCdfProvider_ShouldExtractVariable()
{
    var provider = new NetCdfRasterSourceProvider();
    var uri = "test-data/sample.nc?variable=temperature&time=0";

    var stream = await provider.OpenReadRangeAsync(uri, 0, null);

    stream.Should().NotBeNull();
    // Verify it's a valid GeoTIFF
}

[Fact]
public async Task NetCdfAnalytics_ShouldCalculateStatistics()
{
    var dataset = new RasterDatasetDefinition
    {
        Id = "sst",
        Source = new RasterSourceDefinition
        {
            Type = "netcdf",
            Uri = "sample-sst.nc"
        },
        NetCdfConfig = new NetCdfConfiguration
        {
            VariableName = "sst",
            TimeIndex = 0
        }
    };

    var request = new RasterStatisticsRequest(dataset, null, null);
    var result = await _analyticsService.CalculateStatisticsAsync(request);

    result.Bands.Should().HaveCount(1);
    result.Bands[0].Mean.Should().BeInRange(-2, 35); // SST in Celsius
}
```

## Estimated Effort

- **Basic NetCDF Support**: 3-5 days
  - Provider implementation: 1 day
  - Variable extraction: 1 day
  - GeoTIFF conversion: 1 day
  - Tests: 1 day
  - Documentation: 0.5 days

- **Advanced Features**: 5-7 days
  - Time-series: 2 days
  - CF conventions: 2 days
  - Optimization: 2 days
  - Integration tests: 1 day

- **Production Ready**: 2-3 days
  - Docker/deployment: 1 day
  - Performance tuning: 1 day
  - Documentation: 1 day

**Total**: ~2-3 weeks for full production-ready NetCDF support

## Alternative: GDAL Approach

Instead of SDSLite, could use GDAL (which supports NetCDF):

```csharp
// GDAL already supports NetCDF via drivers
var dataset = Gdal.Open("NETCDF:file.nc:temperature", Access.GA_ReadOnly);
```

**Pros**:
- More mature, widely used
- Handles many formats
- Better performance

**Cons**:
- Larger native dependencies
- More complex deployment
- Heavier weight

## Recommendation

**Start with SDSLite** for simplicity, then migrate to GDAL if needed for production workloads.

The conversion-to-GeoTIFF approach keeps the rest of the analytics pipeline unchanged!
