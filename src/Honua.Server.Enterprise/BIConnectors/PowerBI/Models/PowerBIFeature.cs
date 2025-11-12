// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Results;

namespace Honua.Server.Enterprise.BIConnectors.PowerBI.Models;

/// <summary>
/// Flattened feature representation optimized for Power BI consumption.
/// Power BI works best with flat table structures, so we flatten nested properties.
/// </summary>
public class PowerBIFeature
{
    /// <summary>
    /// Unique feature identifier
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Feature display name
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Geometry type (Point, LineString, Polygon, etc.)
    /// </summary>
    public string? GeometryType { get; set; }

    /// <summary>
    /// Longitude (for Point geometries)
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Latitude (for Point geometries)
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// GeoJSON geometry as string (for non-Point geometries)
    /// </summary>
    public string? Geometry { get; set; }

    /// <summary>
    /// All feature properties as JSON string
    /// (Power BI can parse this with Power Query)
    /// </summary>
    public string? Properties { get; set; }

    /// <summary>
    /// Feature creation timestamp (if available)
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Feature update timestamp (if available)
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Dynamic properties extracted from feature attributes
    /// These are common fields that Power BI can filter on directly
    /// </summary>
    public Dictionary<string, object?> DynamicProperties { get; set; } = new();

    /// <summary>
    /// Creates a PowerBIFeature from a FeatureRecord
    /// </summary>
    public static PowerBIFeature FromFeatureRecord(FeatureRecord record, LayerDefinition layer)
    {
        var feature = new PowerBIFeature
        {
            Id = record.Id?.ToString() ?? Guid.NewGuid().ToString(),
            Properties = record.Attributes != null ? JsonSerializer.Serialize(record.Attributes) : null
        };

        // Extract geometry information
        if (record.Geometry != null)
        {
            feature.GeometryType = record.Geometry.GeometryType;

            // For Point geometries, extract Lat/Lon for easy mapping in Power BI
            if (record.Geometry.GeometryType == "Point" && record.Geometry.Coordinates is JsonElement coordsElement)
            {
                if (coordsElement.ValueKind == JsonValueKind.Array && coordsElement.GetArrayLength() >= 2)
                {
                    feature.Longitude = coordsElement[0].GetDouble();
                    feature.Latitude = coordsElement[1].GetDouble();
                }
            }
            else
            {
                // For other geometries, serialize as GeoJSON string
                feature.Geometry = JsonSerializer.Serialize(record.Geometry);
            }
        }

        // Extract common temporal fields
        if (record.Attributes != null)
        {
            if (record.Attributes.TryGetValue("created_at", out var createdAtObj) && createdAtObj != null)
            {
                if (DateTimeOffset.TryParse(createdAtObj.ToString(), out var createdAt))
                {
                    feature.CreatedAt = createdAt;
                }
            }

            if (record.Attributes.TryGetValue("updated_at", out var updatedAtObj) && updatedAtObj != null)
            {
                if (DateTimeOffset.TryParse(updatedAtObj.ToString(), out var updatedAt))
                {
                    feature.UpdatedAt = updatedAt;
                }
            }

            // Extract display name from common fields
            var displayNameFields = new[] { "name", "title", "label", "display_name", "displayName" };
            foreach (var field in displayNameFields)
            {
                if (record.Attributes.TryGetValue(field, out var nameValue) && nameValue != null)
                {
                    feature.DisplayName = nameValue.ToString();
                    break;
                }
            }

            // Copy all attributes to dynamic properties for easy access
            foreach (var kvp in record.Attributes)
            {
                feature.DynamicProperties[kvp.Key] = kvp.Value;
            }
        }

        return feature;
    }
}
