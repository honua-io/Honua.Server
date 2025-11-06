// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using Honua.MapSDK.Core;
using Honua.MapSDK.Services;

namespace Honua.MapSDK.Tests.Infrastructure;

/// <summary>
/// Base test context for bUnit Blazor component tests in MapSDK.
/// Provides common services and configuration for all component tests.
/// </summary>
public class MapTestContext : TestContext
{
    public MapTestContext()
    {
        // Register MudBlazor services
        Services.AddMudServices();

        // Register logger
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        // Register MapSDK core services
        Services.AddSingleton<ComponentBus>();
        Services.AddSingleton<IMapConfigurationService, MapConfigurationService>();

        // JSInterop setup (for MapLibre components)
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
}

/// <summary>
/// Base class for component tests that provides a fresh test context per test.
/// Implements IDisposable to ensure proper cleanup after each test.
/// </summary>
public abstract class MapComponentTestBase : IDisposable
{
    protected MapTestContext Context { get; }

    protected MapComponentTestBase()
    {
        Context = new MapTestContext();
    }

    public void Dispose()
    {
        Context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
