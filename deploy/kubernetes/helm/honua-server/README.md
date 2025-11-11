# Honua Server Helm Chart

Production-ready Helm chart for deploying Honua Server on Kubernetes.

## Prerequisites

- Kubernetes 1.23+
- Helm 3.8+
- PV provisioner support in the underlying infrastructure (if persistence is enabled)
- PostgreSQL 12+ (external or subchart)
- Redis 6+ (optional, for caching)

## Installation

### Quick Start

```bash
# Add the Helm repository (if published)
helm repo add honua https://charts.honua.io
helm repo update

# Install with default values
helm install honua-server honua/honua-server \
  --namespace honua \
  --create-namespace

# Or install from local chart
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --create-namespace
```

### Environment-Specific Installations

#### Development Environment

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua-dev \
  --create-namespace \
  --values ./deploy/kubernetes/helm/honua-server/values-dev.yaml
```

#### Staging Environment

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua-staging \
  --create-namespace \
  --values ./deploy/kubernetes/helm/honua-server/values-staging.yaml \
  --set database.host=postgres-staging.example.com \
  --set database.existingSecret=honua-db-staging \
  --set redis.host=redis-staging.example.com
```

#### Production Environment

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua-prod \
  --create-namespace \
  --values ./deploy/kubernetes/helm/honua-server/values-production.yaml \
  --set database.host=postgres-prod.cluster-xyz.us-east-1.rds.amazonaws.com \
  --set database.existingSecret=honua-db-prod \
  --set redis.host=redis-prod.cache.amazonaws.com \
  --set redis.existingSecret=honua-redis-prod \
  --set ingress.hosts[0].host=honua.io
```

## Configuration

### Image Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `image.registry` | Image registry | `ghcr.io` |
| `image.repository` | Image repository | `honua-io/honua-server` |
| `image.variant` | Image variant (`full` or `lite`) | `full` |
| `image.tag` | Image tag | Chart appVersion |
| `image.pullPolicy` | Image pull policy | `IfNotPresent` |
| `image.pullSecrets` | Image pull secrets | `[]` |

**Image Variants:**
- **full**: Complete image with GDAL, raster processing, cloud SDKs (~500MB)
- **lite**: Lightweight image for vector-only workloads (~60MB, faster cold starts)

### Deployment Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `replicaCount` | Number of replicas | `2` |
| `strategy.type` | Deployment strategy | `RollingUpdate` |
| `strategy.rollingUpdate.maxSurge` | Max surge during update | `1` |
| `strategy.rollingUpdate.maxUnavailable` | Max unavailable during update | `0` |

### Resource Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `resources.limits.cpu` | CPU limit | `2000m` |
| `resources.limits.memory` | Memory limit | `2Gi` |
| `resources.requests.cpu` | CPU request | `500m` |
| `resources.requests.memory` | Memory request | `512Mi` |

### Autoscaling Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `autoscaling.enabled` | Enable HPA | `true` |
| `autoscaling.minReplicas` | Minimum replicas | `2` |
| `autoscaling.maxReplicas` | Maximum replicas | `10` |
| `autoscaling.targetCPUUtilizationPercentage` | Target CPU % | `70` |
| `autoscaling.targetMemoryUtilizationPercentage` | Target Memory % | `80` |

### Database Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `database.external` | Use external PostgreSQL | `false` |
| `database.host` | Database host | `""` |
| `database.port` | Database port | `5432` |
| `database.name` | Database name | `honua` |
| `database.username` | Database username | `honua` |
| `database.existingSecret` | Existing secret name | `""` |
| `database.sslMode` | SSL mode | `require` |
| `database.pooling.minPoolSize` | Min connection pool size | `5` |
| `database.pooling.maxPoolSize` | Max connection pool size | `100` |

### Redis Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `redis.external` | Use external Redis | `false` |
| `redis.host` | Redis host | `""` |
| `redis.port` | Redis port | `6379` |
| `redis.existingSecret` | Existing secret name | `""` |
| `redis.ssl.enabled` | Enable SSL/TLS | `false` |

### Ingress Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `ingress.enabled` | Enable ingress | `false` |
| `ingress.className` | Ingress class name | `nginx` |
| `ingress.annotations` | Ingress annotations | `{}` |
| `ingress.hosts` | Ingress hosts | See values.yaml |
| `ingress.tls` | TLS configuration | `[]` |

### Monitoring Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `serviceMonitor.enabled` | Enable ServiceMonitor for Prometheus | `false` |
| `serviceMonitor.namespace` | ServiceMonitor namespace | `""` |
| `serviceMonitor.interval` | Scrape interval | `30s` |
| `serviceMonitor.scrapeTimeout` | Scrape timeout | `10s` |

### Security Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `podSecurityContext.runAsNonRoot` | Run as non-root user | `true` |
| `podSecurityContext.runAsUser` | User ID | `1000` |
| `networkPolicy.enabled` | Enable NetworkPolicy | `false` |
| `secrets.provider` | Secrets provider | `kubernetes` |

## Advanced Configuration

### Using Azure Key Vault

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --set secrets.provider=azure-keyvault \
  --set secrets.azureKeyVault.enabled=true \
  --set secrets.azureKeyVault.name=honua-keyvault \
  --set secrets.azureKeyVault.tenantId=your-tenant-id \
  --set serviceAccount.annotations."azure\.workload\.identity/client-id"=your-client-id
```

