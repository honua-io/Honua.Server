// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Results;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Serialization;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Honua.Server.Host.Wfs.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Handlers for WFS GetFeature-related operations.
/// </summary>
internal static class WfsGetFeatureHandlers
{
    /// <summary>
    /// Handles GetFeature requests.
    /// </summary>
    public static async Task<IResult> HandleGetFeatureAsync(
        HttpRequest request,
        IQueryCollection query,
        [FromServices] ICatalogProjectionService catalog,
        [FromServices] IFeatureContextResolver contextResolver,
        [FromServices] IFeatureRepository repository,
        [FromServices] IMetadataRegistry registry,
        [FromServices] ICsvExporter csvExporter,
        [FromServices] IShapefileExporter shapefileExporter,
        CancellationToken cancellationToken)
    {
        using var activity = HonuaTelemetry.OgcProtocols.StartActivity("WFS GetFeature");
        activity?.SetTag("wfs.operation", "GetFeature");

        var executionResult = await ExecuteFeatureQueryAsync(request, query, catalog, contextResolver, repository, registry, cancellationToken, materializeResults: false).ConfigureAwait(false);
        if (executionResult.IsFailure)
        {
            return WfsHelpers.MapExecutionError(executionResult.Error!, query);
        }

        var result = executionResult.Value;
        var execution = result.Execution;
        var layer = execution.Context.Layer;
        var service = execution.Context.Service;

        activity?.SetTag("wfs.service", service.Id);
        activity?.SetTag("wfs.layer", layer.Id);
        activity?.SetTag("wfs.output_format", execution.OutputFormat);

        var responseCrs = CrsNormalizationHelper.NormalizeIdentifier(execution.ResponseCrsUrn);

        // Handle export formats
        if (execution.OutputFormat == WfsConstants.CsvFormat)
        {
            var csvResult = await csvExporter.ExportAsync(
                layer,
                execution.ResultQuery,
                repository.QueryAsync(service.Id, layer.Id, execution.ResultQuery, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return Results.File(csvResult.Content, "text/csv", csvResult.FileName);
        }

        if (execution.OutputFormat == WfsConstants.ShapefileFormat)
        {
            var shapefileResult = await shapefileExporter.ExportAsync(
                layer,
                execution.ResultQuery,
                execution.RequestedCrs,
                repository.QueryAsync(service.Id, layer.Id, execution.ResultQuery, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return Results.File(shapefileResult.Content, "application/zip", shapefileResult.FileName);
        }

        // Stream GeoJSON / GML responses
        var httpResponse = request.HttpContext.Response;
        var writerContext = new StreamingWriterContext
        {
            PropertyNames = execution.ResultQuery.PropertyNames,
            ReturnGeometry = true,
            TargetWkid = execution.Srid,
            TotalCount = result.NumberMatched,
            ExpectedFeatureCount = CalculateExpectedReturnCount(result.NumberMatched, execution.ResultQuery),
            Limit = execution.ResultQuery.Limit,
            Offset = execution.ResultQuery.Offset ?? 0,
            ServiceId = service.Id
        };

        var features = repository.QueryAsync(service.Id, layer.Id, execution.ResultQuery, cancellationToken);

        var loggerFactory = request.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();

        if (execution.OutputFormat.Equals(WfsConstants.GeoJsonFormat, StringComparison.OrdinalIgnoreCase))
        {
            httpResponse.ContentType = WfsConstants.GeoJsonFormat;
            httpResponse.Headers["Content-Crs"] = responseCrs;

            var writerLogger = loggerFactory.CreateLogger<GeoJsonFeatureCollectionStreamingWriter>();
            var writer = new GeoJsonFeatureCollectionStreamingWriter(writerLogger);
            await writer.WriteCollectionAsync(httpResponse.Body, features, layer, writerContext, cancellationToken).ConfigureAwait(false);
            return Results.Empty;
        }

        // Default to GML streaming
        httpResponse.ContentType = WfsConstants.GmlFormat;
        httpResponse.Headers["Content-Crs"] = responseCrs;

        var gmlLogger = loggerFactory.CreateLogger<GmlStreamingWriter>();
        var gmlWriter = new GmlStreamingWriter(gmlLogger);
        await gmlWriter.WriteCollectionAsync(httpResponse.Body, features, layer, writerContext, cancellationToken).ConfigureAwait(false);
        return Results.Empty;
    }

    /// <summary>
    /// Handles GetPropertyValue requests.
    /// </summary>
    public static async Task<IResult> HandleGetPropertyValueAsync(
        HttpRequest request,
        IQueryCollection query,
        [FromServices] ICatalogProjectionService catalog,
        [FromServices] IFeatureContextResolver contextResolver,
        [FromServices] IFeatureRepository repository,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(query);

        var valueReference = QueryParsingHelpers.GetQueryValue(query, "valueReference") ?? QueryParsingHelpers.GetQueryValue(query, "VALUEREFERENCE");
        if (valueReference.IsNullOrWhiteSpace())
        {
            return WfsHelpers.CreateException("MissingParameterValue", "valueReference", "Parameter 'valueReference' is required.");
        }

        var typeNamesRaw = QueryParsingHelpers.GetQueryValue(query, "typeNames") ?? QueryParsingHelpers.GetQueryValue(query, "typeName");
        if (typeNamesRaw.IsNullOrWhiteSpace())
        {
            return WfsHelpers.CreateException("MissingParameterValue", "typeNames", "Parameter 'typeNames' is required.");
        }

        var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeNamesRaw, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
        if (contextResult.IsFailure)
        {
            return WfsHelpers.MapResolutionError(contextResult.Error!, typeNamesRaw);
        }

        var featureContext = contextResult.Value;
        var layer = featureContext.Layer;

        var count = WfsHelpers.ParseLimit(query, "count", layer, featureContext.Service);
        var startIndex = WfsHelpers.ParseOffset(query, "startIndex");

        var featureQuery = new FeatureQuery(
            Limit: count,
            Offset: startIndex,
            PropertyNames: new[] { valueReference });

        var features = new List<object?>();
        await foreach (var record in repository.QueryAsync(featureContext.Service.Id, layer.Id, featureQuery, cancellationToken).ConfigureAwait(false))
        {
            if (record.Attributes.TryGetValue(valueReference, out var value))
            {
                features.Add(value);
            }
        }

        var doc = new System.Xml.Linq.XDocument(
            new System.Xml.Linq.XElement(WfsConstants.Wfs + "ValueCollection",
                new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "wfs", WfsConstants.Wfs),
                features.Select(v => new System.Xml.Linq.XElement(WfsConstants.Wfs + "member", v?.ToString() ?? string.Empty))));

        return Results.Content(doc.ToString(System.Xml.Linq.SaveOptions.DisableFormatting), "application/xml");
    }

    #region Query Execution

    /// <summary>
    /// Executes a feature query and returns the results.
    /// </summary>
    public static async Task<Result<FeatureQueryExecutionResult>> ExecuteFeatureQueryAsync(
        HttpRequest request,
        IQueryCollection query,
        ICatalogProjectionService catalog,
        IFeatureContextResolver contextResolver,
        IFeatureRepository repository,
        IMetadataRegistry registry,
        CancellationToken cancellationToken,
        bool materializeResults = true)
    {
        var buildResult = await BuildFeatureQueryExecutionAsync(request, query, catalog, contextResolver, registry, cancellationToken).ConfigureAwait(false);
        if (buildResult.IsFailure)
        {
            return Result<FeatureQueryExecutionResult>.Failure(buildResult.Error!);
        }

        var execution = buildResult.Value;
        var context = execution.Context;
        var numberMatched = await repository.CountAsync(context.Service.Id, context.Layer.Id, execution.CountQuery, cancellationToken).ConfigureAwait(false);

        var features = new List<WfsFeature>();
        if (materializeResults && execution.ResultQuery.ResultType == FeatureResultType.Results)
        {
            await foreach (var record in repository.QueryAsync(context.Service.Id, context.Layer.Id, execution.ResultQuery, cancellationToken).ConfigureAwait(false))
            {
                var geometry = WfsHelpers.TryReadGeometry(context.Layer, record, execution.Srid);
                features.Add(new WfsFeature(record, geometry));
            }
        }
        var featureCollection = materializeResults ? (IReadOnlyList<WfsFeature>)features : Array.Empty<WfsFeature>();
        return Result<FeatureQueryExecutionResult>.Success(new FeatureQueryExecutionResult(execution, numberMatched, featureCollection));
    }

    private static long? CalculateExpectedReturnCount(long numberMatched, FeatureQuery query)
    {
        if (query.ResultType != FeatureResultType.Results)
        {
            return 0;
        }

        var offset = query.Offset ?? 0;
        var available = Math.Max(0, numberMatched - offset);
        if (available == 0)
        {
            return 0;
        }

        if (query.Limit.HasValue)
        {
            return Math.Min(available, query.Limit.Value);
        }

        return available;
    }

    /// <summary>
    /// Builds the execution plan for a feature query.
    /// </summary>
    private static async Task<Result<FeatureQueryExecution>> BuildFeatureQueryExecutionAsync(
        HttpRequest request,
        IQueryCollection query,
        ICatalogProjectionService catalog,
        IFeatureContextResolver contextResolver,
        IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        // Check for stored query execution
        var storedQueryId = QueryParsingHelpers.GetQueryValue(query, "storedQuery_Id") ?? QueryParsingHelpers.GetQueryValue(query, "STOREDQUERY_ID");
        if (storedQueryId.HasValue())
        {
            return await BuildStoredQueryExecutionAsync(request, query, storedQueryId, catalog, contextResolver, registry, cancellationToken);
        }

        var typeNamesRaw = QueryParsingHelpers.GetQueryValue(query, "typeNames") ?? QueryParsingHelpers.GetQueryValue(query, "typeName");
        if (typeNamesRaw.IsNullOrWhiteSpace())
        {
            return Result<FeatureQueryExecution>.Failure(Error.Invalid("Parameter 'typeNames' is required."));
        }

        // WFS 2.0 supports multiple typeNames in a single request (comma-separated)
        var typeNames = QueryParsingHelpers.ParseCsv(typeNamesRaw);
        if (typeNames.Count > 1)
        {
            // Multiple feature types requested - not yet implemented
            // For now, return a descriptive error
            return Result<FeatureQueryExecution>.Failure(
                Error.Invalid("Multiple feature types in a single GetFeature request are not yet supported. " +
                             "Please query each feature type separately."));
        }

        var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeNamesRaw, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
        if (contextResult.IsFailure)
        {
            return Result<FeatureQueryExecution>.Failure(contextResult.Error!);
        }

        var context = contextResult.Value;
        var service = context.Service;
        var layer = context.Layer;

        var count = WfsHelpers.ParseLimit(query, "count", layer, service);
        var startIndex = WfsHelpers.ParseOffset(query, "startIndex");
        var srsName = QueryParsingHelpers.GetQueryValue(query, "srsName");
        var bbox = WfsHelpers.ParseBoundingBox(query);
        var filter = await WfsHelpers.BuildFilterAsync(request, query, layer, cancellationToken).ConfigureAwait(false);
        var resultType = WfsHelpers.ParseResultType(query);
        var outputFormatRaw = QueryParsingHelpers.GetQueryValue(query, "outputFormat");

        if (!WfsHelpers.TryNormalizeOutputFormat(outputFormatRaw, out var outputFormat))
        {
            return Result<FeatureQueryExecution>.Failure(Error.Invalid($"Output format '{outputFormatRaw}' is not supported."));
        }

        var requestedCrs = srsName.HasValue() ? srsName : service.Ogc.DefaultCrs;
        if (requestedCrs.IsNullOrWhiteSpace())
        {
            requestedCrs = "EPSG:4326";
        }

        var srid = CrsHelper.ParseCrs(requestedCrs);
        var urnCrs = WfsHelpers.ToUrn(requestedCrs);

        // Extract SQL view parameters if layer has SQL view
        // TODO: Re-implement ExtractSqlViewParameters
        IReadOnlyDictionary<string, string>? sqlViewParameters = null;

        var resultQuery = new FeatureQuery(
            Limit: count,
            Offset: startIndex,
            Bbox: bbox,
            Filter: filter,
            ResultType: resultType,
            Crs: requestedCrs,
            SqlViewParameters: sqlViewParameters);

        var countQuery = resultQuery with { Limit = null, Offset = null, ResultType = FeatureResultType.Hits };

        return Result<FeatureQueryExecution>.Success(new FeatureQueryExecution(context, resultQuery, countQuery, resultType, outputFormat, urnCrs, srid, requestedCrs));
    }

    /// <summary>
    /// Builds execution plan for a stored query.
    /// </summary>
    private static async Task<Result<FeatureQueryExecution>> BuildStoredQueryExecutionAsync(
        HttpRequest request,
        IQueryCollection query,
        string storedQueryId,
        ICatalogProjectionService catalog,
        IFeatureContextResolver contextResolver,
        IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        // Handle GetFeatureById (mandatory WFS 2.0 stored query)
        if (storedQueryId.EqualsIgnoreCase("urn:ogc:def:query:OGC-WFS::GetFeatureById"))
        {
            return await ExecuteGetFeatureByIdAsync(request, query, catalog, contextResolver, cancellationToken);
        }

        // Handle configured stored queries
        var snapshot = await registry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        // Find the stored query definition
        WfsStoredQueryDefinition? queryDefinition = null;
        ServiceDefinition? queryService = null;
        foreach (var svc in snapshot.Services)
        {
            var match = svc.Ogc.StoredQueries.FirstOrDefault(sq => sq.Id.EqualsIgnoreCase(storedQueryId));
            if (match is not null)
            {
                queryDefinition = match;
                queryService = svc;
                break;
            }
        }

        if (queryDefinition is null)
        {
            return Result<FeatureQueryExecution>.Failure(Error.Invalid($"Stored query '{storedQueryId}' is not supported."));
        }

        // Get layer context
        var typeNamesRaw = $"{queryService!.Id}:{queryDefinition.LayerId}";
        var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeNamesRaw, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
        if (contextResult.IsFailure)
        {
            return Result<FeatureQueryExecution>.Failure(contextResult.Error!);
        }

        var context = contextResult.Value;
        var service = context.Service;
        var layer = context.Layer;

        // Substitute parameters in CQL filter
        var cqlText = queryDefinition.FilterCql;
        foreach (var parameter in queryDefinition.Parameters)
        {
            var paramValue = QueryParsingHelpers.GetQueryValue(query, parameter.Name);
            if (paramValue.IsNullOrWhiteSpace())
            {
                return Result<FeatureQueryExecution>.Failure(Error.Invalid($"Parameter '{parameter.Name}' is required for stored query '{storedQueryId}'."));
            }

            // Simple placeholder substitution: ${paramName} -> value
            cqlText = cqlText.Replace($"${{{parameter.Name}}}", paramValue);
        }

        // Parse CQL filter
        QueryFilter? filter = null;
        try
        {
            filter = CqlFilterParser.Parse(cqlText, layer);
        }
        catch (Exception ex)
        {
            return Result<FeatureQueryExecution>.Failure(Error.Invalid($"Failed to parse stored query filter: {ex.Message}"));
        }

        var srsName = QueryParsingHelpers.GetQueryValue(query, "srsName");
        var outputFormatRaw = QueryParsingHelpers.GetQueryValue(query, "outputFormat");

        if (!WfsHelpers.TryNormalizeOutputFormat(outputFormatRaw, out var outputFormat))
        {
            return Result<FeatureQueryExecution>.Failure(Error.Invalid($"Output format '{outputFormatRaw}' is not supported."));
        }

        var requestedCrs = srsName.HasValue() ? srsName : service.Ogc.DefaultCrs;
        if (requestedCrs.IsNullOrWhiteSpace())
        {
            requestedCrs = "EPSG:4326";
        }

        var srid = CrsHelper.ParseCrs(requestedCrs);
        var urnCrs = WfsHelpers.ToUrn(requestedCrs);

        var resultQuery = new FeatureQuery(
            Limit: null,
            Filter: filter,
            ResultType: FeatureResultType.Results,
            Crs: requestedCrs);

        var countQuery = resultQuery with { ResultType = FeatureResultType.Hits };

        return Result<FeatureQueryExecution>.Success(new FeatureQueryExecution(context, resultQuery, countQuery, FeatureResultType.Results, outputFormat, urnCrs, srid, requestedCrs));
    }

    /// <summary>
    /// Executes GetFeatureById stored query.
    /// </summary>
    private static async Task<Result<FeatureQueryExecution>> ExecuteGetFeatureByIdAsync(
        HttpRequest request,
        IQueryCollection query,
        ICatalogProjectionService catalog,
        IFeatureContextResolver contextResolver,
        CancellationToken cancellationToken)
    {
        // Get the id parameter
        var featureId = QueryParsingHelpers.GetQueryValue(query, "id") ?? QueryParsingHelpers.GetQueryValue(query, "ID");
        if (featureId.IsNullOrWhiteSpace())
        {
            return Result<FeatureQueryExecution>.Failure(Error.Invalid("Parameter 'id' is required for GetFeatureById stored query."));
        }

        // Get typeNames to identify which layer to query
        var typeNamesRaw = QueryParsingHelpers.GetQueryValue(query, "typeNames") ?? QueryParsingHelpers.GetQueryValue(query, "typeName");
        if (typeNamesRaw.IsNullOrWhiteSpace())
        {
            return Result<FeatureQueryExecution>.Failure(Error.Invalid("Parameter 'typeNames' is required."));
        }

        var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeNamesRaw, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
        if (contextResult.IsFailure)
        {
            return Result<FeatureQueryExecution>.Failure(contextResult.Error!);
        }

        var context = contextResult.Value;
        var service = context.Service;
        var layer = context.Layer;

        var srsName = QueryParsingHelpers.GetQueryValue(query, "srsName");
        var outputFormatRaw = QueryParsingHelpers.GetQueryValue(query, "outputFormat");

        if (!WfsHelpers.TryNormalizeOutputFormat(outputFormatRaw, out var outputFormat))
        {
            return Result<FeatureQueryExecution>.Failure(Error.Invalid($"Output format '{outputFormatRaw}' is not supported."));
        }

        var requestedCrs = srsName.HasValue() ? srsName : service.Ogc.DefaultCrs;
        if (requestedCrs.IsNullOrWhiteSpace())
        {
            requestedCrs = "EPSG:4326";
        }

        var srid = CrsHelper.ParseCrs(requestedCrs);
        var urnCrs = WfsHelpers.ToUrn(requestedCrs);

        // Build a filter to get feature by ID
        var idField = layer.IdField;
        var expression = new QueryBinaryExpression(
            new QueryFieldReference(idField),
            QueryBinaryOperator.Equal,
            new QueryConstant(featureId));
        var filter = new QueryFilter(expression);

        var resultQuery = new FeatureQuery(
            Limit: 1,
            Filter: filter,
            ResultType: FeatureResultType.Results,
            Crs: requestedCrs);

        var countQuery = resultQuery with { Limit = null, ResultType = FeatureResultType.Hits };

        return Result<FeatureQueryExecution>.Success(new FeatureQueryExecution(context, resultQuery, countQuery, FeatureResultType.Results, outputFormat, urnCrs, srid, requestedCrs));
    }

    #endregion
}
