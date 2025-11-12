// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains feature editing and mutation methods.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcSharedHandlers
{
    internal static async Task<JsonDocument?> ParseJsonDocumentAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // BUG FIX #31: SECURITY - DoS prevention for GeoJSON upload endpoints
        // Previous implementation buffered entire request body without size validation,
        // allowing attackers to exhaust memory with multi-GB JSON payloads.
        //
        // Security measures:
        // 1. Check Content-Length before buffering (fail fast)
        // 2. Configurable maximum size (default 100 MB)
        // 3. Return HTTP 413 Payload Too Large for oversized requests
        // 4. Prevent memory exhaustion before Kestrel's limits

        const long DefaultMaxSizeBytes = 100 * 1024 * 1024; // 100 MB
        var maxSize = DefaultMaxSizeBytes;

        // Try to get configured limit from request services (if available)
        var config = request.HttpContext.RequestServices
            .GetService(typeof(Honua.Server.Core.Configuration.HonuaConfiguration))
            as Honua.Server.Core.Configuration.HonuaConfiguration;

        if (config?.Services?.OgcApi?.MaxFeatureUploadSizeBytes > 0)
        {
            maxSize = config.Services.OgcApi.MaxFeatureUploadSizeBytes;
        }

        // Check Content-Length header before buffering
        if (request.ContentLength.HasValue && request.ContentLength.Value > maxSize)
        {
            throw new InvalidOperationException(
                $"Request body size ({request.ContentLength.Value:N0} bytes) exceeds maximum allowed size " +
                $"({maxSize:N0} bytes). To upload larger files, increase OgcApi.MaxFeatureUploadSizeBytes in configuration.");
        }

        request.EnableBuffering(maxSize);
        request.Body.Seek(0, SeekOrigin.Begin);

        try
        {
            // Additional safety: limit how much we'll actually read from the stream
            // This protects against cases where Content-Length is missing or incorrect
            var options = new JsonDocumentOptions
            {
                MaxDepth = 256 // Prevent deeply nested JSON attacks
            };

            return await JsonDocument.ParseAsync(request.Body, options, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            request.Body.Seek(0, SeekOrigin.Begin);
        }
    }
    internal static IResult CreateEditFailureProblem(FeatureEditError? error, int statusCode)
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
    internal static FeatureEditBatch CreateFeatureEditBatch(
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

    internal static async Task<List<(string? FeatureId, object Payload, string? Etag)>> FetchCreatedFeaturesWithETags(
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

            var payload = ToFeature(request, collectionId, layer, record, featureQuery);
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

    internal static IResult BuildMutationResponse(
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
                response = WithResponseHeader(response, HeaderNames.ETag, etag);
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

    internal static bool ValidateIfMatch(HttpRequest request, LayerDefinition layer, FeatureRecord record, out string currentEtag)
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

    internal static string ComputeFeatureEtag(LayerDefinition layer, FeatureRecord record)
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
}
