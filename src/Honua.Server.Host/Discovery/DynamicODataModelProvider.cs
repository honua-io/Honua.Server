// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Discovery;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.OData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Honua.Server.Host.Discovery;

/// <summary>
/// Provides dynamically generated OData EDM models from auto-discovered tables.
/// Integrates with the existing DynamicEdmModelBuilder to create entity sets for discovered tables.
/// </summary>
public sealed class DynamicODataModelProvider
{
    private const string ModelNamespace = "Honua.Discovery";

    private readonly ITableDiscoveryService _discoveryService;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IODataFieldTypeMapper _typeMapper;
    private readonly AutoDiscoveryOptions _options;
    private readonly ILogger<DynamicODataModelProvider> _logger;

    public DynamicODataModelProvider(
        ITableDiscoveryService discoveryService,
        IMetadataRegistry metadataRegistry,
        IODataFieldTypeMapper typeMapper,
        IOptions<AutoDiscoveryOptions> options,
        ILogger<DynamicODataModelProvider> logger)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates service and layer definitions from discovered tables.
    /// These can be merged with configured metadata or used standalone.
    /// </summary>
    public async Task<(IReadOnlyList<ServiceDefinition> Services, IReadOnlyList<LayerDefinition> Layers)>
        GenerateMetadataFromDiscoveryAsync(
            string dataSourceId,
            CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.DiscoverPostGISTablesAsODataCollections)
        {
            return (Array.Empty<ServiceDefinition>(), Array.Empty<LayerDefinition>());
        }

        var tables = await _discoveryService.DiscoverTablesAsync(dataSourceId, cancellationToken);

