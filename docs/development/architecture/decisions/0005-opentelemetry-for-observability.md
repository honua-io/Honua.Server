# 5. OpenTelemetry for Observability

Date: 2025-10-17

Status: Accepted

## Context

Honua is a production geospatial server that requires comprehensive observability for operational excellence. We need to understand system behavior, diagnose issues, monitor performance, and track usage across distributed deployments.

**Observability Requirements:**
- **Metrics**: Track request rates, latency, error rates, resource usage
- **Distributed Tracing**: Follow requests through middleware, database, external services
- **Structured Logging**: Correlate logs with traces and metrics
- **Multi-Backend**: Support Prometheus, Grafana, Jaeger, Azure Monitor, cloud platforms
- **Standards-Based**: Avoid vendor lock-in, enable tooling ecosystem
- **Performance**: Minimal overhead in production

**Existing Codebase Evidence:**
- OpenTelemetry packages in `Honua.Server.Host.csproj`:
  - `OpenTelemetry.Extensions.Hosting`
  - `OpenTelemetry.Instrumentation.AspNetCore`
  - `OpenTelemetry.Instrumentation.Runtime`
  - `OpenTelemetry.Instrumentation.Http`
  - `OpenTelemetry.Exporter.Prometheus.AspNetCore`
  - `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- Observability setup: `/src/Honua.Server.Host/Extensions/ObservabilityExtensions.cs`
- Metrics implementation: `/src/Honua.Server.Core/Data/Postgres/PostgresConnectionPoolMetrics.cs`
- Process framework metrics: `/src/Honua.Cli.AI/Services/Processes/Observability/ProcessFrameworkMetrics.cs`
- Grafana dashboards: `/docker/grafana/dashboards/`
- Prometheus configuration: `/docker/prometheus/`

## Decision

We will use **OpenTelemetry (OTel)** as the standard observability framework for all Honua components.

**Implementation:**
- **OpenTelemetry SDK**: Core instrumentation library
- **OTLP Exporter**: OpenTelemetry Protocol for backend communication
- **Prometheus Exporter**: Metrics scraping endpoint (/metrics)
- **ASP.NET Core Instrumentation**: Automatic HTTP request tracing
- **Runtime Instrumentation**: .NET runtime metrics (GC, threads, memory)
- **Custom Metrics**: Domain-specific metrics (PostgreSQL pool, process framework)
- **Activity Sources**: Custom spans for business operations

**Supported Backends:**
- **Prometheus**: Metrics collection and alerting
- **Grafana**: Metrics visualization
- **Jaeger**: Distributed tracing
- **Azure Monitor**: Cloud-native observability (Azure deployments)
- **OpenTelemetry Collector**: Vendor-agnostic collection

## Consequences

### Positive

- **Vendor Neutral**: Not locked to any specific observability vendor
- **Industry Standard**: CNCF project with broad ecosystem support
- **Unified API**: Single SDK for metrics, traces, and logs (future)
- **Auto-Instrumentation**: ASP.NET Core tracing out-of-the-box
- **Multi-Backend**: Export to multiple systems simultaneously
- **Rich Ecosystem**: Community instrumentations for databases, HTTP clients, etc.
- **Cloud-Native**: First-class support in Kubernetes, cloud platforms
- **Performance**: Low overhead, efficient binary protocols (OTLP)
- **Future-Proof**: Active development, adding new capabilities

### Negative

- **Complexity**: More moving parts than simple logging
- **Configuration**: Requires backend setup (Prometheus, Jaeger, etc.)
- **Learning Curve**: Team must understand OTel concepts (spans, attributes, etc.)
- **Storage**: Traces and metrics require storage infrastructure
- **Cost**: Managed backends can be expensive at scale

### Neutral

- Requires operational infrastructure (Prometheus, Grafana, Jaeger)
- Trace sampling strategies needed for high-volume systems
- Must balance observability depth with performance impact

## Alternatives Considered

### 1. Application Insights Only (Azure-Specific)

Use Microsoft Application Insights for all observability.

**Pros:**
- Excellent .NET integration
- Rich Azure ecosystem
- Auto-instrumentation
- ML-powered insights
- Integrated alerts

**Cons:**
- **Azure vendor lock-in** (major issue)
- Cost increases with volume
- Not suitable for on-premises deployments
- Limited to Microsoft ecosystem
- Migration difficulty if switching platforms

**Verdict:** Rejected - too limiting for multi-cloud/on-premises support

### 2. Prometheus + Grafana Only

Use Prometheus for metrics, Grafana for visualization, no tracing.

**Pros:**
- Simple, proven stack
- Open source, self-hosted
- Excellent for metrics
- Low operational overhead

**Cons:**
- **No distributed tracing** (critical for microservices)
- Manual instrumentation required
- Grafana doesn't natively correlate logs/traces/metrics
- Limited to pull-based metrics

**Verdict:** Rejected - insufficient for comprehensive observability

### 3. Elastic Stack (ELK)

Use Elasticsearch, Logstash, Kibana for logs and APM for tracing.

**Pros:**
- Comprehensive platform
- Powerful search
- Good .NET support (Elastic APM agent)
- Unified UI

**Cons:**
- **Heavy resource requirements** (Elasticsearch cluster)
- Vendor-specific agent
- Complex to operate
- Costly at scale
- Less standard than OpenTelemetry

**Verdict:** Rejected - too heavyweight, vendor-specific

### 4. Datadog / New Relic (Commercial SaaS)

Use commercial observability platform.

**Pros:**
- Turnkey solution
- Excellent UI/UX
- ML-powered insights
- Managed infrastructure

**Cons:**
- **High costs at scale**
- Vendor lock-in
- Not suitable for air-gapped deployments
- Data leaves your infrastructure
- Less control

**Verdict:** Rejected - cost and vendor lock-in concerns

### 5. Custom Metrics + Structured Logging Only

Roll our own metrics with custom exporters and Serilog for logging.

**Pros:**
- Full control
- Minimal dependencies
- Tailored to needs

**Cons:**
- **Reinventing the wheel poorly**
- No ecosystem support
- Maintenance burden
- No distributed tracing
- Limited tooling integration

**Verdict:** Rejected - impractical to maintain

## Implementation Details

### Metrics Setup
```csharp
// /src/Honua.Server.Host/Extensions/ObservabilityExtensions.cs
services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());
```

### Tracing Setup
```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("Honua.Server")
        .AddOtlpExporter());
