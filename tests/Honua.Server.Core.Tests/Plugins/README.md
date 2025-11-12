# Honua Server Plugin System Tests

This directory contains comprehensive tests for the Honua Server plugin architecture, covering both unit tests and integration tests for the plugin loading, lifecycle management, and Configuration V2 integration.

## Test Structure

### Unit Tests (`Honua.Server.Core.Tests/Plugins/`)

- **PluginLoaderTests.cs** - Comprehensive unit tests for the `PluginLoader` class
- **Mocks/MockServicePlugin.cs** - Mock plugin implementation for testing

### Integration Tests (`Honua.Server.Integration.Tests/Plugins/`)

- **PluginIntegrationTests.cs** - End-to-end tests with real plugin assemblies

## Running Plugin Tests

### Run All Plugin Tests

```bash
# Run all plugin-related unit tests
dotnet test tests/Honua.Server.Core.Tests --filter "FullyQualifiedName~Plugin"

# Run all plugin-related integration tests
dotnet test tests/Honua.Server.Integration.Tests --filter "FullyQualifiedName~Plugin"

# Run all plugin tests (unit + integration)
dotnet test --filter "FullyQualifiedName~Plugin"
```

### Run Specific Test Classes

```bash
# Run only PluginLoaderTests
dotnet test tests/Honua.Server.Core.Tests --filter "FullyQualifiedName~PluginLoaderTests"

# Run only PluginIntegrationTests
dotnet test tests/Honua.Server.Integration.Tests --filter "FullyQualifiedName~PluginIntegrationTests"
```

### Run Specific Test Categories

```bash
# Run plugin discovery tests
dotnet test --filter "FullyQualifiedName~LoadPluginsAsync"

# Run plugin lifecycle tests
dotnet test --filter "FullyQualifiedName~Lifecycle"

# Run configuration integration tests
dotnet test --filter "FullyQualifiedName~Configuration"
```

## Test Coverage

### PluginLoaderTests (Unit Tests)

**Plugin Discovery Tests:**
- `LoadPluginsAsync_WithValidPlugin_LoadsSuccessfully` - Verifies basic plugin loading
- `LoadPluginsAsync_WithInvalidManifest_LogsError` - Tests invalid plugin.json handling
- `LoadPluginsAsync_WithMissingManifest_SkipsPlugin` - Tests missing plugin.json
- `LoadPluginsAsync_WithMissingAssembly_LogsError` - Tests missing DLL handling
- `LoadPluginsAsync_FromMultipleDirectories_LoadsAll` - Tests multiple plugin directories
- `LoadPluginsAsync_WithExcludedPlugin_DoesNotLoad` - Tests configuration exclusion
- `LoadPluginsAsync_WithIncludedPlugin_LoadsOnlyIncluded` - Tests configuration inclusion
- `LoadPluginsAsync_WithNonExistentPath_LogsWarning` - Tests non-existent paths

**Plugin Lifecycle Tests:**
- `LoadPluginsAsync_CallsOnLoadAsync` - Verifies OnLoadAsync called
- `GetPlugin_ReturnsLoadedPlugin` - Tests plugin retrieval
- `GetPlugin_WithUnloadedPlugin_ReturnsNull` - Tests null returns
- `GetAllPlugins_ReturnsAllLoadedPlugins` - Tests getting all plugins
- `GetServicePlugins_ReturnsOnlyServicePlugins` - Tests filtering service plugins

**Configuration Integration Tests:**
- `LoadPluginsAsync_ReadsExclusionList` - Tests configuration exclusion
- `LoadPluginsAsync_ReadsInclusionList` - Tests configuration inclusion
- `LoadPluginsAsync_ReadsPluginPaths` - Tests custom plugin paths

**Validation Tests:**
- `PluginValidationResult_WithNoErrors_IsValid` - Tests valid configuration
- `PluginValidationResult_WithErrors_IsInvalid` - Tests invalid configuration
- `PluginValidationResult_WithWarnings_IsValidWithWarnings` - Tests warnings
- `PluginValidationResult_WithErrorsAndWarnings_ShowsBoth` - Tests combined errors/warnings

