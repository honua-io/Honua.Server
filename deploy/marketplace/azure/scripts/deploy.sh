#!/bin/bash
# Azure Marketplace Deployment Script for Honua IO Server

set -e

# Configuration
RESOURCE_GROUP="${RESOURCE_GROUP:-honua-rg}"
LOCATION="${LOCATION:-eastus}"
DEPLOYMENT_NAME="${DEPLOYMENT_NAME:-honua-deployment}"
CLUSTER_NAME="${CLUSTER_NAME:-honua-server}"
LICENSE_TIER="${LICENSE_TIER:-Professional}"
NODE_COUNT="${NODE_COUNT:-2}"
NODE_VM_SIZE="${NODE_VM_SIZE:-Standard_D4s_v3}"
POSTGRES_SKU="${POSTGRES_SKU:-Standard_D4s_v3}"
POSTGRES_STORAGE="${POSTGRES_STORAGE:-128}"
REDIS_SKU="${REDIS_SKU:-Standard}"
REDIS_CAPACITY="${REDIS_CAPACITY:-1}"
MARKETPLACE_OFFER_ID="${MARKETPLACE_OFFER_ID}"
MARKETPLACE_PLAN_ID="${MARKETPLACE_PLAN_ID}"
IMAGE_TAG="${IMAGE_TAG:-latest}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    if ! command -v az &> /dev/null; then
        log_error "Azure CLI is not installed."
        exit 1
    fi

    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl is not installed."
        exit 1
    fi

    if ! az account show &> /dev/null; then
        log_error "Not logged in to Azure. Run 'az login'."
        exit 1
    fi

    log_info "All prerequisites met."
}

# Create resource group
create_resource_group() {
    log_info "Creating resource group..."

    az group create \
        --name "$RESOURCE_GROUP" \
        --location "$LOCATION"

    log_info "Resource group created: $RESOURCE_GROUP"
}

# Generate secure password
generate_password() {
    openssl rand -base64 32 | tr -d "=+/" | cut -c1-25
}

# Deploy ARM template
deploy_infrastructure() {
    log_info "Deploying infrastructure with ARM template..."

    DB_PASSWORD=$(generate_password)

    az deployment group create \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DEPLOYMENT_NAME" \
        --template-file "$(dirname "$0")/../templates/aks-deployment.json" \
        --parameters \
            clusterName="$CLUSTER_NAME" \
            location="$LOCATION" \
            nodeCount="$NODE_COUNT" \
            nodeVmSize="$NODE_VM_SIZE" \
            licenseTier="$LICENSE_TIER" \
            postgresSku="$POSTGRES_SKU" \
            postgresStorageSize="$POSTGRES_STORAGE" \
            redisSku="$REDIS_SKU" \
            redisCapacity="$REDIS_CAPACITY" \
            azureMarketplaceOfferId="$MARKETPLACE_OFFER_ID" \
            azureMarketplacePlanId="$MARKETPLACE_PLAN_ID" \
            databaseAdminPassword="$DB_PASSWORD"

    log_info "Infrastructure deployment completed."
}

