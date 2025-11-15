# Read Replica Routing

Read replica routing is an enterprise feature that distributes read operations across multiple database replicas to improve performance and scalability in high-traffic deployments.

## Overview

In typical geospatial applications, 90%+ of database operations are read queries (feature queries, tile generation, statistics). Read replica routing offloads this read traffic from the primary database to dedicated read replicas, allowing the primary to focus on write operations.

### Key Benefits

- **Improved Performance**: Distributes read load across multiple databases
- **Higher Throughput**: Primary database handles only writes + fallback reads
- **Better Scalability**: Add more replicas to handle increased read traffic
- **High Availability**: Automatic fallback to primary if replicas unavailable
- **Geographic Distribution**: Place replicas closer to users for lower latency

## Architecture

```
┌─────────────────┐
│   Application   │
└────────┬────────┘
         │
    ┌────▼────┐
    │ Router  │  (IDataSourceRouter)
    └────┬────┘
         │
    ┌────┴──────────────────┐
    │                       │
┌───▼────┐          ┌──────▼──────┐
│Primary │          │  Replicas   │
│Database│◄─────────┤  (Round     │
│        │ Repl.    │  Robin)     │
└────────┘          └─────────────┘
  Writes              Reads Only
```

### Components

1. **IDataSourceRouter**: Routes operations to appropriate data sources
2. **ReadReplicaRouter**: Implements round-robin selection with health checking
3. **ReadReplicaHealthCheck**: Monitors replica availability
4. **ReadReplicaInitializationService**: Registers replicas at startup

## Configuration

### HCL Configuration (Configuration V2)

```hcl
# Primary database
data_source "primary" {
  provider   = "postgresql"
  connection = "${env:DATABASE_PRIMARY_CONNECTION}"
  read_only  = false
}

# Read replicas
data_source "replica_1" {
  provider   = "postgresql"
  connection = "${env:DATABASE_REPLICA_1_CONNECTION}"
  read_only  = true
  max_replication_lag = 5  # Skip if lag > 5 seconds
}

data_source "replica_2" {
  provider   = "postgresql"
  connection = "${env:DATABASE_REPLICA_2_CONNECTION}"
  read_only  = true
  max_replication_lag = 5
}
```

### appsettings.json

```json
{
  "ReadReplica": {
    "EnableReadReplicaRouting": true,
    "FallbackToPrimary": true,
    "HealthCheckIntervalSeconds": 30,
    "MaxConsecutiveFailures": 3,
    "UnhealthyRetryIntervalSeconds": 60,
    "MaxReplicationLagSeconds": 10,
    "EnableDetailedLogging": false
  }
}
```

### Environment Variables

```bash
# Enable routing
export ReadReplica__EnableReadReplicaRouting=true
export ReadReplica__FallbackToPrimary=true

# Database connections
export DATABASE_PRIMARY_CONNECTION="Host=primary.db;Database=honua;..."
export DATABASE_REPLICA_1_CONNECTION="Host=replica1.db;Database=honua;..."
export DATABASE_REPLICA_2_CONNECTION="Host=replica2.db;Database=honua;..."
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableReadReplicaRouting` | bool | false | Enable/disable read replica routing (opt-in) |
| `FallbackToPrimary` | bool | true | Fall back to primary if all replicas unavailable |
| `HealthCheckIntervalSeconds` | int | 30 | How often to check replica health |
| `MaxConsecutiveFailures` | int | 3 | Failures before marking replica unhealthy |
| `UnhealthyRetryIntervalSeconds` | int | 60 | Wait time before retrying unhealthy replica |
| `MaxReplicationLagSeconds` | int? | null | Global max replication lag (can be overridden per replica) |
| `EnableDetailedLogging` | bool | false | Log every routing decision |

## Operation Routing

### Routed to Read Replicas

These operations are automatically routed to read replicas when enabled:

