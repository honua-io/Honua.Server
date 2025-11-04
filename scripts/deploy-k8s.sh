#!/bin/bash
set -euo pipefail

# Kubernetes Deployment Script for Multi-Cloud Environments
# Supports: AWS EKS, Azure AKS, GCP GKE

# Configuration
ENVIRONMENT="${1:-dev}"
CLOUD_PROVIDER="${2:-aws}"
IMAGE_TAG="${3:-latest}"
NAMESPACE="honua-${ENVIRONMENT}"
HELM_RELEASE="honua-server"
HELM_CHART_PATH="${HELM_CHART_PATH:-./deploy/helm/honua-server}"
VALUES_FILE="values-${ENVIRONMENT}.yaml"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# Validate inputs
validate_inputs() {
    log_step "Validating inputs..."

    if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
        log_error "Invalid environment: $ENVIRONMENT (must be dev, staging, or production)"
        exit 1
    fi

    if [[ ! "$CLOUD_PROVIDER" =~ ^(aws|azure|gcp)$ ]]; then
        log_error "Invalid cloud provider: $CLOUD_PROVIDER (must be aws, azure, or gcp)"
        exit 1
    fi

    log_info "Environment: $ENVIRONMENT"
    log_info "Cloud Provider: $CLOUD_PROVIDER"
    log_info "Image Tag: $IMAGE_TAG"
}

# Configure kubectl for AWS EKS
configure_aws_eks() {
    log_step "Configuring kubectl for AWS EKS..."

    local cluster_name="${AWS_EKS_CLUSTER_NAME:-honua-${ENVIRONMENT}}"
    local region="${AWS_REGION:-us-east-1}"

    if ! command -v aws &> /dev/null; then
        log_error "AWS CLI not installed"
        exit 1
    fi

    aws eks update-kubeconfig \
        --region "$region" \
        --name "$cluster_name"

    log_info "Successfully configured kubectl for EKS cluster: $cluster_name"
}

# Configure kubectl for Azure AKS
configure_azure_aks() {
    log_step "Configuring kubectl for Azure AKS..."

    local cluster_name="${AZURE_AKS_CLUSTER_NAME:-honua-${ENVIRONMENT}}"
    local resource_group="${AZURE_RESOURCE_GROUP:-honua-${ENVIRONMENT}}"

    if ! command -v az &> /dev/null; then
        log_error "Azure CLI not installed"
        exit 1
    fi

    # Login if needed
    if [ -n "${AZURE_CLIENT_ID:-}" ] && [ -n "${AZURE_CLIENT_SECRET:-}" ] && [ -n "${AZURE_TENANT_ID:-}" ]; then
        az login --service-principal \
            -u "$AZURE_CLIENT_ID" \
            -p "$AZURE_CLIENT_SECRET" \
            --tenant "$AZURE_TENANT_ID" > /dev/null
    fi

    az aks get-credentials \
        --resource-group "$resource_group" \
        --name "$cluster_name" \
        --overwrite-existing

    log_info "Successfully configured kubectl for AKS cluster: $cluster_name"
}

# Configure kubectl for GCP GKE
configure_gcp_gke() {
    log_step "Configuring kubectl for GCP GKE..."

    local cluster_name="${GCP_GKE_CLUSTER_NAME:-honua-${ENVIRONMENT}}"
    local region="${GCP_REGION:-us-central1}"
    local project="${GCP_PROJECT_ID}"

    if ! command -v gcloud &> /dev/null; then
        log_error "Google Cloud SDK not installed"
        exit 1
    fi

    # Authenticate if service account key is provided
    if [ -n "${GCP_SERVICE_ACCOUNT_KEY:-}" ]; then
        echo "$GCP_SERVICE_ACCOUNT_KEY" | gcloud auth activate-service-account --key-file=-
    fi

    gcloud container clusters get-credentials \
        "$cluster_name" \
        --region="$region" \
        --project="$project"

    log_info "Successfully configured kubectl for GKE cluster: $cluster_name"
}

# Configure cluster based on cloud provider
configure_cluster() {
    case "$CLOUD_PROVIDER" in
        aws)
            configure_aws_eks
            ;;
        azure)
            configure_azure_aks
            ;;
        gcp)
            configure_gcp_gke
            ;;
    esac
}

# Create namespace
create_namespace() {
    log_step "Creating namespace: $NAMESPACE"

    kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

    log_info "Namespace ready"
}

