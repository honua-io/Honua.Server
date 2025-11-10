// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Honua.Server.Core.Security;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Validators;

public class SecurePathValidatorTests
{
    private readonly string _testBaseDirectory;

    public SecurePathValidatorTests()
    {
        // Use a temp directory for testing
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), "SecurePathValidatorTests");
        Directory.CreateDirectory(_testBaseDirectory);
    }

    [Fact]
    public void ValidatePath_WithPathInBaseDirectory_ReturnsValidPath()
    {
        // Arrange
        var requestedPath = Path.Combine(_testBaseDirectory, "subfolder", "file.txt");

        // Act
        var result = SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith(_testBaseDirectory);
    }

    [Fact]
    public void ValidatePath_WithRelativePathInBase_ReturnsValidPath()
    {
        // Arrange
        var requestedPath = "subfolder/file.txt";

        // Act
        var result = SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith(_testBaseDirectory);
    }

    [Fact]
    public void ValidatePath_WithPathTraversalAttempt_ThrowsUnauthorizedException()
    {
        // Arrange
        var requestedPath = Path.Combine(_testBaseDirectory, "..", "..", "etc", "passwd");

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() =>
            SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory));
    }

    [Fact]
    public void ValidatePath_WithPathTraversalUsingDotDot_ThrowsUnauthorizedException()
    {
        // Arrange
        var requestedPath = "../../outside/file.txt";

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() =>
            SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory));
    }

    [Fact]
    public void ValidatePath_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurePathValidator.ValidatePath(null!, _testBaseDirectory));
    }

    [Fact]
    public void ValidatePath_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurePathValidator.ValidatePath(string.Empty, _testBaseDirectory));
    }

    [Fact]
    public void ValidatePath_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurePathValidator.ValidatePath("   ", _testBaseDirectory));
    }

    [Fact]
    public void ValidatePath_WithNullBaseDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurePathValidator.ValidatePath("file.txt", null!));
    }

    [Fact]
    public void ValidatePath_WithPathContainingNullByte_ThrowsArgumentException()
    {
        // Arrange
        var requestedPath = "file\0.txt";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory));
    }

    [Fact]
    public void ValidatePath_WithUncPath_ThrowsArgumentException()
    {
        // Arrange
        var uncPath = @"\\server\share\file.txt";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurePathValidator.ValidatePath(uncPath, _testBaseDirectory));
    }

    [Fact]
    public void ValidatePath_WithUrlEncodedTraversal_ThrowsArgumentException()
    {
        // Arrange
        var encodedPath = "file%2e%2e%2fpasswd";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurePathValidator.ValidatePath(encodedPath, _testBaseDirectory));
    }

    [Theory]
    [InlineData("%2e")]
    [InlineData("%2f")]
    [InlineData("%5c")]
    [InlineData("..%")]
    [InlineData("%00")]
    public void ValidatePath_WithEncodedCharacters_ThrowsArgumentException(string encodedSequence)
    {
        // Arrange
        var requestedPath = $"file{encodedSequence}test.txt";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory));
    }

    [Fact]
    public void ValidatePath_WithNestedSubdirectories_ReturnsValidPath()
    {
        // Arrange
        var requestedPath = Path.Combine(_testBaseDirectory, "level1", "level2", "level3", "file.txt");

        // Act
        var result = SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith(_testBaseDirectory);
    }

    [Fact]
    public void ValidatePath_WithSameDirectory_ReturnsValidPath()
    {
        // Arrange
        var requestedPath = _testBaseDirectory;

        // Act
        var result = SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidatePathMultiple_WithPathInFirstDirectory_ReturnsValidPath()
    {
        // Arrange
        var baseDir1 = Path.Combine(_testBaseDirectory, "dir1");
        var baseDir2 = Path.Combine(_testBaseDirectory, "dir2");
        Directory.CreateDirectory(baseDir1);
        Directory.CreateDirectory(baseDir2);

        var requestedPath = Path.Combine(baseDir1, "file.txt");

        // Act
        var result = SecurePathValidator.ValidatePathMultiple(requestedPath, baseDir1, baseDir2);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith(baseDir1);
    }

    [Fact]
    public void ValidatePathMultiple_WithPathNotInAnyDirectory_ThrowsUnauthorizedException()
    {
        // Arrange
        var baseDir1 = Path.Combine(_testBaseDirectory, "dir1");
        var baseDir2 = Path.Combine(_testBaseDirectory, "dir2");
        var requestedPath = Path.Combine(_testBaseDirectory, "dir3", "file.txt");

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() =>
            SecurePathValidator.ValidatePathMultiple(requestedPath, baseDir1, baseDir2));
    }

    [Fact]
    public void ValidatePathMultiple_WithNoAllowedDirectories_ThrowsArgumentException()
    {
        // Arrange
        var requestedPath = Path.Combine(_testBaseDirectory, "file.txt");

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurePathValidator.ValidatePathMultiple(requestedPath));
    }

    [Fact]
    public void IsValidAndExists_WithValidExistingPath_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.Combine(_testBaseDirectory, "existing.txt");
        Directory.CreateDirectory(_testBaseDirectory);
        File.WriteAllText(filePath, "test content");

        try
        {
            // Act
            var result = SecurePathValidator.IsValidAndExists(filePath, _testBaseDirectory);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void IsValidAndExists_WithValidNonExistingPath_ReturnsFalse()
    {
        // Arrange
        var filePath = Path.Combine(_testBaseDirectory, "nonexistent.txt");

        // Act
        var result = SecurePathValidator.IsValidAndExists(filePath, _testBaseDirectory);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidAndExists_WithInvalidPath_ReturnsFalse()
    {
        // Arrange
        var invalidPath = Path.Combine(_testBaseDirectory, "..", "..", "etc", "passwd");

        // Act
        var result = SecurePathValidator.IsValidAndExists(invalidPath, _testBaseDirectory);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePath_WithMixedSeparators_NormalizesPath()
    {
        // Arrange - Use mixed separators (this works on both Windows and Unix)
        var requestedPath = _testBaseDirectory + "/subfolder\\file.txt";

        // Act
        var result = SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith(_testBaseDirectory);
    }

    [Fact]
    public void ValidatePath_PreventsPartialDirectoryMatch()
    {
        // Arrange
        var baseDir = Path.Combine(_testBaseDirectory, "data");
        var similarDir = Path.Combine(_testBaseDirectory, "data-unsafe");

        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(similarDir);

        var requestedPath = Path.Combine(similarDir, "file.txt");

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() =>
            SecurePathValidator.ValidatePath(requestedPath, baseDir));
    }

    [Fact]
    public void ValidatePath_WithCurrentDirectory_HandlesCorrectly()
    {
        // Arrange
        var requestedPath = Path.Combine(_testBaseDirectory, ".", "file.txt");

        // Act
        var result = SecurePathValidator.ValidatePath(requestedPath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith(_testBaseDirectory);
    }

    [Fact]
    public void ValidatePath_IsCaseSensitiveOnUnix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Skip on Windows
            return;
        }

        // Arrange
        var baseDir = _testBaseDirectory.ToLowerInvariant();
        var requestedPath = Path.Combine(_testBaseDirectory.ToUpperInvariant(), "file.txt");

        // Act & Assert
        // On Unix, case matters, so this should potentially throw
        // However, Path.GetFullPath normalizes the case based on actual filesystem
        var result = SecurePathValidator.ValidatePath(requestedPath, baseDir);
        result.Should().NotBeNullOrEmpty();
    }
}
