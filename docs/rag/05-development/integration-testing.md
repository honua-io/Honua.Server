# Integration Testing Guide

**Keywords**: integration-testing, localstack, minikube, kind, kubernetes, ci-cd, test-automation, aws-mocking, docker-testing, e2e-testing, test-orchestration, continuous-integration

**Related**: docker-deployment, kubernetes-deployment, aws-ecs-deployment, troubleshooting, performance-tuning

## Overview

Comprehensive guide for integration testing Honua across local development, AWS (mocked with LocalStack), and Kubernetes environments (using Minikube or kind). This documentation covers cost-free testing strategies, end-to-end test scenarios, and CI/CD integration patterns.

Integration testing validates Honua's behavior across multiple components, services, and infrastructure layers without requiring expensive cloud resources. By leveraging LocalStack for AWS services and kind/Minikube for Kubernetes, developers can test complex deployment scenarios locally.

## Quick Start

```bash
# Run all integration tests
cd /home/mike/projects/HonuaIO/tests/e2e
./run-all-tests.sh

# Run specific test suites
./docker-scenarios.sh        # Docker integration tests
./localstack-scenarios.sh    # AWS LocalStack tests
./k8s-scenarios.sh          # Kubernetes tests (requires kind)

# Run .NET integration tests
dotnet test --filter "Category=Integration"
```

## 1. LocalStack Setup

LocalStack provides a fully functional local AWS cloud stack for testing AWS integrations without incurring cloud costs.

### 1.1 Installation

#### Docker (Recommended)

```bash
# Pull LocalStack image
docker pull localstack/localstack:latest

# Run LocalStack with commonly used services
docker run -d \
  --name localstack \
  -p 4566:4566 \
  -p 4571:4571 \
  -e SERVICES=s3,dynamodb,secretsmanager,cloudwatch,rds,lambda \
  -e DEBUG=1 \
  -e DATA_DIR=/tmp/localstack/data \
  -v /tmp/localstack:/tmp/localstack \
  -v /var/run/docker.sock:/var/run/docker.sock \
  localstack/localstack:latest

# Verify LocalStack is running
curl http://localhost:4566/_localstack/health
```

#### Docker Compose

Create `docker-compose.localstack.yml`:

```yaml
version: '3.9'

services:
  localstack:
    image: localstack/localstack:latest
    container_name: honua-localstack
    ports:
      - "4566:4566"      # LocalStack edge port
      - "4571:4571"      # LocalStack edge port (deprecated, optional)
    environment:
      - SERVICES=s3,dynamodb,secretsmanager,cloudwatch,rds,lambda,ecs,ecr
      - DEBUG=1
      - DATA_DIR=/tmp/localstack/data
      - DOCKER_HOST=unix:///var/run/docker.sock
      - AWS_DEFAULT_REGION=us-east-1
      - AWS_ACCESS_KEY_ID=test
      - AWS_SECRET_ACCESS_KEY=test
    volumes:
      - "/tmp/localstack:/tmp/localstack"
      - "/var/run/docker.sock:/var/run/docker.sock"
    networks:
      - honua-test-network

  postgres:
    image: postgis/postgis:16-3.4
    container_name: honua-postgres-test
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: honua_test_password
      POSTGRES_DB: honua_test
    ports:
      - "5433:5432"
    volumes:
      - postgres-test-data:/var/lib/postgresql/data
    networks:
      - honua-test-network

networks:
  honua-test-network:
    driver: bridge

volumes:
  postgres-test-data:
```

Start LocalStack stack:

```bash
docker-compose -f docker-compose.localstack.yml up -d

# Wait for LocalStack to be ready
for i in {1..30}; do
  if curl -s http://localhost:4566/_localstack/health | grep -q "\"s3\": \"available\""; then
    echo "LocalStack is ready"
    break
  fi
  echo "Waiting for LocalStack... ($i/30)"
  sleep 2
done
```

### 1.2 AWS CLI Configuration for LocalStack

Install AWS CLI and configure for LocalStack:

```bash
# Install AWS CLI
pip install awscli awscli-local

# Configure AWS CLI for LocalStack (add to ~/.aws/config)
[profile localstack]
region = us-east-1
output = json
endpoint_url = http://localhost:4566

# Set credentials (add to ~/.aws/credentials)
[localstack]
aws_access_key_id = test
aws_secret_access_key = test

# Export environment variables for testing
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1
export AWS_ENDPOINT_URL=http://localhost:4566

# Test LocalStack connectivity
aws --endpoint-url=http://localhost:4566 s3 ls
```

### 1.3 S3 Tile Cache Testing with LocalStack

Configure Honua to use LocalStack S3 for tile caching:

#### Create S3 Bucket for Tiles

```bash
# Create tile cache bucket
aws --endpoint-url=http://localhost:4566 s3 mb s3://honua-tile-cache

# Enable versioning (optional)
aws --endpoint-url=http://localhost:4566 s3api put-bucket-versioning \
  --bucket honua-tile-cache \
  --versioning-configuration Status=Enabled

# Configure CORS for browser access
cat > /tmp/cors-config.json <<EOF
{
  "CORSRules": [
    {
      "AllowedHeaders": ["*"],
      "AllowedMethods": ["GET", "HEAD"],
      "AllowedOrigins": ["*"],
      "ExposeHeaders": ["ETag"],
      "MaxAgeSeconds": 3600
    }
  ]
}
EOF

aws --endpoint-url=http://localhost:4566 s3api put-bucket-cors \
  --bucket honua-tile-cache \
  --cors-configuration file:///tmp/cors-config.json

# Set public read policy for tiles
cat > /tmp/bucket-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": "*",
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::honua-tile-cache/*"
    }
  ]
}
EOF

aws --endpoint-url=http://localhost:4566 s3api put-bucket-policy \
  --bucket honua-tile-cache \
  --policy file:///tmp/bucket-policy.json
```

#### Configure Honua for LocalStack S3

Update `appsettings.Development.json`:

```json
{
  "Honua": {
    "TileCache": {
      "Provider": "s3",
      "S3": {
        "BucketName": "honua-tile-cache",
        "Region": "us-east-1",
        "ServiceURL": "http://localhost:4566",
        "ForcePathStyle": true,
        "UseLocalStack": true
      }
    }
  },
  "AWS": {
    "Region": "us-east-1",
    "Profile": "localstack"
  }
}
```

Set environment variables:

```bash
export HONUA__TILECACHE__PROVIDER=s3
export HONUA__TILECACHE__S3__BUCKETNAME=honua-tile-cache
export HONUA__TILECACHE__S3__REGION=us-east-1
export HONUA__TILECACHE__S3__SERVICEURL=http://localhost:4566
export HONUA__TILECACHE__S3__FORCEPATHSTYLE=true
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
```

#### Test Tile Cache

```bash
# Start Honua with LocalStack S3
dotnet run --project src/Honua.Server.Host --urls http://localhost:5000

# Request a tile (this will cache it in S3)
curl -I http://localhost:5000/ogc/tiles/mvt/roads/14/8192/5461

# Verify tile was cached in LocalStack S3
aws --endpoint-url=http://localhost:4566 s3 ls s3://honua-tile-cache/mvt/ --recursive

# Check tile content
aws --endpoint-url=http://localhost:4566 s3 cp \
  s3://honua-tile-cache/mvt/roads/14/8192/5461.mvt \
  /tmp/test-tile.mvt

# Verify it's a valid Mapbox Vector Tile
file /tmp/test-tile.mvt  # Should show: gzip compressed data
```

### 1.4 RDS/PostgreSQL Mocking

LocalStack supports RDS, but for PostgreSQL integration testing, use a real PostgreSQL container (faster and more reliable):

