// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BitMiracle.LibTiff.Classic;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// Parser for GeoTIFF tags (geospatial metadata).
/// Implements GeoTIFF specification for extracting coordinate system and georeferencing information.
/// </summary>
public sealed class GeoTiffTagParser
{
    private readonly ILogger<GeoTiffTagParser> _logger;

    // GeoTIFF tag definitions (not in standard LibTiff)
    private const TiffTag TIFFTAG_GEOKEYDIRECTORY = (TiffTag)34735;
    private const TiffTag TIFFTAG_GEODOUBLEPARAMS = (TiffTag)34736;
    private const TiffTag TIFFTAG_GEOASCIIPARAMS = (TiffTag)34737;
    private const TiffTag TIFFTAG_GDAL_METADATA = (TiffTag)42112;
    private const TiffTag TIFFTAG_GDAL_NODATA = (TiffTag)42113;
    private const TiffTag TIFFTAG_MODELTIEPOINTTAG = (TiffTag)33922;
    private const TiffTag TIFFTAG_MODELPIXELSCALETAG = (TiffTag)33550;
    private const TiffTag TIFFTAG_MODELTRANSFORMATIONTAG = (TiffTag)34264;

    // GeoKey codes
    private const int GTModelTypeGeoKey = 1024;
    private const int GTRasterTypeGeoKey = 1025;
    private const int GTCitationGeoKey = 1026;
    private const int GeographicTypeGeoKey = 2048;
    private const int GeogCitationGeoKey = 2049;
    private const int ProjectedCSTypeGeoKey = 3072;
    private const int PCSCitationGeoKey = 3073;

