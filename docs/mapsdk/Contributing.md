# Contributing to Honua.MapSDK

Thank you for your interest in contributing to Honua.MapSDK! This guide will help you get started.

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Development Setup](#development-setup)
4. [Code Style Guidelines](#code-style-guidelines)
5. [Component Development](#component-development)
6. [Testing Requirements](#testing-requirements)
7. [Documentation Standards](#documentation-standards)
8. [Pull Request Process](#pull-request-process)
9. [Release Process](#release-process)

## Code of Conduct

We are committed to providing a welcoming and inclusive environment. Please be respectful and professional in all interactions.

## Getting Started

### Ways to Contribute

- **Bug Reports** - Found a bug? [Open an issue](https://github.com/honua/Honua.Server/issues)
- **Feature Requests** - Have an idea? [Start a discussion](https://github.com/honua/Honua.Server/discussions)
- **Code Contributions** - Fix bugs or add features via pull requests
- **Documentation** - Improve docs, add examples, or write tutorials
- **Testing** - Write tests, improve coverage, or report issues
- **Community Support** - Help others in discussions

### Before You Start

1. **Check existing issues** - Someone may already be working on it
2. **Start a discussion** - For large changes, discuss first
3. **Read the docs** - Understand the architecture and patterns
4. **Review the codebase** - Get familiar with the code style

## Development Setup

### Prerequisites

- .NET 9 SDK or later
- Visual Studio 2022, VS Code, or Rider
- Git
- Node.js (for JavaScript development)

### Clone and Build

```bash
# Clone the repository
git clone https://github.com/honua/Honua.Server.git
cd Honua.Server

# Restore dependencies
dotnet restore

# Build MapSDK
dotnet build src/Honua.MapSDK/

# Run tests
dotnet test tests/Honua.MapSDK.Tests/

# Run demo app
dotnet run --project examples/Honua.MapSDK.DemoApp/
```

### Project Structure

```
Honua.Server/
├── src/
│   └── Honua.MapSDK/
│       ├── Components/          # UI components
│       ├── Core/                # Core infrastructure
│       ├── Services/            # Services
│       ├── Utilities/           # Utility classes
│       ├── Models/              # Data models
│       └── wwwroot/             # Static assets
├── tests/
│   └── Honua.MapSDK.Tests/     # Unit tests
├── examples/
│   └── Honua.MapSDK.DemoApp/   # Demo application
└── docs/
    └── mapsdk/                  # Documentation
```

### Development Workflow

1. **Create a branch** from main
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes**
   - Write code
   - Add tests
   - Update documentation

3. **Test locally**
   ```bash
   dotnet test
   dotnet run --project examples/Honua.MapSDK.DemoApp/
   ```

4. **Commit your changes**
   ```bash
   git add .
   git commit -m "Add feature: description"
   ```

5. **Push and create PR**
   ```bash
   git push origin feature/your-feature-name
   ```

## Code Style Guidelines

### C# Style

We follow standard .NET coding conventions with these specifics:

#### Naming Conventions

```csharp
// PascalCase for types, methods, properties, events
public class MapComponent { }
public void LoadData() { }
public string MapStyle { get; set; }
public event EventHandler MapReady;

// camelCase for local variables and parameters
public void ProcessData(string dataUrl)
{
    var resultData = LoadData(dataUrl);
}

// _camelCase for private fields
private string _mapId;
private IJSObjectReference? _mapInstance;

// UPPER_CASE for constants
private const int DEFAULT_ZOOM = 10;
```

#### File Organization

```csharp
// 1. Using statements (sorted)
using System;
using System.Collections.Generic;
using Microsoft.JSInterop;

// 2. Namespace
namespace Honua.MapSDK.Components;

// 3. Type definition
/// <summary>
/// Component description
/// </summary>
public class MyComponent : DisposableComponentBase
{
    // 4. Constants
    private const int MAX_ZOOM = 22;

    // 5. Fields
    private string _id = "";
    private IJSObjectReference? _module;

    // 6. Properties (public first, then private)
    [Parameter] public string Id { get; set; } = "";
    [Inject] private ComponentBus Bus { get; set; } = default!;

    // 7. Events
    [Parameter] public EventCallback<ReadyMessage> OnReady { get; set; }

    // 8. Lifecycle methods
    protected override void OnInitialized() { }
    protected override async Task OnAfterRenderAsync(bool firstRender) { }

    // 9. Public methods
    public async Task LoadDataAsync() { }

    // 10. Private methods
    private void ProcessData() { }

    // 11. Disposal
    protected override void OnDispose() { }
}
```

#### Documentation

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Loads geographic data from the specified URL.
/// </summary>
/// <param name="url">The URL to load data from.</param>
/// <param name="useCache">Whether to use cached data if available.</param>
/// <returns>A task that represents the asynchronous operation.</returns>
/// <exception cref="ArgumentNullException">Thrown when url is null.</exception>
/// <exception cref="HttpRequestException">Thrown when the request fails.</exception>
public async Task<GeoJsonData> LoadDataAsync(string url, bool useCache = true)
{
    // Implementation
}
```

### Razor Style

```razor
@* 1. Page directive *@
@page "/map"

@* 2. Using statements *@
@using Honua.MapSDK.Components
@using Honua.MapSDK.Core.Messages

@* 3. Dependency injection *@
@inject ComponentBus Bus
@inject IJSRuntime JS

@* 4. Markup *@
<div class="map-container">
    <HonuaMap
        Id="@_mapId"
        Center="@_center"
        Zoom="@_zoom"
        OnMapReady="HandleReady" />
</div>

@* 5. Code block *@
@code {
    private string _mapId = "map1";
    private double[] _center = new[] { -122, 37 };
    private double _zoom = 10;

    private void HandleReady(MapReadyMessage message)
    {
        // Handle event
    }
}
```

### JavaScript Style

```javascript
// Use ES6+ features
export function createMap(container, options, dotNetRef) {
    const map = new maplibregl.Map({
        container,
        style: options.style,
        center: options.center,
        zoom: options.zoom
    });

    // Set up event handlers
    map.on('moveend', () => {
        dotNetRef.invokeMethodAsync('OnExtentChangedInternal',
            map.getBounds().toArray(),
            map.getZoom()
        );
    });

    // Return API object
    return {
        flyTo: (center, zoom) => {
            map.flyTo({ center, zoom });
        },
        dispose: () => {
            map.remove();
        }
    };
}
```

## Component Development

### Creating a New Component

1. **Create component files**
   ```
   Components/
   └── MyComponent/
       ├── HonuaMyComponent.razor
       ├── HonuaMyComponent.razor.cs (if needed)
       ├── README.md
       └── Examples.razor (optional)
   ```

2. **Implement the component**

```razor
@* HonuaMyComponent.razor *@
@inherits DisposableComponentBase

<div class="honua-mycomponent">
    <!-- Component markup -->
</div>

@code {
    [Parameter] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [Parameter] public string? SyncWith { get; set; }

    [Inject] private ComponentBus Bus { get; set; } = default!;

    protected override void OnInitialized()
    {
        SubscribeToMessage<MapExtentChangedMessage>(HandleExtentChange);
    }

    private void HandleExtentChange(MessageArgs<MapExtentChangedMessage> args)
    {
        if (args.Message.MapId == SyncWith)
        {
            // React to change
        }
    }

    protected override void OnDispose()
    {
        // Custom cleanup if needed
    }
}
```

3. **Add documentation**

Create `Components/MyComponent/README.md`:

```markdown
# HonuaMyComponent

Brief description of what the component does.

## Usage

\`\`\`razor
<HonuaMyComponent
    Id="myComponent"
    SyncWith="map1"
    ... />
\`\`\`

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Id | string | (guid) | Component identifier |
| SyncWith | string? | null | Map ID to sync with |

## Events

| Event | Type | Description |
|-------|------|-------------|
| OnReady | EventCallback<ReadyMessage> | Fired when initialized |

## Examples

See [Examples.razor](Examples.razor) for complete examples.
```

4. **Add tests**

```csharp
public class MyComponentTests : TestContext
{
    [Fact]
    public void Component_Renders_Successfully()
    {
        var cut = RenderComponent<HonuaMyComponent>(parameters => parameters
            .Add(p => p.Id, "test")
        );

        Assert.NotNull(cut.Find(".honua-mycomponent"));
    }

    [Fact]
    public void Component_Subscribes_To_Messages()
    {
        var mockBus = new Mock<ComponentBus>();
        Services.AddSingleton(mockBus.Object);

        var cut = RenderComponent<HonuaMyComponent>();

        mockBus.Verify(b => b.Subscribe<MapExtentChangedMessage>(
            It.IsAny<Func<MessageArgs<MapExtentChangedMessage>, Task>>()),
            Times.Once
        );
    }
}
```

## Testing Requirements

### Test Coverage

- Aim for 80%+ code coverage
- All public APIs must have tests
- Test both success and error cases
- Test ComponentBus integration

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Run specific test
dotnet test --filter FullyQualifiedName~MapComponentTests.Map_Publishes_ReadyMessage
```

### Test Organization

```csharp
public class MyComponentTests
{
    // Use descriptive test names
    [Fact]
    public void Component_Publishes_Message_When_Initialized()
    {
        // Arrange
        var mockBus = new Mock<ComponentBus>();

        // Act
        var component = new MyComponent { Bus = mockBus.Object };
        component.OnInitialized();

        // Assert
        mockBus.Verify(b => b.PublishAsync(
            It.IsAny<ReadyMessage>(),
            It.IsAny<string>()),
            Times.Once
        );
    }
}
```

## Documentation Standards

### Component Documentation

Every component needs:

1. **README.md** in component directory
2. **XML documentation** on public APIs
3. **Usage examples**
4. **Parameter documentation**

### Code Comments

```csharp
// Use comments to explain WHY, not WHAT
// Good:
// Debounce updates to prevent excessive re-renders
await Task.Delay(300);

// Bad:
// Wait 300ms
await Task.Delay(300);

// Use XML docs for public APIs
/// <summary>
/// Loads data from the specified URL with automatic caching.
/// </summary>
public async Task LoadDataAsync(string url) { }
```

## Pull Request Process

### Before Submitting

- [ ] Code follows style guidelines
- [ ] All tests pass
- [ ] New tests added for new features
- [ ] Documentation updated
- [ ] No compiler warnings
- [ ] Changes work in demo app

### PR Title Format

```
[Type] Brief description

Types:
- feat: New feature
- fix: Bug fix
- docs: Documentation only
- style: Code style changes
- refactor: Code refactoring
- test: Adding tests
- chore: Maintenance tasks
```

Examples:
- `feat: Add HonuaHeatmap component`
- `fix: Resolve memory leak in ComponentBus`
- `docs: Update getting started guide`

### PR Description Template

```markdown
## Description
Brief description of changes.

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Related Issues
Closes #123

## Testing
Describe how you tested the changes.

## Screenshots
If applicable, add screenshots.

## Checklist
- [ ] Tests pass
- [ ] Documentation updated
- [ ] Code follows style guidelines
- [ ] No new warnings
```

### Review Process

1. **Automated checks** run (tests, linting)
2. **Code review** by maintainer
3. **Address feedback** if requested
4. **Approval and merge**

## Release Process

Releases are managed by maintainers. If you're interested in the process:

1. Update version in `.csproj`
2. Update CHANGELOG.md
3. Create release notes
4. Tag release
5. Build and publish NuGet package
6. Update documentation

## Questions?

- **GitHub Discussions**: https://github.com/honua/Honua.Server/discussions
- **Email**: support@honua.io

---

**Thank you for contributing to Honua.MapSDK!**
