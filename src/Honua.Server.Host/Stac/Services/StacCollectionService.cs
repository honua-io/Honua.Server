// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Middleware;
using Honua.Server.Host.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Stac.Services;

/// <summary>
/// Service for STAC collection write operations (POST, PUT, PATCH, DELETE).
/// </summary>
public sealed class StacCollectionService
{
    private readonly IStacCatalogStore _store;
    private readonly IStacValidationService _validationService;
    private readonly ISecurityAuditLogger _auditLogger;
    private readonly StacMetrics _metrics;
    private readonly IOutputCacheInvalidationService _cacheInvalidation;
    private readonly StacParsingService _parsingService;
    private readonly ILogger<StacCollectionService> _logger;

    public StacCollectionService(
        IStacCatalogStore store,
        IStacValidationService validationService,
        ISecurityAuditLogger auditLogger,
        StacMetrics metrics,
        IOutputCacheInvalidationService cacheInvalidation,
        StacParsingService parsingService,
        ILogger<StacCollectionService> logger)
    {
        _store = Guard.NotNull(store);
        _validationService = Guard.NotNull(validationService);
        _auditLogger = Guard.NotNull(auditLogger);
        _metrics = Guard.NotNull(metrics);
        _cacheInvalidation = Guard.NotNull(cacheInvalidation);
        _parsingService = Guard.NotNull(parsingService);
        _logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Creates a new collection.
    /// </summary>
    public async Task<CollectionOperationResult> CreateCollectionAsync(
        System.Text.Json.Nodes.JsonObject collectionJson,
        string username,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<CollectionOperationResult>("STAC PostCollection")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.WriteOperationsCounter, _metrics.WriteOperationErrorsCounter, _metrics.WriteOperationDuration)
            .WithTag("stac.operation", "PostCollection")
            .WithTag("operation", "post")
            .WithTag("resource", "collection")
            .ExecuteAsync(async activity =>
            {
                // Validate the collection JSON
                var validationResult = _validationService.ValidateCollection(collectionJson);
                if (!validationResult.IsValid)
                {
                    _metrics.RecordWriteError("post", "collection", "validation_error");
                    var errorMessage = StacRequestHelpers.FormatValidationErrors(validationResult.Errors);
                    return CollectionOperationResult.ValidationError(errorMessage);
                }

                await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                // Parse and validate the collection
                if (!StacRequestHelpers.TryGetId(collectionJson, out var id))
                {
                    _metrics.RecordWriteError("post", "collection", "missing_id");
                    return CollectionOperationResult.ValidationError("Collection 'id' is required and must be a string.");
                }

                activity?.SetTag("stac.collection_id", id);

                // Check if collection already exists
                var existing = await _store.GetCollectionAsync(id, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    _metrics.RecordWriteError("post", "collection", "conflict");
                    return CollectionOperationResult.Conflict($"Collection '{id}' already exists. Use PUT or PATCH to update.");
                }

                // Create collection record from JSON
                StacCollectionRecord record;
                try
                {
                    record = _parsingService.ParseCollectionFromJson(collectionJson);
                }
                catch (InvalidOperationException ex)
                {
                    _metrics.RecordWriteError("post", "collection", "parse_error");
                    return CollectionOperationResult.ValidationError(ex.Message);
                }

                // Store the collection
                await _store.UpsertCollectionAsync(record, expectedETag: null, cancellationToken).ConfigureAwait(false);

                // Log audit event
                _auditLogger.LogDataAccess(username, "CREATE", "STAC_Collection", id, ipAddress);

                // Invalidate cache for collections
                await _cacheInvalidation.InvalidateStacCollectionCacheAsync(id, cancellationToken).ConfigureAwait(false);

                return CollectionOperationResult.Success(record);
            });
    }

