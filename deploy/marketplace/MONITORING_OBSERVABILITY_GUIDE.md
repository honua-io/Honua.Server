# Monitoring and Observability Guide for Honua IO Cloud Deployments

This guide explains the comprehensive monitoring, observability, and alerting configured for Honua IO cloud marketplace deployments.

## Overview

All Honua IO cloud deployments include:

✅ **OpenTelemetry Integration** - Industry-standard observability framework
✅ **Cloud-Native Dashboards** - Pre-configured monitoring dashboards
✅ **Automated Alerting** - Real-time alerts for critical issues
✅ **Distributed Tracing** - Request tracing across services
✅ **Metrics Collection** - Application and infrastructure metrics
✅ **Log Aggregation** - Centralized logging with structured logs

---

## Architecture

```
┌──────────────────┐
│  Honua Server    │
│ (ASP.NET Core)   │
└────────┬─────────┘
         │
         │ OpenTelemetry SDK
         │ (Traces, Metrics, Logs)
         ├──────────────────┬────────────────────┬─────────────────┐
         │                  │                    │                 │
         ▼                  ▼                    ▼                 ▼
┌──────────────┐   ┌──────────────┐    ┌──────────────┐  ┌──────────────┐
│   AWS        │   │    Azure     │    │     GCP      │  │   Grafana    │
│ CloudWatch   │   │   Monitor    │    │ Cloud        │  │   Cloud      │
│              │   │              │    │  Monitoring  │  │  (Optional)  │
└──────────────┘   └──────────────┘    └──────────────┘  └──────────────┘
```

---

## OpenTelemetry Configuration

### Built-in Instrumentation

Honua IO uses OpenTelemetry .NET SDK with automatic instrumentation for:

| Component | Auto-Instrumented |
|-----------|-------------------|
| **HTTP Requests** | ✅ ASP.NET Core |
| **Database Queries** | ✅ Entity Framework Core, Npgsql |
| **Redis Operations** | ✅ StackExchange.Redis |
| **HTTP Client Calls** | ✅ HttpClient |
| **gRPC Calls** | ✅ Grpc.Net.Client |

### Environment Variables

Configure OpenTelemetry via environment variables:

```bash
# Enable/Disable OpenTelemetry
OTEL_SDK_DISABLED=false

# Service identification
OTEL_SERVICE_NAME=honua-server
OTEL_SERVICE_VERSION=1.0.0
OTEL_DEPLOYMENT_ENVIRONMENT=production

# Exporters (automatically configured based on cloud platform)
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318  # Override if using custom collector

# Sampling (1.0 = 100%, 0.1 = 10%)
OTEL_TRACES_SAMPLER=traceidratio
OTEL_TRACES_SAMPLER_ARG=1.0  # Adjust for high-traffic scenarios

# Resource attributes (auto-detected from cloud metadata)
OTEL_RESOURCE_ATTRIBUTES=deployment.environment=production,cloud.provider=aws
```

### Automatic Cloud Detection

Honua IO automatically detects the cloud environment and configures exporters:

```csharp
// Pseudo-code showing automatic configuration
if (IsRunningOnAWS)
{
    UseCloudWatchExporter();
}
else if (IsRunningOnAzure)
{
    UseAzureMonitorExporter();
}
else if (IsRunningOnGCP)
{
    UseGoogleCloudExporter();
}
```

### Custom Metrics

Add custom metrics to your Honua IO deployment:

```csharp
using OpenTelemetry.Metrics;

// Example custom metrics
var meter = new Meter("Honua.Server.Custom");

var requestCounter = meter.CreateCounter<long>(
    "honua.requests.total",
    description: "Total number of requests processed");

var processingDuration = meter.CreateHistogram<double>(
    "honua.processing.duration",
    unit: "ms",
    description: "Request processing duration");

// Use in your code
requestCounter.Add(1, new KeyValuePair<string, object>("endpoint", "/api/geospatial"));
processingDuration.Record(123.45, new KeyValuePair<string, object>("endpoint", "/api/geospatial"));
```

