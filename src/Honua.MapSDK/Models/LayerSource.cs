// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Base class for all layer data sources
/// </summary>
public abstract class LayerSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Type { get; init; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// XYZ tile source (most common tile format)
/// </summary>
public class TileSource : LayerSource
{
    public TileSource()
    {
        Type = "raster";
    }

    /// <summary>
    /// Tile URL template with {x}, {y}, {z} placeholders
    /// Example: "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Tile scheme: "xyz" (default) or "tms"
    /// </summary>
    public string Scheme { get; set; } = "xyz";

    /// <summary>
    /// Min zoom level (0-22)
    /// </summary>
    public int MinZoom { get; set; } = 0;

    /// <summary>
    /// Max zoom level (0-22)
    /// </summary>
    public int MaxZoom { get; set; } = 22;

    /// <summary>
    /// Tile size in pixels (256 or 512)
    /// </summary>
    public int TileSize { get; set; } = 256;

    /// <summary>
    /// Attribution text
    /// </summary>
    public string? Attribution { get; set; }

    /// <summary>
    /// Bounding box [west, south, east, north]
    /// </summary>
    public double[]? Bounds { get; set; }

    /// <summary>
    /// Subdomains for load balancing (e.g., ["a", "b", "c"])
    /// </summary>
    public string[]? Subdomains { get; set; }
}

/// <summary>
/// Vector tile source (Mapbox Vector Tiles / PBF)
/// </summary>
public class VectorTileSource : LayerSource
{
    public VectorTileSource()
    {
        Type = "vector";
    }

    /// <summary>
    /// Tile URL template or TileJSON URL
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Min zoom level
    /// </summary>
    public int MinZoom { get; set; } = 0;

    /// <summary>
    /// Max zoom level
    /// </summary>
    public int MaxZoom { get; set; } = 14;

    /// <summary>
    /// Attribution
    /// </summary>
    public string? Attribution { get; set; }

    /// <summary>
    /// Bounding box [west, south, east, north]
    /// </summary>
    public double[]? Bounds { get; set; }

    /// <summary>
    /// Tile scheme
    /// </summary>
    public string Scheme { get; set; } = "xyz";
}

/// <summary>
/// WMS (Web Map Service) source
/// </summary>
public class WmsSource : LayerSource
{
    public WmsSource()
    {
        Type = "raster";
    }

    /// <summary>
    /// WMS base URL
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// WMS layers (comma-separated)
    /// </summary>
    public required string Layers { get; set; }

    /// <summary>
    /// WMS version (e.g., "1.3.0", "1.1.1")
    /// </summary>
    public string Version { get; set; } = "1.3.0";

    /// <summary>
    /// Image format (e.g., "image/png")
    /// </summary>
    public string Format { get; set; } = "image/png";

    /// <summary>
    /// Coordinate reference system
    /// </summary>
    public string Crs { get; set; } = "EPSG:3857";

    /// <summary>
    /// Transparent background
    /// </summary>
    public bool Transparent { get; set; } = true;

    /// <summary>
    /// Tile size in pixels
    /// </summary>
    public int TileSize { get; set; } = 256;

    /// <summary>
    /// Additional WMS parameters
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>
    /// Attribution
    /// </summary>
    public string? Attribution { get; set; }
}

/// <summary>
/// WFS (Web Feature Service) source
/// </summary>
public class WfsSource : LayerSource
{
    public WfsSource()
    {
        Type = "geojson";
    }

    /// <summary>
    /// WFS base URL
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// WFS type/layer name
    /// </summary>
    public required string TypeName { get; set; }

    /// <summary>
    /// WFS version (e.g., "2.0.0", "1.1.0")
    /// </summary>
    public string Version { get; set; } = "2.0.0";

    /// <summary>
    /// Output format (e.g., "application/json")
    /// </summary>
    public string OutputFormat { get; set; } = "application/json";

    /// <summary>
    /// Coordinate reference system
    /// </summary>
    public string Crs { get; set; } = "EPSG:4326";

    /// <summary>
    /// CQL filter expression
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Maximum features to retrieve
    /// </summary>
    public int? MaxFeatures { get; set; }

    /// <summary>
    /// Property names to retrieve
    /// </summary>
    public string[]? PropertyNames { get; set; }

