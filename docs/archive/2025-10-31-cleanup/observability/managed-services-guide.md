# Honua Observability with Managed Services

This guide explains how to use managed observability services across Azure, AWS, GCP, and Kubernetes platforms instead of self-hosted Prometheus/Grafana.

## Overview

The observability stack can be deployed using cloud-native managed services, eliminating the need to run and maintain Prometheus, Grafana, and Alertmanager yourself.

**Benefits of Managed Services:**
- No infrastructure to maintain
- Automatic scaling and high availability
- Built-in security and compliance
- Pay-as-you-go pricing
- Native integration with cloud platforms

## Azure Monitor & Application Insights

### Architecture
```
Honua Server → Application Insights → Azure Monitor Logs (Log Analytics)
                                    ↓
                              Azure Dashboards & Alerts
```

### Setup

#### 1. Install Application Insights SDK

Already included in Honua! Just configure the connection string:

**appsettings.json:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=xxx;IngestionEndpoint=https://xxx.applicationinsights.azure.com/"
  },
  "Logging": {
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft": "Warning"
      }
    }
  }
}
```

#### 2. Enable in Program.cs

```csharp
// Already configured in Honua! Just ensure it's enabled:
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnablePerformanceCounterCollectionModule = true;
});
```

#### 3. Configure Custom Metrics

Application Insights automatically collects:
- HTTP request metrics (rate, duration, status codes)
- Dependency calls (database, HTTP)
- Exceptions and error traces
- Performance counters (CPU, memory, GC)

**Custom metrics are auto-exported from our OpenTelemetry implementation!**

#### 4. Create Azure Dashboards

**Using Azure Portal:**
1. Navigate to Application Insights resource
2. Click **Workbooks** → **New**
3. Import our queries:

```kusto
// Request rate by protocol
requests
| where customDimensions.api_protocol != ""
| summarize RequestCount = count() by bin(timestamp, 5m), tostring(customDimensions.api_protocol)
| render timechart

// P95 Latency
requests
| summarize percentile(duration, 95) by bin(timestamp, 5m)
| render timechart

// Error rate
requests
| summarize
    TotalRequests = count(),
    Errors = countif(success == false)
| extend ErrorRate = (Errors * 100.0) / TotalRequests

// Cache hit rate (from custom metrics)
customMetrics
| where name in ("honua_raster_cache_hits", "honua_raster_cache_misses")
| summarize
    Hits = sumif(value, name == "honua_raster_cache_hits"),
    Misses = sumif(value, name == "honua_raster_cache_misses")
    by bin(timestamp, 5m)
| extend HitRate = (Hits * 100.0) / (Hits + Misses)
| render timechart
```

#### 5. Configure Alerts

**Azure Monitor Alerts:**
```bash
# High error rate alert
az monitor metrics alert create \
  --name "Honua-HighErrorRate" \
  --resource-group honua-rg \
  --scopes /subscriptions/.../resourceGroups/honua-rg/providers/microsoft.insights/components/honua-ai \
  --condition "avg requests/failed > 5" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --severity 0 \
  --action /subscriptions/.../actionGroups/honua-oncall

# High latency alert (P95 > 2000ms)
az monitor metrics alert create \
  --name "Honua-HighLatency" \
  --resource-group honua-rg \
  --scopes /subscriptions/.../resourceGroups/honua-rg/providers/microsoft.insights/components/honua-ai \
  --condition "avg requests/duration > 2000" \
  --aggregation percentile \
  --window-size 10m \
  --evaluation-frequency 5m \
  --severity 1
```

### Cost Estimation
- **Basic**: $2.30/GB ingested (first 5 GB/month free)
- **Typical Honua deployment**: ~10-20 GB/month = **$10-40/month**
- **Retention**: 90 days included, extended retention extra cost

### AI Consultant Command

```bash
honua ai "Setup Azure Application Insights monitoring"
```

The AI consultant knows how to:
- Generate Application Insights configuration
- Create Kusto queries for dashboards
- Set up Azure Monitor alerts
- Configure log sampling and retention

---

## AWS CloudWatch & X-Ray

### Architecture
```
Honua Server → CloudWatch Metrics → CloudWatch Logs
             ↓
        X-Ray Traces
             ↓
     CloudWatch Dashboards & Alarms
