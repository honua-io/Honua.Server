// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using SharpKml.Base;
using SharpKml.Dom;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Provides streaming KML/KMZ responses with constant memory usage.
/// Writes KML incrementally without buffering entire payload or feature collections.
/// </summary>
public sealed class StreamingKmlWriter
{
    private readonly ILogger<StreamingKmlWriter> logger;
    private const int MaxExportRecords = 10000; // Strict limit for KML exports
    private const string KmlNamespace = "http://www.opengis.net/kml/2.2";

    public StreamingKmlWriter(ILogger<StreamingKmlWriter> logger)
    {
        this.logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Writes a KML document to the HTTP response stream with constant memory usage.
    /// </summary>
    /// <param name="response">HTTP response to write to</param>
    /// <param name="repository">Feature repository for data access</param>
    /// <param name="serviceId">Service identifier</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="context">Query context with filters and pagination</param>
    /// <param name="collectionId">Collection identifier for metadata</param>
    /// <param name="style">Optional style definition to apply to placemarks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task WriteKmlAsync(
        HttpResponse response,
        IFeatureRepository repository,
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        string collectionId,
        StyleDefinition? style,
        CancellationToken cancellationToken)
    {
        long featuresWritten = 0;
        long bytesWritten = 0;

        await OperationInstrumentation.Create<int>("Streaming KML Write")
            .WithActivitySource(HonuaTelemetry.OgcProtocols)
            .WithLogger(this.logger)
            .WithTag("arcgis.service_id", serviceId)
            .WithTag("arcgis.layer_id", layer.Id)
            .WithTag("arcgis.format", "kml")
            .WithLogLevels(LogLevel.Information, LogLevel.Error)
            .ExecuteAsync(async activity =>
            {
                try
                {
                    // Disable buffering to enable true streaming
                    var bufferingFeature = response.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
                    bufferingFeature?.DisableBuffering();

                    // Apply strict export limit
                    var limitedQuery = context.Query with
                    {
                        Limit = Math.Min(context.Query.Limit ?? MaxExportRecords, MaxExportRecords),
                        ResultType = FeatureResultType.Results
                    };

                    // Use XmlWriter for high-performance streaming
                    var settings = new XmlWriterSettings
                    {
                        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        Indent = context.PrettyPrint,
                        IndentChars = "  ",
                        Async = true,
                        CloseOutput = false // Let ASP.NET Core manage the stream
                    };

                    await using var writer = XmlWriter.Create(response.Body, settings);

                    // Write KML document header
                    await writer.WriteStartDocumentAsync().ConfigureAwait(false);
                    await writer.WriteStartElementAsync(null, "kml", KmlNamespace).ConfigureAwait(false);
                    await writer.WriteStartElementAsync(null, "Document", null).ConfigureAwait(false);

                    // Write document metadata
                    await writer.WriteElementStringAsync(null, "name", null,
                        layer.Title.IsNullOrWhiteSpace() ? collectionId : layer.Title).ConfigureAwait(false);

                    if (layer.Description.HasValue())
                    {
                        await writer.WriteStartElementAsync(null, "description", null).ConfigureAwait(false);
                        await writer.WriteCDataAsync(layer.Description).ConfigureAwait(false);
                        await writer.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    await writer.WriteElementStringAsync(null, "open", null, "1").ConfigureAwait(false);

                    // Resolve style (if provided)
                    string? styleUrl = null;
                    if (style is not null)
                    {
                        var resolvedStyleId = BuildStyleId(style.Id);
                        var kmlStyle = StyleFormatConverter.CreateKmlStyle(style, resolvedStyleId, layer.GeometryType ?? style.GeometryType);
                        if (kmlStyle is not null)
                        {
                            styleUrl = "#" + kmlStyle.Id;
                            await WriteStyleAsync(writer, kmlStyle).ConfigureAwait(false);
                        }
                    }

                    // Write extended data with collection metadata
                    await writer.WriteStartElementAsync(null, "ExtendedData", null).ConfigureAwait(false);
                    await WriteDataElementAsync(writer, "collectionId", collectionId).ConfigureAwait(false);
                    await WriteDataElementAsync(writer, "recordLimit", MaxExportRecords.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                    await writer.WriteEndElementAsync().ConfigureAwait(false); // ExtendedData

                    // Stream features one at a time
                    await foreach (var record in repository.QueryAsync(serviceId, layer.Id, limitedQuery, cancellationToken).ConfigureAwait(false))
                    {
                        // Enforce hard limit
                        if (featuresWritten >= MaxExportRecords)
                        {
                            this.logger.LogWarning("KML export hit maximum record limit of {Limit} for {ServiceId}/{LayerId}",
                                MaxExportRecords, serviceId, layer.Id);
                            break;
                        }

                        var content = FeatureComponentBuilder.CreateKmlContent(layer, record, limitedQuery);
                        await WritePlacemarkAsync(writer, layer, content, styleUrl).ConfigureAwait(false);
                        featuresWritten++;

                        // Flush periodically to send data to client
                        if (featuresWritten % 100 == 0)
                        {
                            await writer.FlushAsync().ConfigureAwait(false);

                            // Allow cancellation and backpressure
                            await Task.Yield();
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }

                    // Close Document and KML
                    await writer.WriteEndElementAsync().ConfigureAwait(false); // Document
                    await writer.WriteEndElementAsync().ConfigureAwait(false); // kml
                    await writer.WriteEndDocumentAsync().ConfigureAwait(false);

                    await writer.FlushAsync().ConfigureAwait(false);

                    // STREAM-SAFETY: Only query Length on seekable streams
                    if (response.Body.CanSeek)
                    {
                        bytesWritten = response.Body.Length;
                    }

                    // Add final telemetry
                    activity?.SetTag("arcgis.features_written", featuresWritten);
                    if (bytesWritten > 0)
                    {
                        activity?.SetTag("arcgis.bytes_written", bytesWritten);
                    }
                    activity?.SetTag("arcgis.streaming", true);
                    activity?.SetTag("arcgis.max_records", MaxExportRecords);

                    return 0;
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogInformation("Streaming KML write cancelled after {Features} features", featuresWritten);
                    throw;
                }
                catch (Exception ex) when (featuresWritten > 0 && response.HasStarted)
                {
                    // Response already started - log error but can't send proper error response
                    this.logger.LogWarning(ex,
                        "Cannot send error response - already wrote {Features} features to stream. Client will receive truncated KML.",
                        featuresWritten);
                    throw;
                }
            });
    }

    /// <summary>
    /// Writes a KMZ archive to the HTTP response stream with constant memory usage.
    /// </summary>
    /// <param name="response">HTTP response to write to</param>
    /// <param name="repository">Feature repository for data access</param>
    /// <param name="serviceId">Service identifier</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="context">Query context with filters and pagination</param>
    /// <param name="collectionId">Collection identifier for metadata</param>
    /// <param name="style">Optional style definition to apply to placemarks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task WriteKmzAsync(
        HttpResponse response,
        IFeatureRepository repository,
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        string collectionId,
        StyleDefinition? style,
        CancellationToken cancellationToken)
    {
        long featuresWritten = 0;

        await OperationInstrumentation.Create<int>("Streaming KMZ Write")
            .WithActivitySource(HonuaTelemetry.OgcProtocols)
            .WithLogger(this.logger)
            .WithTag("arcgis.service_id", serviceId)
            .WithTag("arcgis.layer_id", layer.Id)
            .WithTag("arcgis.format", "kmz")
            .WithLogLevels(LogLevel.Information, LogLevel.Error)
            .ExecuteAsync(async activity =>
            {
                try
                {
                    // Set response headers for streaming
                    // Disable buffering to enable true streaming
                    var bufferingFeature = response.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
                    bufferingFeature?.DisableBuffering();

                    // Apply strict export limit
                    var limitedQuery = context.Query with
                    {
                        Limit = Math.Min(context.Query.Limit ?? MaxExportRecords, MaxExportRecords),
                        ResultType = FeatureResultType.Results
                    };

                    // Create ZIP archive directly to response stream
                    await using var responseStream = response.BodyWriter.AsStream(leaveOpen: true);
                    using var archive = new ZipArchive(responseStream, ZipArchiveMode.Create, leaveOpen: true);

                    // Create KML entry in the archive
                    var entryName = FileNameHelper.BuildArchiveEntryName(collectionId, null);
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);

                    await using var entryStream = entry.Open();

                    // Use XmlWriter to stream KML into ZIP entry
                    var settings = new XmlWriterSettings
                    {
                        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        Indent = context.PrettyPrint,
                        IndentChars = "  ",
                        Async = true,
                        CloseOutput = false
                    };

                    await using var writer = XmlWriter.Create(entryStream, settings);

                    // Write KML document header
                    await writer.WriteStartDocumentAsync().ConfigureAwait(false);
                    await writer.WriteStartElementAsync(null, "kml", KmlNamespace).ConfigureAwait(false);
                    await writer.WriteStartElementAsync(null, "Document", null).ConfigureAwait(false);

                    // Write document metadata
                    await writer.WriteElementStringAsync(null, "name", null,
                        layer.Title.IsNullOrWhiteSpace() ? collectionId : layer.Title).ConfigureAwait(false);

                    if (layer.Description.HasValue())
                    {
                        await writer.WriteStartElementAsync(null, "description", null).ConfigureAwait(false);
                        await writer.WriteCDataAsync(layer.Description).ConfigureAwait(false);
                        await writer.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    await writer.WriteElementStringAsync(null, "open", null, "1").ConfigureAwait(false);

                    string? styleUrl = null;
                    if (style is not null)
                    {
                        var resolvedStyleId = BuildStyleId(style.Id);
                        var kmlStyle = StyleFormatConverter.CreateKmlStyle(style, resolvedStyleId, layer.GeometryType ?? style.GeometryType);
                        if (kmlStyle is not null)
                        {
                            styleUrl = "#" + kmlStyle.Id;
                            await WriteStyleAsync(writer, kmlStyle).ConfigureAwait(false);
                        }
                    }

                    // Write extended data with collection metadata
                    await writer.WriteStartElementAsync(null, "ExtendedData", null).ConfigureAwait(false);
                    await WriteDataElementAsync(writer, "collectionId", collectionId).ConfigureAwait(false);
                    await WriteDataElementAsync(writer, "recordLimit", MaxExportRecords.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                    await writer.WriteEndElementAsync().ConfigureAwait(false); // ExtendedData

                    // Stream features one at a time
                    await foreach (var record in repository.QueryAsync(serviceId, layer.Id, limitedQuery, cancellationToken).ConfigureAwait(false))
                    {
                        // Enforce hard limit
                        if (featuresWritten >= MaxExportRecords)
                        {
                            this.logger.LogWarning("KMZ export hit maximum record limit of {Limit} for {ServiceId}/{LayerId}",
                                MaxExportRecords, serviceId, layer.Id);
                            break;
                        }

                        var content = FeatureComponentBuilder.CreateKmlContent(layer, record, limitedQuery);
                        await WritePlacemarkAsync(writer, layer, content, styleUrl).ConfigureAwait(false);
                        featuresWritten++;

                        // Flush periodically
                        if (featuresWritten % 100 == 0)
                        {
                            await writer.FlushAsync().ConfigureAwait(false);

                            // Allow cancellation and backpressure
                            await Task.Yield();
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }

                    // Close Document and KML
                    await writer.WriteEndElementAsync().ConfigureAwait(false); // Document
                    await writer.WriteEndElementAsync().ConfigureAwait(false); // kml
                    await writer.WriteEndDocumentAsync().ConfigureAwait(false);

                    await writer.FlushAsync().ConfigureAwait(false);
                    await responseStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                    // Add final telemetry
                    activity?.SetTag("arcgis.features_written", featuresWritten);
                    activity?.SetTag("arcgis.streaming", true);
                    activity?.SetTag("arcgis.max_records", MaxExportRecords);

                    return 0;
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogInformation("Streaming KMZ write cancelled after {Features} features", featuresWritten);
                    throw;
                }
                catch (Exception ex) when (featuresWritten > 0 && response.HasStarted)
                {
                    this.logger.LogWarning(ex,
                        "Cannot send error response - already wrote {Features} features to stream. Client will receive truncated KMZ.",
                        featuresWritten);
                    throw;
                }
            });
    }

    /// <summary>
    /// Writes a KML Placemark element for a single feature.
    /// </summary>
    private static async Task WritePlacemarkAsync(
        XmlWriter writer,
        LayerDefinition layer,
        KmlFeatureContent content,
        string? styleUrl)
    {
        await writer.WriteStartElementAsync(null, "Placemark", null).ConfigureAwait(false);

        // Write ID if available
        if (!string.IsNullOrWhiteSpace(content.Id))
        {
            var xmlId = BuildXmlId(content.Id);
            if (xmlId != null)
            {
                await writer.WriteAttributeStringAsync(null, "id", null, xmlId).ConfigureAwait(false);
            }
        }

        // Write name
        var name = content.Name.IsNullOrWhiteSpace() ? content.Id : content.Name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            await writer.WriteElementStringAsync(null, "name", null, name).ConfigureAwait(false);
        }

        if (styleUrl.HasValue())
        {
            await writer.WriteElementStringAsync(null, "styleUrl", null, styleUrl).ConfigureAwait(false);
        }

        // Write description if available
        if (layer.Description.HasValue())
        {
            await writer.WriteStartElementAsync(null, "description", null).ConfigureAwait(false);
            await writer.WriteCDataAsync(layer.Description).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        // Write geometry using SharpKml's existing conversion
        // OPTIMIZATION: We serialize to string then write raw XML to avoid building DOM
        if (content.Geometry != null)
        {
            try
            {
                // Use the existing KmlFeatureFormatter to convert geometry
                // This is not ideal but reuses tested code; future optimization would
                // be to port geometry conversion to XmlWriter directly
                var tempKml = KmlFeatureFormatter.WriteSingleFeature(
                    layer.Id ?? "temp",
                    layer,
                    content,
                    style: null);

                // Extract just the Geometry element from the generated KML
                await WriteGeometryFromKmlAsync(writer, tempKml).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log but continue - skip geometry for this feature
                // Don't fail entire export for one bad geometry
            }
        }

        // Write properties as ExtendedData
        if (content.Properties.Count > 0)
        {
            await writer.WriteStartElementAsync(null, "ExtendedData", null).ConfigureAwait(false);

            foreach (var pair in content.Properties)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                var value = FormatValue(pair.Value);
                if (value != null)
                {
                    await WriteDataElementAsync(writer, pair.Key, value).ConfigureAwait(false);
                }
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false); // ExtendedData
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false); // Placemark
    }

    private static async Task WriteStyleAsync(XmlWriter writer, Style style)
    {
        await writer.WriteStartElementAsync(null, "Style", null).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(style.Id))
        {
            await writer.WriteAttributeStringAsync(null, "id", null, style.Id).ConfigureAwait(false);
        }

        if (style.Icon is IconStyle icon)
        {
            await writer.WriteStartElementAsync(null, "IconStyle", null).ConfigureAwait(false);

            if (icon.Color.HasValue)
            {
                await writer.WriteElementStringAsync(null, "color", null, FormatKmlColor(icon.Color)).ConfigureAwait(false);
            }

            var scale = icon.Scale;
            if (scale.HasValue)
            {
                var scaleValue = scale.Value;
                if (!double.IsNaN(scaleValue) && Math.Abs(scaleValue - 1d) > double.Epsilon)
                {
                    await writer.WriteElementStringAsync(null, "scale", null, scaleValue.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                }
            }

            if (icon.Icon?.Href is Uri href)
            {
                await writer.WriteStartElementAsync(null, "Icon", null).ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "href", null, href.ToString()).ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        if (style.Line is LineStyle line)
        {
            await writer.WriteStartElementAsync(null, "LineStyle", null).ConfigureAwait(false);
            if (line.Color.HasValue)
            {
                await writer.WriteElementStringAsync(null, "color", null, FormatKmlColor(line.Color)).ConfigureAwait(false);
            }
            if (line.Width.HasValue)
            {
                await writer.WriteElementStringAsync(null, "width", null, line.Width.Value.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            }
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        if (style.Polygon is PolygonStyle polygon)
        {
            await writer.WriteStartElementAsync(null, "PolyStyle", null).ConfigureAwait(false);
            if (polygon.Color.HasValue)
            {
                await writer.WriteElementStringAsync(null, "color", null, FormatKmlColor(polygon.Color)).ConfigureAwait(false);
            }
            await writer.WriteElementStringAsync(null, "fill", null, FormatBoolean(polygon.Fill ?? true)).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "outline", null, FormatBoolean(polygon.Outline ?? true)).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false); // Style
    }

    private static string BuildStyleId(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId))
        {
            return "default-style";
        }

        var builder = new StringBuilder();
        foreach (var ch in rawId)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        }

        if (builder.Length == 0)
        {
            return "default-style";
        }

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, 's');
        }

        return builder.ToString();
    }

