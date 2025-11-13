// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// IResult wrapper that applies cache headers to responses
/// </summary>
public sealed class CachedResult : IResult
{
    private readonly IResult innerResult;
    private readonly OgcCacheHeaderService cacheService;
    private readonly OgcResourceType resourceType;
    private readonly string? etag;
    private readonly DateTimeOffset? lastModified;

    public CachedResult(
        IResult innerResult,
        OgcCacheHeaderService cacheService,
        OgcResourceType resourceType,
        string? etag = null,
        DateTimeOffset? lastModified = null)
    {
        this.innerResult = Guard.NotNull(innerResult);
        this.cacheService = Guard.NotNull(cacheService);
        this.resourceType = resourceType;
        this.etag = etag;
        this.lastModified = lastModified;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        Guard.NotNull(httpContext);

        // Check if we should return 304 Not Modified
        if (this.cacheService.ShouldReturn304NotModified(httpContext, _etag, _lastModified))
        {
            // Apply cache headers before returning 304
            this.cacheService.ApplyCacheHeaders(httpContext, _resourceType, _etag, _lastModified);
            httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        // Apply cache headers
        this.cacheService.ApplyCacheHeaders(httpContext, _resourceType, _etag, _lastModified);

        // Execute the inner result
        await this.innerResult.ExecuteAsync(httpContext).ConfigureAwait(false);
    }
}
