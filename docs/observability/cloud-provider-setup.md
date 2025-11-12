# Cloud Provider Setup Guide

## Overview

Honua Server supports multiple observability backends through a cloud-agnostic provider pattern. You can choose between self-hosted open-source tools (Prometheus, Grafana, Jaeger) or managed cloud services (Azure, AWS, GCP).

### Provider Options

| Provider | Metrics | Tracing | Logs | Cost | Best For |
|----------|---------|---------|------|------|----------|
| **Self-Hosted** | Prometheus | Jaeger/Tempo | Loki/ELK | Infrastructure only | Full control, on-premises |
| **Azure** | Application Insights | Application Insights | Log Analytics | Pay-per-GB | Azure-native deployments |
| **AWS** | CloudWatch | X-Ray | CloudWatch Logs | Pay-per-metric | AWS-native deployments |
| **GCP** | Cloud Monitoring | Cloud Trace | Cloud Logging | Pay-per-metric | GCP-native deployments |

### Architecture

```
┌─────────────────────────────────────┐
│       Honua Server                  │
│                                     │
│  ┌──────────────────────────────┐  │
│  │  CloudProviderAdapter        │  │
│  │  (Configured via settings)   │  │
│  └──────────────────────────────┘  │
│              │                      │
└──────────────┼──────────────────────┘
               │
    ┌──────────┴──────────┐
    │                     │
    ▼                     ▼
┌─────────┐         ┌──────────┐
│ Self-   │         │ Cloud    │
│ Hosted  │         │ Provider │
└─────────┘         └──────────┘
```

## Quick Start

Choose your provider and follow the configuration steps below:

## Self-Hosted (Prometheus + Jaeger)

**Best for:** Development, on-premises deployments, full control over data

### Configuration

```json
{
  "observability": {
    "cloudProvider": "none",
    "metrics": {
      "enabled": true,
      "endpoint": "/metrics",
      "usePrometheus": true
    },
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://tempo:4317",
      "samplingRatio": 0.1
    }
  }
}
```

### Environment Variables

```bash
export observability__cloudProvider=none
export observability__metrics__enabled=true
export observability__tracing__exporter=otlp
export observability__tracing__otlpEndpoint=http://tempo:4317
```

### Docker Compose Setup

```bash
cd src/Honua.Server.Observability
docker-compose -f docker-compose.monitoring.yml up -d
```

This starts:
- **Prometheus** on http://localhost:9090
- **Grafana** on http://localhost:3000 (admin/admin)
- **Tempo** on http://localhost:3200
- **Alertmanager** on http://localhost:9093

### Kubernetes Setup

```yaml
apiVersion: v1
kind: Service
metadata:
  name: honua-server-metrics
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "5000"
    prometheus.io/path: "/metrics"
spec:
  selector:
    app: honua-server
  ports:
    - port: 5000
      name: metrics
```

### Prometheus Configuration

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'honua-server'
    scrape_interval: 15s
    static_configs:
      - targets: ['honua-server:5000']
    metrics_path: '/metrics'
    basic_auth:
      username: 'metrics-scraper'
      password: '${METRICS_PASSWORD}'
