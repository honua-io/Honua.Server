// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Csw;

/// <summary>
/// CSW 2.0.2 capabilities builder implementing the OGC capabilities base class.
/// Handles CSW-specific capabilities generation including Filter_Capabilities and supported record types.
/// </summary>
public sealed class CswCapabilitiesBuilder : OgcCapabilitiesBuilder
{
    private static readonly XNamespace Csw = "http://www.opengis.net/cat/csw/2.0.2";
    private static readonly XNamespace Ogc = "http://www.opengis.net/ogc";

    protected override XName GetRootElementName() => Csw + "Capabilities";

    protected override string GetServiceName() => "CSW";

    protected override string GetVersion() => "2.0.2";

    protected override string BuildEndpointUrl(HttpRequest request)
    {
        return $"{BuildBaseUrl(request)}/csw";
    }

    protected override IEnumerable<XAttribute> GetNamespaceAttributes()
    {
        foreach (var attr in base.GetNamespaceAttributes())
        {
            yield return attr;
        }

        yield return new XAttribute(XNamespace.Xmlns + "csw", Csw);
        yield return new XAttribute(XNamespace.Xmlns + "ogc", Ogc);
        yield return new XAttribute(XNamespace.Xmlns + "ows", Ows);
        yield return new XAttribute(Xsi + "schemaLocation",
            "http://www.opengis.net/cat/csw/2.0.2 http://schemas.opengis.net/csw/2.0.2/CSW-discovery.xsd");
    }

    protected override IEnumerable<string> GetSupportedOperations()
    {
        yield return "GetCapabilities";
        yield return "DescribeRecord";
        yield return "GetRecords";
        yield return "GetRecordById";
        yield return "GetDomain";
    }

    protected override bool SupportsPost(string operationName)
    {
        // All CSW operations support POST
        return true;
    }

    protected override void AddOperationParameters(XElement operation, string operationName, XNamespace ns)
    {
        // Add outputSchema parameter for GetRecords and GetRecordById operations
        if (operationName == "GetRecords" || operationName == "GetRecordById")
        {
            operation.Add(new XElement(ns + "Parameter",
                new XAttribute("name", "outputSchema"),
                new XElement(ns + "Value", "http://www.opengis.net/cat/csw/2.0.2"),
                new XElement(ns + "Value", "http://www.isotc211.org/2005/gmd")));
        }
    }

    protected override void AddOperationsMetadataExtensions(XElement element, XNamespace ns)
    {
        // Add PostEncoding constraint (CSW supports XML POST requests)
        element.Add(new XElement(ns + "Constraint",
            new XAttribute("name", "PostEncoding"),
            new XElement(ns + "Value", "XML")));
    }

    protected override Task AddProtocolSpecificSectionsAsync(XElement root, MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken)
    {
        // Add Filter_Capabilities section (CSW-specific)
        root.Add(BuildFilterCapabilities());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the Filter_Capabilities section describing supported query capabilities.
    /// </summary>
    private XElement BuildFilterCapabilities()
    {
        return new XElement(Ogc + "Filter_Capabilities",
            BuildSpatialCapabilities(),
            BuildScalarCapabilities());
    }

    /// <summary>
    /// Builds the Spatial_Capabilities section with supported spatial operators.
    /// </summary>
    private XElement BuildSpatialCapabilities()
    {
        return new XElement(Ogc + "Spatial_Capabilities",
            new XElement(Ogc + "GeometryOperands",
                new XElement(Ogc + "GeometryOperand", "gml:Envelope"),
                new XElement(Ogc + "GeometryOperand", "gml:Point"),
                new XElement(Ogc + "GeometryOperand", "gml:Polygon")),
            new XElement(Ogc + "SpatialOperators",
                new XElement(Ogc + "SpatialOperator", new XAttribute("name", "BBOX")),
                new XElement(Ogc + "SpatialOperator", new XAttribute("name", "Intersects")),
                new XElement(Ogc + "SpatialOperator", new XAttribute("name", "Within"))));
    }

    /// <summary>
    /// Builds the Scalar_Capabilities section with supported comparison and logical operators.
    /// </summary>
    private XElement BuildScalarCapabilities()
    {
        return new XElement(Ogc + "Scalar_Capabilities",
            new XElement(Ogc + "LogicalOperators"),
            new XElement(Ogc + "ComparisonOperators",
                new XElement(Ogc + "ComparisonOperator", "EqualTo"),
                new XElement(Ogc + "ComparisonOperator", "Like"),
                new XElement(Ogc + "ComparisonOperator", "Between")));
    }
}
