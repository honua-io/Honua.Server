# Deployment Examples

Quick reference guide with copy-paste examples for common deployment scenarios.

## Local Development

### Start Complete Development Environment

```bash
cd /path/to/HonuaIO

# Start all services
docker-compose -f deployment/docker-compose.yml up -d

# Verify services are running
docker-compose -f deployment/docker-compose.yml ps

# View logs
docker-compose -f deployment/docker-compose.yml logs -f api

# Access services
# API: http://localhost:8080
# Intake: http://localhost:8082
```

### Start with Monitoring

```bash
# Start application
docker-compose -f deployment/docker-compose.yml up -d

# Start monitoring stack
docker-compose -f deployment/docker-compose.monitoring.yml up -d

# Access monitoring
# Prometheus: http://localhost:9090
# Grafana: http://localhost:3000 (admin/admin)
```

## Build and Push Images

### Build All Images

```bash
# Using Make
make build

# Or manually
docker build -f deployment/docker/Dockerfile.host -t honua/server-host:latest .
docker build -f deployment/docker/Dockerfile.intake -t honua/server-intake:latest .
docker build -f deployment/docker/Dockerfile.orchestrator -t honua/build-orchestrator:latest .
```

### Build and Push with Version Tag

```bash
# Set version
export IMAGE_TAG=v1.0.0

# Build and push all
make build-and-push IMAGE_TAG=$IMAGE_TAG

# Or using deploy script
./deployment/scripts/deploy.sh \
  --environment production \
  --tag $IMAGE_TAG \
  --skip-tests
```

### Push to Different Registry

```bash
# Tag for Google Container Registry
docker tag honua/server-host:v1.0.0 gcr.io/my-project/honua/server-host:v1.0.0
docker tag honua/server-intake:v1.0.0 gcr.io/my-project/honua/server-intake:v1.0.0
docker tag honua/build-orchestrator:v1.0.0 gcr.io/my-project/honua/build-orchestrator:v1.0.0

# Configure docker for GCR
gcloud auth configure-docker

# Push
docker push gcr.io/my-project/honua/server-host:v1.0.0
docker push gcr.io/my-project/honua/server-intake:v1.0.0
docker push gcr.io/my-project/honua/build-orchestrator:v1.0.0
```

## Kubernetes Deployments

### Development Environment (Minikube/Kind)

```bash
# Start minikube
minikube start --cpus 4 --memory 8192

# Deploy using Kustomize
kubectl apply -k deployment/k8s/overlays/development

# Or using Helm
helm install honua deployment/helm/honua \
  -n honua \
  --create-namespace \
  -f deployment/helm/honua/values-dev.yaml

# Port forward to access
kubectl port-forward -n honua-dev svc/dev-honua-api 8080:8080

# Access: http://localhost:8080
```

### Staging Environment

```bash
# Ensure kubectl is configured for staging cluster
kubectl config use-context staging-cluster

# Deploy using Helm
helm install honua deployment/helm/honua \
  -n honua-staging \
  --create-namespace \
  -f deployment/helm/honua/values-staging.yaml \
  --set image.tag=v1.0.0-rc1

# Monitor rollout
kubectl rollout status deployment/staging-honua-api -n honua-staging

# Check status
kubectl get all -n honua-staging
```

### Production Environment

```bash
# Ensure kubectl is configured for production cluster
kubectl config use-context production-cluster

# Deploy using Helm
helm install honua deployment/helm/honua \
  -n honua-prod \
  --create-namespace \
  -f deployment/helm/honua/values-prod.yaml \
  --set image.tag=v1.0.0

# Wait for rollout
kubectl rollout status deployment/prod-honua-api -n honua-prod
kubectl rollout status deployment/prod-honua-intake -n honua-prod
kubectl rollout status deployment/prod-honua-orchestrator -n honua-prod

# Verify
kubectl get pods -n honua-prod
kubectl get ingress -n honua-prod
```

## Cloud-Specific Deployments

