# Honua Admin Blazor E2E Tests

End-to-end (E2E) integration tests for the Honua Admin Blazor application using Playwright for .NET.

## Overview

This test project provides comprehensive E2E testing for the Blazor Admin UI, including:

- **Authentication flows** - Login, logout, session management
- **Service CRUD operations** - Create, read, update, delete services
- **Complete user workflows** - Real browser testing of complete user journeys
- **Headless execution** - Runs in CI/CD pipelines without display

## Technology Stack

- **Playwright for .NET** (v1.48.0) - Modern browser automation framework
- **NUnit** - Test framework
- **FluentAssertions** - Readable assertions
- **Chromium/Firefox/WebKit** - Cross-browser support

## Prerequisites

1. **.NET 9.0 SDK** or later
2. **Playwright browsers** (installed via command below)
3. **Running Honua Admin Blazor application** (for tests to connect to)

## Initial Setup

### 1. Install Playwright Browsers

After building the project for the first time, install the required browsers:

```bash
# Navigate to the test project directory
cd tests/Honua.Admin.Blazor.E2ETests

# Build the project
dotnet build

# Install Playwright browsers (Chromium, Firefox, WebKit)
pwsh bin/Debug/net9.0/playwright.ps1 install

# Or on Linux/macOS
./bin/Debug/net9.0/playwright.sh install
```

This downloads the browser binaries needed for testing. You only need to do this once (or when updating Playwright).

### 2. Start the Blazor Application

The E2E tests require the Blazor application to be running:

```bash
# From repository root
cd src/Honua.Admin.Blazor
dotnet run
```

The app should be running at `https://localhost:5001` (or configured URL).

## Running Tests

### Run All E2E Tests

```bash
cd tests/Honua.Admin.Blazor.E2ETests
dotnet test
```

### Run Specific Test Categories

```bash
# Run only authentication tests
dotnet test --filter "Category=Authentication"

# Run only service management tests
dotnet test --filter "Category=ServiceManagement"
```

### Run with Custom Configuration

```bash
# Run in headed mode (see the browser)
E2E_HEADLESS=false dotnet test

# Run against different URL
E2E_BASE_URL=https://staging.honua.io dotnet test

# Run with slow-mo for debugging (delays each operation)
E2E_SLOWMO=500 dotnet test

# Record videos of all tests
E2E_VIDEO=true dotnet test
```

### Run in Visual Studio

1. Open the solution in Visual Studio
2. Open **Test Explorer** (Test > Test Explorer)
3. Right-click on `Honua.Admin.Blazor.E2ETests`
4. Select **Run** or **Debug**

## Configuration

### Environment Variables

Configure test behavior via environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `E2E_BASE_URL` | `https://localhost:5001` | Base URL of the Blazor app |
| `E2E_ADMIN_USERNAME` | `admin` | Test admin username |
| `E2E_ADMIN_PASSWORD` | `admin` | Test admin password |
| `E2E_HEADLESS` | `true` | Run in headless mode |
| `E2E_SLOWMO` | `0` | Slow down operations (ms) for debugging |
| `E2E_VIDEO` | `false` | Record videos of test runs |
| `E2E_TRACE` | `true` | Record traces for failed tests |

### Example: Linux/macOS

```bash
export E2E_BASE_URL=https://dev.honua.io
export E2E_HEADLESS=false
dotnet test
```

### Example: Windows PowerShell

```powershell
$env:E2E_BASE_URL="https://dev.honua.io"
$env:E2E_HEADLESS="false"
dotnet test
```

## Test Structure

```
Honua.Admin.Blazor.E2ETests/
├── Infrastructure/
│   ├── BaseE2ETest.cs           # Base class for all E2E tests
│   └── TestConfiguration.cs     # Centralized test configuration
├── Tests/
│   ├── AuthenticationTests.cs   # Login/logout tests
│   └── ServiceCrudTests.cs      # Service management tests
├── playwright.runsettings        # Playwright configuration
└── README.md                     # This file
```

## Writing New Tests

### Create a New Test Class

```csharp
using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

[TestFixture]
[Category("E2E")]
[Category("YourFeature")]
public class YourFeatureTests : BaseE2ETest
{
    [SetUp]
    public async Task TestSetUp()
    {
        // Login before each test
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [Test]
    [Description("Test description")]
    public async Task YourTest_Scenario_ExpectedOutcome()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/your-page");

        // Act
        await Page.GetByRole(AriaRole.Button, new() { Name = "Click Me" }).ClickAsync();

        // Assert
        await Expect(Page.Locator("text=Success")).ToBeVisibleAsync();
    }
}
```

### Best Practices

