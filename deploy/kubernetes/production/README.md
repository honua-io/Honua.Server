# Honua Production Kubernetes Manifests

This directory contains production-ready Kubernetes manifests for deploying the Honua OGC-compliant geospatial data server.

## Overview

These manifests implement production-grade security, reliability, and observability features:

- **Security**: Pod Security Standards, read-only root filesystem, non-root execution, secret management
- **Reliability**: Resource limits, health probes, PodDisruptionBudget, rolling updates
- **Scalability**: HorizontalPodAutoscaler (HPA) with CPU/memory metrics
- **Observability**: Prometheus metrics, ServiceMonitor, alerting rules
- **Network Security**: NetworkPolicy for pod-to-pod communication control

## Prerequisites

Before deploying, ensure you have:

1. **Kubernetes Cluster** (v1.24+)
   - EKS, GKE, AKS, or self-managed cluster
   - Nodes with at least 4 CPU cores and 8GB RAM

2. **Required Components**:
   - Ingress Controller (NGINX, ALB, or Traefik)
   - cert-manager (for TLS certificates)
   - metrics-server (for HPA)
   - Prometheus Operator (optional, for ServiceMonitor)

3. **External Dependencies**:
   - PostgreSQL/PostGIS database
   - Redis cache
   - Container registry with Honua image

4. **CLI Tools**:
   - kubectl (v1.24+)
   - helm (v3.0+) - optional

## File Structure

```
production/
├── 01-namespace.yaml                  # Namespace with Pod Security Standards labels
├── 02-deployment.yaml                 # Honua Server deployment with security hardening
├── 03-podsecuritypolicy.yaml         # PodSecurityPolicy for K8s < 1.25 (deprecated)
├── 04-podsecurity-standards.yaml     # Pod Security Standards documentation (K8s 1.25+)
├── 05-database-statefulset.yaml      # PostGIS StatefulSet with security configuration
├── 06-redis-statefulset.yaml         # Redis StatefulSet with security hardening
├── hpa.yaml                          # HorizontalPodAutoscaler with PodDisruptionBudget
└── README.md                         # This file
```

## Quick Start

### 1. Update Configuration

**IMPORTANT**: Before deploying, update the following:

#### a. Secrets (`01-secrets.yaml`)
```bash
# Edit secrets file
vi 01-secrets.yaml

# Update the following values:
# - Database credentials
# - Redis password
# - JWT signing key (generate with: openssl rand -base64 32)
# - Cloud storage credentials (AWS/Azure/GCS)
```

#### b. ConfigMap (`02-configmap.yaml`)
```bash
# Update environment-specific values:
# - HONUA__DATABASE__HOST
# - HONUA__CACHE__REDIS__HOST
# - OBSERVABILITY__TRACING__OTLPENDPOINT (if using distributed tracing)
```

#### c. Ingress (`06-ingress.yaml`)
```bash
# Update domain names:
# - honua.example.com -> your actual domain
# - api.honua.example.com -> your API domain
# - Update TLS secret names if using cert-manager
```

#### d. ServiceAccount (`05-serviceaccount.yaml`)
```bash
# Update cloud provider annotations:
# For AWS EKS - Update ACCOUNT_ID
# For GCP GKE - Update PROJECT_ID
# For Azure AKS - Update CLIENT_ID
```

### 2. Deploy to Kubernetes

```bash
# Create namespace and apply all manifests
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

# Optional: Apply ServiceMonitor if Prometheus Operator is installed
kubectl apply -f 10-servicemonitor.yaml

# Or apply all at once
kubectl apply -f .
```

### 3. Verify Deployment

```bash
# Check namespace
kubectl get namespace honua

# Check all resources
kubectl get all -n honua

# Check pod status
kubectl get pods -n honua -l app.kubernetes.io/name=honua-server

# Check pod logs
kubectl logs -n honua -l app.kubernetes.io/name=honua-server --tail=100 -f

# Check service endpoints
kubectl get endpoints -n honua honua-server

# Check ingress
kubectl get ingress -n honua

# Check HPA status
kubectl get hpa -n honua honua-server

# Check PDB status
kubectl get pdb -n honua honua-server
```

### 4. Health Checks

```bash
# Port-forward to check health endpoints locally
kubectl port-forward -n honua svc/honua-server 8080:80

# In another terminal:
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
curl http://localhost:8080/health/startup
curl http://localhost:8080/metrics
```

## Configuration Details

### Resource Limits

Default resource allocation per pod:
- **Requests**: 500m CPU, 1Gi memory, 1Gi ephemeral storage
- **Limits**: 2000m CPU, 4Gi memory, 2Gi ephemeral storage

Adjust based on your workload:
```yaml
resources:
  requests:
    cpu: 1000m      # Increase for higher throughput
    memory: 2Gi     # Increase for larger datasets
  limits:
    cpu: 4000m
    memory: 8Gi
```

