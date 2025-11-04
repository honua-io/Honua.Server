# HonuaIO Architecture Quick Reference

**Quick Start Guide for Developers**

---

## 🏗️ Project Structure at a Glance

```
HonuaIO/
├── src/
│   ├── Honua.Server.Core/          ← Core logic (NO DEPENDENCIES)
│   ├── Honua.Server.Host/          ← Web API entry point
│   ├── Honua.Server.Enterprise/    ← Big data DB connectors
│   ├── Honua.Cli/                  ← CLI application
│   ├── Honua.Cli.AI/               ← AI/LLM integration
│   ├── Honua.Cli.AI.Secrets/       ← Secrets management
│   └── Honua.Server.AlertReceiver/ ← Standalone microservice
├── tests/
└── docs/
    └── architecture/               ← YOU ARE HERE
```

---

## 📋 Dependency Rules (MUST FOLLOW)

### ✅ Allowed Dependencies

```
Honua.Cli
  ├─→ Honua.Cli.AI          ✅
  ├─→ Honua.Cli.AI.Secrets  ✅
  └─→ Honua.Server.Core     ✅

Honua.Cli.AI
  ├─→ Honua.Cli.AI.Secrets  ✅
  └─→ Honua.Server.Core     ✅

Honua.Server.Host
  └─→ Honua.Server.Core     ✅

Honua.Server.Enterprise
  └─→ Honua.Server.Core     ✅

Honua.Server.Core
  └─→ (no Honua.* projects) ✅
```

### ❌ Forbidden Dependencies

```
Honua.Server.Core  ─X→ ANY Honua.* project
Honua.Cli.AI       ─X→ Honua.Cli
Honua.Server.Host  ─X→ Honua.Cli.*
Any circular references at all!
```

---

## 🎯 Where Does My Code Go?

### Decision Tree

```
START HERE
    │
    ├─ Is it business logic or data access?
    │    └─→ YES: Honua.Server.Core/
    │
    ├─ Is it a Web API endpoint or HTTP concern?
    │    └─→ YES: Honua.Server.Host/
    │
    ├─ Is it AI/LLM functionality?
    │    └─→ YES: Honua.Cli.AI/
    │
    ├─ Is it a CLI command?
    │    └─→ YES: Honua.Cli/Commands/
    │
    ├─ Is it enterprise database support?
    │    └─→ YES: Honua.Server.Enterprise/
    │
    └─ Is it secrets/encryption?
         └─→ YES: Honua.Cli.AI.Secrets/
```

### Common Scenarios

**Scenario: Adding a new database provider**
```
Location: src/Honua.Server.Core/Data/{ProviderName}/
Files:
  - {Provider}DataStoreProvider.cs (implements IDataStoreProvider)
  - {Provider}DataStoreCapabilities.cs (implements IDataStoreCapabilities)
  - {Provider}QueryBuilder.cs (optional helper)
Register in: ServiceCollectionExtensions.cs
```

**Scenario: Adding a new OGC endpoint**
```
Location: src/Honua.Server.Host/Ogc/
Files:
  - Ogc{Feature}Handlers.cs (static handler methods)
  - Ogc{Feature}Models.cs (request/response DTOs)
Register in: OgcApiEndpointExtensions.cs
```

**Scenario: Adding a new CLI command**
```
Location: src/Honua.Cli/Commands/
File: {CommandName}Command.cs (inherits AsyncCommand<Settings>)
Register in: Program.cs with app.AddCommand<>()
```

**Scenario: Adding a new AI agent**
```
Location: src/Honua.Cli.AI/Services/Agents/Specialized/
File: {Agent}Agent.cs
Register in: HonuaAgentFactory.cs
```

---

## 🔧 Common Design Patterns

### Repository Pattern

```csharp
// Interface in Core
public interface IFeatureRepository
{
    Task<Feature> GetByIdAsync(string id);
    Task<IEnumerable<Feature>> QueryAsync(FeatureQuery query);
}

// Implementation in Core
public class FeatureRepository : IFeatureRepository
{
    private readonly IDataStoreProviderFactory _factory;

    public FeatureRepository(IDataStoreProviderFactory factory)
    {
        _factory = factory;
    }
}

// Registration in DI
services.AddSingleton<IFeatureRepository, FeatureRepository>();
```

