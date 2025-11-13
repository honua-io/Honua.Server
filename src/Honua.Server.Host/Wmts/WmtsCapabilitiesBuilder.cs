// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Wmts;

/// <summary>
/// WMTS 1.0.0 capabilities builder implementing the OGC capabilities base class.
/// Handles WMTS-specific capabilities generation including Contents, TileMatrixSets, and Layers.
/// </summary>
public sealed class WmtsCapabilitiesBuilder : OgcCapabilitiesBuilder
{
    private static readonly XNamespace Wmts = "http://www.opengis.net/wmts/1.0";

    /// <summary>
    /// WMTS 1.0.0 conformance classes that this implementation supports.
    /// Based on OGC 07-057r7 (WMTS 1.0.0 Implementation Standard).
    /// </summary>
    private static readonly string[] WmtsConformanceClasses =
    {
        "http://www.opengis.net/spec/wmts/1.0/conf/core",
        "http://www.opengis.net/spec/wmts/1.0/conf/getcapabilities",
        "http://www.opengis.net/spec/wmts/1.0/conf/gettile",
        "http://www.opengis.net/spec/wmts/1.0/conf/kvp",
    };

    private readonly IRasterDatasetRegistry rasterRegistry;

    public WmtsCapabilitiesBuilder(IRasterDatasetRegistry rasterRegistry)
    {
        this.rasterRegistry = rasterRegistry ?? throw new ArgumentNullException(nameof(rasterRegistry));
    }

    protected override XName GetRootElementName() => Wmts + "Capabilities";

    protected override string GetServiceName() => "OGC WMTS";

    protected override string GetVersion() => "1.0.0";

    protected override string BuildEndpointUrl(HttpRequest request)
    {
        return $"{BuildBaseUrl(request)}/wmts";
    }

    protected override IEnumerable<XAttribute> GetNamespaceAttributes()
    {
        foreach (var attr in base.GetNamespaceAttributes())
        {
            yield return attr;
        }

        yield return new XAttribute(XNamespace.Xmlns + "wmts", Wmts);
        yield return new XAttribute(XNamespace.Xmlns + "ows", Ows);
        yield return new XAttribute(Xsi + "schemaLocation",
            "http://www.opengis.net/wmts/1.0 http://schemas.opengis.net/wmts/1.0/wmtsGetCapabilities_response.xsd");
    }

    protected override IEnumerable<string> GetSupportedOperations()
    {
        yield return "GetCapabilities";
        yield return "GetTile";
        yield return "GetFeatureInfo";
    }

    protected override void AddServiceIdentificationExtensions(XElement element, MetadataSnapshot metadata)
    {
        var ns = GetOwsNamespace();

        // Add conformance classes as Profile elements
        foreach (var conformanceClass in WmtsConformanceClasses)
        {
            element.Add(new XElement(ns + "Profile", conformanceClass));
        }
    }

    protected override async Task AddProtocolSpecificSectionsAsync(XElement root, MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken)
    {
        var datasets = await this.rasterRegistry.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var contentsElement = new XElement(Wmts + "Contents");

        // Add layer elements
        foreach (var dataset in datasets.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase))
        {
            contentsElement.Add(BuildLayerElement(dataset, request));
        }

        // Add TileMatrixSet definitions
        contentsElement.Add(CreateTileMatrixSet(OgcTileMatrixHelper.WorldCrs84QuadId, OgcTileMatrixHelper.WorldCrs84QuadCrs, datasets));
        contentsElement.Add(CreateTileMatrixSet(OgcTileMatrixHelper.WorldWebMercatorQuadId, OgcTileMatrixHelper.WorldWebMercatorQuadCrs, datasets));

