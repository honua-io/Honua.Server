#!/bin/bash
#
# Kubernetes Security Validation Script for Honua
# This script validates security configurations across all deployed resources
#
# Usage: ./validate-security.sh [namespace]
# Default namespace: honua

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
NAMESPACE="${1:-honua}"
ERRORS=0
WARNINGS=0
PASSED=0

# Helper functions
print_header() {
    echo -e "\n${BLUE}===================================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}===================================================${NC}\n"
}

print_section() {
    echo -e "\n${YELLOW}>>> $1${NC}\n"
}

print_pass() {
    echo -e "${GREEN}✓${NC} $1"
    ((PASSED++))
}

print_fail() {
    echo -e "${RED}✗${NC} $1"
    ((ERRORS++))
}

print_warn() {
    echo -e "${YELLOW}⚠${NC} $1"
    ((WARNINGS++))
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

# Check if kubectl is available
if ! command -v kubectl &> /dev/null; then
    echo -e "${RED}ERROR: kubectl command not found${NC}"
    exit 1
fi

# Check if namespace exists
if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
    echo -e "${RED}ERROR: Namespace '$NAMESPACE' does not exist${NC}"
    exit 1
fi

print_header "Kubernetes Security Validation for Honua"
print_info "Namespace: $NAMESPACE"
print_info "Date: $(date)"
print_info "Kubernetes Version: $(kubectl version --short 2>/dev/null | grep Server || echo 'Unable to determine')"

# 1. Check Pod Security Standards
print_section "1. Pod Security Standards Configuration"

PSS_ENFORCE=$(kubectl get namespace "$NAMESPACE" -o jsonpath='{.metadata.labels.pod-security\.kubernetes\.io/enforce}' 2>/dev/null || echo "")
PSS_AUDIT=$(kubectl get namespace "$NAMESPACE" -o jsonpath='{.metadata.labels.pod-security\.kubernetes\.io/audit}' 2>/dev/null || echo "")
PSS_WARN=$(kubectl get namespace "$NAMESPACE" -o jsonpath='{.metadata.labels.pod-security\.kubernetes\.io/warn}' 2>/dev/null || echo "")

if [ "$PSS_ENFORCE" == "restricted" ]; then
    print_pass "Pod Security Standard enforce mode: restricted"
else
    print_fail "Pod Security Standard enforce mode: $PSS_ENFORCE (expected: restricted)"
fi

if [ "$PSS_AUDIT" == "restricted" ]; then
    print_pass "Pod Security Standard audit mode: restricted"
else
    print_warn "Pod Security Standard audit mode: $PSS_AUDIT (recommended: restricted)"
fi

if [ "$PSS_WARN" == "restricted" ]; then
    print_pass "Pod Security Standard warn mode: restricted"
else
    print_warn "Pod Security Standard warn mode: $PSS_WARN (recommended: restricted)"
fi

# 2. Check SecurityContext for all pods
print_section "2. Pod SecurityContext Configuration"

PODS=$(kubectl get pods -n "$NAMESPACE" -o jsonpath='{.items[*].metadata.name}')

if [ -z "$PODS" ]; then
    print_warn "No pods found in namespace $NAMESPACE"
else
    for POD in $PODS; do
        print_info "Checking pod: $POD"

        # Check runAsNonRoot at pod level
        RUN_AS_NON_ROOT=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath='{.spec.securityContext.runAsNonRoot}' 2>/dev/null || echo "")
        if [ "$RUN_AS_NON_ROOT" == "true" ]; then
            print_pass "  Pod runAsNonRoot: true"
        else
            print_fail "  Pod runAsNonRoot: $RUN_AS_NON_ROOT (expected: true)"
        fi

        # Check runAsUser at pod level
        RUN_AS_USER=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath='{.spec.securityContext.runAsUser}' 2>/dev/null || echo "")
        if [ -n "$RUN_AS_USER" ] && [ "$RUN_AS_USER" != "0" ]; then
            print_pass "  Pod runAsUser: $RUN_AS_USER (non-root)"
        else
            print_warn "  Pod runAsUser: $RUN_AS_USER (should be non-zero)"
        fi

        # Check seccomp profile at pod level
        SECCOMP=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath='{.spec.securityContext.seccompProfile.type}' 2>/dev/null || echo "")
        if [ "$SECCOMP" == "RuntimeDefault" ] || [ "$SECCOMP" == "Localhost" ]; then
            print_pass "  Pod seccompProfile: $SECCOMP"
        else
            print_fail "  Pod seccompProfile: $SECCOMP (expected: RuntimeDefault)"
        fi
    done
fi

# 3. Check Container SecurityContext
print_section "3. Container SecurityContext Configuration"