```

### Cost

- **Infrastructure only** (servers, storage, bandwidth)
- Typical: $50-200/month for small-medium deployments
- Scales linearly with data volume

---

## Azure Application Insights

**Best for:** Azure-native deployments, integrated with Azure services

### Configuration

```json
{
  "observability": {
    "cloudProvider": "azure",
    "azure": {
      "connectionString": "InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/"
    },
    "metrics": {
      "enabled": true
    },
    "tracing": {
      "exporter": "otlp",
      "samplingRatio": 0.1
    }
  }
}
```

### Environment Variables

```bash
export observability__cloudProvider=azure
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/"
```

### Azure Portal Setup

1. **Create Application Insights Resource**
   ```bash
   az monitor app-insights component create \
     --app honua-server-prod \
     --location eastus \
     --resource-group honua-rg \
     --kind web
   ```

2. **Get Connection String**
   ```bash
   az monitor app-insights component show \
     --app honua-server-prod \
     --resource-group honua-rg \
     --query connectionString -o tsv
   ```

3. **Set Environment Variable in App Service**
   ```bash
   az webapp config appsettings set \
     --name honua-server-prod \
     --resource-group honua-rg \
     --settings APPLICATIONINSIGHTS_CONNECTION_STRING="<connection-string>"
   ```

### IAM Permissions Required

- **Monitoring Metrics Publisher** - Write metrics
- **Monitoring Contributor** (optional) - Read metrics for dashboards

### Grafana Integration

Azure Application Insights can be queried from Grafana:

```yaml
# Grafana datasource
apiVersion: 1
datasources:
  - name: Azure Monitor
    type: grafana-azure-monitor-datasource
    jsonData:
      cloudName: azuremonitor
      subscriptionId: <subscription-id>
      tenantId: <tenant-id>
      clientId: <client-id>
    secureJsonData:
      clientSecret: <client-secret>
```

### Cost Estimate

- **First 5 GB/month**: Free
- **Additional data**: $2.30/GB
- **Typical cost**: $50-500/month depending on traffic
- **Retention**: 90 days (default)

---

## AWS CloudWatch + X-Ray

**Best for:** AWS-native deployments (ECS, EKS, Lambda)

### Configuration

```json
{
  "observability": {
    "cloudProvider": "aws",
    "aws": {
      "region": "us-east-1",
      "otlpEndpoint": "http://localhost:4317"
    },
    "metrics": {
      "enabled": true
    },
    "tracing": {
      "exporter": "otlp",
      "samplingRatio": 0.1
    }
  }
}
```

### Environment Variables

```bash
export observability__cloudProvider=aws
export AWS_REGION=us-east-1
export AWS_ACCESS_KEY_ID=<access-key>
export AWS_SECRET_ACCESS_KEY=<secret-key>
```

### AWS Distro for OpenTelemetry (ADOT) Setup

Deploy ADOT Collector as a sidecar (ECS) or DaemonSet (EKS):

**ECS Task Definition:**

```json
{
  "family": "honua-server",
  "containerDefinitions": [
    {
      "name": "honua-server",
      "image": "honua/server:latest",
      "environment": [
        {
          "name": "observability__cloudProvider",
          "value": "aws"
        },
        {
          "name": "observability__aws__otlpEndpoint",
          "value": "http://localhost:4317"
        }
      ]
    },
    {
      "name": "aws-otel-collector",
      "image": "public.ecr.aws/aws-observability/aws-otel-collector:latest",
      "command": ["--config=/etc/ecs/ecs-cloudwatch-xray.yaml"]
    }
  ]
}
```

**EKS DaemonSet:**

```yaml
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: aws-otel-collector
spec:
  selector:
    matchLabels:
      name: aws-otel-collector
  template:
    metadata:
      labels:
        name: aws-otel-collector
    spec:
      containers:
      - name: aws-otel-collector
        image: public.ecr.aws/aws-observability/aws-otel-collector:latest
        env:
        - name: AWS_REGION
          value: us-east-1
```

### IAM Permissions Required

Attach this policy to your ECS task role or EKS service account:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "cloudwatch:PutMetricData",
        "xray:PutTraceSegments",
        "xray:PutTelemetryRecords",
        "logs:PutLogEvents",
        "logs:CreateLogGroup",
        "logs:CreateLogStream"
      ],
      "Resource": "*"
    }
  ]
}
```

### Grafana Integration

CloudWatch datasource for Grafana:

```yaml
apiVersion: 1
datasources:
  - name: CloudWatch
    type: cloudwatch
    jsonData:
      defaultRegion: us-east-1
      authType: keys
    secureJsonData:
      accessKey: <access-key>
      secretKey: <secret-key>
```

### Cost Estimate

