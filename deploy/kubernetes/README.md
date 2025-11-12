# Honua Server Kubernetes Deployment

Complete Kubernetes deployment resources for Honua Server, including production-ready Helm charts and deployment examples.

## Directory Structure

```
deploy/kubernetes/
├── helm/
│   └── honua-server/          # Production-ready Helm chart
│       ├── Chart.yaml          # Chart metadata
│       ├── values.yaml         # Default values
│       ├── values-dev.yaml     # Development environment
│       ├── values-staging.yaml # Staging environment
│       ├── values-production.yaml # Production environment
│       ├── README.md           # Chart documentation
│       └── templates/          # Kubernetes resource templates
├── examples/                   # Example deployment configurations
│   ├── basic-deployment.yaml
│   ├── external-database.yaml
│   ├── azure-deployment.yaml
│   ├── aws-deployment.yaml
│   ├── gcp-deployment.yaml
│   └── multi-region.yaml
└── scripts/                    # Helper scripts
    ├── preflight-check.sh      # Pre-deployment validation
    ├── create-secrets.sh       # Secret creation helper
    └── deploy.sh               # Deployment automation
```

## Quick Start

### 1. Prerequisites Check

Run the preflight check script to verify your cluster meets the requirements:

```bash
./deploy/kubernetes/scripts/preflight-check.sh --namespace honua
```

### 2. Create Secrets

For external databases, create necessary secrets:

```bash
# Interactive secret creation
./deploy/kubernetes/scripts/create-secrets.sh --namespace honua

# Or manually
kubectl create secret generic honua-db-secret \
  --from-literal=password='your-db-password' \
  --namespace honua

kubectl create secret generic honua-redis-secret \
  --from-literal=password='your-redis-password' \
  --namespace honua
```

### 3. Deploy

Use the deployment script for automated deployment:

```bash
# Development environment
./deploy/kubernetes/scripts/deploy.sh \
  --environment dev \
  --namespace honua-dev

# Staging environment
./deploy/kubernetes/scripts/deploy.sh \
  --environment staging \
  --namespace honua-staging

# Production environment
./deploy/kubernetes/scripts/deploy.sh \
  --environment production \
  --namespace honua-prod
```

Or use Helm directly:

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --create-namespace \
  --values ./deploy/kubernetes/helm/honua-server/values-production.yaml
```

## Deployment Options

### Development Environment

Minimal resources, embedded PostgreSQL and Redis, debug logging:

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua-dev \
  --create-namespace \
  --values ./deploy/kubernetes/helm/honua-server/values-dev.yaml
```

**Features:**
- Single replica
- Lite image variant (fast startup)
- Embedded PostgreSQL (no persistence)
- Embedded Redis (no persistence)
- Debug logging
- No network policies
- Relaxed security context

### Staging Environment

Production-like configuration with verbose logging:

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua-staging \
  --create-namespace \
  --values ./deploy/kubernetes/helm/honua-server/values-staging.yaml \
  --set database.host=postgres-staging.example.com \
  --set redis.host=redis-staging.example.com
```

**Features:**
- 2 replicas (auto-scales to 5)
- Full image variant
- External managed database
- External Redis cache
- Structured JSON logging
- OpenTelemetry enabled
- ServiceMonitor for Prometheus
- Network policies enabled

### Production Environment

Optimized for high availability and performance:

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua-prod \
  --create-namespace \
  --values ./deploy/kubernetes/helm/honua-server/values-production.yaml \
  --set database.host=postgres-prod.example.com \
  --set redis.host=redis-prod.example.com \
  --set ingress.hosts[0].host=honua.io
```

**Features:**
- 3 replicas (auto-scales to 20)
- Full image variant
- External managed database (RDS, Cloud SQL, etc.)
- External Redis cache (ElastiCache, Memorystore, etc.)
- Pod Disruption Budget (min 2 available)
- Pod anti-affinity (spread across nodes and zones)
- Network policies
- ServiceMonitor for monitoring
- TLS ingress
- Cloud secret management (AWS Secrets Manager, Azure Key Vault, GCP Secret Manager)

## Cloud-Specific Deployments

### AWS (EKS)

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --values ./deploy/kubernetes/examples/aws-deployment.yaml \
  --set serviceAccount.annotations."eks\.amazonaws\.com/role-arn"=arn:aws:iam::123456789012:role/honua-server
```

**Features:**
- IRSA for secrets access
- RDS PostgreSQL integration
- ElastiCache Redis integration
- ALB Ingress Controller
- AWS Secrets Manager

See: `examples/aws-deployment.yaml`

### Azure (AKS)

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --values ./deploy/kubernetes/examples/azure-deployment.yaml \
  --set serviceAccount.annotations."azure\.workload\.identity/client-id"=your-client-id
```

**Features:**
- Workload Identity for Key Vault access
- Azure PostgreSQL Flexible Server
- Azure Cache for Redis
- Application Gateway Ingress
- Azure Key Vault integration

