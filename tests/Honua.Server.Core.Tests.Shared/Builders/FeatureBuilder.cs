using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Tests.Shared.Builders;

/// <summary>
/// Builder for creating GeoJSON Feature test data with fluent API.
/// Reduces repetitive feature creation across tests.
/// </summary>
/// <remarks>
/// <para>
/// Usage example:
/// </para>
/// <code>
/// var feature = new FeatureBuilder()
///     .WithId(1)
///     .WithPointGeometry(-122.5, 37.8)
///     .WithProperty("name", "San Francisco")
///     .WithProperty("population", 873965)
///     .Build();
/// </code>
/// </remarks>
public class FeatureBuilder
{
    private object _id;
    private Geometry _geometry;
    private readonly Dictionary<string, object> _properties = new();
    private static readonly WKTReader WktReader = new();

    /// <summary>
    /// Sets the feature ID.
    /// </summary>
    public FeatureBuilder WithId(object id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets a Point geometry at the specified coordinates.
    /// </summary>
    public FeatureBuilder WithPointGeometry(double lon, double lat)
    {
        _geometry = new Point(new Coordinate(lon, lat));
        return this;
    }

    /// <summary>
    /// Sets a LineString geometry from the specified coordinates.
    /// </summary>
    public FeatureBuilder WithLineStringGeometry(params (double lon, double lat)[] coords)
    {
        var coordinates = new Coordinate[coords.Length];
        for (int i = 0; i < coords.Length; i++)
        {
            coordinates[i] = new Coordinate(coords[i].lon, coords[i].lat);
        }
        _geometry = new LineString(coordinates);
        return this;
    }

    /// <summary>
    /// Sets a Polygon geometry from the specified bounding box.
    /// </summary>
    public FeatureBuilder WithPolygonGeometry(double west, double south, double east, double north)
    {
        var coordinates = new[]
        {
            new Coordinate(west, south),
            new Coordinate(east, south),
            new Coordinate(east, north),
            new Coordinate(west, north),
            new Coordinate(west, south) // Close the ring
        };

        _geometry = new Polygon(new LinearRing(coordinates));
        return this;
    }

    /// <summary>
    /// Sets geometry from WKT string.
    /// </summary>
    public FeatureBuilder WithWktGeometry(string wkt)
    {
        _geometry = WktReader.Read(wkt);
        return this;
    }

    /// <summary>
    /// Adds a property to the feature.
    /// </summary>
    public FeatureBuilder WithProperty(string key, object value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple properties from a dictionary.
    /// </summary>
    public FeatureBuilder WithProperties(Dictionary<string, object> properties)
    {
        foreach (var kvp in properties)
        {
            _properties[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Builds a GeoJSON Feature dictionary.
    /// </summary>
    public Dictionary<string, object> Build()
    {
        if (_geometry == null)
        {
            throw new InvalidOperationException("Geometry must be set before building feature");
        }

        var feature = new Dictionary<string, object>
        {
            ["type"] = "Feature",
            ["geometry"] = GeometryToGeoJson(_geometry),
            ["properties"] = new Dictionary<string, object>(_properties)
        };

        if (_id != null)
        {
            feature["id"] = _id;
        }

        return feature;
    }

    /// <summary>
    /// Builds a GeoJSON FeatureCollection containing this feature.
    /// </summary>
    public Dictionary<string, object> BuildAsCollection()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "FeatureCollection",
            ["features"] = new[] { Build() }
        };
    }

    private static Dictionary<string, object> GeometryToGeoJson(Geometry geometry)
    {
        if (geometry is Point point)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "Point",
                ["coordinates"] = new[] { point.X, point.Y }
            };
        }

        if (geometry is LineString lineString)
        {
            var coords = new List<double[]>();
            foreach (var coord in lineString.Coordinates)
            {
                coords.Add(new[] { coord.X, coord.Y });
            }

            return new Dictionary<string, object>
            {
                ["type"] = "LineString",
                ["coordinates"] = coords.ToArray()
            };
        }

        if (geometry is Polygon polygon)
        {
            var rings = new List<List<double[]>>();
            var exteriorRing = new List<double[]>();

            foreach (var coord in polygon.ExteriorRing.Coordinates)
            {
                exteriorRing.Add(new[] { coord.X, coord.Y });
            }

            rings.Add(exteriorRing);

            return new Dictionary<string, object>
            {
                ["type"] = "Polygon",
                ["coordinates"] = rings.ToArray()
            };
        }

        throw new NotSupportedException($"Geometry type {geometry.GeometryType} is not supported");
    }
}
