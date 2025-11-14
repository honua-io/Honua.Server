// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.VectorTiles;
using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Metadata;

public sealed class MetadataSnapshot
{
    private readonly IReadOnlyDictionary<string, ServiceDefinition> serviceIndex;
    private readonly IReadOnlyDictionary<string, StyleDefinition> styleIndex;
    private readonly IReadOnlyDictionary<string, LayerGroupDefinition> layerGroupIndex;
    private readonly ILogger<MetadataSnapshot>? _logger;

    public MetadataSnapshot(
        CatalogDefinition catalog,
        IReadOnlyList<FolderDefinition> folders,
        IReadOnlyList<DataSourceDefinition> dataSources,
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<RasterDatasetDefinition>? rasterDatasets = null,
        IReadOnlyList<StyleDefinition>? styles = null,
        IReadOnlyList<LayerGroupDefinition>? layerGroups = null,
        ServerDefinition? server = null,
        ILogger<MetadataSnapshot>? logger = null)
    {
        _logger = logger;
        this.Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.Folders = folders ?? throw new ArgumentNullException(nameof(folders));
        this.DataSources = dataSources ?? throw new ArgumentNullException(nameof(dataSources));
        this.Layers = layers ?? throw new ArgumentNullException(nameof(layers));
        this.RasterDatasets = rasterDatasets ?? Array.Empty<RasterDatasetDefinition>();
        this.Styles = styles ?? Array.Empty<StyleDefinition>();
        this.LayerGroups = layerGroups ?? Array.Empty<LayerGroupDefinition>();
        this.Server = server ?? ServerDefinition.Default;

        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        ValidateMetadata(this.Catalog, this.Server, this.Folders, this.DataSources, services, this.Layers, this.RasterDatasets, this.Styles, this.LayerGroups, _logger);

        var serviceMap = new Dictionary<string, ServiceDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in services)
        {
            if (service is null)
            {
                continue;
            }

            var attachedLayers = layers
                .Where(l => string.Equals(l.ServiceId, service.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var serviceWithLayers = service with
            {
                Layers = new ReadOnlyCollection<LayerDefinition>(attachedLayers)
            };

            serviceMap[serviceWithLayers.Id] = serviceWithLayers;
        }

        this.Services = new ReadOnlyCollection<ServiceDefinition>(serviceMap.Values.ToList());
        this.serviceIndex = serviceMap;

        var styleMap = new Dictionary<string, StyleDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var style in this.Styles)
        {
            if (style is null)
            {
                continue;
            }

            styleMap[style.Id] = style;
        }

        this.styleIndex = styleMap;

        var layerGroupMap = new Dictionary<string, LayerGroupDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var layerGroup in this.LayerGroups)
        {
            if (layerGroup is null)
            {
                continue;
            }

            layerGroupMap[layerGroup.Id] = layerGroup;
        }

