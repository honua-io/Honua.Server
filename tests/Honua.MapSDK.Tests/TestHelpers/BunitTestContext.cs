using Bunit;
using Honua.MapSDK.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Tests.TestHelpers;

/// <summary>
/// Base test context for bUnit tests with common setup
/// </summary>
public class BunitTestContext : TestContext
{
    public TestComponentBus ComponentBus { get; }

    public BunitTestContext()
    {
        // Setup logging
        Services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Setup ComponentBus
        ComponentBus = new TestComponentBus();
        Services.AddSingleton<ComponentBus>(ComponentBus);

        // Setup JSInterop for Blazor components
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Add MudBlazor services if needed
        Services.AddMudServices();
    }

    /// <summary>
    /// Create a new test context with isolated services
    /// </summary>
    public static BunitTestContext Create()
    {
        return new BunitTestContext();
    }
}

/// <summary>
/// Extension methods for TestContext
/// </summary>
public static class TestContextExtensions
{
    /// <summary>
    /// Wait for a condition to be true
    /// </summary>
    public static async Task WaitForAsync(
        this TestContext context,
        Func<bool> condition,
        TimeSpan? timeout = null)
    {
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeoutValue)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Condition was not met within timeout period");
    }

    /// <summary>
    /// Wait for an assertion to pass
    /// </summary>
    public static async Task WaitForAssertionAsync(
        this TestContext context,
        Action assertion,
        TimeSpan? timeout = null)
    {
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        Exception? lastException = null;

        while (DateTime.UtcNow - startTime < timeoutValue)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(50);
            }
        }

        throw new TimeoutException(
            $"Assertion did not pass within timeout period. Last exception: {lastException?.Message}",
            lastException);
    }
}
