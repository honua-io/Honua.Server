// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Carto;

internal static class CartoHandlers
{
    public static IResult GetLanding(HttpRequest request)
    {
        Guard.NotNull(request);

        var links = new List<CartoDatasetLink>
        {
            new("self", RequestLinkHelper.BuildAbsoluteUrl(request, "/carto"), "application/json", "Carto-compatible API root"),
            new("datasets", RequestLinkHelper.BuildAbsoluteUrl(request, "/carto/api/v3/datasets"), "application/json", "Dataset catalog"),
            new("sql", RequestLinkHelper.BuildAbsoluteUrl(request, "/carto/api/v3/sql"), "application/json", "SQL endpoint")
        };

        var payload = new
        {
            name = "Honua Carto API",
            description = "Carto-compatible APIs for dataset discovery and SQL queries.",
            links
        };

        return Results.Json(payload);
    }

    public static IResult GetDatasets(HttpRequest request, CartoDatasetResolver resolver)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);

        var datasets = resolver.GetDatasets()
            .Where(ctx => string.Equals(ctx.Service.ServiceType, "feature", StringComparison.OrdinalIgnoreCase))
            .Select(ctx => MapSummary(ctx, request))
            .OrderBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = new
        {
            count = datasets.Count,
            datasets
        };

        return Results.Json(payload);
    }

    public static async Task<IResult> GetDataset(
        string datasetId,
        HttpRequest request,
        [FromServices] CartoDatasetResolver resolver,
        [FromServices] IFeatureRepository repository,
        CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OData,
            "Carto GetDataset",
            [
                ("carto.operation", "GetDataset"),
                ("carto.dataset_id", (object?)datasetId)
            ],
            async activity =>
            {
                Guard.NotNull(request);
                Guard.NotNull(resolver);
                Guard.NotNull(repository);

                if (!resolver.TryResolve(datasetId, out var context))
                {
                    return GeoservicesRESTErrorHelper.NotFound("Dataset", datasetId);
                }

                if (!string.Equals(context.Service.ServiceType, "feature", StringComparison.OrdinalIgnoreCase))
                {
                    return ApiErrorResponse.Carto.BadRequest($"Dataset '{datasetId}' is not a feature service.");
                }

                var detail = await MapDetailAsync(context, request, repository, cancellationToken).ConfigureAwait(false);
                return Results.Json(detail);
            }).ConfigureAwait(false);
    }

    public static async Task<IResult> GetDatasetSchema(
        string datasetId,
        HttpRequest request,
        [FromServices] CartoDatasetResolver resolver,
        [FromServices] IFeatureRepository repository,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(repository);

        if (!resolver.TryResolve(datasetId, out var context))
        {
            return GeoservicesRESTErrorHelper.NotFound("Dataset", datasetId);
        }

        if (!string.Equals(context.Service.ServiceType, "feature", StringComparison.OrdinalIgnoreCase))
        {
            return ApiErrorResponse.Carto.BadRequest($"Dataset '{datasetId}' is not a feature service.");
        }

        var detail = await MapDetailAsync(context, request, repository, cancellationToken).ConfigureAwait(false);
        var payload = new
        {
            dataset = MapSummary(context, request),
            fields = detail.Fields,
            recordCount = detail.RecordCount
        };

        return Results.Json(payload);
    }

    public static async Task<IResult> ExecuteSqlGet(
        HttpRequest request,
        [FromServices] CartoSqlQueryExecutor executor,
        CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OData,
            "Carto ExecuteSQL",
            [
                ("carto.operation", "ExecuteSQL"),
                ("carto.method", "GET")
            ],
            async activity =>
            {
                Guard.NotNull(request);
                Guard.NotNull(executor);

                var query = ResolveQueryFromRequest(request, null);
                activity.AddTag("carto.query_length", query?.Length ?? 0);
                if (query.IsNullOrWhiteSpace())
                {
                    return ApiErrorResponse.Carto.BadRequest("Query parameter 'q' is required.");
                }

                var result = await executor.ExecuteAsync(query!, cancellationToken).ConfigureAwait(false);
                return MapSqlResult(result);
            }).ConfigureAwait(false);
    }

    public static async Task<IResult> ExecuteSqlPost(
        HttpRequest request,
        [FromServices] CartoSqlQueryExecutor executor,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(executor);

        CartoSqlRequestBody? payload = null;
        if (request.ContentLength.GetValueOrDefault() > 0)
        {
            try
            {
                payload = await request.ReadFromJsonAsync<CartoSqlRequestBody>(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                return ApiErrorResponse.Carto.BadRequest("Invalid JSON payload.", ex.Message);
            }
        }

        var query = ResolveQueryFromRequest(request, payload?.Query ?? payload?.Q);
        if (query.IsNullOrWhiteSpace())
        {
            return ApiErrorResponse.Carto.BadRequest("Request body must include 'q' or 'query'.");
        }

        var result = await executor.ExecuteAsync(query!, cancellationToken).ConfigureAwait(false);
        return MapSqlResult(result);
    }

    public static async Task<IResult> ExecuteSqlLegacy(
        HttpRequest request,
        CartoSqlQueryExecutor executor,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(executor);

        var query = request.Query.TryGetValue("q", out var values) ? values.ToString() : null;
        if (query.IsNullOrWhiteSpace())
        {
            return ApiErrorResponse.Carto.BadRequest("Query parameter 'q' is required.");
        }

        var result = await executor.ExecuteAsync(query!, cancellationToken).ConfigureAwait(false);
        return MapSqlResult(result);
    }

    private static async Task<CartoDatasetDetail> MapDetailAsync(
        CartoDatasetContext context,
        HttpRequest request,
        IFeatureRepository repository,
        CancellationToken cancellationToken)
    {
        var summary = MapSummary(context, request);
        long? recordCount = null;

        try
        {
            recordCount = await repository.CountAsync(context.ServiceId, context.LayerId, query: null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            recordCount = null;
        }

        var fields = context.Layer.Fields
            .Select(field => CartoFieldMapper.ToDatasetField(context.Layer, field))
            .ToList();

        var detail = new CartoDatasetDetail(
            summary.Id,
            summary.Name,
            summary.Description,
            summary.GeometryType,
            summary.ServiceId,
            summary.LayerId,
            context.Service.Title,
            summary.Crs,
            summary.Keywords,
            summary.Links,
            fields,
            recordCount);

        return detail;
    }

    private static CartoDatasetSummary MapSummary(CartoDatasetContext context, HttpRequest request)
    {
        var layer = context.Layer;
        var keywords = MergeKeywords(layer.Keywords, layer.Catalog.Keywords);
        var links = new List<CartoDatasetLink>
        {
            new("self", RequestLinkHelper.BuildAbsoluteUrl(request, $"/carto/api/v3/datasets/{context.DatasetId}"), "application/json", "Dataset details"),
            new("schema", RequestLinkHelper.BuildAbsoluteUrl(request, $"/carto/api/v3/datasets/{context.DatasetId}/schema"), "application/json", "Dataset schema"),
            new("sql", RequestLinkHelper.BuildAbsoluteUrl(request, $"/carto/api/v3/sql?q=SELECT+*+FROM+{Uri.EscapeDataString(context.DatasetId)}"), "application/json", "Sample query")
        };

        var description = layer.Description.HasValue()
            ? layer.Description
            : layer.Catalog.Summary;

        var name = layer.Title.IsNullOrWhiteSpace() ? layer.Id : layer.Title;

        return new CartoDatasetSummary(
            context.DatasetId,
            name,
            description,
            layer.GeometryType,
            context.ServiceId,
            context.LayerId,
            layer.Crs,
            keywords,
            links);
    }

    private static IReadOnlyList<string> MergeKeywords(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        if (left is not null)
        {
            foreach (var keyword in left)
            {
                if (keyword.HasValue())
                {
                    if (set.Add(keyword))
                    {
                        ordered.Add(keyword);
                    }
                }
            }
        }

        if (right is not null)
        {
            foreach (var keyword in right)
            {
                if (keyword.HasValue())
                {
                    if (set.Add(keyword))
                    {
                        ordered.Add(keyword);
                    }
                }
            }
        }

        return ordered.Count == 0 ? Array.Empty<string>() : ordered;
    }

    private static string? ResolveQueryFromRequest(HttpRequest request, string? primary)
    {
        if (primary.HasValue())
        {
            return primary;
        }

        if (request.Query.TryGetValue("q", out var queryValue) && !string.IsNullOrWhiteSpace(queryValue))
        {
            return queryValue.ToString();
        }

        if (request.Query.TryGetValue("query", out var alternateValue) && !string.IsNullOrWhiteSpace(alternateValue))
        {
            return alternateValue.ToString();
        }

        return null;
    }

    private static IResult MapSqlResult(CartoSqlExecutionResult result)
    {
        if (result.IsSuccess && result.Response is not null)
        {
            return Results.Json(result.Response);
        }

        var errorMessage = result.Error?.Error ?? "Unknown error.";
        var detail = result.Error?.Detail;
        return ApiErrorResponse.Carto.Error(errorMessage, detail, result.StatusCode);
    }

    private sealed record CartoSqlRequestBody(string? Q, string? Query);
}