### Using AWS Secrets Manager

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --set secrets.provider=aws-secrets-manager \
  --set secrets.awsSecretsManager.enabled=true \
  --set secrets.awsSecretsManager.region=us-east-1 \
  --set serviceAccount.annotations."eks\.amazonaws\.com/role-arn"=arn:aws:iam::123456789012:role/honua-server
```

### Using GCP Secret Manager

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --set secrets.provider=gcp-secret-manager \
  --set secrets.gcpSecretManager.enabled=true \
  --set secrets.gcpSecretManager.projectId=your-project-id \
  --set serviceAccount.annotations."iam\.gke\.io/gcp-service-account"=honua-server@project-id.iam.gserviceaccount.com
```

### High Availability Setup

```yaml
# ha-values.yaml
replicaCount: 3

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 20

podDisruptionBudget:
  enabled: true
  minAvailable: 2

affinity:
  podAntiAffinity:
    requiredDuringSchedulingIgnoredDuringExecution:
      - labelSelector:
          matchExpressions:
            - key: app.kubernetes.io/name
              operator: In
              values:
                - honua-server
        topologyKey: kubernetes.io/hostname

topologySpreadConstraints:
  - maxSkew: 1
    topologyKey: topology.kubernetes.io/zone
    whenUnsatisfiable: DoNotSchedule
    labelSelector:
      matchLabels:
        app.kubernetes.io/name: honua-server
```

```bash
helm install honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --values ha-values.yaml
```

## Upgrade

### Standard Upgrade

```bash
# Upgrade to new version
helm upgrade honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --values values.yaml

# Upgrade with new image version
helm upgrade honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --set image.tag=1.1.0
```

### Zero-Downtime Upgrade

```bash
# Verify current state
kubectl get pods -n honua -l app.kubernetes.io/name=honua-server

# Perform upgrade
helm upgrade honua-server ./deploy/kubernetes/helm/honua-server \
  --namespace honua \
  --values values.yaml \
  --wait \
  --timeout 10m

# Verify rollout
kubectl rollout status deployment/honua-server -n honua

# Rollback if needed
helm rollback honua-server -n honua
```

## Uninstall

```bash
# Uninstall release
helm uninstall honua-server --namespace honua

# Clean up namespace (if desired)
kubectl delete namespace honua
```

## Troubleshooting

### Check Pod Status

```bash
kubectl get pods -n honua -l app.kubernetes.io/name=honua-server
kubectl describe pod <pod-name> -n honua
kubectl logs <pod-name> -n honua --tail=100
```

### Check Deployment Events

```bash
kubectl describe deployment honua-server -n honua
kubectl get events -n honua --sort-by='.lastTimestamp'
```

### Check Service and Endpoints

```bash
kubectl get svc honua-server -n honua
kubectl get endpoints honua-server -n honua
```

### Check Ingress

```bash
kubectl get ingress -n honua
kubectl describe ingress honua-server -n honua
```

### Check HPA Status

```bash
kubectl get hpa honua-server -n honua
kubectl describe hpa honua-server -n honua
```

### Common Issues

#### Pods Not Starting

1. Check resource availability:
   ```bash
   kubectl describe nodes
   kubectl top nodes
   ```

2. Check image pull:
   ```bash
   kubectl get pods -n honua
   kubectl describe pod <pod-name> -n honua | grep -A 10 Events
   ```

#### Database Connection Issues

1. Verify secret exists:
   ```bash
   kubectl get secret honua-server-secret -n honua
   ```

2. Check connection string (decode secret):
   ```bash
   kubectl get secret honua-server-secret -n honua -o jsonpath='{.data.database-connection-string}' | base64 -d
   ```

3. Test database connectivity:
   ```bash
   kubectl run -it --rm debug --image=postgres:15 --restart=Never -- \
     psql -h <db-host> -U <db-user> -d <db-name>
   ```

#### Performance Issues

1. Check resource usage:
   ```bash
   kubectl top pods -n honua -l app.kubernetes.io/name=honua-server
   ```

2. Check HPA metrics:
   ```bash
   kubectl get hpa honua-server -n honua -o yaml
   ```

3. Review application logs:
   ```bash
   kubectl logs -f deployment/honua-server -n honua --tail=200
   ```

## Examples

See the `deploy/kubernetes/examples/` directory for complete deployment examples:

- `basic-deployment.yaml` - Basic deployment with embedded PostgreSQL and Redis
- `external-database.yaml` - Deployment with external managed database
- `azure-deployment.yaml` - Azure-specific deployment with Key Vault
- `aws-deployment.yaml` - AWS-specific deployment with Secrets Manager
- `multi-region.yaml` - Multi-region deployment configuration

## Support

- Documentation: https://github.com/honua-io/Honua.Server
- Issues: https://github.com/honua-io/Honua.Server/issues
- Email: support@honua.io

## License

Elastic License 2.0. See LICENSE file for details.
