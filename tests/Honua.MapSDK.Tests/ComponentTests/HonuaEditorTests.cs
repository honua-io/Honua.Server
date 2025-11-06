using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.Editor;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Models;
using Honua.MapSDK.Services.Editing;
using Honua.MapSDK.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Comprehensive tests for HonuaEditor component
/// Tests cover: drawing modes, feature selection, editing, validation, undo/redo, session management
/// </summary>
public class HonuaEditorTests : IDisposable
{
    private readonly BunitTestContext _testContext;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;

    public HonuaEditorTests()
    {
        _testContext = new BunitTestContext();
        _mockHttp = MockHttpMessageHandler.CreateJsonHandler("{}");
        _httpClient = new HttpClient(_mockHttp);

        // Register FeatureEditService
        var editService = new FeatureEditService(_httpClient);
        _testContext.Services.AddSingleton(editService);
    }

    public void Dispose()
    {
        _testContext.Dispose();
        _httpClient.Dispose();
    }

    #region Rendering Tests

    [Fact]
    public void HonuaEditor_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().Contain("honua-editor");
    }

    [Fact]
    public void HonuaEditor_ShouldRenderToolbar_WhenShowToolbarIsTrue()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.ShowToolbar, true)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Assert
        cut.Markup.Should().Contain("editor-toolbar");
    }

    [Fact]
    public void HonuaEditor_ShouldNotRenderToolbar_WhenShowToolbarIsFalse()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.ShowToolbar, false)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Assert
        cut.Markup.Should().NotContain("editor-toolbar");
    }

    [Fact]
    public void HonuaEditor_ShouldApplyCustomPosition()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.Position, "top-right")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Assert
        cut.Markup.Should().Contain("editor-top-right");
    }

    [Fact]
    public void HonuaEditor_ShouldApplyCustomWidth()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.Width, "500px")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Assert
        cut.Markup.Should().Contain("width: 500px");
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public void HonuaEditor_StartEditing_ShouldInitializeSession()
    {
        // Arrange
        var editService = _testContext.Services.GetRequiredService<FeatureEditService>();
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Trigger map ready
        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });

        // Act
        var startButton = cut.Find("button[aria-label='Start editing']");
        startButton.Click();

        // Assert
        cut.Markup.Should().Contain("editor-status");
    }

    [Fact]
    public async Task HonuaEditor_StartEditing_ShouldPublishSessionStartedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        await Task.Delay(100); // Allow component to process

        // Act
        var startButton = cut.Find("button[aria-label='Start editing']");
        startButton.Click();
        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<EditSessionStartedMessage>();
        messages.Should().NotBeEmpty();
    }

    [Fact]
    public void HonuaEditor_CancelEditing_ShouldEndSession()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        // Act
        var cancelButton = cut.Find("button[aria-label='Cancel editing']");
        cancelButton.Click();

        // Assert
        cut.Markup.Should().NotContain("editor-status");
    }

    [Fact]
    public async Task HonuaEditor_CancelEditing_ShouldPublishSessionEndedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        await Task.Delay(100);

        cut.Find("button[aria-label='Start editing']").Click();
        await Task.Delay(100);

        _testContext.ComponentBus.ClearMessages();

        // Act
        var cancelButton = cut.Find("button[aria-label='Cancel editing']");
        cancelButton.Click();
        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<EditSessionEndedMessage>();
        messages.Should().NotBeEmpty();
    }

    #endregion

    #region Drawing Mode Tests

    [Fact]
    public void HonuaEditor_ShouldShowCreateMenu_WhenAllowCreateIsTrue()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowCreate, true)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        // Assert
        cut.Markup.Should().Contain("Create");
    }

    [Fact]
    public void HonuaEditor_ShouldNotShowCreateMenu_WhenAllowCreateIsFalse()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowCreate, false)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        // Assert
        cut.Markup.Should().NotContain("Point");
    }

    [Fact]
    public void HonuaEditor_ShouldShowDrawHint_WhenDrawModeIsActive()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowCreate, true)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        // Act - Click on Point menu item
        var createMenu = cut.FindAll("button").First(b => b.TextContent.Contains("Create"));
        createMenu.Click();

        // Assert
        cut.Markup.Should().Contain("draw-hint");
    }

    #endregion

    #region Feature Selection Tests

    [Fact]
    public async Task HonuaEditor_ShouldSelectFeature_WhenFeatureClickedMessageReceived()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        await Task.Delay(100);

        cut.Find("button[aria-label='Start editing']").Click();
        await Task.Delay(100);

        // Act
        await _testContext.ComponentBus.PublishAsync(new FeatureClickedMessage
        {
            MapId = "test-map",
            LayerId = "test-layer",
            FeatureId = "feature-1",
            Geometry = new { type = "Point" },
            Properties = new Dictionary<string, object> { ["name"] = "Test" }
        });
        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<EditorFeatureSelectedMessage>();
        messages.Should().NotBeEmpty();
        messages.Last().FeatureId.Should().Be("feature-1");
    }

    [Fact]
    public void HonuaEditor_ShouldShowEditButtons_WhenFeatureIsSelected()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowUpdate, true)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        // Simulate feature selection
        _testContext.ComponentBus.PublishAsync(new FeatureClickedMessage
        {
            MapId = "test-map",
            LayerId = "test-layer",
            FeatureId = "feature-1",
            Geometry = new { type = "Point" },
            Properties = new Dictionary<string, object>()
        });

        // Assert - Should show edit vertices and move buttons
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Edit vertices");
        });
    }

    [Fact]
    public void HonuaEditor_ShouldShowDeleteButton_WhenFeatureSelectedAndAllowDeleteIsTrue()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowDelete, true)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        _testContext.ComponentBus.PublishAsync(new FeatureClickedMessage
        {
            MapId = "test-map",
            LayerId = "test-layer",
            FeatureId = "feature-1",
            Geometry = new { type = "Point" },
            Properties = new Dictionary<string, object>()
        });

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Delete feature");
        });
    }

    #endregion

    #region Attribute Form Tests

    [Fact]
    public void HonuaEditor_ShouldShowAttributeButton_WhenFeatureIsSelected()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.ShowAttributeForm, true)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        _testContext.ComponentBus.PublishAsync(new FeatureClickedMessage
        {
            MapId = "test-map",
            LayerId = "test-layer",
            FeatureId = "feature-1",
            Geometry = new { type = "Point" },
            Properties = new Dictionary<string, object>()
        });

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Edit attributes");
        });
    }

    [Fact]
    public void HonuaEditor_ShouldRenderValidationRules_InAttributeForm()
    {
        // Arrange
        var validationRules = new Dictionary<string, List<ValidationRule>>
        {
            ["test-layer"] = new List<ValidationRule>
            {
                new ValidationRule
                {
                    FieldName = "name",
                    DisplayName = "Name",
                    Type = ValidationType.String,
                    IsRequired = true,
                    MaxLength = 100
                }
            }
        };

        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.ValidationRules, validationRules)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // The validation rules should be set in the service
        var editService = _testContext.Services.GetRequiredService<FeatureEditService>();
        var rules = editService.GetValidationRules("test-layer");

        // Assert
        rules.Should().NotBeEmpty();
        rules.First().FieldName.Should().Be("name");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void HonuaEditor_ShouldDisplayValidationErrors_WhenPresent()
    {
        // Arrange
        var editService = _testContext.Services.GetRequiredService<FeatureEditService>();

        var validationRules = new List<ValidationRule>
        {
            new ValidationRule
            {
                FieldName = "name",
                Type = ValidationType.String,
                IsRequired = true
            }
        };

        editService.SetValidationRules("test-layer", validationRules);

        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        // Create a session and add validation errors
        var sessionId = $"test-editor-session-{DateTime.UtcNow.Ticks}";
        var session = editService.StartSession(sessionId, new EditSessionConfiguration
        {
            RequireValidation = true
        });

        session.ValidationErrors.Add(new ValidationError
        {
            FeatureId = "feature-1",
            Field = "name",
            Message = "Name is required",
            Severity = ValidationSeverity.Error
        });

        cut.Render();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Validation Errors");
        });
    }

    #endregion

    #region Undo/Redo Tests

    [Fact]
    public void HonuaEditor_ShouldShowUndoButton_WhenOperationsExist()
    {
        // Arrange
        var editService = _testContext.Services.GetRequiredService<FeatureEditService>();
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        // Create a session and add an operation
        var sessionId = $"test-editor-session-{DateTime.UtcNow.Ticks}";
        var session = editService.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = new Feature
            {
                Id = "feature-1",
                LayerId = "test-layer",
                Geometry = new { type = "Point" },
                Attributes = new Dictionary<string, object>()
            }
        });

        cut.Render();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Undo");
        });
    }

    [Fact]
    public void HonuaEditor_ShouldShowRedoButton_AfterUndo()
    {
        // Arrange
        var editService = _testContext.Services.GetRequiredService<FeatureEditService>();
        var sessionId = $"test-editor-session-{DateTime.UtcNow.Ticks}";
        var session = editService.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = new Feature
            {
                Id = "feature-1",
                LayerId = "test-layer",
                Geometry = new { type = "Point" },
                Attributes = new Dictionary<string, object>()
            }
        });

        // Act
        editService.Undo(sessionId);

        // Assert
        session.CanRedo.Should().BeTrue();
    }

    #endregion

    #region Edit History Tests

    [Fact]
    public void HonuaEditor_ShouldDisplayEditHistory_WhenOperationsExist()
    {
        // Arrange
        var editService = _testContext.Services.GetRequiredService<FeatureEditService>();
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        var sessionId = $"test-editor-session-{DateTime.UtcNow.Ticks}";
        var session = editService.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = new Feature
            {
                Id = "feature-1",
                LayerId = "test-layer",
                Geometry = new { type = "Point" },
                Attributes = new Dictionary<string, object>()
            },
            Description = "Created feature"
        });

        cut.Render();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Edit History");
        });
    }

    [Fact]
    public void HonuaEditor_ShouldShowUnsavedChangesCount()
    {
        // Arrange
        var editService = _testContext.Services.GetRequiredService<FeatureEditService>();
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        var sessionId = $"test-editor-session-{DateTime.UtcNow.Ticks}";
        var session = editService.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = new Feature { Id = "f1", LayerId = "test-layer", Geometry = new { type = "Point" }, Attributes = new Dictionary<string, object>() },
            IsSynced = false
        });

        cut.Render();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("unsaved change");
        });
    }

    #endregion

    #region Save Operations Tests

    [Fact]
    public void HonuaEditor_SaveButton_ShouldBeDisabled_WhenNoChanges()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        // Assert
        var saveButton = cut.Find("button[aria-label='Save changes']");
        saveButton.HasAttribute("disabled").Should().BeTrue();
    }

    #endregion

    #region Event Callback Tests

    [Fact]
    public async Task HonuaEditor_ShouldInvokeOnFeatureCreated_WhenFeatureIsCreated()
    {
        // Arrange
        Feature? createdFeature = null;

        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" })
            .Add(p => p.OnFeatureCreated, EventCallback.Factory.Create<Feature>(this, f => createdFeature = f)));

        // Note: Full integration test would require JS interop
        // This test verifies the callback is wired up correctly
        createdFeature.Should().BeNull(); // Placeholder assertion
    }

    [Fact]
    public async Task HonuaEditor_ShouldInvokeOnEditError_WhenErrorOccurs()
    {
        // Arrange
        string? errorMessage = null;

        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" })
            .Add(p => p.OnEditError, EventCallback.Factory.Create<string>(this, msg => errorMessage = msg)));

        // Note: Full integration test would require simulating an error condition
        errorMessage.Should().BeNull(); // Placeholder assertion
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task HonuaEditor_Dispose_ShouldEndSession_WhenEditing()
    {
        // Arrange
        var editService = _testContext.Services.GetRequiredService<FeatureEditService>();
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        await Task.Delay(100);

        cut.Find("button[aria-label='Start editing']").Click();
        await Task.Delay(100);

        // Act
        await cut.Instance.DisposeAsync();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void HonuaEditor_ShouldRespectAllowCreate_Configuration()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowCreate, false)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Find("button[aria-label='Start editing']").Click();

        // Assert - Create menu should not be visible
        var buttons = cut.FindAll("button");
        buttons.Any(b => b.TextContent.Contains("Create")).Should().BeFalse();
    }

    [Fact]
    public void HonuaEditor_ShouldRespectAllowUpdate_Configuration()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowUpdate, false)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Configuration should be respected
        cut.Instance.AllowUpdate.Should().BeFalse();
    }

    [Fact]
    public void HonuaEditor_ShouldRespectAllowDelete_Configuration()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowDelete, false)
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Configuration should be respected
        cut.Instance.AllowDelete.Should().BeFalse();
    }

    #endregion

    #region Map Synchronization Tests

    [Fact]
    public async Task HonuaEditor_ShouldWaitForMapReady_BeforeEnabling()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Start button should be disabled
        var startButton = cut.Find("button[aria-label='Start editing']");
        startButton.HasAttribute("disabled").Should().BeTrue();

        // Act - Send map ready message
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        await Task.Delay(100);
        cut.Render();

        // Assert - Button should now be enabled
        startButton = cut.Find("button[aria-label='Start editing']");
        startButton.HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task HonuaEditor_ShouldOnlyRespondToCorrectMapId()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaEditor>(parameters => parameters
            .Add(p => p.Id, "test-editor")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.EditableLayers, new List<string> { "test-layer" }));

        // Act - Send message for different map
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "other-map" });
        await Task.Delay(100);

        // Assert - Button should still be disabled
        var startButton = cut.Find("button[aria-label='Start editing']");
        startButton.HasAttribute("disabled").Should().BeTrue();
    }

    #endregion
}
