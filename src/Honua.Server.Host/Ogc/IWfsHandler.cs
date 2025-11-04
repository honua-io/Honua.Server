// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Interface for Web Feature Service (WFS) operations.
/// </summary>
/// <remarks>
/// This interface is part of Phase 1 refactoring to split OgcSharedHandlers.
/// TODO Phase 2: Extract WFS-specific methods from OgcSharedHandlers.
/// Operations to be extracted:
/// - GetCapabilities
/// - DescribeFeatureType
/// - GetFeature
/// - Transaction (Insert, Update, Delete)
/// - LockFeature
/// - GetGmlObject
/// </remarks>
internal interface IWfsHandler
{
    /// <summary>
    /// Handles WFS GetCapabilities request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>WFS capabilities document.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetCapabilitiesAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles WFS DescribeFeatureType request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Feature type schema description.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> DescribeFeatureTypeAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles WFS GetFeature request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Feature collection in requested format.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetFeatureAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles WFS Transaction request (Insert, Update, Delete operations).
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction response.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> TransactionAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);
}
