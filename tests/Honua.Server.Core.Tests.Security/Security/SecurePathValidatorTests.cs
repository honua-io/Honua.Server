// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Security;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Runtime.InteropServices;

namespace Honua.Server.Core.Tests.Security.Security;

[TestFixture]
[Category("Unit")]
[Category("Security")]
public class SecurePathValidatorTests
{
    private string _tempBaseDir = null!;
    private string _tempSubDir = null!;
    private string _tempFile = null!;

    [SetUp]
    public void Setup()
    {
        // Create a temporary directory structure for testing
        _tempBaseDir = Path.Combine(Path.GetTempPath(), "honua_test_" + Guid.NewGuid().ToString());
        _tempSubDir = Path.Combine(_tempBaseDir, "subdir");
        Directory.CreateDirectory(_tempSubDir);

        // Create a test file
        _tempFile = Path.Combine(_tempSubDir, "test.txt");
        File.WriteAllText(_tempFile, "test content");
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up temporary directory
        if (Directory.Exists(_tempBaseDir))
        {
            try
            {
                Directory.Delete(_tempBaseDir, true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }

    [Test]
    public void ValidatePath_ValidPathWithinBase_ReturnsAbsolutePath()
    {
        // Arrange
        var requestedPath = Path.Combine(_tempBaseDir, "subdir", "test.txt");

        // Act
        var result = SecurePathValidator.ValidatePath(requestedPath, _tempBaseDir);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith(_tempBaseDir);
    }

    [Test]
    public void ValidatePath_RelativePathWithinBase_ReturnsAbsolutePath()
    {
        // Arrange
        var currentDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = _tempBaseDir;
            var requestedPath = "subdir";

            // Act
            var result = SecurePathValidator.ValidatePath(requestedPath, _tempBaseDir);

            // Assert
            result.Should().StartWith(_tempBaseDir);
            result.Should().Contain("subdir");
        }
        finally
        {
            Environment.CurrentDirectory = currentDir;
        }
    }

    [Test]
    public void ValidatePath_PathTraversalAttempt_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var maliciousPath = Path.Combine(_tempBaseDir, "..", "sensitive_data");

        // Act
        var act = () => SecurePathValidator.ValidatePath(maliciousPath, _tempBaseDir);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*outside allowed directory*");
    }

    [Test]
    public void ValidatePath_MultiplePathTraversal_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var maliciousPath = Path.Combine(_tempBaseDir, "subdir", "..", "..", "..", "etc", "passwd");

        // Act
        var act = () => SecurePathValidator.ValidatePath(maliciousPath, _tempBaseDir);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*outside allowed directory*");
    }

