# Contributing to Honua Server

Thank you for your interest in contributing to Honua Server! This guide will help you get started with development, understand our coding standards, and navigate the contribution process.

## Table of Contents

- [Project Overview](#project-overview)
- [Architecture](#architecture)
- [Development Prerequisites](#development-prerequisites)
- [Local Development Setup](#local-development-setup)
- [Code Style and Conventions](#code-style-and-conventions)
- [Branch Naming Conventions](#branch-naming-conventions)
- [Commit Message Guidelines](#commit-message-guidelines)
- [Pull Request Process](#pull-request-process)
- [Testing Requirements](#testing-requirements)
- [Code Review Process](#code-review-process)
- [Common Development Tasks](#common-development-tasks)
- [Getting Help](#getting-help)

## Project Overview

Honua Server is a cloud-native geospatial server built on .NET 9, implementing OGC standards and Geoservices REST APIs. The project focuses on:

- **Standards Compliance**: Full implementation of OGC API Features/Tiles/Records, WFS, WMS, WCS, and STAC
- **Cloud-Native Architecture**: Designed for containerized deployment with Kubernetes support
- **High Performance**: Leveraging .NET 9 and NetTopologySuite for optimized geospatial operations
- **Multi-Provider Support**: PostgreSQL/PostGIS, MySQL, SQL Server, Oracle, Snowflake, BigQuery, MongoDB, Cosmos DB
- **Enterprise Features**: Real-time geofencing (GeoEvent), data transformation (GeoETL), distributed geoprocessing

## Architecture

### High-Level Architecture

```
src/
├── Honua.Server.Core/              # Core services (query engine, geometry processing)
├── Honua.Server.Core.Raster/       # Raster data support (GDAL integration)
├── Honua.Server.Core.OData/        # OData protocol implementation
├── Honua.Server.Core.Cloud/        # Cloud provider SDKs (AWS, Azure, GCP)
├── Honua.Server.Host/              # Main application entry point
├── Honua.Server.Enterprise/        # Enterprise features (geoprocessing, licensing)
├── Honua.MapSDK/                   # Blazor mapping components
├── Honua.Admin.Blazor/             # Admin portal UI
├── HonuaField/                     # Mobile field app (.NET MAUI)
├── Honua.Server.Intake/            # GeoETL and container registry
├── Honua.Server.AlertReceiver/     # Cloud event webhooks
├── Honua.Server.Gateway/           # API gateway
└── Honua.Cli/                      # Command-line tools
```

### Key Technologies

- **ASP.NET Core**: Minimal APIs for high-performance HTTP handling
- **NetTopologySuite**: Geometry operations and spatial algorithms
- **Dapper**: Lightweight data access with excellent performance
- **Polly**: Resilience policies (retry, circuit breaker, timeout)
- **OpenTelemetry**: Distributed tracing and metrics
- **Serilog**: Structured logging
- **GDAL**: Raster processing (Full variant only)
- **Redis**: Distributed caching
- **PostgreSQL/PostGIS**: Primary geospatial database

## Development Prerequisites

### Required Software

1. **.NET 9 SDK** (9.0 or later)
   - Download: https://dotnet.microsoft.com/download/dotnet/9.0
   - Verify: `dotnet --version`

2. **Docker Desktop** (for local development environment)
   - Download: https://www.docker.com/products/docker-desktop
   - Required for running PostgreSQL, Redis, and observability stack
   - Verify: `docker --version`

3. **Git**
   - Download: https://git-scm.com/downloads
   - Verify: `git --version`

### Recommended IDEs

Choose one of the following:

- **Visual Studio Code** (lightweight, cross-platform)
  - Install C# Dev Kit extension
  - See `.vscode/` for pre-configured settings

- **Visual Studio 2022** (Windows, full-featured)
  - Version 17.9 or later
  - Workload: ASP.NET and web development

- **JetBrains Rider** (cross-platform, powerful)
  - Version 2024.1 or later
  - Excellent .NET support

### Optional but Recommended

- **PostgreSQL Client** (for database inspection)
  - pgAdmin 4 or DBeaver
- **Redis Client** (for cache inspection)
  - RedisInsight or Another Redis Desktop Manager
- **Postman or Insomnia** (for API testing)
- **Git GUI** (GitKraken, Sourcetree, or GitHub Desktop)

## Local Development Setup

### Quick Setup (5 minutes)

Use our automated setup scripts:

**Linux/macOS:**
```bash
cd Honua.Server
./scripts/setup-dev.sh
```

**Windows PowerShell:**
```powershell
cd Honua.Server
.\scripts\setup-dev.ps1
```

These scripts will:
1. Check for required dependencies
2. Install git hooks for code quality
3. Start Docker containers (PostgreSQL, Redis)
4. Restore NuGet packages
5. Build the solution
6. Run database migrations
7. Run tests to verify setup

### Manual Setup

If you prefer to set up manually or the scripts fail:

1. **Clone the repository:**
   ```bash
   git clone https://github.com/honua-io/Honua.Server.git
   cd Honua.Server
   ```

2. **Install pre-commit hooks:**
   ```bash
   # Linux/macOS
   ./scripts/install-hooks.sh

   # Windows
   .\scripts\install-hooks.ps1
   ```

3. **Start infrastructure services:**
   ```bash
   docker compose up -d postgres redis
   ```

4. **Restore dependencies and build:**
   ```bash
   dotnet restore
   dotnet build
   ```

5. **Run tests to verify setup:**
   ```bash
   dotnet test --filter "Category=Unit"
   ```

6. **Start the application:**
   ```bash
   dotnet run --project src/Honua.Server.Host
   ```

   The API will be available at `http://localhost:8080`

### Environment Configuration

Create a `.env.local` file in the project root (copy from `.env.template`):

```bash
# Database
ConnectionStrings__DefaultConnection=Host=localhost;Database=honua;Username=honua;Password=honua_dev_password

# Cache
HONUA__CACHE__PROVIDER=redis
HONUA__CACHE__REDIS__CONNECTIONSTRING=localhost:6379

# Authentication
HONUA__AUTHENTICATION__MODE=Local
HONUA__AUTHENTICATION__JWT__SECRET=dev-jwt-secret-change-in-production

# Logging
Serilog__MinimumLevel__Default=Information
Serilog__MinimumLevel__Override__Honua=Debug
```

## Code Style and Conventions

### EditorConfig

We use **EditorConfig** for consistent code formatting. The `.editorconfig` file defines:

- **Indentation**: 4 spaces for C#, 2 spaces for JSON/YAML
- **Line endings**: CRLF on Windows, LF on Unix
- **Charset**: UTF-8 with BOM for C# files
- **Brace style**: Allman style (opening brace on new line)
- **Naming conventions**: PascalCase for types/methods, interfaces prefixed with `I`

Modern IDEs automatically apply these settings. Verify with:
```bash
dotnet format --verify-no-changes
```

### Naming Conventions

#### C# Code

- **Classes, Structs, Enums**: `PascalCase`
  ```csharp
  public class FeatureCollection { }
  public enum GeometryType { }
  ```

- **Interfaces**: `IPascalCase` (prefixed with I)
  ```csharp
  public interface IDataProvider { }
  ```

- **Methods**: `PascalCase`
  ```csharp
  public async Task<FeatureCollection> GetFeaturesAsync() { }
  ```

- **Properties**: `PascalCase`
  ```csharp
  public string ConnectionString { get; set; }
  ```

- **Private fields**: `_camelCase` (prefixed with underscore)
  ```csharp
  private readonly ILogger _logger;
  ```

- **Constants**: `PascalCase`
  ```csharp
  public const int DefaultPageSize = 100;
  ```

- **Local variables and parameters**: `camelCase`
  ```csharp
  var featureCount = 0;
  public void Process(string layerId) { }
  ```

#### File Naming

- **C# files**: Match the primary type name
  - `FeatureService.cs`, `IDataProvider.cs`
- **Test files**: Append `Tests` to the class under test
  - `FeatureServiceTests.cs`
- **Configuration files**: Lowercase with hyphens
  - `docker-compose.yml`, `appsettings.json`

### Code Documentation

- **Public APIs**: Require XML documentation comments
  ```csharp
  /// <summary>
  /// Retrieves features from the specified layer with optional filtering.
  /// </summary>
  /// <param name="layerId">The unique identifier of the layer.</param>
  /// <param name="filter">Optional CQL2 filter expression.</param>
  /// <returns>A collection of features matching the criteria.</returns>
  public async Task<FeatureCollection> GetFeaturesAsync(string layerId, string? filter = null)
  ```

- **Complex logic**: Add inline comments explaining the "why", not the "what"
  ```csharp
  // Use spatial index hint for PostgreSQL to optimize large geometry queries
  var query = $"SELECT * FROM {tableName} /*+ INDEX(spatial_idx) */ WHERE ...";
  ```

### Code Quality Rules

Our `.editorconfig` enforces:

- **Security**: SQL injection, XSS, weak crypto algorithms (CA3001, CA5350, etc.)
- **Performance**: Async/await patterns, string operations, collection usage (CA1826, CA1834, etc.)
- **Maintainability**: Proper exception handling, dispose patterns, code complexity
- **Best Practices**: CancellationToken forwarding, ConfigureAwait usage

Run static analysis:
```bash
dotnet build /p:TreatWarningsAsErrors=true
```

### Pre-commit Hooks

Our pre-commit hook automatically runs:
1. **Code formatting check** (`dotnet format --verify-no-changes`)
2. **Build** (`dotnet build`)
3. **Unit tests** (`dotnet test --filter "Category=Unit"`)

To bypass hooks (not recommended):
```bash
git commit --no-verify
```

## Branch Naming Conventions

Use descriptive branch names following this pattern:

```
<type>/<short-description>
```

### Branch Types

- `feature/` - New features or enhancements
  - `feature/add-wfs3-support`
  - `feature/mongodb-provider`

- `fix/` - Bug fixes
  - `fix/geometry-precision-error`
  - `fix/memory-leak-in-cache`

- `refactor/` - Code refactoring without changing functionality
  - `refactor/simplify-query-builder`
  - `refactor/extract-authentication-service`

- `docs/` - Documentation updates
  - `docs/update-api-reference`
  - `docs/add-deployment-guide`

- `test/` - Adding or updating tests
  - `test/add-integration-tests-for-tiles`
  - `test/improve-unit-test-coverage`

- `perf/` - Performance improvements
  - `perf/optimize-tile-generation`
  - `perf/improve-query-performance`

- `chore/` - Maintenance tasks, dependency updates
  - `chore/update-dependencies`
  - `chore/cleanup-unused-code`

### Examples

✅ Good:
- `feature/add-ogc-api-processes`
- `fix/null-reference-in-postgis-provider`
- `refactor/consolidate-geometry-validators`

❌ Bad:
- `my-feature` (no type prefix)
- `fix-bug` (too vague)
- `feature/add-new-stuff` (not descriptive)

## Commit Message Guidelines

We follow the **Conventional Commits** specification:

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Commit Types

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, no logic change)
- `refactor`: Code refactoring
- `perf`: Performance improvements
- `test`: Adding or updating tests
- `chore`: Maintenance tasks
- `ci`: CI/CD changes
- `build`: Build system changes

### Commit Scope (Optional)

Indicates the area affected:
- `core`: Core services
- `api`: API endpoints
- `db`: Database layer
- `cache`: Caching
- `auth`: Authentication/Authorization
- `tiles`: Tile generation
- `geoprocessing`: Geoprocessing operations
- `docs`: Documentation

### Commit Message Rules

1. **Subject line** (first line):
   - Use imperative mood ("add" not "added" or "adds")
   - Don't capitalize first letter
   - No period at the end
   - Maximum 72 characters
   - Be specific and concise

2. **Body** (optional):
   - Separate from subject with blank line
   - Explain what and why, not how
   - Wrap at 72 characters

3. **Footer** (optional):
   - Reference issues: `Fixes #123`, `Closes #456`
   - Note breaking changes: `BREAKING CHANGE: description`

### Examples

✅ Good:
```
feat(tiles): add MVT generation for large datasets

Implement Mapbox Vector Tile generation with spatial indexing
and efficient geometry simplification for zoom levels 0-14.

Closes #234
```

```
fix(auth): prevent JWT token expiration race condition

When tokens expire during request processing, the middleware
now properly handles token refresh without throwing exceptions.

Fixes #567
```

```
docs: update deployment guide with Kubernetes examples

Add comprehensive examples for deploying to EKS, GKE, and AKS
with Helm charts and best practices for production environments.
```

❌ Bad:
```
Fixed bug                              # Too vague
Added new feature for tiles            # Not imperative mood
feat: Add tiles. Also fix auth bug.    # Multiple changes in one commit
```

### Commit Frequency

- Commit early and often
- Each commit should be a logical, atomic unit of work
- Don't commit broken code (use branches or stash)
- Squash "work in progress" commits before creating PR

## Pull Request Process

### Before Creating a PR

1. **Ensure all tests pass:**
   ```bash
   dotnet test
   ```

2. **Run code formatting:**
   ```bash
   dotnet format
   ```

3. **Update documentation:**
   - Update README if adding new features
   - Add/update XML documentation comments
   - Update CHANGELOG if applicable

4. **Rebase on latest main:**
   ```bash
   git fetch origin
   git rebase origin/main
   ```

5. **Self-review your changes:**
   - Read through the diff
   - Remove debug code, console logs
   - Check for TODOs or commented code

### Creating the PR

1. **Push your branch:**
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Open PR on GitHub**
   - Use descriptive title following commit message conventions
   - Fill out the PR template completely
   - Link related issues

3. **PR Title Format:**
   ```
   feat(tiles): Add MVT generation for large datasets
   ```

4. **PR Description Template:**
   ```markdown
   ## Summary
   Brief description of changes and motivation

   ## Changes
   - List specific changes
   - Breaking changes (if any)

   ## Testing
   - How was this tested?
   - What test cases were added?

   ## Screenshots (if applicable)

   ## Checklist
   - [ ] Tests added/updated
   - [ ] Documentation updated
   - [ ] Code follows style guidelines
   - [ ] All CI checks passing
   - [ ] Breaking changes documented

   Fixes #123
   ```

### PR Size Guidelines

- **Small PRs are better**: Aim for < 400 lines changed
- **Single purpose**: One feature or fix per PR
- **Break up large changes**: Use feature flags or multiple PRs
- **Exception**: Generated code, migrations, or large refactorings may exceed limits

### Review Process

1. **Automated checks must pass:**
   - Build
   - Tests (unit, integration)
   - Code coverage (minimum 60%)
   - Code analysis (no warnings)

2. **Reviewer approval required:**
   - At least 1 approval from maintainers
   - Address all review comments
   - Re-request review after changes

3. **Common review feedback:**
   - Code clarity and readability
   - Test coverage and quality
   - Performance considerations
   - Security implications
   - Documentation completeness

4. **After approval:**
   - Squash commits if needed
   - Maintainer will merge

### Merge Strategy

- **Squash and merge**: Default for feature branches
- **Rebase and merge**: For small, clean commits
- **Merge commit**: For release branches or large features

## Testing Requirements

### Test Categories

We use test categories for different test types:

```csharp
[TestClass]
[TestCategory("Unit")]
public class FeatureServiceTests { }

[TestClass]
[TestCategory("Integration")]
public class PostgresDataProviderTests { }

[TestClass]
[TestCategory("OGC")]
public class OgcConformanceTests { }
```

### Running Tests

```bash
# All tests
dotnet test

# Unit tests only (fast)
dotnet test --filter "Category=Unit"

# Integration tests (requires Docker)
dotnet test --filter "Category=Integration"

# Specific test class
dotnet test --filter "FullyQualifiedName~FeatureServiceTests"

# With code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Test Coverage Requirements

Minimum coverage thresholds:
- **Honua.Server.Core**: 65%
- **Honua.Server.Host**: 60%
- **Overall**: 60%

Check coverage:
```bash
./scripts/check-coverage.sh
```

### Writing Tests

**Unit Test Example:**
```csharp
[TestClass]
[TestCategory("Unit")]
public class GeometryValidatorTests
{
    [TestMethod]
    public void ValidateGeometry_ValidPoint_ReturnsTrue()
    {
        // Arrange
        var validator = new GeometryValidator();
        var point = new Point(10, 20);

        // Act
        var result = validator.ValidateGeometry(point);

        // Assert
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ValidateGeometry_NullGeometry_ThrowsException()
    {
        // Arrange
        var validator = new GeometryValidator();

        // Act
        validator.ValidateGeometry(null!);
    }
}
```

**Integration Test Example:**
```csharp
[TestClass]
[TestCategory("Integration")]
public class PostgresDataProviderTests : IDisposable
{
    private readonly PostgresDataProvider _provider;
    private readonly TestDatabase _testDb;

    public PostgresDataProviderTests()
    {
        _testDb = new TestDatabase();
        _provider = new PostgresDataProvider(_testDb.ConnectionString);
    }

    [TestMethod]
    public async Task GetFeatures_WithFilter_ReturnsFilteredResults()
    {
        // Arrange
        await _testDb.SeedDataAsync();
        var filter = "population > 10000";

        // Act
        var features = await _provider.GetFeaturesAsync("cities", filter);

        // Assert
        Assert.IsTrue(features.All(f => f.Properties["population"] > 10000));
    }

    public void Dispose()
    {
        _testDb?.Dispose();
        _provider?.Dispose();
    }
}
```

### Test Best Practices

1. **Follow AAA pattern**: Arrange, Act, Assert
2. **Test one thing**: Each test should verify a single behavior
3. **Descriptive names**: `MethodName_Scenario_ExpectedOutcome`
4. **Use test data builders**: For complex object setup
5. **Mock external dependencies**: Use interfaces and dependency injection
6. **Clean up resources**: Implement `IDisposable` for integration tests
7. **Avoid test interdependence**: Each test should run independently

## Code Review Process

### For Authors

1. **Prepare for review:**
   - Ensure CI passes
   - Self-review the diff
   - Add inline PR comments to explain complex changes
   - Highlight areas needing extra attention

2. **Respond to feedback:**
   - Address all comments (or explain why not)
   - Use "Request changes" comments as blocking
   - Use "Comment" as suggestions or questions
   - Push additional commits (don't force push during review)
   - Mark conversations as resolved once addressed

3. **After approval:**
   - Wait for final CI run to complete
   - Squash work-in-progress commits if needed
   - Maintainer will merge when ready

### For Reviewers

1. **Review checklist:**
   - [ ] Code follows project conventions
   - [ ] Tests cover new functionality
   - [ ] No obvious bugs or security issues
   - [ ] Performance implications considered
   - [ ] Documentation updated
   - [ ] Breaking changes are documented
   - [ ] Error handling is appropriate

2. **Provide constructive feedback:**
   - Be specific and actionable
   - Explain the "why" behind suggestions
   - Distinguish between "must fix" and "nice to have"
   - Acknowledge good work
   - Use GitHub suggestions for small fixes

3. **Review promptly:**
   - Aim to review within 1 business day
   - Use "Request changes" sparingly
   - Approve once all major concerns addressed

## Common Development Tasks

### Adding a New API Endpoint

1. **Define the route in the appropriate controller:**
   ```csharp
   app.MapGet("/api/features/{layerId}", async (
       string layerId,
       IFeatureService featureService,
       CancellationToken ct) =>
   {
       var features = await featureService.GetFeaturesAsync(layerId, ct);
       return Results.Ok(features);
   })
   .WithName("GetFeatures")
   .WithTags("Features")
   .Produces<FeatureCollection>(200)
   .Produces(404);
   ```

2. **Add service implementation:**
   ```csharp
   public class FeatureService : IFeatureService
   {
       public async Task<FeatureCollection> GetFeaturesAsync(
           string layerId, CancellationToken ct)
       {
           // Implementation
       }
   }
   ```

3. **Register service in DI:**
   ```csharp
   services.AddScoped<IFeatureService, FeatureService>();
   ```

4. **Add tests:**
   - Unit tests for service logic
   - Integration tests for end-to-end flow

### Adding a New Data Provider

1. **Implement `IDataProvider` interface:**
   ```csharp
   public class CustomDataProvider : IDataProvider
   {
       public async Task<FeatureCollection> GetFeaturesAsync(
           string layerId, QueryParameters parameters, CancellationToken ct)
       {
           // Implementation
       }
       // ... other interface methods
   }
   ```

2. **Add provider registration:**
   ```csharp
   services.AddDataProvider<CustomDataProvider>("custom");
   ```

3. **Add configuration section:**
   ```json
   {
     "DataProviders": {
       "Custom": {
         "ConnectionString": "...",
         "Options": {}
       }
     }
   }
   ```

4. **Add tests and documentation**

### Adding a New Authentication Method

1. **Implement authentication handler:**
   ```csharp
   public class CustomAuthHandler : AuthenticationHandler<CustomAuthOptions>
   {
       protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
       {
           // Implementation
       }
   }
   ```

2. **Register in authentication pipeline:**
   ```csharp
   services.AddAuthentication()
       .AddScheme<CustomAuthOptions, CustomAuthHandler>("Custom", null);
   ```

3. **Add configuration and tests**

### Debugging Failed Tests

1. **Run the specific failing test:**
   ```bash
   dotnet test --filter "FullyQualifiedName~YourTestName"
   ```

2. **Enable detailed logging:**
   ```bash
   dotnet test --logger "console;verbosity=detailed"
   ```

3. **Attach debugger:**
   - VS Code: Use `.vscode/launch.json` test configuration
   - Visual Studio: Right-click test → Debug Test
   - Rider: Click debug icon next to test

4. **Check test output:**
   - Review assertion messages
   - Check exception stack traces
   - Verify test data setup

### Updating Dependencies

1. **Check for outdated packages:**
   ```bash
   dotnet list package --outdated
   ```

2. **Update specific package:**
   ```bash
   dotnet add package PackageName --version x.y.z
   ```

3. **Update all packages (carefully):**
   ```bash
   # Update in test project first
   cd tests/Honua.Server.Core.Tests
   dotnet add package Moq --version latest

   # Run tests
   dotnet test

   # If successful, update in main projects
   ```

4. **Update and test:**
   ```bash
   dotnet restore
   dotnet build
   dotnet test
   ```

5. **Commit dependency updates separately:**
   ```bash
   git commit -m "chore: update Moq to version 4.20.0"
   ```

### Adding New Configuration Options

1. **Define configuration class:**
   ```csharp
   public class CustomFeatureOptions
   {
       public bool Enabled { get; set; }
       public int MaxItems { get; set; } = 100;
   }
   ```

2. **Add to appsettings.json:**
   ```json
   {
     "Honua": {
       "CustomFeature": {
         "Enabled": true,
         "MaxItems": 100
       }
     }
   }
   ```

3. **Register in DI:**
   ```csharp
   services.Configure<CustomFeatureOptions>(
       configuration.GetSection("Honua:CustomFeature"));
   ```

4. **Inject and use:**
   ```csharp
   public class MyService
   {
       private readonly CustomFeatureOptions _options;

       public MyService(IOptions<CustomFeatureOptions> options)
       {
           _options = options.Value;
       }
   }
   ```

## Getting Help

### Resources

- **Documentation**: [docs/README.md](docs/README.md)
- **Quick Start**: [docs/development/quick-start.md](docs/development/quick-start.md)
- **Debugging Guide**: [docs/development/debugging.md](docs/development/debugging.md)
- **API Documentation**: [docs/api/README.md](docs/api/README.md)
- **Architecture**: [docs/architecture/README.md](docs/architecture/README.md)

### Community

- **GitHub Discussions**: Ask questions, share ideas
  - https://github.com/honua-io/Honua.Server/discussions

- **GitHub Issues**: Report bugs, request features
  - https://github.com/honua-io/Honua.Server/issues

### Maintainers

When you need direct help from maintainers:
1. Check existing documentation first
2. Search closed issues for similar problems
3. Ask in GitHub Discussions
4. If it's a bug or feature request, create an issue

### Code of Conduct

We are committed to providing a welcoming and inclusive environment. Please:
- Be respectful and considerate
- Welcome newcomers and help them learn
- Accept constructive criticism gracefully
- Focus on what's best for the community

---

## License

By contributing, you agree that your contributions will be licensed under the same [Elastic License 2.0](LICENSE) as the rest of the project.

---

**Thank you for contributing to Honua Server!** 🌍
