# Cloud-Agnostic Observability Provider Pattern

## Overview

The Honua Server observability system has been refactored to use a **cloud-agnostic provider pattern**, similar to the existing `ISecretsProvider` pattern. This allows seamless integration with any cloud provider's observability stack while maintaining a consistent configuration interface.

## Architecture

### Provider Pattern Structure

```
ICloudObservabilityProvider (interface)
├── AzureObservabilityProvider (Azure Monitor / Application Insights)
├── AwsObservabilityProvider (CloudWatch / X-Ray)
├── GcpObservabilityProvider (Cloud Monitoring / Cloud Trace)
└── SelfHostedObservabilityProvider (Prometheus / Jaeger / Grafana)
```

### Key Design Principles

1. **OpenTelemetry Foundation**: All providers use OpenTelemetry as the underlying instrumentation layer
2. **Opt-In by Configuration**: Default is self-hosted (Prometheus/Jaeger), cloud providers are opt-in
3. **Backward Compatibility**: Existing configurations continue to work
4. **Consistent Interface**: All providers implement the same interface methods
5. **Graceful Degradation**: Missing credentials or configuration result in warnings, not crashes

---

## Files Created

### 1. Provider Interface and Implementations

| File | Location | Purpose |
|------|----------|---------|
| `ICloudObservabilityProvider.cs` | `/home/user/Honua.Server/src/Honua.Server.Core/Observability/Providers/` | Interface defining the provider contract |
| `AzureObservabilityProvider.cs` | `/home/user/Honua.Server/src/Honua.Server.Core/Observability/Providers/` | Azure Monitor / Application Insights integration |
| `AwsObservabilityProvider.cs` | `/home/user/Honua.Server/src/Honua.Server.Core/Observability/Providers/` | AWS CloudWatch / X-Ray integration |
| `GcpObservabilityProvider.cs` | `/home/user/Honua.Server/src/Honua.Server.Core/Observability/Providers/` | GCP Cloud Monitoring / Cloud Trace integration |
| `SelfHostedObservabilityProvider.cs` | `/home/user/Honua.Server/src/Honua.Server.Core/Observability/Providers/` | Self-hosted Prometheus/Jaeger (default) |

---

## Files Modified

### 1. **ObservabilityExtensions.cs**
**Location**: `/home/user/Honua.Server/src/Honua.Server.Host/Extensions/ObservabilityExtensions.cs`

**Changes**:
- Added cloud provider selection logic using pattern matching
- Removed hardcoded Azure Monitor integration from metrics and tracing configuration
- Added provider-based configuration calls: `provider.ConfigureMetrics()` and `provider.ConfigureTracing()`
- Maintained backward compatibility with legacy `exporter: "azuremonitor"` setting

**Key Code Addition**:
```csharp
// Load cloud provider for observability
var cloudProvider = (observability.CloudProvider ?? "none").ToLowerInvariant();
var provider = cloudProvider switch
{
    "azure" => new AzureObservabilityProvider(),
    "aws" => new AwsObservabilityProvider(),
    "gcp" => new GcpObservabilityProvider(),
    _ => new SelfHostedObservabilityProvider()
};

// Configure cloud provider if enabled
if (provider.IsEnabled(configuration))
{
    provider.ConfigureMetrics(services, configuration);
    provider.ConfigureTracing(services, configuration);
}
```

### 2. **ObservabilityOptions.cs**
**Location**: `/home/user/Honua.Server/src/Honua.Server.Host/Observability/ObservabilityOptions.cs`

**Changes**:
- Added `CloudProvider` property to specify which provider to use
- Added nested configuration classes:
  - `AzureObservabilityOptions` - Connection string configuration
  - `AwsObservabilityOptions` - Region and OTLP endpoint configuration
  - `GcpObservabilityOptions` - Project ID and OTLP endpoint configuration
- Removed `UseAzureMonitor` flag from `MetricsOptions` (superseded by `CloudProvider`)

**New Properties**:
```csharp
public string? CloudProvider { get; init; }
public AzureObservabilityOptions Azure { get; init; } = new();
public AwsObservabilityOptions Aws { get; init; } = new();
public GcpObservabilityOptions Gcp { get; init; } = new();
```

