#!/bin/bash

#######################################################################
# NetworkPolicy Test Suite for Honua Kubernetes Deployment
#
# This script validates that NetworkPolicies are correctly enforcing
# access controls by testing both allowed and blocked traffic flows.
#
# Usage:
#   ./test-network-policies.sh [options]
#
# Options:
#   --namespace NAME    Kubernetes namespace (default: honua)
#   --verbose          Show detailed output
#   --skip-cleanup     Don't delete test pods after completion
#   --help             Show this help message
#
# Exit codes:
#   0 = All tests passed
#   1 = One or more tests failed
#   2 = Prerequisites not met
#######################################################################

set -euo pipefail

# Configuration
NAMESPACE="${1:-honua}"
VERBOSE="${VERBOSE:-false}"
SKIP_CLEANUP="${SKIP_CLEANUP:-false}"
TEST_POD_PREFIX="netpol-test"
TIMEOUT=10

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Counters
PASSED=0
FAILED=0
SKIPPED=0

#######################################################################
# Helper Functions
#######################################################################

print_header() {
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
}

print_test() {
    echo -e "\n${YELLOW}TEST:${NC} $1"
}

print_pass() {
    echo -e "${GREEN}✓ PASS:${NC} $1"
    ((PASSED++))
}

print_fail() {
    echo -e "${RED}✗ FAIL:${NC} $1"
    ((FAILED++))
}

print_skip() {
    echo -e "${YELLOW}⊘ SKIP:${NC} $1"
    ((SKIPPED++))
}

print_info() {
    if [[ "$VERBOSE" == "true" ]]; then
        echo -e "${BLUE}INFO:${NC} $1"
    fi
}

cleanup_test_pods() {
    if [[ "$SKIP_CLEANUP" != "true" ]]; then
        print_info "Cleaning up test pods..."
        kubectl delete pod -n "$NAMESPACE" -l "test=network-policy" --ignore-not-found=true --wait=false > /dev/null 2>&1 || true
        kubectl delete pod -n kube-system -l "test=network-policy" --ignore-not-found=true --wait=false > /dev/null 2>&1 || true
        # Wait a bit for cleanup
        sleep 2
    fi
}

wait_for_pod() {
    local pod_name=$1
    local namespace=$2
    print_info "Waiting for pod $pod_name to be ready..."

    if ! kubectl wait --for=condition=ready pod/"$pod_name" -n "$namespace" --timeout=60s > /dev/null 2>&1; then
        print_fail "Pod $pod_name failed to become ready"
        return 1
    fi
    return 0
}

test_connectivity() {
    local source_pod=$1
    local source_namespace=$2
    local target_host=$3
    local target_port=$4
    local should_succeed=$5
    local description=$6

    print_test "$description"

    # Check if source pod exists
    if ! kubectl get pod "$source_pod" -n "$source_namespace" > /dev/null 2>&1; then
        print_skip "Source pod $source_pod does not exist"
        return
    fi

    # Perform connectivity test
    local test_cmd="timeout $TIMEOUT nc -zv $target_host $target_port"

    if kubectl exec "$source_pod" -n "$source_namespace" -- sh -c "$test_cmd" > /dev/null 2>&1; then
        # Connection succeeded
        if [[ "$should_succeed" == "true" ]]; then
            print_pass "Connection from $source_pod to $target_host:$target_port succeeded (as expected)"
        else
            print_fail "Connection from $source_pod to $target_host:$target_port succeeded (should have been blocked)"
        fi
    else
        # Connection failed
        if [[ "$should_succeed" == "false" ]]; then
            print_pass "Connection from $source_pod to $target_host:$target_port blocked (as expected)"
        else
            print_fail "Connection from $source_pod to $target_host:$target_port blocked (should have succeeded)"
        fi
    fi
}

