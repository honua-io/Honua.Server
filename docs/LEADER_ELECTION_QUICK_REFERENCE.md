# Leader Election Quick Reference Card

## TL;DR - 3 Steps to Enable

```csharp
// 1. Add to appsettings.json
{
  "LeaderElection": {
    "ResourceName": "honua-server",
    "LeaseDurationSeconds": 30,
    "RenewalIntervalSeconds": 10
  },
  "Redis": { "ConnectionString": "redis-host:6379" }
}

// 2. Register in Program.cs
services.AddLeaderElection(configuration);

// 3. Inject into your service
public class MyService : BackgroundService
{
    private readonly LeaderElectionService _leader;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_leader.IsLeader)
            {
                // Only runs on leader instance
                await DoWorkAsync(ct);
            }
            await Task.Delay(1000, ct);
        }
    }
}
```

## Common Patterns

### Pattern 1: Background Task Coordination

```csharp
public class BuildQueueProcessor : BackgroundService
{
    private readonly LeaderElectionService _leader;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Skip processing if not leader
            if (_leader?.IsLeader == false)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                continue;
            }

            // Process work as leader
            await ProcessQueueAsync(ct);
        }
    }
}
```

### Pattern 2: Direct Leadership Control

```csharp
public class MyService
{
    private readonly ILeaderElection _leaderElection;

    public async Task DoOnceAsync()
    {
        var acquired = await _leaderElection.TryAcquireLeadershipAsync("my-task");
        if (!acquired) return; // Another instance is handling this

        try
        {
            await PerformTaskAsync();
        }
        finally
        {
            await _leaderElection.ReleaseLeadershipAsync("my-task");
        }
    }
}
```

### Pattern 3: Optional Leader Election (Backward Compatibility)

```csharp
public class MyService : BackgroundService
{
    private readonly LeaderElectionService? _leader; // nullable!

    public MyService(LeaderElectionService? leader = null)
    {
        _leader = leader;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Works with or without leader election
            if (_leader != null && !_leader.IsLeader)
            {
                await Task.Delay(1000, ct);
                continue;
            }

            await ProcessAsync(ct);
        }
    }
}
```

## Configuration Cheat Sheet

| Environment | LeaseDuration | RenewalInterval | DetailedLogging |
|-------------|---------------|-----------------|-----------------|
| Development | 15s | 5s | true |
| Staging | 30s | 10s | true |
| Production | 30s | 10s | false |
| High-Load | 60s | 15s | false |

## Health Check Interpretation

```bash
# Check leader status
curl http://localhost:8080/health | jq '.results.leader_election'

# Leader instance response
{
  "status": "Healthy",
  "data": { "is_leader": true }
}

# Follower instance response
{
  "status": "Degraded",  # This is NORMAL for followers!
  "data": { "is_leader": false }
}

# Error state
{
  "status": "Unhealthy",  # This indicates a problem
  "exception": "..."
}
```

## Troubleshooting Commands

```bash
# Check which instance is leader
for port in 8081 8082 8083; do
  echo "Port $port: $(curl -s http://localhost:$port/health | jq -r '.results.leader_election.data.is_leader')"
done

# Check Redis lock
redis-cli GET "honua:leader:honua-server"

# Force release lock (EMERGENCY ONLY)
redis-cli DEL "honua:leader:honua-server"

# Monitor leadership changes
kubectl logs -f deployment/honua-server | grep -i "leadership"

# Test failover
kubectl delete pod -l app=honua-server | grep "is_leader: true"
```

## Common Mistakes to Avoid

❌ **DON'T** set RenewalInterval >= LeaseDuration
```json
{
  "LeaseDurationSeconds": 30,
  "RenewalIntervalSeconds": 35  // BAD! Will lose leadership
}
```

✅ **DO** set RenewalInterval < LeaseDuration (typically 1/3)
```json
{
  "LeaseDurationSeconds": 30,
  "RenewalIntervalSeconds": 10  // GOOD! 3x buffer
}
```

❌ **DON'T** forget to register Redis first
```csharp
services.AddLeaderElection(configuration);  // Will fail!
```

✅ **DO** register Redis before leader election
```csharp
services.AddStackExchangeRedisCache(options => { ... });
services.AddLeaderElection(configuration);  // Works!
```

❌ **DON'T** treat "Degraded" health as an error
```csharp
// Followers are Degraded - this is normal!
if (health.Status != HealthStatus.Healthy) Alert();  // Wrong!
```