### AWS EKS Complete Setup

```bash
# Variables
export CLUSTER_NAME="honua-eks-cluster"
export REGION="us-west-2"
export VERSION="1.28"

# Create EKS cluster
eksctl create cluster \
  --name $CLUSTER_NAME \
  --region $REGION \
  --version $VERSION \
  --nodegroup-name honua-nodes \
  --node-type t3.xlarge \
  --nodes 3 \
  --nodes-min 3 \
  --nodes-max 10 \
  --managed \
  --with-oidc

# Install AWS Load Balancer Controller
helm repo add eks https://aws.github.io/eks-charts
helm repo update

helm install aws-load-balancer-controller eks/aws-load-balancer-controller \
  -n kube-system \
  --set clusterName=$CLUSTER_NAME \
  --set serviceAccount.create=true

# Install EBS CSI Driver
kubectl apply -k "github.com/kubernetes-sigs/aws-ebs-csi-driver/deploy/kubernetes/overlays/stable/?ref=release-1.25"

# Deploy Honua
kubectl apply -f deployment/cloud/aws/eks-config.yaml

helm install honua deployment/helm/honua \
  -n honua \
  --create-namespace \
  -f deployment/helm/honua/values-prod.yaml \
  --set cloudProvider.type=aws \
  --set cloudProvider.aws.region=$REGION

# Get LoadBalancer URL
kubectl get ingress -n honua
```

### Azure AKS Complete Setup

```bash
# Variables
export RESOURCE_GROUP="honua-rg"
export CLUSTER_NAME="honua-aks-cluster"
export LOCATION="westus2"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create AKS cluster
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $CLUSTER_NAME \
  --node-count 3 \
  --node-vm-size Standard_D4s_v3 \
  --enable-cluster-autoscaler \
  --min-count 3 \
  --max-count 10 \
  --enable-managed-identity \
  --enable-workload-identity \
  --enable-oidc-issuer \
  --network-plugin azure \
  --enable-addons monitoring

# Get credentials
az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER_NAME

# Deploy Honua
kubectl apply -f deployment/cloud/azure/aks-config.yaml

helm install honua deployment/helm/honua \
  -n honua \
  --create-namespace \
  -f deployment/helm/honua/values-prod.yaml \
  --set cloudProvider.type=azure \
  --set cloudProvider.azure.region=$LOCATION

# Get service URL
kubectl get service -n honua
```

### GCP GKE Complete Setup

```bash
# Variables
export PROJECT_ID="my-project-id"
export CLUSTER_NAME="honua-gke-cluster"
export REGION="us-central1"
export ZONE="us-central1-a"

# Set project
gcloud config set project $PROJECT_ID

# Create GKE cluster
gcloud container clusters create $CLUSTER_NAME \
  --region $REGION \
  --node-locations $ZONE \
  --num-nodes 3 \
  --min-nodes 3 \
  --max-nodes 10 \
  --enable-autoscaling \
  --machine-type n2-standard-4 \
  --enable-ip-alias \
  --enable-stackdriver-kubernetes \
  --workload-pool=$PROJECT_ID.svc.id.goog

# Get credentials
gcloud container clusters get-credentials $CLUSTER_NAME --region $REGION

# Create static IP
gcloud compute addresses create honua-static-ip --global

# Update config with PROJECT_ID
sed -i "s/PROJECT_ID/$PROJECT_ID/g" deployment/cloud/gcp/gke-config.yaml

# Deploy Honua
kubectl apply -f deployment/cloud/gcp/gke-config.yaml

helm install honua deployment/helm/honua \
  -n honua \
  --create-namespace \
  -f deployment/helm/honua/values-prod.yaml \
  --set cloudProvider.type=gcp \
  --set cloudProvider.gcp.region=$REGION \
  --set image.registry=gcr.io/$PROJECT_ID

# Get ingress IP
kubectl get ingress -n honua
```

## Helm Operations

### Install

