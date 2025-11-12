# Configuration V2 High Availability Analysis

## Executive Summary

Configuration V2 in Honua Server has **limited high availability support** in its current implementation. While the system provides foundational infrastructure for distributed scenarios (Redis cache configuration, change notifications), the core HclMetadataProvider lacks critical HA features such as file watching, hot-reload capability, and multi-instance coordination mechanisms.

---

## Investigation Results

### 1. File Watching & Hot-Reload Support

**Status**: NOT IMPLEMENTED

**Findings**:

#### HclMetadataProvider.cs Analysis
```csharp
public sealed class HclMetadataProvider : IMetadataProvider
{
    public bool SupportsChangeNotifications => false;  // <-- KEY FINDING
    
    public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;
    
    public Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        // Loads configuration once - NO watching mechanism
        var catalog = this.BuildCatalog();
        var folders = this.BuildFolders();
        // ... builds snapshot one time
        return Task.FromResult(snapshot);
    }
}
```

**Key Finding**: 
- `SupportsChangeNotifications` is hardcoded to `false`
- No FileSystemWatcher or similar monitoring
- Configuration is loaded once and never reloaded unless explicitly triggered
- The MetadataChanged event is defined but never raised

**Impact on HA**:
- Multiple instances cannot be notified of configuration changes automatically
- Configuration updates require manual server restart
- No mechanism for graceful reload in a multi-instance setup

#### IMetadataProvider Interface Analysis
The interface supports change notifications conceptually:
```csharp
public interface IMetadataProvider
{
    bool SupportsChangeNotifications { get; }
    event EventHandler<MetadataChangedEventArgs>? MetadataChanged;
}

public interface IReloadableMetadataProvider : IMetadataProvider
{
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
```

However:
- HclMetadataProvider does NOT implement IReloadableMetadataProvider
- No reload mechanism exists in the loader
- Change notification architecture is present but unused

---

### 2. Distributed Cache & Redis Integration

**Status**: PARTIAL/ASPIRATIONAL

**Findings**:

#### Configuration V2 Redis Support
The Configuration V2 schema includes Redis cache configuration:

```csharp
// From HonuaConfig.cs
public sealed record class CacheBlock
{
    public required string Id { get; init; }
    public required string Type { get; init; }  // "redis" or "memory"
    public bool Enabled { get; init; } = true;
    public string? Connection { get; init; }
    public List<string> RequiredIn { get; init; } = new();
}
```

Configuration Example:
```hcl
cache "redis" {
    type = "redis"
    enabled = true
    connection = "${env:REDIS_URL}"
    required_in = ["production"]
}
```

#### SemanticValidator.cs Redis Validation
```csharp
private void ValidateCacheReferences(HonuaConfig config, ValidationResult result)
{
    // Rate limit can use Redis
    if (config.RateLimit != null && config.RateLimit.Store.ToLowerInvariant() == "redis")
    {
        var hasRedisCache = config.Caches.Values
            .Any(c => c.Type.ToLowerInvariant() == "redis" && c.Enabled);
        
        if (!hasRedisCache)
        {
            result.AddWarning(
                "Rate limiting uses Redis but no Redis cache is defined or enabled",
                "rate_limit.store",
                "Define a Redis cache or use 'memory' store");
        }
    }
    
    // Production-specific check
    if (config.Honua.Environment.ToLowerInvariant() == "production")
    {
        var hasOnlyMemoryCache = config.Caches.Values
            .All(c => c.Type.ToLowerInvariant() == "memory");
        
        if (hasOnlyMemoryCache && config.Caches.Count > 0)
        {
            result.AddWarning(
                "Only in-memory caching is configured in production environment",
                "cache",
                "Consider using Redis for distributed caching in production");
        }
    }
}
```

**Key Findings**:
- Redis configuration is DECLARED but not actually USED by metadata system
- Configuration schema supports Redis, but HclMetadataProvider ignores it
- Rate limiting can use Redis, but metadata provider cannot
- Validation warns about using memory cache in production, suggesting Redis is intended

#### Legacy RedisMetadataProvider
From MetadataProviderMigration.cs, there's evidence of a deleted RedisMetadataProvider:
```csharp
// Metadata migration utility references RedisMetadataProvider
/// <example>
/// // Migrate from File to Redis
/// var fileProvider = new JsonMetadataProvider("./metadata.json");
/// var redisProvider = new RedisMetadataProvider(redis, options, logger);
/// await migration.MigrateAsync(fileProvider, redisProvider);
/// </example>

// Current status:
throw new NotSupportedException("JsonMetadataProvider has been removed. 
    Use HclMetadataProvider with Configuration V2 instead.");
```

