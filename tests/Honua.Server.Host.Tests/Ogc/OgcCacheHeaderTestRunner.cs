using System;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc;

/// <summary>
/// Quick validation runner for cache header tests
/// </summary>
public sealed class OgcCacheHeaderTestRunner
{
    [Fact]
    public async Task ValidateBasic304NotModifiedWorkflow()
    {
        // This test validates the complete workflow for 304 Not Modified responses
        var options = new CacheHeaderOptions();
        var cacheService = new OgcCacheHeaderService(Options.Create(options));

        // Scenario: Client requests a tile with matching ETag
        var context = CreateHttpContext();
        var tileContent = new byte[] { 1, 2, 3, 4, 5 };
        var etag = cacheService.GenerateETag(tileContent);

        // Simulate client sending If-None-Match header with matching ETag
        context.Request.Headers.IfNoneMatch = etag;

        var innerResult = Results.Bytes(tileContent, "image/png");
        var cachedResult = new CachedResult(innerResult, cacheService, OgcResourceType.Tile, etag);

        // Execute the result
        await cachedResult.ExecuteAsync(context);

        // Verify 304 response
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.Equal(etag, context.Response.Headers.ETag.ToString());
        Assert.Contains("immutable", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task ValidateMetadataETagGeneration()
    {
        // This test validates ETag generation for metadata responses
        var options = new CacheHeaderOptions();
        var cacheService = new OgcCacheHeaderService(Options.Create(options));

        var metadata = new { id = "test-collection", title = "Test Collection" };
        var etag = cacheService.GenerateETagForObject(metadata);

        var context = CreateHttpContext();
        var innerResult = Results.Ok(metadata);
        var cachedResult = innerResult.WithMetadataCacheHeaders(cacheService, etag);

        await cachedResult.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(etag, context.Response.Headers.ETag.ToString());
        Assert.Contains("max-age=3600", context.Response.Headers.CacheControl.ToString());
        Assert.DoesNotContain("immutable", context.Response.Headers.CacheControl.ToString());
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }
}
