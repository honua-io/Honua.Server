// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Stac.Services;

/// <summary>
/// Service for streaming STAC search results as GeoJSON FeatureCollections.
/// Writes results incrementally to maintain constant memory usage.
/// </summary>
public sealed class StacStreamingService
{
    private readonly ILogger<StacStreamingService> logger;

    public StacStreamingService(ILogger<StacStreamingService> logger)
    {
        this.logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Streams a STAC search result as a GeoJSON FeatureCollection.
    /// Writes the response incrementally without loading all items into memory.
    /// </summary>
    /// <param name="stream">The output stream to write to.</param>
    /// <param name="items">The async enumerable of STAC items to stream.</param>
    /// <param name="baseUri">The base URI for building links.</param>
    /// <param name="fieldsSpec">Optional field filtering specification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StreamSearchResultsAsync(
        Stream stream,
        IAsyncEnumerable<StacItemRecord> items,
        Uri baseUri,
        FieldsSpecification? fieldsSpec,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(stream);
        Guard.NotNull(items);
        Guard.NotNull(baseUri);

        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false // No indentation for streaming to reduce size
        });

        // Start the FeatureCollection
        writer.WriteStartObject();
        writer.WriteString("type", "FeatureCollection");
        writer.WriteString("stac_version", "1.0.0");

        // Write links
        writer.WritePropertyName("links");
        writer.WriteStartArray();

        WriteLink(writer, "self", CombineUri(baseUri, "/stac/search"), "application/geo+json", "Search");
        WriteLink(writer, "root", CombineUri(baseUri, "/stac"), "application/json", "Root");

        writer.WriteEndArray();

        // Start features array
        writer.WritePropertyName("features");
        writer.WriteStartArray();

        var itemCount = 0;
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            // Write each feature as a JSON object
            WriteStacItem(writer, item, baseUri, fieldsSpec);
            itemCount++;

            // Flush periodically to ensure streaming
            if (itemCount % 10 == 0)
            {
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // End features array
        writer.WriteEndArray();

        // Write context - note: matched count is unknown for streaming
        writer.WritePropertyName("context");
        writer.WriteStartObject();
        writer.WriteNumber("returned", itemCount);
        writer.WriteNumber("limit", 0); // No limit for streaming
        writer.WriteNumber("matched", -1); // Unknown for streaming
        writer.WriteEndObject();

        // End the FeatureCollection
        writer.WriteEndObject();

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        this.logger.LogInformation("Streamed {ItemCount} STAC items", itemCount);
    }

    private static void WriteStacItem(Utf8JsonWriter writer, StacItemRecord item, Uri baseUri, FieldsSpecification? fieldsSpec)
    {
        writer.WriteStartObject();

        writer.WriteString("type", "Feature");
        writer.WriteString("stac_version", "1.0.0");
        writer.WriteString("id", item.Id);
        writer.WriteString("collection", item.CollectionId);

        // Write geometry
        if (!string.IsNullOrWhiteSpace(item.Geometry))
        {
            writer.WritePropertyName("geometry");
            using var geometryDoc = JsonDocument.Parse(item.Geometry);
            geometryDoc.WriteTo(writer);
        }
        else
        {
            writer.WriteNull("geometry");
        }

        // Write bbox if present
        if (item.Bbox != null && item.Bbox.Length >= 4)
        {
            writer.WritePropertyName("bbox");
            writer.WriteStartArray();
            foreach (var coord in item.Bbox)
            {
                writer.WriteNumberValue(coord);
            }
            writer.WriteEndArray();
        }

        // Write properties
        writer.WritePropertyName("properties");
        writer.WriteStartObject();

        // Add datetime properties from record fields
        // Per STAC spec, items must have either:
        // 1. A datetime field with a valid datetime value, OR
        // 2. Both start_datetime and end_datetime fields with datetime explicitly set to null
        if (item.Datetime.HasValue)
        {
            writer.WriteString("datetime", item.Datetime.Value.ToString("O"));
        }
        else if (item.StartDatetime.HasValue && item.EndDatetime.HasValue)
        {
            writer.WriteNull("datetime");
            writer.WriteString("start_datetime", item.StartDatetime.Value.ToString("O"));
            writer.WriteString("end_datetime", item.EndDatetime.Value.ToString("O"));
        }
        else if (item.StartDatetime.HasValue || item.EndDatetime.HasValue)
        {
            // If only one of start/end is present, use it as datetime
            var fallbackDatetime = item.StartDatetime ?? item.EndDatetime;
            writer.WriteString("datetime", fallbackDatetime!.Value.ToString("O"));
        }
        else
        {
            // No temporal information available - provide a default datetime
            // Use Unix epoch as a fallback to indicate unknown acquisition time
            writer.WriteString("datetime", new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).ToString("O"));
        }

        // Write additional properties from the JSON object
        if (item.Properties != null)
        {
            foreach (var prop in item.Properties)
            {
                // Always skip datetime temporal fields - they're handled above from record fields
                if (prop.Key == "datetime") continue;
                if (prop.Key == "start_datetime") continue;
                if (prop.Key == "end_datetime") continue;

                writer.WritePropertyName(prop.Key);
                prop.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject(); // properties

        // Write assets
        writer.WritePropertyName("assets");
        writer.WriteStartObject();
        foreach (var (key, asset) in item.Assets)
        {
            writer.WritePropertyName(key);
            writer.WriteStartObject();

            writer.WriteString("href", asset.Href);

            if (!string.IsNullOrWhiteSpace(asset.Title))
            {
                writer.WriteString("title", asset.Title);
            }

            if (!string.IsNullOrWhiteSpace(asset.Description))
            {
                writer.WriteString("description", asset.Description);
            }

            if (!string.IsNullOrWhiteSpace(asset.Type))
            {
                writer.WriteString("type", asset.Type);
            }

            if (asset.Roles.Count > 0)
            {
                writer.WritePropertyName("roles");
                writer.WriteStartArray();
                foreach (var role in asset.Roles)
                {
                    writer.WriteStringValue(role);
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
        writer.WriteEndObject(); // assets

        // Write links
        writer.WritePropertyName("links");
        writer.WriteStartArray();

        WriteLink(writer, "self",
            CombineUri(baseUri, $"/stac/collections/{Uri.EscapeDataString(item.CollectionId)}/items/{Uri.EscapeDataString(item.Id)}"),
            "application/geo+json", item.Title ?? item.Id);

        WriteLink(writer, "collection",
            CombineUri(baseUri, $"/stac/collections/{Uri.EscapeDataString(item.CollectionId)}"),
            "application/json", item.CollectionId);

        WriteLink(writer, "root",
            CombineUri(baseUri, "/stac"),
            "application/json", "Root");

        // Add any additional links from the item
        foreach (var link in item.Links)
        {
            WriteLink(writer, link.Rel, link.Href, link.Type, link.Title);
        }

        writer.WriteEndArray(); // links

        // Write extensions if present
        if (item.Extensions.Count > 0)
        {
            writer.WritePropertyName("stac_extensions");
            writer.WriteStartArray();
            foreach (var ext in item.Extensions)
            {
                writer.WriteStringValue(ext);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject(); // feature
    }

    private static void WriteLink(Utf8JsonWriter writer, string rel, string href, string? type, string? title)
    {
        writer.WriteStartObject();
        writer.WriteString("rel", rel);
        writer.WriteString("href", href);

        if (!string.IsNullOrWhiteSpace(type))
        {
            writer.WriteString("type", type);
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            writer.WriteString("title", title);
        }

        writer.WriteEndObject();
    }

    private static string CombineUri(Uri baseUri, string path)
    {
        if (Uri.TryCreate(baseUri, path, out var combined))
        {
            return combined.ToString();
        }

        return baseUri.ToString() + path;
    }
}
