// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer editing operations.
/// Extracted from GeoservicesRESTFeatureServerController to follow Single Responsibility Principle.
/// </summary>
public sealed class GeoservicesEditingService : IGeoservicesEditingService
{
    private const string GlobalIdFieldName = "globalId";

    private readonly IFeatureEditOrchestrator _editOrchestrator;
    private readonly IFeatureRepository _repository;
    private readonly ILogger<GeoservicesEditingService> _logger;

    public GeoservicesEditingService(
        IFeatureEditOrchestrator editOrchestrator,
        IFeatureRepository repository,
        ILogger<GeoservicesEditingService> logger)
    {
        _editOrchestrator = Guard.NotNull(editOrchestrator);
        _repository = Guard.NotNull(repository);
        _logger = Guard.NotNull(logger);
    }

    public async Task<GeoservicesEditExecutionResult> ExecuteEditsAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        JsonElement payload,
        HttpRequest request,
        string[] addPropertyNames,
        string[] updatePropertyNames,
        string[] deletePropertyNames,
        bool includeAdds,
        bool includeUpdates,
        bool includeDeletes,
        CancellationToken cancellationToken)
    {
        var contexts = new List<EditCommandContext>();
        var pendingResults = new List<(FeatureEditCommandResult Result, FeatureEditOperation Operation, string? FallbackId, string? FallbackGlobalId)>();
        var returnEditMoment = ResolveReturnEditMoment(payload, request);
        var useGlobalIds = ResolveUseGlobalIds(payload, request);

        // SECURITY: Count and validate total edit operations before processing
        var addCount = 0;
        var updateCount = 0;
        var deleteCount = 0;

        if (includeAdds && TryGetArrayProperty(payload, addPropertyNames, out var addsArray))
        {
            addCount = addsArray.GetArrayLength();
        }

        if (includeUpdates && TryGetArrayProperty(payload, updatePropertyNames, out var updatesArray))
        {
            updateCount = updatesArray.GetArrayLength();
        }

        if (includeDeletes)
        {
            if (TryGetDeleteElement(payload, deletePropertyNames, out var deleteElement))
            {
                deleteCount += ParseDeleteIds(deleteElement).Count();
            }
            deleteCount += ParseDeleteIdsFromQuery(request.Query, deletePropertyNames).Count();
        }

        GeoservicesRESTInputValidator.ValidateEditOperationCount(addCount, updateCount, deleteCount, request.HttpContext, _logger);

        if (includeAdds)
        {
            PopulateAddCommands(payload, addPropertyNames, serviceView.Service.Id, layerView.Layer.Id, layerView.Layer, contexts, useGlobalIds, request.HttpContext, _logger);
        }

        if (includeUpdates)
        {
            PopulateUpdateCommands(payload, updatePropertyNames, serviceView.Service.Id, layerView.Layer.Id, layerView.Layer, contexts, pendingResults, useGlobalIds, request.HttpContext, _logger);
        }

        if (includeDeletes)
        {
            PopulateDeleteCommands(payload, deletePropertyNames, request, serviceView.Service.Id, layerView.Layer.Id, contexts, useGlobalIds);
        }

        if (useGlobalIds)
        {
            await NormalizeGlobalIdCommandsAsync(contexts, serviceView, layerView, cancellationToken).ConfigureAwait(false);
        }

        var clientReference = payload.TryGetProperty("clientReference", out var clientReferenceElement) && clientReferenceElement.ValueKind == JsonValueKind.String
            ? clientReferenceElement.GetString()
            : null;

        var rollbackOnFailure = ResolveRollbackPreference(payload, request);

        var executableContexts = contexts.Where(ctx => !ctx.SkipExecution).ToList();
        var commands = executableContexts.Select(ctx => ctx.Command).ToList();

        FeatureEditBatchResult orchestratorResult;
        if (commands.Count > 0)
        {
            var user = request.HttpContext.User;
            var batch = new FeatureEditBatch(
                commands,
                rollbackOnFailure,
                clientReference,
                user?.Identity?.IsAuthenticated ?? false,
                UserIdentityHelper.ExtractUserRoles(user));

            orchestratorResult = await _editOrchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            orchestratorResult = new FeatureEditBatchResult(Array.Empty<FeatureEditCommandResult>());
        }

        var addResults = new List<object>();
        var updateResults = new List<object>();
        var deleteResults = new List<object>();

        var executedIndex = 0;
        foreach (var context in contexts)
        {
            FeatureEditCommandResult result;
            if (context.SkipExecution)
            {
                result = context.PrecomputedResult ?? FeatureEditCommandResult.CreateFailure(context.Command, FeatureEditError.NotImplemented);
            }
            else
            {
                result = executedIndex < orchestratorResult.Results.Count
                    ? orchestratorResult.Results[executedIndex]
                    : FeatureEditCommandResult.CreateFailure(context.Command, FeatureEditError.NotImplemented);
                executedIndex++;
            }

            context.FallbackGlobalId ??= TryGetAttributeValue(context.Command, GlobalIdFieldName);

            AppendEditResult(
                context.Command.Operation,
                result,
                context.FallbackObjectId,
                context.FallbackGlobalId,
                context.Command,
                addResults,
                updateResults,
                deleteResults);
        }

        foreach (var pending in pendingResults)
        {
            AppendEditResult(
                pending.Operation,
                pending.Result,
                pending.FallbackId,
                pending.FallbackGlobalId,
                pending.Result.Command,
                addResults,
                updateResults,
                deleteResults);
        }

        var hasOperations = contexts.Any(ctx => !ctx.SkipExecution) || pendingResults.Count > 0;
        var editMoment = hasOperations ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;

        return new GeoservicesEditExecutionResult(
            addResults.AsReadOnly(),
            updateResults.AsReadOnly(),
            deleteResults.AsReadOnly(),
            hasOperations,
            returnEditMoment,
            editMoment);
    }

    private static void PopulateAddCommands(
        JsonElement root,
        IReadOnlyList<string> propertyNames,
        string serviceId,
        string layerId,
        LayerDefinition layer,
        List<EditCommandContext> contexts,
        bool useGlobalIds,
        HttpContext? httpContext = null,
        ILogger? logger = null)
    {
        if (!TryGetArrayProperty(root, propertyNames, out var addsElement))
        {
            return;
        }

        foreach (var editElement in addsElement.EnumerateArray())
        {
            if (editElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var attributes = ReadAttributeMap(editElement);
            AttachGeometry(editElement, layer.GeometryField, attributes, httpContext, logger);
            var fallbackId = TryExtractId(attributes, layer.IdField, remove: false);
            var globalId = TryExtractGlobalId(attributes, remove: false);
            if (globalId.IsNullOrWhiteSpace())
            {
                globalId = Guid.NewGuid().ToString();
            }
            attributes[GlobalIdFieldName] = globalId;

            var command = new AddFeatureCommand(serviceId, layerId, attributes);
            contexts.Add(new EditCommandContext(command, fallbackId, globalId, globalId, useGlobalIds));
        }
    }

    private static void PopulateUpdateCommands(
        JsonElement root,
        IReadOnlyList<string> propertyNames,
        string serviceId,
        string layerId,
        LayerDefinition layer,
        List<EditCommandContext> contexts,
        List<(FeatureEditCommandResult Result, FeatureEditOperation Operation, string? FallbackId, string? FallbackGlobalId)> pendingResults,
        bool useGlobalIds,
        HttpContext? httpContext = null,
        ILogger? logger = null)
    {
        if (!TryGetArrayProperty(root, propertyNames, out var updatesElement))
        {
            return;
        }

        foreach (var editElement in updatesElement.EnumerateArray())
        {
            if (editElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var attributes = ReadAttributeMap(editElement);
            AttachGeometry(editElement, layer.GeometryField, attributes);

            var fallbackId = ResolveFeatureIdForUpdate(editElement, attributes, layer.IdField, out var requestedGlobalId);

            // Extract version for optimistic concurrency control
            object? version = null;
            if (editElement.TryGetProperty("version", out var versionElement) && versionElement.ValueKind != JsonValueKind.Null)
            {
                version = versionElement.ValueKind switch
                {
                    JsonValueKind.Number => versionElement.TryGetInt64(out var longVal) ? longVal : (object?)versionElement.GetDouble(),
                    JsonValueKind.String => versionElement.GetString(),
                    _ => null
                };
            }

            if (fallbackId.IsNullOrWhiteSpace())
            {
                if (useGlobalIds && requestedGlobalId.HasValue())
                {
                    var updateWithGlobalId = new UpdateFeatureCommand(serviceId, layerId, requestedGlobalId!, attributes, version);
                    var context = new EditCommandContext(updateWithGlobalId, null, requestedGlobalId, requestedGlobalId, true);
                    contexts.Add(context);
                    continue;
                }

                var error = new FeatureEditError("missing_identifier", "Feature identifier is required for update operations.");
                var command = new UpdateFeatureCommand(serviceId, layerId, "_unknown_", attributes, version);
                var result = FeatureEditCommandResult.CreateFailure(command, error);
                pendingResults.Add((result, FeatureEditOperation.Update, fallbackId, requestedGlobalId));
                continue;
            }

            var updateCommand = new UpdateFeatureCommand(serviceId, layerId, fallbackId, attributes, version);
            contexts.Add(new EditCommandContext(updateCommand, fallbackId, requestedGlobalId, requestedGlobalId, useGlobalIds));
        }
    }

    private static void PopulateDeleteCommands(
        JsonElement root,
        IReadOnlyList<string> propertyNames,
        HttpRequest request,
        string serviceId,
        string layerId,
        List<EditCommandContext> contexts,
        bool useGlobalIds)
    {
        var idsFromQuery = ParseDeleteIdsFromQuery(request.Query, propertyNames);
        var idsFromPayload = TryGetDeleteElement(root, propertyNames, out var deleteElement)
            ? ParseDeleteIds(deleteElement)
            : Enumerable.Empty<string?>();

        var allIds = idsFromQuery.Concat(idsFromPayload)
            .Where(id => id.HasValue())
            .Select(id => NormalizeDeleteIdentifier(id, useGlobalIds))
            .Where(id => id.HasValue())
            .Distinct()
            .ToList();

        foreach (var id in allIds)
        {
            if (id.IsNullOrWhiteSpace())
            {
                continue;
            }

            var command = new DeleteFeatureCommand(serviceId, layerId, id);
            var fallbackObjectId = useGlobalIds ? null : id;
            var fallbackGlobalId = useGlobalIds ? id : null;
            var requestedGlobalId = useGlobalIds ? id : null;

            var context = new EditCommandContext(command, fallbackObjectId, fallbackGlobalId, requestedGlobalId, useGlobalIds);
            contexts.Add(context);
        }
    }

    private async Task NormalizeGlobalIdCommandsAsync(
        List<EditCommandContext> contexts,
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        CancellationToken cancellationToken)
    {
        foreach (var context in contexts.Where(ctx => ctx.UseGlobalIds && ctx.RequestedGlobalId.HasValue()))
        {
            var objectId = await ResolveObjectIdByGlobalIdAsync(serviceView.Service.Id, layerView.Layer, context.RequestedGlobalId!, cancellationToken).ConfigureAwait(false);

            if (objectId.HasValue())
            {
                context.Command = context.Command.Operation switch
                {
                    FeatureEditOperation.Update => new UpdateFeatureCommand(context.Command.ServiceId, context.Command.LayerId, objectId, ((UpdateFeatureCommand)context.Command).Attributes),
                    FeatureEditOperation.Delete => new DeleteFeatureCommand(context.Command.ServiceId, context.Command.LayerId, objectId),
                    _ => context.Command
                };

                context.FallbackObjectId = objectId;
            }
            else if (context.Command.Operation == FeatureEditOperation.Update)
            {
                context.SkipExecution = true;
                context.PrecomputedResult = FeatureEditCommandResult.CreateFailure(context.Command, new FeatureEditError("feature_not_found", "Feature not found for the specified GlobalID."));
            }
            else if (context.Command.Operation == FeatureEditOperation.Delete)
            {
                context.FallbackObjectId ??= context.FallbackGlobalId;
                context.SkipExecution = true;
                context.PrecomputedResult = FeatureEditCommandResult.CreateFailure(context.Command, new FeatureEditError("feature_not_found", "Feature not found for the specified GlobalID."));
            }
        }
    }

    private async Task<string?> ResolveObjectIdByGlobalIdAsync(
        string serviceId,
        LayerDefinition layer,
        string globalId,
        CancellationToken cancellationToken)
    {
        var query = new FeatureQuery(
            Filter: new QueryFilter(new QueryBinaryExpression(
                new QueryFieldReference(GlobalIdFieldName),
                QueryBinaryOperator.Equal,
                new QueryConstant(globalId))),
            PropertyNames: new[] { layer.IdField },
            Limit: 1);

        await foreach (var record in _repository.QueryAsync(serviceId, layer.Id, query, cancellationToken).ConfigureAwait(false))
        {
            if (record.Attributes.TryGetValue(layer.IdField, out var value) && value != null)
            {
                return ConvertToInvariantString(value);
            }
        }

        return null;
    }

    private static Dictionary<string, object?> ReadAttributeMap(JsonElement editElement)
    {
        if (!editElement.TryGetProperty("attributes", out var attrElement) || attrElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object?>();
        }

        return ConvertObject(attrElement);
    }

    private static Dictionary<string, object?> ConvertObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = JsonElementConverter.ToObject(property.Value);
        }
        return result;
    }

    private static void AttachGeometry(JsonElement editElement, string geometryField, IDictionary<string, object?> attributes, HttpContext? httpContext = null, ILogger? logger = null)
    {
        if (!editElement.TryGetProperty("geometry", out var geometryElement) || geometryElement.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        try
        {
            // Convert JsonElement to JsonNode, then use GeoJson reader to parse
            var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(geometryElement.GetRawText());
            if (jsonNode != null)
            {
                var geoJsonReader = new GeoJsonReader();
                var geom = geoJsonReader.Read<Geometry>(jsonNode.ToJsonString());
                if (geom != null)
                {
                    // SECURITY: Validate geometry complexity before accepting
                    GeoservicesRESTInputValidator.ValidateGeometryComplexity(geom, httpContext, logger);
                    attributes[geometryField] = geom;
                }
            }
        }
        catch (GeoservicesRESTQueryException)
        {
            // Re-throw validation errors
            throw;
        }
        catch (Exception)
        {
            // Ignore invalid geometry
        }
    }

    private static string? ResolveFeatureIdForUpdate(JsonElement editElement, Dictionary<string, object?> attributes, string idField, out string? requestedGlobalId)
    {
        requestedGlobalId = TryExtractGlobalId(attributes, remove: false);

        var fallbackId = TryExtractId(attributes, idField, remove: false);

        if (fallbackId.HasValue())
        {
            return fallbackId;
        }

        if (editElement.TryGetProperty("objectId", out var objectIdElement) && objectIdElement.ValueKind != JsonValueKind.Null)
        {
            return ConvertJsonElementToString(objectIdElement);
        }

        return null;
    }

    private static IEnumerable<string?> ParseDeleteIdsFromQuery(IQueryCollection query, IReadOnlyList<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (query.TryGetValue(propertyName, out var values))
            {
                foreach (var value in values)
                {
                    foreach (var id in ParseDeleteIds(value))
                    {
                        yield return id;
                    }
                }
            }
        }
    }

    private static IEnumerable<string?> ParseDeleteIds(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Null)
                {
                    yield return ConvertJsonElementToString(item);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString();
            foreach (var id in ParseDeleteIds(raw))
            {
                yield return id;
            }
        }
        else if (element.ValueKind != JsonValueKind.Null)
        {
            yield return ConvertJsonElementToString(element);
        }
    }

    private static IEnumerable<string?> ParseDeleteIds(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            yield break;
        }

        foreach (var segment in QueryParsingHelpers.ParseCsv(raw))
        {
            yield return segment;
        }
    }

    private static string? NormalizeDeleteIdentifier(string? value, bool useGlobalIds)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return null;
        }

        return useGlobalIds ? GeoservicesGlobalIdHelper.NormalizeGlobalId(value) : value.Trim();
    }


    private static bool TryGetArrayProperty(JsonElement root, IReadOnlyList<string> propertyNames, out JsonElement array)
    {
        foreach (var propertyName in propertyNames)
        {
            if (root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Array)
            {
                array = element;
                return true;
            }
        }

        array = default;
        return false;
    }

    private static bool TryGetDeleteElement(JsonElement root, IReadOnlyList<string> propertyNames, out JsonElement element)
    {
        foreach (var propertyName in propertyNames)
        {
            if (root.TryGetProperty(propertyName, out var candidate) && candidate.ValueKind != JsonValueKind.Null)
            {
                element = candidate;
                return true;
            }
        }

        element = default;
        return false;
    }

    private static bool ResolveReturnEditMoment(JsonElement root, HttpRequest request)
    {
        if (root.TryGetProperty("returnEditMoment", out var element))
        {
            return InterpretBoolean(element, false);
        }

        if (request.Query.TryGetValue("returnEditMoment", out var values) && values.Count > 0)
        {
            return values[0].EqualsIgnoreCase("true");
        }

        return false;
    }

    private static bool ResolveUseGlobalIds(JsonElement root, HttpRequest request)
    {
        if (root.TryGetProperty("useGlobalIds", out var element))
        {
            return InterpretBoolean(element, false);
        }

        if (request.Query.TryGetValue("useGlobalIds", out var values) && values.Count > 0)
        {
            return values[0].EqualsIgnoreCase("true");
        }

        return false;
    }

    private static bool ResolveRollbackPreference(JsonElement root, HttpRequest request)
    {
        if (root.TryGetProperty("rollbackOnFailure", out var element))
        {
            return InterpretBoolean(element, true);
        }

        if (request.Query.TryGetValue("rollbackOnFailure", out var values) && values.Count > 0)
        {
            return !values[0].EqualsIgnoreCase("false");
        }

        return true;
    }


    private static void AppendEditResult(
        FeatureEditOperation operation,
        FeatureEditCommandResult result,
        string? fallbackId,
        string? fallbackGlobalId,
        FeatureEditCommand command,
        List<object> addResults,
        List<object> updateResults,
        List<object> deleteResults)
    {
        var payload = BuildEditResult(command, result, fallbackId, fallbackGlobalId);

        switch (operation)
        {
            case FeatureEditOperation.Add:
                addResults.Add(payload);
                break;
            case FeatureEditOperation.Update:
                updateResults.Add(payload);
                break;
            case FeatureEditOperation.Delete:
                deleteResults.Add(payload);
                break;
        }
    }

    private static object BuildEditResult(FeatureEditCommand command, FeatureEditCommandResult result, string? fallbackId, string? fallbackGlobalId)
    {
        if (result.Success)
        {
            var response = new Dictionary<string, object?>
            {
                ["objectId"] = result.FeatureId ?? fallbackId,
                ["globalId"] = fallbackGlobalId,
                ["success"] = true
            };

            // Include version for optimistic concurrency control (Esri GeoServices extension)
            if (result.Version != null)
            {
                response["version"] = result.Version;
            }

            return response;
        }

        return new Dictionary<string, object?>
        {
            ["objectId"] = result.FeatureId ?? fallbackId,
            ["globalId"] = fallbackGlobalId,
            ["success"] = false,
            ["error"] = CreateErrorPayload(result.Error ?? new FeatureEditError("unknown_error", "An unknown error occurred."))
        };
    }

    private static object CreateErrorPayload(FeatureEditError error)
    {
        // Map internal error codes to Esri GeoServices error codes
        var esriErrorCode = MapToEsriErrorCode(error.Code);

        var errorPayload = new Dictionary<string, object?>
        {
            ["code"] = esriErrorCode,
            ["description"] = error.Message ?? "An error occurred during the edit operation."
        };

        // Include additional details for version conflicts
        if (error.Details != null && error.Details.Count > 0)
        {
            errorPayload["details"] = error.Details;
        }

        return errorPayload;
    }

    private static int MapToEsriErrorCode(string internalCode)
    {
        // Esri GeoServices REST API error codes
        // https://developers.arcgis.com/rest/services-reference/enterprise/error-codes.htm
        return internalCode switch
        {
            "version_conflict" => 409,  // Conflict - concurrent modification detected
            "not_found" => 404,         // Feature not found
            "missing_identifier" => 400, // Bad request - missing required parameter
            "feature_not_found" => 404,  // Feature not found
            "not_implemented" => 501,    // Not implemented
            "edit_failed" => 500,        // Internal server error
            "batch_aborted" => 500,      // Internal server error
            _ => 400                     // Default to bad request
        };
    }

    private static bool InterpretBoolean(JsonElement element, bool defaultValue)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString().EqualsIgnoreCase("true"),
            _ => defaultValue
        };
    }

    private static string? TryExtractId(IDictionary<string, object?> attributes, string idField, bool remove)
    {
        if (attributes.TryGetValue(idField, out var value))
        {
            if (remove)
            {
                attributes.Remove(idField);
            }
            return ConvertToInvariantString(value);
        }

        return null;
    }

    private static string? TryExtractGlobalId(IDictionary<string, object?> attributes, bool remove)
    {
        if (attributes.TryGetValue(GlobalIdFieldName, out var value))
        {
            if (remove)
            {
                attributes.Remove(GlobalIdFieldName);
            }
            return ConvertToInvariantString(value);
        }

        return null;
    }

    private static string? ConvertJsonElementToString(JsonElement element)
    {
        return JsonElementConverter.ToString(element);
    }

    private static string? ConvertToInvariantString(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value is IConvertible convertible
            ? convertible.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : value.ToString();
    }

    private static string? TryGetAttributeValue(FeatureEditCommand command, string attributeName)
    {
        var attributes = command switch
        {
            AddFeatureCommand add => add.Attributes,
            UpdateFeatureCommand update => update.Attributes,
            _ => null
        };

        if (attributes?.TryGetValue(attributeName, out var value) == true)
        {
            return ConvertToInvariantString(value);
        }

        return null;
    }

    private sealed class EditCommandContext
    {
        public FeatureEditCommand Command { get; set; }
        public string? FallbackObjectId { get; set; }
        public string? FallbackGlobalId { get; set; }
        public string? RequestedGlobalId { get; }
        public bool UseGlobalIds { get; }
        public bool SkipExecution { get; set; }
        public FeatureEditCommandResult? PrecomputedResult { get; set; }

        public EditCommandContext(
            FeatureEditCommand command,
            string? fallbackObjectId,
            string? fallbackGlobalId,
            string? requestedGlobalId,
            bool useGlobalIds)
        {
            Command = command;
            FallbackObjectId = fallbackObjectId;
            FallbackGlobalId = fallbackGlobalId;
            RequestedGlobalId = requestedGlobalId;
            UseGlobalIds = useGlobalIds;
        }
    }
}
