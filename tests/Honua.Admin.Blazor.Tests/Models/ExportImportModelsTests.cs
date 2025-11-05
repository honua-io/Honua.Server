// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using FluentAssertions;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Tests.Models;

public class ExportImportModelsTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Fact]
    public void ExportRequest_ShouldSerializeCorrectly()
    {
        // Arrange
        var request = new ExportRequest
        {
            Format = ExportFormat.Json,
            Scope = ExportScope.Services,
            ItemIds = new List<string> { "service1", "service2" },
            IncludeRelated = true,
            IncludeMetadata = false,
            PrettyPrint = true
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ExportRequest>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Format.Should().Be(ExportFormat.Json);
        deserialized.Scope.Should().Be(ExportScope.Services);
        deserialized.ItemIds.Should().HaveCount(2);
        deserialized.IncludeRelated.Should().BeTrue();
        deserialized.IncludeMetadata.Should().BeFalse();
    }

    [Fact]
    public void ExportResponse_ShouldSerializeWithSummary()
    {
        // Arrange
        var response = new ExportResponse
        {
            Format = ExportFormat.Yaml,
            Content = "services:\n  - id: test",
            FileName = "export.yaml",
            ExportedAt = DateTimeOffset.UtcNow,
            Summary = new ExportSummary
            {
                ServiceCount = 5,
                LayerCount = 10,
                FolderCount = 2,
                StyleCount = 3,
                DataSourceCount = 1,
                SizeBytes = 2048
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ExportResponse>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Summary.ServiceCount.Should().Be(5);
        deserialized.Summary.LayerCount.Should().Be(10);
        deserialized.Summary.SizeBytes.Should().Be(2048);
    }

    [Fact]
    public void ImportRequest_WithAllOptions_ShouldSerializeCorrectly()
    {
        // Arrange
        var request = new ImportRequest
        {
            Content = "{\"services\":[]}",
            Mode = ImportMode.Replace,
            DryRun = true,
            SkipValidation = false,
            CreateBackup = true,
            BackupLabel = "pre-import-backup"
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ImportRequest>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Mode.Should().Be(ImportMode.Replace);
        deserialized.DryRun.Should().BeTrue();
        deserialized.CreateBackup.Should().BeTrue();
        deserialized.BackupLabel.Should().Be("pre-import-backup");
    }

    [Fact]
    public void ImportResponse_WithValidationErrors_ShouldSerializeCorrectly()
    {
        // Arrange
        var response = new ImportResponse
        {
            Success = false,
            DryRun = true,
            Mode = ImportMode.Merge,
            Validation = new ImportValidationResult
            {
                IsValid = false,
                Format = ExportFormat.Json,
                SchemaVersion = "1.0",
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Code = "INVALID_ID",
                        Message = "Service ID contains invalid characters",
                        ItemId = "service-1",
                        ItemType = "service",
                        FieldPath = "id"
                    }
                },
                Warnings = new List<ValidationWarning>
                {
                    new ValidationWarning
                    {
                        Code = "DEPRECATED_FIELD",
                        Message = "Field 'oldProperty' is deprecated",
                        ItemId = "service-1",
                        ItemType = "service"
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ImportResponse>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Success.Should().BeFalse();
        deserialized.Validation.IsValid.Should().BeFalse();
        deserialized.Validation.Errors.Should().HaveCount(1);
        deserialized.Validation.Errors[0].Code.Should().Be("INVALID_ID");
        deserialized.Validation.Warnings.Should().HaveCount(1);
    }

    [Fact]
    public void ImportChanges_ShouldTrackAllChangeTypes()
    {
        // Arrange
        var changes = new ImportChanges
        {
            ServicesCreated = new List<string> { "s1", "s2" },
            ServicesUpdated = new List<string> { "s3" },
            ServicesDeleted = new List<string> { "s4" },
            LayersCreated = new List<string> { "l1", "l2", "l3" },
            LayersUpdated = new List<string> { "l4" },
            FoldersCreated = new List<string> { "f1" },
            StylesCreated = new List<string> { "st1" }
        };

        // Act
        var json = JsonSerializer.Serialize(changes, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ImportChanges>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ServicesCreated.Should().HaveCount(2);
        deserialized.ServicesUpdated.Should().HaveCount(1);
        deserialized.ServicesDeleted.Should().HaveCount(1);
        deserialized.LayersCreated.Should().HaveCount(3);
        deserialized.TotalChanges.Should().Be(9); // Sum of all changes
    }

    [Fact]
    public void ImportChanges_TotalChanges_ShouldIncludeAllOperations()
    {
        // Arrange
        var changes = new ImportChanges
        {
            ServicesCreated = new List<string> { "s1" },
            ServicesUpdated = new List<string> { "s2" },
            ServicesDeleted = new List<string> { "s3" },
            LayersCreated = new List<string> { "l1", "l2" },
            LayersUpdated = new List<string> { "l3" },
            LayersDeleted = new List<string> { "l4" },
            FoldersCreated = new List<string> { "f1" },
            FoldersUpdated = new List<string> { "f2" },
            FoldersDeleted = new List<string> { "f3" },
            StylesCreated = new List<string> { "st1" },
            StylesUpdated = new List<string> { "st2" },
            StylesDeleted = new List<string> { "st3" }
        };

        // Act
        var total = changes.TotalChanges;

        // Assert
        total.Should().Be(12); // 1+1+1+2+1+1+1+1+1+1+1+1
    }

    [Fact]
    public void ValidationError_ShouldIncludeAllDetails()
    {
        // Arrange
        var error = new ValidationError
        {
            Code = "MISSING_FIELD",
            Message = "Required field 'name' is missing",
            ItemId = "service-123",
            ItemType = "service",
            FieldPath = "metadata.name"
        };

        // Act
        var json = JsonSerializer.Serialize(error, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ValidationError>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Code.Should().Be("MISSING_FIELD");
        deserialized.ItemId.Should().Be("service-123");
        deserialized.ItemType.Should().Be("service");
        deserialized.FieldPath.Should().Be("metadata.name");
    }

    [Fact]
    public void ExportScope_AllScopes_ShouldContainAllValues()
    {
        // Assert
        ExportScope.AllScopes.Should().Contain(ExportScope.All);
        ExportScope.AllScopes.Should().Contain(ExportScope.Services);
        ExportScope.AllScopes.Should().Contain(ExportScope.Layers);
        ExportScope.AllScopes.Should().Contain(ExportScope.Folders);
        ExportScope.AllScopes.Should().Contain(ExportScope.Styles);
        ExportScope.AllScopes.Should().Contain(ExportScope.DataSources);
    }

    [Fact]
    public void ImportMode_AllModes_ShouldContainAllValues()
    {
        // Assert
        ImportMode.AllModes.Should().Contain(ImportMode.Merge);
        ImportMode.AllModes.Should().Contain(ImportMode.Replace);
        ImportMode.AllModes.Should().Contain(ImportMode.Skip);
    }

    [Fact]
    public void ExportFormat_AllFormats_ShouldContainAllValues()
    {
        // Assert
        ExportFormat.AllFormats.Should().Contain(ExportFormat.Json);
        ExportFormat.AllFormats.Should().Contain(ExportFormat.Yaml);
    }

    [Fact]
    public void ExportRequest_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var request = new ExportRequest();

        // Assert
        request.Format.Should().Be("json");
        request.Scope.Should().Be("all");
        request.ItemIds.Should().NotBeNull();
        request.ItemIds.Should().BeEmpty();
        request.IncludeRelated.Should().BeTrue();
        request.IncludeMetadata.Should().BeTrue();
        request.PrettyPrint.Should().BeTrue();
    }

    [Fact]
    public void ImportRequest_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var request = new ImportRequest();

        // Assert
        request.Content.Should().BeEmpty();
        request.Mode.Should().Be("merge");
        request.DryRun.Should().BeFalse();
        request.SkipValidation.Should().BeFalse();
        request.CreateBackup.Should().BeTrue();
        request.BackupLabel.Should().BeNull();
    }

    [Fact]
    public void ExportSummary_ShouldSerializeAllCounts()
    {
        // Arrange
        var summary = new ExportSummary
        {
            ServiceCount = 10,
            LayerCount = 25,
            FolderCount = 5,
            StyleCount = 8,
            DataSourceCount = 3,
            SizeBytes = 1024 * 1024 // 1 MB
        };

        // Act
        var json = JsonSerializer.Serialize(summary, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ExportSummary>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ServiceCount.Should().Be(10);
        deserialized.LayerCount.Should().Be(25);
        deserialized.FolderCount.Should().Be(5);
        deserialized.StyleCount.Should().Be(8);
        deserialized.DataSourceCount.Should().Be(3);
        deserialized.SizeBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public void ImportResponse_WithWarnings_ShouldSerializeCorrectly()
    {
        // Arrange
        var response = new ImportResponse
        {
            Success = true,
            DryRun = false,
            Mode = ImportMode.Merge,
            Warnings = new List<string>
            {
                "Service 'old-service' uses deprecated features",
                "Layer 'test-layer' has no associated style"
            },
            Errors = new List<string>(),
            BackupLabel = "auto-backup-2025-01-01",
            ImportedAt = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ImportResponse>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Success.Should().BeTrue();
        deserialized.Warnings.Should().HaveCount(2);
        deserialized.Errors.Should().BeEmpty();
        deserialized.BackupLabel.Should().Be("auto-backup-2025-01-01");
    }
}
