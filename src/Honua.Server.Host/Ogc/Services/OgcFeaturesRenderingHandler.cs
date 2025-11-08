// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Implementation of OGC API Features HTML rendering handler service.
/// Provides HTML rendering for landing pages, collections, and features.
/// </summary>
internal sealed class OgcFeaturesRenderingHandler : IOgcFeaturesRenderingHandler
{
    private const string HtmlMediaType = "text/html";
    private static readonly JsonSerializerOptions HtmlJsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public bool WantsHtml(HttpRequest request)
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

    /// <inheritdoc />
    public string RenderLandingHtml(HttpRequest request, MetadataSnapshot snapshot, IReadOnlyList<OgcLink> links)
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

    /// <inheritdoc />
    public string RenderCollectionsHtml(HttpRequest request, MetadataSnapshot snapshot, IReadOnlyList<OgcSharedHandlers.CollectionSummary> collections)
    {
        return RenderHtmlDocument("Collections", body =>
        {
            body.Append("<h1>Collections</h1>");
            body.Append("<p><a href=\"")
                .Append(HtmlEncode(OgcSharedHandlers.BuildHref(request, "/ogc", null, null)))
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

    /// <inheritdoc />
    public string RenderCollectionHtml(
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
                .Append(HtmlEncode(OgcSharedHandlers.BuildHref(request, "/ogc/collections", null, null)))
                .AppendLine("\">Back to collections</a></p>");
        });
    }

    /// <inheritdoc />
    public string RenderFeatureCollectionHtml(
        string title,
        string? subtitle,
        IReadOnlyList<OgcSharedHandlers.HtmlFeatureEntry> features,
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

    /// <inheritdoc />
    public string RenderFeatureHtml(
        string title,
        string? description,
        OgcSharedHandlers.HtmlFeatureEntry entry,
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

    /// <inheritdoc />
    public string FormatPropertyValue(object? value)
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

    /// <inheritdoc />
    public string? FormatGeometryValue(object? geometry)
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

    private void AppendFeaturePropertiesTable(StringBuilder builder, IReadOnlyDictionary<string, object?> properties)
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

    private void AppendGeometrySection(StringBuilder builder, object? geometry)
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

    private static string DetermineStorageCrs(LayerDefinition layer)
    {
        if (layer.Crs.Count > 0)
        {
            return layer.Crs[0];
        }

        if (layer.Extent?.Crs.HasValue() == true)
        {
            return layer.Extent.Crs;
        }

        return "EPSG:4326";
    }

    private static IReadOnlyList<string> BuildOrderedStyleIds(LayerDefinition layer)
    {
        if (layer.StyleIds == null || layer.StyleIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        var styleIds = new List<string>();
        var defaultStyleId = layer.DefaultStyleId;

        if (defaultStyleId.HasValue())
        {
            styleIds.Add(defaultStyleId);
        }

        foreach (var styleId in layer.StyleIds.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.Equals(styleId, defaultStyleId, StringComparison.OrdinalIgnoreCase))
            {
                styleIds.Add(styleId);
            }
        }

        return styleIds;
    }
}
