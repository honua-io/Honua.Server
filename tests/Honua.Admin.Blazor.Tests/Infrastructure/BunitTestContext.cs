// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Honua.Admin.Blazor.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Honua.Admin.Blazor.Tests.Infrastructure;

/// <summary>
/// Base test context for bUnit Blazor component tests.
/// Provides common services and configuration for all component tests.
/// </summary>
public class BunitTestContext : TestContext
{
    public BunitTestContext()
    {
        // Register MudBlazor services
        Services.AddMudServices();

        // Register logger
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        // Register common UI services (with mocks)
        Services.AddSingleton<NavigationState>();
        Services.AddSingleton<EditorState>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<SearchStateService>();

        // JSInterop setup (for MudBlazor components)
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
}

/// <summary>
/// Base class for component tests that provides a fresh test context per test.
/// Implements IDisposable to ensure proper cleanup after each test.
/// </summary>
public abstract class ComponentTestBase : IDisposable
{
    protected BunitTestContext Context { get; }

    protected ComponentTestBase()
    {
        Context = new BunitTestContext();
    }

    public void Dispose()
    {
        Context?.Dispose();
    }
}
