#!/bin/bash
# Automated deployment script for Honua
# Supports multiple environments and cloud providers

set -e  # Exit on error
set -o pipefail  # Exit on pipe failure

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
ENVIRONMENT="${ENVIRONMENT:-development}"
CLOUD_PROVIDER="${CLOUD_PROVIDER:-generic}"
NAMESPACE="${NAMESPACE:-honua}"
DRY_RUN="${DRY_RUN:-false}"
SKIP_BUILD="${SKIP_BUILD:-false}"
SKIP_TESTS="${SKIP_TESTS:-false}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
DEPLOYMENT_METHOD="${DEPLOYMENT_METHOD:-helm}"  # Options: helm, kustomize

# Functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_banner() {
    echo ""
    echo "╔══════════════════════════════════════════════════════════╗"
    echo "║                                                          ║"
    echo "║          Honua Deployment Automation Script             ║"
    echo "║                                                          ║"
    echo "╚══════════════════════════════════════════════════════════╝"
    echo ""
}

print_config() {
    log_info "Deployment Configuration:"
    echo "  Environment:        $ENVIRONMENT"
    echo "  Cloud Provider:     $CLOUD_PROVIDER"
    echo "  Namespace:          $NAMESPACE"
    echo "  Image Tag:          $IMAGE_TAG"
    echo "  Deployment Method:  $DEPLOYMENT_METHOD"
    echo "  Dry Run:            $DRY_RUN"
    echo "  Skip Build:         $SKIP_BUILD"
    echo "  Skip Tests:         $SKIP_TESTS"
    echo ""
}

check_dependencies() {
    log_info "Checking dependencies..."

    local missing_deps=()

    command -v kubectl >/dev/null 2>&1 || missing_deps+=("kubectl")
    command -v helm >/dev/null 2>&1 || missing_deps+=("helm")
    command -v docker >/dev/null 2>&1 || missing_deps+=("docker")

    if [ ${#missing_deps[@]} -ne 0 ]; then
        log_error "Missing dependencies: ${missing_deps[*]}"
        log_error "Please install missing dependencies and try again."
        exit 1
    fi

    log_success "All dependencies are installed"
}

check_cluster_connection() {
    log_info "Checking cluster connection..."

    if ! kubectl cluster-info >/dev/null 2>&1; then
        log_error "Cannot connect to Kubernetes cluster"
        log_error "Please configure kubectl to connect to your cluster"
        exit 1
    fi

    local context=$(kubectl config current-context)
    log_success "Connected to cluster: $context"
}

validate_environment() {
    log_info "Validating environment..."

    case $ENVIRONMENT in
        development|staging|production)
            log_success "Valid environment: $ENVIRONMENT"
            ;;
        *)
            log_error "Invalid environment: $ENVIRONMENT"
            log_error "Must be one of: development, staging, production"
            exit 1
            ;;
    esac
}

build_images() {
    if [ "$SKIP_BUILD" = "true" ]; then
        log_warning "Skipping image build"
        return 0
    fi

    log_info "Building Docker images..."

    local build_args="--build-arg DOTNET_VERSION=9.0"

    docker build -f deployment/docker/Dockerfile.host -t honua/server-host:$IMAGE_TAG $build_args . || {
        log_error "Failed to build API server image"
        exit 1
    }

    docker build -f deployment/docker/Dockerfile.intake -t honua/server-intake:$IMAGE_TAG $build_args . || {
        log_error "Failed to build intake service image"
        exit 1
    }

    docker build -f deployment/docker/Dockerfile.orchestrator -t honua/build-orchestrator:$IMAGE_TAG $build_args . || {
        log_error "Failed to build orchestrator image"
        exit 1
    }

    log_success "All images built successfully"
}

push_images() {
    if [ "$SKIP_BUILD" = "true" ]; then
        log_warning "Skipping image push"
        return 0
    fi

    log_info "Pushing Docker images..."

    docker push honua/server-host:$IMAGE_TAG || {
        log_error "Failed to push API server image"
        exit 1
    }

    docker push honua/server-intake:$IMAGE_TAG || {
        log_error "Failed to push intake service image"
        exit 1
    }

    docker push honua/build-orchestrator:$IMAGE_TAG || {
        log_error "Failed to push orchestrator image"
        exit 1
    }

    log_success "All images pushed successfully"
}

