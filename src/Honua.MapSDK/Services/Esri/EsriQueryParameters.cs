// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models.Esri;

namespace Honua.MapSDK.Services.Esri;

/// <summary>
/// Query parameters for Esri FeatureServer query operations
/// </summary>
public class EsriQueryParameters
{
    /// <summary>
    /// Where clause (SQL where expression)
    /// </summary>
    public string Where { get; set; } = "1=1";

    /// <summary>
    /// Object IDs to query
    /// </summary>
    public List<int>? ObjectIds { get; set; }

    /// <summary>
    /// Geometry filter
    /// </summary>
    public EsriGeometry? Geometry { get; set; }

    /// <summary>
    /// Spatial relationship (esriSpatialRelIntersects, esriSpatialRelContains, etc.)
    /// </summary>
    public EsriSpatialRelationship SpatialRel { get; set; } = EsriSpatialRelationship.Intersects;

    /// <summary>
    /// Input geometry spatial reference (WKID)
    /// </summary>
    public int? InSpatialReference { get; set; }

    /// <summary>
    /// Output fields (list of field names or "*" for all)
    /// </summary>
    public List<string> OutFields { get; set; } = new() { "*" };

    /// <summary>
    /// Return geometry (true/false)
    /// </summary>
    public bool ReturnGeometry { get; set; } = true;

    /// <summary>
    /// Output spatial reference (WKID)
    /// </summary>
    public int? OutSpatialReference { get; set; }

    /// <summary>
    /// Return only distinct values
    /// </summary>
    public bool ReturnDistinctValues { get; set; } = false;

    /// <summary>
    /// Return IDs only (no geometry or attributes)
    /// </summary>
    public bool ReturnIdsOnly { get; set; } = false;

    /// <summary>
    /// Return count only
    /// </summary>
    public bool ReturnCountOnly { get; set; } = false;

    /// <summary>
    /// Return extent only
    /// </summary>
    public bool ReturnExtentOnly { get; set; } = false;

    /// <summary>
    /// Order by clause (e.g., "FIELD_NAME ASC" or "FIELD_NAME DESC")
    /// </summary>
    public string? OrderByFields { get; set; }

    /// <summary>
    /// Group by fields (for statistics)
    /// </summary>
    public List<string>? GroupByFieldsForStatistics { get; set; }

    /// <summary>
    /// Statistical definitions
    /// </summary>
    public List<EsriStatisticDefinition>? OutStatistics { get; set; }

    /// <summary>
    /// Return Z values
    /// </summary>
    public bool ReturnZ { get; set; } = false;

    /// <summary>
    /// Return M values
    /// </summary>
    public bool ReturnM { get; set; } = false;

    /// <summary>
    /// Maximum allowable offset (for generalization)
    /// </summary>
    public double? MaxAllowableOffset { get; set; }

    /// <summary>
    /// Geometry precision (number of decimal places)
    /// </summary>
    public int? GeometryPrecision { get; set; }

    /// <summary>
    /// Result offset (for pagination)
    /// </summary>
    public int? ResultOffset { get; set; }

    /// <summary>
    /// Result record count (for pagination)
    /// </summary>
    public int? ResultRecordCount { get; set; }

    /// <summary>
    /// Output format (json, geojson, pbf)
    /// </summary>
    public string Format { get; set; } = "json";