```bash
# Use Testcontainers in .NET tests
# See tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj

# Or use Docker directly
docker run -d \
  --name honua-postgres-test \
  -e POSTGRES_USER=honua \
  -e POSTGRES_PASSWORD=test_password \
  -e POSTGRES_DB=honua_test \
  -p 5433:5432 \
  postgis/postgis:16-3.4

# Wait for PostgreSQL to be ready
for i in {1..30}; do
  if docker exec honua-postgres-test pg_isready -U honua > /dev/null 2>&1; then
    echo "PostgreSQL is ready"
    break
  fi
  echo "Waiting for PostgreSQL... ($i/30)"
  sleep 2
done

# Test connection
docker exec honua-postgres-test psql -U honua -d honua_test -c "SELECT PostGIS_version();"
```

Configuration for Honua:

```bash
export HONUA__DATABASE__TYPE=postgres
export HONUA__DATABASE__CONNECTIONSTRING="Host=localhost;Port=5433;Database=honua_test;Username=honua;Password=test_password"
```

### 1.5 Secrets Manager for Configuration

Store sensitive configuration in LocalStack Secrets Manager:

```bash
# Create database credentials secret
aws --endpoint-url=http://localhost:4566 secretsmanager create-secret \
  --name honua/database/credentials \
  --secret-string '{
    "username": "honua",
    "password": "secure_password_123",
    "host": "localhost",
    "port": 5432,
    "database": "honua"
  }'

# Create OAuth client credentials
aws --endpoint-url=http://localhost:4566 secretsmanager create-secret \
  --name honua/oauth/credentials \
  --secret-string '{
    "clientId": "honua-test-client",
    "clientSecret": "test-secret-key",
    "authority": "https://test.auth.com"
  }'

# Create S3 access credentials
aws --endpoint-url=http://localhost:4566 secretsmanager create-secret \
  --name honua/s3/credentials \
  --secret-string '{
    "accessKeyId": "test-access-key",
    "secretAccessKey": "test-secret-key",
    "region": "us-east-1"
  }'

# Retrieve secret
aws --endpoint-url=http://localhost:4566 secretsmanager get-secret-value \
  --secret-id honua/database/credentials \
  --query 'SecretString' --output text | jq .

# Update secret
aws --endpoint-url=http://localhost:4566 secretsmanager update-secret \
  --secret-id honua/database/credentials \
  --secret-string '{"username":"honua","password":"new_password"}'

# List all secrets
aws --endpoint-url=http://localhost:4566 secretsmanager list-secrets
```

Configure Honua to use Secrets Manager:

```bash
export HONUA__SECRETSMANAGER__ENABLED=true
export HONUA__SECRETSMANAGER__PROVIDER=aws
export HONUA__SECRETSMANAGER__AWS__REGION=us-east-1
export HONUA__SECRETSMANAGER__AWS__ENDPOINT=http://localhost:4566
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
```

### 1.6 CloudWatch Logs and Metrics

LocalStack supports CloudWatch for testing logging and monitoring:

```bash
# Create log group
aws --endpoint-url=http://localhost:4566 logs create-log-group \
  --log-group-name /honua/application

# Create log stream
aws --endpoint-url=http://localhost:4566 logs create-log-stream \
  --log-group-name /honua/application \
  --log-stream-name honua-server

# Put log events
aws --endpoint-url=http://localhost:4566 logs put-log-events \
  --log-group-name /honua/application \
  --log-stream-name honua-server \
  --log-events timestamp=$(date +%s000),message="Server started"

# Query logs
aws --endpoint-url=http://localhost:4566 logs get-log-events \
  --log-group-name /honua/application \
  --log-stream-name honua-server

# Create CloudWatch metric
aws --endpoint-url=http://localhost:4566 cloudwatch put-metric-data \
  --namespace Honua \
  --metric-name TileRequests \
  --value 100 \
  --unit Count

# Get metric statistics
aws --endpoint-url=http://localhost:4566 cloudwatch get-metric-statistics \
  --namespace Honua \
  --metric-name TileRequests \
  --start-time $(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 3600 \
  --statistics Sum
```

## 2. Minikube/kind Setup

Local Kubernetes clusters enable testing Kubernetes manifests, Helm charts, and cluster configurations without cloud costs.

### 2.1 kind (Kubernetes in Docker) Setup

kind is lightweight and fast for CI/CD pipelines.

#### Installation

```bash
# Install kind (Linux/macOS)
curl -Lo ./kind https://kind.sigs.k8s.io/dl/v0.20.0/kind-linux-amd64
chmod +x ./kind
sudo mv ./kind /usr/local/bin/kind

# Or using Go
GO111MODULE=on go install sigs.k8s.io/kind@latest

# Verify installation
kind version
```

#### Create Cluster

```bash
# Create simple cluster
kind create cluster --name honua-test

# Create cluster with custom configuration
cat > kind-config.yaml <<EOF
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
name: honua-test
nodes:
  - role: control-plane
    kubeadmConfigPatches:
      - |
        kind: InitConfiguration
        nodeRegistration:
          kubeletExtraArgs:
            node-labels: "ingress-ready=true"
    extraPortMappings:
      - containerPort: 80
        hostPort: 8080
        protocol: TCP
      - containerPort: 443
        hostPort: 8443
        protocol: TCP
  - role: worker
  - role: worker
EOF

kind create cluster --config kind-config.yaml

# Verify cluster
kubectl cluster-info --context kind-honua-test
kubectl get nodes

# Delete cluster when done
kind delete cluster --name honua-test
```

#### Load Docker Images into kind

```bash
# Build Honua Docker image
docker build -t honua-server:test .

# Load image into kind cluster
kind load docker-image honua-server:test --name honua-test

# Verify image is available
docker exec -it honua-test-control-plane crictl images | grep honua-server
```

### 2.2 Minikube Setup

Minikube provides more features and better simulates production Kubernetes.

#### Installation

```bash
# Install Minikube (Linux)
curl -LO https://storage.googleapis.com/minikube/releases/latest/minikube-linux-amd64
sudo install minikube-linux-amd64 /usr/local/bin/minikube

# Install Minikube (macOS)
brew install minikube

# Verify installation
minikube version
```

#### Start Cluster

```bash
# Start with Docker driver (default)
minikube start --cpus=4 --memory=8192 --disk-size=20g

# Start with specific Kubernetes version
minikube start --kubernetes-version=v1.28.0

# Enable addons
minikube addons enable ingress
minikube addons enable metrics-server
minikube addons enable dashboard
minikube addons enable registry

# Verify cluster
kubectl get nodes
minikube status

# Access Kubernetes dashboard
minikube dashboard

# Stop cluster
minikube stop

# Delete cluster
minikube delete
```

#### Use Minikube Docker Daemon

```bash
# Configure shell to use Minikube's Docker daemon
eval $(minikube docker-env)

# Build images directly in Minikube
docker build -t honua-server:test .

# Verify image
docker images | grep honua-server

# Reset to host Docker daemon
eval $(minikube docker-env -u)
```

### 2.3 Honua Deployment on Kubernetes

#### Create Namespace

```bash
kubectl create namespace honua-test
kubectl config set-context --current --namespace=honua-test
```

#### Deploy PostgreSQL

Create `postgres-deployment.yaml`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: postgres-config
  namespace: honua-test
data:
  POSTGRES_DB: honua
  POSTGRES_USER: honua
---
apiVersion: v1
kind: Secret
metadata:
  name: postgres-secret
  namespace: honua-test
type: Opaque
stringData:
  POSTGRES_PASSWORD: honua_test_password
---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: honua-test
spec:
  serviceName: postgres
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
        - name: postgres
          image: postgis/postgis:16-3.4
          ports:
            - containerPort: 5432
              name: postgres
          envFrom:
            - configMapRef:
                name: postgres-config
            - secretRef:
                name: postgres-secret
          volumeMounts:
            - name: postgres-storage
              mountPath: /var/lib/postgresql/data
          resources:
            requests:
              memory: "256Mi"
              cpu: "250m"
            limits:
              memory: "1Gi"
              cpu: "1000m"
  volumeClaimTemplates:
    - metadata:
        name: postgres-storage
      spec:
        accessModes: ["ReadWriteOnce"]
        resources:
          requests:
            storage: 10Gi
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: honua-test
spec:
  selector:
    app: postgres
  ports:
    - port: 5432
      targetPort: 5432
  clusterIP: None  # Headless service for StatefulSet
