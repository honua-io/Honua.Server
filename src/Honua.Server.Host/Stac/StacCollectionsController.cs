// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Stac;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Middleware;
using Honua.Server.Host.Stac.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Stac;

/// <summary>
/// STAC Collections controller.
/// Thin controller that delegates all business logic to services.
/// Handles HTTP concerns: routing, status codes, headers, and response formatting.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "RequireViewer")]
[Route("stac/collections")]
[Route("v{version:apiVersion}/stac/collections")]
public sealed class StacCollectionsController : ControllerBase
{
    private readonly StacReadService readService;
    private readonly StacCollectionService collectionService;
    private readonly StacItemService itemService;
    private readonly StacControllerHelper helper;
    private readonly ILogger<StacCollectionsController> logger;

    public StacCollectionsController(
        StacReadService readService,
        StacCollectionService collectionService,
        StacItemService itemService,
        StacControllerHelper helper,
        ILogger<StacCollectionsController> logger)
    {
        this.readService = Guard.NotNull(readService);
        this.collectionService = Guard.NotNull(collectionService);
        this.itemService = Guard.NotNull(itemService);
        this.helper = Guard.NotNull(helper);
        this.logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Gets the list of all STAC collections available in the catalog.
    /// </summary>
    /// <param name="limit">Maximum number of collections to return (default 100, max 1000).</param>
    /// <param name="token">Continuation token for pagination.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of STAC collections with their metadata.</returns>
    /// <response code="200">Returns the list of collections</response>
    /// <response code="404">STAC is not enabled in the configuration</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StacCollectionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.StacCollections)]
    [AllowAnonymous]
    public async Task<ActionResult<StacCollectionsResponse>> GetCollections(
        [FromQuery] int limit = 100,
        [FromQuery] string? token = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = StacRequestHelpers.NormalizeLimit(limit);

        return await ActivityScope.ExecuteAsync<ActionResult<StacCollectionsResponse>>(
            HonuaTelemetry.Stac,
            "STAC GetCollections",
            [
                ("stac.operation", "GetCollections"),
                ("stac.limit", normalizedLimit),
                ("stac.has_token", !token.IsNullOrEmpty())
            ],
            async activity =>
            {
                if (limit != normalizedLimit)
                {
                    this.logger.LogInformation("STAC collections list requested with limit={RequestedLimit} (normalized to {NormalizedLimit}), hasToken={HasToken}",
                        limit, normalizedLimit, !token.IsNullOrEmpty());
                }
                else
                {
                    this.logger.LogInformation("STAC collections list requested with limit={Limit}, hasToken={HasToken}", normalizedLimit, !token.IsNullOrEmpty());
                }

                var error = EnsureStacEnabledOrNotFound();
                if (error is not null)
                {
                    this.logger.LogFeatureDisabled("STAC");
                    return error;
                }

                try
                {
                    var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response);
                    var response = await this.readService.GetCollectionsAsync(baseUri.ToString(), normalizedLimit, token, cancellationToken).ConfigureAwait(false);
                    this.logger.LogInformation("STAC collections returned: {CollectionCount} collections, hasMore={HasMore}",
                        response.Collections?.Count ?? 0, response.Context?["matched"] != null);
                    return this.Ok(response);
                }
                catch (Exception ex)
                {
                    this.logger.LogOperationFailure(ex, "STAC GetCollections");
                    throw;
                }
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a specific STAC collection by its identifier.
    /// </summary>
    /// <param name="collectionId">The unique identifier of the collection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The STAC collection details including spatial and temporal extent.</returns>
    /// <response code="200">Returns the collection details with ETag header for caching</response>
    /// <response code="404">Collection not found or STAC is not enabled</response>
    [HttpGet("{collectionId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StacCollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.StacCollectionMetadata)]
    [AllowAnonymous]
    public async Task<ActionResult<StacCollectionResponse>> GetCollection(string collectionId, CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync<ActionResult<StacCollectionResponse>>(
            HonuaTelemetry.Stac,
            "STAC GetCollection",
            [
                ("stac.operation", "GetCollection"),
                ("stac.collection_id", collectionId)
            ],
            async activity =>
            {
                this.logger.LogInformation("STAC collection requested: {CollectionId}", collectionId);

                var error = EnsureStacEnabledOrNotFound();
                if (error is not null)
                {
                    this.logger.LogFeatureDisabled("STAC");
                    return error;
                }

                var record = await this.readService.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
                if (record is null)
                {
                    this.logger.LogResourceNotFound("Collection", collectionId);
                    return this.NotFound();
                }

                var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response, record.ETag);
                var response = StacApiMapper.BuildCollection(record, baseUri);

                this.logger.LogDebug("STAC collection returned: {CollectionId}", collectionId);
                return this.Ok(response);
            }).ConfigureAwait(false);
    }

    [HttpGet("{collectionId}/items")]
    [Produces("application/geo+json")]
    [ProducesResponseType(typeof(StacItemCollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.StacItems)]
    [AllowAnonymous]
    public async Task<ActionResult<StacItemCollectionResponse>> GetCollectionItems(
        string collectionId,
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "token")] string? pageToken,
        CancellationToken cancellationToken)
    {
        var error = EnsureStacEnabledOrNotFound();
        if (error is not null) return error;

        try
        {
            var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response);
            var response = await this.readService.GetCollectionItemsAsync(
                collectionId, limit, pageToken, baseUri.ToString(), cancellationToken).ConfigureAwait(false);
            return this.Ok(response);
        }
        catch (InvalidOperationException)
        {
            return this.NotFound();
        }
    }

    [HttpGet("{collectionId}/items/{itemId}")]
    [Produces("application/geo+json")]
    [ProducesResponseType(typeof(StacItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.StacItemMetadata)]
    [AllowAnonymous]
    public async Task<ActionResult<StacItemResponse>> GetCollectionItem(string collectionId, string itemId, CancellationToken cancellationToken)
    {
        var error = EnsureStacEnabledOrNotFound();
        if (error is not null) return error;

        var record = await this.readService.GetCollectionItemAsync(collectionId, itemId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return this.NotFound();
        }

        var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response, record.ETag);
        var response = StacApiMapper.BuildItem(record, baseUri);

        return this.Ok(response);
    }

    /// <summary>
    /// Creates a new STAC collection. Request size limited to 10 MB for metadata payloads.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireDataPublisher")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StacCollectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [OutputCache(PolicyName = OutputCachePolicies.NoCache)]
    [RequestSizeLimit((int)ApiLimitsAndConstants.StacMaxRequestBodyBytes)] // STAC metadata size limit
    public async Task<ActionResult<StacCollectionResponse>> PostCollection([FromBody] System.Text.Json.Nodes.JsonObject collectionJson, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("STAC collection creation requested by {Username}", this.helper.GetUsername(User));

        var error = EnsureStacEnabledOrNotFound();
        if (error is not null)
        {
            this.logger.LogFeatureDisabled("STAC");
            return error;
        }

        var (username, ipAddress) = this.helper.BuildAuditContext(HttpContext);

        var result = await this.collectionService.CreateCollectionAsync(
            collectionJson, username, ipAddress, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            this.logger.LogWarning("STAC collection creation failed: {ErrorType} - {ErrorMessage}",
                result.ErrorType, result.ErrorMessage);
            return this.helper.MapOperationErrorToResponse(result.ErrorType!.Value, result.ErrorMessage!, this.Request.Path);
        }

        this.logger.LogInformation("STAC collection created successfully: {CollectionId} by {Username}",
            result.Record!.Id, username);

        var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response, result.Record!.ETag);
        var response = StacApiMapper.BuildCollection(result.Record!, baseUri);

        return this.CreatedAtAction(nameof(GetCollection), new { collectionId = result.Record.Id }, response);
    }

    /// <summary>
    /// Updates a STAC collection. Request size limited to 10 MB for metadata payloads.
    /// Supports optimistic concurrency control via If-Match header with ETag values.
    /// </summary>
    /// <param name="collectionId">The unique identifier of the collection to update.</param>
    /// <param name="collectionJson">The updated collection metadata in JSON format.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The updated collection with a new ETag.</returns>
    /// <response code="200">Collection updated successfully with new ETag</response>
    /// <response code="400">Invalid collection data or validation failure</response>
    /// <response code="404">Collection not found or STAC is not enabled</response>
    /// <response code="412">Precondition Failed - ETag mismatch (collection was modified by another user). Client should GET the latest version, merge changes, and retry with the new ETag.</response>
    [HttpPut("{collectionId}")]
    [Authorize(Policy = "RequireDataPublisher")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StacCollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [OutputCache(PolicyName = OutputCachePolicies.NoCache)]
    [RequestSizeLimit((int)ApiLimitsAndConstants.StacMaxRequestBodyBytes)] // STAC metadata size limit
    public async Task<ActionResult<StacCollectionResponse>> PutCollection(string collectionId, [FromBody] System.Text.Json.Nodes.JsonObject collectionJson, CancellationToken cancellationToken)
    {
        var error = EnsureStacEnabledOrNotFound();
        if (error is not null) return error;

        var (username, ipAddress) = this.helper.BuildAuditContext(HttpContext);
        var ifMatch = this.helper.GetIfMatchETag(Request);

        try
        {
            var result = await this.collectionService.UpsertCollectionAsync(
                collectionId, collectionJson, ifMatch, username, ipAddress, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return this.helper.MapOperationErrorToResponse(result.ErrorType!.Value, result.ErrorMessage!, this.Request.Path);
            }

            var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response, result.Record!.ETag);
            var response = StacApiMapper.BuildCollection(result.Record!, baseUri);

            return this.Ok(response);
        }
        catch (System.Data.DBConcurrencyException ex)
        {
            this.logger.LogWarning(ex, "STAC collection update failed due to ETag mismatch: {CollectionId}", collectionId);
            return this.helper.HandleDBConcurrencyException(ex, "collection", this.Request.Path);
        }
    }

    /// <summary>
    /// Partially updates a STAC collection. Request size limited to 10 MB for metadata payloads.
    /// </summary>
    [HttpPatch("{collectionId}")]
    [Authorize(Policy = "RequireDataPublisher")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StacCollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.NoCache)]
    [RequestSizeLimit((int)ApiLimitsAndConstants.StacMaxRequestBodyBytes)] // STAC metadata size limit
    public async Task<ActionResult<StacCollectionResponse>> PatchCollection(string collectionId, [FromBody] System.Text.Json.Nodes.JsonObject patchJson, CancellationToken cancellationToken)
    {
        var error = EnsureStacEnabledOrNotFound();
        if (error is not null) return error;

        var result = await this.collectionService.PatchCollectionAsync(collectionId, patchJson, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                OperationErrorType.NotFound => NotFound(this.helper.CreateNotFoundProblem("Collection not found", result.ErrorMessage!, this.Request.Path)),
                _ => BadRequest(this.helper.CreateBadRequestProblem("Invalid patch data", result.ErrorMessage!))
            };
        }

