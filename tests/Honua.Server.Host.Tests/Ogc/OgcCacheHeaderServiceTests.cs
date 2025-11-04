using System;
using System.IO;
using System.Text;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc;

[Collection("HostTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public sealed class OgcCacheHeaderServiceTests
{
    private readonly OgcCacheHeaderService _service;
    private readonly CacheHeaderOptions _options;

    public OgcCacheHeaderServiceTests()
    {
        _options = new CacheHeaderOptions();
        _service = new OgcCacheHeaderService(Options.Create(_options));
    }

    [Fact]
    public void ApplyCacheHeaders_ForTileResource_SetsCacheControlWithImmutable()
    {
        // Arrange
        var context = CreateHttpContext();

        // Act
        _service.ApplyCacheHeaders(context, OgcResourceType.Tile);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("public", cacheControl);
        Assert.Contains($"max-age={_options.TileCacheDurationSeconds}", cacheControl);
        Assert.Contains("immutable", cacheControl);
    }

    [Fact]
    public void ApplyCacheHeaders_ForMetadataResource_SetsCacheControlWithoutImmutable()
    {
        // Arrange
        var context = CreateHttpContext();

        // Act
        _service.ApplyCacheHeaders(context, OgcResourceType.Metadata);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("public", cacheControl);
        Assert.Contains($"max-age={_options.MetadataCacheDurationSeconds}", cacheControl);
        Assert.DoesNotContain("immutable", cacheControl);
    }

    [Fact]
    public void ApplyCacheHeaders_WhenCachingDisabled_SetsNoCacheHeaders()
    {
        // Arrange
        var options = new CacheHeaderOptions { EnableCaching = false };
        var service = new OgcCacheHeaderService(Options.Create(options));
        var context = CreateHttpContext();

        // Act
        service.ApplyCacheHeaders(context, OgcResourceType.Tile);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("no-cache", cacheControl);
        Assert.Contains("no-store", cacheControl);
    }

    [Fact]
    public void ApplyCacheHeaders_AddsETagWhenProvided()
    {
        // Arrange
        var context = CreateHttpContext();
        var etag = "\"test-etag-123\"";

        // Act
        _service.ApplyCacheHeaders(context, OgcResourceType.Metadata, etag);

        // Assert
        Assert.Equal(etag, context.Response.Headers.ETag.ToString());
    }

    [Fact]
    public void ApplyCacheHeaders_AddsLastModifiedWhenProvided()
    {
        // Arrange
        var context = CreateHttpContext();
        var lastModified = DateTimeOffset.UtcNow.AddDays(-1);

        // Act
        _service.ApplyCacheHeaders(context, OgcResourceType.Metadata, null, lastModified);

        // Assert
        Assert.Equal(lastModified.ToString("R"), context.Response.Headers.LastModified.ToString());
    }

    [Fact]
    public void ApplyCacheHeaders_AddsVaryHeaders()
    {
        // Arrange
        var context = CreateHttpContext();

        // Act
        _service.ApplyCacheHeaders(context, OgcResourceType.Metadata);

        // Assert
        var vary = context.Response.Headers.Vary.ToString();
        Assert.Contains("Accept", vary);
        Assert.Contains("Accept-Encoding", vary);
    }

    [Fact]
    public void ShouldReturn304NotModified_WhenETagMatches_ReturnsTrue()
    {
        // Arrange
        var context = CreateHttpContext();
        var etag = "\"abc123\"";
        context.Request.Headers.IfNoneMatch = etag;

        // Act
        var result = _service.ShouldReturn304NotModified(context, etag, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldReturn304NotModified_WhenETagDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers.IfNoneMatch = "\"different-etag\"";

        // Act
        var result = _service.ShouldReturn304NotModified(context, "\"abc123\"", null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldReturn304NotModified_WhenIfModifiedSinceNotModified_ReturnsTrue()
    {
        // Arrange
        var context = CreateHttpContext();
        var lastModified = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var ifModifiedSince = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero);
        context.Request.Headers.IfModifiedSince = ifModifiedSince.ToString("R");

        // Act
        var result = _service.ShouldReturn304NotModified(context, null, lastModified);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldReturn304NotModified_WhenIfModifiedSinceIsModified_ReturnsFalse()
    {
        // Arrange
        var context = CreateHttpContext();
        var lastModified = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero);
        var ifModifiedSince = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        context.Request.Headers.IfModifiedSince = ifModifiedSince.ToString("R");

        // Act
        var result = _service.ShouldReturn304NotModified(context, null, lastModified);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GenerateETag_ForString_GeneratesConsistentHash()
    {
        // Arrange
        var content = "test content";

        // Act
        var etag1 = _service.GenerateETag(content);
        var etag2 = _service.GenerateETag(content);

        // Assert
        Assert.Equal(etag1, etag2);
        Assert.StartsWith("\"", etag1);
        Assert.EndsWith("\"", etag1);
    }

    [Fact]
    public void GenerateETag_ForBytes_GeneratesConsistentHash()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("test content");

        // Act
        var etag1 = _service.GenerateETag(content);
        var etag2 = _service.GenerateETag(content);

        // Assert
        Assert.Equal(etag1, etag2);
        Assert.StartsWith("\"", etag1);
        Assert.EndsWith("\"", etag1);
    }

    [Fact]
    public void GenerateETag_ForDifferentContent_GeneratesDifferentHashes()
    {
        // Arrange
        var content1 = "content one";
        var content2 = "content two";

        // Act
        var etag1 = _service.GenerateETag(content1);
        var etag2 = _service.GenerateETag(content2);

        // Assert
        Assert.NotEqual(etag1, etag2);
    }

    [Fact]
    public void GenerateETagForObject_GeneratesConsistentHash()
    {
        // Arrange
        var obj = new { id = 1, name = "test", value = 42 };

        // Act
        var etag1 = _service.GenerateETagForObject(obj);
        var etag2 = _service.GenerateETagForObject(obj);

        // Assert
        Assert.Equal(etag1, etag2);
        Assert.StartsWith("\"", etag1);
        Assert.EndsWith("\"", etag1);
    }

    [Fact]
    public void ApplyCacheHeaders_ForFeatureResource_UsesFeatureCacheDuration()
    {
        // Arrange
        var context = CreateHttpContext();

        // Act
        _service.ApplyCacheHeaders(context, OgcResourceType.Feature);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains($"max-age={_options.FeatureCacheDurationSeconds}", cacheControl);
    }

    [Fact]
    public void ApplyCacheHeaders_ForStyleResource_UsesStyleCacheDuration()
    {
        // Arrange
        var context = CreateHttpContext();

        // Act
        _service.ApplyCacheHeaders(context, OgcResourceType.Style);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains($"max-age={_options.StyleCacheDurationSeconds}", cacheControl);
    }

    [Fact]
    public void ApplyCacheHeaders_ForTileMatrixSet_UsesTileMatrixSetCacheDuration()
    {
        // Arrange
        var context = CreateHttpContext();

        // Act
        _service.ApplyCacheHeaders(context, OgcResourceType.TileMatrixSet);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains($"max-age={_options.TileMatrixSetCacheDurationSeconds}", cacheControl);
    }

    [Fact]
    public void ShouldReturn304NotModified_WhenConditionalRequestsDisabled_ReturnsFalse()
    {
        // Arrange
        var options = new CacheHeaderOptions { EnableConditionalRequests = false };
        var service = new OgcCacheHeaderService(Options.Create(options));
        var context = CreateHttpContext();
        context.Request.Headers.IfNoneMatch = "\"test-etag\"";

        // Act
        var result = service.ShouldReturn304NotModified(context, "\"test-etag\"", null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldReturn304NotModified_WithWildcardETag_ReturnsTrue()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers.IfNoneMatch = "*";

        // Act
        var result = _service.ShouldReturn304NotModified(context, "\"any-etag\"", null);

        // Assert
        Assert.True(result);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }
}
