# Leader Election for High Availability Deployments

This module provides distributed leader election infrastructure for Honua.Server HA deployments. It ensures that only one instance in a cluster executes singleton background tasks at any given time, preventing duplicate processing and race conditions.

## Overview

In a high availability deployment with multiple server instances, certain operations should only be performed by a single instance:

- **Build Queue Processing** - Only one instance should process the build queue
- **Scheduled Jobs** - Cron jobs and scheduled tasks should run once per interval
- **Cleanup Operations** - Database cleanup, log rotation, etc.
- **Email/Notification Sending** - Prevent duplicate notifications
- **External API Polling** - Avoid rate limiting issues

Without leader election, all instances would attempt these operations simultaneously, causing:
- Duplicate processing and notifications
- Race conditions and data corruption
- Resource exhaustion
- Rate limiting from external APIs
- Increased costs

## Architecture

### Components

1. **ILeaderElection** - Core interface for leader election operations
2. **RedisLeaderElection** - Redis-based implementation using SET NX EX
3. **LeaderElectionOptions** - Configuration for lease duration, renewal, etc.
4. **LeaderElectionService** - Background service that maintains leadership
5. **LeaderElectionServiceExtensions** - Dependency injection registration

### How It Works

```
┌──────────────────────────────────────────────────────────────┐
│                    Redis Leader Election                      │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│  Instance A              Redis                Instance B     │
│  ┌─────────┐         ┌─────────┐          ┌─────────┐       │
│  │         │         │         │          │         │       │
│  │ Leader  │◄────────┤  Lock   │──────────│ Follower│       │
│  │         │ acquired│ (30s)   │ failed   │         │       │
│  └────┬────┘         └────┬────┘          └─────────┘       │
│       │                   │                                  │
│       │ Renew every 10s   │                                  │
│       │◄──────────────────┤                                  │
│       │                   │                                  │
│       ▼                   │                                  │
│  ┌──────────┐             │                                  │
│  │ Process  │             │                                  │
│  │ Builds   │             │                                  │
│  └──────────┘             │                                  │
│                           │                                  │
│  On shutdown:             │                                  │
│  Release lock ────────────┤                                  │
│                           │                                  │
│                           │  Instance B becomes leader       │
│                           │◄──────────────────────────────   │
└──────────────────────────────────────────────────────────────┘
```

### Leader Acquisition Flow

1. **Startup**: Each instance attempts to acquire leadership via Redis SET NX
2. **Success**: One instance acquires the lock and becomes the leader
3. **Failure**: Other instances become followers and retry periodically
4. **Renewal**: Leader renews the lease every 10 seconds (configurable)
5. **Processing**: Only the leader processes singleton tasks
6. **Failover**: If leader crashes, lock expires after 30 seconds
7. **Reacquisition**: Followers detect expiry and compete for leadership

## Configuration

### Using Configuration V2 (HCL - Recommended)

Add the leader election configuration to your `.honua` configuration file:

```hcl
honua {
  version     = "1.0"
  environment = "production"

  high_availability {
    enabled = true

    leader_election {
      enabled                  = true
      resource_name            = "honua-server"
      lease_duration_seconds   = 30
      renewal_interval_seconds = 10
      key_prefix               = "honua:leader:"
      enable_detailed_logging  = false
    }
  }
}

# Redis cache (required for leader election)
cache "redis" {
  enabled    = true
  connection = env("REDIS_CONNECTION_STRING")
  prefix     = "honua:"
}
```

See `leader-election.example.honua` for more examples.

### Using appsettings.json (Legacy)

If you're not yet using Configuration V2, add to `appsettings.json`:

```json
{
  "LeaderElection": {
    "ResourceName": "honua-server",
    "LeaseDurationSeconds": 30,
    "RenewalIntervalSeconds": 10,
    "KeyPrefix": "honua:leader:",
    "EnableDetailedLogging": false
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `ResourceName` | `"honua-server"` | Identifier for the coordinated resource |
| `LeaseDurationSeconds` | `30` | How long leadership lasts before expiry |
| `RenewalIntervalSeconds` | `10` | How often to renew the leadership lease |
| `KeyPrefix` | `"honua:leader:"` | Redis key prefix for leader election locks |
| `EnableDetailedLogging` | `false` | Enable verbose logging for debugging |

### Best Practices

- **LeaseDuration**: Set to 3× RenewalInterval (e.g., 30s / 10s)
- **RenewalInterval**: Choose based on failover speed requirements:
  - Fast failover: 5-10 seconds (higher Redis load)
  - Balanced: 10-15 seconds (recommended)
  - Stable: 15-30 seconds (lower Redis load)
- **Production**: Use Redis Sentinel or Cluster for Redis HA
- **Development**: Single Redis instance is sufficient

## Usage

### 1. Register Services

In your `Program.cs` or `Startup.cs`:

```csharp
// Ensure Redis is configured first
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration["Redis:ConnectionString"];
});

