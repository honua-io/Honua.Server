// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Wms;

/// <summary>
/// WMS 1.3.0 capabilities builder implementing the OGC capabilities base class.
/// Handles WMS-specific capabilities generation including layer hierarchy, styles, and CRS.
/// </summary>
public sealed class WmsCapabilitiesBuilder : OgcCapabilitiesBuilder
{
    private static readonly XNamespace Wms = "http://www.opengis.net/wms";

    private readonly IRasterDatasetRegistry rasterRegistry;

    public WmsCapabilitiesBuilder(IRasterDatasetRegistry rasterRegistry)
    {
        this.rasterRegistry = rasterRegistry ?? throw new ArgumentNullException(nameof(rasterRegistry));
    }

    protected override XName GetRootElementName() => Wms + "WMS_Capabilities";

    protected override string GetServiceName() => "WMS";

    protected override string GetVersion() => "1.3.0";

    protected override string BuildEndpointUrl(HttpRequest request)
    {
        return $"{BuildBaseUrl(request)}/wms";
    }

    protected override IEnumerable<XAttribute> GetNamespaceAttributes()
    {
        foreach (var attr in base.GetNamespaceAttributes())
        {
            yield return attr;
        }

        yield return new XAttribute(XNamespace.Xmlns + "wms", Wms);
        yield return new XAttribute(Xsi + "schemaLocation",
            "http://www.opengis.net/wms http://schemas.opengis.net/wms/1.3.0/capabilities_1_3_0.xsd");
    }

    protected override IEnumerable<string> GetSupportedOperations()
    {
        yield return "GetCapabilities";
        yield return "GetMap";
        yield return "GetFeatureInfo";
        yield return "DescribeLayer";
        yield return "GetLegendGraphic";
    }

    protected override async Task<XElement> BuildRootElementAsync(MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken)
    {
        // WMS 1.3.0 uses custom Service element instead of OWS ServiceIdentification
        var root = new XElement(GetRootElementName(),
            GetNamespaceAttributes(),
            BuildWmsServiceElement(metadata, request),
            await BuildWmsCapabilityAsync(metadata, request, cancellationToken).ConfigureAwait(false));

        return root;
    }

    /// <summary>
    /// Builds the WMS-specific Service element (not OWS ServiceIdentification).
    /// </summary>
    private XElement BuildWmsServiceElement(MetadataSnapshot metadata, HttpRequest request)
    {
        var endpoint = BuildEndpointUrl(request);
        var catalog = metadata.Catalog;

        var element = new XElement(Wms + "Service",
            new XElement(Wms + "Name", "WMS"),
            new XElement(Wms + "Title", catalog.Title.IsNullOrWhiteSpace() ? catalog.Id : catalog.Title),
            new XElement(Wms + "Abstract", catalog.Description ?? string.Empty),
            new XElement(Wms + "OnlineResource", new XAttribute(XLink + "href", endpoint)));

        // Add keywords
        if (catalog.Keywords.Count > 0)
        {
            var keywordList = new XElement(Wms + "KeywordList");
            foreach (var keyword in catalog.Keywords)
            {
                if (keyword.HasValue())
                {
                    keywordList.Add(new XElement(Wms + "Keyword", keyword));
                }
            }
            element.Add(keywordList);
        }

        // Add contact information (WMS format)
        if (catalog.Contact != null)
        {
            element.Add(BuildWmsContactInformation(catalog.Contact));
        }

        element.Add(
            new XElement(Wms + "Fees", "NONE"),
            new XElement(Wms + "AccessConstraints", "NONE"));

        return element;
    }

    /// <summary>
    /// Builds WMS-specific contact information (different from OWS format).
    /// </summary>
    private XElement BuildWmsContactInformation(CatalogContactDefinition contact)
    {
        var contactInfo = new XElement(Wms + "ContactInformation");

        if (contact.Organization.HasValue())
        {
            contactInfo.Add(new XElement(Wms + "ContactOrganization", contact.Organization));
        }

        if (contact.Name.HasValue() || contact.Email.HasValue())
        {
            var personPrimary = new XElement(Wms + "ContactPersonPrimary");

            if (contact.Name.HasValue())
            {
                personPrimary.Add(new XElement(Wms + "ContactPerson", contact.Name));
            }

            if (contact.Email.HasValue())
            {
                personPrimary.Add(new XElement(Wms + "ContactElectronicMailAddress", contact.Email));
            }

            contactInfo.Add(personPrimary);
        }

        return contactInfo;
    }

