# 9. Redis for Distributed State Stores

Date: 2025-10-17

Status: Accepted

## Context

Honua needs distributed state management for:
- **Process Framework State**: Workflow checkpoints (deployment, upgrades)
- **Tile Cache**: Distributed caching of raster/vector tiles
- **Session State**: Distributed sessions for horizontal scaling
- **Rate Limiting**: Shared counters across instances
- **Lock Coordination**: Distributed locks for critical sections

**Requirements:**
- Fast read/write performance (sub-millisecond)
- TTL-based expiration for cache entries
- Atomic operations for counters and locks
- Pub/sub for process events
- Persistence options for workflow state
- Cloud-managed offerings (AWS ElastiCache, Azure Cache, GCP Memorystore)

**Existing Evidence:**
- StackExchange.Redis package: `<PackageReference Include="StackExchange.Redis" Version="2.8.16" />`
- Redis state store: `/src/Honua.Cli.AI/Services/Processes/RedisProcessStateStore.cs`
- In-memory fallback: `/src/Honua.Cli.AI/Services/Processes/InMemoryProcessStateStore.cs`
- Configuration options: `/src/Honua.Cli.AI/Configuration/RedisOptions.cs`

## Decision

Use **Redis** as the distributed state store with an in-memory fallback for development.

**Use Cases:**
1. **Process Framework State**: Serialize workflow state to Redis (pause/resume)
2. **Tile Caching**: Cache generated tiles with TTL (future)
3. **Metadata Cache**: Cached metadata JSON with invalidation (future)

**Implementation:**
- **Production**: Redis (standalone or cluster)
- **Development**: In-memory state store (no Redis required)
- **StackExchange.Redis**: High-performance .NET client

## Consequences

### Positive

- **Horizontal Scaling**: State shared across multiple Honua instances
- **High Performance**: Sub-millisecond read/write latency
- **Persistence Options**: RDB snapshots + AOF for durability
- **Cloud-Native**: Managed offerings on all major clouds
- **Rich Features**: Pub/sub, streams, sorted sets, etc.
- **Proven**: Battle-tested at massive scale (Twitter, GitHub, etc.)

### Negative

- **Operational Complexity**: Another service to run and monitor
- **Memory Costs**: Redis data stored in-memory (expensive at scale)
- **Single Point of Failure**: Need Redis Cluster or Sentinel for HA
- **Data Eviction**: LRU eviction can lose data if memory full

### Neutral

- Must configure persistence (RDB/AOF) for workflow state
- Need monitoring for memory usage and eviction rates

## Alternatives Considered

### 1. In-Memory Only
**Verdict:** Accepted for development, insufficient for production

### 2. PostgreSQL for State
**Verdict:** Rejected - too slow for high-frequency state updates

### 3. Memcached
**Verdict:** Rejected - lacks persistence, atomic operations, pub/sub

### 4. Dapr State Store
**Verdict:** Future consideration - adds abstraction layer

## Implementation

```csharp
// Process state storage
public class RedisProcessStateStore : IProcessStateStore
{
    private readonly IConnectionMultiplexer _redis;

    public async Task SaveStateAsync(string processId, object state)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(state);
        await db.StringSetAsync($"process:{processId}", json,
            expiry: TimeSpan.FromHours(24));
    }

    public async Task<T?> LoadStateAsync<T>(string processId)
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync($"process:{processId}");
        return json.HasValue
            ? JsonSerializer.Deserialize<T>(json!)
            : default;
    }
}
```

## References

- [Redis Documentation](https://redis.io/docs/)
- [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/)

## Notes

Redis provides the performance and features needed for distributed state. In-memory fallback ensures good development experience without infrastructure dependencies.
