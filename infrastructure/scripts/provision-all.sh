#!/bin/bash
# Provision complete Honua infrastructure
# Usage: ./provision-all.sh <environment> <cloud_provider>

set -euo pipefail

ENVIRONMENT=${1:-dev}
CLOUD_PROVIDER=${2:-aws}
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TERRAFORM_DIR="${SCRIPT_DIR}/../terraform/environments/${ENVIRONMENT}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Validate environment
if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
    log_error "Invalid environment: $ENVIRONMENT. Must be dev, staging, or production."
    exit 1
fi

# Validate cloud provider
if [[ ! "$CLOUD_PROVIDER" =~ ^(aws|azure|gcp)$ ]]; then
    log_error "Invalid cloud provider: $CLOUD_PROVIDER. Must be aws, azure, or gcp."
    exit 1
fi

log_info "Provisioning Honua infrastructure"
log_info "Environment: $ENVIRONMENT"
log_info "Cloud Provider: $CLOUD_PROVIDER"
echo ""

# Check if Terraform directory exists
if [ ! -d "$TERRAFORM_DIR" ]; then
    log_error "Terraform directory not found: $TERRAFORM_DIR"
    exit 1
fi

cd "$TERRAFORM_DIR"

# Step 1: Initialize Terraform
log_info "Step 1/6: Initializing Terraform..."
if ! terraform init -upgrade; then
    log_error "Terraform initialization failed"
    exit 1
fi
echo ""

# Step 2: Validate configuration
log_info "Step 2/6: Validating Terraform configuration..."
if ! terraform validate; then
    log_error "Terraform validation failed"
    exit 1
fi
echo ""

# Step 3: Plan infrastructure changes
log_info "Step 3/6: Planning infrastructure changes..."
if ! terraform plan -out=tfplan; then
    log_error "Terraform plan failed"
    exit 1
fi
echo ""

# Step 4: Review plan
log_warn "Please review the Terraform plan above."
read -p "Do you want to proceed with applying these changes? (yes/no): " -r
echo
if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
    log_info "Deployment cancelled by user"
    rm -f tfplan
    exit 0
fi

# Step 5: Apply infrastructure changes
log_info "Step 4/6: Applying infrastructure changes..."
if ! terraform apply tfplan; then
    log_error "Terraform apply failed"
    rm -f tfplan
    exit 1
fi
rm -f tfplan
echo ""

# Step 6: Output important information
log_info "Step 5/6: Retrieving infrastructure outputs..."
terraform output
echo ""

# Step 7: Configure kubectl (for AWS EKS)
if [ "$CLOUD_PROVIDER" = "aws" ]; then
    log_info "Step 6/6: Configuring kubectl for EKS..."
    CLUSTER_NAME=$(terraform output -raw eks_cluster_name 2>/dev/null || echo "")
    AWS_REGION=$(terraform output -json | jq -r '.aws_region.value // "us-east-1"')

    if [ -n "$CLUSTER_NAME" ]; then
        aws eks update-kubeconfig --name "$CLUSTER_NAME" --region "$AWS_REGION"
        log_info "kubectl configured for cluster: $CLUSTER_NAME"
    fi
fi

echo ""
log_info "Infrastructure provisioning completed successfully!"
log_info "Next steps:"
echo "  1. Verify cluster connectivity: kubectl get nodes"
echo "  2. Deploy Honua services: ./deploy-honua.sh $ENVIRONMENT"
echo "  3. Access Grafana dashboard: kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80"
echo ""

# Save deployment metadata
cat > "${SCRIPT_DIR}/../.last-deployment" <<EOF
ENVIRONMENT=$ENVIRONMENT
CLOUD_PROVIDER=$CLOUD_PROVIDER
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
DEPLOYED_BY=$(whoami)
EOF

log_info "Deployment metadata saved to infrastructure/.last-deployment"
