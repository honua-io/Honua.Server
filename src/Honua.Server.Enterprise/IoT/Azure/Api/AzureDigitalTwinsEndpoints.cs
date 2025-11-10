// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.IoT.Azure.Models;
using Honua.Server.Enterprise.IoT.Azure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Enterprise.IoT.Azure.Api;

/// <summary>
/// API endpoints for Azure Digital Twins integration.
/// </summary>
public static class AzureDigitalTwinsEndpoints
{
    /// <summary>
    /// Maps Azure Digital Twins API endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapAzureDigitalTwinsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/azure/digital-twins")
            .WithTags("Azure Digital Twins")
            .WithOpenApi();

        // Sync endpoints
        group.MapPost("/sync/layer/{serviceId}/{layerId}",
            async (
                [FromRoute] string serviceId,
                [FromRoute] string layerId,
                [FromServices] ITwinSynchronizationService syncService,
                CancellationToken cancellationToken) =>
            {
                var result = await syncService.PerformBatchSyncAsync(serviceId, layerId, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("SyncLayerToAdt")
            .WithSummary("Trigger batch sync of layer to Azure Digital Twins")
            .Produces<BatchSyncStatistics>()
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/sync/feature/{serviceId}/{layerId}/{featureId}",
            async (
                [FromRoute] string serviceId,
                [FromRoute] string layerId,
                [FromRoute] string featureId,
                [FromBody] Dictionary<string, object?> attributes,
                [FromServices] ITwinSynchronizationService syncService,
                CancellationToken cancellationToken) =>
            {
                var result = await syncService.SyncFeatureToTwinAsync(
                    serviceId,
                    layerId,
                    featureId,
                    attributes,
                    cancellationToken);

                return result.Success
                    ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .WithName("SyncFeatureToAdt")
            .WithSummary("Sync a single feature to Azure Digital Twins")
            .Produces<TwinSyncResult>()
            .Produces<TwinSyncResult>(StatusCodes.Status400BadRequest);

        group.MapDelete("/sync/feature/{serviceId}/{layerId}/{featureId}",
            async (
                [FromRoute] string serviceId,
                [FromRoute] string layerId,
                [FromRoute] string featureId,
                [FromServices] ITwinSynchronizationService syncService,
                CancellationToken cancellationToken) =>
            {
                var result = await syncService.DeleteTwinAsync(
                    serviceId,
                    layerId,
                    featureId,
                    cancellationToken);

                return result.Success
                    ? Results.Ok(result)
                    : Results.BadRequest(result);
            })
            .WithName("DeleteTwinForFeature")
            .WithSummary("Delete a digital twin corresponding to a Honua feature")
            .Produces<TwinSyncResult>()
            .Produces<TwinSyncResult>(StatusCodes.Status400BadRequest);

        // Model endpoints
        group.MapGet("/models",
            async ([FromServices] IAzureDigitalTwinsClient adtClient, CancellationToken cancellationToken) =>
            {
                var models = new List<object>();
                await foreach (var model in adtClient.GetModelsAsync(cancellationToken: cancellationToken))
                {
                    models.Add(new
                    {
                        model.Id,
                        model.DisplayName,
                        model.Description,
                        model.UploadedOn,
                        model.Decommissioned
                    });
                }
                return Results.Ok(models);
            })
            .WithName("ListAdtModels")
            .WithSummary("List all DTDL models in Azure Digital Twins")
            .Produces<List<object>>();

        group.MapGet("/models/{modelId}",
            async (
                [FromRoute] string modelId,
                [FromServices] IAzureDigitalTwinsClient adtClient,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var model = await adtClient.GetModelAsync(modelId, cancellationToken);
                    return Results.Ok(new
                    {
                        model.Value.Id,
                        model.Value.DisplayName,
                        model.Value.Description,
                        model.Value.DtdlModel,
                        model.Value.UploadedOn,
                        model.Value.Decommissioned
                    });
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    return Results.NotFound(new { error = "Model not found" });
                }
            })
            .WithName("GetAdtModel")
            .WithSummary("Get a specific DTDL model")
            .Produces<object>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/models/from-layer/{serviceId}/{layerId}",
            async (
                [FromRoute] string serviceId,
                [FromRoute] string layerId,
                [FromBody] Dictionary<string, object> layerSchema,
                [FromServices] IDtdlModelMapper modelMapper,
                [FromServices] IAzureDigitalTwinsClient adtClient,
                CancellationToken cancellationToken) =>
            {
                // Generate DTDL model
                var modelJson = await modelMapper.GenerateModelJsonFromLayerAsync(
                    serviceId,
                    layerId,
                    layerSchema,
                    cancellationToken);

                // Validate model
                var isValid = await modelMapper.ValidateModelAsync(modelJson, cancellationToken);
                if (!isValid)
                {
                    return Results.BadRequest(new { error = "Invalid DTDL model generated" });
                }

                // Upload to ADT
                var response = await adtClient.CreateModelsAsync(
                    new[] { modelJson },
                    cancellationToken);

                return Results.Ok(new
                {
                    message = "Model created successfully",
                    models = response.Value.Select(m => new
                    {
                        m.Id,
                        m.DisplayName,
                        m.Description
                    })
                });
            })
            .WithName("CreateModelFromLayer")
            .WithSummary("Generate and upload DTDL model from Honua layer schema")
            .Produces<object>()
            .Produces(StatusCodes.Status400BadRequest);

        // Twin query endpoint
        group.MapPost("/twins/query",
            async (
                [FromBody] QueryRequest queryRequest,
                [FromServices] IAzureDigitalTwinsClient adtClient,
                CancellationToken cancellationToken) =>
            {
                var twins = new List<object>();
                var count = 0;
                var maxResults = queryRequest.MaxResults ?? 100;

                await foreach (var twin in adtClient.QueryAsync(queryRequest.Query, cancellationToken))
                {
                    if (count >= maxResults)
                        break;

                    twins.Add(new
                    {
                        twin.Id,
                        twin.Metadata.ModelId,
                        Properties = twin.Contents
                    });
                    count++;
                }

                return Results.Ok(new
                {
                    query = queryRequest.Query,
                    count = twins.Count,
                    results = twins
                });
            })
            .WithName("QueryTwins")
            .WithSummary("Query digital twins using ADT query language")
            .Produces<object>();

        // Twin proxy endpoint
        group.MapGet("/twins/{twinId}",
            async (
                [FromRoute] string twinId,
                [FromServices] IAzureDigitalTwinsClient adtClient,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var twin = await adtClient.GetDigitalTwinAsync(twinId, cancellationToken);
                    return Results.Ok(new
                    {
                        twin.Value.Id,
                        twin.Value.Metadata.ModelId,
                        twin.Value.ETag,
                        Properties = twin.Value.Contents
                    });
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    return Results.NotFound(new { error = "Twin not found" });
                }
            })
            .WithName("GetTwin")
            .WithSummary("Get a digital twin by ID")
            .Produces<object>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>
    /// Request model for twin queries.
    /// </summary>
    public sealed class QueryRequest
    {
        /// <summary>
        /// ADT query language query.
        /// </summary>
        public required string Query { get; set; }

        /// <summary>
        /// Maximum number of results to return.
        /// </summary>
        public int? MaxResults { get; set; }
    }
}
