// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Host.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Query;
using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.OData.Services;

/// <summary>
/// Service responsible for CRUD operations on OData entities.
/// Handles entity creation, mapping between EdmEntityObject and FeatureRecord.
/// </summary>
public sealed class ODataEntityService
{
    private readonly ODataGeometryService _geometryService;
    private readonly ODataConverterService _converterService;
    private readonly ILogger<ODataEntityService> _logger;

    public ODataEntityService(
        ODataGeometryService geometryService,
        ODataConverterService converterService,
        ILogger<ODataEntityService> logger)
    {
        _geometryService = Guard.NotNull(geometryService);
        _converterService = Guard.NotNull(converterService);
        _logger = Guard.NotNull(logger);
    }

    public EdmEntityObject CreateEntity(
        ODataEntityMetadata metadata,
        IEdmEntityType entityType,
        FeatureRecord record,
        ODataQueryOptions? queryOptions = null)
    {
        var entity = new EdmEntityObject(entityType);
        var geometryField = metadata.Layer.GeometryField;
        var geometryShadow = metadata.GeometryShadowProperty;
        var storageSrid = metadata.Layer.Storage?.Srid;

        var projection = BuildProjection(queryOptions, metadata, geometryField, geometryShadow);

        record.Attributes.TryGetValue(geometryField, out var rawGeometry);
        var wkt = _geometryService.ComputeWkt(rawGeometry);

        foreach (var property in entityType.StructuralProperties())
        {
            var name = property.Name;

            if (projection is not null && !projection.Contains(name))
            {
                continue;
            }

            if (geometryField.HasValue() &&
                string.Equals(name, geometryField, StringComparison.OrdinalIgnoreCase))
            {
                if (property.Type.PrimitiveKind() == EdmPrimitiveTypeKind.String)
                {
                    entity.TrySetPropertyValue(name, wkt ?? _converterService.ConvertOutgoingScalar(rawGeometry));
                    continue;
                }

                var spatialValue = _geometryService.ConvertGeometryToSpatial(rawGeometry, storageSrid);
                entity.TrySetPropertyValue(name, spatialValue ?? _converterService.ConvertOutgoingScalar(rawGeometry));
                continue;
            }

            if (geometryShadow is not null &&
                string.Equals(name, geometryShadow, StringComparison.OrdinalIgnoreCase))
            {
                entity.TrySetPropertyValue(name, wkt);
                continue;
            }

            if (record.Attributes.TryGetValue(name, out var attributeValue))
            {
                var value = _converterService.ConvertPropertyValue(property, attributeValue);
                entity.TrySetPropertyValue(name, value);
            }
        }

        return entity;
    }

    public FeatureRecord CreateRecord(
        ODataEntityMetadata metadata,
        IEdmEntityType entityType,
        IEdmEntityObject entity,
        IReadOnlyCollection<string>? changedProperties,
        bool includeKey)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var geometryField = metadata.Layer.GeometryField;
        var geometryShadow = metadata.GeometryShadowProperty;
        object? shadowGeometry = null;

        var propertyFilter = BuildPropertyFilter(changedProperties, metadata, geometryShadow, geometryField);

        foreach (var property in entityType.StructuralProperties())
        {
            var name = property.Name;

            if (!includeKey && string.Equals(name, metadata.Layer.IdField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (propertyFilter is not null && !propertyFilter.Contains(name))
            {
                continue;
            }

            if (geometryShadow is not null && string.Equals(name, geometryShadow, StringComparison.OrdinalIgnoreCase))
            {
                if (entity.TryGetPropertyValue(name, out var shadowValue))
                {
                    shadowGeometry = shadowValue;
                }

                continue;
            }

            if (!entity.TryGetPropertyValue(name, out var value))
            {
                continue;
            }

            if (geometryField.HasValue() &&
                string.Equals(name, geometryField, StringComparison.OrdinalIgnoreCase))
            {
                attributes[name] = _converterService.NormalizeIncomingGeometry(value);
                continue;
            }

            attributes[name] = _converterService.ConvertIncomingValue(value);
        }

        if (geometryField.HasValue() &&
            !attributes.ContainsKey(geometryField) &&
            shadowGeometry is not null)
        {
            attributes[geometryField] = _converterService.NormalizeIncomingGeometry(shadowGeometry);
        }

        return new FeatureRecord(attributes);
    }

    private static HashSet<string>? BuildProjection(
        ODataQueryOptions? queryOptions,
        ODataEntityMetadata metadata,
        string geometryField,
        string? geometryShadow)
    {
        var rawSelect = queryOptions?.SelectExpand?.RawSelect;
        if (rawSelect.IsNullOrWhiteSpace())
        {
            return null;
        }

        var projection = rawSelect
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        projection.Add(metadata.Layer.IdField);

        if (geometryField.HasValue())
        {
            projection.Add(geometryField);
        }

        if (geometryShadow.HasValue())
        {
            projection.Add(geometryShadow);
        }

        return projection;
    }

    private static HashSet<string>? BuildPropertyFilter(
        IReadOnlyCollection<string>? changedProperties,
        ODataEntityMetadata metadata,
        string? geometryShadow,
        string geometryField)
    {
        if (changedProperties is not { Count: > 0 })
        {
            return null;
        }

        var propertyFilter = new HashSet<string>(changedProperties, StringComparer.OrdinalIgnoreCase);
        propertyFilter.Add(metadata.Layer.IdField);

        if (geometryShadow.HasValue() && propertyFilter.Contains(geometryShadow) && geometryField.HasValue())
        {
            propertyFilter.Add(geometryField);
        }

        return propertyFilter;
    }
}
