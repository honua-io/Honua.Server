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
/// Interface for Web Map Tile Service (WMTS) operations.
/// </summary>
/// <remarks>
/// This interface is part of Phase 1 refactoring to split OgcSharedHandlers.
/// TODO Phase 2: Extract WMTS-specific methods from OgcSharedHandlers.
/// Operations to be extracted:
/// - GetCapabilities
/// - GetTile
/// - GetFeatureInfo
/// </remarks>
internal interface IWmtsHandler
{
    /// <summary>
    /// Handles WMTS GetCapabilities request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>WMTS capabilities document.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetCapabilitiesAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles WMTS GetTile request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tile image.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetTileAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles WMTS GetFeatureInfo request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Feature information.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetFeatureInfoAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);
}
