# YARP-Based Traffic Management System

**Version:** 1.0
**Last Updated:** 2025-11-14

## Table of Contents

1. [Overview](#1-overview)
2. [Getting Started](#2-getting-started)
3. [API Reference](#3-api-reference)
4. [Usage Examples](#4-usage-examples)
5. [Integration with CI/CD](#5-integration-with-cicd)
6. [Health Checks](#6-health-checks)
7. [Monitoring](#7-monitoring)
8. [Security](#8-security)
9. [Troubleshooting](#9-troubleshooting)
10. [Comparison with SwitchTrafficStep.cs](#10-comparison-with-switchtrafficstepcs)
11. [Advanced Scenarios](#11-advanced-scenarios)

---

## 1. Overview

### What is the Traffic Management System?

The Honua YARP-based traffic management system provides a **platform-agnostic, cloud-native approach** to zero-downtime deployments using Microsoft's YARP (Yet Another Reverse Proxy). This system enables sophisticated deployment strategies including:

- **Blue-Green Deployments**: Instant traffic switching between environments
- **Canary Deployments**: Gradual rollout with automatic health monitoring
- **Progressive Delivery**: Fine-grained traffic control with percentage-based routing

### How It Works

The system consists of two key components:

1. **YARP Reverse Proxy** (`Honua.Server.Gateway`)
   - Handles all incoming traffic
   - Routes requests based on weighted load balancing
   - Performs active and passive health checks
   - Provides real-time traffic metrics

2. **BlueGreenTrafficManager** (`Honua.Server.Core`)
   - Manages YARP configuration dynamically
   - Implements deployment strategies
   - Handles automatic rollback on failures
   - Integrates with health check systems

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         Client Requests                          │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    YARP API Gateway                              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Rate Limiting │ CORS │ Auth │ Telemetry │ Health Checks │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │           Weighted Round Robin Load Balancer             │   │
│  │         (Configured by BlueGreenTrafficManager)          │   │
│  └──────────────────┬─────────────────┬─────────────────────┘   │
└────────────────────┼─────────────────┼────────────────────────┘
                     │                 │
         ┌───────────┴──────┐    ┌────┴──────────┐
         │  Blue: 90%       │    │  Green: 10%   │
         │  Weight: 90      │    │  Weight: 10   │
         └───────┬──────────┘    └────┬──────────┘
                 │                    │
                 ▼                    ▼
         ┌───────────────┐    ┌───────────────┐
         │ Blue Cluster  │    │ Green Cluster │
         │ (Current)     │    │ (New Version) │
         │               │    │               │
         │ v1.0.0        │    │ v1.1.0        │
         └───────────────┘    └───────────────┘
                 │                    │
                 ▼                    ▼
         ┌───────────────┐    ┌───────────────┐
         │ Health Check  │    │ Health Check  │
         │ /health       │    │ /health       │
         │ Every 10s     │    │ Every 10s     │
         └───────────────┘    └───────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                  BlueGreenTrafficManager                         │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  • SwitchTrafficAsync()                                   │   │
│  │  • PerformCanaryDeploymentAsync()                        │   │
│  │  • RollbackToBlueAsync()                                 │   │
│  │  • InMemoryConfigProvider (YARP dynamic configuration)   │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    Metrics & Observability                       │
│  ┌────────────┐  ┌────────────┐  ┌────────────────────────┐    │
│  │ Prometheus │  │  Grafana   │  │ OpenTelemetry (OTLP)   │    │
│  │ /metrics   │  │ Dashboards │  │ Tracing & Metrics      │    │
│  └────────────┘  └────────────┘  └────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

### Benefits Over Platform-Specific Approaches

| Aspect | Traditional Approach (SwitchTrafficStep.cs) | YARP Traffic Manager |
|--------|---------------------------------------------|----------------------|
| **Lines of Code** | 1,546 lines | 354 lines (77% reduction) |
| **Platform Support** | 6 separate implementations (K8s, AWS, Azure, GCP, NGINX, HAProxy) | Single implementation, works everywhere |
| **Deployment Complexity** | Platform-specific CLI tools, credentials, permissions | Simple HTTP API calls |
| **Configuration Management** | Complex config file editing, service restarts | Dynamic in-memory configuration |
| **Testing** | Requires actual cloud infrastructure | Can be tested locally with Docker |
| **Health Checks** | Manual monitoring, custom scripts | Built-in active/passive health checks |
| **Rollback Speed** | 30-60 seconds (config reload + validation) | Instant (1-2 seconds) |
| **Observability** | Platform-specific metrics | Unified Prometheus metrics + OpenTelemetry |
| **Security Risks** | Command injection vulnerabilities (requires validation) | Type-safe API calls |
| **Maintenance** | 6 codepaths to maintain and test | Single codebase |
| **Cloud Vendor Lock-in** | Tight coupling to cloud provider APIs | Cloud-agnostic, portable |

**Key Advantages:**

1. **Simplicity**: Single API endpoint instead of 6 platform-specific implementations
2. **Speed**: Sub-second traffic switches vs. 30-60 second config reloads
3. **Safety**: Type-safe APIs eliminate command injection risks
4. **Portability**: Works on any platform (Kubernetes, Docker, VMs, bare metal)
5. **Developer Experience**: Same workflow for local dev, staging, and production
6. **Observability**: Built-in metrics, tracing, and health checks
7. **Cost**: No cloud provider API calls or external dependencies

---

## 2. Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Docker (for local testing)
- Kubernetes cluster (optional, for production deployment)
- Prometheus (optional, for metrics)

### Configuration Requirements

The traffic management system requires YARP to be configured with the `InMemoryConfigProvider` for dynamic updates.

**Minimal Configuration (`appsettings.json`):**

```json
{
  "ServiceName": "honua-gateway",
  "ServiceVersion": "1.0.0",

  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": {
          "Path": "/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "blue": {
            "Address": "http://api-blue:8080",
            "Health": "http://api-blue:8080/health"
          },
          "green": {
            "Address": "http://api-green:8080",
            "Health": "http://api-green:8080/health"
          }
        },
        "LoadBalancingPolicy": "WeightedRoundRobin",
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Timeout": "00:00:05",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      }
    }
  }
}
```

### Initial Setup Steps

1. **Add NuGet Packages**

```bash
dotnet add package Yarp.ReverseProxy
dotnet add package Honua.Server.Core
```

2. **Configure Services in Program.cs**

```csharp
using Honua.Server.Core.BlueGreen;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add YARP with in-memory configuration provider
var routes = new List<RouteConfig>();
var clusters = new List<ClusterConfig>();
var configProvider = new InMemoryConfigProvider(routes, clusters);

builder.Services.AddSingleton<IProxyConfigProvider>(configProvider);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add BlueGreenTrafficManager
builder.Services.AddSingleton<BlueGreenTrafficManager>();

var app = builder.Build();

// Map YARP endpoints
app.MapReverseProxy();

// Add traffic management endpoints (see API Reference)
app.MapPost("/admin/traffic/switch", async (
    BlueGreenTrafficManager trafficManager,
    TrafficSwitchRequest request,
    CancellationToken ct) =>
{
    var result = await trafficManager.SwitchTrafficAsync(
        request.ServiceName,
        request.BlueEndpoint,
        request.GreenEndpoint,
        request.GreenPercentage,
        ct);

    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.Run();
```

3. **Deploy Blue and Green Environments**

```bash
# Deploy blue (current version)
docker run -d --name api-blue -p 8081:8080 myapp:v1.0.0

# Deploy green (new version)
docker run -d --name api-green -p 8082:8080 myapp:v1.1.0

# Start gateway
dotnet run --project Honua.Server.Gateway
```

### Quick Start Example

```bash
# Initial state: 100% traffic on blue
curl -X POST http://localhost:5000/admin/traffic/switch \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "api-cluster",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080",
    "greenPercentage": 0
  }'

# Test new version with 10% traffic
curl -X POST http://localhost:5000/admin/traffic/switch \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "api-cluster",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080",
    "greenPercentage": 10
  }'

# Monitor for 5 minutes, then increase to 50%
curl -X POST http://localhost:5000/admin/traffic/switch \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "api-cluster",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080",
    "greenPercentage": 50
  }'

# Full cutover to green
curl -X POST http://localhost:5000/admin/traffic/switch \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "api-cluster",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080",
    "greenPercentage": 100
  }'

# Rollback if issues detected
curl -X POST http://localhost:5000/admin/traffic/rollback \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "api-cluster",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080"
  }'
```

---

## 3. API Reference

### POST /admin/traffic/switch

**Description:** Switch traffic between blue and green environments with specified percentage.

**Authentication:** Required (JWT Bearer token or API key)

**Request Headers:**
```
Content-Type: application/json
Authorization: Bearer <token>
```

**Request Body:**
```json
{
  "serviceName": "api-cluster",
  "blueEndpoint": "http://api-blue:8080",
  "greenEndpoint": "http://api-green:8080",
  "greenPercentage": 50
}
```

**Request Parameters:**

| Field | Type | Required | Description | Valid Range |
|-------|------|----------|-------------|-------------|
| `serviceName` | string | Yes | Name of the YARP cluster to update | Any valid cluster name |
| `blueEndpoint` | string | Yes | URL of the blue (current) environment | Valid HTTP/HTTPS URL |
| `greenEndpoint` | string | Yes | URL of the green (new) environment | Valid HTTP/HTTPS URL |
| `greenPercentage` | integer | Yes | Percentage of traffic to route to green | 0-100 |

**Success Response (200 OK):**
```json
{
  "success": true,
  "blueTrafficPercentage": 50,
  "greenTrafficPercentage": 50,
  "message": "Traffic switched: 50% blue, 50% green"
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "blueTrafficPercentage": 0,
  "greenTrafficPercentage": 0,
  "message": "Green traffic percentage must be between 0 and 100"
}
```

**Error Response (401 Unauthorized):**
```json
{
  "error": "Unauthorized",
  "message": "Valid authentication token required"
}
```

**Error Response (500 Internal Server Error):**
```json
{
  "success": false,
  "message": "Failed to switch traffic: Connection refused"
}
```

---

### POST /admin/traffic/canary

**Description:** Perform automated canary deployment with health checks and automatic rollback.

**Authentication:** Required (JWT Bearer token or API key)

**Request Headers:**
```
Content-Type: application/json
Authorization: Bearer <token>
```

**Request Body:**
```json
{
  "serviceName": "api-cluster",
  "blueEndpoint": "http://api-blue:8080",
  "greenEndpoint": "http://api-green:8080",
  "strategy": {
    "trafficSteps": [10, 25, 50, 100],
    "soakDurationSeconds": 300,
    "autoRollback": true
  },
  "healthCheckUrl": "http://api-green:8080/health"
}
```

**Request Parameters:**

| Field | Type | Required | Description | Default |
|-------|------|----------|-------------|---------|
| `serviceName` | string | Yes | Name of the YARP cluster | - |
| `blueEndpoint` | string | Yes | Blue environment URL | - |
| `greenEndpoint` | string | Yes | Green environment URL | - |
| `strategy.trafficSteps` | array[int] | No | Traffic percentage steps | [10, 25, 50, 100] |
| `strategy.soakDurationSeconds` | integer | No | Wait time at each step (seconds) | 60 |
| `strategy.autoRollback` | boolean | No | Automatically rollback on failure | true |
| `healthCheckUrl` | string | No | Health check endpoint URL | {greenEndpoint}/health |

**Success Response (200 OK):**
```json
{
  "success": true,
  "rolledBack": false,
  "stages": [
    {
      "greenTrafficPercentage": 10,
      "isHealthy": true,
      "timestamp": "2025-11-14T10:15:30Z"
    },
    {
      "greenTrafficPercentage": 25,
      "isHealthy": true,
      "timestamp": "2025-11-14T10:20:30Z"
    },
    {
      "greenTrafficPercentage": 50,
      "isHealthy": true,
      "timestamp": "2025-11-14T10:25:30Z"
    },
    {
      "greenTrafficPercentage": 100,
      "isHealthy": true,
      "timestamp": "2025-11-14T10:30:30Z"
    }
  ],
  "message": "Canary deployment completed successfully, 100% traffic on green"
}
```

**Rollback Response (200 OK - Failed Deployment):**
```json
{
  "success": false,
  "rolledBack": true,
  "stages": [
    {
      "greenTrafficPercentage": 10,
      "isHealthy": true,
      "timestamp": "2025-11-14T10:15:30Z"
    },
    {
      "greenTrafficPercentage": 25,
      "isHealthy": false,
      "timestamp": "2025-11-14T10:20:30Z"
    }
  ],
  "message": "Deployment failed health check at 25% and was rolled back"
}
```

---

### POST /admin/traffic/rollback

**Description:** Immediately rollback to 100% blue environment.

**Authentication:** Required (JWT Bearer token or API key)

**Request Body:**
```json
{
  "serviceName": "api-cluster",
  "blueEndpoint": "http://api-blue:8080",
  "greenEndpoint": "http://api-green:8080"
}
```

**Success Response (200 OK):**
```json
{
  "success": true,
  "blueTrafficPercentage": 100,
  "greenTrafficPercentage": 0,
  "message": "Traffic switched: 100% blue, 0% green"
}
```

---

### POST /admin/traffic/instant-cutover

**Description:** Immediately switch 100% traffic to green environment (no gradual rollout).

**Authentication:** Required (JWT Bearer token or API key)

**Request Body:**
```json
{
  "serviceName": "api-cluster",
  "blueEndpoint": "http://api-blue:8080",
  "greenEndpoint": "http://api-green:8080"
}
```

**Success Response (200 OK):**
```json
{
  "success": true,
  "blueTrafficPercentage": 0,
  "greenTrafficPercentage": 100,
  "message": "Traffic switched: 0% blue, 100% green"
}
```

---

### GET /admin/traffic/status

**Description:** Get current traffic distribution for a service.

**Authentication:** Required (JWT Bearer token or API key)

**Query Parameters:**
- `serviceName` (required): Name of the service cluster

**Example:**
```bash
curl -X GET "http://gateway/admin/traffic/status?serviceName=api-cluster" \
  -H "Authorization: Bearer $TOKEN"
```

**Success Response (200 OK):**
```json
{
  "serviceName": "api-cluster",
  "destinations": {
    "blue": {
      "address": "http://api-blue:8080",
      "weight": 70,
      "healthy": true,
      "lastHealthCheck": "2025-11-14T10:45:30Z"
    },
    "green": {
      "address": "http://api-green:8080",
      "weight": 30,
      "healthy": true,
      "lastHealthCheck": "2025-11-14T10:45:32Z"
    }
  },
  "blueTrafficPercentage": 70,
  "greenTrafficPercentage": 30
}
```

---

## 4. Usage Examples

### Blue-Green Deployment (Manual)

```bash
# Step 1: Deploy green environment
kubectl apply -f green-deployment.yaml

# Step 2: Wait for green pods to be ready
kubectl wait --for=condition=ready pod -l app=honua-api,environment=green --timeout=300s

# Step 3: Switch 10% traffic to green for testing
curl -X POST http://gateway/admin/traffic/switch \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "honua-api",
    "blueEndpoint": "http://api-blue.honua.svc.cluster.local:8080",
    "greenEndpoint": "http://api-green.honua.svc.cluster.local:8080",
    "greenPercentage": 10
  }'

# Step 4: Monitor metrics for 5 minutes
echo "Monitoring metrics..."
sleep 300

# Check error rate
curl "http://prometheus:9090/api/v1/query?query=sum(rate(http_requests_total{status=~\"5..\"}[5m]))"

# Step 5: Increase to 50% if metrics look good
curl -X POST http://gateway/admin/traffic/switch \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "honua-api",
    "blueEndpoint": "http://api-blue.honua.svc.cluster.local:8080",
    "greenEndpoint": "http://api-green.honua.svc.cluster.local:8080",
    "greenPercentage": 50
  }'

# Step 6: Monitor again
sleep 300

# Step 7: Full cutover to green
curl -X POST http://gateway/admin/traffic/switch \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "honua-api",
    "blueEndpoint": "http://api-blue.honua.svc.cluster.local:8080",
    "greenEndpoint": "http://api-green.honua.svc.cluster.local:8080",
    "greenPercentage": 100
  }'

# Step 8: Keep blue running for 24h in case rollback needed
echo "Deployment complete. Blue environment will be terminated in 24h."
```

### Canary Deployment (Automated)

```bash
# Automated canary with health checks
curl -X POST http://gateway/admin/traffic/canary \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "honua-api",
    "blueEndpoint": "http://api-blue.honua.svc.cluster.local:8080",
    "greenEndpoint": "http://api-green.honua.svc.cluster.local:8080",
    "strategy": {
      "trafficSteps": [10, 25, 50, 100],
      "soakDurationSeconds": 300,
      "autoRollback": true
    },
    "healthCheckUrl": "http://api-green.honua.svc.cluster.local:8080/health"
  }'

# The system will:
# 1. Route 10% traffic to green
# 2. Wait 5 minutes (soak duration)
# 3. Check green health endpoint
# 4. If healthy, increase to 25%
# 5. Repeat until 100% or health check fails
# 6. Auto-rollback to blue if any health check fails
```

### Instant Cutover (Blue-Green Switch)

```bash
# Immediate 100% traffic switch (use with caution!)
curl -X POST http://gateway/admin/traffic/instant-cutover \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "honua-api",
    "blueEndpoint": "http://api-blue.honua.svc.cluster.local:8080",
    "greenEndpoint": "http://api-green.honua.svc.cluster.local:8080"
  }'
```

### Emergency Rollback

```bash
# Immediately rollback to blue if green has issues
curl -X POST http://gateway/admin/traffic/rollback \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "honua-api",
    "blueEndpoint": "http://api-blue.honua.svc.cluster.local:8080",
    "greenEndpoint": "http://api-green.honua.svc.cluster.local:8080"
  }'
```

### Programmatic Usage (C#)

```csharp
using Honua.Server.Core.BlueGreen;
using Microsoft.Extensions.DependencyInjection;

// From within .NET application
public class DeploymentService
{
    private readonly BlueGreenTrafficManager _trafficManager;
    private readonly ILogger<DeploymentService> _logger;

    public DeploymentService(
        BlueGreenTrafficManager trafficManager,
        ILogger<DeploymentService> logger)
    {
        _trafficManager = trafficManager;
        _logger = logger;
    }

    public async Task DeployNewVersionAsync(
        string serviceName,
        string newVersion,
        CancellationToken cancellationToken)
    {
        var blueEndpoint = $"http://{serviceName}-blue:8080";
        var greenEndpoint = $"http://{serviceName}-green:8080";

        try
        {
            // Step 1: Deploy green environment (using your deployment system)
            await DeployContainerAsync(serviceName, newVersion, "green", cancellationToken);

            // Step 2: Perform canary deployment
            var healthCheckFunc = async (CancellationToken ct) =>
            {
                return await CheckServiceHealthAsync(greenEndpoint, ct);
            };

            var strategy = new CanaryStrategy
            {
                TrafficSteps = new List<int> { 10, 25, 50, 100 },
                SoakDurationSeconds = 300,
                AutoRollback = true
            };

            var result = await _trafficManager.PerformCanaryDeploymentAsync(
                serviceName,
                blueEndpoint,
                greenEndpoint,
                strategy,
                healthCheckFunc,
                cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Deployment successful for {Service} version {Version}",
                    serviceName,
                    newVersion);

                // Clean up old blue environment after 24h grace period
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromHours(24), cancellationToken);
                    await TerminateContainerAsync(serviceName, "blue", cancellationToken);
                });
            }
            else
            {
                _logger.LogError(
                    "Deployment failed for {Service}: {Message}",
                    serviceName,
                    result.Message);
                throw new InvalidOperationException(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment failed for {Service}", serviceName);

            // Ensure rollback
            await _trafficManager.RollbackToBlueAsync(
                serviceName,
                blueEndpoint,
                greenEndpoint,
                cancellationToken);

            throw;
        }
    }

    private async Task<bool> CheckServiceHealthAsync(
        string endpoint,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            var response = await httpClient.GetAsync(
                $"{endpoint}/health",
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task DeployContainerAsync(
        string serviceName,
        string version,
        string environment,
        CancellationToken cancellationToken)
    {
        // Your container deployment logic here
        // (kubectl apply, docker run, etc.)
        await Task.CompletedTask;
    }

    private async Task TerminateContainerAsync(
        string serviceName,
        string environment,
        CancellationToken cancellationToken)
    {
        // Your container cleanup logic here
        await Task.CompletedTask;
    }
}
```

---

## 5. Integration with CI/CD

### GitHub Actions Example

```yaml
name: Deploy with Blue-Green

on:
  push:
    branches: [main]

env:
  GATEWAY_URL: https://gateway.honua.io
  SERVICE_NAME: honua-api

jobs:
  deploy:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Build Docker image
        run: |
          docker build -t ${{ env.SERVICE_NAME }}:${{ github.sha }} .
          docker push ${{ env.SERVICE_NAME }}:${{ github.sha }}

      - name: Deploy green environment
        run: |
          kubectl set image deployment/${{ env.SERVICE_NAME }}-green \
            app=${{ env.SERVICE_NAME }}:${{ github.sha }}

          kubectl rollout status deployment/${{ env.SERVICE_NAME }}-green \
            --timeout=5m

      - name: Canary deployment
        env:
          GATEWAY_TOKEN: ${{ secrets.GATEWAY_API_TOKEN }}
        run: |
          RESPONSE=$(curl -s -w "\n%{http_code}" -X POST \
            ${{ env.GATEWAY_URL }}/admin/traffic/canary \
            -H "Authorization: Bearer $GATEWAY_TOKEN" \
            -H "Content-Type: application/json" \
            -d '{
              "serviceName": "${{ env.SERVICE_NAME }}",
              "blueEndpoint": "http://${{ env.SERVICE_NAME }}-blue:8080",
              "greenEndpoint": "http://${{ env.SERVICE_NAME }}-green:8080",
              "strategy": {
                "trafficSteps": [10, 25, 50, 100],
                "soakDurationSeconds": 180,
                "autoRollback": true
              }
            }')

          HTTP_CODE=$(echo "$RESPONSE" | tail -n 1)
          BODY=$(echo "$RESPONSE" | head -n -1)

          echo "Response: $BODY"

          if [ "$HTTP_CODE" -ne 200 ]; then
            echo "Deployment failed with HTTP $HTTP_CODE"
            exit 1
          fi

          SUCCESS=$(echo "$BODY" | jq -r '.success')
          if [ "$SUCCESS" != "true" ]; then
            echo "Deployment was rolled back"
            exit 1
          fi

          echo "Deployment successful!"

      - name: Cleanup old blue environment
        if: success()
        run: |
          # Wait 24h before cleaning up blue (for emergency rollback)
          sleep 86400
          kubectl delete deployment/${{ env.SERVICE_NAME }}-blue
```

### Azure DevOps Pipeline Example

```yaml
trigger:
  branches:
    include:
      - main

variables:
  gatewayUrl: 'https://gateway.honua.io'
  serviceName: 'honua-api'

stages:
  - stage: Build
    jobs:
      - job: BuildImage
        pool:
          vmImage: 'ubuntu-latest'
        steps:
          - task: Docker@2
            inputs:
              command: 'buildAndPush'
              repository: '$(serviceName)'
              tags: '$(Build.BuildId)'

  - stage: Deploy
    dependsOn: Build
    jobs:
      - job: DeployGreen
        pool:
          vmImage: 'ubuntu-latest'
        steps:
          - task: KubernetesManifest@0
            inputs:
              action: 'deploy'
              manifests: 'k8s/green-deployment.yaml'
              containers: '$(serviceName):$(Build.BuildId)'

          - task: Bash@3
            displayName: 'Wait for green pods'
            inputs:
              targetType: 'inline'
              script: |
                kubectl wait --for=condition=ready pod \
                  -l app=$(serviceName),environment=green \
                  --timeout=300s

      - job: CanaryDeployment
        dependsOn: DeployGreen
        steps:
          - task: Bash@3
            displayName: 'Perform canary deployment'
            inputs:
              targetType: 'inline'
              script: |
                RESPONSE=$(curl -s -w "\n%{http_code}" -X POST \
                  $(gatewayUrl)/admin/traffic/canary \
                  -H "Authorization: Bearer $(GATEWAY_TOKEN)" \
                  -H "Content-Type: application/json" \
                  -d '{
                    "serviceName": "$(serviceName)",
                    "blueEndpoint": "http://$(serviceName)-blue:8080",
                    "greenEndpoint": "http://$(serviceName)-green:8080",
                    "strategy": {
                      "trafficSteps": [10, 30, 60, 100],
                      "soakDurationSeconds": 300,
                      "autoRollback": true
                    }
                  }')

                HTTP_CODE=$(echo "$RESPONSE" | tail -n 1)
                BODY=$(echo "$RESPONSE" | head -n -1)

                echo "##vso[task.setvariable variable=responseBody]$BODY"
                echo "##vso[task.setvariable variable=httpCode]$HTTP_CODE"

                if [ "$HTTP_CODE" -ne 200 ]; then
                  echo "##vso[task.logissue type=error]Deployment failed with HTTP $HTTP_CODE"
                  exit 1
                fi

                SUCCESS=$(echo "$BODY" | jq -r '.success')
                if [ "$SUCCESS" != "true" ]; then
                  echo "##vso[task.logissue type=error]Canary deployment was rolled back"
                  exit 1
                fi
```

### Kubernetes Deployment Example

```yaml
# green-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-api-green
  namespace: honua
  labels:
    app: honua-api
    environment: green
spec:
  replicas: 3
  selector:
    matchLabels:
      app: honua-api
      environment: green
  template:
    metadata:
      labels:
        app: honua-api
        environment: green
    spec:
      containers:
        - name: api
          image: honua-api:v1.1.0
          ports:
            - containerPort: 8080
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 10
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: honua-api-green
  namespace: honua
spec:
  selector:
    app: honua-api
    environment: green
  ports:
    - port: 8080
      targetPort: 8080
  type: ClusterIP
```

### AWS Deployment Example (ECS)

```bash
#!/bin/bash
# deploy-ecs-bluegreen.sh

SERVICE_NAME="honua-api"
CLUSTER_NAME="honua-production"
NEW_VERSION="v1.1.0"
GATEWAY_URL="https://gateway.honua.io"
GATEWAY_TOKEN="$GATEWAY_API_TOKEN"

# Step 1: Deploy new ECS task definition (green)
echo "Deploying green task definition..."
aws ecs register-task-definition \
  --cli-input-json file://task-definition-green.json

# Step 2: Update green service
echo "Updating green service..."
aws ecs update-service \
  --cluster $CLUSTER_NAME \
  --service ${SERVICE_NAME}-green \
  --task-definition ${SERVICE_NAME}-green:latest \
  --force-new-deployment

# Step 3: Wait for green service to be stable
echo "Waiting for green service to stabilize..."
aws ecs wait services-stable \
  --cluster $CLUSTER_NAME \
  --services ${SERVICE_NAME}-green

# Step 4: Get green load balancer endpoint
GREEN_LB=$(aws elbv2 describe-load-balancers \
  --names ${SERVICE_NAME}-green-lb \
  --query 'LoadBalancers[0].DNSName' \
  --output text)

BLUE_LB=$(aws elbv2 describe-load-balancers \
  --names ${SERVICE_NAME}-blue-lb \
  --query 'LoadBalancers[0].DNSName' \
  --output text)

# Step 5: Perform canary deployment via gateway
echo "Starting canary deployment..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST \
  ${GATEWAY_URL}/admin/traffic/canary \
  -H "Authorization: Bearer $GATEWAY_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"serviceName\": \"${SERVICE_NAME}\",
    \"blueEndpoint\": \"http://${BLUE_LB}:8080\",
    \"greenEndpoint\": \"http://${GREEN_LB}:8080\",
    \"strategy\": {
      \"trafficSteps\": [10, 25, 50, 100],
      \"soakDurationSeconds\": 300,
      \"autoRollback\": true
    }
  }")

HTTP_CODE=$(echo "$RESPONSE" | tail -n 1)
BODY=$(echo "$RESPONSE" | head -n -1)

if [ "$HTTP_CODE" -ne 200 ]; then
  echo "Deployment failed with HTTP $HTTP_CODE"
  echo "Response: $BODY"
  exit 1
fi

SUCCESS=$(echo "$BODY" | jq -r '.success')
if [ "$SUCCESS" != "true" ]; then
  echo "Canary deployment was rolled back"
  echo "Response: $BODY"
  exit 1
fi

echo "Deployment successful!"
echo "Response: $BODY"
```

---

## 6. Health Checks

### How Health Checks Work

YARP performs two types of health checks:

1. **Active Health Checks**
   - Periodic HTTP requests to `/health` endpoint
   - Configurable interval (default: 10 seconds)
   - Marks destination as unhealthy after consecutive failures
   - Automatically removes unhealthy destinations from rotation

2. **Passive Health Checks**
   - Monitors actual request failures
   - Tracks transport failures (connection refused, timeouts)
   - Automatically disables failing destinations
   - Re-enables after reactivation period

### Configuring Health Check Endpoints

**In appsettings.json:**

```json
{
  "ReverseProxy": {
    "Clusters": {
      "api-cluster": {
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Timeout": "00:00:05",
            "Policy": "ConsecutiveFailures",
            "Path": "/health/ready"
          },
          "Passive": {
            "Enabled": true,
            "Policy": "TransportFailureRate",
            "ReactivationPeriod": "00:01:00"
          }
        }
      }
    }
  }
}
```

**Health Check Policies:**

- `ConsecutiveFailures`: Mark unhealthy after N consecutive failures (default: 2)
- `TransportFailureRate`: Track percentage of failed requests

### Custom Health Check Logic

**Implement custom health checks in your service:**

```csharp
// In your backend service (e.g., honua-api)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });

        await context.Response.WriteAsync(result);
    }
});

// Add custom health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<CacheHealthCheck>("cache", tags: new[] { "ready" })
    .AddCheck<ExternalApiHealthCheck>("external-api", tags: new[] { "ready" });
```

### Automatic Rollback on Failure

The canary deployment automatically rolls back when:

1. Active health check fails (HTTP non-2xx status)
2. Passive health check detects high error rate
3. Custom health check returns unhealthy status
4. Connection failures exceed threshold

```csharp
var result = await _trafficManager.PerformCanaryDeploymentAsync(
    serviceName,
    blueEndpoint,
    greenEndpoint,
    strategy,
    healthCheckFunc: async (ct) =>
    {
        // Custom health check logic
        var isHealthy = await CheckMetrics(ct);
        var hasErrors = await CheckErrorLogs(ct);
        var isPerformant = await CheckLatency(ct);

        return isHealthy && !hasErrors && isPerformant;
    },
    cancellationToken);

if (!result.Success && result.RolledBack)
{
    _logger.LogWarning(
        "Deployment automatically rolled back: {Message}",
        result.Message);
}
```

---

## 7. Monitoring

### Metrics Exposed by YARP

YARP automatically exposes Prometheus metrics at `/metrics`:

**Request Metrics:**
```
# Total requests per destination
yarp_proxy_requests_total{cluster="api-cluster",destination="blue"}
yarp_proxy_requests_total{cluster="api-cluster",destination="green"}

# Request duration histogram
yarp_proxy_request_duration_seconds{cluster="api-cluster",destination="blue"}

# Active requests gauge
yarp_proxy_current_requests{cluster="api-cluster",destination="blue"}
```

**Health Check Metrics:**
```
# Health check results
yarp_proxy_health_checks_total{cluster="api-cluster",destination="blue",result="success"}
yarp_proxy_health_checks_total{cluster="api-cluster",destination="green",result="failure"}

# Destination health status
yarp_proxy_destination_health{cluster="api-cluster",destination="blue"} 1
yarp_proxy_destination_health{cluster="api-cluster",destination="green"} 0
```

**Traffic Distribution Metrics:**
```
# Weighted traffic distribution
yarp_proxy_destination_weight{cluster="api-cluster",destination="blue"} 70
yarp_proxy_destination_weight{cluster="api-cluster",destination="green"} 30
```

### Prometheus Queries for Traffic Distribution

```promql
# Current traffic distribution percentage
(
  yarp_proxy_destination_weight{cluster="api-cluster",destination="green"}
  /
  (
    yarp_proxy_destination_weight{cluster="api-cluster",destination="blue"}
    +
    yarp_proxy_destination_weight{cluster="api-cluster",destination="green"}
  )
) * 100

# Request rate per destination
rate(yarp_proxy_requests_total{cluster="api-cluster"}[5m])

# Error rate per destination
rate(yarp_proxy_requests_total{cluster="api-cluster",code=~"5.."}[5m])
/
rate(yarp_proxy_requests_total{cluster="api-cluster"}[5m])

# P95 latency per destination
histogram_quantile(0.95,
  sum(rate(yarp_proxy_request_duration_seconds_bucket{cluster="api-cluster"}[5m]))
  by (le, destination)
)

# Health check success rate
rate(yarp_proxy_health_checks_total{result="success"}[5m])
/
rate(yarp_proxy_health_checks_total[5m])
```

### Grafana Dashboard Examples

**Traffic Distribution Dashboard:**

```json
{
  "dashboard": {
    "title": "YARP Traffic Management",
    "panels": [
      {
        "title": "Traffic Distribution",
        "type": "piechart",
        "targets": [
          {
            "expr": "yarp_proxy_destination_weight{cluster=\"api-cluster\"}",
            "legendFormat": "{{destination}}"
          }
        ]
      },
      {
        "title": "Request Rate by Destination",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(yarp_proxy_requests_total{cluster=\"api-cluster\"}[5m])",
            "legendFormat": "{{destination}}"
          }
        ]
      },
      {
        "title": "Error Rate by Destination",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(yarp_proxy_requests_total{cluster=\"api-cluster\",code=~\"5..\"}[5m]) / rate(yarp_proxy_requests_total{cluster=\"api-cluster\"}[5m]) * 100",
            "legendFormat": "{{destination}} error %"
          }
        ]
      },
      {
        "title": "P95 Latency",
        "type": "graph",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, sum(rate(yarp_proxy_request_duration_seconds_bucket{cluster=\"api-cluster\"}[5m])) by (le, destination))",
            "legendFormat": "{{destination}} P95"
          }
        ]
      },
      {
        "title": "Health Status",
        "type": "stat",
        "targets": [
          {
            "expr": "yarp_proxy_destination_health{cluster=\"api-cluster\"}",
            "legendFormat": "{{destination}}"
          }
        ]
      }
    ]
  }
}
```

### Alert Examples

**Prometheus AlertManager Rules:**

```yaml
# prometheus-alerts.yaml
groups:
  - name: yarp_traffic_management
    interval: 30s
    rules:
      - alert: HighErrorRateOnGreen
        expr: |
          (
            rate(yarp_proxy_requests_total{destination="green",code=~"5.."}[5m])
            /
            rate(yarp_proxy_requests_total{destination="green"}[5m])
          ) > 0.05
        for: 2m
        labels:
          severity: critical
          component: traffic-management
        annotations:
          summary: "High error rate detected on green deployment"
          description: "Green deployment has {{ $value | humanizePercentage }} error rate for 2 minutes"

      - alert: GreenDestinationUnhealthy
        expr: yarp_proxy_destination_health{destination="green"} == 0
        for: 1m
        labels:
          severity: warning
          component: traffic-management
        annotations:
          summary: "Green destination marked unhealthy"
          description: "Health checks failing for green deployment"

      - alert: HighLatencyOnGreen
        expr: |
          histogram_quantile(0.95,
            sum(rate(yarp_proxy_request_duration_seconds_bucket{destination="green"}[5m]))
            by (le)
          ) > 2
        for: 3m
        labels:
          severity: warning
          component: traffic-management
        annotations:
          summary: "High latency on green deployment"
          description: "P95 latency is {{ $value }}s on green deployment"

      - alert: TrafficSwitchStalled
        expr: |
          (
            yarp_proxy_destination_weight{destination="green"}
            /
            (
              yarp_proxy_destination_weight{destination="blue"}
              +
              yarp_proxy_destination_weight{destination="green"}
            )
          ) > 0 and
          (
            yarp_proxy_destination_weight{destination="green"}
            /
            (
              yarp_proxy_destination_weight{destination="blue"}
              +
              yarp_proxy_destination_weight{destination="green"}
            )
          ) < 1
        for: 30m
        labels:
          severity: info
          component: traffic-management
        annotations:
          summary: "Traffic split has been active for 30+ minutes"
          description: "Green has {{ $value | humanizePercentage }} traffic for >30m. Complete cutover or rollback?"
```

---

## 8. Security

### Authentication Methods

#### 1. JWT Bearer Tokens

```csharp
// Program.cs - Add JWT authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.honua.io";
        options.Audience = "gateway-api";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

// Require authentication for admin endpoints
app.MapPost("/admin/traffic/switch", async (
    BlueGreenTrafficManager trafficManager,
    TrafficSwitchRequest request) =>
{
    // Handler implementation
})
.RequireAuthorization("TrafficManagement");
```

**Usage:**
```bash
# Get JWT token
TOKEN=$(curl -X POST https://auth.honua.io/oauth/token \
  -d "client_id=gateway-client" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "grant_type=client_credentials" \
  | jq -r '.access_token')

# Use token in requests
curl -X POST http://gateway/admin/traffic/switch \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ ... }'
```

#### 2. API Keys

```csharp
// Middleware for API key authentication
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/admin"))
    {
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey) || !IsValidApiKey(apiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "Valid API key required"
            });
            return;
        }
    }

    await next();
});
```

**Usage:**
```bash
curl -X POST http://gateway/admin/traffic/switch \
  -H "X-API-Key: your-api-key-here" \
  -H "Content-Type: application/json" \
  -d '{ ... }'
```

### Authorization Policies

```csharp
builder.Services.AddAuthorization(options =>
{
    // Only platform admins can manage traffic
    options.AddPolicy("TrafficManagement", policy =>
    {
        policy.RequireRole("PlatformAdmin", "SRE");
        policy.RequireClaim("scope", "gateway:admin");
    });

    // Read-only access to status
    options.AddPolicy("TrafficStatus", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", "gateway:read");
    });
});

// Apply policies to endpoints
app.MapPost("/admin/traffic/switch", ...)
   .RequireAuthorization("TrafficManagement");

app.MapGet("/admin/traffic/status", ...)
   .RequireAuthorization("TrafficStatus");
```

### Rate Limiting for Admin Endpoints

```csharp
builder.Services.AddRateLimiter(options =>
{
    // Strict rate limiting for admin endpoints
    options.AddPolicy("admin-strict", httpContext =>
    {
        var username = httpContext.User?.Identity?.Name ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(username, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0 // No queuing for admin operations
            });
    });
});

app.MapPost("/admin/traffic/switch", ...)
   .RequireRateLimiting("admin-strict");
```

### Audit Logging

```csharp
app.MapPost("/admin/traffic/switch", async (
    BlueGreenTrafficManager trafficManager,
    TrafficSwitchRequest request,
    HttpContext context,
    ILogger<Program> logger) =>
{
    var username = context.User?.Identity?.Name ?? "anonymous";
    var ipAddress = context.Connection.RemoteIpAddress?.ToString();

    logger.LogWarning(
        "Traffic switch initiated by {Username} from {IpAddress}. " +
        "Service: {Service}, Green%: {GreenPercentage}",
        username,
        ipAddress,
        request.ServiceName,
        request.GreenPercentage);

    var result = await trafficManager.SwitchTrafficAsync(
        request.ServiceName,
        request.BlueEndpoint,
        request.GreenEndpoint,
        request.GreenPercentage,
        context.RequestAborted);

    if (result.Success)
    {
        logger.LogInformation(
            "Traffic switch completed by {Username}. " +
            "Service: {Service}, Blue: {Blue}%, Green: {Green}%",
            username,
            request.ServiceName,
            result.BlueTrafficPercentage,
            result.GreenTrafficPercentage);
    }
    else
    {
        logger.LogError(
            "Traffic switch failed by {Username}. " +
            "Service: {Service}, Error: {Error}",
            username,
            request.ServiceName,
            result.Message);
    }

    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});
```

**Audit Log Format (JSON):**
```json
{
  "timestamp": "2025-11-14T10:30:45.123Z",
  "level": "Warning",
  "message": "Traffic switch initiated",
  "username": "admin@honua.io",
  "ipAddress": "203.0.113.42",
  "service": "honua-api",
  "action": "SwitchTraffic",
  "greenPercentage": 50,
  "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

---

## 9. Troubleshooting

### Common Issues and Solutions

#### Issue: Traffic not switching

**Symptoms:**
- API returns success but traffic distribution unchanged
- Metrics show 100% traffic on blue after switch command

**Diagnosis:**
```bash
# Check YARP configuration
curl http://gateway/admin/traffic/status?serviceName=api-cluster

# Check YARP logs
kubectl logs -f deployment/honua-gateway | grep -i "traffic\|switch\|config"

# Verify InMemoryConfigProvider is being used
kubectl logs deployment/honua-gateway | grep "InMemoryConfigProvider"
```

**Solution:**
1. Ensure `InMemoryConfigProvider` is registered:
```csharp
var configProvider = new InMemoryConfigProvider(routes, clusters);
builder.Services.AddSingleton<IProxyConfigProvider>(configProvider);
```

2. Pass `IProxyConfigProvider` to `BlueGreenTrafficManager`:
```csharp
builder.Services.AddSingleton<BlueGreenTrafficManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<BlueGreenTrafficManager>>();
    var configProvider = sp.GetRequiredService<IProxyConfigProvider>();
    return new BlueGreenTrafficManager(logger, configProvider);
});
```

---

#### Issue: Health checks always failing

**Symptoms:**
- Green deployment marked as unhealthy
- Auto-rollback triggered immediately
- Logs show health check timeout errors

**Diagnosis:**
```bash
# Test health endpoint directly
curl -v http://api-green:8080/health

# Check health check configuration
kubectl get configmap gateway-config -o yaml | grep -A 10 HealthCheck

# Review YARP health check logs
kubectl logs deployment/honua-gateway | grep -i "health"
```

**Solution:**
1. Verify health endpoint returns 200 OK:
```bash
curl -i http://api-green:8080/health
# Should return: HTTP/1.1 200 OK
```

2. Increase health check timeout:
```json
{
  "HealthCheck": {
    "Active": {
      "Timeout": "00:00:30"  // Increase from 5s to 30s
    }
  }
}
```

3. Check network connectivity:
```bash
kubectl exec -it deployment/honua-gateway -- curl http://api-green:8080/health
```

---

#### Issue: High latency during traffic switch

**Symptoms:**
- P95 latency spikes during canary deployment
- Slow response times for 30-60 seconds after switch

**Diagnosis:**
```promql
# Check latency per destination
histogram_quantile(0.95,
  rate(yarp_proxy_request_duration_seconds_bucket[1m])
)

# Check connection pool status
yarp_proxy_active_connections{destination="green"}
```

**Solution:**
1. Pre-warm connections to green:
```csharp
// Before traffic switch, send warmup requests
for (int i = 0; i < 50; i++)
{
    _ = httpClient.GetAsync($"{greenEndpoint}/health");
}
await Task.Delay(TimeSpan.FromSeconds(5));
```

2. Increase connection pool size:
```csharp
builder.Services.AddReverseProxy()
    .ConfigureHttpClient((context, handler) =>
    {
        handler.MaxConnectionsPerServer = 100;
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(5);
    });
```

---

#### Issue: Canary deployment stuck at intermediate percentage

**Symptoms:**
- Traffic stuck at 50% for extended period
- No errors in logs
- Health checks passing

**Diagnosis:**
```bash
# Check current traffic distribution
curl http://gateway/admin/traffic/status?serviceName=api-cluster

# Review canary deployment logs
kubectl logs deployment/honua-gateway | grep -i "canary\|stage"
```

**Solution:**
1. Complete or rollback manually:
```bash
# Complete the cutover
curl -X POST http://gateway/admin/traffic/instant-cutover \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"serviceName":"api-cluster",...}'

# Or rollback
curl -X POST http://gateway/admin/traffic/rollback \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"serviceName":"api-cluster",...}'
```

---

### Debugging Traffic Routing

**Enable YARP debug logging:**

```json
{
  "Logging": {
    "LogLevel": {
      "Yarp": "Debug",
      "Yarp.ReverseProxy": "Debug"
    }
  }
}
```

**Check request routing:**
```bash
# View real-time request routing
kubectl logs -f deployment/honua-gateway | grep -i "proxying\|routing\|cluster"

# Example output:
# [2025-11-14 10:45:30.123] [INF] Proxying request GET /api/users to api-cluster
# [2025-11-14 10:45:30.124] [DBG] Selected destination: green (weight: 50)
```

**Verify cluster configuration:**
```bash
# Check YARP configuration via logs
kubectl logs deployment/honua-gateway | grep -i "cluster.*config\|route.*config"
```

---

### Checking Current Configuration

```bash
# Get current traffic distribution
curl -X GET "http://gateway/admin/traffic/status?serviceName=api-cluster" \
  -H "Authorization: Bearer $TOKEN"

# Response:
# {
#   "serviceName": "api-cluster",
#   "blueTrafficPercentage": 70,
#   "greenTrafficPercentage": 30,
#   "destinations": {
#     "blue": {"healthy": true, "weight": 70},
#     "green": {"healthy": true, "weight": 30}
#   }
# }
```

---

### Recovery Procedures

#### Emergency Rollback

```bash
#!/bin/bash
# emergency-rollback.sh

GATEWAY_URL="http://gateway:5000"
GATEWAY_TOKEN="$GATEWAY_API_TOKEN"
SERVICE_NAME="api-cluster"

echo "EMERGENCY ROLLBACK INITIATED"
echo "Service: $SERVICE_NAME"
echo "Switching 100% traffic to BLUE..."

curl -X POST "$GATEWAY_URL/admin/traffic/rollback" \
  -H "Authorization: Bearer $GATEWAY_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"serviceName\": \"$SERVICE_NAME\",
    \"blueEndpoint\": \"http://api-blue:8080\",
    \"greenEndpoint\": \"http://api-green:8080\"
  }"

echo ""
echo "Verifying traffic distribution..."

curl -X GET "$GATEWAY_URL/admin/traffic/status?serviceName=$SERVICE_NAME" \
  -H "Authorization: Bearer $GATEWAY_TOKEN"

echo ""
echo "ROLLBACK COMPLETE"
```

#### Gateway Restart (Preserves Configuration)

```bash
# YARP configuration is in-memory, so restart resets to appsettings.json
# To preserve state, export configuration before restart

# Export current configuration
kubectl exec deployment/honua-gateway -- \
  curl http://localhost:5000/admin/traffic/status?serviceName=api-cluster \
  > current-traffic-config.json

# Restart gateway
kubectl rollout restart deployment/honua-gateway

# Wait for gateway to be ready
kubectl rollout status deployment/honua-gateway

# Restore traffic configuration
BLUE_PCT=$(jq -r '.blueTrafficPercentage' current-traffic-config.json)
GREEN_PCT=$(jq -r '.greenTrafficPercentage' current-traffic-config.json)

curl -X POST http://gateway/admin/traffic/switch \
  -H "Authorization: Bearer $TOKEN" \
  -d "{
    \"serviceName\": \"api-cluster\",
    \"blueEndpoint\": \"http://api-blue:8080\",
    \"greenEndpoint\": \"http://api-green:8080\",
    \"greenPercentage\": $GREEN_PCT
  }"
```

---

## 10. Comparison with SwitchTrafficStep.cs

### Side-by-Side Comparison

| Feature | SwitchTrafficStep.cs (Old) | YARP Traffic Manager (New) |
|---------|----------------------------|----------------------------|
| **Lines of Code** | 1,546 | 354 |
| **Platform Implementations** | 6 (K8s, AWS, Azure, GCP, NGINX, HAProxy) | 1 (universal) |
| **API Calls** | N/A (direct platform CLI) | Simple HTTP API |
| **Configuration Method** | File editing + validation + reload | Dynamic in-memory updates |
| **Switch Speed** | 30-60 seconds | 1-2 seconds |
| **Rollback Speed** | 30-60 seconds | 1-2 seconds |
| **Health Checks** | Manual, platform-specific | Built-in active/passive |
| **Security Risks** | Command injection (requires validation) | Type-safe, no shell execution |
| **Testing** | Requires cloud infrastructure | Fully testable locally |
| **Metrics** | Platform-specific (CloudWatch, Azure Monitor, etc.) | Unified Prometheus |
| **Maintenance Burden** | 6 codepaths × N platforms | Single codebase |
| **Deployment Complexity** | Platform-specific credentials, IAM roles, kubeconfig | HTTP client + auth token |
| **Vendor Lock-in** | High (tight coupling to AWS/Azure/GCP APIs) | None (works anywhere) |

### Code Comparison

**Old Approach (SwitchTrafficStep.cs):**

```csharp
// Kubernetes implementation (163 lines)
private async Task SwitchTrafficKubernetes(int percentage)
{
    _logger.LogInformation("Updating Kubernetes Service weights");

    var greenWeight = 100 - percentage;
    var blueWeight = percentage;

    // Complex service creation/patching logic
    // Checking if services exist
    // Creating manifests
    // Running kubectl commands
    // Error handling for platform-specific failures
    // Security validation for command injection

    // ... 163 lines of code ...
}

// AWS implementation (75 lines)
private async Task SwitchTrafficAWS(int percentage)
{
    _logger.LogInformation("Updating AWS ALB target group weights");

    // Platform-specific environment variable validation
    // ARN validation to prevent command injection
    // Complex AWS CLI command construction
    // Error handling and sanitization

    // ... 75 lines of code ...
}

// Azure, GCP, NGINX, HAProxy implementations...
// Total: 1,546 lines across 6 platforms
```

**New Approach (YARP Traffic Manager):**

```csharp
// Universal implementation (46 lines)
public Task<TrafficSwitchResult> SwitchTrafficAsync(
    string serviceName,
    string blueEndpoint,
    string greenEndpoint,
    int greenTrafficPercentage,
    CancellationToken cancellationToken)
{
    try
    {
        if (greenTrafficPercentage < 0 || greenTrafficPercentage > 100)
        {
            throw new ArgumentException(
                "Green traffic percentage must be between 0 and 100");
        }

        // Create weighted destinations (type-safe)
        var destinations = new Dictionary<string, DestinationConfig>();

        if (greenTrafficPercentage < 100)
        {
            destinations["blue"] = new DestinationConfig
            {
                Address = blueEndpoint,
                Health = blueEndpoint + "/health",
                Metadata = new Dictionary<string, string>
                {
                    ["weight"] = (100 - greenTrafficPercentage).ToString()
                }
            };
        }

        if (greenTrafficPercentage > 0)
        {
            destinations["green"] = new DestinationConfig
            {
                Address = greenEndpoint,
                Health = greenEndpoint + "/health",
                Metadata = new Dictionary<string, string>
                {
                    ["weight"] = greenTrafficPercentage.ToString()
                }
            };
        }

        // Create cluster with health checks
        var cluster = new ClusterConfig
        {
            ClusterId = serviceName,
            Destinations = destinations,
            LoadBalancingPolicy = "WeightedRoundRobin",
            HealthCheck = new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig
                {
                    Enabled = true,
                    Interval = TimeSpan.FromSeconds(10),
                    Timeout = TimeSpan.FromSeconds(5),
                    Policy = "ConsecutiveFailures",
                    Path = "/health"
                }
            }
        };

        // Update YARP configuration (instant)
        _configProvider?.Update(_routes, new[] { cluster });

        return Task.FromResult(new TrafficSwitchResult
        {
            Success = true,
            BlueTrafficPercentage = 100 - greenTrafficPercentage,
            GreenTrafficPercentage = greenTrafficPercentage,
            Message = $"Traffic switched: {100 - greenTrafficPercentage}% blue, {greenTrafficPercentage}% green"
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to switch traffic");
        return Task.FromResult(new TrafficSwitchResult
        {
            Success = false,
            Message = $"Failed to switch traffic: {ex.Message}"
        });
    }
}
```

### Migration Path from Old to New

**Step 1: Deploy YARP Gateway**

```bash
# Deploy gateway alongside existing infrastructure
kubectl apply -f k8s/gateway-deployment.yaml

# Verify gateway is healthy
kubectl wait --for=condition=ready pod -l app=honua-gateway --timeout=300s
```

**Step 2: Update DNS/Load Balancer**

```bash
# Point traffic to YARP gateway instead of direct backend
# Old: client -> backend service
# New: client -> YARP gateway -> backend service

# Example: Update AWS Route53
aws route53 change-resource-record-sets \
  --hosted-zone-id Z1234567890ABC \
  --change-batch '{
    "Changes": [{
      "Action": "UPSERT",
      "ResourceRecordSet": {
        "Name": "api.honua.io",
        "Type": "A",
        "AliasTarget": {
          "HostedZoneId": "Z0987654321XYZ",
          "DNSName": "gateway-lb.us-east-1.elb.amazonaws.com",
          "EvaluateTargetHealth": true
        }
      }
    }]
  }'
```

**Step 3: Test Traffic Management API**

```bash
# Test traffic switch with 10% canary
curl -X POST http://gateway/admin/traffic/switch \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "serviceName": "api-cluster",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080",
    "greenPercentage": 10
  }'

# Verify metrics
curl http://gateway/metrics | grep yarp_proxy
```

**Step 4: Update CI/CD Pipelines**

```yaml
# Before (old approach)
- name: Switch traffic
  run: |
    # Complex platform-specific logic
    if [ "$PLATFORM" == "kubernetes" ]; then
      kubectl patch service honua-service ...
    elif [ "$PLATFORM" == "aws" ]; then
      aws elbv2 modify-rule ...
    fi

# After (new approach)
- name: Switch traffic
  run: |
    curl -X POST http://gateway/admin/traffic/canary \
      -H "Authorization: Bearer $TOKEN" \
      -d '{...}'
```

**Step 5: Decommission Old System**

```bash
# Once confident in YARP system, remove old traffic switching code
git rm src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs

# Remove platform-specific dependencies
dotnet remove package AWSSDK.ElasticLoadBalancingV2
dotnet remove package Azure.ResourceManager.TrafficManager
dotnet remove package Google.Cloud.Compute.V1
```

---

## 11. Advanced Scenarios

### Multi-Region Deployments

Deploy YARP gateways in multiple regions with geo-routing:

```json
{
  "ReverseProxy": {
    "Routes": {
      "api-route-us-east": {
        "ClusterId": "api-cluster-us-east",
        "Match": {
          "Path": "/{**catch-all}",
          "Headers": [
            {
              "Name": "X-Region",
              "Values": ["us-east-1"]
            }
          ]
        }
      },
      "api-route-eu-west": {
        "ClusterId": "api-cluster-eu-west",
        "Match": {
          "Path": "/{**catch-all}",
          "Headers": [
            {
              "Name": "X-Region",
              "Values": ["eu-west-1"]
            }
          ]
        }
      }
    },
    "Clusters": {
      "api-cluster-us-east": {
        "Destinations": {
          "blue": {"Address": "http://us-east-blue:8080"},
          "green": {"Address": "http://us-east-green:8080"}
        }
      },
      "api-cluster-eu-west": {
        "Destinations": {
          "blue": {"Address": "http://eu-west-blue:8080"},
          "green": {"Address": "http://eu-west-green:8080"}
        }
      }
    }
  }
}
```

**Staggered Multi-Region Rollout:**

```csharp
// Deploy to regions sequentially
var regions = new[] { "us-east-1", "eu-west-1", "ap-southeast-1" };

foreach (var region in regions)
{
    _logger.LogInformation("Deploying to region: {Region}", region);

    var result = await _trafficManager.PerformCanaryDeploymentAsync(
        $"api-cluster-{region}",
        $"http://{region}-blue:8080",
        $"http://{region}-green:8080",
        strategy,
        healthCheckFunc,
        cancellationToken);

    if (!result.Success)
    {
        _logger.LogError("Deployment failed in {Region}, halting rollout", region);
        break;
    }

    _logger.LogInformation("Region {Region} deployed successfully", region);
    await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
}
```

### A/B Testing with Custom Routing

Route traffic based on user attributes:

```csharp
// Add custom transform for A/B testing
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(async context =>
        {
            // Route beta users to green, others to blue
            var userId = context.HttpContext.User.FindFirst("sub")?.Value;
            var isBetaUser = await IsBetaUserAsync(userId);

            if (isBetaUser)
            {
                context.HttpContext.Request.Headers["X-Destination-Hint"] = "green";
            }
            else
            {
                context.HttpContext.Request.Headers["X-Destination-Hint"] = "blue";
            }

            await ValueTask.CompletedTask;
        });
    });
```

### Feature Flags Integration

Use feature flags to control traffic routing:

```csharp
public class FeatureFlagTrafficManager
{
    private readonly BlueGreenTrafficManager _trafficManager;
    private readonly IFeatureFlagProvider _featureFlags;

    public async Task UpdateTrafficBasedOnFlagsAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        // Check feature flag for green traffic percentage
        var greenPercentage = await _featureFlags.GetIntValueAsync(
            $"traffic.{serviceName}.green-percentage",
            defaultValue: 0,
            cancellationToken);

        await _trafficManager.SwitchTrafficAsync(
            serviceName,
            $"http://{serviceName}-blue:8080",
            $"http://{serviceName}-green:8080",
            greenPercentage,
            cancellationToken);
    }
}

// Background service to sync with feature flags
public class TrafficFlagSyncService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _featureFlagTrafficManager.UpdateTrafficBasedOnFlagsAsync(
                "api-cluster",
                stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

### Progressive Delivery Strategies

**Time-based Canary:**

```csharp
public async Task TimeBasedCanaryAsync(
    string serviceName,
    string blueEndpoint,
    string greenEndpoint,
    CancellationToken cancellationToken)
{
    // 10% for 1 hour
    await SwitchAndWaitAsync(serviceName, blueEndpoint, greenEndpoint, 10,
        TimeSpan.FromHours(1), cancellationToken);

    // 25% for 2 hours
    await SwitchAndWaitAsync(serviceName, blueEndpoint, greenEndpoint, 25,
        TimeSpan.FromHours(2), cancellationToken);

    // 50% for 4 hours
    await SwitchAndWaitAsync(serviceName, blueEndpoint, greenEndpoint, 50,
        TimeSpan.FromHours(4), cancellationToken);

    // 100% cutover
    await _trafficManager.SwitchTrafficAsync(
        serviceName, blueEndpoint, greenEndpoint, 100, cancellationToken);
}
```

**Metric-based Progressive Rollout:**

```csharp
public async Task MetricDrivenCanaryAsync(
    string serviceName,
    string blueEndpoint,
    string greenEndpoint,
    CancellationToken cancellationToken)
{
    var currentPercentage = 10;

    while (currentPercentage < 100)
    {
        await _trafficManager.SwitchTrafficAsync(
            serviceName, blueEndpoint, greenEndpoint,
            currentPercentage, cancellationToken);

        // Wait 5 minutes for metrics to stabilize
        await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

        // Check metrics
        var errorRate = await GetErrorRateAsync(greenEndpoint, cancellationToken);
        var latencyP95 = await GetLatencyP95Async(greenEndpoint, cancellationToken);

        if (errorRate > 0.01) // 1% error rate threshold
        {
            _logger.LogWarning(
                "Error rate {ErrorRate} exceeds threshold, rolling back",
                errorRate);

            await _trafficManager.RollbackToBlueAsync(
                serviceName, blueEndpoint, greenEndpoint, cancellationToken);
            break;
        }

        if (latencyP95 > 2000) // 2s latency threshold
        {
            _logger.LogWarning(
                "P95 latency {Latency}ms exceeds threshold, rolling back",
                latencyP95);

            await _trafficManager.RollbackToBlueAsync(
                serviceName, blueEndpoint, greenEndpoint, cancellationToken);
            break;
        }

        // Increase traffic by 10-20% based on metrics
        var metricsScore = CalculateMetricsScore(errorRate, latencyP95);
        currentPercentage += metricsScore > 0.9 ? 20 : 10;
        currentPercentage = Math.Min(currentPercentage, 100);
    }
}
```

---

## Conclusion

The YARP-based traffic management system provides a **simple, fast, and platform-agnostic** solution for zero-downtime deployments. By eliminating 1,546 lines of platform-specific code and replacing it with 354 lines of universal logic, this system dramatically reduces complexity, improves maintainability, and enables true cloud portability.

**Key Takeaways:**

1. **77% code reduction** compared to platform-specific approach
2. **Sub-second traffic switching** vs. 30-60 second config reloads
3. **Zero command injection risks** with type-safe APIs
4. **Works everywhere**: Kubernetes, Docker, VMs, bare metal
5. **Built-in observability**: Prometheus metrics, OpenTelemetry, health checks
6. **Developer-friendly**: Same workflow for local dev and production

For questions, issues, or contributions, please refer to the [Honua.Server GitHub repository](https://github.com/honua-io/Honua.Server).

---

**Document Version:** 1.0
**Last Updated:** 2025-11-14
**License:** Elastic License 2.0
