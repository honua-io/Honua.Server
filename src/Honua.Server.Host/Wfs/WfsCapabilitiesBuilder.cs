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
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// WFS 2.0.0 capabilities builder implementing the OGC capabilities base class.
/// Handles WFS-specific capabilities generation including FeatureTypeList and operations.
/// </summary>
public sealed class WfsCapabilitiesBuilder : OgcCapabilitiesBuilder
{
    protected override XName GetRootElementName() => WfsConstants.Wfs + "WFS_Capabilities";

    protected override string GetServiceName() => "WFS";

    protected override string GetVersion() => "2.0.0";

    protected override string BuildEndpointUrl(HttpRequest request)
    {
        return WfsHelpers.BuildEndpointUrl(request);
    }

    protected override IEnumerable<XAttribute> GetNamespaceAttributes()
    {
        foreach (var attr in base.GetNamespaceAttributes())
        {
            yield return attr;
        }

        yield return new XAttribute(XNamespace.Xmlns + "wfs", WfsConstants.Wfs);
        yield return new XAttribute(XNamespace.Xmlns + "ows", WfsConstants.Ows);
        yield return new XAttribute(XNamespace.Xmlns + "gml", WfsConstants.Gml);
        yield return new XAttribute(XNamespace.Xmlns + "fes", WfsConstants.Fes);
        yield return new XAttribute(Xsi + "schemaLocation",
            "http://www.opengis.net/wfs/2.0 http://schemas.opengis.net/wfs/2.0/wfs.xsd");
    }

    protected override IEnumerable<string> GetSupportedOperations()
    {
        yield return "GetCapabilities";
        yield return "DescribeFeatureType";
        yield return "GetFeature";
        yield return "GetPropertyValue";
        yield return "ListStoredQueries";
        yield return "DescribeStoredQueries";
        yield return "GetFeatureWithLock";
        yield return "LockFeature";
        yield return "Transaction";
    }

    protected override bool SupportsPost(string operationName)
    {
        // WFS 2.0 requires POST for LockFeature and Transaction
        return operationName is "LockFeature" or "Transaction";
    }

    protected override void AddOperationsMetadataExtensions(XElement element, XNamespace ns)
    {
        // Add WFS 2.0 conformance constraints
        element.Add(
            new XElement(ns + "Constraint",
                new XAttribute("name", "ImplementsSimpleWFS"),
                new XElement(ns + "NoValues"),
                new XElement(ns + "DefaultValue", "TRUE")),
            new XElement(ns + "Constraint",
                new XAttribute("name", "ImplementsBasicWFS"),
                new XElement(ns + "NoValues"),
                new XElement(ns + "DefaultValue", "TRUE")),
            new XElement(ns + "Constraint",
                new XAttribute("name", "ImplementsTransactionalWFS"),
                new XElement(ns + "NoValues"),
                new XElement(ns + "DefaultValue", "TRUE")),
            new XElement(ns + "Constraint",
                new XAttribute("name", "ImplementsLockingWFS"),
                new XElement(ns + "NoValues"),
                new XElement(ns + "DefaultValue", "TRUE")));
    }

