// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Bunit;
using FluentAssertions;
using Honua.Admin.Blazor.Components.Shared;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace Honua.Admin.Blazor.Tests.Components;

public class BulkOperationsDialogTests : BunitTestContext
{
    private readonly BulkOperationsApiClient _mockBulkApiClient;

    public BulkOperationsDialogTests()
    {
        _mockBulkApiClient = Substitute.For<BulkOperationsApiClient>(Substitute.For<HttpClient>());
        Services.AddSingleton(_mockBulkApiClient);
        Services.AddSingleton(Substitute.For<ISnackbar>());
    }

    [Fact]
    public void BulkOperationsDialog_DeleteOperation_RendersWarningAndForceOption()
    {
        // Arrange
        var selectedIds = new List<string> { "item1", "item2", "item3" };

        // Act
        var cut = RenderComponent<BulkOperationsDialog>(parameters => parameters
            .Add(p => p.OperationType, BulkOperationType.Delete)
            .Add(p => p.SelectedIds, selectedIds)
            .Add(p => p.ItemType, "services"));

        // Assert
        cut.Find("strong").TextContent.Should().Contain("3");
        cut.Markup.Should().Contain("services selected");
        cut.Markup.Should().Contain("cannot be undone");
        cut.Markup.Should().Contain("Force delete");
    }

    [Fact]
    public void BulkOperationsDialog_MoveToFolderOperation_RendersFolderSelection()
    {
        // Arrange
        var selectedIds = new List<string> { "item1", "item2" };
        var folders = new List<FolderListItem>
        {
            new FolderListItem { Id = "folder1", Path = "/maps" },
            new FolderListItem { Id = "folder2", Path = "/data" }
        };

        // Act
        var cut = RenderComponent<BulkOperationsDialog>(parameters => parameters
            .Add(p => p.OperationType, BulkOperationType.MoveToFolder)
            .Add(p => p.SelectedIds, selectedIds)
            .Add(p => p.ItemType, "layers")
            .Add(p => p.Folders, folders));

        // Assert
        cut.Markup.Should().Contain("Move selected");
        cut.Markup.Should().Contain("Target Folder");
        cut.Markup.Should().Contain("/maps");
        cut.Markup.Should().Contain("/data");
        cut.Markup.Should().Contain("Root (no folder)");
    }

    [Fact]
    public void BulkOperationsDialog_UpdateMetadataOperation_RendersMetadataFields()
    {
        // Arrange
        var selectedIds = new List<string> { "item1" };

        // Act
        var cut = RenderComponent<BulkOperationsDialog>(parameters => parameters
            .Add(p => p.OperationType, BulkOperationType.UpdateMetadata)
            .Add(p => p.SelectedIds, selectedIds)
            .Add(p => p.ItemType, "services"));

        // Assert
        cut.Markup.Should().Contain("Update metadata");
        cut.Markup.Should().Contain("Update Mode");
        cut.Markup.Should().Contain("Merge");
        cut.Markup.Should().Contain("Replace");
        cut.Markup.Should().Contain("Append");
        cut.Markup.Should().Contain("Tags");
        cut.Markup.Should().Contain("Keywords");
        cut.Markup.Should().Contain("comma-separated");
    }

    [Fact]
    public void BulkOperationsDialog_ApplyStyleOperation_RendersStyleSelection()
    {
        // Arrange
        var selectedIds = new List<string> { "layer1", "layer2" };
        var styles = new List<StyleListItem>
        {
            new StyleListItem { Id = "style1", Name = "Blue Style", RendererType = "simple" },
            new StyleListItem { Id = "style2", Name = "Categorized Style", RendererType = "uniqueValue" }
        };

        // Act
        var cut = RenderComponent<BulkOperationsDialog>(parameters => parameters
            .Add(p => p.OperationType, BulkOperationType.ApplyStyle)
            .Add(p => p.SelectedIds, selectedIds)
            .Add(p => p.ItemType, "layers")
            .Add(p => p.Styles, styles));

        // Assert
        cut.Markup.Should().Contain("Apply style to selected layers");
        cut.Markup.Should().Contain("Style");
        cut.Markup.Should().Contain("Blue Style");
        cut.Markup.Should().Contain("Categorized Style");
        cut.Markup.Should().Contain("Set as default");
    }

