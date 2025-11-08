// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Admin.Blazor.E2ETests.Infrastructure;

/// <summary>
/// Centralized configuration for E2E tests.
/// </summary>
public static class TestConfiguration
{
    /// <summary>
    /// Base URL for the Blazor application.
    /// Can be overridden via E2E_BASE_URL environment variable.
    /// </summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "https://localhost:5001";

    /// <summary>
    /// Test admin username.
    /// Can be overridden via E2E_ADMIN_USERNAME environment variable.
    /// </summary>
    public static string AdminUsername =>
        Environment.GetEnvironmentVariable("E2E_ADMIN_USERNAME") ?? "admin";

    /// <summary>
    /// Test admin password.
    /// Can be overridden via E2E_ADMIN_PASSWORD environment variable.
    /// </summary>
    public static string AdminPassword =>
        Environment.GetEnvironmentVariable("E2E_ADMIN_PASSWORD") ?? "admin";

    /// <summary>
    /// Whether to run tests in headless mode.
    /// Can be overridden via E2E_HEADLESS environment variable.
    /// </summary>
    public static bool Headless =>
        Environment.GetEnvironmentVariable("E2E_HEADLESS")?.ToLowerInvariant() != "false";

    /// <summary>
    /// Whether to enable slow-mo for debugging (slows down operations by specified milliseconds).
    /// Can be overridden via E2E_SLOWMO environment variable.
    /// </summary>
    public static float SlowMo =>
        float.TryParse(Environment.GetEnvironmentVariable("E2E_SLOWMO"), out var slowMo)
            ? slowMo
            : 0;

    /// <summary>
    /// Default timeout for test operations in milliseconds.
    /// </summary>
    public const int DefaultTimeout = 30000;

    /// <summary>
    /// Timeout for navigation operations in milliseconds.
    /// </summary>
    public const int NavigationTimeout = 30000;

    /// <summary>
    /// Whether to record videos of test runs.
    /// Can be overridden via E2E_VIDEO environment variable.
    /// </summary>
    public static bool RecordVideo =>
        Environment.GetEnvironmentVariable("E2E_VIDEO")?.ToLowerInvariant() == "true";

    /// <summary>
    /// Whether to record traces for failed tests.
    /// Can be overridden via E2E_TRACE environment variable.
    /// </summary>
    public static bool RecordTrace =>
        Environment.GetEnvironmentVariable("E2E_TRACE")?.ToLowerInvariant() != "false";
}
