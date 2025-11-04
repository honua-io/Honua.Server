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
/// Interface for Web Map Service (WMS) operations.
/// </summary>
/// <remarks>
/// This interface is part of Phase 1 refactoring to split OgcSharedHandlers.
/// TODO Phase 2: Extract WMS-specific methods from OgcSharedHandlers.
/// Operations to be extracted:
/// - GetCapabilities
/// - GetMap
/// - GetFeatureInfo
/// - GetLegendGraphic
/// - DescribeLayer
/// </remarks>
internal interface IWmsHandler
{
    /// <summary>
    /// Handles WMS GetCapabilities request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>WMS capabilities document.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetCapabilitiesAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles WMS GetMap request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendered map image.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetMapAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles WMS GetFeatureInfo request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Feature information at the specified point.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetFeatureInfoAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);
}
