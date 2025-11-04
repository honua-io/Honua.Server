// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Stac;

public sealed class RasterStacCatalogBuilder
{
    private const string ProjectionExtension = "https://stac-extensions.github.io/projection/v1.0.0/schema.json";
    private const string CogType = "cog";

    public bool Supports(RasterDatasetDefinition dataset)
    {
        Guard.NotNull(dataset);
        return string.Equals(dataset.Source.Type, CogType, StringComparison.OrdinalIgnoreCase);
    }

    public (StacCollectionRecord Collection, IReadOnlyList<StacItemRecord> Items) Build(RasterDatasetDefinition dataset, MetadataSnapshot snapshot)
    {
        Guard.NotNull(dataset);
        Guard.NotNull(snapshot);

        var now = DateTimeOffset.UtcNow;
        var service = ResolveService(snapshot, dataset.ServiceId);
        var layer = ResolveLayer(service, dataset.LayerId);

        var extent = BuildExtent(dataset.Extent ?? layer?.Extent);
        var epsg = ResolveEpsg(dataset, layer);
        var collectionExtensions = epsg.HasValue
            ? new[] { ProjectionExtension }
            : Array.Empty<string>();

        var keywords = CombineKeywords(
            dataset.Keywords,
            dataset.Catalog.Keywords,
            layer?.Keywords,
            layer?.Catalog.Keywords,
            service?.Keywords,
            service?.Catalog.Keywords,
            snapshot.Catalog.Keywords);

        var links = BuildLinks(dataset.Catalog.Links);
        if (service?.Catalog.Links is { Count: > 0 })
        {
            links.AddRange(BuildLinks(service.Catalog.Links));
        }
        if (layer?.Catalog.Links is { Count: > 0 })
        {
            links.AddRange(BuildLinks(layer.Catalog.Links));
        }

        var thumbnail = ResolveThumbnail(dataset, layer, service, snapshot);
        var collectionProperties = BuildCollectionProperties(dataset, service, layer, extent, epsg, thumbnail);
        var collection = new StacCollectionRecord
        {
            Id = dataset.Id,
            Title = dataset.Title,
            Description = dataset.Description ?? dataset.Catalog.Summary ?? layer?.Catalog.Summary ?? service?.Catalog.Summary ?? snapshot.Catalog.Description,
            License = snapshot.Catalog.License?.Name,
            Version = snapshot.Catalog.Version,
            Keywords = keywords,
            Links = links,
            Extensions = collectionExtensions,
            Extent = extent,
            Properties = collectionProperties,
            DataSourceId = service?.DataSourceId,
            ServiceId = dataset.ServiceId,
            LayerId = dataset.LayerId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var items = BuildItems(dataset, extent, layer, epsg, now, thumbnail);
        return (collection, items);
    }

    private static ServiceDefinition? ResolveService(MetadataSnapshot snapshot, string? serviceId)
    {
        if (serviceId.IsNullOrWhiteSpace())
        {
            return null;
        }

        return snapshot.Services.FirstOrDefault(s => string.Equals(s.Id, serviceId, StringComparison.OrdinalIgnoreCase));
    }

    private static LayerDefinition? ResolveLayer(ServiceDefinition? service, string? layerId)
    {
        if (service is null || layerId.IsNullOrWhiteSpace())
        {
            return null;
        }

        return service.Layers.FirstOrDefault(l => string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));
    }

    private static StacExtent BuildExtent(LayerExtentDefinition? extent)
    {
        if (extent is null)
        {
            return StacExtent.Empty;
        }

        var spatial = new List<double[]>();
        if (extent.Bbox is { Count: > 0 })
        {
            foreach (var bbox in extent.Bbox)
            {
                spatial.Add(CloneBbox(bbox));
            }
        }

        var temporal = new List<StacTemporalInterval>();
        if (extent.Temporal is { Count: > 0 })
        {
            foreach (var interval in extent.Temporal)
            {
                temporal.Add(new StacTemporalInterval
                {
                    Start = interval.Start,
                    End = interval.End
                });
            }
        }

        JsonObject? additional = null;
        if (extent.Crs.HasValue() || extent.TemporalReferenceSystem.HasValue())
        {
            additional = new JsonObject();
            if (extent.Crs.HasValue())
            {
                additional["spatial_reference_system"] = extent.Crs;
            }

            if (extent.TemporalReferenceSystem.HasValue())
            {
                additional["temporal_reference_system"] = extent.TemporalReferenceSystem;
            }
        }

        return new StacExtent
        {
            Spatial = spatial,
            Temporal = temporal,
            AdditionalProperties = additional
        };
    }

