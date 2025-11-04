using Honua.Server.Host.Validation;
using Xunit;

namespace Honua.Server.Host.Tests.Validation;

public sealed class FileNameSanitizerTests
{
    [Theory]
    [InlineData("document.pdf", "document.pdf")]
    [InlineData("report_2024.xlsx", "report_2024.xlsx")]
    [InlineData("image-file.jpg", "image-file.jpg")]
    [InlineData("my.data.file.csv", "my.data.file.csv")]
    public void Sanitize_ValidFileName_ReturnsUnchanged(string input, string expected)
    {
        // Act
        var result = FileNameSanitizer.Sanitize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("../../etc/passwd", "etc_passwd")]
    [InlineData("../../../config.ini", "config.ini")]
    [InlineData("~/.bashrc", "bashrc")]
    [InlineData("/etc/shadow", "shadow")]
    [InlineData("C:\\Windows\\System32\\config.sys", "config.sys")]
    public void Sanitize_PathTraversal_RemovesPathComponents(string input, string expected)
    {
        // Act
        var result = FileNameSanitizer.Sanitize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("file with spaces.txt", "file_with_spaces.txt")]
    [InlineData("file@#$%^&.txt", "file.txt")]
    [InlineData("file<>|.txt", "file.txt")]
    [InlineData("file:name.txt", "filename.txt")]
    public void Sanitize_InvalidCharacters_ReplacesWithUnderscore(string input, string expected)
    {
        // Act
        var result = FileNameSanitizer.Sanitize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(".hidden", "hidden")]
    [InlineData("..double", "double")]
    [InlineData("...triple", "triple")]
    public void Sanitize_LeadingPeriods_Removed(string input, string expected)
    {
        // Act
        var result = FileNameSanitizer.Sanitize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    public void Sanitize_WindowsReservedName_ThrowsArgumentException(string reservedName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => FileNameSanitizer.Sanitize(reservedName));
    }

    [Fact]
    public void Sanitize_VeryLongFileName_Truncates()
    {
        // Arrange
        var longName = new string('a', 300) + ".txt";

        // Act
        var result = FileNameSanitizer.Sanitize(longName);

        // Assert
        Assert.True(result.Length <= FileNameSanitizer.MaxFileNameLength);
        Assert.EndsWith(".txt", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Sanitize_EmptyOrNull_ThrowsArgumentException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => FileNameSanitizer.Sanitize(input!));
    }

    [Fact]
    public void IsSafe_SafeFileName_ReturnsTrue()
    {
        // Arrange
        var safeFileName = "document.pdf";

        // Act
        var result = FileNameSanitizer.IsSafe(safeFileName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("../file.txt")] // Path traversal
    [InlineData(".hidden")] // Hidden file
    [InlineData("file..txt")] // Multiple periods
    [InlineData("file/name.txt")] // Path separator
    [InlineData("file\\name.txt")] // Windows path separator
    [InlineData("file name.txt")] // Space (invalid character)
    public void IsSafe_UnsafeFileName_ReturnsFalse(string unsafeFileName)
    {
        // Act
        var result = FileNameSanitizer.IsSafe(unsafeFileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TrySanitize_ValidFileName_ReturnsTrue()
    {
        // Arrange
        var fileName = "document.pdf";

        // Act
        var result = FileNameSanitizer.TrySanitize(fileName, out var sanitized, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal("document.pdf", sanitized);
        Assert.Null(error);
    }

    [Fact]
    public void TrySanitize_ReservedName_ReturnsFalse()
    {
        // Arrange
        var fileName = "CON.txt";

        // Act
        var result = FileNameSanitizer.TrySanitize(fileName, out var sanitized, out var error);

        // Assert
        Assert.False(result);
        Assert.Null(sanitized);
        Assert.NotNull(error);
        Assert.Contains("reserved", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateSafeFileName_ValidExtension_ReturnsValidFileName()
    {
        // Arrange
        var extension = ".pdf";

        // Act
        var result = FileNameSanitizer.GenerateSafeFileName(extension);

        // Assert
        Assert.EndsWith(".pdf", result);
        Assert.True(result.Length > 4); // More than just the extension
        Assert.True(FileNameSanitizer.IsSafe(result));
    }

    [Fact]
    public void GenerateSafeFileName_WithPrefix_IncludesPrefix()
    {
        // Arrange
        var extension = ".txt";
        var prefix = "upload";

        // Act
        var result = FileNameSanitizer.GenerateSafeFileName(extension, prefix);

        // Assert
        Assert.StartsWith("upload", result);
        Assert.EndsWith(".txt", result);
    }

    [Theory]
    [InlineData("file.pdf", new[] { ".pdf" }, true)]
    [InlineData("file.pdf", new[] { ".jpg", ".png" }, false)]
    [InlineData("file.PDF", new[] { ".pdf" }, true)] // Case insensitive
    [InlineData("file.txt", new[] { "txt" }, true)] // Without leading period
    public void HasAllowedExtension_VariousExtensions_ReturnsExpectedResult(
        string fileName, string[] allowedExtensions, bool expected)
    {
        // Act
        var result = FileNameSanitizer.HasAllowedExtension(fileName, allowedExtensions);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("file.exe", new[] { ".pdf", ".doc", ".txt" })]
    [InlineData("script.bat", new[] { ".pdf", ".doc" })]
    [InlineData("program.com", new[] { ".pdf" })]
    public void HasAllowedExtension_DisallowedExtensions_ReturnsFalse(
        string fileName, string[] allowedExtensions)
    {
        // Act
        var result = FileNameSanitizer.HasAllowedExtension(fileName, allowedExtensions);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Sanitize_MultipleUnderscores_Collapses()
    {
        // Arrange
        var fileName = "file___name.txt";

        // Act
        var result = FileNameSanitizer.Sanitize(fileName);

        // Assert
        Assert.DoesNotContain("__", result);
    }

    [Theory]
    [InlineData("filename.", "filename")]
    [InlineData("filename..", "filename")]
    public void Sanitize_TrailingPeriods_Removed(string input, string expected)
    {
        // Act
        var result = FileNameSanitizer.Sanitize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Sanitize_PreservesExtension()
    {
        // Arrange
        var fileName = "my-file!@#$%^&*().pdf";

        // Act
        var result = FileNameSanitizer.Sanitize(fileName);

        // Assert
        Assert.EndsWith(".pdf", result);
    }
}
