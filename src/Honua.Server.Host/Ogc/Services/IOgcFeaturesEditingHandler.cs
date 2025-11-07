// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Host.Data;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling OGC API Features editing and mutation operations.
/// Provides ETag-based optimistic concurrency control and feature mutation response building.
/// </summary>
internal interface IOgcFeaturesEditingHandler
{
    /// <summary>
    /// Creates a problem details response for feature edit failures.
    /// </summary>
    /// <param name="error">The feature edit error details</param>
    /// <param name="statusCode">The HTTP status code</param>
    /// <returns>Problem details result</returns>
    IResult CreateEditFailureProblem(FeatureEditError? error, int statusCode);

    /// <summary>
    /// Creates a feature edit batch command from a list of edit commands.
    /// </summary>
    /// <param name="commands">List of feature edit commands</param>
    /// <param name="request">HTTP request for extracting user context</param>
    /// <returns>Feature edit batch</returns>
    FeatureEditBatch CreateFeatureEditBatch(
        IReadOnlyList<FeatureEditCommand> commands,
        HttpRequest request);

    /// <summary>
    /// Fetches created features after a batch edit operation and computes their ETags.
    /// Falls back to minimal ID-only response if features cannot be fetched.
    /// </summary>
    /// <param name="repository">Feature repository</param>
    /// <param name="context">Feature context</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="collectionId">OGC collection ID</param>
    /// <param name="editResult">Result from batch edit operation</param>
    /// <param name="fallbackIds">Fallback IDs for features that cannot be fetched</param>
    /// <param name="featureQuery">Query for fetching features</param>
    /// <param name="request">HTTP request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of created features with payloads and ETags</returns>
    Task<List<(string? FeatureId, object Payload, string? Etag)>> FetchCreatedFeaturesWithETags(
        IFeatureRepository repository,
        FeatureContext context,
        LayerDefinition layer,
        string collectionId,
        FeatureEditBatchResult editResult,
        List<string?> fallbackIds,
        FeatureQuery featureQuery,
        HttpRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds an HTTP response for feature mutation operations (create/update).
    /// Returns single feature for single-item mode, FeatureCollection for batch mode.
    /// </summary>
    /// <param name="createdFeatures">List of created features with payloads and ETags</param>
    /// <param name="collectionId">OGC collection ID</param>
    /// <param name="singleItemMode">Whether to return single feature or collection</param>
    /// <returns>HTTP result with Location and ETag headers</returns>
    IResult BuildMutationResponse(
        List<(string? FeatureId, object Payload, string? Etag)> createdFeatures,
        string collectionId,
        bool singleItemMode);

    /// <summary>
    /// Validates If-Match header against current feature ETag for optimistic concurrency control.
    /// Returns true if match succeeds or no If-Match header present.
    /// </summary>
    /// <param name="request">HTTP request</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="record">Current feature record</param>
    /// <param name="currentEtag">Output current ETag for the feature</param>
    /// <returns>True if validation passes, false if ETag mismatch</returns>
    bool ValidateIfMatch(HttpRequest request, LayerDefinition layer, FeatureRecord record, out string currentEtag);

    /// <summary>
    /// Computes weak ETag for a feature based on its attributes.
    /// Uses SHA-256 hash of sorted attribute JSON.
    /// </summary>
    /// <param name="layer">Layer definition</param>
    /// <param name="record">Feature record</param>
    /// <returns>Weak ETag string (W/"hash")</returns>
    string ComputeFeatureEtag(LayerDefinition layer, FeatureRecord record);
}
