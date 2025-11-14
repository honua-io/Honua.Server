// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
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

namespace Honua.Server.Host.Stac.Services;

/// <summary>
/// Service for STAC item write operations (POST, PUT, PATCH, DELETE).
/// </summary>
public sealed class StacItemService
{
    private readonly IStacCatalogStore store;
    private readonly IStacValidationService validationService;
    private readonly ISecurityAuditLogger auditLogger;
    private readonly StacMetrics metrics;
    private readonly IOutputCacheInvalidationService cacheInvalidation;
    private readonly StacParsingService parsingService;

    // Metrics for OperationInstrumentation
    private readonly Counter<long> writeSuccessCounter;
    private readonly Counter<long> writeErrorCounter;
    private readonly Histogram<double> writeDurationHistogram;

    public StacItemService(
        IStacCatalogStore store,
        IStacValidationService validationService,
        ISecurityAuditLogger auditLogger,
        StacMetrics metrics,
        IOutputCacheInvalidationService cacheInvalidation,
        StacParsingService parsingService,
        IMeterFactory meterFactory)
    {
        this.store = Guard.NotNull(store);
        this.validationService = Guard.NotNull(validationService);
        this.auditLogger = Guard.NotNull(auditLogger);
        this.metrics = Guard.NotNull(metrics);
        this.cacheInvalidation = Guard.NotNull(cacheInvalidation);
        this.parsingService = Guard.NotNull(parsingService);

        // Initialize metrics for OperationInstrumentation
        var meter = meterFactory.Create("Honua.Server.Stac.Items");
        this.writeSuccessCounter = meter.CreateCounter<long>("stac.item.write.success", unit: "{operation}");
        this.writeErrorCounter = meter.CreateCounter<long>("stac.item.write.error", unit: "{error}");
        this.writeDurationHistogram = meter.CreateHistogram<double>("stac.item.write.duration", unit: "ms");
    }