### Strategy Pattern (Provider)

```csharp
// 1. Define interface
public interface IRasterSourceProvider
{
    string ProviderType { get; }
    Task<Stream> ReadAsync(RasterSourceDefinition source);
}

// 2. Create implementations
public class S3RasterSourceProvider : IRasterSourceProvider
{
    public string ProviderType => "s3";
    // Implementation...
}

public class AzureBlobRasterSourceProvider : IRasterSourceProvider
{
    public string ProviderType => "azureblob";
    // Implementation...
}

// 3. Register all providers
services.AddSingleton<IRasterSourceProvider, S3RasterSourceProvider>();
services.AddSingleton<IRasterSourceProvider, AzureBlobRasterSourceProvider>();

// 4. Use via factory/registry
public class RasterSourceProviderRegistry
{
    private readonly IEnumerable<IRasterSourceProvider> _providers;

    public IRasterSourceProvider GetProvider(string type)
        => _providers.First(p => p.ProviderType == type);
}
```

### Factory Pattern

```csharp
public interface IDataStoreProviderFactory
{
    IDataStoreProvider GetProvider(string providerKey);
}

public class DataStoreProviderFactory : IDataStoreProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public IDataStoreProvider GetProvider(string providerKey)
    {
        return _serviceProvider.GetRequiredKeyedService<IDataStoreProvider>(providerKey);
    }
}
```

### Options Pattern (Configuration)

```csharp
// 1. Define options class
public class MyServiceOptions
{
    public const string SectionName = "MyService";

    public string ApiKey { get; set; }
    public int Timeout { get; set; }
}

// 2. Register in DI
services.Configure<MyServiceOptions>(
    configuration.GetSection(MyServiceOptions.SectionName)
);

// 3. Use in class
public class MyService
{
    private readonly MyServiceOptions _options;

    public MyService(IOptions<MyServiceOptions> options)
    {
        _options = options.Value;
    }
}

// 4. appsettings.json
{
  "MyService": {
    "ApiKey": "...",
    "Timeout": 30
  }
}
```

---

## 🧪 Testing Guidelines

### Test Organization

```
Tests should mirror production structure:

Production:  src/Honua.Server.Core/Data/FeatureRepository.cs
Test:        tests/Honua.Server.Core.Tests/Data/FeatureRepositoryTests.cs

Production:  src/Honua.Cli.AI/Services/Agents/HonuaAgentFactory.cs
Test:        tests/Honua.Cli.AI.Tests/Services/Agents/HonuaAgentFactoryTests.cs
```

### Test Naming Convention

```csharp
public class FeatureRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WhenFeatureExists_ReturnsFeature()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetByIdAsync("123");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFeatureDoesNotExist_ReturnsNull()
    {
        // Arrange, Act, Assert
    }
}
```

**Naming Pattern:** `MethodName_Condition_ExpectedResult`

### Test Types

```
Unit Tests:        Test single class in isolation (mock dependencies)
Integration Tests: Test interaction between multiple components
E2E Tests:        Test full system workflow
```

---

## 🔐 Security Best Practices

### Do's ✅

```csharp
// ✅ DO: Use dependency injection
public class MyService
{
    public MyService(IPasswordHasher hasher) { }
}

// ✅ DO: Use IOptions for configuration
public MyService(IOptions<MyOptions> options) { }

// ✅ DO: Use async/await
public async Task<Result> ProcessAsync() { }

// ✅ DO: Validate input
public IResult GetFeature(string id)
{
    if (string.IsNullOrEmpty(id))
        return Results.BadRequest("ID is required");
}

// ✅ DO: Use authorized endpoints
app.MapGet("/admin/config", () => { }).RequireAuthorization("Admin");
```

### Don'ts ❌

```csharp
// ❌ DON'T: Hardcode secrets
var apiKey = "sk-1234567890"; // WRONG!

// ❌ DON'T: Use blocking calls
var result = SomeAsyncMethod().Result; // WRONG! Use await

// ❌ DON'T: Expose internal details in responses
return Results.Ok(new { error = exception.StackTrace }); // WRONG!

// ❌ DON'T: Create circular dependencies
// Honua.Server.Core -> Honua.Server.Host // WRONG!

// ❌ DON'T: Use magic strings
var value = config["SomeKey"]; // WRONG! Use IOptions<T>
```

