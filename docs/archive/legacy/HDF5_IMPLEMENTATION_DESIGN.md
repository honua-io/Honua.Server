# HDF5 Support Implementation Design

## Overview
HDF5 (Hierarchical Data Format 5) is the primary format for:
- **NASA Earth Science** (MODIS, Landsat, Sentinel satellite data)
- **Weather/Climate Models** (high-resolution simulations)
- **Scientific Computing** (large multidimensional arrays)
- **Machine Learning** (training datasets)

## Why HDF5 Matters
- NASA's standard format (80% of Earth observation data)
- Hierarchical structure (like a filesystem inside a file)
- Massive datasets (terabytes), efficient chunked I/O
- Self-describing with rich metadata
- Compression support (gzip, szip)

## Architecture Design

### 1. NuGet Package Options

**Option A: PureHDF (Recommended)**
```xml
<PackageReference Include="PureHDF" Version="2.1.1" />
```
✅ **Pure C# - No native dependencies**
✅ Cross-platform (Linux, Windows, macOS, ARM)
✅ Modern (.NET 6+, .NET 8)
✅ Easy deployment

**Option B: HDF5-CSharp**
```xml
<PackageReference Include="HDF5-CSharp" Version="1.19.1" />
```
✅ More mature, feature-complete
✅ Active development (updated Jan 2025)
❌ Requires native HDF5 library

**Recommendation**: Start with **PureHDF** for simplicity, migrate to HDF5-CSharp if performance needed.

### 2. New HDF5 Source Provider

**File**: `src/Honua.Server.Core/Raster/Sources/Hdf5RasterSourceProvider.cs`

