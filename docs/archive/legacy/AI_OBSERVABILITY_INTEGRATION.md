# AI Observability & Telemetry Integration

## Overview

Honua AI now integrates with Honua Server's existing OpenTelemetry infrastructure to provide intelligent deployment validation, health monitoring, and automated rollback decisions based on telemetry signals.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     DEPLOYMENT FLOW                              │
└──────────────────────────────────┬──────────────────────────────┘
                                   │
                                   ▼
                    ┌───────────────────────────┐
                    │  Deploy Infrastructure    │
                    │  (Terraform/K8s/Docker)   │
                    └─────────────┬─────────────┘
                                  │
                                  ▼
          ┌───────────────────────────────────────────────┐
          │  ObservabilityConfigurationAgent              │
          │  • Generate OpenTelemetry config              │
          │  • Generate Prometheus scrape config          │
          │  • Generate Grafana dashboards                │
          │  • Generate alerting rules                    │
          │  • Deploy observability stack                 │
          └───────────────────┬───────────────────────────┘
                              │
                              ▼
          ┌───────────────────────────────────────────────┐
          │  Deploy & Wait for Metrics                    │
          │  (2-5 minutes for baseline)                   │
          └───────────────────┬───────────────────────────┘
                              │
                              ▼
          ┌───────────────────────────────────────────────┐
          │  ValidationLoopExecutor                       │
          │                                               │
          │  Iteration 1:                                 │
          │  ┌──────────────────────────────────────┐   │
          │  │ ObservabilityValidationAgent         │   │
          │  │ • Query /metrics endpoint            │   │
          │  │ • Analyze honua.api.* metrics        │   │
          │  │ • Check error rates, latency         │   │
          │  │ • LLM anomaly detection              │   │
          │  └────────────┬─────────────────────────┘   │
          │               │                              │
          │               ▼                              │
          │      Healthy? ──Yes──> Success              │
          │               │                              │
          │              No                              │
          │               │                              │
          │               ▼                              │
          │      ┌───────────────────┐                  │
          │      │ Remediation       │                  │
          │      │ • Adjust metadata?│                  │
          │      │ • Scale resources?│                  │
          │      │ • Rollback?       │                  │
          │      └─────────┬─────────┘                  │
          │                │                              │
          │  Iteration 2... (max 3)                      │
          └────────────────────────────────────────────────┘
```

## Honua's Existing OpenTelemetry Metrics

### Core Metrics (from IApiMetrics)

1. **honua.api.requests** (Counter)
   - Dimensions: `api.protocol`, `service.id`, `layer.id`
   - Tracks: Total API requests by OGC protocol, service, and layer

2. **honua.api.request_duration** (Histogram)
   - Dimensions: `api.protocol`, `service.id`, `layer.id`, `http.status_code`
   - Tracks: Request latency in milliseconds
   - Percentiles: p50, p95, p99

3. **honua.api.errors** (Counter)
   - Dimensions: `api.protocol`, `service.id`, `layer.id`, `error.type`, `error.category`
   - Tracks: API errors by type and category
   - Categories: validation, security, performance, network, database, storage, resource, application

4. **honua.api.features_returned** (Counter)
   - Dimensions: `api.protocol`, `service.id`, `layer.id`
   - Tracks: Number of features returned per request

### Activity Sources (Distributed Tracing)

- `Honua.Server.OgcProtocols` - WMS, WFS, WMTS, WCS, CSW operations
- `Honua.Server.OData` - OData query operations
- `Honua.Server.Stac` - STAC catalog operations
- `Honua.Server.Database` - PostGIS query operations
- `Honua.Server.RasterTiles` - Raster tile generation
- `Honua.Server.Metadata` - Metadata operations
- `Honua.Server.Authentication` - Auth operations
- `Honua.Server.Export` - Data export operations
- `Honua.Server.Import` - Data import/migration operations

## ObservabilityValidationAgent

### Purpose
Validates deployed Honua infrastructure by querying OpenTelemetry metrics and analyzing health signals.

### Validation Checks

#### 1. Endpoint Availability
```csharp
var endpoints = new List<EndpointHealthCheck>
{
    new() { Name = "Landing Page", Url = "http://honua:8080/", MaxResponseTimeMs = 5000 },
    new() { Name = "Metrics", Url = "http://honua:8080/metrics", MaxResponseTimeMs = 3000 },
    new() { Name = "Health", Url = "http://honua:8080/health", MaxResponseTimeMs = 5000 }
};
```

#### 2. Error Rate Analysis
Queries Prometheus for error rates:
```promql
rate(honua_api_errors_total[5m]) / rate(honua_api_requests_total[5m])
```

**Thresholds**:
- Warning: > 5% error rate
- Critical: > 10% error rate (recommend rollback)

#### 3. Latency Analysis
Queries P95 and P99 latency:
```promql
histogram_quantile(0.95, rate(honua_api_request_duration_bucket[5m]))
histogram_quantile(0.99, rate(honua_api_request_duration_bucket[5m]))
```

**Thresholds**:
- Warning: P95 > 2000ms
- Critical: P99 > 5000ms

#### 4. Service Health
```promql
up{job="honua-server"}
```

**Threshold**:
- Critical: Service down for 2+ minutes

#### 5. Database Health
Analyzes database error rate:
```promql
rate(honua_api_errors_total{error_category="database"}[5m])
```

**Threshold**:
- High: > 1 database error per second

#### 6. LLM-Powered Anomaly Detection

Uses LLM to detect patterns across metrics:
- Correlation between errors and resource usage
- Cascading failures (one issue causing others)
- Performance degradation trends
- Configuration mismatches

### Usage in Validation Loop

```csharp
var loopExecutor = new ValidationLoopExecutor(logger);

