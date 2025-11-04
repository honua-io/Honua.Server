# GRIB2 Support Implementation Design

## Overview
GRIB2 (General Regularly-distributed Information in Binary form, Edition 2) is the standard for:
- **Weather Forecasts** (NOAA, ECMWF, NCEP, MetOffice)
- **Numerical Weather Prediction** (GFS, NAM, HRRR models)
- **Climate Reanalysis** (ERA5, NCEP/NCAR)
- **Operational Meteorology** (real-time weather data)

## Why GRIB2 Matters
- **WMO Standard** - World Meteorological Organization format
- **Compact** - Highly compressed (10-50x smaller than NetCDF)
- **Fast Access** - Indexed for rapid parameter extraction
- **Real-time** - Updated every 1-6 hours by weather agencies
- **Multi-parameter** - Temperature, pressure, wind, precipitation in one file

## Architecture Design

### 1. NuGet Package

```xml
<PackageReference Include="Grib.Api" Version="1.0.0-beta4" />
```

**Key Details:**
- Wraps ECMWF's **ecCodes** C library (successor to GRIB-API)
- Supports GRIB1 and GRIB2
- Requires **MSVC 2015 redistributable**
- Apache License 2.0
- Thread-safe

**Native Dependencies:**
- Windows: `eccodes.dll`
- Linux: `libeccodes.so`
- macOS: `libeccodes.dylib`

### 2. New GRIB2 Source Provider

**File**: `src/Honua.Server.Core/Raster/Sources/Grib2RasterSourceProvider.cs`

