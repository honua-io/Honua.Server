# Honua Process Framework - Operational Runbooks

**Last Updated**: 2025-10-17
**Status**: Production Ready
**Version**: 1.0

## Table of Contents

- [Overview](#overview)
- [Runbook Format](#runbook-format)
- [Runbook 1: High Process Failure Rate Investigation](#runbook-1-high-process-failure-rate-investigation)
- [Runbook 2: Process Timeout Recovery](#runbook-2-process-timeout-recovery)
- [Runbook 3: Redis Failover Procedure](#runbook-3-redis-failover-procedure)
- [Runbook 4: LLM Provider Failover](#runbook-4-llm-provider-failover)
- [Runbook 5: Emergency Process Cancellation](#runbook-5-emergency-process-cancellation)
- [Runbook 6: Data Recovery from Redis](#runbook-6-data-recovery-from-redis)
- [Runbook 7: Pod Restart and Recovery](#runbook-7-pod-restart-and-recovery)
- [Runbook 8: Database Connection Failure](#runbook-8-database-connection-failure)

## Overview

This document provides step-by-step runbooks for common operational scenarios and incidents. Each runbook includes:
- **Severity**: Incident priority level
- **Symptoms**: How to identify the issue
- **Prerequisites**: What you need before starting
- **Procedure**: Step-by-step resolution
- **Validation**: How to verify the fix
- **Prevention**: How to avoid recurrence

**When to Use**: Follow these runbooks when monitoring alerts fire or users report issues.

## Runbook Format

Each runbook follows this structure:

```
## Runbook X: [Title]

**Severity**: P1/P2/P3/P4
**Estimated Time**: X minutes
**Roles Required**: On-call Engineer / Senior DevOps

### Symptoms
[List of observable symptoms]

### Prerequisites
[What you need to execute this runbook]

### Diagnosis
[How to confirm this is the right issue]

### Procedure
[Step-by-step resolution]

### Validation
[How to verify the fix worked]

### Prevention
[How to avoid this in the future]

### Related Runbooks
[Links to related procedures]
```

---

## Runbook 1: High Process Failure Rate Investigation

**Severity**: P2 (High)
**Estimated Time**: 30-45 minutes
**Roles Required**: On-call Engineer, DevOps Engineer

### Symptoms

- Alert: `HighProcessFailureRate` firing
- Grafana dashboard shows failure rate > 10%
- Multiple processes failing with same or similar errors
- User complaints about failed deployments/operations

### Prerequisites

- kubectl access to honua-process-framework namespace
- Access to Grafana dashboards
- Access to Prometheus metrics

### Diagnosis

**Step 1: Confirm the failure rate**
```bash
# Check current failure rate
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=rate(honua_process_failures_total[5m]) / rate(honua_process_started_total[5m]) * 100' | jq -r '.data.result[0].value[1]'

# Expected: If > 10%, proceed with investigation
```

**Step 2: Identify failure patterns**
```bash
# Get recent failed processes
curl "http://honua-process-framework:9090/api/processes?status=Failed&since=1h" | jq -r '.[] | "\(.processId) \(.processType) \(.errorMessage)"' > failures.txt

# Group by error message
sort failures.txt | uniq -c | sort -rn

# Common patterns:
# - "Redis connection failed" → Redis issue
# - "429 Too Many Requests" → LLM rate limit
# - "Timeout exceeded" → Performance issue
# - "Invalid configuration" → Config problem
```

**Step 3: Check system health**
```bash
# Check pod status
kubectl get pods -n honua-process-framework

# Check Redis
kubectl exec -n honua-process-framework redis-0 -- redis-cli ping

# Check recent deployments
kubectl rollout history deployment/honua-process-framework -n honua-process-framework
```

### Procedure

#### Scenario A: Redis Connection Failures

**Root Cause**: Redis unavailable or connection issues

**Resolution**:
```bash
# 1. Check Redis status
kubectl get pod -n honua-process-framework redis-0

# 2. If pod is not Running, check logs
kubectl logs -n honua-process-framework redis-0 --tail=100

# 3. Common issues:
#    - OOMKilled: Increase Redis memory
#    - CrashLoopBackOff: Check Redis config
#    - Pending: Check PVC availability

# 4. If Redis is crashed, restart it
kubectl delete pod -n honua-process-framework redis-0

# 5. Wait for Redis to be ready
kubectl wait --for=condition=ready pod/redis-0 -n honua-process-framework --timeout=120s

# 6. Verify connection
kubectl exec -n honua-process-framework redis-0 -- redis-cli ping

# 7. Check if processes resume
watch kubectl get pods -n honua-process-framework
```

**See also**: [Runbook 3: Redis Failover](#runbook-3-redis-failover-procedure)

#### Scenario B: LLM API Rate Limits

**Root Cause**: Too many LLM API requests

**Resolution**:
```bash
# 1. Check LLM error rate
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=sum(rate(honua_llm_errors_total{error_type="rate_limit"}[5m]))' | jq

# 2. Reduce concurrent processes immediately
kubectl edit configmap -n honua-process-framework process-config

# 3. Update MaxConcurrentProcesses (example: 20 → 10)
# ProcessFramework:
#   MaxConcurrentProcesses: 10

# 4. Restart pods to apply
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# 5. Monitor failure rate improvement
watch 'curl -s -G http://prometheus:9090/api/v1/query --data-urlencode "query=rate(honua_process_failures_total[5m])" | jq -r ".data.result[0].value[1]"'
```

**See also**: [Runbook 4: LLM Provider Failover](#runbook-4-llm-provider-failover)

#### Scenario C: Recent Deployment Issue

**Root Cause**: Bad deployment introduced bugs

**Resolution**:
```bash
# 1. Check deployment history
kubectl rollout history deployment/honua-process-framework -n honua-process-framework

# 2. Check recent changes (last 2 revisions)
kubectl rollout history deployment/honua-process-framework -n honua-process-framework --revision=10
kubectl rollout history deployment/honua-process-framework -n honua-process-framework --revision=9

# 3. If failures started after recent deployment, rollback
kubectl rollout undo deployment/honua-process-framework -n honua-process-framework

# 4. Monitor rollback progress
kubectl rollout status deployment/honua-process-framework -n honua-process-framework

# 5. Verify failure rate drops
watch 'curl -s -G http://prometheus:9090/api/v1/query --data-urlencode "query=rate(honua_process_failures_total[5m])" | jq -r ".data.result[0].value[1]"'
```

#### Scenario D: Configuration Error

**Root Cause**: Invalid configuration in ConfigMap/Secret

**Resolution**:
```bash
# 1. Check recent ConfigMap changes
kubectl get configmap -n honua-process-framework process-config -o yaml | grep "last-modified"

# 2. Get previous version from backup
aws s3 cp s3://honua-backups/config/configmaps-previous.yaml ./configmap-backup.yaml

# 3. Compare current vs backup
kubectl get configmap -n honua-process-framework process-config -o yaml > current-config.yaml
diff current-config.yaml configmap-backup.yaml

# 4. If config is wrong, restore from backup
kubectl apply -f configmap-backup.yaml

# 5. Restart pods to apply
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# 6. Monitor for improvements
kubectl logs -f -l app=honua-process-framework -n honua-process-framework
```

### Validation

**Success Criteria**:
- ✅ Failure rate < 5% for at least 10 minutes
- ✅ No new failures of the same type
- ✅ Grafana dashboard shows green metrics
- ✅ Alert stops firing

**Validation Commands**:
```bash
# Check failure rate
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=rate(honua_process_failures_total[10m]) / rate(honua_process_started_total[10m]) * 100' | jq -r '.data.result[0].value[1]'

# Check recent processes
curl "http://honua-process-framework:9090/api/processes?status=Completed&since=10m" | jq 'length'

# Should see successful completions
```

### Prevention

1. **Monitoring**: Ensure alert thresholds are appropriate (5% warning, 10% critical)
2. **Testing**: Test all changes in staging before production
3. **Rate Limiting**: Implement LLM request throttling
4. **Capacity Planning**: Monitor trends and scale proactively
5. **Configuration Validation**: Use admission controllers to validate ConfigMaps

### Related Runbooks
- [Runbook 3: Redis Failover](#runbook-3-redis-failover-procedure)
- [Runbook 4: LLM Provider Failover](#runbook-4-llm-provider-failover)
- [Runbook 7: Pod Restart and Recovery](#runbook-7-pod-restart-and-recovery)

---

## Runbook 2: Process Timeout Recovery

**Severity**: P3 (Medium)
**Estimated Time**: 15-30 minutes
**Roles Required**: On-call Engineer

### Symptoms

- Alert: `ProcessTimeout` firing
- Processes stuck in "Running" state for > configured timeout
- Logs show "Process exceeded timeout of X minutes"
- Users report processes not completing

### Prerequisites

- kubectl access to honua-process-framework namespace
- Process ID of stuck process
- Access to application logs

### Diagnosis

**Step 1: Identify timed out processes**
```bash
# Get processes that have been running > 60 minutes
curl "http://honua-process-framework:9090/api/processes?status=Running" | \
  jq -r '.[] | select(.startTime < (now - 3600)) | "\(.processId) \(.processType) \(.startTime)"'

# Example output:
# abc-123-def Running 2025-10-17T08:00:00Z
```

**Step 2: Check process logs**
```bash
PROCESS_ID="abc-123-def"

# Get all logs for this process
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep $PROCESS_ID > process.log

# Check for errors or hangs
tail -50 process.log

# Look for:
# - "Waiting for LLM response" (hung LLM call)
# - "Waiting for external resource" (hung I/O)
# - No recent logs (process truly stuck)
```

**Step 3: Determine root cause**
```bash
# Check if step is making progress
grep "$PROCESS_ID.*progress" process.log | tail -5

# Check last activity
grep "$PROCESS_ID" process.log | tail -1

# If last activity > 30 minutes ago, process is stuck
```

### Procedure

#### Option 1: Cancel and Retry (Recommended)

**When to use**: Process truly stuck, no progress for > 30 minutes

```bash
PROCESS_ID="abc-123-def"

# 1. Cancel the stuck process
curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/cancel"

# 2. Verify cancellation
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | jq '.status'
# Expected: "Cancelled"

# 3. Get process details for retry
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | jq '{processType, input}' > retry-request.json

# 4. Start new process with same input
curl -X POST "http://honua-process-framework:9090/api/processes" \
  -H "Content-Type: application/json" \
  -d @retry-request.json

# 5. Monitor new process
NEW_PROCESS_ID=$(curl -s "http://honua-process-framework:9090/api/processes?status=Running" | jq -r '.[0].processId')
watch "curl -s http://honua-process-framework:9090/api/processes/${NEW_PROCESS_ID} | jq '.status, .completionPercentage'"
```

#### Option 2: Retry Current Step

**When to use**: Process making progress but single step stuck

```bash
PROCESS_ID="abc-123-def"

# 1. Identify stuck step
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | jq '.currentStep'
# Example: "GenerateInfrastructure"

# 2. Retry just the current step
curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/retry-current-step"

# 3. Monitor progress
watch "curl -s http://honua-process-framework:9090/api/processes/${PROCESS_ID} | jq '.currentStep, .status'"
```

#### Option 3: Increase Timeout (Prevention)

**When to use**: Process is legitimate long-running, timeout too aggressive

```bash
# 1. Check historical duration for this process type
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=avg(honua_process_duration_seconds{process_type="DeploymentProcess"})' | jq

# If average is close to timeout, increase timeout

# 2. Update timeout configuration
kubectl edit configmap -n honua-process-framework process-config

# 3. Add/update process-specific timeout
# ProcessFramework:
#   ProcessTimeouts:
#     DeploymentProcess: 180  # Increase from 60 to 180 minutes

# 4. Restart pods to apply
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# 5. Retry the failed process
curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/retry"
```

### Validation

**Success Criteria**:
- ✅ Process completes or moves to next step
- ✅ No timeout alert for 15 minutes
- ✅ Process shows progress updates in logs

**Validation Commands**:
```bash
# Check process status
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | jq '{status, completionPercentage, currentStep}'

# Check for new progress
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "$PROCESS_ID" | tail -10

# Should see recent log entries with progress updates
```

### Prevention

1. **Tune Timeouts**: Set realistic timeouts based on P95 duration
2. **Progress Reporting**: Ensure all steps report progress regularly
3. **LLM Streaming**: Use streaming for long LLM responses
4. **Step Checkpointing**: Break long operations into smaller steps
5. **Monitoring**: Alert on processes approaching timeout threshold

### Related Runbooks
- [Runbook 1: High Process Failure Rate](#runbook-1-high-process-failure-rate-investigation)
- [Runbook 5: Emergency Process Cancellation](#runbook-5-emergency-process-cancellation)

---

## Runbook 3: Redis Failover Procedure

**Severity**: P1 (Critical)
**Estimated Time**: 10-20 minutes
**Roles Required**: Senior DevOps Engineer

### Symptoms

- Alert: `RedisConnectionFailure` firing
- All processes failing with "Redis connection failed"
- Redis pod in CrashLoopBackOff or NotReady state
- Complete system outage

### Prerequisites

- kubectl access with admin privileges
- Access to Redis backups
- Alternative Redis instance (if needed)

### Diagnosis

**Step 1: Check Redis health**
```bash
# Check pod status
kubectl get pod -n honua-process-framework redis-0

# Possible states:
# - Running but not ready: Redis starting or unhealthy
# - CrashLoopBackOff: Redis crashing on startup
# - Pending: PVC or node issues
# - Terminating: Redis shutting down
```

**Step 2: Check Redis logs**
```bash
# Get recent logs
kubectl logs -n honua-process-framework redis-0 --tail=100

# Common errors:
# - "Out of memory": maxmemory exceeded
# - "Can't open append-only file": Filesystem issue
# - "Fatal error": Redis bug or corruption
```

**Step 3: Check underlying resources**
```bash
# Check PVC status
kubectl get pvc -n honua-process-framework | grep redis

# Check node resources
kubectl describe node $(kubectl get pod -n honua-process-framework redis-0 -o jsonpath='{.spec.nodeName}')
```

### Procedure

#### Scenario A: Redis OOMKilled (Out of Memory)

**Root Cause**: Redis memory usage exceeded limits

```bash
# 1. Check memory usage from previous container
kubectl logs -n honua-process-framework redis-0 --previous | grep "used_memory"

# 2. Increase memory limits
kubectl edit statefulset -n honua-process-framework redis

# Update:
# resources:
#   limits:
#     memory: "4Gi"  # Increase from 2Gi to 4Gi
#   requests:
#     memory: "2Gi"

# 3. Delete pod to restart with new limits
kubectl delete pod -n honua-process-framework redis-0

# 4. Wait for Redis to be ready
kubectl wait --for=condition=ready pod/redis-0 -n honua-process-framework --timeout=120s

# 5. Verify memory configuration
kubectl exec -n honua-process-framework redis-0 -- redis-cli CONFIG GET maxmemory
```

#### Scenario B: Redis Data Corruption

**Root Cause**: Corrupted RDB or AOF file

```bash
# 1. Check for corruption errors
kubectl logs -n honua-process-framework redis-0 | grep -i "corrupt\|error\|fatal"

# 2. Backup current data (even if corrupted)
kubectl cp honua-process-framework/redis-0:/data/dump.rdb ./corrupted-dump.rdb

# 3. If AOF is corrupted, try to fix it
kubectl exec -n honua-process-framework redis-0 -- redis-check-aof --fix /data/appendonly.aof

# 4. If fix doesn't work, restore from backup
# Get latest backup
aws s3 cp s3://honua-backups/redis/dump-latest.rdb ./dump.rdb

# Stop Redis
kubectl scale statefulset/redis -n honua-process-framework --replicas=0

# Replace data file
kubectl cp ./dump.rdb honua-process-framework/redis-0:/data/dump.rdb

# Start Redis
kubectl scale statefulset/redis -n honua-process-framework --replicas=1

# 5. Wait for ready
kubectl wait --for=condition=ready pod/redis-0 -n honua-process-framework --timeout=120s

# 6. Verify data
kubectl exec -n honua-process-framework redis-0 -- redis-cli DBSIZE
```

#### Scenario C: PVC Full (Disk Space)

**Root Cause**: Redis persistent volume full

```bash
# 1. Check PVC usage
kubectl exec -n honua-process-framework redis-0 -- df -h /data

# 2. If >90% full, expand PVC
kubectl edit pvc -n honua-process-framework redis-data-redis-0

# Update:
# spec:
#   resources:
#     requests:
#       storage: 20Gi  # Increase from 10Gi to 20Gi

# 3. Delete and recreate pod (for some storage classes)
kubectl delete pod -n honua-process-framework redis-0

# 4. Verify new size
kubectl exec -n honua-process-framework redis-0 -- df -h /data
```

#### Scenario D: Complete Redis Failure (Failover to New Instance)

**Root Cause**: Redis cannot be recovered, need new instance

```bash
# 1. Provision new Redis instance (Azure Cache for Redis example)
az redis create \
  --name honua-redis-backup \
  --resource-group honua \
  --location eastus \
  --sku Standard \
  --vm-size c1

# 2. Get new connection string
REDIS_KEY=$(az redis list-keys --name honua-redis-backup --resource-group honua --query primaryKey -o tsv)
REDIS_HOST=$(az redis show --name honua-redis-backup --resource-group honua --query hostName -o tsv)
NEW_CONN_STRING="$REDIS_HOST:6380,password=$REDIS_KEY,ssl=true,abortConnect=false"

# 3. Update connection string secret
kubectl create secret generic redis-secret \
  --from-literal=connection-string="$NEW_CONN_STRING" \
  --dry-run=client -o yaml | kubectl apply -f - -n honua-process-framework

# 4. Restart application to use new Redis
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# 5. Restore data from backup (if available)
# This depends on Redis provider's restore mechanism
# For Azure Cache for Redis:
az redis import \
  --name honua-redis-backup \
  --resource-group honua \
  --files "https://honuabackups.blob.core.windows.net/redis/dump.rdb"

# 6. Monitor application recovery
kubectl get pods -n honua-process-framework
kubectl logs -f -l app=honua-process-framework -n honua-process-framework
```

### Validation

**Success Criteria**:
- ✅ Redis pod in Running/Ready state
- ✅ `redis-cli ping` returns PONG
- ✅ Application can connect to Redis
- ✅ Processes can be created and executed
- ✅ No Redis alerts firing

**Validation Commands**:
```bash
# 1. Check Redis health
kubectl exec -n honua-process-framework redis-0 -- redis-cli ping
# Expected: PONG

# 2. Check data
kubectl exec -n honua-process-framework redis-0 -- redis-cli DBSIZE
# Expected: > 0 (if restored from backup)

# 3. Test application connection
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "Redis.*connected"

# 4. Create test process
curl -X POST "http://honua-process-framework:9090/api/processes" -d '{"processType":"Test"}'

# 5. Verify test process appears in Redis
kubectl exec -n honua-process-framework redis-0 -- redis-cli KEYS "honua:process:*" | head -1
```

### Prevention

1. **High Availability**: Use Redis Sentinel or Cluster
2. **Monitoring**: Alert on memory usage > 80%
3. **Backups**: Automate daily backups with verification
4. **Capacity Planning**: Monitor data growth trends
5. **Health Checks**: Implement readiness/liveness probes
6. **Disaster Recovery**: Test restore procedure monthly

### Related Runbooks
- [Runbook 1: High Process Failure Rate](#runbook-1-high-process-failure-rate-investigation)
- [Runbook 6: Data Recovery from Redis](#runbook-6-data-recovery-from-redis)

---

## Runbook 4: LLM Provider Failover

**Severity**: P2 (High)
**Estimated Time**: 15-30 minutes
**Roles Required**: On-call Engineer, Platform Engineer

### Symptoms

- Alert: `LlmApiRateLimit` or `LlmApiFailure` firing
- Processes failing with "429 Too Many Requests" or "503 Service Unavailable"
- LLM API dashboard shows 100% error rate
- Azure OpenAI or OpenAI status page reports outage

### Prerequisites

- kubectl access to honua-process-framework namespace
- Backup LLM provider configured (OpenAI if Azure is primary, or vice versa)
- API keys for fallback provider

### Diagnosis

**Step 1: Check LLM error rate**
```bash
# Check error rate by provider
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=sum by (provider) (rate(honua_llm_errors_total[5m]))' | jq

# Example output:
# {"provider":"Azure","value":"5.2"}  # 5.2 errors/sec
# {"provider":"OpenAI","value":"0.1"}  # 0.1 errors/sec
```

**Step 2: Check provider status**
```bash
# Azure OpenAI
curl https://status.azure.com/status

# OpenAI
curl https://status.openai.com/api/v2/status.json | jq '.status.description'

# If either shows "Operational", that provider is available
```

**Step 3: Identify error type**
```bash
# Get recent LLM errors
kubectl logs -n honua-process-framework -l app=honua-process-framework --since=10m | \
  grep -i "llm\|openai\|azure" | \
  grep -i "error\|exception\|failed" | \
  tail -20

# Common errors:
# - "429 Too Many Requests" → Rate limit hit
# - "503 Service Unavailable" → Provider outage
# - "401 Unauthorized" → API key issue
# - "Timeout" → Network or latency issue
```

### Procedure

#### Scenario A: Azure OpenAI Rate Limit (Switch to OpenAI)

**Root Cause**: Exceeding Azure OpenAI TPM/RPM limits

```bash
# 1. Check current provider configuration
kubectl get configmap -n honua-process-framework process-config -o yaml | grep -A5 "LlmProvider"

# 2. Update to use OpenAI as primary (Azure as fallback)
kubectl edit configmap -n honua-process-framework process-config

# Update:
# LlmProvider:
#   Provider: OpenAI           # Changed from Azure
#   FallbackProvider: Azure    # Changed from OpenAI
#   OpenAI:
#     ApiKey: ${OPENAI_API_KEY}
#     DefaultModel: gpt-4o

# 3. Restart pods to apply
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# 4. Monitor error rate improvement
watch 'curl -s -G http://prometheus:9090/api/v1/query --data-urlencode "query=sum(rate(honua_llm_errors_total[1m]))" | jq -r ".data.result[0].value[1]"'

# 5. Verify processes succeed with new provider
curl "http://honua-process-framework:9090/api/processes?status=Completed&since=5m" | jq 'length'
```

#### Scenario B: Both Providers Limited (Reduce Load)

**Root Cause**: Hitting rate limits on all providers

```bash
# 1. Immediately reduce concurrent processes
kubectl edit configmap -n honua-process-framework process-config

# Update:
# ProcessFramework:
#   MaxConcurrentProcesses: 5  # Reduce from 20

# 2. Add rate limiting per provider
# LlmProvider:
#   RateLimiting:
#     Azure:
#       RequestsPerMinute: 60
#       TokensPerMinute: 90000
#     OpenAI:
#       RequestsPerMinute: 60
#       TokensPerMinute: 90000

# 3. Restart pods
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# 4. Queue pending processes (they'll resume when capacity available)
curl "http://honua-process-framework:9090/api/processes?status=Pending" | jq 'length'

# 5. Request quota increase (long-term fix)
# Azure: Go to Azure Portal → Cognitive Services → Quotas → Request increase
# OpenAI: Go to platform.openai.com → Settings → Limits → Request increase
```

#### Scenario C: Primary Provider Down (Complete Failover)

**Root Cause**: Azure OpenAI service outage

```bash
# 1. Disable failed provider entirely
kubectl edit configmap -n honua-process-framework process-config

# Update:
# LlmProvider:
#   Provider: OpenAI
#   FallbackProvider: null  # No fallback since primary is down
#   Azure:
#     Enabled: false        # Disable Azure temporarily

# 2. Restart pods
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# 3. Monitor for Azure recovery
while true; do
  STATUS=$(curl -s https://status.azure.com/api/v2/status.json | jq -r '.status.description')
  echo "$(date): Azure status: $STATUS"
  if [ "$STATUS" = "All Systems Operational" ]; then
    echo "Azure recovered!"
    break
  fi
  sleep 300  # Check every 5 minutes
done

# 4. Re-enable Azure when recovered
kubectl edit configmap -n honua-process-framework process-config
# Azure:
#   Enabled: true

kubectl rollout restart deployment/honua-process-framework -n honua-process-framework
```

#### Scenario D: API Key Invalid or Expired

**Root Cause**: API key rotated or revoked

```bash
# 1. Verify API key works
# For Azure OpenAI:
curl https://YOUR-RESOURCE.openai.azure.com/openai/deployments/gpt-4o/chat/completions?api-version=2024-02-15-preview \
  -H "Content-Type: application/json" \
  -H "api-key: YOUR-KEY" \
  -d '{"messages":[{"role":"user","content":"test"}]}'

# For OpenAI:
curl https://api.openai.com/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR-KEY" \
  -d '{"model":"gpt-4o","messages":[{"role":"user","content":"test"}]}'

# 2. If key is invalid, update secret
kubectl create secret generic azure-openai-secret \
  --from-literal=api-key="NEW-KEY-HERE" \
  --dry-run=client -o yaml | kubectl apply -f - -n honua-process-framework

# 3. Restart pods to load new key
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# 4. Verify new key works
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "LLM.*authenticated"
```

### Validation

**Success Criteria**:
- ✅ LLM error rate < 1%
- ✅ Processes completing successfully with new provider
- ✅ No LLM alerts firing
- ✅ Response times within normal range

**Validation Commands**:
```bash
# 1. Check error rate
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=sum(rate(honua_llm_errors_total[5m]))' | jq -r '.data.result[0].value[1]'
# Expected: < 0.1

# 2. Check which provider is being used
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "LLM provider" | tail -5

# 3. Check recent successful processes
curl "http://honua-process-framework:9090/api/processes?status=Completed&since=10m" | jq 'length'
# Expected: > 0

# 4. Test LLM API directly
curl -X POST "http://honua-process-framework:9090/api/test/llm" -d '{"prompt":"Hello"}'
# Expected: 200 OK with response
```

### Prevention

1. **Multi-Provider Setup**: Always configure fallback provider
2. **Quota Monitoring**: Alert at 80% of quota usage
3. **Rate Limiting**: Implement application-level rate limiting
4. **Capacity Planning**: Request quota increases proactively
5. **Provider Diversity**: Use different providers for different workloads
6. **Status Monitoring**: Subscribe to provider status pages

### Related Runbooks
- [Runbook 1: High Process Failure Rate](#runbook-1-high-process-failure-rate-investigation)

---

## Runbook 5: Emergency Process Cancellation

**Severity**: P3 (Medium)
**Estimated Time**: 5-10 minutes
**Roles Required**: On-call Engineer

### Symptoms

- User reports process running amok (e.g., deleting wrong resources)
- Process stuck in infinite loop
- Process consuming excessive resources
- Need to stop process immediately to prevent damage

### Prerequisites

- kubectl access to honua-process-framework namespace
- Process ID to cancel
- Authority to cancel processes (critical action)

### Diagnosis

**Step 1: Identify the problematic process**
```bash
# If you have process ID
PROCESS_ID="abc-123-def"
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | jq

# If you need to find it by type or user
curl "http://honua-process-framework:9090/api/processes?status=Running" | \
  jq -r '.[] | "\(.processId) \(.processType) \(.startTime) \(.user)"'
```

**Step 2: Assess impact of cancellation**
```bash
# Check what the process has done so far
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | \
  jq '{currentStep, completionPercentage, createdResources}'

# Example output:
# {
#   "currentStep": "DeployInfrastructure",
#   "completionPercentage": 45,
#   "createdResources": ["vpc-123", "subnet-456"]
# }

# Note: May need to manually clean up created resources
```

### Procedure

#### Option 1: Graceful Cancellation (Preferred)

**When to use**: Normal cancellation, allows cleanup

```bash
PROCESS_ID="abc-123-def"

# 1. Request graceful cancellation
curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/cancel"

# 2. Monitor cancellation progress
watch "curl -s http://honua-process-framework:9090/api/processes/${PROCESS_ID} | jq '.status, .currentStep'"

# 3. Process should transition:
#    Running → Cancelling → Cancelled (within 60 seconds)

# 4. If stuck in "Cancelling" for > 2 minutes, proceed to Force Cancel
```

#### Option 2: Force Cancel (If Graceful Fails)

**When to use**: Process not responding to graceful cancel

```bash
PROCESS_ID="abc-123-def"

# 1. Force cancel (skips cleanup)
curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/cancel?force=true"

# 2. Delete process state from Redis
kubectl exec -n honua-process-framework redis-0 -- redis-cli DEL "honua:process:${PROCESS_ID}"

# 3. Verify process is gone
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}"
# Expected: 404 Not Found
```

#### Option 3: Emergency - Cancel All Processes

**When to use**: System-wide issue, need to stop everything

```bash
# ⚠️ WARNING: This cancels ALL running processes ⚠️

# 1. Get all running process IDs
curl "http://honua-process-framework:9090/api/processes?status=Running" | \
  jq -r '.[].processId' > running-processes.txt

# 2. Confirm with team/manager before proceeding
read -p "Cancel all $(wc -l < running-processes.txt) processes? (yes/no): " CONFIRM

if [ "$CONFIRM" = "yes" ]; then
  # 3. Cancel each process
  while read PROCESS_ID; do
    echo "Cancelling $PROCESS_ID..."
    curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/cancel"
  done < running-processes.txt
fi

# 4. OR scale down deployment to 0 (nuclear option)
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=0
```

#### Post-Cancellation Cleanup

**Clean up resources created by cancelled process**:

```bash
PROCESS_ID="abc-123-def"

# 1. Get list of created resources
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | \
  jq -r '.createdResources[]' > resources-to-cleanup.txt

# 2. Manual cleanup (example for AWS resources)
while read RESOURCE; do
  case $RESOURCE in
    vpc-*)
      aws ec2 delete-vpc --vpc-id $RESOURCE
      ;;
    subnet-*)
      aws ec2 delete-subnet --subnet-id $RESOURCE
      ;;
    *)
      echo "Unknown resource type: $RESOURCE"
      ;;
  esac
done < resources-to-cleanup.txt

# 3. Mark process as cleaned up
curl -X POST "http://honua-process-framework:9090/api/processes/${PROCESS_ID}/mark-cleaned-up"
```

### Validation

**Success Criteria**:
- ✅ Process status changed to "Cancelled"
- ✅ Process no longer appears in active processes list
- ✅ Resources stopped creating/modifying
- ✅ System resources (CPU/memory) return to normal

**Validation Commands**:
```bash
# 1. Check process status
curl "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" | jq '.status'
# Expected: "Cancelled"

# 2. Check active processes count
curl "http://honua-process-framework:9090/api/processes?status=Running" | jq 'length'
# Expected: One less than before

# 3. Check system resources
kubectl top pods -n honua-process-framework

# 4. Check logs for cleanup completion
kubectl logs -n honua-process-framework -l app=honua-process-framework | grep "$PROCESS_ID.*cleanup"
```

### Prevention

1. **Pre-Flight Checks**: Validate process input before starting
2. **Dry-Run Mode**: Test processes in dry-run before actual execution
3. **Approval Gates**: Require approval for destructive operations
4. **Rate Limiting**: Limit process start rate
5. **Monitoring**: Alert on abnormal process behavior (resource spikes, etc.)

### Related Runbooks
- [Runbook 2: Process Timeout Recovery](#runbook-2-process-timeout-recovery)
- [Runbook 1: High Process Failure Rate](#runbook-1-high-process-failure-rate-investigation)

---

## Runbook 6: Data Recovery from Redis

**Severity**: P2 (High)
**Estimated Time**: 30-60 minutes
**Roles Required**: Senior DevOps Engineer, DBA

### Symptoms

- Redis data lost due to pod deletion, PVC deletion, or corruption
- Processes cannot resume (no state found)
- DBSIZE shows 0 or much lower than expected
- Need to restore process state from backup

### Prerequisites

- kubectl access with admin privileges
- Access to Redis backups (S3/Azure Blob/GCS)
- Understanding of RDB/AOF format
- Downtime window (recommended)

### Diagnosis

**Step 1: Assess data loss**
```bash
# 1. Check current Redis data
kubectl exec -n honua-process-framework redis-0 -- redis-cli DBSIZE
# Expected: Should be > 0 if processes are running

# 2. Check for process state keys
kubectl exec -n honua-process-framework redis-0 -- redis-cli KEYS "honua:process:*" | wc -l

# 3. Compare to expected count from monitoring
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=honua_active_processes' | jq
```

**Step 2: Identify available backups**
```bash
# List available backups (S3 example)
aws s3 ls s3://honua-backups/redis/ --recursive | tail -10

# Example output:
# 2025-10-17 02:00:00  1048576 dump-20251017-020000.rdb
# 2025-10-16 02:00:00  1023456 dump-20251016-020000.rdb

# Choose most recent backup before data loss
```

**Step 3: Determine recovery point**
```bash
# Check backup metadata
aws s3api head-object --bucket honua-backups --key redis/dump-20251017-020000.rdb | \
  jq '{LastModified, ContentLength}'

# Estimate data loss window
# Data loss window = Current time - Backup time
# Example: 10:00 AM - 2:00 AM = 8 hours of data loss
```

### Procedure

#### Option 1: Restore from RDB Backup (Full Restore)

**When to use**: Complete data loss, need full restore

```bash
# ⚠️ WARNING: This will overwrite all current data ⚠️

# 1. Notify stakeholders about downtime
# Expected downtime: 15-30 minutes

# 2. Scale down application (stop new writes)
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=0

# 3. Wait for all connections to close
kubectl exec -n honua-process-framework redis-0 -- redis-cli CLIENT LIST | wc -l
# Wait until output is 1 (only our connection)

# 4. Download backup
aws s3 cp s3://honua-backups/redis/dump-20251017-020000.rdb ./dump.rdb

# 5. Verify backup integrity
redis-check-rdb dump.rdb
# Expected: "RDB looks OK"

# 6. Stop Redis (optional, safer)
kubectl scale statefulset/redis -n honua-process-framework --replicas=0

# 7. Copy backup to Redis volume
kubectl cp ./dump.rdb honua-process-framework/redis-0:/data/dump.rdb

# 8. Start Redis
kubectl scale statefulset/redis -n honua-process-framework --replicas=1

# 9. Wait for Redis to load data
kubectl wait --for=condition=ready pod/redis-0 -n honua-process-framework --timeout=120s

# 10. Verify data restored
kubectl exec -n honua-process-framework redis-0 -- redis-cli DBSIZE

# 11. Check sample keys
kubectl exec -n honua-process-framework redis-0 -- redis-cli KEYS "honua:process:*" | head -5

# 12. Resume application
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=3

# 13. Monitor for issues
kubectl logs -f -l app=honua-process-framework -n honua-process-framework
```

#### Option 2: Selective Key Restore (Partial Recovery)

**When to use**: Only some keys lost, full restore not needed

```bash
# 1. Load backup into temporary Redis instance
docker run -d --name redis-temp -p 6380:6379 redis:7.2-alpine

# 2. Copy backup to temp Redis
docker cp dump.rdb redis-temp:/data/dump.rdb

# 3. Restart temp Redis to load backup
docker restart redis-temp
sleep 5

# 4. Export specific keys from temp Redis
docker exec redis-temp redis-cli KEYS "honua:process:*" > keys-to-restore.txt

# 5. For each key, copy to production
while read KEY; do
  echo "Restoring $KEY..."

  # Get value from temp Redis
  VALUE=$(docker exec redis-temp redis-cli --raw DUMP "$KEY" | base64)

  # Get TTL
  TTL=$(docker exec redis-temp redis-cli TTL "$KEY")

  # Restore to production Redis
  kubectl exec -n honua-process-framework redis-0 -- redis-cli RESTORE "$KEY" "$TTL" "$VALUE" REPLACE

done < keys-to-restore.txt

# 6. Verify restored keys
kubectl exec -n honua-process-framework redis-0 -- redis-cli KEYS "honua:process:*" | wc -l

# 7. Clean up temp Redis
docker rm -f redis-temp
```

#### Option 3: Replay from Application Logs (Last Resort)

**When to use**: No backup available, need to reconstruct state from logs

```bash
# 1. Get process creation logs from last N hours
kubectl logs -n honua-process-framework -l app=honua-process-framework --since=24h | \
  grep "Process.*started" > process-starts.log

# 2. Parse process IDs and types
grep -oP 'Process \K[a-f0-9-]+' process-starts.log > process-ids.txt

# 3. For each process, check if still active
while read PROCESS_ID; do
  # Try to get process from API (it might have completed)
  curl -s "http://honua-process-framework:9090/api/processes/${PROCESS_ID}" > /dev/null
  RESULT=$?

  if [ $RESULT -ne 0 ]; then
    echo "Process $PROCESS_ID no longer exists (likely completed or failed)"
  fi
done < process-ids.txt

# 4. Reconstruct state for active processes (if possible)
# This is highly application-specific and may not be feasible
```

### Validation

**Success Criteria**:
- ✅ Redis DBSIZE matches expected value
- ✅ Process state keys present
- ✅ Application can read and write to Redis
- ✅ Processes can resume from restored state
- ✅ No data corruption errors

**Validation Commands**:
```bash
# 1. Check Redis data count
kubectl exec -n honua-process-framework redis-0 -- redis-cli DBSIZE
# Compare to pre-loss count (from monitoring)

# 2. Verify key structure
kubectl exec -n honua-process-framework redis-0 -- redis-cli GET "honua:process:$(kubectl exec -n honua-process-framework redis-0 -- redis-cli KEYS 'honua:process:*' | head -1)" | jq .
# Should parse as valid JSON

# 3. Check TTLs are set
kubectl exec -n honua-process-framework redis-0 -- redis-cli KEYS "honua:process:*" | \
  head -5 | \
  xargs -I {} kubectl exec -n honua-process-framework redis-0 -- redis-cli TTL {}
# Should return positive numbers (not -1)

# 4. Test application functionality
curl -X POST "http://honua-process-framework:9090/api/processes" -d '{"processType":"Test"}'
# Should succeed and return process ID

# 5. Check logs for errors
kubectl logs -n honua-process-framework -l app=honua-process-framework --since=5m | grep -i error
# Should be minimal or none
```

### Prevention

1. **Automated Backups**: Schedule daily backups with verification
2. **AOF Persistence**: Enable AOF in addition to RDB snapshots
3. **Replication**: Use Redis Sentinel or Cluster for redundancy
4. **Monitoring**: Alert on DBSIZE drops or missing keys
5. **Backup Testing**: Test restore procedure monthly
6. **Immutable Backups**: Use S3 Object Lock or similar to prevent deletion

### Related Runbooks
- [Runbook 3: Redis Failover Procedure](#runbook-3-redis-failover-procedure)

---

## Runbook 7: Pod Restart and Recovery

**Severity**: P3 (Medium)
**Estimated Time**: 10-15 minutes
**Roles Required**: On-call Engineer

### Symptoms

- Pod in CrashLoopBackOff state
- Application not starting after deployment
- Logs show startup errors
- Pod evicted due to resource pressure

### Prerequisites

- kubectl access to honua-process-framework namespace
- Understanding of pod lifecycle
- Access to container logs

### Diagnosis

**Step 1: Check pod status**
```bash
# Get pod status
kubectl get pods -n honua-process-framework

# Possible problematic states:
# - CrashLoopBackOff: Container crashing after start
# - Error: Container failed to start
# - Evicted: Pod removed due to resource pressure
# - Pending: Pod can't be scheduled

# Get detailed pod description
POD_NAME=$(kubectl get pods -n honua-process-framework -l app=honua-process-framework -o jsonpath='{.items[0].metadata.name}')
kubectl describe pod -n honua-process-framework $POD_NAME
```

**Step 2: Check logs**
```bash
# Current container logs
kubectl logs -n honua-process-framework $POD_NAME

# Previous container logs (if crashed)
kubectl logs -n honua-process-framework $POD_NAME --previous

# Look for:
# - Stack traces
# - Configuration errors
# - Connection failures
# - Resource exhaustion
```

**Step 3: Check events**
```bash
# Get recent events
kubectl get events -n honua-process-framework --sort-by='.lastTimestamp' | tail -20

# Common events:
# - "Back-off restarting failed container"
# - "Liveness probe failed"
# - "Pod evicted"
# - "Failed to pull image"
```

### Procedure

#### Scenario A: Application Crash on Startup

**Root Cause**: Configuration error or missing dependency

```bash
# 1. Check logs for specific error
kubectl logs -n honua-process-framework $POD_NAME --previous | tail -50

# Common errors:
# - "Unable to connect to Redis" → Check Redis
# - "Missing configuration" → Check ConfigMap
# - "Invalid API key" → Check Secret

# 2. Verify configuration
kubectl get configmap -n honua-process-framework process-config -o yaml

# 3. Verify secrets
kubectl get secret -n honua-process-framework azure-openai-secret -o jsonpath='{.data}'

# 4. Fix configuration if wrong
kubectl edit configmap -n honua-process-framework process-config

# 5. Delete pod to restart with new config
kubectl delete pod -n honua-process-framework $POD_NAME

# 6. Monitor startup
kubectl logs -f -n honua-process-framework $POD_NAME
```

#### Scenario B: Liveness Probe Failure

**Root Cause**: Application not responding to health checks

```bash
# 1. Check liveness probe configuration
kubectl get deployment -n honua-process-framework honua-process-framework -o yaml | grep -A10 livenessProbe

# 2. Test health endpoint manually
kubectl exec -n honua-process-framework $POD_NAME -- curl -s http://localhost:9090/health | jq

# 3. If endpoint returns unhealthy, check why
kubectl logs -n honua-process-framework $POD_NAME | grep -i "health\|liveness"

# 4. If probe is too aggressive, adjust timing
kubectl edit deployment -n honua-process-framework honua-process-framework

# Increase:
# livenessProbe:
#   initialDelaySeconds: 60  # Increase from 30
#   periodSeconds: 20         # Increase from 10
#   timeoutSeconds: 10        # Increase from 5
#   failureThreshold: 5       # Increase from 3
```

#### Scenario C: Resource Exhaustion (OOMKilled)

**Root Cause**: Container exceeded memory limit

```bash
# 1. Check for OOMKilled in events
kubectl get events -n honua-process-framework | grep OOMKilled

# 2. Check current resource limits
kubectl get deployment -n honua-process-framework honua-process-framework -o yaml | grep -A4 resources

# 3. Increase memory limits
kubectl set resources deployment/honua-process-framework -n honua-process-framework \
  --limits=memory=4Gi --requests=memory=2Gi

# 4. Monitor memory usage after restart
watch kubectl top pods -n honua-process-framework
```

#### Scenario D: Image Pull Error

**Root Cause**: Can't pull container image

```bash
# 1. Check image pull error
kubectl describe pod -n honua-process-framework $POD_NAME | grep -A5 "Failed to pull image"

# Common causes:
# - Image doesn't exist
# - Registry authentication failed
# - Network issue

# 2. Verify image exists
IMAGE=$(kubectl get deployment -n honua-process-framework honua-process-framework -o jsonpath='{.spec.template.spec.containers[0].image}')
docker pull $IMAGE

# 3. If authentication issue, check image pull secret
kubectl get secret -n honua-process-framework docker-registry-secret

# 4. If secret missing or wrong, create it
kubectl create secret docker-registry docker-registry-secret \
  --docker-server=registry.example.com \
  --docker-username=user \
  --docker-password=pass \
  -n honua-process-framework

# 5. Update deployment to use secret
kubectl patch deployment/honua-process-framework -n honua-process-framework -p '{"spec":{"template":{"spec":{"imagePullSecrets":[{"name":"docker-registry-secret"}]}}}}'
```

### Validation

**Success Criteria**:
- ✅ Pod in Running state with 1/1 Ready
- ✅ No restart count increase
- ✅ Health endpoint returns healthy
- ✅ Application logs show normal operation

**Validation Commands**:
```bash
# 1. Check pod status
kubectl get pods -n honua-process-framework -l app=honua-process-framework
# All should be Running with 1/1 Ready

# 2. Check restart count
kubectl get pods -n honua-process-framework -l app=honua-process-framework -o jsonpath='{.items[*].status.containerStatuses[*].restartCount}'
# Should be low (0-2)

# 3. Test health endpoint
kubectl exec -n honua-process-framework $POD_NAME -- curl http://localhost:9090/health
# Should return 200 OK

# 4. Check recent logs for errors
kubectl logs -n honua-process-framework $POD_NAME --since=5m | grep -i error
# Should be minimal

# 5. Test process creation
curl -X POST "http://honua-process-framework:9090/api/processes" -d '{"processType":"Test"}'
# Should succeed
```

### Prevention

1. **Resource Limits**: Set appropriate CPU/memory limits
2. **Health Checks**: Configure realistic liveness/readiness probes
3. **Graceful Shutdown**: Implement SIGTERM handling
4. **Pre-Stop Hooks**: Add pre-stop hook for cleanup
5. **Pod Disruption Budgets**: Prevent simultaneous pod terminations

### Related Runbooks
- [Runbook 1: High Process Failure Rate](#runbook-1-high-process-failure-rate-investigation)
- [Runbook 3: Redis Failover Procedure](#runbook-3-redis-failover-procedure)

---

## Runbook 8: Database Connection Failure

**Severity**: P2 (High)
**Estimated Time**: 20-30 minutes
**Roles Required**: On-call Engineer, DBA

### Symptoms

- Processes failing with "Database connection failed"
- Logs show "Cannot connect to PostgreSQL"
- Application health check shows database unhealthy
- Metadata operations failing

### Prerequisites

- kubectl access to honua-process-framework namespace
- PostgreSQL connection details
- psql CLI access
- Database admin credentials

### Diagnosis

**Step 1: Check database connectivity**
```bash
# Get database connection string (from secret)
DB_HOST=$(kubectl get secret -n honua-process-framework postgres-secret -o jsonpath='{.data.host}' | base64 -d)
DB_USER=$(kubectl get secret -n honua-process-framework postgres-secret -o jsonpath='{.data.username}' | base64 -d)
DB_NAME=$(kubectl get secret -n honua-process-framework postgres-secret -o jsonpath='{.data.database}' | base64 -d)

# Test connection from your machine
psql "host=$DB_HOST user=$DB_USER dbname=$DB_NAME sslmode=require" -c "SELECT version();"

# If this fails, check:
# - Firewall rules
# - Database server status
# - Network connectivity
```

**Step 2: Check database server health**
```bash
# For AWS RDS
aws rds describe-db-instances --db-instance-identifier honua-postgres | jq '.DBInstances[0].DBInstanceStatus'

# For Azure Database
az postgres server show --name honua-postgres --resource-group honua | jq '.userVisibleState'

# Expected: "available" or "online"
```

**Step 3: Check connection pool**
```bash
# Check active connections
psql -h $DB_HOST -U $DB_USER -d $DB_NAME -c "SELECT count(*) FROM pg_stat_activity WHERE datname='$DB_NAME';"

# Check max connections
psql -h $DB_HOST -U $DB_USER -d $DB_NAME -c "SHOW max_connections;"

# If active ≈ max, connection pool exhausted
```

### Procedure

#### Scenario A: Database Server Down

**Root Cause**: PostgreSQL server unavailable

```bash
# 1. Check database server status (AWS RDS example)
aws rds describe-db-instances --db-instance-identifier honua-postgres

# 2. If stopped, start it
aws rds start-db-instance --db-instance-identifier honua-postgres

# 3. Wait for available status
aws rds wait db-instance-available --db-instance-identifier honua-postgres

# 4. Verify connection
psql "host=$DB_HOST user=$DB_USER dbname=$DB_NAME" -c "SELECT 1;"

# 5. Resume application (if scaled down)
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=3
```

#### Scenario B: Connection Pool Exhausted

**Root Cause**: Too many connections from application

```bash
# 1. Kill idle connections
psql -h $DB_HOST -U $DB_USER -d $DB_NAME -c "
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname='$DB_NAME'
  AND state='idle'
  AND state_change < NOW() - INTERVAL '10 minutes';
"

# 2. Reduce application connection pool size
kubectl edit configmap -n honua-process-framework process-config

# Update:
# Database:
#   MaxPoolSize: 20  # Reduce from 100
#   MinPoolSize: 5

# 3. Restart pods
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework

# 4. Monitor connection count
watch "psql -h $DB_HOST -U $DB_USER -d $DB_NAME -c \"SELECT count(*) FROM pg_stat_activity WHERE datname='$DB_NAME';\""
```

#### Scenario C: Network Connectivity Issue

**Root Cause**: Firewall or network partition

```bash
# 1. Test network connectivity from pod
kubectl exec -n honua-process-framework $POD_NAME -- nc -zv $DB_HOST 5432

# 2. If fails, check security groups (AWS)
aws ec2 describe-security-groups --group-ids sg-12345678 | jq '.SecurityGroups[0].IpPermissions'

# 3. Add ingress rule if missing
aws ec2 authorize-security-group-ingress \
  --group-id sg-12345678 \
  --protocol tcp \
  --port 5432 \
  --cidr 10.0.0.0/16  # Your pod CIDR

# 4. Verify connectivity restored
kubectl exec -n honua-process-framework $POD_NAME -- nc -zv $DB_HOST 5432
```

#### Scenario D: Invalid Credentials

**Root Cause**: Database password changed or wrong

```bash
# 1. Test credentials
psql "host=$DB_HOST user=$DB_USER dbname=$DB_NAME password=$DB_PASS" -c "SELECT 1;"

# 2. If fails, reset password (AWS RDS example)
aws rds modify-db-instance \
  --db-instance-identifier honua-postgres \
  --master-user-password "NewSecurePassword123"

# 3. Update secret
kubectl create secret generic postgres-secret \
  --from-literal=host=$DB_HOST \
  --from-literal=username=$DB_USER \
  --from-literal=password="NewSecurePassword123" \
  --from-literal=database=$DB_NAME \
  --dry-run=client -o yaml | kubectl apply -f - -n honua-process-framework

# 4. Restart pods to load new secret
kubectl rollout restart deployment/honua-process-framework -n honua-process-framework
```

### Validation

**Success Criteria**:
- ✅ Application can connect to database
- ✅ Database health check passes
- ✅ Processes can read/write metadata
- ✅ Connection pool within limits

**Validation Commands**:
```bash
# 1. Test connection from pod
kubectl exec -n honua-process-framework $POD_NAME -- psql "host=$DB_HOST user=$DB_USER dbname=$DB_NAME" -c "SELECT 1;"

# 2. Check health endpoint
curl http://honua-process-framework:9090/health | jq '.checks.database'
# Expected: "Healthy"

# 3. Test metadata operation
curl "http://honua-process-framework:9090/api/metadata?collection=test"

# 4. Check connection count
psql -h $DB_HOST -U $DB_USER -d $DB_NAME -c "SELECT count(*) FROM pg_stat_activity WHERE datname='$DB_NAME';"
# Should be reasonable (< max_connections - 10)
```

### Prevention

1. **Connection Pooling**: Configure appropriate pool sizes
2. **Connection Timeouts**: Set idle connection timeout
3. **Monitoring**: Alert on connection count > 80% of max
4. **High Availability**: Use database replicas
5. **Credential Rotation**: Automate password rotation
6. **Network Policies**: Document and version firewall rules

### Related Runbooks
- [Runbook 1: High Process Failure Rate](#runbook-1-high-process-failure-rate-investigation)
- [Runbook 7: Pod Restart and Recovery](#runbook-7-pod-restart-and-recovery)

---

## Summary

This runbook collection covers the most common operational scenarios for the Honua Process Framework. Each runbook is designed to be followed step-by-step during incidents, with clear success criteria and prevention measures.

**Runbook Summary**:

| # | Title | Severity | Time | Use Case |
|---|-------|----------|------|----------|
| 1 | High Process Failure Rate | P2 | 30-45m | > 10% processes failing |
| 2 | Process Timeout Recovery | P3 | 15-30m | Processes stuck/timed out |
| 3 | Redis Failover Procedure | P1 | 10-20m | Redis unavailable |
| 4 | LLM Provider Failover | P2 | 15-30m | LLM API issues |
| 5 | Emergency Process Cancellation | P3 | 5-10m | Stop runaway process |
| 6 | Data Recovery from Redis | P2 | 30-60m | Lost Redis data |
| 7 | Pod Restart and Recovery | P3 | 10-15m | Pod crashing |
| 8 | Database Connection Failure | P2 | 20-30m | Can't connect to DB |

**Next Steps**:
- [Operations Guide](./PROCESS_FRAMEWORK_OPERATIONS.md) - Daily operations and monitoring
- [Deployment Guide](../deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md) - Initial setup
- [Quick Start](../quickstart/PROCESS_FRAMEWORK_QUICKSTART.md) - Getting started

---

**Document Version**: 1.0
**Last Updated**: 2025-10-17
**Maintainer**: Honua Operations Team