        var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response);
        var response = StacApiMapper.BuildCollection(result.Record!, baseUri);
        return this.Ok(response);
    }

    [HttpDelete("{collectionId}")]
    [Authorize(Policy = "RequireDataPublisher")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.NoCache)]
    public async Task<ActionResult> DeleteCollection(string collectionId, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("STAC collection deletion requested: {CollectionId} by {Username}",
            collectionId, this.helper.GetUsername(User));

        var error = EnsureStacEnabledOrNotFound();
        if (error is not null)
        {
            this.logger.LogFeatureDisabled("STAC");
            return error;
        }

        var (username, ipAddress) = this.helper.BuildAuditContext(HttpContext);

        var deleted = await this.collectionService.DeleteCollectionAsync(
            collectionId, username, ipAddress, cancellationToken).ConfigureAwait(false);

        if (!deleted)
        {
            this.logger.LogWarning("STAC collection deletion failed: collection not found: {CollectionId}", collectionId);
            return this.NotFound(this.helper.CreateNotFoundProblem("Collection not found", $"Collection '{collectionId}' not found.", this.Request.Path));
        }

        this.logger.LogInformation("STAC collection deleted successfully: {CollectionId} by {Username}",
            collectionId, username);
        return this.NoContent();
    }

    /// <summary>
    /// Creates a new STAC item in a collection. Request size limited to 10 MB for metadata payloads.
    /// </summary>
    [HttpPost("{collectionId}/items")]
    [Authorize(Policy = "RequireDataPublisher")]
    [Consumes("application/geo+json")]
    [Produces("application/geo+json")]
    [ProducesResponseType(typeof(StacItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [OutputCache(PolicyName = OutputCachePolicies.NoCache)]
    [RequestSizeLimit((int)ApiLimitsAndConstants.StacMaxRequestBodyBytes)] // STAC metadata size limit
    public async Task<ActionResult<StacItemResponse>> PostCollectionItem(string collectionId, [FromBody] System.Text.Json.Nodes.JsonObject itemJson, CancellationToken cancellationToken)
    {
        var error = EnsureStacEnabledOrNotFound();
        if (error is not null) return error;

        var (username, ipAddress) = this.helper.BuildAuditContext(HttpContext);

        var result = await this.itemService.CreateItemAsync(
            collectionId, itemJson, username, ipAddress, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.helper.MapOperationErrorToResponse(result.ErrorType!.Value, result.ErrorMessage!, this.Request.Path);
        }

        var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response, result.Record!.ETag);
        var response = StacApiMapper.BuildItem(result.Record!, baseUri);

        return this.CreatedAtAction(nameof(GetCollectionItem), new { collectionId, itemId = result.Record.Id }, response);
    }

    /// <summary>
    /// Updates a STAC item in a collection. Request size limited to 10 MB for metadata payloads.
    /// Supports optimistic concurrency control via If-Match header with ETag values.
    /// </summary>
    /// <param name="collectionId">The unique identifier of the collection.</param>
    /// <param name="itemId">The unique identifier of the item to update.</param>
    /// <param name="itemJson">The updated item metadata in GeoJSON format.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The updated item with a new ETag.</returns>
    /// <response code="200">Item updated successfully with new ETag</response>
    /// <response code="400">Invalid item data or validation failure</response>
    /// <response code="404">Item or collection not found, or STAC is not enabled</response>
    /// <response code="412">Precondition Failed - ETag mismatch (item was modified by another user). Client should GET the latest version, merge changes, and retry with the new ETag.</response>
    [HttpPut("{collectionId}/items/{itemId}")]
    [Authorize(Policy = "RequireDataPublisher")]
    [Consumes("application/geo+json")]
    [Produces("application/geo+json")]
    [ProducesResponseType(typeof(StacItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [OutputCache(PolicyName = OutputCachePolicies.NoCache)]
    [RequestSizeLimit((int)ApiLimitsAndConstants.StacMaxRequestBodyBytes)] // STAC metadata size limit
    public async Task<ActionResult<StacItemResponse>> PutCollectionItem(string collectionId, string itemId, [FromBody] System.Text.Json.Nodes.JsonObject itemJson, CancellationToken cancellationToken)
    {
        var error = EnsureStacEnabledOrNotFound();
        if (error is not null) return error;

        var (username, ipAddress) = this.helper.BuildAuditContext(HttpContext);
        var ifMatch = this.helper.GetIfMatchETag(Request);

        try
        {
            var result = await this.itemService.UpsertItemAsync(
                collectionId, itemId, itemJson, ifMatch, username, ipAddress, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    OperationErrorType.NotFound => NotFound(this.helper.CreateNotFoundProblem("Collection not found", result.ErrorMessage!, this.Request.Path)),
                    _ => BadRequest(this.helper.CreateBadRequestProblem("Validation failed", result.ErrorMessage!))
                };
            }

            var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response, result.Record!.ETag);
            var response = StacApiMapper.BuildItem(result.Record!, baseUri);

            return this.Ok(response);
        }
        catch (System.Data.DBConcurrencyException ex)
        {
            this.logger.LogWarning(ex, "STAC item update failed due to ETag mismatch: {CollectionId}/{ItemId}", collectionId, itemId);
            return this.helper.HandleDBConcurrencyException(ex, "item", this.Request.Path);
        }
    }

    /// <summary>
    /// Partially updates a STAC item in a collection. Request size limited to 10 MB for metadata payloads.
    /// </summary>
    [HttpPatch("{collectionId}/items/{itemId}")]
    [Authorize(Policy = "RequireDataPublisher")]
    [Consumes("application/geo+json")]
    [Produces("application/geo+json")]
    [ProducesResponseType(typeof(StacItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.NoCache)]
    [RequestSizeLimit((int)ApiLimitsAndConstants.StacMaxRequestBodyBytes)] // STAC metadata size limit
    public async Task<ActionResult<StacItemResponse>> PatchCollectionItem(string collectionId, string itemId, [FromBody] System.Text.Json.Nodes.JsonObject patchJson, CancellationToken cancellationToken)
    {
        var error = EnsureStacEnabledOrNotFound();
        if (error is not null) return error;

        var result = await this.itemService.PatchItemAsync(collectionId, itemId, patchJson, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                OperationErrorType.NotFound => NotFound(this.helper.CreateNotFoundProblem("Item not found", result.ErrorMessage!, this.Request.Path)),
                _ => BadRequest(this.helper.CreateBadRequestProblem("Invalid patch data", result.ErrorMessage!))
            };
        }

        var baseUri = this.helper.GetBaseUriAndSetETag(Request, Response);
        var response = StacApiMapper.BuildItem(result.Record!, baseUri);
        return this.Ok(response);
    }

    [HttpDelete("{collectionId}/items/{itemId}")]
    [Authorize(Policy = "RequireDataPublisher")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.NoCache)]
    public async Task<ActionResult> DeleteCollectionItem(string collectionId, string itemId, CancellationToken cancellationToken)
    {
        var error = EnsureStacEnabledOrNotFound();
        if (error is not null) return error;

        var (username, ipAddress) = this.helper.BuildAuditContext(HttpContext);

        var deleted = await this.itemService.DeleteItemAsync(
            collectionId, itemId, username, ipAddress, cancellationToken).ConfigureAwait(false);

        if (!deleted)
        {
            return this.NotFound(this.helper.CreateNotFoundProblem("Item not found", $"Item '{itemId}' not found in collection '{collectionId}'.", this.Request.Path));
        }

        return this.NoContent();
    }

    /// <summary>
    /// Checks if STAC is enabled and returns NotFound result if disabled.
    /// </summary>
    /// <returns>NotFound result if STAC is disabled, null if enabled (allowing method continuation).</returns>
    private ActionResult? EnsureStacEnabledOrNotFound()
    {
        if (!this.helper.IsStacEnabled())
        {
            return this.NotFound();
        }
        return null;
    }
}