```

Deploy:

```bash
kubectl apply -f postgres-deployment.yaml

# Wait for PostgreSQL to be ready
kubectl wait --for=condition=ready pod -l app=postgres --timeout=300s

# Verify
kubectl get statefulsets
kubectl get pods -l app=postgres
kubectl logs -l app=postgres --tail=50
```

#### Deploy Honua Server

Create `honua-deployment.yaml`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-config
  namespace: honua-test
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  ASPNETCORE_URLS: "http://+:8080"
  HONUA__DATABASE__TYPE: "postgres"
  HONUA__AUTHENTICATION__MODE: "QuickStart"
  HONUA__AUTHENTICATION__ENFORCE: "false"
  HONUA__METADATA__PROVIDER: "json"
  HONUA__METADATA__PATH: "/app/metadata/metadata.json"
---
apiVersion: v1
kind: Secret
metadata:
  name: honua-secret
  namespace: honua-test
type: Opaque
stringData:
  DATABASE_CONNECTION: "Host=postgres;Port=5432;Database=honua;Username=honua;Password=honua_test_password"
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-metadata
  namespace: honua-test
data:
  metadata.json: |
    {
      "serviceMetadata": {
        "name": "Honua Test Server",
        "description": "Integration testing deployment"
      },
      "layers": []
    }
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua-test
spec:
  replicas: 2
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
        version: test
    spec:
      containers:
        - name: honua-server
          image: honua-server:test
          imagePullPolicy: IfNotPresent
          ports:
            - containerPort: 8080
              name: http
          envFrom:
            - configMapRef:
                name: honua-config
          env:
            - name: ConnectionStrings__DefaultConnection
              valueFrom:
                secretKeyRef:
                  name: honua-secret
                  key: DATABASE_CONNECTION
          volumeMounts:
            - name: metadata
              mountPath: /app/metadata
              readOnly: true
          livenessProbe:
            httpGet:
              path: /healthz
              port: 8080
            initialDelaySeconds: 30
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /healthz/ready
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 5
            timeoutSeconds: 3
            failureThreshold: 3
          resources:
            requests:
              memory: "512Mi"
              cpu: "500m"
            limits:
              memory: "2Gi"
              cpu: "2000m"
      volumes:
        - name: metadata
          configMap:
            name: honua-metadata
---
apiVersion: v1
kind: Service
metadata:
  name: honua-server
  namespace: honua-test
spec:
  selector:
    app: honua-server
  ports:
    - port: 80
      targetPort: 8080
      protocol: TCP
  type: ClusterIP
```

Deploy:

```bash
kubectl apply -f honua-deployment.yaml

# Wait for deployment
kubectl wait --for=condition=available deployment/honua-server --timeout=300s

# Verify
kubectl get deployments
kubectl get pods -l app=honua-server
kubectl logs -l app=honua-server --tail=50 --all-containers=true

# Test service
kubectl port-forward service/honua-server 8080:80
curl http://localhost:8080/ogc
```

### 2.4 Testing Kubernetes Manifests

#### Horizontal Pod Autoscaler (HPA)

Create `hpa.yaml`:

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-server-hpa
  namespace: honua-test
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: honua-server
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
        - type: Percent
          value: 50
          periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
        - type: Percent
          value: 100
          periodSeconds: 15
        - type: Pods
          value: 2
          periodSeconds: 15
      selectPolicy: Max
```

Deploy and test:

```bash
# Enable metrics-server (if using Minikube)
minikube addons enable metrics-server

# Deploy HPA
kubectl apply -f hpa.yaml

# Verify HPA
kubectl get hpa -w

# Generate load to test autoscaling
kubectl run -it --rm load-generator --image=busybox --restart=Never -- /bin/sh -c \
  "while true; do wget -q -O- http://honua-server/ogc/collections; done"

# Watch pods scale up
kubectl get pods -l app=honua-server -w

# Stop load generator (Ctrl+C) and watch scale down
```

### 2.5 Ingress Controller Testing

#### Install Ingress Controller (kind)

```bash
# Install NGINX Ingress Controller
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml

# Wait for ingress controller to be ready
kubectl wait --namespace ingress-nginx \
  --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller \
  --timeout=90s
```

#### Install Ingress Controller (Minikube)

```bash
minikube addons enable ingress

# Verify
kubectl get pods -n ingress-nginx
```

#### Create Ingress

Create `ingress.yaml`:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: honua-ingress
  namespace: honua-test
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
    nginx.ingress.kubernetes.io/ssl-redirect: "false"
spec:
  ingressClassName: nginx
  rules:
    - host: honua.local
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: honua-server
                port:
                  number: 80
```

Deploy and test:

```bash
kubectl apply -f ingress.yaml

# Get ingress IP
kubectl get ingress honua-ingress

# For kind, use localhost:8080 (mapped in cluster config)
# For Minikube, get IP
minikube ip

# Add to /etc/hosts
echo "127.0.0.1 honua.local" | sudo tee -a /etc/hosts

# Test ingress (kind)
curl http://honua.local:8080/ogc

# Test ingress (Minikube)
curl http://honua.local/ogc
```

### 2.6 Storage Class Configuration

#### Create PersistentVolumeClaim for Tile Cache

Create `tile-cache-pvc.yaml`:

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: honua-tile-cache
  namespace: honua-test
spec:
  accessModes:
    - ReadWriteMany  # Multiple pods can read/write
  resources:
    requests:
      storage: 50Gi
  storageClassName: standard  # Use cluster default
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: honua-metadata-storage
  namespace: honua-test
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 10Gi
  storageClassName: standard
```

Update deployment to use PVC:

```yaml
# Add to honua-server Deployment spec.template.spec
volumes:
  - name: tile-cache
    persistentVolumeClaim:
      claimName: honua-tile-cache
  - name: metadata-storage
    persistentVolumeClaim:
      claimName: honua-metadata-storage

# Add to container volumeMounts
volumeMounts:
  - name: tile-cache
    mountPath: /app/tiles
  - name: metadata-storage
    mountPath: /app/metadata
```

Deploy:

```bash
kubectl apply -f tile-cache-pvc.yaml
kubectl get pvc

# Verify volumes are bound
kubectl describe pvc honua-tile-cache
```

### 2.7 Service Mesh Testing (Optional)

#### Install Istio

```bash
# Download Istio
curl -L https://istio.io/downloadIstio | sh -
cd istio-*/
export PATH=$PWD/bin:$PATH

# Install Istio on cluster
istioctl install --set profile=demo -y

# Enable sidecar injection for namespace
kubectl label namespace honua-test istio-injection=enabled

# Redeploy Honua (will inject Istio sidecar)
kubectl rollout restart deployment/honua-server -n honua-test

# Verify sidecar injection
kubectl get pods -n honua-test -o jsonpath='{.items[*].spec.containers[*].name}'
# Should show: honua-server istio-proxy
```

#### Install Linkerd (Lightweight Alternative)

```bash
# Install Linkerd CLI
curl --proto '=https' --tlsv1.2 -sSfL https://run.linkerd.io/install | sh
export PATH=$PATH:$HOME/.linkerd2/bin

# Install Linkerd on cluster
linkerd install | kubectl apply -f -

# Verify installation
linkerd check

# Inject Linkerd into namespace
kubectl get deploy -n honua-test -o yaml | linkerd inject - | kubectl apply -f -

