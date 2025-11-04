#!/bin/bash
# ============================================================================
# Multi-Region Deployment Validation Script
# ============================================================================
# This script validates the multi-region deployment configuration
# ============================================================================

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

check_command() {
    if ! command -v "$1" &> /dev/null; then
        log_error "$1 is not installed"
        return 1
    fi
    log_info "$1 is installed"
    return 0
}

# Main validation
main() {
    log_info "Starting multi-region deployment validation..."
    echo ""

    # Check required commands
    log_info "Checking required commands..."
    check_command terraform
    check_command aws || check_command az || check_command gcloud
    check_command jq
    echo ""

    # Validate Terraform files
    log_info "Validating Terraform configuration..."
    if terraform validate; then
        log_info "Terraform configuration is valid"
    else
        log_error "Terraform configuration validation failed"
        exit 1
    fi
    echo ""

    # Check for required variables
    log_info "Checking for required variables..."
    if [ ! -f "terraform.tfvars" ]; then
        log_warn "terraform.tfvars not found. Copy terraform.tfvars.example to terraform.tfvars"
        exit 1
    fi

    # Validate cloud provider
    CLOUD_PROVIDER=$(grep '^cloud_provider' terraform.tfvars | cut -d'=' -f2 | tr -d ' "')
    log_info "Cloud provider: $CLOUD_PROVIDER"

    # Validate regions
    PRIMARY_REGION=$(grep '^primary_region' terraform.tfvars | cut -d'=' -f2 | tr -d ' "')
    DR_REGION=$(grep '^dr_region' terraform.tfvars | cut -d'=' -f2 | tr -d ' "')
    log_info "Primary region: $PRIMARY_REGION"
    log_info "DR region: $DR_REGION"

    if [ "$PRIMARY_REGION" == "$DR_REGION" ]; then
        log_error "Primary and DR regions must be different"
        exit 1
    fi
    echo ""

    # Run terraform plan
    log_info "Running terraform plan..."
    if terraform plan -out=tfplan; then
        log_info "Terraform plan succeeded"
    else
        log_error "Terraform plan failed"
        exit 1
    fi
    echo ""

    # Show plan summary
    log_info "Plan summary:"
    terraform show -json tfplan | jq -r '
        .resource_changes |
        group_by(.change.actions[0]) |
        map({
            action: .[0].change.actions[0],
            count: length
        }) |
        .[] |
        "\(.action): \(.count) resources"
    '
    echo ""

    log_info "Validation complete!"
    log_info "To apply this configuration, run: terraform apply tfplan"
}

main "$@"