### Horizontal Pod Autoscaling

Default HPA configuration:
- **Min Replicas**: 2
- **Max Replicas**: 10
- **Target CPU**: 70%
- **Target Memory**: 80%

Scale behavior:
- **Scale Up**: Immediate (0s stabilization, 100% or 4 pods per 30s)
- **Scale Down**: Conservative (300s stabilization, 50% or 2 pods per 60s)

The HPA automatically scales the Honua Server deployment based on CPU and memory utilization, ensuring optimal resource usage and performance during traffic spikes.

### Security Features

#### 1. Pod Security Context (Container Level)

All containers are configured with comprehensive security hardening:

- `runAsNonRoot: true` - Prevents running as root user
- `runAsUser: 1000` - Runs as non-privileged user (UID 1000 for app, 999 for DB/Redis)
- `runAsGroup: 1000` - Runs with non-privileged group
- `readOnlyRootFilesystem: true` - Makes root filesystem read-only (where possible)
- `allowPrivilegeEscalation: false` - Prevents privilege escalation
- `capabilities.drop: [ALL]` - Drops all Linux capabilities
- `capabilities.add: [NET_BIND_SERVICE]` - Adds only necessary capabilities
- `seccompProfile: RuntimeDefault` - Enables secure computing mode

#### 2. Pod Security Standards (K8s 1.25+)

The namespace is enforced with the **restricted** security standard:

```yaml
pod-security.kubernetes.io/enforce: restricted
pod-security.kubernetes.io/audit: restricted
pod-security.kubernetes.io/warn: restricted
```

This is the most restrictive level and follows current pod hardening best practices.

#### 3. PodSecurityPolicy (K8s < 1.25 - Deprecated)

For older Kubernetes versions, PodSecurityPolicy resources are provided:
- `honua-restricted` - Restrictive policy for application pods
- `honua-database` - Slightly relaxed policy for database components

**Note**: PodSecurityPolicy is deprecated in K8s 1.21+ and removed in 1.25+.

#### 4. Network Policies

NetworkPolicy resources restrict network traffic to minimum required:

- **PostGIS**: Only accepts connections from Honua server pods on port 5432
- **Redis**: Only accepts connections from Honua server pods on port 6379
- **Egress**: Limited to DNS and necessary outbound traffic
- **Default**: Deny all traffic not explicitly allowed

#### 5. Service Accounts

Each component has dedicated ServiceAccount with minimal privileges:

- `automountServiceAccountToken: false` - Prevents automatic token mounting
- RBAC roles with least-privilege access
- Separate service accounts: honua-server, postgis, redis

#### 6. Secret Management

- Kubernetes Secrets for sensitive data
- Support for External Secrets Operator
- Support for cloud provider secret managers (AWS Secrets Manager, Azure Key Vault, GCP Secret Manager)

### High Availability

1. **Pod Distribution**:
   - Anti-affinity rules spread pods across nodes and zones
   - Minimum 3 replicas at all times
   - PodDisruptionBudget ensures minimum 2 available during updates

2. **Rolling Updates**:
   - `maxSurge: 1` - Create 1 extra pod during update
   - `maxUnavailable: 0` - Zero downtime deployments
   - Graceful shutdown with 60s termination period

3. **Health Probes**:
   - **Startup**: 30 attempts * 5s = 150s max startup time
   - **Liveness**: Restart unhealthy pods
   - **Readiness**: Remove unhealthy pods from service

## Cloud Provider Specific Configuration

### AWS EKS

1. **IRSA (IAM Roles for Service Accounts)**:
   ```bash
   # Create IAM role
   eksctl create iamserviceaccount \
     --name honua-server \
     --namespace honua \
     --cluster <cluster-name> \
     --attach-policy-arn arn:aws:iam::aws:policy/AmazonS3ReadOnlyAccess \
     --approve
   ```

2. **ALB Ingress Controller**:
   - Uncomment ALB annotations in `06-ingress.yaml`
   - Update certificate ARN

3. **EBS CSI Driver** (for persistent volumes):
   ```bash
   kubectl apply -k "github.com/kubernetes-sigs/aws-ebs-csi-driver/deploy/kubernetes/overlays/stable/?ref=release-1.25"
   ```

### GCP GKE

1. **Workload Identity**:
   ```bash
   # Create service account
   gcloud iam service-accounts create honua-server

   # Bind to Kubernetes SA
   gcloud iam service-accounts add-iam-policy-binding \
     honua-server@PROJECT_ID.iam.gserviceaccount.com \
     --role roles/iam.workloadIdentityUser \
     --member "serviceAccount:PROJECT_ID.svc.id.goog[honua/honua-server]"
   ```