```csharp
using Grib.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Raster.Sources;

/// <summary>
/// Provider for loading GRIB2 weather forecast data (.grib, .grb, .grib2)
/// NOAA GFS, ECMWF, NCEP models
/// </summary>
public sealed class Grib2RasterSourceProvider : IRasterSourceProvider
{
    public string ProviderKey => "grib2";

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        return uri.EndsWith(".grib", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith(".grb", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith(".grib2", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith(".grb2", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        // GRIB2 files contain multiple messages (parameters)
        throw new NotSupportedException(
            "GRIB2 files contain multiple parameters. " +
            "Use URI format: file.grib2?parameter=Temperature&level=500");
    }

    public async Task<Stream> OpenReadRangeAsync(string uri, long messageIndex, long? length = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => OpenGrib2Message(uri, (int)messageIndex), cancellationToken);
    }

    private Stream OpenGrib2Message(string uri, int messageIndex)
    {
        // Parse URI: file.grib2?parameter=Temperature&level=500&forecastTime=0
        var config = ParseGrib2Uri(uri);

        using var gribFile = new GribFile(config.FilePath);

        // Find matching message
        var message = FindMessage(gribFile, config);

        if (message == null)
        {
            throw new InvalidOperationException(
                $"GRIB2 message not found: parameter={config.Parameter}, " +
                $"level={config.Level}, forecastTime={config.ForecastTime}");
        }

        // Extract grid data
        var gridData = ExtractGridData(message);

        // Extract geospatial metadata
        var metadata = ExtractGeospatialMetadata(message);

        // Convert to GeoTIFF
        return ConvertToGeoTiff(gridData, metadata);
    }

    private Grib2Configuration ParseGrib2Uri(string uri)
    {
        var parts = uri.Split('?');
        var config = new Grib2Configuration { FilePath = parts[0] };

        if (parts.Length > 1)
        {
            var query = System.Web.HttpUtility.ParseQueryString(parts[1]);

            config.Parameter = query["parameter"] ?? query["shortName"];
            config.Level = query["level"] != null ? double.Parse(query["level"]) : null;
            config.LevelType = query["levelType"] ?? "isobaricInhPa";
            config.ForecastTime = query["forecastTime"] != null
                ? int.Parse(query["forecastTime"])
                : 0;
            config.MessageIndex = query["messageIndex"] != null
                ? int.Parse(query["messageIndex"])
                : null;
        }

        return config;
    }

    private GribMessage? FindMessage(GribFile gribFile, Grib2Configuration config)
    {
        // If message index specified, use it directly
        if (config.MessageIndex.HasValue)
        {
            return gribFile.GetMessage(config.MessageIndex.Value);
        }

        // Otherwise, search by parameter/level/time
        foreach (var message in gribFile.Messages)
        {
            var shortName = message.GetString("shortName");
            var levelType = message.GetString("typeOfLevel");
            var level = message.GetDouble("level");
            var forecastTime = message.GetLong("forecastTime");

            bool matches = true;

            if (config.Parameter != null && shortName != config.Parameter)
                matches = false;

            if (config.LevelType != null && levelType != config.LevelType)
                matches = false;

            if (config.Level.HasValue && Math.Abs(level - config.Level.Value) > 0.01)
                matches = false;

            if (forecastTime != config.ForecastTime)
                matches = false;

            if (matches)
                return message;
        }

        return null;
    }

    private Grib2GridData ExtractGridData(GribMessage message)
    {
        // Get grid dimensions
        var ni = message.GetLong("Ni"); // Number of points along X
        var nj = message.GetLong("Nj"); // Number of points along Y

        // Get data values
        var values = message.GetDoubleArray("values");

        // Get missing value
        var missingValue = message.TryGetDouble("missingValue", out var mv) ? mv : double.NaN;

        // Convert to 2D array
        var data = new double[nj, ni];
        for (long j = 0; j < nj; j++)
        {
            for (long i = 0; i < ni; i++)
            {
                var index = j * ni + i;
                data[j, i] = values[index];
            }
        }

        return new Grib2GridData
        {
            Data = data,
            Width = (int)ni,
            Height = (int)nj,
            MissingValue = missingValue,
            Parameter = message.GetString("shortName"),
            Units = message.GetString("units"),
            Level = message.GetDouble("level"),
            LevelType = message.GetString("typeOfLevel"),
            ForecastTime = message.GetLong("forecastTime")
        };
    }

    private Grib2GeospatialMetadata ExtractGeospatialMetadata(GribMessage message)
    {
        var metadata = new Grib2GeospatialMetadata();

        // Grid type (regular lat/lon, Lambert, polar stereographic, etc.)
        var gridType = message.GetString("gridType");

        if (gridType == "regular_ll")
        {
            // Regular lat/lon grid
            metadata.MinLat = message.GetDouble("latitudeOfFirstGridPointInDegrees");
            metadata.MaxLat = message.GetDouble("latitudeOfLastGridPointInDegrees");
            metadata.MinLon = message.GetDouble("longitudeOfFirstGridPointInDegrees");
            metadata.MaxLon = message.GetDouble("longitudeOfLastGridPointInDegrees");

            metadata.LatSpacing = message.GetDouble("jDirectionIncrementInDegrees");
            metadata.LonSpacing = message.GetDouble("iDirectionIncrementInDegrees");
        }
        else if (gridType == "lambert")
        {
            // Lambert Conformal projection
            metadata.ProjectionType = "lambert_conformal_conic";
            metadata.StandardParallel1 = message.GetDouble("Latin1InDegrees");
            metadata.StandardParallel2 = message.GetDouble("Latin2InDegrees");
            metadata.CentralMeridian = message.GetDouble("LoVInDegrees");
            metadata.OriginLat = message.GetDouble("LaDInDegrees");
        }
        else if (gridType == "polar_stereographic")
        {
            metadata.ProjectionType = "polar_stereographic";
            metadata.CentralMeridian = message.GetDouble("orientationOfTheGridInDegrees");
        }

        // Scanning mode (determines if data is top-to-bottom or bottom-to-top)
        var scanningMode = message.GetLong("scanningMode");
        metadata.ScanNegatively = (scanningMode & 0x40) != 0;

        return metadata;
    }

    private Stream ConvertToGeoTiff(Grib2GridData gridData, Grib2GeospatialMetadata metadata)
    {
        int width = gridData.Width;
        int height = gridData.Height;

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

            // Write GeoTIFF tags based on projection
            WriteGeoTiffTags(tiff, metadata, width, height);

            // Write scanlines
            for (int row = 0; row < height; row++)
            {
                var scanline = new byte[width * 4];
                var rowData = new float[width];

                // Handle scanning mode (top-to-bottom vs bottom-to-top)
                int sourceRow = metadata.ScanNegatively ? (height - 1 - row) : row;

                for (int col = 0; col < width; col++)
                {
                    var value = gridData.Data[sourceRow, col];

                    // Replace missing values with NaN
                    if (!double.IsNaN(gridData.MissingValue) &&
                        Math.Abs(value - gridData.MissingValue) < 0.001)
                    {
                        rowData[col] = float.NaN;
                    }
                    else
                    {
                        rowData[col] = (float)value;
                    }
                }

                Buffer.BlockCopy(rowData, 0, scanline, 0, scanline.Length);
                tiff.WriteScanline(scanline, row);
            }

            // Write TIFF tags for units and parameter info
            WriteMetadataTags(tiff, gridData);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private void WriteGeoTiffTags(Tiff tiff, Grib2GeospatialMetadata metadata,
        int width, int height)
    {
        if (metadata.ProjectionType == null || metadata.ProjectionType == "regular_ll")
        {
            // Regular lat/lon (EPSG:4326)
            double pixelScaleX = metadata.LonSpacing;
            double pixelScaleY = metadata.LatSpacing;

            // ModelPixelScaleTag
            var pixelScale = new double[] { pixelScaleX, pixelScaleY, 0.0 };

            // ModelTiepointTag
            var tiepoint = new double[] {
                0.0, 0.0, 0.0,
                metadata.MinLon, metadata.MaxLat, 0.0
            };

            // GeoTIFF tags (would use proper GeoTIFF library in production)
            // Tag 33550 = ModelPixelScaleTag
            // Tag 33922 = ModelTiepointTag
        }
        else
        {
            // Projected coordinate system (Lambert, Polar Stereographic)
            // Would write appropriate projection parameters
        }
    }

    private void WriteMetadataTags(Tiff tiff, Grib2GridData gridData)
    {
        // Write TIFF description with GRIB2 metadata
        var description = $"Parameter: {gridData.Parameter}, " +
                         $"Units: {gridData.Units}, " +
                         $"Level: {gridData.Level} {gridData.LevelType}, " +
                         $"Forecast: +{gridData.ForecastTime} hours";

        tiff.SetField(TiffTag.IMAGEDESCRIPTION, description);
    }
}

public sealed class Grib2Configuration
{
    public string FilePath { get; set; } = "";
    public string? Parameter { get; set; }        // e.g., "t" (temperature), "gh" (geopotential height)
    public string? LevelType { get; set; }        // e.g., "isobaricInhPa", "surface"
    public double? Level { get; set; }            // e.g., 500 (mb), 2 (m above ground)
    public int ForecastTime { get; set; } = 0;    // Hours from reference time
    public int? MessageIndex { get; set; }        // Direct message index
}

public sealed class Grib2GridData
{
    public double[,] Data { get; set; } = new double[0, 0];
    public int Width { get; set; }
    public int Height { get; set; }
    public double MissingValue { get; set; }
    public string Parameter { get; set; } = "";
    public string Units { get; set; } = "";
    public double Level { get; set; }
    public string LevelType { get; set; } = "";
    public long ForecastTime { get; set; }
}

public sealed class Grib2GeospatialMetadata
{
    public double MinLat { get; set; }
    public double MaxLat { get; set; }
    public double MinLon { get; set; }
    public double MaxLon { get; set; }
    public double LatSpacing { get; set; }
    public double LonSpacing { get; set; }
    public string? ProjectionType { get; set; }
    public double? StandardParallel1 { get; set; }
    public double? StandardParallel2 { get; set; }
    public double? CentralMeridian { get; set; }
    public double? OriginLat { get; set; }
    public bool ScanNegatively { get; set; }
}
```