**Impact on HA**:
- No distributed metadata store between instances
- Each instance loads configuration independently from file
- No shared state or change coordination
- Redis capability exists in schema but is NOT implemented for metadata

---

### 3. IChangeToken Usage in HclMetadataProvider

**Status**: NOT USED / INFRASTRUCTURE ONLY

**Findings**:

#### Change Token Architecture
```csharp
// From IMetadataRegistry.cs
public interface IMetadataRegistry
{
    /// Gets a change token that signals when metadata has been updated.
    IChangeToken GetChangeToken();
}

// From MetadataRegistry.cs
private CancellationTokenSource? _changeTokenSource = new();

public IChangeToken GetChangeToken()
{
    var source = Volatile.Read(ref _changeTokenSource) 
        ?? throw new ObjectDisposedException(nameof(MetadataRegistry));
    return new CancellationChangeToken(source.Token);
}

private void SignalSnapshotChanged()
{
    CancellationTokenSource? previous;
    lock (_changeTokenSync)
    {
        previous = _changeTokenSource;
        _changeTokenSource = new CancellationTokenSource();
    }
    
    if (previous is not null)
    {
        previous.Cancel();  // <-- Signals change to all listeners
    }
}
```

#### How It Works
1. MetadataRegistry maintains a CancellationChangeToken
2. When metadata changes, SignalSnapshotChanged() creates a new token
3. Old token is cancelled, notifying all listeners
4. Listeners can register callbacks: `registry.GetChangeToken().RegisterChangeCallback(...)`

#### Where Changes Are Signaled
```csharp
public async Task ReloadAsync(CancellationToken cancellationToken = default)
{
    // ...
    var snapshot = await loadTask.ConfigureAwait(false);
    SignalSnapshotChanged();  // <-- Notifies listeners
    // ...
}

public async Task UpdateAsync(MetadataSnapshot snapshot, ...)
{
    // ...
    Volatile.Write(ref _snapshotTask, newTask);
    SignalSnapshotChanged();  // <-- Notifies listeners
    // ...
}
```

**Problem with HclMetadataProvider**:
- HclMetadataProvider provides the snapshot but doesn't trigger changes
- No ReloadAsync() method in HclMetadataProvider itself
- MetadataRegistry is the wrapper that provides change token management
- Changes must be explicitly triggered via MetadataRegistry.ReloadAsync() or UpdateAsync()

**Impact on HA**:
- Change tokens work but rely on explicit reload calls
- No automatic detection of file changes
- No cross-instance change propagation
- Consumers can listen for changes but provider never initiates them

---

### 4. Configuration Update Handling

**Status**: MANUAL TRIGGER ONLY

**Findings**:

#### HonuaConfigLoader.cs
```csharp
public static class HonuaConfigLoader
{
    public static HonuaConfig Load(string filePath)
    {
        // Synchronous one-time load
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".json" => LoadFromJson(filePath),
            ".hcl" => LoadFromHcl(filePath),
            ".honua" => LoadFromHcl(filePath),
            _ => throw new NotSupportedException(...)
        };
    }
    
    public static async Task<HonuaConfig> LoadAsync(string filePath)
    {
        // Async one-time load
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".json" => await LoadFromJsonAsync(filePath),
            ".hcl" => await LoadFromHclAsync(filePath),
            ".honua" => await LoadFromHclAsync(filePath),
            _ => throw new NotSupportedException(...)
        };
    }
}
```

**Key Finding**: 
- HonuaConfigLoader is a simple stateless loader with no reload capability
- ConfigurationProcessor resolves environment variables one-time
- No caching, no watching, no change detection

#### ConfigurationProcessor.cs
```csharp
public sealed class ConfigurationProcessor
{
    /// <summary>
    /// Process the configuration to resolve all interpolations and references.
    /// </summary>
    public HonuaConfig Process(HonuaConfig config)
    {
        // One-time processing
        _variables = new Dictionary<string, object?>(config.Variables);
        
        var processedDataSources = ProcessDataSources(config.DataSources);
        var processedServices = ProcessServices(config.Services);
        var processedLayers = ProcessLayers(config.Layers);
        var processedCaches = ProcessCaches(config.Caches);
        // ... etc
        
        return new HonuaConfig { ... };
    }
    
    private string InterpolateString(string input)
    {
        // Environment variables resolved at load time
        result = EnvVarPattern.Replace(result, match =>
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (value == null)
            {
                throw new InvalidOperationException(...);
            }
            return value;
        });
        // ... one-time resolution
    }
}
```