// Add leader election
services.AddLeaderElection(configuration);
```

### 2. Inject into Background Services

```csharp
public class BuildQueueProcessor : BackgroundService
{
    private readonly LeaderElectionService _leaderElection;
    private readonly ILogger<BuildQueueProcessor> _logger;

    public BuildQueueProcessor(
        LeaderElectionService leaderElection,
        ILogger<BuildQueueProcessor> logger)
    {
        _leaderElection = leaderElection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Only process if this instance is the leader
            if (!_leaderElection.IsLeader)
            {
                _logger.LogDebug("Not leader, skipping processing");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }

            // Process work as leader
            await ProcessBuildQueueAsync(stoppingToken);
        }
    }
}
```

### 3. Optional: Use ILeaderElection Directly

For more control, inject `ILeaderElection`:

```csharp
public class MyService
{
    private readonly ILeaderElection _leaderElection;

    public async Task PerformCriticalOperationAsync()
    {
        // Try to become leader for this specific operation
        var acquired = await _leaderElection.TryAcquireLeadershipAsync(
            "my-critical-operation");

        if (!acquired)
        {
            _logger.LogWarning("Another instance is processing this operation");
            return;
        }

        try
        {
            // Perform operation
            await DoWorkAsync();
        }
        finally
        {
            // Release leadership when done
            await _leaderElection.ReleaseLeadershipAsync("my-critical-operation");
        }
    }
}
```

## Health Checks

Leader election includes a health check for monitoring:

```csharp
services.AddHealthChecks()
    .AddCheck<LeaderElectionHealthCheck>("leader_election");
```

Health check results:
- **Healthy**: This instance is the current leader
- **Degraded**: This instance is not the leader (normal for followers)
- **Unhealthy**: Error checking leadership status

Access via `/health` endpoint:

```json
{
  "status": "Healthy",
  "results": {
    "leader_election": {
      "status": "Healthy",
      "description": "This instance is the current leader (InstanceId: server1_12345_abc123)",
      "data": {
        "is_leader": true,
        "instance_id": "server1_12345_abc123"
      }
    }
  }
}
```

## Production Deployment

### HA Checklist

- [ ] **Redis HA**: Deploy Redis Sentinel or Cluster for fault tolerance
- [ ] **Multiple Instances**: Deploy 2+ server instances behind load balancer
- [ ] **Health Monitoring**: Monitor leader election health checks
- [ ] **Alerting**: Alert on prolonged leadership failures
- [ ] **Logging**: Enable detailed logging initially, reduce after validation
- [ ] **Testing**: Test failover scenarios (kill leader, network partition)
- [ ] **Metrics**: Monitor leadership acquisition/loss rates

### Redis Sentinel Configuration

```json
{
  "Redis": {
    "ConnectionString": "redis-sentinel1:26379,redis-sentinel2:26379,redis-sentinel3:26379,serviceName=mymaster"
  }
}
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  replicas: 3  # Deploy multiple instances
  template:
    spec:
      containers:
      - name: honua-server
        image: honua/server:latest
        env:
        - name: LeaderElection__ResourceName
          value: "honua-server"
        - name: LeaderElection__LeaseDurationSeconds
          value: "30"
        - name: LeaderElection__RenewalIntervalSeconds
          value: "10"
        - name: Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: redis-credentials
              key: connection-string
```

## Troubleshooting

### Leadership Not Acquired

**Symptom**: No instance becomes leader

**Causes**:
- Redis connection failure
- Network issues
- Incorrect configuration

**Solution**:
```bash
# Check Redis connectivity
redis-cli -h redis-host -p 6379 ping

# Check leader lock in Redis
redis-cli -h redis-host -p 6379 GET "honua:leader:honua-server"

# Check logs for errors
kubectl logs -f deployment/honua-server | grep -i "leader"
```

### Frequent Leader Changes

**Symptom**: Leadership switches between instances frequently

**Causes**:
- RenewalInterval too long
- Redis timeouts
- Network instability

**Solution**:
- Reduce RenewalInterval (e.g., from 10s to 5s)
- Increase LeaseDuration (e.g., from 30s to 60s)
- Check Redis performance and network stability

### Split-Brain Scenario

**Symptom**: Multiple instances think they're the leader

**Causes**:
- Redis partition
- Clock skew
- Implementation bug

**Solution**:
- **Prevention**: Use Redis Sentinel/Cluster for HA
- **Detection**: Monitor via health checks
- **Mitigation**: Implement idempotency in processing logic

### No Processing Happening

**Symptom**: No instance processes work, all waiting

**Causes**:
- All instances think they're not the leader
- Leader election service not started
- Redis lock exists but instance crashed

**Solution**:
```bash
# Manually clear the lock (emergency only)
redis-cli -h redis-host -p 6379 DEL "honua:leader:honua-server"

