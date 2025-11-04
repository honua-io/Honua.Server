using System;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc;

[Collection("HostTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public sealed class OgcCacheHeaderIntegrationTests
{
    private readonly OgcCacheHeaderService _cacheService;

    public OgcCacheHeaderIntegrationTests()
    {
        var options = new CacheHeaderOptions();
        _cacheService = new OgcCacheHeaderService(Options.Create(options));
    }

    [Fact]
    public async Task CachedResult_AppliesCacheHeaders_ToResponse()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { message = "test" });
        var etag = "\"test-etag\"";
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Contains("Cache-Control", context.Response.Headers.Keys);
        Assert.Equal(etag, context.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task CachedResult_Returns304_WhenETagMatches()
    {
        // Arrange
        var context = CreateHttpContext();
        var etag = "\"test-etag-123\"";
        context.Request.Headers.IfNoneMatch = etag;
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
    }

    [Fact]
    public async Task CachedResult_Returns200_WhenETagDoesNotMatch()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers.IfNoneMatch = "\"different-etag\"";
        var innerResult = Results.Ok(new { message = "test" });
        var etag = "\"test-etag\"";
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task CachedResult_Returns304_WhenNotModifiedSince()
    {
        // Arrange
        var context = CreateHttpContext();
        var lastModified = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var ifModifiedSince = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero);
        context.Request.Headers.IfModifiedSince = ifModifiedSince.ToString("R");
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, null, lastModified);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
    }

    [Fact]
    public void WithTileCacheHeaders_AppliesTileCacheConfiguration()
    {
        // Arrange
        var result = Results.Ok(new { message = "test" });
        var etag = "\"tile-etag\"";

        // Act
        var cachedResult = result.WithTileCacheHeaders(_cacheService, etag);

        // Assert
        Assert.IsType<CachedResult>(cachedResult);
    }

    [Fact]
    public void WithMetadataCacheHeaders_AppliesMetadataCacheConfiguration()
    {
        // Arrange
        var result = Results.Ok(new { message = "test" });
        var etag = "\"metadata-etag\"";

        // Act
        var cachedResult = result.WithMetadataCacheHeaders(_cacheService, etag);

        // Assert
        Assert.IsType<CachedResult>(cachedResult);
    }

    [Fact]
    public void WithFeatureCacheHeaders_AppliesFeatureCacheConfiguration()
    {
        // Arrange
        var result = Results.Ok(new { message = "test" });
        var etag = "\"feature-etag\"";

        // Act
        var cachedResult = result.WithFeatureCacheHeaders(_cacheService, etag);

        // Assert
        Assert.IsType<CachedResult>(cachedResult);
    }

    [Fact]
    public void WithStyleCacheHeaders_AppliesStyleCacheConfiguration()
    {
        // Arrange
        var result = Results.Ok(new { message = "test" });
        var etag = "\"style-etag\"";

        // Act
        var cachedResult = result.WithStyleCacheHeaders(_cacheService, etag);

        // Assert
        Assert.IsType<CachedResult>(cachedResult);
    }

    [Fact]
    public void WithTileMatrixSetCacheHeaders_AppliesTileMatrixSetCacheConfiguration()
    {
        // Arrange
        var result = Results.Ok(new { message = "test" });
        var etag = "\"matrix-etag\"";

        // Act
        var cachedResult = result.WithTileMatrixSetCacheHeaders(_cacheService, etag);

        // Assert
        Assert.IsType<CachedResult>(cachedResult);
    }

    [Fact]
    public async Task CachedResult_IncludesVaryHeader_ForContentNegotiation()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var vary = context.Response.Headers.Vary.ToString();
        Assert.Contains("Accept", vary);
    }

    [Fact]
    public async Task CachedResult_ForTile_IncludesImmutableDirective()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new byte[] { 1, 2, 3 });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Tile);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("immutable", cacheControl);
    }

    [Fact]
    public async Task CachedResult_ForMetadata_DoesNotIncludeImmutableDirective()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.DoesNotContain("immutable", cacheControl);
    }

    [Fact]
    public async Task CachedResult_WithLastModified_SetsLastModifiedHeader()
    {
        // Arrange
        var context = CreateHttpContext();
        var lastModified = DateTimeOffset.UtcNow.AddDays(-7);
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, null, lastModified);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(lastModified.ToString("R"), context.Response.Headers.LastModified.ToString());
    }

    [Fact]
    public async Task CachedResult_Returns304_WithMultipleETags()
    {
        // Arrange
        var context = CreateHttpContext();
        var etag = "\"abc123\"";
        context.Request.Headers.IfNoneMatch = "\"other\", \"abc123\", \"another\"";
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
    }

    [Fact]
    public async Task CachedResult_Returns200_WhenNoIfNoneMatchHeader()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { message = "test" });
        var etag = "\"test-etag\"";
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task CachedResult_Returns304_WithWildcardIfNoneMatch()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers.IfNoneMatch = "*";
        var innerResult = Results.Ok(new { message = "test" });
        var etag = "\"any-etag\"";
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
    }

    [Fact]
    public async Task TileCacheHeaders_IncludesPublicMaxAgeAndImmutable()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new byte[] { 1, 2, 3 });
        var etag = "\"tile-content-hash\"";
        var cachedResult = innerResult.WithTileCacheHeaders(_cacheService, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("public", cacheControl);
        Assert.Contains("max-age=31536000", cacheControl);
        Assert.Contains("immutable", cacheControl);
        Assert.Equal(etag, context.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task MetadataCacheHeaders_IncludesPublicMaxAgeWithoutImmutable()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { id = "test", title = "Test Metadata" });
        var etag = "\"metadata-hash\"";
        var cachedResult = innerResult.WithMetadataCacheHeaders(_cacheService, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("public", cacheControl);
        Assert.Contains("max-age=3600", cacheControl);
        Assert.DoesNotContain("immutable", cacheControl);
    }

    [Fact]
    public async Task FeatureCacheHeaders_UsesFeatureCacheDuration()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { type = "Feature", properties = new { } });
        var etag = "\"feature-hash\"";
        var cachedResult = innerResult.WithFeatureCacheHeaders(_cacheService, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("max-age=300", cacheControl);
    }

    [Fact]
    public async Task StyleCacheHeaders_UsesStyleCacheDuration()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { id = "test-style", renderer = new { } });
        var etag = "\"style-hash\"";
        var cachedResult = innerResult.WithStyleCacheHeaders(_cacheService, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("max-age=3600", cacheControl);
    }

    [Fact]
    public async Task TileMatrixSetCacheHeaders_UsesLongCacheDuration()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { id = "WebMercatorQuad", tileMatrices = new[] { } });
        var etag = "\"matrix-hash\"";
        var cachedResult = innerResult.WithTileMatrixSetCacheHeaders(_cacheService, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("max-age=604800", cacheControl);
    }

    [Fact]
    public async Task CachedResult_Returns304_DoesNotWriteBody()
    {
        // Arrange
        var context = CreateHttpContext();
        var etag = "\"test-etag\"";
        context.Request.Headers.IfNoneMatch = etag;
        var innerResult = Results.Ok(new { message = "This should not be written" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task CachedResult_Returns304_IncludesCacheHeadersInResponse()
    {
        // Arrange
        var context = CreateHttpContext();
        var etag = "\"test-etag\"";
        context.Request.Headers.IfNoneMatch = etag;
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Tile, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
        Assert.Contains("Cache-Control", context.Response.Headers.Keys);
        Assert.Equal(etag, context.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task CachedResult_WithBothETagAndLastModified_PrioritizesETag()
    {
        // Arrange
        var context = CreateHttpContext();
        var etag = "\"test-etag\"";
        var lastModified = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var ifModifiedSince = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero);

        context.Request.Headers.IfNoneMatch = etag;
        context.Request.Headers.IfModifiedSince = ifModifiedSince.ToString("R");

        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, etag, lastModified);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
    }

    [Fact]
    public async Task CachedResult_Returns304_WhenOnlyLastModifiedMatches()
    {
        // Arrange
        var context = CreateHttpContext();
        var lastModified = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var ifModifiedSince = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero);
        context.Request.Headers.IfModifiedSince = ifModifiedSince.ToString("R");
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, null, lastModified);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
    }

    [Fact]
    public async Task CachedResult_Returns200_WhenLastModifiedIsNewer()
    {
        // Arrange
        var context = CreateHttpContext();
        var lastModified = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero);
        var ifModifiedSince = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        context.Request.Headers.IfModifiedSince = ifModifiedSince.ToString("R");
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata, null, lastModified);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task CachedResult_IncludesVaryHeaderForAcceptNegotiation()
    {
        // Arrange
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Metadata);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var vary = context.Response.Headers.Vary.ToString();
        Assert.Contains("Accept", vary);
        Assert.Contains("Accept-Encoding", vary);
        Assert.Contains("Accept-Language", vary);
    }

    [Fact]
    public async Task CachedResult_WithDisabledCaching_SetsNoCacheHeaders()
    {
        // Arrange
        var options = new CacheHeaderOptions { EnableCaching = false };
        var service = new OgcCacheHeaderService(Options.Create(options));
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, service, OgcResourceType.Tile);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("no-cache", cacheControl);
        Assert.Contains("no-store", cacheControl);
    }

    [Fact]
    public async Task CachedResult_WithPrivateCacheDirective_UsesPrivateInsteadOfPublic()
    {
        // Arrange
        var options = new CacheHeaderOptions { UsePublicCacheDirective = false };
        var service = new OgcCacheHeaderService(Options.Create(options));
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new { message = "test" });
        var cachedResult = new CachedResult(innerResult, service, OgcResourceType.Metadata);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("private", cacheControl);
        Assert.DoesNotContain("public", cacheControl);
    }

    [Fact]
    public async Task CachedResult_WithCustomCacheDuration_UsesCustomValue()
    {
        // Arrange
        var options = new CacheHeaderOptions { TileCacheDurationSeconds = 7200 };
        var service = new OgcCacheHeaderService(Options.Create(options));
        var context = CreateHttpContext();
        var innerResult = Results.Ok(new byte[] { 1, 2, 3 });
        var cachedResult = new CachedResult(innerResult, service, OgcResourceType.Tile);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        Assert.Contains("max-age=7200", cacheControl);
    }

    [Fact]
    public async Task CachedResult_Returns304_SetsCorrectStatusCodeWithoutBody()
    {
        // Arrange
        var context = CreateHttpContext();
        var etag = "\"response-hash\"";
        context.Request.Headers.IfNoneMatch = etag;
        var innerResult = Results.Ok(new { data = new byte[1000] });
        var cachedResult = new CachedResult(innerResult, _cacheService, OgcResourceType.Tile, etag);

        // Act
        await cachedResult.ExecuteAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.Equal(etag, context.Response.Headers.ETag.ToString());
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }
}
