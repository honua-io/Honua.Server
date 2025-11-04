# Blue-Green and Canary Deployment Design for Honua

## Executive Summary

This document outlines a comprehensive design for implementing blue-green and canary deployment strategies for Honua, including automatic rollback based on telemetry signals. The system enables zero-downtime deployments for both Honua server upgrades and metadata/configuration changes.

**Key Architectural Decision**: Honua's AI consultant and automation tooling **only supports YARP (Yet Another Reverse Proxy)**. While users *can* use alternative proxies (Nginx, Traefik, Envoy, etc.), they will receive no AI assistance for blue-green deployments, canary rollouts, SSL automation, or advanced traffic management with those alternatives.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      Client Requests                         │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                  YARP Proxy Layer (ONLY)                     │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Microsoft YARP (Yet Another Reverse Proxy)          │  │
│  │  - Dynamic routing based on deployment slots          │  │
│  │  - Weighted traffic distribution (canary)             │  │
│  │  - Request shadowing for validation                   │  │
│  │  - Health check integration                           │  │
│  │  - TLS termination with SNI support                   │  │
│  └──────────────────────────────────────────────────────┘  │
└──────────┬────────────────────────────────────┬─────────────┘
           │                                    │
    ┌──────▼─────────┐               ┌─────────▼──────────┐
    │  Blue Slot     │               │   Green Slot       │
    │  (Production)  │               │   (Staging)        │
    │  Port: 5000    │               │   Port: 5001       │
    │  v1.2.3        │               │   v1.2.4           │
    └────────────────┘               └────────────────────┘
           │                                    │
           ▼                                    ▼
    ┌────────────────────────────────────────────────────────┐
    │         Health & Telemetry Aggregation                  │
    │  - Error rate tracking                                  │
    │  - Response time percentiles (p50, p95, p99)            │
    │  - Request success/failure metrics                      │
    │  - Custom health checks (DB connectivity, etc.)         │
    │  - Metadata validation status                           │
    └────────────────────────────────────────────────────────┘
           │
           ▼
    ┌────────────────────────────────────────────────────────┐
    │          Deployment Controller                          │
    │  - Promotion logic (blue ↔ green)                       │
    │  - Canary rollout orchestration                         │
    │  - Automatic rollback triggers                          │
    │  - State persistence (deployment history)               │
    └────────────────────────────────────────────────────────┘
```

## Component Design

### 1. YARP Reverse Proxy (Only Supported Proxy for AI Automation)

**Technology**: Microsoft YARP (Yet Another Reverse Proxy) - **ONLY SUPPORTED OPTION**
- **Only proxy with AI assistance** - Alternatives work but are unsupported
- Native .NET integration
- Dynamic configuration reloading
- High performance, low latency
- Built-in health checks and load balancing
- TLS termination with SNI support

**Key Features**:
```csharp
public class DeploymentSlot
{
    public string Id { get; set; } // "blue" or "green"
    public string BaseUrl { get; set; } // http://localhost:5000
    public DeploymentVersion Version { get; set; }
    public DeploymentStatus Status { get; set; } // Active, Standby, Draining
    public HealthStatus Health { get; set; }
    public int Weight { get; set; } // 0-100 for traffic distribution
}

public enum DeploymentStrategy
{
    BlueGreen,      // Instant cutover
    Canary,         // Gradual rollout
    Shadow          // Mirror traffic for testing
}
```

**Routing Logic**:
```csharp
public class DeploymentRouter
{
    public RouteDestination SelectDestination(
        HttpContext context,
        DeploymentConfig config)
    {
        if (config.Strategy == DeploymentStrategy.BlueGreen)
        {
            return config.ActiveSlot;
        }

        if (config.Strategy == DeploymentStrategy.Canary)
        {
            // Weighted random selection
            var random = Random.Shared.Next(0, 100);
            return random < config.CanaryWeight
                ? config.CanarySlot
                : config.StableSlot;
        }

        // Sticky sessions for stateful requests
        if (context.Request.Headers.TryGetValue("X-Session-Id", out var sessionId))
        {
            return GetStickyDestination(sessionId);
        }

        return config.ActiveSlot;
    }
}
```

### 2. Deployment Controller

**State Machine**:
```
[Stable] ──deploy──> [Deploying] ──health_ok──> [Testing]
                           │
                           └──health_fail──> [Rollback]