---

## AWS Monitoring

### CloudWatch Dashboard

**Automatically Included**: Yes (via cloudwatch-dashboard.yaml)

The CloudWatch dashboard includes:

| Panel | Metrics |
|-------|---------|
| **Application Performance** | Request count, latency, error rate |
| **Database** | CPU, connections, read/write latency, storage |
| **Redis Cache** | CPU, memory, evictions, connections, throughput |
| **Load Balancer** | Target response time, HTTP status codes |
| **Application Logs** | Error logs, structured log queries |

**Access Dashboard:**
```bash
# Get dashboard URL from CloudFormation output
aws cloudformation describe-stacks \
  --stack-name honua-server \
  --query 'Stacks[0].Outputs[?OutputKey==`DashboardURL`].OutputValue' \
  --output text
```

Or navigate to CloudWatch Console → Dashboards → `honua-server-monitoring`

### CloudWatch Alarms

**Pre-configured Alarms:**

| Alarm | Threshold | Action |
|-------|-----------|--------|
| **High Error Rate** | 5XX errors > 50/5min | Email + SNS |
| **High Latency** | Avg response time > 2s | Email + SNS |
| **Database High CPU** | RDS CPU > 80% | Email + SNS |
| **Database Connections** | Connections > 80% max | Email + SNS |
| **Redis High CPU** | Redis CPU > 75% | Email + SNS |
| **Redis Evictions** | Evictions > 1000/5min | Email + SNS |

**Configure Alert Email:**
```bash
# During CloudFormation deployment
Parameters:
  AlertEmail: ops@example.com
```

**Add Custom Alarms:**
```bash
# Example: Add alarm for custom metric
aws cloudwatch put-metric-alarm \
  --alarm-name honua-custom-metric \
  --metric-name honua.requests.total \
  --namespace HonuaIO \
  --statistic Sum \
  --period 300 \
  --threshold 10000 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 2 \
  --alarm-actions arn:aws:sns:us-east-1:123456789012:honua-alerts
```

### CloudWatch Logs Insights

**Useful Queries:**

**1. Error Rate by Endpoint**
```
fields @timestamp, @message
| filter @message like /ERROR/
| parse @message /endpoint=(?<endpoint>[^\s]+)/
| stats count() by endpoint
| sort count desc
```

**2. Slow Requests (> 1s)**
```
fields @timestamp, @message
| filter @message like /duration=/
| parse @message /duration=(?<duration>[0-9.]+)ms/
| filter duration > 1000
| sort duration desc
```

**3. Database Connection Pool Status**
```
fields @timestamp, @message
| filter @message like /database connection/
| parse @message /pool=(?<pool>[^\s]+) active=(?<active>[0-9]+) idle=(?<idle>[0-9]+)/
| stats latest(active) as ActiveConnections, latest(idle) as IdleConnections by pool
```

### X-Ray Distributed Tracing

Enable AWS X-Ray for distributed tracing:

```bash
# Add to CloudFormation/environment variables
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
AWS_XRAY_DAEMON_ADDRESS=xray-service.honua-system:2000
```

Traces will appear in AWS X-Ray console with:
- Request paths across services
- Service map visualization
- Latency breakdown by component

---

## Azure Monitoring

### Azure Monitor Workbooks

**Automatically Included**: Yes (via azure-monitor-workbook.json)

The Azure Monitor workbook includes:

| Tab | Visualizations |
|-----|----------------|
| **Overview** | Request rate, error rate, latency, active users |
| **Performance** | Response times, database queries, cache hits |
| **Failures** | Error logs, exception counts, failed dependencies |
| **Infrastructure** | CPU, memory, disk, network for all resources |
| **Dependencies** | Database, Redis, external API health |

