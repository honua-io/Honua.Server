// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Wms;

/// <summary>
/// Shared helper methods and constants for WMS operations.
/// </summary>
internal static class WmsSharedHelpers
{
    public const string Version = "1.3.0";
    public const int MaxFeatureInfoCount = 50;

    public static readonly XNamespace Wms = "http://www.opengis.net/wms";
    public static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
    public static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Creates a WMS exception response.
    /// </summary>
    public static IResult CreateException(string code, string message)
        => OgcExceptionHelper.CreateWmsException(code, message, Version);

    /// <summary>
    /// Builds the absolute endpoint URL for WMS.
    /// </summary>
    public static string BuildEndpointUrl(HttpRequest request)
        => request.BuildAbsoluteUrl("/wms");

    /// <summary>
    /// Formats a double value for XML output.
    /// </summary>
    public static string FormatDouble(double value)
        => value.ToString("G", CultureInfo.InvariantCulture);

    /// <summary>
    /// Resolves the bounding box for a dataset.
    /// </summary>
    public static double[]? ResolveDatasetBoundingBox(RasterDatasetDefinition dataset)
    {
        var candidate = dataset.Extent?.Bbox.FirstOrDefault();
        if (candidate is not null && candidate.Length >= 4)
        {
            return candidate;
        }

        candidate = dataset.Catalog.SpatialExtent?.Bbox.FirstOrDefault();
        if (candidate is not null && candidate.Length >= 4)
        {
            return candidate;
        }

        return null;
    }

    /// <summary>
    /// Resolves the root bounding box from snapshot or datasets.
    /// </summary>
    public static double[]? ResolveRootBoundingBox(MetadataSnapshot snapshot, IReadOnlyList<RasterDatasetDefinition> datasets)
    {
        var bounds = snapshot.Catalog.Extents?.Spatial?.Bbox.FirstOrDefault();
        if (bounds is not null && bounds.Length >= 4)
        {
            return bounds;
        }

        var datasetBoxes = datasets.Select(ResolveDatasetBoundingBox).Where(box => box is not null).Cast<double[]>().ToArray();
        if (datasetBoxes.Length == 0)
        {
            return ApiLimitsAndConstants.DefaultWorldBoundingBox;
        }

        var minX = datasetBoxes.Min(box => box[0]);
        var minY = datasetBoxes.Min(box => box[1]);
        var maxX = datasetBoxes.Max(box => box[2]);
        var maxY = datasetBoxes.Max(box => box[3]);
        return new[] { minX, minY, maxX, maxY };
    }

