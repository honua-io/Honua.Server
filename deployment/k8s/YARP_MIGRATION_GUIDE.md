# YARP Gateway Migration Guide

## Overview

This guide covers the migration from NGINX Ingress Controller to YARP (Yet Another Reverse Proxy) as the primary API Gateway and Ingress Controller for the Honua platform.

## What is YARP?

YARP (Yet Another Reverse Proxy) is a highly performant, production-ready reverse proxy library built on ASP.NET Core. It provides:

- **High Performance**: Built on ASP.NET Core's Kestrel server
- **Dynamic Configuration**: Routes and clusters can be updated without restart
- **Advanced Load Balancing**: Multiple load balancing policies
- **Active/Passive Health Checks**: Automatic unhealthy backend detection
- **Transforms**: Request/response header manipulation
- **Rate Limiting**: Built-in distributed rate limiting
- **Observability**: Native OpenTelemetry integration
- **.NET Ecosystem**: Leverage the full .NET ecosystem for custom middleware

## Architecture Changes

### Before (NGINX)

```
Internet → NGINX Ingress Controller → Kubernetes Services → Pods
```

### After (YARP)

```
Internet → Cloud Load Balancer → YARP Gateway Pods → Kubernetes Services → Backend Pods
```

## Key Benefits

1. **Unified Stack**: Same .NET technology stack as backend services
2. **Advanced Features**: Blue-green deployments, canary releases, circuit breakers
3. **Better Observability**: Native OpenTelemetry, Prometheus metrics
4. **Dynamic Configuration**: Hot-reload configuration without pod restart
5. **Cloud Native**: Works seamlessly with Kubernetes, cloud providers
6. **Security**: Built-in rate limiting, security headers, CORS
7. **Cost**: No third-party ingress controller licensing

## Migration Steps

### Step 1: Build the YARP Gateway Image

```bash
# From the repository root
cd src/Honua.Server.Gateway

# Build the Docker image
docker build -t honua/gateway:latest \
  --build-arg DOTNET_VERSION=9.0 \
  --build-arg RUNTIME_ARCH=linux-x64 \
  -f Dockerfile .

# Tag for your container registry
docker tag honua/gateway:latest <YOUR_REGISTRY>/honua/gateway:latest

# Push to registry
docker push <YOUR_REGISTRY>/honua/gateway:latest
```

### Step 2: Update Configuration

Review and update the YARP configuration in `deployment/k8s/base/gateway/configmap.yaml`:

```bash
# Edit the ConfigMap to set your domain names
vim deployment/k8s/base/gateway/configmap.yaml
```

Update the following:
- Replace `api.honua.example.com` with your actual domain
- Update backend service URLs if different
- Adjust rate limiting settings for your needs

### Step 3: Deploy to Kubernetes

#### Option A: Direct kubectl apply

```bash
# Deploy the gateway
kubectl apply -k deployment/k8s/base/gateway/

# Verify deployment
kubectl get pods -n honua -l app.kubernetes.io/name=honua-gateway
kubectl get svc -n honua -l app.kubernetes.io/name=honua-gateway
```

#### Option B: Using Kustomize overlays

```bash
# For production
kubectl apply -k deployment/k8s/overlays/production/

# For staging
kubectl apply -k deployment/k8s/overlays/staging/

# For development
kubectl apply -k deployment/k8s/overlays/development/
```

### Step 4: Configure DNS

Once the LoadBalancer service is created, get the external IP:

```bash
kubectl get svc honua-gateway -n honua

# Output:
# NAME             TYPE           CLUSTER-IP      EXTERNAL-IP       PORT(S)
# honua-gateway    LoadBalancer   10.0.45.123     20.185.123.45     80:30123/TCP,443:31456/TCP
```

Update your DNS records:
```
api.honua.io        A    20.185.123.45
intake.honua.io     A    20.185.123.45
orchestrator.honua.io  A  20.185.123.45
```

### Step 5: Verify TLS Certificates

Check cert-manager has issued certificates:

```bash
# Check certificate status
kubectl get certificate -n honua

# Expected output:
# NAME                   READY   SECRET              AGE
# honua-gateway-cert     True    honua-tls-cert      5m
# honua-monitoring-cert  True    honua-monitoring-tls 5m

# Check certificate details
kubectl describe certificate honua-gateway-cert -n honua
```