```

### Custom Metrics Example
```csharp
// PostgreSQL connection pool metrics
public class PostgresConnectionPoolMetrics
{
    private readonly Counter<int> _connectionsOpened;
    private readonly Histogram<double> _connectionDuration;

    public PostgresConnectionPoolMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Server.Postgres");
        _connectionsOpened = meter.CreateCounter<int>("postgres.connections.opened");
        _connectionDuration = meter.CreateHistogram<double>("postgres.connection.duration");
    }
}
```

### Custom Spans
```csharp
using var activity = ActivitySource.StartActivity("ProcessDeployment");
activity?.SetTag("deployment.target", "aws-ecs");
activity?.SetTag("deployment.region", "us-east-1");
// ... work ...
activity?.SetStatus(ActivityStatusCode.Ok);
```

### Configuration
```json
{
  "OpenTelemetry": {
    "Metrics": {
      "Enabled": true,
      "PrometheusExporterEnabled": true,
      "OtlpExporterEnabled": false
    },
    "Tracing": {
      "Enabled": true,
      "OtlpExporterEndpoint": "http://localhost:4317",
      "SamplingRatio": 0.1
    }
  }
}
```

### Prometheus Endpoint
- Metrics exposed at: `http://localhost:5000/metrics`
- Prometheus scrape configuration: `/docker/prometheus/prometheus.yml`

### Grafana Dashboards
Pre-built dashboards:
- `/docker/grafana/dashboards/honua-detailed.json` - Server metrics
- `/docker/process-testing/grafana/dashboards/process-framework-dashboard.json` - AI framework

## Metrics Catalog

**ASP.NET Core Metrics:**
- `http.server.request.duration` - Request latency histogram
- `http.server.active_requests` - Current active requests

**Runtime Metrics:**
- `process.runtime.dotnet.gc.collections.count` - GC collections
- `process.runtime.dotnet.gc.heap.size` - Heap size
- `process.runtime.dotnet.thread_pool.threads.count` - Thread pool

**Custom Metrics:**
- `postgres.connections.opened` - Database connections
- `postgres.connection.duration` - Connection acquisition time
- `process.step.duration` - AI process step duration
- `process.step.retries` - Process step retry counts

## Operational Integration

**Docker Compose Stack:**
```yaml
services:
  honua:
    environment:
      - OpenTelemetry__Metrics__Enabled=true
      - OpenTelemetry__Tracing__OtlpExporterEndpoint=http://otel-collector:4317

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest

  prometheus:
    image: prom/prometheus:latest

  grafana:
    image: grafana/grafana:latest
```

**Kubernetes Integration:**
```yaml
annotations:
  prometheus.io/scrape: "true"
  prometheus.io/port: "5000"
  prometheus.io/path: "/metrics"
```

## Documentation
- Setup guide: `/docs/MONITORING_SETUP.md`
- Performance baselines: `/docs/observability/performance-baselines.md`
- Managed services: `/docs/observability/managed-services-guide.md`

## References

- [OpenTelemetry Documentation](https://opentelemetry.io/)
- [OpenTelemetry .NET SDK](https://github.com/open-telemetry/opentelemetry-dotnet)
- [CNCF OpenTelemetry](https://www.cncf.io/projects/opentelemetry/)
- [Prometheus Documentation](https://prometheus.io/)

## Notes

OpenTelemetry is a strategic choice for vendor-neutral observability. While it requires more initial setup than turnkey solutions, it provides long-term flexibility and avoids vendor lock-in.

The ecosystem is rapidly maturing, with excellent .NET support and growing adoption across the industry. This decision positions Honua well for future observability needs.
