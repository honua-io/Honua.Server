# Honua Production Kubernetes Deployment Summary

## Created Files

| File | Size | Purpose | Documents |
|------|------|---------|-----------|
| `00-namespace.yaml` | 864B | Namespace, ResourceQuota, LimitRange | 3 |
| `01-secrets.yaml` | 2.0K | Database, Redis, JWT, Storage secrets (TEMPLATE) | 4 |
| `02-configmap.yaml` | 2.7K | Application configuration | 1 |
| `03-deployment.yaml` | 8.0K | Main Honua server deployment | 1 |
| `04-service.yaml` | 1.3K | ClusterIP and Headless services | 2 |
| `05-serviceaccount.yaml` | 932B | ServiceAccount with cloud provider IRSA | 2 |
| `06-ingress.yaml` | 3.6K | NGINX Ingress with TLS and security headers | 2 |
| `07-hpa.yaml` | 4.6K | HorizontalPodAutoscaler (CPU/Memory) | 2 |
| `08-pdb.yaml` | 474B | PodDisruptionBudget | 1 |
| `09-networkpolicy.yaml` | 1.9K | Network security policies | 2 |
| `10-servicemonitor.yaml` | 4.0K | Prometheus ServiceMonitor and alerts | 2 |
| `kustomization.yaml` | 1.1K | Kustomize configuration | 1 |
| `validate.sh` | executable | Validation script | - |
| `README.md` | 12K | Comprehensive deployment guide | - |

**Total:** 11 manifest files, 23 Kubernetes resources

## Key Security Features Implemented

### Pod Security
- [x] `runAsNonRoot: true` - All containers run as non-root user (UID 1000)
- [x] `readOnlyRootFilesystem: true` - Immutable root filesystem
- [x] `allowPrivilegeEscalation: false` - Prevent privilege escalation
- [x] All Linux capabilities dropped (`drop: [ALL]`)
- [x] `seccompProfile: RuntimeDefault` - Secure computing mode enabled
- [x] Explicit user/group IDs set (1000/3000/2000)

### Resource Management
- [x] CPU requests: 500m, limits: 2000m per pod
- [x] Memory requests: 1Gi, limits: 4Gi per pod
- [x] Ephemeral storage limits: 2Gi per pod
- [x] Namespace ResourceQuota: 20 CPU, 40Gi RAM total
- [x] LimitRange: Container defaults and min/max bounds

### Network Security
- [x] NetworkPolicy with default deny-all
- [x] Explicit ingress rules (ingress-controller only)
- [x] Explicit egress rules (database, cache, DNS, HTTPS)
- [x] Internal metrics endpoint with IP whitelist

### High Availability
- [x] 3 replica minimum (configurable via HPA)
- [x] PodDisruptionBudget: min 2 available during disruptions
- [x] Pod anti-affinity across nodes and zones
- [x] Rolling updates: maxSurge=1, maxUnavailable=0 (zero downtime)
- [x] Graceful shutdown: 60s termination grace period

### Health Monitoring
- [x] Liveness probe: `/health/live` (restart unhealthy pods)
- [x] Readiness probe: `/health/ready` (traffic management)
- [x] Startup probe: `/health/startup` (slow-start handling)
- [x] Init containers: Wait for database and Redis
- [x] Lifecycle preStop hook for graceful shutdown

### Autoscaling
- [x] HorizontalPodAutoscaler configured
- [x] CPU target: 70% utilization
- [x] Memory target: 80% utilization
- [x] Scale range: 3-20 replicas
- [x] Aggressive scale-up (100% or 4 pods per 30s)
- [x] Conservative scale-down (50% or 2 pods per 60s, 5min stabilization)

### Observability
- [x] Prometheus metrics endpoint: `/metrics`
- [x] ServiceMonitor for automatic scraping
- [x] Alert rules for common issues:
  - High error rate (>5%)
  - High response time (P95 > 2s)
  - Pod unavailability
  - High resource usage (>90%)
  - Restart loops
- [x] JSON structured logging
- [x] OTLP tracing support (configurable)

### Ingress & TLS
- [x] NGINX Ingress Controller support
- [x] AWS ALB annotations (commented)
- [x] GCP GCE annotations (commented)
- [x] cert-manager integration for TLS
- [x] Security headers (HSTS, X-Frame-Options, CSP, etc.)
- [x] CORS configuration
- [x] Rate limiting annotations
- [x] Request body size limit: 1024MB

### Secret Management
- [x] Kubernetes Secrets for sensitive data
- [x] Template placeholders for production values
- [x] Support for AWS IRSA (IAM Roles for Service Accounts)
- [x] Support for GCP Workload Identity
- [x] Support for Azure Workload Identity
- [x] Separated secrets by purpose (DB, cache, JWT, storage)

## Configuration Highlights

### Deployment Configuration
```yaml
Replicas: 3 (minimum, managed by HPA)
Image: honuaio/honua-server:latest
Port: 8080 (HTTP)
Strategy: RollingUpdate (maxSurge=1, maxUnavailable=0)
```

### Resource Allocation (per pod)
```yaml
Requests:
  CPU: 500m (0.5 cores)
  Memory: 1Gi
  Ephemeral Storage: 1Gi

Limits:
  CPU: 2000m (2 cores)
  Memory: 4Gi
  Ephemeral Storage: 2Gi
```

