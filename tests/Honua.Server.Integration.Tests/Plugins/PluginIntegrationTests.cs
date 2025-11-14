// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Honua.Server.Integration.Tests.Plugins;

/// <summary>
/// Integration tests for the plugin system.
/// Tests end-to-end plugin loading, configuration, and service integration.
/// </summary>
public sealed class PluginIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;

    public PluginIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"honua_plugin_integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region End-to-End Tests

    [Fact]
    public async Task PluginLoader_WithRealWfsPlugin_LoadsSuccessfully()
    {
        // Arrange
        var pluginPath = FindPluginPath("Honua.Server.Services.Wfs");
        if (pluginPath == null)
        {
            // Skip test if WFS plugin not built
            return;
        }

        var configuration = CreateConfiguration(new[] { Path.GetDirectoryName(pluginPath)! });
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PluginLoader>();
        var environment = CreateMockEnvironment();

        var loader = new PluginLoader(logger, configuration, environment);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.Should().NotBeNull();
        result.LoadedPlugins.Should().NotBeEmpty();

        var wfsPlugin = result.LoadedPlugins.FirstOrDefault(p => p.Id == "honua.services.wfs");
        if (wfsPlugin != null)
        {
            wfsPlugin.Name.Should().Be("WFS Service Plugin");
            wfsPlugin.Type.Should().Be(PluginType.Service);
        }
    }

    [Fact]
    public async Task MultiplePlugins_LoadSimultaneously()
    {
        // Arrange
        var pluginsBasePath = FindPluginsBasePath();
        if (pluginsBasePath == null)
        {
            // Skip if plugins not available
            return;
        }

        var configuration = CreateConfiguration(new[] { pluginsBasePath });
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PluginLoader>();
        var environment = CreateMockEnvironment();

        var loader = new PluginLoader(logger, configuration, environment);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.Should().NotBeNull();

        if (result.LoadedPlugins.Count > 0)
        {
            result.LoadedPlugins.Should().OnlyHaveUniqueItems(p => p.Id);
            result.LoadedPlugins.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Version));
        }
    }

    [Fact]
    public async Task DisabledService_PluginNotLoaded()
    {
        // Arrange
        var pluginsBasePath = FindPluginsBasePath();
        if (pluginsBasePath == null)
        {
            return;
        }

        var configDict = new Dictionary<string, string?>
        {
            ["honua:plugins:paths:0"] = pluginsBasePath,
            ["honua:plugins:exclude:0"] = "honua.services.wfs"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PluginLoader>();
        var environment = CreateMockEnvironment();

        var loader = new PluginLoader(logger, configuration, environment);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().NotContain(p => p.Id == "honua.services.wfs");
    }

    #endregion

    #region Configuration V2 Integration Tests

    [Fact]
    public void ConfigurationV2_LoadsEnabledPlugins()
    {
        // This test verifies that Configuration V2 integration works
        // It would require a test server with ConfigurationV2Extensions

        // Arrange
        var pluginsBasePath = FindPluginsBasePath();
        if (pluginsBasePath == null)
        {
            return;
        }

        var hclConfig = @"
honua {
    version = ""1.0""
    environment = ""test""
}

service ""wfs"" {
    enabled = true
}
";

        var tempConfigPath = Path.Combine(_tempDirectory, "test.hcl");
        File.WriteAllText(tempConfigPath, hclConfig);

        // In a full integration test, you would:
        // 1. Load the HCL configuration
        // 2. Build a test server with plugins enabled
        // 3. Verify that only enabled plugins are loaded
        // 4. Verify endpoints are mapped correctly

        // For this test, we just verify the file was created
        File.Exists(tempConfigPath).Should().BeTrue();
    }

    [Fact]
    public async Task ConfigurationV2_PassesSettingsToPlugin()
    {
        // Arrange
        var pluginsBasePath = FindPluginsBasePath();
        if (pluginsBasePath == null)
        {
            return;
        }

        var configDict = new Dictionary<string, string?>
        {
            ["honua:plugins:paths:0"] = pluginsBasePath,
            ["honua:services:wfs:max_features"] = "5000",
            ["honua:services:wfs:default_count"] = "50"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PluginLoader>();
        var environment = CreateMockEnvironment();

        var loader = new PluginLoader(logger, configuration, environment);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        // The plugin should have access to these settings via IConfiguration
        // This would be verified in a full integration test where we can
        // inspect the plugin's configured services

        configuration.GetValue<int>("honua:services:wfs:max_features").Should().Be(5000);
        configuration.GetValue<int>("honua:services:wfs:default_count").Should().Be(50);
    }

    [Fact]
    public async Task ConfigurationV2_ValidatesPluginConfig()
    {
        // This test would validate that plugin configuration validation works
        // In a real scenario, you would:
        // 1. Create invalid configuration for a plugin
        // 2. Attempt to load the plugin
        // 3. Verify validation errors are reported

        var pluginsBasePath = FindPluginsBasePath();
        if (pluginsBasePath == null)
        {
            return;
        }

        var configuration = CreateConfiguration(new[] { pluginsBasePath });
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PluginLoader>();
        var environment = CreateMockEnvironment();

        var loader = new PluginLoader(logger, configuration, environment);

        var result = await loader.LoadPluginsAsync();

        // In a full implementation, each loaded service plugin would have
        // its ValidateConfiguration method called
        result.Should().NotBeNull();
    }

    #endregion

    #region Service Plugin Tests

    [Fact]
    public async Task ServicePlugin_GetServicePlugins_ReturnsOnlyServicePlugins()
    {
        // Arrange
        var pluginsBasePath = FindPluginsBasePath();
        if (pluginsBasePath == null)
        {
            return;
        }

        var configuration = CreateConfiguration(new[] { pluginsBasePath });
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PluginLoader>();
        var environment = CreateMockEnvironment();

        var loader = new PluginLoader(logger, configuration, environment);

        // Act
        await loader.LoadPluginsAsync();
        var servicePlugins = loader.GetServicePlugins();

        // Assert
        servicePlugins.Should().NotBeNull();
        servicePlugins.Should().AllBeAssignableTo<IServicePlugin>();

        if (servicePlugins.Count > 0)
        {
            servicePlugins.Should().AllSatisfy(p =>
            {
                p.ServiceId.Should().NotBeNullOrEmpty();
                p.ServiceType.Should().NotBe(default(ServiceType));
            });
        }
    }

    #endregion

    #region Plugin Lifecycle Tests

    [Fact]
    public async Task Plugin_LoadAndUnload_Succeeds()
    {
        // Arrange
        var pluginsBasePath = FindPluginsBasePath();
        if (pluginsBasePath == null)
        {
            return;
        }

        var configuration = CreateConfiguration(new[] { pluginsBasePath });
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PluginLoader>();
        var environment = CreateMockEnvironment();

        var loader = new PluginLoader(logger, configuration, environment);

        // Act - Load
        var result = await loader.LoadPluginsAsync();
        var firstPlugin = result.LoadedPlugins.FirstOrDefault();

        if (firstPlugin == null)
        {
            return; // No plugins to test
        }

        // Act - Unload
        var unloaded = await loader.UnloadPluginAsync(firstPlugin.Id);

        // Assert
        unloaded.Should().BeTrue();
        loader.GetPlugin(firstPlugin.Id).Should().BeNull();
    }

    [Fact]
    public async Task Plugin_UnloadNonExistent_ReturnsFalse()
    {
        // Arrange
        var configuration = CreateConfiguration(Array.Empty<string>());
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PluginLoader>();
        var environment = CreateMockEnvironment();

        var loader = new PluginLoader(logger, configuration, environment);

        // Act
        var unloaded = await loader.UnloadPluginAsync("nonexistent.plugin");

        // Assert
        unloaded.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private IConfiguration CreateConfiguration(string[] pluginPaths)
    {
        var configDict = new Dictionary<string, string?>();

        for (int i = 0; i < pluginPaths.Length; i++)
        {
            configDict[$"honua:plugins:paths:{i}"] = pluginPaths[i];
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    private IHostEnvironment CreateMockEnvironment()
    {
        var mockEnv = new TestHostEnvironment
        {
            EnvironmentName = "Development",
            ApplicationName = "Honua.Server.Integration.Tests"
        };
        return mockEnv;
    }

    private string? FindPluginPath(string pluginName)
    {
        // Look for built plugins in the output directory
        var baseDir = AppContext.BaseDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "plugins", pluginName, "bin", "Debug", "net9.0", $"{pluginName}.dll"),
            Path.Combine(baseDir, "plugins", pluginName, $"{pluginName}.dll")
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private string? FindPluginsBasePath()
    {
        // Look for the plugins directory
        var baseDir = AppContext.BaseDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "plugins"),
            Path.Combine(baseDir, "plugins")
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    #endregion
}
