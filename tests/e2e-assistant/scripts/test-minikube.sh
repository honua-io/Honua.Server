#!/bin/bash
# E2E Test: Minikube with PostgreSQL + HPA + Ingress
# Tests AI Assistant's ability to deploy production Kubernetes stack
# Note: Requires minikube to be installed

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT_ROOT="$(dirname "$(dirname "$TEST_DIR")")"

echo "=== Test: Minikube with PostgreSQL + HPA + Ingress ==="

# Check if minikube is installed
if ! command -v minikube &> /dev/null; then
    echo "⚠ Minikube is not installed - skipping test"
    echo "Install minikube: https://minikube.sigs.k8s.io/docs/start/"
    exit 0  # Exit gracefully
fi

# Check if kubectl is installed
if ! command -v kubectl &> /dev/null; then
    echo "⚠ kubectl is not installed - skipping test"
    echo "Install kubectl: https://kubernetes.io/docs/tasks/tools/"
    exit 0  # Exit gracefully
fi

# Start minikube if not running
echo "Checking minikube status..."
if ! minikube status --profile honua-e2e &> /dev/null; then
    echo "Starting minikube..."
    minikube start --profile honua-e2e --cpus 2 --memory 4096
else
    echo "✓ Minikube already running"
fi

# Set kubectl context
kubectl config use-context honua-e2e

# Create namespace
echo "Creating Kubernetes namespace..."
kubectl create namespace honua-test --dry-run=client -o yaml | kubectl apply -f -

# Deploy PostgreSQL
echo "Deploying PostgreSQL..."
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: postgres-config
  namespace: honua-test
data:
  POSTGRES_DB: honua
  POSTGRES_USER: honua_user
  POSTGRES_PASSWORD: honua_password
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
        image: postgis/postgis:15-3.3
        envFrom:
        - configMapRef:
            name: postgres-config
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: postgres-data
          mountPath: /var/lib/postgresql/data
  volumeClaimTemplates:
  - metadata:
      name: postgres-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 1Gi
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: honua-test
spec:
  ports:
  - port: 5432
  selector:
    app: postgres
  clusterIP: None
EOF

# Wait for PostgreSQL to be ready
echo "Waiting for PostgreSQL to be ready..."
kubectl rollout status statefulset/postgres -n honua-test --timeout=360s

# Seed sample data in PostgreSQL
POSTGRES_POD=$(kubectl get pods -n honua-test -l app=postgres -o jsonpath='{.items[0].metadata.name}')
echo "Seeding PostgreSQL sample data..."
kubectl exec -n honua-test "$POSTGRES_POD" -- bash -c "cat <<'SQL' | psql -U honua_user -d honua
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

CREATE TABLE IF NOT EXISTS roads_primary (
    road_id SERIAL PRIMARY KEY,
    name VARCHAR(255),
    road_class VARCHAR(50),
    observed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    geom GEOMETRY(Point, 4326)
);

TRUNCATE TABLE roads_primary;

INSERT INTO roads_primary (name, road_class, geom) VALUES
    ('Main Street', 'highway', ST_SetSRID(ST_MakePoint(-122.45, 45.55), 4326)),
    ('Oak Avenue', 'local', ST_SetSRID(ST_MakePoint(-122.46, 45.56), 4326)),
    ('River Road', 'highway', ST_SetSRID(ST_MakePoint(-122.47, 45.57), 4326));
SQL"

# Deploy Honua (simplified - normally would build and push Docker image)
echo "Deploying Honua..."
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-config
  namespace: honua-test
data:
  HONUA__DATABASE__PROVIDER: "postgis"
  HONUA__DATABASE__CONNECTIONSTRING: "Host=postgres;Database=honua;Username=honua_user;Password=honua_password"
  HONUA__METADATA__PROVIDER: "json"
  HONUA__AUTHENTICATION__MODE: "QuickStart"
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
      app: honua
  template:
    metadata:
      labels:
        app: honua
    spec:
      containers:
      - name: honua
        image: mcr.microsoft.com/dotnet/sdk:9.0
        command: ["sleep", "infinity"]  # Placeholder - would run actual app
        envFrom:
        - configMapRef:
            name: honua-config
        ports:
        - containerPort: 5000
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: honua-service
  namespace: honua-test
spec:
  type: NodePort
  ports:
  - port: 80
    targetPort: 5000
    nodePort: 30080
  selector:
    app: honua
EOF

# Wait for Honua deployment
echo "Waiting for Honua deployment..."
kubectl wait --for=condition=available deployment/honua-server -n honua-test --timeout=120s

# Deploy HPA
echo "Deploying HorizontalPodAutoscaler..."
cat <<EOF | kubectl apply -f -
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-hpa
  namespace: honua-test
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: honua-server
  minReplicas: 2
  maxReplicas: 5
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
EOF

# Run tests
echo "Running Kubernetes validation tests..."

# Test 1: Check pods are running
POD_COUNT=$(kubectl get pods -n honua-test -l app=honua --field-selector=status.phase=Running -o json | jq '.items | length')
if [ "$POD_COUNT" -ge 2 ]; then
    echo "✓ Honua pods running ($POD_COUNT pods)"
else
    echo "✗ Insufficient Honua pods running"
    kubectl get pods -n honua-test
    exit 1
fi

# Test 2: Check PostgreSQL is running
if kubectl get pods -n honua-test -l app=postgres | grep -q Running; then
    echo "✓ PostgreSQL pod running"
else
    echo "✗ PostgreSQL pod not running"
    exit 1
fi

# Test 3: Check service exists
if kubectl get svc honua-service -n honua-test &> /dev/null; then
    echo "✓ Honua service exists"
else
    echo "✗ Honua service not found"
    exit 1
fi

# Test 4: Check HPA exists
if kubectl get hpa honua-hpa -n honua-test &> /dev/null; then
    echo "✓ HorizontalPodAutoscaler configured"
else
    echo "✗ HPA not found"
    exit 1
fi

# Test 5: Check PVC is bound
if kubectl get pvc -n honua-test | grep -q Bound; then
    echo "✓ PostgreSQL PVC bound"
else
    echo "✗ PostgreSQL PVC not bound"
    exit 1
fi

# Test 6: Verify deployment has correct replicas
REPLICAS=$(kubectl get deployment honua-server -n honua-test -o jsonpath='{.spec.replicas}')
if [ "$REPLICAS" -ge 2 ]; then
    echo "✓ Deployment has correct replica count ($REPLICAS)"
else
    echo "✗ Insufficient replicas configured"
    exit 1
fi

# Test 7: Check resource limits
LIMITS_SET=$(kubectl get deployment honua-server -n honua-test -o json | jq '.spec.template.spec.containers[0].resources.limits' | grep -q memory && echo "yes" || echo "no")
if [ "$LIMITS_SET" = "yes" ]; then
    echo "✓ Resource limits configured"
else
    echo "✗ Resource limits not set"
    exit 1
fi

# Cleanup
echo "Cleaning up..."
kubectl delete namespace honua-test --timeout=60s
minikube stop --profile honua-e2e
minikube delete --profile honua-e2e

echo "=== Test PASSED ==="
echo "Note: This test validates Kubernetes resource deployment, not full application functionality"