### Health Probes
```yaml
Liveness:  /health/live  (30s delay, 10s period, 5s timeout, 3 failures)
Readiness: /health/ready (10s delay, 5s period, 3s timeout, 3 failures)
Startup:   /health/startup (0s delay, 5s period, 3s timeout, 30 failures = 150s max)
```

### Environment Configuration
- Database: PostgreSQL/PostGIS (external service)
- Cache: Redis (external service)
- Authentication: Local mode with JWT
- Logging: JSON console output
- Metrics: Prometheus format at `/metrics`
- Tracing: OTLP exporter (configurable)

## Validation Results

```
✓ All 11 manifest files have valid YAML syntax
✓ Kubernetes manifest structure is valid
✓ 23 Kubernetes resources defined
✓ Security contexts properly configured
✓ Resource limits defined
✓ Health probes configured
✓ High availability features enabled
✓ Autoscaling configured
✓ Network policies applied
✓ Monitoring configured
```

### Pre-Deployment Checklist

Before deploying to production, update the following:

- [ ] **01-secrets.yaml**: Replace all `CHANGE_ME` placeholders
  - Database password (strong, 32+ characters)
  - Redis password
  - JWT signing key (generate: `openssl rand -base64 32`)
  - Cloud storage credentials (AWS/Azure/GCS)

- [ ] **06-ingress.yaml**: Update domain names
  - Replace `honua.example.com` with your actual domain
  - Replace `api.honua.example.com` with your API domain
  - Update TLS certificate secret names

- [ ] **05-serviceaccount.yaml**: Update cloud provider IDs
  - AWS: Replace `ACCOUNT_ID` with AWS account ID
  - GCP: Replace `PROJECT_ID` with GCP project ID
  - Azure: Replace `CLIENT_ID` with Azure client ID

- [ ] **02-configmap.yaml**: Review environment-specific settings
  - Database host/port
  - Redis host/port
  - OTLP endpoint (if using distributed tracing)
  - CORS allowed origins
  - Rate limiting values

- [ ] **03-deployment.yaml**: Review resource allocation
  - Adjust CPU/memory based on workload
  - Update image reference with your registry
  - Review replica count and HPA settings

## Quick Deployment Commands

### Using kubectl
```bash
# Navigate to the directory
cd /home/mike/projects/HonuaIO/deploy/kubernetes/production

# Validate manifests
./validate.sh

# Apply all manifests (creates resources in order)
kubectl apply -f 00-namespace.yaml
kubectl apply -f 01-secrets.yaml
kubectl apply -f 02-configmap.yaml
kubectl apply -f 03-deployment.yaml
kubectl apply -f 04-service.yaml
kubectl apply -f 05-serviceaccount.yaml
kubectl apply -f 06-ingress.yaml
kubectl apply -f 07-hpa.yaml
kubectl apply -f 08-pdb.yaml
kubectl apply -f 09-networkpolicy.yaml
kubectl apply -f 10-servicemonitor.yaml  # Only if Prometheus Operator is installed

# Or apply all at once
kubectl apply -f .

# Check deployment status
kubectl get all -n honua
kubectl get pods -n honua -w
```

### Using Kustomize
```bash
# Build and preview
kubectl kustomize . | less

# Apply with kustomize
kubectl apply -k .

# Or using kustomize directly
kustomize build . | kubectl apply -f -
```

## Verification Commands

```bash
# Check namespace resources
kubectl get all -n honua

# Check pod status and logs
kubectl get pods -n honua
kubectl logs -n honua -l app.kubernetes.io/name=honua-server --tail=100 -f

# Check service endpoints
kubectl get endpoints -n honua

# Check ingress
kubectl get ingress -n honua
kubectl describe ingress -n honua honua-server

# Check HPA status
kubectl get hpa -n honua
kubectl describe hpa -n honua honua-server

# Check PDB
kubectl get pdb -n honua

# Port-forward for local testing
kubectl port-forward -n honua svc/honua-server 8080:80

# Test health endpoints (in another terminal)
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
curl http://localhost:8080/metrics
```

## Cloud Provider Specific Notes

### AWS EKS
- IRSA configured in `05-serviceaccount.yaml`
- ALB Ingress annotations available in `06-ingress.yaml` (commented)
- EBS CSI driver recommended for persistent volumes
- Consider using AWS Secrets Manager with External Secrets Operator

### GCP GKE
- Workload Identity configured in `05-serviceaccount.yaml`
- GCE Ingress annotations available in `06-ingress.yaml` (commented)
- Consider using Google Secret Manager with External Secrets Operator

### Azure AKS
- Workload Identity configured in `05-serviceaccount.yaml`
- Application Gateway Ingress Controller supported
- Consider using Azure Key Vault with CSI driver

## Monitoring Integration

If Prometheus Operator is installed:
```bash
# Apply ServiceMonitor
kubectl apply -f 10-servicemonitor.yaml

# Check ServiceMonitor status
kubectl get servicemonitor -n honua

# Verify metrics are being scraped
kubectl port-forward -n monitoring svc/prometheus-operated 9090:9090
# Visit http://localhost:9090 and query: honua_*
```

## Troubleshooting

See the comprehensive README.md for detailed troubleshooting steps including:
- Pod startup issues
- Database connection problems
- Ingress/TLS issues
- Performance problems
- Resource constraints

## Support

- Full documentation: `/home/mike/projects/HonuaIO/docs/`
- Deployment guides: `/home/mike/projects/HonuaIO/docs/deployment/`
- README: `README.md` in this directory

## License

See main project LICENSE file.
