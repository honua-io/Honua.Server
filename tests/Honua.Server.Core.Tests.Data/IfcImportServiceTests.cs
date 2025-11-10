// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Core.Models.Ifc;
using Honua.Server.Core.Services;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Data;

/// <summary>
/// Unit tests for IfcImportService - Phase 1.3: IFC Import Support.
/// Tests validation, metadata extraction, and file format detection.
/// Note: Full import tests require Xbim.Essentials integration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Phase", "Phase1")]
public class IfcImportServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<IfcImportService> _logger;
    private readonly IfcImportService _service;

    public IfcImportServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        _logger = loggerFactory.CreateLogger<IfcImportService>();
        _service = new IfcImportService(_logger);
    }

    #region File Validation Tests

    [Fact]
    public async Task ValidateIfc_WithValidStepFile_ShouldReturnValid()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile();
        using var stream = new MemoryStream(ifcContent);

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid, "Valid IFC file should pass validation");
        Assert.Equal("STEP", result.FileFormat);
        Assert.NotNull(result.SchemaVersion);
        Assert.NotEmpty(result.SchemaVersion);

        _output.WriteLine($"Validated IFC file: {result.SchemaVersion}, Format: {result.FileFormat}");
    }

    [Fact]
    public async Task ValidateIfc_WithInvalidFile_ShouldReturnInvalid()
    {
        // Arrange
        var invalidContent = System.Text.Encoding.UTF8.GetBytes("This is not an IFC file");
        using var stream = new MemoryStream(invalidContent);

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid, "Invalid file should fail validation");
        Assert.NotNull(result.Errors);
        Assert.NotEmpty(result.Errors);

        _output.WriteLine($"Expected validation error: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public async Task ValidateIfc_WithEmptyFile_ShouldReturnInvalid()
    {
        // Arrange
        using var stream = new MemoryStream(Array.Empty<byte>());

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ValidateIfc_WithMalformedStepFile_ShouldReturnInvalid()
    {
        // Arrange - Missing ENDSEC
        var malformedIfc = @"ISO-10303-21;
HEADER;
FILE_SCHEMA(('IFC4'));
DATA;
#1=IFCPROJECT('test',$,'Test',$,$,$,$,$,$);
END-ISO-10303-21;";
        var content = System.Text.Encoding.UTF8.GetBytes(malformedIfc);
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region Metadata Extraction Tests

    [Fact]
    public async Task ExtractMetadata_WithValidIfc_ShouldReturnMetadata()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile();
        using var stream = new MemoryStream(ifcContent);

        // Act
        var metadata = await _service.ExtractMetadataAsync(stream);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.SchemaVersion);
        Assert.NotEmpty(metadata.SchemaVersion);
        Assert.Equal("METRE", metadata.LengthUnit);

        _output.WriteLine($"Extracted metadata - Schema: {metadata.SchemaVersion}, Unit: {metadata.LengthUnit}");
    }

    [Fact]
    public async Task ExtractMetadata_WithIfc4File_ShouldDetectIfc4()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile(); // Generates IFC4
        using var stream = new MemoryStream(ifcContent);

        // Act
        var metadata = await _service.ExtractMetadataAsync(stream);

        // Assert
        Assert.Contains("IFC4", metadata.SchemaVersion);
    }

    [Fact]
    public async Task ExtractMetadata_WithIfc2x3File_ShouldDetectIfc2x3()
    {
        // Arrange
        var ifcContent = GenerateIfc2x3StepFile();
        using var stream = new MemoryStream(ifcContent);

        // Act
        var metadata = await _service.ExtractMetadataAsync(stream);

        // Assert
        Assert.Contains("IFC2X3", metadata.SchemaVersion);
    }

    #endregion

    #region Schema Version Tests

    [Fact]
    public void GetSupportedSchemaVersions_ShouldReturnVersionList()
    {
        // Act
        var versions = _service.GetSupportedSchemaVersions();

        // Assert
        Assert.NotNull(versions);
        Assert.NotEmpty(versions);
        Assert.Contains("IFC4", versions);
        Assert.Contains("IFC2X3", versions);

        _output.WriteLine($"Supported IFC schemas: {string.Join(", ", versions)}");
    }

    #endregion

    #region Null/Invalid Input Tests

    [Fact]
    public async Task ValidateIfc_WithNullStream_ShouldThrowArgumentNullException()
    {
        // Arrange
        Stream? nullStream = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.ValidateIfcAsync(nullStream!));
    }

    [Fact]
    public async Task ExtractMetadata_WithNullStream_ShouldThrowArgumentNullException()
    {
        // Arrange
        Stream? nullStream = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.ExtractMetadataAsync(nullStream!));
    }

    [Fact]
    public async Task ImportIfcFile_WithNullStream_ShouldThrowArgumentNullException()
    {
        // Arrange
        Stream? nullStream = null;
        var options = new IfcImportOptions
        {
            TargetServiceId = "test-service",
            TargetLayerId = "test-layer"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.ImportIfcFileAsync(nullStream!, options));
    }

    [Fact]
    public async Task ImportIfcFile_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile();
        using var stream = new MemoryStream(ifcContent);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.ImportIfcFileAsync(stream, null!));
    }

    #endregion

    #region Format Detection Tests

    [Fact]
    public async Task ValidateIfc_WithStepFormat_ShouldDetectFormat()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile();
        using var stream = new MemoryStream(ifcContent);

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert
        Assert.Equal("STEP", result.FileFormat);
    }

    [Fact]
    public async Task ValidateIfc_WithInvalidHeader_ShouldFail()
    {
        // Arrange - Missing ISO-10303-21 header
        var invalidIfc = @"HEADER;
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
ENDSEC;
END-ISO-10303-21;";
        var content = System.Text.Encoding.UTF8.GetBytes(invalidIfc);
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region Import Tests (Skipped - Requires Xbim.Essentials)

    [Fact(Skip = "Requires Xbim.Essentials integration")]
    public async Task ImportIfcFile_WithValidFile_ShouldCreateFeatures()
    {
        // Arrange
        var ifcContent = GenerateSimpleIfcStepFile();
        using var stream = new MemoryStream(ifcContent);

        var options = new IfcImportOptions
        {
            TargetServiceId = "test-service",
            TargetLayerId = "test-layer",
            ImportGeometry = true,
            ImportProperties = true,
            ImportRelationships = true
        };

        // Act
        var result = await _service.ImportIfcFileAsync(stream, options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.FeaturesCreated > 0);
        Assert.NotEmpty(result.EntityTypeCounts);
    }

    [Fact(Skip = "Requires Xbim.Essentials integration")]
    public async Task ImportIfcFile_WithWalls_ShouldImportWallEntities()
    {
        // Arrange
        var ifcContent = GenerateIfcWithWalls();
        using var stream = new MemoryStream(ifcContent);

        var options = new IfcImportOptions
        {
            TargetServiceId = "test-service",
            TargetLayerId = "test-layer",
            EntityTypeFilter = new[] { "IfcWall" }
        };

        // Act
        var result = await _service.ImportIfcFileAsync(stream, options);

        // Assert
        Assert.True(result.EntityTypeCounts.ContainsKey("IfcWall"));
        Assert.True(result.EntityTypeCounts["IfcWall"] > 0);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task ValidateIfc_WithLargeFile_ShouldHandleGracefully()
    {
        // Arrange - Create a large IFC file (simulated)
        var largeIfcContent = GenerateLargeIfcFile();
        using var stream = new MemoryStream(largeIfcContent);

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert
        Assert.NotNull(result);
        // Should either succeed or fail gracefully without crashing

        _output.WriteLine($"Large file validation result: {result.IsValid}");
    }

    [Fact]
    public async Task ValidateIfc_WithUnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var ifcWithUnicode = GenerateIfcWithUnicode();
        using var stream = new MemoryStream(ifcWithUnicode);

        // Act
        var result = await _service.ValidateIfcAsync(stream);

        // Assert - Should handle Unicode without crashing
        Assert.NotNull(result);
    }

    #endregion

    #region Test Data Generators

    private static byte[] GenerateSimpleIfcStepFile()
    {
        // Minimal valid IFC4 STEP file
        var ifc = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('test.ifc','2025-11-10T00:00:00',('Test Author'),('Test Org'),'','','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('3MD_HkJ6X2EhY9W5t6mVFX',$,'Test Project',$,$,$,$,(#2),#3);
#2=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#4,$);
#3=IFCUNITASSIGNMENT((#5));
#4=IFCAXIS2PLACEMENT3D(#6,$,$);
#5=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#6=IFCCARTESIANPOINT((0.,0.,0.));
ENDSEC;
END-ISO-10303-21;
";
        return System.Text.Encoding.UTF8.GetBytes(ifc);
    }

    private static byte[] GenerateIfc2x3StepFile()
    {
        // Minimal valid IFC2X3 STEP file
        var ifc = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('test.ifc','2025-11-10T00:00:00',('Test Author'),('Test Org'),'','','');
FILE_SCHEMA(('IFC2X3'));
ENDSEC;
DATA;
#1=IFCPROJECT('3MD_HkJ6X2EhY9W5t6mVFX',$,'Test Project IFC2X3',$,$,$,$,(#2),#3);
#2=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#4,$);
#3=IFCUNITASSIGNMENT((#5));
#4=IFCAXIS2PLACEMENT3D(#6,$,$);
#5=IFCSIUNIT(*,.LENGTHUNIT.,.METRE.);
#6=IFCCARTESIANPOINT((0.,0.,0.));
ENDSEC;
END-ISO-10303-21;
";
        return System.Text.Encoding.UTF8.GetBytes(ifc);
    }

    private static byte[] GenerateIfcWithWalls()
    {
        // IFC file with wall entities (simplified)
        var ifc = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('walls.ifc','2025-11-10T00:00:00',('Test'),('Test'),'','','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('test',$,'Project',$,$,$,$,(#2),#3);
#2=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#4,$);
#3=IFCUNITASSIGNMENT((#5));
#4=IFCAXIS2PLACEMENT3D(#6,$,$);
#5=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#6=IFCCARTESIANPOINT((0.,0.,0.));
#10=IFCWALL('wall1',$,'Test Wall',$,$,#4,$,$);
#11=IFCWALL('wall2',$,'Test Wall 2',$,$,#4,$,$);
ENDSEC;
END-ISO-10303-21;
";
        return System.Text.Encoding.UTF8.GetBytes(ifc);
    }

    private static byte[] GenerateLargeIfcFile()
    {
        // Simulate a large IFC file with many entities
        var header = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('Large File'),'2;1');
FILE_NAME('large.ifc','2025-11-10T00:00:00',('Test'),('Test'),'','','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('test',$,'Project',$,$,$,$,(#2),#3);
#2=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#4,$);
#3=IFCUNITASSIGNMENT((#5));
#4=IFCAXIS2PLACEMENT3D(#6,$,$);
#5=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#6=IFCCARTESIANPOINT((0.,0.,0.));
";

        var entities = "";
        for (int i = 10; i < 1000; i++)
        {
            entities += $"#{i}=IFCWALL('wall{i}',$,'Wall {i}',$,$,#4,$,$);\n";
        }

        var footer = @"ENDSEC;
END-ISO-10303-21;
";

        return System.Text.Encoding.UTF8.GetBytes(header + entities + footer);
    }

    private static byte[] GenerateIfcWithUnicode()
    {
        // IFC file with Unicode characters
        var ifc = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('Unicode Test'),'2;1');
FILE_NAME('unicode.ifc','2025-11-10T00:00:00',('测试 作者'),('組織'),'','','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('test',$,'プロジェクト 日本語',$,$,$,$,(#2),#3);
#2=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#4,$);
#3=IFCUNITASSIGNMENT((#5));
#4=IFCAXIS2PLACEMENT3D(#6,$,$);
#5=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#6=IFCCARTESIANPOINT((0.,0.,0.));
ENDSEC;
END-ISO-10303-21;
";
        return System.Text.Encoding.UTF8.GetBytes(ifc);
    }

    #endregion
}
