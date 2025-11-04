# Honua Kubernetes Deployment Documentation

Comprehensive deployment automation for the Honua Build Orchestration Platform supporting multi-cloud Kubernetes deployments (AWS EKS, Azure AKS, GCP GKE).

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Directory Structure](#directory-structure)
- [Prerequisites](#prerequisites)
- [Local Development](#local-development)
- [Kubernetes Deployment](#kubernetes-deployment)
- [Cloud Provider Setup](#cloud-provider-setup)
- [Monitoring and Observability](#monitoring-and-observability)
- [Security](#security)
- [Troubleshooting](#troubleshooting)

## Overview

This deployment system provides:

- **Multi-stage Docker builds** with AOT optimization
- **Kubernetes manifests** for all services and infrastructure
- **Helm charts** with environment-specific values
- **Kustomize overlays** for dev/staging/production
- **Cloud-specific configurations** for AWS/Azure/GCP
- **Docker Compose** for local development
- **Automated deployment scripts** and Makefile

## Quick Start

### Local Development

```bash
# Start all services locally
make dev-up

# Start with monitoring
make monitoring-up

# View logs
make dev-logs

# Stop everything
make dev-down
```

Access services:
- API: http://localhost:8080
- Intake: http://localhost:8082
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000

### Deploy to Kubernetes (Development)

```bash
# Using Helm
make helm-install ENVIRONMENT=development

# Or using Kustomize
make k8s-deploy-dev

# Or using the deployment script
./deployment/scripts/deploy.sh -e development --skip-build
```

### Deploy to Production

```bash
# Build and push images
make build-and-push IMAGE_TAG=v1.0.0

# Deploy to AWS EKS
make deploy-aws IMAGE_TAG=v1.0.0

# Or deploy to Azure AKS
make deploy-azure IMAGE_TAG=v1.0.0

# Or deploy to GCP GKE
make deploy-gcp IMAGE_TAG=v1.0.0
```

## Directory Structure

```
deployment/
├── docker/                          # Dockerfiles
│   ├── Dockerfile.host             # API server
│   ├── Dockerfile.intake           # Intake service
│   ├── Dockerfile.orchestrator     # Build orchestrator
│   └── .dockerignore
├── k8s/                            # Kubernetes manifests
│   ├── base/                       # Base manifests
│   │   ├── namespace.yaml
│   │   ├── configmap.yaml
│   │   ├── secrets.yaml
│   │   ├── pvc.yaml
│   │   ├── services.yaml
│   │   ├── deployment-*.yaml
│   │   ├── statefulset-*.yaml
│   │   ├── hpa.yaml
│   │   ├── networkpolicy.yaml
│   │   ├── ingress.yaml
│   │   ├── servicemonitor.yaml
│   │   └── kustomization.yaml
│   └── overlays/                   # Environment overlays
│       ├── development/
│       ├── staging/
│       └── production/
├── helm/                           # Helm chart
│   └── honua/
│       ├── Chart.yaml
│       ├── values.yaml
│       ├── values-dev.yaml
│       ├── values-staging.yaml
│       ├── values-prod.yaml
│       └── templates/
├── cloud/                          # Cloud-specific configs
│   ├── aws/
│   │   ├── eks-config.yaml
│   │   └── README.md
│   ├── azure/
│   │   ├── aks-config.yaml
│   │   └── README.md
│   └── gcp/
│       ├── gke-config.yaml
│       └── README.md
├── scripts/
│   └── deploy.sh                   # Automated deployment script
├── docker-compose.yml              # Local development
├── docker-compose.monitoring.yml   # Monitoring stack
├── Makefile                        # Common operations
└── README.md                       # This file
```

## Prerequisites

### Required Tools

- Docker 20.10+
- kubectl 1.28+
- Helm 3.12+
- GNU Make (optional, for Makefile targets)

### Cloud Provider CLIs (for cloud deployments)

- **AWS**: AWS CLI + eksctl
- **Azure**: Azure CLI
- **GCP**: gcloud CLI

### Kubernetes Cluster

- Local: Docker Desktop, Minikube, or Kind
- Cloud: EKS, AKS, or GKE cluster with appropriate permissions

## Local Development

### Using Docker Compose

```bash
# Start all services
docker-compose -f deployment/docker-compose.yml up -d

# View logs
docker-compose -f deployment/docker-compose.yml logs -f api

# Stop services
docker-compose -f deployment/docker-compose.yml down

# Start monitoring stack
docker-compose -f deployment/docker-compose.monitoring.yml up -d
```

### Using Make

```bash
# Start development environment
make dev-up

# View logs
make dev-logs

# Restart services
make dev-restart

# Stop environment
make dev-down
```

### Testing Database Connection

```bash
make test-connection
```

## Kubernetes Deployment

### Option 1: Helm (Recommended)

```bash
# Development
helm install honua ./deployment/helm/honua \
  -n honua \
  --create-namespace \
  -f ./deployment/helm/honua/values-dev.yaml

# Staging
helm install honua ./deployment/helm/honua \
  -n honua-staging \
  --create-namespace \
  -f ./deployment/helm/honua/values-staging.yaml

# Production
helm install honua ./deployment/helm/honua \
  -n honua-prod \
  --create-namespace \
  -f ./deployment/helm/honua/values-prod.yaml

# Upgrade existing release
helm upgrade honua ./deployment/helm/honua \
  -n honua-prod \
  -f ./deployment/helm/honua/values-prod.yaml

# Rollback
helm rollback honua -n honua-prod
```

### Option 2: Kustomize

```bash
# Development
kubectl apply -k deployment/k8s/overlays/development

# Staging
kubectl apply -k deployment/k8s/overlays/staging

# Production
kubectl apply -k deployment/k8s/overlays/production

# Delete
kubectl delete -k deployment/k8s/overlays/production
```

### Option 3: Deployment Script

```bash
# Development deployment
./deployment/scripts/deploy.sh \
  --environment development \
  --method helm \
  --skip-tests

# Production deployment
./deployment/scripts/deploy.sh \
  --environment production \
  --cloud aws \
  --tag v1.0.0 \
  --method helm

# Dry run
./deployment/scripts/deploy.sh \
  --environment production \
  --dry-run

# Rollback
./deployment/scripts/deploy.sh --rollback
```

## Cloud Provider Setup

### AWS EKS

See [deployment/cloud/aws/README.md](cloud/aws/README.md) for detailed instructions.

Quick setup:
```bash
# Create cluster
eksctl create cluster -f deployment/cloud/aws/cluster-config.yaml

# Deploy
make deploy-aws IMAGE_TAG=v1.0.0
```

### Azure AKS

See [deployment/cloud/azure/README.md](cloud/azure/README.md) for detailed instructions.

Quick setup:
```bash
# Create cluster
az aks create --resource-group honua-rg --name honua-aks-cluster ...

# Deploy
make deploy-azure IMAGE_TAG=v1.0.0
```

### GCP GKE

See [deployment/cloud/gcp/README.md](cloud/gcp/README.md) for detailed instructions.

Quick setup:
```bash
# Create cluster
gcloud container clusters create honua-gke-cluster ...

# Deploy
make deploy-gcp IMAGE_TAG=v1.0.0
```

## Configuration

### Secrets Management

**IMPORTANT**: Never commit secrets to version control!

Update secrets before deploying to production:

```bash
# Edit the secrets template
kubectl edit secret honua-secrets -n honua-prod

# Or use external-secrets-operator, sealed-secrets, or Vault
```

Required secrets:
- Database password
- Redis password
- JWT secret key
- API keys for external services
- GitOps credentials (if using)

### ConfigMaps

Update configuration in:
- `deployment/k8s/base/configmap.yaml` (base configuration)
- Helm values files (environment-specific)

## Monitoring and Observability

### Prometheus and Grafana

Using Docker Compose (local):
```bash
make monitoring-up
```

In Kubernetes:
```bash
# Enable in Helm values
helm install honua ./deployment/helm/honua \
  --set prometheus.enabled=true \
  --set grafana.enabled=true
```

Access:
```bash
# Port forward
kubectl port-forward -n honua svc/prometheus 9090:9090
kubectl port-forward -n honua svc/grafana 3000:3000
```

### Logs

View logs:
```bash
# Using kubectl
kubectl logs -n honua -l app.kubernetes.io/component=api -f

# Using Make
make k8s-logs-api
make k8s-logs-intake
make k8s-logs-orchestrator
```

## Security

### Best Practices

1. **Use non-root users** in containers (already configured)
2. **Enable read-only root filesystem** (already configured)
3. **Apply network policies** (included in manifests)
4. **Use Pod Security Standards** (PSS)
5. **Enable RBAC** with minimal permissions
6. **Use secrets management** (Vault, AWS Secrets Manager, etc.)
7. **Enable TLS** for all external traffic
8. **Regular security scanning** of container images

### Network Policies

Network policies are included in base manifests:
- Default deny all ingress
- Allow specific service-to-service communication
- Allow external access only to API/Intake services

### TLS/SSL

Configure TLS in ingress:
```yaml
# Using cert-manager
annotations:
  cert-manager.io/cluster-issuer: "letsencrypt-prod"
```

## Scaling

### Horizontal Pod Autoscaling

HPA is configured by default for all services:
- API: 3-10 replicas (70% CPU, 80% memory)
- Intake: 2-8 replicas
- Orchestrator: 2-6 replicas

Adjust in Helm values or HPA manifests.

### Vertical Pod Autoscaling

Install VPA:
```bash
kubectl apply -f https://github.com/kubernetes/autoscaler/releases/latest/download/vpa.yaml
```

### Cluster Autoscaling

Configured per cloud provider (see cloud-specific READMEs).

## Backup and Disaster Recovery

### Database Backups

```bash
# Manual backup
make db-backup

# Automated backups (configure per cloud provider)
# AWS: Use RDS automated backups
# Azure: Use Azure Backup
# GCP: Use Cloud SQL automated backups
```

### Kubernetes Resources

Using Velero:
```bash
# Install Velero
helm install velero vmware-tanzu/velero ...

# Create backup
velero backup create honua-backup --include-namespaces honua

# Restore
velero restore create --from-backup honua-backup
```

## Troubleshooting

### Common Issues

#### Pods not starting
```bash
# Check pod status
kubectl get pods -n honua

# Describe pod
kubectl describe pod <pod-name> -n honua

# Check logs
kubectl logs <pod-name> -n honua
```

#### Database connection issues
```bash
# Check PostgreSQL pod
kubectl get pod -n honua -l app.kubernetes.io/name=postgres

# Test connection
kubectl exec -it <postgres-pod> -n honua -- psql -U honua -d honua
```

#### Image pull errors
```bash
# Check image pull secrets
kubectl get secrets -n honua

# Verify image exists
docker pull honua/server-host:latest
```

#### Ingress not working
```bash
# Check ingress
kubectl get ingress -n honua
kubectl describe ingress honua-ingress -n honua

# Check ingress controller logs
kubectl logs -n ingress-nginx deployment/ingress-nginx-controller
```

### Useful Commands

```bash
# Check cluster resources
kubectl top nodes
kubectl top pods -n honua

# Port forward to service
kubectl port-forward -n honua svc/honua-api 8080:8080

# Execute shell in pod
kubectl exec -it -n honua deployment/honua-api -- /bin/sh

# View events
kubectl get events -n honua --sort-by='.lastTimestamp'

# Restart deployment
kubectl rollout restart deployment/honua-api -n honua

# Check rollout status
kubectl rollout status deployment/honua-api -n honua
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Deploy to Production

on:
  push:
    tags:
      - 'v*'

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Deploy to Production
        run: |
          ./deployment/scripts/deploy.sh \
            --environment production \
            --cloud aws \
            --tag ${{ github.ref_name }} \
            --skip-tests
```

### GitLab CI Example

```yaml
deploy:production:
  stage: deploy
  script:
    - ./deployment/scripts/deploy.sh -e production -c aws -t $CI_COMMIT_TAG
  only:
    - tags
```

## Support

For issues or questions:
- GitHub Issues: https://github.com/HonuaIO/honua/issues
- Documentation: https://docs.honua.io
- Email: team@honua.io

## License

Apache 2.0
