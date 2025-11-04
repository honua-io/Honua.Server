using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Attachments;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Attachments;

[Trait("Category", "Unit")]
public sealed class FileSystemAttachmentStoreSecurityTests
{
    [Fact]
    public void ResolveFullPath_ShouldRejectTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "honua-attachments-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new FileSystemAttachmentStore(root, NullLogger<FileSystemAttachmentStore>.Instance);
            var pointer = new AttachmentPointer(AttachmentStoreProviderKeys.FileSystem, "../secrets.txt");

            Action act = () => store.TryGetAsync(pointer).GetAwaiter().GetResult();
            act.Should().Throw<InvalidOperationException>().WithMessage("*Invalid attachment path*");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PutAsync_ShouldCreateHierarchicalPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "honua-attachments-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new FileSystemAttachmentStore(root, NullLogger<FileSystemAttachmentStore>.Instance);
            var request = new AttachmentStorePutRequest
            {
                AttachmentId = "abcdef123456",
                FileName = "note.txt",
                MimeType = "text/plain",
                SizeBytes = 4,
                ChecksumSha256 = "deadbeef"
            };

            await using var content = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var result = await store.PutAsync(content, request);

            result.Pointer.StorageKey.Should().Be(Path.Combine("ab", "cd", "abcdef123456"));
            var fullPath = Path.Combine(root, result.Pointer.StorageKey);
            File.Exists(fullPath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