### 3. **appsettings.Production.json**
**Location**: `/home/user/Honua.Server/src/Honua.Server.Host/appsettings.Production.json`

**Changes**:
- Added `cloudProvider` setting (default: "none")
- Added cloud provider-specific configuration sections:
  - `observability.azure` - Azure Application Insights configuration
  - `observability.aws` - AWS CloudWatch/X-Ray configuration
  - `observability.gcp` - GCP Cloud Monitoring/Trace configuration
- Updated comments to explain the new provider-based approach
- Deprecated `metrics.useAzureMonitor` in favor of `cloudProvider: "azure"`
- Added deployment and credential notes for each provider

### 4. **Honua.Server.Host.csproj**
**Location**: `/home/user/Honua.Server/src/Honua.Server.Host/Honua.Server.Host.csproj`

**Changes**:
- Added AWS observability packages:
  - `OpenTelemetry.Contrib.Extensions.AWSXRay` (Version 1.4.0) - X-Ray trace ID generation
  - `OpenTelemetry.ResourceDetectors.AWS` (Version 1.4.0-beta.1) - AWS resource detection
- Added commented GCP packages (uncomment when needed):
  - `Google.Cloud.Diagnostics.AspNetCore` (Version 5.1.0)
  - `OpenTelemetry.ResourceDetectors.GCP` (Version 1.0.0-beta.1)
- Reorganized OpenTelemetry packages with clear comments

---

## Configuration Examples

### Default (Self-Hosted)