for POD in $PODS; do
    CONTAINERS=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath='{.spec.containers[*].name}')

    for CONTAINER in $CONTAINERS; do
        print_info "Checking container: $CONTAINER in pod: $POD"

        # Get container index
        CONTAINER_INDEX=$(kubectl get pod -n "$NAMESPACE" "$POD" -o json | jq -r ".spec.containers | map(.name) | index(\"$CONTAINER\")")

        # Check runAsNonRoot
        CONTAINER_RUN_AS_NON_ROOT=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath="{.spec.containers[$CONTAINER_INDEX].securityContext.runAsNonRoot}" 2>/dev/null || echo "")
        if [ "$CONTAINER_RUN_AS_NON_ROOT" == "true" ]; then
            print_pass "  Container runAsNonRoot: true"
        else
            print_fail "  Container runAsNonRoot: $CONTAINER_RUN_AS_NON_ROOT (expected: true)"
        fi

        # Check readOnlyRootFilesystem
        READ_ONLY_FS=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath="{.spec.containers[$CONTAINER_INDEX].securityContext.readOnlyRootFilesystem}" 2>/dev/null || echo "")
        if [ "$READ_ONLY_FS" == "true" ]; then
            print_pass "  Container readOnlyRootFilesystem: true"
        else
            print_warn "  Container readOnlyRootFilesystem: $READ_ONLY_FS (recommended: true)"
        fi

        # Check allowPrivilegeEscalation
        ALLOW_PRIV_ESC=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath="{.spec.containers[$CONTAINER_INDEX].securityContext.allowPrivilegeEscalation}" 2>/dev/null || echo "")
        if [ "$ALLOW_PRIV_ESC" == "false" ]; then
            print_pass "  Container allowPrivilegeEscalation: false"
        else
            print_fail "  Container allowPrivilegeEscalation: $ALLOW_PRIV_ESC (expected: false)"
        fi

        # Check capabilities
        CAP_DROP=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath="{.spec.containers[$CONTAINER_INDEX].securityContext.capabilities.drop}" 2>/dev/null || echo "")
        if echo "$CAP_DROP" | grep -q "ALL"; then
            print_pass "  Container capabilities drop: ALL"
        else
            print_fail "  Container capabilities drop: $CAP_DROP (expected: [ALL])"
        fi
    done
done

# 4. Check Service Accounts
print_section "4. Service Account Configuration"

SERVICE_ACCOUNTS=$(kubectl get serviceaccounts -n "$NAMESPACE" -o jsonpath='{.items[*].metadata.name}' 2>/dev/null || echo "")

if [ -z "$SERVICE_ACCOUNTS" ]; then
    print_warn "No service accounts found in namespace $NAMESPACE"
else
    for SA in $SERVICE_ACCOUNTS; do
        if [ "$SA" == "default" ]; then
            continue
        fi

        print_info "Checking service account: $SA"

        # Check automountServiceAccountToken
        AUTO_MOUNT=$(kubectl get serviceaccount -n "$NAMESPACE" "$SA" -o jsonpath='{.automountServiceAccountToken}' 2>/dev/null || echo "")
        if [ "$AUTO_MOUNT" == "false" ]; then
            print_pass "  automountServiceAccountToken: false"
        else
            print_warn "  automountServiceAccountToken: $AUTO_MOUNT (recommended: false for security)"
        fi
    done
fi

# 5. Check Network Policies
print_section "5. Network Policies"

NETWORK_POLICIES=$(kubectl get networkpolicies -n "$NAMESPACE" -o jsonpath='{.items[*].metadata.name}' 2>/dev/null || echo "")

if [ -z "$NETWORK_POLICIES" ]; then
    print_warn "No network policies found in namespace $NAMESPACE"
else
    print_pass "Network policies found: $(echo $NETWORK_POLICIES | wc -w)"
    for NP in $NETWORK_POLICIES; do
        print_info "  - $NP"
    done
fi

# 6. Check Secrets
print_section "6. Secret Management"

SECRETS=$(kubectl get secrets -n "$NAMESPACE" -o jsonpath='{.items[*].metadata.name}' 2>/dev/null || echo "")

if [ -z "$SECRETS" ]; then
    print_warn "No secrets found in namespace $NAMESPACE"
else
    SECRET_COUNT=$(echo $SECRETS | wc -w)
    print_pass "Secrets found: $SECRET_COUNT"

    # Check for common sensitive data in ConfigMaps (anti-pattern)
    CONFIGMAPS=$(kubectl get configmaps -n "$NAMESPACE" -o json 2>/dev/null || echo "{}")
    if echo "$CONFIGMAPS" | grep -iE "(password|secret|api.?key|token)" > /dev/null; then
        print_fail "ConfigMaps may contain sensitive data (passwords, secrets, API keys)"
    else
        print_pass "No obvious sensitive data found in ConfigMaps"
    fi
fi

