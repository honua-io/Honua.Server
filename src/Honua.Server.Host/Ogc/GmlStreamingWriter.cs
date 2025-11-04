// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Wfs;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.GML3;

#nullable enable

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Streaming writer for GML (Geography Markup Language) 3.2.1 format.
/// Extends StreamingFeatureCollectionWriterBase to provide efficient streaming of GML FeatureCollections.
///
/// <para>
/// GML 3.2.1 is an OGC standard for encoding geographic information in XML format.
/// This implementation follows the WFS 2.0 specification and uses proper OGC namespaces.
/// </para>
///
/// <para>
/// Key features:
/// - Streaming XML generation with constant memory usage
/// - GML 3.2.1 compliant geometry encoding using NetTopologySuite.IO.GML3
/// - Proper namespace declarations (wfs, gml, ows, xsi)
/// - Support for coordinate reference system transformations
/// - Compliant with OGC WFS 2.0 and ISO 19142 standards
/// </para>
/// </summary>
public sealed class GmlStreamingWriter : StreamingFeatureCollectionWriterBase
{
    private const string GmlVersion = "3.2.1";

    // XML namespaces per OGC WFS 2.0 and GML 3.2.1 specifications
    private static readonly XNamespace _wfsNs = WfsConstants.Wfs;
    private static readonly XNamespace _gmlNs = WfsConstants.Gml;
    private static readonly XNamespace _owsNs = WfsConstants.Ows;
    private static readonly XNamespace _xsiNs = WfsConstants.Xsi;
    private Envelope? _boundingEnvelope;
    private string? _lockId;
    private long? _expectedFeatureCount;

    /// <summary>
    /// Content type for GML 3.2.1 responses per OGC WFS 2.0 specification.
    /// </summary>
    protected override string ContentType => WfsConstants.GmlFormat;

    /// <summary>
    /// Format name for logging and telemetry.
    /// </summary>
    protected override string FormatName => "GML";

    public GmlStreamingWriter(ILogger<GmlStreamingWriter> logger) : base(logger)
    {
    }

    /// <summary>
    /// Writes the GML FeatureCollection opening element with proper namespace declarations.
    /// Includes metadata such as timestamp, numberMatched, numberReturned, and optional bounding box.
    /// </summary>
    protected override async Task WriteHeaderAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(outputStream);
        Guard.NotNull(layer);
        Guard.NotNull(context);

        _lockId = context.GetOption<string>("lockId");
        _expectedFeatureCount = context.ExpectedFeatureCount;
        _boundingEnvelope = null;

        var serviceId = context.ServiceId ?? "default";
        var featureNamespace = XNamespace.Get($"https://honua.dev/wfs/{serviceId}");

        // Build opening FeatureCollection element with namespace declarations
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        builder.Append("<wfs:FeatureCollection");

        // Namespace declarations
        builder.Append($" xmlns:wfs=\"{_wfsNs}\"");
        builder.Append($" xmlns:gml=\"{_gmlNs}\"");
        builder.Append($" xmlns:ows=\"{_owsNs}\"");
        builder.Append($" xmlns:xsi=\"{_xsiNs}\"");
        builder.Append($" xmlns:tns=\"{featureNamespace}\"");

        // Metadata attributes
        builder.Append($" timeStamp=\"{DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture)}\"");

        if (context.TotalCount.HasValue)
        {
            builder.Append($" numberMatched=\"{context.TotalCount.Value}\"");
        }
        else
        {
            builder.Append(" numberMatched=\"unknown\"");
        }

        var numberReturned = _expectedFeatureCount ?? 0;
        builder.Append($" numberReturned=\"{numberReturned}\"");