        var services = new List<ServiceDefinition>();
        var layers = new List<LayerDefinition>();

        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken);

        // Get or create the discovery folder
        var folderId = _options.DefaultFolderId ?? "discovered";

        // Group tables by schema to create services
        var tablesBySchema = tables.GroupBy(t => t.Schema);

        foreach (var schemaGroup in tablesBySchema)
        {
            var schema = schemaGroup.Key;
            var serviceId = $"discovered_{schema}";
            var friendlyName = _options.UseFriendlyNames
                ? ToFriendlyName(schema)
                : schema;

            // Create service definition
            var service = new ServiceDefinition
            {
                Id = serviceId,
                Title = $"Auto-discovered: {friendlyName}",
                FolderId = folderId,
                ServiceType = "feature",
                DataSourceId = dataSourceId,
                Enabled = true,
                Description = $"Automatically discovered tables from schema '{schema}'",
                Keywords = new[] { "auto-discovered", schema }.ToArray(),
                Ogc = new OgcServiceDefinition
                {
                    CollectionsEnabled = _options.DiscoverPostGISTablesAsOgcCollections,
                    ItemLimit = 1000
                }
            };

            services.Add(service);

            // Create layer for each table
            foreach (var table in schemaGroup)
            {
                var layer = CreateLayerFromTable(serviceId, table);
                layers.Add(layer);
            }
        }

        _logger.LogInformation(
            "Generated {ServiceCount} services and {LayerCount} layers from discovery",
            services.Count, layers.Count);

        return (services, layers);
    }

    /// <summary>
    /// Builds an OData EDM model descriptor from discovered tables.
    /// </summary>
    public async Task<ODataModelDescriptor> BuildModelFromDiscoveryAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        var (services, layers) = await GenerateMetadataFromDiscoveryAsync(dataSourceId, cancellationToken);

        if (!services.Any())
        {
            throw new InvalidOperationException("No discovered tables available to build OData model");
        }

        // Build EDM model
        var model = new EdmModel();
        var container = new EdmEntityContainer(ModelNamespace, "DiscoveredContainer");
        model.AddElement(container);

        var entityMetadata = new List<ODataEntityMetadata>();

        foreach (var service in services)
        {
            var serviceLayers = layers.Where(l => l.ServiceId == service.Id);

            foreach (var layer in serviceLayers)
            {
                var entitySetName = GetEntitySetName(layer);
                var entityTypeName = $"{entitySetName}_Entity";

                var (entityType, geometryShadow) = CreateEntityType(model, layer, entityTypeName);
                var entitySet = container.AddEntitySet(entitySetName, entityType);

                entityMetadata.Add(new ODataEntityMetadata(
                    entitySetName,
                    entityTypeName,
                    service,
                    layer,
                    entitySet,
                    entityType,
                    geometryShadow));

                _logger.LogDebug(
                    "Created OData entity set {EntitySet} for discovered table {Schema}.{Table}",
                    entitySetName,
                    layer.Storage?.Table?.Split('.')[0],
                    layer.Storage?.Table?.Split('.')[1]);
            }
        }

        return new ODataModelDescriptor(model, entityMetadata);
    }

    private LayerDefinition CreateLayerFromTable(string serviceId, DiscoveredTable table)
    {
        var layerId = $"{table.Schema}_{table.TableName}";
        var friendlyName = _options.UseFriendlyNames
            ? ToFriendlyName(table.TableName)
            : table.TableName;

        // Map discovered columns to field definitions
        var fields = table.Columns.Values.Select(col => new FieldDefinition
        {
            Name = col.Name,
            Alias = col.Alias ?? col.Name,
            DataType = col.DataType,
            StorageType = col.StorageType,
            Nullable = col.IsNullable,
            Editable = true
        }).ToArray();

        // Determine CRS
        var crs = new List<string>
        {
            $"http://www.opengis.net/def/crs/EPSG/0/{table.SRID}",
            "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
        };

        // Create extent if available
        LayerExtentDefinition? extent = null;
        if (table.Extent != null)
        {
            extent = new LayerExtentDefinition
            {
                Bbox = new[] { table.Extent.ToArray() },
                Crs = crs[0]
            };
        }

        return new LayerDefinition
        {
            Id = layerId,
            ServiceId = serviceId,
            Title = friendlyName,
            Description = table.Description ?? $"Auto-discovered table from {table.QualifiedName}",
            GeometryType = NormalizeGeometryType(table.GeometryType),
            IdField = table.PrimaryKeyColumn,
            DisplayField = table.PrimaryKeyColumn,
            GeometryField = table.GeometryColumn,
            Crs = crs,
            Extent = extent,
            Keywords = new[] { "auto-discovered", table.Schema, table.TableName }.ToArray(),
            ItemType = "feature",
            Fields = fields,
            Storage = new LayerStorageDefinition
            {
                Table = table.QualifiedName,
                GeometryColumn = table.GeometryColumn,
                PrimaryKey = table.PrimaryKeyColumn,
                Srid = table.SRID
            },
            Query = new LayerQueryDefinition
            {
                MaxRecordCount = 1000
            }
        };
    }

    private (IEdmEntityType EntityType, string? GeometryShadowProperty) CreateEntityType(
        EdmModel model,
        LayerDefinition layer,
        string entityTypeName)
    {
        var entityType = new EdmEntityType(ModelNamespace, entityTypeName, baseType: null, isAbstract: false, isOpen: true);

        // Add primary key
        var keyType = _typeMapper.GetKeyType(layer);
        var keyProperty = entityType.AddStructuralProperty(layer.IdField, keyType);
        entityType.AddKeys(keyProperty);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { layer.IdField };

        // Add geometry property
        string? geometryShadow = null;
        if (!string.IsNullOrWhiteSpace(layer.GeometryField))
        {
            var geometryType = _typeMapper.GetGeometryType(layer);
            entityType.AddStructuralProperty(layer.GeometryField, geometryType);
            seen.Add(layer.GeometryField);

            // Add WKT shadow property for compatibility
            var wktName = $"{layer.GeometryField}_wkt";
            if (!seen.Contains(wktName))
            {
                entityType.AddStructuralProperty(wktName, EdmCoreModel.Instance.GetString(isNullable: true));
                geometryShadow = wktName;
            }
        }

        // Add field properties
        foreach (var field in layer.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name) || !seen.Add(field.Name))
            {
                continue;
            }

            if (_typeMapper.TryGetPrimitiveType(field, out var typeReference))
            {
                entityType.AddStructuralProperty(field.Name, typeReference);
            }
        }

        model.AddElement(entityType);
        return (entityType, geometryShadow);
    }

    private static string GetEntitySetName(LayerDefinition layer)
    {
        // Use table name as entity set name, sanitized
        var tableName = layer.Storage?.Table ?? layer.Id;
        var parts = tableName.Split('.');
        var name = parts.Length > 1 ? parts[1] : parts[0];

        // Sanitize for OData
        return SanitizeIdentifier(name);
    }

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Entity";
        }

        var result = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            result.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        // Ensure starts with letter
        if (result.Length > 0 && !char.IsLetter(result[0]))
        {
            result.Insert(0, 'E');
        }

        return result.Length > 0 ? result.ToString() : "Entity";
    }

    private static string ToFriendlyName(string name)
    {
        // Convert snake_case or PascalCase to "Friendly Name"
        return name
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant())
            .Aggregate((a, b) => $"{a} {b}");
    }

    private static string NormalizeGeometryType(string geometryType)
    {
        // PostGIS returns types like "POINT", "LINESTRING", "POLYGON"
        // Normalize to lowercase singular forms expected by Honua
        return geometryType.ToLowerInvariant() switch
        {
            "point" => "point",
            "linestring" or "line" => "polyline",
            "polygon" => "polygon",
            "multipoint" => "multipoint",
            "multilinestring" or "multiline" => "polyline",
            "multipolygon" => "polygon",
            "geometrycollection" => "geometrycollection",
            _ => "geometry"
        };
    }
}