### Step 6: Test the Gateway

```bash
# Test HTTP endpoint
curl -v http://api.honua.io/health

# Test HTTPS endpoint
curl -v https://api.honua.io/health

# Test rate limiting
for i in {1..150}; do
  curl -s -o /dev/null -w "%{http_code}\n" https://api.honua.io/health
done
# Should see 429 (Too Many Requests) after ~100 requests

# Test metrics
curl https://api.honua.io/metrics
```

### Step 7: Monitor Metrics

Access Prometheus to verify metrics are being collected:

```bash
# Port-forward to Prometheus
kubectl port-forward -n honua svc/prometheus 9090:9090

# Open http://localhost:9090
# Query: yarp_proxy_requests_total
```

### Step 8: Remove NGINX Ingress (Optional)

Once YARP is verified working:

```bash
# Backup NGINX ingress configuration
kubectl get ingress -n honua -o yaml > nginx-ingress-backup.yaml

# Delete NGINX ingress
kubectl delete ingress -n honua --all

# Uninstall NGINX ingress controller (if no other apps use it)
helm uninstall nginx-ingress -n ingress-nginx
```

## Cloud-Specific Configuration

### AWS EKS

Apply AWS-specific configuration:

```bash
kubectl apply -f deployment/cloud/aws/gateway-config.yaml
```

Key features:
- Network Load Balancer (NLB)
- AWS ACM for TLS certificates
- S3 access logging
- IRSA (IAM Roles for Service Accounts)

### Google Cloud GKE

Apply GCP-specific configuration:

```bash
kubectl apply -f deployment/cloud/gcp/gateway-config.yaml
```

Key features:
- Google Cloud Load Balancer
- Google-managed certificates
- Cloud Armor (WAF/DDoS protection)
- Workload Identity

### Azure AKS

Apply Azure-specific configuration:

```bash
kubectl apply -f deployment/cloud/azure/gateway-config.yaml
```

Key features:
- Azure Load Balancer
- Azure Managed Identity
- Application Gateway (optional)
- Azure Front Door integration

## Configuration Hot-Reload

YARP supports configuration hot-reload. To update routes without pod restart:

```bash
# Edit the ConfigMap
kubectl edit configmap honua-gateway-config -n honua

# YARP will automatically detect and reload the configuration
# Monitor logs to verify reload
kubectl logs -n honua -l app.kubernetes.io/name=honua-gateway -f
```

## Scaling

### Horizontal Scaling

The HorizontalPodAutoscaler automatically scales based on CPU/memory:

```bash
# View current scaling status
kubectl get hpa -n honua honua-gateway

# Manually scale if needed
kubectl scale deployment honua-gateway -n honua --replicas=5
```

### Vertical Scaling

Update resource limits in `deployment/k8s/base/gateway/deployment.yaml`:

```yaml
resources:
  requests:
    cpu: 500m
    memory: 512Mi
  limits:
    cpu: 2000m
    memory: 1Gi
```

## Monitoring and Alerting

### Prometheus Metrics

YARP exposes the following metric categories:

- `yarp_proxy_*`: Proxy-specific metrics
- `http_*`: HTTP request metrics
- `aspnetcore_*`: ASP.NET Core metrics
- `process_*`: Process metrics

Example queries:

```promql
# Request rate
rate(yarp_proxy_requests_total[5m])

# Error rate
rate(yarp_proxy_requests_total{code=~"5.."}[5m])

# Request duration
histogram_quantile(0.95, rate(yarp_proxy_request_duration_seconds_bucket[5m]))

# Active connections
yarp_proxy_current_requests
```

### Grafana Dashboard

Import the YARP dashboard:

```bash
# Dashboard ID (create custom or use community dashboard)
# Add panels for:
# - Request rate by route
# - Error rate by cluster
# - Request duration percentiles
# - Active health check status
```

## Troubleshooting

### Gateway Pods Not Starting

```bash
# Check pod logs
kubectl logs -n honua -l app.kubernetes.io/name=honua-gateway

# Check events
kubectl describe pod -n honua -l app.kubernetes.io/name=honua-gateway

# Common issues:
# - ConfigMap not found
# - Secret not found (TLS certificates)
# - Redis connection failure (if using distributed rate limiting)
```

