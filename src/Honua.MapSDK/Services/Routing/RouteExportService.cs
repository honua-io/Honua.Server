// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Honua.MapSDK.Models.Routing;
using Honua.Server.Core.LocationServices.Models;

namespace Honua.MapSDK.Services.Routing;

/// <summary>
/// Service for exporting routes to various formats (GPX, KML, GeoJSON).
/// </summary>
public sealed class RouteExportService
{
    /// <summary>
    /// Exports a route to GPX format.
    /// </summary>
    /// <param name="route">Route to export.</param>
    /// <param name="coordinates">Route coordinates [longitude, latitude].</param>
    /// <param name="routeName">Name for the route.</param>
    /// <param name="description">Route description.</param>
    /// <returns>GPX XML string.</returns>
    public string ExportToGpx(
        Route route,
        List<double[]> coordinates,
        string routeName = "Route",
        string? description = null)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("gpx", "http://www.topografix.com/GPX/1/1");
        writer.WriteAttributeString("version", "1.1");
        writer.WriteAttributeString("creator", "Honua MapSDK");

        // Metadata
        writer.WriteStartElement("metadata");
        writer.WriteElementString("name", routeName);
        if (description != null)
        {
            writer.WriteElementString("desc", description);
        }
        writer.WriteElementString("time", DateTime.UtcNow.ToString("o"));
        writer.WriteEndElement(); // metadata

        // Track
        writer.WriteStartElement("trk");
        writer.WriteElementString("name", routeName);
        writer.WriteElementString("type", "route");

        // Track segment
        writer.WriteStartElement("trkseg");
        foreach (var coord in coordinates)
        {
            writer.WriteStartElement("trkpt");
            writer.WriteAttributeString("lat", coord[1].ToString("F6"));
            writer.WriteAttributeString("lon", coord[0].ToString("F6"));
            writer.WriteEndElement(); // trkpt
        }
        writer.WriteEndElement(); // trkseg
        writer.WriteEndElement(); // trk

        // Waypoints (if available)
        if (route.Instructions != null)
        {
            foreach (var instruction in route.Instructions.Where(i => i.Location != null))
            {
                writer.WriteStartElement("wpt");
                writer.WriteAttributeString("lat", instruction.Location![1].ToString("F6"));
                writer.WriteAttributeString("lon", instruction.Location[0].ToString("F6"));
                writer.WriteElementString("name", instruction.ManeuverType ?? "turn");
                writer.WriteElementString("desc", instruction.Text);
                writer.WriteEndElement(); // wpt
            }
        }

        writer.WriteEndElement(); // gpx
        writer.WriteEndDocument();