    /// <summary>
    /// Resolves the CRS values for a dataset.
    /// </summary>
    public static IEnumerable<string> ResolveDatasetCrs(RasterDatasetDefinition dataset)
    {
        var crsValues = dataset.Crs.Select(CrsNormalizationHelper.NormalizeForWms).ToList();

        if (dataset.Extent?.Crs.HasValue() == true)
        {
            crsValues.Add(CrsNormalizationHelper.NormalizeForWms(dataset.Extent.Crs));
        }

        if (dataset.Catalog.SpatialExtent?.Crs.HasValue() == true)
        {
            crsValues.Add(CrsNormalizationHelper.NormalizeForWms(dataset.Catalog.SpatialExtent.Crs));
        }

        if (!crsValues.Contains("CRS:84", StringComparer.OrdinalIgnoreCase))
        {
            crsValues.Add("CRS:84");
        }

        return crsValues.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the root CRS from snapshot or datasets.
    /// </summary>
    public static IEnumerable<string> ResolveRootCrs(MetadataSnapshot snapshot, IReadOnlyList<RasterDatasetDefinition> datasets)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var catalogCrs = snapshot.Catalog.Extents?.Spatial?.Crs;
        if (catalogCrs.HasValue())
        {
            values.Add(CrsNormalizationHelper.NormalizeForWms(catalogCrs));
        }

        foreach (var dataset in datasets)
        {
            foreach (var crs in ResolveDatasetCrs(dataset))
            {
                values.Add(crs);
            }
        }

        if (values.Count == 0)
        {
            values.Add("CRS:84");
        }

        return values.OrderBy(crs => crs, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the layer name for a dataset.
    /// </summary>
    public static string BuildLayerName(RasterDatasetDefinition dataset)
    {
        if (dataset.ServiceId.HasValue())
        {
            return $"{dataset.ServiceId}:{dataset.Id}";
        }

        return dataset.Id;
    }

    /// <summary>
    /// Resolves a dataset by layer name.
    /// </summary>
    public static async ValueTask<RasterDatasetDefinition?> ResolveDatasetAsync(
        string layerName,
        IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        if (!layerName.Contains(':', StringComparison.Ordinal))
        {
            return await rasterRegistry.FindAsync(layerName, cancellationToken);
        }

        var parts = layerName.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return await rasterRegistry.FindAsync(layerName, cancellationToken);
        }

        var dataset = await rasterRegistry.FindAsync(parts[1], cancellationToken).ConfigureAwait(false);
        if (dataset is null)
        {
            return null;
        }

        if (dataset.ServiceId.IsNullOrWhiteSpace() || dataset.ServiceId.EqualsIgnoreCase(parts[0]))
        {
            return dataset;
        }

        return null;
    }

    /// <summary>
    /// Resolves the requested style ID for a dataset.
    /// </summary>
    public static string? ResolveRequestedStyleId(RasterDatasetDefinition dataset, string? styleToken)
    {
        var (success, styleId, error) = StyleResolutionHelper.TryResolveRasterStyleId(dataset, styleToken);
        if (!success)
        {
            throw new InvalidOperationException(error ?? $"Style '{styleToken}' is not available for layer '{dataset.Id}'.");
        }
        return styleId;
    }

    /// <summary>
    /// Resolves the style definition from metadata snapshot.
    /// </summary>
    public static StyleDefinition? ResolveStyleDefinition(MetadataSnapshot snapshot, string? styleId)
    {
        if (styleId.IsNullOrWhiteSpace())
        {
            return null;
        }

        snapshot.TryGetStyle(styleId, out var style);
        return style;
    }

    /// <summary>
    /// Gets ordered style IDs for a dataset (default first, then others).
    /// </summary>
    public static IEnumerable<string> GetOrderedStyleIds(RasterDatasetDefinition dataset)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (dataset.Styles.DefaultStyleId.HasValue() && seen.Add(dataset.Styles.DefaultStyleId))
        {
            yield return dataset.Styles.DefaultStyleId;
        }

        foreach (var styleId in dataset.Styles.StyleIds)
        {
            if (styleId.HasValue() && seen.Add(styleId))
            {
                yield return styleId;
            }
        }
    }

    /// <summary>
    /// Determines if a CRS requires axis order swapping for WMS 1.3.0.
    /// EPSG:4326 and other geographic CRS use lat,lon (north,east) in WMS 1.3.0,
    /// while CRS:84 and projected CRS use lon,lat (east,north).
    /// </summary>
    public static bool RequiresAxisOrderSwap(string? crs)
    {
        if (crs.IsNullOrWhiteSpace())
        {
            return false;
        }

        var normalized = crs.Trim().ToUpperInvariant();

        // CRS:84 explicitly uses lon,lat order (no swap needed)
        if (normalized == "CRS:84" || normalized == "OGC:CRS84")
        {
            return false;
        }

        // EPSG:4326 uses lat,lon in WMS 1.3.0 (swap needed)
        if (normalized == "EPSG:4326")
        {
            return true;
        }

        // Other geographic CRS (EPSG codes 4001-4999) typically use lat,lon
        if (normalized.StartsWith("EPSG:"))
        {
            var epsgCode = normalized.Substring(5);
            if (int.TryParse(epsgCode, out var code))
            {
                // Geographic CRS range (simplified heuristic)
                return code >= 4001 && code <= 4999;
            }
        }

        return false;
    }

