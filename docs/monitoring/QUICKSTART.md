# Honua Server Monitoring - Quick Start Guide

Get the monitoring stack up and running in 5 minutes.

## Prerequisites

- Docker and Docker Compose installed
- Port 5000 (app), 9090 (Prometheus), 3000 (Grafana) available
- 2GB free disk space

## Step 1: Start the Monitoring Stack

```bash
cd src/Honua.Server.Observability
docker-compose -f docker-compose.monitoring.yml up -d
```

**What's running**:
- Prometheus (metrics storage): http://localhost:9090
- Grafana (visualization): http://localhost:3000
- Alertmanager (alert routing): http://localhost:9093
- PostgreSQL Exporter: http://localhost:9187
- Node Exporter: http://localhost:9100

## Step 2: Enable Metrics in Your App

Add to your `Program.cs`:

```csharp
using Honua.Server.Observability;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Add observability
builder.Services.AddHonuaObservability(
    serviceName: "Honua.Server",
    serviceVersion: "1.0.0"
);

var app = builder.Build();

// Enable metrics endpoint
app.UsePrometheusMetrics();

app.Run();
```

Start your app on port 5000:

```bash
dotnet run --project src/Honua.Server.Host
```

## Step 3: Verify Setup

```bash
# Check metrics endpoint
curl http://localhost:5000/metrics | head -20

# Check Prometheus is scraping
curl http://localhost:9090/api/v1/targets
# Should show honua-server with status "up"
```

## Step 4: View Dashboards

**Open Grafana**: http://localhost:3000
- Username: `admin`
- Password: `admin`

**Navigate to Dashboards**:
1. Click "Dashboards" in left menu
2. Select "Honua - Overview"
3. View real-time metrics

**Available Dashboards**:
- **Honua - Overview**: System-wide health
- **Honua - Database Metrics**: Query performance
- **Honua - Cache Performance**: Cache efficiency
- **Honua - Error Rates & Health**: Error tracking
- **Honua - Response Times & Latency**: Performance percentiles

## Step 5: View Alerts

**Open Alertmanager**: http://localhost:9093

Currently configured alerts (no notification channels):
- Build queue depth
- Error rates
- Response times
- Cache hit rate
- Memory usage
- Database performance

To enable notifications:
1. Edit `alertmanager/alertmanager.yml`
2. Add Slack/PagerDuty webhook
3. Restart container: `docker-compose restart alertmanager`

## Step 6 (Optional): Enable Distributed Tracing

```bash
# Start Jaeger
docker-compose -f docker-compose.jaeger.yml up -d

# Configure app for OTLP tracing
export observability__tracing__exporter=otlp
export observability__tracing__otlpEndpoint=http://localhost:4317

# Restart your app
```

**View traces**: http://localhost:16686

## Example Queries

### Prometheus Queries

Test in Prometheus UI (http://localhost:9090):

```promql
# Request rate (per second)
rate(http_requests_total[5m])

# Error rate (%)
rate(http_requests_total{status_class="5xx"}[5m]) /
rate(http_requests_total[5m])

# p95 response time
histogram_quantile(0.95,
  sum(rate(http_request_duration_seconds_bucket[5m])) by (le)
)

# Cache hit rate (%)
rate(cache_lookups_total{result="hit"}[5m]) /
rate(cache_lookups_total[5m])
```

## Troubleshooting

### Metrics not appearing in Grafana?

1. Check metrics endpoint:
   ```bash
   curl http://localhost:5000/metrics | grep http_requests
   ```

2. Check Prometheus scraping:
   ```bash
   curl http://localhost:9090/api/v1/targets
   # Look for honua-server job with "UP" state
   ```

3. Check app is exposing metrics:
   ```csharp
   // In Program.cs, verify:
   app.UsePrometheusMetrics();  // Must be called
   ```

### Containers not starting?

```bash
# Check logs
docker-compose logs -f prometheus
docker-compose logs -f grafana

# Verify ports are free
lsof -i :9090  # Prometheus
lsof -i :3000  # Grafana
```

### High memory usage?

```bash
# Check container memory
docker stats

# Reduce retention time in prometheus.yml
# retention.time: 7d  # Instead of 15d

# Restart Prometheus
docker-compose restart prometheus
```

## Next Steps

1. **Configure Alerts**: Edit `alertmanager/alertmanager.yml`
2. **Create Custom Dashboard**: Use Grafana UI
3. **Setup Distributed Tracing**: Follow Step 6
4. **Production Deployment**: See [Monitoring Guide](./README.md)
5. **Alert Runbook**: See [Runbook](./RUNBOOK.md) for incident response

## Cleanup

```bash
# Stop monitoring stack
docker-compose -f docker-compose.monitoring.yml down

# Stop tracing stack
docker-compose -f docker-compose.jaeger.yml down

# Remove all data
docker volume prune
```

## Learn More

- [Complete Monitoring Guide](./README.md)
- [Incident Response Runbook](./RUNBOOK.md)
- [SLA/SLO Documentation](./SLA-SLO.md)
- [Distributed Tracing](../architecture/tracing.md)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/grafana/latest/)

---

**Time to setup**: ~5 minutes
**Maintenance**: Minimal (auto-cleanup after 15 days)
**Data storage**: ~1GB for 15 days of metrics