run_tests() {
    if [ "$SKIP_TESTS" = "true" ]; then
        log_warning "Skipping tests"
        return 0
    fi

    log_info "Running tests..."

    # Add your test commands here
    # For example:
    # dotnet test tests/Honua.Server.Core.Tests/

    log_success "All tests passed"
}

create_namespace() {
    log_info "Creating namespace: $NAMESPACE"

    if [ "$DRY_RUN" = "true" ]; then
        kubectl create namespace $NAMESPACE --dry-run=client -o yaml
        return 0
    fi

    kubectl create namespace $NAMESPACE --dry-run=client -o yaml | kubectl apply -f - || {
        log_warning "Namespace already exists or creation failed"
    }

    log_success "Namespace ready"
}

deploy_helm() {
    log_info "Deploying with Helm..."

    local values_file="deployment/helm/honua/values-${ENVIRONMENT}.yaml"

    if [ ! -f "$values_file" ]; then
        log_error "Values file not found: $values_file"
        exit 1
    fi

    local helm_args=(
        "upgrade" "--install"
        "honua"
        "deployment/helm/honua"
        "--namespace" "$NAMESPACE"
        "--create-namespace"
        "--values" "$values_file"
        "--set" "image.tag=$IMAGE_TAG"
        "--set" "cloudProvider.type=$CLOUD_PROVIDER"
    )

    if [ "$DRY_RUN" = "true" ]; then
        helm_args+=("--dry-run" "--debug")
    fi

    helm "${helm_args[@]}" || {
        log_error "Helm deployment failed"
        exit 1
    }

    log_success "Helm deployment complete"
}

deploy_kustomize() {
    log_info "Deploying with Kustomize..."

    local overlay_path="deployment/k8s/overlays/${ENVIRONMENT}"

    if [ ! -d "$overlay_path" ]; then
        log_error "Overlay not found: $overlay_path"
        exit 1
    fi

    if [ "$DRY_RUN" = "true" ]; then
        kubectl apply -k "$overlay_path" --dry-run=client
        return 0
    fi

    kubectl apply -k "$overlay_path" || {
        log_error "Kustomize deployment failed"
        exit 1
    }

    log_success "Kustomize deployment complete"
}

apply_cloud_config() {
    log_info "Applying cloud-specific configurations..."

    case $CLOUD_PROVIDER in
        aws)
            if [ -f "deployment/cloud/aws/eks-config.yaml" ]; then
                kubectl apply -f deployment/cloud/aws/eks-config.yaml -n $NAMESPACE
                log_success "AWS EKS configuration applied"
            fi
            ;;
        azure)
            if [ -f "deployment/cloud/azure/aks-config.yaml" ]; then
                kubectl apply -f deployment/cloud/azure/aks-config.yaml -n $NAMESPACE
                log_success "Azure AKS configuration applied"
            fi
            ;;
        gcp)
            if [ -f "deployment/cloud/gcp/gke-config.yaml" ]; then
                kubectl apply -f deployment/cloud/gcp/gke-config.yaml -n $NAMESPACE
                log_success "GCP GKE configuration applied"
            fi
            ;;
        generic)
            log_info "No cloud-specific configuration for generic provider"
            ;;
        *)
            log_warning "Unknown cloud provider: $CLOUD_PROVIDER"
            ;;
    esac
}

wait_for_rollout() {
    log_info "Waiting for rollout to complete..."

    local deployments=(
        "deployment/${NAMESPACE}-api"
        "deployment/${NAMESPACE}-intake"
        "deployment/${NAMESPACE}-orchestrator"
    )

    for deployment in "${deployments[@]}"; do
        log_info "Waiting for $deployment..."
        kubectl rollout status "$deployment" -n "$NAMESPACE" --timeout=5m || {
            log_error "Rollout failed for $deployment"
            return 1
        }
    done

    log_success "All deployments are ready"
}