    private static double[] CloneBbox(double[] bbox)
    {
        var copy = new double[bbox.Length];
        Array.Copy(bbox, copy, bbox.Length);
        return copy;
    }

    private static List<StacLink> BuildLinks(IReadOnlyList<LinkDefinition> links)
    {
        if (links is null || links.Count == 0)
        {
            return new List<StacLink>();
        }

        var result = new List<StacLink>(links.Count);
        foreach (var link in links)
        {
            if (link.Href.IsNullOrWhiteSpace())
            {
                continue;
            }

            result.Add(new StacLink
            {
                Rel = link.Rel.IsNullOrWhiteSpace() ? "alternate" : link.Rel!,
                Href = link.Href!,
                Type = link.Type,
                Title = link.Title
            });
        }

        return result;
    }

    private static IReadOnlyList<string> CombineKeywords(params IReadOnlyList<string>?[] sources)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            foreach (var keyword in source)
            {
                if (keyword.HasValue())
                {
                    set.Add(keyword);
                }
            }
        }

        return set.Count == 0 ? Array.Empty<string>() : set.ToList();
    }

    private static JsonObject? BuildCollectionProperties(
        RasterDatasetDefinition dataset,
        ServiceDefinition? service,
        LayerDefinition? layer,
        StacExtent extent,
        int? epsg,
        string? thumbnail)
    {
        var properties = new JsonObject
        {
            ["honua:serviceId"] = dataset.ServiceId,
            ["honua:layerId"] = dataset.LayerId
        };

        if (service?.Title.HasValue() == true)
        {
            properties["honua:serviceTitle"] = service.Title;
        }

        if (layer?.Title.HasValue() == true)
        {
            properties["honua:layerTitle"] = layer.Title;
        }

        if (service?.DataSourceId.HasValue() == true)
        {
            properties["honua:dataSourceId"] = service.DataSourceId;
        }

        if (dataset.Catalog.Summary.HasValue())
        {
            properties["summary"] = dataset.Catalog.Summary;
        }
        else if (layer?.Catalog.Summary.HasValue() == true)
        {
            properties["summary"] = layer.Catalog.Summary;
        }

        if (thumbnail.HasValue())
        {
            properties["thumbnail"] = thumbnail;
        }

        if (dataset.Catalog.Themes is { Count: > 0 })
        {
            var themes = new JsonArray();
            foreach (var theme in dataset.Catalog.Themes)
            {
                if (theme.HasValue())
                {
                    themes.Add(theme);
                }
            }

            if (themes.Count > 0)
            {
                properties["honua:themes"] = themes;
            }
        }

        var honuaKeywords = CombineKeywords(dataset.Keywords, dataset.Catalog.Keywords);
        if (honuaKeywords.Count > 0)
        {
            var keywords = new JsonArray();
            foreach (var keyword in honuaKeywords)
            {
                keywords.Add(keyword);
            }

            properties["honua:keywords"] = keywords;
        }

        if (epsg.HasValue)
        {
            properties["proj:epsg"] = epsg.Value;

            if (extent.Spatial.Count > 0)
            {
                var projBbox = new JsonArray();
                foreach (var bbox in extent.Spatial)
                {
                    var coords = new JsonArray();
                    foreach (var value in bbox)
                    {
                        coords.Add(JsonValue.Create(value));
                    }

                    projBbox.Add(coords);
                }

                properties["proj:bbox"] = projBbox;
            }
        }

        if (properties.Count == 0)
        {
            return null;
        }

        return properties;
    }

    private static IReadOnlyList<StacItemRecord> BuildItems(
        RasterDatasetDefinition dataset,
        StacExtent extent,
        LayerDefinition? layer,
        int? epsg,
        DateTimeOffset timestamp,
        string? thumbnail)
    {
        var baseProperties = new JsonObject
        {
            ["title"] = dataset.Title,
            ["honua:serviceId"] = dataset.ServiceId,
            ["honua:layerId"] = dataset.LayerId
        };

        if (dataset.Description.HasValue())
        {
            baseProperties["description"] = dataset.Description;
        }
        else if (dataset.Catalog.Summary.HasValue())
        {
            baseProperties["description"] = dataset.Catalog.Summary;
        }
        else if (layer?.Catalog.Summary.HasValue() == true)
        {
            baseProperties["description"] = layer.Catalog.Summary;
        }

        if (dataset.Catalog.Themes is { Count: > 0 })
        {
            var themes = new JsonArray();
            foreach (var theme in dataset.Catalog.Themes)
            {
                if (theme.HasValue())
                {
                    themes.Add(theme);
                }
            }

            if (themes.Count > 0)
            {
                baseProperties["honua:themes"] = themes;
            }
        }

        var datasetKeywords = CombineKeywords(dataset.Keywords, dataset.Catalog.Keywords);
        if (datasetKeywords.Count > 0)
        {
            var keywords = new JsonArray();
            foreach (var keyword in datasetKeywords)
            {
                keywords.Add(keyword);
            }

            baseProperties["honua:keywords"] = keywords;
        }

        var assetRoles = dataset.Source.Type.Equals(CogType, StringComparison.OrdinalIgnoreCase)
            ? new[] { "data" }
            : Array.Empty<string>();
        var assetTemplate = new JsonObject
        {
            ["honua:sourceType"] = dataset.Source.Type
        };

        if (dataset.Source.CredentialsId.HasValue())
        {
            assetTemplate["honua:credentialsId"] = dataset.Source.CredentialsId;
        }

        if (dataset.Styles.DefaultStyleId.HasValue())
        {
            assetTemplate["honua:defaultStyleId"] = dataset.Styles.DefaultStyleId;
        }

        if (dataset.Styles.StyleIds is { Count: > 0 })
        {
            var styles = new JsonArray();
            foreach (var styleId in dataset.Styles.StyleIds)
            {
                if (styleId.HasValue())
                {
                    styles.Add(styleId);
                }
            }

            if (styles.Count > 0)
            {
                assetTemplate["honua:styleIds"] = styles;
            }
        }

        var total = Math.Max(Math.Max(extent.Spatial.Count, extent.Temporal.Count), 1);
        var items = new List<StacItemRecord>(total);
        var itemExtensions = epsg.HasValue ? new[] { ProjectionExtension } : Array.Empty<string>();

        for (var index = 0; index < total; index++)
        {
            var bbox = index < extent.Spatial.Count
                ? extent.Spatial[index]
                : extent.Spatial.Count > 0 ? extent.Spatial[^1] : null;

            var temporal = index < extent.Temporal.Count
                ? extent.Temporal[index]
                : extent.Temporal.Count > 0 ? extent.Temporal[^1] : null;

            var itemProperties = CloneJsonObject(baseProperties);
            if (temporal?.Start is not null)
            {
                itemProperties["start_datetime"] = temporal.Start.Value.ToString("O");
            }
            if (temporal?.End is not null)
            {
                itemProperties["end_datetime"] = temporal.End.Value.ToString("O");
            }

            if (epsg.HasValue)
            {
                itemProperties["proj:epsg"] = epsg.Value;
            }

            if (bbox is not null)
            {
                var projBbox = new JsonArray();
                foreach (var value in bbox)
                {
                    projBbox.Add(JsonValue.Create(value));
                }

                itemProperties["proj:bbox"] = projBbox;
            }

            var itemId = total == 1 ? dataset.Id : $"{dataset.Id}-{index + 1}";

            // Determine datetime handling per STAC spec:
            // - If dataset.Datetime is explicitly set, use it
            // - If both temporal Start and End exist, datetime must be null (use start_datetime/end_datetime instead)
            // - Otherwise use Start or End if one exists
            DateTimeOffset? itemDatetime;
            if (dataset.Datetime.HasValue)
            {
                itemDatetime = dataset.Datetime;
            }
            else if (temporal?.Start is not null && temporal?.End is not null)
            {
                itemDatetime = null;
            }
            else
            {
                itemDatetime = temporal?.Start ?? temporal?.End;
            }

            var item = new StacItemRecord
            {
                Id = itemId,
                CollectionId = dataset.Id,
                Title = dataset.Title,
                Description = dataset.Description ?? dataset.Catalog.Summary,
                Properties = itemProperties,
                Assets = BuildAssets(dataset, thumbnail, assetRoles, assetTemplate),
                Links = Array.Empty<StacLink>(),
                Extensions = itemExtensions,
                Bbox = bbox is null ? null : CloneBbox(bbox),
                Geometry = BuildGeometry(bbox),
                Datetime = itemDatetime,
                StartDatetime = temporal?.Start,
                EndDatetime = temporal?.End,
                RasterDatasetId = dataset.Id,
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            };

            items.Add(item);
        }

        return items;
    }

    private static IReadOnlyDictionary<string, StacAsset> BuildAssets(
        RasterDatasetDefinition dataset,
        string? thumbnail,
        IReadOnlyList<string> assetRoles,
        JsonObject assetTemplate)
    {
        var assets = new Dictionary<string, StacAsset>(StringComparer.OrdinalIgnoreCase)
        {
            ["cog"] = new StacAsset
            {
                Href = dataset.Source.Uri,
                Title = dataset.Title,
                Description = dataset.Description,
                Type = dataset.Source.MediaType ?? "image/tiff; application=geotiff; profile=cloud-optimized",
                Roles = assetRoles,
                Properties = CloneJsonObject(assetTemplate)
            }
        };

        if (thumbnail.HasValue())
        {
            assets["thumbnail"] = new StacAsset
            {
                Href = thumbnail,
                Title = dataset.Title,
                Type = GuessThumbnailMediaType(thumbnail),
                Roles = new[] { "thumbnail" }
            };
        }

        return assets;
    }

    private static string? BuildGeometry(double[]? bbox)
    {
        if (bbox is null || bbox.Length < 4)
        {
            return null;
        }

        var minX = bbox[0];
        var minY = bbox[1];
        var maxX = bbox[2];
        var maxY = bbox[3];

        var coordinates = new JsonArray
        {
            new JsonArray
            {
                new JsonArray(JsonValue.Create(minX), JsonValue.Create(minY)),
                new JsonArray(JsonValue.Create(maxX), JsonValue.Create(minY)),
                new JsonArray(JsonValue.Create(maxX), JsonValue.Create(maxY)),
                new JsonArray(JsonValue.Create(minX), JsonValue.Create(maxY)),
                new JsonArray(JsonValue.Create(minX), JsonValue.Create(minY))
            }
        };

        var geometry = new JsonObject
        {
            ["type"] = "Polygon",
            ["coordinates"] = coordinates
        };

        return geometry.ToJsonString();
    }

    private static JsonObject CloneJsonObject(JsonObject template)
    {
        var clone = new JsonObject();
        foreach (var property in template)
        {
            clone[property.Key] = property.Value?.DeepClone();
        }

        return clone;
    }

    private static int? ResolveEpsg(RasterDatasetDefinition dataset, LayerDefinition? layer)
    {
        if (dataset.Crs is { Count: > 0 })
        {
            foreach (var crs in dataset.Crs)
            {
                if (TryParseEpsg(crs, out var code))
                {
                    return code;
                }
            }
        }

        if (layer?.Crs is { Count: > 0 })
        {
            foreach (var crs in layer.Crs)
            {
                if (TryParseEpsg(crs, out var code))
                {
                    return code;
                }
            }
        }

        return null;
    }

    private static bool TryParseEpsg(string? value, out int epsg)
    {
        epsg = 0;
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        var span = value.AsSpan().Trim();
        var lastColon = span.LastIndexOf(':');
        if (lastColon >= 0)
        {
            span = span[(lastColon + 1)..];
        }

        return int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out epsg);
    }

    private static string? ResolveThumbnail(
        RasterDatasetDefinition dataset,
        LayerDefinition? layer,
        ServiceDefinition? service,
        MetadataSnapshot snapshot)
    {
        return dataset.Catalog.Thumbnail
            ?? layer?.Catalog.Thumbnail
            ?? service?.Catalog.Thumbnail;
    }

    private static string? GuessThumbnailMediaType(string uri)
    {
        if (uri.IsNullOrWhiteSpace())
        {
            return null;
        }

        var extension = Path.GetExtension(uri)?.ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => null
        };
    }
}
