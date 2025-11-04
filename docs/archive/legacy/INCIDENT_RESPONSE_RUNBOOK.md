# Honua Incident Response Runbook

## Overview

This runbook provides step-by-step procedures for responding to incidents in the Honua geospatial API server.

## Incident Severity Levels

### Critical (P0)
- Service is completely down
- Data loss or corruption
- Security breach
- Multiple critical alerts firing

**Response Time**: Immediate (< 5 minutes)
**Escalation**: Page on-call engineer immediately

### High (P1)
- Significant degradation in service
- High error rates (> 5%)
- Database or cache unavailable
- Critical functionality broken

**Response Time**: < 15 minutes
**Escalation**: Notify on-call engineer

### Medium (P2)
- Performance degradation
- Non-critical functionality issues
- Resource warnings

**Response Time**: < 1 hour
**Escalation**: Normal business hours support

### Low (P3)
- Minor issues
- Maintenance notifications

**Response Time**: Next business day
**Escalation**: Create ticket for review

---

## Common Incidents

### 1. Service Down (HonuaServiceDown Alert)

**Symptoms:**
- Health check endpoint returning errors
- No API responses
- `up{job="honua-api"} == 0`

**Diagnosis:**
```bash
# Check service status
docker ps | grep honua-api
# or
kubectl get pods -l app=honua-api

# Check logs
docker logs honua-api --tail=100
# or
kubectl logs -l app=honua-api --tail=100

# Check resource usage
docker stats honua-api
# or
kubectl top pods -l app=honua-api
```

**Resolution Steps:**

1. **Immediate**: Check if service is running
   ```bash
   # Docker
   docker restart honua-api

   # Kubernetes
   kubectl rollout restart deployment/honua-api
   ```

2. **Check dependencies**: Verify PostgreSQL and Redis are healthy
   ```bash
   # Test database connection
   docker exec postgres pg_isready

   # Test Redis connection
   docker exec redis redis-cli ping
   ```

3. **Check configuration**: Review environment variables and config files
   ```bash
   # Docker
   docker inspect honua-api | grep -A 20 "Env"

   # Kubernetes
   kubectl describe pod <pod-name>
   ```

4. **Review logs**: Look for startup errors or panics
   ```bash
   # Search for errors
   docker logs honua-api | grep -i "error\|exception\|fatal"
   ```

5. **Verify network**: Ensure container networking is functional
   ```bash
   # Docker
   docker network ls
   docker network inspect honua_network

   # Kubernetes
   kubectl get svc
   kubectl describe svc honua-api
   ```

**Escalation**: If service doesn't recover after restart, escalate to P0 and page engineering team.

---

### 2. High Error Rate (HighErrorRate Alert)

**Symptoms:**
- Error rate > 5% sustained for 5+ minutes
- Increased 5xx responses
- Client complaints

**Diagnosis:**
```bash
# Check error distribution by protocol
curl -s http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=sum(rate(honua_api_errors_total[5m])) by (api_protocol)'

# Check logs for specific errors
docker logs honua-api --tail=500 | grep -i error

# Check database errors
docker exec postgres psql -U honua -c "SELECT * FROM pg_stat_activity WHERE state = 'active' AND query_start < NOW() - INTERVAL '1 minute';"
```

**Resolution Steps:**

1. **Identify error source**: Determine which API protocol or layer is failing
   ```bash
   # View Grafana dashboard
   # Navigate to Honua Overview → Error Rate by Endpoint
   ```

2. **Check database health**:
   ```bash
   # Connection count
   docker exec postgres psql -U honua -c "SELECT count(*) FROM pg_stat_activity;"

   # Lock analysis
   docker exec postgres psql -U honua -c "SELECT * FROM pg_locks WHERE NOT granted;"
   ```

3. **Check for slow queries**:
   ```bash
   # Long-running queries
   docker exec postgres psql -U honua -c "
   SELECT pid, now() - query_start AS duration, query
   FROM pg_stat_activity
   WHERE state = 'active'
   ORDER BY duration DESC;"
   ```

4. **Restart affected components** if errors are localized to specific service
   ```bash
   # Restart API if database is healthy
   docker restart honua-api
   ```

5. **Scale up** if errors are due to capacity
   ```bash
   # Kubernetes
   kubectl scale deployment honua-api --replicas=5
   ```

**Escalation**: If error rate doesn't improve within 15 minutes, escalate to P0.

---

### 3. High Latency (HighLatency Alert)

**Symptoms:**
- p95 latency > 2000ms sustained for 10+ minutes
- Slow API responses
- Timeouts

