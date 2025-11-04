#!/bin/bash
# Safely destroy Honua infrastructure
# Usage: ./destroy-env.sh <environment>

set -euo pipefail

ENVIRONMENT=${1:-}
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
if [ -z "$ENVIRONMENT" ]; then
    log_error "Usage: $0 <environment>"
    echo "Example: $0 dev"
    exit 1
fi

if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
    log_error "Invalid environment: $ENVIRONMENT. Must be dev, staging, or production."
    exit 1
fi

# Extra protection for production
if [ "$ENVIRONMENT" = "production" ]; then
    log_error "DANGER: You are about to destroy PRODUCTION infrastructure!"
    log_warn "This action is IRREVERSIBLE and will DELETE ALL DATA!"
    echo ""
    read -p "Type 'delete-production' to confirm: " -r
    echo
    if [ "$REPLY" != "delete-production" ]; then
        log_info "Destruction cancelled"
        exit 0
    fi

    log_warn "Final confirmation required"
    read -p "Are you absolutely sure? Type 'YES' in capital letters: " -r
    echo
    if [ "$REPLY" != "YES" ]; then
        log_info "Destruction cancelled"
        exit 0
    fi
fi

log_warn "You are about to destroy the $ENVIRONMENT environment"
echo ""
read -p "Are you sure you want to continue? (yes/no): " -r
echo
if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
    log_info "Destruction cancelled"
    exit 0
fi

# Check if Terraform directory exists
if [ ! -d "$TERRAFORM_DIR" ]; then
    log_error "Terraform directory not found: $TERRAFORM_DIR"
    exit 1
fi

cd "$TERRAFORM_DIR"

# Step 1: Create backup of Terraform state
log_info "Step 1/5: Creating backup of Terraform state..."
STATE_BACKUP_DIR="${SCRIPT_DIR}/../backups/states"
mkdir -p "$STATE_BACKUP_DIR"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

if terraform state pull > "${STATE_BACKUP_DIR}/${ENVIRONMENT}_${TIMESTAMP}.tfstate"; then
    log_info "State backed up to: ${STATE_BACKUP_DIR}/${ENVIRONMENT}_${TIMESTAMP}.tfstate"
else
    log_warn "Failed to backup state, continuing anyway..."
fi
echo ""

# Step 2: Initialize Terraform
log_info "Step 2/5: Initializing Terraform..."
if ! terraform init; then
    log_error "Terraform initialization failed"
    exit 1
fi
echo ""

# Step 3: Disable deletion protection (if any)
log_info "Step 3/5: Checking for deletion-protected resources..."
log_warn "Note: Some resources may have deletion protection enabled"
echo ""

# Step 4: Plan destruction
log_info "Step 4/5: Planning infrastructure destruction..."
if ! terraform plan -destroy -out=destroy.tfplan; then
    log_error "Terraform destroy plan failed"
    exit 1
fi
echo ""

# Review destroy plan
log_warn "Please review the destruction plan above."
read -p "Proceed with destroying these resources? (yes/no): " -r
echo
if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
    log_info "Destruction cancelled"
    rm -f destroy.tfplan
    exit 0
fi

# Step 5: Destroy infrastructure
log_info "Step 5/5: Destroying infrastructure..."
if ! terraform apply destroy.tfplan; then
    log_error "Terraform destroy failed"
    log_warn "Some resources may still exist. Check the AWS/Azure/GCP console."
    rm -f destroy.tfplan
    exit 1
fi
rm -f destroy.tfplan
echo ""

log_info "Infrastructure destruction completed"
log_warn "The following may still need manual cleanup:"
echo "  - S3 buckets with versioning enabled"
echo "  - RDS snapshots"
echo "  - CloudWatch log groups"
echo "  - Backup vault recovery points"
echo ""

# Update deployment metadata
if [ -f "${SCRIPT_DIR}/../.last-deployment" ]; then
    mv "${SCRIPT_DIR}/../.last-deployment" "${SCRIPT_DIR}/../.last-deployment.destroyed"
    log_info "Moved deployment metadata to .last-deployment.destroyed"
fi

log_info "Destruction of $ENVIRONMENT environment completed"