    /// <summary>
    /// Updates or creates a collection.
    /// </summary>
    public async Task<CollectionOperationResult> UpsertCollectionAsync(
        string collectionId,
        System.Text.Json.Nodes.JsonObject collectionJson,
        string? ifMatch,
        string username,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<CollectionOperationResult>("STAC PutCollection")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.WriteOperationsCounter, _metrics.WriteOperationErrorsCounter, _metrics.WriteOperationDuration)
            .WithTag("stac.operation", "PutCollection")
            .WithTag("stac.collection_id", collectionId)
            .WithTag("operation", "put")
            .WithTag("resource", "collection")
            .ExecuteAsync(async activity =>
            {
                // Validate the collection JSON
                var validationResult = _validationService.ValidateCollection(collectionJson);
                if (!validationResult.IsValid)
                {
                    _metrics.RecordWriteError("put", "collection", "validation_error");
                    var errorMessage = StacRequestHelpers.FormatValidationErrors(validationResult.Errors);
                    return CollectionOperationResult.ValidationError(errorMessage);
                }

                await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                // Verify the ID in the path matches the ID in the body
                if (!StacRequestHelpers.TryGetId(collectionJson, out var id))
                {
                    _metrics.RecordWriteError("put", "collection", "missing_id");
                    return CollectionOperationResult.ValidationError("Collection 'id' in body is required and must be a string.");
                }

                if (!id.EqualsIgnoreCase(collectionId))
                {
                    _metrics.RecordWriteError("put", "collection", "id_mismatch");
                    return CollectionOperationResult.ValidationError("Collection 'id' in body must match the path parameter.");
                }

                // Create or replace the collection
                StacCollectionRecord record;
                try
                {
                    record = _parsingService.ParseCollectionFromJson(collectionJson);
                }
                catch (InvalidOperationException ex)
                {
                    _metrics.RecordWriteError("put", "collection", "parse_error");
                    return CollectionOperationResult.ValidationError(ex.Message);
                }

                await _store.UpsertCollectionAsync(record, expectedETag: ifMatch, cancellationToken).ConfigureAwait(false);

                // Log audit event
                _auditLogger.LogDataAccess(username, "UPDATE", "STAC_Collection", collectionId, ipAddress);

                // Invalidate cache for this collection
                await _cacheInvalidation.InvalidateStacCollectionCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

                return CollectionOperationResult.Success(record);
            });
    }

    /// <summary>
    /// Patches an existing collection.
    /// </summary>
    public async Task<CollectionOperationResult> PatchCollectionAsync(
        string collectionId,
        System.Text.Json.Nodes.JsonObject patchJson,
        CancellationToken cancellationToken)
    {
        await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Get existing collection
        var existing = await _store.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return CollectionOperationResult.NotFound($"Collection '{collectionId}' not found.");
        }

        // Merge the patch with existing collection
        StacCollectionRecord merged;
        try
        {
            merged = _parsingService.MergeCollectionPatch(existing, patchJson);
        }
        catch (InvalidOperationException ex)
        {
            return CollectionOperationResult.ValidationError(ex.Message);
        }

        await _store.UpsertCollectionAsync(merged, expectedETag: null, cancellationToken).ConfigureAwait(false);

        // Invalidate cache for this collection
        await _cacheInvalidation.InvalidateStacCollectionCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

        return CollectionOperationResult.Success(merged);
    }

    /// <summary>
    /// Deletes a collection.
    /// </summary>
    public async Task<bool> DeleteCollectionAsync(
        string collectionId,
        string username,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<bool>("STAC DeleteCollection")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.WriteOperationsCounter, _metrics.WriteOperationErrorsCounter, _metrics.WriteOperationDuration)
            .WithTag("stac.operation", "DeleteCollection")
            .WithTag("stac.collection_id", collectionId)
            .WithTag("operation", "delete")
            .WithTag("resource", "collection")
            .ExecuteAsync(async activity =>
            {
                await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                var deleted = await _store.DeleteCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
                if (!deleted)
                {
                    _metrics.RecordWriteError("delete", "collection", "not_found");
                    return false;
                }

                // Log audit event
                _auditLogger.LogDataAccess(username, "DELETE", "STAC_Collection", collectionId, ipAddress);

                // SECURITY FIX (Issue 38): Invalidate cache for this collection AND its items
                // When a collection is deleted, all its items are also removed, so we must invalidate
                // both collection and item caches, plus search results
                await _cacheInvalidation.InvalidateStacCollectionCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);
                await _cacheInvalidation.InvalidateStacItemsCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

                return true;
            });
    }

}

/// <summary>
/// Result of a collection operation.
/// </summary>
public sealed class CollectionOperationResult
{
    public bool IsSuccess { get; init; }
    public StacCollectionRecord? Record { get; init; }
    public string? ErrorMessage { get; init; }
    public OperationErrorType? ErrorType { get; init; }

    public static CollectionOperationResult Success(StacCollectionRecord record) =>
        new() { IsSuccess = true, Record = record };

    public static CollectionOperationResult ValidationError(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorType = OperationErrorType.Validation };

    public static CollectionOperationResult NotFound(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorType = OperationErrorType.NotFound };

    public static CollectionOperationResult Conflict(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorType = OperationErrorType.Conflict };
}

public enum OperationErrorType
{
    Validation,
    NotFound,
    Conflict
}