    /// <summary>
    /// Parses a bounding box parameter and swaps axis order if required by the CRS.
    /// WMS 1.3.0 Compliance: Validates that bbox coordinates follow CRS-specific axis ordering.
    /// </summary>
    public static double[] ParseBoundingBox(string? raw, string? crs = null)
    {
        var (bbox, error) = QueryParameterHelper.ParseBoundingBoxArray(raw, allowAltitude: false);
        if (bbox is not null && error is null)
        {
            // WMS 1.3.0 Compliance: Swap axis order if CRS requires it (e.g., EPSG:4326 uses lat,lon in WMS 1.3.0)
            if (RequiresAxisOrderSwap(crs))
            {
                // Input: minLat, minLon, maxLat, maxLon (north,east,north,east)
                // Output: minLon, minLat, maxLon, maxLat (east,north,east,north)
                var swapped = new[] { bbox[1], bbox[0], bbox[3], bbox[2] };

                // Validate swapped coordinates make sense
                ValidateBoundingBoxCoordinates(swapped, crs);
                return swapped;
            }

            // Validate coordinates for non-swapped CRS
            ValidateBoundingBoxCoordinates(bbox, crs);
            return bbox;
        }

        if (raw.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Parameter 'bbox' is required.");
        }

        throw new InvalidOperationException($"Parameter 'bbox': {error}");
    }

    /// <summary>
    /// Validates that bounding box coordinates are in the correct order and within valid ranges.
    /// WMS 1.3.0 Compliance: Ensures minx &lt; maxx and miny &lt; maxy.
    /// </summary>
    private static void ValidateBoundingBoxCoordinates(double[] bbox, string? crs)
    {
        if (bbox.Length < 4)
        {
            throw new InvalidOperationException("Bounding box must have at least 4 coordinates.");
        }

        var minX = bbox[0];
        var minY = bbox[1];
        var maxX = bbox[2];
        var maxY = bbox[3];

        // WMS 1.3.0: minx must be less than maxx, miny must be less than maxy
        if (minX >= maxX)
        {
            throw new InvalidOperationException($"Invalid bounding box: minX ({minX}) must be less than maxX ({maxX}) for CRS {crs ?? "unspecified"}.");
        }

        if (minY >= maxY)
        {
            throw new InvalidOperationException($"Invalid bounding box: minY ({minY}) must be less than maxY ({maxY}) for CRS {crs ?? "unspecified"}.");
        }

        // Additional validation for geographic CRS (lat/lon ranges)
        if (crs.HasValue())
        {
            var normalizedCrs = crs.Trim().ToUpperInvariant();
            if (normalizedCrs == "CRS:84" || normalizedCrs == "OGC:CRS84")
            {
                // CRS:84 is lon,lat with lon in [-180, 180] and lat in [-90, 90]
                if (minX < ApiLimitsAndConstants.MinLongitude || maxX > ApiLimitsAndConstants.MaxLongitude)
                {
                    throw new InvalidOperationException($"Invalid bounding box for {crs}: longitude must be in range [{ApiLimitsAndConstants.MinLongitude}, {ApiLimitsAndConstants.MaxLongitude}].");
                }
                if (minY < ApiLimitsAndConstants.MinLatitude || maxY > ApiLimitsAndConstants.MaxLatitude)
                {
                    throw new InvalidOperationException($"Invalid bounding box for {crs}: latitude must be in range [{ApiLimitsAndConstants.MinLatitude}, {ApiLimitsAndConstants.MaxLatitude}].");
                }
            }
            else if (normalizedCrs == "EPSG:4326")
            {
                // EPSG:4326 in WMS 1.3.0 is lat,lon (already swapped at this point, so coordinates are lon,lat)
                if (minX < ApiLimitsAndConstants.MinLongitude || maxX > ApiLimitsAndConstants.MaxLongitude)
                {
                    throw new InvalidOperationException($"Invalid bounding box for {crs}: longitude must be in range [{ApiLimitsAndConstants.MinLongitude}, {ApiLimitsAndConstants.MaxLongitude}].");
                }
                if (minY < ApiLimitsAndConstants.MinLatitude || maxY > ApiLimitsAndConstants.MaxLatitude)
                {
                    throw new InvalidOperationException($"Invalid bounding box for {crs}: latitude must be in range [{ApiLimitsAndConstants.MinLatitude}, {ApiLimitsAndConstants.MaxLatitude}].");
                }
            }
        }
    }