```csharp
using PureHDF;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Raster.Sources;

/// <summary>
/// Provider for loading HDF5 raster data (.h5, .hdf5, .he5)
/// NASA Earth Observation data (MODIS, Landsat, Sentinel)
/// </summary>
public sealed class Hdf5RasterSourceProvider : IRasterSourceProvider
{
    public string ProviderKey => "hdf5";

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        return uri.EndsWith(".h5", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith(".hdf5", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith(".he5", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith(".hdf", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        // HDF5 files are hierarchical - need dataset path
        throw new NotSupportedException(
            "HDF5 files require dataset path. Use URI format: file.h5?dataset=/MODIS/Band_1");
    }

    public async Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => OpenHdf5Dataset(uri, offset, length), cancellationToken);
    }

    private Stream OpenHdf5Dataset(string uri, long rowOffset, long? rowCount)
    {
        // Parse URI: file.h5?dataset=/MODIS/Band_1&subdataset=0
        var (filePath, datasetPath) = ParseHdf5Uri(uri);

        // Open HDF5 file
        using var file = H5File.OpenRead(filePath);

        // Navigate hierarchy to dataset
        var dataset = file.Dataset(datasetPath);

        // Read data (2D or 3D array)
        var data = ReadDataset(dataset, rowOffset, rowCount);

        // Extract geospatial metadata
        var metadata = ExtractGeospatialMetadata(file, datasetPath);

        // Convert to GeoTIFF
        return ConvertToGeoTiff(data, metadata);
    }

    private (string filePath, string datasetPath) ParseHdf5Uri(string uri)
    {
        // Parse: file.h5?dataset=/MODIS_Grid/Data_Fields/sur_refl_b01
        var parts = uri.Split('?');
        var filePath = parts[0];

        var query = System.Web.HttpUtility.ParseQueryString(parts.Length > 1 ? parts[1] : "");
        var datasetPath = query["dataset"] ?? "/"; // Default root

        return (filePath, datasetPath);
    }

    private float[,] ReadDataset(H5Dataset dataset, long rowOffset, long? rowCount)
    {
        // Get dataset dimensions
        var shape = dataset.Space.Dimensions;

        if (shape.Length == 2)
        {
            // Simple 2D raster [height, width]
            return Read2DDataset(dataset, rowOffset, rowCount);
        }
        else if (shape.Length == 3)
        {
            // 3D raster [bands, height, width] or [time, height, width]
            // Extract first band/time slice
            return Read3DDatasetSlice(dataset, 0, rowOffset, rowCount);
        }
        else
        {
            throw new NotSupportedException(
                $"Unsupported HDF5 dataset dimensions: {shape.Length}D. " +
                "Only 2D [height, width] and 3D [bands, height, width] supported.");
        }
    }

    private float[,] Read2DDataset(H5Dataset dataset, long rowOffset, long? rowCount)
    {
        var dims = dataset.Space.Dimensions;
        long height = dims[0];
        long width = dims[1];

        // Apply row offset/count for chunked reading
        long startRow = rowOffset;
        long endRow = rowCount.HasValue
            ? Math.Min(startRow + rowCount.Value, height)
            : height;

        // Read subset
        var selection = new HyperslabSelection(
            rank: 2,
            starts: new[] { startRow, 0L },
            counts: new[] { endRow - startRow, width });

        var data = dataset.Read<float>(fileSelection: selection);

        // Convert 1D array to 2D
        return ConvertTo2D(data, (int)(endRow - startRow), (int)width);
    }

    private float[,] Read3DDatasetSlice(H5Dataset dataset, int sliceIndex,
        long rowOffset, long? rowCount)
    {
        var dims = dataset.Space.Dimensions;
        long slices = dims[0];
        long height = dims[1];
        long width = dims[2];

        // Read single slice [sliceIndex, :, :]
        var selection = new HyperslabSelection(
            rank: 3,
            starts: new[] { (long)sliceIndex, rowOffset, 0L },
            counts: new[] { 1L, rowCount ?? height, width });

        var data = dataset.Read<float>(fileSelection: selection);

        return ConvertTo2D(data, (int)(rowCount ?? height), (int)width);
    }

    private float[,] ConvertTo2D(float[] data, int height, int width)
    {
        var result = new float[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[y, x] = data[y * width + x];
            }
        }
        return result;
    }

    private Hdf5GeospatialMetadata ExtractGeospatialMetadata(H5File file, string datasetPath)
    {
        var metadata = new Hdf5GeospatialMetadata();

        try
        {
            // HDF5-EOS uses StructMetadata.0 attribute
            if (file.Attributes.Exists("StructMetadata.0"))
            {
                var structMeta = file.Attribute("StructMetadata.0").Read<string>();
                metadata = ParseStructMetadata(structMeta);
            }

            // MODIS specific: Read corner coordinates
            if (file.Attributes.Exists("RANGEBEGINNINGDATE"))
            {
                // MODIS swath data
                metadata = ParseModisMetadata(file);
            }

            // Generic: Look for lat/lon datasets
            if (file.Exists("/lat") && file.Exists("/lon"))
            {
                var latData = file.Dataset("/lat").Read<float[]>();
                var lonData = file.Dataset("/lon").Read<float[]>();

                metadata.MinLat = latData.Min();
                metadata.MaxLat = latData.Max();
                metadata.MinLon = lonData.Min();
                metadata.MaxLon = lonData.Max();
            }
        }
        catch
        {
            // Fallback: Use dataset attributes
            var dataset = file.Dataset(datasetPath);
            if (dataset.Attributes.Exists("valid_range"))
            {
                var range = dataset.Attribute("valid_range").Read<float[]>();
                metadata.ValidMin = range[0];
                metadata.ValidMax = range[1];
            }
        }

        return metadata;
    }

    private Hdf5GeospatialMetadata ParseStructMetadata(string structMeta)
    {
        // Parse HDF-EOS StructMetadata ODL format
        // Example:
        // GROUP=GridStructure
        //   UpperLeftPointMtrs=(-20015109.354000,10007554.677000)
        //   LowerRightMtrs=(20015109.354000,-10007554.677000)

        var metadata = new Hdf5GeospatialMetadata();

        // Simple regex parsing (production would use proper ODL parser)
        var upperLeft = System.Text.RegularExpressions.Regex.Match(
            structMeta, @"UpperLeftPointMtrs=\(([-\d.]+),([-\d.]+)\)");
        var lowerRight = System.Text.RegularExpressions.Regex.Match(
            structMeta, @"LowerRightMtrs=\(([-\d.]+),([-\d.]+)\)");

        if (upperLeft.Success && lowerRight.Success)
        {
            metadata.MinLon = double.Parse(upperLeft.Groups[1].Value);
            metadata.MaxLat = double.Parse(upperLeft.Groups[2].Value);
            metadata.MaxLon = double.Parse(lowerRight.Groups[1].Value);
            metadata.MinLat = double.Parse(lowerRight.Groups[2].Value);
        }

        return metadata;
    }

    private Hdf5GeospatialMetadata ParseModisMetadata(H5File file)
    {
        // MODIS-specific metadata parsing
        var metadata = new Hdf5GeospatialMetadata();

        // Read bounding coordinates from MODIS attributes
        if (file.Attributes.Exists("WESTBOUNDINGCOORDINATE"))
        {
            metadata.MinLon = file.Attribute("WESTBOUNDINGCOORDINATE").Read<double>();
            metadata.MaxLon = file.Attribute("EASTBOUNDINGCOORDINATE").Read<double>();
            metadata.MinLat = file.Attribute("SOUTHBOUNDINGCOORDINATE").Read<double>();
            metadata.MaxLat = file.Attribute("NORTHBOUNDINGCOORDINATE").Read<double>();
        }

        return metadata;
    }

    private Stream ConvertToGeoTiff(float[,] data, Hdf5GeospatialMetadata metadata)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);

        var memoryStream = new MemoryStream();
        using (var tiff = BitMiracle.LibTiff.Classic.Tiff.ClientOpen(
            "memory", "w", memoryStream, new TiffStream()))
        {
            tiff.SetField(TiffTag.IMAGEWIDTH, width);
            tiff.SetField(TiffTag.IMAGELENGTH, height);
            tiff.SetField(TiffTag.BITSPERSAMPLE, 32);
            tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
            tiff.SetField(TiffTag.SAMPLEFORMAT, SampleFormat.IEEEFP); // Float32
            tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
            tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);

            // Write GeoTIFF tags
            WriteGeoTiffTags(tiff, metadata, width, height);

            // Write scanlines
            for (int row = 0; row < height; row++)
            {
                var scanline = new byte[width * 4];
                var rowData = new float[width];
                for (int col = 0; col < width; col++)
                {
                    rowData[col] = data[row, col];
                }
                Buffer.BlockCopy(rowData, 0, scanline, 0, scanline.Length);
                tiff.WriteScanline(scanline, row);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private void WriteGeoTiffTags(Tiff tiff, Hdf5GeospatialMetadata metadata,
        int width, int height)
    {
        // Calculate pixel scale
        double pixelScaleX = (metadata.MaxLon - metadata.MinLon) / width;
        double pixelScaleY = (metadata.MaxLat - metadata.MinLat) / height;

        // ModelPixelScaleTag (tag 33550)
        var pixelScale = new double[] { pixelScaleX, pixelScaleY, 0.0 };

        // ModelTiepointTag (tag 33922)
        var tiepoint = new double[] {
            0.0, 0.0, 0.0,  // Raster point (pixel, line, height)
            metadata.MinLon, metadata.MaxLat, 0.0  // World point (X, Y, Z)
        };

        // Write tags (would need proper GeoTIFF library in production)
        // For now, just document the approach
    }
}

public sealed class Hdf5GeospatialMetadata
{
    public double MinLon { get; set; }
    public double MaxLon { get; set; }
    public double MinLat { get; set; }
    public double MaxLat { get; set; }
    public float ValidMin { get; set; } = float.MinValue;
    public float ValidMax { get; set; } = float.MaxValue;
    public string? Projection { get; set; }
}
```