### 502 Bad Gateway

```bash
# Check backend service health
kubectl get endpoints -n honua honua-api
kubectl get pods -n honua -l app.kubernetes.io/component=api

# Check YARP health checks
kubectl logs -n honua -l app.kubernetes.io/name=honua-gateway | grep "health"

# Verify network policies allow traffic
kubectl get networkpolicy -n honua
```

### Rate Limiting Not Working

```bash
# Check Redis connection (if using distributed rate limiting)
kubectl get pods -n honua -l app=redis

# Check rate limiting configuration in ConfigMap
kubectl get configmap honua-gateway-config -n honua -o yaml

# Verify environment variables
kubectl get deployment honua-gateway -n honua -o yaml | grep -A 5 "RateLimiting"
```

### Certificate Issues

```bash
# Check cert-manager logs
kubectl logs -n cert-manager -l app=cert-manager

# Check certificate status
kubectl describe certificate honua-gateway-cert -n honua

# Check certificate secret
kubectl get secret honua-tls-cert -n honua
kubectl describe secret honua-tls-cert -n honua

# Manually trigger certificate renewal
kubectl delete certificate honua-gateway-cert -n honua
kubectl apply -f deployment/k8s/base/gateway/certificates.yaml
```

## Advanced Features

### Blue-Green Deployments

YARP includes built-in blue-green deployment support. Example configuration:

```csharp
// In a custom YARP configuration provider
var blueGreenManager = new BlueGreenTrafficManager(proxyConfigProvider);

// Switch 50% traffic to green
await blueGreenManager.SwitchToGreenAsync(50);

// Full switchover
await blueGreenManager.SwitchToGreenAsync(100);

// Rollback
await blueGreenManager.SwitchToBlueAsync();
```

### Canary Deployments

```csharp
var canaryStrategy = new CanaryStrategy
{
    TrafficSteps = new List<int> { 10, 25, 50, 100 },
    SoakDurationSeconds = 300,
    AutoRollback = true
};

await blueGreenManager.ExecuteCanaryDeploymentAsync("v2.0", canaryStrategy);
```

### Custom Transforms

Add custom request/response transforms in `Program.cs`:

```csharp
.AddTransforms(builderContext =>
{
    // Add custom header based on route
    builderContext.AddRequestTransform(async context =>
    {
        if (context.Path.StartsWithSegments("/api"))
        {
            context.HttpContext.Request.Headers["X-API-Version"] = "v1";
        }
        await ValueTask.CompletedTask;
    });
});
```

## Performance Tuning

### Connection Pooling

Update `appsettings.json`:

```json
{
  "ReverseProxy": {
    "Clusters": {
      "api-cluster": {
        "HttpClient": {
          "MaxConnectionsPerServer": 100
        }
      }
    }
  }
}
```

### Request Timeouts

```json
{
  "ReverseProxy": {
    "Clusters": {
      "api-cluster": {
        "HttpRequest": {
          "Timeout": "00:10:00",
          "Version": "2",
          "VersionPolicy": "RequestVersionOrLower"
        }
      }
    }
  }
}
```

### Memory Optimization

Update deployment resource limits based on traffic patterns:

```yaml
resources:
  requests:
    memory: "256Mi"
  limits:
    memory: "512Mi"
```

## Rollback Plan

If you need to rollback to NGINX:

```bash
# 1. Restore NGINX ingress
kubectl apply -f deployment/k8s/base/ingress.yaml.nginx-backup

# 2. Scale down YARP gateway
kubectl scale deployment honua-gateway -n honua --replicas=0

# 3. Update DNS to point back to NGINX LoadBalancer IP
kubectl get svc -n ingress-nginx

# 4. Verify services are accessible
```

## Support

For issues or questions:

1. Check logs: `kubectl logs -n honua -l app.kubernetes.io/name=honua-gateway`
2. Review metrics in Prometheus/Grafana
3. Consult YARP documentation: https://microsoft.github.io/reverse-proxy/
4. File an issue in the repository

## References

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [YARP GitHub](https://github.com/microsoft/reverse-proxy)
- [Kubernetes Ingress](https://kubernetes.io/docs/concepts/services-networking/ingress/)
- [cert-manager Documentation](https://cert-manager.io/docs/)