**Access Workbook:**
```bash
# Navigate to Azure Portal
# Monitoring → Workbooks → "honua-server-monitoring"
```

Or use Azure CLI:
```bash
az monitor workbook show \
  --resource-group honua-rg \
  --name honua-server-monitoring
```

### Azure Monitor Alerts

**Pre-configured Alert Rules:**

| Alert Rule | Condition | Severity |
|------------|-----------|----------|
| **High Error Rate** | 5XX rate > 5% | Medium (2) |
| **High Latency** | Avg latency > 2s | Medium (2) |
| **PostgreSQL High CPU** | CPU > 80% | Medium (2) |
| **PostgreSQL High Connections** | Connections > 80% | Medium (2) |
| **Redis High CPU** | Server Load > 75% | Medium (2) |
| **Redis High Memory** | Memory > 90% | High (1) |

**Alert Action Groups:**
- Email notifications
- SMS (optional - configure in Action Group)
- Webhook (optional - for PagerDuty, Slack, etc.)

**Add Webhook to Action Group:**
```bash
az monitor action-group update \
  --name honua-server-alerts \
  --resource-group honua-rg \
  --add-receiver webhook alert-webhook https://hooks.slack.com/services/YOUR/WEBHOOK/URL
```

### Application Insights Integration

For deeper application insights, add Application Insights:

```bash
# Create Application Insights instance
az monitor app-insights component create \
  --app honua-server-insights \
  --location eastus \
  --resource-group honua-rg \
  --workspace /subscriptions/.../resourceGroups/honua-rg/providers/Microsoft.OperationalInsights/workspaces/honua-workspace

# Get instrumentation key
az monitor app-insights component show \
  --app honua-server-insights \
  --resource-group honua-rg \
  --query instrumentationKey
```

Add to Container App environment:
```bash
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=<key>;IngestionEndpoint=https://...
```

Benefits:
- **Live Metrics**: Real-time performance monitoring
- **Application Map**: Automatic service dependency mapping
- **Smart Detection**: AI-powered anomaly detection
- **Profiling**: CPU and memory profiling

### Log Analytics Queries

**Useful Kusto Queries:**

**1. Top 10 Slowest Requests**
```kusto
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "honua-server"
| where Log_s contains "request completed"
| extend Duration = extract("duration=([0-9.]+)ms", 1, Log_s)
| extend DurationMs = todouble(Duration)
| top 10 by DurationMs desc
| project TimeGenerated, Log_s, DurationMs
```

**2. Error Distribution by Type**
```kusto
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "honua-server"
| where Log_s contains "ERROR" or Log_s contains "Exception"
| extend ErrorType = extract("(\\w+Exception)", 1, Log_s)
| summarize Count = count() by ErrorType
| render piechart
```

**3. Request Rate Over Time**
```kusto
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "honua-server"
| where Log_s contains "HTTP"
| summarize Requests = count() by bin(TimeGenerated, 1m)
| render timechart
```

---

## GCP Monitoring

### Cloud Monitoring Dashboards

For GCP deployments (GKE or Cloud Run), create a dashboard:

```bash
# Using gcloud CLI
gcloud monitoring dashboards create --config-from-file=gcp-dashboard.json
```

**gcp-dashboard.json** includes:
- Cloud Run request metrics
- Cloud SQL database metrics
- Memorystore Redis metrics
- Error logs widget
- Latency distribution

### Cloud Logging

**Structured Logs:**
Honua IO outputs JSON-structured logs compatible with Cloud Logging:

```json
{
  "timestamp": "2025-01-15T10:30:00Z",
  "severity": "INFO",
  "message": "Request completed",
  "http.method": "POST",
  "http.route": "/api/geospatial",
  "http.status_code": 200,
  "duration_ms": 123.45,
  "trace_id": "abc123...",
  "span_id": "def456..."
}
```

**Log Queries:**

**1. Errors in Last Hour**
```
resource.type="cloud_run_revision"
severity>=ERROR
timestamp>="2025-01-15T09:00:00Z"
```

