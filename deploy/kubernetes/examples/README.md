# Honua Server Kubernetes Deployment Examples

This directory contains example deployment configurations for various scenarios and cloud providers.

## Examples Overview

| File | Description | Use Case |
|------|-------------|----------|
| `basic-deployment.yaml` | Basic deployment with embedded databases | Development, testing |
| `external-database.yaml` | Deployment with external managed databases | Staging, production |
| `azure-deployment.yaml` | Azure-specific with Key Vault integration | Azure AKS |
| `aws-deployment.yaml` | AWS-specific with Secrets Manager | AWS EKS |
| `gcp-deployment.yaml` | GCP-specific with Secret Manager | GCP GKE |
| `multi-region.yaml` | Multi-region deployment | Global, DR |

## Usage

### Basic Deployment

Perfect for development and testing with embedded PostgreSQL and Redis:

```bash
helm install honua-server ../helm/honua-server \
  --namespace honua-dev \
  --create-namespace \
  -f basic-deployment.yaml
```

**Features:**
- Single replica
- Lite image (fast startup)
- Embedded PostgreSQL (Bitnami chart)
- Embedded Redis (Bitnami chart)
- No persistence (data lost on restart)
- Debug logging

**Access:**
```bash
kubectl port-forward svc/honua-server 8080:80 -n honua-dev
# Visit http://localhost:8080
```

### External Database Deployment

Production-ready with external managed databases:

```bash
# 1. Create secrets
kubectl create secret generic honua-db-secret \
  --from-literal=password='your-db-password' \
  --namespace honua

kubectl create secret generic honua-redis-secret \
  --from-literal=password='your-redis-password' \
  --namespace honua

# 2. Deploy
helm install honua-server ../helm/honua-server \
  --namespace honua \
  --create-namespace \
  -f external-database.yaml \
  --set database.host=postgres.example.com \
  --set redis.host=redis.example.com
```

**Features:**
- Multiple replicas (auto-scales)
- Full image variant
- External PostgreSQL
- External Redis
- Pod Disruption Budget
- Network policies
- ServiceMonitor for Prometheus
- TLS ingress

### Azure Deployment (AKS)

Deploy on Azure Kubernetes Service with Azure integrations:

```bash
# Prerequisites:
# 1. Create Azure Key Vault and add secrets
# 2. Set up Workload Identity:
#    az identity create --name honua-server --resource-group honua-rg
#    az identity federated-credential create ...
# 3. Grant Key Vault access to managed identity
# 4. Create Azure PostgreSQL Flexible Server
# 5. Create Azure Cache for Redis

# Deploy
helm install honua-server ../helm/honua-server \
  --namespace honua \
  --create-namespace \
  -f azure-deployment.yaml \
  --set secrets.azureKeyVault.name=honua-keyvault \
  --set secrets.azureKeyVault.tenantId=your-tenant-id \
  --set secrets.azureKeyVault.clientId=your-client-id \
  --set serviceAccount.annotations."azure\.workload\.identity/client-id"=your-client-id \
  --set database.host=honua-postgres.postgres.database.azure.com \
  --set redis.host=honua-redis.redis.cache.windows.net
```

**Azure-Specific Features:**
- Workload Identity for Key Vault access
- Azure PostgreSQL Flexible Server
- Azure Cache for Redis (TLS enabled)
- Application Gateway Ingress Controller
- Azure Key Vault for secrets
- Availability zone distribution

**Integration Points:**
- **Key Vault**: Stores database passwords, API keys
- **PostgreSQL**: Managed database with automated backups
- **Redis**: In-memory cache with high availability
- **Application Gateway**: Layer 7 load balancing
- **Azure Monitor**: Integrated monitoring

### AWS Deployment (EKS)

Deploy on Amazon EKS with AWS integrations:

```bash
# Prerequisites:
# 1. Create IAM role for IRSA:
#    eksctl create iamserviceaccount \
#      --name honua-server \
#      --namespace honua \
#      --cluster your-cluster \
#      --attach-policy-arn arn:aws:iam::aws:policy/SecretsManagerReadWrite \
#      --approve
# 2. Create AWS Secrets Manager secrets
# 3. Create RDS PostgreSQL instance
# 4. Create ElastiCache Redis cluster
# 5. Configure security groups

# Deploy
helm install honua-server ../helm/honua-server \
  --namespace honua \
  --create-namespace \
  -f aws-deployment.yaml \
  --set serviceAccount.annotations."eks\.amazonaws\.com/role-arn"=arn:aws:iam::123456789012:role/honua-server \
  --set database.host=honua-postgres.cluster-xyz.us-east-1.rds.amazonaws.com \
  --set redis.host=honua-redis.abc123.cache.amazonaws.com
```

**AWS-Specific Features:**
- IRSA (IAM Roles for Service Accounts)
- RDS PostgreSQL with Multi-AZ
- ElastiCache Redis with clustering
- ALB Ingress Controller
- AWS Secrets Manager
- Cross-AZ pod distribution

**Integration Points:**
- **Secrets Manager**: Rotatable database credentials
- **RDS**: Managed PostgreSQL with automated backups
- **ElastiCache**: Redis with automatic failover
- **ALB**: Application Load Balancer with WAF
- **CloudWatch**: Centralized logging and metrics

### GCP Deployment (GKE)