        if (_lockId.HasValue())
        {
            builder.Append($" lockId=\"{_lockId}\"");
        }
        builder.Append(">");

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        await outputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// GML uses wfs:member elements to wrap features, no separator needed between members.
    /// </summary>
    protected override Task WriteFeatureSeparatorAsync(
        Stream outputStream,
        bool isFirst,
        CancellationToken cancellationToken)
    {
        // No separator needed - each feature is wrapped in its own wfs:member element
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes a single feature as a GML member element.
    /// Each feature is wrapped in a wfs:member element containing the feature properties and geometry.
    /// Geometry is encoded using GML 3.2.1 format via NetTopologySuite.IO.GML3.
    /// </summary>
    protected override async Task WriteFeatureAsync(
        Stream outputStream,
        FeatureRecord feature,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(outputStream);
        Guard.NotNull(feature);
        Guard.NotNull(layer);
        Guard.NotNull(context);

        var serviceId = context.ServiceId ?? "default";
        var featureNamespace = XNamespace.Get($"https://honua.dev/wfs/{serviceId}");
        var srsName = BuildSrsName(context.TargetWkid);

        // Extract feature ID for gml:id attribute
        var featureId = ExtractFeatureId(layer, feature);

        // Start wfs:member element
        var builder = new StringBuilder();
        builder.Append("<wfs:member>");

        // Feature element with gml:id
        builder.Append($"<tns:{XmlConvert.EncodeLocalName(layer.Id)}");
        builder.Append($" gml:id=\"{XmlConvert.EncodeLocalName(featureId)}\">");

        // Write geometry if requested and available
        if (context.ReturnGeometry &&
            feature.Attributes.TryGetValue(layer.GeometryField, out var geomObj) &&
            geomObj is Geometry geometry &&
            !geometry.IsEmpty)
        {
            // Apply SRID if specified
            if (context.TargetWkid != 0 && geometry.SRID != context.TargetWkid)
            {
                geometry = (Geometry)geometry.Copy();
                geometry.SRID = context.TargetWkid;
            }

            UpdateEnvelope(geometry);

            var geometryXml = WriteGeometryAsGml(geometry, srsName);
            if (geometryXml.HasValue())
            {
                builder.Append($"<tns:{XmlConvert.EncodeLocalName(layer.GeometryField)}>");
                builder.Append(geometryXml);
                builder.Append($"</tns:{XmlConvert.EncodeLocalName(layer.GeometryField)}>");
            }
        }

        // Write attribute properties
        foreach (var field in layer.Fields)
        {
            if (field.Name.EqualsIgnoreCase(layer.GeometryField))
            {
                continue; // Skip geometry field - already written above
            }

            if (feature.Attributes.TryGetValue(field.Name, out var value) && value is not null)
            {
                var propertyValue = ConvertToPropertyValue(value);
                if (propertyValue.HasValue())
                {
                    var encodedName = XmlConvert.EncodeLocalName(field.Name);
                    var encodedValue = System.Security.SecurityElement.Escape(propertyValue);
                    builder.Append($"<tns:{encodedName}>{encodedValue}</tns:{encodedName}>");
                }
            }
        }

        // Close feature element
        builder.Append($"</tns:{XmlConvert.EncodeLocalName(layer.Id)}>");
        builder.Append("</wfs:member>");

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        await outputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the GML FeatureCollection closing tag.
    /// Note: The numberReturned attribute is set in the header and cannot be updated in streaming mode.
    /// For accurate counts, the total should be known before streaming begins.
    /// </summary>
    protected override async Task WriteFooterAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        long featuresWritten,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(outputStream);

        var builder = new StringBuilder();

        if (_boundingEnvelope is { IsNull: false })
        {
            builder.Append("<gml:boundedBy>");
            builder.Append("<gml:Envelope");
            builder.Append($" srsName=\"{BuildSrsName(context.TargetWkid)}\"");
            builder.Append('>');
            builder.AppendFormat(CultureInfo.InvariantCulture, "<gml:lowerCorner>{0} {1}</gml:lowerCorner>",
                WfsHelpers.FormatCoordinate(_boundingEnvelope.MinX),
                WfsHelpers.FormatCoordinate(_boundingEnvelope.MinY));
            builder.AppendFormat(CultureInfo.InvariantCulture, "<gml:upperCorner>{0} {1}</gml:upperCorner>",
                WfsHelpers.FormatCoordinate(_boundingEnvelope.MaxX),
                WfsHelpers.FormatCoordinate(_boundingEnvelope.MaxY));
            builder.Append("</gml:Envelope>");
            builder.Append("</gml:boundedBy>");
        }

        builder.Append("</wfs:FeatureCollection>");

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        await outputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a geometry as GML 3.2.1 XML using NetTopologySuite.IO.GML3.
    /// </summary>
    private static string WriteGeometryAsGml(Geometry geometry, string srsName)
    {
        if (geometry.IsEmpty)
        {
            return string.Empty;
        }

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            Encoding = Encoding.UTF8
        };

        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, settings))
        {
            // THREAD-SAFETY FIX: Create per-request GML3Writer instead of static shared instance
            var gml3Writer = new GML3Writer();
            gml3Writer.Write(geometry, writer);
        }

        var gmlXml = builder.ToString();

        // Add srsName attribute if specified
        if (srsName.HasValue() && gmlXml.Contains("<gml:"))
        {
            // Find the first GML geometry element and add srsName attribute
            var firstGmlTag = gmlXml.IndexOf("<gml:", StringComparison.Ordinal);
            if (firstGmlTag >= 0)
            {
                var tagEnd = gmlXml.IndexOf('>', firstGmlTag);
                if (tagEnd >= 0)
                {
                    // Insert srsName before closing >
                    gmlXml = gmlXml.Insert(tagEnd, $" srsName=\"{srsName}\"");
                }
            }
        }

        return gmlXml;
    }

    /// <summary>
    /// Extracts feature identifier from a feature record for use as gml:id.
    /// Falls back to a generated ID if no ID field is available.
    /// </summary>
    private static string ExtractFeatureId(LayerDefinition layer, FeatureRecord feature)
    {
        if (layer.IdField.HasValue() &&
            feature.Attributes.TryGetValue(layer.IdField, out var idValue) &&
            idValue is not null)
        {
            var id = Convert.ToString(idValue, CultureInfo.InvariantCulture);
            if (id.HasValue())
            {
                // gml:id must be a valid XML ID (start with letter or underscore)
                if (char.IsDigit(id[0]))
                {
                    return $"{layer.Id}.{id}";
                }
                return id;
            }
        }

        // Fallback to generated ID
        return $"{layer.Id}.{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Builds an SRS name in URN format (e.g., "urn:ogc:def:crs:EPSG::4326").
    /// </summary>
    private static string BuildSrsName(int wkid)
    {
        if (wkid <= 0)
        {
            return "urn:ogc:def:crs:EPSG::4326"; // Default to WGS84
        }

        return $"urn:ogc:def:crs:EPSG::{wkid}";
    }

    /// <summary>
    /// Converts a value to a string suitable for GML property elements.
    /// Handles different .NET types and ensures culture-invariant formatting.
    /// </summary>
    private static string? ConvertToPropertyValue(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            bool b => b ? "true" : "false",
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private void UpdateEnvelope(Geometry geometry)
    {
        if (geometry.IsEmpty)
        {
            return;
        }

        var geometryEnvelope = geometry.EnvelopeInternal;
        if (geometryEnvelope is null || geometryEnvelope.IsNull)
        {
            return;
        }

        (_boundingEnvelope ??= new Envelope()).ExpandToInclude(geometryEnvelope);
    }
}
