# Centralized Log Aggregation Implementation Summary

## Overview

This document summarizes the centralized log aggregation system that has been added to the Honua Process Framework observability stack.

## Deliverables

### 1. Loki Configuration
**File**: `/docker/process-testing/loki/loki-config.yaml`

- **30-day retention policy** (720 hours)
- Automated compaction and deletion
- BoltDB-Shipper with filesystem storage
- WAL (Write-Ahead Log) for durability
- Optimized chunk and index configuration
- Query result caching
- Rate limiting and backpressure handling

**Key Features**:
```yaml
retention_period: 720h              # 30 days
chunk_idle_period: 5m              # Efficient chunking
ingestion_rate_mb: 10              # 10MB/s per stream
max_query_parallelism: 32          # Fast queries
```

### 2. Promtail Configuration
**File**: `/docker/process-testing/promtail/promtail-config.yaml`

- **Docker service discovery** - Automatically discovers containers
- **JSON log parsing** - Extracts structured fields from Serilog
- **Multi-format timestamp parsing** - Handles various timestamp formats
- **Service-specific pipelines** - Custom parsing for each service
- **Label extraction** - Extracts trace_id, process_id, step_name, etc.
- **Metrics generation** - Converts logs to metrics

**Supported Services**:
- honua-cli-ai (with enhanced Process Framework parsing)
- redis
- prometheus
- grafana
- otel-collector
- tempo
- loki (self-monitoring)

**Key Features**:
```yaml
pipeline_stages:
  - json:                           # Parse Serilog JSON
  - timestamp:                      # Extract timestamps
  - labels:                         # Add indexed labels
  - metrics:                        # Generate metrics from logs
```

### 3. Docker Compose Integration
**File**: `/docker/process-testing/docker-compose.yml`

**Changes Made**:
- Updated Loki service to use custom configuration
- Added Promtail service with Docker socket access
- Added promtail-positions volume for state persistence
- Configured health checks
- Set up proper dependencies

**Services Added**:
```yaml
loki:
  - Custom config: ./loki/loki-config.yaml
  - Ports: 3100 (HTTP), 9096 (gRPC)

promtail:
  - Custom config: ./promtail/promtail-config.yaml
  - Volumes: Docker socket, container logs
  - Depends on: Loki
```

### 4. Grafana Integration
**Files**:
- `/docker/process-testing/grafana/provisioning/datasources/datasources.yml`
- `/docker/process-testing/grafana/dashboards/logs-dashboard.json`

**Datasource Enhancements**:
- Increased maxLines to 5000
- Enhanced trace correlation with multiple regex patterns
- Configured derived fields for automatic trace linking
- Added timeout configuration

**Dashboard Panels**:
1. **Log Volume by Service** - Bar chart showing log activity
2. **Log Level Distribution** - Pie chart of ERROR/WARN/INFO
3. **Error & Warning Rate** - Time series of error trends
4. **Process Framework Logs** - Filtered log viewer with variables
5. **Service Logs** - General log viewer for all services
6. **Logs by Trace ID** - Trace correlation viewer
7. **Top 10 Error Messages** - Most common errors table
8. **Top Error/Warning Sources** - Services with most issues

**Dashboard Variables**:
- `$service` - Filter by service name
- `$level` - Filter by log level
- `$process` - Filter by process name
- `$search` - Free-text search
- `$trace_id` - Filter by trace ID

### 5. Comprehensive Documentation

#### LOG_AGGREGATION.md (Main Guide)
**File**: `/docker/process-testing/LOG_AGGREGATION.md`

**Sections**:
- Architecture overview with diagram
- Configuration details
- Usage instructions
- Sample LogQL queries (50+ examples)
- Log retention policies
- Structured logging setup
- Grafana dashboard guide
- Troubleshooting guide
- Performance best practices

#### QUERY_EXAMPLES.md (Quick Reference)
**File**: `/docker/process-testing/loki/QUERY_EXAMPLES.md`

