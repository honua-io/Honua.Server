// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Results;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Honua.Server.Host.Wfs.Filters;
using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.IO.GML3;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Shared helper methods used across WFS handler classes.
/// </summary>
internal static class WfsHelpers
{
    #region Context Resolution

    /// <summary>
    /// Resolves a layer context from a type name.
    /// </summary>
    public static async Task<Result<FeatureContext>> ResolveLayerContextAsync(
        string typeNamesRaw,
        ICatalogProjectionService catalog,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken)
    {
        if (typeNamesRaw.IsNullOrWhiteSpace())
        {
            return Result<FeatureContext>.Failure(Error.Invalid("Parameter 'typeNames' is required."));
        }

        var typeName = QueryParsingHelpers.ParseCsv(typeNamesRaw).FirstOrDefault();
        if (typeName.IsNullOrWhiteSpace())
        {
            return Result<FeatureContext>.Failure(Error.Invalid("Parameter 'typeNames' is required."));
        }

        string? serviceId = null;
        string layerId;

        var parts = typeName.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            serviceId = parts[0];
            layerId = parts[1];
        }
        else
        {
            layerId = parts[0];
            var projection = catalog.GetSnapshot();
            foreach (var service in projection.ServiceIndex.Values)
            {
                if (service.Layers.Any(l => l.Layer.Id.EqualsIgnoreCase(layerId)))
                {
                    serviceId = service.Service.Id;
                    break;
                }
            }
        }

