#!/bin/bash
# Preflight check script for Honua Server Kubernetes deployment
# Verifies cluster requirements, dependencies, and configuration

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
NAMESPACE="${NAMESPACE:-honua}"
MIN_K8S_VERSION="1.23"
MIN_HELM_VERSION="3.8"
REQUIRED_CPU=2
REQUIRED_MEMORY=4096 # MB

# Functions
print_header() {
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

version_gt() {
    test "$(printf '%s\n' "$@" | sort -V | head -n 1)" != "$1"
}

check_command() {
    if command -v "$1" &> /dev/null; then
        print_success "$1 is installed"
        return 0
    else
        print_error "$1 is not installed"
        return 1
    fi
}

check_kubernetes_connection() {
    print_info "Checking Kubernetes cluster connection..."
    if kubectl cluster-info &> /dev/null; then
        print_success "Connected to Kubernetes cluster"
        kubectl cluster-info | head -n 1
        return 0
    else
        print_error "Cannot connect to Kubernetes cluster"
        return 1
    fi
}

check_kubernetes_version() {
    print_info "Checking Kubernetes version..."
    local version=$(kubectl version --short 2>/dev/null | grep "Server Version" | awk '{print $3}' | sed 's/v//')
    if [ -z "$version" ]; then
        version=$(kubectl version -o json 2>/dev/null | grep -oP '(?<="gitVersion": "v)[^"]*' | head -1)
    fi

    if version_gt "$MIN_K8S_VERSION" "$version"; then
        print_error "Kubernetes version $version is below minimum required version $MIN_K8S_VERSION"
        return 1
    else
        print_success "Kubernetes version $version meets minimum requirement"
        return 0
    fi
}

check_helm_version() {
    print_info "Checking Helm version..."
    local version=$(helm version --short 2>/dev/null | grep -oP 'v\K[0-9]+\.[0-9]+' | head -1)

    if version_gt "$MIN_HELM_VERSION" "$version"; then
        print_error "Helm version $version is below minimum required version $MIN_HELM_VERSION"
        return 1
    else
        print_success "Helm version $version meets minimum requirement"
        return 0
    fi
}

check_namespace() {
    print_info "Checking namespace '$NAMESPACE'..."
    if kubectl get namespace "$NAMESPACE" &> /dev/null; then
        print_success "Namespace '$NAMESPACE' exists"
        return 0
    else
        print_warning "Namespace '$NAMESPACE' does not exist (will be created during installation)"
        return 0
    fi
}

check_rbac_permissions() {
    print_info "Checking RBAC permissions..."
    local errors=0

    # Check if we can create deployments
    if kubectl auth can-i create deployments --namespace="$NAMESPACE" &> /dev/null; then
        print_success "Can create deployments"
    else
        print_error "Cannot create deployments in namespace '$NAMESPACE'"
        ((errors++))
    fi

    # Check if we can create services
    if kubectl auth can-i create services --namespace="$NAMESPACE" &> /dev/null; then
        print_success "Can create services"
    else
        print_error "Cannot create services in namespace '$NAMESPACE'"
        ((errors++))
    fi

    # Check if we can create ingresses
    if kubectl auth can-i create ingresses --namespace="$NAMESPACE" &> /dev/null; then
        print_success "Can create ingresses"
    else
        print_warning "Cannot create ingresses in namespace '$NAMESPACE'"
    fi

    # Check if we can create secrets
    if kubectl auth can-i create secrets --namespace="$NAMESPACE" &> /dev/null; then
        print_success "Can create secrets"
    else
        print_error "Cannot create secrets in namespace '$NAMESPACE'"
        ((errors++))
    fi

    return $errors
}

check_cluster_resources() {
    print_info "Checking cluster resources..."

    # Get total allocatable resources
    local total_cpu=$(kubectl get nodes -o json | jq -r '.items[].status.allocatable.cpu' | awk '{sum += $1} END {print sum}')
    local total_memory=$(kubectl get nodes -o json | jq -r '.items[].status.allocatable.memory' | sed 's/Ki//' | awk '{sum += $1} END {print int(sum/1024)}')

    print_info "Total allocatable CPU: ${total_cpu} cores"
    print_info "Total allocatable Memory: ${total_memory} MB"

    if (( $(echo "$total_cpu >= $REQUIRED_CPU" | bc -l) )); then
        print_success "Sufficient CPU resources available"
    else
        print_warning "Limited CPU resources (minimum recommended: ${REQUIRED_CPU} cores)"
    fi

    if (( total_memory >= REQUIRED_MEMORY )); then
        print_success "Sufficient memory resources available"
    else
        print_warning "Limited memory resources (minimum recommended: ${REQUIRED_MEMORY} MB)"
    fi
}

check_storage_class() {
    print_info "Checking storage classes..."
    if kubectl get storageclass &> /dev/null; then
        local default_sc=$(kubectl get storageclass -o json | jq -r '.items[] | select(.metadata.annotations["storageclass.kubernetes.io/is-default-class"]=="true") | .metadata.name')
        if [ -n "$default_sc" ]; then
            print_success "Default storage class found: $default_sc"
        else
            print_warning "No default storage class found (required if persistence is enabled)"
        fi
        return 0
    else
        print_warning "No storage classes available"
        return 0
    fi
}

check_ingress_controller() {
    print_info "Checking ingress controller..."
    if kubectl get ingressclass &> /dev/null; then
        local ingress_classes=$(kubectl get ingressclass -o json | jq -r '.items[].metadata.name' | tr '\n' ', ' | sed 's/,$//')
        if [ -n "$ingress_classes" ]; then
            print_success "Ingress classes found: $ingress_classes"
        else
            print_warning "No ingress classes found (required if ingress is enabled)"
        fi
        return 0
    else
        print_warning "No ingress controller detected"
        return 0
    fi
}

check_metrics_server() {
    print_info "Checking metrics server..."
    if kubectl get deployment metrics-server -n kube-system &> /dev/null; then
        print_success "Metrics server is installed"
        return 0
    else
        print_warning "Metrics server not found (required for HPA)"
        return 0
    fi
}

check_prometheus_operator() {
    print_info "Checking Prometheus Operator..."
    if kubectl get crd servicemonitors.monitoring.coreos.com &> /dev/null; then
        print_success "Prometheus Operator CRDs found"
        return 0
    else
        print_warning "Prometheus Operator not found (required for ServiceMonitor)"
        return 0
    fi
}

check_cert_manager() {
    print_info "Checking cert-manager..."
    if kubectl get namespace cert-manager &> /dev/null; then
        print_success "cert-manager is installed"
        return 0
    else
        print_warning "cert-manager not found (required for TLS certificate management)"
        return 0
    fi
}

check_secrets() {
    print_info "Checking required secrets..."

    local missing_secrets=()

    # Check for database secret (only if external database is used)
    if [ "${CHECK_DB_SECRET:-false}" = "true" ]; then
        if ! kubectl get secret "${DB_SECRET_NAME:-honua-db-secret}" --namespace="$NAMESPACE" &> /dev/null; then
            missing_secrets+=("${DB_SECRET_NAME:-honua-db-secret}")
        fi
    fi

    # Check for Redis secret (only if external Redis is used)
    if [ "${CHECK_REDIS_SECRET:-false}" = "true" ]; then
        if ! kubectl get secret "${REDIS_SECRET_NAME:-honua-redis-secret}" --namespace="$NAMESPACE" &> /dev/null; then
            missing_secrets+=("${REDIS_SECRET_NAME:-honua-redis-secret}")
        fi
    fi

    if [ ${#missing_secrets[@]} -eq 0 ]; then
        print_success "All required secrets exist"
    else
        print_warning "Missing secrets: ${missing_secrets[*]}"
        print_info "Create secrets before deployment or use embedded databases"
    fi
}

check_network_policies() {
    print_info "Checking network policy support..."
    if kubectl get networkpolicies &> /dev/null; then
        print_success "NetworkPolicy support available"
        return 0
    else
        print_warning "NetworkPolicy support not detected"
        return 0
    fi
}

# Main execution
main() {
    print_header "Honua Server Kubernetes Preflight Check"
    echo ""

    local errors=0

    # Required tools
    print_header "Checking Required Tools"
    check_command kubectl || ((errors++))
    check_command helm || ((errors++))
    check_command jq || print_warning "jq not installed (optional, improves output)"
    echo ""

    # Kubernetes connection
    print_header "Checking Kubernetes Cluster"
    check_kubernetes_connection || ((errors++))
    check_kubernetes_version || ((errors++))
    echo ""

    # Helm
    print_header "Checking Helm"
    check_helm_version || ((errors++))
    echo ""

    # Namespace and RBAC
    print_header "Checking Namespace and RBAC"
    check_namespace
    check_rbac_permissions || ((errors++))
    echo ""

    # Cluster resources
    print_header "Checking Cluster Resources"
    check_cluster_resources
    echo ""

    # Storage
    print_header "Checking Storage"
    check_storage_class
    echo ""

    # Networking
    print_header "Checking Networking"
    check_ingress_controller
    check_network_policies
    echo ""

    # Monitoring
    print_header "Checking Monitoring Stack"
    check_metrics_server
    check_prometheus_operator
    echo ""

    # Security
    print_header "Checking Security"
    check_cert_manager
    check_secrets
    echo ""

    # Summary
    print_header "Preflight Check Summary"
    if [ $errors -eq 0 ]; then
        print_success "All critical checks passed! Ready to deploy Honua Server."
        echo ""
        print_info "To deploy, run:"
        echo "  helm install honua-server ./deploy/kubernetes/helm/honua-server \\"
        echo "    --namespace $NAMESPACE \\"
        echo "    --create-namespace \\"
        echo "    --values values.yaml"
        echo ""
        exit 0
    else
        print_error "$errors critical check(s) failed. Please fix the issues above before deploying."
        echo ""
        exit 1
    fi
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        --check-db-secret)
            CHECK_DB_SECRET=true
            shift
            ;;
        --check-redis-secret)
            CHECK_REDIS_SECRET=true
            shift
            ;;
        --db-secret-name)
            DB_SECRET_NAME="$2"
            shift 2
            ;;
        --redis-secret-name)
            REDIS_SECRET_NAME="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -n, --namespace NAMESPACE          Target namespace (default: honua)"
            echo "  --check-db-secret                  Check for database secret"
            echo "  --check-redis-secret               Check for Redis secret"
            echo "  --db-secret-name NAME              Database secret name"
            echo "  --redis-secret-name NAME           Redis secret name"
            echo "  -h, --help                         Show this help message"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

main
