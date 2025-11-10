# Contributing to Honua.MapSDK

Thank you for your interest in contributing to Honua.MapSDK! This document provides guidelines and instructions for contributing to the project.

---

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Development Setup](#development-setup)
4. [Making Changes](#making-changes)
5. [Pull Request Process](#pull-request-process)
6. [Coding Standards](#coding-standards)
7. [Testing](#testing)
8. [Documentation](#documentation)

---

## Code of Conduct

### Our Pledge

We are committed to providing a welcoming and inclusive experience for everyone. We expect all contributors to:

- Use welcoming and inclusive language
- Be respectful of differing viewpoints and experiences
- Gracefully accept constructive criticism
- Focus on what is best for the community
- Show empathy towards other community members

### Unacceptable Behavior

- Harassment, trolling, or discriminatory comments
- Personal or political attacks
- Publishing others' private information without permission
- Other conduct which could reasonably be considered inappropriate

---

## Getting Started

### Ways to Contribute

- **Report Bugs**: Submit detailed bug reports
- **Suggest Features**: Propose new features or improvements
- **Fix Issues**: Pick up issues labeled `good-first-issue` or `help-wanted`
- **Improve Documentation**: Fix typos, add examples, clarify explanations
- **Write Tests**: Increase code coverage
- **Review Pull Requests**: Help review and test PRs from others

### Before You Start

1. **Search existing issues** to avoid duplicates
2. **Discuss major changes** by opening an issue first
3. **Check the roadmap** to align with project direction
4. **Read the documentation** to understand the architecture

---

## Development Setup

### Prerequisites

- .NET 8.0 SDK or higher
- Node.js 18+ (for JavaScript development)
- Git
- Visual Studio 2022, VS Code, or Rider

### Clone the Repository

```bash
git clone https://github.com/honua-io/Honua.Server.git
cd Honua.Server/src/Honua.MapSDK
```

### Install Dependencies

```bash
# Restore NuGet packages
dotnet restore

# Install JavaScript dependencies (if contributing to JS code)
npm install
```

### Build the Project

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Run the Demo Application

```bash
cd ../Honua.Admin.Blazor
dotnet run
```

Navigate to `https://localhost:5001` to see the demo.

---

## Making Changes

### 1. Create a Branch

Create a feature branch from `main`:

```bash
git checkout main
git pull origin main
git checkout -b feature/your-feature-name
```

**Branch Naming:**
- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation updates
- `refactor/` - Code refactoring
- `test/` - Test additions or modifications

### 2. Make Your Changes

- Write clean, readable code
- Follow the coding standards (see below)
- Add tests for new functionality
- Update documentation as needed
- Commit regularly with clear messages

### 3. Commit Your Changes

Use clear, descriptive commit messages:

```bash
git add .
git commit -m "Add histogram chart type to HonuaChart component"
```

**Commit Message Format:**
```
<type>: <subject>

<body>

<footer>
```

**Types:**
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation only
- `style:` - Code style (formatting, missing semicolons, etc.)
- `refactor:` - Code refactoring
- `test:` - Adding or modifying tests
- `chore:` - Maintenance tasks

**Example:**
```
feat: Add histogram chart type to HonuaChart

- Implement histogram binning algorithm
- Add Bins parameter to control bin count
- Update chart.js integration for histogram rendering
- Add unit tests for histogram calculations

Closes #123
```

### 4. Push Your Branch

```bash
git push origin feature/your-feature-name
```

---

## Pull Request Process

### Before Submitting

- [ ] Code builds without errors
- [ ] All tests pass
- [ ] New tests added for new functionality
- [ ] Documentation updated
- [ ] No merge conflicts with `main`
- [ ] Code follows project style guidelines

### Submit Pull Request

1. Go to GitHub repository
2. Click "New Pull Request"
3. Select your branch
4. Fill out the PR template:

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
How has this been tested?

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-reviewed the code
- [ ] Commented complex code
- [ ] Updated documentation
- [ ] Added tests
- [ ] All tests pass
```

5. Link related issues
6. Request review from maintainers

### Review Process

- Maintainers will review your PR
- Address any requested changes
- Once approved, your PR will be merged
- Your contribution will be credited in release notes

---

## Coding Standards

### C# Style Guide

Follow Microsoft's C# coding conventions with these additions:

#### Naming Conventions

```csharp
// Classes, Methods, Properties: PascalCase
public class HonuaMap { }
public void FlyToAsync() { }
public string MapId { get; set; }

// Private fields: _camelCase with underscore
private string _mapStyle;
private int _zoomLevel;

// Parameters, local variables: camelCase
public void UpdateExtent(double[] bounds, int zoom) { }

// Constants: PascalCase
public const int DefaultZoom = 10;
```

#### Code Organization

```csharp
// 1. Using statements
using System;
using Honua.MapSDK.Core;

// 2. Namespace
namespace Honua.MapSDK.Components;

// 3. Class with XML documentation
/// <summary>
/// Interactive map component powered by MapLibre GL JS
/// </summary>
public class HonuaMap : ComponentBase, IAsyncDisposable
{
    // 4. Parameters (public properties)
    [Parameter]
    public string Id { get; set; } = $"map-{Guid.NewGuid():N}";

    // 5. Injected services
    [Inject]
    private ComponentBus Bus { get; set; } = default!;

    // 6. Private fields
    private IJSObjectReference? _mapInstance;

    // 7. Lifecycle methods
    protected override async Task OnAfterRenderAsync(bool firstRender) { }

    // 8. Public methods
    public async Task FlyToAsync(double[] center, double? zoom) { }

    // 9. Private methods
    private void SetupSubscriptions() { }

    // 10. Dispose
    public async ValueTask DisposeAsync() { }
}
```

#### XML Documentation

All public members must have XML documentation:

```csharp
/// <summary>
/// Animates the map to a specific location
/// </summary>
/// <param name="center">Center coordinates [longitude, latitude]</param>
/// <param name="zoom">Target zoom level (optional)</param>
/// <returns>Task representing the async operation</returns>
public async Task FlyToAsync(double[] center, double? zoom = null)
{
    // Implementation
}
```

### Razor Component Style

```razor
@* 1. Using directives *@
@using Honua.MapSDK.Core
@using Honua.MapSDK.Core.Messages

@* 2. Implements/Inherits *@
@implements IAsyncDisposable

@* 3. Dependency injection *@
@inject ComponentBus Bus
@inject IJSRuntime JS

@* 4. HTML markup *@
<div class="honua-map @CssClass" @ref="_mapContainer">
    @ChildContent
</div>

@* 5. Code block *@
@code {
    // Parameters
    [Parameter]
    public string Id { get; set; } = $"map-{Guid.NewGuid():N}";

    // Fields and methods
}
```

### JavaScript Style

For JavaScript code in `wwwroot/js`:

```javascript
// Use ES6+ features
export function createMap(container, options, dotNetRef) {
    // Use const/let, not var
    const map = new maplibregl.Map({
        container: container,
        ...options
    });

    // Arrow functions
    map.on('load', () => {
        dotNetRef.invokeMethodAsync('OnMapReady');
    });

    return map;
}

// Document all exported functions
/**
 * Creates a MapLibre GL JS map instance
 * @param {HTMLElement} container - Map container element
 * @param {Object} options - Map initialization options
 * @param {Object} dotNetRef - .NET object reference for callbacks
 * @returns {maplibregl.Map} Map instance
 */
```

---

## Testing

### Unit Tests

Write unit tests for all new functionality:

```csharp
using Xunit;
using Honua.MapSDK.Components;

public class HonuaMapTests
{
    [Fact]
    public void MapId_DefaultValue_IsNotEmpty()
    {
        // Arrange
        var map = new HonuaMap();

        // Act
        var id = map.Id;

        // Assert
        Assert.NotEmpty(id);
        Assert.StartsWith("map-", id);
    }

    [Theory]
    [InlineData(-180, -90)]
    [InlineData(0, 0)]
    [InlineData(180, 90)]
    public void Center_ValidCoordinates_Accepted(double lon, double lat)
    {
        // Arrange & Act
        var map = new HonuaMap
        {
            Center = new[] { lon, lat }
        };

        // Assert
        Assert.Equal(lon, map.Center[0]);
        Assert.Equal(lat, map.Center[1]);
    }
}
```

### Integration Tests

Test component interactions:

```csharp
[Fact]
public async Task Map_And_DataGrid_Sync_WhenExtentChanges()
{
    // Arrange
    using var ctx = new TestContext();
    ctx.Services.AddHonuaMapSDK();

    var cut = ctx.RenderComponent<MapWithGrid>();
    var map = cut.FindComponent<HonuaMap>();

    // Act
    await map.Instance.FlyToAsync(new[] { -122.4, 37.7 }, 12);

    // Assert
    var grid = cut.FindComponent<HonuaDataGrid>();
    Assert.NotEmpty(grid.Instance.VisibleItems);
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportFormat=lcov

# Run specific test
dotnet test --filter "FullyQualifiedName~HonuaMapTests"
```

---

## Documentation

### Update Documentation

When adding new features:

1. **Update API docs** in `/docs/mapsdk/api/`
2. **Add examples** to component documentation
3. **Update README** if adding major features
4. **Update CHANGELOG** for release notes

### Documentation Style

- Use active voice
- Be concise and clear
- Include code examples
- Add screenshots where helpful
- Cross-reference related docs

**Example:**

```markdown
## FlyToAsync Method

Smoothly animates the map to a specified location.

### Parameters

- `center` (double[]): Target center coordinates [longitude, latitude]
- `zoom` (double?): Optional target zoom level

### Returns

Task representing the asynchronous operation.

### Example

\`\`\`csharp
await map.FlyToAsync(new[] { -122.4194, 37.7749 }, zoom: 13);
\`\`\`

### See Also

- [FitBoundsAsync](fitboundsasync.md)
- [Map Navigation Guide](../guides/map-navigation.md)
```

---

## Release Process

(For maintainers)

### Version Numbering

We follow [Semantic Versioning](https://semver.org/):

- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Creating a Release

1. Update version in `.csproj`
2. Update `CHANGELOG.md`
3. Create release branch: `release/v1.2.0`
4. Run all tests
5. Create GitHub release
6. Publish to NuGet
7. Merge to main
8. Tag release

---

## Getting Help

- **Questions**: [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
- **Bugs**: [GitHub Issues](https://github.com/honua-io/Honua.Server/issues)
- **Chat**: Join our Discord server (link in README)

---

## Recognition

All contributors will be recognized in:

- Release notes
- Contributors list
- Project README

Thank you for contributing to Honua.MapSDK!

---

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