    /// <summary>
    /// Creates a new item in a collection.
    /// </summary>
    public async Task<ItemOperationResult> CreateItemAsync(
        string collectionId,
        System.Text.Json.Nodes.JsonObject itemJson,
        string username,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<ItemOperationResult>("STAC PostCollectionItem")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithMetrics(this.writeSuccessCounter, this.writeErrorCounter, this.writeDurationHistogram)
            .WithTag("stac.operation", "PostCollectionItem")
            .WithTag("stac.collection_id", collectionId)
            .WithTag("operation", "post")
            .WithTag("resource", "item")
            .ExecuteAsync(async activity =>
            {
                // Validate the item JSON
                var validationResult = this.validationService.ValidateItem(itemJson);
                if (!validationResult.IsValid)
                {
                    this.metrics.RecordWriteError("post", "item", "validation_error");
                    var errorMessage = StacRequestHelpers.FormatValidationErrors(validationResult.Errors);
                    return ItemOperationResult.ValidationError(errorMessage);
                }

                await this.store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                // Verify collection exists
                var collection = await this.store.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
                if (collection is null)
                {
                    this.metrics.RecordWriteError("post", "item", "collection_not_found");
                    return ItemOperationResult.NotFound($"Collection '{collectionId}' not found.");
                }

                // Parse and validate the item
                if (!StacRequestHelpers.TryGetId(itemJson, out var id))
                {
                    this.metrics.RecordWriteError("post", "item", "missing_id");
                    return ItemOperationResult.ValidationError("Item 'id' is required and must be a string.");
                }

                activity?.SetTag("stac.item_id", id);

                // Check if item already exists
                var existing = await this.store.GetItemAsync(collectionId, id, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    this.metrics.RecordWriteError("post", "item", "conflict");
                    return ItemOperationResult.Conflict($"Item '{id}' already exists in collection '{collectionId}'. Use PUT or PATCH to update.");
                }

                // Create item record from JSON
                StacItemRecord record;
                try
                {
                    record = this.parsingService.ParseItemFromJson(itemJson, collectionId);
                }
                catch (InvalidOperationException ex)
                {
                    this.metrics.RecordWriteError("post", "item", "parse_error");
                    return ItemOperationResult.ValidationError(ex.Message);
                }

                // Store the item
                await this.store.UpsertItemAsync(record, expectedETag: null, cancellationToken).ConfigureAwait(false);

                // Log audit event
                this.auditLogger.LogDataAccess(username, "CREATE", "STAC_Item", $"{collectionId}/{id}", ipAddress);

                // Record legacy metrics for backward compatibility
                this.metrics.RecordWriteSuccess("post", "item", 0); // Duration tracked by OperationInstrumentation

                // Invalidate cache for items in this collection
                await this.cacheInvalidation.InvalidateStacItemsCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

                return ItemOperationResult.Success(record);
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates or creates an item.
    /// </summary>
    public async Task<ItemOperationResult> UpsertItemAsync(
        string collectionId,
        string itemId,
        System.Text.Json.Nodes.JsonObject itemJson,
        string? ifMatch,
        string username,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<ItemOperationResult>("STAC PutCollectionItem")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithMetrics(this.writeSuccessCounter, this.writeErrorCounter, this.writeDurationHistogram)
            .WithTag("stac.operation", "PutCollectionItem")
            .WithTag("stac.collection_id", collectionId)
            .WithTag("stac.item_id", itemId)
            .WithTag("operation", "put")
            .WithTag("resource", "item")
            .ExecuteAsync(async activity =>
            {
                // Validate the item JSON
                var validationResult = this.validationService.ValidateItem(itemJson);
                if (!validationResult.IsValid)
                {
                    this.metrics.RecordWriteError("put", "item", "validation_error");
                    var errorMessage = StacRequestHelpers.FormatValidationErrors(validationResult.Errors);
                    return ItemOperationResult.ValidationError(errorMessage);
                }

                await this.store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                // Verify collection exists
                var collection = await this.store.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
                if (collection is null)
                {
                    this.metrics.RecordWriteError("put", "item", "collection_not_found");
                    return ItemOperationResult.NotFound($"Collection '{collectionId}' not found.");
                }

                // Verify the ID in the path matches the ID in the body
                if (!StacRequestHelpers.TryGetId(itemJson, out var id))
                {
                    this.metrics.RecordWriteError("put", "item", "missing_id");
                    return ItemOperationResult.ValidationError("Item 'id' in body is required and must be a string.");
                }

                if (!id.EqualsIgnoreCase(itemId))
                {
                    this.metrics.RecordWriteError("put", "item", "id_mismatch");
                    return ItemOperationResult.ValidationError("Item 'id' in body must match the path parameter.");
                }

                // Create or replace the item
                StacItemRecord record;
                try
                {
                    record = this.parsingService.ParseItemFromJson(itemJson, collectionId);
                }
                catch (InvalidOperationException ex)
                {
                    this.metrics.RecordWriteError("put", "item", "parse_error");
                    return ItemOperationResult.ValidationError(ex.Message);
                }

                await this.store.UpsertItemAsync(record, expectedETag: ifMatch, cancellationToken).ConfigureAwait(false);

                // Log audit event
                this.auditLogger.LogDataAccess(username, "UPDATE", "STAC_Item", $"{collectionId}/{itemId}", ipAddress);

                // Record legacy metrics for backward compatibility
                this.metrics.RecordWriteSuccess("put", "item", 0); // Duration tracked by OperationInstrumentation

                // Invalidate cache for items in this collection
                await this.cacheInvalidation.InvalidateStacItemsCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

                return ItemOperationResult.Success(record);
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Patches an existing item.
    /// </summary>
    public async Task<ItemOperationResult> PatchItemAsync(
        string collectionId,
        string itemId,
        System.Text.Json.Nodes.JsonObject patchJson,
        CancellationToken cancellationToken)
    {
        await this.store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Get existing item
        var existing = await this.store.GetItemAsync(collectionId, itemId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return ItemOperationResult.NotFound($"Item '{itemId}' not found in collection '{collectionId}'.");
        }

        // Merge the patch with existing item
        StacItemRecord merged;
        try
        {
            merged = this.parsingService.MergeItemPatch(existing, patchJson);
        }
        catch (InvalidOperationException ex)
        {
            return ItemOperationResult.ValidationError(ex.Message);
        }

        await this.store.UpsertItemAsync(merged, expectedETag: null, cancellationToken).ConfigureAwait(false);

        // Invalidate cache for items in this collection
        await this.cacheInvalidation.InvalidateStacItemsCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

        return ItemOperationResult.Success(merged);
    }

    /// <summary>
    /// Deletes an item.
    /// </summary>
    public async Task<bool> DeleteItemAsync(
        string collectionId,
        string itemId,
        string username,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<bool>("STAC DeleteCollectionItem")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithMetrics(this.writeSuccessCounter, this.writeErrorCounter, this.writeDurationHistogram)
            .WithTag("stac.operation", "DeleteCollectionItem")
            .WithTag("stac.collection_id", collectionId)
            .WithTag("stac.item_id", itemId)
            .WithTag("operation", "delete")
            .WithTag("resource", "item")
            .ExecuteAsync(async activity =>
            {
                await this.store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                var deleted = await this.store.DeleteItemAsync(collectionId, itemId, cancellationToken).ConfigureAwait(false);
                if (!deleted)
                {
                    this.metrics.RecordWriteError("delete", "item", "not_found");
                    return false;
                }

                // Log audit event
                this.auditLogger.LogDataAccess(username, "DELETE", "STAC_Item", $"{collectionId}/{itemId}", ipAddress);

                // Record legacy metrics for backward compatibility
                this.metrics.RecordWriteSuccess("delete", "item", 0); // Duration tracked by OperationInstrumentation

                // Invalidate cache for items in this collection
                await this.cacheInvalidation.InvalidateStacItemsCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);

                return true;
            }).ConfigureAwait(false);
    }
}

/// <summary>
/// Result of an item operation.
/// </summary>
public sealed class ItemOperationResult
{
    public bool IsSuccess { get; init; }
    public StacItemRecord? Record { get; init; }
    public string? ErrorMessage { get; init; }
    public OperationErrorType? ErrorType { get; init; }

    public static ItemOperationResult Success(StacItemRecord record) =>
        new() { IsSuccess = true, Record = record };

    public static ItemOperationResult ValidationError(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorType = OperationErrorType.Validation };

    public static ItemOperationResult NotFound(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorType = OperationErrorType.NotFound };

    public static ItemOperationResult Conflict(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorType = OperationErrorType.Conflict };
}
