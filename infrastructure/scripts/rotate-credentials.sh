#!/bin/bash
# Rotate customer and service credentials
# Usage: ./rotate-credentials.sh <environment> [customer-id]

set -euo pipefail

ENVIRONMENT=${1:-}
CUSTOMER_ID=${2:-}
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

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

# Validate arguments
if [ -z "$ENVIRONMENT" ]; then
    log_error "Usage: $0 <environment> [customer-id]"
    echo "Example: $0 production customer-123"
    exit 1
fi

if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
    log_error "Invalid environment: $ENVIRONMENT. Must be dev, staging, or production."
    exit 1
fi

log_info "Rotating credentials for environment: $ENVIRONMENT"
if [ -n "$CUSTOMER_ID" ]; then
    log_info "Customer ID: $CUSTOMER_ID"
fi
echo ""

# Detect cloud provider
CLOUD_PROVIDER="aws"
if [ -f "${SCRIPT_DIR}/../.last-deployment" ]; then
    source "${SCRIPT_DIR}/../.last-deployment"
fi

log_info "Cloud Provider: $CLOUD_PROVIDER"
echo ""

# AWS credential rotation
if [ "$CLOUD_PROVIDER" = "aws" ]; then
    if [ -n "$CUSTOMER_ID" ]; then
        # Rotate specific customer credentials
        log_info "Rotating credentials for customer: $CUSTOMER_ID"
        IAM_USER="honua-customer-${CUSTOMER_ID}"

        # List existing access keys
        log_info "Listing existing access keys..."
        KEYS=$(aws iam list-access-keys --user-name "$IAM_USER" --query 'AccessKeyMetadata[*].AccessKeyId' --output text)

        if [ -z "$KEYS" ]; then
            log_error "No access keys found for user: $IAM_USER"
            exit 1
        fi

        # Create new access key
        log_info "Creating new access key..."
        NEW_KEY_JSON=$(aws iam create-access-key --user-name "$IAM_USER")
        NEW_ACCESS_KEY_ID=$(echo "$NEW_KEY_JSON" | jq -r '.AccessKey.AccessKeyId')
        NEW_SECRET_ACCESS_KEY=$(echo "$NEW_KEY_JSON" | jq -r '.AccessKey.SecretAccessKey')

        log_info "New access key created: $NEW_ACCESS_KEY_ID"

        # Update Secrets Manager
        SECRET_NAME="honua-customer-${CUSTOMER_ID}-credentials-${ENVIRONMENT}"
        REPO_NAME="honua-customer-${CUSTOMER_ID}"

        aws secretsmanager update-secret \
            --secret-id "$SECRET_NAME" \
            --secret-string "{\"access_key_id\":\"${NEW_ACCESS_KEY_ID}\",\"secret_access_key\":\"${NEW_SECRET_ACCESS_KEY}\",\"repository\":\"${REPO_NAME}\",\"rotated_at\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"

        log_info "Credentials updated in Secrets Manager: $SECRET_NAME"

        # Output new credentials
        echo ""
        log_info "New credentials:"
        echo "=========================================="
        echo "Access Key ID: $NEW_ACCESS_KEY_ID"
        echo "Secret Access Key: $NEW_SECRET_ACCESS_KEY"
        echo "=========================================="
        log_warn "IMPORTANT: Provide these credentials to the customer"
        log_warn "Old credentials will remain active for 24 hours for transition period"

        # Schedule deletion of old keys (after 24 hours manually)
        echo ""
        log_warn "To deactivate old keys after customer transitions:"
        for KEY in $KEYS; do
            echo "  aws iam delete-access-key --user-name $IAM_USER --access-key-id $KEY"
        done

    else
        # Rotate all service credentials
        log_info "Rotating all service credentials..."
        log_warn "This will rotate credentials for all Honua services"

        read -p "Continue? (yes/no): " -r
        echo
        if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
            log_info "Rotation cancelled"
            exit 0
        fi

        # List all customer IAM users
        USERS=$(aws iam list-users --path-prefix "/customers/" --query 'Users[?starts_with(UserName, `honua-customer-`)].UserName' --output text)

        for USER in $USERS; do
            log_info "Processing user: $USER"
            CUSTOMER=$(echo "$USER" | sed 's/honua-customer-//')

            # Rotate this customer's credentials
            bash "$0" "$ENVIRONMENT" "$CUSTOMER"

            echo ""
        done

        log_info "All credentials rotated successfully"
    fi
fi

echo ""
log_info "Credential rotation completed"
