#!/bin/bash
# AWS Marketplace Deployment Script for Honua IO Server

set -e

# Configuration
STACK_NAME="${STACK_NAME:-honua-server}"
AWS_REGION="${AWS_REGION:-us-east-1}"
CLUSTER_NAME="${CLUSTER_NAME:-honua-server}"
LICENSE_TIER="${LICENSE_TIER:-Professional}"
NODE_INSTANCE_TYPE="${NODE_INSTANCE_TYPE:-t3.large}"
NODE_COUNT="${NODE_COUNT:-2}"
MIN_NODE_COUNT="${MIN_NODE_COUNT:-2}"
MAX_NODE_COUNT="${MAX_NODE_COUNT:-10}"
DATABASE_INSTANCE_CLASS="${DATABASE_INSTANCE_CLASS:-db.t3.medium}"
DATABASE_STORAGE="${DATABASE_STORAGE:-100}"
REDIS_NODE_TYPE="${REDIS_NODE_TYPE:-cache.t3.medium}"
MARKETPLACE_PRODUCT_CODE="${MARKETPLACE_PRODUCT_CODE}"
IMAGE_TAG="${IMAGE_TAG:-latest}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    # Check AWS CLI
    if ! command -v aws &> /dev/null; then
        log_error "AWS CLI is not installed. Please install it first."
        exit 1
    fi

    # Check kubectl
    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl is not installed. Please install it first."
        exit 1
    fi

    # Check AWS credentials
    if ! aws sts get-caller-identity &> /dev/null; then
        log_error "AWS credentials are not configured. Please run 'aws configure'."
        exit 1
    fi

    log_info "All prerequisites met."
}

# Deploy CloudFormation stack
deploy_infrastructure() {
    log_info "Deploying infrastructure with CloudFormation..."

    aws cloudformation create-stack \
        --stack-name "$STACK_NAME" \
        --template-body file://$(dirname "$0")/../templates/eks-deployment.yaml \
        --parameters \
            ParameterKey=ClusterName,ParameterValue="$CLUSTER_NAME" \
            ParameterKey=LicenseTier,ParameterValue="$LICENSE_TIER" \
            ParameterKey=NodeInstanceType,ParameterValue="$NODE_INSTANCE_TYPE" \
            ParameterKey=NodeGroupDesiredSize,ParameterValue="$NODE_COUNT" \
            ParameterKey=NodeGroupMinSize,ParameterValue="$MIN_NODE_COUNT" \
            ParameterKey=NodeGroupMaxSize,ParameterValue="$MAX_NODE_COUNT" \
            ParameterKey=DatabaseInstanceClass,ParameterValue="$DATABASE_INSTANCE_CLASS" \
            ParameterKey=DatabaseAllocatedStorage,ParameterValue="$DATABASE_STORAGE" \
            ParameterKey=RedisNodeType,ParameterValue="$REDIS_NODE_TYPE" \
            ParameterKey=MarketplaceProductCode,ParameterValue="$MARKETPLACE_PRODUCT_CODE" \
        --capabilities CAPABILITY_IAM \
        --region "$AWS_REGION"

    log_info "Waiting for CloudFormation stack to complete..."
    aws cloudformation wait stack-create-complete \
        --stack-name "$STACK_NAME" \
        --region "$AWS_REGION"

    log_info "Infrastructure deployment completed successfully."
}

# Get CloudFormation outputs
get_stack_outputs() {
    log_info "Retrieving stack outputs..."

    DB_ENDPOINT=$(aws cloudformation describe-stacks \
        --stack-name "$STACK_NAME" \
        --query 'Stacks[0].Outputs[?OutputKey==`DatabaseEndpoint`].OutputValue' \
        --output text \
        --region "$AWS_REGION")

    DB_PORT=$(aws cloudformation describe-stacks \
        --stack-name "$STACK_NAME" \
        --query 'Stacks[0].Outputs[?OutputKey==`DatabasePort`].OutputValue' \
        --output text \
        --region "$AWS_REGION")

    REDIS_ENDPOINT=$(aws cloudformation describe-stacks \
        --stack-name "$STACK_NAME" \
        --query 'Stacks[0].Outputs[?OutputKey==`RedisEndpoint`].OutputValue' \
        --output text \
        --region "$AWS_REGION")

    REDIS_PORT=$(aws cloudformation describe-stacks \
        --stack-name "$STACK_NAME" \
        --query 'Stacks[0].Outputs[?OutputKey==`RedisPort`].OutputValue' \
        --output text \
        --region "$AWS_REGION")

    S3_BUCKET=$(aws cloudformation describe-stacks \
        --stack-name "$STACK_NAME" \
        --query 'Stacks[0].Outputs[?OutputKey==`S3BucketName`].OutputValue' \
        --output text \
        --region "$AWS_REGION")

    SERVICE_ACCOUNT_ROLE=$(aws cloudformation describe-stacks \
        --stack-name "$STACK_NAME" \
        --query 'Stacks[0].Outputs[?OutputKey==`ServiceAccountRoleArn`].OutputValue' \
        --output text \
        --region "$AWS_REGION")

    DB_PASSWORD_SECRET=$(aws cloudformation describe-stacks \
        --stack-name "$STACK_NAME" \
        --query 'Stacks[0].Outputs[?OutputKey==`DatabasePasswordSecretArn`].OutputValue' \
        --output text \
        --region "$AWS_REGION")

    log_info "Stack outputs retrieved successfully."
}

