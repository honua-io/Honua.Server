using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Attachments;
using Honua.Server.Host.Attachments;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Tests.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Attachments;

/// <summary>
/// Tests for AttachmentDownloadHelper ensuring proper async/await patterns,
/// no blocking synchronous operations, and correct stream handling.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "Attachments")]
[Trait("Speed", "Fast")]
public sealed class AttachmentDownloadHelperTests
{
    private static readonly OgcCacheHeaderService CacheHeaderService = new(Options.Create(new CacheHeaderOptions()));

    #region TryDownloadAsync Tests

    [Fact]
    public async Task TryDownloadAsync_ReturnsSuccess_WhenAttachmentExists()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "photo.jpg");
        var storeMock = CreateStoreMock(descriptor, "test content");
        var selectorMock = CreateSelectorMock(descriptor.StorageProvider, storeMock.Object);

        // Act
        var result = await AttachmentDownloadHelper.TryDownloadAsync(
            descriptor,
            "storage-profile-1",
            selectorMock.Object,
            NullLogger.Instance,
            "service-1",
            "layer-1",
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ReadResult.Should().NotBeNull();
        result.Descriptor.Should().Be(descriptor);
        result.IsNotFound.Should().BeFalse();
        result.StorageProfileError.Should().BeNull();
    }

    [Fact]
    public async Task TryDownloadAsync_ReturnsNotFound_WhenAttachmentDoesNotExist()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-missing", "missing.jpg");
        var storeMock = new Mock<IAttachmentStore>();
        storeMock
            .Setup(x => x.TryGetAsync(It.IsAny<AttachmentPointer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttachmentReadResult?)null);

        var selectorMock = CreateSelectorMock(descriptor.StorageProvider, storeMock.Object);

        // Act
        var result = await AttachmentDownloadHelper.TryDownloadAsync(
            descriptor,
            "storage-profile-1",
            selectorMock.Object,
            NullLogger.Instance,
            "service-1",
            "layer-1",
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
        result.StorageProfileError.Should().BeNull();
    }

    [Fact]
    public async Task TryDownloadAsync_ReturnsStorageProfileMissing_WhenStorageProviderNotFoundAndProfileIdNull()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "photo.jpg");
        var selectorMock = new Mock<IAttachmentStoreSelector>();
        selectorMock
            .Setup(x => x.Resolve(descriptor.StorageProvider))
            .Throws(new AttachmentStoreNotFoundException(descriptor.StorageProvider));

        // Act
        var result = await AttachmentDownloadHelper.TryDownloadAsync(
            descriptor,
            storageProfileId: null,
            selectorMock.Object,
            NullLogger.Instance,
            "service-1",
            "layer-1",
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.StorageProfileError.Should().Be("Attachment storage profile is not configured for this layer.");
    }

    [Fact]
    public async Task TryDownloadAsync_ReturnsStorageProfileUnresolvable_WhenBothStorageProviderAndProfileIdFail()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "photo.jpg");
        var selectorMock = new Mock<IAttachmentStoreSelector>();
        selectorMock
            .Setup(x => x.Resolve(It.IsAny<string>()))
            .Throws(new AttachmentStoreNotFoundException("storage-profile"));

        // Act
        var result = await AttachmentDownloadHelper.TryDownloadAsync(
            descriptor,
            "storage-profile-1",
            selectorMock.Object,
            NullLogger.Instance,
            "service-1",
            "layer-1",
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.StorageProfileError.Should().Be("Attachment storage profile could not be resolved.");
    }

    [Fact]
    public async Task TryDownloadAsync_FallsBackToStorageProfileId_WhenStorageProviderNotFound()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "photo.jpg");
        var storeMock = CreateStoreMock(descriptor, "fallback content");
        var selectorMock = new Mock<IAttachmentStoreSelector>();

        // First call with descriptor.StorageProvider throws
        selectorMock
            .Setup(x => x.Resolve(descriptor.StorageProvider))
            .Throws(new AttachmentStoreNotFoundException(descriptor.StorageProvider));

        // Second call with storageProfileId succeeds
        selectorMock
            .Setup(x => x.Resolve("storage-profile-1"))
            .Returns(storeMock.Object);

        // Act
        var result = await AttachmentDownloadHelper.TryDownloadAsync(
            descriptor,
            "storage-profile-1",
            selectorMock.Object,
            NullLogger.Instance,
            "service-1",
            "layer-1",
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ReadResult.Should().NotBeNull();
    }

    [Fact]
    public async Task TryDownloadAsync_PropagatesCancellation()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "photo.jpg");
        var storeMock = new Mock<IAttachmentStore>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        storeMock
            .Setup(x => x.TryGetAsync(It.IsAny<AttachmentPointer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var selectorMock = CreateSelectorMock(descriptor.StorageProvider, storeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await AttachmentDownloadHelper.TryDownloadAsync(
                descriptor,
                "storage-profile-1",
                selectorMock.Object,
                NullLogger.Instance,
                "service-1",
                "layer-1",
                cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
    }

    #endregion

    #region ToActionResultAsync Tests

    [Fact]
    public async Task ToActionResultAsync_ReturnsFile_WhenSuccessful()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "photo.jpg");
        var content = "test file content"u8.ToArray();
        var readResult = new AttachmentReadResult
        {
            Content = new MemoryStream(content),
            MimeType = "image/jpeg",
            FileName = "photo.jpg",
            SizeBytes = content.Length,
            ChecksumSha256 = "abc123"
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);

        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<FileStreamResult>();
        var fileResult = (FileStreamResult)actionResult;
        fileResult.FileDownloadName.Should().Be("photo.jpg");
        fileResult.ContentType.Should().Be("image/jpeg");
        controller.Response.Headers["ETag"].ToString().Should().Be("\"abc123\"");
        controller.Response.Headers["Content-Disposition"].ToString().Should().Contain("photo.jpg");
    }

    [Fact]
    public async Task ToActionResultAsync_ReturnsNotFound_WhenNotFound()
    {
        // Arrange
        var downloadResult = AttachmentDownloadHelper.DownloadResult.NotFound();
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ToActionResultAsync_ReturnsProblem_WhenStorageProfileError()
    {
        // Arrange
        var downloadResult = AttachmentDownloadHelper.DownloadResult.StorageProfileMissing();
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)actionResult;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task ToActionResultAsync_HandlesSeekableStream()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "data.txt");
        var content = "seekable content"u8.ToArray();
        var seekableStream = new MemoryStream(content); // MemoryStream is seekable
        var readResult = new AttachmentReadResult
        {
            Content = seekableStream,
            MimeType = "text/plain",
            SizeBytes = content.Length
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<FileStreamResult>();
        var fileResult = (FileStreamResult)actionResult;
        fileResult.EnableRangeProcessing.Should().BeTrue(); // Seekable streams enable range processing
    }

    [Fact]
    public async Task ToActionResultAsync_HandlesSmallNonSeekableStream()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "small.txt");
        var content = "small content"u8.ToArray();
        var nonSeekableStream = new NonSeekableMemoryStream(content);
        var readResult = new AttachmentReadResult
        {
            Content = nonSeekableStream,
            MimeType = "text/plain",
            SizeBytes = content.Length
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<FileStreamResult>();
        var fileResult = (FileStreamResult)actionResult;
        // Small non-seekable streams should be buffered and made seekable
        fileResult.EnableRangeProcessing.Should().BeTrue();
    }

    #endregion

    #region ToResultAsync Tests

    [Fact]
    public async Task ToResultAsync_ReturnsFile_WhenSuccessful()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "document.pdf");
        var content = "PDF content"u8.ToArray();
        var readResult = new AttachmentReadResult
        {
            Content = new MemoryStream(content),
            MimeType = "application/pdf",
            SizeBytes = content.Length,
            ChecksumSha256 = "pdf123"
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);

        // Act
        var result = await AttachmentDownloadHelper.ToResultAsync(
            downloadResult,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();

        // Execute result to verify it's a file result
        var httpContext = new DefaultHttpContext();
        await result.ExecuteAsync(httpContext).ConfigureAwait(false);
        httpContext.Response.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task ToResultAsync_ReturnsNotFound_WhenNotFound()
    {
        // Arrange
        var downloadResult = AttachmentDownloadHelper.DownloadResult.NotFound();

        // Act
        var result = await AttachmentDownloadHelper.ToResultAsync(
            downloadResult,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task ToResultAsync_ReturnsProblem_WhenStorageProfileError()
    {
        // Arrange
        var downloadResult = AttachmentDownloadHelper.DownloadResult.StorageProfileUnresolvable();

        // Act
        var result = await AttachmentDownloadHelper.ToResultAsync(
            downloadResult,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.ProblemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task ToResultAsync_AppliesCacheHeaders_WhenCacheServiceProvided()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "cached.jpg");
        descriptor = descriptor with { ChecksumSha256 = "cache-etag-123" };
        var content = "cached content"u8.ToArray();
        var readResult = new AttachmentReadResult
        {
            Content = new MemoryStream(content),
            MimeType = "image/jpeg",
            SizeBytes = content.Length,
            ChecksumSha256 = descriptor.ChecksumSha256
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);

        // Act
        var result = await AttachmentDownloadHelper.ToResultAsync(
            downloadResult,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        var httpContext = new DefaultHttpContext();
        await result.ExecuteAsync(httpContext).ConfigureAwait(false);
        httpContext.Response.Headers["ETag"].Should().NotBeNullOrEmpty();
        httpContext.Response.Headers["Cache-Control"].Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Stream Handling Tests

    [Fact]
    public async Task ToActionResultAsync_HandlesLargeNonSeekableStream_DisablesRangeProcessing()
    {
        // Arrange - Create a stream larger than 10MB threshold
        var descriptor = CreateDescriptor("attachment-1", "large-file.bin");
        var largeSize = 11 * 1024 * 1024; // 11 MB
        var largeStream = new NonSeekableMemoryStream(new byte[100]); // Small actual data for test performance
        var readResult = new AttachmentReadResult
        {
            Content = largeStream,
            MimeType = "application/octet-stream",
            SizeBytes = largeSize // Report as large
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<FileStreamResult>();
        var fileResult = (FileStreamResult)actionResult;
        fileResult.EnableRangeProcessing.Should().BeFalse(); // Large non-seekable streams disable range processing
    }

    [Fact]
    public async Task ToResultAsync_PropagatesCancellation()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "file.txt");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var slowStream = new SlowCancellableStream();
        var readResult = new AttachmentReadResult
        {
            Content = slowStream,
            MimeType = "text/plain",
            SizeBytes = 100
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await AttachmentDownloadHelper.ToResultAsync(
                downloadResult,
                CacheHeaderService,
                cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
    }

    #endregion

    #region Security Tests - Header Injection Prevention

    [Theory]
    [InlineData("photo\r\nX-Malicious: injected")]  // CRLF injection
    [InlineData("photo\nX-Malicious: injected")]     // LF injection
    [InlineData("photo\rX-Malicious: injected")]     // CR injection
    [InlineData("photo\"X-Malicious: injected")]     // Quote escape
    [InlineData("photo\r\n\r\n<script>alert('xss')</script>")]  // Response splitting
    public async Task ToActionResultAsync_PreventsCrlfInjection_InFilename(string maliciousFilename)
    {
        // Arrange - Create attachment with malicious filename
        var descriptor = CreateDescriptor("attachment-1", maliciousFilename);
        var content = "test content"u8.ToArray();
        var readResult = new AttachmentReadResult
        {
            Content = new MemoryStream(content),
            MimeType = "application/octet-stream",
            SizeBytes = content.Length
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<FileStreamResult>();
        var contentDisposition = controller.Response.Headers["Content-Disposition"].ToString();

        // Verify: The header should not contain raw CRLF sequences
        contentDisposition.Should().NotContain("\r\n", "CRLF injection should be prevented");
        contentDisposition.Should().NotContain("\r", "CR injection should be prevented");
        contentDisposition.Should().NotContain("\n", "LF injection should be prevented");

        // Verify: Header should use proper RFC 5987 encoding (filename*=UTF-8'')
        // The ContentDispositionHeaderValue should encode dangerous characters
        contentDisposition.Should().Contain("attachment", "disposition type should be present");
    }

    [Theory]
    [InlineData("文件.jpg")]                    // Chinese characters
    [InlineData("ファイル.pdf")]               // Japanese characters
    [InlineData("файл.doc")]                   // Cyrillic characters
    [InlineData("αρχείο.txt")]                // Greek characters
    [InlineData("tệp.zip")]                   // Vietnamese characters
    public async Task ToActionResultAsync_HandlesInternationalCharacters_Safely(string filename)
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", filename);
        var content = "test content"u8.ToArray();
        var readResult = new AttachmentReadResult
        {
            Content = new MemoryStream(content),
            MimeType = "application/octet-stream",
            SizeBytes = content.Length
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<FileStreamResult>();
        var contentDisposition = controller.Response.Headers["Content-Disposition"].ToString();

        // Verify: The header is present and properly formed
        contentDisposition.Should().Contain("attachment");

        // Verify: International characters should be RFC 5987 encoded (filename*=UTF-8'')
        // ContentDispositionHeaderValue.FileNameStar handles this automatically
        contentDisposition.Should().MatchRegex(@"filename\*=UTF-8''.*", "international characters should use RFC 5987 encoding");
    }

    [Fact]
    public async Task ToActionResultAsync_HandlesEmptyFilename_Gracefully()
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", "");
        var content = "test content"u8.ToArray();
        var readResult = new AttachmentReadResult
        {
            Content = new MemoryStream(content),
            MimeType = "application/octet-stream",
            SizeBytes = content.Length
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<FileStreamResult>();
        var contentDisposition = controller.Response.Headers["Content-Disposition"].ToString();
        contentDisposition.Should().Contain("attachment");
    }

    [Theory]
    [InlineData("file with spaces.pdf")]
    [InlineData("file-with-dashes.pdf")]
    [InlineData("file_with_underscores.pdf")]
    [InlineData("file.multiple.dots.pdf")]
    public async Task ToActionResultAsync_HandlesLegitimateSpecialCharacters(string filename)
    {
        // Arrange
        var descriptor = CreateDescriptor("attachment-1", filename);
        var content = "test content"u8.ToArray();
        var readResult = new AttachmentReadResult
        {
            Content = new MemoryStream(content),
            MimeType = "application/octet-stream",
            SizeBytes = content.Length
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<FileStreamResult>();
        var contentDisposition = controller.Response.Headers["Content-Disposition"].ToString();

        // Verify: Header is properly formed
        contentDisposition.Should().Contain("attachment");
        contentDisposition.Should().NotContain("\r\n", "should not contain CRLF");
    }

    [Fact]
    public async Task ToActionResultAsync_HandlesVeryLongFilename()
    {
        // Arrange - Create a filename longer than typical limits
        var longFilename = new string('a', 300) + ".pdf";
        var descriptor = CreateDescriptor("attachment-1", longFilename);
        var content = "test content"u8.ToArray();
        var readResult = new AttachmentReadResult
        {
            Content = new MemoryStream(content),
            MimeType = "application/octet-stream",
            SizeBytes = content.Length
        };
        var downloadResult = AttachmentDownloadHelper.DownloadResult.Success(readResult, descriptor);
        var controller = new TestController();

        // Act
        var actionResult = await AttachmentDownloadHelper.ToActionResultAsync(
            downloadResult,
            controller,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        actionResult.Should().BeOfType<FileStreamResult>();
        var contentDisposition = controller.Response.Headers["Content-Disposition"].ToString();

        // Verify: Header is present and doesn't contain injection
        contentDisposition.Should().Contain("attachment");
        contentDisposition.Should().NotContain("\r\n");
    }

    #endregion

    #region Helper Methods and Classes

    private static AttachmentDescriptor CreateDescriptor(string attachmentId, string fileName)
    {
        return new AttachmentDescriptor
        {
            AttachmentObjectId = 1,
            AttachmentId = attachmentId,
            ServiceId = "service-1",
            LayerId = "layer-1",
            FeatureId = "feature-1",
            Name = fileName,
            MimeType = "application/octet-stream",
            SizeBytes = 1024,
            ChecksumSha256 = "checksum123",
            StorageProvider = "test-storage",
            StorageKey = $"attachments/{attachmentId}",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
    }

    private static Mock<IAttachmentStore> CreateStoreMock(AttachmentDescriptor descriptor, string content)
    {
        var storeMock = new Mock<IAttachmentStore>();
        var contentBytes = Encoding.UTF8.GetBytes(content);

        storeMock
            .Setup(x => x.TryGetAsync(
                It.Is<AttachmentPointer>(p =>
                    p.StorageProvider == descriptor.StorageProvider &&
                    p.StorageKey == descriptor.StorageKey),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentReadResult
            {
                Content = new MemoryStream(contentBytes),
                MimeType = descriptor.MimeType,
                FileName = descriptor.Name,
                SizeBytes = contentBytes.Length,
                ChecksumSha256 = descriptor.ChecksumSha256
            });

        return storeMock;
    }

    private static Mock<IAttachmentStoreSelector> CreateSelectorMock(string storageProvider, IAttachmentStore store)
    {
        var selectorMock = new Mock<IAttachmentStoreSelector>();
        selectorMock
            .Setup(x => x.Resolve(storageProvider))
            .Returns(store);

        return selectorMock;
    }

    private sealed class TestController : ControllerBase
    {
        public TestController()
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }
    }

    /// <summary>
    /// Non-seekable stream wrapper for testing non-seekable stream handling.
    /// </summary>
    private sealed class NonSeekableMemoryStream : Stream
    {
        private readonly MemoryStream _innerStream;

        public NonSeekableMemoryStream(byte[] data)
        {
            _innerStream = new MemoryStream(data);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false; // Non-seekable
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _innerStream.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Slow stream that respects cancellation tokens for testing async cancellation.
    /// </summary>
    private sealed class SlowCancellableStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 100;
        public override long Position { get; set; }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(10000, cancellationToken).ConfigureAwait(false); // Long delay to trigger cancellation
            return 0;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10000, cancellationToken).ConfigureAwait(false); // Long delay to trigger cancellation
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    #endregion
}