### 3. Configuration Extension

```csharp
public sealed class RasterDatasetDefinition
{
    // ... existing properties

    /// <summary>
    /// GRIB2-specific configuration
    /// </summary>
    public Grib2MetadataConfiguration? Grib2Config { get; init; }
}

public sealed class Grib2MetadataConfiguration
{
    /// <summary>
    /// Parameter short name (e.g., "t" for temperature, "r" for humidity)
    /// </summary>
    public string Parameter { get; init; } = "t";

    /// <summary>
    /// Level type (e.g., "isobaricInhPa", "surface", "heightAboveGround")
    /// </summary>
    public string LevelType { get; init; } = "isobaricInhPa";

    /// <summary>
    /// Level value (e.g., 500 for 500mb, 2 for 2m above ground)
    /// </summary>
    public double Level { get; init; }

    /// <summary>
    /// Forecast hour from reference time
    /// </summary>
    public int ForecastHour { get; init; } = 0;
}
```

### 4. Configuration Example

```yaml
raster_datasets:
  - id: "gfs-temperature-500mb"
    title: "GFS Temperature at 500mb"
    source:
      type: "grib2"
      uri: "s3://noaa-gfs/gfs.20250114/00/gfs.t00z.pgrb2.0p25.f000"
      grib2_config:
        parameter: "t"               # Temperature
        level_type: "isobaricInhPa"  # Pressure level
        level: 500                   # 500 millibars
        forecast_hour: 0
    extent:
      bbox: [[-180, -90, 180, 90]]
      crs: "EPSG:4326"

  - id: "gfs-surface-temp"
    title: "GFS 2m Temperature"
    source:
      type: "grib2"
      uri: "/data/weather/gfs.grb2"
      grib2_config:
        parameter: "t"
        level_type: "heightAboveGround"
        level: 2                     # 2 meters
        forecast_hour: 6
```