**Diagnosis:**
```bash
# Check current latency percentiles
curl -s http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=histogram_quantile(0.95, rate(honua_api_request_duration_bucket[5m]))'

# Check database query performance
docker exec postgres psql -U honua -c "
SELECT query, mean_exec_time, calls
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;"

# Check cache performance
curl -s http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=rate(honua_cache_hits_total[5m])/(rate(honua_cache_hits_total[5m])+rate(honua_cache_misses_total[5m]))'
```

**Resolution Steps:**

1. **Identify bottleneck**: Check if issue is DB, cache, or compute
   - High DB latency → Database issue
   - Low cache hit rate → Cache issue
   - High CPU → Compute issue

2. **Database optimization**:
   ```bash
   # Kill long-running queries
   docker exec postgres psql -U honua -c "
   SELECT pg_terminate_backend(pid)
   FROM pg_stat_activity
   WHERE query_start < NOW() - INTERVAL '5 minutes'
   AND state = 'active';"

   # Analyze slow queries
   docker exec postgres psql -U honua -c "
   SELECT query, calls, mean_exec_time
   FROM pg_stat_statements
   WHERE mean_exec_time > 1000
   ORDER BY mean_exec_time DESC;"
   ```

3. **Cache optimization**:
   ```bash
   # Check Redis memory usage
   docker exec redis redis-cli INFO memory

   # If Redis is full, flush LRU keys
   docker exec redis redis-cli CONFIG SET maxmemory-policy allkeys-lru
   ```

4. **Scale horizontally** if CPU/memory bound:
   ```bash
   # Kubernetes
   kubectl scale deployment honua-api --replicas=10

   # Check HPA status
   kubectl get hpa
   ```

5. **Restart services** as last resort:
   ```bash
   docker restart honua-api redis
   ```

**Escalation**: If latency doesn't improve within 30 minutes, escalate to P1.

---

### 4. Database Connection Pool Exhausted (DatabaseConnectionsExhausted Alert)

**Symptoms:**
- `pg_stat_activity_count / pg_settings_max_connections > 0.9`
- "Too many connections" errors
- API timeouts

**Diagnosis:**
```bash
# Check current connection count
docker exec postgres psql -U honua -c "
SELECT count(*), state
FROM pg_stat_activity
GROUP BY state;"

# Check idle connections
docker exec postgres psql -U honua -c "
SELECT count(*)
FROM pg_stat_activity
WHERE state = 'idle'
AND state_change < NOW() - INTERVAL '5 minutes';"

# Check max connections setting
docker exec postgres psql -U honua -c "SHOW max_connections;"
```

**Resolution Steps:**

1. **Kill idle connections**:
   ```bash
   docker exec postgres psql -U honua -c "
   SELECT pg_terminate_backend(pid)
   FROM pg_stat_activity
   WHERE state = 'idle'
   AND state_change < NOW() - INTERVAL '5 minutes'
   AND pid != pg_backend_pid();"
   ```

2. **Kill long-running connections**:
   ```bash
   docker exec postgres psql -U honua -c "
   SELECT pg_terminate_backend(pid)
   FROM pg_stat_activity
   WHERE query_start < NOW() - INTERVAL '10 minutes'
   AND state = 'active'
   AND pid != pg_backend_pid();"
   ```

3. **Increase max_connections** (requires restart):
   ```bash
   # Edit postgresql.conf
   docker exec -it postgres bash
   echo "max_connections = 200" >> /var/lib/postgresql/data/postgresql.conf
   exit

   # Restart PostgreSQL
   docker restart postgres
   ```

4. **Review application connection pooling**:
   - Check Honua API connection pool settings
   - Ensure connections are being properly released

5. **Scale database** if persistent issue:
   - Consider read replicas for read-heavy workloads
   - Upgrade database instance size

**Escalation**: If connections remain exhausted after killing idle connections, escalate to P0.

---

### 5. Disk Space Critical (DiskAlmostFull Alert)

**Symptoms:**
- Available disk space < 10%
- Write errors
- Service degradation

**Diagnosis:**
```bash
# Check disk usage
df -h

# Find largest directories
du -h / | sort -rh | head -20

# Check Docker disk usage
docker system df

# Check PostgreSQL data size
docker exec postgres psql -U honua -c "
SELECT pg_database.datname,
       pg_size_pretty(pg_database_size(pg_database.datname)) AS size
FROM pg_database
ORDER BY pg_database_size(pg_database.datname) DESC;"
```

**Resolution Steps:**

