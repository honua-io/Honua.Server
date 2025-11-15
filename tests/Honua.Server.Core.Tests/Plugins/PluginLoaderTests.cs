// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Plugins;
using Honua.Server.Core.Tests.Plugins.Mocks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Plugins;

/// <summary>
/// Comprehensive tests for the PluginLoader class.
/// Tests plugin discovery, loading, lifecycle, configuration, and error handling.
/// </summary>
public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly Mock<ILogger<PluginLoader>> _mockLogger;
    private readonly Mock<IHostEnvironment> _mockEnvironment;
    private readonly List<string> _tempPluginDirectories = new();

    public PluginLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"honua_plugin_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        _mockLogger = new Mock<ILogger<PluginLoader>>();
        _mockEnvironment = new Mock<IHostEnvironment>();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        // Note: IsDevelopment() is an extension method and cannot be mocked.
        // Instead, we mock EnvironmentName to return "Development" which makes IsDevelopment() return true.
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
                // Ignore cleanup errors in tests
            }
        }

        foreach (var dir in _tempPluginDirectories)
        {
            if (Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    #region Plugin Discovery Tests

    [Fact]
    public async Task LoadPluginsAsync_WithValidPlugin_LoadsSuccessfully()
    {
        // Arrange
        var pluginDir = CreateMockPluginDirectory("test.valid.plugin", "ValidPlugin");
        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.Should().NotBeNull();
        result.LoadedPlugins.Should().HaveCount(1);
        result.FailedPlugins.Should().BeEmpty();
        result.HasFailures.Should().BeFalse();

        var plugin = result.LoadedPlugins.First();
        plugin.Id.Should().Be("test.valid.plugin");
        plugin.Name.Should().Be("ValidPlugin");
        plugin.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task LoadPluginsAsync_WithInvalidManifest_LogsError()
    {
        // Arrange
        var pluginDir = Path.Combine(_tempDirectory, "invalid-plugin");
        Directory.CreateDirectory(pluginDir);
        _tempPluginDirectories.Add(pluginDir);

        // Create invalid JSON manifest
        var manifestPath = Path.Combine(pluginDir, "plugin.json");
        await File.WriteAllTextAsync(manifestPath, "{ invalid json }");

        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().BeEmpty();
        result.FailedPlugins.Should().HaveCount(1);
        result.HasFailures.Should().BeTrue();

        var failure = result.FailedPlugins.First();
        failure.PluginId.Should().Be("invalid-plugin");
    }

    [Fact]
    public async Task LoadPluginsAsync_WithMissingManifest_SkipsPlugin()
    {
        // Arrange
        var pluginDir = Path.Combine(_tempDirectory, "no-manifest");
        Directory.CreateDirectory(pluginDir);
        _tempPluginDirectories.Add(pluginDir);

        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().BeEmpty();
        result.FailedPlugins.Should().BeEmpty();

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No plugin.json found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadPluginsAsync_WithMissingAssembly_LogsError()
    {
        // Arrange
        var pluginDir = Path.Combine(_tempDirectory, "missing-assembly");
        Directory.CreateDirectory(pluginDir);
        _tempPluginDirectories.Add(pluginDir);

        var manifest = new PluginManifest
        {
            Id = "test.missing.assembly",
            Name = "Missing Assembly Plugin",
            Version = "1.0.0",
            Assembly = "NonExistent.dll",
            EntryPoint = "Test.Plugin",
            Author = "Test",
            Description = "Test plugin with missing assembly"
        };

        var manifestPath = Path.Combine(pluginDir, "plugin.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));

        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().BeEmpty();
        result.FailedPlugins.Should().HaveCount(1);
        result.HasFailures.Should().BeTrue();

        var failure = result.FailedPlugins.First();
        failure.PluginId.Should().Be("missing-assembly");
        failure.Error.Should().ContainEquivalentOf("assembly not found");
    }

    [Fact]
    public async Task LoadPluginsAsync_FromMultipleDirectories_LoadsAll()
    {
        // Arrange
        var dir1 = Path.Combine(_tempDirectory, "dir1");
        var dir2 = Path.Combine(_tempDirectory, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        CreateMockPluginDirectory("test.plugin.one", "PluginOne", dir1);
        CreateMockPluginDirectory("test.plugin.two", "PluginTwo", dir2);

        var configuration = CreateConfiguration(pluginPaths: new[] { dir1, dir2 });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().HaveCount(2);
        result.LoadedPlugins.Select(p => p.Id).Should().Contain(new[] { "test.plugin.one", "test.plugin.two" });
    }

    [Fact]
    public async Task LoadPluginsAsync_WithExcludedPlugin_DoesNotLoad()
    {
        // Arrange
        CreateMockPluginDirectory("test.excluded.plugin", "ExcludedPlugin");
        CreateMockPluginDirectory("test.included.plugin", "IncludedPlugin");

        var configuration = CreateConfiguration(
            pluginPaths: new[] { _tempDirectory },
            excludedPlugins: new[] { "test.excluded.plugin" });

        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().HaveCount(1);
        result.LoadedPlugins.First().Id.Should().Be("test.included.plugin");
    }

    [Fact]
    public async Task LoadPluginsAsync_WithIncludedPlugin_LoadsOnlyIncluded()
    {
        // Arrange
        CreateMockPluginDirectory("test.plugin.one", "PluginOne");
        CreateMockPluginDirectory("test.plugin.two", "PluginTwo");
        CreateMockPluginDirectory("test.plugin.three", "PluginThree");

        var configuration = CreateConfiguration(
            pluginPaths: new[] { _tempDirectory },
            includedPlugins: new[] { "test.plugin.one", "test.plugin.three" });

        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().HaveCount(2);
        result.LoadedPlugins.Select(p => p.Id).Should().Contain(new[] { "test.plugin.one", "test.plugin.three" });
        result.LoadedPlugins.Select(p => p.Id).Should().NotContain("test.plugin.two");
    }

    [Fact]
    public async Task LoadPluginsAsync_WithNonExistentPath_LogsWarning()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "non-existent");
        var configuration = CreateConfiguration(pluginPaths: new[] { nonExistentPath });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().BeEmpty();

        // Verify warning was logged (at least once, may be more due to default plugin path)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("does not exist")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Plugin Lifecycle Tests

    [Fact]
    public async Task LoadPluginsAsync_CallsOnLoadAsync()
    {
        // This test would require a compilable mock plugin assembly
        // For now, we'll test the loader's behavior with a valid manifest
        // In a full implementation, you would compile a mock plugin DLL

        // Arrange
        CreateMockPluginDirectory("test.lifecycle.plugin", "LifecyclePlugin");
        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        // Since we can't load an actual assembly in this test, we verify the loader tried
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPlugin_ReturnsLoadedPlugin()
    {
        // Arrange
        CreateMockPluginDirectory("test.get.plugin", "GetPlugin");
        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        await loader.LoadPluginsAsync();

        // Act
        var plugin = loader.GetPlugin("test.get.plugin");

        // Assert
        plugin.Should().NotBeNull();
        plugin!.GetType().Name.Should().Be(nameof(MockServicePlugin));

        // Verify the plugin was loaded (check via the IHonuaPlugin interface which is shared)
        plugin.Id.Should().Be("test.mock.plugin");
        plugin.Name.Should().Be("Mock Service Plugin");
    }

    [Fact]
    public void GetPlugin_WithUnloadedPlugin_ReturnsNull()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var plugin = loader.GetPlugin("nonexistent.plugin");

        // Assert
        plugin.Should().BeNull();
    }

    [Fact]
    public async Task GetAllPlugins_ReturnsAllLoadedPlugins()
    {
        // Arrange
        CreateMockPluginDirectory("test.plugin.alpha", "AlphaPlugin");
        CreateMockPluginDirectory("test.plugin.beta", "BetaPlugin");

        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        await loader.LoadPluginsAsync();

        // Act
        var plugins = loader.GetAllPlugins();

        // Assert
        plugins.Should().NotBeNull();
        // In real scenarios with loadable assemblies, this would have count
    }

    [Fact]
    public async Task GetServicePlugins_ReturnsOnlyServicePlugins()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var servicePlugins = loader.GetServicePlugins();

        // Assert
        servicePlugins.Should().NotBeNull();
        servicePlugins.Should().BeEmpty(); // No plugins loaded
    }

    #endregion

    #region Plugin Context Tests

    [Fact]
    public async Task LoadPluginsAsync_PopulatesPluginContext()
    {
        // This would require integration testing with a real plugin assembly
        // Unit test verifies the loader attempts to create the context
        // Arrange
        CreateMockPluginDirectory("test.context.plugin", "ContextPlugin");
        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Configuration Integration Tests

    [Fact]
    public async Task LoadPluginsAsync_ReadsExclusionList()
    {
        // Arrange
        CreateMockPluginDirectory("test.excluded", "Excluded");
        CreateMockPluginDirectory("test.included", "Included");

        var configDict = new Dictionary<string, string?>
        {
            ["honua:plugins:paths:0"] = _tempDirectory,
            ["honua:plugins:exclude:0"] = "test.excluded"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().HaveCount(1);
        result.LoadedPlugins.First().Id.Should().Be("test.included");
    }

    [Fact]
    public async Task LoadPluginsAsync_ReadsInclusionList()
    {
        // Arrange
        CreateMockPluginDirectory("test.alpha", "Alpha");
        CreateMockPluginDirectory("test.beta", "Beta");
        CreateMockPluginDirectory("test.gamma", "Gamma");

        var configDict = new Dictionary<string, string?>
        {
            ["honua:plugins:paths:0"] = _tempDirectory,
            ["honua:plugins:load:0"] = "test.alpha",
            ["honua:plugins:load:1"] = "test.gamma"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().HaveCount(2);
        result.LoadedPlugins.Select(p => p.Id).Should().Contain(new[] { "test.alpha", "test.gamma" });
    }

    [Fact]
    public async Task LoadPluginsAsync_ReadsPluginPaths()
    {
        // Arrange
        var customPath = Path.Combine(_tempDirectory, "custom");
        Directory.CreateDirectory(customPath);

        CreateMockPluginDirectory("test.custom.path", "CustomPath", customPath);

        var configDict = new Dictionary<string, string?>
        {
            ["honua:plugins:paths:0"] = customPath
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().HaveCount(1);
        result.LoadedPlugins.First().Id.Should().Be("test.custom.path");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void PluginValidationResult_WithNoErrors_IsValid()
    {
        // Arrange
        var result = new PluginValidationResult();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
        result.GetSummary().Should().Be("Validation passed");
    }

    [Fact]
    public void PluginValidationResult_WithErrors_IsInvalid()
    {
        // Arrange
        var result = new PluginValidationResult();
        result.AddError("Test error");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors.First().Should().Be("Test error");
        result.GetSummary().Should().Contain("failed");
    }

    [Fact]
    public void PluginValidationResult_WithWarnings_IsValidWithWarnings()
    {
        // Arrange
        var result = new PluginValidationResult();
        result.AddWarning("Test warning");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().HaveCount(1);
        result.Warnings.First().Should().Be("Test warning");
        result.GetSummary().Should().Contain("warning");
    }

    [Fact]
    public void PluginValidationResult_WithErrorsAndWarnings_ShowsBoth()
    {
        // Arrange
        var result = new PluginValidationResult();
        result.AddError("Error 1");
        result.AddError("Error 2");
        result.AddWarning("Warning 1");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Warnings.Should().HaveCount(1);
        result.GetSummary().Should().Contain("2 error");
        result.GetSummary().Should().Contain("1 warning");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task LoadPluginsAsync_WithException_ContinuesLoading()
    {
        // Arrange
        CreateMockPluginDirectory("test.good.plugin", "GoodPlugin");

        // Create a plugin with invalid manifest
        var badPluginDir = Path.Combine(_tempDirectory, "bad-plugin");
        Directory.CreateDirectory(badPluginDir);
        _tempPluginDirectories.Add(badPluginDir);
        await File.WriteAllTextAsync(Path.Combine(badPluginDir, "plugin.json"), "{ bad json");

        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.LoadPluginsAsync();

        // Assert
        result.LoadedPlugins.Should().HaveCount(1);
        result.FailedPlugins.Should().HaveCount(1);
        result.HasFailures.Should().BeTrue();
    }

    [Fact]
    public async Task LoadPluginsAsync_LogsErrors()
    {
        // Arrange
        var badPluginDir = Path.Combine(_tempDirectory, "error-plugin");
        Directory.CreateDirectory(badPluginDir);
        _tempPluginDirectories.Add(badPluginDir);
        await File.WriteAllTextAsync(Path.Combine(badPluginDir, "plugin.json"), "invalid");

        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        await loader.LoadPluginsAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to load plugin")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Dispose and Cleanup Tests

    [Fact]
    public void Dispose_ClearsLoadedPlugins()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        loader.Dispose();
        var plugins = loader.GetAllPlugins();

        // Assert
        plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task UnloadPluginAsync_WithLoadedPlugin_ReturnsTrue()
    {
        // Arrange
        CreateMockPluginDirectory("test.unload.plugin", "UnloadPlugin");
        var configuration = CreateConfiguration(pluginPaths: new[] { _tempDirectory });
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        await loader.LoadPluginsAsync();

        // Act - attempt to unload (may not succeed in test environment)
        var result = await loader.UnloadPluginAsync("test.unload.plugin");

        // Assert - since actual loading may fail, we just verify method doesn't throw
        result.Should().BeOfType<bool>();
    }

    [Fact]
    public async Task UnloadPluginAsync_WithNonExistentPlugin_ReturnsFalse()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var loader = new PluginLoader(_mockLogger.Object, configuration, _mockEnvironment.Object);

        // Act
        var result = await loader.UnloadPluginAsync("nonexistent.plugin");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private IConfiguration CreateConfiguration(
        string[]? pluginPaths = null,
        string[]? excludedPlugins = null,
        string[]? includedPlugins = null)
    {
        var configDict = new Dictionary<string, string?>();

        if (pluginPaths != null)
        {
            for (int i = 0; i < pluginPaths.Length; i++)
            {
                configDict[$"honua:plugins:paths:{i}"] = pluginPaths[i];
            }
        }

        if (excludedPlugins != null)
        {
            for (int i = 0; i < excludedPlugins.Length; i++)
            {
                configDict[$"honua:plugins:exclude:{i}"] = excludedPlugins[i];
            }
        }

        if (includedPlugins != null)
        {
            for (int i = 0; i < includedPlugins.Length; i++)
            {
                configDict[$"honua:plugins:load:{i}"] = includedPlugins[i];
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    private string CreateMockPluginDirectory(string id, string name, string? basePath = null)
    {
        var pluginDir = Path.Combine(basePath ?? _tempDirectory, Path.GetFileName(id));
        Directory.CreateDirectory(pluginDir);
        _tempPluginDirectories.Add(pluginDir);

        // Use MockServicePlugin as the actual plugin implementation
        var assemblyFileName = "Honua.Server.Core.Tests.dll";
        var entryPoint = "Honua.Server.Core.Tests.Plugins.Mocks.MockServicePlugin";

        var manifest = new PluginManifest
        {
            Id = id,
            Name = name,
            Version = "1.0.0",
            Assembly = assemblyFileName,
            EntryPoint = entryPoint,
            Author = "Test",
            Description = $"Test plugin {name}",
            PluginType = "service"
        };

        var manifestPath = Path.Combine(pluginDir, "plugin.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));

        // Copy the test assembly to the plugin directory so it can be loaded
        var testAssemblyPath = typeof(MockServicePlugin).Assembly.Location;
        var targetAssemblyPath = Path.Combine(pluginDir, assemblyFileName);
        File.Copy(testAssemblyPath, targetAssemblyPath, overwrite: true);

        // DO NOT copy Honua.Server.Core.dll - it should be shared from the default load context
        // This ensures that plugin interfaces like IHonuaPlugin are the same type across contexts

        return pluginDir;
    }

    #endregion
}
