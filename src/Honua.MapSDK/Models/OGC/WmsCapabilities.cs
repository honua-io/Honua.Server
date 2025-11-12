// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Xml.Serialization;

namespace Honua.MapSDK.Models.OGC;

/// <summary>
/// Represents WMS GetCapabilities response
/// </summary>
public class WmsCapabilities
{
    public required string Version { get; set; }
    public required WmsService Service { get; set; }
    public required WmsCapability Capability { get; set; }
}

/// <summary>
/// WMS Service metadata
/// </summary>
public class WmsService
{
    public required string Name { get; set; }
    public required string Title { get; set; }
    public string? Abstract { get; set; }
    public List<string> Keywords { get; set; } = new();
    public WmsOnlineResource? OnlineResource { get; set; }
    public WmsContact? ContactInformation { get; set; }
    public string? Fees { get; set; }
    public string? AccessConstraints { get; set; }
    public int? LayerLimit { get; set; }
    public int? MaxWidth { get; set; }
    public int? MaxHeight { get; set; }
}

/// <summary>
/// WMS Capability information
/// </summary>
public class WmsCapability
{
    public required WmsRequest Request { get; set; }
    public WmsException? Exception { get; set; }
    public WmsLayer? Layer { get; set; }
}

/// <summary>
/// WMS Request information
/// </summary>
public class WmsRequest
{
    public WmsOperation? GetCapabilities { get; set; }
    public WmsOperation? GetMap { get; set; }
    public WmsOperation? GetFeatureInfo { get; set; }
    public WmsOperation? DescribeLayer { get; set; }
    public WmsOperation? GetLegendGraphic { get; set; }
}

/// <summary>
/// WMS Operation details
/// </summary>
public class WmsOperation
{
    public List<string> Formats { get; set; } = new();
    public List<WmsDcpType> DcpType { get; set; } = new();
}

/// <summary>
/// WMS DCP (Distributed Computing Platform) type
/// </summary>
public class WmsDcpType
{
    public WmsHttp? Http { get; set; }
}

/// <summary>
/// WMS HTTP methods
/// </summary>
public class WmsHttp
{
    public WmsOnlineResource? Get { get; set; }
    public WmsOnlineResource? Post { get; set; }
}

/// <summary>
/// WMS Online Resource (URL)
/// </summary>
public class WmsOnlineResource
{
    public required string Href { get; set; }
}

/// <summary>
/// WMS Exception information
/// </summary>
public class WmsException
{
    public List<string> Formats { get; set; } = new();
}

/// <summary>
/// WMS Layer definition
/// </summary>
public class WmsLayer
{
    public string? Name { get; set; }
    public required string Title { get; set; }
    public string? Abstract { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> Crs { get; set; } = new();
    public List<string> Srs { get; set; } = new(); // WMS 1.1.1
    public WmsBoundingBox? BoundingBox { get; set; }
    public WmsGeographicBoundingBox? GeographicBoundingBox { get; set; }
    public WmsExtent? Extent { get; set; }
    public WmsAttribution? Attribution { get; set; }
    public List<WmsLayer> Layers { get; set; } = new();
    public List<WmsStyle> Styles { get; set; } = new();
    public List<WmsDimension> Dimensions { get; set; } = new();
    public bool Queryable { get; set; }
    public bool Opaque { get; set; }
    public bool NoSubsets { get; set; }
    public int? FixedWidth { get; set; }
    public int? FixedHeight { get; set; }
    public double? MinScaleDenominator { get; set; }
    public double? MaxScaleDenominator { get; set; }
}

/// <summary>
/// WMS BoundingBox in specific CRS
/// </summary>
public class WmsBoundingBox
{
    public required string Crs { get; set; }
    public required double MinX { get; set; }
    public required double MinY { get; set; }
    public required double MaxX { get; set; }
    public required double MaxY { get; set; }
    public double? ResX { get; set; }
    public double? ResY { get; set; }
}

/// <summary>
/// WMS Geographic Bounding Box (WGS84)
/// </summary>
public class WmsGeographicBoundingBox
{
    public required double WestLongitude { get; set; }
    public required double EastLongitude { get; set; }
    public required double SouthLatitude { get; set; }
    public required double NorthLatitude { get; set; }
}

/// <summary>
/// WMS Geographic Extent for backward compatibility with 1.1.1
/// </summary>
public class WmsExtent
{
    public required double MinX { get; set; }
    public required double MinY { get; set; }
    public required double MaxX { get; set; }
    public required double MaxY { get; set; }
}

/// <summary>
/// WMS Style definition
/// </summary>
public class WmsStyle
{
    public required string Name { get; set; }
    public required string Title { get; set; }
    public string? Abstract { get; set; }
    public WmsLegendUrl? LegendUrl { get; set; }
    public WmsStyleSheetUrl? StyleSheetUrl { get; set; }
    public WmsStyleUrl? StyleUrl { get; set; }
}

/// <summary>
/// WMS Legend URL
/// </summary>
public class WmsLegendUrl
{
    public required string Format { get; set; }
    public required WmsOnlineResource OnlineResource { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <summary>
/// WMS StyleSheet URL
/// </summary>
public class WmsStyleSheetUrl
{
    public required string Format { get; set; }
    public required WmsOnlineResource OnlineResource { get; set; }
}

/// <summary>
/// WMS Style URL
/// </summary>
public class WmsStyleUrl
{
    public required string Format { get; set; }
    public required WmsOnlineResource OnlineResource { get; set; }
}

/// <summary>
/// WMS Dimension (e.g., time, elevation)
/// </summary>
public class WmsDimension
{
    public required string Name { get; set; }
    public required string Units { get; set; }
    public string? UnitSymbol { get; set; }
    public string? Default { get; set; }
    public bool MultipleValues { get; set; }
    public bool NearestValue { get; set; }
    public bool Current { get; set; }
    public string? Value { get; set; }
}

/// <summary>
/// WMS Attribution
/// </summary>
public class WmsAttribution
{
    public string? Title { get; set; }
    public WmsOnlineResource? OnlineResource { get; set; }
    public WmsLogoUrl? LogoUrl { get; set; }
}

/// <summary>
/// WMS Logo URL
/// </summary>
public class WmsLogoUrl
{
    public required string Format { get; set; }
    public required WmsOnlineResource OnlineResource { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <summary>
/// WMS Contact information
/// </summary>
public class WmsContact
{
    public string? Person { get; set; }
    public string? Organization { get; set; }
    public string? Position { get; set; }
    public WmsAddress? Address { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// WMS Contact address
/// </summary>
public class WmsAddress
{
    public string? Type { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostCode { get; set; }
    public string? Country { get; set; }
}
