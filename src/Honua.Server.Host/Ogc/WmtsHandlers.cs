// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;

#nullable enable

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Implementation of Web Map Tile Service (WMTS) operations.
/// </summary>
/// <remarks>
/// This class is part of Phase 1 refactoring to split OgcSharedHandlers.
/// TODO Phase 2: Move WMTS-specific implementation from OgcSharedHandlers to this class.
/// This stub provides the structure and dependencies needed for the refactoring.
///
/// IMPORTANT: This class is NOT currently registered in DI (see ServiceCollectionExtensions).
/// All WMTS operations are still handled by OgcSharedHandlers. This stub will not cause
/// runtime failures as it is not instantiated or called.
///
/// BUG FIX #16: Marked as obsolete to prevent accidental DI registration.
/// This handler serves hard-coded stub responses (skeleton capabilities XML and 1x1 transparent PNG).
/// Any attempt to wire this into DI would ship a broken WMTS endpoint.
/// </remarks>
[Obsolete("Implementation incomplete - returns stubbed responses. Do not register in DI until Phase 2 migration is complete.")]
internal sealed class WmtsHandlers : IWmtsHandler
{
    private readonly IRasterDatasetRegistry _rasterRegistry;
    private readonly ILogger<WmtsHandlers> _logger;

    public WmtsHandlers(
        IRasterDatasetRegistry rasterRegistry,
        ILogger<WmtsHandlers> logger)
    {
        _rasterRegistry = Guard.NotNull(rasterRegistry);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    /// <remarks>
    /// TEMPORARY STUB: Returns minimal valid WMTS 1.0.0 capabilities XML.
    /// TODO Phase 2: Move full implementation from OgcSharedHandlers.
    /// </remarks>
    public Task<IResult> GetCapabilitiesAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken)
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Capabilities xmlns=""http://www.opengis.net/wmts/1.0"" xmlns:ows=""http://www.opengis.net/ows/1.1"" version=""1.0.0"">
  <ows:ServiceIdentification>
    <ows:Title>Honua WMTS Service (Stub)</ows:Title>
    <ows:ServiceType>OGC WMTS</ows:ServiceType>
    <ows:ServiceTypeVersion>1.0.0</ows:ServiceTypeVersion>
  </ows:ServiceIdentification>
  <Contents/>
</Capabilities>";
        return Task.FromResult(Results.Content(xml, "application/xml"));
    }

    /// <inheritdoc />
    /// <remarks>
    /// TEMPORARY STUB: Returns a 1x1 transparent PNG.
    /// TODO Phase 2: Move full implementation from OgcSharedHandlers.
    /// </remarks>
    public Task<IResult> GetTileAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken)
    {
        // 1x1 transparent PNG (67 bytes)
        var transparentPng = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        };
        return Task.FromResult(Results.Bytes(transparentPng, "image/png"));
    }

    /// <inheritdoc />
    /// <remarks>
    /// TEMPORARY STUB: Returns an empty response.
    /// TODO Phase 2: Move full implementation from OgcSharedHandlers.
    /// </remarks>
    public Task<IResult> GetFeatureInfoAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken)
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<FeatureInfoResponse xmlns=""http://www.opengis.net/wmts/1.0"">
  <FeatureInfo/>
</FeatureInfoResponse>";
        return Task.FromResult(Results.Content(xml, "application/xml"));
    }
}