**Error Handling Tests:**
- `LoadPluginsAsync_WithException_ContinuesLoading` - Tests graceful error handling
- `LoadPluginsAsync_LogsErrors` - Tests error logging

### PluginIntegrationTests (Integration Tests)

**End-to-End Tests:**
- `PluginLoader_WithRealWfsPlugin_LoadsSuccessfully` - Tests WFS plugin loading
- `MultiplePlugins_LoadSimultaneously` - Tests multiple plugins loading
- `DisabledService_PluginNotLoaded` - Tests disabled services

**Configuration V2 Integration:**
- `ConfigurationV2_LoadsEnabledPlugins` - Tests Config V2 plugin enabling
- `ConfigurationV2_PassesSettingsToPlugin` - Tests settings propagation
- `ConfigurationV2_ValidatesPluginConfig` - Tests configuration validation

**Service Plugin Tests:**
- `ServicePlugin_GetServicePlugins_ReturnsOnlyServicePlugins` - Tests service plugin filtering

**Plugin Lifecycle:**
- `Plugin_LoadAndUnload_Succeeds` - Tests plugin load/unload
- `Plugin_UnloadNonExistent_ReturnsFalse` - Tests unloading non-existent plugins

## Creating Mock Plugins for Testing

The `MockServicePlugin` class provides a flexible mock implementation of `IServicePlugin` that can be configured for various test scenarios.

### Basic Usage

```csharp
var mockPlugin = new MockServicePlugin
{
    Id = "test.plugin",
    Name = "Test Plugin",
    ServiceId = "test"
};

// Use in tests
await mockPlugin.OnLoadAsync(context);
Assert.True(mockPlugin.OnLoadAsyncCalled);
```

### Configuring Mock Behavior

```csharp
// Make the plugin throw on load
var failingPlugin = new MockServicePlugin
{
    ThrowOnLoad = true
};

// Configure validation to fail
var invalidPlugin = new MockServicePlugin
{
    ValidationShouldFail = true,
    ValidationErrors = new List<string> { "Test error 1", "Test error 2" }
};

// Add warnings
var warningPlugin = new MockServicePlugin
{
    ValidationWarnings = new List<string> { "Test warning" }
};
```

### Verifying Plugin Calls

```csharp
var mockPlugin = new MockServicePlugin();

// Call plugin methods
await mockPlugin.OnLoadAsync(context);
mockPlugin.ConfigureServices(services, configuration, context);
mockPlugin.MapEndpoints(endpoints, context);

// Verify calls were made
Assert.True(mockPlugin.OnLoadAsyncCalled);
Assert.True(mockPlugin.ConfigureServicesCalled);
Assert.True(mockPlugin.MapEndpointsCalled);

// Verify context was passed
Assert.NotNull(mockPlugin.LastContext);
Assert.Equal(expectedPath, mockPlugin.LastContext.PluginPath);

// Reset for next test
mockPlugin.Reset();
```

## Testing Plugin Integration with Configuration V2

### Creating Test Configuration

```csharp
var hclConfig = @"
honua {
    version = ""1.0""
    environment = ""test""
}

service ""wfs"" {
    enabled = true
    max_features = 5000
    default_count = 100
}

service ""wms"" {
    enabled = false
}
";

var tempConfigPath = Path.Combine(tempDirectory, "test.hcl");
File.WriteAllText(tempConfigPath, hclConfig);

var config = HonuaConfigLoader.Load(tempConfigPath);
```

### Testing Plugin Configuration

```csharp
var configDict = new Dictionary<string, string?>
{
    ["honua:plugins:paths:0"] = pluginPath,
    ["honua:services:wfs:max_features"] = "5000",
    ["honua:services:wfs:enabled"] = "true"
};

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(configDict)
    .Build();

var loader = new PluginLoader(logger, configuration, environment);
var result = await loader.LoadPluginsAsync();
```

## Common Test Scenarios

### Testing Plugin Discovery

```csharp
[Fact]
public async Task DiscoverPlugins_FromMultiplePaths()
{
    // Arrange
    var path1 = CreatePluginDirectory("plugin1");
    var path2 = CreatePluginDirectory("plugin2");

    var config = CreateConfiguration(pluginPaths: new[] { path1, path2 });
    var loader = new PluginLoader(logger, config, environment);

    // Act
    var result = await loader.LoadPluginsAsync();

    // Assert
    result.LoadedPlugins.Should().HaveCount(2);
}
```