var result = await loopExecutor.ExecuteWithValidationAsync(
    // Execute: Deploy infrastructure
    async ct => await deploymentAgent.DeployAsync(config, ct),

    // Validate: Check observability metrics
    async (deployResult, ct) =>
    {
        var healthCheck = new DeploymentHealthCheckRequest
        {
            DeploymentName = "honua-prod",
            Endpoints = new List<EndpointHealthCheck>
            {
                new() { Name = "API", Url = "http://honua:8080/", MaxResponseTimeMs = 5000 }
            },
            MetricsEndpoint = "http://prometheus:9090/api/v1/query",
            ExpectedMetrics = new Dictionary<string, MetricExpectation>
            {
                ["honua_api_errors_rate"] = new() { Max = 0.05 }, // 5% max error rate
                ["honua_api_request_duration_p95"] = new() { Max = 2000 } // 2s max P95
            },
            ResourceMetricsEndpoint = "http://prometheus:9090/api/v1/query",
            EnableAnomalyDetection = true,
            RollbackOnMultipleWarnings = true
        };

        var validation = await observabilityAgent.ValidateDeploymentHealthAsync(
            healthCheck, context, ct);

        return new ValidationResult
        {
            Passed = validation.OverallStatus == HealthStatus.Healthy,
            Failures = validation.Checks
                .Where(c => c.Status != HealthStatus.Healthy)
                .Select(c => new ValidationFailure
                {
                    Category = c.Category,
                    Message = c.Message,
                    Severity = c.Status == HealthStatus.Critical ? "high" : "medium",
                    Suggestion = c.Details.GetValueOrDefault("recommended_action")
                })
                .ToList(),
            Message = validation.Recommendation
        };
    },

    // Remediate: Fix issues or rollback
    async (validationResult, ct) =>
    {
        var failures = validationResult.Failures;

        // Check if metadata fix can resolve issues
        if (failures.Any(f => f.Category == "Metadata"))
        {
            // Update metadata and retry
            return new RemediationResult
            {
                CanRetry = true,
                ActionTaken = "Updated metadata configuration",
                Reasoning = "Metadata issues detected, reconfiguring services",
                ChangesApplied = new List<string> { "metadata_update" }
            };
        }

        // Check if resource scaling can help
        if (failures.Any(f => f.Category == "Resources"))
        {
            // Scale up resources
            return new RemediationResult
            {
                CanRetry = true,
                ActionTaken = "Scaled up resources",
                Reasoning = "High resource utilization detected",
                ChangesApplied = new List<string> { "scale_up" }
            };
        }

        // Critical errors - recommend rollback
        if (failures.Any(f => f.Severity == "high"))
        {
            return new RemediationResult
            {
                CanRetry = false,
                ActionTaken = "Recommend rollback",
                Reasoning = "Critical errors detected, automatic remediation not possible",
                ChangesApplied = new List<string>()
            };
        }

        return new RemediationResult
        {
            CanRetry = true,
            ActionTaken = "Wait and retry",
            Reasoning = "Transient issues may resolve",
            ChangesApplied = new List<string>()
        };
    },

    "Production Deployment",
    cancellationToken
);

