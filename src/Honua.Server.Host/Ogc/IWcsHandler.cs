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
/// Interface for Web Coverage Service (WCS) operations.
/// </summary>
/// <remarks>
/// This interface is part of Phase 1 refactoring to split OgcSharedHandlers.
/// TODO Phase 2: Extract WCS-specific methods from OgcSharedHandlers.
/// Operations to be extracted:
/// - GetCapabilities
/// - DescribeCoverage
/// - GetCoverage
/// </remarks>
internal interface IWcsHandler
{
    /// <summary>
    /// Handles WCS GetCapabilities request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>WCS capabilities document.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetCapabilitiesAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles WCS DescribeCoverage request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Coverage description document.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> DescribeCoverageAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles WCS GetCoverage request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="serviceDefinition">The service definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Coverage data in requested format.</returns>
    /// <remarks>
    /// TODO Phase 2: Move implementation from OgcSharedHandlers
    /// </remarks>
    Task<IResult> GetCoverageAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken);
}