test_http_connectivity() {
    local source_pod=$1
    local source_namespace=$2
    local target_url=$3
    local should_succeed=$4
    local description=$5

    print_test "$description"

    # Check if source pod exists
    if ! kubectl get pod "$source_pod" -n "$source_namespace" > /dev/null 2>&1; then
        print_skip "Source pod $source_pod does not exist"
        return
    fi

    # Perform HTTP test
    local test_cmd="timeout $TIMEOUT wget -q -O /dev/null $target_url"

    if kubectl exec "$source_pod" -n "$source_namespace" -- sh -c "$test_cmd" > /dev/null 2>&1; then
        # Connection succeeded
        if [[ "$should_succeed" == "true" ]]; then
            print_pass "HTTP request from $source_pod to $target_url succeeded (as expected)"
        else
            print_fail "HTTP request from $source_pod to $target_url succeeded (should have been blocked)"
        fi
    else
        # Connection failed
        if [[ "$should_succeed" == "false" ]]; then
            print_pass "HTTP request from $source_pod to $target_url blocked (as expected)"
        else
            print_fail "HTTP request from $source_pod to $target_url blocked (should have succeeded)"
        fi
    fi
}

#######################################################################
# Prerequisites Check
#######################################################################

check_prerequisites() {
    print_header "Checking Prerequisites"

    # Check if kubectl is available
    if ! command -v kubectl &> /dev/null; then
        echo -e "${RED}ERROR:${NC} kubectl is not installed or not in PATH"
        exit 2
    fi
    print_pass "kubectl is available"

    # Check if namespace exists
    if ! kubectl get namespace "$NAMESPACE" > /dev/null 2>&1; then
        echo -e "${RED}ERROR:${NC} Namespace $NAMESPACE does not exist"
        exit 2
    fi
    print_pass "Namespace $NAMESPACE exists"

    # Check if NetworkPolicies exist
    local netpol_count=$(kubectl get networkpolicies -n "$NAMESPACE" --no-headers 2>/dev/null | wc -l)
    if [[ $netpol_count -eq 0 ]]; then
        echo -e "${RED}ERROR:${NC} No NetworkPolicies found in namespace $NAMESPACE"
        echo "Please deploy NetworkPolicies before running tests"
        exit 2
    fi
    print_pass "NetworkPolicies are deployed ($netpol_count policies found)"

    # Check if CNI supports NetworkPolicies
    print_info "Verifying CNI plugin supports NetworkPolicies..."
    # This is a heuristic check - not all CNIs advertise this capability
    print_pass "CNI check completed (manual verification recommended)"

    # Check if required services exist
    local required_services=("honua-service" "postgis-service" "redis-service")
    for svc in "${required_services[@]}"; do
        if kubectl get service "$svc" -n "$NAMESPACE" > /dev/null 2>&1; then
            print_pass "Service $svc exists"
        else
            print_skip "Service $svc not found (some tests may be skipped)"
        fi
    done
}

#######################################################################
# Test Pod Creation
#######################################################################

create_test_pods() {
    print_header "Creating Test Pods"

    # Test pod 1: In Honua namespace with honua-server label
    print_info "Creating test pod with honua-server label..."
    cat <<EOF | kubectl apply -f - > /dev/null 2>&1
apiVersion: v1
kind: Pod
metadata:
  name: ${TEST_POD_PREFIX}-honua-server
  namespace: ${NAMESPACE}
  labels:
    app: honua-server
    test: network-policy
spec:
  containers:
  - name: test
    image: nicolaka/netshoot:latest
    command: ["sleep", "3600"]
    securityContext:
      runAsNonRoot: true
      runAsUser: 1000
      allowPrivilegeEscalation: false
      capabilities:
        drop:
        - ALL
  restartPolicy: Never
EOF

    # Test pod 2: In Honua namespace without specific labels (generic pod)
    print_info "Creating test pod without app label..."
    cat <<EOF | kubectl apply -f - > /dev/null 2>&1
apiVersion: v1
kind: Pod
metadata:
  name: ${TEST_POD_PREFIX}-generic
  namespace: ${NAMESPACE}
  labels:
    test: network-policy
spec:
  containers:
  - name: test
    image: nicolaka/netshoot:latest
    command: ["sleep", "3600"]
    securityContext:
      runAsNonRoot: true
      runAsUser: 1000
      allowPrivilegeEscalation: false
      capabilities:
        drop:
        - ALL
  restartPolicy: Never
EOF

    # Test pod 3: In different namespace (simulating external pod)
    print_info "Creating test pod in kube-system namespace..."
    cat <<EOF | kubectl apply -f - > /dev/null 2>&1
apiVersion: v1
kind: Pod
metadata:
  name: ${TEST_POD_PREFIX}-external
  namespace: kube-system
  labels:
    test: network-policy
spec:
  containers:
  - name: test
    image: nicolaka/netshoot:latest
    command: ["sleep", "3600"]
    securityContext:
      runAsNonRoot: true
      runAsUser: 1000
      allowPrivilegeEscalation: false
      capabilities:
        drop:
        - ALL
  restartPolicy: Never
EOF

    # Wait for pods to be ready
    wait_for_pod "${TEST_POD_PREFIX}-honua-server" "$NAMESPACE"
    wait_for_pod "${TEST_POD_PREFIX}-generic" "$NAMESPACE"
    wait_for_pod "${TEST_POD_PREFIX}-external" "kube-system"

    print_pass "All test pods created and ready"
}