[Testing] ──promote──> [Promoting] ──done──> [Stable]
    │
    └──telemetry_alert──> [Rollback]

[Rollback] ──complete──> [Stable]
```

**Core Interface**:
```csharp
public interface IDeploymentController
{
    Task<DeploymentResult> DeployAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken);

    Task<PromotionResult> PromoteToProductionAsync(
        string deploymentId,
        CancellationToken cancellationToken);

    Task<RollbackResult> RollbackAsync(
        string deploymentId,
        RollbackReason reason,
        CancellationToken cancellationToken);

    Task<DeploymentStatus> GetStatusAsync(
        string deploymentId,
        CancellationToken cancellationToken);
}

public class DeploymentRequest
{
    public DeploymentType Type { get; set; } // ServerUpgrade, MetadataChange
    public DeploymentStrategy Strategy { get; set; }
    public string Version { get; set; }
    public Dictionary<string, string> Configuration { get; set; }
    public CanaryConfig? CanaryConfig { get; set; }
    public RollbackPolicy RollbackPolicy { get; set; }
}

public class CanaryConfig
{
    public int InitialWeight { get; set; } // Start at 5%
    public int IncrementWeight { get; set; } // Increase by 10%
    public TimeSpan StageInterval { get; set; } // Every 5 minutes
    public int MaxWeight { get; set; } // Stop at 50% or 100%
    public List<PromotionCriteria> PromotionCriteria { get; set; }
}
```

### 3. Telemetry-Based Health Monitoring

**Metrics Collection**:
```csharp
public class DeploymentHealthMetrics
{
    // Error metrics
    public double ErrorRate { get; set; } // Errors per second
    public double ErrorRateChange { get; set; } // % change from baseline

    // Performance metrics
    public double P50ResponseTime { get; set; }
    public double P95ResponseTime { get; set; }
    public double P99ResponseTime { get; set; }
    public double ResponseTimeDegradation { get; set; } // % slower than baseline

    // Success metrics
    public double SuccessRate { get; set; } // 2xx responses / total
    public double ThroughputRps { get; set; } // Requests per second

    // Custom Honua metrics
    public double MetadataLoadSuccess { get; set; }
    public double DataSourceConnectivity { get; set; }
    public double TileRenderSuccessRate { get; set; }
    public double WfsTransactionSuccess { get; set; }

    // Comparative baseline
    public DeploymentHealthMetrics? Baseline { get; set; }
}
```

**Health Evaluation**:
```csharp
public class HealthEvaluator
{
    public HealthStatus EvaluateDeployment(
        DeploymentHealthMetrics current,
        DeploymentHealthMetrics baseline,
        RollbackPolicy policy)
    {
        // Critical failure - immediate rollback
        if (current.ErrorRate > policy.MaxErrorRate ||
            current.SuccessRate < policy.MinSuccessRate)
        {
            return HealthStatus.Critical;
        }

        // Performance degradation - warning
        if (current.ResponseTimeDegradation > policy.MaxResponseTimeDegradation)
        {
            return HealthStatus.Degraded;
        }

        // Relative degradation from baseline
        if (baseline != null)
        {
            var errorRateIncrease =
                (current.ErrorRate - baseline.ErrorRate) / baseline.ErrorRate;

            if (errorRateIncrease > policy.MaxErrorRateIncrease)
            {
                return HealthStatus.Degraded;
            }
        }

        return HealthStatus.Healthy;
    }
}

public class RollbackPolicy
{
    // Absolute thresholds
    public double MaxErrorRate { get; set; } = 0.05; // 5% error rate
    public double MinSuccessRate { get; set; } = 0.95; // 95% success
    public double MaxResponseTimeDegradation { get; set; } = 0.5; // 50% slower

