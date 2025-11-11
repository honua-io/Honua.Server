// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;

namespace Honua.Server.Enterprise.Geoprocessing.Operations;

/// <summary>
/// Shared helper class for loading geometries from various sources in geoprocessing operations
/// </summary>
public static class GeometryLoader
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Loads geometries from a geoprocessing input source
    /// </summary>
    /// <param name="input">The input specification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of loaded geometries</returns>
    /// <exception cref="ArgumentException">Thrown when input is invalid</exception>
    /// <exception cref="NotImplementedException">Thrown when input type is not supported</exception>
    public static async Task<List<Geometry>> LoadGeometriesAsync(
        GeoprocessingInput input,
        CancellationToken cancellationToken = default)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.Type))
        {
            throw new ArgumentException("Input type cannot be null or empty", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.Source))
        {
            throw new ArgumentException("Input source cannot be null or empty", nameof(input));
        }

        return input.Type.ToLowerInvariant() switch
        {
            "wkt" => LoadFromWkt(input.Source),
            "geojson" => LoadFromGeoJson(input.Source),
            "collection" => await LoadFromCollectionAsync(input, cancellationToken),
            "url" => await LoadFromUrlAsync(input.Source, cancellationToken),
            _ => throw new NotImplementedException($"Input type '{input.Type}' is not supported. Supported types: wkt, geojson, collection, url")
        };
    }

    /// <summary>
    /// Loads geometry from WKT (Well-Known Text)
    /// </summary>
    private static List<Geometry> LoadFromWkt(string wkt)
    {
        try
        {
            var reader = new WKTReader();
            var geometry = reader.Read(wkt);
            return new List<Geometry> { geometry };
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid WKT format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads geometries from GeoJSON
    /// </summary>
    private static List<Geometry> LoadFromGeoJson(string geoJson)
    {
        try
        {
            var reader = new GeoJsonReader();
            var geometry = reader.Read<Geometry>(geoJson);

            // If it's a GeometryCollection, extract individual geometries
            if (geometry is GeometryCollection collection)
            {
                var geometries = new List<Geometry>();
                for (int i = 0; i < collection.NumGeometries; i++)
                {
                    geometries.Add(collection.GetGeometryN(i));
                }
                return geometries;
            }

            // Try to parse as FeatureCollection to extract geometries from all features
            try
            {
                using var jsonDoc = JsonDocument.Parse(geoJson);
                if (jsonDoc.RootElement.TryGetProperty("type", out var typeElement) &&
                    typeElement.GetString() == "FeatureCollection" &&
                    jsonDoc.RootElement.TryGetProperty("features", out var featuresElement))
                {
                    var geometries = new List<Geometry>();
                    foreach (var feature in featuresElement.EnumerateArray())
                    {
                        if (feature.TryGetProperty("geometry", out var geometryElement))
                        {
                            var geomJson = geometryElement.GetRawText();
                            var geom = reader.Read<Geometry>(geomJson);
                            if (geom != null && !geom.IsEmpty)
                            {
                                geometries.Add(geom);
                            }
                        }
                    }

                    if (geometries.Count > 0)
                    {
                        return geometries;
                    }
                }
            }
            catch
            {
                // If FeatureCollection parsing fails, fall back to single geometry
            }

            return new List<Geometry> { geometry };
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid GeoJSON format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads geometries from a database collection/table
    /// </summary>
    private static async Task<List<Geometry>> LoadFromCollectionAsync(
        GeoprocessingInput input,
        CancellationToken cancellationToken)
    {
        // Get connection string from input parameters or environment variable
        var connectionString = input.Parameters?.GetValueOrDefault("connectionString")?.ToString()
            ?? Environment.GetEnvironmentVariable("GEOPROCESSING_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Connection string required for collection input. " +
                "Provide via input.Parameters['connectionString'] or GEOPROCESSING_CONNECTION_STRING environment variable");
        }

        // Get geometry column name (default: "geometry" or "geom")
        var geometryColumn = input.Parameters?.GetValueOrDefault("geometryColumn")?.ToString() ?? "geometry";

        // Get optional filter (WHERE clause)
        var filter = input.Filter;

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Validate table/collection name to prevent SQL injection
            var tableName = ValidateTableName(input.Source);

            // Build query
            var sql = $"SELECT ST_AsText({geometryColumn}) AS wkt FROM {tableName}";

            if (!string.IsNullOrWhiteSpace(filter))
            {
                // Basic CQL filter support - in production, use a proper CQL parser
                sql += $" WHERE {SanitizeFilter(filter)}";
            }

            // Limit to reasonable number of features
            var maxFeatures = input.Parameters?.ContainsKey("maxFeatures") == true
                ? Convert.ToInt32(input.Parameters["maxFeatures"])
                : 10000;

            sql += $" LIMIT {maxFeatures}";

            var results = await connection.QueryAsync<string>(sql, cancellationToken);

            var geometries = new List<Geometry>();
            var reader = new WKTReader();

            foreach (var wkt in results)
            {
                if (!string.IsNullOrWhiteSpace(wkt))
                {
                    var geometry = reader.Read(wkt);
                    if (geometry != null && !geometry.IsEmpty)
                    {
                        geometries.Add(geometry);
                    }
                }
            }

            if (geometries.Count == 0)
            {
                throw new InvalidOperationException($"No geometries found in collection '{input.Source}'");
            }

            return geometries;
        }
        catch (PostgresException ex)
        {
            throw new InvalidOperationException($"Database error loading from collection '{input.Source}': {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Error loading from collection '{input.Source}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads geometries from a remote URL
    /// </summary>
    private static async Task<List<Geometry>> LoadFromUrlAsync(
        string url,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid URL format: {url}");
        }

        // Only allow http and https schemes
        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            throw new ArgumentException($"Only HTTP and HTTPS URLs are supported. Provided: {uri.Scheme}");
        }

        try
        {
            var response = await HttpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException($"Empty response from URL: {url}");
            }

            // Determine format based on content type or try to parse
            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();

            // Try GeoJSON first (most common for web services)
            if (contentType?.Contains("json") == true || content.TrimStart().StartsWith("{") || content.TrimStart().StartsWith("["))
            {
                try
                {
                    return LoadFromGeoJson(content);
                }
                catch
                {
                    // If GeoJSON fails, try WKT
                }
            }

            // Try WKT
            try
            {
                return LoadFromWkt(content);
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Unable to parse response from URL '{url}' as GeoJSON or WKT. " +
                    $"Content type: {contentType ?? "unknown"}");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP error fetching from URL '{url}': {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException($"Request to URL '{url}' timed out after 30 seconds");
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not ArgumentException)
        {
            throw new InvalidOperationException($"Error loading from URL '{url}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates and sanitizes table name to prevent SQL injection
    /// </summary>
    private static string ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name cannot be null or empty");
        }

        // Remove any dangerous characters
        // Allow: letters, numbers, underscores, dots (for schema.table), and hyphens
        var sanitized = new string(tableName.Where(c =>
            char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-').ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException($"Invalid table name: {tableName}");
        }

        // Optionally quote the table name for PostgreSQL
        // Handle schema.table format
        if (sanitized.Contains('.'))
        {
            var parts = sanitized.Split('.');
            if (parts.Length != 2)
            {
                throw new ArgumentException($"Invalid table name format: {tableName}. Expected: schema.table");
            }
            return $"\"{parts[0]}\".\"{parts[1]}\"";
        }

        return $"\"{sanitized}\"";
    }

    /// <summary>
    /// Sanitizes filter clause to prevent SQL injection
    /// This is a basic implementation - production should use a proper CQL parser
    /// </summary>
    private static string SanitizeFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return string.Empty;
        }

        // Remove any potentially dangerous SQL keywords and patterns
        var dangerous = new[] { ";", "--", "/*", "*/", "xp_", "sp_", "DROP", "DELETE", "TRUNCATE", "INSERT", "UPDATE" };

        foreach (var term in dangerous)
        {
            if (filter.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Filter contains potentially dangerous content: {term}");
            }
        }

        return filter;
    }
}
