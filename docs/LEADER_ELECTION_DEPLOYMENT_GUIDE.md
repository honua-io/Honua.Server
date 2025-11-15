# Leader Election Deployment Guide - Phase 2.1

This guide walks through deploying the Redis-based leader election infrastructure for Honua.Server HA deployments.

## Prerequisites

- Redis server (standalone, Sentinel, or Cluster)
- Multiple Honua.Server instances (2+)
- StackExchange.Redis package (already included)

## Quick Start

### 1. Update Configuration

#### Option A: Configuration V2 (HCL - Recommended)

Add to your `.honua` configuration file:

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

See `examples/config-v2/docker-compose-ha.honua` or `kubernetes-ha.honua` for complete examples.

#### Option B: appsettings.json (Legacy)

Add to your `appsettings.json`:

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
    "ConnectionString": "your-redis-host:6379"
  }
}
```

### 2. Register Services

In your `Program.cs` or `Startup.ConfigureServices`:

```csharp
// Ensure Redis is registered first
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration["Redis:ConnectionString"];
});

// Add leader election
services.AddLeaderElection(configuration);
```

### 3. Verify Deployment

After deploying multiple instances:

```bash
# Check health endpoint on each instance
curl http://instance1:8080/health | jq '.results.leader_election'
curl http://instance2:8080/health | jq '.results.leader_election'
curl http://instance3:8080/health | jq '.results.leader_election'

# Expected: One instance shows "Healthy" (is_leader: true), others show "Degraded" (is_leader: false)
```

## Production Configuration

### Environment-Specific Settings (HCL)

**Development**:
```hcl
high_availability {
  leader_election {
    lease_duration_seconds   = 15
    renewal_interval_seconds = 5
    enable_detailed_logging  = true
  }
}
```

**Production**:
```hcl
high_availability {
  leader_election {
    lease_duration_seconds   = 30
    renewal_interval_seconds = 10
    enable_detailed_logging  = false
  }
}
```

**High-Load Production**:
```hcl
high_availability {
  leader_election {
    lease_duration_seconds   = 60
    renewal_interval_seconds = 15
    enable_detailed_logging  = false
  }
}
```

### Redis High Availability (HCL)

#### Option 1: Redis Sentinel (Recommended)

```hcl
cache "redis" {
  enabled    = true
  connection = "sentinel1:26379,sentinel2:26379,sentinel3:26379,serviceName=mymaster,password=your-password,ssl=true"
  prefix     = "honua:"
}
```

Or use environment variable:
```hcl
cache "redis" {
  enabled    = true
  connection = env("REDIS_CONNECTION_STRING")  # Set via environment
  prefix     = "honua:"
}
```

#### Option 2: Redis Cluster

```hcl
cache "redis" {
  enabled    = true
  connection = "node1:6379,node2:6379,node3:6379,password=your-password,ssl=true"
  prefix     = "honua:"
}
```

#### Option 3: Azure Redis Cache

```hcl
cache "redis" {
  enabled    = true
  connection = env("AZURE_REDIS_CONNECTION_STRING")
  prefix     = "honua:"
}

# Set environment variable:
# AZURE_REDIS_CONNECTION_STRING=your-redis.redis.cache.windows.net:6380,password=your-access-key,ssl=true,abortConnect=false
```

#### Option 4: AWS ElastiCache

```hcl
cache "redis" {
  enabled    = true
  connection = env("AWS_REDIS_ENDPOINT")
  prefix     = "honua:"
}