- **Feature Queries**: `GET /collections/{id}/items`
- **Single Feature**: `GET /collections/{id}/items/{featureId}`
- **Count Operations**: `GET /collections/{id}/items?resultType=hits`
- **Tile Generation**: `GET /tiles/{z}/{x}/{y}`
- **Statistics**: Aggregation queries (SUM, AVG, MIN, MAX)
- **Distinct Values**: `DISTINCT` queries
- **Extent Calculation**: Bounding box queries

### Routed to Primary Database

These operations always go to the primary database:

- **Writes**: `POST /collections/{id}/items`
- **Updates**: `PUT /collections/{id}/items/{featureId}`
- **Deletes**: `DELETE /collections/{id}/items/{featureId}`
- **Transactions**: WFS-T operations
- **Bulk Operations**: Bulk inserts/updates/deletes

## Health Checking

The system continuously monitors replica health:

1. **Health Check Query**: Executes lightweight query (e.g., `SELECT 1`)
2. **Connectivity Test**: Uses `IDataStoreProvider.TestConnectivityAsync()`
3. **Circuit Breaker**: After N failures, marks replica as unhealthy
4. **Retry Logic**: Retries unhealthy replicas after configured interval

### Health States

- **Healthy**: Passing health checks, actively serving traffic
- **Unhealthy**: Failed N consecutive checks, excluded from routing
- **Retry**: Unhealthy but retry interval elapsed, will attempt next request

## Load Balancing

### Round-Robin Selection

The router uses round-robin to distribute load:

```
Request 1 → Replica 1
Request 2 → Replica 2
Request 3 → Replica 3
Request 4 → Replica 1 (wraps around)
```

### Skipping Unhealthy Replicas

If a replica is unhealthy, it's skipped in rotation:

```
Replicas: [Replica 1 (healthy), Replica 2 (unhealthy), Replica 3 (healthy)]

Request 1 → Replica 1
Request 2 → Replica 3 (skips unhealthy Replica 2)
Request 3 → Replica 1
Request 4 → Replica 3
```

## Replication Lag Handling

Replicas with excessive lag are skipped to avoid serving stale data:

```hcl
data_source "replica_1" {
  read_only = true
  max_replication_lag = 5  # Seconds
}
```

If replication lag > 5 seconds, the router skips this replica.

**Note**: Actual lag checking requires querying `pg_stat_replication` or similar provider-specific views. Implementation is provider-dependent.

## Monitoring and Metrics

### Metrics Exported

- **Routing Decisions**: Which data source was selected and why
- **Fallback Count**: How often we fell back to primary
- **Unhealthy Replica Count**: Number of unhealthy replicas
- **Query Distribution**: Percentage of queries to each data source
- **Replica Lag**: Current replication lag (if supported)

### Logging

Enable detailed logging to see routing decisions:

```json
{
  "Logging": {
    "LogLevel": {
      "Honua.Server.Core.Data.ReadReplicaRouter": "Debug"
    }
  }
}
```

Example log output:

```
[Information] Registered 2 read replicas for primary data source 'primary': replica_1, replica_2
[Debug] Routing read operation for 'primary' to replica 'replica_1'
[Warning] Replica 'replica_2' marked as unhealthy after 3 consecutive failures
[Warning] All read replicas unavailable for 'primary', falling back to primary database
```

## PostgreSQL Replication Setup

### Streaming Replication (Recommended)

1. **Primary Database**: Configure for replication

```sql
-- postgresql.conf
wal_level = replica
max_wal_senders = 10
max_replication_slots = 10
```

2. **Create Replication User**

```sql
CREATE USER replicator WITH REPLICATION ENCRYPTED PASSWORD 'secure_password';
```

3. **Configure pg_hba.conf**

```
host replication replicator replica1.ip/32 md5
host replication replicator replica2.ip/32 md5
```

4. **Set Up Replica**

```bash
# On replica server
pg_basebackup -h primary.db -D /var/lib/postgresql/data -U replicator -P
```

5. **Create standby.signal**

```bash
touch /var/lib/postgresql/data/standby.signal
```

6. **Configure postgresql.conf on Replica**