# 7. Check Resource Limits
print_section "7. Resource Limits and Requests"

for POD in $PODS; do
    CONTAINERS=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath='{.spec.containers[*].name}')

    for CONTAINER in $CONTAINERS; do
        CONTAINER_INDEX=$(kubectl get pod -n "$NAMESPACE" "$POD" -o json | jq -r ".spec.containers | map(.name) | index(\"$CONTAINER\")")

        # Check CPU requests
        CPU_REQUEST=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath="{.spec.containers[$CONTAINER_INDEX].resources.requests.cpu}" 2>/dev/null || echo "")
        CPU_LIMIT=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath="{.spec.containers[$CONTAINER_INDEX].resources.limits.cpu}" 2>/dev/null || echo "")

        if [ -n "$CPU_REQUEST" ] && [ -n "$CPU_LIMIT" ]; then
            print_pass "  $POD/$CONTAINER: CPU requests and limits set"
        else
            print_warn "  $POD/$CONTAINER: Missing CPU requests or limits"
        fi

        # Check memory requests
        MEM_REQUEST=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath="{.spec.containers[$CONTAINER_INDEX].resources.requests.memory}" 2>/dev/null || echo "")
        MEM_LIMIT=$(kubectl get pod -n "$NAMESPACE" "$POD" -o jsonpath="{.spec.containers[$CONTAINER_INDEX].resources.limits.memory}" 2>/dev/null || echo "")

        if [ -n "$MEM_REQUEST" ] && [ -n "$MEM_LIMIT" ]; then
            print_pass "  $POD/$CONTAINER: Memory requests and limits set"
        else
            print_warn "  $POD/$CONTAINER: Missing memory requests or limits"
        fi
    done
done

# 8. Runtime Verification (if pods are running)
print_section "8. Runtime Security Verification"

RUNNING_PODS=$(kubectl get pods -n "$NAMESPACE" --field-selector=status.phase=Running -o jsonpath='{.items[*].metadata.name}')

if [ -z "$RUNNING_PODS" ]; then
    print_warn "No running pods found for runtime verification"
else
    for POD in $RUNNING_PODS; do
        print_info "Runtime check for pod: $POD"

        # Check running user
        RUNNING_USER=$(kubectl exec -n "$NAMESPACE" "$POD" -- id -u 2>/dev/null || echo "")
        if [ -n "$RUNNING_USER" ] && [ "$RUNNING_USER" != "0" ]; then
            print_pass "  Running as non-root user (UID: $RUNNING_USER)"
        elif [ "$RUNNING_USER" == "0" ]; then
            print_fail "  Running as root user (UID: 0)"
        else
            print_warn "  Unable to determine running user"
        fi

        # Check read-only filesystem
        if kubectl exec -n "$NAMESPACE" "$POD" -- touch /test 2>&1 | grep -q "Read-only file system"; then
            print_pass "  Root filesystem is read-only"
        elif kubectl exec -n "$NAMESPACE" "$POD" -- touch /test 2>&1 | grep -q "Permission denied"; then
            print_pass "  Root filesystem write is denied"
        else
            # Try to clean up test file
            kubectl exec -n "$NAMESPACE" "$POD" -- rm -f /test 2>/dev/null || true
            print_warn "  Root filesystem may be writable"
        fi
    done
fi

# 9. Check PodDisruptionBudget
print_section "9. High Availability Configuration"

PDBS=$(kubectl get poddisruptionbudgets -n "$NAMESPACE" -o jsonpath='{.items[*].metadata.name}' 2>/dev/null || echo "")

if [ -z "$PDBS" ]; then
    print_warn "No PodDisruptionBudgets found"
else
    print_pass "PodDisruptionBudgets found: $(echo $PDBS | wc -w)"
fi

# 10. Check HPA
HPAS=$(kubectl get hpa -n "$NAMESPACE" -o jsonpath='{.items[*].metadata.name}' 2>/dev/null || echo "")

if [ -z "$HPAS" ]; then
    print_warn "No HorizontalPodAutoscalers found"
else
    print_pass "HorizontalPodAutoscalers found: $(echo $HPAS | wc -w)"
fi

# Summary
print_header "Validation Summary"

echo -e "${GREEN}Passed:${NC}   $PASSED"
echo -e "${YELLOW}Warnings:${NC} $WARNINGS"
echo -e "${RED}Errors:${NC}   $ERRORS"

echo ""

if [ $ERRORS -eq 0 ]; then
    if [ $WARNINGS -eq 0 ]; then
        echo -e "${GREEN}✓ All security checks passed!${NC}"
        exit 0
    else
        echo -e "${YELLOW}⚠ Security checks passed with warnings${NC}"
        exit 0
    fi
else
    echo -e "${RED}✗ Security validation failed with $ERRORS error(s)${NC}"
    exit 1
fi