2. **GCE Ingress**:
   - Uncomment GCE annotations in `06-ingress.yaml`
   - Reserve static IP address

### Azure AKS

1. **Workload Identity**:
   ```bash
   # Enable workload identity
   az aks update \
     --resource-group <rg> \
     --name <cluster> \
     --enable-oidc-issuer \
     --enable-workload-identity
   ```

2. **Application Gateway Ingress**:
   - Configure AGIC annotations in `06-ingress.yaml`

## Monitoring and Observability

### Prometheus Metrics

The application exposes Prometheus metrics at `/metrics`:
- HTTP request rates and latencies
- Database connection pool metrics
- Cache hit/miss rates
- Custom business metrics

### Grafana Dashboards

Import pre-built dashboards:
1. Kubernetes Pod Metrics (Dashboard ID: 6417)
2. Kubernetes Deployment Metrics (Dashboard ID: 8588)
3. NGINX Ingress Controller (Dashboard ID: 9614)

### Alerts

Configured alerts in `10-servicemonitor.yaml`:
- High error rate (>5% for 5min)
- High response time (P95 > 2s)
- Pod unavailability
- High memory/CPU usage (>90%)
- Pod restart loops

## Troubleshooting

### Pods Not Starting

```bash
# Check pod events
kubectl describe pod -n honua <pod-name>

# Check init container logs
kubectl logs -n honua <pod-name> -c wait-for-database
kubectl logs -n honua <pod-name> -c wait-for-redis

# Check main container logs
kubectl logs -n honua <pod-name> -c honua-server
```

### Database Connection Issues

```bash
# Test database connectivity from pod
kubectl exec -n honua <pod-name> -- nc -zv postgis-service 5432

# Check database secret
kubectl get secret -n honua honua-database-secret -o yaml
```

### Ingress Not Working

```bash
# Check ingress status
kubectl describe ingress -n honua honua-server

# Check ingress controller logs
kubectl logs -n ingress-nginx -l app.kubernetes.io/name=ingress-nginx

# Check certificate status (if using cert-manager)
kubectl get certificate -n honua
kubectl describe certificate -n honua honua-tls-cert
```

### Performance Issues

```bash
# Check HPA status and metrics
kubectl get hpa -n honua honua-server
kubectl describe hpa -n honua honua-server

# Check resource usage
kubectl top pods -n honua

# Check for throttling
kubectl describe pod -n honua <pod-name> | grep -i throttl
```

## Upgrading

### Rolling Update

```bash
# Update image tag in deployment
kubectl set image deployment/honua-server -n honua \
  honua-server=honuaio/honua-server:v2.0.0

# Watch rollout status
kubectl rollout status deployment/honua-server -n honua

# Check rollout history
kubectl rollout history deployment/honua-server -n honua
```

### Rollback

```bash
# Rollback to previous version
kubectl rollout undo deployment/honua-server -n honua

# Rollback to specific revision
kubectl rollout undo deployment/honua-server -n honua --to-revision=3
```

## Backup and Disaster Recovery

### ConfigMap and Secrets Backup

```bash
# Export all resources
kubectl get configmap,secret -n honua -o yaml > honua-backup.yaml

# Restore
kubectl apply -f honua-backup.yaml
```

### Database Backup

Refer to your PostgreSQL backup strategy (pg_dump, WAL archiving, cloud snapshots).

## Security Validation

### Automated Security Checks

Run the security validation script to verify all security configurations:

```bash
./validate-security.sh
```

This script checks:
- Pod Security Standards configuration
- SecurityContext settings (pod and container level)
- Capabilities dropped and added
- Read-only root filesystem
- Non-root user execution
- Service account configuration
- Network policies
- Secret management

### Manual Security Verification

#### 1. Verify Pod Security Standards

```bash
# Check namespace labels
kubectl get namespace honua -o yaml | grep pod-security

# Expected output:
# pod-security.kubernetes.io/audit: restricted
# pod-security.kubernetes.io/enforce: restricted
# pod-security.kubernetes.io/warn: restricted
```

#### 2. Verify SecurityContext Settings

```bash
# Check pod-level security context
kubectl get pod -n honua -l app=honua-server -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{.spec.securityContext}{"\n"}{end}' | jq

# Check container-level security context
kubectl get pod -n honua -l app=honua-server -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{.spec.containers[*].securityContext}{"\n"}{end}' | jq
```

#### 3. Verify Non-Root Execution

```bash
# Get a pod name
POD_NAME=$(kubectl get pod -n honua -l app=honua-server -o jsonpath='{.items[0].metadata.name}')

# Check which user the process is running as
kubectl exec -n honua $POD_NAME -- id

# Expected output:
# uid=1000 gid=1000 groups=1000
```

#### 4. Verify Read-Only Root Filesystem