```
primary_conninfo = 'host=primary.db port=5432 user=replicator password=secure_password'
hot_standby = on
```

### Verify Replication

On primary:

```sql
SELECT * FROM pg_stat_replication;
```

On replica:

```sql
SELECT pg_is_in_recovery();  -- Should return true
SELECT pg_last_wal_receive_lsn(), pg_last_wal_replay_lsn();
```

## Best Practices

### 1. Pool Sizing

- **Primary**: Sized for write workload (10-50 connections)
- **Replicas**: Smaller pools per replica (10-20 each), scaled by replica count

```hcl
data_source "primary" {
  pool {
    min_size = 5
    max_size = 50
  }
}

data_source "replica_1" {
  pool {
    min_size = 2
    max_size = 20  # 3 replicas × 20 = 60 total read capacity
  }
}
```

### 2. Failover Strategy

Always enable fallback for high availability:

```json
{
  "ReadReplica": {
    "FallbackToPrimary": true  // Critical for HA
  }
}
```

### 3. Monitoring

Set up alerts for:

- All replicas unhealthy
- High fallback rate (indicates replica issues)
- Excessive replication lag
- Replica connection pool exhaustion

### 4. Geographic Distribution

Place replicas near users:

```hcl
data_source "replica_us_west" {
  connection = "host=us-west.replica.db..."
  max_replication_lag = 10  # Allow more lag for cross-region
}

data_source "replica_eu" {
  connection = "host=eu.replica.db..."
  max_replication_lag = 10
}
```

### 5. Read-Only User

Create dedicated read-only database user for replicas:

```sql
CREATE USER honua_readonly WITH PASSWORD 'secure_password';
GRANT CONNECT ON DATABASE honua TO honua_readonly;
GRANT USAGE ON SCHEMA public TO honua_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO honua_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO honua_readonly;
```

## Troubleshooting

### Issue: All Replicas Unhealthy

**Symptoms**: Constant fallback to primary

**Solutions**:
1. Check replica connectivity: `psql -h replica.db -U honua`
2. Verify health check query succeeds
3. Review replica logs for connection errors
4. Check firewall/network rules
5. Verify replication is running: `SELECT * FROM pg_stat_replication`

### Issue: High Replication Lag

**Symptoms**: Queries returning stale data

**Solutions**:
1. Check replica server resources (CPU, disk I/O)
2. Increase `max_replication_lag` temporarily
3. Consider adding more replicas to reduce load
4. Check for long-running queries on replica
5. Monitor `pg_stat_replication` for lag metrics

### Issue: Uneven Load Distribution

**Symptoms**: One replica getting more traffic

**Solutions**:
1. Verify round-robin is working (check logs with detailed logging)
2. Check if some replicas are marked unhealthy
3. Ensure replicas have similar hardware specs
4. Review health check configuration

## Migration Path

### Phase 1: Single Database (Current)

```
All traffic → Primary Database
```

### Phase 2: Add First Replica

```hcl
data_source "primary" { read_only = false }
data_source "replica_1" { read_only = true }
```

Enable routing, monitor for 1-2 weeks.

### Phase 3: Scale Out

Add more replicas as needed:

```hcl
data_source "replica_2" { read_only = true }
data_source "replica_3" { read_only = true }
```

### Phase 4: Geographic Distribution

Add cross-region replicas for global users.

## Performance Impact

### Expected Improvements

| Metric | Before | After (3 Replicas) | Improvement |
|--------|--------|-------------------|-------------|
| Primary CPU | 80% | 20% | 75% reduction |
| Read Latency | 100ms | 60ms | 40% faster |
| Write Latency | 50ms | 30ms | 40% faster |
| Max Throughput | 1000 req/s | 3500 req/s | 3.5x increase |

**Note**: Actual results depend on workload characteristics, hardware, and network topology.

## See Also

- [Deployment Tiers](../deployment-tiers.md)
- [PostgreSQL Replication](https://www.postgresql.org/docs/current/high-availability.html)
- [Connection Pooling](./connection-pooling.md)
- [Health Checks](./health-checks.md)