        this.layerGroupIndex = layerGroupMap;
    }

    public CatalogDefinition Catalog { get; }
    public IReadOnlyList<FolderDefinition> Folders { get; }
    public IReadOnlyList<DataSourceDefinition> DataSources { get; }
    public IReadOnlyList<ServiceDefinition> Services { get; }
    public IReadOnlyList<LayerDefinition> Layers { get; }
    public IReadOnlyList<RasterDatasetDefinition> RasterDatasets { get; }
    public IReadOnlyList<StyleDefinition> Styles { get; }
    public IReadOnlyList<LayerGroupDefinition> LayerGroups { get; }
    public ServerDefinition Server { get; }

    private static void ValidateMetadata(
        CatalogDefinition catalog,
        ServerDefinition server,
        IReadOnlyList<FolderDefinition> folders,
        IReadOnlyList<DataSourceDefinition> dataSources,
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<RasterDatasetDefinition> rasterDatasets,
        IReadOnlyList<StyleDefinition> styles,
        IReadOnlyList<LayerGroupDefinition> layerGroups,
        ILogger<MetadataSnapshot>? logger)
    {
        if (catalog.Id.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException("Catalog id must be provided.");
        }

        // Protocol-specific validation (warnings only, not blocking)
        ValidateProtocolRequirements(services, layers, rasterDatasets);

        if (server.Cors.AllowCredentials && server.Cors.AllowAnyOrigin)
        {
            throw new InvalidDataException("CORS configuration cannot allow credentials when all origins are allowed. Specify explicit origins or disable credential forwarding.");
        }

        var folderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            if (folder is null)
            {
                continue;
            }

            if (folder.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Folders must include an id.");
            }

            if (!folderIds.Add(folder.Id))
            {
                throw new InvalidDataException($"Duplicate folder id '{folder.Id}'.");
            }
        }

        var dataSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dataSource in dataSources)
        {
            if (dataSource is null)
            {
                continue;
            }

            if (dataSource.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Data sources must include an id.");
            }

            if (!dataSourceIds.Add(dataSource.Id))
            {
                throw new InvalidDataException($"Duplicate data source id '{dataSource.Id}'.");
            }
        }

        var styleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var style in styles)
        {
            if (style is null)
            {
                continue;
            }

            if (style.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Styles must include an id.");
            }

            if (!styleIds.Add(style.Id))
            {
                throw new InvalidDataException($"Duplicate style id '{style.Id}'.");
            }

            ValidateStyleDefinition(style);
        }

        var serviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in services)
        {
            if (service is null)
            {
                continue;
            }

            if (service.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Services must include an id.");
            }

            if (!serviceIds.Add(service.Id))
            {
                throw new InvalidDataException($"Duplicate service id '{service.Id}'.");
            }

            if (service.FolderId.IsNullOrWhiteSpace() || !folderIds.Contains(service.FolderId))
            {
                throw new InvalidDataException($"Service '{service.Id}' references unknown folder '{service.FolderId}'.");
            }

            if (service.DataSourceId.IsNullOrWhiteSpace() || !dataSourceIds.Contains(service.DataSourceId))
            {
                throw new InvalidDataException($"Service '{service.Id}' references unknown data source '{service.DataSourceId}'.");
            }

            ValidateStoredQueries(service);
        }

        var layerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in layers)
        {
            if (layer is null)
            {
                continue;
            }

            if (layer.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Layers must include an id.");
            }

            if (!layerIds.Add(layer.Id))
            {
                throw new InvalidDataException($"Duplicate layer id '{layer.Id}'.");
            }

            if (layer.ServiceId.IsNullOrWhiteSpace() || !serviceIds.Contains(layer.ServiceId))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' references unknown service '{layer.ServiceId}'.");
            }

            if (layer.GeometryType.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing a geometryType.");
            }

            if (layer.IdField.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing an idField.");
            }

            if (layer.GeometryField.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing a geometryField.");
            }

            // Validate that layer has either Storage or SqlView, but not both
            var hasStorage = layer.Storage?.Table.HasValue() == true;
            var hasSqlView = layer.SqlView?.Sql.HasValue() == true;

            if (!hasStorage && !hasSqlView)
            {
                throw new InvalidDataException($"Layer '{layer.Id}' must have either Storage.Table or SqlView defined.");
            }

            if (hasStorage && hasSqlView)
            {
                throw new InvalidDataException($"Layer '{layer.Id}' cannot have both Storage.Table and SqlView. Choose one or the other.");
            }

            // Validate SQL view if present
            if (hasSqlView)
            {
                ValidateSqlView(layer, logger);
            }

            if (layer.DefaultStyleId.HasValue() && !styleIds.Contains(layer.DefaultStyleId))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' references unknown default style '{layer.DefaultStyleId}'.");
            }

            foreach (var styleId in layer.StyleIds)
            {
                if (!styleIds.Contains(styleId))
                {
                    throw new InvalidDataException($"Layer '{layer.Id}' references unknown style '{styleId}'.");
                }
            }

            if (layer.MinScale is < 0)
            {
                throw new InvalidDataException($"Layer '{layer.Id}' minScale cannot be negative.");
            }

            if (layer.MaxScale is < 0)
            {
                throw new InvalidDataException($"Layer '{layer.Id}' maxScale cannot be negative.");
            }

            if (layer.MinScale is double minScale && minScale > 0 &&
                layer.MaxScale is double maxScale && maxScale > 0 && maxScale > minScale)
            {
                throw new InvalidDataException($"Layer '{layer.Id}' maxScale ({maxScale}) cannot be greater than minScale ({minScale}).");
            }
        }

        var rasterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raster in rasterDatasets)
        {
            if (raster is null)
            {
                continue;
            }

            if (raster.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Raster datasets must include an id.");
            }

            if (!rasterIds.Add(raster.Id))
            {
                throw new InvalidDataException($"Duplicate raster dataset id '{raster.Id}'.");
            }

            if (raster.ServiceId.HasValue() && !serviceIds.Contains(raster.ServiceId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown service '{raster.ServiceId}'.");
            }

            if (raster.LayerId.HasValue() && !layerIds.Contains(raster.LayerId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown layer '{raster.LayerId}'.");
            }

            if (raster.Source is null)
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' must include a source definition.");
            }

            if (raster.Source.Type.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' is missing a source type.");
            }

            if (raster.Source.Uri.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' is missing a source uri.");
            }

            if (raster.Styles.DefaultStyleId.HasValue() && !styleIds.Contains(raster.Styles.DefaultStyleId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' default style '{raster.Styles.DefaultStyleId}' is not defined.");
            }

            foreach (var styleId in raster.Styles.StyleIds)
            {
                if (!styleIds.Contains(styleId))
                {
                    throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown style '{styleId}'.");
                }
            }
        }

        // Validate layer groups
        var layerGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layerGroup in layerGroups)
        {
            if (layerGroup is null)
            {
                continue;
            }

            if (layerGroup.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Layer groups must include an id.");
            }

            if (!layerGroupIds.Add(layerGroup.Id))
            {
                throw new InvalidDataException($"Duplicate layer group id '{layerGroup.Id}'.");
            }

            if (layerGroup.Title.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' must have a title.");
            }

            if (layerGroup.ServiceId.IsNullOrWhiteSpace() || !serviceIds.Contains(layerGroup.ServiceId))
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' references unknown service '{layerGroup.ServiceId}'.");
            }

            if (layerGroup.DefaultStyleId.HasValue() && !styleIds.Contains(layerGroup.DefaultStyleId))
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' references unknown default style '{layerGroup.DefaultStyleId}'.");
            }

            foreach (var styleId in layerGroup.StyleIds)
            {
                if (!styleIds.Contains(styleId))
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' references unknown style '{styleId}'.");
                }
            }

            if (layerGroup.MinScale is < 0)
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' minScale cannot be negative.");
            }

            if (layerGroup.MaxScale is < 0)
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' maxScale cannot be negative.");
            }

            if (layerGroup.MinScale is double minScale && minScale > 0 &&
                layerGroup.MaxScale is double maxScale && maxScale > 0 && maxScale > minScale)
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' maxScale ({maxScale}) cannot be greater than minScale ({minScale}).");
            }

            // Validate members
            if (layerGroup.Members.Count == 0)
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' must have at least one member.");
            }

            for (int i = 0; i < layerGroup.Members.Count; i++)
            {
                var member = layerGroup.Members[i];
                if (member is null)
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' contains a null member at index {i}.");
                }

                // Validate that member has exactly one reference (LayerId or GroupId)
                var hasLayerId = member.LayerId.HasValue();
                var hasGroupId = member.GroupId.HasValue();

                if (!hasLayerId && !hasGroupId)
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {i} must specify either layerId or groupId.");
                }

                if (hasLayerId && hasGroupId)
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {i} cannot specify both layerId and groupId.");
                }

                // Validate type matches the ID provided
                if (member.Type == LayerGroupMemberType.Layer && !hasLayerId)
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {i} has type 'Layer' but no layerId specified.");
                }

                if (member.Type == LayerGroupMemberType.Group && !hasGroupId)
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {i} has type 'Group' but no groupId specified.");
                }

                // Validate referenced layer exists (only for layers in same service)
                if (hasLayerId)
                {
                    var referencedLayerExists = layers.Any(l =>
                        string.Equals(l.Id, member.LayerId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(l.ServiceId, layerGroup.ServiceId, StringComparison.OrdinalIgnoreCase));

                    if (!referencedLayerExists)
                    {
                        throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {i} references unknown layer '{member.LayerId}' in service '{layerGroup.ServiceId}'.");
                    }
                }

                // For group references, we'll validate after all groups are collected
                // to allow forward references

                // Validate opacity range
                if (member.Opacity is < 0 or > 1)
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {i} opacity must be between 0 and 1.");
                }

                // Validate style reference if specified
                if (member.StyleId.HasValue() && !styleIds.Contains(member.StyleId))
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {i} references unknown style '{member.StyleId}'.");
                }
            }
        }

        // Second pass: validate group references and circular dependencies
        foreach (var layerGroup in layerGroups)
        {
            if (layerGroup is null)
            {
                continue;
            }

            foreach (var member in layerGroup.Members)
            {
                if (member.Type == LayerGroupMemberType.Group)
                {
                    if (!layerGroupIds.Contains(member.GroupId!))
                    {
                        throw new InvalidDataException($"Layer group '{layerGroup.Id}' references unknown nested group '{member.GroupId}'.");
                    }

                    // Check that nested group is in the same service
                    var nestedGroup = layerGroups.FirstOrDefault(g =>
                        string.Equals(g.Id, member.GroupId, StringComparison.OrdinalIgnoreCase));

                    if (nestedGroup != null && !string.Equals(nestedGroup.ServiceId, layerGroup.ServiceId, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Layer group '{layerGroup.Id}' cannot reference group '{member.GroupId}' from a different service.");
                    }
                }
            }

            // Detect circular references
            DetectCircularGroupReferences(layerGroup, layerGroups, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static void DetectCircularGroupReferences(
        LayerGroupDefinition group,
        IReadOnlyList<LayerGroupDefinition> allGroups,
        HashSet<string> visitedGroups)
    {
        if (!visitedGroups.Add(group.Id))
        {
            throw new InvalidDataException($"Circular reference detected in layer group '{group.Id}'.");
        }

        foreach (var member in group.Members)
        {
            if (member.Type == LayerGroupMemberType.Group)
            {
                var nestedGroup = allGroups.FirstOrDefault(g =>
                    string.Equals(g.Id, member.GroupId, StringComparison.OrdinalIgnoreCase));

                if (nestedGroup != null)
                {
                    DetectCircularGroupReferences(nestedGroup, allGroups, new HashSet<string>(visitedGroups, StringComparer.OrdinalIgnoreCase));
                }
            }
        }
    }

    private static void ValidateStyleDefinition(StyleDefinition style)
    {
        if (style.Format.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Style '{style.Id}' must specify a format.");
        }

        if (style.GeometryType.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Style '{style.Id}' must specify a geometryType.");
        }

        foreach (var rule in style.Rules)
        {
            if (rule is null)
            {
                throw new InvalidDataException($"Style '{style.Id}' contains an undefined rule entry.");
            }

            if (rule.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Style '{style.Id}' contains a rule without an id.");
            }

            if (rule.Symbolizer is null)
            {
                throw new InvalidDataException($"Style '{style.Id}' rule '{rule.Id}' is missing a symbolizer definition.");
            }

            if (rule.Filter is { } filter)
            {
                if (filter.Field.IsNullOrWhiteSpace() || filter.Value.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Style '{style.Id}' rule '{rule.Id}' filter must include both field and value.");
                }
            }

            if (rule.MinScale is double minScale && rule.MaxScale is double maxScale && minScale > maxScale)
            {
                throw new InvalidDataException($"Style '{style.Id}' rule '{rule.Id}' has minScale greater than maxScale.");
            }
        }

        var renderer = style.Renderer?.Trim().ToLowerInvariant();
        switch (renderer)
        {
            case null or "" or "simple":
                if (style.Simple is null)
                {
                    throw new InvalidDataException($"Style '{style.Id}' with renderer 'simple' must include simple symbol details.");
                }
                break;
            case "uniquevalue":
            case "unique-value":
                if (style.UniqueValue is null)
                {
                    throw new InvalidDataException($"Style '{style.Id}' with renderer 'uniqueValue' must include unique value configuration.");
                }

                if (style.UniqueValue.Field.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Style '{style.Id}' unique value renderer must specify a field.");
                }

                if (style.UniqueValue.Classes.Count == 0)
                {
                    throw new InvalidDataException($"Style '{style.Id}' unique value renderer must include at least one class.");
                }

                foreach (var valueClass in style.UniqueValue.Classes)
                {
                    if (valueClass is null || valueClass.Value.IsNullOrWhiteSpace())
                    {
                        throw new InvalidDataException($"Style '{style.Id}' unique value renderer contains a class without a value.");
                    }

                    if (valueClass.Symbol is null)
                    {
                        throw new InvalidDataException($"Style '{style.Id}' unique value renderer class '{valueClass.Value}' is missing a symbol definition.");
                    }
                }
                break;
            default:
                throw new InvalidDataException($"Style '{style.Id}' specifies unsupported renderer '{style.Renderer}'.");
        }
    }

    private static void ValidateSqlView(LayerDefinition layer, ILogger<MetadataSnapshot>? logger)
    {
        var sqlView = layer.SqlView;
        if (sqlView is null)
        {
            return;
        }

        if (sqlView.Sql.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' SQL view must have a non-empty SQL query.");
        }

        // Basic SQL injection prevention - check for dangerous patterns
        var sql = sqlView.Sql.Trim();
        var sqlLower = sql.ToLowerInvariant();

        // Must be a SELECT statement
        if (!sqlLower.StartsWith("select"))
        {
            throw new InvalidDataException($"Layer '{layer.Id}' SQL view must start with SELECT. Only SELECT queries are allowed.");
        }

        // Check for dangerous SQL keywords that could modify data or structure
        var dangerousKeywords = new[]
        {
            "drop ", "truncate ", "alter ", "create ", "insert ", "update ", "delete ",
            "exec ", "execute ", "xp_", "sp_", "grant ", "revoke ", "commit ", "rollback ",
            "begin ", "end;", "declare ", "set ", "use ", "shutdown ", "backup ", "restore "
        };

        foreach (var keyword in dangerousKeywords)
        {
            if (sqlLower.Contains(keyword))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view contains potentially dangerous keyword '{keyword.Trim()}'. Only SELECT queries are allowed.");
            }
        }

        // Check for SQL comment patterns that could be used for SQL injection
        if (sqlLower.Contains("--") || sqlLower.Contains("/*"))
        {
            throw new InvalidDataException($"Layer '{layer.Id}' SQL view contains SQL comments which are not allowed for security reasons.");
        }

        // Validate parameters
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in sqlView.Parameters)
        {
            if (parameter.Name.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view contains a parameter without a name.");
            }

            if (!parameterNames.Add(parameter.Name))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view has duplicate parameter '{parameter.Name}'.");
            }

            if (parameter.Type.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view parameter '{parameter.Name}' must have a type.");
            }

            // Validate type is a known type
            var validTypes = new[] { "string", "integer", "long", "double", "decimal", "boolean", "date", "datetime" };
            if (!validTypes.Contains(parameter.Type.ToLowerInvariant()))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view parameter '{parameter.Name}' has invalid type '{parameter.Type}'. Valid types: {string.Join(", ", validTypes)}");
            }

            // Check that parameter is actually used in the SQL
            var paramRef = $":{parameter.Name}";
            if (!sql.Contains(paramRef, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view defines parameter '{parameter.Name}' but it is not used in the SQL query.");
            }

            // Validate parameter validation rules
            if (parameter.Validation is not null)
            {
                ValidateParameterValidation(layer.Id, parameter);
            }
        }

        // Warn if timeout is very high
        if (sqlView.TimeoutSeconds is > 300)
        {
            logger?.LogWarning("Layer {LayerId} SQL view has a very high timeout of {TimeoutSeconds} seconds", layer.Id, sqlView.TimeoutSeconds);
        }

        // Check that required fields are included in the query
        // This is a simple check - we look for the field names in the SQL
        var requiredFields = new[] { layer.IdField, layer.GeometryField };
        foreach (var field in requiredFields)
        {
            if (!sql.Contains(field, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view must include the '{field}' field in the SELECT clause.");
            }
        }
    }

    private static void ValidateParameterValidation(string layerId, SqlViewParameterDefinition parameter)
    {
        var validation = parameter.Validation!;

        // Numeric validations
        if (validation.Min.HasValue || validation.Max.HasValue)
        {
            var numericTypes = new[] { "integer", "long", "double", "decimal" };
            if (!numericTypes.Contains(parameter.Type.ToLowerInvariant()))
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has Min/Max validation but is not a numeric type.");
            }

            if (validation.Min.HasValue && validation.Max.HasValue && validation.Min.Value > validation.Max.Value)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has Min greater than Max.");
            }
        }

        // String validations
        if (validation.MinLength.HasValue || validation.MaxLength.HasValue || validation.Pattern.HasValue())
        {
            if (!parameter.Type.Equals("string", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has string validation but is not a string type.");
            }

            if (validation.MinLength.HasValue && validation.MinLength.Value < 0)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' MinLength cannot be negative.");
            }

            if (validation.MaxLength.HasValue && validation.MaxLength.Value < 1)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' MaxLength must be at least 1.");
            }

            if (validation.MinLength.HasValue && validation.MaxLength.HasValue && validation.MinLength.Value > validation.MaxLength.Value)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' MinLength is greater than MaxLength.");
            }

            // Validate regex pattern if present
            if (validation.Pattern.HasValue())
            {
                try
                {
                    _ = new System.Text.RegularExpressions.Regex(validation.Pattern!);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has invalid regex pattern: {ex.Message}");
                }
            }
        }

        // Allowed values validation
        if (validation.AllowedValues is { Count: > 0 })
        {
            if (validation.AllowedValues.Count > 1000)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has too many allowed values (max 1000).");
            }

            foreach (var value in validation.AllowedValues)
            {
                if (value.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has an empty allowed value.");
                }
            }
        }
    }

    private static void ValidateStoredQueries(ServiceDefinition service)
    {
        if (service.Ogc.StoredQueries.Count == 0)
        {
            return;
        }

        var queryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var storedQuery in service.Ogc.StoredQueries)
        {
            if (storedQuery is null)
            {
                throw new InvalidDataException($"Service '{service.Id}' contains a null stored query.");
            }

            if (storedQuery.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' contains a stored query without an id.");
            }

            if (!queryIds.Add(storedQuery.Id))
            {
                throw new InvalidDataException($"Service '{service.Id}' has duplicate stored query id '{storedQuery.Id}'.");
            }

            if (storedQuery.Title.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' must have a title.");
            }

            if (storedQuery.LayerId.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' must specify a layerId.");
            }

            if (storedQuery.FilterCql.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' must specify a filterCql expression.");
            }

            var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in storedQuery.Parameters)
            {
                if (parameter is null)
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' contains a null parameter.");
                }

                if (parameter.Name.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' contains a parameter without a name.");
                }

                if (!parameterNames.Add(parameter.Name))
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' has duplicate parameter '{parameter.Name}'.");
                }

                if (parameter.Type.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' parameter '{parameter.Name}' must have a type.");
                }

                if (parameter.Title.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' parameter '{parameter.Name}' must have a title.");
                }
            }
        }
    }

    public ServiceDefinition GetService(string id)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (!this.serviceIndex.TryGetValue(id, out var service))
        {
            throw new ServiceNotFoundException(id);
        }

        return service;
    }

    public bool TryGetService(string id, out ServiceDefinition service)
    {
        if (id is null)
        {
            service = null!;
            return false;
        }

        return this.serviceIndex.TryGetValue(id, out service!);
    }

    public bool TryGetLayer(string serviceId, string layerId, out LayerDefinition layer)
    {
        layer = null!;
        if (!this.TryGetService(serviceId, out var service))
        {
            return false;
        }

        var match = service.Layers.FirstOrDefault(l =>
            string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return false;
        }

        layer = match;
        return true;
    }

    public bool TryGetStyle(string styleId, out StyleDefinition style)
    {
        return this.styleIndex.TryGetValue(styleId, out style!);
    }

    public StyleDefinition GetStyle(string styleId)
    {
        if (this.TryGetStyle(styleId, out var style))
        {
            return style;
        }

        throw new StyleNotFoundException(styleId);
    }

    public bool TryGetLayerGroup(string serviceId, string layerGroupId, out LayerGroupDefinition layerGroup)
    {
        layerGroup = null!;
        if (!this.layerGroupIndex.TryGetValue(layerGroupId, out var group))
        {
            return false;
        }

        // Verify the layer group belongs to the specified service
        if (!string.Equals(group.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        layerGroup = group;
        return true;
    }

    public LayerGroupDefinition GetLayerGroup(string serviceId, string layerGroupId)
    {
        if (this.TryGetLayerGroup(serviceId, layerGroupId, out var layerGroup))
        {
            return layerGroup;
        }

        throw new InvalidDataException($"Layer group '{layerGroupId}' not found in service '{serviceId}'.");
    }

    private static void ValidateProtocolRequirements(
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<RasterDatasetDefinition> rasterDatasets)
    {
        // Group layers by service for validation
        var layersByService = layers.GroupBy(l => l.ServiceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g, StringComparer.OrdinalIgnoreCase);

        foreach (var service in services)
        {
            if (!layersByService.TryGetValue(service.Id, out var serviceLayers))
            {
                continue;
            }

            // Protocol-specific validation has been removed
            // ProtocolMetadataValidator was deleted as part of Configuration V2 migration
            // Validation is now handled by HCL schema validation
        }

        // Raster dataset protocol-specific validation has been removed
        // ProtocolMetadataValidator was deleted as part of Configuration V2 migration
        // Validation is now handled by HCL schema validation
    }
}