**Problem**:
- Environment variables are captured at startup
- Changes to environment variables are NOT picked up
- Configuration updates require full application restart
- No partial reload mechanism

**How Updates Must Work Currently**:
1. File modified on disk
2. Manual admin must call ReloadAsync() on MetadataRegistry
3. MetadataRegistry calls HclMetadataProvider.LoadAsync()
4. HonuaConfigLoader loads fresh file
5. ConfigurationProcessor resolves variables again
6. MetadataRegistry emits change token
7. Dependents listen to change token and react

**Missing for HA**:
- No automatic trigger for step 2
- No cross-instance propagation
- No consistency guarantees between instances

---

### 5. Multi-Instance Coordination

**Status**: NOT IMPLEMENTED

**Findings**:

#### Architecture Pattern
In a multi-instance deployment:
```
Instance 1                Instance 2                Instance 3
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│ honua.hcl    │         │ honua.hcl    │         │ honua.hcl    │
└──────┬───────┘         └──────┬───────┘         └──────┬───────┘
       │                        │                        │
       v                        v                        v
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│ HonuaConfig  │         │ HonuaConfig  │         │ HonuaConfig  │
│ Loaded       │         │ Loaded       │         │ Loaded       │
│ Independently│         │ Independently│         │ Independently│
└──────┬───────┘         └──────┬───────┘         └──────┬───────┘
       │                        │                        │
       v                        v                        v
  (NO SYNC)                 (NO SYNC)                 (NO SYNC)
  ───────────────────────────────────────────────────────────────
```

**Gap**: Zero coordination mechanism between instances

#### What EXISTS for Coordination
```csharp
// CachedMetadataRegistry can use IDistributedCache
public sealed class CachedMetadataRegistry : IMetadataRegistry
{
    private readonly IMetadataRegistry _innerRegistry;
    private readonly IDistributedCache? _distributedCache;  // <-- Can be Redis
    
    // But this caches RESULTS, not configuration itself
    // and doesn't coordinate across instances
}
```

**What's MISSING**:
- No Redis Pub/Sub for broadcast notifications
- No distributed lock for coordinated updates
- No version/hash checking across instances
- No leader-election for configuration updates
- No synchronization guarantees

---

### 6. Plans & TODOs for HA Support

**Status**: NO EXPLICIT HA ROADMAP

**Findings**:

#### From Configuration 2.0 Proposal
```markdown
# Future Enhancements

Phase 1 (Configuration Parser) is now complete. Future phases include:

- **Phase 2**: Validation Engine - CLI tool for validation (`honua validate`)
- **Phase 3**: Dynamic Service Loader - Load service assemblies dynamically
- **Phase 4**: CLI Tooling - `honua introspect`, `honua plan`, `honua init`
- **Phase 5**: Database Introspection - Generate configuration from DB schemas
- **Phase 6**: Migration Tooling - Migrate from old config format
- **Phase 7**: Documentation & Examples
```

**Observation**: No phase mentions HA, distributed coordination, or multi-instance scenarios

#### Code Search Results
```bash
$ grep -r "TODO.*HA\|TODO.*distributed\|TODO.*multi-instance\|FIXME.*reload" 
  /src/Honua.Server.Core/Configuration
  
# NO RESULTS
```

**No TODOs found** specifically addressing HA or distributed configuration

#### Validation Warnings (Production-Specific)
```csharp
// From SemanticValidator.cs
if (config.Honua.Environment.ToLowerInvariant() == "production")
{
    // Production-specific issues detected
    
    // Issue 1: In-memory cache in production
    if (hasOnlyMemoryCache)
    {
        result.AddWarning(
            "Only in-memory caching is configured in production environment",
            "cache",
            "Consider using Redis for distributed caching in production");
    }
    
    // Issue 2: Rate limiting not enabled
    if (config.RateLimit == null || !config.RateLimit.Enabled)
    {
        result.AddWarning(
            "Rate limiting is not enabled in production environment",
            "rate_limit",
            "Enable rate limiting to protect against abuse");
    }
}
```

**Implication**: Validator warns about production issues but doesn't enforce multi-instance safety