    /// <summary>
    /// Parses a positive integer parameter.
    /// </summary>
    public static int ParsePositiveInt(IQueryCollection query, string key)
    {
        var raw = QueryParsingHelpers.GetQueryValue(query, key);
        var (value, error) = QueryParameterHelper.ParsePositiveInt(raw, allowZero: false);

        if (error is not null)
        {
            throw new InvalidOperationException($"Parameter '{key}' {error}");
        }

        return value ?? throw new InvalidOperationException($"Parameter '{key}' is required.");
    }

    /// <summary>
    /// Creates a geographic bounding box XML element.
    /// </summary>
    public static XElement CreateGeographicBoundingBox(double[] bbox)
    {
        return new XElement(Wms + "EX_GeographicBoundingBox",
            new XElement(Wms + "westBoundLongitude", FormatDouble(bbox[0])),
            new XElement(Wms + "eastBoundLongitude", FormatDouble(bbox[2])),
            new XElement(Wms + "southBoundLatitude", FormatDouble(bbox[1])),
            new XElement(Wms + "northBoundLatitude", FormatDouble(bbox[3])));
    }

    /// <summary>
    /// Creates a bounding box XML element with CRS.
    /// </summary>
    public static XElement CreateBoundingBox(string crs, double[] bbox)
    {
        return new XElement(Wms + "BoundingBox",
            new XAttribute("CRS", crs),
            new XAttribute("minx", FormatDouble(bbox[0])),
            new XAttribute("miny", FormatDouble(bbox[1])),
            new XAttribute("maxx", FormatDouble(bbox[2])),
            new XAttribute("maxy", FormatDouble(bbox[3])));
    }

    /// <summary>
    /// Validates and resolves the TIME parameter against temporal definition.
    /// </summary>
    public static string? ValidateTimeParameter(string? timeValue, RasterTemporalDefinition temporal)
    {
        // Use default if no time specified
        if (timeValue.IsNullOrWhiteSpace())
        {
            return temporal.DefaultValue;
        }

        // If fixed values are specified, validate against them
        if (temporal.FixedValues is { Count: > 0 })
        {
            if (!temporal.FixedValues.Contains(timeValue, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"TIME value '{timeValue}' is not in the allowed set: {string.Join(", ", temporal.FixedValues)}");
            }
            return timeValue;
        }

        // If range is specified, validate within bounds
        if (temporal.MinValue.HasValue() && temporal.MaxValue.HasValue())
        {
            if (string.CompareOrdinal(timeValue, temporal.MinValue) < 0 || string.CompareOrdinal(timeValue, temporal.MaxValue) > 0)
            {
                throw new InvalidOperationException($"TIME value '{timeValue}' is outside the valid range: {temporal.MinValue} to {temporal.MaxValue}");
            }
        }

        return timeValue;
    }

    /// <summary>
    /// Converts a WMS TIME parameter to a TemporalInterval for feature queries.
    /// Supports single instant values (e.g., "2024-01-15T12:00:00Z") and intervals (e.g., "2024-01-15T00:00:00Z/2024-01-16T00:00:00Z").
    /// </summary>
    public static TemporalInterval? ParseTemporalInterval(string? timeValue)
    {
        if (timeValue.IsNullOrWhiteSpace())
        {
            return null;
        }

        // Multiple temporal values can be comma-separated; for GetFeatureInfo we honour the first entry
        var firstToken = QueryParameterHelper.ParseCommaSeparatedList(timeValue).FirstOrDefault()
            ?? timeValue.Trim();

        if (firstToken.Length == 0)
        {
            return null;
        }

        // Handle interval notation (e.g., "2024-01-15T00:00:00Z/2024-01-16T00:00:00Z" or with a period component)
        if (firstToken.Contains('/', StringComparison.Ordinal))
        {
            var parts = firstToken.Split('/', StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                var start = TryParseTemporalComponent(parts[0]);
                var end = TryParseTemporalComponent(parts[1]);
                if (start is null && end is null)
                {
                    return null;
                }

                return new TemporalInterval(start, end);
            }
        }

        // Handle single instant (treat as a point-in-time query)
        var instant = TryParseTemporalComponent(firstToken);
        if (instant is not null)
        {
            return new TemporalInterval(instant, instant);
        }

        return null;
    }

