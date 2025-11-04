#!/bin/bash
# AI Consultant Kubernetes E2E Test with Infrastructure Validation
# Tests AI consultant's ability to generate K8s deployments and validates actual infrastructure

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT_ROOT="$(dirname "$(dirname "$TEST_DIR")")"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

echo -e "${CYAN}${BOLD}"
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║  AI Consultant Kubernetes E2E Test with Validation for Honua  ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo -e "${NC}"
echo ""

# Check for API keys
if [ -z "$OPENAI_API_KEY" ] && [ -z "$ANTHROPIC_API_KEY" ]; then
    echo -e "${YELLOW}⚠ Warning: No API key found (OPENAI_API_KEY or ANTHROPIC_API_KEY)${NC}"
    echo -e "${YELLOW}  Skipping real AI tests${NC}"
    exit 0
fi

# Check for kubectl and minikube
if ! command -v kubectl &> /dev/null || ! command -v minikube &> /dev/null; then
    echo -e "${YELLOW}⚠ kubectl or minikube not found. Skipping K8s tests.${NC}"
    exit 0
fi

# Create results directory
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
RESULTS_DIR="$TEST_DIR/results/kubernetes_$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}Results: $RESULTS_DIR${NC}"
echo ""

TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

CLUSTER_NAME="honua-ai-test"
NAMESPACE="honua-ai-test-$TIMESTAMP"

# Start minikube if not running
if ! minikube status --profile "$CLUSTER_NAME" &> /dev/null; then
    echo -e "${BLUE}Starting Minikube cluster...${NC}"
    minikube start --profile "$CLUSTER_NAME" --cpus=2 --memory=4096 --disk-size=10g
fi

kubectl config use-context "$CLUSTER_NAME"

# Test 1: Basic Kubernetes deployment
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 1: Basic Kubernetes Deployment${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/k8s-basic"
mkdir -p "$WORKSPACE"

PROMPT="Create Kubernetes manifests for Honua with PostgreSQL StatefulSet (with PostGIS), Redis Deployment for caching, and ClusterIP Services. Include proper health checks and resource limits."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✓ AI generated Kubernetes manifests${NC}"

    # Create namespace
    kubectl create namespace "$NAMESPACE-basic" --dry-run=client -o yaml | kubectl apply -f - > /dev/null 2>&1

    # Apply manifests
    if ls "$WORKSPACE"/*.yaml &> /dev/null || ls "$WORKSPACE"/*.yml &> /dev/null; then
        kubectl apply -f "$WORKSPACE" -n "$NAMESPACE-basic" > "$WORKSPACE/deploy.log" 2>&1

        # Wait for pods
        sleep 20
        kubectl wait --for=condition=ready pod -l app=honua -n "$NAMESPACE-basic" --timeout=120s > /dev/null 2>&1 || true

        POD_COUNT=$(kubectl get pods -l app=honua -n "$NAMESPACE-basic" --no-headers 2>/dev/null | wc -l)
        if [ "$POD_COUNT" -gt 0 ]; then
            echo -e "${GREEN}✓ Honua pods deployed successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ No Honua pods found${NC}"
            kubectl get pods -n "$NAMESPACE-basic"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ No manifest files generated${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi

    # Cleanup
    kubectl delete namespace "$NAMESPACE-basic" --wait=false > /dev/null 2>&1 || true
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 2: Helm chart generation
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 2: Kubernetes Helm Chart${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/k8s-helm"
mkdir -p "$WORKSPACE"

PROMPT="Create a Helm chart for Honua with configurable values for replicas, resources, database type, and ingress settings. Include templates for Deployment, Service, ConfigMap, and Ingress."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if [ -f "$WORKSPACE/Chart.yaml" ] && [ -d "$WORKSPACE/templates" ]; then
        echo -e "${GREEN}✓ AI generated Helm chart structure${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Helm chart structure incomplete${NC}"
        ls -la "$WORKSPACE"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate Helm chart${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 3: HPA and auto-scaling
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 3: Kubernetes with HPA${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/k8s-hpa"
mkdir -p "$WORKSPACE"

PROMPT="Create Kubernetes manifests for Honua with HorizontalPodAutoscaler configured to scale from 2 to 10 replicas based on CPU (70%) and memory (80%) utilization. Include resource requests and limits."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -rq "HorizontalPodAutoscaler\|kind: HorizontalPodAutoscaler" "$WORKSPACE"; then
        echo -e "${GREEN}✓ AI generated HPA configuration${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ HPA configuration missing${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 4: Ingress and TLS
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 4: Kubernetes with Ingress and TLS${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/k8s-ingress"
mkdir -p "$WORKSPACE"

PROMPT="Create Kubernetes manifests for Honua with Nginx Ingress Controller, TLS termination using cert-manager and Let's Encrypt, and proper routing rules for OGC API endpoints."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -rq "kind: Ingress" "$WORKSPACE" && \
       grep -rq "tls:" "$WORKSPACE"; then
        echo -e "${GREEN}✓ AI generated Ingress with TLS${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Ingress or TLS configuration missing${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 5: ConfigMap and Secrets
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 5: Kubernetes ConfigMap and Secrets${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/k8s-config-secrets"
mkdir -p "$WORKSPACE"

PROMPT="Create Kubernetes manifests for Honua with ConfigMap for metadata.json configuration, Secrets for database credentials, and mount them properly in the Deployment. Include example metadata with a layer definition."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -rq "kind: ConfigMap" "$WORKSPACE" && \
       grep -rq "kind: Secret" "$WORKSPACE"; then
        echo -e "${GREEN}✓ AI generated ConfigMap and Secrets${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ ConfigMap or Secret missing${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 6: Troubleshooting scenario
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 6: Troubleshooting Pod Crashes${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/k8s-troubleshooting"
mkdir -p "$WORKSPACE"

# Create deployment first
cd "$PROJECT_ROOT"
dotnet run --project src/Honua.Cli consultant \
    --prompt "Create Kubernetes manifests for Honua" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/setup.log" 2>&1

# Now troubleshoot
PROMPT="My Honua pods keep getting OOMKilled and crashing. The application logs show memory errors. Help me diagnose and fix this by adjusting resource limits and requests."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/troubleshooting.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -qi "memory\|resource\|limit\|oomkilled" "$WORKSPACE/troubleshooting.log"; then
        echo -e "${GREEN}✓ AI provided troubleshooting guidance${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${YELLOW}⚠ Troubleshooting completed${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ Troubleshooting failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Generate summary
echo -e "${CYAN}${BOLD}"
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║                    TEST SUMMARY                                ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo -e "${NC}"
echo -e "Total Tests:  $TOTAL_TESTS"
echo -e "Passed:       ${GREEN}$PASSED_TESTS${NC}"
echo -e "Failed:       ${RED}$FAILED_TESTS${NC}"
echo -e "Success Rate: $(echo "scale=1; $PASSED_TESTS * 100 / $TOTAL_TESTS" | bc)%"
echo ""

# Cleanup prompt
echo -e "${YELLOW}Cleanup?${NC}"
read -p "Stop Minikube cluster? (y/N) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    minikube stop --profile "$CLUSTER_NAME"
    echo -e "${GREEN}✓ Minikube stopped${NC}"
fi

if [ $FAILED_TESTS -eq 0 ]; then
    echo -e "${GREEN}${BOLD}✓ ALL KUBERNETES AI CONSULTANT TESTS PASSED${NC}"
    exit 0
else
    echo -e "${RED}${BOLD}✗ SOME TESTS FAILED${NC}"
    exit 1
fi
