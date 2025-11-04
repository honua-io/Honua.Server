// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.OData;

public sealed class DynamicEdmModelBuilder
{
    private const string ModelNamespace = "Honua.OData";
    private const string ContainerName = "HonuaContainer";

    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IODataFieldTypeMapper _typeMapper;
    private readonly IHonuaConfigurationService _configurationService;
    private readonly ILogger<DynamicEdmModelBuilder> _logger;

    public DynamicEdmModelBuilder(
        IMetadataRegistry metadataRegistry,
        IODataFieldTypeMapper typeMapper,
        IHonuaConfigurationService configurationService,
        ILogger<DynamicEdmModelBuilder> logger)
    {
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _typeMapper = Guard.NotNull(typeMapper);
        _configurationService = Guard.NotNull(configurationService);
        _logger = Guard.NotNull(logger);
    }

    public async Task<ODataModelDescriptor> BuildAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _metadataRegistry
            .GetSnapshotAsync(cancellationToken)
            .ConfigureAwait(false);
        var model = new EdmModel();
        var container = new EdmEntityContainer(ModelNamespace, ContainerName);
        model.AddElement(container);

        var entitySetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entityTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entityMetadata = new List<ODataEntityMetadata>();

        foreach (var service in snapshot.Services.Where(IsFeatureService))
        {
            foreach (var layer in service.Layers.Where(IsFeatureLayer))
            {
                var entitySetName = GenerateUniqueName($"{service.Id}_{layer.Id}", entitySetNames);
                var entityTypeName = GenerateUniqueName($"{entitySetName}_Entity", entityTypeNames);

                var (entityType, geometryShadow) = CreateEntityType(model, layer, entityTypeName);
                var entitySet = container.AddEntitySet(entitySetName, entityType);

                model.SetAnnotationValue(entityType, new ClrTypeAnnotation(typeof(EdmEntityObject)));
                model.SetAnnotationValue(entitySet, new ClrTypeAnnotation(typeof(EdmEntityObject)));

                entityMetadata.Add(new ODataEntityMetadata(entitySetName, entityTypeName, service, layer, entitySet, entityType, geometryShadow));

                _logger.LogInformation(
                    "Created OData entity set {EntitySet} for service {ServiceId} layer {LayerId}",
                    entitySetName,
                    service.Id,
                    layer.Id);
            }
        }

        if (entityMetadata.Count == 0)
        {
            _logger.LogWarning("No feature layers were available to build the OData EDM model. OData endpoints will not be functional until layers are added.");
        }

        return new ODataModelDescriptor(model, entityMetadata);
    }

    private (IEdmEntityType EntityType, string? GeometryShadowProperty) CreateEntityType(EdmModel model, LayerDefinition layer, string entityTypeName)
    {
        var entityType = new EdmEntityType(ModelNamespace, entityTypeName, baseType: null, isAbstract: false, isOpen: true);

        var keyType = _typeMapper.GetKeyType(layer);
        var keyProperty = entityType.AddStructuralProperty(layer.IdField, keyType);
        entityType.AddKeys(keyProperty);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            layer.IdField
        };

        string? geometryShadow = null;
        if (layer.GeometryField.HasValue())
        {
            var geometryType = _typeMapper.GetGeometryType(layer);
            entityType.AddStructuralProperty(layer.GeometryField, geometryType);
            seen.Add(layer.GeometryField);

            if (GetODataConfiguration().EmitWktShadowProperties)
            {
                var wktName = GenerateUniqueName($"{layer.GeometryField}_wkt", seen);
                entityType.AddStructuralProperty(wktName, EdmCoreModel.Instance.GetString(isNullable: true));
                geometryShadow = wktName;
            }
        }

        var fields = FieldMetadataResolver.ResolveFields(layer, includeGeometry: false, includeIdField: true);
        foreach (var field in fields)
        {
            if (field.Name.IsNullOrWhiteSpace())
            {
                continue;
            }

            if (!seen.Add(field.Name))
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

    private static bool IsFeatureService(ServiceDefinition service) =>
        service.Enabled &&
        string.Equals(service.ServiceType, "feature", StringComparison.OrdinalIgnoreCase);

    private static bool IsFeatureLayer(LayerDefinition layer) =>
        string.Equals(layer.ItemType, "feature", StringComparison.OrdinalIgnoreCase);

    private static string GenerateUniqueName(string source, ISet<string> allocated)
    {
        var baseName = SanitizeIdentifier(source);
        var candidate = baseName;
        var suffix = 1;

        while (!allocated.Add(candidate))
        {
            candidate = $"{baseName}_{suffix++}";
        }

        return candidate;
    }

    private static string SanitizeIdentifier(string value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return "Entity";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        while (builder.Length > 0 && !char.IsLetter(builder[0]))
        {
            builder.Insert(0, 'E');
        }

        if (builder.Length == 0)
        {
            builder.Append('E');
        }

        return builder.ToString();
    }

    private ODataConfiguration GetODataConfiguration() => _configurationService.Current.Services.OData;
}