    // Relative thresholds (vs baseline)
    public double MaxErrorRateIncrease { get; set; } = 2.0; // 2x errors
    public TimeSpan ObservationWindow { get; set; } = TimeSpan.FromMinutes(5);

    // Custom health checks
    public List<string> RequiredHealthChecks { get; set; } = new()
    {
        "metadata",
        "dataSources",
        "schema"
    };

    // Automatic rollback
    public bool AutomaticRollback { get; set; } = true;
    public int ConsecutiveFailuresBeforeRollback { get; set; } = 3;
}
```

### 4. Deployment Strategies

#### Blue-Green Deployment

**Flow**:
1. Deploy new version to inactive slot (green)
2. Run health checks and validation tests
3. Perform limited traffic test (smoke test)
4. Switch traffic instantly from blue to green
5. Monitor metrics for rollback window (5-10 minutes)
6. Keep blue slot warm for instant rollback

**Code**:
```csharp
public async Task<DeploymentResult> BlueGreenDeployAsync(
    DeploymentRequest request,
    CancellationToken cancellationToken)
{
    // 1. Identify inactive slot
    var activeSlot = await GetActiveSlotAsync(cancellationToken);
    var inactiveSlot = activeSlot.Id == "blue" ? "green" : "blue";

    // 2. Deploy to inactive slot
    await DeployToSlotAsync(inactiveSlot, request.Version, cancellationToken);

    // 3. Health check inactive slot
    var health = await WaitForHealthyAsync(inactiveSlot, timeout: TimeSpan.FromMinutes(5), cancellationToken);
    if (!health.IsHealthy)
    {
        return DeploymentResult.Failed("Health check failed");
    }

    // 4. Smoke test with 1% traffic
    await RunSmokeTestAsync(inactiveSlot, cancellationToken);

    // 5. Switch traffic
    await SwitchActiveSlotAsync(inactiveSlot, cancellationToken);

    // 6. Monitor with automatic rollback
    var monitoring = MonitorDeploymentAsync(
        inactiveSlot,
        activeSlot,
        request.RollbackPolicy,
        cancellationToken);

    return new DeploymentResult
    {
        DeploymentId = Guid.NewGuid().ToString(),
        Strategy = DeploymentStrategy.BlueGreen,
        ActiveSlot = inactiveSlot,
        StandbySlot = activeSlot,
        MonitoringTask = monitoring
    };
}
```

#### Canary Deployment

**Flow**:
1. Deploy new version to canary slot
2. Start with 5% traffic to canary
3. Every 5 minutes, increase by 10% if healthy
4. Monitor metrics continuously
5. Rollback on any failure
6. Complete at 100% or configured max (e.g., 50%)

**Progressive Rollout**:
```
Time    Canary Traffic    Status
0:00    5%                Initial deployment
0:05    15%               First increment
0:10    25%               Second increment
0:15    35%               Third increment
0:20    50%               Stop at max or continue
...     ...               ...
1:00    100%              Full rollout complete
```

**Code**:
```csharp
public async Task<DeploymentResult> CanaryDeployAsync(
    DeploymentRequest request,
    CancellationToken cancellationToken)
{
    var canarySlot = await PrepareCanarySlotAsync(request, cancellationToken);
    var stableSlot = await GetActiveSlotAsync(cancellationToken);

    var config = request.CanaryConfig!;
    var currentWeight = config.InitialWeight;

    // Set initial canary weight
    await SetTrafficWeightAsync(canarySlot.Id, currentWeight, cancellationToken);

    // Progressive rollout loop
    while (currentWeight < config.MaxWeight)
    {
        // Wait for stage interval
        await Task.Delay(config.StageInterval, cancellationToken);

        // Collect metrics
        var canaryMetrics = await CollectMetricsAsync(canarySlot, cancellationToken);
        var stableMetrics = await CollectMetricsAsync(stableSlot, cancellationToken);

        // Evaluate health
        var health = _healthEvaluator.EvaluateDeployment(
            canaryMetrics,
            stableMetrics,
            request.RollbackPolicy);

        if (health != HealthStatus.Healthy)
        {
            await RollbackAsync(canarySlot.Id, RollbackReason.HealthCheckFailed, cancellationToken);
            return DeploymentResult.Failed($"Canary failed at {currentWeight}% traffic");
        }

        // Check promotion criteria
        if (!EvaluatePromotionCriteria(config.PromotionCriteria, canaryMetrics, stableMetrics))
        {
            await RollbackAsync(canarySlot.Id, RollbackReason.PromotionCriteriaNotMet, cancellationToken);
            return DeploymentResult.Failed("Promotion criteria not met");
        }

        // Increase traffic
        currentWeight = Math.Min(currentWeight + config.IncrementWeight, config.MaxWeight);
        await SetTrafficWeightAsync(canarySlot.Id, currentWeight, cancellationToken);

        _logger.LogInformation("Canary traffic increased to {Weight}%", currentWeight);
    }

    // Complete rollout
    await PromoteCanaryAsync(canarySlot, stableSlot, cancellationToken);

    return DeploymentResult.Success(canarySlot.Id);
}
```

### 5. Automatic Rollback System

**Rollback Triggers**:
```csharp
public enum RollbackReason
{
    HealthCheckFailed,
    HighErrorRate,
    PerformanceDegradation,
    PromotionCriteriaNotMet,
    ManualTrigger,
    MetadataValidationFailed,
    DataSourceConnectivityLost
}

