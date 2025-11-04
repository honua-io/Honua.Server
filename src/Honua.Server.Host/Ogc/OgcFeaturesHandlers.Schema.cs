// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcFeaturesHandlers
{
    /// <summary>
    /// Gets queryables (schema) for a collection.
    /// OGC API - Features /collections/{collectionId}/queryables endpoint.
    /// Returns JSON Schema describing the queryable properties of features in the collection.
    /// </summary>
    public static async Task<IResult> GetCollectionQueryables(
        string collectionId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken)
    {
        var (context, error) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        var layer = context!.Layer;
        var schema = OgcSharedHandlers.BuildQueryablesSchema(layer);
        return Results.Ok(schema);
    }
}
