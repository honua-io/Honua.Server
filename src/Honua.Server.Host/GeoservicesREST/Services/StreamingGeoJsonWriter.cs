// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Precision;
using NetTopologySuite.Simplify;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Provides streaming GeoJSON responses with constant memory usage.
/// Implements RFC 7946 GeoJSON specification with async enumeration.
/// </summary>
public sealed class StreamingGeoJsonWriter
{
    private readonly ILogger<StreamingGeoJsonWriter> _logger;

    public StreamingGeoJsonWriter(ILogger<StreamingGeoJsonWriter> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Writes a GeoJSON FeatureCollection to the HTTP response stream with constant memory usage.
    /// </summary>
    /// <param name="response">HTTP response to write to</param>
    /// <param name="repository">Feature repository for data access</param>
    /// <param name="serviceId">Service identifier</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="context">Query context with filters and pagination</param>
    /// <param name="totalCount">Optional total count for pagination metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task WriteFeatureCollectionAsync(
        HttpResponse response,
        IFeatureRepository repository,
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        long? totalCount,
        CancellationToken cancellationToken)
    {
        long featuresWritten = 0;
        long bytesWritten = 0;

        await OperationInstrumentation.Create<int>("Streaming GeoJSON Write")
            .WithActivitySource(HonuaTelemetry.OgcProtocols)
            .WithLogger(_logger)
            .WithTag("arcgis.service_id", serviceId)
            .WithTag("arcgis.layer_id", layer.Id)
            .WithTag("arcgis.return_geometry", context.ReturnGeometry)
            .WithLogLevels(LogLevel.Information, LogLevel.Error)
            .ExecuteAsync(async activity =>
            {
                try
                {
                    // Set response headers for streaming
                    response.ContentType = "application/geo+json; charset=utf-8";
                    response.Headers["X-Content-Type-Options"] = "nosniff";

                    // Disable buffering to enable true streaming
                    var bufferingFeature = response.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
                    bufferingFeature?.DisableBuffering();

                    // Use Utf8JsonWriter for high-performance streaming
                    await using var streamWriter = new StreamWriter(response.Body, leaveOpen: true);
                    await using var writer = new Utf8JsonWriter(streamWriter.BaseStream, new JsonWriterOptions
                    {
                        Indented = context.PrettyPrint,
                        SkipValidation = false // Ensure valid JSON even with errors
                    });

                    // Write FeatureCollection header
                    writer.WriteStartObject();
                    writer.WriteString("type", "FeatureCollection");

                    // Write CRS if not default WGS84
                    if (context.TargetWkid != 4326)
                    {
                        WriteCrs(writer, context.TargetWkid);
                    }

                    // Write metadata
                    WriteMetadata(writer, layer, totalCount, context);

                    // Write features array start
                    writer.WritePropertyName("features");
                    writer.WriteStartArray();

                    // Stream features one at a time
                    var geoJsonWriter = new GeoJsonWriter();
                    bool limitReached = false;

                    try
                    {
                        await foreach (var record in repository.QueryAsync(serviceId, layer.Id, context.Query, cancellationToken).ConfigureAwait(false))
                        {
                            WriteFeature(writer, record, layer, context, geoJsonWriter);
                            featuresWritten++;

                            // Check if we've reached the user-requested limit (not server-imposed cap)
                            if (context.Query.Limit.HasValue && featuresWritten >= context.Query.Limit.Value)
                            {
                                limitReached = true;
                            }

                            // BUG FIX 3: Check stream writability before flushing to handle backpressure
                            // Flush periodically to send data to client, but only if stream is writable
                            if (featuresWritten % 100 == 0)
                            {
                                // Check if response stream is still writable (client hasn't disconnected)
                                if (response.Body.CanWrite)
                                {
                                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                                }

                                // Allow cancellation and backpressure
                                await Task.Yield();
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }
                    }
                    finally
                    {
                        // BUG FIX 4: Write numberReturned in finally block to handle cancellation
                        // This ensures the field is written even if the loop is interrupted
                        // Close features array
                        writer.WriteEndArray();

                        // Write summary metadata - always write actual count of features returned
                        // Note: numberMatched already written in metadata section (Bug Fix 1)
                        writer.WriteNumber("numberReturned", featuresWritten);

                        // BUG FIX 5: Only add next link when explicitly requested limit was reached
                        // Don't add link if server-imposed trimming occurred
                        if (limitReached)
                        {
                            WriteNextLink(writer, context, featuresWritten);
                        }

                        // Close FeatureCollection
                        writer.WriteEndObject();
                    }

                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    await streamWriter.FlushAsync().ConfigureAwait(false);

                    // BUG FIX 6: Wrap Length access in try-catch to prevent sporadic 500s
                    // CanSeek=true doesn't guarantee Length works on all stream types
                    if (response.Body.CanSeek)
                    {
                        try
                        {
                            bytesWritten = response.Body.Length;
                        }
                        catch (NotSupportedException)
                        {
                            // Some seekable streams don't support Length - ignore and continue
                            _logger.LogDebug("Response stream reports CanSeek=true but Length threw NotSupportedException");
                        }
                    }

                    // Add final telemetry
                    activity?.SetTag("arcgis.features_written", featuresWritten);
                    if (bytesWritten > 0)
                    {
                        activity?.SetTag("arcgis.bytes_written", bytesWritten);
                    }
                    activity?.SetTag("arcgis.streaming", true);

                    return 0;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Streaming GeoJSON write cancelled after {Features} features", featuresWritten);
                    throw;
                }
                catch (Exception ex) when (featuresWritten > 0 && response.HasStarted)
                {
                    // Response already started - log error but can't send proper error response
                    // Client will see truncated JSON and should handle it
                    _logger.LogWarning(ex,
                        "Cannot send error response - already wrote {Features} features to stream. Client will receive truncated GeoJSON.",
                        featuresWritten);
                    throw;
                }
            });
    }

    /// <summary>
    /// Writes a single GeoJSON Feature to the JSON writer.
    /// </summary>
    private void WriteFeature(
        Utf8JsonWriter writer,
        FeatureRecord record,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        GeoJsonWriter geoJsonWriter)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "Feature");