1. **Use BaseE2ETest** - Inherit from `BaseE2ETest` for automatic setup
2. **Descriptive test names** - `MethodName_Scenario_ExpectedResult`
3. **Arrange-Act-Assert** - Clear test structure
4. **Auto-waiting** - Playwright waits automatically, avoid manual `Thread.Sleep`
5. **Network idle** - Wait for `LoadState.NetworkIdle` after navigation for Blazor Server
6. **Cleanup** - Use `[TearDown]` to clean up test data
7. **Screenshots** - Use `TakeScreenshotAsync()` helper for debugging
8. **Categories** - Tag tests with `[Category]` for selective execution

### Locator Strategies

Playwright provides multiple ways to locate elements:

```csharp
// By role (preferred - accessible)
await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

// By label (form fields)
await Page.GetByLabel("Username").FillAsync("admin");

// By text
await Page.GetByText("Dashboard").ClickAsync();

// By test ID (add data-testid attributes in your Blazor components)
await Page.Locator("[data-testid='user-menu']").ClickAsync();

// By CSS selector
await Page.Locator(".mud-dialog button.save").ClickAsync();
```

**Recommendation**: Prefer role-based and label-based locators (more resilient to UI changes).

## Headless Testing

By default, tests run in **headless mode**, meaning no browser window is visible. This is ideal for:

- **CI/CD pipelines** - Automated testing without display
- **Faster execution** - No GUI overhead
- **Parallel execution** - Run multiple tests simultaneously

### Debugging in Headed Mode

When debugging test failures, run in **headed mode** to see what's happening:

```bash
E2E_HEADLESS=false dotnet test
```

Or set it in your IDE's test configuration.

## CI/CD Integration

### GitHub Actions Example

```yaml
name: E2E Tests

on: [push, pull_request]

jobs:
  e2e-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Start Blazor App
        run: |
          cd src/Honua.Admin.Blazor
          dotnet run &
          sleep 10  # Wait for app to start

      - name: Install Playwright Browsers
        run: |
          cd tests/Honua.Admin.Blazor.E2ETests
          dotnet build
          pwsh bin/Debug/net9.0/playwright.ps1 install --with-deps

      - name: Run E2E Tests
        run: dotnet test tests/Honua.Admin.Blazor.E2ETests --logger "trx"
        env:
          E2E_BASE_URL: https://localhost:5001
          E2E_HEADLESS: true

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: test-results/
```

## Troubleshooting

### Tests Fail with "Browser not installed"

Run the Playwright browser installation:

```bash
cd tests/Honua.Admin.Blazor.E2ETests
pwsh bin/Debug/net9.0/playwright.ps1 install
```

### Tests Timeout

- Ensure the Blazor app is running
- Increase timeout: `Page.SetDefaultTimeout(60000)` (60 seconds)
- Check network connectivity to the app
- For Blazor Server, ensure SignalR connection is established (use `NetworkIdle` wait state)

### Flaky Tests

- Use `WaitForLoadStateAsync(LoadState.NetworkIdle)` for Blazor Server
- Avoid hardcoded waits (`Thread.Sleep`) - use Playwright's auto-waiting
- Use specific locators instead of broad selectors
- Ensure test data cleanup in `[TearDown]`

### HTTPS Certificate Errors

The test configuration ignores HTTPS errors for localhost testing. If needed, adjust in `BaseE2ETest.cs`:

```csharp
await Page.Context.Browser!.NewContextAsync(new()
{
    IgnoreHTTPSErrors = true
});
```

## Test Results and Artifacts

After running tests, the following artifacts are generated:

- **Test Results**: `TestResults/` (TRX files)
- **Screenshots**: `screenshots/` (on failure or manual capture)
- **Videos**: `test-results/videos/` (if `E2E_VIDEO=true`)
- **Traces**: `test-results/traces/` (on failure, open with Playwright Trace Viewer)

### Viewing Traces

Playwright traces are powerful debugging tools:

```bash
# Install Playwright CLI
dotnet tool install --global Microsoft.Playwright.CLI

# View trace
playwright show-trace test-results/traces/trace.zip
```

## Performance Considerations

- **Parallel execution**: NUnit runs tests in parallel by default
- **Browser reuse**: Each test class gets a new browser instance
- **Resource cleanup**: Always clean up test data to prevent conflicts
- **Network idle**: Blazor Server needs SignalR to settle - use `NetworkIdle`

## Additional Resources

- [Playwright for .NET Documentation](https://playwright.dev/dotnet/)
- [NUnit Documentation](https://docs.nunit.org/)
- [Blazor Testing Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/test)
- [FluentAssertions Documentation](https://fluentassertions.com/)

## Support

For issues or questions:
- Review test output and screenshots
- Check application logs
- Enable headed mode and slow-mo for debugging
- Review Playwright traces for failed tests
