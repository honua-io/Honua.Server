# Honua Process Framework - Operations Guide

**Last Updated**: 2025-10-17
**Status**: Production Ready
**Version**: 1.0

## Table of Contents

- [Overview](#overview)
- [Daily Operations](#daily-operations)
- [Monitoring Dashboards](#monitoring-dashboards)
- [Common Troubleshooting Scenarios](#common-troubleshooting-scenarios)
- [Performance Tuning](#performance-tuning)
- [Backup and Recovery](#backup-and-recovery)
- [Maintenance Procedures](#maintenance-procedures)
- [Incident Response](#incident-response)

## Overview

This guide provides operational procedures for running and maintaining the Honua Process Framework in production environments. It covers daily operations, monitoring, troubleshooting, and performance optimization.

**Target Audience**: DevOps engineers, SREs, platform operators

**Prerequisites**:
- Familiarity with [Deployment Guide](../deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md)
- Access to monitoring dashboards (Grafana, Prometheus)
- Access to application logs (kubectl, CloudWatch, etc.)
- Understanding of Process Framework architecture

## Daily Operations

### Morning Checklist

Perform these checks at the start of each business day:

#### 1. System Health Check (5 minutes)

```bash
# Check all services are running
kubectl get pods -n honua-process-framework

# Expected output: All pods in Running state, 3/3 Ready
# NAME                                      READY   STATUS    RESTARTS   AGE
# honua-process-framework-5d4f8c9b7-abc12   1/1     Running   0          12h
# honua-process-framework-5d4f8c9b7-def34   1/1     Running   0          12h
# honua-process-framework-5d4f8c9b7-ghi56   1/1     Running   0          12h
# redis-0                                   1/1     Running   0          5d

# Check Redis health
kubectl exec -n honua-process-framework redis-0 -- redis-cli ping
# Expected: PONG

# Check application health endpoint
curl http://honua-process-framework:9090/health
# Expected: {"status":"Healthy","checks":{"redis":"Healthy",...}}
```

**Action if unhealthy**: See [Troubleshooting](#common-troubleshooting-scenarios)

#### 2. Review Overnight Process Activity (10 minutes)

```bash
# Check active processes
curl http://honua-process-framework:9090/api/processes/active | jq

# Check completed processes (last 24 hours)
curl "http://honua-process-framework:9090/api/processes?status=Completed&since=24h" | jq

# Check failed processes (last 24 hours)
curl "http://honua-process-framework:9090/api/processes?status=Failed&since=24h" | jq
```

**Key Metrics to Note**:
- Number of completed processes
- Number of failed processes (should be < 5%)
- Any processes stuck in "Running" state for > 4 hours

**Action if issues found**: Review failed process logs, escalate if pattern detected

#### 3. Review Monitoring Alerts (5 minutes)

```bash
# Check Prometheus alerts
curl http://prometheus:9090/api/v1/alerts | jq '.data.alerts[] | select(.state=="firing")'

# Check Grafana dashboard
open http://grafana:3000/d/honua-process-framework
```

**Common Alerts**:
- `HighProcessFailureRate`: > 10% processes failing
- `ProcessTimeout`: Processes exceeding configured timeout
- `RedisConnectionFailure`: Redis unavailable
- `HighMemoryUsage`: Pod memory > 80%
- `LlmApiRateLimit`: Hitting LLM API limits

**Action**: Investigate root cause, see [Runbooks](./RUNBOOKS.md) for detailed procedures

#### 4. Check Resource Usage (5 minutes)

```bash
# CPU and memory usage
kubectl top pods -n honua-process-framework

# Expected: CPU < 70%, Memory < 80% of limits
# NAME                                      CPU(cores)   MEMORY(bytes)
# honua-process-framework-5d4f8c9b7-abc12   450m         1200Mi
# honua-process-framework-5d4f8c9b7-def34   480m         1150Mi
# honua-process-framework-5d4f8c9b7-ghi56   420m         1100Mi
# redis-0                                   50m          450Mi

# Redis memory usage
kubectl exec -n honua-process-framework redis-0 -- redis-cli INFO memory | grep used_memory_human
# Expected: < 80% of maxmemory
```

**Action if high usage**: Consider scaling (see [Scaling](#scaling-operations))

### Weekly Checklist

Perform these tasks once per week (Monday morning recommended):

#### 1. Review Process Metrics (15 minutes)

```bash
# Average process duration (last 7 days)
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=avg_over_time(honua_process_duration_seconds[7d])' | jq

# Process success rate (last 7 days)
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=sum(rate(honua_process_completed_total[7d])) / sum(rate(honua_process_started_total[7d]))' | jq

# LLM token usage (last 7 days)
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=sum(increase(honua_llm_tokens_used_total[7d]))' | jq
```

**Generate Weekly Report**:
```bash
# Export metrics to CSV
curl -G http://prometheus:9090/api/v1/query_range \
  --data-urlencode 'query=honua_process_completed_total' \
  --data-urlencode 'start=2025-10-10T00:00:00Z' \
  --data-urlencode 'end=2025-10-17T00:00:00Z' \
  --data-urlencode 'step=1h' > weekly_metrics.json

# Parse and format
jq -r '.data.result[0].values[] | @csv' weekly_metrics.json > weekly_metrics.csv
```

#### 2. Review Failed Processes (20 minutes)

```bash
# Get all failed processes in the last week
curl "http://honua-process-framework:9090/api/processes?status=Failed&since=7d" | jq > failed_processes.json

# Group by failure reason
jq -r 'group_by(.errorMessage) | map({error: .[0].errorMessage, count: length})' failed_processes.json

# Common patterns to look for:
# - LLM API timeouts
# - Redis connection failures
# - Invalid configuration
# - Resource exhaustion
```

**Action**: Document recurring issues, create tickets for fixes

#### 3. Cleanup Old Process Data (10 minutes)

```bash
# Check Redis key count
kubectl exec -n honua-process-framework redis-0 -- redis-cli DBSIZE

# Check TTL on sample keys
kubectl exec -n honua-process-framework redis-0 -- redis-cli --scan --pattern "honua:process:*" | head -5 | xargs -I {} kubectl exec -n honua-process-framework redis-0 -- redis-cli TTL {}

# Delete completed processes older than 30 days (manual cleanup if needed)
curl -X DELETE "http://honua-process-framework:9090/api/processes?status=Completed&olderThan=30d"
```

**Note**: Redis TTL should handle most cleanup automatically (default 24 hours)

#### 4. Review Security and Compliance (15 minutes)

```bash
# Check for API key rotations needed
kubectl get secret -n honua-process-framework azure-openai-secret -o jsonpath='{.metadata.creationTimestamp}'

# Check TLS certificate expiration
echo | openssl s_client -connect redis.example.com:6380 2>/dev/null | openssl x509 -noout -dates

# Review access logs for suspicious activity
kubectl logs -n honua-process-framework deployment/honua-process-framework --since=7d | grep "401\|403\|429"
```

**Action**: Rotate credentials if > 90 days old, renew certificates if < 30 days remaining

### Monthly Checklist

#### 1. Capacity Planning Review (30 minutes)

```bash
# Historical growth trends
curl -G http://prometheus:9090/api/v1/query_range \
  --data-urlencode 'query=sum(honua_active_processes)' \
  --data-urlencode 'start=2025-09-01T00:00:00Z' \
  --data-urlencode 'end=2025-10-01T00:00:00Z' \
  --data-urlencode 'step=1d' > monthly_capacity.json

# Calculate growth rate
jq -r '.data.result[0].values | (.[length-1][1] - .[0][1]) / .[0][1] * 100' monthly_capacity.json
# Output: 15.3 (meaning 15.3% growth)
```

**Capacity Planning Decision Matrix**:
| Growth Rate | Action |
|-------------|--------|
| < 10% | No action needed |
| 10-30% | Plan for scaling in next quarter |
| 30-50% | Scale up next month |
| > 50% | Scale up immediately |

#### 2. Dependency Updates (20 minutes)

```bash
# Check for .NET updates
dotnet list package --outdated

# Check for Redis updates
docker pull redis:7.2-alpine
docker images redis --format "{{.Tag}}\t{{.CreatedAt}}"

# Check for Semantic Kernel updates
dotnet list package | grep Microsoft.SemanticKernel
```

**Update Strategy**:
1. Test updates in staging environment
2. Review changelog for breaking changes
3. Schedule maintenance window for production update
4. Have rollback plan ready

#### 3. Disaster Recovery Test (45 minutes)

See [Disaster Recovery Testing](#disaster-recovery-testing) section below.

## Monitoring Dashboards

### Grafana Dashboard Overview

The main Grafana dashboard (`honua-process-framework`) has 5 sections:

#### 1. System Health (Top Row)

**Panels**:
- **Active Processes**: Current number of running processes
- **Success Rate (1h)**: Percentage of successful completions
- **Error Rate (1h)**: Percentage of failures
- **Average Duration**: Mean process execution time

**Normal Values**:
- Active Processes: 0-50 (depends on load)
- Success Rate: > 95%
- Error Rate: < 5%
- Average Duration: 10-30 minutes (process-specific)

**Alert Thresholds**:
- Success Rate < 90%: Warning
- Success Rate < 80%: Critical
- Error Rate > 10%: Warning
- Error Rate > 20%: Critical

#### 2. Process Metrics (Second Row)

**Panels**:
- **Process Starts (rate)**: Processes started per minute
- **Process Completions (rate)**: Processes completed per minute
- **Process Timeouts**: Number of timeouts
- **Queue Depth**: Pending processes waiting to start

**What to Watch**:
- Sudden spike in starts without matching completions (possible stuck processes)
- Increasing queue depth (system overload)
- Rising timeout count (performance degradation)

#### 3. Resource Usage (Third Row)

**Panels**:
- **CPU Usage**: Per-pod CPU utilization
- **Memory Usage**: Per-pod memory utilization
- **Redis Memory**: Redis memory usage
- **Redis Operations**: Redis operations per second

**Capacity Indicators**:
- CPU > 70% sustained: Consider scaling
- Memory > 80%: Investigate memory leaks, consider scaling
- Redis Memory > 80%: Increase Redis memory or reduce TTL
- Redis Ops > 10K/sec: Consider Redis cluster

#### 4. LLM API Metrics (Fourth Row)

**Panels**:
- **LLM API Calls**: Requests per minute to LLM providers
- **LLM Token Usage**: Tokens consumed per minute
- **LLM API Latency**: P50/P95/P99 latency
- **LLM API Errors**: Error rate by provider

**Cost Tracking**:
- Monitor token usage for billing
- GPT-4: ~$0.03 per 1K input tokens, ~$0.06 per 1K output tokens
- Daily cost estimate: (tokens_per_day / 1000) × cost_per_1k

**Rate Limit Management**:
- Azure OpenAI: Typically 60 req/min, 90K tokens/min
- OpenAI: Varies by tier (check dashboard.openai.com)
- If hitting limits: Reduce `MaxConcurrentProcesses` or add delays

#### 5. Process Type Breakdown (Fifth Row)

**Panels**:
- **Deployment Processes**: Count by status
- **Upgrade Processes**: Count by status
- **Metadata Processes**: Count by status
- **GitOps Processes**: Count by status
- **Benchmark Processes**: Count by status

**Use Case**: Identify which process types are most active and resource-intensive

### Prometheus Queries

#### Essential Queries

**Active processes**:
```promql
honua_active_processes
```

**Success rate (last hour)**:
```promql
sum(rate(honua_process_completed_total[1h])) / sum(rate(honua_process_started_total[1h])) * 100
```

**Average process duration**:
```promql
avg(honua_process_duration_seconds)
```

**P95 process duration**:
```promql
histogram_quantile(0.95, rate(honua_process_duration_seconds_bucket[5m]))
```

**Failure rate by process type**:
```promql
sum by (process_type) (rate(honua_process_failures_total[5m]))
```

**Redis connection status**:
```promql
redis_up
```

**Memory usage percentage**:
```promql
(container_memory_usage_bytes{pod=~"honua-process-framework.*"} / container_spec_memory_limit_bytes{pod=~"honua-process-framework.*"}) * 100
```

### Log Aggregation

#### Kubernetes Logs

```bash
# Real-time logs from all pods
kubectl logs -f -l app=honua-process-framework -n honua-process-framework

# Logs from specific pod
kubectl logs -n honua-process-framework honua-process-framework-5d4f8c9b7-abc12

# Logs with timestamps
kubectl logs -n honua-process-framework honua-process-framework-5d4f8c9b7-abc12 --timestamps

# Previous pod logs (after crash)
kubectl logs -n honua-process-framework honua-process-framework-5d4f8c9b7-abc12 --previous
```

#### Log Patterns to Monitor

**Errors**:
```bash
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep -i "error\|exception\|failed"
```

**Process lifecycle**:
```bash
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "Process.*started\|completed\|failed"
```

**Redis operations**:
```bash
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "Redis"
```

**LLM API calls**:
```bash
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "LLM\|OpenAI\|Azure"
```

#### Centralized Logging (ELK/Loki)

**Example Loki Query**:
```logql
{namespace="honua-process-framework", app="honua-process-framework"}
|= "error" or "exception"
| json
| process_id != ""
| line_format "{{.timestamp}} [{{.level}}] {{.process_id}}: {{.message}}"
```

## Common Troubleshooting Scenarios

### Scenario 1: Process Stuck in "Running" State

**Symptoms**:
- Process shows as "Running" for > 4 hours
- No progress updates in logs
- Process not responding to status checks

**Diagnosis**:
```bash
# Get process details
PROCESS_ID="abc-123-def-456"
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | jq

# Check process logs
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep $PROCESS_ID

# Check if step is waiting for external response
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "$PROCESS_ID.*waiting"
```

**Root Causes**:
1. **LLM API timeout**: API call hanging without response
2. **Deadlock**: Two steps waiting on each other
3. **Resource exhaustion**: Process paused due to memory/CPU limits
4. **Network partition**: Lost connection to Redis or external service

**Resolution**:
```bash
# Option 1: Cancel the process
curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/cancel"

# Option 2: Force restart the step (if supported)
curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/retry-current-step"

# Option 3: Restart the pod (last resort)
kubectl delete pod -n honua-process-framework honua-process-framework-5d4f8c9b7-abc12
```

**Prevention**:
- Set appropriate timeouts for all process steps
- Implement health checks for long-running steps
- Add progress reporting in multi-step operations

### Scenario 2: Process Timeout

**Symptoms**:
- Process fails with "Timeout exceeded" error
- Process duration > configured timeout
- Logs show no errors until timeout

**Diagnosis**:
```bash
# Check process timeout configuration
curl "http://honua-process-framework:9090/api/config" | jq '.processFramework.defaultTimeoutMinutes'

# Check actual process duration
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | jq '.duration'

# Identify slow step
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "$PROCESS_ID.*step.*duration"
```

**Root Causes**:
1. **LLM API slow response**: GPT-4 taking > 60 seconds per call
2. **Large dataset processing**: Processing huge files
3. **Network latency**: Slow downloads from external sources
4. **Insufficient timeout**: Timeout too aggressive for legitimate use case

**Resolution**:
```bash
# Increase timeout for specific process type
kubectl edit configmap -n honua-process-framework process-config

# Add or update:
# ProcessFramework:
#   ProcessTimeouts:
#     DeploymentProcess: 180
#     UpgradeProcess: 240

# Restart pods to apply config
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# Retry failed process
curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/retry"
```

**Prevention**:
- Set realistic timeouts based on historical data
- Use streaming LLM responses for long generations
- Implement checkpoint/resume for long operations
- Break down large processes into smaller steps

### Scenario 3: Redis Connection Issues

**Symptoms**:
- Errors: "Redis connection failed" or "RedisConnectionException"
- Process state not persisting
- Intermittent process failures

**Diagnosis**:
```bash
# Check Redis health
kubectl exec -n honua-process-framework redis-0 -- redis-cli ping

# Check Redis connections
kubectl exec -n honua-process-framework redis-0 -- redis-cli CLIENT LIST

# Check Redis memory
kubectl exec -n honua-process-framework redis-0 -- redis-cli INFO memory

# Check connection string
kubectl get secret -n honua-process-framework redis-secret -o jsonpath='{.data.connection-string}' | base64 -d
```

**Root Causes**:
1. **Redis down**: Pod crashed or restarting
2. **Network partition**: Firewall blocking connection
3. **Authentication failure**: Incorrect password
4. **Connection pool exhausted**: Too many connections
5. **Memory exhaustion**: Redis out of memory

**Resolution**:

**If Redis is down**:
```bash
# Check pod status
kubectl get pod -n honua-process-framework redis-0

# Check logs
kubectl logs -n honua-process-framework redis-0

# Restart if necessary
kubectl delete pod -n honua-process-framework redis-0
```

**If connection string is wrong**:
```bash
# Update secret
kubectl create secret generic redis-secret \
  --from-literal=connection-string="redis:6379,ssl=false,abortConnect=false" \
  --dry-run=client -o yaml | kubectl apply -f - -n honua-process-framework

# Restart pods
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework
```

**If memory exhausted**:
```bash
# Check memory usage
kubectl exec -n honua-process-framework redis-0 -- redis-cli INFO memory | grep used_memory_human

# Clear old keys (if TTL not working)
kubectl exec -n honua-process-framework redis-0 -- redis-cli --scan --pattern "honua:process:*" | xargs kubectl exec -n honua-process-framework redis-0 -- redis-cli DEL

# Or increase Redis memory
kubectl edit statefulset -n honua-process-framework redis
# Update resources.limits.memory
```

**Prevention**:
- Use Redis Sentinel or Cluster for HA
- Monitor Redis memory usage
- Set appropriate maxmemory-policy
- Enable connection pooling and limits

### Scenario 4: LLM API Quota/Rate Limits

**Symptoms**:
- Errors: "429 Too Many Requests" or "Rate limit exceeded"
- Processes failing in LLM-dependent steps
- High failure rate during peak hours

**Diagnosis**:
```bash
# Check LLM error rate
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=sum(rate(honua_llm_errors_total{error_type="rate_limit"}[5m]))' | jq

# Check current quota usage (Azure OpenAI)
az cognitiveservices account list-usage \
  --name myresource \
  --resource-group honua

# Check token usage rate
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=sum(rate(honua_llm_tokens_used_total[1m]))' | jq
```

**Root Causes**:
1. **Quota exceeded**: Monthly token limit reached
2. **Rate limit hit**: Too many requests per minute
3. **Concurrent request limit**: Too many simultaneous calls
4. **No fallback provider**: Primary provider limited, no fallback

**Resolution**:

**Immediate (reduce load)**:
```bash
# Reduce concurrent processes
kubectl edit configmap -n honua-process-framework process-config
# Set MaxConcurrentProcesses: 5

kubectl rollout restart deployment/honua-process-framework -n honua-process-framework
```

**Short-term (add fallback)**:
```bash
# Update config to use fallback provider
kubectl edit configmap -n honua-process-framework process-config
# LlmProvider:
#   Provider: Azure
#   FallbackProvider: OpenAI

kubectl rollout restart deployment/honua-process-framework -n honua-process-framework
```

**Long-term (increase quota)**:
```bash
# Azure OpenAI: Request quota increase in Azure Portal
# Go to Cognitive Services → Quotas → Request increase

# OpenAI: Upgrade to higher tier or add payment method
```

**Prevention**:
- Monitor token usage trends
- Set up alerts for > 80% quota usage
- Configure fallback providers
- Implement exponential backoff and retry logic
- Use token budgets per process type

### Scenario 5: High Memory Usage / OOMKilled

**Symptoms**:
- Pods restarting with OOMKilled reason
- Memory usage > 90% of limit
- Slow performance before crash

**Diagnosis**:
```bash
# Check pod memory usage
kubectl top pods -n honua-process-framework

# Check OOMKilled events
kubectl get events -n honua-process-framework | grep OOMKilled

# Check memory limits
kubectl get pod -n honua-process-framework honua-process-framework-5d4f8c9b7-abc12 -o jsonpath='{.spec.containers[0].resources.limits.memory}'

# Analyze heap dump (if available)
kubectl exec -n honua-process-framework honua-process-framework-5d4f8c9b7-abc12 -- dotnet-dump collect -p 1
```

**Root Causes**:
1. **Memory leak**: Objects not being garbage collected
2. **Large payloads**: Processing huge LLM responses or datasets
3. **Too many concurrent processes**: Each process consuming memory
4. **Insufficient memory limits**: Limits too low for workload

**Resolution**:

**Immediate (increase limits)**:
```bash
# Update deployment with higher memory limits
kubectl set resources deployment/honua-process-framework -n honua-process-framework \
  --limits=memory=8Gi --requests=memory=4Gi

# Verify
kubectl get deployment -n honua-process-framework honua-process-framework -o jsonpath='{.spec.template.spec.containers[0].resources}'
```

**Identify memory leak**:
```bash
# Enable GC logging
kubectl set env deployment/honua-process-framework -n honua-process-framework \
  DOTNET_gcServer=1 \
  DOTNET_GCHeapCount=4

# Analyze GC events
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "GC"
```

**Reduce memory usage**:
```bash
# Reduce concurrent processes
kubectl edit configmap -n honua-process-framework process-config
# MaxConcurrentProcesses: 10 -> 5

# Enable process state cleanup
# ProcessFramework:
#   StateCheckpointIntervalSeconds: 30
#   MaxStateSizeKB: 1024

kubectl rollout restart deployment/honua-process-framework -n honua-process-framework
```

**Prevention**:
- Set appropriate memory requests and limits
- Implement memory profiling in staging
- Clean up large objects after use
- Use streaming for large payloads
- Monitor memory trends

### Scenario 6: Process State Corruption

**Symptoms**:
- Processes fail with "Invalid state" errors
- State deserialization exceptions
- Processes can't resume after pause

**Diagnosis**:
```bash
# Get process state from Redis
kubectl exec -n honua-process-framework redis-0 -- redis-cli GET "honua:process:${PROCESS_ID}"

# Check for corrupted JSON
kubectl exec -n honua-process-framework redis-0 -- redis-cli GET "honua:process:${PROCESS_ID}" | jq .
# If jq fails, state is corrupted

# Check Redis logs for errors
kubectl logs -n honua-process-framework redis-0 | grep ERROR
```

**Root Causes**:
1. **Schema change**: State class changed without migration
2. **Partial write**: Redis write interrupted
3. **Encoding issue**: Character encoding mismatch
4. **Manual modification**: Someone edited state directly

**Resolution**:
```bash
# Option 1: Delete corrupted state and restart process
kubectl exec -n honua-process-framework redis-0 -- redis-cli DEL "honua:process:${PROCESS_ID}"
curl -X POST "http://honua-process-framework:9090/api/processes" -d '{"processType":"Deployment", ...}'

# Option 2: Fix state manually (advanced)
# Get current state
kubectl exec -n honua-process-framework redis-0 -- redis-cli GET "honua:process:${PROCESS_ID}" > state.json

# Edit state.json to fix issues
nano state.json

# Set corrected state
cat state.json | kubectl exec -i -n honua-process-framework redis-0 -- redis-cli -x SET "honua:process:${PROCESS_ID}"
```

**Prevention**:
- Implement state schema versioning
- Use atomic writes to Redis
- Add state validation before deserialization
- Test state migrations in staging

## Performance Tuning

### CPU Optimization

#### Identify CPU Bottlenecks

```bash
# Check CPU usage per pod
kubectl top pods -n honua-process-framework --sort-by=cpu

# Profile CPU usage (requires dotnet-trace)
kubectl exec -n honua-process-framework honua-process-framework-5d4f8c9b7-abc12 -- \
  dotnet-trace collect --duration 00:00:30 -p 1 --format speedscope

# Analyze hotspots in speedscope.app
```

#### Tuning Recommendations

**1. Adjust Thread Pool Size**:
```bash
kubectl set env deployment/honua-process-framework -n honua-process-framework \
  DOTNET_ThreadPool_MinThreads=50 \
  DOTNET_ThreadPool_MaxThreads=500
```

**2. Enable Server GC**:
```bash
kubectl set env deployment/honua-process-framework -n honua-process-framework \
  DOTNET_gcServer=1
```

**3. Optimize Concurrent Processes**:
```json
{
  "ProcessFramework": {
    "MaxConcurrentProcesses": 20,
    "ProcessStepParallelism": 4
  }
}
```

### Memory Optimization

#### Identify Memory Issues

```bash
# Check memory usage trends
kubectl top pods -n honua-process-framework --sort-by=memory

# Take heap snapshot
kubectl exec -n honua-process-framework honua-process-framework-5d4f8c9b7-abc12 -- \
  dotnet-dump collect -p 1

# Analyze with dotnet-dump
kubectl cp honua-process-framework/honua-process-framework-5d4f8c9b7-abc12:/tmp/core_20251017_123456 ./dump
dotnet-dump analyze ./dump
> dumpheap -stat
```

#### Tuning Recommendations

**1. Configure GC**:
```bash
kubectl set env deployment/honua-process-framework -n honua-process-framework \
  DOTNET_gcServer=1 \
  DOTNET_GCHeapCount=4 \
  DOTNET_GCHeapAffinitizeMask=0xFF
```

**2. Set Memory Limits**:
```yaml
resources:
  requests:
    memory: "2Gi"
  limits:
    memory: "6Gi"  # Leave headroom for GC
```

**3. Limit State Size**:
```json
{
  "ProcessFramework": {
    "MaxStateSizeKB": 1024,
    "EnableStateCompression": true
  }
}
```

### Redis Optimization

#### Identify Redis Bottlenecks

```bash
# Check Redis latency
kubectl exec -n honua-process-framework redis-0 -- redis-cli --latency

# Check slow queries
kubectl exec -n honua-process-framework redis-0 -- redis-cli SLOWLOG GET 10

# Monitor Redis stats
kubectl exec -n honua-process-framework redis-0 -- redis-cli INFO stats
```

#### Tuning Recommendations

**1. Enable Pipelining**:
```json
{
  "Redis": {
    "ConnectionString": "redis:6379,ssl=false,abortConnect=false,allowAdmin=true",
    "EnablePipelining": true,
    "PipelineBatchSize": 100
  }
}
```

**2. Increase Connection Pool**:
```json
{
  "Redis": {
    "MinConnectionPoolSize": 10,
    "MaxConnectionPoolSize": 50
  }
}
```

**3. Use Redis Cluster for Scale**:
```bash
# Deploy 6-node Redis cluster (3 masters, 3 replicas)
# See deployment guide for details
```

### LLM API Optimization

#### Reduce Token Usage

**1. Use Smaller Models Where Possible**:
```json
{
  "LlmProvider": {
    "ModelSelection": {
      "SimpleTask": "gpt-3.5-turbo",
      "ComplexTask": "gpt-4o",
      "CodeGeneration": "gpt-4o"
    }
  }
}
```

**2. Limit Response Length**:
```json
{
  "LlmProvider": {
    "DefaultMaxTokens": 2000,
    "MaxTokensByStep": {
      "ValidateRequirements": 500,
      "GenerateInfrastructure": 4000
    }
  }
}
```

**3. Cache LLM Responses**:
```json
{
  "ProcessFramework": {
    "LlmCaching": {
      "Enabled": true,
      "TtlMinutes": 60
    }
  }
}
```

#### Optimize API Latency

**1. Use Streaming Responses**:
```csharp
// Enable in LLM client configuration
var options = new OpenAIPromptExecutionSettings
{
    Temperature = 0.2,
    MaxTokens = 2000,
    StreamResponse = true  // Enable streaming
};
```

**2. Regional Deployment**:
- Deploy Process Framework in same region as LLM API
- Azure OpenAI: Use nearest region (East US, West Europe, etc.)
- Measure latency: Should be < 100ms baseline

**3. Connection Pooling**:
```json
{
  "LlmProvider": {
    "HttpClient": {
      "MaxConnectionsPerServer": 50,
      "PooledConnectionLifetime": 300
    }
  }
}
```

## Backup and Recovery

### Redis State Backup

#### Automated Backups (Recommended)

**Option 1: Redis RDB Snapshots**

Configure Redis for periodic snapshots:
```bash
# Edit Redis config
kubectl exec -n honua-process-framework redis-0 -- sh -c 'cat >> /etc/redis/redis.conf <<EOF
save 900 1      # Save after 900 seconds if at least 1 key changed
save 300 10     # Save after 300 seconds if at least 10 keys changed
save 60 10000   # Save after 60 seconds if at least 10000 keys changed
EOF'

# Restart Redis to apply
kubectl delete pod -n honua-process-framework redis-0
```

**Backup script** (run daily via CronJob):
```bash
#!/bin/bash
# backup-redis.sh

BACKUP_DIR="/backups/redis"
DATE=$(date +%Y%m%d-%H%M%S)

# Trigger Redis save
kubectl exec -n honua-process-framework redis-0 -- redis-cli BGSAVE

# Wait for save to complete
while true; do
  LASTSAVE=$(kubectl exec -n honua-process-framework redis-0 -- redis-cli LASTSAVE)
  sleep 1
  NEWSAVE=$(kubectl exec -n honua-process-framework redis-0 -- redis-cli LASTSAVE)
  if [ "$LASTSAVE" != "$NEWSAVE" ]; then
    break
  fi
done

# Copy dump file
kubectl cp honua-process-framework/redis-0:/data/dump.rdb "$BACKUP_DIR/dump-$DATE.rdb"

# Upload to S3/Azure Blob
aws s3 cp "$BACKUP_DIR/dump-$DATE.rdb" s3://honua-backups/redis/

# Keep last 7 days locally
find "$BACKUP_DIR" -name "dump-*.rdb" -mtime +7 -delete

echo "Backup completed: dump-$DATE.rdb"
```

**Kubernetes CronJob**:
```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: redis-backup
  namespace: honua-process-framework
spec:
  schedule: "0 2 * * *"  # Daily at 2 AM
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: backup
            image: amazon/aws-cli:latest
            command: ["/bin/bash", "/scripts/backup-redis.sh"]
            volumeMounts:
            - name: backup-script
              mountPath: /scripts
            - name: backup-storage
              mountPath: /backups
          volumes:
          - name: backup-script
            configMap:
              name: backup-scripts
          - name: backup-storage
            persistentVolumeClaim:
              claimName: backup-pvc
          restartPolicy: OnFailure
```

#### Manual Backup

```bash
# Take immediate backup
kubectl exec -n honua-process-framework redis-0 -- redis-cli BGSAVE

# Wait for completion
kubectl exec -n honua-process-framework redis-0 -- redis-cli LASTSAVE

# Copy dump file
kubectl cp honua-process-framework/redis-0:/data/dump.rdb ./dump.rdb

# Compress and store
gzip dump.rdb
aws s3 cp dump.rdb.gz s3://honua-backups/redis/manual-backup-$(date +%Y%m%d).rdb.gz
```

### Restore from Backup

#### Restore Redis from RDB

```bash
# Stop application (to prevent writes during restore)
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=0

# Copy backup to Redis pod
kubectl cp ./dump.rdb honua-process-framework/redis-0:/data/dump.rdb

# Restart Redis to load backup
kubectl delete pod -n honua-process-framework redis-0

# Wait for Redis to start
kubectl wait --for=condition=ready pod/redis-0 -n honua-process-framework --timeout=60s

# Verify data
kubectl exec -n honua-process-framework redis-0 -- redis-cli DBSIZE

# Resume application
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=3
```

### Configuration Backup

#### Backup Configuration

```bash
#!/bin/bash
# backup-config.sh

BACKUP_DIR="/backups/config"
DATE=$(date +%Y%m%d-%H%M%S)

# Backup ConfigMaps
kubectl get configmap -n honua-process-framework -o yaml > "$BACKUP_DIR/configmaps-$DATE.yaml"

# Backup Secrets (encrypted)
kubectl get secret -n honua-process-framework -o yaml > "$BACKUP_DIR/secrets-$DATE.yaml"
# Encrypt before storing
gpg --encrypt --recipient ops@example.com "$BACKUP_DIR/secrets-$DATE.yaml"
rm "$BACKUP_DIR/secrets-$DATE.yaml"

# Backup Deployments
kubectl get deployment,statefulset,service -n honua-process-framework -o yaml > "$BACKUP_DIR/k8s-resources-$DATE.yaml"

# Upload to S3
aws s3 sync "$BACKUP_DIR" s3://honua-backups/config/

echo "Configuration backup completed"
```

#### Restore Configuration

```bash
# Restore ConfigMaps
kubectl apply -f /backups/config/configmaps-20251017.yaml

# Restore Secrets (decrypt first)
gpg --decrypt /backups/config/secrets-20251017.yaml.gpg > secrets.yaml
kubectl apply -f secrets.yaml
rm secrets.yaml

# Restart pods to load new config
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework
```

### Disaster Recovery Testing

#### Monthly DR Drill Procedure

**Goal**: Verify full system recovery from catastrophic failure

**Steps**:

1. **Preparation** (5 minutes):
```bash
# Create test namespace
kubectl create namespace honua-process-framework-dr-test

# Deploy Redis from backup
kubectl apply -f k8s/redis-deployment.yaml -n honua-process-framework-dr-test
```

2. **Restore Redis Data** (10 minutes):
```bash
# Download latest backup
aws s3 cp s3://honua-backups/redis/dump-latest.rdb ./dump.rdb

# Restore to test Redis
kubectl cp ./dump.rdb honua-process-framework-dr-test/redis-0:/data/dump.rdb
kubectl delete pod -n honua-process-framework-dr-test redis-0
```

3. **Deploy Application** (10 minutes):
```bash
# Restore configuration
kubectl apply -f /backups/config/configmaps-latest.yaml -n honua-process-framework-dr-test
gpg --decrypt /backups/config/secrets-latest.yaml.gpg | kubectl apply -f - -n honua-process-framework-dr-test

# Deploy application
kubectl apply -f k8s/process-framework-deployment.yaml -n honua-process-framework-dr-test
```

4. **Validation** (10 minutes):
```bash
# Check all pods running
kubectl get pods -n honua-process-framework-dr-test

# Verify Redis data
kubectl exec -n honua-process-framework-dr-test redis-0 -- redis-cli DBSIZE

# Test process execution
curl -X POST http://test-process-framework:9090/api/processes -d '{"processType":"Test"}'

# Verify logs
kubectl logs -n honua-process-framework-dr-test -l app=honua-process-framework
```

5. **Cleanup** (5 minutes):
```bash
# Delete test namespace
kubectl delete namespace honua-process-framework-dr-test
```

**Success Criteria**:
- ✅ All pods running within 5 minutes
- ✅ Redis data restored correctly (DBSIZE matches)
- ✅ Application can execute new process
- ✅ No errors in logs

**Document Results**:
```bash
# Create DR test report
cat > dr-test-report-$(date +%Y%m%d).md <<EOF
# DR Test Report - $(date +%Y-%m-%d)

## Test Results
- Redis restore time: X minutes
- Application deploy time: Y minutes
- Total recovery time: Z minutes

## Issues Encountered
- (List any issues)

## Action Items
- (List any improvements needed)

## Next Test Date
- $(date -d "+1 month" +%Y-%m-%d)
EOF
```

## Maintenance Procedures

### Planned Downtime

#### Maintenance Window Procedure

**Pre-Maintenance** (1 week before):
1. Announce maintenance window to stakeholders
2. Schedule during low-traffic period (e.g., Sunday 2-4 AM)
3. Ensure all backups are recent and verified
4. Prepare rollback plan

**During Maintenance**:

```bash
# 1. Stop accepting new processes (15 minutes before)
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=0

# 2. Wait for active processes to complete (check every 5 minutes)
while true; do
  ACTIVE=$(curl -s http://honua-process-framework:9090/api/processes/active | jq '. | length')
  echo "Active processes: $ACTIVE"
  if [ "$ACTIVE" -eq 0 ]; then
    break
  fi
  sleep 300
done

# 3. Perform maintenance (example: upgrade Redis)
kubectl apply -f k8s/redis-7.4-deployment.yaml -n honua-process-framework

# 4. Verify new version
kubectl get pods -n honua-process-framework
kubectl logs -n honua-process-framework redis-0

# 5. Resume application
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=3

# 6. Smoke test
curl -X POST http://honua-process-framework:9090/api/processes -d '{"processType":"Test"}'
```

**Post-Maintenance**:
1. Monitor for 30 minutes
2. Verify all metrics normal
3. Announce completion
4. Document any issues

### Rolling Updates

For zero-downtime updates:

```bash
# 1. Update deployment with new image
kubectl set image deployment/honua-process-framework -n honua-process-framework \
  honua-cli=honua/cli-ai:v2.0.0

# 2. Monitor rollout
kubectl rollout status deployment/honua-process-framework -n honua-process-framework

# 3. If issues, rollback immediately
kubectl rollout undo deployment/honua-process-framework -n honua-process-framework
```

## Incident Response

### Incident Severity Levels

| Severity | Description | Response Time | Example |
|----------|-------------|---------------|---------|
| **P1 - Critical** | Complete outage | 15 minutes | All processes failing |
| **P2 - High** | Major degradation | 1 hour | > 50% failure rate |
| **P3 - Medium** | Partial degradation | 4 hours | Single process type failing |
| **P4 - Low** | Minor issue | 1 business day | Non-critical alerts |

### Incident Response Checklist

#### P1 - Critical Incident

**Immediate Actions** (0-15 minutes):
1. ✅ Acknowledge alert in PagerDuty/Slack
2. ✅ Check system status (pods, Redis, LLM APIs)
3. ✅ Check monitoring dashboards for obvious issues
4. ✅ Notify stakeholders (if needed)

**Investigation** (15-60 minutes):
1. ✅ Collect logs from all pods
2. ✅ Check recent changes (deployments, config updates)
3. ✅ Review error patterns
4. ✅ Identify root cause

**Resolution** (60+ minutes):
1. ✅ Apply fix (restart pods, update config, etc.)
2. ✅ Verify fix resolved issue
3. ✅ Monitor for 30 minutes
4. ✅ Update stakeholders

**Post-Incident**:
1. ✅ Document root cause
2. ✅ Create post-mortem
3. ✅ Identify preventive measures
4. ✅ Update runbooks

### Escalation Path

1. **On-Call Engineer** (initial response)
2. **Senior DevOps Engineer** (if not resolved in 30 minutes)
3. **Platform Lead** (if not resolved in 1 hour)
4. **Engineering Manager** (if critical business impact)

## Next Steps

- [Runbooks](./RUNBOOKS.md) - Detailed step-by-step incident response procedures
- [Deployment Guide](../deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md) - Initial setup and configuration
- [Quick Start](../quickstart/PROCESS_FRAMEWORK_QUICKSTART.md) - Getting started guide

---

**Document Version**: 1.0
**Last Updated**: 2025-10-17
**Maintainer**: Honua Operations Team