---

## 📝 Configuration Guidelines

### Adding New Configuration

**Step 1:** Create Options class
```csharp
// src/Honua.Server.Core/Configuration/MyFeatureOptions.cs
public class MyFeatureOptions
{
    public const string SectionName = "honua:myfeature";

    public bool Enabled { get; set; }
    public string ConnectionString { get; set; }
}
```

**Step 2:** Add to appsettings.json
```json
{
  "honua": {
    "myfeature": {
      "enabled": true,
      "connectionString": "..."
    }
  }
}
```

**Step 3:** Register in DI
```csharp
services.Configure<MyFeatureOptions>(
    configuration.GetSection(MyFeatureOptions.SectionName)
);
```

**Step 4:** Use in service
```csharp
public class MyFeature
{
    private readonly MyFeatureOptions _options;

    public MyFeature(IOptions<MyFeatureOptions> options)
    {
        _options = options.Value;
    }
}
```

---

## 🌐 Adding New API Endpoints

### Minimal API Pattern (Recommended)

```csharp
// In src/Honua.Server.Host/MyFeature/MyFeatureEndpoints.cs
public static class MyFeatureEndpoints
{
    public static IEndpointRouteBuilder MapMyFeatureEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/myfeature")
            .WithTags("MyFeature")
            .RequireAuthorization(); // If auth required

        group.MapGet("", GetAll)
            .WithName("GetAllMyFeatures")
            .Produces<MyFeatureResponse[]>();

        group.MapGet("/{id}", GetById)
            .WithName("GetMyFeatureById")
            .Produces<MyFeatureResponse>()
            .Produces(404);

        group.MapPost("", Create)
            .WithName("CreateMyFeature")
            .Produces<MyFeatureResponse>(201);

        return endpoints;
    }

    private static async Task<IResult> GetAll(
        IMyFeatureService service,
        CancellationToken cancellationToken)
    {
        var results = await service.GetAllAsync(cancellationToken);
        return Results.Ok(results);
    }

    private static async Task<IResult> GetById(
        string id,
        IMyFeatureService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static async Task<IResult> Create(
        MyFeatureRequest request,
        IMyFeatureService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/myfeature/{result.Id}", result);
    }
}

// In Program.cs
app.MapMyFeatureEndpoints();
```

---

## 🎨 Code Style Guidelines

### Naming Conventions

```csharp
// Interfaces: PascalCase with 'I' prefix
public interface IFeatureRepository { }

// Classes: PascalCase
public class FeatureRepository { }

// Methods: PascalCase
public async Task<Result> ProcessAsync() { }

// Parameters/variables: camelCase
public void Method(string parameterName) { }

// Private fields: _camelCase
private readonly IService _service;

// Constants: PascalCase
public const string DefaultValue = "value";

// Async methods: end with 'Async'
public async Task<Result> GetResultAsync() { }
```

### File Organization

```csharp
// 1. Using statements (organized, remove unused)
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;

// 2. Namespace (match folder structure)
namespace Honua.Server.Core.Features;

// 3. Class definition
public class MyFeature
{
    // 4. Constants
    private const string DefaultName = "Default";

    // 5. Fields
    private readonly IDataStoreProvider _dataStore;
    private readonly ILogger<MyFeature> _logger;

    // 6. Constructor
    public MyFeature(IDataStoreProvider dataStore, ILogger<MyFeature> logger)
    {
        _dataStore = dataStore;
        _logger = logger;
    }

    // 7. Public methods
    public async Task<Result> ExecuteAsync()
    {
        // Implementation
    }

    // 8. Private methods
    private void Helper()
    {
        // Implementation
    }
}
```

---

## 🚀 Performance Best Practices

### Async/Await

```csharp
// ✅ DO: Use async all the way
public async Task<Result> ProcessAsync()
{
    var data = await _repository.GetAsync();
    return await TransformAsync(data);
}

// ❌ DON'T: Block on async
public Result Process()
{
    var data = _repository.GetAsync().Result; // DEADLOCK RISK!
    return Transform(data);
}
```