    /// <summary>
    /// Builds the WMS-specific Capability element containing operations and layers.
    /// </summary>
    private async Task<XElement> BuildWmsCapabilityAsync(MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken)
    {
        var endpoint = BuildEndpointUrl(request);
        var datasets = await this.rasterRegistry.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var requestElement = new XElement(Wms + "Request",
            BuildWmsOperationElement("GetCapabilities", endpoint, "application/xml"),
            BuildWmsOperationElement("GetMap", endpoint, "image/png", "image/jpeg"),
            BuildWmsOperationElement("GetFeatureInfo", endpoint, "application/json", "text/plain"),
            BuildWmsOperationElement("DescribeLayer", endpoint, "application/xml"),
            BuildWmsOperationElement("GetLegendGraphic", endpoint, "image/png"));

        var exceptionElement = new XElement(Wms + "Exception",
            new XElement(Wms + "Format", "application/vnd.ogc.se_xml"));

        var layerElement = BuildRootLayer(metadata, datasets);

        return new XElement(Wms + "Capability", requestElement, exceptionElement, layerElement);
    }

    /// <summary>
    /// Builds a WMS operation element with supported formats and DCPType.
    /// </summary>
    private XElement BuildWmsOperationElement(string name, string endpoint, params string[] formats)
    {
        var element = new XElement(Wms + name);

        foreach (var format in formats)
        {
            element.Add(new XElement(Wms + "Format", format));
        }

        element.Add(new XElement(Wms + "DCPType",
            new XElement(Wms + "HTTP",
                new XElement(Wms + "Get",
                    new XElement(Wms + "OnlineResource", new XAttribute(XLink + "href", endpoint))))));

        return element;
    }