Deploy on Google Kubernetes Engine with GCP integrations:

```bash
# Prerequisites:
# 1. Create GCP service account and configure Workload Identity:
#    gcloud iam service-accounts create honua-server
#    gcloud iam service-accounts add-iam-policy-binding \
#      honua-server@project-id.iam.gserviceaccount.com \
#      --role roles/iam.workloadIdentityUser \
#      --member "serviceAccount:project-id.svc.id.goog[honua/honua-server]"
# 2. Create Secret Manager secrets
# 3. Create Cloud SQL PostgreSQL instance
# 4. Create Memorystore Redis instance

# Deploy
helm install honua-server ../helm/honua-server \
  --namespace honua \
  --create-namespace \
  -f gcp-deployment.yaml \
  --set secrets.gcpSecretManager.projectId=your-project-id \
  --set serviceAccount.annotations."iam\.gke\.io/gcp-service-account"=honua-server@project-id.iam.gserviceaccount.com \
  --set sidecars[0].args[2]=project-id:us-central1:honua-postgres
```

**GCP-Specific Features:**
- Workload Identity for Secret Manager access
- Cloud SQL with Cloud SQL Proxy sidecar
- Memorystore Redis
- GCE Ingress Controller with Google-managed certificates
- GCP Secret Manager
- Multi-zone distribution

**Integration Points:**
- **Secret Manager**: Versioned secret storage
- **Cloud SQL**: Managed PostgreSQL with high availability
- **Memorystore**: Redis with automatic failover
- **Cloud Load Balancing**: Global load balancing
- **Cloud Monitoring**: Integrated observability

### Multi-Region Deployment

Deploy across multiple regions for disaster recovery and low latency:

```bash
# Deploy in US East
helm install honua-us-east ../helm/honua-server \
  --namespace honua-us-east \
  --create-namespace \
  -f multi-region.yaml \
  --set database.host=postgres-global.example.com \
  --set redis.host=redis-us-east.cache.amazonaws.com \
  --set ingress.hosts[0].host=api-us-east.honua.io \
  --set podLabels.region=us-east

# Deploy in EU West
helm install honua-eu-west ../helm/honua-server \
  --namespace honua-eu-west \
  --create-namespace \
  -f multi-region.yaml \
  --set database.host=postgres-global.example.com \
  --set redis.host=redis-eu-west.cache.amazonaws.com \
  --set ingress.hosts[0].host=api-eu-west.honua.io \
  --set podLabels.region=eu-west

# Deploy in AP Southeast
helm install honua-ap-southeast ../helm/honua-server \
  --namespace honua-ap-southeast \
  --create-namespace \
  -f multi-region.yaml \
  --set database.host=postgres-global.example.com \
  --set redis.host=redis-ap-southeast.cache.amazonaws.com \
  --set ingress.hosts[0].host=api-ap-southeast.honua.io \
  --set podLabels.region=ap-southeast
```

**Multi-Region Features:**
- Shared global database (Aurora Global, Cosmos DB)
- Region-specific Redis caches
- Geographic DNS routing (Route 53, Traffic Manager)
- Cross-region disaster recovery
- Low-latency regional access

**Architecture:**
- **Database**: Single global database with read replicas in each region
- **Cache**: Separate Redis per region for low latency
- **Traffic**: Geo-routed DNS to nearest region
- **Failover**: Automatic failover between regions

## Customization

All examples can be customized with additional `--set` flags:

```bash
# Change image version
--set image.tag=1.1.0

# Adjust replicas
--set replicaCount=5

# Modify resources
--set resources.limits.cpu=4000m \
--set resources.limits.memory=4Gi

# Add custom environment variables
--set extraEnv[0].name=CUSTOM_VAR \
--set extraEnv[0].value=custom-value

# Enable features
--set serviceMonitor.enabled=true \
--set networkPolicy.enabled=true
```

## Testing Deployments

### Test Basic Connectivity

```bash
# Port forward
kubectl port-forward svc/honua-server 8080:80 -n honua

# Health check
curl http://localhost:8080/healthz/live
curl http://localhost:8080/healthz/ready

# Metrics
curl http://localhost:8080/metrics
```

### Test Ingress

```bash
# Get ingress address
kubectl get ingress -n honua

# Test HTTPS
curl -k https://honua.example.com/healthz/ready
```

### Load Testing

```bash
# Simple load test with Apache Bench
ab -n 10000 -c 100 http://localhost:8080/healthz/ready

# Or use k6
k6 run load-test.js
```

## Monitoring

### View Logs

```bash
# All pods
kubectl logs -f -l app.kubernetes.io/name=honua-server -n honua

# Specific pod
kubectl logs -f <pod-name> -n honua

# Previous instance
kubectl logs <pod-name> -n honua --previous
```

### Check Metrics

```bash
# HPA status
kubectl get hpa -n honua

# Resource usage
kubectl top pods -n honua

# Events
kubectl get events -n honua --sort-by='.lastTimestamp'
```

## Cleanup

### Uninstall Release

```bash
helm uninstall honua-server -n honua
```

### Delete Secrets

```bash
kubectl delete secret honua-db-secret honua-redis-secret -n honua
```

### Delete Namespace

```bash
kubectl delete namespace honua
```

## Support

For issues or questions about these examples:
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Email: support@honua.io
- Documentation: https://github.com/honua-io/Honua.Server
