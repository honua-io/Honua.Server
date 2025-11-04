using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Sources;
using Moq;
using Moq.Protected;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Sources;

/// <summary>
/// Tests for HttpRasterSourceProvider to increase coverage from 0% to 100%.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class HttpRasterSourceProviderTests
{
    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new HttpRasterSourceProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProviderKey_ReturnsHttp()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act
        var key = provider.ProviderKey;

        // Assert
        key.Should().Be("http");
    }

    [Theory]
    [InlineData("https://example.com/data.tif", true)]
    [InlineData("http://example.com/data.tif", true)]
    [InlineData("HTTPS://EXAMPLE.COM/data.tif", true)]
    [InlineData("HTTP://EXAMPLE.COM/data.tif", true)]
    [InlineData("ftp://example.com/data.tif", false)]
    [InlineData("/local/path/data.tif", false)]
    [InlineData("C:\\data\\file.tif", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void CanHandle_VariousUris_ReturnsExpectedResult(string? uri, bool expected)
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act
        var result = provider.CanHandle(uri);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task OpenReadAsync_WithValidHttpsUrl_ReturnsStream()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var testContent = "test raster data"u8.ToArray();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(testContent)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act
        var stream = await provider.OpenReadAsync("https://example.com/test.tif");

        // Assert
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        content.Should().Be("test raster data");
    }

    [Fact]
    public async Task OpenReadAsync_WithInvalidUri_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act & Assert
        await provider.Invoking(p => p.OpenReadAsync("not a valid uri"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid HTTP URI*");
    }

    [Fact]
    public async Task OpenReadAsync_WithHttpError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act & Assert
        await provider.Invoking(p => p.OpenReadAsync("https://example.com/missing.tif"))
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task OpenReadRangeAsync_WithLengthSpecified_SendsCorrectRangeHeader()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var testContent = "partial content"u8.ToArray();
        HttpRequestMessage? capturedRequest = null;

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.PartialContent,
                Content = new ByteArrayContent(testContent)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act
        var stream = await provider.OpenReadRangeAsync("https://example.com/test.tif", offset: 100, length: 500);

        // Assert
        stream.Should().NotBeNull();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Range.Should().NotBeNull();
        capturedRequest.Headers.Range!.Ranges.Should().ContainSingle();
        var range = capturedRequest.Headers.Range.Ranges.First();
        range.From.Should().Be(100);
        range.To.Should().Be(599); // offset + length - 1
    }

    [Fact]
    public async Task OpenReadRangeAsync_WithoutLength_SendsOpenEndedRangeHeader()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var testContent = "rest of file"u8.ToArray();
        HttpRequestMessage? capturedRequest = null;

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.PartialContent,
                Content = new ByteArrayContent(testContent)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act
        var stream = await provider.OpenReadRangeAsync("https://example.com/test.tif", offset: 1000);

        // Assert
        stream.Should().NotBeNull();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Range.Should().NotBeNull();
        var range = capturedRequest.Headers.Range!.Ranges.First();
        range.From.Should().Be(1000);
        range.To.Should().BeNull();
    }

    [Fact]
    public async Task OpenReadRangeAsync_WithOkResponse_AcceptsFallback()
    {
        // Arrange - some servers return 200 OK even for range requests
        var mockHandler = new Mock<HttpMessageHandler>();
        var testContent = "full file content"u8.ToArray();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(testContent)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act
        var stream = await provider.OpenReadRangeAsync("https://example.com/test.tif", offset: 100, length: 500);

        // Assert
        stream.Should().NotBeNull();
    }

    [Fact]
    public async Task OpenReadRangeAsync_WithInvalidUri_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act & Assert
        await provider.Invoking(p => p.OpenReadRangeAsync("invalid uri", 0, 100))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid HTTP URI*");
    }

    [Fact]
    public async Task OpenReadRangeAsync_WithServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act & Assert
        await provider.Invoking(p => p.OpenReadRangeAsync("https://example.com/test.tif", 0, 100))
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task OpenReadAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act & Assert
        await provider.Invoking(p => p.OpenReadAsync("https://example.com/test.tif", cts.Token))
            .Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task OpenReadRangeAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new HttpRasterSourceProvider(httpClient);

        // Act & Assert
        await provider.Invoking(p => p.OpenReadRangeAsync("https://example.com/test.tif", 0, 100, cts.Token))
            .Should().ThrowAsync<TaskCanceledException>();
    }
}