✅ **DO** only alert on Unhealthy status
```csharp
// Only alert on actual errors
if (health.Status == HealthStatus.Unhealthy) Alert();  // Correct!
```

## Redis Connection Strings

```bash
# Local Development
"localhost:6379"

# With Authentication
"redis-host:6379,password=secret"

# With TLS
"redis-host:6380,password=secret,ssl=true"

# Redis Sentinel (HA)
"sentinel1:26379,sentinel2:26379,sentinel3:26379,serviceName=mymaster,password=secret"

# Redis Cluster
"node1:6379,node2:6379,node3:6379,password=secret"

# Azure Redis Cache
"cache.redis.cache.windows.net:6380,password=key,ssl=true,abortConnect=false"

# AWS ElastiCache
"cluster.cache.amazonaws.com:6379,ssl=true"
```

## Key Metrics to Monitor

```promql
# Current leader count (should always be 1)
sum(honua_leader_election_is_leader)

# Leadership changes per hour (should be low)
rate(honua_leader_election_acquired_total[1h]) * 3600

# Failed renewals (should be 0)
rate(honua_leader_election_renewal_failures_total[5m])
```

## Alert Thresholds

| Metric | Threshold | Severity | Action |
|--------|-----------|----------|--------|
| No leader | sum() == 0 for >1m | Critical | Check Redis |
| Multiple leaders | sum() > 1 for >10s | Critical | Network partition |
| Frequent changes | >6/hour | Warning | Increase lease |
| Renewal failures | >10/min | Warning | Check Redis |

## Environment Variables Override

```bash
# Override config via environment variables
export LeaderElection__ResourceName="honua-server"
export LeaderElection__LeaseDurationSeconds="30"
export LeaderElection__RenewalIntervalSeconds="10"
export Redis__ConnectionString="redis:6379"
```

## Docker Compose Quick Start

```yaml
version: '3.8'
services:
  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]

  server1:
    image: honua/server:latest
    environment:
      - Redis__ConnectionString=redis:6379
      - LeaderElection__ResourceName=honua-server
    ports: ["8081:8080"]

  server2:
    image: honua/server:latest
    environment:
      - Redis__ConnectionString=redis:6379
      - LeaderElection__ResourceName=honua-server
    ports: ["8082:8080"]
```

```bash
# Start and test
docker-compose up -d
curl http://localhost:8081/health | jq .results.leader_election
curl http://localhost:8082/health | jq .results.leader_election
```

## Kubernetes Quick Start

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: server
        image: honua/server:latest
        env:
        - name: LeaderElection__ResourceName
          value: "honua-server"
        - name: Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: redis-secret
              key: connection-string
```

```bash
# Deploy and check
kubectl apply -f deployment.yaml
kubectl logs -l app=honua-server | grep "Leadership acquired"
```

## Testing Scenarios

### Test 1: Normal Operation
```bash
# Start 3 instances
# Expected: 1 leader, 2 followers
# Time: Immediate
```

### Test 2: Leader Failure
```bash
# Kill leader instance
# Expected: New leader elected within 30s
# Time: <LeaseDuration
```

### Test 3: Redis Failure
```bash
# Stop Redis
# Expected: All instances become followers, processing stops
# Time: Immediate on next renewal
```

### Test 4: Redis Recovery
```bash
# Restart Redis after failure
# Expected: One instance becomes leader within 10s
# Time: ~RenewalInterval
```

## Performance Targets

- Acquisition: <10ms
- Renewal: <5ms
- Failover: <30s (LeaseDuration)
- CPU overhead: <1%
- Memory overhead: ~5MB
- Redis operations: ~10/minute

## Security Checklist

- [ ] Redis password set
- [ ] TLS enabled for Redis
- [ ] Redis in private network
- [ ] Firewall rules configured
- [ ] No sensitive data in instance IDs
- [ ] Logs don't contain secrets
- [ ] Health endpoint not exposed publicly

## Need More Help?

- Full docs: `src/Honua.Server.Core/Coordination/README.md`
- Deployment guide: `docs/LEADER_ELECTION_DEPLOYMENT_GUIDE.md`
- Implementation details: `IMPLEMENTATION_SUMMARY_PHASE_2.1.md`
- GitHub issues: https://github.com/honua-io/Honua.Server/issues
