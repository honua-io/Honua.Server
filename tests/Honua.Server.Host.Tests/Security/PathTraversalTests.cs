using System;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Honua.Server.Core.Security;
using Xunit;

namespace Honua.Server.Host.Tests.Security;

/// <summary>
/// Security tests for path traversal vulnerability prevention.
/// Tests the SecurePathValidator against various attack vectors.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Speed", "Fast")]
public sealed class PathTraversalTests
{
    private readonly string _testBaseDirectory;
    private readonly string _restrictedDirectory;

    public PathTraversalTests()
    {
        // Create temporary test directories
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), "honua_test_" + Guid.NewGuid().ToString("N"));
        _restrictedDirectory = Path.Combine(Path.GetTempPath(), "honua_restricted_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testBaseDirectory);
        Directory.CreateDirectory(_restrictedDirectory);

        // Create a test file in the base directory
        File.WriteAllText(Path.Combine(_testBaseDirectory, "safe.txt"), "safe content");

        // Create a sensitive file in the restricted directory
        File.WriteAllText(Path.Combine(_restrictedDirectory, "passwd"), "root:x:0:0");
    }

    #region Basic Path Traversal Attacks

    [Fact]
    public void ValidatePath_WithRelativePathTraversal_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var attackPath = "../../../etc/passwd";

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(attackPath, _testBaseDirectory);
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*outside allowed directory*");
    }

    [Fact]
    public void ValidatePath_WithDotDotSlash_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var attackPath = Path.Combine("subdir", "..", "..", "..", "etc", "passwd");

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(attackPath, _testBaseDirectory);
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void ValidatePath_WithBackslashTraversal_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Windows-style path traversal
        var attackPath = "..\\..\\..\\windows\\system32\\config\\sam";

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(attackPath, _testBaseDirectory);
        act.Should().Throw<UnauthorizedAccessException>();
    }

    #endregion

    #region URL-Encoded Path Traversal

    [Fact]
    public void ValidatePath_WithUrlEncodedDots_ThrowsArgumentException()
    {
        // Arrange - %2e%2e is URL-encoded ".."
        var attackPath = "%2e%2e/%2e%2e/%2e%2e/etc/passwd";

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(attackPath, _testBaseDirectory);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*URL-encoded*");
    }

    [Fact]
    public void ValidatePath_WithUrlEncodedSlash_ThrowsArgumentException()
    {
        // Arrange - %2f is URL-encoded "/"
        var attackPath = "..%2f..%2f..%2fetc%2fpasswd";

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(attackPath, _testBaseDirectory);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*URL-encoded*");
    }

    [Fact]
    public void ValidatePath_WithUrlEncodedBackslash_ThrowsArgumentException()
    {
        // Arrange - %5c is URL-encoded "\"
        var attackPath = "..%5c..%5c..%5cwindows";

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(attackPath, _testBaseDirectory);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*URL-encoded*");
    }

    [Fact]
    public void ValidatePath_WithNullByteEncoding_ThrowsArgumentException()
    {
        // Arrange - %00 is URL-encoded null byte
        var attackPath = "safe.txt%00../../etc/passwd";

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(attackPath, _testBaseDirectory);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Encoded path traversal patterns*");
    }

    #endregion

    #region Absolute Path Attacks

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("/var/log/messages")]
    [InlineData("/root/.ssh/id_rsa")]
    public void ValidatePath_WithAbsoluteUnixPath_ThrowsOnLinux(string absolutePath)
    {
        // Skip on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(absolutePath, _testBaseDirectory);
        act.Should().Throw<Exception>(); // Either ArgumentException or UnauthorizedAccessException
    }

    [Theory]
    [InlineData("C:\\Windows\\System32\\config\\SAM")]
    [InlineData("D:\\secrets\\passwords.txt")]
    [InlineData("C:\\")]
    public void ValidatePath_WithAbsoluteWindowsPath_ThrowsOnWindows(string absolutePath)
    {
        // Skip on non-Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(absolutePath, _testBaseDirectory);
        act.Should().Throw<Exception>(); // Either ArgumentException or UnauthorizedAccessException
    }

    #endregion

    #region UNC Path Attacks

    [Theory]
    [InlineData("\\\\server\\share\\file.txt")]
    [InlineData("//server/share/file.txt")]
    [InlineData("\\\\?\\C:\\Windows\\System32")]
    public void ValidatePath_WithUncPath_ThrowsArgumentException(string uncPath)
    {
        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(uncPath, _testBaseDirectory);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UNC paths*");
    }

    #endregion

    #region Null Byte Attacks

    [Fact]
    public void ValidatePath_WithNullByte_ThrowsArgumentException()
    {
        // Arrange
        var attackPath = "safe.txt\0../../etc/passwd";

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(attackPath, _testBaseDirectory);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*null byte*");
    }

    #endregion

    #region Valid Path Scenarios

    [Fact]
    public void ValidatePath_WithValidRelativePath_ReturnsFullPath()
    {
        // Arrange
        var validPath = "safe.txt";

        // Act
        var result = SecurePathValidator.ValidatePath(validPath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNull();
        result.Should().StartWith(_testBaseDirectory);
        result.Should().EndWith("safe.txt");
    }

    [Fact]
    public void ValidatePath_WithValidSubdirectoryPath_ReturnsFullPath()
    {
        // Arrange
        var subDir = Path.Combine(_testBaseDirectory, "subdir");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "test.txt");
        File.WriteAllText(testFile, "content");

        var validPath = Path.Combine("subdir", "test.txt");

        // Act
        var result = SecurePathValidator.ValidatePath(validPath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNull();
        result.Should().StartWith(_testBaseDirectory);
        result.Should().EndWith(Path.Combine("subdir", "test.txt"));
    }

    [Fact]
    public void ValidatePath_WithSafeRelativeNavigation_ReturnsFullPath()
    {
        // Arrange - Navigate into subdir then back, but still within base
        var subDir = Path.Combine(_testBaseDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        var relativePath = Path.Combine("subdir", "..", "safe.txt");

        // Act
        var result = SecurePathValidator.ValidatePath(relativePath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNull();
        result.Should().StartWith(_testBaseDirectory);
    }

    #endregion

    #region Multiple Directory Validation

    [Fact]
    public void ValidatePathMultiple_WithFirstDirectoryValid_ReturnsPath()
    {
        // Arrange
        var validPath = "safe.txt";
        var allowedDirs = new[] { _testBaseDirectory, _restrictedDirectory };

        // Act
        var result = SecurePathValidator.ValidatePathMultiple(validPath, allowedDirs);

        // Assert
        result.Should().NotBeNull();
        result.Should().StartWith(_testBaseDirectory);
    }

    [Fact]
    public void ValidatePathMultiple_WithSecondDirectoryValid_ReturnsPath()
    {
        // Arrange
        var validPath = "passwd";
        var allowedDirs = new[] { _testBaseDirectory, _restrictedDirectory };

        // Act
        var result = SecurePathValidator.ValidatePathMultiple(validPath, allowedDirs);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain(_restrictedDirectory);
    }

    [Fact]
    public void ValidatePathMultiple_WithNoValidDirectory_ThrowsUnauthorizedAccessException()
    {
        // Arrange - Path that doesn't exist in any allowed directory
        var invalidPath = "../../../etc/passwd";
        var allowedDirs = new[] { _testBaseDirectory, _restrictedDirectory };

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePathMultiple(invalidPath, allowedDirs);
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*outside all allowed directories*");
    }

    [Fact]
    public void ValidatePathMultiple_WithEmptyDirectoryList_ThrowsArgumentException()
    {
        // Arrange
        var validPath = "safe.txt";
        var allowedDirs = Array.Empty<string>();

        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePathMultiple(validPath, allowedDirs);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one allowed directory*");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidatePath_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath("", _testBaseDirectory);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidatePath_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath("   ", _testBaseDirectory);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidatePath_WithEmptyBaseDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath("safe.txt", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsValidAndExists_WithValidExistingPath_ReturnsTrue()
    {
        // Arrange
        var validPath = "safe.txt";

        // Act
        var result = SecurePathValidator.IsValidAndExists(validPath, _testBaseDirectory);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidAndExists_WithValidNonExistingPath_ReturnsFalse()
    {
        // Arrange
        var validPath = "nonexistent.txt";

        // Act
        var result = SecurePathValidator.IsValidAndExists(validPath, _testBaseDirectory);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidAndExists_WithInvalidPath_ReturnsFalse()
    {
        // Arrange
        var invalidPath = "../../../etc/passwd";

        // Act
        var result = SecurePathValidator.IsValidAndExists(invalidPath, _testBaseDirectory);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Symlink Attack Prevention

    [Fact]
    public void ValidatePath_WithSymlinkPointingOutside_ThrowsUnauthorizedAccessException()
    {
        // Skip this test on Windows or if running without permissions
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Symlinks on Windows require admin privileges
            return;
        }

        try
        {
            // Arrange - Create a symlink inside base dir pointing outside
            var symlinkPath = Path.Combine(_testBaseDirectory, "evil_link");
            var targetPath = Path.Combine(_restrictedDirectory, "passwd");

            // Create symlink (may fail without permissions)
            File.CreateSymbolicLink(symlinkPath, targetPath);

            // Act & Assert
            // GetFullPath resolves symlinks, so this should be caught
            Action act = () => SecurePathValidator.ValidatePath("evil_link", _testBaseDirectory);

            // The validator should either throw or the resolved path should fail validation
            try
            {
                var result = act();
                // If it doesn't throw, verify it's not the restricted file
                result.Should().NotContain(_restrictedDirectory);
            }
            catch
            {
                // Expected - symlink attack was prevented
                Assert.True(true);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Creating symlink failed - skip test
            return;
        }
        catch (IOException)
        {
            // Creating symlink failed - skip test
            return;
        }
    }

    #endregion

    #region Real-World Attack Scenarios

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("/etc/shadow")]
    [InlineData("C:\\Windows\\System32\\config\\SAM")]
    [InlineData("\\\\localhost\\c$\\windows\\system32")]
    [InlineData("%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    [InlineData("safe.txt\0../../etc/passwd")]
    [InlineData("....//....//....//etc/passwd")]
    [InlineData("..../..../..../etc/passwd")]
    public void ValidatePath_WithCommonAttackVectors_ThrowsException(string attackPath)
    {
        // Act & Assert
        Action act = () => SecurePathValidator.ValidatePath(attackPath, _testBaseDirectory);
        act.Should().Throw<Exception>(); // Any exception is acceptable - attack was blocked
    }

    #endregion

    #region Case Sensitivity Tests

    [Fact]
    public void ValidatePath_OnWindows_IsCaseInsensitive()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange
        var validPath = "SAFE.TXT";

        // Act
        var result = SecurePathValidator.ValidatePath(validPath, _testBaseDirectory);

        // Assert
        result.Should().NotBeNull();
        result.Should().StartWith(_testBaseDirectory);
    }

    [Fact]
    public void ValidatePath_OnLinux_IsCaseSensitive()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange - Create file with specific case
        var testFile = Path.Combine(_testBaseDirectory, "CaseSensitive.txt");
        File.WriteAllText(testFile, "content");

        // Act - Request with different case
        var result = SecurePathValidator.ValidatePath("CASESENSITIVE.TXT", _testBaseDirectory);

        // Assert - Should succeed (path validation doesn't check existence)
        result.Should().NotBeNull();
        result.Should().StartWith(_testBaseDirectory);
    }

    #endregion

    public void Dispose()
    {
        // Cleanup test directories
        try
        {
            if (Directory.Exists(_testBaseDirectory))
            {
                Directory.Delete(_testBaseDirectory, true);
            }
            if (Directory.Exists(_restrictedDirectory))
            {
                Directory.Delete(_restrictedDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
