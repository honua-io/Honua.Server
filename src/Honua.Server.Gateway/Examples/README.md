# YARP Traffic Management Examples

This directory contains example configurations and scripts for implementing blue-green and canary deployments using the Honua YARP gateway.

## Files

### Configuration Examples

- **[blue-green-deployment.json](./blue-green-deployment.json)** - Schema and examples for blue-green deployment configurations
- **[canary-deployment.json](./canary-deployment.json)** - Schema and examples for canary deployment with health checks and metrics
- **[kubernetes-integration.yaml](./kubernetes-integration.yaml)** - Complete Kubernetes manifests for deploying blue-green environments with YARP gateway

## Quick Start

### 1. Blue-Green Deployment (Kubernetes)

Deploy both blue and green environments:

```bash
# Apply Kubernetes manifests
kubectl apply -f kubernetes-integration.yaml

# Wait for deployments to be ready
kubectl wait --for=condition=available --timeout=300s deployment/honua-api-blue -n honua
kubectl wait --for=condition=available --timeout=300s deployment/honua-api-green -n honua

# Switch 50% traffic to green
curl -X POST http://gateway/admin/traffic/switch \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "api-cluster",
    "blueEndpoint": "http://honua-api-blue.honua.svc.cluster.local:8080",
    "greenEndpoint": "http://honua-api-green.honua.svc.cluster.local:8080",
    "greenPercentage": 50
  }'
```

### 2. Canary Deployment (Automated)

Use the canary configuration for automated progressive rollout:

```bash
# Start canary deployment
curl -X POST http://gateway/admin/traffic/canary \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @canary-deployment.json
```

### 3. Using PowerShell Module

```powershell
# Import module
Import-Module ../Scripts/TrafficManagement.psm1

# Connect to gateway
Connect-TrafficGateway `
  -Url "https://gateway.honua.io" `
  -Token $env:GATEWAY_TOKEN

# Switch traffic
Switch-Traffic `
  -ServiceName "honua-api" `
  -GreenPercentage 25

# Start canary deployment
Start-CanaryDeployment `
  -ServiceName "honua-api" `
  -TrafficSteps @(10, 30, 60, 100) `
  -SoakDurationSeconds 300

# Check status
Get-TrafficStatus -ServiceName "honua-api"

# Rollback if needed
Rollback-Traffic -ServiceName "honua-api"
```

### 4. Using C# Client Library

```csharp
using Honua.Server.Gateway.Clients;

// Create client
var client = new TrafficManagementClient(
    baseUrl: "https://gateway.honua.io",
    apiToken: Environment.GetEnvironmentVariable("GATEWAY_TOKEN"));

// Switch traffic
var result = await client.SwitchTrafficAsync(
    serviceName: "honua-api",
    blueEndpoint: "http://api-blue:8080",
    greenEndpoint: "http://api-green:8080",
    greenPercentage: 50);

// Perform canary deployment
var strategy = new CanaryStrategy
{
    TrafficSteps = new List<int> { 10, 25, 50, 100 },
    SoakDurationSeconds = 300,
    AutoRollback = true
};

var canaryResult = await client.PerformCanaryDeploymentAsync(
    serviceName: "honua-api",
    blueEndpoint: "http://api-blue:8080",
    greenEndpoint: "http://api-green:8080",
    strategy: strategy);

if (canaryResult.Success)
{
    Console.WriteLine("Canary deployment completed successfully!");
}
else if (canaryResult.RolledBack)
{
    Console.WriteLine($"Deployment rolled back: {canaryResult.Message}");
}
```

## Configuration Schema Validation

The JSON configuration files include JSON Schema definitions for IDE autocomplete and validation.

**In VS Code:**

Install the "JSON Schema" extension and the schemas will be automatically recognized.

**Manual Validation:**

```bash
# Validate blue-green configuration
npm install -g ajv-cli
ajv validate -s blue-green-deployment.json -d your-config.json

# Validate canary configuration
ajv validate -s canary-deployment.json -d your-canary-config.json
```

## Kubernetes Deployment Patterns

### Pattern 1: Blue-Green with Manual Switch

```bash
# 1. Deploy green
kubectl apply -f - <<EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-api-green
spec:
  # ... green deployment spec
EOF

# 2. Wait for ready
kubectl rollout status deployment/honua-api-green -n honua

# 3. Switch traffic manually
curl -X POST http://gateway/admin/traffic/switch -d '{...}'
```

### Pattern 2: Automated Canary with Job

The `kubernetes-integration.yaml` includes a Job example that automates canary deployment:

```bash
# Create secret for gateway token
kubectl create secret generic gateway-api-token \
  --from-literal=token=$GATEWAY_TOKEN \
  -n honua

# Deploy green and trigger canary job
kubectl apply -f kubernetes-integration.yaml