**2. Slow Requests**
```
resource.type="cloud_run_revision"
jsonPayload.duration_ms > 1000
```

### Cloud Trace

Distributed tracing is automatically enabled for:
- Cloud Run services
- Cloud SQL database queries
- External HTTP calls

View in Cloud Console → Trace → Trace List

### Alerting Policies

**Create Alert Policies:**

```bash
# High error rate alert
gcloud alpha monitoring policies create \
  --notification-channels=CHANNEL_ID \
  --display-name="Honua High Error Rate" \
  --condition-display-name="Error rate > 5%" \
  --condition-threshold-value=5 \
  --condition-threshold-duration=300s \
  --condition-filter='resource.type="cloud_run_revision" AND metric.type="run.googleapis.com/request_count" AND metric.label.response_code_class="5xx"'
```

**Available Metrics:**
- `run.googleapis.com/request_count`
- `run.googleapis.com/request_latencies`
- `cloudsql.googleapis.com/database/cpu/utilization`
- `redis.googleapis.com/stats/cpu_utilization`

---

## Grafana Cloud Integration (Optional)

For unified observability across all cloud platforms, use Grafana Cloud:

### Setup

1. **Create Grafana Cloud account**: https://grafana.com/

2. **Get credentials**:
   - Prometheus Remote Write Endpoint
   - Loki Push Endpoint
   - Tempo Endpoint
   - API Key

3. **Configure OpenTelemetry Collector**:

```yaml
# otel-collector-config.yaml
receivers:
  otlp:
    protocols:
      grpc:
      http:

exporters:
  prometheusremotewrite:
    endpoint: https://prometheus-prod-XX-prod-us-central-0.grafana.net/api/prom/push
    headers:
      authorization: Basic <base64-encoded-credentials>

  loki:
    endpoint: https://logs-prod-XX.grafana.net/loki/api/v1/push
    headers:
      authorization: Basic <base64-encoded-credentials>

  otlp/tempo:
    endpoint: tempo-prod-XX-prod-us-central-0.grafana.net:443
    headers:
      authorization: Basic <base64-encoded-credentials>

service:
  pipelines:
    metrics:
      receivers: [otlp]
      exporters: [prometheusremotewrite]
    logs:
      receivers: [otlp]
      exporters: [loki]
    traces:
      receivers: [otlp]
      exporters: [otlp/tempo]
```

4. **Deploy OpenTelemetry Collector** (Kubernetes):

```bash
helm repo add open-telemetry https://open-telemetry.github.io/opentelemetry-helm-charts
helm install opentelemetry-collector open-telemetry/opentelemetry-collector \
  --set mode=deployment \
  --set config="$(cat otel-collector-config.yaml)"
```

5. **Update Honua Server environment**:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://opentelemetry-collector:4318
```

### Benefits

- **Unified Dashboard**: Single pane of glass across AWS, Azure, GCP
- **Advanced Querying**: PromQL, LogQL for powerful analytics
- **Long-term Retention**: Metrics and logs retained beyond cloud defaults
- **Alerting**: Unified alerting across all environments
- **Cost Optimization**: Potentially lower cost than cloud-native solutions

---

## Best Practices

### 1. Set Appropriate Sampling Rates

**Production (high traffic)**:
```bash
OTEL_TRACES_SAMPLER=traceidratio
OTEL_TRACES_SAMPLER_ARG=0.1  # 10% sampling
```

**Staging/Development**:
```bash
OTEL_TRACES_SAMPLER_ARG=1.0  # 100% sampling
```

### 2. Add Semantic Attributes

Enrich traces with business context:

```csharp
using var activity = Activity.Current;
activity?.SetTag("user.id", userId);
activity?.SetTag("tenant.id", tenantId);
activity?.SetTag("feature.name", "geospatial-analysis");
activity?.SetTag("data.size_mb", dataSizeMb);
```

### 3. Monitor Alert Fatigue

Review alerts monthly:
- Adjust thresholds if too noisy
- Disable alerts that don't require action
- Create "warning" vs "critical" severity levels

### 4. Use Log Levels Appropriately

| Level | Use Case | Example |
|-------|----------|---------|
| **Trace** | Detailed debugging | Variable values, loop iterations |
| **Debug** | Developer diagnostics | Method entry/exit, configuration |
| **Information** | General flow | Request started, processing completed |
| **Warning** | Recoverable issues | Retry after failure, deprecated API use |
| **Error** | Errors requiring attention | Exceptions, failed operations |
| **Critical** | System failures | Database unavailable, service crash |

### 5. Dashboard Maintenance

- Review dashboards quarterly
- Remove unused panels
- Add panels for new features
- Share dashboards with team

---

## Troubleshooting

### OpenTelemetry Not Exporting

**Check**:
```bash
# Kubernetes
kubectl logs -n honua-system deployment/honua-server | grep -i "opentelemetry\|exporter"