if (!result.Success)
{
    Console.WriteLine($"❌ Deployment validation failed after {result.TotalIterations} iterations");
    Console.WriteLine(result.Message);

    // Initiate rollback
    await rollbackAgent.RollbackAsync(deploymentId, cancellationToken);
}
```

## ObservabilityConfigurationAgent

### Purpose
Generates complete observability stack configuration for monitoring Honua deployments.

### Generated Configurations

#### 1. OpenTelemetry Collector (`otel-collector.yaml`)
- Scrapes Honua's `/metrics` endpoint
- Batches and exports to Prometheus, Jaeger, Datadog, etc.
- Adds resource attributes (service name, environment)

#### 2. Prometheus (`prometheus.yml`)
- Scrape configs for Honua server, database, OTel collector
- Alert rule references
- External labels for multi-cluster deployments

#### 3. Grafana Dashboard (`grafana-dashboard-honua.json`)
- Request rate by protocol (WFS, WMS, OGC API Features)
- Error rate by error type
- Latency percentiles (P50, P95, P99)
- Features returned by layer

#### 4. Alert Rules (`alert-rules.yml`)
- **HonuaHighErrorRate**: Warning at 5% error rate
- **HonuaCriticalErrorRate**: Critical at 10% (recommend rollback)
- **HonuaSlowResponse**: Warning at P95 > 2s
- **HonuaCriticalLatency**: Critical at P99 > 5s
- **HonuaServiceDown**: Critical if down 2+ minutes
- **HonuaDatabaseErrors**: High if > 1 DB error/sec

#### 5. Deployment Manifests
- **Docker Compose** (`docker-compose.observability.yml`):
  - otel-collector, prometheus, grafana, alertmanager services
  - Shared network, persistent volumes

- **Kubernetes** (`observability-stack.yaml`):
  - Deployments for collector, prometheus
  - Services and ConfigMaps
  - namespace: observability

### Usage

```csharp
var observabilityConfig = new ObservabilityConfigurationAgent(kernel, llmProvider, logger);

var result = await observabilityConfig.GenerateObservabilityConfigAsync(
    new ObservabilityConfigRequest
    {
        DeploymentName = "honua-prod",
        Environment = "production",
        Platform = "kubernetes", // or "docker-compose"
        Backend = "prometheus", // or "jaeger", "datadog", "newrelic"
        HonuaBaseUrl = "http://honua:8080",
        HonuaMetricsEndpoint = "honua:8080/metrics",
        DatabaseMetricsEndpoint = "postgres-exporter:9187"
    },
    context,
    cancellationToken
);

if (result.Success)
{
    var configs = JsonSerializer.Deserialize<Dictionary<string, string>>(result.GeneratedArtifact);

    // Write configurations to files
    foreach (var (filename, content) in configs)
    {
        await File.WriteAllTextAsync($"observability/{filename}", content);
    }

    // Deploy observability stack
    if (platform == "kubernetes")
    {
        await kubectl.ApplyAsync("observability-stack.yaml");
    }
    else if (platform == "docker-compose")
    {
        await docker.ComposeUpAsync("docker-compose.observability.yml");
    }
}
```

## GitOps Observability Reconciliation

### Concept
GitOps reconciler continuously monitors Honua's telemetry and automatically adjusts metadata/configuration when issues detected.

### Use Cases

#### 1. Automatic Metadata Optimization
**Problem**: Layer performing poorly (high latency, errors)
**Detection**: ObservabilityValidationAgent detects elevated error rate for specific layer
**Action**: Adjust layer metadata (simplify geometry, add spatial index hints)

```csharp
// In GitOps reconciliation loop
var validation = await observabilityAgent.ValidateDeploymentHealthAsync(...);