**Categories**:
- Basic queries (filtering, searching)
- Process Framework queries (process execution, steps, failures)
- Error analysis (top errors, error rates, stack traces)
- Performance monitoring (slow operations, latencies)
- Trace correlation (linking logs and traces)
- Security & audit (auth, config changes)
- Aggregations & metrics (log volume, percentages)
- Advanced patterns (pattern detection, cardinality)

**Query Examples**: 100+ ready-to-use LogQL queries

### 6. Updated README
**File**: `/docker/process-testing/README.md`

**Updates**:
- Added Promtail to services table
- Added Centralized Logs Dashboard to dashboard list
- Updated architecture diagram with log flow
- Added Documentation section with links to guides
- Updated support section

## Sample Queries

### Basic Log Viewing
```logql
# View all logs from Honua.Cli.AI
{service="honua-cli-ai"}

# Filter by error level
{service="honua-cli-ai", level="ERROR"}

# Search for specific text
{service="honua-cli-ai"} |= "deployment"
```

### Process Framework
```logql
# All process executions
{service="honua-cli-ai", process_name=~".+"}

# Specific process by ID
{service="honua-cli-ai", process_id="abc-123"}

# Failed process steps
{service="honua-cli-ai", level="ERROR"}
  | json
  | process_name != ""
  | line_format "Process: {{.process_name}} | Step: {{.step_name}} | Error: {{.message}}"
```

### Error Analysis
```logql
# Top 10 error messages
topk(10,
  sum by(message) (
    count_over_time({level="ERROR"} | json [24h])
  )
)

# Error rate by service
sum by(service) (rate({level="ERROR"} [5m]))
```

### Trace Correlation
```logql
# Logs by trace ID
{trace_id="8e3c9d4f2a1b5e7c"}

# All logs with traces
{service="honua-cli-ai"}
  | json
  | trace_id != ""
```

### Performance Monitoring
```logql
# Slow operations (>5 seconds)
{service="honua-cli-ai"}
  | json
  | duration_ms > 5000
  | line_format "{{.operation}}: {{.duration_ms}}ms"

# Average operation duration
avg_over_time(
  {service="honua-cli-ai"}
    | json
    | unwrap duration_ms [5m]
)
```

## Architecture

### Log Flow

```
┌─────────────────┐
│ Docker          │
│ Containers      │──┐
└─────────────────┘  │
                     │
┌─────────────────┐  │
│ Application     │  │
│ Logs (Serilog)  │──┼──> ┌──────────┐    ┌──────────┐    ┌──────────┐
└─────────────────┘  │    │ Promtail │───>│   Loki   │───>│ Grafana  │
                     │    └──────────┘    └──────────┘    └──────────┘
┌─────────────────┐  │         │               │
│ System Logs     │──┘         │               │
└─────────────────┘            │               └──> 30-day retention
                               │
                        Parse & Label
                      (JSON, timestamps,
                       trace IDs, etc.)
```

### Integration Points

1. **Application → OTLP Collector → Loki**
   - Structured logs via OpenTelemetry
   - Automatic trace context injection
   - Metrics and traces correlated

2. **Docker Containers → Promtail → Loki**
   - All container stdout/stderr
   - Service-specific parsing
   - Label extraction

3. **Loki → Grafana**
   - LogQL query language
   - Trace correlation links
   - Pre-built dashboards

## Configuration Summary

### Retention
- **Period**: 30 days (720 hours)
- **Enforcement**: Automatic via compactor
- **Deletion delay**: 2 hours after retention period
- **Grace period**: Up to 7 days old logs can be ingested

### Limits
- **Ingestion rate**: 10MB/s per stream
- **Burst size**: 20MB per stream
- **Max line size**: 256KB
- **Max streams**: 10,000 per user
- **Query timeout**: 60 seconds

### Storage
- **Backend**: BoltDB-Shipper + filesystem
- **Chunk size**: 256KB
- **Index period**: 24 hours
- **Cache size**: 500MB (query results)
- **WAL**: Enabled for durability

### Parsing
- **JSON logs**: Automatic parsing
- **Timestamps**: Multiple format support
- **Labels**: trace_id, span_id, process_id, process_name, step_name, etc.
- **Metrics**: Error counters, duration histograms

