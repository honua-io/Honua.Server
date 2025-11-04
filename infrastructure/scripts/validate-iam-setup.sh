#!/bin/bash
#
# Honua Build Orchestrator IAM Setup Validation Script
#
# This script validates that all required IAM infrastructure is properly
# configured across AWS, Azure, and GCP.
#
# Usage:
#   ./validate-iam-setup.sh [--provider aws|azure|gcp|all]
#
# Environment variables:
#   AWS_ORCHESTRATOR_ROLE_ARN - ARN of the AWS orchestrator IAM role
#   AZURE_CLIENT_ID - Client ID of the Azure managed identity
#   AZURE_RESOURCE_GROUP - Azure resource group name
#   GCP_PROJECT_ID - GCP project ID
#   GCP_SERVICE_ACCOUNT - GCP service account email
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print success
success() {
    echo -e "${GREEN}✓${NC} $1"
}

# Function to print error
error() {
    echo -e "${RED}✗${NC} $1"
}

# Function to print warning
warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Function to print info
info() {
    echo -e "${NC}ℹ${NC} $1"
}

# Parse command line arguments
PROVIDER="${1:-all}"
if [[ "$PROVIDER" == "--provider" ]]; then
    PROVIDER="${2:-all}"
fi

# Validate AWS infrastructure
validate_aws() {
    echo ""
    info "Validating AWS infrastructure..."
    echo ""

    # Check AWS CLI is installed
    if ! command -v aws &> /dev/null; then
        error "AWS CLI is not installed"
        return 1
    fi
    success "AWS CLI is installed"

    # Check AWS credentials
    if ! aws sts get-caller-identity &> /dev/null; then
        error "AWS credentials not configured or invalid"
        return 1
    fi
    success "AWS credentials are valid"

    ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
    info "AWS Account ID: $ACCOUNT_ID"

    # Check orchestrator IAM role
    if aws iam get-role --role-name honua-build-orchestrator &> /dev/null; then
        success "Orchestrator IAM role exists"

        # Check policy attachment
        POLICY_ARN="arn:aws:iam::$ACCOUNT_ID:policy/honua-build-orchestrator-policy"
        if aws iam get-policy --policy-arn "$POLICY_ARN" &> /dev/null; then
            success "Orchestrator IAM policy exists"

            # Check if policy is attached to role
            if aws iam list-attached-role-policies --role-name honua-build-orchestrator | grep -q "$POLICY_ARN"; then
                success "Policy is attached to role"
            else
                error "Policy is not attached to role"
            fi
        else
            error "Orchestrator IAM policy does not exist"
        fi
    else
        error "Orchestrator IAM role does not exist"
    fi

    # Check KMS key
    if aws kms describe-alias --alias-name alias/honua/build-orchestrator &> /dev/null; then
        success "KMS key exists"
    else
        warning "KMS key does not exist (will be created by Terraform)"
    fi

    # Check Secrets Manager secret
    if aws secretsmanager describe-secret --secret-id honua/github-pat-build-orchestrator &> /dev/null; then
        success "GitHub PAT secret exists in Secrets Manager"

        # Check if secret has a value
        if aws secretsmanager get-secret-value --secret-id honua/github-pat-build-orchestrator &> /dev/null; then
            success "GitHub PAT secret has a value"
        else
            warning "GitHub PAT secret exists but has no value"
        fi
    else
        warning "GitHub PAT secret does not exist (will be created by Terraform)"
    fi

    # Check ECR permissions (attempt to create a test repository)
    info "Testing ECR permissions..."
    if aws ecr create-repository --repository-name honua/test-validation-repo &> /dev/null; then
        success "Can create ECR repositories"
        aws ecr delete-repository --repository-name honua/test-validation-repo --force &> /dev/null
        success "Can delete ECR repositories"
    else
        # Repository might already exist or permission issue
        if aws ecr describe-repositories --repository-names honua/test-validation-repo &> /dev/null; then
            warning "Test repository already exists (manual cleanup needed)"
        else
            error "Cannot create ECR repositories (permission issue)"
        fi
    fi

    echo ""
}

