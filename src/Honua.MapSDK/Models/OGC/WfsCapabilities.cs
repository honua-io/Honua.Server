// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.OGC;

/// <summary>
/// Represents WFS GetCapabilities response
/// </summary>
public class WfsCapabilities
{
    public required string Version { get; set; }
    public required WfsService Service { get; set; }
    public required WfsCapability Capability { get; set; }
    public required WfsFeatureTypeList FeatureTypeList { get; set; }
    public WfsFilterCapabilities? FilterCapabilities { get; set; }
}

/// <summary>
/// WFS Service metadata
/// </summary>
public class WfsService
{
    public required string Name { get; set; }
    public required string Title { get; set; }
    public string? Abstract { get; set; }
    public List<string> Keywords { get; set; } = new();
    public WmsOnlineResource? OnlineResource { get; set; }
    public string? Fees { get; set; }
    public string? AccessConstraints { get; set; }
}

/// <summary>
/// WFS Capability information
/// </summary>
public class WfsCapability
{
    public required WfsRequest Request { get; set; }
}

/// <summary>
/// WFS Request operations
/// </summary>
public class WfsRequest
{
    public WfsOperation? GetCapabilities { get; set; }
    public WfsOperation? DescribeFeatureType { get; set; }
    public WfsOperation? GetFeature { get; set; }
    public WfsOperation? Transaction { get; set; }
    public WfsOperation? LockFeature { get; set; }
}

/// <summary>
/// WFS Operation details
/// </summary>
public class WfsOperation
{
    public List<string> Formats { get; set; } = new();
    public List<WmsDcpType> DcpType { get; set; } = new();
}

/// <summary>
/// WFS Feature Type List
/// </summary>
public class WfsFeatureTypeList
{
    public WfsOperations? Operations { get; set; }
    public List<WfsFeatureType> FeatureTypes { get; set; } = new();
}

/// <summary>
/// WFS Operations on feature types
/// </summary>
public class WfsOperations
{
    public List<string> Query { get; set; } = new();
    public List<string> Insert { get; set; } = new();
    public List<string> Update { get; set; } = new();
    public List<string> Delete { get; set; } = new();
    public List<string> Lock { get; set; } = new();
}

/// <summary>
/// WFS Feature Type definition
/// </summary>
public class WfsFeatureType
{
    public required string Name { get; set; }
    public required string Title { get; set; }
    public string? Abstract { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> DefaultCrs { get; set; } = new(); // WFS 2.0
    public List<string> OtherCrs { get; set; } = new(); // WFS 2.0
    public List<string> Srs { get; set; } = new(); // WFS 1.1.0
    public WfsOutputFormats? OutputFormats { get; set; }
    public WmsBoundingBox? BoundingBox { get; set; }
    public WmsGeographicBoundingBox? GeographicBoundingBox { get; set; }
    public List<string> Operations { get; set; } = new();
    public WfsMetadataUrl? MetadataUrl { get; set; }
}

/// <summary>
/// WFS Output formats for feature type
/// </summary>
public class WfsOutputFormats
{
    public List<string> Formats { get; set; } = new();
}

/// <summary>
/// WFS Metadata URL
/// </summary>
public class WfsMetadataUrl
{
    public required string Type { get; set; }
    public required string Format { get; set; }
    public required string Href { get; set; }
}

/// <summary>
/// WFS Filter Capabilities
/// </summary>
public class WfsFilterCapabilities
{
    public WfsSpatialCapabilities? Spatial { get; set; }
    public WfsScalarCapabilities? Scalar { get; set; }
    public WfsIdCapabilities? Id { get; set; }
}

/// <summary>
/// WFS Spatial Capabilities
/// </summary>
public class WfsSpatialCapabilities
{
    public List<string> GeometryOperands { get; set; } = new();
    public List<string> SpatialOperators { get; set; } = new();
}

/// <summary>
/// WFS Scalar Capabilities
/// </summary>
public class WfsScalarCapabilities
{
    public bool LogicalOperators { get; set; }
    public List<string> ComparisonOperators { get; set; } = new();
    public List<string> ArithmeticOperators { get; set; } = new();
}

/// <summary>
/// WFS ID Capabilities
/// </summary>
public class WfsIdCapabilities
{
    public List<string> ResourceIdentifiers { get; set; } = new();
}

/// <summary>
/// WFS Query configuration
/// </summary>
public class WfsQueryOptions
{
    public required string FeatureType { get; set; }
    public string Version { get; set; } = "2.0.0";
    public string? Srs { get; set; }
    public List<string>? PropertyNames { get; set; }
    public string? Filter { get; set; }
    public string? CqlFilter { get; set; }
    public double[]? BoundingBox { get; set; }
    public string? SortBy { get; set; }
    public int? MaxFeatures { get; set; }
    public int? StartIndex { get; set; }
    public string OutputFormat { get; set; } = "application/json";
}

/// <summary>
/// WFS GetFeature response
/// </summary>
public class WfsFeatureCollection
{
    public required string Type { get; set; }
    public int NumberOfFeatures { get; set; }
    public int NumberReturned { get; set; }
    public string? TimeStamp { get; set; }
    public List<WfsFeature> Features { get; set; } = new();
    public double[]? BoundingBox { get; set; }
    public string? Crs { get; set; }
}

/// <summary>
/// Individual WFS Feature
/// </summary>
public class WfsFeature
{
    public required string Type { get; set; }
    public string? Id { get; set; }
    public object? Geometry { get; set; }
    public string? GeometryName { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public double[]? BoundingBox { get; set; }
}