### 3. Configuration Extension

```csharp
public sealed class Hdf5Configuration
{
    /// <summary>
    /// HDF5 dataset path (hierarchical, e.g., /MODIS_Grid/Data_Fields/sur_refl_b01)
    /// </summary>
    public string DatasetPath { get; init; } = "/";

    /// <summary>
    /// Subdataset index (for files with multiple datasets)
    /// </summary>
    public int? SubdatasetIndex { get; init; }

    /// <summary>
    /// Band index to extract (for 3D data [bands, height, width])
    /// </summary>
    public int? BandIndex { get; init; }

    /// <summary>
    /// Scale factor to apply to raw values
    /// </summary>
    public double? ScaleFactor { get; init; }

    /// <summary>
    /// Offset to apply to raw values (scaled_value = raw * scale + offset)
    /// </summary>
    public double? Offset { get; init; }

    /// <summary>
    /// Fill value (NoData)
    /// </summary>
    public double? FillValue { get; init; }
}
```

### 4. Configuration Example

```yaml
raster_datasets:
  - id: "modis-ndvi"
    title: "MODIS NDVI 250m"
    source:
      type: "hdf5"
      uri: "s3://nasa-modis/MOD13Q1.A2025001.h09v05.061.hdf"
      hdf5_config:
        dataset_path: "/MODIS_Grid_16DAY_250m_500m_VI/Data_Fields/250m_16_days_NDVI"
        scale_factor: 0.0001
        fill_value: -3000
    extent:
      bbox: [[-120, 30, -110, 40]]
      crs: "EPSG:4326"

  - id: "landsat-band4"
    title: "Landsat 8 NIR Band"
    source:
      type: "hdf5"
      uri: "/data/landsat/LC08_L1TP_042034_20250114.h5"
      hdf5_config:
        dataset_path: "/LC08/Band4"
        band_index: 0
```

## Real-World Examples

### NASA MODIS Data
```bash
# MODIS Surface Reflectance
curl -X POST https://honua.io/raster/analytics/algebra \
  -d '{
    "datasetIds": ["modis-nir-hdf5", "modis-red-hdf5"],
    "expression": "ndvi",
    "boundingBox": [-120, 35, -119, 36]
  }'
```