See: `examples/azure-deployment.yaml`

### GCP (GKE)

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --values ./deploy/kubernetes/examples/gcp-deployment.yaml \
  --set serviceAccount.annotations."iam\.gke\.io/gcp-service-account"=honua-server@project.iam.gserviceaccount.com
```

**Features:**
- Workload Identity for Secret Manager
- Cloud SQL with Cloud SQL Proxy sidecar
- Memorystore Redis
- GCE Ingress Controller
- GCP Secret Manager integration

See: `examples/gcp-deployment.yaml`

## Configuration

### Image Variants

Choose between two image variants based on your workload:

**Full Image** (`image.variant: full`)
- Complete geospatial processing capabilities
- GDAL support for raster operations
- SkiaSharp for image rendering
- Cloud provider SDKs
- Size: ~500MB
- Use for: Full-featured deployments

**Lite Image** (`image.variant: lite`)
- Vector-only operations
- Minimal dependencies
- Fast cold starts (<2s)
- Size: ~60MB
- Use for: Serverless, development, vector-only workloads

### Resource Recommendations

| Environment | CPU Request | CPU Limit | Memory Request | Memory Limit |
|-------------|-------------|-----------|----------------|--------------|
| Dev         | 100m        | 500m      | 128Mi          | 512Mi        |
| Staging     | 500m        | 2000m     | 512Mi          | 2Gi          |
| Production  | 1000m       | 4000m     | 1Gi            | 4Gi          |

### Autoscaling

HPA is enabled by default with:
- Min replicas: 2 (dev: 1, prod: 3)
- Max replicas: 10 (prod: 20)
- Target CPU: 70%
- Target Memory: 80%

Customize in values:

```yaml
autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 20
  targetCPUUtilizationPercentage: 70
  targetMemoryUtilizationPercentage: 75
```

## Monitoring

### Prometheus Integration

Enable ServiceMonitor for Prometheus Operator:

```yaml
serviceMonitor:
  enabled: true
  namespace: observability
  interval: 30s
  labels:
    release: prometheus-operator
```

Metrics available at `/metrics` endpoint.

### Health Checks

| Endpoint | Purpose | Configuration |
|----------|---------|---------------|
| `/healthz/live` | Liveness probe | Check if app is running |
| `/healthz/ready` | Readiness probe | Check if app can serve traffic |
| `/healthz/startup` | Startup probe | Check initial startup |

## Security

### Network Policies

Enable network policies to restrict traffic:

```yaml
networkPolicy:
  enabled: true
```

Default policies allow:
- Ingress from ingress controller and gateway pods
- Egress to DNS, PostgreSQL, Redis, and HTTPS

### Pod Security

Default security context:
- Run as non-root user (UID 1000)
- Read-only root filesystem
- Drop all capabilities
- Seccomp profile: RuntimeDefault

### Secret Management

Supports multiple secret providers:

**Kubernetes Secrets** (default)
```yaml
secrets:
  provider: kubernetes
```

**Azure Key Vault**
```yaml
secrets:
  provider: azure-keyvault
  azureKeyVault:
    enabled: true
    name: honua-keyvault
    tenantId: "..."
```

**AWS Secrets Manager**
```yaml
secrets:
  provider: aws-secrets-manager
  awsSecretsManager:
    enabled: true
    region: us-east-1
```

**GCP Secret Manager**
```yaml
secrets:
  provider: gcp-secret-manager
  gcpSecretManager:
    enabled: true
    projectId: "..."
```

## Upgrading

### Rolling Update

```bash
# Update image version
helm upgrade honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --set image.tag=1.1.0 \
  --wait

# Verify rollout
kubectl rollout status deployment/honua-server -n honua
```

### Rollback

```bash
# View release history
helm history honua-server -n honua

# Rollback to previous version
helm rollback honua-server -n honua

# Rollback to specific revision
helm rollback honua-server 3 -n honua
```

## Troubleshooting

### Common Issues

**Pods not starting**
```bash
kubectl get pods -n honua
kubectl describe pod <pod-name> -n honua
kubectl logs <pod-name> -n honua
```

**Database connection issues**
```bash
# Verify secret
kubectl get secret honua-server-secret -n honua -o yaml

# Test connectivity
kubectl run -it --rm debug --image=postgres:15 --restart=Never -- \
  psql -h <host> -U <user> -d <db>
```

**Resource constraints**
```bash
# Check resource usage
kubectl top nodes
kubectl top pods -n honua

# Check HPA status
kubectl get hpa -n honua
```

See the [Helm chart README](./helm/honua-server/README.md) for detailed troubleshooting.

## Support

- Documentation: https://github.com/honua-io/Honua.Server
- Issues: https://github.com/honua-io/Honua.Server/issues
- Email: support@honua.io

## License

Elastic License 2.0