public class RollbackExecutor
{
    public async Task<RollbackResult> RollbackAsync(
        string deploymentId,
        RollbackReason reason,
        CancellationToken cancellationToken)
    {
        var deployment = await _deploymentStore.GetAsync(deploymentId, cancellationToken);

        _logger.LogWarning(
            "Initiating automatic rollback for deployment {DeploymentId}. Reason: {Reason}",
            deploymentId,
            reason);

        // 1. Immediately switch traffic to stable slot
        await SwitchActiveSlotAsync(deployment.StandbySlot, cancellationToken);

        // 2. Drain connections from failed slot
        await DrainConnectionsAsync(deployment.ActiveSlot, cancellationToken);

        // 3. Stop failed slot
        await StopSlotAsync(deployment.ActiveSlot, cancellationToken);

        // 4. Record rollback event
        await _telemetryClient.TrackEventAsync(new RollbackEvent
        {
            DeploymentId = deploymentId,
            Reason = reason,
            Timestamp = DateTime.UtcNow,
            RollbackDuration = deployment.RollbackDuration
        });

        // 5. Alert on-call team
        await _alertingService.SendAlertAsync(new DeploymentAlert
        {
            Severity = AlertSeverity.High,
            Message = $"Automatic rollback triggered: {reason}",
            DeploymentId = deploymentId
        });

        return new RollbackResult
        {
            Success = true,
            Reason = reason,
            Duration = deployment.RollbackDuration
        };
    }
}
```

## Opinionated Architecture: YARP as Only Supported Proxy

**Decision**: Honua's AI consultant and automation features **only support YARP (Yet Another Reverse Proxy)** for traffic management, deployment orchestration, and routing. Alternative proxies are **unsupported** - users can configure them manually but will not receive AI assistance.

**Why YARP-Only Support**:
- **Native .NET integration** - Same runtime as Honua, zero impedance mismatch
- **Monolithic architecture** - Honua is not a microservices system requiring service mesh complexity
- **Simplicity** - Single proxy technology to learn, configure, test, and maintain
- **Dynamic reconfiguration** - No config file reloads; routes update via C# code
- **Performance** - Native .NET performance without additional proxy hops
- **Sufficient features** - YARP provides everything needed:
  - Dynamic routing and traffic splitting
  - Health checks and load balancing
  - Request/response transformation
  - Metrics and observability integration
  - TLS termination and SNI support

**Alternative Proxies: Unsupported (Manual Configuration Only)**:
- 🚫 **Nginx** - Possible but unsupported: No AI config generation, no blue-green automation
- 🚫 **Traefik** - Possible but unsupported: No AI assistance with Docker labels or dynamic config
- 🚫 **Envoy** - Possible but unsupported: No xDS config generation, no AI help with service mesh
- 🚫 **HAProxy** - Possible but unsupported: No AI-generated config files or health check setup
- 🚫 **Kong** - Possible but unsupported: No AI plugin configuration or rate limiting automation
- 🚫 **Service Mesh (Linkerd/Istio)** - Possible but unsupported: No AI assistance with mesh configuration

**What "Unsupported" Means**:
- AI consultant will not generate configurations for these proxies
- No automated blue-green or canary deployment commands
- No SSL/Let's Encrypt automation via AI
- No health check or telemetry integration assistance
- Users must manually configure and manage these proxies
- Honua server will work behind any proxy, but deployment automation is YARP-specific

**YARP as Single Point of Control**:
```
┌──────────────────────────────────────────┐
│  All Traffic Flows Through YARP          │
│  ┌────────────────────────────────────┐  │
│  │  Traffic Splitting (Canary)        │  │
│  │  Blue-Green Cutover                │  │
│  │  Health Check Routing              │  │
│  │  TLS Termination                   │  │
│  │  Telemetry Collection              │  │
│  │  Request Shadowing                 │  │
│  └────────────────────────────────────┘  │
└──────────────────────────────────────────┘
```

**Future-Proofing**:
If Honua evolves into a microservices architecture, YARP can still serve as the edge proxy while a service mesh handles internal traffic. This decision does not block future architectural evolution.

## Deployment Types

### 1. Server Version Upgrade

**Scenario**: Upgrading Honua from v1.2.3 to v1.2.4

**Process**:
1. Build new Docker image with v1.2.4
2. Deploy to green slot
3. Run integration tests
4. Blue-green cutover or canary rollout
5. Monitor for 10 minutes
6. Keep blue slot for 24 hours before decommission

### 2. Metadata/Configuration Change

**Scenario**: Adding new layer to metadata, changing datasource connections

**Process**:
1. Apply metadata changes to green slot
2. Validate metadata integrity
3. Test layer rendering
4. Canary rollout with 5% → 25% → 100%
5. Monitor error rates and response times
6. Rollback if validation fails

**Special Considerations**:
```csharp
public class MetadataDeploymentValidator
{
    public async Task<ValidationResult> ValidateAsync(
        string slotId,
        CancellationToken cancellationToken)
    {
        // Validate metadata schema
        var schemaValid = await ValidateSchemaAsync(slotId, cancellationToken);
        if (!schemaValid)
        {
            return ValidationResult.Failed("Schema validation failed");
        }

        // Test layer accessibility
        var layersValid = await TestAllLayersAsync(slotId, cancellationToken);
        if (!layersValid)
        {
            return ValidationResult.Failed("Layer accessibility test failed");
        }

        // Verify datasource connectivity
        var datasourcesValid = await TestDatasourcesAsync(slotId, cancellationToken);
        if (!datasourcesValid)
        {
            return ValidationResult.Failed("Datasource connectivity failed");
        }

        return ValidationResult.Success();
    }
}
```

## Implementation Plan

### Phase 1: Foundation (2-3 weeks)
1. Add YARP NuGet package
2. Implement `DeploymentSlot` abstraction
3. Create basic traffic routing logic
4. Add deployment state persistence

### Phase 2: Blue-Green (2 weeks)
1. Implement blue-green controller
2. Add health check integration
3. Build deployment CLI commands
4. Create monitoring dashboard

### Phase 3: Telemetry Integration (2 weeks)
1. Integrate with existing OpenTelemetry
2. Add deployment-specific metrics
3. Implement health evaluator
4. Build automatic rollback logic

### Phase 4: Canary Deployment (2-3 weeks)
1. Implement canary controller
2. Add progressive rollout logic
3. Create promotion criteria evaluation
4. Test with metadata changes

### Phase 5: Observability & Alerting (1-2 weeks)
1. Build deployment dashboard
2. Add Grafana panels for deployment metrics
3. Integrate with alerting (PagerDuty/Slack)
4. Document runbooks

## Configuration Example

```json
{
  "Deployment": {
    "Strategy": "Canary",
    "Slots": {
      "Blue": {
        "BaseUrl": "http://localhost:5000",
        "HealthCheckUrl": "/healthz/ready"
      },
      "Green": {
        "BaseUrl": "http://localhost:5001",
        "HealthCheckUrl": "/healthz/ready"
      }
    },
    "Canary": {
      "InitialWeight": 5,
      "IncrementWeight": 10,
      "StageInterval": "00:05:00",
      "MaxWeight": 50,
      "PromotionCriteria": [
        {
          "Metric": "ErrorRate",
          "Operator": "LessThan",
          "Threshold": 0.02
        },
        {
          "Metric": "P95ResponseTime",
          "Operator": "LessThan",
          "Threshold": 500
        }
      ]
    },
    "RollbackPolicy": {
      "AutomaticRollback": true,
      "MaxErrorRate": 0.05,
      "MaxResponseTimeDegradation": 0.5,
      "ObservationWindow": "00:05:00",
      "ConsecutiveFailuresBeforeRollback": 3
    }
  }
}
```

## CLI Commands

```bash
# Deploy new version
honua deploy --version 1.2.4 --strategy blue-green

