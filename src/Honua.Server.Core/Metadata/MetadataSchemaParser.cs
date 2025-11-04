// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Handles parsing of metadata schema elements into definition objects.
/// </summary>
internal sealed class MetadataSchemaParser
{
    private const string DefaultTemporalReferenceSystem = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian";
    private static readonly IReadOnlyDictionary<string, string?> EmptyStringDictionary = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>());

    /// <summary>
    /// Builds catalog definition from catalog document.
    /// </summary>
    public CatalogDefinition BuildCatalog(CatalogDocument document)
    {
        return new CatalogDefinition
        {
            Id = document.Id!,
            Title = document.Title,
            Description = document.Description,
            Version = document.Version,
            Publisher = document.Publisher,
            Links = BuildLinks(document.Links),
            Keywords = ToReadOnlyList(document.Keywords),
            ThemeCategories = ToReadOnlyList(document.ThemeCategories),
            Contact = BuildCatalogContact(document.Contact),
            License = BuildCatalogLicense(document.License),
            Extents = BuildCatalogExtent(document.Extents)
        };
    }

    /// <summary>
    /// Builds server definition from server document.
    /// </summary>
    public ServerDefinition BuildServer(ServerDocument? document)
    {
        if (document is null)
        {
            return ServerDefinition.Default;
        }

        return new ServerDefinition
        {
            AllowedHosts = ToTrimmedReadOnlyList(document.AllowedHosts),
            Cors = BuildCors(document.Cors)
        };
    }

    /// <summary>
    /// Builds folder definitions from folder documents.
    /// </summary>
    public IReadOnlyList<FolderDefinition> BuildFolders(List<FolderDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<FolderDefinition>();
        }

        var folders = documents
            .Where(d => d?.Id is not null)
            .Select(d => new FolderDefinition
            {
                Id = d!.Id!,
                Title = d.Title,
                Order = d.Order
            })
            .ToList();

        return new ReadOnlyCollection<FolderDefinition>(folders);
    }

    /// <summary>
    /// Builds data source definitions from data source documents.
    /// </summary>
    public IReadOnlyList<DataSourceDefinition> BuildDataSources(List<DataSourceDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<DataSourceDefinition>();
        }

        var sources = documents
            .Where(d => d?.Id is not null)
            .Select(d => new DataSourceDefinition
            {
                Id = d!.Id!,
                Provider = d.Provider!,
                ConnectionString = d.ConnectionString!
            })
            .ToList();

        return new ReadOnlyCollection<DataSourceDefinition>(sources);
    }

    /// <summary>
    /// Builds service definitions from service documents.
    /// </summary>
    public IReadOnlyList<ServiceDefinition> BuildServices(List<ServiceDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<ServiceDefinition>();
        }

        var services = documents
            .Where(d => d?.Id is not null)
            .Select(BuildService)
            .ToList();

        return new ReadOnlyCollection<ServiceDefinition>(services);
    }

    /// <summary>
    /// Builds style definitions from style documents.
    /// </summary>
    public IReadOnlyList<StyleDefinition> BuildStyles(List<StyleDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<StyleDefinition>();
        }

        var styles = new List<StyleDefinition>(documents.Count);
        foreach (var document in documents)
        {
            if (document?.Id.IsNullOrWhiteSpace() == true)
            {
                continue;
            }

            var rules = BuildStyleRules(document.Rules);
            styles.Add(new StyleDefinition
            {
                Id = document.Id!,
                Title = document.Title,
                Renderer = NormalizeRendererName(document.Renderer),
                Format = NormalizeStyleFormat(document.Format),
                GeometryType = NormalizeStyleGeometryType(document.GeometryType),
                Rules = rules,
                Simple = BuildSimpleStyle(document.Simple),
                UniqueValue = BuildUniqueValueStyle(document.UniqueValue)
            });
        }

        return new ReadOnlyCollection<StyleDefinition>(styles);
    }

    private ServiceDefinition BuildService(ServiceDocument document)
    {
        return new ServiceDefinition
        {
            Id = document.Id!,
            Title = document.Title ?? document.Id!,
            FolderId = document.FolderId!,
            ServiceType = document.ServiceType.IsNullOrWhiteSpace() ? "feature" : document.ServiceType!,
            DataSourceId = document.DataSourceId!,
            Enabled = document.Enabled ?? true,
            Description = document.Description,
            Keywords = ToReadOnlyList(document.Keywords),
            Links = BuildLinks(document.Links),
            Catalog = BuildCatalogEntry(document.Catalog, document.Id!),
            Ogc = BuildOgc(document.Ogc)
        };
    }

    private OgcServiceDefinition BuildOgc(OgcServiceDocument? document)
    {
        if (document is null)
        {
            return new OgcServiceDefinition();
        }

        return new OgcServiceDefinition
        {
            CollectionsEnabled = document.CollectionsEnabled ?? true,
            ItemLimit = document.ItemLimit,
            DefaultCrs = document.DefaultCrs,
            AdditionalCrs = ToReadOnlyList(document.AdditionalCrs),
            ConformanceClasses = ToReadOnlyList(document.ConformanceClasses)
        };
    }

    private CatalogContactDefinition? BuildCatalogContact(CatalogContactDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        return new CatalogContactDefinition
        {
            Name = document.Name,
            Email = document.Email,
            Organization = document.Organization,
            Phone = document.Phone,
            Url = document.Url,
            Role = document.Role
        };
    }

    private CatalogLicenseDefinition? BuildCatalogLicense(CatalogLicenseDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        return new CatalogLicenseDefinition
        {
            Name = document.Name,
            Url = document.Url
        };
    }

    private CatalogExtentDefinition? BuildCatalogExtent(CatalogExtentDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        var spatial = BuildCatalogSpatialExtent(document.Spatial, "catalog");
        var temporal = BuildCatalogTemporalCollection(document.Temporal, "catalog");

        if (spatial is null && temporal is null)
        {
            return null;
        }

        return new CatalogExtentDefinition
        {
            Spatial = spatial,
            Temporal = temporal
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

    private CatalogTemporalCollectionDefinition? BuildCatalogTemporalCollection(CatalogTemporalCollectionDocument? document, string contextId)
    {
        if (document is null)
        {
            return null;
        }

        var intervals = new List<CatalogTemporalExtentDefinition>();
        if (document.Interval is { Count: > 0 })
        {
            foreach (var tuple in document.Interval)
            {
                if (tuple is null || tuple.Length < 2)
                {
                    throw new InvalidDataException($"Catalog temporal extent for '{contextId}' is invalid.");
                }

                var start = ParseDate(tuple[0], contextId);
                var end = ParseDate(tuple[1], contextId);
                intervals.Add(new CatalogTemporalExtentDefinition { Start = start, End = end });
            }
        }

        if (intervals.Count == 0 && document.Trs.IsNullOrWhiteSpace())
        {
            return null;
        }

        return new CatalogTemporalCollectionDefinition
        {
            Interval = new ReadOnlyCollection<CatalogTemporalExtentDefinition>(intervals),
            TemporalReferenceSystem = document.Trs.IsNullOrWhiteSpace() ? DefaultTemporalReferenceSystem : document.Trs
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

    private CorsDefinition BuildCors(CorsDocument? document)
    {
        if (document is null)
        {
            return CorsDefinition.Disabled;
        }

        var (allowedOrigins, allowAnyOrigin) = NormalizeCorsList(document.AllowedOrigins, supportWildcard: true);
        var (allowedMethods, allowAnyMethod) = NormalizeCorsList(document.AllowedMethods, supportWildcard: true);
        var (allowedHeaders, allowAnyHeader) = NormalizeCorsList(document.AllowedHeaders, supportWildcard: true);
        var (exposedHeaders, _) = NormalizeCorsList(document.ExposedHeaders, supportWildcard: false);

        int? maxAge = document.MaxAgeSeconds;
        if (maxAge is <= 0)
        {
            maxAge = null;
        }

        var enabled = document.Enabled ?? (allowAnyOrigin || allowedOrigins.Count > 0);

        return new CorsDefinition
        {
            Enabled = enabled,
            AllowAnyOrigin = allowAnyOrigin,
            AllowedOrigins = allowedOrigins,
            AllowedMethods = allowedMethods,
            AllowAnyMethod = allowAnyMethod,
            AllowedHeaders = allowedHeaders,
            AllowAnyHeader = allowAnyHeader,
            ExposedHeaders = exposedHeaders,
            AllowCredentials = document.AllowCredentials ?? false,
            MaxAge = maxAge
        };
    }

    private (IReadOnlyList<string> Values, bool AllowAny) NormalizeCorsList(List<string>? values, bool supportWildcard)
    {
        if (values is null || values.Count == 0)
        {
            return (Array.Empty<string>(), false);
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        var seen = new HashSet<string>(comparer);
        var normalized = new List<string>();
        var allowAny = false;

        foreach (var value in values)
        {
            if (value.IsNullOrWhiteSpace())
            {
                continue;
            }

            var trimmed = value.Trim();
            if (supportWildcard && string.Equals(trimmed, "*", StringComparison.Ordinal))
            {
                allowAny = true;
                continue;
            }

            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        if (normalized.Count == 0)
        {
            return (Array.Empty<string>(), allowAny);
        }

        return (new ReadOnlyCollection<string>(normalized), allowAny);
    }

    private IReadOnlyList<StyleRuleDefinition> BuildStyleRules(List<StyleRuleDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<StyleRuleDefinition>();
        }

        var rules = new List<StyleRuleDefinition>(documents.Count);
        foreach (var document in documents)
        {
            if (document is null || document.Id.IsNullOrWhiteSpace())
            {
                continue;
            }

            var symbol = BuildSimpleStyle(document.Symbolizer) ?? new SimpleStyleDefinition();
            RuleFilterDefinition? filter = null;
            if (document.Filter is { Field: { } field, Value: { } value })
            {
                filter = new RuleFilterDefinition(field, value);
            }

            rules.Add(new StyleRuleDefinition
            {
                Id = document.Id!,
                IsDefault = document.Default ?? false,
                Label = document.Label,
                Filter = filter,
                MinScale = document.MinScale,
                MaxScale = document.MaxScale,
                Symbolizer = symbol
            });
        }

        return new ReadOnlyCollection<StyleRuleDefinition>(rules);
    }

    private SimpleStyleDefinition? BuildSimpleStyle(SimpleStyleDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        return new SimpleStyleDefinition
        {
            Label = document.Label,
            Description = document.Description,
            SymbolType = document.SymbolType.IsNullOrWhiteSpace() ? "shape" : document.SymbolType!,
            FillColor = document.FillColor,
            StrokeColor = document.StrokeColor,
            StrokeWidth = document.StrokeWidth,
            StrokeStyle = document.StrokeStyle,
            IconHref = document.IconHref,
            Size = document.Size,
            Opacity = document.Opacity
        };
    }

    private UniqueValueStyleDefinition? BuildUniqueValueStyle(UniqueValueStyleDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        var classes = new List<UniqueValueStyleClassDefinition>();
        if (document.Classes is not null)
        {
            foreach (var classDocument in document.Classes)
            {
                if (classDocument?.Value is null)
                {
                    continue;
                }

                classes.Add(new UniqueValueStyleClassDefinition
                {
                    Value = classDocument.Value,
                    Symbol = BuildSimpleStyle(classDocument.Symbol) ?? new SimpleStyleDefinition()
                });
            }
        }

        return new UniqueValueStyleDefinition
        {
            Field = document.Field.IsNullOrWhiteSpace() ? string.Empty : document.Field!,
            DefaultSymbol = BuildSimpleStyle(document.DefaultSymbol),
            Classes = new ReadOnlyCollection<UniqueValueStyleClassDefinition>(classes)
        };
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

    private static DateTimeOffset? ParseDate(string? value, string layerId)
    {
        if (value.IsNullOrWhiteSpace() || value == "..")
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
        {
            throw new InvalidDataException($"Layer '{layerId}' temporal extent boundary '{value}' is not a valid date/time value.");
        }

        return result;
    }

    private static string NormalizeRendererName(string? renderer)
    {
        if (renderer.IsNullOrWhiteSpace())
        {
            return "simple";
        }

        var normalized = renderer.Trim().ToLowerInvariant();
        return normalized switch
        {
            "uniquevalue" or "unique-value" => "uniqueValue",
            _ => normalized
        };
    }

    private static string NormalizeStyleFormat(string? format)
    {
        if (format.IsNullOrWhiteSpace())
        {
            return "legacy";
        }

        return format.Trim();
    }

    private static string NormalizeStyleGeometryType(string? geometryType)
    {
        if (geometryType.IsNullOrWhiteSpace())
        {
            return "polygon";
        }

        var normalized = geometryType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "polyline" => "line",
            "line" or "point" or "polygon" or "raster" => normalized,
            _ => "polygon"
        };
    }

    private static IReadOnlyList<string> ToTrimmedReadOnlyList(List<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        var seen = new HashSet<string>(comparer);
        var normalized = new List<string>();

        foreach (var value in values)
        {
            if (value.IsNullOrWhiteSpace())
            {
                continue;
            }

            var trimmed = value.Trim();
            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        return normalized.Count == 0
            ? Array.Empty<string>()
            : new ReadOnlyCollection<string>(normalized);
    }

    private static IReadOnlyList<string> ToReadOnlyList(List<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        return new ReadOnlyCollection<string>(values.Where(v => v.HasValue()).ToList());
    }

    public static IReadOnlyDictionary<string, string?> ToReadOnlyDictionary(Dictionary<string, string?>? values)
    {
        if (values is null || values.Count == 0)
        {
            return EmptyStringDictionary;
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        var copy = new Dictionary<string, string?>(values.Count, comparer);
        foreach (var pair in values)
        {
            if (pair.Key.IsNullOrWhiteSpace())
            {
                continue;
            }

            copy[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<string, string?>(copy);
    }
}