```bash
# Try to write to root filesystem (should fail)
kubectl exec -n honua $POD_NAME -- touch /test

# Expected output:
# touch: /test: Read-only file system
```

#### 5. Verify Capabilities

```bash
# Check process capabilities
kubectl exec -n honua $POD_NAME -- grep Cap /proc/1/status

# Verify only NET_BIND_SERVICE is present
kubectl exec -n honua $POD_NAME -- sh -c "capsh --decode=\$(grep CapEff /proc/1/status | awk '{print \$2}')"
```

#### 6. Verify Network Policies

```bash
# List network policies
kubectl get networkpolicy -n honua

# Describe specific policy
kubectl describe networkpolicy postgis-netpol -n honua
kubectl describe networkpolicy redis-netpol -n honua
```

#### 7. Test Pod Security Admission

```bash
# Try to create a privileged pod (should be rejected)
cat <<EOF | kubectl apply -f - --dry-run=server
apiVersion: v1
kind: Pod
metadata:
  name: test-privileged
  namespace: honua
spec:
  containers:
  - name: test
    image: nginx
    securityContext:
      privileged: true
EOF

# Expected: Error from server (Forbidden): admission webhook denied
```

## Security Best Practices

1. **Use External Secret Management**:
   - AWS Secrets Manager with External Secrets Operator
   - Azure Key Vault with CSI Driver
   - HashiCorp Vault
   - Sealed Secrets

2. **Enable Pod Security Standards**:
   ```bash
   # Already configured in 01-namespace.yaml
   kubectl label namespace honua pod-security.kubernetes.io/enforce=restricted
   kubectl label namespace honua pod-security.kubernetes.io/audit=restricted
   kubectl label namespace honua pod-security.kubernetes.io/warn=restricted
   ```

3. **Regular Security Scanning**:
   - Image scanning with Trivy/Grype
   - Kubernetes manifest scanning with Kubesec/Polaris
   - Runtime security with Falco
   - Vulnerability scanning with Snyk

4. **Network Policies**:
   - Already configured in database and cache StatefulSets
   - Review and adjust based on your network requirements
   - Consider using a service mesh (Istio, Linkerd) for advanced traffic control

5. **Image Security**:
   - Use minimal base images (alpine, distroless)
   - Scan images for vulnerabilities before deployment
   - Use specific image tags (not :latest)
   - Sign images with Cosign/Notary
   - Use private registries with authentication

6. **Admission Controllers**:
   - Use OPA Gatekeeper or Kyverno for policy enforcement
   - Enforce image signing verification
   - Require resource limits on all pods
   - Block privileged containers

7. **Runtime Security**:
   - Deploy Falco for runtime threat detection
   - Enable audit logging
   - Use AppArmor or SELinux profiles
   - Monitor suspicious process behavior

## Security Compliance Checklist

Use this checklist to verify your deployment meets security requirements:

- [ ] All pods run as non-root user (UID 1000 or 999)
- [ ] Read-only root filesystem enabled (where applicable)
- [ ] No privilege escalation allowed
- [ ] All capabilities dropped, only NET_BIND_SERVICE added
- [ ] Seccomp profile enabled (RuntimeDefault)
- [ ] Pod Security Standards enforced (restricted level)
- [ ] Network policies restrict ingress/egress traffic
- [ ] Service accounts with minimal privileges
- [ ] Secrets properly managed (not in ConfigMaps)
- [ ] Resource limits and requests defined
- [ ] Health checks configured (liveness, readiness, startup)
- [ ] TLS/HTTPS enabled for all external traffic
- [ ] Database credentials stored securely
- [ ] Regular security updates applied
- [ ] Container images scanned for vulnerabilities
- [ ] Audit logging enabled
- [ ] Network traffic encrypted in transit
- [ ] Persistent volumes encrypted at rest

## Performance Tuning

### Database Connection Pooling

Adjust in `01-secrets.yaml` connection string:
```
MinPoolSize=5;MaxPoolSize=100;ConnectionLifetime=600
```

### Memory Cache

Adjust in `02-configmap.yaml`:
```yaml
PERFORMANCE__MEMORYCACHE__MAXSIZEMB: "500"  # Increase for caching
```

### Redis Configuration

Consider Redis Cluster or Sentinel for HA:
```yaml
HONUA__CACHE__REDIS__CONFIGURATION: "redis-cluster:6379,redis-cluster:6380,redis-cluster:6381"
```

## Support and Documentation

- **Project Documentation**: `/home/mike/projects/HonuaIO/docs/`
- **API Documentation**: `/home/mike/projects/HonuaIO/docs/api/`
- **Deployment Guides**: `/home/mike/projects/HonuaIO/docs/deployment/`
- **Issue Tracker**: GitHub Issues

## License

See main project LICENSE file.
