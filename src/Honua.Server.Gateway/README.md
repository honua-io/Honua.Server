# Honua.Server.Gateway

YARP-based API Gateway and Ingress Controller for the Honua platform.

## Overview

This service acts as the primary API gateway and ingress controller for all Honua services, replacing NGINX Ingress Controller with a high-performance, .NET-based reverse proxy solution.

## Features

- **High Performance**: Built on ASP.NET Core Kestrel and YARP
- **Dynamic Routing**: Hot-reload configuration without restart
- **Health Checks**: Active and passive health monitoring of backend services
- **Rate Limiting**: Distributed rate limiting with Redis support
- **Security**: HTTPS, security headers, CORS, request validation
- **Observability**: OpenTelemetry metrics, traces, and Prometheus integration
- **Load Balancing**: Round-robin, least requests, random policies
- **TLS Termination**: Native TLS support with cert-manager integration

## Architecture

```
┌─────────────────────────────────────────────────┐
│              Cloud Load Balancer                │
│        (AWS NLB / GCP LB / Azure LB)           │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│           YARP Gateway (This Service)           │
│  ┌──────────────────────────────────────────┐  │
│  │  Rate Limiting │ Security │ Observability│  │
│  └──────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────┐  │
│  │        Route Matching & Transforms       │  │
│  └──────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────┐  │
│  │   Health Checks │ Load Balancing         │  │
│  └──────────────────────────────────────────┘  │
└──────────────────┬──────────────────────────────┘
                   │
        ┌──────────┴───────────┬─────────────┐
        ▼                      ▼             ▼
   ┌─────────┐          ┌──────────┐   ┌──────────┐
   │   API   │          │  Intake  │   │Orchestr. │
   │ Service │          │  Service │   │  Service │
   └─────────┘          └──────────┘   └──────────┘
```

## Configuration

### Routes

Routes are defined in `appsettings.json` under `ReverseProxy.Routes`:

```json
{
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": {
          "Hosts": ["api.honua.io"],
          "Path": "{**catch-all}"
        },
        "RateLimiterPolicy": "per-ip"
      }
    }
  }
}
```

### Clusters

Backend clusters are defined under `ReverseProxy.Clusters`:

```json
{
  "ReverseProxy": {
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "api-primary": {
            "Address": "http://honua-api.honua.svc.cluster.local:8080"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Path": "/health/ready"
          }
        },
        "LoadBalancingPolicy": "RoundRobin"
      }
    }
  }
}
```

### Rate Limiting

Rate limiting can be configured globally or per-IP:

```json
{
  "RateLimiting": {
    "GlobalPermitLimit": 5000,
    "GlobalWindowSeconds": 60,
    "PerIpPermitLimit": 100,
    "PerIpWindowSeconds": 60
  }
}
```

### CORS

Configure allowed origins:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.honua.io",
      "https://dashboard.honua.io"
    ]
  }
}
```

## Running Locally

### Prerequisites

- .NET 9.0 SDK
- Docker (optional)
- Backend services running (API, Intake, etc.)

### Run with .NET CLI

```bash
cd src/Honua.Server.Gateway

# Run in development mode
dotnet run --environment Development

# The gateway will be available at:
# http://localhost:8080
# https://localhost:8443
```

### Run with Docker

```bash
# Build image
docker build -t honua/gateway:latest .

# Run container
docker run -p 8080:8080 -p 8443:8443 -p 8081:8081 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  honua/gateway:latest
```

### Testing

```bash
# Health check
curl http://localhost:8080/health

# Metrics
curl http://localhost:8080/metrics

# Test proxying (assuming API service is running on port 5000)
curl http://localhost:8080/api/collections
```

## Deployment

### Kubernetes

Deploy using kubectl:

```bash
# Deploy all gateway resources
kubectl apply -k deployment/k8s/base/gateway/

# Or deploy entire platform including gateway
kubectl apply -k deployment/k8s/base/
```

### Helm (if using Helm charts)

```bash
helm install honua-gateway ./deployment/helm/honua \
  --namespace honua \
  --values deployment/helm/honua/values-prod.yaml