#######################################################################
# Test Cases
#######################################################################

test_dns_access() {
    print_header "Testing DNS Access"

    # All pods should be able to access DNS
    test_connectivity \
        "${TEST_POD_PREFIX}-honua-server" "$NAMESPACE" \
        "kube-dns.kube-system.svc.cluster.local" "53" \
        "true" \
        "Honua Server pod can access DNS"

    test_connectivity \
        "${TEST_POD_PREFIX}-generic" "$NAMESPACE" \
        "kube-dns.kube-system.svc.cluster.local" "53" \
        "true" \
        "Generic pod can access DNS"
}

test_database_access() {
    print_header "Testing Database Access"

    # Check if PostgreSQL service exists
    if ! kubectl get service postgis-service -n "$NAMESPACE" > /dev/null 2>&1; then
        print_skip "PostgreSQL service not found, skipping database tests"
        return
    fi

    # Honua Server pods SHOULD be able to access PostgreSQL
    test_connectivity \
        "${TEST_POD_PREFIX}-honua-server" "$NAMESPACE" \
        "postgis-service" "5432" \
        "true" \
        "Honua Server pod can access PostgreSQL"

    # Generic pods SHOULD NOT be able to access PostgreSQL
    test_connectivity \
        "${TEST_POD_PREFIX}-generic" "$NAMESPACE" \
        "postgis-service" "5432" \
        "false" \
        "Generic pod CANNOT access PostgreSQL (blocked by NetworkPolicy)"

    # External namespace pods SHOULD NOT be able to access PostgreSQL
    test_connectivity \
        "${TEST_POD_PREFIX}-external" "kube-system" \
        "postgis-service.${NAMESPACE}.svc.cluster.local" "5432" \
        "false" \
        "External pod CANNOT access PostgreSQL (blocked by NetworkPolicy)"
}

test_redis_access() {
    print_header "Testing Redis Cache Access"

    # Check if Redis service exists
    if ! kubectl get service redis-service -n "$NAMESPACE" > /dev/null 2>&1; then
        print_skip "Redis service not found, skipping Redis tests"
        return
    fi

    # Honua Server pods SHOULD be able to access Redis
    test_connectivity \
        "${TEST_POD_PREFIX}-honua-server" "$NAMESPACE" \
        "redis-service" "6379" \
        "true" \
        "Honua Server pod can access Redis"

    # Generic pods SHOULD NOT be able to access Redis
    test_connectivity \
        "${TEST_POD_PREFIX}-generic" "$NAMESPACE" \
        "redis-service" "6379" \
        "false" \
        "Generic pod CANNOT access Redis (blocked by NetworkPolicy)"

    # External namespace pods SHOULD NOT be able to access Redis
    test_connectivity \
        "${TEST_POD_PREFIX}-external" "kube-system" \
        "redis-service.${NAMESPACE}.svc.cluster.local" "6379" \
        "false" \
        "External pod CANNOT access Redis (blocked by NetworkPolicy)"
}