# Get deployment outputs
get_deployment_outputs() {
    log_info "Retrieving deployment outputs..."

    POSTGRES_SERVER=$(az deployment group show \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DEPLOYMENT_NAME" \
        --query 'properties.outputs.postgresServerFqdn.value' \
        --output tsv)

    REDIS_HOST=$(az deployment group show \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DEPLOYMENT_NAME" \
        --query 'properties.outputs.redisHostName.value' \
        --output tsv)

    REDIS_PORT=$(az deployment group show \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DEPLOYMENT_NAME" \
        --query 'properties.outputs.redisSslPort.value' \
        --output tsv)

    STORAGE_ACCOUNT=$(az deployment group show \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DEPLOYMENT_NAME" \
        --query 'properties.outputs.storageAccountName.value' \
        --output tsv)

    KEY_VAULT=$(az deployment group show \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DEPLOYMENT_NAME" \
        --query 'properties.outputs.keyVaultName.value' \
        --output tsv)

    MANAGED_IDENTITY_CLIENT_ID=$(az deployment group show \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DEPLOYMENT_NAME" \
        --query 'properties.outputs.managedIdentityClientId.value' \
        --output tsv)

    log_info "Deployment outputs retrieved successfully."
}

# Configure kubectl
configure_kubectl() {
    log_info "Configuring kubectl for AKS cluster..."

    az aks get-credentials \
        --resource-group "$RESOURCE_GROUP" \
        --name "$CLUSTER_NAME" \
        --overwrite-existing

    if kubectl get nodes &> /dev/null; then
        log_info "kubectl configured successfully."
    else
        log_error "Failed to connect to AKS cluster."
        exit 1
    fi
}

# Deploy application
deploy_application() {
    log_info "Deploying Honua Server application..."

    # Get Redis access key
    REDIS_CACHE_NAME=$(echo "$REDIS_HOST" | cut -d'.' -f1)
    REDIS_KEY=$(az redis list-keys \
        --resource-group "$RESOURCE_GROUP" \
        --name "$REDIS_CACHE_NAME" \
        --query 'primaryKey' \
        --output tsv)

    # Create namespace
    kubectl create namespace honua-system --dry-run=client -o yaml | kubectl apply -f -

    # Create ConfigMap
    kubectl create configmap honua-config \
        --namespace=honua-system \
        --from-literal=ASPNETCORE_ENVIRONMENT=Production \
        --from-literal=DATABASE_HOST="$POSTGRES_SERVER" \
        --from-literal=DATABASE_PORT=5432 \
        --from-literal=REDIS_HOST="$REDIS_HOST" \
        --from-literal=REDIS_PORT="$REDIS_PORT" \
        --from-literal=STORAGE_ACCOUNT="$STORAGE_ACCOUNT" \
        --from-literal=KEY_VAULT_NAME="$KEY_VAULT" \
        --from-literal=MANAGED_IDENTITY_CLIENT_ID="$MANAGED_IDENTITY_CLIENT_ID" \
        --from-literal=LICENSE_TIER="$LICENSE_TIER" \
        --from-literal=MARKETPLACE_OFFER_ID="$MARKETPLACE_OFFER_ID" \
        --from-literal=MARKETPLACE_PLAN_ID="$MARKETPLACE_PLAN_ID" \
        --dry-run=client -o yaml | kubectl apply -f -

    # Create Secret
    kubectl create secret generic honua-secrets \
        --namespace=honua-system \
        --from-literal=database-password="$DB_PASSWORD" \
        --from-literal=redis-key="$REDIS_KEY" \
        --dry-run=client -o yaml | kubectl apply -f -

    # Deploy application
    cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: honua-system
spec:
  replicas: 2
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: honua-server
      containers:
      - name: honua-server
        image: ghcr.io/honua-io/honua-server:$IMAGE_TAG
        ports:
        - containerPort: 8080
        envFrom:
        - configMapRef:
            name: honua-config
        - secretRef:
            name: honua-secrets
---
apiVersion: v1
kind: Service
metadata:
  name: honua-server
  namespace: honua-system
spec:
  type: LoadBalancer
  selector:
    app: honua-server
  ports:
  - port: 80
    targetPort: 8080
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: honua-server
  namespace: honua-system
  annotations:
    azure.workload.identity/client-id: "$MANAGED_IDENTITY_CLIENT_ID"
EOF

    log_info "Waiting for deployment..."
    kubectl wait --for=condition=available --timeout=600s \
        deployment/honua-server -n honua-system

    log_info "Application deployed successfully."
}

# Get service endpoint
get_service_endpoint() {
    log_info "Retrieving service endpoint..."

    local max_attempts=30
    local attempt=0

    while [ $attempt -lt $max_attempts ]; do
        HONUA_URL=$(kubectl get service honua-server -n honua-system \
            -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null)

        if [ -n "$HONUA_URL" ]; then
            log_info "Honua Server is available at: http://$HONUA_URL"
            echo ""
            echo "========================================="
            echo "Deployment Summary"
            echo "========================================="
            echo "Resource Group: $RESOURCE_GROUP"
            echo "Location: $LOCATION"
            echo "Cluster Name: $CLUSTER_NAME"
            echo "License Tier: $LICENSE_TIER"
            echo "Service URL: http://$HONUA_URL"
            echo "========================================="
            return 0
        fi

        attempt=$((attempt + 1))
        log_info "Waiting for LoadBalancer... (attempt $attempt/$max_attempts)"
        sleep 10
    done

    log_warn "LoadBalancer endpoint not available yet."
}

# Main deployment flow
main() {
    log_info "Starting Honua IO Server deployment to Azure Marketplace..."

    check_prerequisites
    create_resource_group
    deploy_infrastructure
    get_deployment_outputs
    configure_kubectl
    deploy_application
    get_service_endpoint

    log_info "Deployment completed successfully!"
}

main "$@"