```

### Setup

#### 1. Install AWS SDK

Add to your project:
```bash
dotnet add package AWS.Logger.AspNetCore
dotnet add package AWSSDK.CloudWatch
dotnet add package AWSXRayRecorder.Handlers.AspNetCore
```

#### 2. Configure CloudWatch Logging

**appsettings.json:**
```json
{
  "AWS": {
    "Region": "us-east-1"
  },
  "AWS.Logging": {
    "LogGroup": "/aws/honua/server",
    "Region": "us-east-1",
    "LogStreamNamePrefix": "honua-",
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

**Program.cs:**
```csharp
builder.Logging.AddAWSProvider(builder.Configuration.GetAWSLoggingConfigSection());
```

#### 3. Push Custom Metrics to CloudWatch

```csharp
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

// Honua already exports OpenTelemetry metrics - use EMF (Embedded Metric Format)
// to publish directly to CloudWatch from logs:

logger.LogInformation(
    "_aws_cloudwatch_metric_{MetricName}={MetricValue} {Dimensions}",
    "HonuaApiRequests",
    requestCount,
    new { protocol = "wfs", status = 200 });
```

Or use AWS Distro for OpenTelemetry (ADOT):
```yaml
# ADOT Collector configuration
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317

processors:
  batch:

exporters:
  awsxray:
  awsemf:
    namespace: Honua/API
    log_group_name: '/aws/honua/metrics'
    region: us-east-1

service:
  pipelines:
    metrics:
      receivers: [otlp]
      processors: [batch]
      exporters: [awsemf]
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [awsxray]
```

#### 4. Create CloudWatch Dashboards

```bash
# Using AWS CLI to create dashboard
aws cloudwatch put-dashboard --dashboard-name Honua-Overview --dashboard-body file://dashboard.json
```

**dashboard.json:**
```json
{
  "widgets": [
    {
      "type": "metric",
      "properties": {
        "metrics": [
          [ "Honua/API", "RequestRate", { "stat": "Sum", "period": 300 } ],
          [ ".", "ErrorRate", { "stat": "Average" } ]
        ],
        "period": 300,
        "stat": "Average",
        "region": "us-east-1",
        "title": "API Performance"
      }
    },
    {
      "type": "log",
      "properties": {
        "query": "SOURCE '/aws/honua/server' | fields @timestamp, @message | filter @message like /ERROR/ | stats count() by bin(5m)",
        "region": "us-east-1",
        "title": "Error Count"
      }
    }
  ]
}
```

#### 5. Configure CloudWatch Alarms

```bash
# High error rate alarm
aws cloudwatch put-metric-alarm \
  --alarm-name honua-high-error-rate \
  --alarm-description "Alert when error rate exceeds 5%" \
  --metric-name ErrorRate \
  --namespace Honua/API \
  --statistic Average \
  --period 300 \
  --threshold 5.0 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 2 \
  --alarm-actions arn:aws:sns:us-east-1:123456789:honua-alerts

# High latency alarm
aws cloudwatch put-metric-alarm \
  --alarm-name honua-high-latency \
  --metric-name Latency \
  --namespace Honua/API \
  --statistic p95 \
  --period 600 \
  --threshold 2000 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 1
```

### Cost Estimation
- **Metrics**: $0.30 per custom metric/month (first 10 metrics free)
- **Logs**: $0.50/GB ingested + $0.03/GB storage
- **Typical Honua deployment**: **$20-60/month**

### AI Consultant Command

```bash
honua ai "Setup AWS CloudWatch monitoring with ADOT"
```

---

## Google Cloud Operations (formerly Stackdriver)

### Architecture
```
Honua Server → Cloud Monitoring → Cloud Logging
             ↓
     Cloud Trace (APM)
             ↓
  Cloud Monitoring Dashboards & Alerts
```

### Setup

#### 1. Install Google Cloud SDK

```bash
dotnet add package Google.Cloud.Diagnostics.AspNetCore3
```

#### 2. Configure Cloud Logging

**Program.cs:**
```csharp
builder.Services.AddGoogleDiagnosticsForAspNetCore(
    projectId: "your-gcp-project-id",
    serviceName: "honua-server",
    serviceVersion: "1.0.0");

builder.Logging.AddGoogle(new LoggerOptions
{
    ProjectId = "your-gcp-project-id",
    ServiceName = "honua-server",
    Labels = new Dictionary<string, string>
    {
        { "environment", "production" },
        { "service", "honua" }
    }
});
```

#### 3. Export Metrics to Cloud Monitoring

Use **Google Cloud OpenTelemetry exporter**:

```yaml
# OpenTelemetry Collector config
exporters:
  googlecloud:
    project: your-gcp-project-id
    metric:
      prefix: custom.googleapis.com/honua

service:
  pipelines:
    metrics:
      receivers: [otlp]
      processors: [batch]
      exporters: [googlecloud]
```

Or use Cloud Monitoring API directly:

```csharp
using Google.Cloud.Monitoring.V3;

var client = MetricServiceClient.Create();
var projectName = ProjectName.FromProject("your-project-id");

var timeSeries = new TimeSeries
{
    Metric = new Metric
    {
        Type = "custom.googleapis.com/honua/api_requests",
        Labels = { { "protocol", "wfs" } }
    },
    Resource = new MonitoredResource
    {
        Type = "generic_task",
        Labels = { { "project_id", "your-project-id" } }
    },
    Points = { new Point { ... } }
};

client.CreateTimeSeries(projectName, new[] { timeSeries });
```

#### 4. Create Monitoring Dashboards

**Using gcloud CLI:**
```bash
gcloud monitoring dashboards create --config-from-file=dashboard.json
```

**dashboard.json:**
```json
{
  "displayName": "Honua Overview",
  "mosaicLayout": {
    "columns": 12,
    "tiles": [
      {
        "width": 6,
        "height": 4,
        "widget": {
          "title": "Request Rate",
          "xyChart": {
            "dataSets": [{
              "timeSeriesQuery": {
                "timeSeriesFilter": {
                  "filter": "metric.type=\"custom.googleapis.com/honua/api_requests\"",
                  "aggregation": {
                    "perSeriesAligner": "ALIGN_RATE"
                  }
                }
              }
            }]
          }
        }
      }
    ]
  }
}
```

#### 5. Configure Alerting Policies

```bash
# High error rate alert
gcloud alpha monitoring policies create \
  --notification-channels=CHANNEL_ID \
  --display-name="Honua High Error Rate" \
  --condition-display-name="Error rate > 5%" \
  --condition-expression='
    resource.type = "generic_task" AND
    metric.type = "custom.googleapis.com/honua/error_rate" AND
    metric.value > 0.05'
```

### Cost Estimation
- **Metrics**: First 150 MB/month free, then $0.2580/MB
- **Logs**: First 50 GB/month free, then $0.50/GB
- **Typical Honua deployment**: **$10-30/month** (mostly free tier)

### AI Consultant Command

```bash
honua ai "Setup Google Cloud Monitoring with OpenTelemetry"
```

---

## Kubernetes (Platform-Agnostic)

### Architecture
```
Honua Pods → Prometheus Operator → Prometheus (managed)
                                  ↓
                            Grafana (managed)
                                  ↓
                          Alertmanager (managed)
```

### Setup with Prometheus Operator

#### 1. Install Prometheus Operator

```bash
# Using Helm
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm install kube-prometheus-stack prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --create-namespace \
  --set prometheus.prometheusSpec.retention=30d \
  --set prometheus.prometheusSpec.storageSpec.volumeClaimTemplate.spec.resources.requests.storage=50Gi
```

This installs:
- Prometheus Operator
- Prometheus (with persistent storage)
- Grafana (with dashboards)
- Alertmanager
- Node Exporter
- Kube State Metrics

#### 2. Annotate Honua Pods for Scraping

**deployment.yaml:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua
spec:
  template:
    metadata:
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "5000"
        prometheus.io/path: "/metrics"
    spec:
      containers:
      - name: honua
        image: honua/server:latest
        ports:
        - containerPort: 5000
          name: http
```

#### 3. Create ServiceMonitor

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: honua
  namespace: production
  labels:
    app: honua
spec:
  selector:
    matchLabels:
      app: honua
  endpoints:
  - port: http
    path: /metrics
    interval: 10s
```

#### 4. Import Grafana Dashboards

**ConfigMap with our dashboard:**
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-grafana-dashboard
  namespace: monitoring
  labels:
    grafana_dashboard: "1"
data:
  honua-detailed.json: |
    # Paste contents of docker/grafana/dashboards/honua-detailed.json
```

Grafana will auto-import dashboards with the `grafana_dashboard: "1"` label!

#### 5. Configure PrometheusRule for Alerts

```yaml
apiVersion: monitoring.coreos.com/v1
kind: PrometheusRule
metadata:
  name: honua-alerts
  namespace: monitoring
  labels:
    prometheus: kube-prometheus-stack
    role: alert-rules
spec:
  groups:
  - name: honua
    interval: 30s
    rules:
    - alert: HonuaHighErrorRate
      expr: |
        (
          sum(rate(honua_api_errors_total{job="honua"}[5m]))
          /
          sum(rate(honua_api_requests_total{job="honua"}[5m]))
        ) > 0.05
      for: 5m
      labels:
        severity: critical
      annotations:
        summary: "High API error rate detected"
        description: "Error rate is {{ $value | humanizePercentage }}"
```

### Alternative: Managed Prometheus

**Azure Monitor for Containers** (AKS):
```bash
az aks enable-addons -a monitoring -n myAKSCluster -g myResourceGroup
```

**Amazon Managed Prometheus** (EKS):
```bash
# Create AMP workspace
aws amp create-workspace --alias honua-metrics --region us-east-1

# Configure ADOT Collector to remote write
kubectl apply -f adot-collector-amp.yaml
```

**Google Cloud Managed Prometheus** (GKE):
```bash
gcloud container clusters update CLUSTER_NAME \
  --enable-managed-prometheus \
  --zone=ZONE
```

### Cost Estimation (Self-Managed in K8s)
- Compute for Prometheus/Grafana pods: **$20-50/month**
- Persistent storage (50 GB): **$5-10/month**
- **Total: $25-60/month**

### Cost Estimation (Managed)
- **Azure**: $0.01/GB ingested
- **AWS**: $0.08/GB ingested + $0.10/hour query
- **GCP**: Bundled with GKE, minimal extra cost

---

## Datadog / New Relic (SaaS APM)

### Datadog

**Install agent:**
```bash
helm install datadog datadog/datadog \
  --set datadog.apiKey=YOUR_API_KEY \
  --set datadog.site=datadoghq.com
```

**Annotate pods:**
```yaml
metadata:
  annotations:
    ad.datadoghq.com/honua.check_names: '["openmetrics"]'
    ad.datadoghq.com/honua.init_configs: '[{}]'
    ad.datadoghq.com/honua.instances: '[{"prometheus_url": "http://%%host%%:5000/metrics", "namespace": "honua", "metrics": ["*"]}]'
```

**Cost**: ~$15/host/month + $5/million logs

### New Relic

**Install .NET agent:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y wget && \
    wget https://download.newrelic.com/dot_net_agent/latest_release/newrelic-dotnet-agent_amd64.deb && \
    dpkg -i newrelic-dotnet-agent_amd64.deb

ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
ENV NEW_RELIC_LICENSE_KEY=YOUR_KEY
ENV NEW_RELIC_APP_NAME="Honua Server"
```

**Cost**: ~$100/month for 100 GB data

---

## Summary Comparison

| Platform | Setup Complexity | Monthly Cost (Est.) | Auto-Discovery | Alerting | Dashboards |
|----------|-----------------|---------------------|----------------|----------|------------|
| **Azure Monitor** | Low | $10-40 | ✅ Yes | ✅ Excellent | ✅ Good |
| **AWS CloudWatch** | Medium | $20-60 | ⚠️ Partial | ✅ Good | ⚠️ Basic |
| **GCP Cloud Monitoring** | Low | $10-30 (mostly free) | ✅ Yes | ✅ Good | ✅ Good |
| **Prometheus Operator** | Medium | $25-60 | ✅ Yes | ✅ Excellent | ✅ Excellent |
| **Managed Prometheus** | Low | $20-100 | ✅ Yes | ✅ Good | ⚠️ External |
| **Datadog** | Low | $100-300 | ✅ Excellent | ✅ Excellent | ✅ Excellent |
| **New Relic** | Low | $100-200 | ✅ Excellent | ✅ Excellent | ✅ Excellent |

## Recommendations

### For Azure Deployments
**Use Azure Monitor + Application Insights**
- Native integration
- Cost-effective
- Excellent .NET support
- Built-in anomaly detection

### For AWS Deployments
**Use AWS CloudWatch + X-Ray + ADOT**
- Native AWS integration
- Good cost at scale
- EMF for custom metrics
- X-Ray for distributed tracing

### For GCP Deployments
**Use Cloud Monitoring + Cloud Logging**
- Best free tier
- Simple setup
- Good OpenTelemetry support

### For Kubernetes (Any Cloud)
**Use Prometheus Operator**
- Industry standard
- Portable across clouds
- Existing Grafana dashboards work
- Community support

### For Multi-Cloud
**Use Datadog or New Relic**
- Unified view across clouds
- No infrastructure to manage
- Advanced AI/ML for anomaly detection
- Worth the cost for large deployments

## AI Consultant Integration

The Honua AI consultant can help set up any of these platforms:

```bash
# Azure
honua ai "Setup Azure Application Insights with custom metrics"

# AWS
honua ai "Configure CloudWatch monitoring with ADOT collector"

# GCP
honua ai "Deploy Google Cloud Monitoring with OpenTelemetry"

# Kubernetes
honua ai "Install Prometheus Operator and import Grafana dashboards"

# General
honua ai "What's the best monitoring solution for my deployment?"
```

The AI has built-in knowledge of:
- Platform-specific configurations
- Cost optimization strategies
- Alert threshold recommendations
- Dashboard query generation
- Troubleshooting procedures

## Migration from Self-Hosted

To migrate from docker-compose Prometheus/Grafana to managed services:

1. **Export existing dashboards**: Already in `docker/grafana/dashboards/*.json`
2. **Convert Prometheus queries** to platform-specific (AI can help!)
3. **Recreate alerts** using platform-native alerting
4. **Test in parallel** before switching over
5. **Update application configuration** (remove `/metrics` authentication if needed)

The AI consultant can automate much of this conversion!
