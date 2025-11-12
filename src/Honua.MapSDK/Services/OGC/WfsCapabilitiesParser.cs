// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Honua.MapSDK.Models.OGC;

namespace Honua.MapSDK.Services.OGC;

/// <summary>
/// Parser for WFS GetCapabilities XML responses
/// Supports WFS 1.0.0, 1.1.0, and 2.0.0
/// </summary>
public class WfsCapabilitiesParser
{
    public WfsCapabilities Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid XML: no root element");

        var version = root.Attribute("version")?.Value ?? "2.0.0";

        return new WfsCapabilities
        {
            Version = version,
            Service = ParseService(root, version),
            Capability = ParseCapability(root, version),
            FeatureTypeList = ParseFeatureTypeList(root, version),
            FilterCapabilities = ParseFilterCapabilities(root, version)
        };
    }

    private WfsService ParseService(XElement root, string version)
    {
        var serviceElement = root.Element(GetName("ServiceIdentification", version))
            ?? root.Element(GetName("Service", version))
            ?? throw new InvalidOperationException("Service element not found");

        return new WfsService
        {
            Name = GetElementValue(serviceElement, "Name", version)
                ?? GetElementValue(serviceElement, "ServiceType", version)
                ?? "WFS",
            Title = GetElementValue(serviceElement, "Title", version) ?? "Unknown WFS Service",
            Abstract = GetElementValue(serviceElement, "Abstract", version),
            Keywords = GetKeywords(serviceElement, version),
            Fees = GetElementValue(serviceElement, "Fees", version),
            AccessConstraints = GetElementValue(serviceElement, "AccessConstraints", version)
        };
    }

    private WfsCapability ParseCapability(XElement root, string version)
    {
        var capabilityElement = root.Element(GetName("OperationsMetadata", version))
            ?? root.Element(GetName("Capability", version))
            ?? throw new InvalidOperationException("Capability element not found");

        return new WfsCapability
        {
            Request = ParseRequest(capabilityElement, version)
        };
    }

    private WfsRequest ParseRequest(XElement capabilityElement, string version)
    {
        var request = new WfsRequest();

        if (version.StartsWith("2.0"))
        {
            // WFS 2.0 uses OperationsMetadata/Operation
            foreach (var operation in capabilityElement.Elements(GetName("Operation", version)))
            {
                var name = operation.Attribute("name")?.Value;
                var wfsOperation = ParseOperationElement(operation, version);

                switch (name)
                {
                    case "GetCapabilities":
                        request.GetCapabilities = wfsOperation;
                        break;
                    case "DescribeFeatureType":
                        request.DescribeFeatureType = wfsOperation;
                        break;
                    case "GetFeature":
                        request.GetFeature = wfsOperation;
                        break;
                    case "Transaction":
                        request.Transaction = wfsOperation;
                        break;
                    case "LockFeature":
                        request.LockFeature = wfsOperation;
                        break;
                }
            }
        }
        else
        {
            // WFS 1.x uses Capability/Request
            var requestElement = capabilityElement.Element(GetName("Request", version));
            if (requestElement != null)
            {
                request.GetCapabilities = ParseOperationElement(requestElement.Element(GetName("GetCapabilities", version)), version);
                request.DescribeFeatureType = ParseOperationElement(requestElement.Element(GetName("DescribeFeatureType", version)), version);
                request.GetFeature = ParseOperationElement(requestElement.Element(GetName("GetFeature", version)), version);
                request.Transaction = ParseOperationElement(requestElement.Element(GetName("Transaction", version)), version);
                request.LockFeature = ParseOperationElement(requestElement.Element(GetName("LockFeature", version)), version);
            }
        }

        return request;
    }

    private WfsOperation? ParseOperationElement(XElement? operationElement, string version)
    {
        if (operationElement == null) return null;

        var operation = new WfsOperation();

        if (version.StartsWith("2.0"))
        {
            // WFS 2.0 format
            foreach (var param in operationElement.Elements(GetName("Parameter", version)))
            {
                var name = param.Attribute("name")?.Value;
                if (name == "outputFormat")
                {
                    foreach (var value in param.Elements(GetName("AllowedValues", version))
                        .Elements(GetName("Value", version)))
                    {
                        var formatValue = value.Value;
                        if (!string.IsNullOrEmpty(formatValue))
                        {
                            operation.Formats.Add(formatValue);
                        }
                    }
                }
            }

            // DCP (Distributed Computing Platform)
            foreach (var dcp in operationElement.Elements(GetName("DCP", version)))
            {
                var httpElement = dcp.Element(GetName("HTTP", version));
                if (httpElement != null)
                {
                    var dcpType = new WmsDcpType
                    {
                        Http = new WmsHttp()
                    };

                    var getElement = httpElement.Element(GetName("Get", version));
                    if (getElement != null)
                    {
                        var href = getElement.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"))?.Value;
                        if (href != null)
                        {
                            dcpType.Http.Get = new WmsOnlineResource { Href = href };
                        }
                    }

                    var postElement = httpElement.Element(GetName("Post", version));
                    if (postElement != null)
                    {
                        var href = postElement.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"))?.Value;
                        if (href != null)
                        {
                            dcpType.Http.Post = new WmsOnlineResource { Href = href };
                        }
                    }

                    operation.DcpType.Add(dcpType);
                }
            }
        }
        else
        {
            // WFS 1.x format
            foreach (var resultFormat in operationElement.Elements(GetName("ResultFormat", version)))
            {
                foreach (var format in resultFormat.Elements())
                {
                    operation.Formats.Add(format.Name.LocalName);
                }
            }

            foreach (var dcpType in operationElement.Elements(GetName("DCPType", version)))
            {
                var httpElement = dcpType.Element(GetName("HTTP", version));
                if (httpElement != null)
                {
                    var parsedDcp = new WmsDcpType
                    {
                        Http = new WmsHttp()
                    };

                    var getElement = httpElement.Element(GetName("Get", version));
                    if (getElement != null)
                    {
                        var href = getElement.Attribute("onlineResource")?.Value;
                        if (href != null)
                        {
                            parsedDcp.Http.Get = new WmsOnlineResource { Href = href };
                        }
                    }

                    operation.DcpType.Add(parsedDcp);
                }
            }
        }

        return operation;
    }

    private WfsFeatureTypeList ParseFeatureTypeList(XElement root, string version)
    {
        var featureTypeListElement = root.Element(GetName("FeatureTypeList", version))
            ?? throw new InvalidOperationException("FeatureTypeList element not found");

        var featureTypeList = new WfsFeatureTypeList();

        foreach (var featureTypeElement in featureTypeListElement.Elements(GetName("FeatureType", version)))
        {
            featureTypeList.FeatureTypes.Add(ParseFeatureType(featureTypeElement, version));
        }

        return featureTypeList;
    }

    private WfsFeatureType ParseFeatureType(XElement featureTypeElement, string version)
    {
        var featureType = new WfsFeatureType
        {
            Name = GetElementValue(featureTypeElement, "Name", version) ?? "Unknown",
            Title = GetElementValue(featureTypeElement, "Title", version) ?? "Unknown Feature Type",
            Abstract = GetElementValue(featureTypeElement, "Abstract", version),
            Keywords = GetKeywords(featureTypeElement, version)
        };

        if (version.StartsWith("2.0"))
        {
            // WFS 2.0
            featureType.DefaultCrs = GetMultipleElementValues(featureTypeElement, "DefaultCRS", version);
            featureType.OtherCrs = GetMultipleElementValues(featureTypeElement, "OtherCRS", version);
        }
        else
        {
            // WFS 1.x
            featureType.Srs = GetMultipleElementValues(featureTypeElement, "SRS", version);
        }

        // Parse bounding boxes
        var bboxElement = featureTypeElement.Element(GetName("WGS84BoundingBox", version))
            ?? featureTypeElement.Element(GetName("LatLongBoundingBox", version));

        if (bboxElement != null)
        {
            if (version.StartsWith("2.0"))
            {
                var lowerCorner = GetElementValue(bboxElement, "LowerCorner", version)?.Split(' ');
                var upperCorner = GetElementValue(bboxElement, "UpperCorner", version)?.Split(' ');

                if (lowerCorner?.Length == 2 && upperCorner?.Length == 2)
                {
                    featureType.GeographicBoundingBox = new WmsGeographicBoundingBox
                    {
                        WestLongitude = double.Parse(lowerCorner[0]),
                        SouthLatitude = double.Parse(lowerCorner[1]),
                        EastLongitude = double.Parse(upperCorner[0]),
                        NorthLatitude = double.Parse(upperCorner[1])
                    };
                }
            }
            else
            {
                featureType.GeographicBoundingBox = new WmsGeographicBoundingBox
                {
                    WestLongitude = double.Parse(bboxElement.Attribute("minx")?.Value ?? "0"),
                    SouthLatitude = double.Parse(bboxElement.Attribute("miny")?.Value ?? "0"),
                    EastLongitude = double.Parse(bboxElement.Attribute("maxx")?.Value ?? "0"),
                    NorthLatitude = double.Parse(bboxElement.Attribute("maxy")?.Value ?? "0")
                };
            }
        }

        return featureType;
    }

    private WfsFilterCapabilities? ParseFilterCapabilities(XElement root, string version)
    {
        var filterCapsElement = root.Element(GetName("Filter_Capabilities", version));
        if (filterCapsElement == null) return null;

        var filterCapabilities = new WfsFilterCapabilities();

        // Parse spatial capabilities
        var spatialCapsElement = filterCapsElement.Element(GetName("Spatial_Capabilities", version));
        if (spatialCapsElement != null)
        {
            filterCapabilities.Spatial = new WfsSpatialCapabilities
            {
                SpatialOperators = spatialCapsElement.Elements(GetName("Spatial_Operators", version))
                    .Elements()
                    .Select(e => e.Name.LocalName)
                    .ToList()
            };
        }

        // Parse scalar capabilities
        var scalarCapsElement = filterCapsElement.Element(GetName("Scalar_Capabilities", version));
        if (scalarCapsElement != null)
        {
            filterCapabilities.Scalar = new WfsScalarCapabilities
            {
                LogicalOperators = scalarCapsElement.Element(GetName("Logical_Operators", version)) != null,
                ComparisonOperators = scalarCapsElement.Elements(GetName("Comparison_Operators", version))
                    .Elements()
                    .Select(e => e.Name.LocalName)
                    .ToList()
            };
        }

        return filterCapabilities;
    }

    private List<string> GetKeywords(XElement element, string version)
    {
        var keywords = new List<string>();

        if (version.StartsWith("2.0"))
        {
            // WFS 2.0 format
            var keywordsElement = element.Element(GetName("Keywords", version));
            if (keywordsElement != null)
            {
                foreach (var keyword in keywordsElement.Elements(GetName("Keyword", version)))
                {
                    var value = keyword.Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        keywords.Add(value);
                    }
                }
            }
        }
        else
        {
            // WFS 1.x format
            foreach (var keyword in element.Elements(GetName("Keywords", version)))
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
        // WFS uses different namespaces based on version
        if (version.StartsWith("2.0"))
        {
            return localName switch
            {
                "ServiceIdentification" or "OperationsMetadata" or "Operation" or "Parameter" or
                "AllowedValues" or "Value" or "DCP" => XName.Get(localName, "http://www.opengis.net/ows/1.1"),
                "LowerCorner" or "UpperCorner" => XName.Get(localName, "http://www.opengis.net/ows/1.1"),
                _ => XName.Get(localName, "http://www.opengis.net/wfs/2.0")
            };
        }
        else if (version.StartsWith("1.1"))
        {
            return XName.Get(localName, "http://www.opengis.net/wfs");
        }
        else
        {
            return XName.Get(localName);
        }
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
}