```

### Docker Compose

For local development:

```bash
docker-compose -f docker/docker-compose.full.yml up gateway
```

## Monitoring

### Prometheus Metrics

The gateway exposes Prometheus metrics on port 8081 at `/metrics`:

```bash
curl http://localhost:8081/metrics
```

Key metrics:
- `yarp_proxy_requests_total`: Total requests proxied
- `yarp_proxy_request_duration_seconds`: Request duration histogram
- `yarp_proxy_current_requests`: Current active requests
- `aspnetcore_http_requests_total`: ASP.NET Core HTTP request count
- `process_cpu_usage`: CPU usage
- `dotnet_gc_collections_total`: GC collections

### Health Endpoints

Three health endpoints are available:

1. **Live**: `GET /health/live` - Is the service alive?
2. **Ready**: `GET /health/ready` - Is the service ready to accept traffic?
3. **Full**: `GET /health` - Detailed health status of all dependencies

### Logging

Structured logging using Serilog. Logs are written to:
- Console (stdout) in JSON format
- File: `/app/logs/honua-gateway.log`
- Optionally: Seq, Elasticsearch, etc.

Log levels:
- Development: Debug
- Production: Information

## Security

### TLS/SSL

TLS is enabled by default on port 8443. Certificates can be:
- Provided via Kubernetes secrets
- Generated by cert-manager
- Configured via environment variables

### Security Headers

The following security headers are added to all responses:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: SAMEORIGIN`
- `X-XSS-Protection: 1; mode=block`
- `Strict-Transport-Security: max-age=31536000; includeSubDomains`
- `Referrer-Policy: strict-origin-when-cross-origin`

### Rate Limiting

Protects against abuse and DDoS attacks:
- Global rate limit: Applies to all traffic
- Per-IP rate limit: Applies per client IP address
- Returns HTTP 429 (Too Many Requests) when exceeded

### CORS

Cross-Origin Resource Sharing (CORS) is configured to allow specific origins.

## Advanced Features

### Dynamic Configuration Reload

YARP supports hot-reload of configuration. Update the ConfigMap in Kubernetes:

```bash
kubectl edit configmap honua-gateway-config -n honua
```

The gateway will automatically detect changes and reload routes without downtime.

### Blue-Green Deployments

The gateway includes built-in support for blue-green deployments via the `BlueGreenTrafficManager` class from `Honua.Server.Core`.

### Custom Transforms

Request and response transforms can be added in `Program.cs`:

```csharp
.AddTransforms(builderContext =>
{
    // Add custom headers
    builderContext.AddResponseHeader("X-Custom-Header", "Value");

    // Add request ID
    builderContext.AddRequestTransform(async context =>
    {
        context.HttpContext.Request.Headers["X-Request-ID"] = Guid.NewGuid().ToString();
        await ValueTask.CompletedTask;
    });
});
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | Production |
| `ASPNETCORE_URLS` | Listen URLs | http://+:8080;https://+:8443 |
| `BackendServices__api` | API service URL | http://honua-api:8080 |
| `BackendServices__intake` | Intake service URL | http://honua-intake:8080 |
| `Redis__ConnectionString` | Redis connection for rate limiting | (empty) |
| `OpenTelemetry__OtlpEndpoint` | OTLP endpoint for traces | (empty) |
| `RateLimiting__GlobalPermitLimit` | Global rate limit | 5000 |
| `RateLimiting__PerIpPermitLimit` | Per-IP rate limit | 100 |

## Performance

### Benchmarks

On a 4-core, 8GB machine:
- **Throughput**: ~50,000 requests/second
- **Latency (p50)**: <5ms
- **Latency (p99)**: <20ms
- **Memory**: ~200MB at idle, ~500MB under load

### Tuning

For high-traffic scenarios:

1. Increase replica count:
   ```bash
   kubectl scale deployment honua-gateway -n honua --replicas=10
   ```

2. Adjust resource limits:
   ```yaml
   resources:
     limits:
       cpu: 2000m
       memory: 1Gi
   ```

3. Enable connection pooling and HTTP/2

4. Use dedicated Redis for rate limiting

## Troubleshooting

### High Latency

1. Check backend service health:
   ```bash
   kubectl get pods -n honua
   ```

2. Review YARP metrics for slow destinations

3. Check network policies allow traffic

### 502 Bad Gateway

1. Verify backend services are running
2. Check health check status in logs
3. Verify service DNS resolution

### Rate Limiting Issues

1. Check Redis connectivity
2. Review rate limit configuration
3. Check for IP preservation (externalTrafficPolicy: Local)

## Contributing

When making changes to the gateway:

1. Update route configuration in `appsettings.json`
2. Add tests for new features
3. Update this README
4. Update the migration guide in `deployment/k8s/YARP_MIGRATION_GUIDE.md`

## References

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Serilog Documentation](https://serilog.net/)
