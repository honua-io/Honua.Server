// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for managing OGC API conformance classes and conformance declarations.
/// Implements OGC API - Features Part 1 (Core) conformance requirements.
/// </summary>
internal interface IOgcConformanceService
{
    /// <summary>
    /// Gets the conformance declaration for the OGC API, including all supported conformance classes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP result containing the conformance response</returns>
    Task<IResult> GetConformanceAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of OGC API conformance service.
/// </summary>
internal sealed class ConformanceService : IOgcConformanceService
{
    private readonly IMetadataRegistry metadataRegistry;
    private readonly OgcCacheHeaderService cacheHeaderService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConformanceService"/> class.
    /// </summary>
    /// <param name="metadataRegistry">Metadata registry for accessing service configurations</param>
    /// <param name="cacheHeaderService">Cache header service for ETag generation</param>
    public ConformanceService(
        IMetadataRegistry metadataRegistry,
        OgcCacheHeaderService cacheHeaderService)
    {
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.cacheHeaderService = Guard.NotNull(cacheHeaderService);
    }

    /// <inheritdoc/>
    public async Task<IResult> GetConformanceAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await this.metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        // Start with default conformance classes from OGC API spec
        var classes = new HashSet<string>(OgcSharedHandlers.DefaultConformanceClasses);

        // Add service-specific conformance classes
        foreach (var service in snapshot.Services)
        {
            foreach (var conformance in service.Ogc.ConformanceClasses ?? Array.Empty<string>())
            {
                if (conformance.HasValue())
                {
                    classes.Add(conformance);
                }
            }
        }

        var response = new { conformsTo = classes };
        var etag = this.cacheHeaderService.GenerateETagForObject(response);

        return Results.Ok(response)
            .WithMetadataCacheHeaders(this.cacheHeaderService, etag);
    }
}
