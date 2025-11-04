# Quick Start: Centralized Logs

Get started with log aggregation in 5 minutes.

## Prerequisites

- Docker and Docker Compose installed
- Honua Process Framework testing stack
- 2GB+ available RAM

## Start the Stack

```bash
cd /home/mike/projects/HonuaIO/docker/process-testing

# Start all services
docker-compose up -d

# Wait for services to be ready (30-60 seconds)
./scripts/verify-health.sh
```

## Access the Logs Dashboard

Open your browser to:

**http://localhost:3000/d/honua-logs**

Default credentials:
- Username: `admin`
- Password: `admin`

## View Logs

### Option 1: Dashboard (Recommended)

The pre-built dashboard shows:
- Log volume by service
- Error rates and distributions
- Process framework logs
- Trace-correlated logs

### Option 2: Explore View

1. Go to http://localhost:3000/explore
2. Select "Loki" datasource
3. Enter a query:
   ```logql
   {service="honua-cli-ai"}
   ```
4. Click "Run query"

## Common Queries

### All logs from a service
```logql
{service="honua-cli-ai"}
```

### Only errors
```logql
{service="honua-cli-ai", level="ERROR"}
```

### Search for text
```logql
{service="honua-cli-ai"} |= "deployment"
```

### Process framework logs
```logql
{service="honua-cli-ai", process_name=~".+"}
```

### Logs by trace ID
```logql
{trace_id="your-trace-id-here"}
```

## Test Log Generation

Run a process to generate logs:

```bash
# Set environment for testing
export ASPNETCORE_ENVIRONMENT=Testing

# Run Honua.Cli.AI
cd /home/mike/projects/HonuaIO
dotnet run --project src/Honua.Cli.AI

# Logs will appear in Grafana within seconds
```

## Verify It's Working

### Check Promtail is collecting logs
```bash
docker logs honua-process-promtail | tail -20
```

### Check Loki is receiving logs
```bash
curl http://localhost:3100/loki/api/v1/label/service/values
```

Should return a list of services like:
```json
{
  "status": "success",
  "data": ["honua-cli-ai", "redis", "prometheus", "grafana", ...]
}
```

### Query logs via API
```bash
curl -G -s "http://localhost:3100/loki/api/v1/query" \
  --data-urlencode 'query={service="honua-cli-ai"}' \
  --data-urlencode 'limit=5' | jq
```

## Dashboard Features

### Variables
Use the dropdown filters at the top:
- **service**: Select which service(s) to view
- **level**: Filter by log level (ERROR, WARN, INFO)
- **process**: Filter by process name
- **search**: Free-text search across all logs
- **trace_id**: Filter by distributed trace ID

### Time Range
Use the time picker in the top-right:
- Last 5 minutes
- Last 15 minutes
- Last 1 hour
- Last 24 hours
- Custom range

### Panels

1. **Log Volume by Service** - Shows which services are logging the most
2. **Log Level Distribution** - Pie chart of ERROR/WARN/INFO
3. **Error & Warning Rate** - Trend of errors over time
4. **Process Framework Logs** - Filtered logs with process context
5. **Service Logs** - All service logs
6. **Logs by Trace ID** - Correlated logs for a trace
7. **Top 10 Error Messages** - Most common errors
8. **Top Error/Warning Sources** - Which services have issues

## Tips

### Efficient Querying
- Always start with label filters: `{service="x"}`
- Use specific time ranges
- Limit results with `| limit 100`

### Finding Errors Quickly
```logql
{level="ERROR"} | json | line_format "{{.service}}: {{.message}}"
```

### Following a Process Execution
```logql
{process_id="your-process-id"} | json | line_format "{{.step_name}}: {{.message}}"
```

### Linking Logs and Traces
1. Click on a trace ID in the log line
2. Grafana will automatically jump to that trace in Tempo
3. View all logs for that trace

## Troubleshooting

### No logs appearing

1. **Check Promtail**:
   ```bash
   docker logs honua-process-promtail
   ```

2. **Check Loki**:
   ```bash
   docker logs honua-process-loki
   ```

3. **Restart services**:
   ```bash
   docker-compose restart promtail loki
   ```

### Logs not parsing

Check the format of your logs:
```bash
docker logs honua-cli-ai | head -5
```

Logs should be JSON formatted like:
```json
{
  "Timestamp": "2024-10-18T10:30:00.123Z",
  "Level": "Information",
  "Message": "Process started",
  "Properties": {
    "ProcessId": "abc-123",
    "ProcessName": "DeploymentProcess"
  }
}
```

### Dashboard not loading

1. **Refresh Grafana**:
   ```bash
   docker-compose restart grafana
   ```

2. **Manually import**:
   - Go to http://localhost:3000
   - Dashboards → Import
   - Upload `grafana/dashboards/logs-dashboard.json`

## Next Steps

### Learn More
- Read the [Log Aggregation Guide](LOG_AGGREGATION.md) for comprehensive documentation
- Browse [Query Examples](loki/QUERY_EXAMPLES.md) for 100+ ready-to-use queries
- Explore the [Process Framework Testing Stack README](README.md)

### Create Custom Queries
1. Go to http://localhost:3000/explore
2. Select "Loki" datasource
3. Experiment with LogQL
4. Save useful queries to dashboards

### Set Up Alerts
1. Create alert rules in Loki for high error rates
2. Configure notification channels
3. Get alerted when issues occur

### Optimize Performance
- Use specific label filters
- Query recent logs first
- Use dashboard variables for dynamic filtering
- Pre-aggregate common queries

## Configuration Files

All configuration files are in:
```
/home/mike/projects/HonuaIO/docker/process-testing/
├── loki/loki-config.yaml          # Loki configuration
├── promtail/promtail-config.yaml  # Promtail configuration
└── docker-compose.yml             # Service definitions
```

## Log Retention

Logs are automatically retained for **30 days**.

To change retention, edit `loki/loki-config.yaml`:
```yaml
limits_config:
  retention_period: 1440h  # 60 days
```

Then restart:
```bash
docker-compose restart loki
```

## Performance

### Current Settings
- **Ingestion rate**: 10MB/s per stream
- **Max line size**: 256KB
- **Query timeout**: 60 seconds
- **Chunk size**: 256KB
- **Cache size**: 500MB

### Resource Usage
- **Loki**: ~500MB RAM, ~100MB/day disk
- **Promtail**: ~100MB RAM
- **Query time**: <1s for recent logs

## API Access

### Query logs
```bash
curl -G "http://localhost:3100/loki/api/v1/query" \
  --data-urlencode 'query={service="honua-cli-ai"}' \
  --data-urlencode 'limit=10'
```

### Query range
```bash
curl -G "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service="honua-cli-ai"}' \
  --data-urlencode 'start=2024-01-01T00:00:00Z' \
  --data-urlencode 'end=2024-01-01T01:00:00Z' \
  --data-urlencode 'limit=100'
```

### Get labels
```bash
curl "http://localhost:3100/loki/api/v1/labels"
curl "http://localhost:3100/loki/api/v1/label/service/values"
```

## Support

For help:
1. Check [Troubleshooting section](LOG_AGGREGATION.md#troubleshooting)
2. Run `./scripts/verify-health.sh`
3. Check logs: `docker-compose logs -f loki promtail`

## Summary

You now have:
- ✅ Centralized log aggregation
- ✅ 30-day log retention
- ✅ Grafana dashboard for visualization
- ✅ Structured log parsing
- ✅ Trace correlation
- ✅ Powerful LogQL queries

Start exploring your logs at **http://localhost:3000/d/honua-logs**!