### IAsyncEnumerable for Streaming

```csharp
// ✅ DO: Use IAsyncEnumerable for large datasets
public async IAsyncEnumerable<Feature> StreamFeaturesAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (var feature in _dataStore.QueryAsync(cancellationToken))
    {
        yield return feature;
    }
}

// ❌ DON'T: Load everything into memory
public async Task<List<Feature>> GetAllFeaturesAsync()
{
    return await _dataStore.QueryAsync().ToListAsync(); // OOM risk!
}
```

### Caching

```csharp
// ✅ DO: Use IMemoryCache for expensive operations
public async Task<Result> GetExpensiveDataAsync(string key)
{
    if (!_cache.TryGetValue(key, out Result result))
    {
        result = await ComputeExpensiveAsync();
        _cache.Set(key, result, TimeSpan.FromMinutes(10));
    }
    return result;
}
```

---

## 🔍 Observability

### Logging

```csharp
// ✅ DO: Use structured logging
_logger.LogInformation(
    "Processing feature {FeatureId} for user {UserId}",
    featureId,
    userId
);

// ❌ DON'T: Use string concatenation
_logger.LogInformation($"Processing feature {featureId}"); // WRONG!
```

### Metrics

```csharp
// ✅ DO: Emit custom metrics
using var activity = ActivitySource.StartActivity("ProcessFeature");
activity?.SetTag("feature.id", featureId);
activity?.SetTag("feature.count", count);

// Track operation duration
_metrics.RecordProcessingDuration(duration);
```

### Error Handling

```csharp
// ✅ DO: Log exceptions with context
try
{
    await ProcessAsync();
}
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "Failed to process feature {FeatureId}",
        featureId
    );
    throw; // Re-throw if you can't handle
}
```

---

## 📚 Quick Links

**Architecture Documents:**
- [Full Architecture Review](./ARCHITECTURE_REVIEW_2025-10-17.md)
- [Architecture Metrics Dashboard](./ARCHITECTURE_METRICS.md)
- [Dependency Graph](./DEPENDENCY_GRAPH.md)
- [Circular Dependency Analysis](./CIRCULAR_DEPENDENCY_ANALYSIS.md)

**ADRs (Architecture Decision Records):**
- [ADR-0001: Authentication & RBAC](./ADR-0001-authentication-rbac.md)
- [ADR-0002: OpenRosa ODK Integration](./ADR-0002-openrosa-odk-integration.md)
- [ADR-0003: Dependency Management](./ADR-0003-dependency-management.md)

**Other Documentation:**
- [Testing Guide](../../docs/TESTING.md)
- [CI/CD Guide](../../docs/CI_CD.md)
- [README](../../README.md)

---

## ❓ FAQ

**Q: Can Honua.Server.Core reference Honua.Server.Host?**
A: ❌ **NO!** Core must never reference any Honua.* project. Use dependency inversion instead.

**Q: Where should I put shared DTOs?**
A: ✅ Put them in `Honua.Server.Core` if they're domain models, or in the project that owns them.

**Q: Can I add a dependency to an external NuGet package?**
A: ✅ Yes, but consider:
   - Is it truly needed?
   - Does it align with existing patterns?
   - Is it a stable, well-maintained package?

**Q: How do I test code that depends on external services?**
A: ✅ Use interfaces and mock them in tests. Example: `IDataStoreProvider`, `IRasterSourceProvider`

**Q: Should I use Controllers or Minimal APIs?**
A: ✅ **Minimal APIs** are preferred for new endpoints (see examples above).

**Q: How do I add configuration?**
A: ✅ Use the Options Pattern (see Configuration Guidelines above).

---

## 🆘 Need Help?

**Before adding code, ask yourself:**
1. ✅ Does it follow the dependency rules?
2. ✅ Does it use dependency injection?
3. ✅ Does it follow async/await patterns?
4. ✅ Is it properly configured (no hardcoded values)?
5. ✅ Does it have tests?

**If unsure:**
- Check existing similar code
- Review the Architecture Review document
- Ask the team
- Create an ADR for significant decisions

---

**Last Updated:** 2025-10-17
**Maintained By:** Architecture Team
