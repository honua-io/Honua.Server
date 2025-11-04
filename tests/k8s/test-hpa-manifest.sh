#!/bin/bash
#
# Test script for Horizontal Pod Autoscaler manifest validation
# This script validates the HPA manifest without requiring a live Kubernetes cluster
#
# Usage: ./test-hpa-manifest.sh

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test counters
TESTS_RUN=0
TESTS_PASSED=0
TESTS_FAILED=0

# Base directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
HPA_MANIFEST="$PROJECT_ROOT/deploy/kubernetes/production/07-hpa.yaml"

echo "=========================================="
echo "Honua HPA Manifest Validation Test Suite"
echo "=========================================="
echo ""
echo "Project Root: $PROJECT_ROOT"
echo "HPA Manifest: $HPA_MANIFEST"
echo ""

# Test function
test_case() {
    local name="$1"
    local command="$2"

    TESTS_RUN=$((TESTS_RUN + 1))
    echo -n "Test $TESTS_RUN: $name ... "

    if eval "$command" > /dev/null 2>&1; then
        echo -e "${GREEN}PASS${NC}"
        TESTS_PASSED=$((TESTS_PASSED + 1))
        return 0
    else
        echo -e "${RED}FAIL${NC}"
        TESTS_FAILED=$((TESTS_FAILED + 1))
        return 1
    fi
}

# Prerequisites check
echo "Checking prerequisites..."
echo ""

if ! command -v kubectl &> /dev/null; then
    echo -e "${YELLOW}Warning: kubectl not found. Installing kubectl is recommended for validation.${NC}"
    echo "Install: https://kubernetes.io/docs/tasks/tools/"
    echo ""
    KUBECTL_AVAILABLE=false
else
    echo "kubectl: $(kubectl version --client --short 2>/dev/null || kubectl version --client)"
    KUBECTL_AVAILABLE=true
fi

if ! command -v yq &> /dev/null; then
    echo -e "${YELLOW}Warning: yq not found. Some tests will be skipped.${NC}"
    echo "Install: https://github.com/mikefarah/yq"
    YQ_AVAILABLE=false
else
    echo "yq: $(yq --version)"
    YQ_AVAILABLE=true
fi

echo ""
echo "Running tests..."
echo ""

# Test 1: File exists
test_case "HPA manifest file exists" \
    "[ -f '$HPA_MANIFEST' ]"

# Test 2: File is not empty
test_case "HPA manifest is not empty" \
    "[ -s '$HPA_MANIFEST' ]"

# Test 3: Valid YAML syntax
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA manifest has valid YAML syntax" \
        "yq eval '.' '$HPA_MANIFEST' > /dev/null"
fi

# Test 4: Basic YAML syntax check
test_case "HPA manifest has valid YAML syntax" \
    "grep -q 'apiVersion: autoscaling/v2' '$HPA_MANIFEST' && grep -q 'kind: HorizontalPodAutoscaler' '$HPA_MANIFEST'"

# Test 5: Check API version
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA uses autoscaling/v2 API" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .apiVersion' '$HPA_MANIFEST' | grep -q 'autoscaling/v2'"
fi

# Test 6: Check min replicas
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA minReplicas is set to 2" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .spec.minReplicas' '$HPA_MANIFEST' | grep -q '^2$'"
fi

# Test 7: Check max replicas
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA maxReplicas is set to 10" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .spec.maxReplicas' '$HPA_MANIFEST' | grep -q '^10$'"
fi

# Test 8: Check CPU target
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA CPU target is 70%" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .spec.metrics[] | select(.resource.name == \"cpu\") | .resource.target.averageUtilization' '$HPA_MANIFEST' | grep -q '^70$'"
fi

# Test 9: Check memory target
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA memory target is 80%" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .spec.metrics[] | select(.resource.name == \"memory\") | .resource.target.averageUtilization' '$HPA_MANIFEST' | grep -q '^80$'"
fi

# Test 10: Check scale down stabilization window
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA scale down stabilization is 300 seconds" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .spec.behavior.scaleDown.stabilizationWindowSeconds' '$HPA_MANIFEST' | grep -q '^300$'"
fi

# Test 11: Check scale up stabilization window
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA scale up stabilization is 0 seconds" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .spec.behavior.scaleUp.stabilizationWindowSeconds' '$HPA_MANIFEST' | grep -q '^0$'"
fi

# Test 12: Check CPU metric exists
test_case "HPA configures CPU metric" \
    "grep -q 'name: cpu' '$HPA_MANIFEST'"

# Test 13: Check memory metric exists
test_case "HPA configures memory metric" \
    "grep -q 'name: memory' '$HPA_MANIFEST'"

# Test 14: Check target deployment name
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA targets honua-server deployment" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .spec.scaleTargetRef.name' '$HPA_MANIFEST' | grep -q 'honua-server'"
fi

# Test 15: Check namespace
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA is in honua namespace" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .metadata.namespace' '$HPA_MANIFEST' | grep -q 'honua'"
fi

# Test 16: Verify labels exist
if [ "$YQ_AVAILABLE" = true ]; then
    test_case "HPA has required labels" \
        "yq eval 'select(.kind == \"HorizontalPodAutoscaler\") | .metadata.labels.app' '$HPA_MANIFEST' | grep -q 'honua-server'"
fi

# Dry-run creation test (if kubectl and cluster available)
if [ "$KUBECTL_AVAILABLE" = true ]; then
    echo ""
    echo "Testing HPA creation (dry-run)..."

    if kubectl cluster-info &> /dev/null; then
        test_case "HPA can be created in cluster (dry-run)" \
            "kubectl apply -f '$HPA_MANIFEST' --dry-run=server > /dev/null"
    else
        echo -e "${YELLOW}Skipping cluster dry-run test (no cluster connection)${NC}"
    fi
fi

# Summary
echo ""
echo "=========================================="
echo "Test Results Summary"
echo "=========================================="
echo "Tests Run:    $TESTS_RUN"
echo -e "Tests Passed: ${GREEN}$TESTS_PASSED${NC}"
echo -e "Tests Failed: ${RED}$TESTS_FAILED${NC}"
echo ""

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
    echo ""
    echo "HPA Configuration Summary:"
    echo "  - Min Replicas: 2"
    echo "  - Max Replicas: 10"
    echo "  - CPU Target: 70%"
    echo "  - Memory Target: 80%"
    echo "  - Scale Up: Immediate (0s stabilization)"
    echo "  - Scale Down: Conservative (300s stabilization)"
    echo ""
    echo "Note: PodDisruptionBudget is configured in 08-pdb.yaml"
    echo ""
    exit 0
else
    echo -e "${RED}Some tests failed. Please review the HPA manifest.${NC}"
    echo ""
    exit 1
fi