    /// <summary>
    /// Builds the complete Filter_Capabilities section for WFS 2.0.
    /// </summary>
    private XElement BuildFilterCapabilities()
    {
        var fesNs = WfsConstants.Fes;

        var filterCaps = new XElement(fesNs + "Filter_Capabilities");

        // Conformance declaration
        filterCaps.Add(
            new XElement(fesNs + "Conformance",
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsQuery"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE")),
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsAdHocQuery"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE")),
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsFunctions"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE")),
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsResourceId"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE")),
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsMinStandardFilter"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE")),
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsStandardFilter"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE")),
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsMinSpatialFilter"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE")),
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsSpatialFilter"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE")),
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsMinTemporalFilter"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE")),
                new XElement(fesNs + "Constraint",
                    new XAttribute("name", "ImplementsTemporalFilter"),
                    new XElement(fesNs + "NoValues"),
                    new XElement(fesNs + "DefaultValue", "TRUE"))));

        // Scalar capabilities (comparison operators)
        filterCaps.Add(
            new XElement(fesNs + "Scalar_Capabilities",
                new XElement(fesNs + "LogicalOperators"),
                new XElement(fesNs + "ComparisonOperators",
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsEqualTo")),
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsNotEqualTo")),
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsLessThan")),
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsGreaterThan")),
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsLessThanOrEqualTo")),
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsGreaterThanOrEqualTo")),
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsLike")),
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsNull")),
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsNil")),
                    new XElement(fesNs + "ComparisonOperator", new XAttribute("name", "PropertyIsBetween")))));

        // Spatial capabilities
        filterCaps.Add(
            new XElement(fesNs + "Spatial_Capabilities",
                new XElement(fesNs + "GeometryOperands",
                    new XElement(fesNs + "GeometryOperand", new XAttribute("name", "gml:Envelope")),
                    new XElement(fesNs + "GeometryOperand", new XAttribute("name", "gml:Point")),
                    new XElement(fesNs + "GeometryOperand", new XAttribute("name", "gml:LineString")),
                    new XElement(fesNs + "GeometryOperand", new XAttribute("name", "gml:Polygon")),
                    new XElement(fesNs + "GeometryOperand", new XAttribute("name", "gml:MultiPoint")),
                    new XElement(fesNs + "GeometryOperand", new XAttribute("name", "gml:MultiLineString")),
                    new XElement(fesNs + "GeometryOperand", new XAttribute("name", "gml:MultiPolygon")),
                    new XElement(fesNs + "GeometryOperand", new XAttribute("name", "gml:MultiGeometry"))),
                new XElement(fesNs + "SpatialOperators",
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "BBOX")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "Equals")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "Disjoint")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "Intersects")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "Touches")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "Crosses")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "Within")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "Contains")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "Overlaps")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "Beyond")),
                    new XElement(fesNs + "SpatialOperator", new XAttribute("name", "DWithin")))));

        // Temporal capabilities
        filterCaps.Add(
            new XElement(fesNs + "Temporal_Capabilities",
                new XElement(fesNs + "TemporalOperands",
                    new XElement(fesNs + "TemporalOperand", new XAttribute("name", "gml:TimeInstant")),
                    new XElement(fesNs + "TemporalOperand", new XAttribute("name", "gml:TimePeriod"))),
                new XElement(fesNs + "TemporalOperators",
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "After")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "Before")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "Begins")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "BegunBy")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "TContains")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "During")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "TEquals")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "TOverlaps")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "Meets")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "OverlappedBy")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "MetBy")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "Ends")),
                    new XElement(fesNs + "TemporalOperator", new XAttribute("name", "EndedBy")))));

        // Function capabilities
        filterCaps.Add(
            new XElement(fesNs + "Functions",
                new XElement(fesNs + "Function",
                    new XAttribute("name", "area"),
                    new XElement(fesNs + "Returns", "xs:double"),
                    new XElement(fesNs + "Arguments",
                        new XElement(fesNs + "Argument",
                            new XAttribute("name", "geometry"),
                            new XElement(fesNs + "Type", "gml:GeometryPropertyType")))),
                new XElement(fesNs + "Function",
                    new XAttribute("name", "length"),
                    new XElement(fesNs + "Returns", "xs:double"),
                    new XElement(fesNs + "Arguments",
                        new XElement(fesNs + "Argument",
                            new XAttribute("name", "geometry"),
                            new XElement(fesNs + "Type", "gml:GeometryPropertyType")))),
                new XElement(fesNs + "Function",
                    new XAttribute("name", "buffer"),
                    new XElement(fesNs + "Returns", "gml:GeometryPropertyType"),
                    new XElement(fesNs + "Arguments",
                        new XElement(fesNs + "Argument",
                            new XAttribute("name", "geometry"),
                            new XElement(fesNs + "Type", "gml:GeometryPropertyType")),
                        new XElement(fesNs + "Argument",
                            new XAttribute("name", "distance"),
                            new XElement(fesNs + "Type", "xs:double"))))));

        return filterCaps;
    }

    protected override Task AddProtocolSpecificSectionsAsync(XElement root, MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken)
    {
        root.Add(BuildFeatureTypeList(metadata));
        root.Add(BuildFilterCapabilities());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the FeatureTypeList section containing all available feature types.
    /// </summary>
    private XElement BuildFeatureTypeList(MetadataSnapshot metadata)
    {
        var featureTypeList = new XElement(WfsConstants.Wfs + "FeatureTypeList");

        foreach (var service in metadata.Services.Where(s => s.Enabled && s.Ogc.CollectionsEnabled))
        {
            // Add regular layers
            foreach (var layer in service.Layers)
            {
                featureTypeList.Add(BuildFeatureType(service, layer));
            }

            // Add layer groups if WFS is enabled for the service
            if (service.Ogc.WfsEnabled)
            {
                var serviceLayerGroups = metadata.LayerGroups
                    .Where(lg => lg.Enabled &&
                                string.Equals(lg.ServiceId, service.Id, StringComparison.OrdinalIgnoreCase) &&
                                lg.Queryable)
                    .OrderBy(lg => lg.Title, StringComparer.OrdinalIgnoreCase);

                foreach (var layerGroup in serviceLayerGroups)
                {
                    featureTypeList.Add(BuildLayerGroupFeatureType(service, layerGroup, metadata));
                }
            }
        }

        return featureTypeList;
    }

    /// <summary>
    /// Builds a FeatureType element for a specific layer.
    /// </summary>
    private XElement BuildFeatureType(ServiceDefinition service, LayerDefinition layer)
    {
        var baseCrs = layer.Crs.FirstOrDefault() ?? service.Ogc.DefaultCrs ?? "EPSG:4326";
        var qualifiedName = $"{service.Id}:{layer.Id}";

        var featureType = new XElement(WfsConstants.Wfs + "FeatureType",
            new XElement(WfsConstants.Wfs + "Name", qualifiedName),
            new XElement(WfsConstants.Wfs + "Title", layer.Title ?? layer.Id),
            new XElement(WfsConstants.Wfs + "DefaultCRS", WfsHelpers.ToUrn(baseCrs)));

        // Add abstract if present
        if (layer.Description.HasValue())
        {
            featureType.Add(new XElement(WfsConstants.Wfs + "Abstract", layer.Description));
        }

        // Add keywords
        if (layer.Catalog.Keywords.Count > 0)
        {
            var keywords = new XElement(WfsConstants.Ows + "Keywords");
            foreach (var keyword in layer.Catalog.Keywords)
            {
                if (keyword.HasValue())
                {
                    keywords.Add(new XElement(WfsConstants.Ows + "Keyword", keyword));
                }
            }
            featureType.Add(keywords);
        }

        // Add other supported CRS
        foreach (var crs in layer.Crs.Skip(1))
        {
            featureType.Add(new XElement(WfsConstants.Wfs + "OtherCRS", WfsHelpers.ToUrn(crs)));
        }

        // Add output formats
        featureType.Add(new XElement(WfsConstants.Wfs + "OutputFormats",
            new XElement(WfsConstants.Wfs + "Format", WfsConstants.GmlFormat),
            new XElement(WfsConstants.Wfs + "Format", WfsConstants.GeoJsonFormat),
            new XElement(WfsConstants.Wfs + "Format", WfsConstants.CsvFormat),
            new XElement(WfsConstants.Wfs + "Format", WfsConstants.ShapefileFormat)));

        // Add WGS84 bounding box if available (supports 2D and 3D)
        var bbox = layer.Extent?.Bbox?.FirstOrDefault();
        if (bbox is { Length: >= 4 })
        {
            var lowerCorner = bbox.Length >= 6
                ? $"{FormatDouble(bbox[0])} {FormatDouble(bbox[1])} {FormatDouble(bbox[2])}" // 3D: minX minY minZ
                : $"{FormatDouble(bbox[0])} {FormatDouble(bbox[1])}"; // 2D: minX minY

            var upperCorner = bbox.Length >= 6
                ? $"{FormatDouble(bbox[3])} {FormatDouble(bbox[4])} {FormatDouble(bbox[5])}" // 3D: maxX maxY maxZ
                : $"{FormatDouble(bbox[2])} {FormatDouble(bbox[3])}"; // 2D: maxX maxY

            var bboxElement = new XElement(WfsConstants.Wfs + "WGS84BoundingBox",
                new XElement(WfsConstants.Ows + "LowerCorner", lowerCorner),
                new XElement(WfsConstants.Ows + "UpperCorner", upperCorner));

            // Add dimensions attribute for 3D bounding boxes
            if (bbox.Length >= 6)
            {
                bboxElement.Add(new XAttribute("dimensions", "3"));
            }

            featureType.Add(bboxElement);
        }

        // Add metadata URL if present
        if (layer.Catalog?.Links?.FirstOrDefault() is { } link && link.Href.HasValue())
        {
            featureType.Add(new XElement(WfsConstants.Wfs + "MetadataURL",
                new XAttribute(XLink + "href", link.Href)));
        }

        return featureType;
    }

    /// <summary>
    /// Builds a FeatureType element for a layer group.
    /// </summary>
    private XElement BuildLayerGroupFeatureType(ServiceDefinition service, LayerGroupDefinition layerGroup, MetadataSnapshot metadata)
    {
        // Get supported CRS from the group or its members
        var supportedCrs = LayerGroupExpander.GetSupportedCrs(layerGroup, metadata);
        var baseCrs = supportedCrs.FirstOrDefault() ?? service.Ogc.DefaultCrs ?? "EPSG:4326";
        var qualifiedName = $"{service.Id}:{layerGroup.Id}";

        var featureType = new XElement(WfsConstants.Wfs + "FeatureType",
            new XElement(WfsConstants.Wfs + "Name", qualifiedName),
            new XElement(WfsConstants.Wfs + "Title", layerGroup.Title),
            new XElement(WfsConstants.Wfs + "DefaultCRS", WfsHelpers.ToUrn(baseCrs)));

        // Add abstract if present
        if (layerGroup.Description.HasValue())
        {
            featureType.Add(new XElement(WfsConstants.Wfs + "Abstract", layerGroup.Description));
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
            var keywordsElement = new XElement(WfsConstants.Ows + "Keywords");
            foreach (var keyword in keywords)
            {
                if (keyword.HasValue())
                {
                    keywordsElement.Add(new XElement(WfsConstants.Ows + "Keyword", keyword));
                }
            }
            featureType.Add(keywordsElement);
        }

        // Add other supported CRS
        foreach (var crs in supportedCrs.Skip(1))
        {
            featureType.Add(new XElement(WfsConstants.Wfs + "OtherCRS", WfsHelpers.ToUrn(crs)));
        }

        // Add output formats
        featureType.Add(new XElement(WfsConstants.Wfs + "OutputFormats",
            new XElement(WfsConstants.Wfs + "Format", WfsConstants.GmlFormat),
            new XElement(WfsConstants.Wfs + "Format", WfsConstants.GeoJsonFormat),
            new XElement(WfsConstants.Wfs + "Format", WfsConstants.CsvFormat),
            new XElement(WfsConstants.Wfs + "Format", WfsConstants.ShapefileFormat)));

        // Add WGS84 bounding box from calculated group extent (supports 2D and 3D)
        var extent = LayerGroupExpander.CalculateGroupExtent(layerGroup, metadata);
        if (extent?.Bbox is { Count: > 0 })
        {
            var bbox = extent.Bbox[0];
            if (bbox.Length >= 4)
            {
                var lowerCorner = bbox.Length >= 6
                    ? $"{FormatDouble(bbox[0])} {FormatDouble(bbox[1])} {FormatDouble(bbox[2])}" // 3D
                    : $"{FormatDouble(bbox[0])} {FormatDouble(bbox[1])}"; // 2D

                var upperCorner = bbox.Length >= 6
                    ? $"{FormatDouble(bbox[3])} {FormatDouble(bbox[4])} {FormatDouble(bbox[5])}" // 3D
                    : $"{FormatDouble(bbox[2])} {FormatDouble(bbox[3])}"; // 2D

                var bboxElement = new XElement(WfsConstants.Wfs + "WGS84BoundingBox",
                    new XElement(WfsConstants.Ows + "LowerCorner", lowerCorner),
                    new XElement(WfsConstants.Ows + "UpperCorner", upperCorner));

                if (bbox.Length >= 6)
                {
                    bboxElement.Add(new XAttribute("dimensions", "3"));
                }

                featureType.Add(bboxElement);
            }
        }

        // Add metadata URL if present
        if (layerGroup.Links?.FirstOrDefault() is { } link && link.Href.HasValue())
        {
            featureType.Add(new XElement(WfsConstants.Wfs + "MetadataURL",
                new XAttribute(XLink + "href", link.Href)));
        }

        return featureType;
    }
}