# Set environment variable:
# AWS_REDIS_ENDPOINT=your-cluster.cache.amazonaws.com:6379,ssl=true
```

## Kubernetes Deployment

### Complete Example

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-server-config
data:
  appsettings.Production.json: |
    {
      "LeaderElection": {
        "ResourceName": "honua-server",
        "LeaseDurationSeconds": 30,
        "RenewalIntervalSeconds": 10
      }
    }
---
apiVersion: v1
kind: Secret
metadata:
  name: honua-redis-secret
type: Opaque
stringData:
  connection-string: "redis-sentinel:26379,serviceName=mymaster,password=your-password"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  labels:
    app: honua-server
spec:
  replicas: 3  # Deploy 3 instances for HA
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
    spec:
      containers:
      - name: honua-server
        image: honua/server:latest
        ports:
        - containerPort: 8080
          name: http
        - containerPort: 8081
          name: metrics
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: LeaderElection__ResourceName
          value: "honua-server"
        - name: LeaderElection__LeaseDurationSeconds
          value: "30"
        - name: LeaderElection__RenewalIntervalSeconds
          value: "10"
        - name: Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: honua-redis-secret
              key: connection-string
        volumeMounts:
        - name: config
          mountPath: /app/appsettings.Production.json
          subPath: appsettings.Production.json
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
      volumes:
      - name: config
        configMap:
          name: honua-server-config
---
apiVersion: v1
kind: Service
metadata:
  name: honua-server
spec:
  type: LoadBalancer
  selector:
    app: honua-server
  ports:
  - name: http
    port: 80
    targetPort: 8080
  - name: metrics
    port: 8081
    targetPort: 8081
```

### Deploy to Kubernetes

```bash
# Apply the configuration
kubectl apply -f honua-server-deployment.yaml

# Verify pods are running
kubectl get pods -l app=honua-server

# Check which pod is the leader
kubectl logs -l app=honua-server | grep -i "leadership acquired"

# Test failover by deleting the leader pod
LEADER_POD=$(kubectl logs -l app=honua-server | grep "Leadership acquired" | awk '{print $1}' | head -1)
kubectl delete pod $LEADER_POD

# Watch for new leader election (should happen within 30 seconds)
kubectl logs -l app=honua-server -f | grep -i "leadership"
```

## Docker Compose Deployment

```yaml
version: '3.8'

services:
  redis:
    image: redis:7-alpine
    command: redis-server --requirepass your-password
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

  honua-server-1:
    image: honua/server:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - LeaderElection__ResourceName=honua-server
      - LeaderElection__LeaseDurationSeconds=30
      - LeaderElection__RenewalIntervalSeconds=10
      - Redis__ConnectionString=redis:6379,password=your-password
    ports:
      - "8081:8080"
    depends_on:
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 3

  honua-server-2:
    image: honua/server:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - LeaderElection__ResourceName=honua-server
      - LeaderElection__LeaseDurationSeconds=30
      - LeaderElection__RenewalIntervalSeconds=10
      - Redis__ConnectionString=redis:6379,password=your-password
    ports:
      - "8082:8080"
    depends_on:
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 3

  honua-server-3:
    image: honua/server:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - LeaderElection__ResourceName=honua-server
      - LeaderElection__LeaseDurationSeconds=30
      - LeaderElection__RenewalIntervalSeconds=10
      - Redis__ConnectionString=redis:6379,password=your-password
    ports:
      - "8083:8080"
    depends_on:
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 3

volumes:
  redis-data:
```

### Test Docker Compose Deployment

```bash
# Start all services
docker-compose up -d

# Check which instance is the leader
docker-compose logs | grep -i "leadership acquired"

# Check health of all instances
curl http://localhost:8081/health | jq '.results.leader_election'
curl http://localhost:8082/health | jq '.results.leader_election'
curl http://localhost:8083/health | jq '.results.leader_election'

# Simulate leader failure
LEADER_CONTAINER=$(docker-compose ps | grep "honua-server-" | grep "Up" | head -1 | awk '{print $1}')
docker-compose stop $LEADER_CONTAINER

# Wait for failover and check new leader
sleep 35
docker-compose logs | grep -i "leadership acquired" | tail -5
```

## Monitoring & Alerting

### Prometheus Metrics

The leader election service exposes OpenTelemetry metrics:

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'honua-server'
    static_configs:
      - targets: ['honua-server-1:8081', 'honua-server-2:8081', 'honua-server-3:8081']
    metrics_path: '/metrics'
