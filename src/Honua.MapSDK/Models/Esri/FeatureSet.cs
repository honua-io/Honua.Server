// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Models.Esri;

/// <summary>
/// FeatureSet returned from query operations
/// </summary>
public class EsriFeatureSet
{
    /// <summary>
    /// Object ID field name
    /// </summary>
    [JsonPropertyName("objectIdFieldName")]
    public string? ObjectIdFieldName { get; set; }

    /// <summary>
    /// Global ID field name
    /// </summary>
    [JsonPropertyName("globalIdFieldName")]
    public string? GlobalIdFieldName { get; set; }

    /// <summary>
    /// Geometry type
    /// </summary>
    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; set; }

    /// <summary>
    /// Spatial reference
    /// </summary>
    [JsonPropertyName("spatialReference")]
    public EsriSpatialReference? SpatialReference { get; set; }

    /// <summary>
    /// Features in the result set
    /// </summary>
    [JsonPropertyName("features")]
    public List<EsriFeature> Features { get; set; } = new();

    /// <summary>
    /// Fields (only returned if includeFields=true)
    /// </summary>
    [JsonPropertyName("fields")]
    public List<EsriField>? Fields { get; set; }

    /// <summary>
    /// Whether there are more features to query
    /// </summary>
    [JsonPropertyName("exceededTransferLimit")]
    public bool ExceededTransferLimit { get; set; }
}

/// <summary>
/// A single feature
/// </summary>
public class EsriFeature
{
    /// <summary>
    /// Geometry (point, polyline, polygon, etc.)
    /// </summary>
    [JsonPropertyName("geometry")]
    public EsriGeometry? Geometry { get; set; }

    /// <summary>
    /// Attributes (properties)
    /// </summary>
    [JsonPropertyName("attributes")]
    public Dictionary<string, object?> Attributes { get; set; } = new();
}

/// <summary>
/// Base class for Esri geometries
/// </summary>
public class EsriGeometry
{
    /// <summary>
    /// Spatial reference
    /// </summary>
    [JsonPropertyName("spatialReference")]
    public EsriSpatialReference? SpatialReference { get; set; }
}

/// <summary>
/// Point geometry
/// </summary>
public class EsriPoint : EsriGeometry
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("z")]
    public double? Z { get; set; }

    [JsonPropertyName("m")]
    public double? M { get; set; }
}

/// <summary>
/// Multipoint geometry
/// </summary>
public class EsriMultipoint : EsriGeometry
{
    [JsonPropertyName("points")]
    public double[][] Points { get; set; } = Array.Empty<double[]>();

    [JsonPropertyName("hasZ")]
    public bool HasZ { get; set; }

    [JsonPropertyName("hasM")]
    public bool HasM { get; set; }
}

/// <summary>
/// Polyline geometry
/// </summary>
public class EsriPolyline : EsriGeometry
{
    [JsonPropertyName("paths")]
    public double[][][] Paths { get; set; } = Array.Empty<double[][]>();

    [JsonPropertyName("hasZ")]
    public bool HasZ { get; set; }

    [JsonPropertyName("hasM")]
    public bool HasM { get; set; }
}

/// <summary>
/// Polygon geometry
/// </summary>
public class EsriPolygon : EsriGeometry
{
    [JsonPropertyName("rings")]
    public double[][][] Rings { get; set; } = Array.Empty<double[][]>();

    [JsonPropertyName("hasZ")]
    public bool HasZ { get; set; }

    [JsonPropertyName("hasM")]
    public bool HasM { get; set; }
}

/// <summary>
/// Envelope (bounding box)
/// </summary>
public class EsriEnvelope : EsriGeometry
{
    [JsonPropertyName("xmin")]
    public double Xmin { get; set; }

    [JsonPropertyName("ymin")]
    public double Ymin { get; set; }

    [JsonPropertyName("xmax")]
    public double Xmax { get; set; }

    [JsonPropertyName("ymax")]
    public double Ymax { get; set; }

    [JsonPropertyName("zmin")]
    public double? Zmin { get; set; }

    [JsonPropertyName("zmax")]
    public double? Zmax { get; set; }

    [JsonPropertyName("mmin")]
    public double? Mmin { get; set; }

    [JsonPropertyName("mmax")]
    public double? Mmax { get; set; }
}
