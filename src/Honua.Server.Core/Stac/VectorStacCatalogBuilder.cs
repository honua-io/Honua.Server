// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Stac;

public sealed class VectorStacCatalogBuilder
{
    public bool Supports(LayerDefinition layer)
    {
        Guard.NotNull(layer);
        return layer.Stac?.Enabled ?? false;
    }

    public string GetCollectionId(LayerDefinition layer)
    {
        Guard.NotNull(layer);
        return layer.Stac?.CollectionId ?? layer.Id;
    }

    public StacCollectionRecord Build(LayerDefinition layer, ServiceDefinition service, MetadataSnapshot snapshot)
    {
        Guard.NotNull(layer);
        Guard.NotNull(service);
        Guard.NotNull(snapshot);

        var stac = layer.Stac ?? new StacMetadata();
        var now = DateTimeOffset.UtcNow;
        var collectionId = stac.CollectionId ?? layer.Id;

        var extent = BuildExtent(layer.Extent);
        var keywords = CombineKeywords(
            layer.Keywords,
            layer.Catalog.Keywords,
            service.Keywords,
            service.Catalog.Keywords,
            snapshot.Catalog.Keywords);

        var links = BuildLinks(layer.Catalog.Links);
        if (service.Catalog.Links is { Count: > 0 })
        {
            links.AddRange(BuildLinks(service.Catalog.Links));
        }

        var description =
            layer.Description ??
            layer.Catalog.Summary ??
            service.Catalog.Summary ??
            snapshot.Catalog.Description;

        var properties = BuildCollectionProperties(layer, service, stac);

        return new StacCollectionRecord
        {
            Id = collectionId,
            Title = layer.Title,
            Description = description,
            License = stac.License ?? snapshot.Catalog.License?.Name,
            Version = snapshot.Catalog.Version,
            Keywords = keywords,
            Links = links,
            Extensions = stac.StacExtensions,
            Extent = extent,
            Properties = properties,
            DataSourceId = service.DataSourceId,
            ServiceId = service.Id,
            LayerId = layer.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public IReadOnlyList<StacItemRecord> BuildItems(LayerDefinition layer, ServiceDefinition service, MetadataSnapshot snapshot, string? baseUri = null)
    {
        Guard.NotNull(layer);
        Guard.NotNull(service);
        Guard.NotNull(snapshot);

        var stac = layer.Stac ?? new StacMetadata();
        if (!(stac.Enabled && Supports(layer)))
        {
            return Array.Empty<StacItemRecord>();
        }

        var collectionId = stac.CollectionId ?? layer.Id;
        var now = DateTimeOffset.UtcNow;

        var itemId = BuildItemId(layer, stac);
        var temporal = GetTemporalInterval(layer);

        var properties = new JsonObject
        {
            ["honua:serviceId"] = service.Id,
            ["honua:layerId"] = layer.Id,
            ["honua:geometryType"] = layer.GeometryType
        };

        if (layer.Catalog.Summary.HasValue())
        {
            properties["summary"] = layer.Catalog.Summary;
        }

        if (layer.Description.HasValue())
        {
            properties["description"] = layer.Description;
        }

        if (layer.IdField.HasValue())
        {
            properties["honua:idField"] = layer.IdField;
        }

        if (layer.GeometryField.HasValue())
        {
            properties["honua:geometryField"] = layer.GeometryField;
        }

        if (layer.Fields is { Count: > 0 })
        {
            var fieldNames = new JsonArray();
            foreach (var field in layer.Fields)
            {
                if (field.Name.HasValue())
                {
                    fieldNames.Add(field.Name);
                }
            }
            if (fieldNames.Count > 0)
            {
                properties["honua:fields"] = fieldNames;
            }
        }

        if (stac.AdditionalProperties is { Count: > 0 })
        {
            foreach (var (key, value) in stac.AdditionalProperties)
            {
                properties[key] = JsonSerializer.SerializeToNode(value);
            }
        }

        var bbox = BuildBoundingBox(layer.Extent);
        var geometry = BuildGeometry(layer.Extent);
        var assets = BuildVectorAssets(layer, service, stac, baseUri);
        var links = BuildLinks(layer.Catalog.Links);

        var item = new StacItemRecord
        {
            Id = itemId,
            CollectionId = collectionId,
            Title = layer.Title,
            Description = layer.Description ?? layer.Catalog.Summary ?? service.Catalog.Summary ?? snapshot.Catalog.Description,
            Properties = properties,
            Assets = assets,
            Links = links,
            Extensions = stac.StacExtensions,
            Bbox = bbox,
            Geometry = geometry,
            Datetime = temporal.datetime,
            StartDatetime = temporal.start,
            EndDatetime = temporal.end,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        return new[] { item };
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

        return set.ToList();
    }

    private static JsonObject? BuildCollectionProperties(LayerDefinition layer, ServiceDefinition service, StacMetadata stac)
    {
        var properties = new JsonObject
        {
            ["honua:serviceId"] = service.Id,
            ["honua:layerId"] = layer.Id
        };

        if (service.Title.HasValue())
        {
            properties["honua:serviceTitle"] = service.Title;
        }

        if (layer.Catalog.Summary.HasValue())
        {
            properties["summary"] = layer.Catalog.Summary;
        }

        if (stac.ItemIdTemplate.HasValue())
        {
            properties["honua:itemIdTemplate"] = stac.ItemIdTemplate;
        }

        if (stac.Providers is { Count: > 0 })
        {
            var providers = new JsonArray();
            foreach (var provider in stac.Providers)
            {
                var providerObject = new JsonObject
                {
                    ["name"] = provider.Name
                };

                if (provider.Description.HasValue())
                {
                    providerObject["description"] = provider.Description;
                }

                if (provider.Roles is { Count: > 0 })
                {
                    var roles = new JsonArray();
                    foreach (var role in provider.Roles)
                    {
                        roles.Add(JsonValue.Create(role));
                    }

                    providerObject["roles"] = roles;
                }

                if (provider.Url.HasValue())
                {
                    providerObject["url"] = provider.Url;
                }

                providers.Add(providerObject);
            }

            properties["providers"] = providers;
        }

        if (stac.Summaries is { Count: > 0 })
        {
            var summaries = ConvertToJsonObject(stac.Summaries);
            if (summaries.Count > 0)
            {
                properties["summaries"] = summaries;
            }
        }

        if (stac.Assets is { Count: > 0 })
        {
            var assets = ConvertAssets(stac.Assets);
            if (assets.Count > 0)
            {
                properties["assets"] = assets;
            }
        }

        if (stac.ItemAssets is { Count: > 0 })
        {
            var itemAssets = ConvertAssets(stac.ItemAssets);
            if (itemAssets.Count > 0)
            {
                properties["item_assets"] = itemAssets;
            }
        }

        if (stac.AdditionalProperties is { Count: > 0 })
        {
            foreach (var (key, value) in stac.AdditionalProperties)
            {
                properties[key] = JsonSerializer.SerializeToNode(value);
            }
        }

        return properties;
    }

    private static JsonObject ConvertAssets(IReadOnlyDictionary<string, StacAssetDefinition> assets)
    {
        var result = new JsonObject();
        foreach (var (key, definition) in assets)
        {
            var asset = new JsonObject
            {
                ["title"] = definition.Title,
                ["type"] = definition.Type
            };

            if (definition.Description.HasValue())
            {
                asset["description"] = definition.Description;
            }

            if (definition.Href.HasValue())
            {
                asset["href"] = definition.Href;
            }

            if (definition.Roles is { Count: > 0 })
            {
                var roles = new JsonArray();
                foreach (var role in definition.Roles)
                {
                    roles.Add(JsonValue.Create(role));
                }

                asset["roles"] = roles;
            }

            if (definition.AdditionalProperties is { Count: > 0 })
            {
                foreach (var (additionalKey, value) in definition.AdditionalProperties)
                {
                    asset[additionalKey] = JsonSerializer.SerializeToNode(value);
                }
            }

            result[key] = asset;
        }

        return result;
    }

    private static JsonObject ConvertToJsonObject(IReadOnlyDictionary<string, object> dictionary)
    {
        var result = new JsonObject();
        foreach (var (key, value) in dictionary)
        {
            result[key] = JsonSerializer.SerializeToNode(value);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, StacAsset> ConvertItemAssets(IReadOnlyDictionary<string, StacAssetDefinition> itemAssets)
    {
        if (itemAssets is null || itemAssets.Count == 0)
        {
            return new Dictionary<string, StacAsset>();
        }

        var result = new Dictionary<string, StacAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, definition) in itemAssets)
        {
            if (definition.Href.IsNullOrWhiteSpace())
            {
                continue;
            }

            result[key] = new StacAsset
            {
                Href = definition.Href!,
                Title = definition.Title,
                Description = definition.Description,
                Type = definition.Type,
                Roles = definition.Roles,
                Properties = definition.AdditionalProperties.Count > 0
                    ? JsonSerializer.SerializeToNode(definition.AdditionalProperties) as JsonObject
                    : null
            };
        }

        return result;
    }

    private static string BuildItemId(LayerDefinition layer, StacMetadata stac)
    {
        if (stac.ItemIdTemplate.HasValue() && !stac.ItemIdTemplate.Contains('{', StringComparison.Ordinal))
        {
            return stac.ItemIdTemplate!;
        }

        var collectionId = stac.CollectionId ?? layer.Id;
        return $"{collectionId}-overview";
    }

    private static (DateTimeOffset? datetime, DateTimeOffset? start, DateTimeOffset? end) GetTemporalInterval(LayerDefinition layer)
    {
        if (layer.Extent?.Temporal is { Count: > 0 })
        {
            var interval = layer.Extent.Temporal[0];
            var datetime = interval.Start ?? interval.End;
            return (datetime, interval.Start, interval.End);
        }

        return (null, null, null);
    }

    private static double[]? BuildBoundingBox(LayerExtentDefinition? extent)
    {
        if (extent?.Bbox is { Count: > 0 })
        {
            var bbox = extent.Bbox[0];
            if (bbox.Length >= 4)
            {
                var copy = new double[4];
                Array.Copy(bbox, copy, 4);
                return copy;
            }
        }

        return null;
    }

    private static string? BuildGeometry(LayerExtentDefinition? extent)
    {
        if (extent?.Bbox is not { Count: > 0 })
        {
            return null;
        }

        var bbox = extent.Bbox[0];
        if (bbox.Length < 4)
        {
            return null;
        }

        var coordinates = new JsonArray
        {
            new JsonArray(JsonValue.Create(bbox[0]), JsonValue.Create(bbox[1])),
            new JsonArray(JsonValue.Create(bbox[2]), JsonValue.Create(bbox[1])),
            new JsonArray(JsonValue.Create(bbox[2]), JsonValue.Create(bbox[3])),
            new JsonArray(JsonValue.Create(bbox[0]), JsonValue.Create(bbox[3])),
            new JsonArray(JsonValue.Create(bbox[0]), JsonValue.Create(bbox[1]))
        };

        var polygon = new JsonObject
        {
            ["type"] = "Polygon",
            ["coordinates"] = new JsonArray(coordinates)
        };

        return polygon.ToJsonString();
    }

    private static IReadOnlyDictionary<string, StacAsset> BuildVectorAssets(
        LayerDefinition layer,
        ServiceDefinition service,
        StacMetadata stac,
        string? baseUri)
    {
        var assets = new Dictionary<string, StacAsset>(StringComparer.OrdinalIgnoreCase);

        // Add user-defined assets from STAC metadata
        if (stac.ItemAssets is { Count: > 0 })
        {
            foreach (var (key, definition) in stac.ItemAssets)
            {
                if (definition.Href.IsNullOrWhiteSpace())
                {
                    continue;
                }

                assets[key] = new StacAsset
                {
                    Href = definition.Href!,
                    Title = definition.Title,
                    Description = definition.Description,
                    Type = definition.Type,
                    Roles = definition.Roles,
                    Properties = definition.AdditionalProperties.Count > 0
                        ? JsonSerializer.SerializeToNode(definition.AdditionalProperties) as JsonObject
                        : null
                };
            }
        }

        // Auto-generate vector format assets if baseUri is provided
        if (baseUri.HasValue())
        {
            var collectionId = stac.CollectionId ?? layer.Id;
            var serviceId = service.Id;

            // GeoJSON asset - primary data format
            if (!assets.ContainsKey("geojson"))
            {
                var geoJsonUri = $"{baseUri}/ogc/collections/{serviceId}:{layer.Id}/items?f=json";
                assets["geojson"] = new StacAsset
                {
                    Href = geoJsonUri,
                    Title = $"{layer.Title} - GeoJSON",
                    Description = "Vector features in GeoJSON format",
                    Type = "application/geo+json",
                    Roles = new[] { "data" }
                };
            }

            // FlatGeobuf asset - efficient binary format
            if (!assets.ContainsKey("flatgeobuf"))
            {
                var fgbUri = $"{baseUri}/ogc/collections/{serviceId}:{layer.Id}/items?f=flatgeobuf";
                assets["flatgeobuf"] = new StacAsset
                {
                    Href = fgbUri,
                    Title = $"{layer.Title} - FlatGeobuf",
                    Description = "Vector features in FlatGeobuf format (efficient binary)",
                    Type = "application/vnd.flatgeobuf",
                    Roles = new[] { "data" }
                };
            }

            // Vector Tiles asset (MVT) if geometry is suitable
            if (IsVectorTileCompatible(layer.GeometryType) && !assets.ContainsKey("tiles"))
            {
                var tilesUri = $"{baseUri}/vector-tiles/{serviceId}/{layer.Id}/{{z}}/{{x}}/{{y}}.pbf";
                assets["tiles"] = new StacAsset
                {
                    Href = tilesUri,
                    Title = $"{layer.Title} - Vector Tiles",
                    Description = "Vector tiles in Mapbox Vector Tile (MVT) format",
                    Type = "application/vnd.mapbox-vector-tile",
                    Roles = new[] { "tiles" }
                };
            }

            // WFS GetFeature endpoint
            if (!assets.ContainsKey("wfs") && service.Ogc.WfsEnabled)
            {
                var wfsUri = $"{baseUri}/services/{serviceId}/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames={layer.Id}";
                assets["wfs"] = new StacAsset
                {
                    Href = wfsUri,
                    Title = $"{layer.Title} - WFS",
                    Description = "OGC Web Feature Service endpoint",
                    Type = "application/gml+xml",
                    Roles = new[] { "data", "wfs" }
                };
            }

            // Thumbnail if available
            if (layer.Catalog.Thumbnail.HasValue() && !assets.ContainsKey("thumbnail"))
            {
                assets["thumbnail"] = new StacAsset
                {
                    Href = layer.Catalog.Thumbnail,
                    Title = $"{layer.Title} - Thumbnail",
                    Type = GuessThumbnailMediaType(layer.Catalog.Thumbnail),
                    Roles = new[] { "thumbnail" }
                };
            }
        }

        return assets;
    }

    private static bool IsVectorTileCompatible(string geometryType)
    {
        return geometryType switch
        {
            "Point" or "MultiPoint" or
            "LineString" or "MultiLineString" or
            "Polygon" or "MultiPolygon" => true,
            _ => false
        };
    }

    private static string? GuessThumbnailMediaType(string uri)
    {
        if (uri.IsNullOrWhiteSpace())
        {
            return null;
        }

        var extension = System.IO.Path.GetExtension(uri)?.ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png"
        };
    }
}