# Fetch secrets from cloud provider
fetch_secrets() {
    log_step "Fetching secrets from cloud provider..."

    local db_password=""

    case "$CLOUD_PROVIDER" in
        aws)
            if ! command -v aws &> /dev/null; then
                log_error "AWS CLI not installed"
                exit 1
            fi

            db_password=$(aws secretsmanager get-secret-value \
                --secret-id "honua-${ENVIRONMENT}-db-password" \
                --query SecretString \
                --output text)
            ;;

        azure)
            if ! command -v az &> /dev/null; then
                log_error "Azure CLI not installed"
                exit 1
            fi

            local vault_name="${AZURE_KEYVAULT_NAME:-honua-${ENVIRONMENT}}"

            db_password=$(az keyvault secret show \
                --name "honua-${ENVIRONMENT}-db-password" \
                --vault-name "$vault_name" \
                --query value \
                --output tsv)
            ;;

        gcp)
            if ! command -v gcloud &> /dev/null; then
                log_error "Google Cloud SDK not installed"
                exit 1
            fi

            db_password=$(gcloud secrets versions access latest \
                --secret="honua-${ENVIRONMENT}-db-password" \
                --project="${GCP_PROJECT_ID}")
            ;;
    esac

    # Create Kubernetes secret
    kubectl create secret generic honua-db-credentials \
        --from-literal=password="$db_password" \
        --namespace="$NAMESPACE" \
        --dry-run=client -o yaml | kubectl apply -f -

    log_info "Secrets configured"
}

# Backup current deployment
backup_deployment() {
    log_step "Backing up current deployment..."

    local backup_dir="/tmp/honua-backup-$(date +%Y%m%d-%H%M%S)"
    mkdir -p "$backup_dir"

    # Export current deployment
    kubectl get deployment "$HELM_RELEASE" \
        -n "$NAMESPACE" \
        -o yaml > "$backup_dir/deployment.yaml" 2>/dev/null || true

    # Save current image
    local current_image=$(kubectl get deployment "$HELM_RELEASE" \
        -n "$NAMESPACE" \
        -o jsonpath='{.spec.template.spec.containers[0].image}' 2>/dev/null || echo "none")

    echo "$current_image" > "$backup_dir/image.txt"

    log_info "Backup saved to: $backup_dir"
    echo "$backup_dir" > /tmp/honua-last-backup.txt
}

# Run database migrations
run_migrations() {
    log_step "Running database migrations..."

    local migration_pod="honua-migrations-$(date +%s)"

    kubectl run "$migration_pod" \
        --image="$IMAGE_TAG" \
        --namespace="$NAMESPACE" \
        --restart=Never \
        --env="ASPNETCORE_ENVIRONMENT=${ENVIRONMENT^}" \
        --command -- dotnet Honua.Server.Host.dll migrate

    # Wait for migration to complete
    kubectl wait --for=condition=complete \
        pod/"$migration_pod" \
        --namespace="$NAMESPACE" \
        --timeout=600s

    # Show migration logs
    kubectl logs "$migration_pod" --namespace="$NAMESPACE"

    # Cleanup migration pod
    kubectl delete pod "$migration_pod" --namespace="$NAMESPACE" || true

    log_info "Database migrations completed"
}

# Deploy with Helm
deploy_with_helm() {
    log_step "Deploying with Helm..."

    if ! command -v helm &> /dev/null; then
        log_error "Helm not installed"
        exit 1
    fi

    # Build values override
    local values_override=(
        --set "image.tag=$IMAGE_TAG"
        --set "image.pullPolicy=Always"
        --set "environment=$ENVIRONMENT"
        --set "cloud.provider=$CLOUD_PROVIDER"
    )

    # Environment-specific settings
    case "$ENVIRONMENT" in
        dev)
            values_override+=(
                --set "replicaCount=2"
                --set "autoscaling.enabled=false"
            )
            ;;
        staging)
            values_override+=(
                --set "replicaCount=3"
                --set "autoscaling.enabled=true"
                --set "autoscaling.minReplicas=3"
                --set "autoscaling.maxReplicas=10"
            )
            ;;
        production)
            values_override+=(
                --set "replicaCount=5"
                --set "autoscaling.enabled=true"
                --set "autoscaling.minReplicas=5"
                --set "autoscaling.maxReplicas=20"
            )
            ;;
    esac

    # Deploy
    helm upgrade --install "$HELM_RELEASE" "$HELM_CHART_PATH" \
        --namespace="$NAMESPACE" \
        --values="$HELM_CHART_PATH/$VALUES_FILE" \
        "${values_override[@]}" \
        --wait \
        --timeout 15m \
        --create-namespace

    log_info "Helm deployment completed"
}

# Verify deployment
verify_deployment() {
    log_step "Verifying deployment..."

    # Wait for rollout
    kubectl rollout status deployment/"$HELM_RELEASE" \
        --namespace="$NAMESPACE" \
        --timeout=10m

    # Check pod health
    kubectl wait --for=condition=ready pod \
        --selector="app.kubernetes.io/name=$HELM_RELEASE" \
        --namespace="$NAMESPACE" \
        --timeout=600s

    # Get deployment status
    kubectl get pods -n "$NAMESPACE" -l "app.kubernetes.io/name=$HELM_RELEASE"

    log_info "Deployment verified successfully"
}