# Check service registration
# Ensure AddLeaderElection() is called in Program.cs
```

## Implementation Details

### Atomic Operations

All Redis operations are atomic to prevent race conditions:

**Acquisition**:
```lua
SET honua:leader:honua-server server1_12345_abc123 NX EX 30
```

**Renewal**:
```lua
if redis.call('get', KEYS[1]) == ARGV[1] then
    return redis.call('expire', KEYS[1], ARGV[2])
else
    return 0
end
```

**Release**:
```lua
if redis.call('get', KEYS[1]) == ARGV[1] then
    return redis.call('del', KEYS[1])
else
    return 0
end
```

### Instance ID Format

Each instance has a unique ID:
```
{MachineName}_{ProcessId}_{Guid}
```

Example: `server1_12345_a1b2c3d4e5f6g7h8`

This ensures uniqueness across:
- Different machines
- Multiple processes on same machine
- Process restarts

## Security Considerations

### Redis Authentication

Always use Redis authentication in production:

```json
{
  "Redis": {
    "ConnectionString": "redis-host:6379,password=your-secure-password,ssl=true"
  }
}
```

### Network Isolation

- Deploy Redis in a private network
- Use VPC/VNet for isolation
- Enable TLS for Redis connections
- Restrict Redis access via firewall

### Key Expiry

Leader locks automatically expire to prevent indefinite locks from crashed instances. This is a safety mechanism, not a bug.

## Performance

### Redis Load

- **Acquisition**: 1 SET operation per instance startup
- **Renewal**: 1 Lua script execution every RenewalInterval (per leader)
- **Check**: 1 GET operation per check (as needed)

For 10 instances with 10s renewal:
- Leader: 6 operations/minute
- Followers: 6 acquisition attempts/minute
- Total: ~60 operations/minute (negligible Redis load)

### Latency

- **Acquisition**: <10ms (typical)
- **Renewal**: <5ms (typical)
- **Check**: <5ms (typical)

Impact on processing: Minimal (<1% overhead)

## Testing

### Unit Tests

```csharp
[Fact]
public async Task TryAcquireLeadershipAsync_WhenFirstInstance_ReturnsTrue()
{
    var leaderElection = CreateLeaderElection();
    var acquired = await leaderElection.TryAcquireLeadershipAsync("test-resource");
    Assert.True(acquired);
}

[Fact]
public async Task TryAcquireLeadershipAsync_WhenAlreadyAcquired_ReturnsFalse()
{
    var leaderElection1 = CreateLeaderElection();
    var leaderElection2 = CreateLeaderElection();

    await leaderElection1.TryAcquireLeadershipAsync("test-resource");
    var acquired = await leaderElection2.TryAcquireLeadershipAsync("test-resource");

    Assert.False(acquired);
}
```

### Integration Tests

```bash
# Start Redis
docker run -d --name redis -p 6379:6379 redis:latest

# Start multiple server instances
dotnet run --urls http://localhost:5001 &
dotnet run --urls http://localhost:5002 &
dotnet run --urls http://localhost:5003 &

# Check which instance is leader
curl http://localhost:5001/health | jq '.results.leader_election'
curl http://localhost:5002/health | jq '.results.leader_election'
curl http://localhost:5003/health | jq '.results.leader_election'

# Kill leader, verify failover
kill $(lsof -ti:5001)

# Wait 30 seconds for lease expiry
sleep 30

# Check new leader
curl http://localhost:5002/health | jq '.results.leader_election'
curl http://localhost:5003/health | jq '.results.leader_election'
```

## Future Enhancements

Potential improvements for future releases:

1. **Leadership Events** - Publish events when leadership changes
2. **Multi-Resource Support** - Different leaders for different resources
3. **Leader Priorities** - Prefer certain instances as leaders
4. **Graceful Handoff** - Coordinate handoff during deployments
5. **Metrics Dashboard** - Visualize leadership history
6. **Automatic Failback** - Prefer specific instances after recovery

## References

- [Redis Documentation](https://redis.io/docs/)
- [Distributed Locks with Redis](https://redis.io/docs/manual/patterns/distributed-locks/)
- [ASP.NET Core Background Services](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)
- [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/)

## Support

For issues or questions:
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Documentation: https://docs.honua.io
- Email: support@honua.io