        return sb.ToString();
    }

    /// <summary>
    /// Exports a route to KML format.
    /// </summary>
    /// <param name="route">Route to export.</param>
    /// <param name="coordinates">Route coordinates [longitude, latitude].</param>
    /// <param name="routeName">Name for the route.</param>
    /// <param name="description">Route description.</param>
    /// <param name="style">Route style for visualization.</param>
    /// <returns>KML XML string.</returns>
    public string ExportToKml(
        Route route,
        List<double[]> coordinates,
        string routeName = "Route",
        string? description = null,
        RouteStyle? style = null)
    {
        style ??= new RouteStyle();

        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("kml", "http://www.opengis.net/kml/2.2");

        writer.WriteStartElement("Document");
        writer.WriteElementString("name", routeName);
        if (description != null)
        {
            writer.WriteElementString("description", description);
        }

        // Style definition
        writer.WriteStartElement("Style");
        writer.WriteAttributeString("id", "routeStyle");
        writer.WriteStartElement("LineStyle");
        writer.WriteElementString("color", ConvertColorToKml(style.Color, style.Opacity));
        writer.WriteElementString("width", style.Width.ToString("F1"));
        writer.WriteEndElement(); // LineStyle
        writer.WriteEndElement(); // Style

        // Route line
        writer.WriteStartElement("Placemark");
        writer.WriteElementString("name", routeName);
        writer.WriteElementString("description",
            $"Distance: {route.DistanceMeters / 1000:F2} km, " +
            $"Duration: {TimeSpan.FromSeconds(route.DurationSeconds).TotalMinutes:F0} min");
        writer.WriteElementString("styleUrl", "#routeStyle");

        writer.WriteStartElement("LineString");
        writer.WriteElementString("tessellate", "1");
        writer.WriteStartElement("coordinates");

        var coordsText = string.Join(" ",
            coordinates.Select(c => $"{c[0]:F6},{c[1]:F6},0"));
        writer.WriteString(coordsText);

        writer.WriteEndElement(); // coordinates
        writer.WriteEndElement(); // LineString
        writer.WriteEndElement(); // Placemark

        // Waypoint placemarks
        if (route.Instructions != null)
        {
            foreach (var instruction in route.Instructions.Where(i => i.Location != null))
            {
                writer.WriteStartElement("Placemark");
                writer.WriteElementString("name", instruction.ManeuverType ?? "instruction");
                writer.WriteElementString("description", instruction.Text);

                writer.WriteStartElement("Point");
                writer.WriteElementString("coordinates",
                    $"{instruction.Location![0]:F6},{instruction.Location[1]:F6},0");
                writer.WriteEndElement(); // Point

                writer.WriteEndElement(); // Placemark
            }
        }

        writer.WriteEndElement(); // Document
        writer.WriteEndElement(); // kml
        writer.WriteEndDocument();

        return sb.ToString();
    }

    /// <summary>
    /// Exports a route to GeoJSON format.
    /// </summary>
    /// <param name="route">Route to export.</param>
    /// <param name="coordinates">Route coordinates [longitude, latitude].</param>
    /// <param name="routeName">Name for the route.</param>
    /// <param name="includeInstructions">Whether to include turn-by-turn instructions.</param>
    /// <returns>GeoJSON string.</returns>
    public string ExportToGeoJson(
        Route route,
        List<double[]> coordinates,
        string routeName = "Route",
        bool includeInstructions = true)
    {
        var features = new List<string>();

        // Route line feature
        var lineCoords = string.Join(",",
            coordinates.Select(c => $"[{c[0]:F6},{c[1]:F6}]"));

        var lineFeature = $@"{{
  ""type"": ""Feature"",
  ""properties"": {{
    ""name"": ""{routeName}"",
    ""distance"": {route.DistanceMeters},
    ""duration"": {route.DurationSeconds},
    ""distanceKm"": {route.DistanceMeters / 1000:F2},
    ""durationMin"": {route.DurationSeconds / 60:F1}
  }},
  ""geometry"": {{
    ""type"": ""LineString"",
    ""coordinates"": [{lineCoords}]
  }}
}}";
        features.Add(lineFeature);

        // Instruction point features
        if (includeInstructions && route.Instructions != null)
        {
            foreach (var (instruction, index) in route.Instructions
                .Where(i => i.Location != null)
                .Select((i, idx) => (i, idx)))
            {
                var pointFeature = $@"{{
  ""type"": ""Feature"",
  ""properties"": {{
    ""type"": ""instruction"",
    ""index"": {index},
    ""maneuver"": ""{instruction.ManeuverType ?? "unknown"}"",
    ""text"": ""{instruction.Text.Replace("\"", "\\\"")}"",
    ""distance"": {instruction.DistanceMeters},
    ""duration"": {instruction.DurationSeconds}
  }},
  ""geometry"": {{
    ""type"": ""Point"",
    ""coordinates"": [{instruction.Location![0]:F6},{instruction.Location[1]:F6}]
  }}
}}";
                features.Add(pointFeature);
            }
        }

        var allFeatures = string.Join(",\n", features);

        return $@"{{
  ""type"": ""FeatureCollection"",
  ""features"": [
{allFeatures}
  ]
}}";
    }

    /// <summary>
    /// Generates a shareable route link.
    /// </summary>
    /// <param name="waypoints">Route waypoints.</param>
    /// <param name="travelMode">Travel mode.</param>
    /// <param name="baseUrl">Base URL for the application.</param>
    /// <returns>Shareable URL.</returns>
    public string GenerateShareLink(
        IReadOnlyList<double[]> waypoints,
        string travelMode = "car",
        string baseUrl = "/map")
    {
        var waypointsParam = string.Join("|",
            waypoints.Select(w => $"{w[1]:F6},{w[0]:F6}"));

        return $"{baseUrl}?waypoints={Uri.EscapeDataString(waypointsParam)}&mode={travelMode}";
    }

    /// <summary>
    /// Generates a printable HTML document for the route.
    /// </summary>
    /// <param name="route">Route to format.</param>
    /// <param name="routeName">Name for the route.</param>
    /// <returns>HTML string.</returns>
    public string GeneratePrintableHtml(Route route, string routeName = "Route Directions")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine($"  <title>{routeName}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(@"
    body { font-family: Arial, sans-serif; margin: 20px; }
    h1 { color: #333; }
    .summary { background: #f0f0f0; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
    .instruction { padding: 10px; border-bottom: 1px solid #ddd; }
    .instruction:hover { background: #f9f9f9; }
    .maneuver { font-weight: bold; color: #1967D2; }
    .distance { color: #666; float: right; }
    .warnings { background: #fff3cd; padding: 10px; border-left: 3px solid #ffc107; margin-top: 20px; }
    @media print { .no-print { display: none; } }
  ");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine($"  <h1>{routeName}</h1>");

        // Summary
        sb.AppendLine("  <div class='summary'>");
        sb.AppendLine($"    <p><strong>Distance:</strong> {route.DistanceMeters / 1000:F2} km</p>");
        sb.AppendLine($"    <p><strong>Duration:</strong> {TimeSpan.FromSeconds(route.DurationSeconds).ToString(@"h\h\ m\m")}</p>");
        if (route.DurationWithTrafficSeconds.HasValue)
        {
            sb.AppendLine($"    <p><strong>With Traffic:</strong> {TimeSpan.FromSeconds(route.DurationWithTrafficSeconds.Value).ToString(@"h\h\ m\m")}</p>");
        }
        sb.AppendLine("  </div>");

        // Instructions
        if (route.Instructions != null && route.Instructions.Count > 0)
        {
            sb.AppendLine("  <h2>Directions</h2>");
            foreach (var (instruction, index) in route.Instructions.Select((i, idx) => (i, idx + 1)))
            {
                sb.AppendLine("  <div class='instruction'>");
                sb.AppendLine($"    <span class='distance'>{instruction.DistanceMeters / 1000:F1} km</span>");
                sb.AppendLine($"    <span class='maneuver'>{index}. {instruction.ManeuverType ?? "Continue"}</span>");
                sb.AppendLine($"    <p>{instruction.Text}</p>");
                if (!string.IsNullOrEmpty(instruction.RoadName))
                {
                    sb.AppendLine($"    <p><em>{instruction.RoadName}</em></p>");
                }
                sb.AppendLine("  </div>");
            }
        }

        // Warnings
        if (route.Warnings != null && route.Warnings.Count > 0)
        {
            sb.AppendLine("  <div class='warnings'>");
            sb.AppendLine("    <h3>Route Warnings</h3>");
            sb.AppendLine("    <ul>");
            foreach (var warning in route.Warnings)
            {
                sb.AppendLine($"      <li>{warning}</li>");
            }
            sb.AppendLine("    </ul>");
            sb.AppendLine("  </div>");
        }

        sb.AppendLine("  <button class='no-print' onclick='window.print()'>Print Directions</button>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Converts hex color to KML format (AABBGGRR).
    /// </summary>
    private static string ConvertColorToKml(string hexColor, double opacity)
    {
        // Remove # if present
        var color = hexColor.TrimStart('#');

        // Convert hex to RGB
        var r = Convert.ToInt32(color.Substring(0, 2), 16);
        var g = Convert.ToInt32(color.Substring(2, 2), 16);
        var b = Convert.ToInt32(color.Substring(4, 2), 16);

        // Convert opacity to alpha (0-255)
        var a = (int)(opacity * 255);

        // Return in KML format: AABBGGRR
        return $"{a:X2}{b:X2}{g:X2}{r:X2}";
    }

    /// <summary>
    /// Exports route statistics to CSV format.
    /// </summary>
    /// <param name="comparisons">List of route comparisons.</param>
    /// <returns>CSV string.</returns>
    public string ExportComparisonToCsv(IReadOnlyList<RouteComparisonMetrics> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Provider,Distance (km),Duration (min),Duration with Traffic (min),Toll Roads,Ferries");

        foreach (var comp in comparisons)
        {
            sb.AppendLine($"{comp.Provider}," +
                         $"{comp.DistanceMeters / 1000:F2}," +
                         $"{comp.DurationSeconds / 60:F1}," +
                         $"{(comp.DurationWithTrafficSeconds.HasValue ? (comp.DurationWithTrafficSeconds.Value / 60).ToString("F1") : "N/A")}," +
                         $"{comp.TollRoadCount}," +
                         $"{comp.FerryCount}");
        }

        return sb.ToString();
    }
}
