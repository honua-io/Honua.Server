# Configuration V2 HA Support - Quick Summary

## Overview
Configuration V2 in Honua Server was designed primarily for single-instance and basic multi-instance deployments where all instances independently load the same file-based configuration. **It does NOT provide true high availability support for coordinated, multi-instance scenarios with dynamic reconfiguration.**

---

## Key Findings Table

| Aspect | Status | Details |
|--------|--------|---------|
| **File Watching** | NOT IMPLEMENTED | No FileSystemWatcher, configuration loaded once at startup |
| **Hot-Reload** | LIMITED | Manual trigger only via `MetadataRegistry.ReloadAsync()` |
| **Redis Integration** | DECLARED, NOT USED | Schema supports it but HclMetadataProvider ignores Redis |
| **IChangeToken Usage** | INFRASTRUCTURE ONLY | Present in MetadataRegistry but never auto-triggered by provider |
| **Multi-Instance Sync** | NOT IMPLEMENTED | Zero coordination mechanism between instances |
| **HA Roadmap** | NO PLANS | No TODO items or roadmap items for HA |
| **Distributed Store** | MISSING | Legacy RedisMetadataProvider was deleted, not replaced |

---

## What Actually Works for HA

1. **Stateless Config Loading** - Each instance can independently read the same file
2. **Change Token Infrastructure** - Framework exists for listeners to react to changes
3. **Environment Variables** - Can inject different configs via env vars per deployment
4. **Basic Validation** - Warns about memory-only cache in production

---

## What's Missing for True HA

1. **No Automatic Change Detection**
   - File changes not detected
   - Must manually call `ReloadAsync()` 
   - No Pub/Sub notification mechanism

2. **No Cross-Instance Coordination**
   - No distributed lock
   - No leader election
   - No version tracking

3. **Environment Variables Fixed at Startup**
   - Cannot update Redis URL or DB connection without restart
   - No runtime reconfiguration

4. **No Distributed Metadata Store**
   - Legacy RedisMetadataProvider deleted
   - Each instance maintains independent in-memory state
   - No shared configuration source

---

## Architecture Gaps

### What We Have
```
File → HonuaConfigLoader → ConfigurationProcessor → MetadataRegistry
         (one-time)        (one-time, env vars)     (change tokens)
```

### What's Missing
```
FileSystemWatcher (MISSING) → Auto-trigger reload
             ↓
MetadataChanged event (NOT RAISED)
             ↓
Redis Pub/Sub (NOT IMPLEMENTED) → Notify other instances
             ↓
Distributed Lock (MISSING) → Coordinate updates
```

---

## Deployment Implications

### Safe Scenarios
- Single instance deployments
- Multiple instances with shared file storage (all read same .hcl file)
- Horizontal scaling where config doesn't change frequently
- Stateless services (no session affinity needed)

### Unsafe Scenarios
- Dynamic configuration updates without restart
- Multi-instance deployments requiring coordinated updates
- Production HA with in-memory caches (no coherency)
- Automated config management (GitOps) with cross-instance sync requirements

---

## Code References

### Files to Review
- `/src/Honua.Server.Core/Metadata/HclMetadataProvider.cs` - Core metadata provider
- `/src/Honua.Server.Core/Metadata/IMetadataProvider.cs` - Interface (supports change notifications)
- `/src/Honua.Server.Core/Metadata/MetadataRegistry.cs` - Wrapper with change token support
- `/src/Honua.Server.Core/Configuration/V2/HonuaConfigLoader.cs` - File loader (no watching)
- `/src/Honua.Server.Core/Configuration/V2/ConfigurationProcessor.cs` - Env var resolution

### Key Evidence
```csharp
// HclMetadataProvider.cs line 25
public bool SupportsChangeNotifications => false;  // <-- Not supported

// IMetadataProvider.cs line 88
public string? Source { get; }  // "file-watcher", "redis-pubsub" - but never used
```

---

## Recommendations

### Immediate (If HA Needed)
1. Add external file watching (implement IReloadableMetadataProvider)
2. Create management endpoint to trigger reload manually
3. Document deployment pattern: "Restart instances after config changes"

### Near-term (Better HA)
1. Implement FileSystemWatcher in HclMetadataProvider
2. Add Redis Pub/Sub for cross-instance notifications
3. Implement configuration versioning/checksums

### Long-term (Enterprise HA)
1. Restore RedisMetadataProvider functionality
2. Add distributed locks for update coordination
3. Implement rollback/rollforward strategies

---

## Configuration for HA Best Practices

Even with current limitations, here's a production-ready approach:

```hcl
honua {
    version = "1.0"
    environment = "production"
}

# Use Redis for distributed caching (even if not for config)
cache "redis" {
    type = "redis"
    enabled = true
    connection = "${env:REDIS_CONNECTION}"
    required_in = ["production"]
}

# Stateless configuration that all instances can load
data_source "primary" {
    provider = "postgresql"
    connection = "${env:DATABASE_URL}"
    pool = {
        min_size = 10
        max_size = 50
    }
}

# All services enabled and configured
service "odata" {
    type = "odata"
    enabled = true
}

# Deployment strategy: Use GitOps with coordinated restarts
# 1. Update honua.hcl in Git
2. Trigger rolling restart of all instances (via orchestrator)
```

---

## See Also
- Full analysis: `/HA_ANALYSIS.md`
- Configuration proposal: `/docs/proposals/configuration-2.0.md`
- Configuration V2 README: `/src/Honua.Server.Core/Configuration/V2/README.md`