        root.Add(contentsElement);
    }

    /// <summary>
    /// Builds a Layer element for a specific dataset.
    /// </summary>
    private XElement BuildLayerElement(RasterDatasetDefinition dataset, HttpRequest request)
    {
        var baseUrl = BuildEndpointUrl(request);
        var bbox = dataset.Extent?.Bbox != null && dataset.Extent.Bbox.Count > 0
            ? dataset.Extent.Bbox[0]
            : new[] { -180.0, -90.0, 180.0, 90.0 };

        var layerElements = new List<object>
        {
            new XElement(Ows + "Title", dataset.Title ?? dataset.Id),
            new XElement(Ows + "Abstract", dataset.Description ?? $"Raster layer {dataset.Id}"),
            new XElement(Ows + "WGS84BoundingBox",
                new XAttribute("crs", "urn:ogc:def:crs:OGC:2:84"),
                new XElement(Ows + "LowerCorner", $"{FormatDouble(bbox[0])} {FormatDouble(bbox[1])}"),
                new XElement(Ows + "UpperCorner", $"{FormatDouble(bbox[2])} {FormatDouble(bbox[3])}")),
            new XElement(Ows + "Identifier", dataset.Id)
        };

        // Add keywords
        if (dataset.Keywords.Count > 0)
        {
            var keywords = new XElement(Ows + "Keywords");
            foreach (var keyword in dataset.Keywords.Where(k => k.HasValue()))
            {
                keywords.Add(new XElement(Ows + "Keyword", keyword));
            }
            layerElements.Add(keywords);
        }

        // Add temporal dimension if enabled
        if (dataset.Temporal.Enabled)
        {
            layerElements.Add(BuildWmtsTemporalDimension(dataset.Temporal));
        }

        // Add default style
        layerElements.Add(new XElement(Wmts + "Style",
            new XAttribute("isDefault", "true"),
            new XElement(Ows + "Identifier", "default"),
            new XElement(Ows + "Title", "Default Style")));

        // Add format
        layerElements.Add(new XElement(Wmts + "Format", "image/png"));

        // Add TileMatrixSetLinks
        layerElements.Add(new XElement(Wmts + "TileMatrixSetLink",
            new XElement(Wmts + "TileMatrixSet", OgcTileMatrixHelper.WorldCrs84QuadId)));
        layerElements.Add(new XElement(Wmts + "TileMatrixSetLink",
            new XElement(Wmts + "TileMatrixSet", OgcTileMatrixHelper.WorldWebMercatorQuadId)));

        // Add ResourceURL template
        var resourceUrlTemplate = dataset.Temporal.Enabled
            ? $"{baseUrl}?service=WMTS&request=GetTile&version=1.0.0&layer={dataset.Id}&style=default&tilematrixset={{TileMatrixSet}}&tilematrix={{TileMatrix}}&tilerow={{TileRow}}&tilecol={{TileCol}}&format=image/png&time={{Time}}"
            : $"{baseUrl}?service=WMTS&request=GetTile&version=1.0.0&layer={dataset.Id}&style=default&tilematrixset={{TileMatrixSet}}&tilematrix={{TileMatrix}}&tilerow={{TileRow}}&tilecol={{TileCol}}&format=image/png";

        layerElements.Add(new XElement(Wmts + "ResourceURL",
            new XAttribute("format", "image/png"),
            new XAttribute("resourceType", "tile"),
            new XAttribute("template", resourceUrlTemplate)));

        return new XElement(Wmts + "Layer", layerElements);
    }

    /// <summary>
    /// Builds a temporal dimension element for datasets with temporal support.
    /// </summary>
    private XElement BuildWmtsTemporalDimension(RasterTemporalDefinition temporal)
    {
        var dimension = new XElement(Wmts + "Dimension",
            new XElement(Ows + "Identifier", "Time"));

        if (temporal.DefaultValue.HasValue())
        {
            dimension.Add(new XElement(Wmts + "Default", temporal.DefaultValue));
        }

        // Build value list
        if (temporal.FixedValues is { Count: > 0 })
        {
            foreach (var value in temporal.FixedValues)
            {
                dimension.Add(new XElement(Wmts + "Value", value));
            }
        }
        else if (temporal.MinValue.HasValue() && temporal.MaxValue.HasValue())
        {
            // For continuous ranges, add min and max as values
            dimension.Add(new XElement(Wmts + "Value", temporal.MinValue));
            dimension.Add(new XElement(Wmts + "Value", temporal.MaxValue));
        }

        dimension.Add(new XElement(Wmts + "UOM", "ISO8601"));

        return dimension;
    }

    /// <summary>
    /// Creates a TileMatrixSet element with all supported zoom levels.
    /// </summary>
    private XElement CreateTileMatrixSet(string identifier, string crs, IReadOnlyList<RasterDatasetDefinition> datasets)
    {
        // Find the broadest zoom range across all datasets
        var minZoom = int.MaxValue;
        var maxZoom = int.MinValue;

        foreach (var dataset in datasets)
        {
            var (datasetMin, datasetMax) = OgcTileMatrixHelper.ResolveZoomRange(dataset.Cache.ZoomLevels);
            if (datasetMin < minZoom) minZoom = datasetMin;
            if (datasetMax > maxZoom) maxZoom = datasetMax;
        }

        if (minZoom == int.MaxValue) minZoom = 0;
        if (maxZoom == int.MinValue) maxZoom = 14;

        var matrices = new List<XElement>();
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var matrixSize = 1 << zoom;
            var scaleDenominator = identifier == OgcTileMatrixHelper.WorldWebMercatorQuadId
                ? GetWebMercatorScaleDenominator(zoom)
                : GetCrs84ScaleDenominator(zoom);

            var topLeftCorner = identifier == OgcTileMatrixHelper.WorldWebMercatorQuadId
                ? "-20037508.3427892 20037508.3427892"
                : "-180 90";

            matrices.Add(new XElement(Wmts + "TileMatrix",
                new XElement(Ows + "Identifier", zoom.ToString(CultureInfo.InvariantCulture)),
                new XElement(Wmts + "ScaleDenominator", scaleDenominator.ToString("F8", CultureInfo.InvariantCulture)),
                new XElement(Wmts + "TopLeftCorner", topLeftCorner),
                new XElement(Wmts + "TileWidth", "256"),
                new XElement(Wmts + "TileHeight", "256"),
                new XElement(Wmts + "MatrixWidth", matrixSize.ToString(CultureInfo.InvariantCulture)),
                new XElement(Wmts + "MatrixHeight", matrixSize.ToString(CultureInfo.InvariantCulture))));
        }

        return new XElement(Wmts + "TileMatrixSet",
            new XElement(Ows + "Identifier", identifier),
            new XElement(Ows + "SupportedCRS", crs),
            matrices);
    }

    /// <summary>
    /// Calculates the scale denominator for Web Mercator at a given zoom level.
    /// </summary>
    private static double GetWebMercatorScaleDenominator(int zoom)
    {
        const double WebMercatorExtent = 20037508.3427892 * 2;
        var matrixSize = 1 << zoom;
        var resolution = WebMercatorExtent / (256 * matrixSize);
        return resolution / 0.00028;
    }

    /// <summary>
    /// Calculates the scale denominator for CRS:84 at a given zoom level.
    /// </summary>
    private static double GetCrs84ScaleDenominator(int zoom)
    {
        const double Crs84Width = 360.0;
        var matrixSize = 1 << zoom;
        var resolution = Crs84Width / (256 * matrixSize);
        return resolution / 0.00028;
    }
}