---

## Comparison with Legacy System

### RedisMetadataProvider (Deleted)
Based on migration hints, the legacy system had:
- **RedisMetadataProvider** - Stored metadata in Redis
- **Pub/Sub capability** - Could broadcast changes across instances
- **Distributed coordination** - Instances shared state

**Evidence**:
```csharp
// From MetadataProviderMigration.cs
/// <example>
/// // Migrate from File to Redis
/// var fileProvider = new JsonMetadataProvider("./metadata.json");
/// var redisProvider = new RedisMetadataProvider(redis, options, logger);
/// await migration.MigrateAsync(fileProvider, redisProvider);
/// </example>
```

**Why Removed**: Likely replaced by Configuration V2's declarative approach, but HA features weren't ported

### Configuration V2 Gaps vs. Legacy
| Feature | Legacy System | Configuration V2 |
|---------|--------------|-----------------|
| File watching | Unknown | NO |
| Redis metadata store | YES | NO (schema exists, not implemented) |
| Pub/Sub notifications | Likely | NO |
| Hot-reload | Likely | NO (manual via ReloadAsync) |
| Multi-instance sync | YES (via Redis) | NO |
| Change tokens | Unknown | YES (but not triggered automatically) |

---

## Current HA Limitations

### 1. **Configuration is File-Based Only**
- Each instance reads from disk
- No shared metadata store
- No consistency guarantees

### 2. **Changes Are Not Propagated**
- No automatic detection of file changes
- No broadcast mechanism between instances
- Requires manual reload intervention

### 3. **Environment Variables Captured at Startup**
- Cannot be updated without restart
- No runtime reconfiguration capability
- Database URLs, Redis endpoints fixed at boot

### 4. **No Distributed Lock**
- Multiple instances could update configuration simultaneously
- No serialization of updates
- Potential for inconsistent state

### 5. **Change Token Not Auto-Triggered**
- Infrastructure exists but not utilized
- Manual explicit calls required
- No external event source monitoring

### 6. **In-Memory Caches Not Shared**
- Each instance maintains separate caches
- No cache coherency between instances
- Potential stale data issues

---

## What Works for Basic HA

Despite limitations, some things ARE available:

1. **Stateless Configuration**
   - Each instance can load independently
   - No session affinity required
   - Safe for horizontal scaling (each reads same file)

2. **Change Token Infrastructure**
   - Listeners can hook changes
   - CachedMetadataRegistry can invalidate caches
   - Async change propagation path exists

3. **Environment Variable Support**
   - Can pass different configs via env vars in different deployments
   - Enables per-environment configuration

4. **Redis Cache Validation**
   - Schema validates Redis requirements
   - Warns about memory-only cache in production
   - Foundation for future distributed caching

---

## Recommendations for HA Enhancement

### Short Term (Quick Wins)
1. Implement IReloadableMetadataProvider in HclMetadataProvider
2. Add FileSystemWatcher for configuration file changes
3. Trigger MetadataChanged event when file is modified
4. Add management endpoint to trigger reload via API

### Medium Term (Production-Ready HA)
1. Implement file-based coordination (lease files in shared storage)
2. Add Redis Pub/Sub for cross-instance notifications
3. Implement distributed version tracking (file hash/timestamp)
4. Add configuration diff/merge strategies

### Long Term (Enterprise HA)
1. Implement full RedisMetadataProvider with Pub/Sub
2. Add distributed lock for coordinated updates
3. Implement versioning and rollback mechanisms
4. Add health checks for configuration consistency

---

## Conclusion

**Configuration V2 provides adequate support for single-instance deployments and basic horizontal scaling** (where instances independently load the same file configuration). However, **it lacks critical features for true high availability scenarios**:

- No file watching or hot-reload
- No multi-instance coordination mechanism
- Redis support declared but not implemented
- Change token infrastructure present but not auto-triggered
- No distributed metadata store

The system is **suitable for stateless, file-based deployments** where all instances:
1. Can read the same configuration file (via shared storage or identical images)
2. Don't need dynamic reconfiguration without restart
3. Can tolerate eventual consistency (different instances might be slightly out of sync)

For **true HA with dynamic updates**, additional implementation work is required to:
1. Watch configuration files for changes
2. Propagate changes across instances
3. Coordinate updates using distributed mechanisms (Redis Pub/Sub, distributed locks)
4. Implement the RedisMetadataProvider that was deleted from the legacy system
