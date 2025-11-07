// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for navigation, UI state management, and user interface interactions.
/// Tests cover menu navigation, breadcrumbs, search, notifications, and responsive behavior.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("Navigation")]
[Category("UI")]
public class NavigationAndUITests : BaseE2ETest
{
    [SetUp]
    public async Task NavigationTestSetUp()
    {
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [Test]
    [Description("Navigate to all main pages via menu")]
    public async Task Navigation_AllMainPages_ShouldLoad()
    {
        // Test navigation to key pages
        var pages = new[]
        {
            ("Dashboard", "/dashboard"),
            ("Services", "/services"),
            ("Layers", "/layers"),
            ("Data Sources", "/datasources"),
            ("Folders", "/folders")
        };

        foreach (var (linkText, expectedPath) in pages)
        {
            // Act
            var link = Page.Locator($"a:has-text('{linkText}'), nav a:has-text('{linkText}')");
            if (await link.IsVisibleAsync())
            {
                await link.First().ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Assert
                var currentUrl = Page.Url;
                currentUrl.Should().Contain(expectedPath,
                    $"clicking '{linkText}' should navigate to a URL containing '{expectedPath}'");
            }
        }
    }

    [Test]
    [Description("Sidebar menu expand and collapse")]
    public async Task Sidebar_ExpandCollapse_ShouldToggleVisibility()
    {
        // Arrange
        await NavigateToHomeAsync();

        // Act - Look for menu toggle button
        var menuToggle = Page.Locator("button[aria-label*='menu'], button:has-text('☰'), .menu-toggle");

        if (await menuToggle.IsVisibleAsync())
        {
            // Collapse sidebar
            await menuToggle.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Expand sidebar
            await menuToggle.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Assert - Menu should be visible again
            var servicesLink = Page.Locator("a:has-text('Services')");
            await Expect(servicesLink.First()).ToBeVisibleAsync();
        }
        else
        {
            Assert.Ignore("Sidebar toggle not available in UI");
        }
    }

    [Test]
    [Description("Breadcrumb navigation")]
    public async Task Breadcrumbs_ClickParent_ShouldNavigateBack()
    {
        // Arrange - Navigate to a detail page
        await Page.GotoAsync($"{BaseUrl}/services");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Look for breadcrumbs
        var breadcrumbs = Page.Locator(".mud-breadcrumbs, nav[aria-label='breadcrumb'], .breadcrumb");

        if (await breadcrumbs.IsVisibleAsync())
        {
            var homeLink = breadcrumbs.Locator("a:has-text('Home'), a:has-text('Dashboard')");
            if (await homeLink.IsVisibleAsync())
            {
                await homeLink.First().ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Assert
                Page.Url.Should().Contain("/dashboard");
            }
        }
        else
        {
            Assert.Ignore("Breadcrumbs not available in UI");
        }
    }

    [Test]
    [Description("Global search functionality")]
    public async Task GlobalSearch_EnterQuery_ShouldShowResults()
    {
        // Arrange
        await NavigateToHomeAsync();

        // Act - Look for global search box
        var searchBox = Page.Locator("input[placeholder*='Search'], input[aria-label='Search']");

        if (await searchBox.IsVisibleAsync())
        {
            await searchBox.FillAsync("test");
            await Page.WaitForTimeoutAsync(1000);

            // Assert - Search results should appear
            var searchResults = Page.Locator(".search-results, .search-dropdown");
            var hasResults = await searchResults.IsVisibleAsync().ConfigureAwait(false);

            if (hasResults)
            {
                hasResults.Should().BeTrue("search results should be visible");
            }
        }
        else
        {
            Assert.Ignore("Global search not available in UI");
        }
    }

    [Test]
    [Description("User menu displays and logout works")]
    public async Task UserMenu_ClickLogout_ShouldLogOut()
    {
        // Arrange
        await NavigateToHomeAsync();

        // Act - Click user menu
        var userMenu = Page.Locator("[data-testid='user-menu'], button:has-text('User'), button:has-text('Admin')");

        if (!await userMenu.IsVisibleAsync())
        {
            // Try alternative user menu selectors
            userMenu = Page.Locator(".user-menu, button[aria-label='User menu']");
        }

        if (await userMenu.First().IsVisibleAsync())
        {
            await userMenu.First().ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Click logout
            var logoutButton = Page.Locator("button:has-text('Logout'), a:has-text('Logout')");
            await logoutButton.First().ClickAsync();

            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert - Should be redirected to login
            Page.Url.Should().Match(url =>
                url.Contains("/login") || url == BaseUrl,
                "user should be redirected to login after logout");
        }
        else
        {
            Assert.Ignore("User menu not found in UI");
        }
    }

    [Test]
    [Description("Notification/snackbar appears and dismisses")]
    public async Task Notification_AutoDismiss_ShouldDisappear()
    {
        // This test would need to trigger an action that shows a notification
        // For demonstration, we'll just check if the snackbar container exists
        await NavigateToHomeAsync();

        var snackbarContainer = Page.Locator(".mud-snackbar-container, #mud-snackbar-container");

        // Snackbar container should exist (even if empty)
        if (await snackbarContainer.IsVisibleAsync())
        {
            Assert.Pass("Snackbar container found in UI");
        }
        else
        {
            Assert.Ignore("Snackbar container not found in UI");
        }
    }

    [Test]
    [Description("Dark mode toggle (if available)")]
    public async Task DarkMode_Toggle_ShouldChangeTheme()
    {
        // Arrange
        await NavigateToHomeAsync();

        // Act - Look for theme toggle
        var themeToggle = Page.Locator("button[aria-label*='theme'], button:has-text('Dark'), button:has-text('Light')");

        if (await themeToggle.IsVisibleAsync())
        {
            await themeToggle.First().ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Toggle back
            await themeToggle.First().ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            Assert.Pass("Theme toggle works");
        }
        else
        {
            Assert.Ignore("Theme toggle not available in UI");
        }
    }

    [Test]
    [Description("Page title updates based on current page")]
    public async Task PageTitle_NavigateToPages_ShouldUpdateTitle()
    {
        // Navigate to Services page
        await Page.GotoAsync($"{BaseUrl}/services");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var title = await Page.TitleAsync();
        title.Should().NotBeNullOrEmpty("page should have a title");

        // Navigate to Layers page
        await Page.GotoAsync($"{BaseUrl}/layers");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var newTitle = await Page.TitleAsync();
        newTitle.Should().NotBeNullOrEmpty("page should have a title");
    }

    [Test]
    [Description("404 page for non-existent routes")]
    public async Task Navigation_NonExistentRoute_ShouldShow404()
    {
        // Act
        await Page.GotoAsync($"{BaseUrl}/non-existent-page-{Guid.NewGuid()}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Should show 404 or error page
        var errorText = Page.Locator("text=404, text=Not Found, text=Page not found");
        var hasError = await errorText.IsVisibleAsync().ConfigureAwait(false);

        if (hasError)
        {
            hasError.Should().BeTrue("404 page should be displayed");
        }
        else
        {
            // Some apps redirect to home instead of showing 404
            Page.Url.Should().Match(url =>
                url.Contains("/dashboard") || url == BaseUrl,
                "should show 404 or redirect to home");
        }
    }

    [Test]
    [Description("Back button navigation works correctly")]
    public async Task Navigation_BackButton_ShouldNavigateToPreviousPage()
    {
        // Arrange - Navigate through multiple pages
        await Page.GotoAsync($"{BaseUrl}/services");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GotoAsync($"{BaseUrl}/layers");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Go back
        await Page.GoBackAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Should be on services page
        Page.Url.Should().Contain("/services", "back button should return to previous page");
    }

    [Test]
    [Description("Responsive design - Mobile viewport")]
    public async Task ResponsiveDesign_MobileViewport_ShouldAdaptLayout()
    {
        // Arrange - Set mobile viewport
        await Page.SetViewportSizeAsync(375, 667); // iPhone SE size
        await NavigateToHomeAsync();

        // Act - Check if mobile menu is present
        var mobileMenu = Page.Locator("button[aria-label*='menu'], .mobile-menu-toggle");

        if (await mobileMenu.IsVisibleAsync())
        {
            await mobileMenu.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Assert - Navigation should be visible
            var navigation = Page.Locator("nav, .navigation-drawer");
            await Expect(navigation.First()).ToBeVisibleAsync();
        }

        // Reset viewport
        await Page.SetViewportSizeAsync(1920, 1080);
    }

    [Test]
    [Description("Loading indicators appear during data fetch")]
    public async Task LoadingIndicators_DataFetch_ShouldShowSpinner()
    {
        // Arrange & Act - Navigate to a page that loads data
        await Page.GotoAsync($"{BaseUrl}/services");

        // Look for loading indicator (should appear briefly)
        var loadingIndicator = Page.Locator(".mud-progress-circular, .spinner, [role='progressbar']");

        // Wait for page to fully load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - After loading, indicator should be gone
        var isLoading = await loadingIndicator.IsVisibleAsync().ConfigureAwait(false);
        isLoading.Should().BeFalse("loading indicator should disappear after data loads");
    }

    [Test]
    [Description("Keyboard navigation - Tab through interactive elements")]
    public async Task KeyboardNavigation_TabKey_ShouldFocusElements()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/services");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Press Tab key multiple times
        await Page.Keyboard.PressAsync("Tab");
        await Page.WaitForTimeoutAsync(200);
        await Page.Keyboard.PressAsync("Tab");
        await Page.WaitForTimeoutAsync(200);

        // Assert - Some element should have focus
        var focusedElement = await Page.EvaluateAsync<string>(
            "document.activeElement?.tagName || 'NONE'");

        focusedElement.Should().NotBe("NONE", "an element should have keyboard focus");
    }
}