    private static string FormatKmlColor(Color32? color)
    {
        return color.HasValue ? color.Value.ToString() : "ffffffff";
    }

    private static string FormatBoolean(bool value) => value ? "1" : "0";

    /// <summary>
    /// Extracts and writes the Geometry element from a KML string.
    /// This is a temporary solution that reuses existing geometry conversion logic.
    /// </summary>
    private static async Task WriteGeometryFromKmlAsync(XmlWriter writer, string kml)
    {
        using var reader = new StringReader(kml);
        using var xmlReader = XmlReader.Create(reader, new XmlReaderSettings
        {
            Async = true,
            IgnoreWhitespace = true
        });

        // Find the Geometry element (Point, LineString, Polygon, or MultiGeometry)
        bool foundGeometry = false;
        while (await xmlReader.ReadAsync().ConfigureAwait(false))
        {
            if (xmlReader.NodeType == XmlNodeType.Element)
            {
                var localName = xmlReader.LocalName;
                if (localName is "Point" or "LineString" or "Polygon" or "MultiGeometry")
                {
                    // Write the entire geometry subtree
                    await writer.WriteNodeAsync(xmlReader, defattr: false).ConfigureAwait(false);
                    foundGeometry = true;
                    break;
                }
            }
        }

        if (!foundGeometry)
        {
            // No geometry found in the KML, which is acceptable
        }
    }

    /// <summary>
    /// Writes a KML Data element with name and value.
    /// </summary>
    private static async Task WriteDataElementAsync(XmlWriter writer, string name, string value)
    {
        await writer.WriteStartElementAsync(null, "Data", null).ConfigureAwait(false);
        await writer.WriteAttributeStringAsync(null, "name", null, name).ConfigureAwait(false);
        await writer.WriteElementStringAsync(null, "value", null, value).ConfigureAwait(false);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a valid XML ID from a string by sanitizing invalid characters.
    /// </summary>
    private static string? BuildXmlId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var sanitized = new StringBuilder();
        foreach (var ch in id)
        {
            sanitized.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        }

        if (sanitized.Length == 0)
        {
            return null;
        }

        if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized.Insert(0, 'f');
        }

        return sanitized.ToString();
    }

    /// <summary>
    /// Formats an object value as a string for KML output.
    /// </summary>
    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => null,
            string s when s.IsNullOrWhiteSpace() => null,
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            System.Text.Json.Nodes.JsonNode node => node.ToJsonString(),
            _ => value?.ToString()
        };
    }
}