    /// <summary>
    /// Attribution
    /// </summary>
    public string? Attribution { get; set; }
}

/// <summary>
/// GeoJSON source (from URL or inline data)
/// </summary>
public class GeoJsonSource : LayerSource
{
    public GeoJsonSource()
    {
        Type = "geojson";
    }

    /// <summary>
    /// URL to GeoJSON file (mutually exclusive with Data)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Inline GeoJSON data (mutually exclusive with Url)
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Enable clustering
    /// </summary>
    public bool Cluster { get; set; } = false;

    /// <summary>
    /// Cluster radius in pixels
    /// </summary>
    public int ClusterRadius { get; set; } = 50;

    /// <summary>
    /// Max zoom to cluster
    /// </summary>
    public int ClusterMaxZoom { get; set; } = 14;

    /// <summary>
    /// Buffer around tiles (0-512)
    /// </summary>
    public int Buffer { get; set; } = 128;

    /// <summary>
    /// Enable tolerance for simplification
    /// </summary>
    public double Tolerance { get; set; } = 0.375;

    /// <summary>
    /// Line metrics for gradient lines
    /// </summary>
    public bool LineMetrics { get; set; } = false;

    /// <summary>
    /// Generate unique feature IDs
    /// </summary>
    public bool GenerateId { get; set; } = false;

    /// <summary>
    /// Attribution
    /// </summary>
    public string? Attribution { get; set; }
}

/// <summary>
/// Image source (georeferenced image overlay)
/// </summary>
public class ImageSource : LayerSource
{
    public ImageSource()
    {
        Type = "image";
    }

    /// <summary>
    /// Image URL
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Image corners [top-left, top-right, bottom-right, bottom-left]
    /// Each corner is [longitude, latitude]
    /// </summary>
    public required double[][] Coordinates { get; set; }

    /// <summary>
    /// Attribution
    /// </summary>
    public string? Attribution { get; set; }
}

/// <summary>
/// Video source (georeferenced video overlay)
/// </summary>
public class VideoSource : LayerSource
{
    public VideoSource()
    {
        Type = "video";
    }

    /// <summary>
    /// Video URLs (multiple formats for browser compatibility)
    /// </summary>
    public required string[] Urls { get; set; }

    /// <summary>
    /// Video corners [top-left, top-right, bottom-right, bottom-left]
    /// Each corner is [longitude, latitude]
    /// </summary>
    public required double[][] Coordinates { get; set; }
}

/// <summary>
/// Canvas source (HTML canvas element)
/// </summary>
public class CanvasSource : LayerSource
{
    public CanvasSource()
    {
        Type = "canvas";
    }

    /// <summary>
    /// Canvas element ID or canvas reference
    /// </summary>
    public required string CanvasId { get; set; }

    /// <summary>
    /// Canvas corners [top-left, top-right, bottom-right, bottom-left]
    /// </summary>
    public required double[][] Coordinates { get; set; }

    /// <summary>
    /// Enable animation (calls animate callback)
    /// </summary>
    public bool Animate { get; set; } = false;
}

/// <summary>
/// WMTS (Web Map Tile Service) source
/// </summary>
public class WmtsSource : LayerSource
{
    public WmtsSource()
    {
        Type = "raster";
    }

    /// <summary>
    /// WMTS base URL
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Layer identifier
    /// </summary>
    public required string Layer { get; set; }

    /// <summary>
    /// Tile matrix set
    /// </summary>
    public required string TileMatrixSet { get; set; }

    /// <summary>
    /// Image format
    /// </summary>
    public string Format { get; set; } = "image/png";

    /// <summary>
    /// Tile matrix prefix
    /// </summary>
    public string? TileMatrixPrefix { get; set; }

    /// <summary>
    /// Min zoom level
    /// </summary>
    public int MinZoom { get; set; } = 0;

    /// <summary>
    /// Max zoom level
    /// </summary>
    public int MaxZoom { get; set; } = 22;

    /// <summary>
    /// Attribution
    /// </summary>
    public string? Attribution { get; set; }
}

/// <summary>
/// ArcGIS REST source
/// </summary>
public class ArcGISSource : LayerSource
{
    public ArcGISSource()
    {
        Type = "raster";
    }

    /// <summary>
    /// ArcGIS MapServer or ImageServer URL
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Token for authentication
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Tile size
    /// </summary>
    public int TileSize { get; set; } = 256;

    /// <summary>
    /// Attribution
    /// </summary>
    public string? Attribution { get; set; }
}