### Landsat 8 Analysis
```bash
# Calculate EVI from Landsat HDF5
curl -X POST https://honua.io/raster/analytics/algebra \
  -d '{
    "datasetIds": ["landsat-nir", "landsat-red", "landsat-blue"],
    "expression": "evi",
    "boundingBox": [-122, 37, -121, 38]
  }'
```

## HDF5 Structure Examples

### MODIS MOD13Q1 (Vegetation Indices)
```
/MODIS_Grid_16DAY_250m_500m_VI/
  ├── Data_Fields/
  │   ├── 250m_16_days_NDVI          [2D array: 4800 x 4800]
  │   ├── 250m_16_days_EVI           [2D array: 4800 x 4800]
  │   ├── 250m_16_days_pixel_reliability
  │   └── 250m_16_days_VI_Quality
  └── Geolocation_Fields/
      ├── lat
      └── lon
```

### NASA GPM (Precipitation)
```
/Grid/
  ├── precipitationCal              [3D: time x lat x lon]
  ├── precipitationUncal
  ├── lat                           [1D coordinate]
  ├── lon                           [1D coordinate]
  └── time                          [1D coordinate]
```

## Implementation Checklist

### Phase 1: Basic Support (3-5 days)
- [ ] Add PureHDF NuGet package
- [ ] Create Hdf5RasterSourceProvider
- [ ] Implement 2D dataset reading
- [ ] Basic metadata extraction
- [ ] Convert to GeoTIFF
- [ ] Unit tests with sample HDF5 files

### Phase 2: NASA Formats (5-7 days)
- [ ] HDF-EOS StructMetadata parsing
- [ ] MODIS swath/grid support
- [ ] Landsat HDF5 format
- [ ] Sentinel HDF5 format
- [ ] Scale factor/offset application
- [ ] Fill value (NoData) handling

### Phase 3: Advanced Features (5-7 days)
- [ ] 3D data (multi-band extraction)
- [ ] Hierarchical navigation
- [ ] Chunked reading for large files
- [ ] Compression support
- [ ] Attribute metadata propagation
- [ ] Performance optimization

**Total**: ~2-3 weeks for production-ready

## Challenges

### 1. **Hierarchical Structure**
HDF5 is like a filesystem - datasets at different paths
- **Solution**: URI query parameters specify dataset path

### 2. **Scale/Offset**
NASA data uses scaled integers: `value = raw * scale + offset`
- **Solution**: Apply transformations during GeoTIFF conversion

### 3. **Coordinate Systems**
Various conventions (HDF-EOS, CF, MODIS-specific)
- **Solution**: Format-specific metadata parsers

### 4. **Large Files**
MODIS files can be 500MB+, 10k x 10k pixels
- **Solution**: Chunked reading with row offset/count

## Testing Strategy

### Sample Data Sources
```bash
# NASA EarthData
wget https://e4ftl01.cr.usgs.gov/MOLT/MOD13Q1.061/MOD13Q1.A2025001.h09v05.061.hdf

# Landsat Collection 2
aws s3 cp s3://usgs-landsat/collection02/level-1/...

# Test files
- MODIS MOD13Q1 (NDVI)
- Landsat 8 OLI
- Sentinel-2 L2A
```

### Unit Tests
```csharp
[Fact]
public async Task Hdf5Provider_ShouldReadModisNDVI()
{
    var uri = "test-data/MOD13Q1.hdf?dataset=/MODIS_Grid/Data_Fields/250m_16_days_NDVI";
    var provider = new Hdf5RasterSourceProvider();

    var stream = await provider.OpenReadRangeAsync(uri, 0, null);

    stream.Should().NotBeNull();
    // Verify it's valid GeoTIFF with NDVI values
}
```

## Deployment Considerations

**Pure C# (PureHDF)**:
- ✅ No native dependencies
- ✅ xcopy deployment
- ✅ Docker-friendly

**Native (HDF5-CSharp)**:
- ❌ Requires HDF5 native libraries
- ❌ Platform-specific binaries
- ✅ Better performance

## Estimated Effort

- **Basic HDF5 Support**: 3-5 days
- **NASA Format Support**: 5-7 days
- **Production Ready**: 2-3 days

**Total**: ~2-3 weeks

## Why This Approach Works

✅ **Same architecture as NetCDF**
✅ **Convert to GeoTIFF internally**
✅ **All analytics work unchanged**
✅ **Consistent API**

The HDF5 provider handles the complexity, the rest of the system just sees GeoTIFF!