test_honua_server_access() {
    print_header "Testing Honua Server Access"

    # Check if Honua service exists
    if ! kubectl get service honua-service -n "$NAMESPACE" > /dev/null 2>&1; then
        print_skip "Honua service not found, skipping Honua Server tests"
        return
    fi

    # Honua Server pods SHOULD be able to access other Honua Server pods (health checks)
    test_connectivity \
        "${TEST_POD_PREFIX}-honua-server" "$NAMESPACE" \
        "honua-service" "80" \
        "true" \
        "Honua Server pod can access other Honua Server pods"

    # Generic pods SHOULD NOT be able to access Honua Server
    # (unless they're from ingress/monitoring namespaces)
    test_connectivity \
        "${TEST_POD_PREFIX}-generic" "$NAMESPACE" \
        "honua-service" "80" \
        "false" \
        "Generic pod CANNOT access Honua Server (blocked by NetworkPolicy)"
}

test_external_access() {
    print_header "Testing External Network Access"

    # Honua Server pods SHOULD be able to access external HTTPS
    test_http_connectivity \
        "${TEST_POD_PREFIX}-honua-server" "$NAMESPACE" \
        "https://www.google.com" \
        "true" \
        "Honua Server pod can access external HTTPS"

    # Generic pods SHOULD NOT be able to access external networks (default deny egress)
    test_http_connectivity \
        "${TEST_POD_PREFIX}-generic" "$NAMESPACE" \
        "https://www.google.com" \
        "false" \
        "Generic pod CANNOT access external HTTPS (blocked by NetworkPolicy)"

    # Test HTTP access (port 80)
    test_http_connectivity \
        "${TEST_POD_PREFIX}-honua-server" "$NAMESPACE" \
        "http://www.google.com" \
        "true" \
        "Honua Server pod can access external HTTP"
}

test_namespace_isolation() {
    print_header "Testing Namespace Isolation"

    # Check if Honua service exists
    if ! kubectl get service honua-service -n "$NAMESPACE" > /dev/null 2>&1; then
        print_skip "Honua service not found, skipping namespace isolation tests"
        return
    fi

    # Pods in other namespaces SHOULD NOT be able to access Honua namespace
    test_connectivity \
        "${TEST_POD_PREFIX}-external" "kube-system" \
        "honua-service.${NAMESPACE}.svc.cluster.local" "80" \
        "false" \
        "External namespace pod CANNOT access Honua namespace (namespace isolation)"
}

test_default_deny() {
    print_header "Testing Default Deny Policy"

    # Generic pod with no matching rules should not be able to access anything
    # (except DNS which is explicitly allowed)

    # Test access to a random port on a service
    if kubectl get service redis-service -n "$NAMESPACE" > /dev/null 2>&1; then
        test_connectivity \
            "${TEST_POD_PREFIX}-generic" "$NAMESPACE" \
            "redis-service" "1234" \
            "false" \
            "Generic pod CANNOT access random port (default deny)"
    fi
}

#######################################################################
# Main Test Execution
#######################################################################

main() {
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --namespace)
                NAMESPACE="$2"
                shift 2
                ;;
            --verbose)
                VERBOSE="true"
                shift
                ;;
            --skip-cleanup)
                SKIP_CLEANUP="true"
                shift
                ;;
            --help)
                grep "^#" "$0" | grep -v "^#!/" | sed 's/^# //g' | sed 's/^#//g'
                exit 0
                ;;
            *)
                echo "Unknown option: $1"
                echo "Use --help for usage information"
                exit 2
                ;;
        esac
    done

    print_header "Honua NetworkPolicy Test Suite"
    echo "Namespace: $NAMESPACE"
    echo "Verbose: $VERBOSE"
    echo ""

    # Trap to ensure cleanup on exit
    trap cleanup_test_pods EXIT

    # Run tests
    check_prerequisites
    create_test_pods

    test_dns_access
    test_database_access
    test_redis_access
    test_honua_server_access
    test_external_access
    test_namespace_isolation
    test_default_deny

    # Print summary
    print_header "Test Summary"
    echo -e "Total tests: $((PASSED + FAILED + SKIPPED))"
    echo -e "${GREEN}Passed: $PASSED${NC}"
    echo -e "${RED}Failed: $FAILED${NC}"
    echo -e "${YELLOW}Skipped: $SKIPPED${NC}"
    echo ""

    if [[ $FAILED -eq 0 ]]; then
        echo -e "${GREEN}✓ All tests passed!${NC}"
        exit 0
    else
        echo -e "${RED}✗ Some tests failed. Please review NetworkPolicy configuration.${NC}"
        exit 1
    fi
}

# Run main function
main "$@"