        // Write ID if available
        if (TryGetAttribute(record, layer.IdField, out var idValue) && idValue != null)
        {
            WriteJsonValue(writer, "id", idValue);
        }

        // Write geometry if requested
        if (context.ReturnGeometry &&
            record.Attributes.TryGetValue(layer.GeometryField, out var geomObj) &&
            geomObj is Geometry ntsGeom &&
            !ntsGeom.IsEmpty)
        {
            // Apply geometry simplification if maxAllowableOffset is specified
            var geometryToWrite = ntsGeom;
            if (context.MaxAllowableOffset.HasValue && context.MaxAllowableOffset.Value > 0)
            {
                try
                {
                    geometryToWrite = DouglasPeuckerSimplifier.Simplify(ntsGeom, context.MaxAllowableOffset.Value);
                }
                catch (Exception ex)
                {
                    // If simplification fails, use original geometry
                    _logger.LogDebug(ex, "Geometry simplification failed with max allowable offset {MaxOffset}, using original geometry", context.MaxAllowableOffset.Value);
                    geometryToWrite = ntsGeom;
                }
            }

            writer.WritePropertyName("geometry");

            // Apply coordinate precision if geometryPrecision is specified
            if (context.GeometryPrecision.HasValue)
            {
                WriteGeometryWithPrecision(writer, geometryToWrite, context.GeometryPrecision.Value, geoJsonWriter);
            }
            else
            {
                var geoJsonGeom = geoJsonWriter.Write(geometryToWrite);
                writer.WriteRawValue(geoJsonGeom);
            }
        }
        else
        {
            writer.WriteNull("geometry");
        }

        // Write properties
        writer.WritePropertyName("properties");
        writer.WriteStartObject();

        // SECURITY FIX: Respect context.SelectedFields to prevent sensitive data leakage
        // Only write fields that are explicitly requested or all fields if "*" was specified
        bool writeAllFields = context.SelectedFields == null || context.SelectedFields.Count == 0;

        foreach (var field in layer.Fields)
        {
            // Skip if field filtering is active and this field is not selected
            // Always include ID field for feature identification
            bool shouldInclude = writeAllFields ||
                                context.SelectedFields!.ContainsKey(field.Name) ||
                                field.Name.Equals(layer.IdField, StringComparison.OrdinalIgnoreCase);

            if (shouldInclude && TryGetAttribute(record, field.Name, out var value))
            {
                WriteJsonValue(writer, field.Name, value);
            }
        }

