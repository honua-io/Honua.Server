using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FluentAssertions;
using Honua.Server.Core.Security;
using Xunit;

namespace Honua.Server.Host.Tests.Security;

/// <summary>
/// Security tests for ZIP archive validation to prevent path traversal,
/// zip bombs, and malicious file types.
/// </summary>
[Trait("Category", "Security")]
public sealed class ZipArchiveValidatorTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _createdFiles = new();

    public ZipArchiveValidatorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "honua_zip_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    #region Basic Validation Tests

    [Fact]
    public void ValidateZipArchive_WithValidZip_ReturnsSuccess()
    {
        // Arrange
        var zipPath = CreateTestZip("valid.zip", new Dictionary<string, string>
        {
            ["test.txt"] = "Hello World",
            ["data.json"] = "{\"test\": true}"
        });

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.EntryCount.Should().Be(2);
        result.ValidatedEntries.Should().Contain("test.txt");
        result.ValidatedEntries.Should().Contain("data.json");
    }

    [Fact]
    public void ValidateZipArchive_WithEmptyZip_ReturnsFailure()
    {
        // Arrange
        var zipPath = CreateTestZip("empty.zip", new Dictionary<string, string>());

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public void ValidateZipArchive_WithNonExistentFile_ReturnsFailure()
    {
        // Act
        var result = ZipArchiveValidator.ValidateZipFile("/nonexistent/file.zip");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    #endregion

    #region Path Traversal Tests

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("subdir/../../etc/passwd")]
    [InlineData("./../../etc/passwd")]
    public void ValidateZipArchive_WithPathTraversal_ReturnsFailure(string maliciousPath)
    {
        // Arrange
        var zipPath = CreateMaliciousZip("traversal.zip", maliciousPath, "malicious content");

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().MatchRegex("traversal|Invalid entry|outside");
    }

    [Fact]
    public void ValidateZipArchive_WithAbsolutePath_ReturnsFailure()
    {
        // Arrange
        var zipPath = CreateMaliciousZip("absolute.zip", "/etc/passwd", "malicious");

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("absolute path");
    }

    [Fact]
    public void ValidateZipArchive_WithWindowsAbsolutePath_ReturnsFailure()
    {
        // Arrange
        var zipPath = CreateMaliciousZip("absolute_win.zip", "C:\\Windows\\System32\\cmd.exe", "malicious");

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("absolute path");
    }

    [Fact]
    public void ValidateZipArchive_WithUncPath_ReturnsFailure()
    {
        // Arrange
        var zipPath = CreateMaliciousZip("unc.zip", "\\\\server\\share\\file.txt", "malicious");

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().MatchRegex("UNC|Invalid entry");
    }

    #endregion

    #region Malicious File Type Tests

    [Theory]
    [InlineData(".exe")]
    [InlineData(".dll")]
    [InlineData(".bat")]
    [InlineData(".cmd")]
    [InlineData(".sh")]
    [InlineData(".ps1")]
    [InlineData(".vbs")]
    [InlineData(".js")]
    [InlineData(".msi")]
    [InlineData(".jar")]
    public void ValidateZipArchive_WithDangerousExtension_ReturnsFailure(string extension)
    {
        // Arrange
        var zipPath = CreateTestZip($"malicious{extension}.zip", new Dictionary<string, string>
        {
            [$"malicious{extension}"] = "malicious content"
        });

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Dangerous file type");
    }

    [Fact]
    public void ValidateZipArchive_WithAllowedExtensions_AcceptsOnlyAllowed()
    {
        // Arrange
        var zipPath = CreateTestZip("mixed.zip", new Dictionary<string, string>
        {
            ["test.txt"] = "text content",
            ["data.csv"] = "csv,content"
        });

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt" };

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath, allowedExtensions);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(".csv");
        result.ErrorMessage.Should().Contain("not allowed");
    }

    [Fact]
    public void ValidateZipArchive_WithGeospatialExtensions_AcceptsValidFormats()
    {
        // Arrange
        var zipPath = CreateTestZip("geospatial.zip", new Dictionary<string, string>
        {
            ["layer.shp"] = "shapefile content",
            ["layer.shx"] = "index content",
            ["layer.dbf"] = "attributes content",
            ["layer.prj"] = "projection content",
            ["data.geojson"] = "{\"type\":\"FeatureCollection\"}",
            ["data.kml"] = "<kml></kml>"
        });

        var allowedExtensions = ZipArchiveValidator.GetGeospatialExtensions();

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath, allowedExtensions);

        // Assert
        result.IsValid.Should().BeTrue();
        result.EntryCount.Should().Be(6);
    }

    #endregion

    #region Zip Bomb Tests

    [Fact]
    public void ValidateZipArchive_WithExcessiveUncompressedSize_ReturnsFailure()
    {
        // Arrange
        var zipPath = CreateLargeUncompressedZip("large.zip", sizeInMB: 50);
        var maxSize = 10L * 1024 * 1024; // 10MB limit

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath, maxUncompressedSize: maxSize);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().MatchRegex("exceeds maximum|zip bomb");
    }

    [Fact]
    public void ValidateZipArchive_WithHighCompressionRatio_ReturnsFailure()
    {
        // Arrange - Create highly compressible content
        var zipPath = CreateHighlyCompressedZip("bomb.zip", compressionRatio: 150);

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath, maxCompressionRatio: 100);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().MatchRegex("compression ratio|zip bomb");
    }

    [Fact]
    public void ValidateZipArchive_WithTooManyEntries_ReturnsFailure()
    {
        // Arrange
        var entries = new Dictionary<string, string>();
        for (int i = 0; i < 150; i++)
        {
            entries[$"file_{i}.txt"] = $"Content {i}";
        }
        var zipPath = CreateTestZip("many.zip", entries);

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath, maxEntries: 100);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("too many entries");
    }

    [Fact]
    public void ValidateZipArchive_WithNestedZip_AddsWarning()
    {
        // Arrange
        var innerZip = CreateTestZip("inner.zip", new Dictionary<string, string>
        {
            ["test.txt"] = "inner content"
        });

        var outerEntries = new Dictionary<string, byte[]>
        {
            ["inner.zip"] = File.ReadAllBytes(innerZip),
            ["outer.txt"] = Encoding.UTF8.GetBytes("outer content")
        };

        var zipPath = CreateBinaryZip("nested.zip", outerEntries);

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Nested zip"));
    }

    #endregion

    #region Special Character Tests

    [Fact]
    public void ValidateZipArchive_WithNullByteInName_ReturnsFailure()
    {
        // Arrange
        var zipPath = CreateMaliciousZip("nullbyte.zip", "file\0.txt", "content");

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null byte");
    }

    [Fact]
    public void ValidateZipArchive_WithUrlEncodedPath_ReturnsFailure()
    {
        // Arrange
        var zipPath = CreateMaliciousZip("encoded.zip", "%2e%2e%2f%2e%2e%2fetc%2fpasswd", "content");

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("URL encoding");
    }

    [Fact]
    public void ValidateZipArchive_WithExcessivelyLongFileName_ReturnsFailure()
    {
        // Arrange
        var longName = new string('a', 300) + ".txt";
        var zipPath = CreateTestZip("longname.zip", new Dictionary<string, string>
        {
            [longName] = "content"
        });

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("name too long");
    }

    [Fact]
    public void ValidateZipArchive_WithControlCharacters_ReturnsFailure()
    {
        // Arrange
        var zipPath = CreateMaliciousZip("control.zip", "file\x01name.txt", "content");

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("control character");
    }

    #endregion

    #region Safe Extraction Tests

    [Fact]
    public void SafeExtract_WithValidZip_ExtractsFiles()
    {
        // Arrange
        var zipPath = CreateTestZip("extract.zip", new Dictionary<string, string>
        {
            ["file1.txt"] = "Content 1",
            ["file2.txt"] = "Content 2"
        });

        var validationResult = ZipArchiveValidator.ValidateZipFile(zipPath);
        var extractDir = Path.Combine(_testDirectory, "extracted");

        // Act
        using var stream = File.OpenRead(zipPath);
        var extractedFiles = ZipArchiveValidator.SafeExtract(stream, extractDir, validationResult);

        // Assert
        extractedFiles.Should().HaveCount(2);
        File.Exists(Path.Combine(extractDir, "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(extractDir, "file2.txt")).Should().BeTrue();
        File.ReadAllText(Path.Combine(extractDir, "file1.txt")).Should().Be("Content 1");
    }

    [Fact]
    public void SafeExtract_WithInvalidValidationResult_ThrowsException()
    {
        // Arrange
        var zipPath = CreateTestZip("test.zip", new Dictionary<string, string>
        {
            ["file.txt"] = "Content"
        });

        var invalidResult = ZipArchiveValidator.ValidationResult.Failure("Invalid");
        var extractDir = Path.Combine(_testDirectory, "extracted2");

        // Act
        using var stream = File.OpenRead(zipPath);
        Action act = () => ZipArchiveValidator.SafeExtract(stream, extractDir, invalidResult);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*failed validation*");
    }

    [Fact]
    public void SafeExtract_SkipsNestedZipFiles()
    {
        // Arrange
        var innerZip = CreateTestZip("inner.zip", new Dictionary<string, string>
        {
            ["nested.txt"] = "nested"
        });

        var outerEntries = new Dictionary<string, byte[]>
        {
            ["inner.zip"] = File.ReadAllBytes(innerZip),
            ["outer.txt"] = Encoding.UTF8.GetBytes("outer")
        };

        var zipPath = CreateBinaryZip("nested_extract.zip", outerEntries);
        var validationResult = ZipArchiveValidator.ValidateZipFile(zipPath);
        var extractDir = Path.Combine(_testDirectory, "extracted3");

        // Act
        using var stream = File.OpenRead(zipPath);
        var extractedFiles = ZipArchiveValidator.SafeExtract(stream, extractDir, validationResult);

        // Assert
        extractedFiles.Should().HaveCount(1);
        extractedFiles.Should().Contain(f => f.EndsWith("outer.txt"));
        extractedFiles.Should().NotContain(f => f.EndsWith("inner.zip"));
    }

    [Fact]
    public void SafeExtract_StripsPaths_ExtractsOnlyFileName()
    {
        // Arrange - Create zip with directory structure
        var zipPath = Path.Combine(_testDirectory, "with_dirs.zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("subdir/file.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("content");
        }
        _createdFiles.Add(zipPath);

        var validationResult = ZipArchiveValidator.ValidateZipFile(zipPath);
        var extractDir = Path.Combine(_testDirectory, "extracted4");

        // Act
        using var stream = File.OpenRead(zipPath);
        var extractedFiles = ZipArchiveValidator.SafeExtract(stream, extractDir, validationResult);

        // Assert
        extractedFiles.Should().HaveCount(1);
        // File should be extracted with just the filename, not the path
        File.Exists(Path.Combine(extractDir, "file.txt")).Should().BeTrue();
        File.Exists(Path.Combine(extractDir, "subdir", "file.txt")).Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidateZipArchive_WithDirectoryEntries_IgnoresThem()
    {
        // Arrange
        var zipPath = Path.Combine(_testDirectory, "with_dirs2.zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            archive.CreateEntry("dir1/");
            archive.CreateEntry("dir2/");
            var entry = archive.CreateEntry("dir1/file.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("content");
        }
        _createdFiles.Add(zipPath);

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeTrue();
        result.EntryCount.Should().Be(1); // Only the file, not the directories
    }

    [Fact]
    public void ValidateZipArchive_WithEmptyFileNames_HandlesGracefully()
    {
        // Arrange
        var zipPath = Path.Combine(_testDirectory, "empty_name.zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            archive.CreateEntry("validfile.txt");
        }
        _createdFiles.Add(zipPath);

        // Act
        var result = ZipArchiveValidator.ValidateZipFile(zipPath);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetGeospatialExtensions_ReturnsExpectedExtensions()
    {
        // Act
        var extensions = ZipArchiveValidator.GetGeospatialExtensions();

        // Assert
        extensions.Should().Contain(".shp");
        extensions.Should().Contain(".geojson");
        extensions.Should().Contain(".kml");
        extensions.Should().Contain(".gpkg");
        extensions.Should().Contain(".csv");
        extensions.Should().NotContain(".exe");
    }

    #endregion

    #region Helper Methods

    private string CreateTestZip(string fileName, Dictionary<string, string> entries)
    {
        var zipPath = Path.Combine(_testDirectory, fileName);
        using var fs = File.Create(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        foreach (var kvp in entries)
        {
            var entry = archive.CreateEntry(kvp.Key);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(kvp.Value);
        }

        _createdFiles.Add(zipPath);
        return zipPath;
    }

    private string CreateBinaryZip(string fileName, Dictionary<string, byte[]> entries)
    {
        var zipPath = Path.Combine(_testDirectory, fileName);
        using var fs = File.Create(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        foreach (var kvp in entries)
        {
            var entry = archive.CreateEntry(kvp.Key);
            using var stream = entry.Open();
            stream.Write(kvp.Value, 0, kvp.Value.Length);
        }

        _createdFiles.Add(zipPath);
        return zipPath;
    }

    private string CreateMaliciousZip(string fileName, string maliciousEntryName, string content)
    {
        var zipPath = Path.Combine(_testDirectory, fileName);
        using var fs = File.Create(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        // Use internal APIs to create entries with invalid names
        var entry = archive.CreateEntry(maliciousEntryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);

        _createdFiles.Add(zipPath);
        return zipPath;
    }

    private string CreateLargeUncompressedZip(string fileName, int sizeInMB)
    {
        var zipPath = Path.Combine(_testDirectory, fileName);
        using var fs = File.Create(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        // Create a file that will be large when uncompressed
        var entry = archive.CreateEntry("large.bin");
        using var stream = entry.Open();

        var buffer = new byte[1024 * 1024]; // 1MB
        for (int i = 0; i < sizeInMB; i++)
        {
            // Write random data that doesn't compress well
            Random.Shared.NextBytes(buffer);
            stream.Write(buffer, 0, buffer.Length);
        }

        _createdFiles.Add(zipPath);
        return zipPath;
    }

    private string CreateHighlyCompressedZip(string fileName, int compressionRatio)
    {
        var zipPath = Path.Combine(_testDirectory, fileName);
        using var fs = File.Create(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        // Create highly compressible content (repeated zeros)
        var entry = archive.CreateEntry("compressed.bin");
        using var stream = entry.Open();

        // Write zeros which compress extremely well
        var buffer = new byte[1024 * 1024]; // 1MB of zeros
        for (int i = 0; i < compressionRatio; i++)
        {
            stream.Write(buffer, 0, buffer.Length);
        }

        _createdFiles.Add(zipPath);
        return zipPath;
    }

    #endregion

    public void Dispose()
    {
        // Cleanup
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