    public GeoTiffTagParser(ILogger<GeoTiffTagParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Extract geospatial metadata from a TIFF file.
    /// </summary>
    public (GeoTransform? geoTransform, string? projectionWkt) ParseGeoTags(Tiff tiff)
    {
        try
        {
            var geoTransform = ExtractGeoTransform(tiff);
            var projectionWkt = ExtractProjection(tiff);

            return (geoTransform, projectionWkt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse GeoTIFF tags, geospatial metadata may be incomplete");
            return (null, null);
        }
    }

    /// <summary>
    /// Extract geo-transform from ModelTiepointTag and ModelPixelScaleTag or ModelTransformationTag.
    /// </summary>
    private GeoTransform? ExtractGeoTransform(Tiff tiff)
    {
        // Try ModelTransformationTag first (preferred for complex transforms)
        var transformField = tiff.GetField(TIFFTAG_MODELTRANSFORMATIONTAG);
        if (transformField != null && transformField.Length > 0)
        {
            var transform = transformField[1].ToDoubleArray();
            if (transform != null && transform.Length >= 16)
            {
                // 4x4 transformation matrix
                return new GeoTransform
                {
                    OriginX = transform[3],
                    PixelSizeX = transform[0],
                    RotationX = transform[1],
                    OriginY = transform[7],
                    RotationY = transform[4],
                    PixelSizeY = transform[5]
                };
            }
        }

        // Fallback to ModelTiepointTag + ModelPixelScaleTag (most common)
        var tiepointField = tiff.GetField(TIFFTAG_MODELTIEPOINTTAG);
        var pixelScaleField = tiff.GetField(TIFFTAG_MODELPIXELSCALETAG);

        if (tiepointField != null && pixelScaleField != null &&
            tiepointField.Length > 0 && pixelScaleField.Length > 0)
        {
            var tiepoints = tiepointField[1].ToDoubleArray();
            var pixelScale = pixelScaleField[1].ToDoubleArray();

            if (tiepoints != null && tiepoints.Length >= 6 &&
                pixelScale != null && pixelScale.Length >= 3)
            {
                // Tiepoint format: [I, J, K, X, Y, Z]
                // I, J = raster coordinates (usually 0, 0)
                // X, Y = map coordinates
                var i = tiepoints[0];
                var j = tiepoints[1];
                var x = tiepoints[3];
                var y = tiepoints[4];

                // Pixel scale: [scaleX, scaleY, scaleZ]
                var scaleX = pixelScale[0];
                var scaleY = pixelScale[1];

                // Calculate origin (top-left corner)
                var originX = x - (i * scaleX);
                var originY = y - (j * scaleY);

                return new GeoTransform
                {
                    OriginX = originX,
                    PixelSizeX = scaleX,
                    RotationX = 0.0,
                    OriginY = originY,
                    RotationY = 0.0,
                    PixelSizeY = -scaleY  // Negative because Y decreases going down
                };
            }
        }

        _logger.LogDebug("No GeoTIFF transform tags found");
        return null;
    }

    /// <summary>
    /// Extract projection/CRS information as WKT.
    /// </summary>
    private string? ExtractProjection(Tiff tiff)
    {
        // Parse GeoKeyDirectory to extract CRS information
        var geoKeyDirField = tiff.GetField(TIFFTAG_GEOKEYDIRECTORY);
        var geoDoubleParamsField = tiff.GetField(TIFFTAG_GEODOUBLEPARAMS);
        var geoAsciiParamsField = tiff.GetField(TIFFTAG_GEOASCIIPARAMS);

        if (geoKeyDirField == null || geoKeyDirField.Length == 0)
        {
            _logger.LogDebug("No GeoKeyDirectory found");
            return null;
        }

        var geoKeyDir = geoKeyDirField[1].ToShortArray();
        if (geoKeyDir == null || geoKeyDir.Length < 4)
        {
            _logger.LogWarning("Invalid GeoKeyDirectory format");
            return null;
        }

        // Parse header: [KeyDirectoryVersion, KeyRevision, MinorRevision, NumberOfKeys]
        var version = geoKeyDir[0];
        var revision = geoKeyDir[1];
        var numberOfKeys = geoKeyDir[3];

        _logger.LogDebug("GeoKeyDirectory: version={Version}, revision={Revision}, keys={Keys}",
            version, revision, numberOfKeys);

        // Parse GeoKeys
        var geoKeys = new Dictionary<int, object>();
        double[]? geoDoubleParams = geoDoubleParamsField?[1].ToDoubleArray();
        string? geoAsciiParams = null;

        if (geoAsciiParamsField != null && geoAsciiParamsField.Length > 0)
        {
            var bytes = geoAsciiParamsField[1].ToByteArray();
            if (bytes != null)
            {
                geoAsciiParams = Encoding.ASCII.GetString(bytes).TrimEnd('\0');
            }
        }

        // Each GeoKey is 4 shorts: [KeyID, TIFFTagLocation, Count, Value_Offset]
        for (int i = 0; i < numberOfKeys; i++)
        {
            var offset = 4 + (i * 4);
            if (offset + 3 >= geoKeyDir.Length) break;

            var keyId = geoKeyDir[offset];
            var tagLocation = (ushort)geoKeyDir[offset + 1];
            var count = geoKeyDir[offset + 2];
            var valueOffset = geoKeyDir[offset + 3];

            object? value = null;

            if (tagLocation == 0)
            {
                // Value is stored directly in valueOffset
                value = valueOffset;
            }
            else if (tagLocation == (ushort)TIFFTAG_GEODOUBLEPARAMS)
            {
                // Value is in GeoDoubleParams array
                if (geoDoubleParams != null && valueOffset + count <= geoDoubleParams.Length)
                {
                    if (count == 1)
                    {
                        value = geoDoubleParams[valueOffset];
                    }
                    else
                    {
                        value = geoDoubleParams.Skip(valueOffset).Take(count).ToArray();
                    }
                }
            }
            else if (tagLocation == (ushort)TIFFTAG_GEOASCIIPARAMS)
            {
                // Value is in GeoAsciiParams string
                if (geoAsciiParams != null && valueOffset + count <= geoAsciiParams.Length)
                {
                    value = geoAsciiParams.Substring(valueOffset, count).TrimEnd('|', '\0');
                }
            }

            if (value != null)
            {
                geoKeys[keyId] = value;
            }
        }

        // Build WKT from GeoKeys
        return BuildWktFromGeoKeys(geoKeys);
    }

    /// <summary>
    /// Build WKT string from parsed GeoKeys.
    /// </summary>
    private string? BuildWktFromGeoKeys(Dictionary<int, object> geoKeys)
    {
        // Check for ProjectedCSTypeGeoKey (EPSG code)
        if (geoKeys.TryGetValue(ProjectedCSTypeGeoKey, out var projectedCsType) && projectedCsType is int epsgCode)
        {
            if (epsgCode != 32767)  // 32767 = user-defined
            {
                _logger.LogDebug("Found EPSG code: {EpsgCode}", epsgCode);
                return $"EPSG:{epsgCode}";
            }
        }

        // Check for GeographicTypeGeoKey (geographic CRS)
        if (geoKeys.TryGetValue(GeographicTypeGeoKey, out var geogType) && geogType is int geogCode)
        {
            if (geogCode != 32767)
            {
                _logger.LogDebug("Found geographic CRS code: {GeogCode}", geogCode);
                return MapGeographicCodeToWkt(geogCode);
            }
        }

        // Check for GTModelTypeGeoKey
        if (geoKeys.TryGetValue(GTModelTypeGeoKey, out var modelType))
        {
            _logger.LogDebug("Model type: {ModelType}", modelType);
        }

        // Check for citation strings
        if (geoKeys.TryGetValue(PCSCitationGeoKey, out var pcsCitation) && pcsCitation is string pcsStr)
        {
            _logger.LogDebug("PCS Citation: {Citation}", pcsStr);
        }

        if (geoKeys.TryGetValue(GeogCitationGeoKey, out var geogCitation) && geogCitation is string geogStr)
        {
            _logger.LogDebug("Geographic Citation: {Citation}", geogStr);
        }

        // Default: assume WGS84 if no projection specified
        _logger.LogWarning("Could not determine projection from GeoKeys, assuming WGS84");
        return "EPSG:4326";
    }

    /// <summary>
    /// Map EPSG geographic codes to WKT.
    /// </summary>
    private string MapGeographicCodeToWkt(int code)
    {
        return code switch
        {
            4326 => "EPSG:4326",  // WGS84
            4269 => "EPSG:4269",  // NAD83
            4267 => "EPSG:4267",  // NAD27
            4019 => "EPSG:4019",  // GRS 1980
            _ => $"EPSG:{code}"
        };
    }

    /// <summary>
    /// Extract GDAL metadata if present.
    /// </summary>
    public string? ExtractGdalMetadata(Tiff tiff)
    {
        var metadataField = tiff.GetField(TIFFTAG_GDAL_METADATA);
        if (metadataField != null && metadataField.Length > 0)
        {
            var bytes = metadataField[0].ToByteArray();
            if (bytes != null)
            {
                return Encoding.UTF8.GetString(bytes);
            }
        }

        return null;
    }

    /// <summary>
    /// Extract GDAL NoData value if present.
    /// </summary>
    public string? ExtractGdalNoData(Tiff tiff)
    {
        var noDataField = tiff.GetField(TIFFTAG_GDAL_NODATA);
        if (noDataField != null && noDataField.Length > 0)
        {
            var bytes = noDataField[0].ToByteArray();
            if (bytes != null)
            {
                return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
            }
        }

        return null;
    }
}
