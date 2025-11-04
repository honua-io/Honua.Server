#!/bin/bash
# Provision customer-specific resources (registry, IAM, credentials)
# Usage: ./provision-customer.sh <environment> <customer-id> <customer-name>

set -euo pipefail

ENVIRONMENT=${1:-}
CUSTOMER_ID=${2:-}
CUSTOMER_NAME=${3:-}
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
if [ -z "$ENVIRONMENT" ] || [ -z "$CUSTOMER_ID" ] || [ -z "$CUSTOMER_NAME" ]; then
    log_error "Usage: $0 <environment> <customer-id> <customer-name>"
    echo "Example: $0 production customer-123 AcmeCorp"
    exit 1
fi

if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
    log_error "Invalid environment: $ENVIRONMENT. Must be dev, staging, or production."
    exit 1
fi

log_info "Provisioning customer resources"
log_info "Environment: $ENVIRONMENT"
log_info "Customer ID: $CUSTOMER_ID"
log_info "Customer Name: $CUSTOMER_NAME"
echo ""

# Detect cloud provider from last deployment
CLOUD_PROVIDER="aws"
if [ -f "${SCRIPT_DIR}/../.last-deployment" ]; then
    source "${SCRIPT_DIR}/../.last-deployment"
fi

log_info "Cloud Provider: $CLOUD_PROVIDER"
echo ""

# AWS-specific provisioning
if [ "$CLOUD_PROVIDER" = "aws" ]; then
    log_info "Creating AWS resources for customer..."

    # Step 1: Create ECR repository
    log_info "Step 1/4: Creating ECR repository..."
    REPO_NAME="honua-customer-${CUSTOMER_ID}"
    if aws ecr describe-repositories --repository-names "$REPO_NAME" &>/dev/null; then
        log_warn "ECR repository already exists: $REPO_NAME"
    else
        aws ecr create-repository \
            --repository-name "$REPO_NAME" \
            --image-scanning-configuration scanOnPush=true \
            --encryption-configuration encryptionType=KMS \
            --tags Key=Customer,Value="$CUSTOMER_NAME" Key=Environment,Value="$ENVIRONMENT"
        log_info "Created ECR repository: $REPO_NAME"
    fi
    echo ""

    # Step 2: Create IAM user
    log_info "Step 2/4: Creating IAM user..."
    IAM_USER="honua-customer-${CUSTOMER_ID}"
    if aws iam get-user --user-name "$IAM_USER" &>/dev/null; then
        log_warn "IAM user already exists: $IAM_USER"
    else
        aws iam create-user --user-name "$IAM_USER" --tags Key=Customer,Value="$CUSTOMER_NAME"
        log_info "Created IAM user: $IAM_USER"
    fi
    echo ""

    # Step 3: Attach policy to IAM user
    log_info "Step 3/4: Attaching IAM policy..."
    POLICY_NAME="${IAM_USER}-policy"
    POLICY_DOCUMENT=$(cat <<EOF
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "ecr:GetAuthorizationToken"
            ],
            "Resource": "*"
        },
        {
            "Effect": "Allow",
            "Action": [
                "ecr:GetDownloadUrlForLayer",
                "ecr:BatchGetImage",
                "ecr:BatchCheckLayerAvailability"
            ],
            "Resource": "arn:aws:ecr:*:*:repository/${REPO_NAME}"
        }
    ]
}
EOF
)

    POLICY_ARN=$(aws iam create-policy \
        --policy-name "$POLICY_NAME" \
        --policy-document "$POLICY_DOCUMENT" \
        --query 'Policy.Arn' \
        --output text 2>/dev/null || echo "")

    if [ -z "$POLICY_ARN" ]; then
        # Policy might already exist, get its ARN
        ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
        POLICY_ARN="arn:aws:iam::${ACCOUNT_ID}:policy/${POLICY_NAME}"
        log_warn "IAM policy already exists: $POLICY_NAME"
    else
        log_info "Created IAM policy: $POLICY_NAME"
    fi

    aws iam attach-user-policy --user-name "$IAM_USER" --policy-arn "$POLICY_ARN" || true
    echo ""

    # Step 4: Create access key
    log_info "Step 4/4: Creating access key..."
    ACCESS_KEY_JSON=$(aws iam create-access-key --user-name "$IAM_USER" 2>/dev/null || echo "")

    if [ -n "$ACCESS_KEY_JSON" ]; then
        ACCESS_KEY_ID=$(echo "$ACCESS_KEY_JSON" | jq -r '.AccessKey.AccessKeyId')
        SECRET_ACCESS_KEY=$(echo "$ACCESS_KEY_JSON" | jq -r '.AccessKey.SecretAccessKey')

        # Store credentials in AWS Secrets Manager
        SECRET_NAME="honua-customer-${CUSTOMER_ID}-credentials-${ENVIRONMENT}"
        aws secretsmanager create-secret \
            --name "$SECRET_NAME" \
            --secret-string "{\"access_key_id\":\"${ACCESS_KEY_ID}\",\"secret_access_key\":\"${SECRET_ACCESS_KEY}\",\"repository\":\"${REPO_NAME}\"}" \
            --tags Key=Customer,Value="$CUSTOMER_NAME" Key=Environment,Value="$ENVIRONMENT" \
            &>/dev/null || \
        aws secretsmanager update-secret \
            --secret-id "$SECRET_NAME" \
            --secret-string "{\"access_key_id\":\"${ACCESS_KEY_ID}\",\"secret_access_key\":\"${SECRET_ACCESS_KEY}\",\"repository\":\"${REPO_NAME}\"}" \
            &>/dev/null

        log_info "Credentials stored in Secrets Manager: $SECRET_NAME"
        echo ""

        # Output credentials (for initial setup only)
        log_info "Customer credentials created:"
        echo "=========================================="
        echo "Customer ID: $CUSTOMER_ID"
        echo "Customer Name: $CUSTOMER_NAME"
        echo "ECR Repository: $REPO_NAME"
        echo "IAM User: $IAM_USER"
        echo "Access Key ID: $ACCESS_KEY_ID"
        echo "Secret Access Key: $SECRET_ACCESS_KEY"
        echo "=========================================="
        log_warn "IMPORTANT: Store these credentials securely!"
        log_warn "They are also available in AWS Secrets Manager: $SECRET_NAME"
    else
        log_error "Failed to create access key. User may already have maximum number of keys."
        exit 1
    fi
fi

echo ""
log_info "Customer provisioning completed successfully!"
log_info "Customer $CUSTOMER_NAME ($CUSTOMER_ID) can now use Honua build orchestration"
