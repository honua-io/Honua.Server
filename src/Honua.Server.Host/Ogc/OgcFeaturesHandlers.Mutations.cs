// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Query;
using Honua.Server.Core.Results;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcFeaturesHandlers
{
    /// <summary>
    /// Creates new features in a collection via POST.
    /// OGC API - Features /collections/{collectionId}/items endpoint (POST).
    /// </summary>
    public static async Task<IResult> PostCollectionItems(
        string collectionId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IFeatureEditOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(repository);
        Guard.NotNull(orchestrator);

        var (context, contextError) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (contextError is not null)
        {
            return contextError;
        }
        var layer = context.Layer;

        using var document = await OgcSharedHandlers.ParseJsonDocumentAsync(request, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return OgcSharedHandlers.CreateValidationProblem("Request body must contain a valid GeoJSON feature or FeatureCollection.", "body");
        }

        // Process features lazily without materializing entire list
        var commands = new List<FeatureEditCommand>();
        var fallbackIds = new List<string?>();

        foreach (var featureElement in OgcSharedHandlers.EnumerateGeoJsonFeatures(document.RootElement))
        {
            if (featureElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var attributes = OgcSharedHandlers.ReadGeoJsonAttributes(featureElement, layer, removeId: false, out var fallbackId);
            if (attributes.Count == 0 && fallbackId is null)
            {
                continue;
            }

            commands.Add(new AddFeatureCommand(context.Service.Id, layer.Id, attributes));
            fallbackIds.Add(fallbackId);
        }

        if (commands.Count == 0)
        {
            return OgcSharedHandlers.CreateValidationProblem("No features were supplied.", "features");
        }

        var batch = OgcSharedHandlers.CreateFeatureEditBatch(commands, request);
        var editResult = await orchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        if (editResult.Results.Count != commands.Count)
        {
            return Results.Problem("Unexpected response from edit pipeline.", statusCode: StatusCodes.Status500InternalServerError, title: "Feature edit failed");
        }

        var failure = editResult.Results.FirstOrDefault(result => !result.Success);
        if (failure is not null)
        {
            return OgcSharedHandlers.CreateEditFailureProblem(failure.Error, StatusCodes.Status400BadRequest);
        }

        var featureQuery = new FeatureQuery(ResultType: FeatureResultType.Results);
        var created = await OgcSharedHandlers.FetchCreatedFeaturesWithETags(
            repository,
            context,
            layer,
            collectionId,
            editResult,
            fallbackIds,
            featureQuery,
            request,
            cancellationToken).ConfigureAwait(false);

        return OgcSharedHandlers.BuildMutationResponse(created, collectionId, singleItemMode: true);
    }

    /// <summary>
    /// Updates a feature via PUT (full replacement).
    /// OGC API - Features /collections/{collectionId}/items/{featureId} endpoint (PUT).
    /// Requires If-Match header for optimistic concurrency control.
    /// </summary>
    public static async Task<IResult> PutCollectionItem(
        string collectionId,
        string featureId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IFeatureEditOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var (context, contextError) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (contextError is not null)
        {
            return contextError;
        }
        var layer = context.Layer;

        using var document = await OgcSharedHandlers.ParseJsonDocumentAsync(request, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return OgcSharedHandlers.CreateValidationProblem("Request body must contain a valid GeoJSON feature.", "body");
        }

        var featureElement = document.RootElement;
        if (featureElement.ValueKind == JsonValueKind.Object &&
            featureElement.TryGetProperty("type", out var typeElement) &&
            string.Equals(typeElement.GetString(), "FeatureCollection", StringComparison.OrdinalIgnoreCase))
        {
            return OgcSharedHandlers.CreateValidationProblem("FeatureCollection payloads are not supported for PUT operations.", "type");
        }

        var attributes = OgcSharedHandlers.ReadGeoJsonAttributes(featureElement, layer, removeId: true, out var bodyFeatureId);
        if (attributes.Count == 0 && !featureElement.TryGetProperty("geometry", out _))
        {
            return OgcSharedHandlers.CreateValidationProblem("Feature update must contain properties or geometry.", "feature");
        }

        if (bodyFeatureId.HasValue() && !string.Equals(bodyFeatureId, featureId, StringComparison.OrdinalIgnoreCase))
        {
            return OgcSharedHandlers.CreateValidationProblem("Feature id in payload does not match request path.", "id");
        }

        var featureQuery = new FeatureQuery(ResultType: FeatureResultType.Results);
        var existingRecord = await repository.GetAsync(context.Service.Id, layer.Id, featureId, featureQuery, cancellationToken).ConfigureAwait(false);

        // OPTIMISTIC CONCURRENCY CONTROL: Enforce If-Match header for updates
        if (existingRecord is null)
        {
            // Feature doesn't exist - return 404
            return OgcSharedHandlers.CreateNotFoundProblem($"Feature '{featureId}' was not found in collection '{collectionId}'.");
        }

        if (!OgcSharedHandlers.ValidateIfMatch(request, layer, existingRecord, out var currentEtag))
        {
            // If-Match header provided but doesn't match - return 412 Precondition Failed
            return Results.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        // Require If-Match header for all PUT operations to prevent lost updates
        if (!request.Headers.ContainsKey(HeaderNames.IfMatch))
        {
            // No If-Match header provided - return 428 Precondition Required
            var response428 = Results.StatusCode(StatusCodes.Status428PreconditionRequired);
            response428 = OgcSharedHandlers.WithResponseHeader(response428, HeaderNames.ETag, currentEtag);
            return response428;
        }

        // Create update command with version from existing record for optimistic locking
        var command = new UpdateFeatureCommand(context.Service.Id, layer.Id, featureId, attributes, existingRecord.Version);
        var batch = OgcSharedHandlers.CreateFeatureEditBatch(new[] { command }, request);

        try
        {
            var editResult = await orchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            var result = editResult.Results.FirstOrDefault();
            if (result is null)
            {
                return Results.Problem("Unexpected response from edit pipeline.", statusCode: StatusCodes.Status500InternalServerError, title: "Feature edit failed");
            }

            if (!result.Success)
            {
                return OgcSharedHandlers.CreateEditFailureProblem(result.Error, string.Equals(result.Error?.Code, "not_found", StringComparison.OrdinalIgnoreCase) ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
            }
        }
        catch (Core.Exceptions.ConcurrencyException)
        {
            // Concurrent modification detected - return 409 Conflict with current ETag
            var conflictRecord = await repository.GetAsync(context.Service.Id, layer.Id, featureId, featureQuery, cancellationToken).ConfigureAwait(false);
            if (conflictRecord is not null)
            {
                var conflictEtag = OgcSharedHandlers.ComputeFeatureEtag(layer, conflictRecord);
                var conflictResponse = Results.StatusCode(StatusCodes.Status409Conflict);
                conflictResponse = OgcSharedHandlers.WithResponseHeader(conflictResponse, HeaderNames.ETag, conflictEtag);
                return conflictResponse;
            }
            // Feature was deleted - return 404
            return OgcSharedHandlers.CreateNotFoundProblem($"Feature '{featureId}' was not found in collection '{collectionId}'.");
        }

        var record = await repository.GetAsync(context.Service.Id, layer.Id, featureId, featureQuery, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return Results.NoContent();
        }

        var feature = OgcSharedHandlers.ToFeature(request, collectionId, layer, record, featureQuery);
        var etag = OgcSharedHandlers.ComputeFeatureEtag(layer, record);
        var response = Results.Ok(feature);
        return OgcSharedHandlers.WithResponseHeader(response, HeaderNames.ETag, etag);
    }

    /// <summary>
    /// Updates a feature via PATCH (partial update).
    /// OGC API - Features /collections/{collectionId}/items/{featureId} endpoint (PATCH).
    /// Requires If-Match header for optimistic concurrency control.
    /// </summary>
    public static async Task<IResult> PatchCollectionItem(
        string collectionId,
        string featureId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IFeatureEditOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var (context, contextError) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (contextError is not null)
        {
            return contextError;
        }
        var layer = context.Layer;

        using var document = await OgcSharedHandlers.ParseJsonDocumentAsync(request, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return OgcSharedHandlers.CreateValidationProblem("Request body must contain a valid GeoJSON feature.", "body");
        }

        var featureElement = document.RootElement;
        if (featureElement.ValueKind == JsonValueKind.Object &&
            featureElement.TryGetProperty("type", out var typeElement) &&
            string.Equals(typeElement.GetString(), "FeatureCollection", StringComparison.OrdinalIgnoreCase))
        {
            return OgcSharedHandlers.CreateValidationProblem("FeatureCollection payloads are not supported for PATCH operations.", "type");
        }

        var attributes = OgcSharedHandlers.ReadGeoJsonAttributes(featureElement, layer, removeId: true, out var bodyFeatureId);
        if (attributes.Count == 0 && !featureElement.TryGetProperty("geometry", out _))
        {
            return OgcSharedHandlers.CreateValidationProblem("Feature update must contain properties or geometry.", "feature");
        }

        if (bodyFeatureId.HasValue() && !string.Equals(bodyFeatureId, featureId, StringComparison.OrdinalIgnoreCase))
        {
            return OgcSharedHandlers.CreateValidationProblem("Feature id in payload does not match request path.", "id");
        }

        var featureQuery = new FeatureQuery(ResultType: FeatureResultType.Results);
        var existingRecord = await repository.GetAsync(context.Service.Id, layer.Id, featureId, featureQuery, cancellationToken).ConfigureAwait(false);

        // OPTIMISTIC CONCURRENCY CONTROL: Enforce If-Match header for updates
        if (existingRecord is null)
        {
            // Feature doesn't exist - return 404
            return OgcSharedHandlers.CreateNotFoundProblem($"Feature '{featureId}' was not found in collection '{collectionId}'.");
        }

        if (!OgcSharedHandlers.ValidateIfMatch(request, layer, existingRecord, out var currentEtag))
        {
            // If-Match header provided but doesn't match - return 412 Precondition Failed
            return Results.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        // Require If-Match header for all PATCH operations to prevent lost updates
        if (!request.Headers.ContainsKey(HeaderNames.IfMatch))
        {
            // No If-Match header provided - return 428 Precondition Required
            var response428 = Results.StatusCode(StatusCodes.Status428PreconditionRequired);
            response428 = OgcSharedHandlers.WithResponseHeader(response428, HeaderNames.ETag, currentEtag);
            return response428;
        }

        // Create update command with version from existing record for optimistic locking
        var command = new UpdateFeatureCommand(context.Service.Id, layer.Id, featureId, attributes, existingRecord.Version);
        var batch = OgcSharedHandlers.CreateFeatureEditBatch(new[] { command }, request);

        try
        {
            var editResult = await orchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            var result = editResult.Results.FirstOrDefault();
            if (result is null)
            {
                return Results.Problem("Unexpected response from edit pipeline.", statusCode: StatusCodes.Status500InternalServerError, title: "Feature edit failed");
            }

            if (!result.Success)
            {
                return OgcSharedHandlers.CreateEditFailureProblem(result.Error, string.Equals(result.Error?.Code, "not_found", StringComparison.OrdinalIgnoreCase) ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
            }
        }
        catch (Core.Exceptions.ConcurrencyException)
        {
            // Concurrent modification detected - return 409 Conflict with current ETag
            var conflictRecord = await repository.GetAsync(context.Service.Id, layer.Id, featureId, featureQuery, cancellationToken).ConfigureAwait(false);
            if (conflictRecord is not null)
            {
                var conflictEtag = OgcSharedHandlers.ComputeFeatureEtag(layer, conflictRecord);
                var conflictResponse = Results.StatusCode(StatusCodes.Status409Conflict);
                conflictResponse = OgcSharedHandlers.WithResponseHeader(conflictResponse, HeaderNames.ETag, conflictEtag);
                return conflictResponse;
            }
            // Feature was deleted - return 404
            return OgcSharedHandlers.CreateNotFoundProblem($"Feature '{featureId}' was not found in collection '{collectionId}'.");
        }

        var record = await repository.GetAsync(context.Service.Id, layer.Id, featureId, featureQuery, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return Results.NoContent();
        }

        var feature = OgcSharedHandlers.ToFeature(request, collectionId, layer, record, featureQuery);
        var etag = OgcSharedHandlers.ComputeFeatureEtag(layer, record);
        var response = Results.Ok(feature);
        return OgcSharedHandlers.WithResponseHeader(response, HeaderNames.ETag, etag);
    }

    /// <summary>
    /// Deletes a feature from a collection.
    /// OGC API - Features /collections/{collectionId}/items/{featureId} endpoint (DELETE).
    /// Supports If-Match header for optimistic concurrency control.
    /// </summary>
    public static async Task<IResult> DeleteCollectionItem(
        string collectionId,
        string featureId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IFeatureEditOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var (context, contextError) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (contextError is not null)
        {
            return contextError;
        }
        var featureQuery = new FeatureQuery(ResultType: FeatureResultType.Results);
        var existingRecord = await repository.GetAsync(context.Service.Id, context.Layer.Id, featureId, featureQuery, cancellationToken).ConfigureAwait(false);
        if (existingRecord is not null && !OgcSharedHandlers.ValidateIfMatch(request, context.Layer, existingRecord, out _))
        {
            return Results.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var command = new DeleteFeatureCommand(context.Service.Id, context.Layer.Id, featureId);
        var batch = OgcSharedHandlers.CreateFeatureEditBatch(new[] { command }, request);
        var editResult = await orchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        var result = editResult.Results.FirstOrDefault();
        if (result is null)
        {
            return Results.Problem("Unexpected response from edit pipeline.", statusCode: StatusCodes.Status500InternalServerError, title: "Feature edit failed");
        }

        if (!result.Success)
        {
            return OgcSharedHandlers.CreateEditFailureProblem(result.Error, string.Equals(result.Error?.Code, "not_found", StringComparison.OrdinalIgnoreCase) ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }
}
