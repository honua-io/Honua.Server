#!/bin/bash
# Deployment script for Honua Server Helm chart

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_success() { echo -e "${GREEN}✓${NC} $1"; }
print_error() { echo -e "${RED}✗${NC} $1"; }
print_warning() { echo -e "${YELLOW}⚠${NC} $1"; }
print_info() { echo -e "${BLUE}ℹ${NC} $1"; }

# Configuration
RELEASE_NAME="${RELEASE_NAME:-honua-server}"
NAMESPACE="${NAMESPACE:-honua}"
CHART_PATH="${CHART_PATH:-./deploy/kubernetes/helm/honua-server}"
ENVIRONMENT="${ENVIRONMENT:-dev}"
DRY_RUN="${DRY_RUN:-false}"
SKIP_PREFLIGHT="${SKIP_PREFLIGHT:-false}"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -r|--release)
            RELEASE_NAME="$2"
            shift 2
            ;;
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -c|--chart)
            CHART_PATH="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --skip-preflight)
            SKIP_PREFLIGHT=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -r, --release NAME         Release name (default: honua-server)"
            echo "  -n, --namespace NAMESPACE  Target namespace (default: honua)"
            echo "  -e, --environment ENV      Environment (dev|staging|production) (default: dev)"
            echo "  -c, --chart PATH           Path to Helm chart (default: ./deploy/kubernetes/helm/honua-server)"
            echo "  --dry-run                  Perform a dry run"
            echo "  --skip-preflight           Skip preflight checks"
            echo "  -h, --help                 Show this help message"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

print_info "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
print_info "  Honua Server Deployment"
print_info "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
print_info "Configuration:"
echo "  Release:     $RELEASE_NAME"
echo "  Namespace:   $NAMESPACE"
echo "  Environment: $ENVIRONMENT"
echo "  Chart:       $CHART_PATH"
echo "  Dry Run:     $DRY_RUN"
echo ""

# Run preflight checks
if [ "$SKIP_PREFLIGHT" = "false" ]; then
    print_info "Running preflight checks..."
    if [ -f "./deploy/kubernetes/scripts/preflight-check.sh" ]; then
        ./deploy/kubernetes/scripts/preflight-check.sh --namespace "$NAMESPACE" || {
            print_error "Preflight checks failed"
            read -p "Continue anyway? (y/N): " CONTINUE
            if [[ ! "$CONTINUE" =~ ^[Yy]$ ]]; then
                exit 1
            fi
        }
    else
        print_warning "Preflight check script not found, skipping..."
    fi
    echo ""
fi

# Validate chart
print_info "Validating Helm chart..."
if helm lint "$CHART_PATH" &> /dev/null; then
    print_success "Chart validation passed"
else
    print_error "Chart validation failed"
    helm lint "$CHART_PATH"
    exit 1
fi
echo ""

# Check if release exists
RELEASE_EXISTS=false
if helm list -n "$NAMESPACE" | grep -q "^$RELEASE_NAME"; then
    RELEASE_EXISTS=true
    print_info "Release '$RELEASE_NAME' already exists in namespace '$NAMESPACE'"
    OPERATION="upgrade"
else
    print_info "Release '$RELEASE_NAME' does not exist, will perform fresh install"
    OPERATION="install"
fi
echo ""

# Build Helm command
VALUES_FILE="$CHART_PATH/values-${ENVIRONMENT}.yaml"
HELM_CMD="helm $OPERATION $RELEASE_NAME $CHART_PATH"
HELM_CMD="$HELM_CMD --namespace $NAMESPACE"
HELM_CMD="$HELM_CMD --create-namespace"

if [ -f "$VALUES_FILE" ]; then
    print_info "Using values file: $VALUES_FILE"
    HELM_CMD="$HELM_CMD --values $VALUES_FILE"
else
    print_warning "Values file not found: $VALUES_FILE"
    print_info "Using default values only"
fi

if [ "$DRY_RUN" = "true" ]; then
    HELM_CMD="$HELM_CMD --dry-run --debug"
fi

# Add wait and timeout for production
if [ "$ENVIRONMENT" = "production" ]; then
    HELM_CMD="$HELM_CMD --wait --timeout 10m"
fi

echo ""
print_info "Executing: $HELM_CMD"
echo ""

# Execute Helm command
if eval "$HELM_CMD"; then
    print_success "$OPERATION completed successfully!"
else
    print_error "$OPERATION failed"
    exit 1
fi

if [ "$DRY_RUN" = "false" ]; then
    echo ""
    print_info "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    print_success "Deployment Summary"
    print_info "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""

    # Show release info
    print_info "Release Information:"
    helm list -n "$NAMESPACE" | grep "$RELEASE_NAME"
    echo ""

    # Show pod status
    print_info "Pod Status:"
    kubectl get pods -n "$NAMESPACE" -l "app.kubernetes.io/name=honua-server"
    echo ""

    # Show service info
    print_info "Service Information:"
    kubectl get svc -n "$NAMESPACE" -l "app.kubernetes.io/name=honua-server"
    echo ""

    # Show ingress info
    if kubectl get ingress -n "$NAMESPACE" &> /dev/null; then
        print_info "Ingress Information:"
        kubectl get ingress -n "$NAMESPACE"
        echo ""
    fi

    print_info "Useful Commands:"
    echo "  # View logs"
    echo "  kubectl logs -f -l app.kubernetes.io/name=honua-server -n $NAMESPACE"
    echo ""
    echo "  # Check deployment status"
    echo "  kubectl rollout status deployment/$RELEASE_NAME -n $NAMESPACE"
    echo ""
    echo "  # Port forward to access locally"
    echo "  kubectl port-forward svc/$RELEASE_NAME 8080:80 -n $NAMESPACE"
    echo ""
    echo "  # Uninstall"
    echo "  helm uninstall $RELEASE_NAME -n $NAMESPACE"
    echo ""
fi