# ECS Fargate
aws logs tail /ecs/honua-server --follow | grep -i "opentelemetry"

# Azure Container Apps
az containerapp logs show --name honua-server --resource-group honua-rg | grep -i "opentelemetry"
```

**Common Issues**:
1. Endpoint not reachable: Verify network connectivity
2. Authentication failed: Check API keys/credentials
3. Rate limiting: Reduce sampling rate or batch size

### High Cardinality Metrics

**Symptom**: Metrics storage costs increasing rapidly

**Solution**: Reduce cardinality
```csharp
// Bad - unbounded cardinality
meter.CreateCounter("requests").Add(1, new("user_id", userId));  // Millions of unique values

// Good - bounded cardinality
meter.CreateCounter("requests").Add(1, new("user_tier", userTier));  // Few unique values
```

### Missing Traces

**Check Sampling**:
```bash
# Ensure sampling is not too aggressive
echo $OTEL_TRACES_SAMPLER_ARG  # Should be > 0
```

**Check Propagation**:
Verify trace context headers are propagated:
- `traceparent`
- `tracestate`

---

## Cost Optimization

### AWS

- Use CloudWatch Logs Insights instead of exporting to S3 when possible
- Set appropriate log retention (7-30 days for dev, 90+ days for prod)
- Use X-Ray sampling to reduce trace ingestion costs

### Azure

- Use Log Analytics query alerts instead of metric alerts where possible (cheaper)
- Archive old logs to Storage Account (cheaper long-term storage)
- Use Basic tier for Application Insights in non-prod environments

### GCP

- Use Cloud Logging exclusion filters to drop noisy logs
- Set log retention policies (default is 30 days)
- Use Cloud Trace sampling (default 0.1 QPS is usually sufficient)

---

## Support

For monitoring issues:

1. **Check Cloud Console**: Verify metrics are being received
2. **Review Application Logs**: Look for OpenTelemetry export errors
3. **Test Connectivity**: Ensure firewall rules allow outbound connections
4. **Contact Support**: support@honua.io with:
   - Cloud platform (AWS/Azure/GCP)
   - Deployment type (EKS/ECS/Container Apps/Cloud Run)
   - Monitoring tool (CloudWatch/Azure Monitor/Cloud Monitoring/Grafana)
   - Error messages or symptoms

---

## Additional Resources

- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [AWS CloudWatch Best Practices](https://docs.aws.amazon.com/AmazonCloudWatch/latest/monitoring/Best_Practice_Recommended_Alarms_AWS_Services.html)
- [Azure Monitor Best Practices](https://learn.microsoft.com/en-us/azure/azure-monitor/best-practices)
- [GCP Cloud Monitoring Best Practices](https://cloud.google.com/monitoring/best-practices)
- [Grafana Cloud Documentation](https://grafana.com/docs/grafana-cloud/)