1. **Clean Docker resources**:
   ```bash
   # Remove unused images
   docker image prune -a -f

   # Remove unused volumes
   docker volume prune -f

   # Remove build cache
   docker builder prune -a -f
   ```

2. **Clean application logs**:
   ```bash
   # Truncate old logs
   find /var/log -name "*.log" -type f -mtime +7 -delete

   # Rotate logs
   logrotate -f /etc/logrotate.conf
   ```

3. **Clean PostgreSQL**:
   ```bash
   # Vacuum database
   docker exec postgres psql -U honua -c "VACUUM FULL;"

   # Clean old WAL files
   docker exec postgres psql -U honua -c "SELECT pg_switch_wal();"
   ```

4. **Archive old data**:
   ```bash
   # Export old raster tiles to S3
   # (Custom script based on retention policy)

   # Archive old logs
   tar -czf logs-$(date +%Y%m%d).tar.gz /var/log/*.log
   aws s3 cp logs-$(date +%Y%m%d).tar.gz s3://honua-archives/
   rm /var/log/*.log
   ```

5. **Expand disk** as permanent solution:
   - Resize EBS volume (AWS)
   - Add new disk and migrate data
   - Implement retention policies

**Escalation**: If disk usage > 95%, immediately escalate to P0 and provision additional storage.

---

### 6. Memory Exhausted (OutOfMemory Alert)

**Symptoms:**
- Available memory < 10%
- OOM kills
- Service crashes

**Diagnosis:**
```bash
# Check system memory
free -h

# Check container memory usage
docker stats --no-stream

# Kubernetes memory usage
kubectl top pods

# Check for OOM kills
dmesg | grep -i "out of memory"
journalctl | grep -i oom
```

**Resolution Steps:**

1. **Identify memory hog**:
   ```bash
   # Top memory consumers
   ps aux --sort=-%mem | head -10

   # Docker containers
   docker stats --no-stream --format "table {{.Name}}\t{{.MemUsage}}" | sort -k2 -h
   ```

2. **Restart high-memory containers**:
   ```bash
   docker restart <container-name>
   ```

3. **Clear caches**:
   ```bash
   # Clear Redis cache
   docker exec redis redis-cli FLUSHALL

   # Clear system page cache (use cautiously)
   sync; echo 3 > /proc/sys/vm/drop_caches
   ```

4. **Scale down non-essential services**:
   ```bash
   # Reduce replicas temporarily
   kubectl scale deployment preseed-worker --replicas=1
   ```

5. **Add memory** as permanent fix:
   - Increase container memory limits
   - Upgrade instance size
   - Add horizontal scaling

**Escalation**: If memory usage doesn't decrease after restarts, immediately escalate to P0.

---

## Post-Incident Procedures

### 1. Document the Incident
Create incident report with:
- Timeline of events
- Root cause analysis
- Actions taken
- Resolution details

### 2. Post-Mortem Meeting
Schedule within 48 hours with:
- What happened
- What went well
- What could be improved
- Action items

### 3. Update Runbook
Add lessons learned to this document

### 4. Implement Preventive Measures
- Add new alerts
- Update monitoring dashboards
- Improve automation
- Update documentation

---

## Emergency Contacts

| Role | Contact | Availability |
|------|---------|--------------|
| On-Call Engineer | PagerDuty | 24/7 |
| Database Administrator | [Email/Slack] | Business hours + escalation |
| Infrastructure Lead | [Email/Slack] | Business hours + escalation |
| Product Owner | [Email/Slack] | Business hours |

## Useful Links

- **Grafana**: http://grafana.honua.local
- **Prometheus**: http://prometheus.honua.local
- **AlertManager**: http://alertmanager.honua.local
- **Logs (Loki)**: http://grafana.honua.local/explore
- **Documentation**: https://docs.honua.io
- **Status Page**: https://status.honua.io

## Quick Reference Commands

```bash
# Health check
curl http://localhost:8080/health/ready

# Service restart (Docker)
docker restart honua-api postgres redis

# Service restart (Kubernetes)
kubectl rollout restart deployment/honua-api

# View logs
docker logs honua-api --tail=100 -f
kubectl logs -f -l app=honua-api

# Database connection test
docker exec postgres pg_isready -U honua

# Cache test
docker exec redis redis-cli ping

# Kill long-running queries
docker exec postgres psql -U honua -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE query_start < NOW() - INTERVAL '5 minutes';"

# Scale up (Kubernetes)
kubectl scale deployment honua-api --replicas=10

# Check alerts
curl http://alertmanager:9093/api/v1/alerts
```
