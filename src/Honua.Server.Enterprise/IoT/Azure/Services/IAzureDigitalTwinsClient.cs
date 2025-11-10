// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure;
using Azure.DigitalTwins.Core;

namespace Honua.Server.Enterprise.IoT.Azure.Services;

/// <summary>
/// Abstraction for Azure Digital Twins client operations.
/// Enables testing with mock implementations.
/// </summary>
public interface IAzureDigitalTwinsClient
{
    /// <summary>
    /// Creates or replaces a digital twin.
    /// </summary>
    Task<Response<BasicDigitalTwin>> CreateOrReplaceDigitalTwinAsync(
        string twinId,
        BasicDigitalTwin twin,
        ETag? ifNoneMatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a digital twin.
    /// </summary>
    Task<Response<BasicDigitalTwin>> GetDigitalTwinAsync(
        string twinId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a digital twin using JSON patch.
    /// </summary>
    Task<Response<BasicDigitalTwin>> UpdateDigitalTwinAsync(
        string twinId,
        string jsonPatch,
        ETag? ifMatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a digital twin.
    /// </summary>
    Task<Response> DeleteDigitalTwinAsync(
        string twinId,
        ETag? ifMatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries digital twins using ADT query language.
    /// </summary>
    AsyncPageable<BasicDigitalTwin> QueryAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces a relationship.
    /// </summary>
    Task<Response<BasicRelationship>> CreateOrReplaceRelationshipAsync(
        string twinId,
        string relationshipId,
        BasicRelationship relationship,
        ETag? ifNoneMatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a relationship.
    /// </summary>
    Task<Response<BasicRelationship>> GetRelationshipAsync(
        string twinId,
        string relationshipId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a relationship.
    /// </summary>
    Task<Response> DeleteRelationshipAsync(
        string twinId,
        string relationshipId,
        ETag? ifMatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists relationships for a twin.
    /// </summary>
    AsyncPageable<BasicRelationship> GetRelationshipsAsync(
        string twinId,
        string? relationshipName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces a model.
    /// </summary>
    Task<Response<DigitalTwinsModelData[]>> CreateModelsAsync(
        IEnumerable<string> models,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a model.
    /// </summary>
    Task<Response<DigitalTwinsModelData>> GetModelAsync(
        string modelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all models.
    /// </summary>
    AsyncPageable<DigitalTwinsModelData> GetModelsAsync(
        GetModelsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a model.
    /// </summary>
    Task<Response> DeleteModelAsync(
        string modelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decommissions a model.
    /// </summary>
    Task<Response> DecommissionModelAsync(
        string modelId,
        CancellationToken cancellationToken = default);
}