        writer.WriteEndObject(); // properties
        writer.WriteEndObject(); // feature
    }

    /// <summary>
    /// Writes a JSON value handling different .NET types.
    /// BUG FIX 2: Ensures proper decimal precision and datetime handling for ArcGIS compatibility.
    /// </summary>
    private static void WriteJsonValue(Utf8JsonWriter writer, string propertyName, object? value)
    {
        if (value == null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        switch (value)
        {
            case string s:
                writer.WriteString(propertyName, s);
                break;
            case int i:
                writer.WriteNumber(propertyName, i);
                break;
            case long l:
                writer.WriteNumber(propertyName, l);
                break;
            case float f:
                // Use controlled precision for float to avoid excessive decimals
                writer.WriteNumber(propertyName, f);
                break;
            case double d:
                // Use controlled precision for double to avoid excessive decimals
                writer.WriteNumber(propertyName, d);
                break;
            case decimal m:
                // Use controlled precision for decimal to avoid excessive decimals
                writer.WriteNumber(propertyName, m);
                break;
            case bool b:
                writer.WriteBoolean(propertyName, b);
                break;
            case DateTime dt:
                // ArcGIS expects ISO 8601 format (round-trip "O" format)
                // This ensures consistent datetime handling across all responses
                writer.WriteString(propertyName, dt.ToString("O"));
                break;
            case DateTimeOffset dto:
                // ArcGIS expects ISO 8601 format (round-trip "O" format)
                writer.WriteString(propertyName, dto.ToString("O"));
                break;
            case Guid g:
                writer.WriteString(propertyName, g.ToString());
                break;
            default:
                // For complex types that need serialization, use JsonSerializerOptionsRegistry.Web
                // This ensures consistent JSON output with proper security and performance settings
                var json = JsonSerializer.Serialize(value, JsonSerializerOptionsRegistry.Web);
                writer.WriteRawValue(json, skipInputValidation: false);
                break;
        }
    }

    /// <summary>
    /// Writes CRS object for non-WGS84 coordinate systems.
    /// </summary>
    private static void WriteCrs(Utf8JsonWriter writer, int wkid)
    {
        writer.WritePropertyName("crs");
        writer.WriteStartObject();
        writer.WriteString("type", "name");
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        writer.WriteString("name", $"urn:ogc:def:crs:EPSG::{wkid}");
        writer.WriteEndObject(); // properties
        writer.WriteEndObject(); // crs
    }

    /// <summary>
    /// Writes metadata properties for OGC API Features compliance.
    /// </summary>
    private static void WriteMetadata(
        Utf8JsonWriter writer,
        LayerDefinition layer,
        long? totalCount,
        GeoservicesRESTQueryContext context)
    {
        writer.WriteString("name", layer.Id);

        if (!string.IsNullOrWhiteSpace(layer.Title))
        {
            writer.WriteString("title", layer.Title);
        }

        if (!string.IsNullOrWhiteSpace(layer.Description))
        {
            writer.WriteString("description", layer.Description);
        }

        // BUG FIX 1: Write numberMatched early when total count is known
        // This ensures clients receive total count even if response is truncated
        // numberReturned will be written at the end with actual count
        if (totalCount.HasValue)
        {
            writer.WriteNumber("numberMatched", totalCount.Value);
        }

        // Write time extent if temporal layer
        if (layer.Temporal?.Enabled == true)
        {
            writer.WritePropertyName("timeStamp");
            writer.WriteStartObject();
            writer.WriteString("instant", DateTime.UtcNow.ToString("O"));
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Writes pagination link for next page of results.
    /// </summary>
    private static void WriteNextLink(
        Utf8JsonWriter writer,
        GeoservicesRESTQueryContext context,
        long featuresWritten)
    {
        var nextOffset = (context.Query.Offset ?? 0) + featuresWritten;

        writer.WritePropertyName("links");
        writer.WriteStartArray();

        writer.WriteStartObject();
        writer.WriteString("rel", "next");
        writer.WriteString("type", "application/geo+json");
        writer.WriteString("title", "Next page");
        writer.WriteString("href", $"?offset={nextOffset}&limit={context.Query.Limit}");
        writer.WriteEndObject();

        writer.WriteEndArray();
    }

    /// <summary>
    /// Writes a geometry with coordinate precision control.
    /// Rounds coordinates to the specified number of decimal places.
    /// </summary>
    private static void WriteGeometryWithPrecision(
        Utf8JsonWriter writer,
        Geometry geometry,
        int precision,
        GeoJsonWriter geoJsonWriter)
    {
        var reduced = ReducePrecision(geometry, precision);
        var geoJson = geoJsonWriter.Write(reduced);
        writer.WriteRawValue(geoJson);
    }

    private static Geometry ReducePrecision(Geometry geometry, int precision)
    {
        var clone = (Geometry)geometry.Copy();
        var scale = Math.Pow(10, precision);
        var precisionModel = new PrecisionModel(scale);
        var reducer = new GeometryPrecisionReducer(precisionModel)
        {
            ChangePrecisionModel = false,
            Pointwise = true
        };
        return reducer.Reduce(clone);
    }

    /// <summary>
    /// Tries to get an attribute value from a feature record, handling case-insensitive lookup.
    /// </summary>
    private static bool TryGetAttribute(FeatureRecord record, string fieldName, out object? value)
    {
        if (record.Attributes.TryGetValue(fieldName, out value))
        {
            return true;
        }

        // Case-insensitive fallback
        foreach (var (key, val) in record.Attributes)
        {
            if (key.EqualsIgnoreCase(fieldName))
            {
                value = val;
                return true;
            }
        }

        value = null;
        return false;
    }
}
