// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Handles building raster dataset configuration definitions from raster dataset documents.
/// </summary>
internal sealed class RasterConfigurationBuilder
{
    private static readonly HashSet<string> SupportedRasterTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "geotiff",
        "cog",
        "cloud-optimized-geotiff",
        "vector"
    };

    private readonly LayerConfigurationBuilder _layerBuilder;

    public RasterConfigurationBuilder(LayerConfigurationBuilder layerBuilder)
    {
        _layerBuilder = layerBuilder ?? throw new ArgumentNullException(nameof(layerBuilder));
    }

    /// <summary>
    /// Builds raster dataset definitions from raster dataset documents.
    /// </summary>
    public IReadOnlyList<RasterDatasetDefinition> BuildRasterDatasets(List<RasterDatasetDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<RasterDatasetDefinition>();
        }

        var datasets = new List<RasterDatasetDefinition>(documents.Count);
        foreach (var document in documents)
        {
            if (document?.Id is null)
            {
                continue;
            }

            var source = BuildRasterSource(document.Source, document.Id);
            var styles = BuildRasterStyles(document.Styles);
            var cache = BuildRasterCache(document.Cache);

            datasets.Add(new RasterDatasetDefinition
            {
                Id = document.Id,
                Title = document.Title.IsNullOrWhiteSpace() ? document.Id : document.Title!,
                Description = document.Description,
                ServiceId = document.ServiceId,
                LayerId = document.LayerId,
                Keywords = ToReadOnlyList(document.Keywords),
                Crs = ToReadOnlyList(document.Crs),
                Catalog = BuildCatalogEntry(document.Catalog, $"raster:{document.Id}"),
                Extent = BuildExtent(document.Extent, document.Id),
                Source = source,
                Styles = styles,
                Cache = cache
            });
        }

        return new ReadOnlyCollection<RasterDatasetDefinition>(datasets);
    }

    private RasterSourceDefinition BuildRasterSource(RasterSourceDocument? document, string datasetId)
    {
        if (document is null)
        {
            throw new InvalidDataException($"Raster dataset '{datasetId}' must include a source definition.");
        }

        var type = NormalizeRasterType(document.Type, datasetId);
        var uri = document.Uri;
        if (uri.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Raster dataset '{datasetId}' source must include a uri.");
        }

        return new RasterSourceDefinition
        {
            Type = type,
            Uri = uri,
            MediaType = document.MediaType,
            CredentialsId = document.CredentialsId,
            DisableHttpRangeRequests = document.DisableHttpRangeRequests
        };
    }

    private static string NormalizeRasterType(string? type, string datasetId)
    {
        if (type.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Raster dataset '{datasetId}' source must include a type.");
        }

        if (!SupportedRasterTypes.Contains(type))
        {
            var supported = string.Join(", ", SupportedRasterTypes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            throw new InvalidDataException($"Raster dataset '{datasetId}' source type '{type}' is not supported. Supported types: {supported}.");
        }

        if (string.Equals(type, "cloud-optimized-geotiff", StringComparison.OrdinalIgnoreCase))
        {
            return "cog";
        }

        return type.ToLowerInvariant();
    }

    private RasterStyleDefinition BuildRasterStyles(RasterStyleDocument? document)
    {
        if (document is null)
        {
            return new RasterStyleDefinition();
        }

        return new RasterStyleDefinition
        {
            DefaultStyleId = document.DefaultStyleId,
            StyleIds = ToReadOnlyList(document.StyleIds)
        };
    }

    private RasterCacheDefinition BuildRasterCache(RasterCacheDocument? document)
    {
        if (document is null)
        {
            return new RasterCacheDefinition();
        }

        IReadOnlyList<int> zoomLevels = Array.Empty<int>();
        if (document.ZoomLevels is { Count: > 0 })
        {
            var normalized = document.ZoomLevels
                .Where(z => z >= 0)
                .Distinct()
                .OrderBy(z => z)
                .ToList();

            zoomLevels = normalized.Count == 0
                ? Array.Empty<int>()
                : new ReadOnlyCollection<int>(normalized);
        }

        return new RasterCacheDefinition
        {
            Enabled = document.Enabled ?? true,
            Preseed = document.Preseed ?? false,
            ZoomLevels = zoomLevels
        };
    }

    private LayerExtentDefinition? BuildExtent(LayerExtentDocument? document, string datasetId)
    {
        if (document is null)
        {
            return null;
        }

        var bbox = new List<double[]>();
        if (document.Bbox is { Count: > 0 })
        {
            foreach (var raw in document.Bbox)
            {
                if (raw is null || raw.Length < 4)
                {
                    throw new InvalidDataException($"Raster dataset '{datasetId}' extent contains an invalid bounding box.");
                }

                bbox.Add(raw);
            }
        }

        if (bbox.Count == 0 && document.Crs.IsNullOrWhiteSpace() && document.Temporal is null)
        {
            return null;
        }

        return new LayerExtentDefinition
        {
            Bbox = new ReadOnlyCollection<double[]>(bbox),
            Crs = document.Crs,
            Temporal = Array.Empty<TemporalIntervalDefinition>(),
            TemporalReferenceSystem = null
        };
    }

    private CatalogEntryDefinition BuildCatalogEntry(CatalogEntryDocument? document, string contextId)
    {
        if (document is null)
        {
            return new CatalogEntryDefinition();
        }

        return new CatalogEntryDefinition
        {
            Summary = document.Summary,
            Keywords = ToReadOnlyList(document.Keywords),
            Themes = ToReadOnlyList(document.Themes),
            Contacts = BuildCatalogContacts(document.Contacts),
            Links = BuildLinks(document.Links),
            Thumbnail = document.Thumbnail,
            Ordering = document.Ordering,
            SpatialExtent = BuildCatalogSpatialExtent(document.SpatialExtent, contextId),
            TemporalExtent = BuildCatalogTemporalExtent(document.TemporalExtent, contextId)
        };
    }

    private IReadOnlyList<CatalogContactDefinition> BuildCatalogContacts(List<CatalogContactDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<CatalogContactDefinition>();
        }

        var contacts = documents
            .Where(d => d is not null)
            .Select(d => new CatalogContactDefinition
            {
                Name = d!.Name,
                Email = d.Email,
                Organization = d.Organization,
                Phone = d.Phone,
                Url = d.Url,
                Role = d.Role
            })
            .ToList();

        return new ReadOnlyCollection<CatalogContactDefinition>(contacts);
    }

    private CatalogSpatialExtentDefinition? BuildCatalogSpatialExtent(CatalogSpatialExtentDocument? document, string contextId)
    {
        if (document is null)
        {
            return null;
        }

        var bbox = new List<double[]>();
        if (document.Bbox is { Count: > 0 })
        {
            foreach (var raw in document.Bbox)
            {
                if (raw is null || raw.Length < 4)
                {
                    throw new InvalidDataException($"Catalog spatial extent for '{contextId}' contains an invalid bounding box.");
                }

                bbox.Add(raw);
            }
        }

        return new CatalogSpatialExtentDefinition
        {
            Bbox = new ReadOnlyCollection<double[]>(bbox),
            Crs = document.Crs
        };
    }

    private CatalogTemporalExtentDefinition? BuildCatalogTemporalExtent(CatalogTemporalExtentDocument? document, string contextId)
    {
        if (document is null)
        {
            return null;
        }

        var start = ParseDate(document.Start, contextId);
        var end = ParseDate(document.End, contextId);

        if (!start.HasValue && !end.HasValue)
        {
            return null;
        }

        return new CatalogTemporalExtentDefinition
        {
            Start = start,
            End = end
        };
    }

    private static DateTimeOffset? ParseDate(string? value, string contextId)
    {
        if (value.IsNullOrWhiteSpace() || value == "..")
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var result))
        {
            throw new InvalidDataException($"Raster dataset '{contextId}' temporal extent boundary '{value}' is not a valid date/time value.");
        }

        return result;
    }

    private IReadOnlyList<LinkDefinition> BuildLinks(List<LinkDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<LinkDefinition>();
        }

        var links = documents
            .Where(d => d?.Href.HasValue() == true)
            .Select(d => new LinkDefinition
            {
                Href = d!.Href!,
                Rel = d.Rel,
                Type = d.Type,
                Title = d.Title
            })
            .ToList();

        return new ReadOnlyCollection<LinkDefinition>(links);
    }

    private static IReadOnlyList<string> ToReadOnlyList(List<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        return new ReadOnlyCollection<string>(values.Where(v => v.HasValue()).ToList());
    }
}