    private static DateTimeOffset? TryParseTemporalComponent(string? component)
    {
        if (component.IsNullOrWhiteSpace())
        {
            return null;
        }

        return DateTimeOffset.TryParse(component.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Tries to resolve a layer group by name (format: serviceId:groupId).
    /// </summary>
    public static bool TryResolveLayerGroup(
        string layerName,
        MetadataSnapshot snapshot,
        out LayerGroupDefinition? layerGroup)
    {
        layerGroup = null;

        if (!layerName.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var parts = layerName.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var serviceId = parts[0];
        var groupId = parts[1];

        return snapshot.TryGetLayerGroup(serviceId, groupId, out layerGroup);
    }

    /// <summary>
    /// Expands a layer group to raster datasets for rendering.
    /// </summary>
    public static async ValueTask<IReadOnlyList<ExpandedRasterMember>> ExpandLayerGroupToRasterDatasetsAsync(
        LayerGroupDefinition layerGroup,
        MetadataSnapshot snapshot,
        IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        // Expand the layer group to get component layers
        var expandedLayers = LayerGroupExpander.ExpandLayerGroup(layerGroup, snapshot);

        var result = new List<ExpandedRasterMember>();

        // For each expanded layer, find its corresponding raster dataset
        foreach (var expandedMember in expandedLayers)
        {
            var layer = expandedMember.Layer;

            // Find the raster dataset for this layer
            // Raster datasets can be linked to layers via ServiceId and LayerId
            var rasterDataset = await FindRasterDatasetForLayerAsync(
                layer,
                rasterRegistry,
                cancellationToken).ConfigureAwait(false);

            if (rasterDataset is not null)
            {
                result.Add(new ExpandedRasterMember(
                    rasterDataset,
                    expandedMember.Opacity,
                    expandedMember.StyleId,
                    expandedMember.Order));
            }
        }

        return result;
    }

    /// <summary>
    /// Finds the raster dataset associated with a layer.
    /// </summary>
    private static async ValueTask<RasterDatasetDefinition?> FindRasterDatasetForLayerAsync(
        LayerDefinition layer,
        IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        // Get all raster datasets and find one that matches this layer
        var allDatasets = await rasterRegistry.GetAllAsync(cancellationToken).ConfigureAwait(false);

        foreach (var dataset in allDatasets)
        {
            if (dataset.ServiceId.HasValue() &&
                dataset.LayerId.HasValue() &&
                string.Equals(dataset.ServiceId, layer.ServiceId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(dataset.LayerId, layer.Id, StringComparison.OrdinalIgnoreCase))
            {
                return dataset;
            }
        }

        return null;
    }
}

/// <summary>
/// Represents a raster dataset that has been expanded from a layer group,
/// including its effective opacity and style.
/// </summary>
public sealed record ExpandedRasterMember
{
    /// <summary>
    /// The raster dataset definition.
    /// </summary>
    public required RasterDatasetDefinition Dataset { get; init; }

    /// <summary>
    /// The cumulative opacity (0.0 - 1.0) from all parent groups.
    /// </summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>
    /// The style ID to use for rendering (may be overridden from the dataset's default).
    /// </summary>
    public string? StyleId { get; init; }

    /// <summary>
    /// The order value from the group member.
    /// </summary>
    public int Order { get; init; }

    public ExpandedRasterMember(RasterDatasetDefinition dataset, double opacity, string? styleId, int order)
    {
        Dataset = dataset;
        Opacity = opacity;
        StyleId = styleId;
        Order = order;
    }
}