    [Fact]
    public void BulkOperationsDialog_EnableServicesOperation_RendersEnableConfirmation()
    {
        // Arrange
        var selectedIds = new List<string> { "service1", "service2" };

        // Act
        var cut = RenderComponent<BulkOperationsDialog>(parameters => parameters
            .Add(p => p.OperationType, BulkOperationType.EnableServices)
            .Add(p => p.SelectedIds, selectedIds)
            .Add(p => p.ItemType, "services"));

        // Assert
        cut.Markup.Should().Contain("Enable the selected services");
    }

    [Fact]
    public void BulkOperationsDialog_DisableServicesOperation_RendersDisableConfirmation()
    {
        // Arrange
        var selectedIds = new List<string> { "service1", "service2" };

        // Act
        var cut = RenderComponent<BulkOperationsDialog>(parameters => parameters
            .Add(p => p.OperationType, BulkOperationType.DisableServices)
            .Add(p => p.SelectedIds, selectedIds)
            .Add(p => p.ItemType, "services"));

        // Assert
        cut.Markup.Should().Contain("Disable the selected services");
    }

    [Fact]
    public void BulkOperationsDialog_SelectionCount_DisplaysCorrectly()
    {
        // Arrange
        var selectedIds = new List<string> { "item1", "item2", "item3", "item4", "item5" };

        // Act
        var cut = RenderComponent<BulkOperationsDialog>(parameters => parameters
            .Add(p => p.OperationType, BulkOperationType.Delete)
            .Add(p => p.SelectedIds, selectedIds)
            .Add(p => p.ItemType, "layers"));

        // Assert
        cut.Find("strong").TextContent.Should().Be("5");
        cut.Markup.Should().Contain("layers selected");
    }

    [Fact]
    public void BulkOperationsDialog_CancelButton_IsRendered()
    {
        // Arrange & Act
        var cut = RenderComponent<BulkOperationsDialog>(parameters => parameters
            .Add(p => p.OperationType, BulkOperationType.Delete)
            .Add(p => p.SelectedIds, new List<string> { "item1" })
            .Add(p => p.ItemType, "services"));

        // Assert
        var cancelButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Cancel"));
        cancelButton.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkOperationsDialog_DeleteOperation_ExecutesDeleteAsync()
    {
        // Arrange
        var selectedIds = new List<string> { "service1", "service2" };
        var expectedResponse = new BulkOperationResponse
        {
            OperationId = "op-123",
            TotalItems = 2,
            SuccessCount = 2,
            FailureCount = 0,
            Status = "completed"
        };

        _mockBulkApiClient.BulkDeleteServicesAsync(
            Arg.Is<List<string>>(ids => ids.Count == 2),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResponse));

        var onCompletedCalled = false;
        var completedResponse = (BulkOperationResponse?)null;

        // Act
        var cut = RenderComponent<BulkOperationsDialog>(parameters => parameters
            .Add(p => p.OperationType, BulkOperationType.Delete)
            .Add(p => p.SelectedIds, selectedIds)
            .Add(p => p.ItemType, "services")
            .Add(p => p.OnOperationCompleted, EventCallback.Factory.Create<BulkOperationResponse>(this, response =>
            {
                onCompletedCalled = true;
                completedResponse = response;
            })));

        var executeButton = cut.FindAll("button").First(b => b.TextContent.Contains("Delete"));

        // Execute operation
        await cut.InvokeAsync(() => executeButton.Click());

        // Assert
        await _mockBulkApiClient.Received(1).BulkDeleteServicesAsync(
            Arg.Is<List<string>>(ids => ids.SequenceEqual(selectedIds)),
            false,
            Arg.Any<CancellationToken>());

        // Note: The OnOperationCompleted callback assertion may need adjustments based on
        // how the dialog is implemented and closed
    }
}
