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
    private readonly IStacCatalogStore store;
    private readonly IStacValidationService validationService;
    private readonly ISecurityAuditLogger auditLogger;
    private readonly StacMetrics metrics;
    private readonly IOutputCacheInvalidationService cacheInvalidation;
    private readonly StacParsingService parsingService;
    private readonly ILogger<StacCollectionService> logger;

    public StacCollectionService(
        IStacCatalogStore store,
        IStacValidationService validationService,
        ISecurityAuditLogger auditLogger,
        StacMetrics metrics,
        IOutputCacheInvalidationService cacheInvalidation,
        StacParsingService parsingService,
        ILogger<StacCollectionService> logger)
    {
        this.store = Guard.NotNull(store);
        this.validationService = Guard.NotNull(validationService);
        this.auditLogger = Guard.NotNull(auditLogger);
        this.metrics = Guard.NotNull(metrics);
        this.cacheInvalidation = Guard.NotNull(cacheInvalidation);
        this.parsingService = Guard.NotNull(parsingService);
        this.logger = Guard.NotNull(logger);
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
            .WithLogger(this.logger)
            .WithMetrics(this.metrics.WriteOperationsCounter, this.metrics.WriteOperationErrorsCounter, this.metrics.WriteOperationDuration)
            .WithTag("stac.operation", "PostCollection")
            .WithTag("operation", "post")
            .WithTag("resource", "collection")
            .ExecuteAsync(async activity =>
            {
                // Validate the collection JSON
                var validationResult = this.validationService.ValidateCollection(collectionJson);
                if (!validationResult.IsValid)
                {
                    this.metrics.RecordWriteError("post", "collection", "validation_error");
                    var errorMessage = StacRequestHelpers.FormatValidationErrors(validationResult.Errors);
                    return CollectionOperationResult.ValidationError(errorMessage);
                }

                await this.store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                // Parse and validate the collection
                if (!StacRequestHelpers.TryGetId(collectionJson, out var id))
                {
                    this.metrics.RecordWriteError("post", "collection", "missing_id");
                    return CollectionOperationResult.ValidationError("Collection 'id' is required and must be a string.");
                }

                activity?.SetTag("stac.collection_id", id);

                // Check if collection already exists
                var existing = await this.store.GetCollectionAsync(id, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    this.metrics.RecordWriteError("post", "collection", "conflict");
                    return CollectionOperationResult.Conflict($"Collection '{id}' already exists. Use PUT or PATCH to update.");
                }

                // Create collection record from JSON
                StacCollectionRecord record;
                try
                {
                    record = this.parsingService.ParseCollectionFromJson(collectionJson);
                }
                catch (InvalidOperationException ex)
                {
                    this.metrics.RecordWriteError("post", "collection", "parse_error");
                    return CollectionOperationResult.ValidationError(ex.Message);
                }

                // Store the collection
                await this.store.UpsertCollectionAsync(record, expectedETag: null, cancellationToken).ConfigureAwait(false);

                // Log audit event
                this.auditLogger.LogDataAccess(username, "CREATE", "STAC_Collection", id, ipAddress);

                // Invalidate cache for collections
                await this.cacheInvalidation.InvalidateStacCollectionCacheAsync(id, cancellationToken).ConfigureAwait(false);

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
            .WithLogger(this.logger)
            .WithMetrics(this.metrics.WriteOperationsCounter, this.metrics.WriteOperationErrorsCounter, this.metrics.WriteOperationDuration)
            .WithTag("stac.operation", "PutCollection")
            .WithTag("stac.collection_id", collectionId)
            .WithTag("operation", "put")
            .WithTag("resource", "collection")
            .ExecuteAsync(async activity =>
            {
                // Validate the collection JSON
                var validationResult = this.validationService.ValidateCollection(collectionJson);
                if (!validationResult.IsValid)
                {
                    this.metrics.RecordWriteError("put", "collection", "validation_error");
                    var errorMessage = StacRequestHelpers.FormatValidationErrors(validationResult.Errors);
                    return CollectionOperationResult.ValidationError(errorMessage);
                }

                await this.store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                // Verify the ID in the path matches the ID in the body
                if (!StacRequestHelpers.TryGetId(collectionJson, out var id))
                {
                    this.metrics.RecordWriteError("put", "collection", "missing_id");
                    return CollectionOperationResult.ValidationError("Collection 'id' in body is required and must be a string.");
                }

                if (!id.EqualsIgnoreCase(collectionId))
                {
                    this.metrics.RecordWriteError("put", "collection", "id_mismatch");
                    return CollectionOperationResult.ValidationError("Collection 'id' in body must match the path parameter.");
                }

                // Create or replace the collection
                StacCollectionRecord record;
                try
                {
                    record = this.parsingService.ParseCollectionFromJson(collectionJson);
                }
                catch (InvalidOperationException ex)
                {
                    this.metrics.RecordWriteError("put", "collection", "parse_error");
                    return CollectionOperationResult.ValidationError(ex.Message);
                }

                await this.store.UpsertCollectionAsync(record, expectedETag: ifMatch, cancellationToken).ConfigureAwait(false);

                // Log audit event
                this.auditLogger.LogDataAccess(username, "UPDATE", "STAC_Collection", collectionId, ipAddress);

                // Invalidate cache for this collection
                await this.cacheInvalidation.InvalidateStacCollectionCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

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
        await this.store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Get existing collection
        var existing = await this.store.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return CollectionOperationResult.NotFound($"Collection '{collectionId}' not found.");
        }

        // Merge the patch with existing collection
        StacCollectionRecord merged;
        try
        {
            merged = this.parsingService.MergeCollectionPatch(existing, patchJson);
        }
        catch (InvalidOperationException ex)
        {
            return CollectionOperationResult.ValidationError(ex.Message);
        }

        await this.store.UpsertCollectionAsync(merged, expectedETag: null, cancellationToken).ConfigureAwait(false);

        // Invalidate cache for this collection
        await this.cacheInvalidation.InvalidateStacCollectionCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

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
            .WithLogger(this.logger)
            .WithMetrics(this.metrics.WriteOperationsCounter, this.metrics.WriteOperationErrorsCounter, this.metrics.WriteOperationDuration)
            .WithTag("stac.operation", "DeleteCollection")
            .WithTag("stac.collection_id", collectionId)
            .WithTag("operation", "delete")
            .WithTag("resource", "collection")
            .ExecuteAsync(async activity =>
            {
                await this.store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                var deleted = await this.store.DeleteCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
                if (!deleted)
                {
                    this.metrics.RecordWriteError("delete", "collection", "not_found");
                    return false;
                }

                // Log audit event
                this.auditLogger.LogDataAccess(username, "DELETE", "STAC_Collection", collectionId, ipAddress);

                // SECURITY FIX (Issue 38): Invalidate cache for this collection AND its items
                // When a collection is deleted, all its items are also removed, so we must invalidate
                // both collection and item caches, plus search results
                await this.cacheInvalidation.InvalidateStacCollectionCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);
                await this.cacheInvalidation.InvalidateStacItemsCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

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