## Real-World Examples

### NOAA GFS Temperature Analysis
```bash
# Get 500mb temperature
curl -X POST https://honua.io/raster/analytics/statistics \
  -d '{
    "datasetId": "gfs-temperature-500mb",
    "boundingBox": [-130, 20, -60, 50]
  }'
```

### Calculate Temperature Gradient
```bash
# Slope/aspect of temperature field
curl -X POST https://honua.io/raster/analytics/terrain \
  -d '{
    "elevationDatasetId": "gfs-temperature-500mb",
    "analysisType": "Slope",
    "format": "png"
  }'
```

## GRIB2 Parameter Names

### Common Parameters
| Short Name | Description | Units |
|------------|-------------|-------|
| `t` | Temperature | K |
| `r` | Relative Humidity | % |
| `gh` | Geopotential Height | gpm |
| `u`, `v` | Wind Components | m/s |
| `prate` | Precipitation Rate | kg/m²/s |
| `tcc` | Total Cloud Cover | % |
| `prmsl` | Pressure at MSL | Pa |

### Level Types
- `surface` - Ground level
- `isobaricInhPa` - Pressure levels (1000, 925, 850, 700, 500, 250 mb)
- `heightAboveGround` - Fixed height (2m, 10m)
- `meanSea` - Mean sea level

## Implementation Checklist

### Phase 1: Basic Support (3-5 days)
- [ ] Add Grib.Api NuGet package
- [ ] Create Grib2RasterSourceProvider
- [ ] Implement message search by parameter/level
- [ ] Regular lat/lon grid support
- [ ] Convert to GeoTIFF
- [ ] Unit tests with sample GRIB2 files

### Phase 2: Advanced Grids (5-7 days)
- [ ] Lambert Conformal projection
- [ ] Polar Stereographic projection
- [ ] Gaussian grids
- [ ] Scanning mode handling
- [ ] Missing value handling
- [ ] Multi-message files

### Phase 3: Production Features (3-5 days)
- [ ] Index file (.idx) support for fast access
- [ ] Time-series extraction
- [ ] Ensemble forecasts
- [ ] Performance optimization
- [ ] Caching

**Total**: ~2-3 weeks for production-ready

## Challenges

### 1. **Multiple Messages**
One GRIB2 file = 100+ messages (parameters × levels × times)
- **Solution**: URI query parameters specify which message

### 2. **Compressed Data**
GRIB2 uses JPEG2000, complex packing
- **Solution**: ecCodes library handles decompression

### 3. **Projection Variants**
Lambert, Polar Stereographic, Gaussian, etc.
- **Solution**: Extract projection parameters, write GeoTIFF tags

### 4. **Missing Values**
Various encoding schemes for missing data
- **Solution**: Use ecCodes API to get decoded values

## Testing Strategy

### Sample Data Sources
```bash
# NOAA GFS (0.25 degree)
aws s3 cp s3://noaa-gfs-bdp-pds/gfs.20250114/00/atmos/gfs.t00z.pgrb2.0p25.f000 .

# NOAA HRRR (3km)
wget https://noaa-hrrr-bdp-pds.s3.amazonaws.com/hrrr.20250114/conus/hrrr.t00z.wrfsfcf00.grib2

# ECMWF ERA5
# Requires Copernicus account
```

### Unit Tests
```csharp
[Fact]
public async Task Grib2Provider_ShouldExtractTemperature()
{
    var uri = "test-data/gfs.grb2?parameter=t&level=500&levelType=isobaricInhPa";
    var provider = new Grib2RasterSourceProvider();

    var stream = await provider.OpenReadRangeAsync(uri, 0, null);

    stream.Should().NotBeNull();
    // Verify GeoTIFF with temperature values
}
```

## Deployment Considerations

**Native Dependencies Required**:
- Windows: `eccodes.dll` + MSVC 2015 Runtime
- Linux: `libeccodes.so`
- Docker: Include in base image

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y libeccodes0
COPY --from=build /app/publish .
```

## Estimated Effort

- **Basic GRIB2 Support**: 3-5 days
- **Advanced Projections**: 5-7 days
- **Production Ready**: 3-5 days

**Total**: ~2-3 weeks

## Why This Approach Works

✅ **Same pattern as NetCDF/HDF5**
✅ **Convert to GeoTIFF internally**
✅ **All analytics unchanged**
✅ **Handles weather forecast complexity**

GRIB2 is the most complex format, but the conversion approach keeps everything else simple!