```

Key metrics to monitor:
- `honua_leader_election_acquired_total` - Total leadership acquisitions
- `honua_leader_election_lost_total` - Total leadership losses
- `honua_leader_election_renewal_failures_total` - Failed renewal attempts
- `honua_leader_election_is_leader` - Current leadership status (0 or 1)

### Grafana Dashboard

```json
{
  "title": "Leader Election Status",
  "panels": [
    {
      "title": "Current Leader",
      "targets": [
        {
          "expr": "honua_leader_election_is_leader{job=\"honua-server\"}",
          "legendFormat": "{{instance}}"
        }
      ]
    },
    {
      "title": "Leadership Changes",
      "targets": [
        {
          "expr": "rate(honua_leader_election_acquired_total[5m])",
          "legendFormat": "Acquisitions"
        },
        {
          "expr": "rate(honua_leader_election_lost_total[5m])",
          "legendFormat": "Losses"
        }
      ]
    }
  ]
}
```

### Alerting Rules

```yaml
# alerts.yml
groups:
  - name: leader_election
    rules:
      - alert: NoLeaderElected
        expr: sum(honua_leader_election_is_leader) == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "No leader instance elected"
          description: "No Honua.Server instance is currently the leader. Check Redis connectivity and instance health."

      - alert: FrequentLeaderChanges
        expr: rate(honua_leader_election_acquired_total[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Frequent leader election changes"
          description: "Leadership is changing too frequently. This may indicate network instability or configuration issues."

      - alert: LeaderElectionRenewalFailures
        expr: rate(honua_leader_election_renewal_failures_total[5m]) > 0.5
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "Leader election renewal failures"
          description: "Instance is failing to renew leadership. Check Redis connectivity."
```

## Troubleshooting

### Common Issues

#### Issue: No instance becomes leader

**Diagnosis**:
```bash
# Check Redis connectivity from server instances
redis-cli -h <redis-host> -p 6379 -a <password> ping

# Check if lock exists in Redis
redis-cli -h <redis-host> -p 6379 -a <password> GET "honua:leader:honua-server"

# Check server logs
kubectl logs -l app=honua-server | grep -i "leader\|redis"
```

**Resolution**:
1. Verify Redis connection string is correct
2. Ensure Redis is accessible from server instances
3. Check firewall rules and network connectivity
4. Verify Redis authentication credentials

#### Issue: Multiple leaders (split-brain)

**Diagnosis**:
```bash
# Check health endpoint on all instances
for port in 8081 8082 8083; do
  echo "Instance on port $port:"
  curl -s http://localhost:$port/health | jq '.results.leader_election'
done
```

**Resolution**:
1. Check for Redis network partition
2. Verify system clocks are synchronized (NTP)
3. Review Redis logs for errors
4. Increase LeaseDuration if network is unstable

#### Issue: Frequent leader changes

**Diagnosis**:
```bash
# Monitor leadership changes
kubectl logs -l app=honua-server -f | grep "Leadership acquired\|Leadership lost"

# Check Redis response times
redis-cli -h <redis-host> --latency
```

**Resolution**:
1. Increase RenewalInterval if Redis is slow
2. Increase LeaseDuration for more stability
3. Check Redis performance and resource usage
4. Verify network stability between instances and Redis

## Migration from Single Instance

### Backward Compatibility

The implementation is backward compatible - if LeaderElectionService is not registered, BuildQueueProcessor runs in single-instance mode:

```csharp
// Without leader election (existing deployments)
services.AddHostedService<BuildQueueProcessor>();

// With leader election (new HA deployments)
services.AddLeaderElection(configuration);
services.AddHostedService<BuildQueueProcessor>();
```

### Gradual Rollout

1. **Phase 1**: Deploy with leader election disabled
   - Update code to latest version
   - Don't register leader election services yet
   - Verify application still works

2. **Phase 2**: Enable leader election in staging
   - Add configuration to staging environment
   - Register leader election services
   - Test failover scenarios

3. **Phase 3**: Enable in production
   - Add configuration to production
   - Deploy multiple instances
   - Monitor leadership status

## Performance Impact

- **CPU**: <1% overhead
- **Memory**: ~5 MB per instance
- **Network**: ~10 KB/minute per instance to Redis
- **Redis Load**: Negligible (<0.1% CPU for 10 instances)
- **Latency**: <10ms added to background task loops

## Security Considerations

1. **Use Redis authentication**: Always set a strong password
2. **Enable TLS**: Use SSL/TLS for Redis connections
3. **Network isolation**: Deploy Redis in private network
4. **Key rotation**: Rotate Redis passwords regularly
5. **Access control**: Use Redis ACLs to restrict operations

## Support

For issues or questions:
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Documentation: https://docs.honua.io/leader-election
- Email: support@honua.io