### Testing Plugin Exclusion

```csharp
[Fact]
public async Task ExcludePlugin_NotLoaded()
{
    // Arrange
    CreatePluginDirectory("plugin.excluded");
    CreatePluginDirectory("plugin.included");

    var config = CreateConfiguration(
        pluginPaths: new[] { pluginBasePath },
        excludedPlugins: new[] { "plugin.excluded" }
    );

    var loader = new PluginLoader(logger, config, environment);

    // Act
    var result = await loader.LoadPluginsAsync();

    // Assert
    result.LoadedPlugins.Should().NotContain(p => p.Id == "plugin.excluded");
}
```

### Testing Plugin Validation

```csharp
[Fact]
public void ValidatePluginConfiguration_ReturnsErrors()
{
    // Arrange
    var plugin = new MockServicePlugin
    {
        ValidationErrors = new List<string> { "Invalid setting" }
    };

    var config = CreateConfiguration();

    // Act
    var result = plugin.ValidateConfiguration(config);

    // Assert
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain("Invalid setting");
}
```

### Testing Plugin Lifecycle

```csharp
[Fact]
public async Task LoadUnloadPlugin_Succeeds()
{
    // Arrange
    var loader = new PluginLoader(logger, config, environment);
    await loader.LoadPluginsAsync();

    // Act - Unload
    var unloaded = await loader.UnloadPluginAsync("plugin.id");

    // Assert
    unloaded.Should().BeTrue();
    loader.GetPlugin("plugin.id").Should().BeNull();
}
```

## Test Fixtures and Helpers

### Temporary Plugin Directory

The tests use temporary directories that are automatically cleaned up:

```csharp
public PluginLoaderTests()
{
    _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"honua_plugin_test_{Guid.NewGuid():N}"
    );
    Directory.CreateDirectory(_tempDirectory);
}

public void Dispose()
{
    if (Directory.Exists(_tempDirectory))
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }
}
```

### Creating Mock Plugin Manifests

```csharp
private string CreateMockPluginDirectory(string id, string name)
{
    var pluginDir = Path.Combine(_tempDirectory, id);
    Directory.CreateDirectory(pluginDir);

    var manifest = new PluginManifest
    {
        Id = id,
        Name = name,
        Version = "1.0.0",
        Assembly = $"{name}.dll",
        EntryPoint = $"Test.{name}",
        Author = "Test",
        Description = $"Test plugin {name}",
        PluginType = "service"
    };

    var manifestPath = Path.Combine(pluginDir, "plugin.json");
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

    return pluginDir;
}
```

## Troubleshooting

### Plugin Assembly Not Found

If integration tests fail to find plugin assemblies:

1. Ensure plugins are built: `dotnet build src/plugins/`
2. Check plugin paths in test configuration
3. Verify plugin.json manifests are correct
4. Check assembly names match manifest

### Tests Fail in CI/CD

- Ensure plugin assemblies are included in test artifacts
- Use relative paths that work in different environments
- Skip integration tests if plugins aren't available

### Plugin Context Null

If `PluginContext` is null in tests:

1. Verify the mock environment is properly configured
2. Ensure configuration is passed to plugin loader
3. Check that logger factory is created

## Best Practices

1. **Use temporary directories** - Always use temp directories for test artifacts
2. **Clean up resources** - Implement `IDisposable` and clean up in `Dispose()`
3. **Mock external dependencies** - Use mocks for ILogger, IConfiguration, IHostEnvironment
4. **Test error paths** - Test both success and failure scenarios
5. **Use descriptive test names** - Follow pattern: `Method_Scenario_ExpectedResult`
6. **Verify logging** - Use Moq to verify important log messages
7. **Test async properly** - Always await async operations in tests
8. **Use FluentAssertions** - Use FluentAssertions for readable test assertions

## Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Honua Plugin Architecture](../../../docs/plugin-architecture.md)
- [Configuration V2 Guide](../../../docs/configuration-v2.md)