verify_deployment() {
    log_info "Verifying deployment..."

    log_info "Pods:"
    kubectl get pods -n $NAMESPACE

    log_info "Services:"
    kubectl get services -n $NAMESPACE

    log_info "Ingresses:"
    kubectl get ingress -n $NAMESPACE

    # Check if all pods are running
    local not_running=$(kubectl get pods -n $NAMESPACE --field-selector=status.phase!=Running --no-headers 2>/dev/null | wc -l)

    if [ "$not_running" -gt 0 ]; then
        log_warning "$not_running pod(s) are not in Running state"
        kubectl get pods -n $NAMESPACE --field-selector=status.phase!=Running
    else
        log_success "All pods are running"
    fi
}

print_access_info() {
    log_info "Access Information:"
    echo ""

    # Get ingress host
    local ingress_host=$(kubectl get ingress -n $NAMESPACE -o jsonpath='{.items[0].spec.rules[0].host}' 2>/dev/null || echo "N/A")

    if [ "$ingress_host" != "N/A" ]; then
        echo "  API URL: https://$ingress_host"
    else
        echo "  Use port-forward to access services:"
        echo "    kubectl port-forward -n $NAMESPACE svc/honua-api 8080:8080"
    fi

    echo ""
}

rollback() {
    log_warning "Rolling back deployment..."

    if [ "$DEPLOYMENT_METHOD" = "helm" ]; then
        helm rollback honua -n $NAMESPACE || {
            log_error "Rollback failed"
            exit 1
        }
        log_success "Rollback complete"
    else
        log_error "Rollback is only supported for Helm deployments"
        exit 1
    fi
}

cleanup_on_error() {
    log_error "Deployment failed. Check logs above for details."

    if [ "$ENVIRONMENT" = "development" ]; then
        read -p "Do you want to rollback? (y/N) " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            rollback
        fi
    fi

    exit 1
}

# Main deployment flow
main() {
    print_banner
    print_config

    # Trap errors
    trap cleanup_on_error ERR

    # Pre-deployment checks
    check_dependencies
    check_cluster_connection
    validate_environment

    # Build and push images
    build_images
    if [ "$ENVIRONMENT" != "development" ]; then
        push_images
    fi

    # Run tests
    run_tests

    # Create namespace
    create_namespace

    # Deploy application
    case $DEPLOYMENT_METHOD in
        helm)
            deploy_helm
            ;;
        kustomize)
            deploy_kustomize
            ;;
        *)
            log_error "Unknown deployment method: $DEPLOYMENT_METHOD"
            exit 1
            ;;
    esac

    # Apply cloud-specific configurations
    if [ "$CLOUD_PROVIDER" != "generic" ]; then
        apply_cloud_config
    fi

    # Wait for rollout (skip in dry-run mode)
    if [ "$DRY_RUN" != "true" ]; then
        wait_for_rollout
        verify_deployment
        print_access_info
    fi

    log_success "Deployment completed successfully!"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -c|--cloud)
            CLOUD_PROVIDER="$2"
            shift 2
            ;;
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        -t|--tag)
            IMAGE_TAG="$2"
            shift 2
            ;;
        -m|--method)
            DEPLOYMENT_METHOD="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN="true"
            shift
            ;;
        --skip-build)
            SKIP_BUILD="true"
            shift
            ;;
        --skip-tests)
            SKIP_TESTS="true"
            shift
            ;;
        --rollback)
            rollback
            exit 0
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -e, --environment ENV    Environment (development/staging/production)"
            echo "  -c, --cloud PROVIDER     Cloud provider (aws/azure/gcp/generic)"
            echo "  -n, --namespace NS       Kubernetes namespace"
            echo "  -t, --tag TAG           Docker image tag"
            echo "  -m, --method METHOD     Deployment method (helm/kustomize)"
            echo "  --dry-run               Perform a dry run without applying changes"
            echo "  --skip-build            Skip building Docker images"
            echo "  --skip-tests            Skip running tests"
            echo "  --rollback              Rollback to previous release"
            echo "  -h, --help              Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0 -e production -c aws -t v1.0.0"
            echo "  $0 -e staging --dry-run"
            echo "  $0 --rollback"
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            echo "Use -h or --help for usage information"
            exit 1
            ;;
    esac
done

# Run main deployment
main
