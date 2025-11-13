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
/// Implementation of Web Coverage Service (WCS) operations.
/// </summary>
/// <remarks>
/// This class is part of Phase 1 refactoring to split OgcSharedHandlers.
/// TODO Phase 2: Move WCS-specific implementation from OgcSharedHandlers to this class.
/// This stub provides the structure and dependencies needed for the refactoring.
///
/// IMPORTANT: This class is NOT currently registered in DI (see ServiceCollectionExtensions).
/// All WCS operations are still handled by OgcSharedHandlers. This stub will not cause
/// runtime failures as it is not instantiated or called.
///
/// BUG FIX #19: Marked as obsolete to prevent accidental DI registration.
/// This handler was never filled out and returns minimal stub responses.
/// Modularization attempts using this handler would yield empty coverage descriptions.
/// </remarks>
[Obsolete("Implementation incomplete - returns stubbed responses. Do not register in DI until Phase 2 migration is complete.")]
internal sealed class WcsHandlers : IWcsHandler
{
    private readonly IRasterDatasetRegistry rasterRegistry;
    private readonly ILogger<WcsHandlers> logger;

    public WcsHandlers(
        IRasterDatasetRegistry rasterRegistry,
        ILogger<WcsHandlers> logger)
    {
        this.rasterRegistry = Guard.NotNull(rasterRegistry);
        this.logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    /// <remarks>
    /// TEMPORARY STUB: Returns minimal valid WCS 2.0.1 capabilities XML.
    /// TODO Phase 2: Move full implementation from OgcSharedHandlers.
    /// </remarks>
    public Task<IResult> GetCapabilitiesAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken)
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Capabilities xmlns=""http://www.opengis.net/wcs/2.0"" xmlns:ows=""http://www.opengis.net/ows/2.0"" version=""2.0.1"">
  <ows:ServiceIdentification>
    <ows:Title>Honua WCS Service (Stub)</ows:Title>
    <ows:ServiceType>WCS</ows:ServiceType>
    <ows:ServiceTypeVersion>2.0.1</ows:ServiceTypeVersion>
  </ows:ServiceIdentification>
  <Contents/>
</Capabilities>";
        return Task.FromResult(Results.Content(xml, "application/xml"));
    }

    /// <inheritdoc />
    /// <remarks>
    /// TEMPORARY STUB: Returns an empty coverage description.
    /// TODO Phase 2: Move full implementation from OgcSharedHandlers.
    /// </remarks>
    public Task<IResult> DescribeCoverageAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken)
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<CoverageDescriptions xmlns=""http://www.opengis.net/wcs/2.0"" xmlns:gml=""http://www.opengis.net/gml/3.2"" version=""2.0.1"">
</CoverageDescriptions>";
        return Task.FromResult(Results.Content(xml, "application/xml"));
    }

    /// <inheritdoc />
    /// <remarks>
    /// TEMPORARY STUB: Returns an error indicating no coverage is available.
    /// TODO Phase 2: Move full implementation from OgcSharedHandlers.
    /// </remarks>
    public Task<IResult> GetCoverageAsync(
        HttpRequest request,
        ServiceDefinition serviceDefinition,
        CancellationToken cancellationToken)
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<ows:ExceptionReport xmlns:ows=""http://www.opengis.net/ows/2.0"" version=""2.0.0"">
  <ows:Exception exceptionCode=""NoApplicableCode"">
    <ows:ExceptionText>No coverage available</ows:ExceptionText>
  </ows:Exception>
</ows:ExceptionReport>";
        return Task.FromResult(Results.Content(xml, "application/xml", statusCode: 400));
    }
}