    /// <summary>
    /// Builds the root layer containing all datasets.
    /// </summary>
    private XElement BuildRootLayer(MetadataSnapshot metadata, IReadOnlyList<RasterDatasetDefinition> datasets)
    {
        var catalog = metadata.Catalog;
        var layer = new XElement(Wms + "Layer");

        layer.Add(new XElement(Wms + "Title", catalog.Title.IsNullOrWhiteSpace() ? catalog.Id : catalog.Title));

        if (catalog.Description.HasValue())
        {
            layer.Add(new XElement(Wms + "Abstract", catalog.Description));
        }

        // Add keywords
        if (catalog.Keywords.Count > 0)
        {
            var keywords = new XElement(Wms + "KeywordList");
            foreach (var keyword in catalog.Keywords)
            {
                if (keyword.HasValue())
                {
                    keywords.Add(new XElement(Wms + "Keyword", keyword));
                }
            }
            layer.Add(keywords);
        }

        // Add root bounding box
        var rootBbox = WmsSharedHelpers.ResolveRootBoundingBox(metadata, datasets);
        if (rootBbox != null)
        {
            layer.Add(WmsSharedHelpers.CreateGeographicBoundingBox(rootBbox));
            layer.Add(WmsSharedHelpers.CreateBoundingBox("CRS:84", rootBbox));
        }

        // Add root CRS list
        foreach (var crs in WmsSharedHelpers.ResolveRootCrs(metadata, datasets))
        {
            layer.Add(new XElement(Wms + "CRS", crs));
        }

        // Add dataset layers
        var styleLookup = metadata.Styles.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var dataset in datasets.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase))
        {
            layer.Add(BuildDatasetLayer(dataset, styleLookup));
        }

        // Add layer groups
        foreach (var service in metadata.Services.Where(s => s.Enabled && s.Ogc.WmsEnabled))
        {
            var serviceLayerGroups = metadata.LayerGroups
                .Where(lg => lg.Enabled &&
                            string.Equals(lg.ServiceId, service.Id, StringComparison.OrdinalIgnoreCase) &&
                            lg.Wms.AdvertiseInCapabilities)
                .OrderBy(lg => lg.Title, StringComparer.OrdinalIgnoreCase);

            foreach (var layerGroup in serviceLayerGroups)
            {
                layer.Add(BuildLayerGroupElement(layerGroup, metadata, styleLookup));
            }
        }

        return layer;
    }

    /// <summary>
    /// Builds a layer element for a specific dataset.
    /// </summary>
    private XElement BuildDatasetLayer(RasterDatasetDefinition dataset, IReadOnlyDictionary<string, StyleDefinition> styleLookup)
    {
        var layer = new XElement(Wms + "Layer",
            new XElement(Wms + "Name", WmsSharedHelpers.BuildLayerName(dataset)),
            new XElement(Wms + "Title", dataset.Title));

        var isQueryable = dataset.ServiceId.HasValue() && dataset.LayerId.HasValue();
        layer.SetAttributeValue("queryable", isQueryable ? "1" : "0");

        if (dataset.Description.HasValue())
        {
            layer.Add(new XElement(Wms + "Abstract", dataset.Description));
        }

        // Add keywords
        var keywords = dataset.Keywords;
        if (dataset.Catalog.Keywords.Count > 0)
        {
            keywords = keywords.Concat(dataset.Catalog.Keywords).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        if (keywords.Count > 0)
        {
            var keywordList = new XElement(Wms + "KeywordList");
            foreach (var keyword in keywords)
            {
                if (keyword.HasValue())
                {
                    keywordList.Add(new XElement(Wms + "Keyword", keyword));
                }
            }
            layer.Add(keywordList);
        }

        // Add CRS
        foreach (var crs in WmsSharedHelpers.ResolveDatasetCrs(dataset))
        {
            layer.Add(new XElement(Wms + "CRS", crs));
        }

        // Add bounding boxes
        var bbox = WmsSharedHelpers.ResolveDatasetBoundingBox(dataset);
        if (bbox != null)
        {
            layer.Add(WmsSharedHelpers.CreateGeographicBoundingBox(bbox));

            // Use the actual CRS from the extent, or fallback to the dataset's default CRS
            var bboxCrs = dataset.Extent?.Crs ?? dataset.Crs.FirstOrDefault() ?? "CRS:84";
            var normalizedCrs = CrsNormalizationHelper.NormalizeForWms(bboxCrs);

            // Swap bbox coordinates if CRS requires lat,lon order (e.g., EPSG:4326)
            var bboxForCrs = WmsSharedHelpers.RequiresAxisOrderSwap(normalizedCrs)
                ? new[] { bbox[1], bbox[0], bbox[3], bbox[2] }  // lon,lat -> lat,lon
                : bbox;

            layer.Add(WmsSharedHelpers.CreateBoundingBox(normalizedCrs, bboxForCrs));
        }

        // Add styles
        foreach (var styleElement in BuildStyleElements(dataset, styleLookup))
        {
            layer.Add(styleElement);
        }

        // Add temporal dimension if enabled
        if (dataset.Temporal.Enabled)
        {
            layer.Add(BuildTimeDimension(dataset.Temporal));
        }

        return layer;
    }

    /// <summary>
    /// Builds style elements for a dataset.
    /// </summary>
    private IEnumerable<XElement> BuildStyleElements(RasterDatasetDefinition dataset, IReadOnlyDictionary<string, StyleDefinition> styleLookup)
    {
        var orderedStyleIds = WmsSharedHelpers.GetOrderedStyleIds(dataset).ToArray();
        if (orderedStyleIds.Length == 0)
        {
            yield break;
        }

        foreach (var styleId in orderedStyleIds)
        {
            var title = styleLookup.TryGetValue(styleId, out var style) && style.Title.HasValue()
                ? style.Title
                : styleId;

            yield return new XElement(Wms + "Style",
                new XElement(Wms + "Name", styleId),
                new XElement(Wms + "Title", title));
        }
    }

    /// <summary>
    /// Builds a time dimension element for temporal datasets.
    /// </summary>
    private XElement BuildTimeDimension(RasterTemporalDefinition temporal)
    {
        var dimension = new XElement(Wms + "Dimension",
            new XAttribute("name", "time"),
            new XAttribute("units", "ISO8601"));

        if (temporal.DefaultValue.HasValue())
        {
            dimension.SetAttributeValue("default", temporal.DefaultValue);
        }

        // Build extent value
        string extentValue;
        if (temporal.FixedValues is { Count: > 0 })
        {
            // Discrete values
            extentValue = string.Join(",", temporal.FixedValues);
        }
        else if (temporal.MinValue.HasValue() && temporal.MaxValue.HasValue())
        {
            // Range with optional period
            if (temporal.Period.HasValue())
            {
                extentValue = $"{temporal.MinValue}/{temporal.MaxValue}/{temporal.Period}";
            }
            else
            {
                extentValue = $"{temporal.MinValue}/{temporal.MaxValue}";
            }
        }
        else
        {
            extentValue = temporal.DefaultValue ?? string.Empty;
        }

        dimension.SetValue(extentValue);
        return dimension;
    }

    /// <summary>
    /// Builds a layer element for a layer group.
    /// </summary>
    private XElement BuildLayerGroupElement(
        LayerGroupDefinition layerGroup,
        MetadataSnapshot metadata,
        IReadOnlyDictionary<string, StyleDefinition> styleLookup)
    {
        var layerName = $"{layerGroup.ServiceId}:{layerGroup.Id}";
        var layer = new XElement(Wms + "Layer",
            new XElement(Wms + "Name", layerName),
            new XElement(Wms + "Title", layerGroup.Title));

        // Set queryable attribute
        layer.SetAttributeValue("queryable", layerGroup.Queryable ? "1" : "0");

        if (layerGroup.Description.HasValue())
        {
            layer.Add(new XElement(Wms + "Abstract", layerGroup.Description));
        }

        // Add keywords
        var keywords = new HashSet<string>(layerGroup.Keywords, StringComparer.OrdinalIgnoreCase);
        if (layerGroup.Catalog.Keywords.Count > 0)
        {
            foreach (var keyword in layerGroup.Catalog.Keywords)
            {
                keywords.Add(keyword);
            }
        }

        if (keywords.Count > 0)
        {
            var keywordList = new XElement(Wms + "KeywordList");
            foreach (var keyword in keywords)
            {
                if (keyword.HasValue())
                {
                    keywordList.Add(new XElement(Wms + "Keyword", keyword));
                }
            }
            layer.Add(keywordList);
        }

        // Add CRS (either explicit or inherited from member layers)
        var supportedCrs = LayerGroupExpander.GetSupportedCrs(layerGroup, metadata);
        foreach (var crs in supportedCrs)
        {
            layer.Add(new XElement(Wms + "CRS", crs));
        }

        // Add bounding box
        var extent = LayerGroupExpander.CalculateGroupExtent(layerGroup, metadata);
        if (extent?.Bbox != null && extent.Bbox.Count > 0)
        {
            var bbox = extent.Bbox[0];
            if (bbox.Length >= 4)
            {
                layer.Add(WmsSharedHelpers.CreateGeographicBoundingBox(bbox));

                // Use the CRS from the extent, or fallback to the first supported CRS
                var bboxCrs = extent.Crs ?? supportedCrs.FirstOrDefault() ?? "CRS:84";
                var normalizedCrs = CrsNormalizationHelper.NormalizeForWms(bboxCrs);

                // Swap bbox coordinates if CRS requires lat,lon order (e.g., EPSG:4326)
                var bboxForCrs = WmsSharedHelpers.RequiresAxisOrderSwap(normalizedCrs)
                    ? new[] { bbox[1], bbox[0], bbox[3], bbox[2] }  // lon,lat -> lat,lon
                    : bbox;

                layer.Add(WmsSharedHelpers.CreateBoundingBox(normalizedCrs, bboxForCrs));
            }
        }

        // Add styles
        var styleIds = new List<string>();
        if (layerGroup.DefaultStyleId.HasValue())
        {
            styleIds.Add(layerGroup.DefaultStyleId!);
        }
        styleIds.AddRange(layerGroup.StyleIds);

        foreach (var styleId in styleIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var title = styleLookup.TryGetValue(styleId, out var style) && style.Title.HasValue()
                ? style.Title
                : styleId;

            layer.Add(new XElement(Wms + "Style",
                new XElement(Wms + "Name", styleId),
                new XElement(Wms + "Title", title)));
        }

        // Add default style if no styles are defined
        if (styleIds.Count == 0)
        {
            layer.Add(new XElement(Wms + "Style",
                new XElement(Wms + "Name", "default"),
                new XElement(Wms + "Title", "Default")));
        }

        // Add scale hints if defined
        if (layerGroup.MinScale.HasValue || layerGroup.MaxScale.HasValue)
        {
            // WMS 1.3.0 uses MinScaleDenominator/MaxScaleDenominator
            if (layerGroup.MinScale.HasValue)
            {
                layer.Add(new XElement(Wms + "MinScaleDenominator", layerGroup.MinScale.Value));
            }

            if (layerGroup.MaxScale.HasValue)
            {
                layer.Add(new XElement(Wms + "MaxScaleDenominator", layerGroup.MaxScale.Value));
            }
        }

        // Optionally, expand and include member layers as nested layers
        // (This depends on whether we want a flat or hierarchical structure)
        // For now, we'll just advertise the group as a named layer

        return layer;
    }

    protected override Task AddProtocolSpecificSectionsAsync(XElement root, MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken)
    {
        // WMS uses a completely custom structure, so this method is not called
        // The BuildRootElementAsync override handles the entire document structure
        return Task.CompletedTask;
    }
}
