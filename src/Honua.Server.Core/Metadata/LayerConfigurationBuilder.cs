// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Handles building layer configuration definitions from layer documents.
/// </summary>
internal sealed class LayerConfigurationBuilder
{
    private const string DefaultTemporalReferenceSystem = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian";
    private readonly MetadataSchemaParser _schemaParser;

    public LayerConfigurationBuilder(MetadataSchemaParser schemaParser)
    {
        _schemaParser = schemaParser ?? throw new ArgumentNullException(nameof(schemaParser));
    }

    /// <summary>
    /// Builds layer definitions from layer documents.
    /// </summary>
    public IReadOnlyList<LayerDefinition> BuildLayers(List<LayerDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<LayerDefinition>();
        }

        var layers = documents
            .Where(d => d?.Id is not null)
            .Select(BuildLayer)
            .Select(ApplyLayerAutoFilter)
            .ToList();

        return new ReadOnlyCollection<LayerDefinition>(layers);
    }

    private LayerDefinition BuildLayer(LayerDocument document)
    {
        return new LayerDefinition
        {
            Id = document.Id!,
            ServiceId = document.ServiceId!,
            Title = document.Title ?? document.Id!,
            Description = document.Description,
            GeometryType = document.GeometryType!,
            IdField = document.IdField!,
            DisplayField = document.DisplayField,
            GeometryField = document.GeometryField!,
            Crs = ToReadOnlyList(document.Crs),
            Extent = BuildExtent(document.Extent, document.Id!),
            Keywords = ToReadOnlyList(document.Keywords),
            Links = BuildLinks(document.Links),
            Catalog = BuildCatalogEntry(document.Catalog, $"{document.ServiceId}:{document.Id}"),
            Query = BuildLayerQuery(document.Query),
            Editing = BuildLayerEditing(document.Editing),
            Attachments = BuildLayerAttachments(document.Attachments),
            Storage = BuildLayerStorage(document.Storage),
            Fields = BuildFields(document.Fields),
            ItemType = document.ItemType.IsNullOrWhiteSpace() ? "feature" : document.ItemType!,
            DefaultStyleId = document.Styles?.DefaultStyleId,
            StyleIds = ToReadOnlyList(document.Styles?.StyleIds),
            Relationships = BuildLayerRelationships(document.Relationships),
            MinScale = document.MinScale,
            MaxScale = document.MaxScale
        };
    }

    private LayerExtentDefinition? BuildExtent(LayerExtentDocument? document, string layerId)
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
                    throw new InvalidDataException($"Layer '{layerId}' extent contains an invalid bounding box.");
                }

                bbox.Add(raw);
            }
        }

        var temporal = BuildTemporal(document.Temporal, layerId);
        var trs = ResolveTemporalReferenceSystem(document.Temporal, temporal);

        if (bbox.Count == 0 && temporal.Count == 0 && document.Crs.IsNullOrWhiteSpace() && trs.IsNullOrWhiteSpace())
        {
            return null;
        }

        return new LayerExtentDefinition
        {
            Bbox = new ReadOnlyCollection<double[]>(bbox),
            Crs = document.Crs,
            Temporal = temporal,
            TemporalReferenceSystem = trs
        };
    }

    private IReadOnlyList<TemporalIntervalDefinition> BuildTemporal(LayerTemporalExtentDocument? document, string layerId)
    {
        if (document?.Interval is not { Count: > 0 })
        {
            return Array.Empty<TemporalIntervalDefinition>();
        }

        var intervals = new List<TemporalIntervalDefinition>();
        foreach (var tuple in document.Interval)
        {
            if (tuple is null || tuple.Length < 2)
            {
                throw new InvalidDataException($"Layer '{layerId}' temporal extent entry is invalid.");
            }

            var start = ParseDate(tuple[0], layerId);
            var end = ParseDate(tuple[1], layerId);
            intervals.Add(new TemporalIntervalDefinition { Start = start, End = end });
        }

        return new ReadOnlyCollection<TemporalIntervalDefinition>(intervals);
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

    private static string? ResolveTemporalReferenceSystem(LayerTemporalExtentDocument? document, IReadOnlyList<TemporalIntervalDefinition> intervals)
    {
        if (intervals.Count == 0)
        {
            return null;
        }

        if (document?.Trs.HasValue() == true)
        {
            return document.Trs;
        }

        return DefaultTemporalReferenceSystem;
    }

    private static LayerDefinition ApplyLayerAutoFilter(LayerDefinition layer)
    {
        var cql = layer.Query.AutoFilter?.Cql;
        if (cql.IsNullOrWhiteSpace())
        {
            return layer;
        }

        try
        {
            var parsed = CqlFilterParser.Parse(cql, layer);
            var filter = new LayerQueryFilterDefinition
            {
                Cql = cql,
                Expression = parsed
            };

            return layer with { Query = layer.Query with { AutoFilter = filter } };
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Layer '{layer.Id}' autoFilter is invalid: {ex.Message}", ex);
        }
    }

    private LayerQueryDefinition BuildLayerQuery(LayerQueryDocument? document)
    {
        if (document is null)
        {
            return new LayerQueryDefinition();
        }

        return new LayerQueryDefinition
        {
            MaxRecordCount = document.MaxRecordCount,
            SupportedParameters = ToReadOnlyList(document.SupportedParameters),
            AutoFilter = BuildLayerAutoFilter(document.AutoFilter)
        };
    }

    private static LayerQueryFilterDefinition? BuildLayerAutoFilter(LayerQueryFilterDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(document.Cql))
        {
            return null;
        }

        return new LayerQueryFilterDefinition
        {
            Cql = document.Cql.Trim()
        };
    }

    private LayerAttachmentDefinition BuildLayerAttachments(LayerAttachmentDocument? document)
    {
        if (document is null)
        {
            return LayerAttachmentDefinition.Disabled;
        }

        return new LayerAttachmentDefinition
        {
            Enabled = document.Enabled ?? false,
            StorageProfileId = document.StorageProfileId,
            MaxSizeMiB = document.MaxSizeMiB,
            AllowedContentTypes = ToReadOnlyList(document.AllowedContentTypes),
            DisallowedContentTypes = ToReadOnlyList(document.DisallowedContentTypes),
            RequireGlobalIds = document.RequireGlobalIds ?? false,
            ReturnPresignedUrls = document.ReturnPresignedUrls ?? false,
            ExposeOgcLinks = document.ExposeOgcLinks ?? false
        };
    }

    private LayerEditingDefinition BuildLayerEditing(LayerEditingDocument? document)
    {
        if (document is null)
        {
            return LayerEditingDefinition.Disabled;
        }

        return new LayerEditingDefinition
        {
            Capabilities = BuildLayerEditCapabilities(document.Capabilities),
            Constraints = BuildLayerEditConstraints(document.Constraints)
        };
    }

    private LayerEditCapabilitiesDefinition BuildLayerEditCapabilities(LayerEditCapabilitiesDocument? document)
    {
        if (document is null)
        {
            return LayerEditCapabilitiesDefinition.Disabled;
        }

        return new LayerEditCapabilitiesDefinition
        {
            AllowAdd = document.AllowAdd ?? false,
            AllowUpdate = document.AllowUpdate ?? false,
            AllowDelete = document.AllowDelete ?? false,
            RequireAuthentication = document.RequireAuthentication ?? true,
            AllowedRoles = ToReadOnlyList(document.AllowedRoles)
        };
    }

    private LayerEditConstraintDefinition BuildLayerEditConstraints(LayerEditConstraintsDocument? document)
    {
        if (document is null)
        {
            return LayerEditConstraintDefinition.Empty;
        }

        return new LayerEditConstraintDefinition
        {
            ImmutableFields = ToReadOnlyList(document.ImmutableFields),
            RequiredFields = ToReadOnlyList(document.RequiredFields),
            DefaultValues = MetadataSchemaParser.ToReadOnlyDictionary(document.DefaultValues)
        };
    }

    private LayerStorageDefinition? BuildLayerStorage(LayerStorageDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        if (document.Table.IsNullOrWhiteSpace() &&
            document.GeometryColumn.IsNullOrWhiteSpace() &&
            document.PrimaryKey.IsNullOrWhiteSpace() &&
            document.TemporalColumn.IsNullOrWhiteSpace() &&
            document.Srid is null &&
            document.Crs.IsNullOrWhiteSpace())
        {
            return null;
        }

        var parsedSrid = document.Srid;
        if (parsedSrid is null && document.Crs.HasValue())
        {
            parsedSrid = CrsHelper.ParseCrs(document.Crs);
        }

        return new LayerStorageDefinition
        {
            Table = document.Table,
            GeometryColumn = document.GeometryColumn,
            PrimaryKey = document.PrimaryKey,
            TemporalColumn = document.TemporalColumn,
            Srid = parsedSrid,
            Crs = document.Crs.IsNullOrWhiteSpace() ? null : CrsHelper.NormalizeIdentifier(document.Crs)
        };
    }

    private IReadOnlyList<FieldDefinition> BuildFields(List<FieldDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<FieldDefinition>();
        }

        var fields = new List<FieldDefinition>();
        foreach (var document in documents)
        {
            if (document?.Name is null)
            {
                continue;
            }

            fields.Add(new FieldDefinition
            {
                Name = document.Name,
                Alias = document.Alias,
                DataType = document.Type,
                StorageType = document.StorageType,
                Nullable = document.Nullable ?? true,
                Editable = document.Editable ?? true,
                MaxLength = document.MaxLength,
                Precision = document.Precision,
                Scale = document.Scale
            });
        }

        return new ReadOnlyCollection<FieldDefinition>(fields);
    }

    private IReadOnlyList<LayerRelationshipDefinition> BuildLayerRelationships(List<LayerRelationshipDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return Array.Empty<LayerRelationshipDefinition>();
        }

        var list = new List<LayerRelationshipDefinition>(documents.Count);
        foreach (var document in documents)
        {
            if (document is null || document.RelatedLayerId.IsNullOrWhiteSpace() || document.KeyField.IsNullOrWhiteSpace() || document.RelatedKeyField.IsNullOrWhiteSpace())
            {
                continue;
            }

            list.Add(new LayerRelationshipDefinition
            {
                Id = document.Id ?? list.Count,
                Role = document.Role.IsNullOrWhiteSpace() ? "esriRelRoleOrigin" : document.Role!,
                Cardinality = document.Cardinality.IsNullOrWhiteSpace() ? "esriRelCardinalityOneToMany" : document.Cardinality!,
                RelatedLayerId = document.RelatedLayerId!,
                RelatedTableId = document.RelatedTableId,
                KeyField = document.KeyField!,
                RelatedKeyField = document.RelatedKeyField!,
                Composite = document.Composite,
                ReturnGeometry = document.ReturnGeometry,
                Semantics = ParseRelationshipSemantics(document.Semantics)
            });
        }

        return list.Count == 0 ? Array.Empty<LayerRelationshipDefinition>() : new ReadOnlyCollection<LayerRelationshipDefinition>(list);
    }

    private static LayerRelationshipSemantics ParseRelationshipSemantics(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return LayerRelationshipSemantics.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "pkfk" or "primary_key_foreign_key" or "primarykeyforeignkey" => LayerRelationshipSemantics.PrimaryKeyForeignKey,
            _ => LayerRelationshipSemantics.Unknown
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
