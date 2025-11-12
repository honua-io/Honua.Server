// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using H3;
using H3.Model;
using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Geoprocessing.Operations;

/// <summary>
/// Service for H3 hexagonal grid operations
/// Provides conversion between geographic coordinates and H3 hexagon indices
/// </summary>
public class H3Service
{
    private readonly H3Api _h3Api;

    public H3Service()
    {
        _h3Api = new H3Api();
    }

    /// <summary>
    /// Convert a point to H3 hexagon index
    /// </summary>
    /// <param name="lat">Latitude in degrees</param>
    /// <param name="lon">Longitude in degrees</param>
    /// <param name="resolution">H3 resolution (0-15)</param>
    /// <returns>H3 index as string</returns>
    public string PointToH3(double lat, double lon, int resolution)
    {
        if (resolution < 0 || resolution > 15)
        {
            throw new ArgumentException("H3 resolution must be between 0 and 15", nameof(resolution));
        }

        var h3Index = _h3Api.LatLngToCell(new LatLng(lat, lon), resolution);
        return h3Index.ToString();
    }

    /// <summary>
    /// Get the boundary polygon for an H3 hexagon
    /// </summary>
    /// <param name="h3Index">H3 index as string</param>
    /// <returns>Polygon representing the hexagon boundary</returns>
    public Polygon GetH3Boundary(string h3Index)
    {
        var cellIndex = new H3Index(h3Index);
        var boundary = _h3Api.CellToBoundary(cellIndex);

        var coordinates = new List<Coordinate>();
        foreach (var latLng in boundary)
        {
            coordinates.Add(new Coordinate(latLng.Longitude, latLng.Latitude));
        }

        // Close the ring
        coordinates.Add(coordinates[0]);

        var factory = new GeometryFactory();
        return factory.CreatePolygon(coordinates.ToArray());
    }

    /// <summary>
    /// Get all H3 hexagons that cover a polygon
    /// </summary>
    /// <param name="polygon">Input polygon</param>
    /// <param name="resolution">H3 resolution</param>
    /// <returns>List of H3 indices covering the polygon</returns>
    public List<string> PolygonToH3(Polygon polygon, int resolution)
    {
        if (resolution < 0 || resolution > 15)
        {
            throw new ArgumentException("H3 resolution must be between 0 and 15", nameof(resolution));
        }

        // Convert polygon coordinates to LatLng
        var geoPolygon = new List<LatLng>();
        foreach (var coord in polygon.Coordinates)
        {
            geoPolygon.Add(new LatLng(coord.Y, coord.X));
        }

        // Get all hexagons that cover the polygon
        var hexagons = _h3Api.PolygonToCells(new GeoPolygon(geoPolygon), resolution);

        return hexagons.Select(h => h.ToString()).ToList();
    }

    /// <summary>
    /// Get the center point of an H3 hexagon
    /// </summary>
    /// <param name="h3Index">H3 index as string</param>
    /// <returns>Center point as coordinate</returns>
    public Coordinate GetH3Center(string h3Index)
    {
        var cellIndex = new H3Index(h3Index);
        var center = _h3Api.CellToLatLng(cellIndex);
        return new Coordinate(center.Longitude, center.Latitude);
    }

    /// <summary>
    /// Get the area of an H3 hexagon in square meters
    /// </summary>
    /// <param name="h3Index">H3 index as string</param>
    /// <returns>Area in square meters</returns>
    public double GetH3Area(string h3Index)
    {
        var cellIndex = new H3Index(h3Index);
        return _h3Api.CellArea(cellIndex, AreaUnit.M2);
    }

    /// <summary>
    /// Get the resolution of an H3 hexagon
    /// </summary>
    /// <param name="h3Index">H3 index as string</param>
    /// <returns>Resolution (0-15)</returns>
    public int GetH3Resolution(string h3Index)
    {
        var cellIndex = new H3Index(h3Index);
        return _h3Api.GetResolution(cellIndex);
    }

    /// <summary>
    /// Get neighboring hexagons for a given H3 index
    /// </summary>
    /// <param name="h3Index">H3 index as string</param>
    /// <returns>List of neighboring H3 indices</returns>
    public List<string> GetH3Neighbors(string h3Index)
    {
        var cellIndex = new H3Index(h3Index);
        var neighbors = _h3Api.GridDisk(cellIndex, 1);
        return neighbors
            .Where(h => h.ToString() != h3Index) // Exclude the center hex
            .Select(h => h.ToString())
            .ToList();
    }

    /// <summary>
    /// Get hexagons within k distance from the origin hexagon
    /// </summary>
    /// <param name="h3Index">Origin H3 index</param>
    /// <param name="k">Ring distance</param>
    /// <returns>List of H3 indices within k distance</returns>
    public List<string> GetH3Ring(string h3Index, int k)
    {
        var cellIndex = new H3Index(h3Index);
        var ring = _h3Api.GridDisk(cellIndex, k);
        return ring.Select(h => h.ToString()).ToList();
    }

    /// <summary>
    /// Get the average hexagon edge length for a resolution
    /// </summary>
    /// <param name="resolution">H3 resolution (0-15)</param>
    /// <returns>Average edge length in meters</returns>
    public double GetAverageEdgeLength(int resolution)
    {
        if (resolution < 0 || resolution > 15)
        {
            throw new ArgumentException("H3 resolution must be between 0 and 15", nameof(resolution));
        }

        return _h3Api.GetHexagonEdgeLengthAvg(resolution, LengthUnit.M);
    }

    /// <summary>
    /// Get the average hexagon area for a resolution
    /// </summary>
    /// <param name="resolution">H3 resolution (0-15)</param>
    /// <returns>Average area in square meters</returns>
    public double GetAverageArea(int resolution)
    {
        if (resolution < 0 || resolution > 15)
        {
            throw new ArgumentException("H3 resolution must be between 0 and 15", nameof(resolution));
        }

        return _h3Api.GetHexagonAreaAvg(resolution, AreaUnit.M2);
    }

    /// <summary>
    /// Validates if a string is a valid H3 index
    /// </summary>
    /// <param name="h3Index">H3 index string to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValidH3Index(string h3Index)
    {
        try
        {
            var cellIndex = new H3Index(h3Index);
            return _h3Api.IsValidCell(cellIndex);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Aggregation function types for H3 binning
/// </summary>
public enum H3AggregationType
{
    Count,
    Sum,
    Average,
    Min,
    Max,
    StdDev,
    Median
}

/// <summary>
/// Result of H3 binning operation
/// </summary>
public class H3BinResult
{
    /// <summary>
    /// H3 hexagon index
    /// </summary>
    public string H3Index { get; set; } = string.Empty;

    /// <summary>
    /// Number of points in this hexagon
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Aggregated value for this hexagon
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Hexagon boundary as polygon
    /// </summary>
    public Polygon? Boundary { get; set; }

    /// <summary>
    /// Center point of hexagon
    /// </summary>
    public Coordinate? Center { get; set; }

    /// <summary>
    /// Additional statistics
    /// </summary>
    public Dictionary<string, double> Statistics { get; set; } = new();
}
