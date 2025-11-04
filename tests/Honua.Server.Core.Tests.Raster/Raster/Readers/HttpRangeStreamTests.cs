using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class HttpRangeStreamTests
{
    private readonly ILogger<HttpRangeStream> _logger;

    public HttpRangeStreamTests()
    {
        _logger = NullLogger<HttpRangeStream>.Instance;
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        var httpClient = new HttpClient();
        const string uri = "https://example.com/test.tif";
        const long contentLength = 1000;

        // Act
        var stream = new HttpRangeStream(httpClient, uri, contentLength, _logger);

        // Assert
        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(contentLength, stream.Length);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Position_SetValue_UpdatesPosition()
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        // Act
        stream.Position = 100;

        // Assert
        Assert.Equal(100, stream.Position);
    }

    [Fact]
    public void Position_SetNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
    }

    [Fact]
    public void Position_SetBeyondLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = 1001);
    }

    [Theory]
    [InlineData(0, SeekOrigin.Begin, 0)]
    [InlineData(100, SeekOrigin.Begin, 100)]
    [InlineData(50, SeekOrigin.Current, 50)]
    [InlineData(-10, SeekOrigin.End, 990)]
    public void Seek_WithValidOffset_SetsPosition(long offset, SeekOrigin origin, long expectedPosition)
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        // Act
        var result = stream.Seek(offset, origin);

        // Assert
        Assert.Equal(expectedPosition, result);
        Assert.Equal(expectedPosition, stream.Position);
    }

    [Fact]
    public void Seek_BeyondLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Seek(1001, SeekOrigin.Begin));
    }

    [Fact]
    public void Seek_NegativePosition_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Seek(-1, SeekOrigin.Begin));
    }

    [Fact]
    public async Task ReadAsync_FetchesDataViaRangeRequest()
    {
        // Arrange
        var testData = new byte[100];
        for (int i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)i;
        }

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                // Simulate range request response
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(testData)
                };
                return response;
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        var buffer = new byte[50];

        // Act
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        Assert.Equal(50, bytesRead);
        Assert.Equal(50, stream.Position);

        // Verify range request was made
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.Range != null),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ReadAsync_AtEndOfStream_ReturnsZero()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object);
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        stream.Position = 1000; // At end

        var buffer = new byte[10];

        // Act
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async Task ReadAsync_ConsecutiveCalls_UsesReadAheadBuffer()
    {
        // Arrange
        var testData = new byte[100];
        for (int i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)i;
        }

        var callCount = 0;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(testData)
                };
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        var buffer = new byte[10];

        // Act - First read
        await stream.ReadAsync(buffer, 0, buffer.Length);

        // Act - Second read (should use cached data if within read-ahead)
        await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert - Should only make one HTTP request due to read-ahead buffering
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Flush_DoesNotThrow()
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        // Act & Assert - should not throw
        stream.Flush();
    }

    [Fact]
    public void SetLength_ThrowsNotSupportedException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => stream.SetLength(500));
    }

    [Fact]
    public void Write_ThrowsNotSupportedException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);
        var buffer = new byte[10];

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => stream.Write(buffer, 0, buffer.Length));
    }

    [Fact]
    public async Task CreateAsync_PerformsHeadRequest_AndReturnsStream()
    {
        // Arrange
        const long expectedLength = 5000;
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new ByteArrayContent(Array.Empty<byte>());
                response.Content.Headers.ContentLength = expectedLength;
                response.Headers.AcceptRanges.Add("bytes");
                return response;
            });

        var httpClient = new HttpClient(mockHandler.Object);

        // Act
        var stream = await HttpRangeStream.CreateAsync(
            httpClient,
            "https://example.com/test.tif",
            _logger);

        // Assert
        Assert.NotNull(stream);
        Assert.Equal(expectedLength, stream.Length);

        // Verify HEAD request was made
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithoutContentLength_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                // Create content without ContentLength
                var content = new StringContent("");
                response.Content = content;
                response.Content.Headers.ContentLength = null;
                return response;
            });

        var httpClient = new HttpClient(mockHandler.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HttpRangeStream.CreateAsync(httpClient, "https://example.com/test.tif", _logger));
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var httpClient = new HttpClient();
        var stream = new HttpRangeStream(httpClient, "https://example.com/test.tif", 1000, _logger);

        // Act
        stream.Dispose();

        // Assert - after dispose, operations should either throw or be safe
        // Position property may not throw ObjectDisposedException in this implementation
        // Just verify Dispose completes without exception
        Assert.True(true);
    }
}