# Monitor job progress
kubectl logs -f job/canary-deployment -n honua
```

### Pattern 3: GitOps with Argo CD/Flux

```yaml
# Example ArgoCD Application
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: honua-api-green
spec:
  project: default
  source:
    repoURL: https://github.com/your-org/honua-deployments
    targetRevision: main
    path: manifests/green
  destination:
    server: https://kubernetes.default.svc
    namespace: honua
  syncPolicy:
    automated:
      prune: false
      selfHeal: true
    syncOptions:
      - CreateNamespace=false
  # Post-sync hook to trigger traffic switch
  postSyncHook:
    exec:
      command:
        - /bin/sh
        - -c
        - |
          curl -X POST http://honua-gateway/admin/traffic/canary \
            -H "Authorization: Bearer $ARGOCD_APP_PARAMETERS_GATEWAY_TOKEN" \
            -d '{"serviceName":"api-cluster",...}'
```

## CI/CD Integration Examples

### GitHub Actions

```yaml
- name: Deploy and Canary
  run: |
    # Deploy green
    kubectl apply -f k8s/green/
    kubectl rollout status deployment/honua-api-green

    # Canary deployment
    curl -X POST ${{ secrets.GATEWAY_URL }}/admin/traffic/canary \
      -H "Authorization: Bearer ${{ secrets.GATEWAY_TOKEN }}" \
      -d @canary-config.json
```

### GitLab CI

```yaml
deploy:canary:
  stage: deploy
  script:
    - kubectl apply -f manifests/green/
    - kubectl wait --for=condition=available deployment/honua-api-green
    - |
      curl -X POST $GATEWAY_URL/admin/traffic/canary \
        -H "Authorization: Bearer $GATEWAY_TOKEN" \
        -d @.gitlab/canary-config.json
```

### Azure DevOps

```yaml
- task: Kubernetes@1
  inputs:
    command: apply
    arguments: -f manifests/green/

- task: Bash@3
  inputs:
    targetType: inline
    script: |
      curl -X POST $(GATEWAY_URL)/admin/traffic/canary \
        -H "Authorization: Bearer $(GATEWAY_TOKEN)" \
        -d @canary-config.json
```

## Monitoring and Observability

### Prometheus Queries

Monitor traffic distribution:

```promql
# Current traffic percentage
yarp_proxy_destination_weight{cluster="api-cluster",destination="green"}
/
sum(yarp_proxy_destination_weight{cluster="api-cluster"})

# Request rate by destination
rate(yarp_proxy_requests_total{cluster="api-cluster"}[5m])

# Error rate by destination
rate(yarp_proxy_requests_total{cluster="api-cluster",code=~"5.."}[5m])
/
rate(yarp_proxy_requests_total{cluster="api-cluster"}[5m])
```

### Grafana Dashboard

Import the included Grafana dashboard for traffic management visualization:

```bash
# Create Grafana dashboard from JSON
curl -X POST http://grafana:3000/api/dashboards/db \
  -H "Content-Type: application/json" \
  -d @grafana-dashboard.json
```

## Troubleshooting

### Issue: Traffic not switching

Check YARP logs:
```bash
kubectl logs -f deployment/honua-gateway -n honua | grep -i "traffic\|switch"
```

Verify configuration:
```bash
curl http://gateway/admin/traffic/status?serviceName=api-cluster
```

### Issue: Health checks failing

Test health endpoint directly:
```bash
kubectl exec -it deployment/honua-gateway -n honua -- \
  curl -v http://honua-api-green.honua.svc.cluster.local:8080/health
```

### Issue: Canary auto-rollback

Check metrics:
```bash
# Error rate
curl "http://prometheus:9090/api/v1/query?query=rate(http_requests_total{status=~\"5..\",destination=\"green\"}[5m])"

# Latency
curl "http://prometheus:9090/api/v1/query?query=histogram_quantile(0.95,rate(http_request_duration_seconds_bucket{destination=\"green\"}[5m]))"
```

## Best Practices

1. **Always test in staging first** - Validate deployment configuration in non-production environment
2. **Start with small traffic percentages** - Begin canary with 5-10% traffic
3. **Monitor metrics during rollout** - Watch error rates, latency, and resource usage
4. **Use health checks** - Configure readiness and liveness probes
5. **Enable auto-rollback** - Protect production from bad deployments
6. **Keep blue running** - Maintain old version for 24h after cutover for emergency rollback
7. **Use notifications** - Configure Slack/email alerts for deployment events
8. **Document rollback procedures** - Ensure team knows how to rollback quickly
9. **Test rollback in staging** - Validate rollback process works correctly
10. **Use semantic versioning** - Tag deployments with proper versions (v1.0.0, v1.1.0, etc.)

## Further Reading

- [TRAFFIC_MANAGEMENT.md](../TRAFFIC_MANAGEMENT.md) - Complete traffic management documentation
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/) - Official YARP documentation
- [Kubernetes Blue-Green Deployments](https://kubernetes.io/blog/2018/04/30/zero-downtime-deployment-kubernetes-jenkins/) - Kubernetes deployment patterns

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