# Run smoke tests
run_smoke_tests() {
    log_step "Running smoke tests..."

    # Get service endpoint
    local service_ip=$(kubectl get svc "$HELM_RELEASE" \
        -n "$NAMESPACE" \
        -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")

    if [ -z "$service_ip" ]; then
        log_warn "Service IP not available, using port-forward"
        kubectl port-forward "svc/$HELM_RELEASE" 8080:80 -n "$NAMESPACE" &
        local pf_pid=$!
        sleep 5
        local endpoint="http://localhost:8080"
    else
        local endpoint="http://$service_ip"
    fi

    # Wait for endpoint to be ready
    log_info "Waiting for service to be ready..."
    for i in {1..30}; do
        if curl -sSf "$endpoint/healthz/ready" > /dev/null 2>&1; then
            log_info "Service is ready"
            break
        fi
        echo "Waiting... ($i/30)"
        sleep 10
    done

    # Run health checks
    log_info "Running health checks..."
    curl -f "$endpoint/healthz/live" || { log_error "Live health check failed"; exit 1; }
    curl -f "$endpoint/healthz/ready" || { log_error "Ready health check failed"; exit 1; }
    curl -f "$endpoint/api/v1/collections" || { log_error "API health check failed"; exit 1; }

    # Cleanup port-forward if used
    if [ -n "${pf_pid:-}" ]; then
        kill "$pf_pid" 2>/dev/null || true
    fi

    log_info "Smoke tests passed"
}

# Rollback on failure
rollback() {
    log_error "Deployment failed, initiating rollback..."

    # Rollback using Helm
    helm rollback "$HELM_RELEASE" -n "$NAMESPACE" || true

    # Wait for rollback
    kubectl rollout status deployment/"$HELM_RELEASE" \
        --namespace="$NAMESPACE" \
        --timeout=5m

    log_info "Rollback completed"
}

# Display deployment info
display_info() {
    log_step "Deployment Information"

    echo ""
    echo "=== Deployment Status ==="
    kubectl get all -n "$NAMESPACE"

    echo ""
    echo "=== Recent Events ==="
    kubectl get events -n "$NAMESPACE" --sort-by='.lastTimestamp' | tail -20

    echo ""
    echo "=== Service Endpoints ==="
    kubectl get ingress -n "$NAMESPACE" || true
    kubectl get svc -n "$NAMESPACE"
}

# Usage
usage() {
    cat << EOF
Usage: $0 ENVIRONMENT CLOUD_PROVIDER IMAGE_TAG

Deploy Honua to Kubernetes

ARGUMENTS:
    ENVIRONMENT      Deployment environment (dev, staging, production)
    CLOUD_PROVIDER   Cloud provider (aws, azure, gcp)
    IMAGE_TAG        Docker image tag to deploy

ENVIRONMENT VARIABLES:
    AWS:
        AWS_EKS_CLUSTER_NAME    EKS cluster name
        AWS_REGION              AWS region
        AWS_ACCESS_KEY_ID       AWS access key
        AWS_SECRET_ACCESS_KEY   AWS secret key

    Azure:
        AZURE_AKS_CLUSTER_NAME  AKS cluster name
        AZURE_RESOURCE_GROUP    Resource group
        AZURE_CLIENT_ID         Service principal client ID
        AZURE_CLIENT_SECRET     Service principal secret
        AZURE_TENANT_ID         Azure tenant ID
        AZURE_KEYVAULT_NAME     Key Vault name

    GCP:
        GCP_GKE_CLUSTER_NAME    GKE cluster name
        GCP_REGION              GCP region
        GCP_PROJECT_ID          GCP project ID
        GCP_SERVICE_ACCOUNT_KEY Service account key (JSON)

EXAMPLES:
    $0 dev aws ghcr.io/org/honua-server:1.0.0
    $0 staging azure myregistry.azurecr.io/honua-server:1.0.0
    $0 production gcp gcr.io/project/honua-server:1.0.0

EOF
}

# Main execution
main() {
    if [ $# -lt 3 ]; then
        usage
        exit 1
    fi

    log_info "Starting deployment to $CLOUD_PROVIDER ($ENVIRONMENT)..."
    echo ""

    validate_inputs
    configure_cluster
    create_namespace
    fetch_secrets
    backup_deployment

    # Set trap to rollback on failure
    trap rollback ERR

    run_migrations
    deploy_with_helm
    verify_deployment
    run_smoke_tests

    display_info

    log_info "Deployment completed successfully!"
}

main "$@"
