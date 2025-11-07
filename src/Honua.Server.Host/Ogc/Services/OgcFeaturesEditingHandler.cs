// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Data;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling OGC API Features editing and mutation operations.
/// Provides ETag-based optimistic concurrency control and feature mutation response building.
/// Extracted from OgcSharedHandlers to enable dependency injection and testability.
/// </summary>
internal sealed class OgcFeaturesEditingHandler : IOgcFeaturesEditingHandler
{
    private readonly IOgcFeaturesGeoJsonHandler _geoJsonHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="OgcFeaturesEditingHandler"/> class.
    /// </summary>
    /// <param name="geoJsonHandler">GeoJSON handler for feature serialization</param>
    public OgcFeaturesEditingHandler(IOgcFeaturesGeoJsonHandler geoJsonHandler)
    {
        _geoJsonHandler = Guard.NotNull(geoJsonHandler);
    }

    /// <inheritdoc />
    public IResult CreateEditFailureProblem(FeatureEditError? error, int statusCode)
    {
        if (error is null)
        {
            return Results.Problem("Feature edit failed.", statusCode: statusCode, title: "Feature edit failed");
        }

        var details = error.Details is not null && error.Details.Count > 0
            ? string.Join(",", error.Details.Select(pair => pair.Key.IsNullOrWhiteSpace() ? pair.Value : $"{pair.Key}:{pair.Value}"))
            : null;

        if (details is null)
        {
            return Results.Problem(detail: error.Message, statusCode: statusCode, title: "Feature edit failed");
        }

        var extensions = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["details"] = details
        };

        return Results.Problem(detail: error.Message, statusCode: statusCode, title: "Feature edit failed", extensions: extensions);
    }

    /// <inheritdoc />
    public FeatureEditBatch CreateFeatureEditBatch(
        IReadOnlyList<FeatureEditCommand> commands,
        HttpRequest request)
    {
        return new FeatureEditBatch(
            commands,
            rollbackOnFailure: true,
            clientReference: null,
            isAuthenticated: request.HttpContext.User?.Identity?.IsAuthenticated ?? false,
            userRoles: UserIdentityHelper.ExtractUserRoles(request.HttpContext.User));
    }

    /// <inheritdoc />
    public async Task<List<(string? FeatureId, object Payload, string? Etag)>> FetchCreatedFeaturesWithETags(
        IFeatureRepository repository,
        FeatureContext context,
        LayerDefinition layer,
        string collectionId,
        FeatureEditBatchResult editResult,
        List<string?> fallbackIds,
        FeatureQuery featureQuery,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var created = new List<(string? FeatureId, object Payload, string? Etag)>();

        for (var index = 0; index < editResult.Results.Count; index++)
        {
            var result = editResult.Results[index];
            var featureId = result.FeatureId ?? fallbackIds.ElementAtOrDefault(index);
            if (featureId.IsNullOrWhiteSpace())
            {
                continue;
            }

            var record = await repository.GetAsync(context.Service.Id, layer.Id, featureId!, featureQuery, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                continue;
            }

            var payload = _geoJsonHandler.ToFeature(request, collectionId, layer, record, featureQuery);
            var etag = ComputeFeatureEtag(layer, record);
            created.Add((featureId, payload, etag));
        }

        // If no features were fetched successfully, return minimal response with IDs only
        if (created.Count == 0)
        {
            foreach (var result in editResult.Results)
            {
                created.Add((result.FeatureId, new { id = result.FeatureId }, null));
            }
        }

        return created;
    }

    /// <inheritdoc />
    public IResult BuildMutationResponse(
        List<(string? FeatureId, object Payload, string? Etag)> createdFeatures,
        string collectionId,
        bool singleItemMode)
    {
        if (singleItemMode && createdFeatures.Count == 1)
        {
            var (featureId, payload, etag) = createdFeatures[0];
            var location = featureId.IsNullOrWhiteSpace()
                ? null
                : $"/ogc/collections/{collectionId}/items/{featureId}";

            var response = Results.Created(location, payload);
            if (etag.HasValue())
            {
                response = OgcSharedHandlers.WithResponseHeader(response, HeaderNames.ETag, etag);
            }

            return response;
        }

        var collectionResponse = new
        {
            type = "FeatureCollection",
            features = createdFeatures.Select(entry => entry.Payload)
        };

        return Results.Created($"/ogc/collections/{collectionId}/items", collectionResponse);
    }

    /// <inheritdoc />
    public bool ValidateIfMatch(HttpRequest request, LayerDefinition layer, FeatureRecord record, out string currentEtag)
    {
        currentEtag = ComputeFeatureEtag(layer, record);

        if (!request.Headers.TryGetValue(HeaderNames.IfMatch, out var headerValues) || headerValues.Count == 0)
        {
            return true;
        }

        var normalizedCurrent = NormalizeEtagValue(currentEtag);
        foreach (var rawValue in headerValues.SelectMany(value =>
                     value is not null ? QueryParsingHelpers.ParseCsv(value) : Array.Empty<string>()))
        {
            var normalizedRequested = NormalizeEtagValue(rawValue);
            if (string.Equals(normalizedRequested, "*", StringComparison.Ordinal) ||
                string.Equals(normalizedRequested, normalizedCurrent, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public string ComputeFeatureEtag(LayerDefinition layer, FeatureRecord record)
    {
        var ordered = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in record.Attributes)
        {
            ordered[pair.Key] = pair.Value;
        }

        string json;
        try
        {
            json = JsonSerializer.Serialize(ordered, JsonSerializerOptionsRegistry.Web);
        }
        catch (NotSupportedException)
        {
            json = JsonSerializer.Serialize(ordered, RuntimeSerializerOptions);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"W/\"{Convert.ToHexString(hash)}\"";
    }

    private static string NormalizeEtagValue(string? etag)
    {
        if (etag.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        var trimmed = etag.Trim();
        if (trimmed.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..].Trim();
        }

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed;
    }

    private static readonly JsonSerializerOptions RuntimeSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 64,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
