// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Spatial;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.IO;
using Honua.Server.Core.Data;
using Honua.Server.Core.Query;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using SpatialGeometry = Microsoft.Spatial.Geometry;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.OData.Services;

/// <summary>
/// Service responsible for geometry conversions and spatial filtering operations.
/// Handles WKT, GeoJSON, and spatial transformations for OData operations.
/// </summary>
public sealed class ODataGeometryService
{
    private static readonly GeoJsonReader GeoJsonReader = new();
    private static readonly GeoJsonWriter GeoJsonWriter = new();
    private static readonly WKTReader WktReader = new();
    private static readonly WKTWriter WktWriter = new();

    private readonly ILogger<ODataGeometryService> _logger;

    public ODataGeometryService(ILogger<ODataGeometryService> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public IPreparedGeometry? PrepareFilterGeometry(GeoIntersectsFilterInfo info, int targetSrid)
    {
        if (info.Geometry.WellKnownText.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            var ntsGeometry = WktReader.Read(info.Geometry.WellKnownText);
            var sourceSrid = info.Geometry.Srid ?? info.StorageSrid ?? targetSrid;
            if (sourceSrid > 0)
            {
                ntsGeometry.SRID = sourceSrid;
            }

            if (targetSrid > 0 && ntsGeometry.SRID != targetSrid)
            {
                ntsGeometry = (NtsGeometry)CrsTransform.TransformGeometry(ntsGeometry, ntsGeometry.SRID, targetSrid);
            }
            else if (targetSrid > 0)
            {
                ntsGeometry.SRID = targetSrid;
            }

            var prepared = PreparedGeometryFactory.Prepare(ntsGeometry);
            _logger.LogDebug("Prepared geo.intersects filter from SRID {SourceSrid} to {TargetSrid}", sourceSrid, ntsGeometry.SRID);
            return prepared;
        }
        catch (ParseException)
        {
            return null;
        }
    }

    public bool RecordIntersectsFilter(FeatureRecord record, GeoIntersectsFilterInfo info, IPreparedGeometry preparedFilter)
    {
        if (!record.Attributes.TryGetValue(info.Field, out var value))
        {
            return false;
        }

        var featureGeometry = ComputeGeometry(value);
        if (featureGeometry is null)
        {
            return false;
        }

        var filterGeometry = preparedFilter.Geometry;
        var comparisonSrid = info.TargetSrid ?? filterGeometry.SRID;
        if (comparisonSrid > 0)
        {
            var sourceSrid = featureGeometry.SRID;
            if (sourceSrid <= 0)
            {
                sourceSrid = info.StorageSrid ?? comparisonSrid;
            }
            else if (info.StorageSrid.HasValue && sourceSrid == comparisonSrid && Math.Abs(featureGeometry.EnvelopeInternal.MinX) > 360)
            {
                sourceSrid = info.StorageSrid.Value;
            }

            _logger.LogDebug("Transforming feature geometry from SRID {SourceSrid} to {TargetSrid}", sourceSrid, comparisonSrid);

            if (sourceSrid > 0 && sourceSrid != comparisonSrid)
            {
                featureGeometry = (NtsGeometry)CrsTransform.TransformGeometry(featureGeometry, sourceSrid, comparisonSrid);
                _logger.LogDebug("Transformed feature geometry: {Wkt}", WktWriter.Write(featureGeometry));
            }
            else
            {
                featureGeometry = (NtsGeometry)featureGeometry.Copy();
                featureGeometry.SRID = comparisonSrid;
            }
        }

        var intersects = preparedFilter.Intersects(featureGeometry);
        _logger.LogDebug(
            "Evaluated geo.intersects: feature {FeatureWkt} (SRID {FeatureSrid}) vs filter {FilterWkt} (SRID {FilterSrid}) => {Intersects}",
            WktWriter.Write(featureGeometry),
            featureGeometry.SRID,
            WktWriter.Write(filterGeometry),
            filterGeometry.SRID,
            intersects);
        return intersects;
    }

    public SpatialGeometry? ConvertGeometryToSpatial(object? rawGeometry, int? srid)
    {
        if (rawGeometry is null)
        {
            return null;
        }

        if (rawGeometry is SpatialGeometry spatialValue)
        {
            if (!srid.HasValue || srid.Value <= 0)
            {
                return spatialValue;
            }

            var current = spatialValue.CoordinateSystem?.EpsgId;
            if (current.HasValue && current.Value == srid.Value)
            {
                return spatialValue;
            }

            var wktFromSpatial = WriteSpatialAsWkt(spatialValue);
            return ConvertWktToSpatial(wktFromSpatial, srid);
        }

        var geometry = ComputeGeometry(rawGeometry);
        if (geometry is not null)
        {
            return ConvertNetTopologyToSpatial(geometry, srid);
        }

        if (rawGeometry is string text)
        {
            return ConvertWktToSpatial(text, srid);
        }

        return null;
    }

    public string? ComputeWkt(object? geometry)
    {
        try
        {
            var geom = ComputeGeometry(geometry);
            return geom is null ? null : WktWriter.Write(geom);
        }
        catch (Exception)
        {
            return geometry?.ToString();
        }
    }

    public NtsGeometry? ComputeGeometry(object? geometry)
    {
        return geometry switch
        {
            null => null,
            NtsGeometry g => g,
            System.Text.Json.Nodes.JsonNode node => GeoJsonReader.Read<NtsGeometry>(node.ToJsonString()),
            System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.String => ParseGeometryText(element.GetString()),
            System.Text.Json.JsonElement element => GeoJsonReader.Read<NtsGeometry>(element.GetRawText()),
            string text => LooksLikeJson(text) ? GeoJsonReader.Read<NtsGeometry>(text) : ParseGeometryText(text),
            _ => ParseGeometryText(geometry.ToString())
        };
    }

    private static SpatialGeometry? ConvertNetTopologyToSpatial(NtsGeometry geometry, int? srid)
    {
        var targetSrid = srid ?? (geometry.SRID > 0 ? geometry.SRID : (int?)null);
        var prepared = targetSrid.HasValue && geometry.SRID != targetSrid.Value
            ? (NtsGeometry)geometry.Copy()
            : geometry;

        if (targetSrid.HasValue && prepared.SRID != targetSrid.Value)
        {
            prepared.SRID = targetSrid.Value;
        }

        var wkt = WktWriter.Write(prepared);
        return ConvertWktToSpatial(wkt, targetSrid);
    }

    private static SpatialGeometry? ConvertWktToSpatial(string? wkt, int? srid)
    {
        if (wkt.IsNullOrWhiteSpace())
        {
            return null;
        }

        var appliedSrid = srid;
        if (TryExtractSridFromWkt(wkt, out var sridFromWkt))
        {
            appliedSrid ??= sridFromWkt;
        }

        var text = EnsureWktHasSrid(wkt, appliedSrid);
        var formatter = WellKnownTextSqlFormatter.Create();

        try
        {
            using var reader = new StringReader(text);
            return formatter.Read<SpatialGeometry>(reader);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ParseErrorException)
        {
            return null;
        }
    }

    private static string EnsureWktHasSrid(string wkt, int? srid)
    {
        if (!srid.HasValue || srid.Value <= 0)
        {
            return wkt;
        }

        var trimmed = wkt.TrimStart();
        if (TryExtractSridFromWkt(trimmed, out _))
        {
            var semicolon = trimmed.IndexOf(';');
            var remainder = semicolon >= 0 && semicolon + 1 < trimmed.Length
                ? trimmed[(semicolon + 1)..]
                : string.Empty;

            return $"SRID={srid.Value};{remainder}";
        }

        return $"SRID={srid.Value};{trimmed}";
    }

    private static bool TryExtractSridFromWkt(string wkt, out int srid)
    {
        srid = 0;
        var trimmed = wkt.TrimStart();
        if (!trimmed.StartsWith("SRID=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separator = trimmed.IndexOf(';');
        if (separator <= 5)
        {
            return false;
        }

        var sridSegment = trimmed.Substring(5, separator - 5);
        return int.TryParse(sridSegment, NumberStyles.Integer, CultureInfo.InvariantCulture, out srid);
    }

    private static string? ConvertSpatialToGeoJson(SpatialGeometry spatial)
    {
        if (spatial is null)
        {
            return null;
        }

        var wkt = WriteSpatialAsWkt(spatial);
        var geometry = ParseGeometryText(wkt);
        if (geometry is null)
        {
            return wkt;
        }

        return GeoJsonWriter.Write(geometry);
    }

    private static string WriteSpatialAsWkt(SpatialGeometry spatial)
    {
        var formatter = WellKnownTextSqlFormatter.Create();
        using var writer = new StringWriter();
        formatter.Write(spatial, writer);
        return writer.ToString();
    }

    private static NtsGeometry? ParseGeometryText(string? text)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            return WktReader.Read(text);
        }
        catch (ParseException)
        {
            return GeoJsonReader.Read<NtsGeometry>(text);
        }
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) ||
               trimmed.StartsWith("[", StringComparison.Ordinal);
    }
}