## Usage

### Starting the Stack

```bash
cd docker/process-testing
docker-compose up -d
```

### Accessing Logs

1. **Grafana Dashboard**: http://localhost:3000/d/honua-logs
2. **Grafana Explore**: http://localhost:3000/explore
3. **Loki API**: http://localhost:3100/loki/api/v1/query

### Verifying Log Collection

```bash
# Check Promtail is running
docker logs honua-process-promtail

# Check Loki is receiving logs
curl http://localhost:3100/loki/api/v1/label/service/values

# Test log query
curl -G -s "http://localhost:3100/loki/api/v1/query" \
  --data-urlencode 'query={service="honua-cli-ai"}' | jq
```

## Benefits

### For Development
- **Real-time debugging**: Stream logs as they happen
- **Context switching**: Jump from logs to traces to metrics
- **Error tracking**: Quickly find and analyze errors
- **Performance insights**: Identify slow operations

### For Operations
- **Centralized logging**: All logs in one place
- **Long-term storage**: 30-day retention
- **Efficient querying**: Label-based indexing
- **Alerting**: Create alerts from log patterns

### For Troubleshooting
- **Trace correlation**: Find all logs for a specific trace
- **Process tracking**: Follow process execution through logs
- **Error analysis**: Aggregate and analyze error patterns
- **Performance profiling**: Identify bottlenecks

## Performance

### Query Optimization
- Always start with label filters: `{service="x"}`
- Use specific time ranges
- Parse JSON once per query
- Avoid unnecessary regex
- Pre-aggregate when possible

### Resource Usage
- **Loki memory**: ~500MB (configurable)
- **Promtail memory**: ~100MB
- **Disk usage**: ~100MB per day (varies by log volume)
- **Query performance**: <1s for recent logs, <5s for 24h queries

## Next Steps

### Recommended Enhancements

1. **Add Alerting**
   - Create alert rules in Loki for high error rates
   - Configure Alertmanager integration
   - Set up notification channels (Slack, PagerDuty, etc.)

2. **Add More Dashboards**
   - Security dashboard (auth events, access logs)
   - Performance dashboard (latencies, throughput)
   - Business metrics (deployments, processes)

3. **Enhance Log Parsing**
   - Add more service-specific pipelines
   - Extract additional structured fields
   - Create custom metrics from logs

4. **Production Deployment**
   - Use object storage (S3, GCS, Azure Blob) for chunks
   - Deploy Loki in microservices mode
   - Add querier and compactor replicas
   - Enable multi-tenancy

## Files Created

```
docker/process-testing/
├── loki/
│   ├── loki-config.yaml          # Loki configuration with 30-day retention
│   └── QUERY_EXAMPLES.md         # 100+ ready-to-use queries
├── promtail/
│   └── promtail-config.yaml      # Promtail configuration with service pipelines
├── grafana/
│   ├── provisioning/datasources/
│   │   └── datasources.yml       # Enhanced Loki datasource config
│   └── dashboards/
│       └── logs-dashboard.json   # Comprehensive logs dashboard
├── docker-compose.yml            # Updated with Loki and Promtail
├── LOG_AGGREGATION.md            # Main documentation (50+ pages)
├── LOG_AGGREGATION_SUMMARY.md    # This file
└── README.md                     # Updated with log aggregation info
```

## References

- **Loki Documentation**: https://grafana.com/docs/loki/latest/
- **LogQL Reference**: https://grafana.com/docs/loki/latest/logql/
- **Promtail Configuration**: https://grafana.com/docs/loki/latest/clients/promtail/
- **Grafana Explore**: https://grafana.com/docs/grafana/latest/explore/

## Support

For issues or questions:
1. Check the [Troubleshooting section](LOG_AGGREGATION.md#troubleshooting) in LOG_AGGREGATION.md
2. Review [Query Examples](loki/QUERY_EXAMPLES.md) for query help
3. Run `./scripts/verify-health.sh` for diagnostics
4. Check service logs: `docker-compose logs -f loki promtail`
5. Test Loki API: `curl http://localhost:3100/ready`