if (!validation.ShouldRollback && validation.Warnings > 0)
{
    var layerIssues = validation.Checks
        .Where(c => c.Category == "Metrics" && c.Details.ContainsKey("layer_id"))
        .ToList();

    foreach (var issue in layerIssues)
    {
        var layerId = issue.Details["layer_id"];

        // Recommend metadata adjustments
        var recommendation = await metadataAgent.RecommendOptimizationsAsync(layerId);

        // Create PR with metadata updates
        await gitOps.CreateOptimizationPR(layerId, recommendation);
    }
}
```

#### 2. Automatic Scaling
**Problem**: High resource utilization
**Detection**: CPU/memory metrics above threshold
**Action**: Scale deployment up

```csharp
if (validation.Checks.Any(c => c.Category == "Resources" && c.Status == HealthStatus.Warning))
{
    var resourceCheck = validation.Checks.First(c => c.Category == "Resources");
    var cpuUsage = double.Parse(resourceCheck.Details["cpu_percent"]);

    if (cpuUsage > 80)
    {
        await k8s.ScaleDeploymentAsync("honua-server", replicas: currentReplicas + 1);

        // Track scaling event for learning
        await telemetry.TrackAutoScalingAsync("honua-server", cpuUsage, currentReplicas + 1);
    }
}
```

#### 3. Proactive Rollback
**Problem**: Deployment showing early signs of failure
**Detection**: Multiple warnings + anomaly detection signals
**Action**: Automatic rollback before critical threshold

```csharp
if (validation.ShouldRollback)
{
    _logger.LogCritical("Automatic rollback triggered: {Reason}", validation.Recommendation);

    // Rollback to previous version
    await gitOps.RollbackToLastKnownGoodAsync();

    // Track rollback for learning
    await telemetry.TrackAutoRollbackAsync(deploymentId, validation);
}
```

## Integration with Existing Agents

### DeploymentConfigurationAgent
Now generates observability config alongside infrastructure:

```csharp
// Generate Terraform
var terraformConfig = await GenerateTerraformAsync(...);

// Generate observability config
var observabilityConfig = await observabilityAgent.GenerateObservabilityConfigAsync(...);

// Bundle together
return new DeploymentPackage
{
    InfrastructureConfig = terraformConfig,
    ObservabilityConfig = observabilityConfig,
    HealthCheckEndpoints = healthEndpoints
};
```

### DeploymentExecutionAgent
Now includes observability validation:

```csharp
// Deploy infrastructure
await terraform.ApplyAsync();

// Deploy observability stack
await deployObservabilityStack();

// Wait for metrics baseline
await Task.Delay(TimeSpan.FromMinutes(2));

// Validate using ObservabilityValidationAgent
var validation = await observabilityAgent.ValidateDeploymentHealthAsync(...);

if (!validation.Passed)
{
    await terraform.DestroyAsync(); // Rollback
    throw new DeploymentValidationException(validation.Message);
}
```

## Benefits

### 1. Intelligent Validation
- Goes beyond simple "is it up?" checks
- Analyzes actual traffic patterns and errors
- Detects performance degradation early
- LLM-powered anomaly detection

### 2. Automatic Remediation
- Metadata optimization for slow layers
- Resource scaling for high load
- Automatic rollback on critical issues
- Learning from past failures

### 3. Continuous Monitoring
- GitOps reconciliation continuously validates health
- Proactive issue detection before user impact
- Automated metadata adjustments
- Platform health insights

### 4. Learning Loop
All validation data feeds back into telemetry:
- Which deployments had issues
- What remediation worked
- Common failure patterns
- Optimal thresholds per deployment type

This improves future deployments automatically!

## GisEndpointValidationAgent

### Purpose
Validates deployed Honua GIS services by smoke-testing all supported protocols and endpoints.

### Protocol Support

#### 1. Honua Health Check
```csharp
GET /health
Expected: { "status": "healthy" }
```

#### 2. OGC API Features
- Landing page conformance (`/`)
- Collections endpoint (`/collections`)
- Individual collection metadata (`/collections/{id}`)
- Feature retrieval (`/collections/{id}/items`)

#### 3. WFS (Web Feature Service)
```
GET /wfs?service=WFS&version=2.0.0&request=GetCapabilities
Expected: WFS_Capabilities XML document
```

#### 4. WMS (Web Map Service)
```
GET /wms?service=WMS&version=1.3.0&request=GetCapabilities
Expected: WMS_Capabilities XML document
```

#### 5. WMTS (Web Map Tile Service)
```
GET /wmts?service=WMTS&version=1.0.0&request=GetCapabilities
Expected: WMTS Capabilities XML document
```

#### 6. STAC (SpatioTemporal Asset Catalog)
```
GET /stac
Expected: { "type": "Catalog", ... }
```

#### 7. Geoservices REST a.k.a. Esri REST API
```
GET /rest/services?f=json
Expected: { "folders": [...], "services": [...] }
```

Tests catalog functionality and service discovery.

#### 8. OData
```
GET /odata/{serviceId}
GET /odata/{serviceId}/{entitySet}?$top=1
```

Tests service document and basic query functionality.

#### 9. Security Configuration
```
GET /admin (expect 401/403)
```

Verifies protected endpoints require authentication.

#### 10. Metrics Endpoint
```
GET /metrics
Expected: Prometheus format with honua_api_* metrics
```

### Usage

```csharp
var gisValidator = new GisEndpointValidationAgent(kernel, llmProvider, logger, httpClient);