# Validate Azure infrastructure
validate_azure() {
    echo ""
    info "Validating Azure infrastructure..."
    echo ""

    # Check Azure CLI is installed
    if ! command -v az &> /dev/null; then
        error "Azure CLI is not installed"
        return 1
    fi
    success "Azure CLI is installed"

    # Check Azure login
    if ! az account show &> /dev/null; then
        error "Not logged in to Azure (run 'az login')"
        return 1
    fi
    success "Logged in to Azure"

    SUBSCRIPTION_ID=$(az account show --query id --output tsv)
    info "Azure Subscription ID: $SUBSCRIPTION_ID"

    # Check resource group
    RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-honua-registries-production}"
    if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
        success "Resource group '$RESOURCE_GROUP' exists"
    else
        warning "Resource group '$RESOURCE_GROUP' does not exist (will be created by Terraform)"
    fi

    # Check managed identity
    if az identity show --name honua-build-orchestrator --resource-group "$RESOURCE_GROUP" &> /dev/null; then
        success "Managed identity 'honua-build-orchestrator' exists"

        CLIENT_ID=$(az identity show --name honua-build-orchestrator --resource-group "$RESOURCE_GROUP" --query clientId --output tsv)
        info "Managed Identity Client ID: $CLIENT_ID"
    else
        warning "Managed identity does not exist (will be created by Terraform)"
    fi

    # Check custom role definition
    if az role definition list --name "Honua Build Orchestrator" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
        success "Custom role definition 'Honua Build Orchestrator' exists"
    else
        warning "Custom role definition does not exist (will be created by Terraform)"
    fi

    # Check Key Vault
    if az keyvault show --name honua-orchestrator-kv &> /dev/null; then
        success "Key Vault 'honua-orchestrator-kv' exists"

        # Check GitHub PAT secret
        if az keyvault secret show --vault-name honua-orchestrator-kv --name github-pat &> /dev/null; then
            success "GitHub PAT secret exists in Key Vault"
        else
            warning "GitHub PAT secret does not exist in Key Vault"
        fi
    else
        warning "Key Vault does not exist (will be created by Terraform)"
    fi

    echo ""
}

# Validate GCP infrastructure
validate_gcp() {
    echo ""
    info "Validating GCP infrastructure..."
    echo ""

    # Check gcloud CLI is installed
    if ! command -v gcloud &> /dev/null; then
        error "gcloud CLI is not installed"
        return 1
    fi
    success "gcloud CLI is installed"

    # Check gcloud authentication
    if ! gcloud auth list --filter=status:ACTIVE --format="value(account)" &> /dev/null; then
        error "Not authenticated with gcloud (run 'gcloud auth login')"
        return 1
    fi
    success "Authenticated with gcloud"

    # Get project ID
    PROJECT_ID="${GCP_PROJECT_ID:-$(gcloud config get-value project 2>/dev/null)}"
    if [[ -z "$PROJECT_ID" ]]; then
        error "GCP project ID not set (use --project or set default project)"
        return 1
    fi
    info "GCP Project ID: $PROJECT_ID"

    # Check service account
    SERVICE_ACCOUNT="${GCP_SERVICE_ACCOUNT:-honua-build-orchestrator@$PROJECT_ID.iam.gserviceaccount.com}"
    if gcloud iam service-accounts describe "$SERVICE_ACCOUNT" --project="$PROJECT_ID" &> /dev/null; then
        success "Service account '$SERVICE_ACCOUNT' exists"
    else
        warning "Service account does not exist (will be created by Terraform)"
    fi

    # Check custom IAM role
    if gcloud iam roles describe honuaBuildOrchestrator --project="$PROJECT_ID" &> /dev/null; then
        success "Custom IAM role 'honuaBuildOrchestrator' exists"
    else
        warning "Custom IAM role does not exist (will be created by Terraform)"
    fi

    # Check IAM bindings
    if gcloud projects get-iam-policy "$PROJECT_ID" --flatten="bindings[].members" --filter="bindings.members:$SERVICE_ACCOUNT" &> /dev/null; then
        success "Service account has IAM bindings"
    else
        warning "Service account does not have IAM bindings"
    fi

    # Check Secret Manager secret
    if gcloud secrets describe honua-github-pat-build-orchestrator --project="$PROJECT_ID" &> /dev/null; then
        success "GitHub PAT secret exists in Secret Manager"

        # Check if secret has versions
        if gcloud secrets versions list honua-github-pat-build-orchestrator --project="$PROJECT_ID" --limit=1 &> /dev/null; then
            success "GitHub PAT secret has versions"
        else
            warning "GitHub PAT secret exists but has no versions"
        fi
    else
        warning "GitHub PAT secret does not exist (will be created by Terraform)"
    fi

    # Check Artifact Registry API is enabled
    if gcloud services list --enabled --filter="artifactregistry.googleapis.com" --project="$PROJECT_ID" &> /dev/null; then
        success "Artifact Registry API is enabled"
    else
        warning "Artifact Registry API is not enabled"
    fi

    # Check Secret Manager API is enabled
    if gcloud services list --enabled --filter="secretmanager.googleapis.com" --project="$PROJECT_ID" &> /dev/null; then
        success "Secret Manager API is enabled"
    else
        warning "Secret Manager API is not enabled"
    fi

    echo ""
}

# Main validation logic
echo "========================================"
echo "Honua Build Orchestrator IAM Validation"
echo "========================================"

case "$PROVIDER" in
    aws)
        validate_aws
        ;;
    azure)
        validate_azure
        ;;
    gcp)
        validate_gcp
        ;;
    all)
        validate_aws
        validate_azure
        validate_gcp
        ;;
    *)
        error "Invalid provider: $PROVIDER"
        echo "Usage: $0 [--provider aws|azure|gcp|all]"
        exit 1
        ;;
esac

echo "========================================"
echo "Validation complete"
echo "========================================"
echo ""
info "Next steps:"
echo "  1. Review any warnings or errors above"
echo "  2. If resources are missing, run 'terraform apply'"
echo "  3. Populate GitHub PAT in all secret managers"
echo "  4. Test orchestrator permissions with test builds"
echo ""