```bash
# Development
helm install honua deployment/helm/honua \
  -n honua \
  --create-namespace \
  -f deployment/helm/honua/values-dev.yaml

# With custom values
helm install honua deployment/helm/honua \
  -n honua \
  --set api.replicaCount=5 \
  --set image.tag=v1.2.0
```

### Upgrade

```bash
# Upgrade with new version
helm upgrade honua deployment/helm/honua \
  -n honua \
  -f deployment/helm/honua/values-prod.yaml \
  --set image.tag=v1.1.0

# Upgrade and wait
helm upgrade honua deployment/helm/honua \
  -n honua \
  -f deployment/helm/honua/values-prod.yaml \
  --wait \
  --timeout 10m
```

### Rollback

```bash
# Rollback to previous version
helm rollback honua -n honua

# Rollback to specific revision
helm rollback honua 3 -n honua

# View revision history
helm history honua -n honua
```

### Uninstall

```bash
# Uninstall release
helm uninstall honua -n honua

# Uninstall and delete namespace
helm uninstall honua -n honua
kubectl delete namespace honua
```

## Monitoring and Debugging

### View Logs

```bash
# API logs
kubectl logs -n honua -l app.kubernetes.io/component=api -f

# Specific pod
kubectl logs -n honua pod/honua-api-7d8f6c9b5-4xz2w -f

# Previous container logs
kubectl logs -n honua pod/honua-api-7d8f6c9b5-4xz2w --previous

# All containers in pod
kubectl logs -n honua pod/honua-api-7d8f6c9b5-4xz2w --all-containers
```

### Port Forwarding

```bash
# API service
kubectl port-forward -n honua svc/honua-api 8080:8080

# Prometheus
kubectl port-forward -n honua svc/prometheus 9090:9090

# Grafana
kubectl port-forward -n honua svc/grafana 3000:3000

# PostgreSQL
kubectl port-forward -n honua statefulset/postgres 5432:5432
```

### Execute Commands in Pods

```bash
# Shell in API pod
kubectl exec -it -n honua deployment/honua-api -- /bin/sh

# Run command
kubectl exec -n honua deployment/honua-api -- ls -la /app

# PostgreSQL shell
kubectl exec -it -n honua statefulset/postgres -- psql -U honua -d honua

# Redis CLI
kubectl exec -it -n honua statefulset/redis -- redis-cli -a PASSWORD
```

### Check Resource Usage

```bash
# Node resources
kubectl top nodes

# Pod resources
kubectl top pods -n honua

# Specific pod
kubectl top pod -n honua honua-api-7d8f6c9b5-4xz2w
```

### Describe Resources

```bash
# Describe pod
kubectl describe pod -n honua honua-api-7d8f6c9b5-4xz2w

# Describe deployment
kubectl describe deployment -n honua honua-api

# Describe service
kubectl describe service -n honua honua-api

# View events
kubectl get events -n honua --sort-by='.lastTimestamp'
```

## Scaling Operations

### Manual Scaling

```bash
# Scale deployment
kubectl scale deployment honua-api -n honua --replicas=5

# Scale multiple deployments
kubectl scale deployment honua-api honua-intake -n honua --replicas=3
```

### Auto-scaling

```bash
# Enable HPA
kubectl autoscale deployment honua-api -n honua \
  --cpu-percent=70 \
  --min=3 \
  --max=10

# View HPA status
kubectl get hpa -n honua

# Describe HPA
kubectl describe hpa honua-api-hpa -n honua
```

## Database Operations

### Backup Database

```bash
# Backup to file
kubectl exec -n honua statefulset/postgres -- \
  pg_dump -U honua honua > backup-$(date +%Y%m%d-%H%M%S).sql

# Backup with compression
kubectl exec -n honua statefulset/postgres -- \
  pg_dump -U honua honua | gzip > backup-$(date +%Y%m%d-%H%M%S).sql.gz
```

### Restore Database