var result = await gisValidator.ValidateDeployedServicesAsync(
    new GisValidationRequest
    {
        BaseUrl = "http://honua.example.com",
        ServicesToValidate = new List<string> { "parcels", "roads" },
        TestWfs = true,
        TestWms = true,
        TestWmts = true,
        TestStac = false,
        TestEsriRest = true,
        TestOData = true,
        TestSecurity = true,
        TestFeatureRetrieval = true
    },
    context,
    cancellationToken);

if (result.OverallStatus == EndpointStatus.Failed)
{
    Console.WriteLine($"❌ Validation failed: {result.FailedChecks} checks failed");
    foreach (var check in result.Checks.Where(c => c.Status == EndpointStatus.Failed))
    {
        Console.WriteLine($"  • {check.EndpointType}: {check.Message}");
    }
}
else
{
    Console.WriteLine($"✅ Validation passed: {result.PassedChecks}/{result.Checks.Count} checks successful");
}
```

### Integration with Deployment Validation

The `DeploymentExecutionAgent` now automatically runs GIS endpoint validation after terraform apply:

```csharp
var deploymentAgent = new DeploymentExecutionAgent(
    kernel,
    llmProvider,
    logger,
    gisValidator: new GisEndpointValidationAgent(kernel, llmProvider, logger),
    observabilityValidator: new ObservabilityValidationAgent(kernel, llmProvider, logger)
);

var result = await deploymentAgent.ExecuteDeploymentAsync(
    "docker-compose",
    "./terraform",
    context,
    cancellationToken);

// Deployment includes automatic validation:
// 1. Terraform state check
// 2. Extract deployment URL from outputs
// 3. Wait 30s for service initialization
// 4. GIS endpoint smoke tests (all protocols)
// 5. Observability metrics validation (if Prometheus configured)
```

### Validation Workflow Integration

When used with `ValidationLoopExecutor`, the deployment will automatically retry with remediation on validation failures:

```csharp
var loopExecutor = new ValidationLoopExecutor(logger);

var result = await loopExecutor.ExecuteWithValidationAsync(
    // Execute: Deploy infrastructure
    async ct => await deploymentAgent.ExecuteDeploymentAsync("kubernetes", "./terraform", context, ct),

    // Validate: Comprehensive GIS + observability checks (built into DeploymentExecutionAgent)
    async (deployResult, ct) => new ValidationResult
    {
        Passed = deployResult.Success,
        Failures = deployResult.Success ? new() : new()
        {
            new ValidationFailure
            {
                Category = "Deployment",
                Message = deployResult.Message,
                Severity = "high"
            }
        }
    },

    // Remediate: Rollback on failure
    async (validationResult, ct) =>
    {
        if (!validationResult.Passed)
        {
            await deploymentAgent.DestroyDeploymentAsync("./terraform", context, ct);
            return new RemediationResult
            {
                CanRetry = false,
                ActionTaken = "Rolled back deployment",
                Reasoning = "GIS endpoint validation failed"
            };
        }
        return new RemediationResult { CanRetry = true };
    },

    "Production Deployment",
    cancellationToken
);
```

## Next Steps

1. ✅ ObservabilityValidationAgent created
2. ✅ ObservabilityConfigurationAgent created
3. ✅ GisEndpointValidationAgent created with full protocol support
4. ✅ Integrated with DeploymentExecutionAgent
5. ⏳ Integrate ObservabilityValidationAgent with ValidationLoopExecutor
6. ⏳ Add GitOps reconciliation observability
7. ⏳ Create telemetry aggregation backend
8. ⏳ Build analytics dashboard for deployment health trends
