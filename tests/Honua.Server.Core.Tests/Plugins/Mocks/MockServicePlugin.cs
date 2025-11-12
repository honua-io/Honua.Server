// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Tests.Plugins.Mocks;

/// <summary>
/// Mock service plugin for testing the plugin system.
/// Allows configuration of behavior for various test scenarios.
/// </summary>
public sealed class MockServicePlugin : IServicePlugin
{
    public bool OnLoadAsyncCalled { get; private set; }
    public bool OnUnloadAsyncCalled { get; private set; }
    public bool ConfigureServicesCalled { get; private set; }
    public bool MapEndpointsCalled { get; private set; }
    public bool ValidateConfigurationCalled { get; private set; }
    public bool ConfigureMiddlewareCalled { get; private set; }

    public PluginContext? LastContext { get; private set; }

    // Configuration options for controlling mock behavior
    public bool ThrowOnLoad { get; set; }
    public bool ThrowOnUnload { get; set; }
    public bool ThrowOnConfigureServices { get; set; }
    public bool ThrowOnMapEndpoints { get; set; }
    public bool ThrowOnValidateConfiguration { get; set; }
    public bool ValidationShouldFail { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();

    // IHonuaPlugin implementation
    public string Id { get; set; } = "test.mock.plugin";
    public string Name { get; set; } = "Mock Service Plugin";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "Mock plugin for testing";
    public string Author { get; set; } = "Test Suite";
    public IReadOnlyList<PluginDependency> Dependencies { get; set; } = Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion { get; set; } = "1.0.0";

    // IServicePlugin implementation
    public string ServiceId { get; set; } = "mock";
    public ServiceType ServiceType { get; set; } = ServiceType.Custom;

    public Task OnLoadAsync(PluginContext context)
    {
        if (ThrowOnLoad)
        {
            throw new InvalidOperationException("Mock plugin configured to fail on load");
        }

        OnLoadAsyncCalled = true;
        LastContext = context;
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync()
    {
        if (ThrowOnUnload)
        {
            throw new InvalidOperationException("Mock plugin configured to fail on unload");
        }

        OnUnloadAsyncCalled = true;
        return Task.CompletedTask;
    }

    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        if (ThrowOnConfigureServices)
        {
            throw new InvalidOperationException("Mock plugin configured to fail on configure services");
        }

        ConfigureServicesCalled = true;
        LastContext = context;
    }

    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        if (ThrowOnMapEndpoints)
        {
            throw new InvalidOperationException("Mock plugin configured to fail on map endpoints");
        }

        MapEndpointsCalled = true;
        LastContext = context;
    }

    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        if (ThrowOnValidateConfiguration)
        {
            throw new InvalidOperationException("Mock plugin configured to fail on validate configuration");
        }

        ValidateConfigurationCalled = true;

        var result = new PluginValidationResult();

        if (ValidationShouldFail)
        {
            result.AddError("Mock validation error");
        }

        foreach (var error in ValidationErrors)
        {
            result.AddError(error);
        }

        foreach (var warning in ValidationWarnings)
        {
            result.AddWarning(warning);
        }

        return result;
    }

    public void ConfigureMiddleware(IApplicationBuilder app, PluginContext context)
    {
        ConfigureMiddlewareCalled = true;
        LastContext = context;
    }

    public void Reset()
    {
        OnLoadAsyncCalled = false;
        OnUnloadAsyncCalled = false;
        ConfigureServicesCalled = false;
        MapEndpointsCalled = false;
        ValidateConfigurationCalled = false;
        ConfigureMiddlewareCalled = false;
        LastContext = null;
        ThrowOnLoad = false;
        ThrowOnUnload = false;
        ThrowOnConfigureServices = false;
        ThrowOnMapEndpoints = false;
        ThrowOnValidateConfiguration = false;
        ValidationShouldFail = false;
        ValidationErrors.Clear();
        ValidationWarnings.Clear();
    }
}
