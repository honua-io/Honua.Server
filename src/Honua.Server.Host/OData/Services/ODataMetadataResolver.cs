// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.OData.Services;

/// <summary>
/// Service responsible for resolving OData metadata from HTTP requests.
/// Handles entity set resolution, key extraction, and path segment parsing.
/// </summary>
public sealed class ODataMetadataResolver
{
    private const string MetadataHttpContextItem = "__honua_odata_metadata";

    private readonly ODataModelCache _modelCache;

    public ODataMetadataResolver(ODataModelCache modelCache)
    {
        _modelCache = Guard.NotNull(modelCache);
    }

    public async ValueTask<ODataEntityMetadata> ResolveMetadataAsync(
        HttpContext httpContext,
        object? path,
        CancellationToken cancellationToken = default)
    {
        if (httpContext.Items.TryGetValue(MetadataHttpContextItem, out var cached) &&
            cached is ODataEntityMetadata metadataFromCache)
        {
            return metadataFromCache;
        }

        var descriptor = await _modelCache
            .GetOrCreateAsync(cancellationToken)
            .ConfigureAwait(false);
        var segments = GetPathSegments(path);

        var entitySetSegment = segments.OfType<EntitySetSegment>().FirstOrDefault();
        if (entitySetSegment is null)
        {
            throw new InvalidOperationException("Unable to resolve entity set for the current OData request.");
        }

        if (!descriptor.TryGetByEntitySet(entitySetSegment.EntitySet.Name, out var metadata))
        {
            throw new InvalidOperationException($"Entity set '{entitySetSegment.EntitySet.Name}' was not found in the current OData model.");
        }

        httpContext.Items[MetadataHttpContextItem] = metadata;
        return metadata;
    }

    public (IEdmCollectionType CollectionType, IEdmEntityType EntityType) ResolveCollectionTypes(ODataEntityMetadata metadata)
    {
        if (metadata.EntitySet.Type is not IEdmCollectionType collectionType)
        {
            throw new InvalidOperationException($"Entity set '{metadata.EntitySet.Name}' is not a collection.");
        }

        return (collectionType, metadata.EntityType);
    }

    public string? ResolveKeyFromRoute(HttpRequest request, ODataEntityMetadata metadata, object? path)
    {
        var keySegment = GetPathSegments(path).OfType<KeySegment>().LastOrDefault();
        if (keySegment is not null)
        {
            var keyValue = keySegment.Keys.FirstOrDefault().Value;
            if (keyValue is not null)
            {
                return Convert.ToString(keyValue, CultureInfo.InvariantCulture);
            }
        }

        var routeValues = request.RouteValues;
        foreach (var key in metadata.EntityType.Key().Select(k => k.Name))
        {
            if (routeValues.TryGetValue(key, out var fromRoute) && fromRoute is not null)
            {
                return Convert.ToString(fromRoute, CultureInfo.InvariantCulture);
            }
        }

        if (routeValues.TryGetValue(metadata.Layer.IdField, out var idValue) && idValue is not null)
        {
            return Convert.ToString(idValue, CultureInfo.InvariantCulture);
        }

        return null;
    }

    public bool IsCountRequest(object? path)
    {
        return GetPathSegments(path).LastOrDefault() is CountSegment;
    }

    private static IReadOnlyList<ODataPathSegment> GetPathSegments(object? path)
    {
        if (path is null)
        {
            return Array.Empty<ODataPathSegment>();
        }

        if (path is IEnumerable<ODataPathSegment> enumerable)
        {
            return enumerable.ToArray();
        }

        var property = path.GetType().GetProperty("Segments");
        if (property?.GetValue(path) is System.Collections.IEnumerable segments)
        {
            return segments.OfType<ODataPathSegment>().ToArray();
        }

        throw new InvalidOperationException("Unable to resolve OData path segments for the current request.");
    }
}
