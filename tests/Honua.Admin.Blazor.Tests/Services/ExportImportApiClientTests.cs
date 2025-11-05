// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using RichardSzalay.MockHttp;

namespace Honua.Admin.Blazor.Tests.Services;

public class ExportImportApiClientTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExportImportApiClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ExportImportApiClientTests()
    {
        _mockHandler = new MockHttpMessageHandler();

        var httpClient = _mockHandler.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://api.test");

        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient("AdminApi").Returns(httpClient);

        _client = new ExportImportApiClient(_httpClientFactory, NullLogger<ExportImportApiClient>.Instance);

        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public void Dispose()
    {
        _mockHandler.Dispose();
    }

    [Fact]
    public async Task ExportAsync_ShouldReturnExportResponse()
    {
        // Arrange
        var request = new ExportRequest
        {
            Format = ExportFormat.Json,
            Scope = ExportScope.All,
            IncludeRelated = true,
            IncludeMetadata = true,
            PrettyPrint = true
        };

        var expectedResponse = new ExportResponse
        {
            Format = ExportFormat.Json,
            Content = "{\"services\":[]}",
            FileName = "honua-catalog-20250101-120000.json",
            ExportedAt = DateTimeOffset.UtcNow,
            Summary = new ExportSummary
            {
                ServiceCount = 5,
                LayerCount = 10,
                FolderCount = 3,
                StyleCount = 2,
                SizeBytes = 1024
            }
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/metadata/export")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.ExportAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Format.Should().Be(ExportFormat.Json);
        result.Summary.ServiceCount.Should().Be(5);
        result.Summary.LayerCount.Should().Be(10);
    }

    [Fact]
    public async Task ExportAllAsync_ShouldExportEntireCatalog()
    {
        // Arrange
        var expectedResponse = new ExportResponse
        {
            Format = ExportFormat.Json,
            Content = "{\"services\":[],\"layers\":[],\"folders\":[]}",
            FileName = "honua-catalog-20250101-120000.json",
            ExportedAt = DateTimeOffset.UtcNow,
            Summary = new ExportSummary { ServiceCount = 10, LayerCount = 20, FolderCount = 5 }
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/metadata/export")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.ExportAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Summary.ServiceCount.Should().Be(10);
        result.Summary.LayerCount.Should().Be(20);
        result.Summary.FolderCount.Should().Be(5);
    }

    [Fact]
    public async Task ExportServicesAsync_ShouldExportSpecificServices()
    {
        // Arrange
        var serviceIds = new List<string> { "service1", "service2" };
        var expectedResponse = new ExportResponse
        {
            Format = ExportFormat.Json,
            Content = "{\"services\":[{\"id\":\"service1\"},{\"id\":\"service2\"}]}",
            FileName = "honua-services-20250101-120000.json",
            ExportedAt = DateTimeOffset.UtcNow,
            Summary = new ExportSummary { ServiceCount = 2, LayerCount = 4 }
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/metadata/export")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.ExportServicesAsync(serviceIds);

        // Assert
        result.Should().NotBeNull();
        result.Summary.ServiceCount.Should().Be(2);
        result.Summary.LayerCount.Should().Be(4);
    }

    [Fact]
    public async Task ImportAsync_ShouldReturnImportResponse()
    {
        // Arrange
        var request = new ImportRequest
        {
            Content = "{\"services\":[]}",
            Mode = ImportMode.Merge,
            DryRun = true,
            CreateBackup = false
        };

        var expectedResponse = new ImportResponse
        {
            Success = true,
            DryRun = true,
            Mode = ImportMode.Merge,
            Validation = new ImportValidationResult { IsValid = true, Format = ExportFormat.Json },
            Changes = new ImportChanges
            {
                ServicesCreated = new List<string> { "service1" },
                LayersCreated = new List<string> { "layer1", "layer2" }
            },
            ImportedAt = DateTimeOffset.UtcNow
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/metadata/import")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.ImportAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.DryRun.Should().BeTrue();
        result.Validation.IsValid.Should().BeTrue();
        result.Changes.ServicesCreated.Should().HaveCount(1);
        result.Changes.LayersCreated.Should().HaveCount(2);
    }

    [Fact]
    public async Task ValidateImportAsync_ShouldPerformDryRun()
    {
        // Arrange
        var content = "{\"services\":[{\"id\":\"test\"}]}";
        var expectedResponse = new ImportResponse
        {
            Success = true,
            DryRun = true,
            Mode = ImportMode.Merge,
            Validation = new ImportValidationResult
            {
                IsValid = true,
                Format = ExportFormat.Json,
                Errors = new List<ValidationError>(),
                Warnings = new List<ValidationWarning>()
            },
            Changes = new ImportChanges { ServicesCreated = new List<string> { "test" } }
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/metadata/import")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.ValidateImportAsync(content);

        // Assert
        result.Should().NotBeNull();
        result.DryRun.Should().BeTrue();
        result.Validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateImportAsync_WithErrors_ShouldReturnValidationErrors()
    {
        // Arrange
        var content = "{\"services\":[{\"invalid\":\"data\"}]}";
        var expectedResponse = new ImportResponse
        {
            Success = false,
            DryRun = true,
            Mode = ImportMode.Merge,
            Validation = new ImportValidationResult
            {
                IsValid = false,
                Format = ExportFormat.Json,
                Errors = new List<ValidationError>
                {
                    new ValidationError { Code = "MISSING_ID", Message = "Service ID is required", ItemType = "service" }
                }
            }
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/metadata/import")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.ValidateImportAsync(content);

        // Assert
        result.Should().NotBeNull();
        result.Validation.IsValid.Should().BeFalse();
        result.Validation.Errors.Should().HaveCount(1);
        result.Validation.Errors[0].Code.Should().Be("MISSING_ID");
    }

    [Fact]
    public async Task ApplyImportAsync_ShouldApplyImportWithBackup()
    {
        // Arrange
        var content = "{\"services\":[]}";
        var expectedResponse = new ImportResponse
        {
            Success = true,
            DryRun = false,
            Mode = ImportMode.Merge,
            Validation = new ImportValidationResult { IsValid = true },
            Changes = new ImportChanges { ServicesCreated = new List<string> { "service1" } },
            BackupLabel = "pre-import-backup",
            ImportedAt = DateTimeOffset.UtcNow
        };

        _mockHandler.When(HttpMethod.Post, "https://api.test/admin/metadata/import")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse, _jsonOptions));

        // Act
        var result = await _client.ApplyImportAsync(content, ImportMode.Merge, createBackup: true, backupLabel: "pre-import-backup");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.DryRun.Should().BeFalse();
        result.BackupLabel.Should().Be("pre-import-backup");
    }

    [Fact]
    public async Task DownloadExportAsync_ShouldReturnBytes()
    {
        // Arrange
        var export = new ExportResponse
        {
            Content = "{\"services\":[]}",
            FileName = "test.json"
        };

        // Act
        var bytes = await _client.DownloadExportAsync(export);

        // Assert
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void GetSuggestedFileName_ForCompleteExport_ShouldReturnCatalogFileName()
    {
        // Arrange
        var request = new ExportRequest { Scope = ExportScope.All, Format = ExportFormat.Json };

        // Act
        var fileName = _client.GetSuggestedFileName(request);

        // Assert
        fileName.Should().Contain("honua-catalog-");
        fileName.Should().EndWith(".json");
    }

    [Fact]
    public void GetSuggestedFileName_ForSingleService_ShouldIncludeServiceId()
    {
        // Arrange
        var request = new ExportRequest
        {
            Scope = ExportScope.Services,
            Format = ExportFormat.Yaml,
            ItemIds = new List<string> { "my-service" }
        };

        // Act
        var fileName = _client.GetSuggestedFileName(request);

        // Assert
        fileName.Should().Contain("honua-services-my-service-");
        fileName.Should().EndWith(".yaml");
    }

    [Fact]
    public void DetectFormat_ForJsonContent_ShouldReturnJson()
    {
        // Arrange
        var content = "{\"services\":[]}";

        // Act
        var format = _client.DetectFormat(content);

        // Assert
        format.Should().Be(ExportFormat.Json);
    }

    [Fact]
    public void DetectFormat_ForYamlContent_ShouldReturnYaml()
    {
        // Arrange
        var content = "services:\n  - id: test\n    name: Test Service";

        // Act
        var format = _client.DetectFormat(content);

        // Assert
        format.Should().Be(ExportFormat.Yaml);
    }

    [Fact]
    public void ValidateFileSize_WithValidSize_ShouldReturnTrue()
    {
        // Arrange
        var fileSize = 10 * 1024 * 1024; // 10 MB

        // Act
        var isValid = _client.ValidateFileSize(fileSize, out var errorMessage);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateFileSize_WithExcessiveSize_ShouldReturnFalse()
    {
        // Arrange
        var fileSize = 100 * 1024 * 1024; // 100 MB

        // Act
        var isValid = _client.ValidateFileSize(fileSize, out var errorMessage);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeEmpty();
        errorMessage.Should().Contain("exceeds maximum");
    }

    [Fact]
    public void ImportChanges_TotalChanges_ShouldCalculateCorrectly()
    {
        // Arrange
        var changes = new ImportChanges
        {
            ServicesCreated = new List<string> { "s1", "s2" },
            ServicesUpdated = new List<string> { "s3" },
            LayersCreated = new List<string> { "l1", "l2", "l3" },
            FoldersCreated = new List<string> { "f1" }
        };

        // Act
        var total = changes.TotalChanges;

        // Assert
        total.Should().Be(7); // 2 + 1 + 3 + 1
    }
}