# View dashboard
linkerd dashboard
```

## 3. Integration Test Scenarios

### 3.1 End-to-End OGC API Testing

Comprehensive E2E test for OGC API Features conformance.

#### Test Script

Create `tests/e2e/ogc-api-e2e.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${HONUA_BASE_URL:-http://localhost:5000}"
TESTS_PASSED=0
TESTS_FAILED=0

test_ogc_landing_page() {
    echo "Testing OGC API landing page..."
    response=$(curl -s -w "%{http_code}" -o /tmp/landing.json "$BASE_URL/ogc")

    if [ "$response" -eq 200 ]; then
        if jq -e '.links' /tmp/landing.json > /dev/null 2>&1; then
            echo "✓ Landing page passed"
            ((TESTS_PASSED++))
        else
            echo "✗ Landing page missing links"
            ((TESTS_FAILED++))
        fi
    else
        echo "✗ Landing page returned HTTP $response"
        ((TESTS_FAILED++))
    fi
}

test_ogc_conformance() {
    echo "Testing OGC API conformance classes..."
    response=$(curl -s -w "%{http_code}" -o /tmp/conformance.json "$BASE_URL/ogc/conformance")

    if [ "$response" -eq 200 ]; then
        if jq -e '.conformsTo | map(select(. == "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core"))' \
           /tmp/conformance.json | grep -q "core"; then
            echo "✓ Conformance passed"
            ((TESTS_PASSED++))
        else
            echo "✗ Missing core conformance class"
            ((TESTS_FAILED++))
        fi
    else
        echo "✗ Conformance returned HTTP $response"
        ((TESTS_FAILED++))
    fi
}

test_ogc_collections() {
    echo "Testing OGC API collections..."
    response=$(curl -s -w "%{http_code}" -o /tmp/collections.json "$BASE_URL/ogc/collections")

    if [ "$response" -eq 200 ]; then
        count=$(jq '.collections | length' /tmp/collections.json)
        if [ "$count" -gt 0 ]; then
            echo "✓ Collections passed ($count collections)"
            ((TESTS_PASSED++))
        else
            echo "✗ No collections found"
            ((TESTS_FAILED++))
        fi
    else
        echo "✗ Collections returned HTTP $response"
        ((TESTS_FAILED++))
    fi
}

test_ogc_items() {
    echo "Testing OGC API items..."

    # Get first collection ID
    collection=$(jq -r '.collections[0].id' /tmp/collections.json)

    if [ -z "$collection" ] || [ "$collection" = "null" ]; then
        echo "✗ No collection ID found"
        ((TESTS_FAILED++))
        return
    fi

    response=$(curl -s -w "%{http_code}" -o /tmp/items.json "$BASE_URL/ogc/collections/$collection/items?limit=10")

    if [ "$response" -eq 200 ]; then
        type=$(jq -r '.type' /tmp/items.json)
        if [ "$type" = "FeatureCollection" ]; then
            echo "✓ Items passed (collection: $collection)"
            ((TESTS_PASSED++))
        else
            echo "✗ Invalid GeoJSON type: $type"
            ((TESTS_FAILED++))
        fi
    else
        echo "✗ Items returned HTTP $response"
        ((TESTS_FAILED++))
    fi
}

# Run all tests
test_ogc_landing_page
test_ogc_conformance
test_ogc_collections
test_ogc_items

# Print results
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Results: $TESTS_PASSED passed, $TESTS_FAILED failed"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

[ "$TESTS_FAILED" -eq 0 ] && exit 0 || exit 1
```

Run:

```bash
chmod +x tests/e2e/ogc-api-e2e.sh
./tests/e2e/ogc-api-e2e.sh
```

### 3.2 Tile Cache Integration Tests

Test tile generation, caching, and retrieval.

#### Test with LocalStack S3

```bash
#!/usr/bin/env bash
set -euo pipefail

BASE_URL="http://localhost:5000"
LOCALSTACK_ENDPOINT="http://localhost:4566"
BUCKET="honua-tile-cache"

# Create test bucket
aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 mb s3://$BUCKET 2>/dev/null || true

# Request tile (should generate and cache)
echo "Requesting tile (first request - should cache)..."
time curl -s -o /tmp/tile1.mvt "$BASE_URL/ogc/tiles/mvt/roads/14/8192/5461"

# Verify tile was cached in S3
echo "Verifying tile in S3..."
aws --endpoint-url="$LOCALSTACK_ENDPOINT" s3 ls s3://$BUCKET/ --recursive | grep "14/8192/5461"

# Request same tile again (should serve from cache)
echo "Requesting tile (second request - should be cached)..."
time curl -s -o /tmp/tile2.mvt "$BASE_URL/ogc/tiles/mvt/roads/14/8192/5461"

# Compare tiles (should be identical)
if diff /tmp/tile1.mvt /tmp/tile2.mvt; then
    echo "✓ Tiles are identical (cache working)"
else
    echo "✗ Tiles differ (cache not working)"
    exit 1
fi

# Check tile is valid MVT
file /tmp/tile1.mvt | grep -q "gzip" && echo "✓ Valid MVT format" || echo "✗ Invalid MVT format"
```

### 3.3 Authentication Flow Testing

Test OAuth/OIDC authentication flows.

#### Test QuickStart Mode

```bash
#!/usr/bin/env bash
set -euo pipefail

BASE_URL="http://localhost:5000"

# Test unauthenticated access (should work in QuickStart mode)
echo "Testing unauthenticated access..."
response=$(curl -s -w "%{http_code}" -o /dev/null "$BASE_URL/ogc/collections")

if [ "$response" -eq 200 ]; then
    echo "✓ QuickStart mode allows unauthenticated access"
else
    echo "✗ QuickStart mode blocked access (HTTP $response)"
    exit 1
fi

# Test with API key (optional in QuickStart)
echo "Testing API key authentication..."
response=$(curl -s -w "%{http_code}" -o /dev/null -H "X-API-Key: test-key" "$BASE_URL/ogc/collections")

if [ "$response" -eq 200 ]; then
    echo "✓ API key accepted"
else
    echo "✗ API key rejected (HTTP $response)"
    exit 1
fi
```

#### Test OAuth Flow (with Mock Provider)

```bash
#!/usr/bin/env bash
# Requires running mock OAuth server (e.g., mock-oauth2-server)

OAUTH_URL="http://localhost:8080"
CLIENT_ID="honua-test"
CLIENT_SECRET="test-secret"
BASE_URL="http://localhost:5000"

# Get access token
echo "Requesting OAuth token..."
response=$(curl -s -X POST "$OAUTH_URL/token" \
  -d "grant_type=client_credentials" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET")

token=$(echo "$response" | jq -r '.access_token')

if [ -z "$token" ] || [ "$token" = "null" ]; then
    echo "✗ Failed to get access token"
    exit 1
fi

echo "✓ Received access token"

# Test authenticated request
echo "Testing authenticated request..."
response=$(curl -s -w "%{http_code}" -o /tmp/auth-response.json \
  -H "Authorization: Bearer $token" \
  "$BASE_URL/ogc/collections")

if [ "$response" -eq 200 ]; then
    echo "✓ Authenticated request succeeded"
else
    echo "✗ Authenticated request failed (HTTP $response)"
    exit 1
fi
```

### 3.4 Database Integration Tests

Test multi-database support (PostgreSQL, MySQL, SQL Server).

#### PostgreSQL Integration Test

```bash
#!/usr/bin/env bash
set -euo pipefail

# Start PostgreSQL container
docker run -d --name honua-postgres-test \
  -e POSTGRES_USER=honua \
  -e POSTGRES_PASSWORD=test \
  -e POSTGRES_DB=honua_test \
  -p 5433:5432 \
  postgis/postgis:16-3.4

# Wait for PostgreSQL
for i in {1..30}; do
  if docker exec honua-postgres-test pg_isready -U honua > /dev/null 2>&1; then
    break
  fi
  sleep 2
done

# Run migrations
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=honua_test;Username=honua;Password=test"
dotnet run --project tools/MigrationTool migrate

# Start Honua with PostgreSQL
dotnet run --project src/Honua.Server.Host --urls http://localhost:5000 &
HONUA_PID=$!

# Wait for Honua to start
sleep 10

# Test API with database
response=$(curl -s -w "%{http_code}" -o /dev/null http://localhost:5000/ogc/collections)

if [ "$response" -eq 200 ]; then
    echo "✓ PostgreSQL integration working"
else
    echo "✗ PostgreSQL integration failed (HTTP $response)"
fi

# Cleanup
kill $HONUA_PID
docker rm -f honua-postgres-test
```

### 3.5 Multi-Service Orchestration Tests

Test complete stack with all services.

#### Docker Compose E2E Test

Create `docker-compose.e2e.yml`:

```yaml
version: '3.9'

services:
  postgres:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: honua_test
      POSTGRES_DB: honua
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua"]
      interval: 10s
      timeout: 5s
      retries: 5

  localstack:
    image: localstack/localstack:latest
    environment:
      SERVICES: s3,secretsmanager
      DEBUG: 1
    ports:
      - "4566:4566"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:4566/_localstack/health"]
      interval: 10s
      timeout: 5s
      retries: 5

  honua:
    build: .
    depends_on:
      postgres:
        condition: service_healthy
      localstack:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=honua;Username=honua;Password=honua_test"
      HONUA__AUTHENTICATION__MODE: QuickStart
      HONUA__TILECACHE__PROVIDER: s3
      HONUA__TILECACHE__S3__BUCKETNAME: honua-tiles
      HONUA__TILECACHE__S3__SERVICEURL: http://localstack:4566
      AWS_ACCESS_KEY_ID: test
      AWS_SECRET_ACCESS_KEY: test
    ports:
      - "5000:8080"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 10s
      timeout: 5s
      retries: 5

  test-runner:
    image: curlimages/curl:latest
    depends_on:
      honua:
        condition: service_healthy
    command: >
      sh -c "
        echo 'Running E2E tests...' &&
        curl -f http://honua:8080/ogc &&
        curl -f http://honua:8080/ogc/collections &&
        echo 'All tests passed!'
      "
```

Run:

```bash
docker-compose -f docker-compose.e2e.yml up --abort-on-container-exit --exit-code-from test-runner

# Cleanup
docker-compose -f docker-compose.e2e.yml down -v
```

## 4. CI/CD Integration

### 4.1 GitHub Actions with LocalStack

Create `.github/workflows/integration-tests.yml`:

```yaml
name: Integration Tests

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  localstack-tests:
    name: LocalStack Integration Tests
    runs-on: ubuntu-latest

    services:
      localstack:
        image: localstack/localstack:latest
        env:
          SERVICES: s3,dynamodb,secretsmanager
          DEBUG: 1
        ports:
          - 4566:4566
        options: >-
          --health-cmd "curl -f http://localhost:4566/_localstack/health || exit 1"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      postgres:
        image: postgis/postgis:16-3.4
        env:
          POSTGRES_USER: honua
          POSTGRES_PASSWORD: honua_test
          POSTGRES_DB: honua_test
        ports:
          - 5432:5432
        options: >-
          --health-cmd "pg_isready -U honua"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Install AWS CLI
        run: pip install awscli

      - name: Configure AWS for LocalStack
        run: |
          aws configure set aws_access_key_id test
          aws configure set aws_secret_access_key test
          aws configure set region us-east-1

      - name: Setup LocalStack S3 bucket
        run: |
          aws --endpoint-url=http://localhost:4566 s3 mb s3://honua-test-bucket
          aws --endpoint-url=http://localhost:4566 s3api put-bucket-versioning \
            --bucket honua-test-bucket \
            --versioning-configuration Status=Enabled

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Run integration tests
        env:
          HONUA__DATABASE__CONNECTIONSTRING: "Host=localhost;Port=5432;Database=honua_test;Username=honua;Password=honua_test"
          HONUA__TILECACHE__S3__SERVICEURL: "http://localhost:4566"
          HONUA__TILECACHE__S3__BUCKETNAME: "honua-test-bucket"
          AWS_ACCESS_KEY_ID: test
          AWS_SECRET_ACCESS_KEY: test
          AWS_DEFAULT_REGION: us-east-1
        run: |
          dotnet test \
            --no-build \
            --configuration Release \
            --filter "Category=Integration" \
            --logger "trx;LogFileName=integration-test-results.trx" \
            --results-directory ./TestResults

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: integration-test-results
          path: TestResults/*.trx
          retention-days: 30

  docker-tests:
    name: Docker Integration Tests
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Build Docker image
        run: docker build -t honua-server:test .

      - name: Run Docker tests
        run: |
          chmod +x tests/e2e/docker-scenarios.sh
          ./tests/e2e/docker-scenarios.sh

      - name: Upload Docker logs
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: docker-logs
          path: /tmp/*.log

  kubernetes-tests:
    name: Kubernetes Integration Tests
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Install kind
        uses: helm/kind-action@v1
        with:
          cluster_name: honua-test
          wait: 120s

      - name: Verify cluster
        run: |
          kubectl cluster-info
          kubectl get nodes

      - name: Build Docker image
        run: docker build -t honua-server:test .

      - name: Load image into kind
        run: kind load docker-image honua-server:test --name honua-test

      - name: Run Kubernetes tests
        run: |
          chmod +x tests/e2e/k8s-scenarios.sh
          ./tests/e2e/k8s-scenarios.sh

      - name: Export cluster logs on failure
        if: failure()
        run: |
          kubectl get all -A
          kubectl describe pods -A
          kubectl logs -l app=honua-server --tail=100

      - name: Upload cluster logs
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: kubernetes-logs
          path: /tmp/k8s-*.log
```

### 4.2 GitLab CI with Minikube

Create `.gitlab-ci.yml`:

```yaml
stages:
  - build
  - test-unit
  - test-integration
  - test-e2e

variables:
  DOCKER_DRIVER: overlay2
  DOCKER_TLS_CERTDIR: ""
  KUBERNETES_VERSION: "v1.28.0"

build:
  stage: build
  image: mcr.microsoft.com/dotnet/sdk:9.0
  script:
    - dotnet restore
    - dotnet build --configuration Release --no-restore
  artifacts:
    paths:
      - "**/bin/Release/**"
      - "**/obj/Release/**"
    expire_in: 1 hour

unit-tests:
  stage: test-unit
  image: mcr.microsoft.com/dotnet/sdk:9.0
  script:
    - dotnet test --configuration Release --filter "Category!=Integration"
  dependencies:
    - build

integration-tests-localstack:
  stage: test-integration
  image: mcr.microsoft.com/dotnet/sdk:9.0
  services:
    - name: localstack/localstack:latest
      alias: localstack
      variables:
        SERVICES: s3,dynamodb,secretsmanager
        DEBUG: "1"
    - name: postgis/postgis:16-3.4
      alias: postgres
      variables:
        POSTGRES_USER: honua
        POSTGRES_PASSWORD: honua_test
        POSTGRES_DB: honua_test
  before_script:
    - apt-get update && apt-get install -y curl awscli
    - aws configure set aws_access_key_id test
    - aws configure set aws_secret_access_key test
    - aws configure set region us-east-1
  script:
    - aws --endpoint-url=http://localstack:4566 s3 mb s3://honua-test-bucket || true
    - export HONUA__TILECACHE__S3__SERVICEURL="http://localstack:4566"
    - export HONUA__DATABASE__CONNECTIONSTRING="Host=postgres;Port=5432;Database=honua_test;Username=honua;Password=honua_test"
    - dotnet test --configuration Release --filter "Category=Integration"
  dependencies:
    - build

integration-tests-docker:
  stage: test-integration
  image: docker:24
  services:
    - docker:24-dind
  before_script:
    - apk add --no-cache bash curl jq
  script:
    - docker build -t honua-server:test .
    - chmod +x tests/e2e/docker-scenarios.sh
    - ./tests/e2e/docker-scenarios.sh

e2e-tests-kubernetes:
  stage: test-e2e
  image: docker:24
  services:
    - docker:24-dind
  before_script:
    - apk add --no-cache bash curl kubectl
    # Install kind
    - curl -Lo ./kind https://kind.sigs.k8s.io/dl/v0.20.0/kind-linux-amd64
    - chmod +x ./kind
    - mv ./kind /usr/local/bin/kind
  script:
    # Create kind cluster
    - kind create cluster --name honua-test --wait 300s
    - kubectl cluster-info
    # Build and load image
    - docker build -t honua-server:test .
    - kind load docker-image honua-server:test --name honua-test
    # Run tests
    - chmod +x tests/e2e/k8s-scenarios.sh
    - ./tests/e2e/k8s-scenarios.sh
  after_script:
    - kind delete cluster --name honua-test
  artifacts:
    when: on_failure
    paths:
      - /tmp/k8s-*.log
    expire_in: 7 days
```

### 4.3 Test Automation Workflows

#### Nightly Integration Tests

Create `.github/workflows/nightly-integration.yml`:

```yaml
name: Nightly Integration Tests

on:
  schedule:
    - cron: '0 2 * * *'  # 2 AM daily
  workflow_dispatch:

jobs:
  comprehensive-tests:
    name: Comprehensive Integration Tests
    runs-on: ubuntu-latest
    timeout-minutes: 60

    strategy:
      matrix:
        database:
          - postgres
          - mysql
          - sqlserver
        cache:
          - filesystem
          - s3

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Start LocalStack
        if: matrix.cache == 's3'
        run: |
          docker run -d --name localstack \
            -p 4566:4566 \
            -e SERVICES=s3 \
            localstack/localstack:latest

      - name: Start Database
        run: |
          if [ "${{ matrix.database }}" = "postgres" ]; then
            docker run -d --name db -p 5432:5432 \
              -e POSTGRES_USER=honua -e POSTGRES_PASSWORD=test -e POSTGRES_DB=honua \
              postgis/postgis:16-3.4
          elif [ "${{ matrix.database }}" = "mysql" ]; then
            docker run -d --name db -p 3306:3306 \
              -e MYSQL_ROOT_PASSWORD=test -e MYSQL_DATABASE=honua \
              mysql/mysql-server:8.0
          else
            docker run -d --name db -p 1433:1433 \
              -e ACCEPT_EULA=Y -e SA_PASSWORD=Test123! \
              mcr.microsoft.com/mssql/server:2022-latest
          fi

      - name: Run tests
        env:
          TEST_DATABASE: ${{ matrix.database }}
          TEST_CACHE: ${{ matrix.cache }}
        run: |
          dotnet test --filter "Category=Integration|Category=E2E" \
            --logger "trx;LogFileName=${{ matrix.database }}-${{ matrix.cache }}-results.trx"

      - name: Upload results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results-${{ matrix.database }}-${{ matrix.cache }}
          path: TestResults/*.trx

      - name: Create issue on failure
        if: failure()
        uses: actions/github-script@v7
        with:
          script: |
            github.rest.issues.create({
              owner: context.repo.owner,
              repo: context.repo.repo,
              title: `Nightly integration tests failed: ${{ matrix.database }}/${{ matrix.cache }}`,
              body: `Integration tests failed for database=${{ matrix.database }}, cache=${{ matrix.cache }}\n\nRun: ${context.serverUrl}/${context.repo.owner}/${context.repo.repo}/actions/runs/${context.runId}`,
              labels: ['testing', 'automated', 'integration']
            })
```

### 4.4 Integration Test Reporting

#### Generate HTML Reports

```bash
#!/usr/bin/env bash
# generate-test-report.sh

set -euo pipefail

RESULTS_DIR="${1:-./TestResults}"
REPORT_DIR="${2:-./test-reports}"

mkdir -p "$REPORT_DIR"

# Install ReportGenerator (if not already installed)
dotnet tool install --global dotnet-reportgenerator-globaltool || true

# Generate coverage report
reportgenerator \
  -reports:"$RESULTS_DIR/**/coverage.opencover.xml" \
  -targetdir:"$REPORT_DIR/coverage" \
  -reporttypes:"Html;Badges;JsonSummary"

# Generate test report from TRX files
cat > "$REPORT_DIR/index.html" <<EOF
<!DOCTYPE html>
<html>
<head>
    <title>Honua Integration Test Results</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        h1 { color: #333; }
        .passed { color: green; font-weight: bold; }
        .failed { color: red; font-weight: bold; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #4CAF50; color: white; }
    </style>
</head>
<body>
    <h1>Integration Test Results</h1>
    <p>Generated: $(date)</p>

    <h2>Summary</h2>
    <table>
        <tr>
            <th>Test Suite</th>
            <th>Total</th>
            <th>Passed</th>
            <th>Failed</th>
            <th>Status</th>
        </tr>
EOF

# Parse TRX files and add to report
for trx in "$RESULTS_DIR"/*.trx; do
    [ -e "$trx" ] || continue

    # Extract test counts (simplified - would need proper XML parsing)
    total=$(grep -c '<UnitTestResult' "$trx" || echo "0")
    failed=$(grep -c 'outcome="Failed"' "$trx" || echo "0")
    passed=$((total - failed))

    suite=$(basename "$trx" .trx)
    status="✓ PASSED"
    status_class="passed"

    if [ "$failed" -gt 0 ]; then
        status="✗ FAILED"
        status_class="failed"
    fi

    cat >> "$REPORT_DIR/index.html" <<EOF
        <tr>
            <td>$suite</td>
            <td>$total</td>
            <td>$passed</td>
            <td>$failed</td>
            <td class="$status_class">$status</td>
        </tr>
EOF
done

cat >> "$REPORT_DIR/index.html" <<EOF
    </table>

    <h2>Coverage Report</h2>
    <p><a href="coverage/index.html">View detailed coverage report</a></p>
</body>
</html>
EOF

echo "Report generated: $REPORT_DIR/index.html"
```

## 5. Testing Tools

### 5.1 Postman/Newman for API Testing

#### Create Postman Collection

Create `tests/postman/honua-integration.postman_collection.json`:

```json
{
  "info": {
    "name": "Honua Integration Tests",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "variable": [
    {
      "key": "base_url",
      "value": "http://localhost:5000"
    }
  ],
  "item": [
    {
      "name": "OGC API Landing Page",
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "pm.test('Status is 200', function() {",
              "    pm.response.to.have.status(200);",
              "});",
              "",
              "pm.test('Has links array', function() {",
              "    const json = pm.response.json();",
              "    pm.expect(json).to.have.property('links');",
              "    pm.expect(json.links).to.be.an('array');",
              "});"
            ]
          }
        }
      ],
      "request": {
        "method": "GET",
        "header": [],
        "url": {
          "raw": "{{base_url}}/ogc",
          "host": ["{{base_url}}"],
          "path": ["ogc"]
        }
      }
    },
    {
      "name": "OGC API Conformance",
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "pm.test('Status is 200', function() {",
              "    pm.response.to.have.status(200);",
              "});",
              "",
              "pm.test('Conforms to Core', function() {",
              "    const json = pm.response.json();",
              "    pm.expect(json.conformsTo).to.include('http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core');",
              "});"
            ]
          }
        }
      ],
      "request": {
        "method": "GET",
        "url": "{{base_url}}/ogc/conformance"
      }
    },
    {
      "name": "Get Collections",
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "pm.test('Status is 200', function() {",
              "    pm.response.to.have.status(200);",
              "});",
              "",
              "pm.test('Has collections', function() {",
              "    const json = pm.response.json();",
              "    pm.expect(json.collections).to.be.an('array');",
              "    pm.expect(json.collections.length).to.be.above(0);",
              "    ",
              "    // Save first collection ID for next request",
              "    pm.environment.set('collection_id', json.collections[0].id);",
              "});"
            ]
          }
        }
      ],
      "request": {
        "method": "GET",
        "url": "{{base_url}}/ogc/collections"
      }
    },
    {
      "name": "Get Items from Collection",
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "pm.test('Status is 200', function() {",
              "    pm.response.to.have.status(200);",
              "});",
              "",
              "pm.test('Is GeoJSON FeatureCollection', function() {",
              "    const json = pm.response.json();",
              "    pm.expect(json.type).to.equal('FeatureCollection');",
              "    pm.expect(json.features).to.be.an('array');",
              "});"
            ]
          }
        }
      ],
      "request": {
        "method": "GET",
        "url": {
          "raw": "{{base_url}}/ogc/collections/{{collection_id}}/items?limit=10",
          "host": ["{{base_url}}"],
          "path": ["ogc", "collections", "{{collection_id}}", "items"],
          "query": [
            {
              "key": "limit",
              "value": "10"
            }
          ]
        }
      }
    }
  ]
}
```

#### Run with Newman

```bash
# Install Newman
npm install -g newman newman-reporter-htmlextra

# Run collection
newman run tests/postman/honua-integration.postman_collection.json \
  --environment tests/postman/local.postman_environment.json \
  --reporters cli,htmlextra \
  --reporter-htmlextra-export ./test-reports/newman-report.html

# Run in CI/CD
newman run tests/postman/honua-integration.postman_collection.json \
  --environment tests/postman/ci.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export ./TestResults/newman-results.json \
  --bail  # Stop on first failure
```

### 5.2 k6 for Load Testing

Create `tests/k6/load-test.js`:

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const errorRate = new Rate('errors');

export const options = {
  stages: [
    { duration: '30s', target: 10 },  // Ramp up to 10 users
    { duration: '1m', target: 10 },   // Stay at 10 users
    { duration: '30s', target: 50 },  // Ramp up to 50 users
    { duration: '2m', target: 50 },   // Stay at 50 users
    { duration: '30s', target: 0 },   // Ramp down
  ],
  thresholds: {
    'http_req_duration': ['p(95)<500'],  // 95% of requests must complete below 500ms
    'errors': ['rate<0.1'],              // Error rate must be below 10%
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function() {
  // Test landing page
  let landingRes = http.get(`${BASE_URL}/ogc`);
  check(landingRes, {
    'landing page status is 200': (r) => r.status === 200,
    'landing page has links': (r) => JSON.parse(r.body).links !== undefined,
  }) || errorRate.add(1);

  sleep(1);

  // Test collections
  let collectionsRes = http.get(`${BASE_URL}/ogc/collections`);
  check(collectionsRes, {
    'collections status is 200': (r) => r.status === 200,
    'has collections array': (r) => JSON.parse(r.body).collections !== undefined,
  }) || errorRate.add(1);

  if (collectionsRes.status === 200) {
    const collections = JSON.parse(collectionsRes.body).collections;
    if (collections.length > 0) {
      const collectionId = collections[0].id;

      // Test items endpoint
      let itemsRes = http.get(`${BASE_URL}/ogc/collections/${collectionId}/items?limit=10`);
      check(itemsRes, {
        'items status is 200': (r) => r.status === 200,
        'items is FeatureCollection': (r) => JSON.parse(r.body).type === 'FeatureCollection',
      }) || errorRate.add(1);
    }
  }

  sleep(1);

  // Test tile endpoint (if available)
  let tileRes = http.get(`${BASE_URL}/ogc/tiles/mvt/roads/14/8192/5461`);
  check(tileRes, {
    'tile request completed': (r) => r.status === 200 || r.status === 404,
  });

  sleep(1);
}
```

Run k6 tests:

```bash
# Install k6
# macOS
brew install k6

# Linux
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg \
  --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | \
  sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6

# Run load test
k6 run tests/k6/load-test.js

# Run with custom environment
BASE_URL=http://honua.local:8080 k6 run tests/k6/load-test.js

# Run with cloud output
k6 run --out cloud tests/k6/load-test.js
```

### 5.3 Terraform for Infrastructure Testing

Create `tests/terraform/localstack/main.tf`:

```hcl
terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region                      = "us-east-1"
  access_key                  = "test"
  secret_key                  = "test"
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true

  endpoints {
    s3             = "http://localhost:4566"
    dynamodb       = "http://localhost:4566"
    secretsmanager = "http://localhost:4566"
  }
}

# S3 bucket for tile cache
resource "aws_s3_bucket" "tile_cache" {
  bucket = "honua-tile-cache"
}

resource "aws_s3_bucket_versioning" "tile_cache_versioning" {
  bucket = aws_s3_bucket.tile_cache.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_cors_configuration" "tile_cache_cors" {
  bucket = aws_s3_bucket.tile_cache.id

  cors_rule {
    allowed_headers = ["*"]
    allowed_methods = ["GET", "HEAD"]
    allowed_origins = ["*"]
    expose_headers  = ["ETag"]
    max_age_seconds = 3600
  }
}

# DynamoDB table for metadata
resource "aws_dynamodb_table" "metadata" {
  name         = "HonuaMetadata"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "CollectionId"
  range_key    = "Timestamp"

  attribute {
    name = "CollectionId"
    type = "S"
  }

  attribute {
    name = "Timestamp"
    type = "N"
  }

  tags = {
    Environment = "test"
    Application = "honua"
  }
}

# Secrets Manager secret for database credentials
resource "aws_secretsmanager_secret" "db_credentials" {
  name = "honua/database/credentials"
}

resource "aws_secretsmanager_secret_version" "db_credentials_value" {
  secret_id = aws_secretsmanager_secret.db_credentials.id
  secret_string = jsonencode({
    username = "honua"
    password = "test_password"
    host     = "localhost"
    port     = 5432
    database = "honua"
  })
}

# Outputs
output "tile_cache_bucket_name" {
  value = aws_s3_bucket.tile_cache.bucket
}

output "metadata_table_name" {
  value = aws_dynamodb_table.metadata.name
}

output "db_secret_arn" {
  value = aws_secretsmanager_secret.db_credentials.arn
}
```

Test with Terraform:

```bash
cd tests/terraform/localstack

# Initialize
terraform init

# Plan
terraform plan

# Apply
terraform apply -auto-approve

# Verify resources
aws --endpoint-url=http://localhost:4566 s3 ls
aws --endpoint-url=http://localhost:4566 dynamodb list-tables
aws --endpoint-url=http://localhost:4566 secretsmanager list-secrets

# Destroy
terraform destroy -auto-approve
```

### 5.4 Docker Compose Test Stacks

Create `tests/docker-compose/full-stack.yml`:

```yaml
version: '3.9'

services:
  # Infrastructure
  postgres:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: honua_test
      POSTGRES_DB: honua
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua"]
      interval: 10s
      timeout: 5s
      retries: 5

  localstack:
    image: localstack/localstack:latest
    environment:
      SERVICES: s3,dynamodb,secretsmanager
      DEBUG: 1
    ports:
      - "4566:4566"
    volumes:
      - "/var/run/docker.sock:/var/run/docker.sock"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:4566/_localstack/health"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Application
  honua:
    build:
      context: ../../..
      dockerfile: Dockerfile
    depends_on:
      postgres:
        condition: service_healthy
      localstack:
        condition: service_healthy
      redis:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=honua;Username=honua;Password=honua_test"
      HONUA__AUTHENTICATION__MODE: QuickStart
      HONUA__TILECACHE__PROVIDER: s3
      HONUA__TILECACHE__S3__BUCKETNAME: honua-tiles
      HONUA__TILECACHE__S3__SERVICEURL: http://localstack:4566
      HONUA__CACHE__REDIS__ENDPOINT: redis:6379
      AWS_ACCESS_KEY_ID: test
      AWS_SECRET_ACCESS_KEY: test
    ports:
      - "5000:8080"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Testing
  integration-tests:
    image: mcr.microsoft.com/dotnet/sdk:9.0
    depends_on:
      honua:
        condition: service_healthy
    volumes:
      - ../../..:/app
    working_dir: /app
    command: >
      bash -c "
        dotnet test --filter Category=Integration --logger 'console;verbosity=detailed'
      "

volumes:
  postgres-data:
```

Run:

```bash
cd tests/docker-compose

# Start stack and run tests
docker-compose -f full-stack.yml up --abort-on-container-exit --exit-code-from integration-tests

# Cleanup
docker-compose -f full-stack.yml down -v
```

## 6. Cost-Free Testing Strategy

### 6.1 When to Use LocalStack vs Real AWS

#### Use LocalStack For:
- **Unit and integration tests** in CI/CD pipelines
- **Local development** and testing
- **S3 operations** (file upload/download, bucket policies)
- **DynamoDB** basic operations
- **Secrets Manager** credential management
- **Early-stage development** and prototyping
- **Testing infrastructure-as-code** (Terraform, CloudFormation)

#### Use Real AWS For:
- **Production deployment** testing
- **Performance testing** at scale
- **Advanced AWS features** not fully supported by LocalStack:
  - RDS Aurora Serverless
  - Advanced IAM policies
  - Cross-region replication
  - CloudFront edge cases
  - Complex VPC configurations
- **Compliance and security** testing
- **Final pre-production validation**

### 6.2 When to Use Minikube/kind vs Real Clusters

#### Use Minikube/kind For:
- **Local Kubernetes development**
- **CI/CD pipeline testing**
- **Manifest validation** and debugging
- **Helm chart testing**
- **Multi-container orchestration** tests
- **Resource limit** testing
- **Network policy** validation
- **Storage class** configuration

#### Use Real Kubernetes Clusters For:
- **Production deployment** testing
- **Performance and load testing** at scale
- **Multi-node** behavior and failover
- **Cloud-specific features**:
  - EKS-specific configurations
  - GKE Autopilot
  - AKS virtual nodes
- **Production ingress controllers** with real TLS
- **Cluster autoscaling** behavior
- **Production monitoring** and observability

### 6.3 When Real Cloud Credentials Are Needed

#### Absolutely Required:
1. **Production deployments**
2. **Integration with cloud-specific managed services**:
   - AWS RDS (not PostgreSQL container)
   - AWS ElastiCache (not Redis container)
   - AWS EKS (not kind/Minikube)
3. **Testing cloud-specific features**:
   - CloudFront CDN behavior
   - Route53 DNS
   - AWS Certificate Manager
   - IAM authentication
4. **Performance testing** with production-like scale
5. **Security and compliance** validation

#### Not Required:
1. **Local development**
2. **Unit tests**
3. **Integration tests** (use LocalStack/Testcontainers)
4. **CI/CD pipeline tests** (use LocalStack/kind)
5. **Infrastructure testing** (use LocalStack + Terraform)
6. **API contract testing**
7. **Database migration** testing (use containers)

### 6.4 Cost Optimization for Testing

#### Free Tier Usage
```bash
# AWS Free Tier Limits (12 months)
# - EC2: 750 hours/month t2.micro (1 instance)
# - S3: 5GB storage, 20K GET, 2K PUT
# - RDS: 750 hours/month db.t2.micro
# - Lambda: 1M requests/month, 400K GB-seconds

# Use free tier for:
# - Smoke tests on real infrastructure
# - Production deployment validation
# - Performance baseline measurements
```

#### On-Demand vs Spot Instances
```bash
# Use spot instances for testing (up to 90% savings)
aws ec2 run-instances \
  --instance-market-options MarketType=spot,MaxPrice=0.05 \
  --instance-type t3.medium \
  --image-id ami-xxxxx

# Automatically terminate after tests
aws ec2 terminate-instances --instance-ids i-xxxxx
```

#### Ephemeral Test Environments
```bash
# Create test environment only when needed
./scripts/create-test-env.sh

# Run tests
./tests/e2e/run-all-tests.sh

# Destroy immediately after tests
./scripts/destroy-test-env.sh
```

#### LocalStack Pro vs Community
- **Community** (Free): S3, DynamoDB, Lambda, SQS, SNS, CloudWatch Logs
- **Pro** ($$$): Advanced features, better AWS parity, enterprise support
- **Decision**: Start with Community, upgrade only if needed

### 6.5 Hybrid Testing Approach

Recommended strategy balancing cost and coverage:

```text
┌─────────────────────────────────────────────────────────┐
│ Development Phase          │ Testing Environment        │
├─────────────────────────────────────────────────────────┤
│ Local Development         │ LocalStack + Testcontainers│
│ Feature Development       │ LocalStack + kind          │
│ Pull Request CI           │ LocalStack + kind          │
│ Integration Tests         │ LocalStack + Minikube      │
│ Nightly Tests             │ LocalStack + kind          │
│ Pre-Release Testing       │ Real AWS (limited)         │
│ Production Validation     │ Real AWS + Real K8s        │
└─────────────────────────────────────────────────────────┘
```

#### Cost Breakdown Example

```text
Monthly Testing Costs (Estimate):

LocalStack Community:     $0/month
kind/Minikube:            $0/month
Testcontainers:           $0/month
GitHub Actions (2000min): $0/month (free tier)

AWS Free Tier:            $0/month
AWS Testing (beyond free):$50-100/month (if using real AWS)

Recommendation: 95% LocalStack, 5% Real AWS = ~$5-10/month
```

## 7. Best Practices

### 7.1 Test Isolation
- Each test should create and clean up its own resources
- Use unique names/IDs to prevent conflicts
- Don't rely on shared state between tests

### 7.2 Fast Feedback
- Run fast tests (LocalStack, containers) in CI on every PR
- Run expensive tests (real cloud) nightly or on-demand
- Fail fast: Stop on first critical failure

### 7.3 Reproducibility
- Use fixed versions for all Docker images
- Pin Kubernetes versions in kind/Minikube
- Document all environment requirements

### 7.4 Monitoring and Observability
- Capture logs from all services
- Export metrics during tests
- Generate HTML reports for visibility

### 7.5 Security
- Never commit real AWS credentials
- Use IAM roles when possible
- Rotate test credentials regularly
- Use separate AWS accounts for testing

## 8. Troubleshooting

### 8.1 LocalStack Issues

**Issue**: LocalStack services not starting
```bash
# Check logs
docker logs honua-localstack

# Restart with debug
docker run -e DEBUG=1 -p 4566:4566 localstack/localstack:latest

# Verify health
curl http://localhost:4566/_localstack/health | jq
```

**Issue**: Cannot connect to LocalStack from container
```bash
# Use host.docker.internal instead of localhost
export AWS_ENDPOINT_URL=http://host.docker.internal:4566

# Or add to same Docker network
docker network create honua-test
docker network connect honua-test honua-localstack
```

### 8.2 Kubernetes Issues

**Issue**: Image not found in kind
```bash
# Verify image is loaded
docker exec -it kind-control-plane crictl images | grep honua

# Load image again
kind load docker-image honua-server:test --name honua-test
```

**Issue**: Pods not starting
```bash
# Check pod status
kubectl describe pod <pod-name>

# Check logs
kubectl logs <pod-name> --previous

# Check events
kubectl get events --sort-by='.lastTimestamp'
```

### 8.3 Test Failures

**Issue**: Intermittent test failures
```bash
# Increase timeouts
export TEST_TIMEOUT=300

# Add retry logic
for i in {1..3}; do
  ./tests/e2e/run-tests.sh && break
  sleep 10
done
```

## 9. See Also

- [Docker Deployment Guide](../02-deployment/docker-deployment.md)
- [Kubernetes Deployment Guide](../02-deployment/kubernetes-deployment.md)
- [AWS ECS Deployment Guide](../02-deployment/aws-ecs-deployment.md)
- [Troubleshooting Guide](../04-operations/troubleshooting.md)
- [Performance Tuning Guide](../04-operations/performance-tuning.md)
- [Environment Variables Reference](../01-configuration/environment-variables.md)

## External Resources

- [LocalStack Documentation](https://docs.localstack.cloud/)
- [kind Quick Start](https://kind.sigs.k8s.io/docs/user/quick-start/)
- [Minikube Documentation](https://minikube.sigs.k8s.io/docs/)
- [Testcontainers Documentation](https://dotnet.testcontainers.org/)
- [k6 Load Testing](https://k6.io/docs/)
- [Newman CLI](https://learning.postman.com/docs/running-collections/using-newman-cli/command-line-integration-with-newman/)
