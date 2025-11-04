using System;
using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Serialization;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Security;

/// <summary>
/// Security tests for KMZ archive asset name validation to prevent path traversal attacks.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Speed", "Fast")]
public sealed class KmzArchiveBuilderSecurityTests
{
    #region Asset Name Path Traversal Tests

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("./../../sensitive.txt")]
    [InlineData(".\\..\\..\\secret.key")]
    public void CreateArchive_WithPathTraversalInAssetName_ThrowsArgumentException(string maliciousAssetName)
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { maliciousAssetName, new byte[] { 1, 2, 3 } }
        };

        // Act & Assert
        Action act = () => KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*path traversal*");
    }

    [Theory]
    [InlineData("/etc/passwd")]  // Unix absolute path
    [InlineData("/var/log/messages")]  // Unix absolute path
    public void CreateArchive_WithAbsoluteUnixPathInAssetName_ThrowsArgumentException(string absolutePath)
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { absolutePath, new byte[] { 1, 2, 3 } }
        };

        // Act & Assert
        Action act = () => KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*absolute path*");
    }

    [Theory]
    [InlineData("C:\\Windows\\System32\\config\\SAM")]
    [InlineData("D:\\secrets\\passwords.txt")]
    public void CreateArchive_WithWindowsPathInAssetName_ValidatesOnWindows(string windowsPath)
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { windowsPath, new byte[] { 1, 2, 3 } }
        };

        // Act & Assert
        if (OperatingSystem.IsWindows())
        {
            // On Windows, these should be rejected as absolute paths
            Action act = () => KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);
            act.Should().Throw<ArgumentException>()
                .WithMessage("*absolute path*");
        }
        else
        {
            // On Linux/Mac, these are valid relative paths (though unusual)
            // The backslashes will be normalized to forward slashes
            // These could still be blocked by path traversal checks if they contain ".."
            // For this test, we just verify they don't crash
            var result = KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);
            result.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("../images/logo.png")]
    [InlineData("../../assets/style.css")]
    [InlineData("subdir/../sensitive.dat")]
    public void CreateArchive_WithRelativePathsContainingDotDot_ThrowsArgumentException(string relativePath)
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { relativePath, new byte[] { 1, 2, 3 } }
        };

        // Act & Assert
        Action act = () => KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Valid Asset Name Tests

    [Theory]
    [InlineData("logo.png")]
    [InlineData("images/icon.jpg")]
    [InlineData("assets/styles/main.css")]
    [InlineData("data_file.txt")]
    [InlineData("subfolder/nested/file.xml")]
    public void CreateArchive_WithValidAssetName_Succeeds(string validAssetName)
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { validAssetName, new byte[] { 1, 2, 3 } }
        };

        // Act
        var result = KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateArchive_WithNullAssets_Succeeds()
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";

        // Act
        var result = KmzArchiveBuilder.CreateArchive(kmlContent, null, null);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateArchive_WithEmptyAssets_Succeeds()
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>();

        // Act
        var result = KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    #endregion

    #region Invalid Character Tests

    [Theory]
    [InlineData("file\0name.txt")]  // Null byte
    [InlineData("file\tname.txt")]  // Tab (control character)
    [InlineData("file\nname.txt")]  // Newline (control character)
    public void CreateArchive_WithDangerousCharactersInAssetName_ThrowsArgumentException(string invalidAssetName)
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { invalidAssetName, new byte[] { 1, 2, 3 } }
        };

        // Act & Assert
        Action act = () => KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateArchive_WithNullByteInAssetName_ThrowsArgumentException()
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { "file\0name.txt", new byte[] { 1, 2, 3 } }
        };

        // Act & Assert
        Action act = () => KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*null byte*");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CreateArchive_WithEmptyAssetName_SkipsAsset()
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { "", new byte[] { 1, 2, 3 } },
            { "valid.png", new byte[] { 4, 5, 6 } }
        };

        // Act
        var result = KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateArchive_WithWhitespaceAssetName_SkipsAsset()
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { "   ", new byte[] { 1, 2, 3 } },
            { "valid.png", new byte[] { 4, 5, 6 } }
        };

        // Act
        var result = KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateArchive_WithNullAssetContent_SkipsAsset()
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]?>
        {
            { "test.png", null },
            { "valid.png", new byte[] { 1, 2, 3 } }
        };

        // Act - Cast to IReadOnlyDictionary<string, byte[]>
        var result = KmzArchiveBuilder.CreateArchive(kmlContent, null, (IReadOnlyDictionary<string, byte[]>)assets!);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    #endregion

    #region Comprehensive Attack Vectors

    [Theory]
    [InlineData("....//....//....//etc/passwd")]
    [InlineData("..../..../..../etc/passwd")]
    [InlineData("images/../../../secrets.txt")]
    [InlineData("./././../../../sensitive")]
    [InlineData("subdir/./../../attack")]
    public void CreateArchive_WithVariousPathTraversalPatterns_ThrowsArgumentException(string attackPattern)
    {
        // Arrange
        var kmlContent = "<kml>test</kml>";
        var assets = new Dictionary<string, byte[]>
        {
            { attackPattern, new byte[] { 1, 2, 3 } }
        };

        // Act & Assert
        Action act = () => KmzArchiveBuilder.CreateArchive(kmlContent, null, assets);
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