# Configure kubectl
configure_kubectl() {
    log_info "Configuring kubectl for EKS cluster..."

    aws eks update-kubeconfig \
        --name "$CLUSTER_NAME" \
        --region "$AWS_REGION"

    # Verify connection
    if kubectl get nodes &> /dev/null; then
        log_info "kubectl configured successfully."
    else
        log_error "Failed to connect to EKS cluster."
        exit 1
    fi
}

# Deploy Honua Server application
deploy_application() {
    log_info "Deploying Honua Server application..."

    # Get database password from Secrets Manager
    DB_PASSWORD=$(aws secretsmanager get-secret-value \
        --secret-id "$DB_PASSWORD_SECRET" \
        --query 'SecretString' \
        --output text \
        --region "$AWS_REGION" | jq -r '.password')

    # Export variables for envsubst
    export DATABASE_ENDPOINT="$DB_ENDPOINT"
    export DATABASE_PORT="$DB_PORT"
    export REDIS_ENDPOINT="$REDIS_ENDPOINT"
    export REDIS_PORT="$REDIS_PORT"
    export S3_BUCKET_NAME="$S3_BUCKET"
    export SERVICE_ACCOUNT_ROLE_ARN="$SERVICE_ACCOUNT_ROLE"
    export DATABASE_PASSWORD="$DB_PASSWORD"
    export AWS_REGION="$AWS_REGION"
    export MARKETPLACE_PRODUCT_CODE="$MARKETPLACE_PRODUCT_CODE"
    export LICENSE_TIER="$LICENSE_TIER"
    export IMAGE_TAG="$IMAGE_TAG"
    export ACM_CERTIFICATE_ARN="${ACM_CERTIFICATE_ARN:-}"

    # Apply Kubernetes manifests
    envsubst < "$(dirname "$0")/../templates/kubernetes-manifest.yaml" | kubectl apply -f -

    log_info "Waiting for deployment to be ready..."
    kubectl wait --for=condition=available --timeout=600s \
        deployment/honua-server -n honua-system

    log_info "Application deployed successfully."
}

# Get service endpoint
get_service_endpoint() {
    log_info "Retrieving service endpoint..."

    # Wait for LoadBalancer to be provisioned
    local max_attempts=30
    local attempt=0

    while [ $attempt -lt $max_attempts ]; do
        HONUA_URL=$(kubectl get service honua-server -n honua-system \
            -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>/dev/null)

        if [ -n "$HONUA_URL" ]; then
            log_info "Honua Server is available at: http://$HONUA_URL"
            echo ""
            echo "========================================="
            echo "Deployment Summary"
            echo "========================================="
            echo "Cluster Name: $CLUSTER_NAME"
            echo "Region: $AWS_REGION"
            echo "License Tier: $LICENSE_TIER"
            echo "Service URL: http://$HONUA_URL"
            echo "========================================="
            return 0
        fi

        attempt=$((attempt + 1))
        log_info "Waiting for LoadBalancer... (attempt $attempt/$max_attempts)"
        sleep 10
    done

    log_warn "LoadBalancer endpoint not available yet. Check with: kubectl get service honua-server -n honua-system"
}

# Main deployment flow
main() {
    log_info "Starting Honua IO Server deployment to AWS Marketplace..."

    check_prerequisites
    deploy_infrastructure
    get_stack_outputs
    configure_kubectl
    deploy_application
    get_service_endpoint

    log_info "Deployment completed successfully!"
}

# Run main function
main "$@"
