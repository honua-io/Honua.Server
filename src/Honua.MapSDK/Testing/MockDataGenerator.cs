using System.Text;
using System.Text.Json;

namespace Honua.MapSDK.Testing;

/// <summary>
/// Generates mock data for testing MapSDK components.
/// </summary>
public static class MockDataGenerator
{
    private static readonly Random Random = new();

    /// <summary>
    /// Generates a GeoJSON FeatureCollection with random points.
    /// </summary>
    /// <param name="count">Number of features to generate.</param>
    /// <param name="bounds">Bounding box for coordinates (minLon, minLat, maxLon, maxLat).</param>
    /// <param name="properties">Property generators (name -> value generator).</param>
    /// <returns>GeoJSON string.</returns>
    public static string GenerateGeoJsonPoints(
        int count,
        (double MinLon, double MinLat, double MaxLon, double MaxLat)? bounds = null,
        Dictionary<string, Func<int, object>>? properties = null)
    {
        var bbox = bounds ?? (-180, -90, 180, 90);
        var props = properties ?? new Dictionary<string, Func<int, object>>
        {
            ["name"] = i => $"Feature {i}",
            ["value"] = _ => Random.Next(0, 100)
        };

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"type\": \"FeatureCollection\",");
        sb.AppendLine("  \"features\": [");

