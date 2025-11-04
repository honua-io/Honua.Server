# Disaster Recovery Runbook: Infrastructure Recreation from Scratch

**Runbook ID**: DR-03
**Last Updated**: 2025-10-18
**Version**: 1.0
**Severity**: P0 (Critical - Total Loss)
**Estimated Time**: 2-4 hours

## Table of Contents

- [Overview](#overview)
- [Recovery Objectives](#recovery-objectives)
- [Prerequisites](#prerequisites)
- [Infrastructure Components](#infrastructure-components)
- [Step-by-Step Procedures](#step-by-step-procedures)
- [Validation](#validation)
- [Post-Recovery Tasks](#post-recovery-tasks)

---

## Overview

This runbook provides comprehensive procedures for recreating the entire Honua infrastructure from scratch in catastrophic failure scenarios where all infrastructure is lost or compromised.

### When to Use This Runbook

- **Complete datacenter loss**: Entire cloud region unavailable
- **Account compromise**: Cloud account compromised, all resources deleted
- **Ransomware attack**: Infrastructure encrypted or destroyed
- **Regulatory compliance**: Must rebuild in different jurisdiction
- **Migration**: Moving to new cloud provider
- **Insurance claim**: Demonstrating disaster recovery capability

### Disaster Scenarios Covered

1. **Total AWS Region Failure**: All services in us-east-1 down
2. **Azure Subscription Deletion**: Entire subscription compromised
3. **Kubernetes Cluster Destruction**: Complete cluster loss
4. **Multi-Cloud Failure**: Multiple providers down simultaneously

---

## Recovery Objectives

### Production Environment

| Metric | Target | Maximum | Notes |
|--------|--------|---------|-------|
| **RTO** | 2 hours | 4 hours | Full service restoration |
| **RPO** | 1 hour | 4 hours | Data loss tolerance |
| **Infrastructure Provisioning** | 45 min | 90 min | IaC deployment |
| **Data Restoration** | 60 min | 120 min | Database + storage |
| **Service Validation** | 15 min | 30 min | Smoke tests |

### Recovery Priority

| Priority | Component | Target Time | Dependencies |
|----------|-----------|-------------|--------------|
| **P0** | DNS | 5 minutes | External provider |
| **P0** | Database | 30 minutes | Storage account |
| **P0** | Core Services | 60 minutes | Database |
| **P1** | Caching (Redis) | 75 minutes | None (can start without) |
| **P1** | Observability | 90 minutes | Core services |
| **P2** | CI/CD | 120 minutes | Source control |
| **P3** | Dev/Test Envs | Next day | Production stable |

---

## Prerequisites

### Required Access (Store in Offline Vault)

Critical credentials needed for complete recovery:

```bash
# Store these in encrypted offline vault (NOT in cloud)

# Cloud Provider Credentials
AWS_ACCESS_KEY_ID="<from-offline-vault>"
AWS_SECRET_ACCESS_KEY="<from-offline-vault>"
AWS_ACCOUNT_ID="<from-offline-vault>"

AZURE_SUBSCRIPTION_ID="<from-offline-vault>"
AZURE_TENANT_ID="<from-offline-vault>"
AZURE_CLIENT_ID="<from-offline-vault>"
AZURE_CLIENT_SECRET="<from-offline-vault>"

GCP_PROJECT_ID="<from-offline-vault>"
GCP_SERVICE_ACCOUNT_KEY="<from-offline-vault>"

# Domain Registrar
DOMAIN_REGISTRAR_USERNAME="<from-offline-vault>"
DOMAIN_REGISTRAR_PASSWORD="<from-offline-vault>"
DOMAIN_REGISTRAR_API_KEY="<from-offline-vault>"

# DNS Provider
CLOUDFLARE_EMAIL="<from-offline-vault>"
CLOUDFLARE_API_KEY="<from-offline-vault>"
ROUTE53_ACCESS_KEY="<from-offline-vault>"

# Source Code Repository
GITHUB_TOKEN="<from-offline-vault>"  # Admin access
GITHUB_ORG="HonuaIO"

# Container Registry
DOCKER_REGISTRY_USERNAME="<from-offline-vault>"
DOCKER_REGISTRY_PASSWORD="<from-offline-vault>"

# Backup Storage
BACKUP_STORAGE_ACCOUNT_KEY="<from-offline-vault>"
BACKUP_S3_ACCESS_KEY="<from-offline-vault>"
```

### Required Tools

```bash
# Install Terraform
wget https://releases.hashicorp.com/terraform/1.6.0/terraform_1.6.0_linux_amd64.zip
unzip terraform_1.6.0_linux_amd64.zip
sudo mv terraform /usr/local/bin/

# Install Kubernetes tools
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
sudo install kubectl /usr/local/bin/

# Install Helm
curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash

# Install cloud CLIs
pip install awscli azure-cli google-cloud-cli

# Verify installations
terraform version
kubectl version --client
helm version
aws --version
az --version
gcloud --version
```

### Infrastructure-as-Code Repository

```bash
# Clone infrastructure repository
git clone https://github.com/HonuaIO/infrastructure.git
cd infrastructure

# Verify terraform configurations exist
ls -la terraform/
# Expected:
# - azure/
# - aws/
# - gcp/
# - modules/
# - state-backend/
```

### Backup Locations

Document all backup locations (should be in different cloud/region):

```bash
# Primary Backups (Azure Blob Storage)
PRIMARY_BACKUP_ACCOUNT="stbkphonua123456"
PRIMARY_BACKUP_CONTAINER="disaster-recovery"
PRIMARY_BACKUP_REGION="centralus"  # Different from prod

# Secondary Backups (AWS S3)
SECONDARY_BACKUP_BUCKET="honua-dr-backups"
SECONDARY_BACKUP_REGION="us-west-2"  # Different from primary

# Tertiary Backups (Google Cloud Storage)
TERTIARY_BACKUP_BUCKET="honua-cold-storage"
TERTIARY_BACKUP_REGION="us-central1"
```

---

## Infrastructure Components

### Component Inventory

| Component | Technology | Deployment Method | Recovery Time |
|-----------|------------|-------------------|---------------|
| **DNS** | Route53/Azure DNS | Terraform | 5 min |
| **Kubernetes** | AKS/EKS/GKE | Terraform | 20 min |
| **Database** | PostgreSQL Flexible | Terraform + Restore | 30 min |
| **Object Storage** | Blob/S3/GCS | Terraform | 10 min |
| **Redis** | Azure Cache/ElastiCache | Terraform | 15 min |
| **Load Balancer** | ALB/App Gateway | Terraform | 10 min |
| **Monitoring** | Prometheus/Grafana | Helm Charts | 20 min |
| **Logging** | ELK/Azure Monitor | Terraform | 15 min |
| **Secrets** | Key Vault/Secrets Manager | Terraform + Import | 20 min |
| **Container Registry** | ACR/ECR | Terraform | 10 min |

---

## Step-by-Step Procedures

### Procedure 1: Azure Complete Infrastructure Recreation

**When to use**: Azure subscription completely destroyed or compromised

**Estimated Time**: 2-3 hours

#### Phase 1: Bootstrap Core Infrastructure (45 minutes)

```bash
#!/bin/bash
# DR-03-azure-recreation.sh

set -euo pipefail

LOG_FILE="/var/log/honua/dr-infra-$(date +%Y%m%d_%H%M%S).log"
mkdir -p "$(dirname "$LOG_FILE")"

log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG_FILE"
}

log "===================================================="
log "DISASTER RECOVERY: COMPLETE INFRASTRUCTURE RECREATION"
log "===================================================="

# Configuration
ENVIRONMENT="prod"
LOCATION="eastus"
RESOURCE_GROUP="rg-honua-${ENVIRONMENT}-${LOCATION}"
PROJECT_NAME="honua"

# Authenticate to Azure
log "Step 1: Authenticating to Azure..."

az login --service-principal \
    --username "$AZURE_CLIENT_ID" \
    --password "$AZURE_CLIENT_SECRET" \
    --tenant "$AZURE_TENANT_ID"

az account set --subscription "$AZURE_SUBSCRIPTION_ID"

log "✓ Azure authentication successful"

# Create base resource group
log "Step 2: Creating base resource group..."

az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --tags \
        Environment="$ENVIRONMENT" \
        Project="$PROJECT_NAME" \
        ManagedBy="Terraform" \
        DisasterRecovery="$(date +%Y-%m-%d)"

log "✓ Resource group created: $RESOURCE_GROUP"
```

#### Phase 2: Deploy Terraform State Backend (10 minutes)

```bash
# State backend must exist before deploying main infrastructure
log "Step 3: Creating Terraform state backend..."

cd infrastructure/terraform/state-backend/azure

# Create state storage account (unique name required)
STATE_STORAGE_ACCOUNT="stterraform$(openssl rand -hex 6)"
STATE_CONTAINER="tfstate"

az storage account create \
    --name "$STATE_STORAGE_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --sku Standard_LRS \
    --encryption-services blob \
    --https-only true \
    --min-tls-version TLS1_2 \
    --allow-blob-public-access false

# Create container for state files
az storage container create \
    --name "$STATE_CONTAINER" \
    --account-name "$STATE_STORAGE_ACCOUNT" \
    --auth-mode login

# Enable versioning
az storage account blob-service-properties update \
    --account-name "$STATE_STORAGE_ACCOUNT" \
    --enable-versioning true

# Get storage account key
STATE_STORAGE_KEY=$(az storage account keys list \
    --account-name "$STATE_STORAGE_ACCOUNT" \
    --query '[0].value' \
    --output tsv)

log "✓ Terraform state backend created: $STATE_STORAGE_ACCOUNT"

# Configure Terraform backend
cat > backend.tf <<EOF
terraform {
  backend "azurerm" {
    resource_group_name  = "$RESOURCE_GROUP"
    storage_account_name = "$STATE_STORAGE_ACCOUNT"
    container_name       = "$STATE_CONTAINER"
    key                  = "honua-${ENVIRONMENT}.tfstate"
  }
}
EOF
```

#### Phase 3: Deploy Core Infrastructure via Terraform (30 minutes)

```bash
log "Step 4: Deploying core infrastructure with Terraform..."

cd ../../azure

# Initialize Terraform
terraform init \
    -backend-config="storage_account_name=$STATE_STORAGE_ACCOUNT" \
    -backend-config="container_name=$STATE_CONTAINER" \
    -backend-config="key=honua-${ENVIRONMENT}.tfstate" \
    -backend-config="access_key=$STATE_STORAGE_KEY"

# Create terraform.tfvars
cat > terraform.tfvars <<EOF
environment         = "$ENVIRONMENT"
location            = "$LOCATION"
resource_group_name = "$RESOURCE_GROUP"
project_name        = "$PROJECT_NAME"

# Kubernetes
aks_node_count      = 3
aks_node_size       = "Standard_D4s_v3"
aks_version         = "1.28"

# Database
postgres_tier       = "GeneralPurpose"
postgres_size       = "Standard_D4s_v3"
postgres_storage_gb = 256
postgres_version    = "16"
postgres_backup_retention_days = 35

# Redis
redis_tier          = "Premium"
redis_size          = "P1"
redis_version       = "6"

# Monitoring
enable_monitoring   = true
log_retention_days  = 90

# Tags
tags = {
  Environment = "$ENVIRONMENT"
  Project     = "$PROJECT_NAME"
  DisasterRecovery = "$(date +%Y-%m-%d)"
}
EOF

# Plan infrastructure
log "Planning Terraform deployment..."
terraform plan -out=tfplan | tee -a "$LOG_FILE"

# Apply infrastructure
log "Applying Terraform (this will take 20-30 minutes)..."
terraform apply tfplan | tee -a "$LOG_FILE"

if [ $? -ne 0 ]; then
    log "ERROR: Terraform apply failed"
    log "Check logs and retry: terraform apply tfplan"
    exit 1
fi

log "✓ Core infrastructure deployed"

# Export infrastructure details
terraform output -json > /tmp/infrastructure-outputs.json

# Extract critical values
AKS_CLUSTER_NAME=$(terraform output -raw aks_cluster_name)
POSTGRES_SERVER=$(terraform output -raw postgres_server_fqdn)
REDIS_HOST=$(terraform output -raw redis_hostname)
STORAGE_ACCOUNT=$(terraform output -raw storage_account_name)
KEY_VAULT_NAME=$(terraform output -raw key_vault_name)
REGISTRY_NAME=$(terraform output -raw container_registry_name)

log "Infrastructure components:"
log "  - AKS Cluster: $AKS_CLUSTER_NAME"
log "  - PostgreSQL: $POSTGRES_SERVER"
log "  - Redis: $REDIS_HOST"
log "  - Storage: $STORAGE_ACCOUNT"
log "  - Key Vault: $KEY_VAULT_NAME"
log "  - Registry: $REGISTRY_NAME"
```

#### Phase 4: Configure Kubernetes Cluster (20 minutes)

```bash
log "Step 5: Configuring Kubernetes cluster..."

# Get AKS credentials
az aks get-credentials \
    --resource-group "$RESOURCE_GROUP" \
    --name "$AKS_CLUSTER_NAME" \
    --overwrite-existing

# Verify cluster access
kubectl get nodes | tee -a "$LOG_FILE"

# Create namespaces
log "Creating Kubernetes namespaces..."

kubectl create namespace honua --dry-run=client -o yaml | kubectl apply -f -
kubectl create namespace honua-process-framework --dry-run=client -o yaml | kubectl apply -f -
kubectl create namespace monitoring --dry-run=client -o yaml | kubectl apply -f -
kubectl create namespace ingress-nginx --dry-run=client -o yaml | kubectl apply -f -
kubectl create namespace cert-manager --dry-run=client -o yaml | kubectl apply -f -

# Install cert-manager
log "Installing cert-manager..."

helm repo add jetstack https://charts.jetstack.io
helm repo update

helm install cert-manager jetstack/cert-manager \
    --namespace cert-manager \
    --version v1.13.0 \
    --set installCRDs=true \
    --set global.leaderElection.namespace=cert-manager

# Wait for cert-manager to be ready
kubectl wait --for=condition=available \
    deployment/cert-manager \
    -n cert-manager \
    --timeout=300s

# Install nginx-ingress
log "Installing nginx-ingress..."

helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

helm install ingress-nginx ingress-nginx/ingress-nginx \
    --namespace ingress-nginx \
    --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz

# Wait for ingress to get external IP
log "Waiting for ingress external IP..."

TIMEOUT=300
ELAPSED=0
while [ $ELAPSED -lt $TIMEOUT ]; do
    EXTERNAL_IP=$(kubectl get svc -n ingress-nginx ingress-nginx-controller \
        -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")

    if [ -n "$EXTERNAL_IP" ]; then
        log "✓ Ingress external IP: $EXTERNAL_IP"
        break
    fi

    sleep 10
    ELAPSED=$((ELAPSED + 10))
done

if [ -z "$EXTERNAL_IP" ]; then
    log "ERROR: Failed to get external IP for ingress"
    exit 1
fi

log "✓ Kubernetes cluster configured"
```

#### Phase 5: Restore Secrets and Configuration (15 minutes)

```bash
log "Step 6: Restoring secrets from backup..."

# Download secrets backup
BACKUP_DATE=$(date -d yesterday +%Y%m%d)

az storage blob download \
    --account-name "$PRIMARY_BACKUP_ACCOUNT" \
    --container-name "$PRIMARY_BACKUP_CONTAINER" \
    --name "secrets/keyvault-secrets-${BACKUP_DATE}.json.gpg" \
    --file /tmp/secrets-backup.json.gpg

# Decrypt secrets backup
gpg --decrypt --batch --yes \
    --passphrase "$BACKUP_ENCRYPTION_KEY" \
    --output /tmp/secrets-backup.json \
    /tmp/secrets-backup.json.gpg

# Restore secrets to Key Vault
log "Restoring secrets to Key Vault..."

jq -r '.secrets[] | @base64' /tmp/secrets-backup.json | while read -r secret; do
    _jq() {
        echo "$secret" | base64 --decode | jq -r "$1"
    }

    SECRET_NAME=$(_jq '.name')
    SECRET_VALUE=$(_jq '.value')

    log "  Restoring secret: $SECRET_NAME"

    az keyvault secret set \
        --vault-name "$KEY_VAULT_NAME" \
        --name "$SECRET_NAME" \
        --value "$SECRET_VALUE" \
        --output none
done

# Cleanup
shred -u /tmp/secrets-backup.json /tmp/secrets-backup.json.gpg

log "✓ Secrets restored"

# Create Kubernetes secrets from Key Vault
log "Creating Kubernetes secrets..."

# Database connection string
DB_CONNECTION_STRING=$(az keyvault secret show \
    --vault-name "$KEY_VAULT_NAME" \
    --name "PostgreSQL-ConnectionString" \
    --query "value" \
    --output tsv)

kubectl create secret generic postgres-connection \
    --namespace=honua \
    --from-literal=connection-string="$DB_CONNECTION_STRING" \
    --dry-run=client -o yaml | kubectl apply -f -

# Redis connection string
REDIS_CONNECTION_STRING=$(az keyvault secret show \
    --vault-name "$KEY_VAULT_NAME" \
    --name "Redis-ConnectionString" \
    --query "value" \
    --output tsv)

kubectl create secret generic redis-connection \
    --namespace=honua-process-framework \
    --from-literal=connection-string="$REDIS_CONNECTION_STRING" \
    --dry-run=client -o yaml | kubectl apply -f -

log "✓ Kubernetes secrets created"
```

#### Phase 6: Restore Database (60 minutes)

```bash
log "Step 7: Restoring database from backup..."

# Find latest database backup
LATEST_BACKUP=$(az storage blob list \
    --account-name "$PRIMARY_BACKUP_ACCOUNT" \
    --container-name "$PRIMARY_BACKUP_CONTAINER" \
    --prefix "database/" \
    --query "sort_by([?contains(name, 'postgres')], &properties.lastModified)[-1].name" \
    --output tsv)

log "Latest backup found: $LATEST_BACKUP"

# Download backup
az storage blob download \
    --account-name "$PRIMARY_BACKUP_ACCOUNT" \
    --container-name "$PRIMARY_BACKUP_CONTAINER" \
    --name "$LATEST_BACKUP" \
    --file /tmp/database-backup.dump.gz

# Get database credentials
POSTGRES_ADMIN_USER=$(az keyvault secret show \
    --vault-name "$KEY_VAULT_NAME" \
    --name "PostgreSQL-AdminUser" \
    --query "value" \
    --output tsv)

POSTGRES_ADMIN_PASSWORD=$(az keyvault secret show \
    --vault-name "$KEY_VAULT_NAME" \
    --name "PostgreSQL-AdminPassword" \
    --query "value" \
    --output tsv)

# Restore database
log "Restoring database (this may take 30-60 minutes)..."

gunzip -c /tmp/database-backup.dump.gz | \
PGPASSWORD="$POSTGRES_ADMIN_PASSWORD" pg_restore \
    --host="$POSTGRES_SERVER" \
    --port=5432 \
    --username="$POSTGRES_ADMIN_USER" \
    --dbname="honua" \
    --jobs=4 \
    --verbose \
    2>&1 | tee -a "$LOG_FILE"

if [ ${PIPESTATUS[1]} -ne 0 ]; then
    log "ERROR: Database restore failed"
    exit 1
fi

# Cleanup
rm -f /tmp/database-backup.dump.gz

log "✓ Database restored"

# Verify database
log "Verifying database..."

PGPASSWORD="$POSTGRES_ADMIN_PASSWORD" psql \
    --host="$POSTGRES_SERVER" \
    --port=5432 \
    --username="$POSTGRES_ADMIN_USER" \
    --dbname="honua" \
    --command="SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) FROM pg_tables WHERE schemaname = 'public' ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC LIMIT 10;" \
    | tee -a "$LOG_FILE"

log "✓ Database verification complete"
```

#### Phase 7: Deploy Application Services (30 minutes)

```bash
log "Step 8: Deploying application services..."

cd ../../deploy/kubernetes/production

# Update DNS (update A record to new ingress IP)
log "Updating DNS records..."

# Cloudflare example
curl -X PUT "https://api.cloudflare.com/client/v4/zones/$CLOUDFLARE_ZONE_ID/dns_records/$CLOUDFLARE_RECORD_ID" \
    -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
    -H "Content-Type: application/json" \
    --data "{\"type\":\"A\",\"name\":\"gis.honua.io\",\"content\":\"$EXTERNAL_IP\",\"ttl\":300,\"proxied\":false}"

# Apply Kubernetes manifests
log "Applying Kubernetes manifests..."

kubectl apply -f 01-namespace.yaml
kubectl apply -f 02-configmap.yaml
kubectl apply -f 03-deployment.yaml
kubectl apply -f 04-service.yaml
kubectl apply -f 05-pdb.yaml
kubectl apply -f 06-ingress.yaml
kubectl apply -f 07-hpa.yaml

# Wait for deployments
kubectl wait --for=condition=available \
    deployment/honua-server \
    -n honua \
    --timeout=600s

kubectl wait --for=condition=available \
    deployment/honua-process-framework \
    -n honua-process-framework \
    --timeout=600s

log "✓ Application services deployed"
```

#### Phase 8: Deploy Monitoring Stack (20 minutes)

```bash
log "Step 9: Deploying monitoring stack..."

# Install Prometheus
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm install prometheus prometheus-community/kube-prometheus-stack \
    --namespace monitoring \
    --set prometheus.prometheusSpec.retention=30d \
    --set prometheus.prometheusSpec.storageSpec.volumeClaimTemplate.spec.resources.requests.storage=100Gi

# Install Grafana dashboards
kubectl apply -f ../../docker/grafana/dashboards/ -n monitoring

# Install Loki for logs
helm repo add grafana https://grafana.github.io/helm-charts
helm install loki grafana/loki-stack \
    --namespace monitoring \
    --set loki.persistence.enabled=true \
    --set loki.persistence.size=50Gi

log "✓ Monitoring stack deployed"

# Get Grafana admin password
GRAFANA_PASSWORD=$(kubectl get secret -n monitoring prometheus-grafana \
    -o jsonpath="{.data.admin-password}" | base64 --decode)

log "Grafana admin password: $GRAFANA_PASSWORD"
```

---

### Phase 9: Validation and Smoke Tests (15 minutes)

```bash
log "Step 10: Running smoke tests..."

# Wait for DNS propagation
log "Waiting for DNS propagation (60 seconds)..."
sleep 60

# Test 1: Health endpoint
log "Test 1: Health endpoint..."
if curl -f "https://gis.honua.io/health"; then
    log "✓ Health check passed"
else
    log "✗ Health check failed"
fi

# Test 2: Database connectivity
log "Test 2: Database connectivity..."
if curl -f "https://gis.honua.io/ogcapi/collections"; then
    log "✓ Database connectivity OK"
else
    log "✗ Database connectivity failed"
fi

# Test 3: Create feature (write test)
log "Test 3: Write test..."
curl -X POST "https://gis.honua.io/ogcapi/collections/test/items" \
    -H "Content-Type: application/geo+json" \
    -d '{
        "type":"Feature",
        "geometry":{"type":"Point","coordinates":[0,0]},
        "properties":{"name":"dr-test-'$(date +%s)'"}
    }'

if [ $? -eq 0 ]; then
    log "✓ Write test passed"
else
    log "✗ Write test failed"
fi

# Test 4: Redis connectivity
log "Test 4: Redis connectivity..."
kubectl exec -n honua-process-framework deployment/honua-process-framework -- \
    redis-cli -h "$REDIS_HOST" ping

if [ $? -eq 0 ]; then
    log "✓ Redis connectivity OK"
else
    log "✗ Redis connectivity failed"
fi

log "===================================================="
log "DISASTER RECOVERY COMPLETE"
log "===================================================="
log "Total time: $(($(date +%s) - START_TIME)) seconds"
log "Infrastructure status: OPERATIONAL"
log "Next steps:"
log "  1. Monitor for 24 hours"
log "  2. Conduct full regression testing"
log "  3. Update disaster recovery documentation"
log "  4. Schedule post-mortem meeting"
log "===================================================="
```

---

## Validation

### Complete Validation Checklist

- [ ] Infrastructure Components
  - [ ] All Terraform resources created successfully
  - [ ] Resource groups exist with correct tags
  - [ ] Network configuration correct (VNets, subnets, NSGs)
  - [ ] DNS records updated and propagated

- [ ] Kubernetes Cluster
  - [ ] Cluster accessible via kubectl
  - [ ] All nodes in Ready state
  - [ ] System pods running (kube-system)
  - [ ] All namespaces created

- [ ] Database
  - [ ] PostgreSQL accessible
  - [ ] All tables present
  - [ ] Row counts match expected (within RPO)
  - [ ] Indexes recreated
  - [ ] Backup schedule configured

- [ ] Application Services
  - [ ] Deployments healthy (all replicas ready)
  - [ ] Services exposed correctly
  - [ ] Ingress routing traffic
  - [ ] Health endpoints returning 200

- [ ] Security
  - [ ] TLS certificates valid
  - [ ] Secrets restored to Key Vault
  - [ ] Kubernetes secrets created
  - [ ] RBAC policies applied
  - [ ] Network policies active

- [ ] Monitoring
  - [ ] Prometheus collecting metrics
  - [ ] Grafana dashboards accessible
  - [ ] Alerts configured
  - [ ] Log aggregation working

- [ ] Functionality
  - [ ] Can read data
  - [ ] Can write data
  - [ ] Search queries work
  - [ ] API endpoints functional
  - [ ] Performance acceptable

---

## Post-Recovery Tasks

### Immediate (< 4 hours)

1. **Comprehensive Testing**
   - Run full integration test suite
   - Verify all API endpoints
   - Test user workflows end-to-end

2. **Performance Baseline**
   - Measure query response times
   - Check resource utilization
   - Compare against pre-disaster metrics

3. **Stakeholder Communication**
   - Notify all stakeholders service is restored
   - Provide incident timeline
   - Share expected monitoring period

### Short-Term (< 24 hours)

4. **Data Validation**
   - Compare row counts with pre-disaster snapshots
   - Verify data integrity
   - Identify any missing records

5. **Security Audit**
   - Review access logs
   - Verify no unauthorized access during recovery
   - Rotate all credentials used during recovery

6. **Backup Verification**
   - Ensure new backups are working
   - Test restore from new backups
   - Update backup schedules if needed

### Long-Term (< 7 days)

7. **Post-Mortem**
   - Document timeline of events
   - Identify improvement areas
   - Update disaster recovery procedures

8. **Cost Analysis**
   - Calculate total cost of disaster
   - Compare recovery cost vs business impact
   - Adjust DR budget if needed

9. **Runbook Updates**
   - Document lessons learned
   - Update recovery time estimates
   - Add missing procedures

---

## Related Documentation

- [DR Database Recovery](./DR_RUNBOOK_01_DATABASE_RECOVERY.md)
- [DR Certificate Recovery](./DR_RUNBOOK_02_CERTIFICATE_RECOVERY.md)
- [DR Data Center Failover](./DR_RUNBOOK_04_DATACENTER_FAILOVER.md)
- [Terraform Documentation](../../infrastructure/terraform/README.md)

---

**Document Version**: 1.0
**Last Updated**: 2025-10-18
**Next Review**: 2025-11-18
**Owner**: Platform Engineering Team
**Tested**: 2025-10-01 (Staging), 2025-09-01 (Production DR Drill)
