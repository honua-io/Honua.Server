// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Wcs;

/// <summary>
/// WCS 2.0.1 capabilities builder implementing the OGC capabilities base class.
/// Handles WCS-specific capabilities generation including coverage summaries and service metadata.
/// </summary>
public sealed class WcsCapabilitiesBuilder : OgcCapabilitiesBuilder
{
    private static readonly XNamespace Wcs = "http://www.opengis.net/wcs/2.0";
    private static readonly XNamespace Crs = "http://www.opengis.net/wcs/service-extension/crs/1.0";
    private static readonly XNamespace Gml = "http://www.opengis.net/gml/3.2";

    private readonly IRasterDatasetRegistry _rasterRegistry;

    public WcsCapabilitiesBuilder(IRasterDatasetRegistry rasterRegistry)
    {
        _rasterRegistry = rasterRegistry ?? throw new System.ArgumentNullException(nameof(rasterRegistry));
    }

    protected override XName GetRootElementName() => Wcs + "Capabilities";

    protected override string GetServiceName() => "WCS";

    protected override string GetVersion() => "2.0.1";

    protected override string BuildEndpointUrl(HttpRequest request)
    {
        return $"{BuildBaseUrl(request)}/wcs";
    }

    protected override XNamespace GetOwsNamespace() => OwsV2;

    protected override IEnumerable<XAttribute> GetNamespaceAttributes()
    {
        foreach (var attr in base.GetNamespaceAttributes())
        {
            yield return attr;
        }

        yield return new XAttribute(XNamespace.Xmlns + "wcs", Wcs);
        yield return new XAttribute(XNamespace.Xmlns + "ows", OwsV2);
        yield return new XAttribute(XNamespace.Xmlns + "gml", Gml);
        yield return new XAttribute(Xsi + "schemaLocation",
            "http://www.opengis.net/wcs/2.0 http://schemas.opengis.net/wcs/2.0/wcsAll.xsd");
    }

    protected override IEnumerable<string> GetSupportedOperations()
    {
        yield return "GetCapabilities";
        yield return "DescribeCoverage";
        yield return "GetCoverage";
    }

    protected override bool SupportsPost(string operationName) => true;

    protected override void AddServiceIdentificationExtensions(XElement element, MetadataSnapshot metadata)
    {
        var ns = GetOwsNamespace();

        // Add WCS 2.0.1 profile
        element.Add(new XElement(ns + "Profile", "http://www.opengis.net/spec/WCS/2.0/conf/core"));
    }

    protected override async Task AddProtocolSpecificSectionsAsync(XElement root, MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken)
    {
        // PERFORMANCE FIX (Issue #40): Use async/await instead of blocking on GetAwaiter().GetResult()
        var coverages = await _rasterRegistry.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Add ServiceMetadata section
        root.Add(BuildServiceMetadata());

        // Add Contents section
        root.Add(BuildContents(coverages, request));
    }

    /// <summary>
    /// Builds the WCS ServiceMetadata section containing supported formats, CRS, and extensions.
    /// </summary>
    private XElement BuildServiceMetadata()
    {
        var metadata = new XElement(Wcs + "ServiceMetadata",
            new XElement(Wcs + "formatSupported", "image/tiff"),
            new XElement(Wcs + "formatSupported", "image/png"),
            new XElement(Wcs + "formatSupported", "image/jpeg"),
            new XElement(Wcs + "formatSupported", "image/jp2"),
            new XElement(Wcs + "formatSupported", "application/netcdf"));

        var extensionsElement = new XElement(Wcs + "Extension");

        // Add CRS extension with expanded list of supported CRS
        var crsMetadata = new XElement(Crs + "CrsMetadata");
        foreach (var crsUri in WcsCrsHelper.GetSupportedCrsUris())
        {
            crsMetadata.Add(new XElement(Crs + "crsSupported", crsUri));
        }
        extensionsElement.Add(crsMetadata);

        // Add Scaling extension (WCS 2.0 Scaling Extension)
        var scalingNs = XNamespace.Get("http://www.opengis.net/wcs/service-extension/scaling/1.0");
        var scalingMetadata = new XElement(scalingNs + "ScalingMetadata",
            new XAttribute(XNamespace.Xmlns + "scal", scalingNs),
            new XElement(scalingNs + "ScaleByFactor",
                new XElement(scalingNs + "Axis", "i"),
                new XElement(scalingNs + "Axis", "j")
            )
        );
        extensionsElement.Add(scalingMetadata);

        // Add Interpolation extension (WCS 2.0 Interpolation Extension)
        var interpolationNs = XNamespace.Get("http://www.opengis.net/wcs/service-extension/interpolation/1.0");
        var interpolationMetadata = new XElement(interpolationNs + "InterpolationMetadata",
            new XAttribute(XNamespace.Xmlns + "int", interpolationNs));
        foreach (var methodUri in WcsInterpolationHelper.GetSupportedInterpolationUris())
        {
            interpolationMetadata.Add(new XElement(interpolationNs + "InterpolationSupported", methodUri));
        }
        extensionsElement.Add(interpolationMetadata);

        // Add Range Subsetting extension (WCS 2.0 Range Subsetting Extension)
        var rangeNs = XNamespace.Get("http://www.opengis.net/wcs/service-extension/range-subsetting/1.0");
        var rangeMetadata = new XElement(rangeNs + "RangeSubsettingMetadata",
            new XAttribute(XNamespace.Xmlns + "rsub", rangeNs),
            new XElement(rangeNs + "FieldSemantics", "Band"),
            new XElement(rangeNs + "FieldSemantics", "Index"));
        extensionsElement.Add(rangeMetadata);

        metadata.Add(extensionsElement);

        return metadata;
    }

    /// <summary>
    /// Builds the WCS Contents section containing coverage summaries.
    /// </summary>
    private XElement BuildContents(IReadOnlyList<RasterDatasetDefinition> coverages, HttpRequest request)
    {
        var contents = new XElement(Wcs + "Contents");
        var endpoint = BuildEndpointUrl(request);

        foreach (var coverage in coverages.OrderBy(c => c.Id))
        {
            contents.Add(BuildCoverageSummary(coverage, endpoint));
        }

        return contents;
    }

    /// <summary>
    /// Builds a CoverageSummary element for a specific coverage.
    /// </summary>
    private XElement BuildCoverageSummary(RasterDatasetDefinition dataset, string baseUrl)
    {
        var summary = new XElement(Wcs + "CoverageSummary",
            new XElement(Wcs + "CoverageId", dataset.Id),
            new XElement(Wcs + "CoverageSubtype", "RectifiedGridCoverage"));

        // Add WGS84 bounding box if extent is available
        if (dataset.Extent?.Bbox is { Count: > 0 } bboxList)
        {
            var bbox = bboxList[0];
            if (bbox.Length >= 4)
            {
                var ns = GetOwsNamespace();
                summary.Add(new XElement(ns + "WGS84BoundingBox",
                    new XElement(ns + "LowerCorner", $"{FormatDouble(bbox[1])} {FormatDouble(bbox[0])}"),
                    new XElement(ns + "UpperCorner", $"{FormatDouble(bbox[3])} {FormatDouble(bbox[2])}")));
            }
        }

        return summary;
    }
}
