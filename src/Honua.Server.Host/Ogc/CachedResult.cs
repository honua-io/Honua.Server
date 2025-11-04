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
    private readonly IResult _innerResult;
    private readonly OgcCacheHeaderService _cacheService;
    private readonly OgcResourceType _resourceType;
    private readonly string? _etag;
    private readonly DateTimeOffset? _lastModified;

    public CachedResult(
        IResult innerResult,
        OgcCacheHeaderService cacheService,
        OgcResourceType resourceType,
        string? etag = null,
        DateTimeOffset? lastModified = null)
    {
        _innerResult = Guard.NotNull(innerResult);
        _cacheService = Guard.NotNull(cacheService);
        _resourceType = resourceType;
        _etag = etag;
        _lastModified = lastModified;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        Guard.NotNull(httpContext);

        // Check if we should return 304 Not Modified
        if (_cacheService.ShouldReturn304NotModified(httpContext, _etag, _lastModified))
        {
            // Apply cache headers before returning 304
            _cacheService.ApplyCacheHeaders(httpContext, _resourceType, _etag, _lastModified);
            httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        // Apply cache headers
        _cacheService.ApplyCacheHeaders(httpContext, _resourceType, _etag, _lastModified);

        // Execute the inner result
        await _innerResult.ExecuteAsync(httpContext).ConfigureAwait(false);
    }
}
