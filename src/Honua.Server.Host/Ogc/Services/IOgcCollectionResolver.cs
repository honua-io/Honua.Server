// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Results;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for resolving and validating OGC collection identifiers.
/// </summary>
internal interface IOgcCollectionResolver
{
    /// <summary>
    /// Resolves a collection ID to a feature context, performing validation and security checks.
    /// </summary>
    Task<Result<FeatureContext>> ResolveCollectionAsync(
        string collectionId,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a collection and returns either the context or an error result.
    /// </summary>
    Task<(FeatureContext? Context, IResult? Error)> TryResolveCollectionAsync(
        string collectionId,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken);

    /// <summary>
    /// Maps a collection resolution error to an appropriate IResult.
    /// </summary>
    IResult MapCollectionResolutionError(Error error, string collectionId);

    /// <summary>
    /// Builds a collection ID from service and layer IDs.
    /// </summary>
    string BuildCollectionId(ServiceDefinition service, LayerDefinition layer);
}