- **Metrics**: $0.30 per metric per month
- **CloudWatch Logs**: $0.50/GB ingested
- **X-Ray traces**: $5 per million traces recorded
- **Typical cost**: $100-1000/month depending on scale

---

## GCP Cloud Monitoring + Cloud Trace

**Best for:** GCP-native deployments (GKE, Cloud Run)

### Configuration

```json
{
  "observability": {
    "cloudProvider": "gcp",
    "gcp": {
      "projectId": "my-project-123456",
      "otlpEndpoint": "http://localhost:4317"
    },
    "metrics": {
      "enabled": true
    },
    "tracing": {
      "exporter": "otlp",
      "samplingRatio": 0.1
    }
  }
}
```

### Environment Variables

```bash
export observability__cloudProvider=gcp
export GOOGLE_CLOUD_PROJECT=my-project-123456
export GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account.json
```

### GKE Setup with OpenTelemetry Collector

Deploy OpenTelemetry Collector with Google Cloud exporter:

```yaml
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: otel-collector
spec:
  selector:
    matchLabels:
      app: otel-collector
  template:
    metadata:
      labels:
        app: otel-collector
    spec:
      serviceAccountName: otel-collector
      containers:
      - name: otel-collector
        image: otel/opentelemetry-collector-contrib:latest
        command:
        - "/otelcol-contrib"
        - "--config=/conf/otel-collector-config.yaml"
        env:
        - name: GOOGLE_APPLICATION_CREDENTIALS
          value: /var/secrets/google/key.json
        volumeMounts:
        - name: otel-config
          mountPath: /conf
        - name: google-cloud-key
          mountPath: /var/secrets/google
      volumes:
      - name: otel-config
        configMap:
          name: otel-collector-config
      - name: google-cloud-key
        secret:
          secretName: otel-collector-sa
```

**OpenTelemetry Collector Config:**

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317

exporters:
  googlecloud:
    project: my-project-123456
    metric:
      prefix: custom.googleapis.com/honua
    trace: {}

service:
  pipelines:
    metrics:
      receivers: [otlp]
      exporters: [googlecloud]
    traces:
      receivers: [otlp]
      exporters: [googlecloud]
```

### IAM Permissions Required

Create service account with these roles:

```bash
gcloud projects add-iam-policy-binding my-project-123456 \
  --member="serviceAccount:otel-collector@my-project-123456.iam.gserviceaccount.com" \
  --role="roles/monitoring.metricWriter"

gcloud projects add-iam-policy-binding my-project-123456 \
  --member="serviceAccount:otel-collector@my-project-123456.iam.gserviceaccount.com" \
  --role="roles/cloudtrace.agent"
```

### Grafana Integration

GCP datasource for Grafana:

```yaml
apiVersion: 1
datasources:
  - name: Google Cloud Monitoring
    type: stackdriver
    jsonData:
      authenticationType: gce
      defaultProject: my-project-123456
```

### Cost Estimate

- **Cloud Monitoring**: First 150 MB/month free, then $0.2580/MB
- **Cloud Trace**: First 2.5 million spans/month free, then $0.20 per million
- **Typical cost**: $50-500/month depending on volume

---

## Switching Between Providers

### Runtime Switch (Environment Variable)

```bash
# Switch to Azure
export observability__cloudProvider=azure
export APPLICATIONINSIGHTS_CONNECTION_STRING="..."

# Switch to AWS
export observability__cloudProvider=aws
export AWS_REGION=us-east-1