# Deploy with canary
honua deploy --version 1.2.4 --strategy canary --max-weight 50

# Deploy metadata change
honua deploy metadata --file metadata.json --strategy canary

# Check deployment status
honua deploy status --deployment-id abc123

# Manual promotion
honua deploy promote --deployment-id abc123

# Manual rollback
honua deploy rollback --deployment-id abc123

# List deployments
honua deploy list --environment production
```

## Monitoring & Observability

**Key Metrics**:
- Deployment success rate
- Rollback frequency
- Time to rollback (target: < 30 seconds)
- Deployment duration
- Traffic distribution accuracy
- Health check status per slot

**Grafana Dashboard Panels**:
1. Traffic distribution (blue vs green)
2. Error rate comparison
3. Response time percentiles
4. Deployment timeline
5. Rollback events
6. Health status per slot

## Testing Strategy

### Unit Tests
- Routing logic
- Health evaluation
- Rollback triggers
- Canary weight calculation

### Integration Tests
- Full blue-green deployment
- Canary rollout simulation
- Automatic rollback scenarios
- Metadata validation

### E2E Tests
- Production-like deployment
- Real traffic patterns
- Failure injection
- Performance validation

## Security Considerations

1. **Slot Isolation**: Each slot should have isolated resources
2. **Secret Management**: Credentials should not leak between slots
3. **RBAC**: Only authorized users can trigger deployments
4. **Audit Logging**: All deployment actions logged
5. **Replay Protection**: Prevent duplicate deployments

## Cost & Resource Management

**Resource Requirements**:
- 2x compute resources during deployment
- Standby slot kept warm (15-20% overhead)
- Temporary storage for deployment artifacts

**Optimization**:
- Graceful connection draining (save memory)
- Lazy slot initialization (on-demand startup)
- Resource quotas per slot

## Conclusion

This design provides a robust, production-grade deployment system for Honua with:
- ✅ Zero-downtime deployments
- ✅ Automatic rollback based on telemetry
- ✅ Support for both server upgrades and metadata changes
- ✅ Flexible strategies (blue-green, canary)
- ✅ Native .NET integration (YARP)
- ✅ Comprehensive observability

**Next Steps**:
1. Review and approve design
2. Create implementation tickets
3. Begin Phase 1 (Foundation)
4. Set up CI/CD integration

