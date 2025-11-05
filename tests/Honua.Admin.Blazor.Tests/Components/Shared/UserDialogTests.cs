// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Components.Shared;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Tests.Infrastructure;
using Bunit;
using MudBlazor;

namespace Honua.Admin.Blazor.Tests.Components.Shared;

/// <summary>
/// Tests for the UserDialog component.
/// Demonstrates testing Blazor components with bUnit, including:
/// - Component rendering
/// - Form validation
/// - User interactions
/// - Event callbacks
/// </summary>
public class UserDialogTests : ComponentTestBase
{
    [Fact]
    public void UserDialog_CreateMode_RendersAllFields()
    {
        // Arrange
        var model = new CreateUserRequest();

        // Act
        var cut = Context.RenderComponent<UserDialog>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false));

        // Assert
        cut.Find("input[placeholder='Enter username']").Should().NotBeNull();
        cut.Find("input[placeholder='Enter display name']").Should().NotBeNull();
        cut.Find("input[placeholder='Enter email address']").Should().NotBeNull();
        cut.Find("input[type='password']").Should().NotBeNull(); // Password field

        // Verify title shows "Create User"
        cut.FindAll("h6").Should().ContainSingle(h6 => h6.TextContent.Contains("Create User"));
    }

    [Fact]
    public void UserDialog_EditMode_HidesPasswordFields()
    {
        // Arrange
        var model = new UpdateUserRequest
        {
            DisplayName = "Test User",
            Email = "test@example.com",
            Roles = new List<string> { "administrator" }
        };

        // Act
        var cut = Context.RenderComponent<UserDialog>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, true)
            .Add(p => p.Username, "testuser"));

        // Assert
        // Password fields should not be rendered in edit mode
        cut.FindAll("input[type='password']").Should().BeEmpty();

        // Verify title shows "Edit User"
        cut.FindAll("h6").Should().ContainSingle(h6 => h6.TextContent.Contains("Edit User"));
    }

    [Fact]
    public void UserDialog_SubmitWithEmptyUsername_ShowsValidationError()
    {
        // Arrange
        var model = new CreateUserRequest();

        var cut = Context.RenderComponent<UserDialog>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false));

        // Act
        var saveButton = cut.FindAll("button").First(b => b.TextContent.Contains("Save"));
        saveButton.Click();

        // Assert
        // MudBlazor will show validation errors
        cut.Markup.Should().Contain("required"); // Validation message should appear
    }

    [Fact]
    public void UserDialog_PasswordMismatch_ShowsValidationError()
    {
        // Arrange
        var model = new CreateUserRequest
        {
            Username = "testuser",
            DisplayName = "Test User",
            Email = "test@example.com",
            Password = "Password123",
            ConfirmPassword = "DifferentPassword"
        };

        var cut = Context.RenderComponent<UserDialog>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false));

        // Act
        var saveButton = cut.FindAll("button").First(b => b.TextContent.Contains("Save"));
        saveButton.Click();

        // Assert
        cut.Markup.Should().Contain("match"); // Password match validation message
    }

    [Fact]
    public void UserDialog_CancelButton_InvokesOnCancelCallback()
    {
        // Arrange
        var onCancelInvoked = false;
        var model = new CreateUserRequest();

        var cut = Context.RenderComponent<UserDialog>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.OnCancel, () => { onCancelInvoked = true; }));

        // Act
        var cancelButton = cut.FindAll("button").First(b => b.TextContent.Contains("Cancel"));
        cancelButton.Click();

        // Assert
        onCancelInvoked.Should().BeTrue();
    }

    [Fact]
    public void UserDialog_ValidForm_InvokesOnSaveCallback()
    {
        // Arrange
        var onSaveInvoked = false;
        var model = new CreateUserRequest
        {
            Username = "testuser",
            DisplayName = "Test User",
            Email = "test@example.com",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Roles = new List<string> { "administrator" }
        };

        var cut = Context.RenderComponent<UserDialog>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false)
            .Add(p => p.OnSave, () => { onSaveInvoked = true; return Task.CompletedTask; }));

        // Act
        var saveButton = cut.FindAll("button").First(b => b.TextContent.Contains("Save"));
        saveButton.Click();

        // Assert
        onSaveInvoked.Should().BeTrue();
    }
}