# Switch back to self-hosted
export observability__cloudProvider=none
```

### Configuration File Switch

Update `appsettings.Production.json`:

```json
{
  "observability": {
    "cloudProvider": "azure"  // Change this: "none", "azure", "aws", "gcp"
  }
}
```

### Zero-Downtime Migration

1. **Dual-write period**: Send metrics to both old and new providers
2. **Verify data**: Confirm new provider receives data correctly
3. **Switch primary**: Update cloudProvider setting
4. **Monitor**: Watch for issues in new provider
5. **Cleanup**: Remove old provider after verification period

---

## Cost Comparison

### Monthly Cost Estimates (Small Deployment)

| Provider | Metrics | Traces | Logs | Total |
|----------|---------|--------|------|-------|
| **Self-Hosted** | $50 | $30 | $20 | **$100** |
| **Azure** | $100 | $50 | $50 | **$200** |
| **AWS** | $150 | $100 | $75 | **$325** |
| **GCP** | $120 | $80 | $60 | **$260** |

### Monthly Cost Estimates (Medium Deployment)

| Provider | Metrics | Traces | Logs | Total |
|----------|---------|--------|------|-------|
| **Self-Hosted** | $200 | $100 | $100 | **$400** |
| **Azure** | $300 | $150 | $200 | **$650** |
| **AWS** | $500 | $300 | $250 | **$1,050** |
| **GCP** | $400 | $250 | $200 | **$850** |

*Note: Costs vary based on data volume, retention, and usage patterns.*

---

## Best Practices

### 1. Start with Self-Hosted for Development

```bash
# Quick start with Docker Compose
cd src/Honua.Server.Observability
docker-compose -f docker-compose.monitoring.yml up -d
```

### 2. Use Managed Services in Production

- **Less operational overhead**
- **Built-in high availability**
- **Integrated with cloud services**

### 3. Enable Sampling for High-Volume Systems

```json
{
  "observability": {
    "tracing": {
      "samplingRatio": 0.01  // Sample 1% of requests
    }
  }
}
```

### 4. Monitor Observability Costs

Set up billing alerts in your cloud provider:

- **Azure**: Budget alerts in Cost Management
- **AWS**: CloudWatch billing alarms
- **GCP**: Budget alerts in Billing

### 5. Use Recording Rules to Reduce Query Load

Recording rules pre-compute expensive queries and reduce cardinality:

```yaml
# prometheus/recording-rules.yml
groups:
  - name: honua_sli
    interval: 30s
    rules:
      - record: honua:availability:ratio_5m
        expr: 1 - (rate(honua.http.requests.total{http.status_class="5xx"}[5m]) / rate(honua.http.requests.total[5m]))
```

---

## Troubleshooting

### Metrics Not Appearing in Cloud Provider

**Check 1: Verify cloud provider is configured**
```bash
# Check environment variable
echo $observability__cloudProvider

# Should output: azure, aws, or gcp
```

**Check 2: Verify credentials**
```bash
# Azure
az account show

# AWS
aws sts get-caller-identity

# GCP
gcloud auth list
```

**Check 3: Check application logs**
```bash
# Look for OpenTelemetry export errors
docker logs honua-server | grep -i "export"
```

### High Costs

**Solution 1: Reduce metric cardinality**
- Remove high-cardinality labels (user IDs, request IDs)
- Use sampling for traces

**Solution 2: Adjust retention periods**
- Azure: Configure data retention in Application Insights
- AWS: Set CloudWatch Logs retention policies
- GCP: Configure retention in Cloud Monitoring

**Solution 3: Use recording rules**
- Pre-compute expensive queries
- Reduce raw metric storage

---

## References

### Documentation
- [Azure Application Insights](https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview)
- [AWS CloudWatch](https://docs.aws.amazon.com/cloudwatch/)
- [GCP Cloud Monitoring](https://cloud.google.com/monitoring/docs)
- [OpenTelemetry](https://opentelemetry.io/docs/)

### Related Guides
- [SLI/SLO Monitoring Guide](sli-slo-monitoring.md)
- [Deployment Checklist](../deployment/observability-deployment-checklist.md)
- [Migration Guide](migration-guide.md)

---

**Next Steps:**
1. Choose your provider based on deployment environment
2. Follow the configuration steps above
3. Deploy and verify metrics are being collected
4. Set up dashboards and alerts
5. Review costs and optimize as needed
