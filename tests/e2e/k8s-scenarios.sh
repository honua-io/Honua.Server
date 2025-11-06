#!/usr/bin/env bash
#
# Kubernetes E2E Test Suite for Honua
# Tests K8s deployment scenarios using kind (Kubernetes in Docker)
#

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test results
TESTS_PASSED=0
TESTS_FAILED=0
TESTS_TOTAL=0

# Kind cluster name
CLUSTER_NAME="honua-test-cluster"

# Cleanup function
cleanup() {
    echo -e "${YELLOW}Cleaning up K8s resources...${NC}"
    kind delete cluster --name "$CLUSTER_NAME" 2>/dev/null || true
    rm -rf /tmp/honua-k8s-test 2>/dev/null || true
}

trap cleanup EXIT

# Test helper functions
test_start() {
    local test_name="$1"
    TESTS_TOTAL=$((TESTS_TOTAL + 1))
    echo -e "\n${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}Test $TESTS_TOTAL: $test_name${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

test_pass() {
    TESTS_PASSED=$((TESTS_PASSED + 1))
    echo -e "${GREEN}✓ PASSED${NC}"
}

test_fail() {
    local reason="$1"
    TESTS_FAILED=$((TESTS_FAILED + 1))
    echo -e "${RED}✗ FAILED: $reason${NC}"
}

# Create Kind cluster
create_cluster() {
    echo "Creating Kind cluster..."
    cat > /tmp/kind-config.yaml <<EOF
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
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
EOF

    if ! kind create cluster --name "$CLUSTER_NAME" --config /tmp/kind-config.yaml; then
        echo -e "${RED}Failed to create Kind cluster${NC}"
        return 1
    fi

    echo "Waiting for cluster to be ready..."
    kubectl wait --for=condition=Ready nodes --all --timeout=120s

    return 0
}

# Test 1: Deploy PostGIS to K8s
test_postgis_deployment() {
    test_start "Deploy PostGIS StatefulSet to Kubernetes"

    # Create namespace
    kubectl create namespace honua-test

    # Create PostGIS StatefulSet
    cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: Service
metadata:
  name: postgis
  namespace: honua-test
spec:
  ports:
  - port: 5432
    name: postgres
  clusterIP: None
  selector:
    app: postgis
---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgis
  namespace: honua-test
spec:
  serviceName: postgis
  replicas: 1
  selector:
    matchLabels:
      app: postgis
  template:
    metadata:
      labels:
        app: postgis
    spec:
      containers:
      - name: postgis
        image: postgis/postgis:latest
        ports:
        - containerPort: 5432
          name: postgres
        env:
        - name: POSTGRES_USER
          value: honua
        - name: POSTGRES_PASSWORD
          value: honua123
        - name: POSTGRES_DB
          value: honuadb
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        volumeMounts:
        - name: postgis-data
          mountPath: /var/lib/postgresql/data
  volumeClaimTemplates:
  - metadata:
      name: postgis-data
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 1Gi
EOF

    # Wait for StatefulSet to be ready
    echo "Waiting for PostGIS to be ready..."
    if kubectl wait --for=condition=Ready pod/postgis-0 -n honua-test --timeout=120s > /dev/null 2>&1; then
        # Test connection
        if kubectl exec -n honua-test postgis-0 -- psql -U honua -d honuadb -c "SELECT version();" > /dev/null 2>&1; then
            test_pass
        else
            test_fail "Could not connect to PostGIS"
        fi
    else
        test_fail "PostGIS pod did not become ready"
    fi

    kubectl delete namespace honua-test
}

# Test 2: Deploy Honua Server with ConfigMap
test_honua_server_deployment() {
    test_start "Deploy Honua Server with ConfigMap and Secrets"

    kubectl create namespace honua-app

    # Create ConfigMap for metadata
    cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-config
  namespace: honua-app
data:
  metadata.json: |
    {
      "title": "Honua OGC API",
      "description": "K8s deployed Honua instance",
      "collections": []
    }
EOF

    # Create Secret for database credentials
    kubectl create secret generic honua-db-secret \
      --from-literal=username=honua \
      --from-literal=password=honua123 \
      -n honua-app

    # Create Deployment
    cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua-app
spec:
  replicas: 2
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
    spec:
      containers:
      - name: honua
        image: nginx:alpine  # Placeholder for actual Honua image
        ports:
        - containerPort: 5000
          name: http
        env:
        - name: DB_USER
          valueFrom:
            secretKeyRef:
              name: honua-db-secret
              key: username
        - name: DB_PASSWORD
          valueFrom:
            secretKeyRef:
              name: honua-db-secret
              key: password
        volumeMounts:
        - name: config
          mountPath: /app/config
          readOnly: true
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
      volumes:
      - name: config
        configMap:
          name: honua-config
---
apiVersion: v1
kind: Service
metadata:
  name: honua-server
  namespace: honua-app
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 5000
    protocol: TCP
    name: http
  selector:
    app: honua-server
EOF

    # Wait for deployment
    if kubectl wait --for=condition=Available deployment/honua-server -n honua-app --timeout=120s > /dev/null 2>&1; then
        # Check if replicas are running
        ready_replicas=$(kubectl get deployment honua-server -n honua-app -o jsonpath='{.status.readyReplicas}')
        if [ "$ready_replicas" = "2" ]; then
            test_pass
        else
            test_fail "Not all replicas are ready (expected 2, got $ready_replicas)"
        fi
    else
        test_fail "Deployment did not become available"
    fi

    kubectl delete namespace honua-app
}

# Test 3: Horizontal Pod Autoscaling
test_hpa() {
    test_start "Horizontal Pod Autoscaler configuration"

    kubectl create namespace honua-hpa

    # Create deployment with resource requests (required for HPA)
    cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-api
  namespace: honua-hpa
spec:
  replicas: 1
  selector:
    matchLabels:
      app: honua-api
  template:
    metadata:
      labels:
        app: honua-api
    spec:
      containers:
      - name: honua
        image: nginx:alpine
        ports:
        - containerPort: 80
        resources:
          requests:
            cpu: 100m
            memory: 128Mi
          limits:
            cpu: 500m
            memory: 512Mi
EOF

    # Create HPA
    kubectl autoscale deployment honua-api -n honua-hpa --cpu-percent=50 --min=1 --max=10

    # Verify HPA is created
    if kubectl get hpa honua-api -n honua-hpa > /dev/null 2>&1; then
        test_pass
    else
        test_fail "HPA was not created"
    fi

    kubectl delete namespace honua-hpa
}

# Test 4: Ingress Configuration
test_ingress() {
    test_start "Ingress resource for external access"

    kubectl create namespace honua-ingress

    # Install nginx ingress controller
    echo "Installing NGINX Ingress Controller..."
    kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml > /dev/null 2>&1

    # Wait for ingress controller
    kubectl wait --namespace ingress-nginx \
      --for=condition=ready pod \
      --selector=app.kubernetes.io/component=controller \
      --timeout=120s > /dev/null 2>&1 || true

    # Create test service
    kubectl create deployment web --image=nginx:alpine -n honua-ingress
    kubectl expose deployment web --port=80 -n honua-ingress

    # Create Ingress
    cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: honua-ingress
  namespace: honua-ingress
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
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
            name: web
            port:
              number: 80
EOF

    # Verify Ingress is created
    if kubectl get ingress honua-ingress -n honua-ingress > /dev/null 2>&1; then
        test_pass
    else
        test_fail "Ingress was not created"
    fi

    kubectl delete namespace honua-ingress
}

# Test 5: PersistentVolumeClaim for tiles
test_persistent_storage() {
    test_start "PersistentVolumeClaim for tile cache"

    kubectl create namespace honua-storage

    # Create PVC
    cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: tile-cache
  namespace: honua-storage
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 5Gi
EOF

    # Create pod using the PVC
    cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: Pod
metadata:
  name: tile-writer
  namespace: honua-storage
spec:
  containers:
  - name: writer
    image: busybox
    command: ["sh", "-c", "echo 'tile data' > /cache/test.mvt && sleep 3600"]
    volumeMounts:
    - name: cache
      mountPath: /cache
  volumes:
  - name: cache
    persistentVolumeClaim:
      claimName: tile-cache
EOF

    # Wait for pod
    if kubectl wait --for=condition=Ready pod/tile-writer -n honua-storage --timeout=120s > /dev/null 2>&1; then
        # Verify file was written
        if kubectl exec -n honua-storage tile-writer -- cat /cache/test.mvt | grep -q "tile data"; then
            test_pass
        else
            test_fail "Could not verify data in PVC"
        fi
    else
        test_fail "Pod did not become ready"
    fi

    kubectl delete namespace honua-storage
}

# Test 6: Network Policy
test_network_policy() {
    test_start "Network Policy for security"

    kubectl create namespace honua-netpol

    # Create two deployments
    kubectl create deployment frontend --image=nginx:alpine -n honua-netpol
    kubectl create deployment backend --image=nginx:alpine -n honua-netpol
    kubectl expose deployment backend --port=80 -n honua-netpol

    # Create Network Policy allowing only frontend->backend
    cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: backend-policy
  namespace: honua-netpol
spec:
  podSelector:
    matchLabels:
      app: backend
  policyTypes:
  - Ingress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: frontend
    ports:
    - protocol: TCP
      port: 80
EOF

    # Verify Network Policy is created
    if kubectl get networkpolicy backend-policy -n honua-netpol > /dev/null 2>&1; then
        test_pass
    else
        test_fail "Network Policy was not created"
    fi

    kubectl delete namespace honua-netpol
}

# Main execution
main() {
    echo -e "${GREEN}╔══════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}║  Honua Kubernetes E2E Test Suite                                ║${NC}"
    echo -e "${GREEN}╚══════════════════════════════════════════════════════════════════╝${NC}\n"

    # Check dependencies
    if ! command -v kind &> /dev/null; then
        echo -e "${RED}ERROR: kind is not installed${NC}"
        echo -e "${YELLOW}Install with: GO111MODULE=on go install sigs.k8s.io/kind@latest${NC}"
        exit 1
    fi

    if ! command -v kubectl &> /dev/null; then
        echo -e "${RED}ERROR: kubectl is not installed${NC}"
        exit 1
    fi

    # Create cluster
    if ! create_cluster; then
        echo -e "${RED}Failed to create cluster${NC}"
        exit 1
    fi

    # Run all tests
    test_postgis_deployment
    test_honua_server_deployment
    test_hpa
    test_ingress
    test_persistent_storage
    test_network_policy

    # Print results
    echo -e "\n${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}Test Results${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "Total Tests: $TESTS_TOTAL"
    echo -e "${GREEN}Passed: $TESTS_PASSED${NC}"
    echo -e "${RED}Failed: $TESTS_FAILED${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

    if [ "$TESTS_FAILED" -eq "0" ]; then
        echo -e "${GREEN}✓ All tests passed!${NC}\n"
        exit 0
    else
        echo -e "${RED}✗ Some tests failed${NC}\n"
        exit 1
    fi
}

main "$@"
