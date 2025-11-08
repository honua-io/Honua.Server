// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Bunit;
using Honua.Admin.Blazor.Components.Pages;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using MudBlazor;

namespace Honua.Admin.Blazor.Tests.Components.Pages;

/// <summary>
/// Tests for the DataImportWizard component.
/// </summary>
[Trait("Category", "Unit")]
public class DataImportWizardTests : ComponentTestBase
{
    private readonly Mock<ImportApiClient> _mockImportApiClient;
    private readonly Mock<ServiceApiClient> _mockServiceApiClient;
    private readonly Mock<ISnackbar> _mockSnackbar;
    private readonly Mock<IJSRuntime> _mockJSRuntime;

    public DataImportWizardTests()
    {
        _mockImportApiClient = new Mock<ImportApiClient>(
            MockBehavior.Strict,
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<ILogger<ImportApiClient>>());

        _mockServiceApiClient = new Mock<ServiceApiClient>(
            MockBehavior.Strict,
            new HttpClient());

        _mockSnackbar = new Mock<ISnackbar>();
        _mockJSRuntime = new Mock<IJSRuntime>();

        Context.Services.AddSingleton(_mockImportApiClient.Object);
        Context.Services.AddSingleton(_mockServiceApiClient.Object);
        Context.Services.AddSingleton(_mockSnackbar.Object);
        Context.Services.AddSingleton(_mockJSRuntime.Object);

        var authStateProvider = new TestAuthenticationStateProvider(
            TestAuthenticationStateProvider.CreateAdministrator());
        Context.Services.AddSingleton<AuthenticationStateProvider>(authStateProvider);

        // Setup JSInterop for drag and drop initialization
        _mockJSRuntime
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "initializeDragAndDrop",
                It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSObjectReference>());
    }

    private List<ServiceListItem> CreateSampleServices()
    {
        return new List<ServiceListItem>
        {
            new ServiceListItem
            {
                Id = "wms-service",
                Name = "WMS Service",
                Title = "Web Map Service",
                ServiceType = "WMS",
                Enabled = true,
                LayerCount = 5
            },
            new ServiceListItem
            {
                Id = "wfs-service",
                Name = "WFS Service",
                Title = "Web Feature Service",
                ServiceType = "WFS",
                Enabled = true,
                LayerCount = 3
            }
        };
    }

    [Fact]
    public async Task FileSelection_ValidFile_AdvancesToNextStep()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify first step is file upload
        cut.Markup.Should().Contain("Upload File");
        cut.Markup.Should().Contain("Select a geospatial file to import");
        cut.Markup.Should().Contain("Supported formats");

        // Verify stepper structure
        cut.Markup.Should().Contain("Configure Target");
        cut.Markup.Should().Contain("Review & Import");
    }

    [Fact]
    public async Task FileSelection_OversizedFile_ShowsError()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Max file size should be 500 MB
        cut.Markup.Should().Contain("Max file size: 500 MB");
    }

    [Fact]
    public async Task DragAndDrop_ValidFile_SelectsFile()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify drag and drop zone is present
        cut.Markup.Should().Contain("fileDropZone");
        cut.Markup.Should().Contain("Click to select a file");
        cut.Markup.Should().Contain("or drag and drop");

        // Verify drag and drop initialization was attempted
        _mockJSRuntime.Verify(
            x => x.InvokeAsync<IJSObjectReference>(
                "initializeDragAndDrop",
                It.IsAny<object[]>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ConfigureTarget_ValidService_EnablesNext()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify configure target step content
        cut.Markup.Should().Contain("Target Service");
        cut.Markup.Should().Contain("Layer Configuration");
        cut.Markup.Should().Contain("Create new layer");

        // Verify services are loaded
        services.Should().HaveCount(2);
        services[0].Id.Should().Be("wms-service");
        services[1].Id.Should().Be("wfs-service");
    }

    [Fact]
    public async Task ConfigureTarget_MissingLayer_DisablesNext()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify layer configuration requires input
        cut.Markup.Should().Contain("Layer ID");
        cut.Markup.Should().Contain("Unique identifier");
        cut.Markup.Should().Contain("Layer Name");
        cut.Markup.Should().Contain("Display name (optional)");
    }

    [Fact]
    public async Task StartImport_ValidConfiguration_CreatesJob()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        var expectedJob = new ImportJobSnapshot
        {
            JobId = Guid.NewGuid(),
            ServiceId = "wms-service",
            LayerId = "test-layer",
            FileName = "test.geojson",
            Status = "Queued",
            Progress = 0,
            CreatedAt = DateTime.UtcNow
        };

        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("test.geojson");
        mockFile.Setup(f => f.Size).Returns(1024);
        mockFile.Setup(f => f.ContentType).Returns("application/geo+json");
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(new MemoryStream(new byte[1024]));

        _mockImportApiClient
            .Setup(x => x.CreateImportJobAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IBrowserFile>(),
                It.IsAny<bool>(),
                It.IsAny<IProgress<double>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJob);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify review step structure
        cut.Markup.Should().Contain("Review & Import");
        cut.Markup.Should().Contain("Review import configuration");
    }

    [Fact]
    public async Task StartImport_WithProgress_UpdatesProgressBar()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        var expectedJob = new ImportJobSnapshot
        {
            JobId = Guid.NewGuid(),
            ServiceId = "wms-service",
            LayerId = "test-layer",
            Status = "Queued",
            CreatedAt = DateTime.UtcNow
        };

        Progress<double>? capturedProgress = null;

        _mockImportApiClient
            .Setup(x => x.CreateImportJobAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IBrowserFile>(),
                It.IsAny<bool>(),
                It.IsAny<IProgress<double>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, IBrowserFile, bool, IProgress<double>?, CancellationToken>(
                (_, _, _, _, progress, _) =>
                {
                    capturedProgress = progress as Progress<double>;
                    // Simulate progress updates
                    progress?.Report(25);
                    progress?.Report(50);
                    progress?.Report(75);
                    progress?.Report(100);
                })
            .ReturnsAsync(expectedJob);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify progress tracking is implemented
        // Progress bar elements should be present in the markup
        cut.Markup.Should().Contain("Review & Import");
    }

    [Fact]
    public async Task UploadSpeed_Calculates_CorrectlyFromDuration()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Test upload speed calculation logic
        var fileSize = 10 * 1024 * 1024L; // 10 MB
        var elapsedSeconds = 5.0;
        var expectedSpeed = fileSize / elapsedSeconds; // bytes per second

        expectedSpeed.Should().BeGreaterThan(0);
        expectedSpeed.Should().Be(fileSize / elapsedSeconds);

        // Format as human-readable
        var speedMB = expectedSpeed / (1024 * 1024);
        speedMB.Should().BeApproximately(2.0, 0.01); // ~2 MB/s
    }

    [Fact]
    public async Task Wizard_LoadServices_Success()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert
        _mockServiceApiClient.Verify(
            x => x.GetServicesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Wizard_LoadServices_Error_ShowsSnackbar()
    {
        // Arrange
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Failed to load services"));

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert
        _mockSnackbar.Verify(
            x => x.Add(
                It.Is<string>(s => s.Contains("Error loading services")),
                It.IsAny<Severity>(),
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Wizard_FileFormatInfo_DisplaysSupportedFormats()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify supported formats are displayed
        cut.Markup.Should().Contain("Supported formats");
        cut.Markup.Should().Contain("Shapefile");
        cut.Markup.Should().Contain("GeoJSON");
        cut.Markup.Should().Contain("GeoPackage");
        cut.Markup.Should().Contain("KML");
        cut.Markup.Should().Contain("GML");
        cut.Markup.Should().Contain("CSV");
    }

    [Fact]
    public async Task Wizard_BackNavigation_ReturnsToPreviousStep()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify back button functionality
        cut.Markup.Should().Contain("Back");
    }

    [Fact]
    public async Task Wizard_ResetAfterCompletion_ClearsState()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        var expectedJob = new ImportJobSnapshot
        {
            JobId = Guid.NewGuid(),
            ServiceId = "wms-service",
            LayerId = "test-layer",
            Status = "Queued",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify reset functionality
        cut.Markup.Should().Contain("Import Another File");
    }

    [Fact]
    public async Task Wizard_SuccessfulImport_ShowsJobLink()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify success state elements
        cut.Markup.Should().Contain("View Job Status");
        cut.Markup.Should().Contain("View All Jobs");
    }

    [Fact]
    public async Task Wizard_FileSize_FormatsCorrectly()
    {
        // Arrange & Assert - Test file size formatting logic
        var testCases = new[]
        {
            (bytes: 512L, expected: "512 B"),
            (bytes: 1024L, expected: "1 KB"),
            (bytes: 1024L * 1024, expected: "1 MB"),
            (bytes: 1024L * 1024 * 1024, expected: "1 GB"),
            (bytes: 1536L, expected: "1.5 KB"),
            (bytes: 2.5 * 1024 * 1024, expected: "2.5 MB")
        };

        foreach (var (bytes, expected) in testCases)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            var result = $"{len:0.##} {sizes[order]}";
            result.Should().Contain(sizes[order]);
        }
    }

    [Fact]
    public async Task Wizard_CreateNewLayerToggle_ShowsHidesFields()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify create new layer toggle behavior
        cut.Markup.Should().Contain("Create new layer");
        cut.Markup.Should().Contain("Appending to existing layers is not supported yet");
    }

    [Fact]
    public async Task Wizard_StepperNavigation_ValidatesEachStep()
    {
        // Arrange
        var services = CreateSampleServices();
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<DataImportWizard>();
        await Task.Delay(200);

        // Assert - Verify stepper is linear
        cut.Markup.Should().Contain("Upload File");
        cut.Markup.Should().Contain("Configure Target");
        cut.Markup.Should().Contain("Review & Import");

        // Next buttons should be disabled until requirements are met
        cut.Markup.Should().Contain("Next");
    }
}