    /// <summary>
    /// Convert to URL query string
    /// </summary>
    public Dictionary<string, string> ToQueryParameters()
    {
        var parameters = new Dictionary<string, string>
        {
            ["where"] = Where,
            ["outFields"] = string.Join(",", OutFields),
            ["returnGeometry"] = ReturnGeometry.ToString().ToLower(),
            ["f"] = Format
        };

        if (ObjectIds != null && ObjectIds.Count > 0)
        {
            parameters["objectIds"] = string.Join(",", ObjectIds);
        }

        if (Geometry != null)
        {
            parameters["geometry"] = SerializeGeometry(Geometry);
            parameters["geometryType"] = GetGeometryType(Geometry);
            parameters["spatialRel"] = GetSpatialRelString(SpatialRel);

            if (InSpatialReference.HasValue)
            {
                parameters["inSR"] = InSpatialReference.Value.ToString();
            }
        }

        if (OutSpatialReference.HasValue)
        {
            parameters["outSR"] = OutSpatialReference.Value.ToString();
        }

        if (ReturnDistinctValues)
        {
            parameters["returnDistinctValues"] = "true";
        }

        if (ReturnIdsOnly)
        {
            parameters["returnIdsOnly"] = "true";
        }

        if (ReturnCountOnly)
        {
            parameters["returnCountOnly"] = "true";
        }

        if (ReturnExtentOnly)
        {
            parameters["returnExtentOnly"] = "true";
        }

        if (!string.IsNullOrEmpty(OrderByFields))
        {
            parameters["orderByFields"] = OrderByFields;
        }

        if (GroupByFieldsForStatistics != null && GroupByFieldsForStatistics.Count > 0)
        {
            parameters["groupByFieldsForStatistics"] = string.Join(",", GroupByFieldsForStatistics);
        }

        if (OutStatistics != null && OutStatistics.Count > 0)
        {
            parameters["outStatistics"] = System.Text.Json.JsonSerializer.Serialize(OutStatistics);
        }

        if (ReturnZ)
        {
            parameters["returnZ"] = "true";
        }

        if (ReturnM)
        {
            parameters["returnM"] = "true";
        }

        if (MaxAllowableOffset.HasValue)
        {
            parameters["maxAllowableOffset"] = MaxAllowableOffset.Value.ToString();
        }

        if (GeometryPrecision.HasValue)
        {
            parameters["geometryPrecision"] = GeometryPrecision.Value.ToString();
        }

        if (ResultOffset.HasValue)
        {
            parameters["resultOffset"] = ResultOffset.Value.ToString();
        }

        if (ResultRecordCount.HasValue)
        {
            parameters["resultRecordCount"] = ResultRecordCount.Value.ToString();
        }

        return parameters;
    }

    private static string SerializeGeometry(EsriGeometry geometry)
    {
        return System.Text.Json.JsonSerializer.Serialize(geometry);
    }

    private static string GetGeometryType(EsriGeometry geometry)
    {
        return geometry switch
        {
            EsriPoint => "esriGeometryPoint",
            EsriMultipoint => "esriGeometryMultipoint",
            EsriPolyline => "esriGeometryPolyline",
            EsriPolygon => "esriGeometryPolygon",
            EsriEnvelope => "esriGeometryEnvelope",
            _ => "esriGeometryEnvelope"
        };
    }

    private static string GetSpatialRelString(EsriSpatialRelationship spatialRel)
    {
        return spatialRel switch
        {
            EsriSpatialRelationship.Intersects => "esriSpatialRelIntersects",
            EsriSpatialRelationship.Contains => "esriSpatialRelContains",
            EsriSpatialRelationship.Crosses => "esriSpatialRelCrosses",
            EsriSpatialRelationship.EnvelopeIntersects => "esriSpatialRelEnvelopeIntersects",
            EsriSpatialRelationship.IndexIntersects => "esriSpatialRelIndexIntersects",
            EsriSpatialRelationship.Overlaps => "esriSpatialRelOverlaps",
            EsriSpatialRelationship.Touches => "esriSpatialRelTouches",
            EsriSpatialRelationship.Within => "esriSpatialRelWithin",
            _ => "esriSpatialRelIntersects"
        };
    }
}

/// <summary>
/// Spatial relationship types
/// </summary>
public enum EsriSpatialRelationship
{
    Intersects,
    Contains,
    Crosses,
    EnvelopeIntersects,
    IndexIntersects,
    Overlaps,
    Touches,
    Within
}

/// <summary>
/// Statistic definition for outStatistics
/// </summary>
public class EsriStatisticDefinition
{
    /// <summary>
    /// Statistic type (count, sum, min, max, avg, stddev, var)
    /// </summary>
    public string StatisticType { get; set; } = "count";

    /// <summary>
    /// Field to perform statistic on
    /// </summary>
    public string OnStatisticField { get; set; } = "*";

    /// <summary>
    /// Output field name alias
    /// </summary>
    public string OutStatisticFieldName { get; set; } = "value";
}