    [Test]
    public void ValidatePath_NullRequestedPath_ThrowsArgumentException()
    {
        // Act
        var act = () => SecurePathValidator.ValidatePath(null!, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ValidatePath_EmptyRequestedPath_ThrowsArgumentException()
    {
        // Act
        var act = () => SecurePathValidator.ValidatePath(string.Empty, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ValidatePath_WhitespaceRequestedPath_ThrowsArgumentException()
    {
        // Act
        var act = () => SecurePathValidator.ValidatePath("   ", _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ValidatePath_NullBaseDirectory_ThrowsArgumentException()
    {
        // Act
        var act = () => SecurePathValidator.ValidatePath("somepath", null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ValidatePath_EmptyBaseDirectory_ThrowsArgumentException()
    {
        // Act
        var act = () => SecurePathValidator.ValidatePath("somepath", string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ValidatePath_NullByteInPath_ThrowsArgumentException()
    {
        // Arrange
        var maliciousPath = _tempBaseDir + "\0malicious";

        // Act
        var act = () => SecurePathValidator.ValidatePath(maliciousPath, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*null byte*");
    }

    [Test]
    public void ValidatePath_UncPathWindows_ThrowsArgumentException()
    {
        // Arrange
        var uncPath = @"\\server\share\file.txt";

        // Act
        var act = () => SecurePathValidator.ValidatePath(uncPath, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UNC paths are not allowed*");
    }

    [Test]
    public void ValidatePath_UncPathUnix_ThrowsArgumentException()
    {
        // Arrange
        var uncPath = "//server/share/file.txt";

        // Act
        var act = () => SecurePathValidator.ValidatePath(uncPath, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UNC paths are not allowed*");
    }

    [Test]
    public void ValidatePath_UrlEncodedDot_ThrowsArgumentException()
    {
        // Arrange
        var encodedPath = Path.Combine(_tempBaseDir, "%2e%2e", "file.txt");

        // Act
        var act = () => SecurePathValidator.ValidatePath(encodedPath, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*URL-encoded path characters*");
    }

    [Test]
    public void ValidatePath_UrlEncodedSlash_ThrowsArgumentException()
    {
        // Arrange
        var encodedPath = _tempBaseDir + "%2f..%2f..%2fetc%2fpasswd";

        // Act
        var act = () => SecurePathValidator.ValidatePath(encodedPath, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*URL-encoded path characters*");
    }

    [Test]
    public void ValidatePath_UrlEncodedBackslash_ThrowsArgumentException()
    {
        // Arrange
        var encodedPath = _tempBaseDir + "%5c..%5c..%5cfile.txt";

        // Act
        var act = () => SecurePathValidator.ValidatePath(encodedPath, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*URL-encoded path characters*");
    }

    [Test]
    public void ValidatePath_NullByteEncoded_ThrowsArgumentException()
    {
        // Arrange
        var encodedPath = Path.Combine(_tempBaseDir, "file%00.txt");

        // Act
        var act = () => SecurePathValidator.ValidatePath(encodedPath, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Encoded path traversal patterns*");
    }

    [Test]
    public void ValidatePath_PartialDirectoryMatch_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Create two directories with similar names
        var publicDir = Path.Combine(Path.GetTempPath(), "honua_public_" + Guid.NewGuid().ToString());
        var publicUnsafeDir = Path.Combine(Path.GetTempPath(), "honua_public_unsafe_" + Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(publicDir);
            Directory.CreateDirectory(publicUnsafeDir);

            var maliciousPath = Path.Combine(publicUnsafeDir, "file.txt");

            // Act - Try to access public_unsafe when only public is allowed
            var act = () => SecurePathValidator.ValidatePath(maliciousPath, publicDir);

            // Assert
            act.Should().Throw<UnauthorizedAccessException>()
                .WithMessage("*outside the allowed directory*");
        }
        finally
        {
            if (Directory.Exists(publicDir)) Directory.Delete(publicDir, true);
            if (Directory.Exists(publicUnsafeDir)) Directory.Delete(publicUnsafeDir, true);
        }
    }

    [Test]
    public void ValidatePath_DotSegmentsInPath_ResolvesCorrectly()
    {
        // Arrange
        var pathWithDots = Path.Combine(_tempBaseDir, "subdir", ".", "test.txt");

        // Act
        var result = SecurePathValidator.ValidatePath(pathWithDots, _tempBaseDir);

        // Assert
        result.Should().StartWith(_tempBaseDir);
        result.Should().NotContain(Path.DirectorySeparatorChar + "." + Path.DirectorySeparatorChar);
    }

    [Test]
    public void ValidatePath_CaseInsensitiveOnWindows_WorksCorrectly()
    {
        // Arrange
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Ignore("This test is specific to Windows");
        }

        var upperCasePath = _tempBaseDir.ToUpperInvariant();
        var requestedPath = Path.Combine(_tempBaseDir.ToLowerInvariant(), "subdir");

        // Act
        var result = SecurePathValidator.ValidatePath(requestedPath, upperCasePath);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidatePathMultiple_FirstDirectoryMatches_ReturnsPath()
    {
        // Arrange
        var dir1 = _tempBaseDir;
        var dir2 = Path.Combine(Path.GetTempPath(), "honua_other_" + Guid.NewGuid().ToString());
        var requestedPath = Path.Combine(dir1, "subdir", "test.txt");

        // Act
        var result = SecurePathValidator.ValidatePathMultiple(requestedPath, dir1, dir2);

        // Assert
        result.Should().StartWith(dir1);
    }

    [Test]
    public void ValidatePathMultiple_SecondDirectoryMatches_ReturnsPath()
    {
        // Arrange
        var dir1 = Path.Combine(Path.GetTempPath(), "honua_first_" + Guid.NewGuid().ToString());
        var dir2 = _tempBaseDir;
        var requestedPath = Path.Combine(dir2, "subdir", "test.txt");

        // Act
        var result = SecurePathValidator.ValidatePathMultiple(requestedPath, dir1, dir2);

        // Assert
        result.Should().StartWith(dir2);
    }

    [Test]
    public void ValidatePathMultiple_NoDirectoryMatches_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var dir1 = Path.Combine(Path.GetTempPath(), "honua_first_" + Guid.NewGuid().ToString());
        var dir2 = Path.Combine(Path.GetTempPath(), "honua_second_" + Guid.NewGuid().ToString());
        var requestedPath = Path.Combine(Path.GetTempPath(), "honua_third_" + Guid.NewGuid().ToString(), "file.txt");

        // Act
        var act = () => SecurePathValidator.ValidatePathMultiple(requestedPath, dir1, dir2);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*outside all allowed directories*");
    }

    [Test]
    public void ValidatePathMultiple_NullRequestedPath_ThrowsArgumentException()
    {
        // Act
        var act = () => SecurePathValidator.ValidatePathMultiple(null!, _tempBaseDir);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ValidatePathMultiple_NoAllowedDirectories_ThrowsArgumentException()
    {
        // Act
        var act = () => SecurePathValidator.ValidatePathMultiple("somepath");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one allowed directory*");
    }

    [Test]
    public void ValidatePathMultiple_NullAllowedDirectories_ThrowsArgumentException()
    {
        // Act
        var act = () => SecurePathValidator.ValidatePathMultiple("somepath", null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one allowed directory*");
    }

    [Test]
    public void ValidatePathMultiple_EmptyAllowedDirectoriesArray_ThrowsArgumentException()
    {
        // Act
        var act = () => SecurePathValidator.ValidatePathMultiple("somepath", Array.Empty<string>());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one allowed directory*");
    }

    [Test]
    public void IsValidAndExists_ValidFileExists_ReturnsTrue()
    {
        // Arrange
        var filePath = _tempFile;

        // Act
        var result = SecurePathValidator.IsValidAndExists(filePath, _tempBaseDir);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsValidAndExists_ValidDirectoryExists_ReturnsTrue()
    {
        // Arrange
        var dirPath = _tempSubDir;

        // Act
        var result = SecurePathValidator.IsValidAndExists(dirPath, _tempBaseDir);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsValidAndExists_ValidPathDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempBaseDir, "nonexistent.txt");

        // Act
        var result = SecurePathValidator.IsValidAndExists(nonExistentPath, _tempBaseDir);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsValidAndExists_InvalidPathOutsideBase_ReturnsFalse()
    {
        // Arrange
        var maliciousPath = Path.Combine(_tempBaseDir, "..", "..", "etc", "passwd");

        // Act
        var result = SecurePathValidator.IsValidAndExists(maliciousPath, _tempBaseDir);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsValidAndExists_LogsWarningOnUnauthorizedAccess()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var maliciousPath = Path.Combine(_tempBaseDir, "..", "sensitive_data");

        // Act
        var result = SecurePathValidator.IsValidAndExists(maliciousPath, _tempBaseDir, mockLogger.Object);

        // Assert
        result.Should().BeFalse();
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("path traversal")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void IsValidAndExists_LogsDebugOnOtherException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var invalidPath = "\0invalid";

        // Act
        var result = SecurePathValidator.IsValidAndExists(invalidPath, _tempBaseDir, mockLogger.Object);

        // Assert
        result.Should().BeFalse();
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void ValidatePath_AbsolutePathWithinBase_DoesNotThrow()
    {
        // Arrange
        var absolutePath = Path.Combine(_tempBaseDir, "subdir", "file.txt");

        // Act
        var act = () => SecurePathValidator.ValidatePath(absolutePath, _tempBaseDir);

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void ValidatePath_InvalidCharactersInPath_ThrowsArgumentException()
    {
        // Arrange - Path with invalid characters
        var invalidPath = Path.Combine(_tempBaseDir, "file<>?.txt");

        // Act
        var act = () => SecurePathValidator.ValidatePath(invalidPath, _tempBaseDir);

        // Assert - Should throw ArgumentException from Path.GetFullPath
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid path format*");
    }

    [Test]
    public void ValidatePath_MixedPathSeparators_ResolvesCorrectly()
    {
        // Arrange
        var mixedPath = _tempBaseDir + "/subdir\\test.txt";

        // Act
        var result = SecurePathValidator.ValidatePath(mixedPath, _tempBaseDir);

        // Assert
        result.Should().StartWith(_tempBaseDir);
    }
}
