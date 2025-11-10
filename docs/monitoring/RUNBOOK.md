# Honua Server Monitoring Runbook

Operational procedures for responding to alerts and issues in Honua Server monitoring.

## Table of Contents

- [Alert Response Procedures](#alert-response-procedures)
- [Incident Investigation Steps](#incident-investigation-steps)
- [Common Issues & Solutions](#common-issues--solutions)
- [Escalation Paths](#escalation-paths)
- [Post-Incident Actions](#post-incident-actions)

## Alert Response Procedures

### Critical: ServiceDown

**Alert**: Service unreachable for 2+ minutes

**Immediate Actions** (0-5 minutes):

1. Confirm service is down:
   ```bash
   curl -v http://honua-server:5000/health/live
   # Should respond 200 OK if healthy
   ```

2. Check process status:
   ```bash
   docker ps | grep honua
   # Should show "honua-server" with status "Up"
   ```

3. Check system resources:
   ```bash
   # High CPU or memory usage?
   top
   # OOM (Out of Memory) errors?
   dmesg | tail -20
   ```

4. Check network connectivity:
   ```bash
   # Can we reach the server?
   ping honua-server
   # Can we reach the port?
   nc -zv honua-server 5000
   ```

**Next Steps**:
- If container stopped: Check logs with `docker logs honua-server -n 100`
- If high CPU: Check for infinite loops or stuck threads
- If high memory: Check for memory leak or heap corruption
- If network issue: Verify DNS, firewall rules

**Escalation**: Page on-call engineer immediately

---

### Critical: HighErrorBudgetBurn

**Alert**: Error rate > 10% for 5+ minutes (SLO at risk)

**Immediate Actions** (0-5 minutes):

1. Verify alert is real (not false positive):
   ```bash
   # Check actual error rate
   curl 'http://prometheus:9090/api/v1/query' \
     -d 'query=rate(http_requests_total{status_class="5xx"}[5m])/rate(http_requests_total[5m])'
   ```

2. Identify affected endpoints:
   ```bash
   # Which endpoints are failing?
   curl 'http://prometheus:9090/api/v1/query' \
     -d 'query=topk(5, rate(http_requests_total{status_class="5xx"}[5m]) by (path))'
   ```

3. Check error types:
   ```bash
   # In app logs:
   docker logs honua-server -f --tail 100 | grep -i error
   ```

**Diagnosis** (5-15 minutes):

1. Check recent deployments:
   ```bash
   # Was there a recent change?
   git log --oneline -10
   ```

2. Check database health:
   ```bash
   # Is database responding?
   psql -h postgres -U honua -d honua -c "SELECT 1"
   ```

3. Check external dependencies:
   ```bash
   # Are external APIs responding?
   curl -v https://external-api.example.com
   ```

**Remediation**:
- **If new deployment caused it**: Rollback
- **If database issue**: Restart database/increase connections
- **If external dependency down**: Route around or degrade service
- **If genuine bug**: Apply hotfix

**Escalation**: Page on-call engineer and SRE

---

### Warning: P95ResponseTimeHigh

**Alert**: p95 latency > 5 seconds for 10+ minutes

**Initial Investigation** (0-5 minutes):

1. Check if it's system-wide or endpoint-specific:
   ```bash
   # Which endpoints are slow?
   curl 'http://prometheus:9090/api/v1/query' \
     -d 'query=histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le, path))'
   ```

2. Compare against baseline:
   - Is p50 also high? → System issue
   - Is p50 normal, p95 high? → Outliers/specific condition

3. Check resource usage:
   ```bash
   # CPU usage
   top -p $(docker inspect -f '{{ .State.Pid }}' honua-server)
   # Memory usage
   docker stats honua-server
   ```

**Deep Dive** (5-15 minutes):

1. Check database performance:
   ```bash
   # Slow queries?
   curl 'http://prometheus:9090/api/v1/query' \
     -d 'query=histogram_quantile(0.95, sum(rate(db_query_duration_seconds_bucket[5m])) by (le))'
   ```

2. Check cache efficiency:
   ```bash
   # Low hit rate?
   curl 'http://prometheus:9090/api/v1/query' \
     -d 'query=rate(cache_lookups_total{result="hit"}[5m])/rate(cache_lookups_total[5m])'
   ```

3. Check garbage collection:
   ```bash
   # In logs: Look for "GC pause" entries
   docker logs honua-server | grep -i "gc\|garbage"
   ```

4. Profile with tracing:
   - Open Jaeger: http://localhost:16686
   - Find slowest traces
   - Look for slow spans

**Common Fixes**:
- Scale up: Add more instances/resources
- Optimize queries: Add indexes, cache more
- Increase timeout if legitimate slow operation
- Reduce garbage collection pressure

---

### Warning: DatabaseSlowQueries

**Alert**: p95 query duration > 2 seconds

**Investigation** (0-5 minutes):

1. Identify the slow queries:
   ```bash
   # PostgreSQL slow query log
   psql -h postgres -U honua -d honua -c "
   SELECT query, calls, mean_exec_time
   FROM pg_stat_statements
   ORDER BY mean_exec_time DESC
   LIMIT 10;"
   ```

2. Check query plans for problematic queries:
   ```bash
   psql -h postgres -U honua -d honua -c "
   EXPLAIN ANALYZE [SLOW_QUERY_HERE];"
   ```

3. Check for missing indexes:
   - Look for "Seq Scan" in EXPLAIN output
   - Check table size with `\d+ table_name`

**Remediation**:

1. **If missing index**:
   ```sql
   CREATE INDEX idx_table_column ON table_name(column_name);
   ```

2. **If query is inefficient**:
   - Rewrite query
   - Add WHERE clause to reduce rows
   - Use JOIN instead of subqueries

3. **If table is too large**:
   - Archive old data
   - Partition table
   - Increase table statistics

---

### Warning: HighMemoryUsage

**Alert**: Process memory > 4GB for 10+ minutes

**Immediate Check** (0-2 minutes):

1. Is this expected?
   ```bash
   # Check traffic volume
   curl 'http://prometheus:9090/api/v1/query' \
     -d 'query=rate(http_requests_total[5m])'
   # If high traffic, high memory may be OK
   ```

2. Check memory trend:
   ```bash
   # Is memory growing (leak) or stable?
   # Look at graph over last 1 hour
   http://prometheus:9090
   # Query: process_resident_memory_bytes
   ```

**Investigation** (2-10 minutes):

1. Check .NET heap size:
   ```bash
   # In logs, look for GC information
   docker logs honua-server | grep -i "heap\|gc"
   ```

2. Dump heap for analysis:
   ```bash
   # Create heap dump
   docker exec honua-server dotnet-dump collect -o /tmp/heap.dmp
   ```

3. Check for memory leaks:
   - Are object counts growing?
   - Check cache sizes
   - Verify connection pools are sized correctly

**Solutions**:

1. **Temporary**: Increase memory limit
   ```yaml
   deploy:
     resources:
       limits:
         memory: 8G
   ```

2. **Long-term**: Fix root cause
   - Optimize algorithm
   - Reduce cache size
   - Fix memory leak

**Escalation**: If memory continues growing, scale horizontally

---

## Incident Investigation Steps

### Step-by-Step Investigation Framework

1. **Gather Information** (5 minutes)
   ```bash
   # System status
   docker ps -a
   docker stats

   # Recent logs
   docker logs honua-server --since 10m

   # Recent metrics
   curl 'http://prometheus:9090/api/v1/query_range' \
     -d 'query=http_requests_total' \
     -d 'start=<10m_ago>' \
     -d 'end=<now>' \
     -d 'step=1m'
   ```

2. **Determine Timeline**
   - When did alert fire?
   - When did symptoms start?
   - What changed around that time?

3. **Isolate the Problem** (10 minutes)
   - Is it system-wide or component-specific?
   - Which layer is affected?
     - Network?
     - Application?
     - Database?
     - External dependency?

4. **Check Recent Changes**
   ```bash
   git log --since="30 minutes ago" --oneline
   # Check deployed version
   docker inspect honua-server | grep -i image
   ```

5. **Check Dependencies**
   ```bash
   # Database
   psql -h postgres -U honua -d honua -c "SELECT 1"

   # Cache
   redis-cli -h redis ping

   # External services
   curl -v https://api.example.com/health
   ```

6. **Apply Mitigation** (immediate)
   - Scale up
   - Restart service
   - Rollback deployment
   - Route around issue

7. **Implement Fix** (ongoing)
   - Code change
   - Configuration change
   - Infrastructure change

---

## Common Issues & Solutions

### Issue: High Database Connection Pool Usage

**Symptoms**:
- Alert: DatabaseConnectionPoolExhausted
- p95 latency increases
- Requests queue up

**Investigation**:

```bash
# Check connection pool metrics
curl 'http://prometheus:9090/api/v1/query' \
  -d 'query=db_connection_pool_in_use'

# Check slow queries
curl 'http://prometheus:9090/api/v1/query' \
  -d 'query=histogram_quantile(0.95, sum(rate(db_query_duration_seconds_bucket[5m])) by (le))'

# Check for query locks in database
psql -h postgres -U honua -d honua -c "
SELECT pid, usename, pg_blocking_pids(pid), query
FROM pg_stat_activity
WHERE pg_blocking_pids(pid) != ARRAY[]::int[];"
```

**Solutions** (in priority order):

1. **Increase pool size** (immediate):
   ```csharp
   var connectionString = "Server=postgres;...;Max Pool Size=50";
   ```

2. **Kill stuck connections** (if necessary):
   ```sql
   SELECT pg_terminate_backend(pid) FROM pg_stat_activity
   WHERE query LIKE 'SELECT%' AND state = 'idle in transaction';
   ```

3. **Optimize slow queries** (long-term):
   - Add indexes
   - Rewrite queries
   - Enable query result caching

---

### Issue: Cache Hit Rate Drops Suddenly

**Symptoms**:
- Alert: LowCacheHitRate
- Database load increases
- p95 latency increases

**Investigation**:

```bash
# Compare hit rate over time
curl 'http://prometheus:9090/api/v1/query_range' \
  -d 'query=rate(cache_lookups_total{result="hit"}[5m])/rate(cache_lookups_total[5m])' \
  -d 'start=<1h_ago>' \
  -d 'end=<now>' \
  -d 'step=5m'

# Check cache eviction rate
curl 'http://prometheus:9090/api/v1/query' \
  -d 'query=rate(cache_evictions_total[5m])'

# Check if cache was cleared
docker logs honua-server | grep -i "cache.*clear\|cache.*reset"
```

**Solutions**:

1. **Identify cache thrashing**:
   - Are there more unique keys than cache capacity?
   - Working set size > cache size?

2. **Increase cache size**:
   ```csharp
   var cache = new MemoryCache(new MemoryCacheOptions {
       SizeLimit = 1024 * 1024 * 500 // 500 MB
   });
   ```

3. **Improve cache hit strategy**:
   - Pre-warm cache on startup
   - Cache more aggressively
   - Increase TTL for frequently accessed data

4. **Check for cache invalidation issues**:
   - Are cache entries being cleared too frequently?
   - Review cache invalidation logic

---

### Issue: High CPU Usage

**Symptoms**:
- Alert: HighCPUUsage
- Request latency increases
- System becomes unresponsive

**Investigation**:

```bash
# Identify process using CPU
top

# Check CPU by thread
top -H

# Get detailed metrics
curl 'http://prometheus:9090/api/v1/query' \
  -d 'query=rate(process_cpu_seconds_total[5m])'
```

**Root Causes & Solutions**:

1. **High request volume**:
   - Scale horizontally (add more instances)
   - Optimize hot code paths

2. **Inefficient algorithm**:
   - Profile with: `dotnet trace collect`
   - Optimize algorithm
   - Use better data structures

3. **Garbage collection pressure**:
   - Reduce allocations
   - Tune GC settings
   - Increase heap size

4. **Busy waiting/polling**:
   - Use events/callbacks instead
   - Increase polling interval
   - Use async/await properly

---

### Issue: OOM (Out of Memory) Killer

**Symptoms**:
- Process restarts suddenly
- No error in logs (killed by kernel)
- System shows "Killed"

**Investigation**:

```bash
# Check kernel messages
dmesg | tail -20 | grep -i "killed\|memory\|oom"

# Check process memory limit
docker inspect honua-server | grep -i memory

# Check system memory
free -h
```

**Solutions**:

1. **Increase memory limit** (immediate):
   ```yaml
   deploy:
     resources:
       limits:
         memory: 8G
   ```

2. **Fix memory leak** (long-term):
   ```bash
   # Create heap dump
   docker exec honua-server dotnet-dump collect -o /tmp/heap.dmp

   # Analyze with dotnet-heapq or Visual Studio
   ```

3. **Reduce memory footprint**:
   - Reduce cache size
   - Use streaming for large data
   - Implement pagination

---

## Escalation Paths

### Severity Levels and Escalation

#### Severity 1 (Critical)

**Condition**: Service completely down OR error rate > 10%

**Response Time**: Immediate (< 2 minutes)

**Actions**:
1. Page on-call engineer
2. If not responding in 30 seconds, page manager
3. If not responding in 60 seconds, page director

**Communication**:
- Alert goes to #honua-critical Slack channel
- PagerDuty alert sent to on-call rotation
- Conference bridge established if needed

#### Severity 2 (Warning)

**Condition**: Degraded performance OR error rate 5-10%

**Response Time**: Urgent (< 15 minutes)

**Actions**:
1. Alert team in #honua-alerts
2. On-call engineer investigates
3. Escalate to manager if not resolved in 30 minutes

**Communication**:
- Slack notification
- Optional PagerDuty alert

#### Severity 3 (Info)

**Condition**: Minor issue OR error rate < 5%

**Response Time**: Within business hours

**Actions**:
1. Log in monitoring system
2. Investigate during normal on-call rotation
3. Create ticket for future improvement

**Communication**:
- Email notification
- Ticket created in issue tracker

### Escalation Decision Tree

```
Alert Fires
├── Is service responding?
│   ├── No → SEVERITY 1 (Page on-call)
│   └── Yes → Continue
├── Is error rate > 10%?
│   ├── Yes → SEVERITY 1 (Page on-call)
│   └── No → Continue
├── Is p95 latency > 10s?
│   ├── Yes → SEVERITY 2 (Alert team)
│   └── No → Continue
└── → SEVERITY 3 (Log ticket)
```

---

## Post-Incident Actions

### Immediate (0-30 minutes)

1. **Stabilize the system**
   - Apply hot fix or rollback
   - Monitor metrics to confirm recovery
   - Keep service running

2. **Communicate status**
   ```
   [Incident Started 14:30 UTC]
   Issue: Database connection pool exhausted
   Status: Investigating
   Impact: 5% of requests failing
   ETA: 5 minutes
   ```

3. **Gather information**
   - Save logs
   - Screenshot metrics
   - Note timeline

### Short-term (30 minutes - 24 hours)

1. **Post-mortem meeting**
   - When: Within 24 hours
   - Attendees: On-call, manager, affected teams
   - Duration: 30 minutes
   - Agenda: What happened, why, what's next

2. **Post-mortem document**
   ```
   Title: Database Connection Pool Exhaustion - 2024-11-10

   Timeline:
   - 14:30: Alert fired
   - 14:32: Investigated
   - 14:35: Increased pool size
   - 14:37: Recovered

   Root Cause:
   - Slow query caused connection to hang
   - Pool filled up
   - New requests queued

   Contributing Factors:
   - No query timeout configured
   - Pool size too small

   Action Items:
   - [ ] Add query timeout (24h)
   - [ ] Increase pool size (24h)
   - [ ] Add alert for slow queries (1 week)
   - [ ] Load test (2 weeks)

   Lessons Learned:
   - Need better monitoring for connection pool
   - Timeout configuration is critical
   ```

3. **Update runbook**
   - Add new procedures
   - Update thresholds based on incident
   - Add prevention measures

### Long-term (1-2 weeks)

1. **Implement preventions**
   - Code changes
   - Configuration updates
   - Alert rule improvements

2. **Load testing**
   - Test at 2x normal load
   - Verify thresholds are appropriate

3. **Documentation updates**
   - Update this runbook
   - Add incident as case study
   - Update SLOs if needed

4. **Blameless culture**
   - Focus on systems, not people
   - Thank responders
   - Share learnings with team

---

## Quick Reference Commands

### Metrics Queries

```bash
# Error rate
curl 'http://prometheus:9090/api/v1/query' \
  -d 'query=rate(http_requests_total{status_class="5xx"}[5m])/rate(http_requests_total[5m])'

# p95 latency
curl 'http://prometheus:9090/api/v1/query' \
  -d 'query=histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le))'

# Cache hit rate
curl 'http://prometheus:9090/api/v1/query' \
  -d 'query=rate(cache_lookups_total{result="hit"}[5m])/rate(cache_lookups_total[5m])'

# Database connections
curl 'http://prometheus:9090/api/v1/query' \
  -d 'query=db_connection_pool_in_use'
```

### Logs

```bash
# Tail live logs
docker logs honua-server -f

# Last 100 lines
docker logs honua-server -n 100

# Since specific time
docker logs honua-server --since 30m

# Search logs
docker logs honua-server | grep -i "error\|exception"
```

### Database

```bash
# Connect
psql -h postgres -U honua -d honua

# Check slow queries
SELECT query, calls, mean_exec_time FROM pg_stat_statements
ORDER BY mean_exec_time DESC LIMIT 10;

# Check connections
SELECT * FROM pg_stat_activity;

# Kill stuck connection
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE pid = <pid>;
```

---

**Version**: 1.0.0
**Last Updated**: November 2024
**Maintained By**: Platform Engineering
