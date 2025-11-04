# Error Recovery Runbook

This runbook provides step-by-step procedures for recovering from common error scenarios in Honua.

## Table of Contents

1. [Circuit Breaker Opened](#circuit-breaker-opened)
2. [Database Read-Only Mode](#database-read-only-mode)
3. [Cache Unavailable](#cache-unavailable)
4. [External Service Failures](#external-service-failures)
5. [High Error Rate](#high-error-rate)
6. [Cascading Failures](#cascading-failures)

---

## Circuit Breaker Opened

### Symptoms
- `honua_circuit_breaker_state=open` metric
- Log entries: "Circuit breaker OPENED for {ServiceName}"
- 503 Service Unavailable responses
- `isTransient: true` in error responses

### Impact
- Service is failing fast to prevent cascading failures
- Users receive 503 errors instead of timeouts
- Fallback mechanisms are being used

### Investigation Steps

1. **Check Service Health**
   ```bash
   # Check if the service is responding
   curl https://your-service/health

   # Check service logs
   kubectl logs -n honua deployment/honua-server --tail=100
   ```

2. **Identify Root Cause**
   ```bash
   # Check for error patterns
   grep "Circuit breaker OPENED" logs/*.log

   # Check metrics for failure rate
   curl http://prometheus:9090/api/v1/query?query=honua_operation_failures_total
   ```

3. **Check Dependencies**
   - Database connectivity
   - Cache (Redis) availability
   - Cloud storage (S3/Azure/GCS) access
   - Network connectivity

### Resolution Steps

#### Option 1: Wait for Auto-Recovery
Circuit breaker will automatically close after the break duration (default 30 seconds) if the service recovers.

1. Monitor logs for "Circuit breaker HALF-OPEN"
2. Watch for "Circuit breaker CLOSED" (service recovered)

#### Option 2: Fix Underlying Issue

1. **If Database Issues:**
   ```bash
   # Restart database connection pool
   kubectl rollout restart deployment/honua-server
   ```

2. **If Cache Issues:**
   ```bash
   # Check Redis health
   redis-cli ping

   # Restart Redis if needed
   kubectl rollout restart deployment/redis
   ```

3. **If Cloud Storage Issues:**
   - Check cloud provider status page
   - Verify credentials and permissions
   - Check network connectivity

#### Option 3: Disable Circuit Breaker (Emergency Only)

```bash
# Update configuration to disable circuit breaker
kubectl set env deployment/honua-server HONUA_RESILIENCE_CIRCUITBREAKER_ENABLED=false

# This should only be used as a last resort!
```

### Prevention
- Set up monitoring for service dependencies
- Configure appropriate timeout values
- Implement health checks for all services
- Use fallback mechanisms

---

## Database Read-Only Mode

### Symptoms
- `honua_database_readonly_mode=true` metric
- Log entries: "Read-only mode enabled until {Timestamp}"
- Write operations return 503 errors
- Read operations continue to work

### Impact
- Users cannot create, update, or delete data
- Read-only operations continue normally
- Data integrity is protected

### Investigation Steps

1. **Check Read-Only Status**
   ```bash
   # Check via API
   curl https://your-service/admin/database/status

   # Check logs
   grep "Read-only mode" logs/*.log
   ```

2. **Identify Cause**
   ```bash
   # Look for permanent write errors
   grep "Permanent database write error" logs/*.log

   # Check database status
   psql -c "SELECT pg_is_in_recovery();"
   ```

### Resolution Steps

#### Option 1: Wait for Auto-Recovery
Read-only mode automatically disables after the configured duration (default 5 minutes).

#### Option 2: Manually Disable Read-Only Mode

```bash
# Via API
curl -X POST https://your-service/admin/database/readonly/disable \
  -H "Authorization: Bearer YOUR_TOKEN"

# Via CLI
honua admin database readonly --disable
```

#### Option 3: Fix Database Issue

1. **If Disk Full:**
   ```bash
   # Check disk space
   df -h

   # Clean up old WAL files
   pg_archivecleanup /var/lib/postgresql/data/pg_wal

   # Increase disk size if needed
   ```

2. **If Permission Issues:**
   ```bash
   # Check database permissions
   psql -c "SELECT grantee, privilege_type FROM information_schema.role_table_grants WHERE table_name='your_table';"

   # Grant necessary permissions
   GRANT INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO honua_user;
   ```

3. **If Database Read-Only:**
   ```bash
   # Check if database is in recovery mode
   psql -c "SELECT pg_is_in_recovery();"

   # Promote standby to primary if needed
   pg_ctl promote -D /var/lib/postgresql/data
   ```

### Prevention
- Monitor disk space
- Set up database replication
- Regularly test failover procedures
- Configure appropriate disk alerts

---

## Cache Unavailable

### Symptoms
- Slow response times
- Cache miss rate near 100%
- Log entries: "Cache unavailable"
- Fallback to direct database queries

### Impact
- Increased database load
- Slower response times
- Higher resource usage
- Service still functional (degraded)

### Investigation Steps

1. **Check Cache Health**
   ```bash
   # Check Redis connectivity
   redis-cli ping

   # Check Redis stats
   redis-cli info stats

   # Check memory usage
   redis-cli info memory
   ```

2. **Check Network Connectivity**
   ```bash
   # Test connection from application server
   telnet redis-host 6379
   ```

### Resolution Steps

#### Option 1: Restart Cache

```bash
# Kubernetes
kubectl rollout restart deployment/redis

# Docker
docker restart redis

# Systemd
systemctl restart redis
```

#### Option 2: Clear Cache (If Corrupted)

```bash
# WARNING: This will delete all cached data
redis-cli FLUSHALL

# Or flush specific database
redis-cli -n 0 FLUSHDB
```

#### Option 3: Scale Cache Resources

```bash
# Increase Redis memory limit
redis-cli CONFIG SET maxmemory 4gb

# Or update Kubernetes deployment
kubectl set resources deployment/redis --limits=memory=4Gi
```

### Prevention
- Monitor cache hit rates
- Set up cache replication
- Configure appropriate eviction policies
- Monitor memory usage

---

## External Service Failures

### Symptoms
- Timeouts accessing cloud storage (S3, Azure Blob, GCS)
- STAC catalog unavailable
- Migration source services failing
- 503/504 errors from external APIs

### Impact
- Features depending on external services unavailable
- Fallback to cached data or defaults
- Slower response times

### Investigation Steps

1. **Identify Failing Service**
   ```bash
   # Check logs for external service errors
   grep "ServiceUnavailable\|ServiceTimeout" logs/*.log

   # Check metrics
   curl http://prometheus:9090/api/v1/query?query=honua_external_service_errors_total
   ```

2. **Check Service Status**
   - AWS Status: https://status.aws.amazon.com/
   - Azure Status: https://status.azure.com/
   - GCP Status: https://status.cloud.google.com/

3. **Verify Credentials**
   ```bash
   # Test AWS credentials
   aws s3 ls s3://your-bucket --profile honua

   # Test Azure credentials
   az storage account list --subscription YOUR_SUB

   # Test GCS credentials
   gcloud storage buckets list
   ```

### Resolution Steps

#### Option 1: Wait for Service Recovery
External service failures are usually temporary. The system will automatically retry.

#### Option 2: Switch to Alternative Provider

```bash
# Update configuration to use backup provider
kubectl set env deployment/honua-server \
  HONUA_RASTER_STORAGE_PRIMARY=gcs \
  HONUA_RASTER_STORAGE_FALLBACK=s3
```

#### Option 3: Use Cached Data

```bash
# Increase cache TTL to serve stale data longer
kubectl set env deployment/honua-server \
  HONUA_CACHE_STALE_DATA_TTL=3600
```

### Prevention
- Use multiple cloud providers
- Implement comprehensive caching
- Set up monitoring for external services
- Configure appropriate timeouts

---

## High Error Rate

### Symptoms
- Error rate >5% (normal is <1%)
- Multiple error types occurring simultaneously
- Alert: "High error rate detected"

### Investigation Steps

1. **Check Error Types**
   ```bash
   # Group errors by type
   grep "ERROR" logs/*.log | awk '{print $5}' | sort | uniq -c | sort -nr

   # Check error metrics
   curl http://prometheus:9090/api/v1/query?query=honua_errors_total
   ```

2. **Check System Resources**
   ```bash
   # CPU usage
   kubectl top pods -n honua

   # Memory usage
   kubectl top nodes

   # Disk usage
   df -h
   ```

3. **Check Recent Changes**
   ```bash
   # Recent deployments
   kubectl rollout history deployment/honua-server

   # Recent configuration changes
   kubectl describe configmap honua-config
   ```

### Resolution Steps

#### Option 1: Rollback Recent Changes

```bash
# Rollback deployment
kubectl rollout undo deployment/honua-server

# Verify rollback
kubectl rollout status deployment/honua-server
```

#### Option 2: Scale Resources

```bash
# Scale up replicas
kubectl scale deployment/honua-server --replicas=5

# Increase resource limits
kubectl set resources deployment/honua-server \
  --limits=cpu=2,memory=4Gi \
  --requests=cpu=1,memory=2Gi
```

#### Option 3: Enable Maintenance Mode

```bash
# Redirect traffic to maintenance page
kubectl apply -f maintenance-mode.yaml

# This gives time to fix issues without impacting users
```

### Prevention
- Implement gradual rollouts (canary/blue-green)
- Set up comprehensive monitoring
- Use auto-scaling
- Regular load testing

---

## Cascading Failures

### Symptoms
- Multiple services failing simultaneously
- Circuit breakers opening in sequence
- Database connection pool exhausted
- Overall system degradation

### Investigation Steps

1. **Identify Origin of Failure**
   ```bash
   # Check timestamps of first failures
   grep "ERROR" logs/*.log | head -20

   # Check dependency graph
   curl http://prometheus:9090/api/v1/query?query=honua_dependency_health
   ```

2. **Check Resource Exhaustion**
   ```bash
   # Database connections
   psql -c "SELECT count(*) FROM pg_stat_activity;"

   # Thread pool
   curl http://localhost:5000/metrics | grep threadpool

   # File descriptors
   lsof | wc -l
   ```

### Resolution Steps

#### Option 1: Emergency Shutdown

```bash
# Stop all traffic
kubectl scale deployment/honua-server --replicas=0

# Fix issues
# ...

# Gradually bring back online
kubectl scale deployment/honua-server --replicas=1
# Wait and verify
kubectl scale deployment/honua-server --replicas=3
```

#### Option 2: Isolate Failing Component

```bash
# Disable specific feature
kubectl set env deployment/honua-server \
  HONUA_FEATURES_RASTER_ENABLED=false

# This allows rest of system to function
```

#### Option 3: Full System Restart

```bash
# Restart all components in order
kubectl rollout restart deployment/redis
sleep 30
kubectl rollout restart deployment/postgres
sleep 30
kubectl rollout restart deployment/honua-server
```

### Prevention
- Implement circuit breakers for all services
- Set resource limits on all operations
- Use bulkhead pattern to isolate failures
- Regular chaos engineering tests

---

## General Recovery Checklist

For any error scenario:

1. ✅ **Assess Impact** - How many users affected?
2. ✅ **Check Monitoring** - What do metrics show?
3. ✅ **Review Recent Changes** - Any recent deployments?
4. ✅ **Check Dependencies** - Are all dependencies healthy?
5. ✅ **Review Logs** - What errors are occurring?
6. ✅ **Implement Fix** - Apply resolution steps
7. ✅ **Verify Recovery** - Confirm system is healthy
8. ✅ **Document Incident** - Record for future reference
9. ✅ **Post-Mortem** - Analyze and prevent recurrence

## Contact Information

- **On-Call Engineer**: [On-call rotation]
- **Database Team**: [Contact info]
- **Infrastructure Team**: [Contact info]
- **Cloud Provider Support**: [Support contacts]

## Additional Resources

- [Error Boundary Documentation](./ERROR_BOUNDARY_HANDLING.md)
- [Monitoring Dashboard](http://grafana:3000)
- [Alerting Rules](../docker/prometheus/alerts/)
- [Runbooks Directory](./operations/RUNBOOKS.md)