        for (var i = 0; i < count; i++)
        {
            var lon = Random.NextDouble() * (bbox.MaxLon - bbox.MinLon) + bbox.MinLon;
            var lat = Random.NextDouble() * (bbox.MaxLat - bbox.MinLat) + bbox.MinLat;

            sb.AppendLine("    {");
            sb.AppendLine("      \"type\": \"Feature\",");
            sb.AppendLine("      \"geometry\": {");
            sb.AppendLine("        \"type\": \"Point\",");
            sb.AppendLine($"        \"coordinates\": [{lon:F6}, {lat:F6}]");
            sb.AppendLine("      },");
            sb.AppendLine("      \"properties\": {");

            var propLines = props.Select(kvp =>
            {
                var value = kvp.Value(i);
                var jsonValue = value is string ? $"\"{value}\"" : JsonSerializer.Serialize(value);
                return $"        \"{kvp.Key}\": {jsonValue}";
            });

            sb.AppendLine(string.Join(",\n", propLines));
            sb.AppendLine("      }");
            sb.Append("    }");

            if (i < count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a GeoJSON FeatureCollection with random polygons.
    /// </summary>
    /// <param name="count">Number of polygons to generate.</param>
    /// <param name="verticesPerPolygon">Number of vertices per polygon.</param>
    /// <param name="bounds">Bounding box for coordinates.</param>
    /// <returns>GeoJSON string.</returns>
    public static string GenerateGeoJsonPolygons(
        int count,
        int verticesPerPolygon = 5,
        (double MinLon, double MinLat, double MaxLon, double MaxLat)? bounds = null)
    {
        var bbox = bounds ?? (-180, -90, 180, 90);

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"type\": \"FeatureCollection\",");
        sb.AppendLine("  \"features\": [");

        for (var i = 0; i < count; i++)
        {
            var centerLon = Random.NextDouble() * (bbox.MaxLon - bbox.MinLon) + bbox.MinLon;
            var centerLat = Random.NextDouble() * (bbox.MaxLat - bbox.MinLat) + bbox.MinLat;
            var radius = Random.NextDouble() * 2 + 0.5;

            sb.AppendLine("    {");
            sb.AppendLine("      \"type\": \"Feature\",");
            sb.AppendLine("      \"geometry\": {");
            sb.AppendLine("        \"type\": \"Polygon\",");
            sb.AppendLine("        \"coordinates\": [[");

            for (var j = 0; j <= verticesPerPolygon; j++)
            {
                var angle = (360.0 / verticesPerPolygon) * j;
                var lon = centerLon + radius * Math.Cos(angle * Math.PI / 180);
                var lat = centerLat + radius * Math.Sin(angle * Math.PI / 180);

                sb.Append($"          [{lon:F6}, {lat:F6}]");
                if (j < verticesPerPolygon)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }

            sb.AppendLine("        ]]");
            sb.AppendLine("      },");
            sb.AppendLine("      \"properties\": {");
            sb.AppendLine($"        \"name\": \"Polygon {i}\",");
            sb.AppendLine($"        \"area\": {Random.Next(100, 10000)}");
            sb.AppendLine("      }");
            sb.Append("    }");

            if (i < count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates CSV data with random values.
    /// </summary>
    /// <param name="rows">Number of rows to generate.</param>
    /// <param name="columns">Column definitions (name -> value generator).</param>
    /// <returns>CSV string.</returns>
    public static string GenerateCsv(int rows, Dictionary<string, Func<int, object>> columns)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", columns.Keys.Select(k => $"\"{k}\"")));

        // Rows
        for (var i = 0; i < rows; i++)
        {
            var values = columns.Values.Select(gen =>
            {
                var value = gen(i);
                return $"\"{value}\"";
            });

            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates time series data.
    /// </summary>
    /// <param name="points">Number of data points.</param>
    /// <param name="start">Start time.</param>
    /// <param name="interval">Interval between points.</param>
    /// <param name="valueGenerator">Value generator function.</param>
    /// <returns>Time series data as JSON array.</returns>
    public static string GenerateTimeSeries(
        int points,
        DateTime start,
        TimeSpan interval,
        Func<int, double> valueGenerator)
    {
        var data = new List<object>();

        for (var i = 0; i < points; i++)
        {
            var timestamp = start.Add(interval * i);
            var value = valueGenerator(i);

            data.Add(new
            {
                timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                value = value
            });
        }

        return JsonSerializer.Serialize(data);
    }

    /// <summary>
    /// Generates random coordinates within bounds.
    /// </summary>
    /// <param name="count">Number of coordinates to generate.</param>
    /// <param name="bounds">Bounding box for coordinates.</param>
    /// <returns>List of (longitude, latitude) tuples.</returns>
    public static List<(double Lon, double Lat)> GenerateCoordinates(
        int count,
        (double MinLon, double MinLat, double MaxLon, double MaxLat)? bounds = null)
    {
        var bbox = bounds ?? (-180, -90, 180, 90);
        var coordinates = new List<(double, double)>();

        for (var i = 0; i < count; i++)
        {
            var lon = Random.NextDouble() * (bbox.MaxLon - bbox.MinLon) + bbox.MinLon;
            var lat = Random.NextDouble() * (bbox.MaxLat - bbox.MinLat) + bbox.MinLat;
            coordinates.Add((lon, lat));
        }

        return coordinates;
    }

    /// <summary>
    /// Generates a sine wave time series.
    /// </summary>
    /// <param name="points">Number of points.</param>
    /// <param name="amplitude">Wave amplitude.</param>
    /// <param name="frequency">Wave frequency.</param>
    /// <param name="offset">Baseline offset.</param>
    /// <returns>Time series data.</returns>
    public static string GenerateSineWave(
        int points,
        double amplitude = 10,
        double frequency = 0.1,
        double offset = 50)
    {
        return GenerateTimeSeries(
            points,
            DateTime.UtcNow.AddHours(-points),
            TimeSpan.FromHours(1),
            i => amplitude * Math.Sin(2 * Math.PI * frequency * i) + offset
        );
    }

    /// <summary>
    /// Generates random walk time series data.
    /// </summary>
    /// <param name="points">Number of points.</param>
    /// <param name="start">Start value.</param>
    /// <param name="volatility">Volatility (step size).</param>
    /// <returns>Time series data.</returns>
    public static string GenerateRandomWalk(
        int points,
        double start = 100,
        double volatility = 2)
    {
        var value = start;
        return GenerateTimeSeries(
            points,
            DateTime.UtcNow.AddDays(-points),
            TimeSpan.FromDays(1),
            i =>
            {
                value += (Random.NextDouble() - 0.5) * volatility;
                return value;
            }
        );
    }

    /// <summary>
    /// Common property generators for testing.
    /// </summary>
    public static class PropertyGenerators
    {
        public static Func<int, object> SequentialId => i => i;
        public static Func<int, object> Name => i => $"Item {i}";
        public static Func<int, object> RandomInt => _ => Random.Next(0, 100);
        public static Func<int, object> RandomDouble => _ => Random.NextDouble() * 100;
        public static Func<int, object> RandomBool => _ => Random.Next(2) == 0;
        public static Func<int, object> RandomDate => _ => DateTime.UtcNow.AddDays(-Random.Next(0, 365));
        public static Func<int, object> Category => _ => new[] { "A", "B", "C", "D" }[Random.Next(4)];
    }
}
