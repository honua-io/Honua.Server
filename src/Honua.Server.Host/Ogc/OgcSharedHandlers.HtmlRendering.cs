// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains HTML rendering methods for OGC API responses.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcSharedHandlers
{
    internal static bool WantsHtml(HttpRequest request)
    {
        if (request.Query.TryGetValue("f", out var formatValues))
        {
            var formatValue = formatValues.ToString();
            if (string.Equals(formatValue, "html", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (formatValue.HasValue())
            {
                return false;
            }
        }

        if (request.Headers.TryGetValue(HeaderNames.Accept, out var acceptValues) &&
            MediaTypeHeaderValue.TryParseList(acceptValues, out var parsedAccepts))
        {
            var ordered = parsedAccepts
                .OrderByDescending(value => value.Quality ?? 1.0)
                .ToList();

            foreach (var media in ordered)
            {
                var mediaType = media.MediaType.ToString();
                if (mediaType.IsNullOrWhiteSpace())
                {
                    continue;
                }

                if (string.Equals(mediaType, HtmlMediaType, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.Equals(mediaType, "*/*", StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return false;
    }

    internal static string RenderLandingHtml(HttpRequest request, MetadataSnapshot snapshot, IReadOnlyList<OgcLink> links)
    {
        return RenderHtmlDocument(snapshot.Catalog.Title ?? "OGC API", body =>
        {
            body.Append("<h1>").Append(HtmlEncode(snapshot.Catalog.Title ?? snapshot.Catalog.Id)).AppendLine("</h1>");
            if (snapshot.Catalog.Description.HasValue())
            {
                body.Append("<p>").Append(HtmlEncode(snapshot.Catalog.Description)).AppendLine("</p>");
            }

            body.Append("<p><strong>Catalog ID:</strong> ")
                .Append(HtmlEncode(snapshot.Catalog.Id))
                .AppendLine("</p>");

            AppendLinksHtml(body, links);

            if (snapshot.Services.Count > 0)
            {
                body.AppendLine("<h2>Services</h2>");
                body.AppendLine("<ul>");
                foreach (var service in snapshot.Services.OrderBy(s => s.Title ?? s.Id, StringComparer.OrdinalIgnoreCase))
                {
                    body.Append("  <li><strong>")
                        .Append(HtmlEncode(service.Title ?? service.Id))
                        .Append("</strong> (")
                        .Append(HtmlEncode(service.Id))
                        .Append(")<br/><span class=\"meta\">")
                        .Append(HtmlEncode(service.ServiceType ?? ""))
                        .AppendLine("</span></li>");
                }

                body.AppendLine("</ul>");
            }
        });
    }

    internal static string RenderCollectionsHtml(HttpRequest request, MetadataSnapshot snapshot, IReadOnlyList<CollectionSummary> collections)
    {
        return RenderHtmlDocument("Collections", body =>
        {
            body.Append("<h1>Collections</h1>");
            body.Append("<p><a href=\"")
                .Append(HtmlEncode(BuildHref(request, "/ogc", null, null)))
                .AppendLine("\">Back to landing</a></p>");

            if (collections.Count == 0)
            {
                body.AppendLine("<p>No collections are published.</p>");
                return;
            }

            body.AppendLine("<table><thead><tr><th>ID</th><th>Title</th><th>Description</th><th>Item Type</th><th>CRS</th></tr></thead><tbody>");
            foreach (var collection in collections)
            {
                body.Append("<tr><td>")
                    .Append(HtmlEncode(collection.Id))
                    .Append("</td><td>")
                    .Append(HtmlEncode(collection.Title ?? collection.Id))
                    .Append("</td><td>")
                    .Append(HtmlEncode(collection.Description))
                    .Append("</td><td>")
                    .Append(HtmlEncode(collection.ItemType))
                    .Append("</td><td>")
                    .Append(HtmlEncode(string.Join(", ", collection.Crs)))
                    .AppendLine("</td></tr>");
            }

            body.AppendLine("</tbody></table>");
        });
    }

    internal static string RenderCollectionHtml(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        IReadOnlyList<string> crs,
        IReadOnlyList<OgcLink> links)
    {
        return RenderHtmlDocument(layer.Title ?? collectionId, body =>
        {
            body.Append("<h1>")
                .Append(HtmlEncode(layer.Title ?? collectionId))
                .AppendLine("</h1>");

            if (layer.Description.HasValue())
            {
                body.Append("<p>")
                    .Append(HtmlEncode(layer.Description))
                    .AppendLine("</p>");
            }

            body.AppendLine("<table><tbody>");
            AppendMetadataRow(body, "Collection ID", collectionId);
            AppendMetadataRow(body, "Service", service.Title ?? service.Id);
            AppendMetadataRow(body, "Item Type", layer.ItemType);
            AppendMetadataRow(body, "Storage CRS", DetermineStorageCrs(layer));
            AppendMetadataRow(body, "Supported CRS", string.Join(", ", crs));
            AppendMetadataRow(body, "Default Style", layer.DefaultStyleId);
            AppendMetadataRow(body, "Styles", string.Join(", ", BuildOrderedStyleIds(layer)));
            body.AppendLine("</tbody></table>");

            AppendLinksHtml(body, links);

            body.Append("<p><a href=\"")
                .Append(HtmlEncode(BuildHref(request, "/ogc/collections", null, null)))
                .AppendLine("\">Back to collections</a></p>");
        });
    }

    internal static string RenderFeatureCollectionHtml(
        string title,
        string? subtitle,
        IReadOnlyList<HtmlFeatureEntry> features,
        long? numberMatched,
        long numberReturned,
        string? contentCrs,
        IReadOnlyList<OgcLink> links,
        bool hitsOnly)
    {
        return RenderHtmlDocument(title, body =>
        {
            body.Append("<h1>").Append(HtmlEncode(title)).AppendLine("</h1>");
            if (subtitle.HasValue())
            {
                body.Append("<p>").Append(HtmlEncode(subtitle)).AppendLine("</p>");
            }

            var matchedDisplay = numberMatched.HasValue
                ? numberMatched.Value.ToString(CultureInfo.InvariantCulture)
                : "unknown";

            body.Append("<p><strong>Number matched:</strong> ")
                .Append(HtmlEncode(matchedDisplay))
                .Append(" &nbsp; <strong>Number returned:</strong> ")
                .Append(HtmlEncode(numberReturned.ToString(CultureInfo.InvariantCulture)))
                .AppendLine("</p>");

            if (contentCrs.HasValue())
            {
                body.Append("<p><strong>Content CRS:</strong> ")
                    .Append(HtmlEncode(contentCrs))
                    .AppendLine("</p>");
            }

            AppendLinksHtml(body, links);

            if (hitsOnly)
            {
                body.AppendLine("<p>Result type is <code>hits</code>; no features are returned.</p>");
                return;
            }

            if (features.Count == 0)
            {
                body.AppendLine("<p>No features found.</p>");
                return;
            }

            foreach (var entry in features)
            {
                var displayName = entry.Components.DisplayName ?? entry.Components.FeatureId;
                body.Append("<details open><summary>")
                    .Append(HtmlEncode(displayName ?? entry.Components.FeatureId))
                    .Append("</summary>");

                if (entry.CollectionTitle.HasValue())
                {
                    body.Append("<p><strong>Collection:</strong> ")
                        .Append(HtmlEncode(entry.CollectionTitle))
                        .AppendLine("</p>");
                }

                AppendFeaturePropertiesTable(body, entry.Components.Properties);
                AppendGeometrySection(body, entry.Components.Geometry);
                body.AppendLine("</details>");
            }
        });
    }

    private static async Task WriteGeoJsonSearchResponseAsync(
        Stream outputStream,
        HttpRequest request,
        IReadOnlyList<string> collections,
        string contentType,
        FeatureQuery baseQuery,
        IReadOnlyList<(SearchCollectionContext Context, FeatureQuery Query, string ContentCrs)> preparedQueries,
        bool includeCount,
        FeatureResultType resultType,
        IFeatureRepository repository,
        long initialLimit,
        long initialOffset,
        CancellationToken cancellationToken)
    {
        await using var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { SkipValidation = false });

        writer.WriteStartObject();
        writer.WriteString("type", "FeatureCollection");
        writer.WriteString("timeStamp", DateTimeOffset.UtcNow);
        writer.WritePropertyName("features");
        writer.WriteStartArray();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        var iterationResult = await EnumerateSearchAsync(
            preparedQueries,
            resultType,
            includeCount,
            initialLimit,
            initialOffset,
            repository,
            async (context, layer, query, record, components) =>
            {
                var feature = ToFeature(request, context.CollectionId, layer, record, query, components);
                JsonSerializer.Serialize(writer, feature, GeoJsonSerializerOptions);

                if (writer.BytesPending > 8192)
                {
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);

        writer.WriteEndArray();

        var matched = iterationResult.NumberMatched ?? iterationResult.NumberReturned;
        writer.WriteNumber("numberMatched", matched);
        writer.WriteNumber("numberReturned", iterationResult.NumberReturned);

        var links = BuildSearchLinks(request, collections, baseQuery, iterationResult.NumberMatched, contentType);
        writer.WritePropertyName("links");
        JsonSerializer.Serialize(writer, links, GeoJsonSerializerOptions);

        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SearchIterationResult> EnumerateSearchAsync(
        IReadOnlyList<(SearchCollectionContext Context, FeatureQuery Query, string ContentCrs)> preparedQueries,
        FeatureResultType resultType,
        bool includeCount,
        long initialLimit,
        long initialOffset,
        IFeatureRepository repository,
        Func<SearchCollectionContext, LayerDefinition, FeatureQuery, FeatureRecord, FeatureComponents, ValueTask<bool>> onFeature,
        CancellationToken cancellationToken)
    {
        long? numberMatchedTotal = includeCount ? 0L : null;
        long numberReturnedTotal = 0;
        var remainingLimit = initialLimit;
        var remainingOffset = initialOffset;
        var enforceLimit = remainingLimit != long.MaxValue;

        if (resultType == FeatureResultType.Hits)
        {
            foreach (var prepared in preparedQueries)
            {
                var service = prepared.Context.FeatureContext.Service;
                var layer = prepared.Context.FeatureContext.Layer;
                var query = prepared.Query;
                var matched = await repository.CountAsync(service.Id, layer.Id, query, cancellationToken).ConfigureAwait(false);
                numberMatchedTotal += matched;
            }

            return new SearchIterationResult(numberMatchedTotal, 0);
        }

        foreach (var prepared in preparedQueries)
        {
            var service = prepared.Context.FeatureContext.Service;
            var layer = prepared.Context.FeatureContext.Layer;
            var query = prepared.Query;

            if (includeCount)
            {
                var matched = await repository.CountAsync(service.Id, layer.Id, query, cancellationToken).ConfigureAwait(false);
                numberMatchedTotal += matched;

                long skip = Math.Min(remainingOffset, matched);
                remainingOffset -= skip;

                var available = matched - skip;
                if (available <= 0)
                {
                    continue;
                }

                var allowed = enforceLimit ? Math.Min(available, remainingLimit) : available;
                if (allowed <= 0)
                {
                    continue;
                }

                var adjustedQuery = query with
                {
                    Offset = (int)Math.Min(skip, int.MaxValue),
                    Limit = (int)Math.Min(allowed, int.MaxValue)
                };

                await foreach (var record in repository.QueryAsync(service.Id, layer.Id, adjustedQuery, cancellationToken).ConfigureAwait(false))
                {
                    var components = FeatureComponentBuilder.BuildComponents(layer, record, adjustedQuery);
                    var shouldContinue = await onFeature(prepared.Context, layer, adjustedQuery, record, components).ConfigureAwait(false);
                    numberReturnedTotal++;

                    if (enforceLimit)
                    {
                        remainingLimit--;
                        if (remainingLimit <= 0)
                        {
                            return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                        }
                    }

                    if (!shouldContinue)
                    {
                        return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                    }
                }

                continue;
            }

            var streamingQuery = query with
            {
                Offset = null,
                Limit = query.Limit
            };

            await foreach (var record in repository.QueryAsync(service.Id, layer.Id, streamingQuery, cancellationToken).ConfigureAwait(false))
            {
                if (remainingOffset > 0)
                {
                    remainingOffset--;
                    continue;
                }

                var components = FeatureComponentBuilder.BuildComponents(layer, record, streamingQuery);
                var shouldContinue = await onFeature(prepared.Context, layer, streamingQuery, record, components).ConfigureAwait(false);
                numberReturnedTotal++;

                if (enforceLimit)
                {
                    remainingLimit--;
                    if (remainingLimit <= 0)
                    {
                        return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                    }
                }

                if (!shouldContinue)
                {
                    return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                }
            }
        }

        return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
    }

    private sealed record SearchIterationResult(long? NumberMatched, long NumberReturned);

    internal static string RenderFeatureHtml(
        string title,
        string? description,
        HtmlFeatureEntry entry,
        string? contentCrs,
        IReadOnlyList<OgcLink> links)
    {
        return RenderHtmlDocument(title, body =>
        {
            body.Append("<h1>").Append(HtmlEncode(title)).AppendLine("</h1>");
            if (description.HasValue())
            {
                body.Append("<p>").Append(HtmlEncode(description)).AppendLine("</p>");
            }

            AppendFeaturePropertiesTable(body, entry.Components.Properties);
            AppendGeometrySection(body, entry.Components.Geometry);

            if (contentCrs.HasValue())
            {
                body.Append("<p><strong>Content CRS:</strong> ")
                    .Append(HtmlEncode(contentCrs))
                    .AppendLine("</p>");
            }

            AppendLinksHtml(body, links);
        });
    }

    private static void AppendLinksHtml(StringBuilder builder, IReadOnlyList<OgcLink> links)
    {
        if (links.Count == 0)
        {
            return;
        }

        builder.AppendLine("<h2>Links</h2>");
        builder.AppendLine("<ul>");
        foreach (var link in links)
        {
            builder.Append("  <li><a href=\"")
                .Append(HtmlEncode(link.Href))
                .Append("\">")
                .Append(HtmlEncode(link.Title ?? link.Rel))
                .Append("</a> <span class=\"meta\">(")
                .Append(HtmlEncode(link.Rel))
                .Append(")")
                .Append(link.Type.IsNullOrWhiteSpace() ? string.Empty : $", {HtmlEncode(link.Type)}")
                .AppendLine("</span></li>");
        }
        builder.AppendLine("</ul>");
    }

    private static void AppendFeaturePropertiesTable(StringBuilder builder, IReadOnlyDictionary<string, object?> properties)
    {
        builder.AppendLine("<h3>Properties</h3>");
        builder.AppendLine("<table><tbody>");
        foreach (var pair in properties.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("<tr><th>")
                .Append(HtmlEncode(pair.Key))
                .Append("</th><td>")
                .Append(HtmlEncode(FormatPropertyValue(pair.Value)))
                .AppendLine("</td></tr>");
        }
        builder.AppendLine("</tbody></table>");
    }

    private static void AppendGeometrySection(StringBuilder builder, object? geometry)
    {
        var geometryText = FormatGeometryValue(geometry);
        if (geometryText.IsNullOrWhiteSpace())
        {
            return;
        }

        builder.AppendLine("<h3>Geometry</h3>");
        builder.Append("<pre>")
            .Append(HtmlEncode(geometryText))
            .AppendLine("</pre>");
    }

    private static void AppendMetadataRow(StringBuilder builder, string label, string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return;
        }

        builder.Append("<tr><th>")
            .Append(HtmlEncode(label))
            .Append("</th><td>")
            .Append(HtmlEncode(value))
            .AppendLine("</td></tr>");
    }

    private static string RenderHtmlDocument(string title, Action<StringBuilder> bodyWriter)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.Append("<title>").Append(HtmlEncode(title)).AppendLine("</title>");
        builder.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:1.5rem;}table{border-collapse:collapse;margin-bottom:1rem;}th,td{border:1px solid #ccc;padding:0.35rem 0.6rem;text-align:left;}th{background:#f5f5f5;}details{margin-bottom:1rem;}summary{font-weight:600;}code{font-family:Consolas,Menlo,monospace;}pre{background:#f5f5f5;padding:0.75rem;overflow:auto;}ul{list-style:disc;margin-left:1.5rem;} .meta{color:#555;font-size:0.9em;}</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        bodyWriter(builder);
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static string HtmlEncode(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    internal static string FormatPropertyValue(object? value)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case string text:
                return text;
            case bool b:
                return b ? "true" : "false";
            case JsonNode node:
                return node.ToJsonString(HtmlJsonOptions);
            case JsonElement element:
                return element.ValueKind == JsonValueKind.String
                    ? element.GetString() ?? string.Empty
                    : element.GetRawText();
            case byte[] bytes:
                return $"[binary: {bytes.Length} bytes]";
            case IEnumerable enumerable when value is not string:
                try
                {
                    return JsonSerializer.Serialize(enumerable, HtmlJsonOptions);
                }
                catch
                {
                    return value.ToString() ?? string.Empty;
                }
            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            default:
                try
                {
                    return JsonSerializer.Serialize(value, HtmlJsonOptions);
                }
                catch
                {
                    return value.ToString() ?? string.Empty;
                }
        }
    }

    internal static string? FormatGeometryValue(object? geometry)
    {
        return geometry switch
        {
            null => null,
            JsonNode node => node.ToJsonString(HtmlJsonOptions),
            JsonElement element => element.GetRawText(),
            string text => text,
            _ => FormatPropertyValue(geometry)
        };
    }

    private static readonly JsonSerializerOptions HtmlJsonOptions = new(JsonSerializerDefaults.Web);
}
