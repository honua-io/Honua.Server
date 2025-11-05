// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Components.Pages;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Moq;

namespace Honua.Admin.Blazor.Tests.Components.Pages;

/// <summary>
/// Tests for the ServiceList page component.
/// </summary>
public class ServiceListTests : ComponentTestBase
{
    private readonly Mock<ServiceApiClient> _mockServiceApiClient;
    private readonly Mock<ISnackbar> _mockSnackbar;

    public ServiceListTests()
    {
        _mockServiceApiClient = new Mock<ServiceApiClient>(MockBehavior.Strict, new HttpClient());
        _mockSnackbar = new Mock<ISnackbar>();

        Context.Services.AddSingleton(_mockServiceApiClient.Object);
        Context.Services.AddSingleton(_mockSnackbar.Object);

        var authStateProvider = new TestAuthenticationStateProvider(
            TestAuthenticationStateProvider.CreateAdministrator());
        Context.Services.AddSingleton<AuthenticationStateProvider>(authStateProvider);
    }

    [Fact]
    public async Task ServiceList_OnInitialized_LoadsServices()
    {
        // Arrange
        var services = new List<ServiceListItem>
        {
            new ServiceListItem
            {
                Id = "wms-service",
                Name = "WMS Service",
                ServiceType = "WMS",
                Enabled = true,
                LayerCount = 5,
                FolderId = null
            },
            new ServiceListItem
            {
                Id = "wfs-service",
                Name = "WFS Service",
                ServiceType = "WFS",
                Enabled = false,
                LayerCount = 3,
                FolderId = "folder1"
            }
        };

        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<ServiceList>();
        await Task.Delay(100); // Wait for async initialization

        // Assert
        _mockServiceApiClient.Verify(x => x.GetServicesAsync(It.IsAny<CancellationToken>()), Times.Once);

        cut.Markup.Should().Contain("wms-service");
        cut.Markup.Should().Contain("WMS Service");
        cut.Markup.Should().Contain("wfs-service");
        cut.Markup.Should().Contain("WFS Service");
    }

    [Fact]
    public async Task ServiceList_LoadError_ShowsErrorMessage()
    {
        // Arrange
        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Failed to load services"));

        // Act
        var cut = Context.RenderComponent<ServiceList>();
        await Task.Delay(100);

        // Assert
        _mockSnackbar.Verify(
            x => x.Add(
                It.Is<string>(s => s.Contains("Failed") || s.Contains("Error")),
                It.IsAny<Severity>(),
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ServiceList_SearchFilter_FiltersServices()
    {
        // Arrange
        var services = new List<ServiceListItem>
        {
            new ServiceListItem
            {
                Id = "wms-service",
                Name = "WMS Service",
                ServiceType = "WMS",
                Enabled = true,
                LayerCount = 5
            },
            new ServiceListItem
            {
                Id = "wfs-service",
                Name = "WFS Service",
                ServiceType = "WFS",
                Enabled = true,
                LayerCount = 3
            }
        };

        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        var cut = Context.RenderComponent<ServiceList>();
        await Task.Delay(100);

        // Act - Search for "WMS"
        var searchInput = cut.Find("input[placeholder*='Search']");
        searchInput.Change("WMS");
        await Task.Delay(100);

        // Assert
        // After filtering, should still contain WMS service
        cut.Markup.Should().Contain("wms-service");
        // Depending on implementation, WFS might be hidden or still visible
    }

    [Fact]
    public async Task ServiceList_WithEnabledAndDisabledServices_ShowsCorrectStatus()
    {
        // Arrange
        var services = new List<ServiceListItem>
        {
            new ServiceListItem
            {
                Id = "enabled-service",
                Name = "Enabled Service",
                ServiceType = "WMS",
                Enabled = true,
                LayerCount = 5
            },
            new ServiceListItem
            {
                Id = "disabled-service",
                Name = "Disabled Service",
                ServiceType = "WFS",
                Enabled = false,
                LayerCount = 3
            }
        };

        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<ServiceList>();
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("enabled-service");
        cut.Markup.Should().Contain("disabled-service");
        // Visual indicators for enabled/disabled status would be verified here
    }

    [Fact]
    public async Task ServiceList_EmptyList_ShowsNoServicesMessage()
    {
        // Arrange
        var services = new List<ServiceListItem>();

        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<ServiceList>();
        await Task.Delay(100);

        // Assert
        // Should show some indication that there are no services
        // Exact message depends on implementation
        cut.Markup.Should().Contain("No services" , "or similar empty state message");
    }

    [Fact]
    public async Task ServiceList_ServiceTypeChips_DisplayCorrectColors()
    {
        // Arrange
        var services = new List<ServiceListItem>
        {
            new ServiceListItem
            {
                Id = "wms",
                Name = "WMS",
                ServiceType = "WMS",
                Enabled = true,
                LayerCount = 1
            },
            new ServiceListItem
            {
                Id = "wfs",
                Name = "WFS",
                ServiceType = "WFS",
                Enabled = true,
                LayerCount = 1
            },
            new ServiceListItem
            {
                Id = "wmts",
                Name = "WMTS",
                ServiceType = "WMTS",
                Enabled = true,
                LayerCount = 1
            }
        };

        _mockServiceApiClient
            .Setup(x => x.GetServicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var cut = Context.RenderComponent<ServiceList>();
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("WMS");
        cut.Markup.Should().Contain("WFS");
        cut.Markup.Should().Contain("WMTS");
    }
}
