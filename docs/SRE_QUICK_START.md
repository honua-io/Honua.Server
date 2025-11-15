# SRE Quick Start Guide

## 5-Minute Setup

### 1. Enable SRE Features

```bash
SRE__ENABLED=true
SRE__ROLLINGWINDOWDAYS=28
SRE__EVALUATIONINTERVALMINUTES=5
```

### 2. Define Your First SLO

**Example: 99% of requests should complete in under 500ms**

```bash
SRE__SLOS__API_LATENCY__ENABLED=true
SRE__SLOS__API_LATENCY__TYPE=Latency
SRE__SLOS__API_LATENCY__TARGET=0.99
SRE__SLOS__API_LATENCY__THRESHOLDMS=500
SRE__SLOS__API_LATENCY__DESCRIPTION="99% of API requests under 500ms"
```

### 3. Register Services (in Program.cs or Startup)

```csharp
// Add SRE services
builder.Services.AddSreServices();
builder.Services.Configure<SreOptions>(builder.Configuration.GetSection("SRE"));

// Add middleware (in Configure/app setup)
app.UseMiddleware<SliIntegrationMiddleware>();

// Add admin endpoints
app.MapAdminSreEndpoints();
```

### 4. Check Status

```bash
curl -H "Authorization: Bearer YOUR_TOKEN" \
  http://localhost:5000/admin/sre/slos
```

## Common SLO Templates

### High Availability Service (99.9%)

```bash
SRE__SLOS__HIGH_AVAILABILITY__ENABLED=true
SRE__SLOS__HIGH_AVAILABILITY__TYPE=Availability
SRE__SLOS__HIGH_AVAILABILITY__TARGET=0.999
SRE__SLOS__HIGH_AVAILABILITY__DESCRIPTION="99.9% availability (43 min downtime/month)"
```

### Fast API (95th percentile < 1s)

```bash
SRE__SLOS__FAST_API__ENABLED=true
SRE__SLOS__FAST_API__TYPE=Latency
SRE__SLOS__FAST_API__TARGET=0.95
SRE__SLOS__FAST_API__THRESHOLDMS=1000
SRE__SLOS__FAST_API__DESCRIPTION="95% of requests under 1 second"
```

### Ultra-Low Error Rate (99.95%)

```bash
SRE__SLOS__LOW_ERROR__ENABLED=true
SRE__SLOS__LOW_ERROR__TYPE=ErrorRate
SRE__SLOS__LOW_ERROR__TARGET=0.9995
SRE__SLOS__LOW_ERROR__DESCRIPTION="99.95% of requests error-free"
```

## Quick Reference

### SLO Targets

| Target | Allowed Downtime (30 days) | Allowed Errors (per 10,000) |
|--------|---------------------------|----------------------------|
| 90% | 3 days | 1,000 |
| 95% | 1.5 days | 500 |
| 99% | 7.2 hours | 100 |
| 99.5% | 3.6 hours | 50 |
| 99.9% | 43 minutes | 10 |
| 99.95% | 21 minutes | 5 |
| 99.99% | 4.3 minutes | 1 |

### Error Budget Status

| Status | Action |
|--------|--------|
| Healthy (>25%) | Deploy normally |
| Warning (10-25%) | Reduce deployment velocity |
| Critical (0-10%) | Only critical fixes |
| Exhausted (<0%) | Halt all deployments |

### API Endpoints

```bash
# List all SLOs
GET /admin/sre/slos

# Get SLO details
GET /admin/sre/slos/{sloName}

# Get error budgets
GET /admin/sre/error-budgets

# Get deployment policy
GET /admin/sre/deployment-policy

# Get configuration
GET /admin/sre/config
```

### Prometheus Metrics

```promql
# SLO compliance rate
honua.slo.compliance{slo.name="api_latency"}

# Error budget remaining
honua.slo.error_budget.remaining{slo.name="api_latency"}

# SLI event counts
honua.sli.good_events.total{slo.name="api_latency"}
honua.sli.bad_events.total{slo.name="api_latency"}
```

## Deployment Policy Integration

### Bash Script

```bash
#!/bin/bash
POLICY=$(curl -s http://localhost:5000/admin/sre/deployment-policy)
CAN_DEPLOY=$(echo $POLICY | jq -r '.canDeploy')

if [ "$CAN_DEPLOY" == "false" ]; then
  echo "❌ Deployment blocked"
  exit 1
fi

echo "✅ Deployment approved"
```

### GitHub Actions

```yaml
- name: Check SLO
  run: |
    POLICY=$(curl -s http://api/admin/sre/deployment-policy)
    CAN_DEPLOY=$(echo $POLICY | jq -r '.canDeploy')
    if [ "$CAN_DEPLOY" == "false" ]; then
      exit 1
    fi
```

## Troubleshooting

### No Data Showing

1. Is SRE enabled? `SRE__ENABLED=true`
2. Is SLO enabled? `SRE__SLOS__{NAME}__ENABLED=true`
3. Is middleware registered? Check `UseMiddleware<SliIntegrationMiddleware>()`
4. Is traffic flowing? Check request counts

### Metrics Not in Prometheus

1. Check `/metrics` endpoint
2. Look for `honua.slo.*` and `honua.sli.*` metrics
3. Verify OpenTelemetry exporter configuration

### Error Budget Not Updating

1. Check SloEvaluator is running (background service)
2. Verify evaluation interval setting
3. Review application logs for errors

## Next Steps

1. Read full documentation: `SRE_SLO_TRACKING.md`
2. Set up Prometheus alerts
3. Create Grafana dashboards
4. Integrate with CI/CD pipeline
5. Train team on SLO concepts

## Support

- Full Documentation: `docs/SRE_SLO_TRACKING.md`
- API Docs: `/swagger`
- Metrics: `/metrics`
