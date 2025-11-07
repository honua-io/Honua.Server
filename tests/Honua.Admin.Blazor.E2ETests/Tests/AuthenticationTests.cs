// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for authentication flows in the Honua Admin Blazor application.
/// These tests verify login, logout, and authentication state management.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("Authentication")]
public class AuthenticationTests : BaseE2ETest
{
    [Test]
    [Description("Verify that users can successfully log in with valid credentials")]
    public async Task Login_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        await NavigateToHomeAsync();

        // Act
        await Page.GetByLabel("Username").FillAsync(TestConfiguration.AdminUsername);
        await Page.GetByLabel("Password").FillAsync(TestConfiguration.AdminPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

        // Wait for navigation to complete
        await Page.WaitForURLAsync($"{BaseUrl}/dashboard", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Assert
        var currentUrl = Page.Url;
        currentUrl.Should().Contain("/dashboard", "user should be redirected to dashboard after successful login");

        // Verify user is authenticated by checking for user menu or logout button
        var userMenu = Page.Locator("[data-testid='user-menu'], .user-menu, button:has-text('Logout')");
        await Expect(userMenu.First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that login fails with invalid credentials")]
    public async Task Login_WithInvalidCredentials_ShouldFail()
    {
        // Arrange
        await NavigateToHomeAsync();

        // Act
        await Page.GetByLabel("Username").FillAsync("invalid-user");
        await Page.GetByLabel("Password").FillAsync("wrong-password");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

        // Wait for error message
        await Page.WaitForTimeoutAsync(2000); // Give time for error to appear

        // Assert - should stay on login page
        var currentUrl = Page.Url;
        currentUrl.Should().NotContain("/dashboard", "user should not be redirected on failed login");

        // Verify error message is displayed
        var errorMessage = Page.Locator(".mud-alert-error, .error-message, text=Invalid username or password");
        var isErrorVisible = await errorMessage.First().IsVisibleAsync().ConfigureAwait(false);
        isErrorVisible.Should().BeTrue("error message should be displayed");
    }

    [Test]
    [Description("Verify that users can successfully log out")]
    public async Task Logout_WhenAuthenticated_ShouldRedirectToLogin()
    {
        // Arrange - login first
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);

        // Act - find and click logout button
        var logoutButton = Page.Locator("button:has-text('Logout'), [data-testid='logout-button']");
        await logoutButton.First().ClickAsync();

        // Wait for navigation
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - should be redirected to login page
        var currentUrl = Page.Url;
        currentUrl.Should().Contain("/login", "user should be redirected to login page after logout")
            .Or.Subject.Should().Be(BaseUrl, "user should be redirected to home page after logout");
    }

    [Test]
    [Description("Verify that unauthenticated users cannot access protected pages")]
    public async Task ProtectedPage_WithoutAuthentication_ShouldRedirectToLogin()
    {
        // Arrange & Act - try to navigate directly to dashboard without logging in
        await Page.GotoAsync($"{BaseUrl}/dashboard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - should be redirected to login
        var currentUrl = Page.Url;
        (currentUrl.Contains("/login") || currentUrl == BaseUrl)
            .Should().BeTrue("unauthenticated users should be redirected to login page");
    }

    [Test]
    [Description("Verify that login form has proper validation")]
    public async Task Login_WithEmptyFields_ShouldShowValidationErrors()
    {
        // Arrange
        await NavigateToHomeAsync();

        // Act - try to submit empty form
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

        // Assert - check for validation messages
        var validationErrors = Page.Locator(".mud-input-error, .validation-message, .field-validation-error");
        var hasErrors = await validationErrors.First().IsVisibleAsync().ConfigureAwait(false);

        // If using client-side validation, errors should appear
        // If using server-side only, button might be disabled
        var loginButton = Page.GetByRole(AriaRole.Button, new() { Name = "Login" });
        var isButtonDisabled = await loginButton.IsDisabledAsync().ConfigureAwait(false);

        (hasErrors || isButtonDisabled).Should().BeTrue(
            "either validation errors should be shown or login button should be disabled");
    }

    [Test]
    [Description("Verify that session persists across page reloads")]
    public async Task AuthenticatedSession_AfterPageReload_ShouldPersist()
    {
        // Arrange - login
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);

        // Act - reload the page
        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Assert - should still be on dashboard, not redirected to login
        var currentUrl = Page.Url;
        currentUrl.Should().Contain("/dashboard", "authenticated session should persist after page reload");
    }
}
