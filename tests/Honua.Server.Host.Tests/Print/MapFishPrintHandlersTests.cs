using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Print.MapFish;
using Honua.Server.Host.Print;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Print;

[Trait("Category", "Unit")]
public class MapFishPrintHandlersTests
{
    private readonly Mock<IMapFishPrintService> _printServiceMock;
    private readonly Mock<ILogger<MapFishPrintHandlers.MapFishPrintHandlerLogger>> _loggerMock;
    private readonly DefaultHttpContext _httpContext;

    public MapFishPrintHandlersTests()
    {
        _printServiceMock = new Mock<IMapFishPrintService>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<MapFishPrintHandlers.MapFishPrintHandlerLogger>>(MockBehavior.Loose);
        _httpContext = new DefaultHttpContext();

        // Setup request body with a minimal valid spec
        var spec = new MapFishPrintSpec
        {
            Layout = "default",
            OutputFormat = "pdf",
            Attributes = new MapFishPrintSpecAttributes
            {
                Map = new MapFishPrintMapSpec
                {
                    BoundingBox = new[] { 0d, 0d, 1000d, 1000d },
                    Projection = "EPSG:3857",
                    Dpi = 150
                }
            }
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(spec);
        _httpContext.Request.Body = new MemoryStream(jsonBytes);
        _httpContext.Request.ContentType = "application/json";
    }

    [Fact]
    public async Task CreateReportAsync_WhenSuccessful_ReturnsFileResult()
    {
        // Arrange
        var pdfContent = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // PDF header
        var printResult = new MapFishPrintResult(pdfContent, "application/pdf", "map.pdf", 10000);

        _printServiceMock
            .Setup(s => s.CreateReportAsync(It.IsAny<string>(), It.IsAny<MapFishPrintSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(printResult);

        // Act
        var result = await MapFishPrintHandlers.CreateReportAsync(
            "test-app",
            "pdf",
            _httpContext,
            _printServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentHttpResult>();
        var fileResult = (FileContentHttpResult)result;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("map.pdf");

        // Verify no errors were logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateReportAsync_WhenInvalidOperationException_ReturnsGenericBadRequest()
    {
        // Arrange
        var sensitiveException = new InvalidOperationException(
            "Application 'test-app' not found in /var/secrets/apps/config.json at line 42");

        _printServiceMock
            .Setup(s => s.CreateReportAsync(It.IsAny<string>(), It.IsAny<MapFishPrintSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(sensitiveException);

        // Act
        var result = await MapFishPrintHandlers.CreateReportAsync(
            "test-app",
            "pdf",
            _httpContext,
            _printServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequest<object>>();
        var badRequestResult = (BadRequest<object>)result;
        var responseJson = JsonSerializer.Serialize(badRequestResult.Value);

        // Verify the response does NOT contain sensitive information
        responseJson.Should().NotContain("/var/secrets");
        responseJson.Should().NotContain("config.json");
        responseJson.Should().NotContain("line 42");
        responseJson.Should().NotContain("test-app' not found");

        // Verify it contains a generic message
        responseJson.Should().Contain("print request could not be processed");

        // Verify the exception was logged at Warning level with full details
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MapFish print request validation failed")),
                sensitiveException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify no errors were logged (should be warning only)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateReportAsync_WhenUnexpectedException_ReturnsGenericProblemDetails()
    {
        // Arrange
        var sensitiveException = new Exception(
            "Failed to render map: Stack trace at /opt/mapfish/renderer.py:123\n" +
            "  File \"/opt/mapfish/internal/wms.py\", line 456\n" +
            "Connection to database server at 10.0.0.42:5432 failed");

        _printServiceMock
            .Setup(s => s.CreateReportAsync(It.IsAny<string>(), It.IsAny<MapFishPrintSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(sensitiveException);

        // Act
        var result = await MapFishPrintHandlers.CreateReportAsync(
            "test-app",
            "pdf",
            _httpContext,
            _printServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)result;
        var problemDetails = problemResult.ProblemDetails;

        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Title.Should().Be("Print Generation Failed");

        // Verify the response does NOT contain sensitive information
        var detailsJson = JsonSerializer.Serialize(problemDetails);
        detailsJson.Should().NotContain("/opt/mapfish");
        detailsJson.Should().NotContain("renderer.py");
        detailsJson.Should().NotContain("wms.py");
        detailsJson.Should().NotContain("10.0.0.42");
        detailsJson.Should().NotContain("5432");
        detailsJson.Should().NotContain("Stack trace");
        detailsJson.Should().NotContain("line 456");
        detailsJson.Should().NotContain("line 123");

        // Verify it contains a generic message
        detailsJson.Should().Contain("error occurred while generating the print report");

        // Verify the exception was logged at Error level with full details
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MapFish print request failed unexpectedly")),
                sensitiveException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateReportAsync_WhenFileNotFoundException_DoesNotLeakFilePaths()
    {
        // Arrange
        var sensitiveException = new FileNotFoundException(
            "Template file not found",
            "/var/lib/mapfish/templates/secret-template.jrxml");

        _printServiceMock
            .Setup(s => s.CreateReportAsync(It.IsAny<string>(), It.IsAny<MapFishPrintSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(sensitiveException);

        // Act
        var result = await MapFishPrintHandlers.CreateReportAsync(
            "test-app",
            "pdf",
            _httpContext,
            _printServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)result;
        var detailsJson = JsonSerializer.Serialize(problemResult.ProblemDetails);

        // Verify the response does NOT contain file paths
        detailsJson.Should().NotContain("/var/lib/mapfish");
        detailsJson.Should().NotContain("secret-template.jrxml");
        detailsJson.Should().NotContain("Template file not found");

        // Verify it contains a generic message
        detailsJson.Should().Contain("error occurred while generating the print report");
    }

    [Fact]
    public async Task CreateReportAsync_WhenArgumentException_DoesNotLeakArgumentDetails()
    {
        // Arrange
        var sensitiveException = new ArgumentException(
            "Invalid parameter 'adminPassword' with value 'super-secret-123' at position 5");

        _printServiceMock
            .Setup(s => s.CreateReportAsync(It.IsAny<string>(), It.IsAny<MapFishPrintSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(sensitiveException);

        // Act
        var result = await MapFishPrintHandlers.CreateReportAsync(
            "test-app",
            "pdf",
            _httpContext,
            _printServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)result;
        var detailsJson = JsonSerializer.Serialize(problemResult.ProblemDetails);

        // Verify the response does NOT contain sensitive argument details
        detailsJson.Should().NotContain("adminPassword");
        detailsJson.Should().NotContain("super-secret-123");
        detailsJson.Should().NotContain("position 5");

        // Verify it contains a generic message
        detailsJson.Should().Contain("error occurred while generating the print report");
    }

    [Fact]
    public async Task CreateReportAsync_WhenNullReferenceException_DoesNotLeakInternalDetails()
    {
        // Arrange
        var sensitiveException = new NullReferenceException(
            "Object reference not set to an instance of an object at MapFishPrintService.RenderLayer");

        _printServiceMock
            .Setup(s => s.CreateReportAsync(It.IsAny<string>(), It.IsAny<MapFishPrintSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(sensitiveException);

        // Act
        var result = await MapFishPrintHandlers.CreateReportAsync(
            "test-app",
            "pdf",
            _httpContext,
            _printServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)result;
        var detailsJson = JsonSerializer.Serialize(problemResult.ProblemDetails);

        // Verify the response does NOT contain internal class/method names
        detailsJson.Should().NotContain("MapFishPrintService");
        detailsJson.Should().NotContain("RenderLayer");
        detailsJson.Should().NotContain("Object reference not set");

        // Verify it contains a generic message
        detailsJson.Should().Contain("error occurred while generating the print report");
    }

    [Fact]
    public async Task CreateReportAsync_EnsuresAllExceptionsAreLogged()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        _printServiceMock
            .Setup(s => s.CreateReportAsync(It.IsAny<string>(), It.IsAny<MapFishPrintSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        await MapFishPrintHandlers.CreateReportAsync(
            "test-app",
            "pdf",
            _httpContext,
            _printServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert - Verify the exception object itself was logged (not just a message)
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception, // The actual exception object should be logged
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "The actual exception object should be logged for full stack trace capture");
    }

    [Fact]
    public async Task CreateReportAsync_LogsApplicationIdInAllCases()
    {
        // Arrange
        const string appId = "sensitive-app-id";
        var exception = new Exception("Test");

        _printServiceMock
            .Setup(s => s.CreateReportAsync(It.IsAny<string>(), It.IsAny<MapFishPrintSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        await MapFishPrintHandlers.CreateReportAsync(
            appId,
            "pdf",
            _httpContext,
            _printServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert - Verify the application ID is logged for debugging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(appId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Application ID should be logged for internal debugging");
    }
}