**Prometheus + Jaeger + Grafana**

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
      "otlpEndpoint": "http://jaeger:4317"
    }
  }
}
```

**Deployment**:
```bash
# Docker Compose
docker run -d -p 9090:9090 prom/prometheus
docker run -d -p 16686:16686 -p 4317:4317 jaegertracing/all-in-one
docker run -d -p 3000:3000 grafana/grafana
```

---

### Azure Monitor / Application Insights

**Configuration**:
```json
{
  "observability": {
    "cloudProvider": "azure",
    "azure": {
      "connectionString": "InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/"
    },
    "metrics": {
      "enabled": true,
      "usePrometheus": false
    }
  }
}
```

**Environment Variables**:
```bash
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/"
# or
observability__cloudProvider=azure
observability__azure__connectionString="InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/"
```

**Features**:
- Metrics exported to Azure Monitor
- Distributed tracing in Application Insights
- Automatic correlation with Application Map
- Integration with Azure Alerts and Dashboards

**Package Requirements**:
- ✅ `Azure.Monitor.OpenTelemetry.Exporter` (Already included)

---

### AWS CloudWatch / X-Ray

**Configuration**:
```json
{
  "observability": {
    "cloudProvider": "aws",
    "aws": {
      "region": "us-east-1",
      "otlpEndpoint": "http://localhost:4317"
    },
    "metrics": {
      "enabled": true,
      "usePrometheus": false
    }
  }
}
```

**Environment Variables**:
```bash
AWS_REGION=us-east-1
AWS_ACCESS_KEY_ID=your-access-key-id
AWS_SECRET_ACCESS_KEY=your-secret-access-key
# or use IAM role (recommended)
# or
observability__cloudProvider=aws
observability__aws__region=us-east-1
```

**ADOT Collector Setup** (Required):

**ECS Task Definition** (Sidecar):
```json
{
  "containerDefinitions": [
    {
      "name": "honua-server",
      "image": "honua/server:latest",
      "environment": [
        { "name": "observability__cloudProvider", "value": "aws" }
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

**EKS DaemonSet**:
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: adot-collector-config
data:
  config.yaml: |
    receivers:
      otlp:
        protocols:
          grpc:
            endpoint: 0.0.0.0:4317
    exporters:
      awsxray:
      awsemf:
    service:
      pipelines:
        traces:
          receivers: [otlp]
          exporters: [awsxray]
        metrics:
          receivers: [otlp]
          exporters: [awsemf]
---
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: adot-collector
spec:
  selector:
    matchLabels:
      app: adot-collector
  template:
    metadata:
      labels:
        app: adot-collector
    spec:
      serviceAccountName: adot-collector
      containers:
      - name: adot-collector
        image: public.ecr.aws/aws-observability/aws-otel-collector:latest
        env:
        - name: AWS_REGION
          value: us-east-1
        ports:
        - containerPort: 4317
          protocol: TCP
        volumeMounts:
        - name: config
          mountPath: /etc/otel
      volumes:
      - name: config
        configMap:
          name: adot-collector-config
```

**IAM Permissions Required**:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "cloudwatch:PutMetricData",
        "xray:PutTraceSegments",
        "xray:PutTelemetryRecords"
      ],
      "Resource": "*"
    }
  ]
}
```

**Features**:
- Metrics exported to CloudWatch
- Distributed tracing in X-Ray
- Integration with AWS Service Map
- X-Ray trace segments and subsegments

**Package Requirements**:
- ✅ `OpenTelemetry.Contrib.Extensions.AWSXRay` (Already included)
- ✅ `OpenTelemetry.ResourceDetectors.AWS` (Already included)

---

### GCP Cloud Monitoring / Cloud Trace

**Configuration**:
```json
{
  "observability": {
    "cloudProvider": "gcp",
    "gcp": {
      "projectId": "my-project-123456",
      "otlpEndpoint": "http://localhost:4317"
    },
    "metrics": {
      "enabled": true,
      "usePrometheus": false
    }
  }
}
```

**Environment Variables**:
```bash
GOOGLE_CLOUD_PROJECT=my-project-123456
GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account.json
# or use GCE metadata (automatic on GKE/Cloud Run)
# or
observability__cloudProvider=gcp
observability__gcp__projectId=my-project-123456
```

**OpenTelemetry Collector Setup** (Required):

**GKE DaemonSet**:
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: otel-collector-config
data:
  config.yaml: |
    receivers:
      otlp:
        protocols:
          grpc:
            endpoint: 0.0.0.0:4317
    exporters:
      googlecloud:
        project: my-project-123456
        metric:
          prefix: honua.server/
        trace:
          endpoint: cloudtrace.googleapis.com:443
    service:
      pipelines:
        traces:
          receivers: [otlp]
          exporters: [googlecloud]
        metrics:
          receivers: [otlp]
          exporters: [googlecloud]
---
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
        env:
        - name: GOOGLE_APPLICATION_CREDENTIALS
          value: /var/secrets/google/key.json
        ports:
        - containerPort: 4317
          protocol: TCP
        volumeMounts:
        - name: config
          mountPath: /etc/otel
        - name: gcp-key
          mountPath: /var/secrets/google
      volumes:
      - name: config
        configMap:
          name: otel-collector-config
      - name: gcp-key
        secret:
          secretName: gcp-service-account
```

**IAM Permissions Required**:
```json
{
  "roles": [
    "roles/monitoring.metricWriter",
    "roles/cloudtrace.agent"
  ]
}
```

**Service Account Setup**:
```bash
# Create service account
gcloud iam service-accounts create honua-observability \
    --display-name="Honua Server Observability"

# Grant permissions
gcloud projects add-iam-policy-binding my-project-123456 \
    --member="serviceAccount:honua-observability@my-project-123456.iam.gserviceaccount.com" \
    --role="roles/monitoring.metricWriter"

gcloud projects add-iam-policy-binding my-project-123456 \
    --member="serviceAccount:honua-observability@my-project-123456.iam.gserviceaccount.com" \
    --role="roles/cloudtrace.agent"

# Create key
gcloud iam service-accounts keys create key.json \
    --iam-account=honua-observability@my-project-123456.iam.gserviceaccount.com

# Create Kubernetes secret
kubectl create secret generic gcp-service-account \
    --from-file=key.json=key.json
```

**Features**:
- Metrics exported to Cloud Monitoring
- Distributed tracing in Cloud Trace
- Integration with Cloud Console
- Custom metrics and dashboards

**Package Requirements**:
- ⚠️ `Google.Cloud.Diagnostics.AspNetCore` (Commented out - uncomment to use)
- ⚠️ `OpenTelemetry.ResourceDetectors.GCP` (Commented out - uncomment to use)

**To Enable GCP Packages**:
1. Open `/home/user/Honua.Server/src/Honua.Server.Host/Honua.Server.Host.csproj`
2. Uncomment the GCP package references:
   ```xml
   <PackageReference Include="Google.Cloud.Diagnostics.AspNetCore" Version="5.1.0" />
   <PackageReference Include="OpenTelemetry.ResourceDetectors.GCP" Version="1.0.0-beta.1" />
   ```
3. Uncomment the resource detector code in `GcpObservabilityProvider.cs`

---

## Migration Guide

### From Legacy Azure Monitor Integration

**Old Configuration** (Deprecated but still works):
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=xxx;..."
  },
  "observability": {
    "metrics": {
      "useAzureMonitor": true
    },
    "tracing": {
      "exporter": "azuremonitor"
    }
  }
}
```

**New Configuration** (Recommended):
```json
{
  "observability": {
    "cloudProvider": "azure",
    "azure": {
      "connectionString": "InstrumentationKey=xxx;..."
    }
  }
}
```

**Benefits of New Approach**:
- ✅ Consistent with other cloud providers
- ✅ Centralized cloud provider selection
- ✅ Easier to switch between providers
- ✅ No redundant configuration in multiple places

---

## Provider Implementation Details

### ICloudObservabilityProvider Interface

```csharp
public interface ICloudObservabilityProvider
{
    string ProviderName { get; }
    void ConfigureMetrics(IServiceCollection services, IConfiguration configuration);
    void ConfigureTracing(IServiceCollection services, IConfiguration configuration);
    void ConfigureLogging(ILoggingBuilder loggingBuilder, IConfiguration configuration);
    bool IsEnabled(IConfiguration configuration);
}
```

### Provider Selection Logic

```csharp
var cloudProvider = (observability.CloudProvider ?? "none").ToLowerInvariant();
var provider = cloudProvider switch
{
    "azure" => new AzureObservabilityProvider(),
    "aws" => new AwsObservabilityProvider(),
    "gcp" => new GcpObservabilityProvider(),
    _ => new SelfHostedObservabilityProvider()
};
```

### Credential Loading Priority

**Azure**:
1. `observability:azure:connectionString`
2. `ApplicationInsights:ConnectionString`
3. `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable

**AWS**:
1. `observability:aws:region`
2. `AWS:Region`
3. `AWS_REGION` environment variable
4. `AWS_DEFAULT_REGION` environment variable

**GCP**:
1. `observability:gcp:projectId`
2. `GCP:ProjectId`
3. `GOOGLE_CLOUD_PROJECT` environment variable
4. `GCP_PROJECT` environment variable

---

## Troubleshooting

### Provider Not Activating

**Symptoms**: Metrics/traces not appearing in cloud platform

**Check**:
1. Verify `cloudProvider` is set correctly in configuration
2. Check provider-specific credentials are configured
3. Look for warnings in application logs
4. Verify `provider.IsEnabled(configuration)` returns true

**Debug Commands**:
```bash
# Check configuration
cat appsettings.Production.json | grep cloudProvider

# Check environment variables
printenv | grep -i observability
printenv | grep -i aws
printenv | grep -i google
printenv | grep -i application

# Check application logs for provider initialization
docker logs honua-server | grep -i "observability\|provider"
```

### Azure Provider Not Working

**Check**:
1. Connection string format is correct
2. Application Insights resource exists
3. Ingestion endpoint is reachable
4. No firewall blocking `*.in.applicationinsights.azure.com`

**Test Connection**:
```bash
curl -v https://dc.services.visualstudio.com/v2/track
```

### AWS Provider Not Working

**Check**:
1. AWS region is set correctly
2. IAM credentials have necessary permissions
3. ADOT Collector is running and accessible at `otlpEndpoint`
4. EC2/ECS/EKS instance has IAM role attached

**Test ADOT Collector**:
```bash
curl -v http://localhost:4317
nc -zv localhost 4317
```

**Check IAM Permissions**:
```bash
aws sts get-caller-identity
aws cloudwatch list-metrics --namespace Honua/Server
aws xray get-trace-summaries --start-time $(date -d '1 hour ago' +%s) --end-time $(date +%s)
```

### GCP Provider Not Working

**Check**:
1. Project ID is correct
2. Service account has necessary IAM roles
3. `GOOGLE_APPLICATION_CREDENTIALS` points to valid JSON key
4. OpenTelemetry Collector is running and accessible

**Test GCP Credentials**:
```bash
gcloud auth application-default print-access-token
gcloud projects describe my-project-123456
```

**Test OpenTelemetry Collector**:
```bash
curl -v http://localhost:4317
nc -zv localhost 4317
```

---

## Performance Considerations

### Metrics Export Overhead

| Provider | Export Method | Overhead | Batching |
|----------|---------------|----------|----------|
| Self-Hosted | Pull (Prometheus scrape) | Minimal | N/A |
| Azure | Push (HTTP) | Low | Yes (default: 60s) |
| AWS | Push (OTLP → ADOT) | Low-Medium | Yes (via ADOT) |
| GCP | Push (OTLP → Collector) | Low-Medium | Yes (via Collector) |

### Sampling Recommendations

| Environment | Traffic Level | Sampling Ratio | Rationale |
|-------------|---------------|----------------|-----------|
| Development | Low | 1.0 (100%) | Debug all traces |
| Staging | Medium | 0.5 (50%) | Representative sample |
| Production (Low) | < 100 RPS | 1.0 (100%) | Trace all requests |
| Production (Medium) | 100-1000 RPS | 0.1 (10%) | Balance cost/visibility |
| Production (High) | > 1000 RPS | 0.01 (1%) | Cost control |

**Configure Sampling**:
```json
{
  "observability": {
    "tracing": {
      "samplingRatio": 0.1
    }
  }
}
```

---

## Cost Estimation

### Azure Monitor

| Metric | Free Tier | Paid Rate | Estimate (1M req/day) |
|--------|-----------|-----------|------------------------|
| Ingestion | 5 GB/month | $2.76/GB | ~$100-200/month |
| Retention | 90 days | Free | $0 |
| Queries | Unlimited | Free | $0 |

### AWS CloudWatch + X-Ray

| Metric | Free Tier | Paid Rate | Estimate (1M req/day) |
|--------|-----------|-----------|------------------------|
| CloudWatch Metrics | 10 metrics | $0.30/metric | ~$30/month |
| X-Ray Traces | 100K/month | $5/1M | ~$150/month |
| CloudWatch Logs | 5 GB/month | $0.50/GB | ~$50-100/month |

### GCP Cloud Monitoring + Trace

| Metric | Free Tier | Paid Rate | Estimate (1M req/day) |
|--------|-----------|-----------|------------------------|
| Monitoring Metrics | 150 MB/month | $0.2580/MB | ~$50-100/month |
| Cloud Trace | First 2.5M/month | $0.20/1M | ~$6/month |
| Logging | 50 GB/month | $0.50/GB | ~$25-50/month |

### Self-Hosted (Infrastructure Only)

| Component | Resources | Estimate (AWS/GCP) |
|-----------|-----------|-------------------|
| Prometheus | 2 vCPU, 4 GB RAM | ~$50/month |
| Jaeger | 2 vCPU, 4 GB RAM | ~$50/month |
| Grafana | 1 vCPU, 2 GB RAM | ~$25/month |
| Storage (1TB) | Block storage | ~$100/month |
| **Total** | | **~$225/month** |

**Cost Optimization Tips**:
1. Use sampling in high-traffic environments
2. Set retention policies appropriately
3. Filter noisy metrics/traces
4. Use cloud provider free tiers
5. Consider self-hosted for cost-sensitive workloads

---

## Security Considerations

### Credentials Management

**Do NOT**:
- ❌ Store connection strings in source control
- ❌ Use hardcoded credentials in configuration files
- ❌ Share credentials across environments

**Do**:
- ✅ Use environment variables for credentials
- ✅ Use secrets management (Azure Key Vault, AWS Secrets Manager, GCP Secret Manager)
- ✅ Use managed identities/IAM roles when possible
- ✅ Rotate credentials regularly

### Network Security

**Firewall Rules Required**:

**Azure**:
- Outbound HTTPS to `*.in.applicationinsights.azure.com` (443)
- Outbound HTTPS to `dc.services.visualstudio.com` (443)

**AWS**:
- Outbound TCP to ADOT Collector (4317)
- ADOT Collector to CloudWatch/X-Ray (443)

**GCP**:
- Outbound TCP to OpenTelemetry Collector (4317)
- Collector to `monitoring.googleapis.com` (443)
- Collector to `cloudtrace.googleapis.com` (443)

### Data Privacy

**Sensitive Data Handling**:
1. **Metrics**: Generally safe (aggregated numbers)
2. **Traces**: May contain PII in span attributes - filter as needed
3. **Logs**: Configure separately via Serilog (not covered by this provider pattern)

**Filtering PII Example**:
```csharp
tracingBuilder.AddAspNetCoreInstrumentation(options =>
{
    options.Filter = (httpContext) =>
    {
        // Don't trace sensitive endpoints
        return !httpContext.Request.Path.StartsWithSegments("/api/auth/credentials");
    };
    options.EnrichWithHttpRequest = (activity, httpRequest) =>
    {
        // Don't capture sensitive headers
        activity.SetTag("http.request.headers", "REDACTED");
    };
});
```

---

## Future Enhancements

### Planned Improvements

1. **Additional Providers**:
   - Datadog
   - New Relic
   - Honeycomb
   - Grafana Cloud (native integration)

2. **Provider Features**:
   - Automatic resource detection (EC2, GKE metadata)
   - Custom metric dimensions per provider
   - Provider-specific sampling strategies

3. **Configuration**:
   - Dynamic provider switching without restart
   - Multi-provider support (e.g., send to both Azure and Prometheus)
   - Provider-specific dashboards and alerts

4. **Observability**:
   - Provider health checks
   - Export failure metrics
   - Automatic fallback to self-hosted on cloud provider failure

---

## References

### Documentation

- **OpenTelemetry**: https://opentelemetry.io/docs/
- **Azure Monitor**: https://learn.microsoft.com/en-us/azure/azure-monitor/
- **AWS CloudWatch**: https://docs.aws.amazon.com/cloudwatch/
- **GCP Cloud Monitoring**: https://cloud.google.com/monitoring/docs

### Package Documentation

- `Azure.Monitor.OpenTelemetry.Exporter`: https://www.nuget.org/packages/Azure.Monitor.OpenTelemetry.Exporter
- `OpenTelemetry.Contrib.Extensions.AWSXRay`: https://www.nuget.org/packages/OpenTelemetry.Contrib.Extensions.AWSXRay
- `Google.Cloud.Diagnostics.AspNetCore`: https://www.nuget.org/packages/Google.Cloud.Diagnostics.AspNetCore

### Related Patterns

- **ISecretsProvider**: `/home/user/Honua.Server/src/Honua.Server.Core/Security/Secrets/`
- **Honua Authentication**: Similar multi-provider pattern for auth

---

## Summary

The cloud-agnostic observability provider pattern provides:

✅ **Flexibility**: Easy to switch between cloud providers or self-hosted solutions
✅ **Consistency**: Same configuration interface across all providers
✅ **Maintainability**: Cloud-specific code isolated in provider implementations
✅ **Scalability**: Add new providers without changing core code
✅ **Backward Compatibility**: Existing configurations continue to work
✅ **Production Ready**: Comprehensive error handling and logging

**Quick Start**:
1. Set `observability.cloudProvider` to `"azure"`, `"aws"`, `"gcp"`, or `"none"`
2. Configure provider-specific settings (connection string, region, project ID)
3. Deploy required infrastructure (ADOT Collector for AWS, OpenTelemetry Collector for GCP)
4. Verify metrics and traces appear in your chosen platform

For questions or issues, refer to the troubleshooting section or check application logs for provider initialization messages.
