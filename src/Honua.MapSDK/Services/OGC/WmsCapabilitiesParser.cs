// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Honua.MapSDK.Models.OGC;

namespace Honua.MapSDK.Services.OGC;

/// <summary>
/// Parser for WMS GetCapabilities XML responses
/// Supports WMS 1.1.0, 1.1.1, and 1.3.0
/// </summary>
public class WmsCapabilitiesParser
{
    public WmsCapabilities Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid XML: no root element");

        var version = root.Attribute("version")?.Value ?? "1.3.0";

        return new WmsCapabilities
        {
            Version = version,
            Service = ParseService(root, version),
            Capability = ParseCapability(root, version)
        };
    }

    private WmsService ParseService(XElement root, string version)
    {
        var serviceElement = root.Element(GetName("Service", version))
            ?? throw new InvalidOperationException("Service element not found");

        return new WmsService
        {
            Name = GetElementValue(serviceElement, "Name", version) ?? "WMS",
            Title = GetElementValue(serviceElement, "Title", version) ?? "Unknown WMS Service",
            Abstract = GetElementValue(serviceElement, "Abstract", version),
            Keywords = GetKeywords(serviceElement, version),
            OnlineResource = ParseOnlineResource(serviceElement.Element(GetName("OnlineResource", version))),
            ContactInformation = ParseContactInformation(serviceElement.Element(GetName("ContactInformation", version)), version),
            Fees = GetElementValue(serviceElement, "Fees", version),
            AccessConstraints = GetElementValue(serviceElement, "AccessConstraints", version),
            LayerLimit = GetIntValue(serviceElement, "LayerLimit", version),
            MaxWidth = GetIntValue(serviceElement, "MaxWidth", version),
            MaxHeight = GetIntValue(serviceElement, "MaxHeight", version)
        };
    }

    private WmsCapability ParseCapability(XElement root, string version)
    {
        var capabilityElement = root.Element(GetName("Capability", version))
            ?? throw new InvalidOperationException("Capability element not found");

        return new WmsCapability
        {
            Request = ParseRequest(capabilityElement, version),
            Exception = ParseException(capabilityElement, version),
            Layer = ParseLayer(capabilityElement.Element(GetName("Layer", version)), version)
        };
    }

    private WmsRequest ParseRequest(XElement capabilityElement, string version)
    {
        var requestElement = capabilityElement.Element(GetName("Request", version))
            ?? throw new InvalidOperationException("Request element not found");

        return new WmsRequest
        {
            GetCapabilities = ParseOperation(requestElement.Element(GetName("GetCapabilities", version)), version),
            GetMap = ParseOperation(requestElement.Element(GetName("GetMap", version)), version),
            GetFeatureInfo = ParseOperation(requestElement.Element(GetName("GetFeatureInfo", version)), version),
            DescribeLayer = ParseOperation(requestElement.Element(GetName("DescribeLayer", version)), version),
            GetLegendGraphic = ParseOperation(requestElement.Element(GetName("GetLegendGraphic", version)), version)
        };
    }

    private WmsOperation? ParseOperation(XElement? operationElement, string version)
    {
        if (operationElement == null) return null;

        var operation = new WmsOperation();

        foreach (var format in operationElement.Elements(GetName("Format", version)))
        {
            var formatValue = format.Value;
            if (!string.IsNullOrEmpty(formatValue))
            {
                operation.Formats.Add(formatValue);
            }
        }

        foreach (var dcpType in operationElement.Elements(GetName("DCPType", version)))
        {
            operation.DcpType.Add(ParseDcpType(dcpType, version));
        }

        return operation;
    }

    private WmsDcpType ParseDcpType(XElement dcpTypeElement, string version)
    {
        var httpElement = dcpTypeElement.Element(GetName("HTTP", version));

        return new WmsDcpType
        {
            Http = httpElement != null ? new WmsHttp
            {
                Get = ParseOnlineResource(httpElement.Element(GetName("Get", version))),
                Post = ParseOnlineResource(httpElement.Element(GetName("Post", version)))
            } : null
        };
    }

    private WmsException? ParseException(XElement capabilityElement, string version)
    {
        var exceptionElement = capabilityElement.Element(GetName("Exception", version));
        if (exceptionElement == null) return null;

        var exception = new WmsException();
        foreach (var format in exceptionElement.Elements(GetName("Format", version)))
        {
            var formatValue = format.Value;
            if (!string.IsNullOrEmpty(formatValue))
            {
                exception.Formats.Add(formatValue);
            }
        }

        return exception;
    }

    private WmsLayer? ParseLayer(XElement? layerElement, string version)
    {
        if (layerElement == null) return null;

        var layer = new WmsLayer
        {
            Name = GetElementValue(layerElement, "Name", version),
            Title = GetElementValue(layerElement, "Title", version) ?? "Untitled Layer",
            Abstract = GetElementValue(layerElement, "Abstract", version),
            Keywords = GetKeywords(layerElement, version),
            Queryable = GetBoolAttribute(layerElement, "queryable"),
            Opaque = GetBoolAttribute(layerElement, "opaque"),
            NoSubsets = GetBoolAttribute(layerElement, "noSubsets")
        };

        // Parse CRS/SRS
        if (version == "1.3.0")
        {
            layer.Crs = GetMultipleElementValues(layerElement, "CRS", version);
        }
        else
        {
            layer.Srs = GetMultipleElementValues(layerElement, "SRS", version);
        }

        // Parse bounding boxes
        ParseBoundingBoxes(layerElement, layer, version);

        // Parse styles
        foreach (var styleElement in layerElement.Elements(GetName("Style", version)))
        {
            layer.Styles.Add(ParseStyle(styleElement, version));
        }

        // Parse dimensions
        foreach (var dimensionElement in layerElement.Elements(GetName("Dimension", version)))
        {
            layer.Dimensions.Add(ParseDimension(dimensionElement, version));
        }

        // Parse scale denominators
        layer.MinScaleDenominator = GetDoubleValue(layerElement, "MinScaleDenominator", version);
        layer.MaxScaleDenominator = GetDoubleValue(layerElement, "MaxScaleDenominator", version);

        // Parse child layers recursively
        foreach (var childLayerElement in layerElement.Elements(GetName("Layer", version)))
        {
            var childLayer = ParseLayer(childLayerElement, version);
            if (childLayer != null)
            {
                layer.Layers.Add(childLayer);
            }
        }

        return layer;
    }

    private void ParseBoundingBoxes(XElement layerElement, WmsLayer layer, string version)
    {
        if (version == "1.3.0")
        {
            // WMS 1.3.0
            var geoBboxElement = layerElement.Element(GetName("EX_GeographicBoundingBox", version));
            if (geoBboxElement != null)
            {
                layer.GeographicBoundingBox = new WmsGeographicBoundingBox
                {
                    WestLongitude = double.Parse(GetElementValue(geoBboxElement, "westBoundLongitude", version) ?? "0"),
                    EastLongitude = double.Parse(GetElementValue(geoBboxElement, "eastBoundLongitude", version) ?? "0"),
                    SouthLatitude = double.Parse(GetElementValue(geoBboxElement, "southBoundLatitude", version) ?? "0"),
                    NorthLatitude = double.Parse(GetElementValue(geoBboxElement, "northBoundLatitude", version) ?? "0")
                };
            }

            // CRS-specific bounding box
            var bboxElement = layerElement.Element(GetName("BoundingBox", version));
            if (bboxElement != null)
            {
                layer.BoundingBox = ParseBoundingBox(bboxElement, version);
            }
        }
        else
        {
            // WMS 1.1.x - LatLonBoundingBox
            var latLonBboxElement = layerElement.Element(GetName("LatLonBoundingBox", version));
            if (latLonBboxElement != null)
            {
                layer.GeographicBoundingBox = new WmsGeographicBoundingBox
                {
                    WestLongitude = double.Parse(latLonBboxElement.Attribute("minx")?.Value ?? "0"),
                    EastLongitude = double.Parse(latLonBboxElement.Attribute("maxx")?.Value ?? "0"),
                    SouthLatitude = double.Parse(latLonBboxElement.Attribute("miny")?.Value ?? "0"),
                    NorthLatitude = double.Parse(latLonBboxElement.Attribute("maxy")?.Value ?? "0")
                };
            }

            var bboxElement = layerElement.Element(GetName("BoundingBox", version));
            if (bboxElement != null)
            {
                layer.BoundingBox = ParseBoundingBox(bboxElement, version);
            }
        }
    }

    private WmsBoundingBox? ParseBoundingBox(XElement bboxElement, string version)
    {
        var crs = bboxElement.Attribute("CRS")?.Value ?? bboxElement.Attribute("SRS")?.Value;
        if (crs == null) return null;

        return new WmsBoundingBox
        {
            Crs = crs,
            MinX = double.Parse(bboxElement.Attribute("minx")?.Value ?? "0"),
            MinY = double.Parse(bboxElement.Attribute("miny")?.Value ?? "0"),
            MaxX = double.Parse(bboxElement.Attribute("maxx")?.Value ?? "0"),
            MaxY = double.Parse(bboxElement.Attribute("maxy")?.Value ?? "0"),
            ResX = GetDoubleAttribute(bboxElement, "resx"),
            ResY = GetDoubleAttribute(bboxElement, "resy")
        };
    }

    private WmsStyle ParseStyle(XElement styleElement, string version)
    {
        return new WmsStyle
        {
            Name = GetElementValue(styleElement, "Name", version) ?? "default",
            Title = GetElementValue(styleElement, "Title", version) ?? "Default Style",
            Abstract = GetElementValue(styleElement, "Abstract", version),
            LegendUrl = ParseLegendUrl(styleElement.Element(GetName("LegendURL", version)), version)
        };
    }

    private WmsLegendUrl? ParseLegendUrl(XElement? legendElement, string version)
    {
        if (legendElement == null) return null;

        var onlineResource = legendElement.Element(GetName("OnlineResource", version));
        if (onlineResource == null) return null;

        return new WmsLegendUrl
        {
            Format = GetElementValue(legendElement, "Format", version) ?? "image/png",
            OnlineResource = ParseOnlineResource(onlineResource) ?? new WmsOnlineResource { Href = "" },
            Width = GetIntAttribute(legendElement, "width"),
            Height = GetIntAttribute(legendElement, "height")
        };
    }

    private WmsDimension ParseDimension(XElement dimensionElement, string version)
    {
        return new WmsDimension
        {
            Name = dimensionElement.Attribute("name")?.Value ?? "unknown",
            Units = dimensionElement.Attribute("units")?.Value ?? "",
            UnitSymbol = dimensionElement.Attribute("unitSymbol")?.Value,
            Default = dimensionElement.Attribute("default")?.Value,
            MultipleValues = GetBoolAttribute(dimensionElement, "multipleValues"),
            NearestValue = GetBoolAttribute(dimensionElement, "nearestValue"),
            Current = GetBoolAttribute(dimensionElement, "current"),
            Value = dimensionElement.Value
        };
    }

    private WmsOnlineResource? ParseOnlineResource(XElement? element)
    {
        if (element == null) return null;

        var href = element.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"))?.Value;
        if (href == null) return null;

        return new WmsOnlineResource { Href = href };
    }

    private WmsContact? ParseContactInformation(XElement? contactElement, string version)
    {
        if (contactElement == null) return null;

        var personPrimary = contactElement.Element(GetName("ContactPersonPrimary", version));
        var addressElement = contactElement.Element(GetName("ContactAddress", version));

        return new WmsContact
        {
            Person = GetElementValue(personPrimary, "ContactPerson", version),
            Organization = GetElementValue(personPrimary, "ContactOrganization", version),
            Position = GetElementValue(contactElement, "ContactPosition", version),
            Phone = GetElementValue(contactElement, "ContactVoiceTelephone", version),
            Fax = GetElementValue(contactElement, "ContactFacsimileTelephone", version),
            Email = GetElementValue(contactElement, "ContactElectronicMailAddress", version),
            Address = addressElement != null ? new WmsAddress
            {
                Type = GetElementValue(addressElement, "AddressType", version),
                Street = GetElementValue(addressElement, "Address", version),
                City = GetElementValue(addressElement, "City", version),
                StateOrProvince = GetElementValue(addressElement, "StateOrProvince", version),
                PostCode = GetElementValue(addressElement, "PostCode", version),
                Country = GetElementValue(addressElement, "Country", version)
            } : null
        };
    }

    private List<string> GetKeywords(XElement element, string version)
    {
        var keywords = new List<string>();
        var keywordListElement = element.Element(GetName("KeywordList", version));

        if (keywordListElement != null)
        {
            foreach (var keyword in keywordListElement.Elements(GetName("Keyword", version)))
            {
                var value = keyword.Value;
                if (!string.IsNullOrEmpty(value))
                {
                    keywords.Add(value);
                }
            }
        }

        return keywords;
    }

    private XName GetName(string localName, string version)
    {
        // WMS uses different namespaces based on version
        return version == "1.3.0"
            ? XName.Get(localName, "http://www.opengis.net/wms")
            : XName.Get(localName);
    }

    private string? GetElementValue(XElement? parent, string localName, string version)
    {
        return parent?.Element(GetName(localName, version))?.Value;
    }

    private List<string> GetMultipleElementValues(XElement parent, string localName, string version)
    {
        return parent.Elements(GetName(localName, version))
            .Select(e => e.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }

    private int? GetIntValue(XElement parent, string localName, string version)
    {
        var value = GetElementValue(parent, localName, version);
        return int.TryParse(value, out var result) ? result : null;
    }

    private double? GetDoubleValue(XElement parent, string localName, string version)
    {
        var value = GetElementValue(parent, localName, version);
        return double.TryParse(value, out var result) ? result : null;
    }

    private bool GetBoolAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        return value == "1" || value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    }

    private int? GetIntAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        return int.TryParse(value, out var result) ? result : null;
    }

    private double? GetDoubleAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        return double.TryParse(value, out var result) ? result : null;
    }
}