```bash
# Restore from backup
kubectl exec -i -n honua statefulset/postgres -- \
  psql -U honua -d honua < backup.sql

# Restore from compressed backup
gunzip -c backup.sql.gz | \
  kubectl exec -i -n honua statefulset/postgres -- \
  psql -U honua -d honua
```

### Run Database Migrations

```bash
# Using kubectl exec
kubectl exec -n honua deployment/honua-api -- \
  dotnet ef database update

# Or use a job
kubectl apply -f - <<EOF
apiVersion: batch/v1
kind: Job
metadata:
  name: db-migration
  namespace: honua
spec:
  template:
    spec:
      containers:
      - name: migrate
        image: honua/server-host:v1.0.0
        command: ["dotnet", "ef", "database", "update"]
        envFrom:
        - configMapRef:
            name: honua-config
        - secretRef:
            name: honua-secrets
      restartPolicy: OnFailure
EOF
```

## Troubleshooting Examples

### Pods Not Starting

```bash
# Check pod status
kubectl get pods -n honua

# Describe pod to see events
kubectl describe pod -n honua honua-api-7d8f6c9b5-4xz2w

# Check logs
kubectl logs -n honua honua-api-7d8f6c9b5-4xz2w

# Check if image exists
docker pull honua/server-host:latest
```

### Network Issues

```bash
# Test DNS
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup honua-api.honua.svc.cluster.local

# Test connectivity
kubectl run -it --rm debug --image=nicolaka/netshoot --restart=Never -- bash
# Inside the pod:
curl http://honua-api.honua.svc.cluster.local:8080/health

# Check network policies
kubectl get networkpolicies -n honua
```

### Storage Issues

```bash
# Check PVCs
kubectl get pvc -n honua

# Describe PVC
kubectl describe pvc postgres-data -n honua

# Check PVs
kubectl get pv

# Check storage class
kubectl get storageclass
```

### Performance Issues

```bash
# Check resource usage
kubectl top nodes
kubectl top pods -n honua

# Check HPA status
kubectl get hpa -n honua

# View pod events
kubectl get events -n honua --field-selector involvedObject.name=honua-api-7d8f6c9b5-4xz2w

# Check throttling
kubectl describe pod -n honua honua-api-7d8f6c9b5-4xz2w | grep -i throttl
```

## CI/CD Examples

### GitHub Actions

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

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v2
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: us-west-2

      - name: Login to ECR
        uses: aws-actions/amazon-ecr-login@v1

      - name: Build and push
        run: |
          make build IMAGE_TAG=${{ github.ref_name }}
          make push IMAGE_TAG=${{ github.ref_name }}

      - name: Deploy
        run: |
          aws eks update-kubeconfig --name honua-eks-cluster --region us-west-2
          ./deployment/scripts/deploy.sh \
            --environment production \
            --cloud aws \
            --tag ${{ github.ref_name }} \
            --skip-tests
```

### GitLab CI

```yaml
stages:
  - build
  - deploy

build:
  stage: build
  script:
    - make build IMAGE_TAG=$CI_COMMIT_TAG
    - make push IMAGE_TAG=$CI_COMMIT_TAG
  only:
    - tags

deploy:production:
  stage: deploy
  script:
    - kubectl config use-context production
    - ./deployment/scripts/deploy.sh -e production -t $CI_COMMIT_TAG
  only:
    - tags
  when: manual
```

## Cleanup

### Remove Deployment

```bash
# Using Helm
helm uninstall honua -n honua

# Using Kustomize
kubectl delete -k deployment/k8s/overlays/production

# Delete namespace (includes all resources)
kubectl delete namespace honua
```

### Clean Docker

```bash
# Remove unused images
docker system prune -a

# Remove volumes
docker volume prune

# Remove everything
docker system prune -a --volumes
```

### Clean Local Development

```bash
# Stop and remove containers
docker-compose -f deployment/docker-compose.yml down -v
docker-compose -f deployment/docker-compose.monitoring.yml down -v

# Or using Make
make dev-down
make clean-all
```