        if (serviceId.IsNullOrWhiteSpace())
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Layer '{typeName}' was not found."));
        }

        try
        {
            var context = await resolver.ResolveAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
            return Result<FeatureContext>.Success(context);
        }
        catch (KeyNotFoundException ex)
        {
            return Result<FeatureContext>.Failure(Error.NotFound(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result<FeatureContext>.Failure(Error.Invalid(ex.Message));
        }
        catch (NotSupportedException ex)
        {
            return Result<FeatureContext>.Failure(Error.Invalid(ex.Message));
        }
    }

    /// <summary>
    /// Maps a resolution error to a WFS exception.
    /// </summary>
    public static IResult MapResolutionError(Error error, string typeNamesRaw)
    {
        return error.Code switch
        {
            "not_found" => CreateException("InvalidParameterValue", "typeNames", error.Message ?? $"Layer '{typeNamesRaw}' was not found."),
            "invalid" => CreateException("NoApplicableCode", "typeNames", error.Message ?? "Layer resolution failed."),
            _ => CreateException("NoApplicableCode", "typeNames", error.Message ?? "Layer resolution failed.")
        };
    }

    /// <summary>
    /// Maps an execution error to a WFS exception.
    /// </summary>
    public static IResult MapExecutionError(Error error, IQueryCollection query)
    {
        if (error is null)
        {
            return CreateException("NoApplicableCode", "request", "Request could not be processed.");
        }

        return error.Code switch
        {
            "not_found" => CreateException("InvalidParameterValue", "typeNames", error.Message ?? "Requested layer was not found."),
            "invalid" => CreateException("InvalidParameterValue", "request", error.Message ?? "Request is not valid."),
            _ => CreateException("NoApplicableCode", "request", error.Message ?? "Request could not be processed.")
        };
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a limit (count/maxFeatures) parameter using shared helper.
    /// Automatically enforces service and layer limits.
    /// </summary>
    /// <param name="query">Query collection.</param>
    /// <param name="paramName">Parameter name (e.g., "count" or "maxFeatures").</param>
    /// <param name="layer">Layer definition for limit enforcement.</param>
    /// <param name="service">Service definition for limit enforcement.</param>
    /// <returns>Parsed and clamped limit value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when parameter is invalid.</exception>
    public static int ParseLimit(IQueryCollection query, string paramName, LayerDefinition layer, ServiceDefinition service)
    {
        var raw = QueryParsingHelpers.GetQueryValue(query, paramName);
        var (limit, error) = QueryParameterHelper.ParseLimit(
            raw,
            service.Ogc?.ItemLimit,
            layer.Query?.MaxRecordCount,
            fallback: WfsConstants.DefaultCount);

        if (error is not null)
        {
            throw new InvalidOperationException($"Parameter '{paramName}' {error}");
        }

        if (!limit.HasValue)
        {
            throw new InvalidOperationException($"Parameter '{paramName}' is required.");
        }

        return limit.Value;
    }

    /// <summary>
    /// Parses an offset (startIndex) parameter using shared helper.
    /// </summary>
    /// <param name="query">Query collection.</param>
    /// <param name="paramName">Parameter name (typically "startIndex").</param>
    /// <returns>Parsed offset value, or 0 if not provided.</returns>
    /// <exception cref="InvalidOperationException">Thrown when parameter is invalid.</exception>
    public static int ParseOffset(IQueryCollection query, string paramName)
    {
        var raw = QueryParsingHelpers.GetQueryValue(query, paramName);
        var (offset, error) = QueryParameterHelper.ParseOffset(raw);

        if (error is not null)
        {
            throw new InvalidOperationException($"Parameter '{paramName}' {error}");
        }

        return offset ?? 0;
    }

    /// <summary>
    /// Parses a bounding box from query parameters using shared helper.
    /// </summary>
    public static BoundingBox? ParseBoundingBox(IQueryCollection query)
    {
        var raw = QueryParsingHelpers.GetQueryValue(query, "bbox");

        // First try to extract CRS from the raw parameter if present (WFS 2.0 format: minx,miny,maxx,maxy,crs)
        string? crs = null;
        if (raw.HasValue())
        {
            var parts = raw.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 5)
            {
                // Fifth part is CRS
                crs = parts[4];
                raw = string.Join(',', parts[0], parts[1], parts[2], parts[3]);
            }
        }

        var (bbox, error) = QueryParameterHelper.ParseBoundingBox(raw, crs);

        if (error is not null)
        {
            if (raw.IsNullOrWhiteSpace())
            {
                return null;
            }
            throw new InvalidOperationException($"Parameter 'bbox': {error}");
        }

        return bbox;
    }

    /// <summary>
    /// Parses the result type from query parameters using shared helper.
    /// </summary>
    public static FeatureResultType ParseResultType(IQueryCollection query)
    {
        var raw = QueryParsingHelpers.GetQueryValue(query, "resultType");
        var (resultType, error) = QueryParameterHelper.ParseResultType(raw, FeatureResultType.Results);

        if (error is not null)
        {
            throw new InvalidOperationException($"Parameter 'resultType': {error}");
        }

        return resultType;
    }

    /// <summary>
    /// Parses lock duration from query parameters.
    /// </summary>
    public static TimeSpan ParseLockDuration(IQueryCollection query)
    {
        var expiry = QueryParsingHelpers.GetQueryValue(query, "expiry") ?? QueryParsingHelpers.GetQueryValue(query, "EXPIRY");
        if (expiry.IsNullOrWhiteSpace())
        {
            return WfsConstants.DefaultLockDuration;
        }

        if (!expiry.TryParseDoubleStrict(out var minutes) || minutes <= 0)
        {
            throw new InvalidOperationException("Parameter 'expiry' must be a positive numeric value representing minutes.");
        }

        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Parses a literal value from text.
    /// </summary>
    public static object? ParseLiteral(string? text)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return null;
        }

        var trimmed = text.Trim();

        if ((trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal)) ||
            (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal)))
        {
            try
            {
                return JsonNode.Parse(trimmed);
            }
            catch
            {
                // fall through to string
            }
        }

        if (trimmed.TryParseInt(out var i))
        {
            return i;
        }

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }

        if (trimmed.TryParseDouble(out var d))
        {
            return d;
        }

        if (bool.TryParse(trimmed, out var b))
        {
            return b;
        }

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            return dto;
        }

        return trimmed;
    }

    #endregion

    #region Filter Building

    /// <summary>
    /// Builds a query filter from request.
    /// </summary>
    public static async Task<QueryFilter?> BuildFilterAsync(HttpRequest request, IQueryCollection query, LayerDefinition layer, CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(query);
        Guard.NotNull(layer);

        foreach (var candidate in new[]
        {
            QueryParsingHelpers.GetQueryValue(query, "filter"),
            QueryParsingHelpers.GetQueryValue(query, "cql_filter"),
            QueryParsingHelpers.GetQueryValue(query, "FILTER")
        })
        {
            var parsed = TryParseFilter(candidate, layer);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        if (!HttpMethods.IsPost(request.Method))
        {
            return null;
        }

        request.EnableBuffering();
        request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        request.Body.Seek(0, SeekOrigin.Begin);

        if (body.IsNullOrWhiteSpace())
        {
            return null;
        }

        return ParseXmlFilter(body, layer);
    }

    private static QueryFilter? TryParseFilter(string? candidate, LayerDefinition layer)
    {
        if (candidate.IsNullOrWhiteSpace())
        {
            return null;
        }

        var trimmed = candidate.Trim();
        return trimmed.StartsWith("<", StringComparison.Ordinal)
            ? ParseXmlFilter(trimmed, layer)
            : ParseCqlFilter(trimmed, layer);
    }

    private static QueryFilter ParseCqlFilter(string text, LayerDefinition layer)
        => CqlFilterParser.Parse(text, layer);

    private static QueryFilter ParseXmlFilter(string text, LayerDefinition layer)
        => XmlFilterParser.Parse(text, layer);

    #endregion

    #region Format Handling

    /// <summary>
    /// Tries to normalize the output format.
    /// </summary>
    public static bool TryNormalizeOutputFormat(string? format, out string normalized)
    {
        if (format.IsNullOrWhiteSpace())
        {
            normalized = WfsConstants.GmlFormat;
            return true;
        }

        var candidate = format.Trim();
        candidate = candidate.Replace(' ', '+');
        if (CultureInvariantHelpers.StartsWithIgnoreCase(candidate, WfsConstants.GeoJsonFormat) ||
            candidate.EqualsIgnoreCase("json") ||
            candidate.EqualsIgnoreCase("application/json"))
        {
            normalized = WfsConstants.GeoJsonFormat;
            return true;
        }

        if (CultureInvariantHelpers.StartsWithIgnoreCase(candidate, "application/gml+xml") ||
            candidate.EqualsIgnoreCase("gml32") ||
            candidate.EqualsIgnoreCase("gml"))
        {
            normalized = WfsConstants.GmlFormat;
            return true;
        }

        if (CultureInvariantHelpers.StartsWithIgnoreCase(candidate, "text/csv") ||
            candidate.EqualsIgnoreCase("csv"))
        {
            normalized = WfsConstants.CsvFormat;
            return true;
        }

        if (CultureInvariantHelpers.StartsWithIgnoreCase(candidate, "application/x-shapefile") ||
            candidate.EqualsIgnoreCase("shapefile") ||
            candidate.EqualsIgnoreCase("shape"))
        {
            normalized = WfsConstants.ShapefileFormat;
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    #endregion

    #region Geometry Handling

    /// <summary>
    /// Tries to read geometry from a feature record.
    /// </summary>
    public static Geometry? TryReadGeometry(LayerDefinition layer, FeatureRecord record, int srid)
    {
        if (layer.GeometryField.IsNullOrWhiteSpace())
        {
            return null;
        }

        if (!record.Attributes.TryGetValue(layer.GeometryField, out var value) || value is null)
        {
            return null;
        }

        try
        {
            Geometry geometry = value switch
            {
                Geometry g => g,
                JsonNode node => WfsConstants.GeoJsonReader.Read<Geometry>(node.ToJsonString()),
                JsonElement element when element.ValueKind == JsonValueKind.String => WfsConstants.GeoJsonReader.Read<Geometry>(element.GetString()!),
                JsonElement element => WfsConstants.GeoJsonReader.Read<Geometry>(element.GetRawText()),
                string text => WfsConstants.GeoJsonReader.Read<Geometry>(text),
                _ => WfsConstants.GeoJsonReader.Read<Geometry>(value.ToString()!)
            };

            if (srid != 0)
            {
                geometry.SRID = srid;
            }

            return geometry;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a geometry element in GML format.
    /// </summary>
    public static XElement? WriteGeometryElement(Geometry geometry, string srsName)
    {
        if (geometry.IsEmpty)
        {
            return null;
        }

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            NamespaceHandling = NamespaceHandling.OmitDuplicates
        };

        var builder = new StringBuilder();
        var geometryWriter = new GML3Writer();
        using (var writer = XmlWriter.Create(builder, settings))
        {
            geometryWriter.Write(geometry, writer);
        }

        var element = XElement.Parse(builder.ToString());
        if (srsName.HasValue())
        {
            element.SetAttributeValue("srsName", srsName);
        }

        return element;
    }

    /// <summary>
    /// Tries to parse a geometry element from XML.
    /// </summary>
    public static JsonNode? TryParseGeometryElement(XElement element)
    {
        var geometryElement = element.Name.Namespace == WfsConstants.Gml ? element : element.Descendants().FirstOrDefault(e => e.Name.Namespace == WfsConstants.Gml);
        if (geometryElement is null)
        {
            return null;
        }

        switch (geometryElement.Name.LocalName)
        {
            case "Point":
                {
                    var pos = geometryElement.Element(WfsConstants.Gml + "pos")?.Value;
                    if (pos.HasValue())
                    {
                        var coordinates = ParseCoordinateArray(pos);
                        if (coordinates.Count > 0 && coordinates[0] is JsonArray point)
                        {
                            return new JsonObject
                            {
                                ["type"] = "Point",
                                ["coordinates"] = point
                            };
                        }
                    }

                    break;
                }
            case "LineString":
                {
                    var posList = geometryElement.Element(WfsConstants.Gml + "posList")?.Value;
                    if (posList.HasValue())
                    {
                        var coordinates = ParseCoordinateArray(posList);
                        if (coordinates.Count > 0)
                        {
                            return new JsonObject
                            {
                                ["type"] = "LineString",
                                ["coordinates"] = coordinates
                            };
                        }
                    }

                    var positions = geometryElement.Elements(WfsConstants.Gml + "pos").Select(e => e.Value).Where(v => v.HasValue()).ToList();
                    if (positions.Count > 0)
                    {
                        var coordinates = new JsonArray();
                        foreach (var position in positions)
                        {
                            var parsed = ParseCoordinateArray(position);
                            if (parsed.Count > 0 && parsed[0] is JsonArray point)
                            {
                                coordinates.Add(point);
                            }
                        }

                        if (coordinates.Count > 0)
                        {
                            return new JsonObject
                            {
                                ["type"] = "LineString",
                                ["coordinates"] = coordinates
                            };
                        }
                    }

                    break;
                }
            case "Polygon":
                {
                    var exterior = geometryElement.Descendants(WfsConstants.Gml + "exterior").FirstOrDefault();
                    var ring = exterior?.Descendants(WfsConstants.Gml + "posList").FirstOrDefault()?.Value;
                    if (ring.HasValue())
                    {
                        var coordinates = ParseCoordinateArray(ring);
                        if (coordinates.Count > 0)
                        {
                            var rings = new JsonArray { coordinates };
                            return new JsonObject
                            {
                                ["type"] = "Polygon",
                                ["coordinates"] = rings
                            };
                        }
                    }

                    break;
                }
        }

        return null;
    }

    private static JsonArray ParseCoordinateArray(string? text)
    {
        var coordinates = new JsonArray();

        if (text.IsNullOrWhiteSpace())
        {
            return coordinates;
        }

        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index + 1 < tokens.Length; index += 2)
        {
            if (!tokens[index].TryParseDouble(out var x))
            {
                continue;
            }

            if (!tokens[index + 1].TryParseDouble(out var y))
            {
                continue;
            }

            coordinates.Add(new JsonArray(x, y));
        }

        return coordinates;
    }

    #endregion

    #region CRS Handling

    /// <summary>
    /// Converts a CRS to URN format.
    /// </summary>
    public static string ToUrn(string crs)
    {
        if (crs.IsNullOrWhiteSpace())
        {
            return "urn:ogc:def:crs:EPSG::4326";
        }

        if (CultureInvariantHelpers.StartsWithIgnoreCase(crs, "urn:"))
        {
            return crs;
        }

        if (CultureInvariantHelpers.StartsWithIgnoreCase(crs, "EPSG"))
        {
            var code = crs.Split(':').LastOrDefault() ?? "4326";
            return $"urn:ogc:def:crs:EPSG::{code}";
        }

        return crs;
    }

    #endregion

    #region Conversion Helpers

    /// <summary>
    /// Converts a value to an invariant culture string.
    /// </summary>
    public static string? ConvertToInvariantString(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            JsonNode node => node.ToJsonString(),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Converts a value to a property value string for GML.
    /// </summary>
    public static string? ConvertToPropertyValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode node => ConvertJsonNodeToString(node),
            JsonElement element => JsonElementConverter.ToString(element),
            bool b => b ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static string? ConvertJsonNodeToString(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return JsonElementConverter.ToString(document.RootElement);
    }


    /// <summary>
    /// Converts a value to a JSON node.
    /// </summary>
    public static JsonNode? ConvertToJsonNode(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode node => node?.DeepClone(),
            JsonElement element => JsonElementConverter.ToObjectWithJsonNode(element) as JsonNode,
            string s => JsonValue.Create(s),
            int i => JsonValue.Create(i),
            long l => JsonValue.Create(l),
            double d => JsonValue.Create(d),
            float f => JsonValue.Create(f),
            decimal m => JsonValue.Create(m),
            bool b => JsonValue.Create(b),
            DateTimeOffset dto => JsonValue.Create(dto),
            DateTime dt => JsonValue.Create(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => JsonValue.Create(value.ToString())
        };
    }


    #endregion

    #region Formatting

    /// <summary>
    /// Formats a coordinate value.
    /// </summary>
    public static string FormatCoordinate(double value)
    {
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a lock target.
    /// </summary>
    public static string FormatLockTarget(WfsLockTarget target)
    {
        return $"{target.ServiceId}:{target.LayerId}.{target.FeatureId}";
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Builds the endpoint URL for WFS.
    /// </summary>
    public static string BuildEndpointUrl(HttpRequest request)
    {
        return request.BuildAbsoluteUrl("/wfs");
    }

    /// <summary>
    /// Resolves the lock owner from the HTTP context.
    /// Delegates to UserIdentityHelper for consistent identity resolution.
    /// </summary>
    public static string ResolveLockOwner(HttpContext context)
    {
        return UserIdentityHelper.GetUserIdentifier(context?.User);
    }

    /// <summary>
    /// Extracts service ID from namespace URI.
    /// </summary>
    public static string? ExtractServiceIdFromNamespace(string? namespaceUri)
    {
        if (namespaceUri.IsNullOrWhiteSpace())
        {
            return null;
        }

        var parts = namespaceUri.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts[^1];
    }

    /// <summary>
    /// Extracts feature identifier from a feature record.
    /// </summary>
    public static string? ExtractFeatureIdentifier(LayerDefinition layer, FeatureRecord record)
    {
        if (layer.IdField.IsNullOrWhiteSpace())
        {
            return null;
        }

        if (!record.Attributes.TryGetValue(layer.IdField, out var value) || value is null)
        {
            return null;
        }

        return ConvertToInvariantString(value);
    }

    /// <summary>
    /// Tries to extract ID value from attributes.
    /// </summary>
    public static string? TryExtractIdValue(IDictionary<string, object?> attributes, string idField)
    {
        if (attributes.TryGetValue(idField, out var value) && value is not null)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return null;
    }

    /// <summary>
    /// Builds an envelope from features.
    /// </summary>
    public static Envelope? BuildEnvelope(IReadOnlyList<WfsFeature> features)
    {
        var geometries = features
            .Where(f => f.Geometry is not null && !f.Geometry.IsEmpty)
            .Select(f => f.Geometry!)
            .ToList();

        var extent = ExtentCalculator.CalculateExtentFromGeometries(geometries);
        if (extent is null)
        {
            return null;
        }

        return ExtentCalculator.ExtentToEnvelope(extent.Value.MinX, extent.Value.MinY, extent.Value.MaxX, extent.Value.MaxY);
    }

    /// <summary>
    /// Creates a WFS exception result.
    /// </summary>
    public static IResult CreateException(string code, string locator, string message)
    {
        return OgcExceptionHelper.CreateWfsException(code, locator, message);
    }

    #endregion

    #region Schema Helpers

    /// <summary>
    /// Resolves geometry type to GML type.
    /// </summary>
    public static string ResolveGeometryType(string? geometryType)
    {
        return FieldMetadataResolver.ResolveGmlGeometryType(geometryType);
    }

    /// <summary>
    /// Maps field data type to XML schema type.
    /// </summary>
    public static string MapFieldType(string? dataType)
    {
        return FieldMetadataResolver.MapToOgcType(dataType);
    }

    #endregion
}
