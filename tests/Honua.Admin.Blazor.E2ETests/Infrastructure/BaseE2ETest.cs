// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Admin.Blazor.E2ETests.Infrastructure;

/// <summary>
/// Base class for all E2E tests using Playwright.
/// Provides automatic browser lifecycle management and common test utilities.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public class BaseE2ETest : PageTest
{
    protected IPage Page { get; private set; } = null!;
    protected string BaseUrl { get; private set; } = null!;

    [SetUp]
    public async Task BaseSetUp()
    {
        // Get base URL from environment variable or use default
        BaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "https://localhost:5001";

        // Get current page from PageTest base class
        Page = base.Page;

        // Configure page timeouts
        Page.SetDefaultTimeout(30000); // 30 seconds default timeout
        Page.SetDefaultNavigationTimeout(30000);
    }

    /// <summary>
    /// Navigates to the application home page and waits for it to be fully loaded.
    /// </summary>
    protected async Task NavigateToHomeAsync()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for Blazor Server to establish SignalR connection
        // This prevents flaky tests due to component loading
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Performs login with the provided credentials.
    /// </summary>
    protected async Task LoginAsync(string username, string password)
    {
        await NavigateToHomeAsync();

        // Fill in login form
        await Page.GetByLabel("Username").FillAsync(username);
        await Page.GetByLabel("Password").FillAsync(password);

        // Click login button and wait for navigation
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

        // Wait for successful navigation to dashboard
        await Page.WaitForURLAsync($"{BaseUrl}/dashboard", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });
    }

    /// <summary>
    /// Takes a screenshot with the current test name for debugging failures.
    /// </summary>
    protected async Task TakeScreenshotAsync(string? suffix = null)
    {
        var testName = TestContext.CurrentContext.Test.Name;
        var fileName = suffix != null ? $"{testName}_{suffix}.png" : $"{testName}.png";
        var screenshotPath = Path.Combine("screenshots", fileName);

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
    }

    /// <summary>
    /// Waits for a MudBlazor snackbar notification to appear with the expected message.
    /// </summary>
    protected async Task WaitForSnackbarAsync(string expectedMessage)
    {
        var snackbar = Page.Locator(".mud-snackbar").Filter(new()
        {
            HasText = expectedMessage
        });
        await snackbar.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Waits for MudBlazor dialog to appear.
    /// </summary>
    protected async Task<ILocator> WaitForDialogAsync()
    {
        var dialog = Page.Locator(".mud-dialog");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        return dialog;
    }

    /// <summary>
    /// Closes any open MudBlazor dialogs.
    /// </summary>
    protected async Task CloseDialogAsync()
    {
        var closeButton = Page.Locator(".mud-dialog .mud-button-close");
        if (await closeButton.IsVisibleAsync())
        {
            await closeButton.ClickAsync();
        }
    }
}
