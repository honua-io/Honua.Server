# Honua Infrastructure Deployment Guide

Complete step-by-step guide for deploying Honua infrastructure across cloud providers.

## Table of Contents

1. [Pre-Deployment Checklist](#pre-deployment-checklist)
2. [AWS Deployment](#aws-deployment)
3. [Azure Deployment](#azure-deployment)
4. [GCP Deployment](#gcp-deployment)
5. [Post-Deployment Configuration](#post-deployment-configuration)
6. [Disaster Recovery Setup](#disaster-recovery-setup)
7. [Validation and Testing](#validation-and-testing)
8. [Troubleshooting](#troubleshooting)

## Pre-Deployment Checklist

### Required Accounts and Access

- [ ] Cloud provider account with appropriate permissions
- [ ] GitHub account (for OIDC integration)
- [ ] Email addresses for monitoring alerts
- [ ] SSL/TLS certificates (if using custom domains)

### Required Tools Installation

```bash
# Terraform
wget https://releases.hashicorp.com/terraform/1.6.6/terraform_1.6.6_linux_amd64.zip
unzip terraform_1.6.6_linux_amd64.zip
sudo mv terraform /usr/local/bin/

# Verify
terraform version

# kubectl
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
chmod +x kubectl
sudo mv kubectl /usr/local/bin/

# AWS CLI
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install

# Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# gcloud CLI
curl https://sdk.cloud.google.com | bash
exec -l $SHELL
gcloud init

# jq (JSON processor)
sudo apt-get install jq  # Debian/Ubuntu
# or
sudo yum install jq      # RHEL/CentOS
```

### Environment Variables

```bash
# Set common variables
export ENVIRONMENT="dev"  # or staging, production
export REGION="us-east-1"
export PROJECT_NAME="honua"

# AWS
export AWS_REGION="us-east-1"
export AWS_PROFILE="default"

# Azure
export AZURE_SUBSCRIPTION_ID="your-subscription-id"
export AZURE_LOCATION="eastus"

# GCP
export GCP_PROJECT_ID="your-project-id"
export GCP_REGION="us-central1"
```

## AWS Deployment

### Step 1: Configure AWS Credentials

```bash
# Configure AWS CLI
aws configure

# Or use environment variables
export AWS_ACCESS_KEY_ID="your-access-key"
export AWS_SECRET_ACCESS_KEY="your-secret-key"
export AWS_DEFAULT_REGION="us-east-1"

# Verify access
aws sts get-caller-identity
```

### Step 2: Create State Backend

```bash
cd infrastructure/scripts

# Run backend setup script
cat > setup-aws-backend.sh <<'EOF'
#!/bin/bash
set -euo pipefail

ENV=$1
REGION=${2:-us-east-1}

# Create S3 bucket for Terraform state
aws s3api create-bucket \
  --bucket honua-terraform-state-${ENV} \
  --region ${REGION}

# Enable versioning
aws s3api put-bucket-versioning \
  --bucket honua-terraform-state-${ENV} \
  --versioning-configuration Status=Enabled

# Enable encryption
aws s3api put-bucket-encryption \
  --bucket honua-terraform-state-${ENV} \
  --server-side-encryption-configuration '{
    "Rules": [{
      "ApplyServerSideEncryptionByDefault": {
        "SSEAlgorithm": "AES256"
      }
    }]
  }'

# Block public access
aws s3api put-public-access-block \
  --bucket honua-terraform-state-${ENV} \
  --public-access-block-configuration \
    "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true"

# Create DynamoDB table for state locking
aws dynamodb create-table \
  --table-name honua-terraform-locks-${ENV} \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region ${REGION}

echo "Backend created successfully for environment: ${ENV}"
EOF

chmod +x setup-aws-backend.sh
./setup-aws-backend.sh dev us-east-1
```

### Step 3: Configure Variables

```bash
cd ../terraform/environments/dev

# Copy example variables
cp terraform.tfvars.example terraform.tfvars

# Edit with your values
cat > terraform.tfvars <<EOF
aws_region             = "us-east-1"
github_org             = "YourOrganization"
github_repo            = "honua"
grafana_admin_password = "$(openssl rand -base64 32)"
alarm_emails           = ["devops@example.com"]
EOF
```

### Step 4: Deploy Infrastructure

```bash
# Initialize Terraform
terraform init

# Validate configuration
terraform validate

# Plan deployment
terraform plan -out=tfplan

# Review the plan carefully
# Then apply
terraform apply tfplan

# This will take 15-30 minutes
```

### Step 5: Configure kubectl

```bash
# Get cluster name from outputs
CLUSTER_NAME=$(terraform output -raw eks_cluster_name)
REGION=$(terraform output -raw aws_region)

# Update kubeconfig
aws eks update-kubeconfig --name ${CLUSTER_NAME} --region ${REGION}

# Verify connectivity
kubectl get nodes
kubectl get namespaces
```

### Step 6: Deploy Core Services

```bash
# Deploy monitoring stack (already done via Terraform)
kubectl get pods -n monitoring

# Verify Prometheus and Grafana
kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80 &
kubectl port-forward -n monitoring svc/prometheus-kube-prometheus-prometheus 9090:9090 &

# Access Grafana at http://localhost:3000
# Login: admin / <grafana_admin_password from terraform.tfvars>
```

### Step 7: Verify Deployment

```bash
# Check all resources
terraform output

# Verify database connectivity
DB_ENDPOINT=$(terraform output -raw database_endpoint)
echo "Database: ${DB_ENDPOINT}"

# Verify Redis connectivity
REDIS_ENDPOINT=$(terraform output -raw redis_endpoint)
echo "Redis: ${REDIS_ENDPOINT}"

# Check ECR repositories
ECR_REPOS=$(terraform output -json ecr_repositories | jq -r 'keys[]')
echo "ECR Repositories:"
echo "${ECR_REPOS}"
```

## Azure Deployment

### Step 1: Configure Azure Credentials

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "your-subscription-id"

# Verify
az account show
```

### Step 2: Create Resource Group

```bash
# Create resource group for Terraform state
az group create \
  --name honua-terraform-state \
  --location eastus

# Create storage account
STORAGE_ACCOUNT="honuatfstate$(date +%s)"
az storage account create \
  --name ${STORAGE_ACCOUNT} \
  --resource-group honua-terraform-state \
  --location eastus \
  --sku Standard_LRS \
  --encryption-services blob

# Get storage account key
ACCOUNT_KEY=$(az storage account keys list \
  --resource-group honua-terraform-state \
  --account-name ${STORAGE_ACCOUNT} \
  --query '[0].value' -o tsv)

# Create blob container
az storage container create \
  --name tfstate \
  --account-name ${STORAGE_ACCOUNT} \
  --account-key ${ACCOUNT_KEY}

echo "Storage Account: ${STORAGE_ACCOUNT}"
echo "Store this for backend configuration!"
```

### Step 3: Configure Backend

```bash
cd infrastructure/terraform/environments/dev

# Update backend.tf with Azure storage
cat > backend.tf <<EOF
terraform {
  backend "azurerm" {
    resource_group_name  = "honua-terraform-state"
    storage_account_name = "${STORAGE_ACCOUNT}"
    container_name       = "tfstate"
    key                  = "dev.terraform.tfstate"
  }
}
EOF
```

### Step 4: Configure Variables

```bash
# Update main.tf to set cloud_provider = "azure"
sed -i 's/cloud_provider = "aws"/cloud_provider = "azure"/g' main.tf

# Create terraform.tfvars
cat > terraform.tfvars <<EOF
azure_location         = "eastus"
resource_group_name    = "honua-dev"
grafana_admin_password = "$(openssl rand -base64 32)"
alarm_emails           = ["devops@example.com"]
EOF
```

### Step 5: Deploy

```bash
# Initialize and deploy
terraform init
terraform plan -out=tfplan
terraform apply tfplan

# Configure kubectl for AKS
AKS_NAME=$(terraform output -raw aks_cluster_name)
RESOURCE_GROUP=$(terraform output -raw resource_group_name)

az aks get-credentials \
  --name ${AKS_NAME} \
  --resource-group ${RESOURCE_GROUP}

# Verify
kubectl get nodes
```

## GCP Deployment

### Step 1: Configure GCP

```bash
# Login
gcloud auth login

# Set project
gcloud config set project your-project-id

# Enable required APIs
gcloud services enable \
  container.googleapis.com \
  sqladmin.googleapis.com \
  redis.googleapis.com \
  artifactregistry.googleapis.com \
  secretmanager.googleapis.com \
  cloudkms.googleapis.com \
  compute.googleapis.com
```

### Step 2: Create State Backend

```bash
# Create GCS bucket for state
gsutil mb -l us-central1 gs://honua-terraform-state-dev

# Enable versioning
gsutil versioning set on gs://honua-terraform-state-dev

# Set lifecycle (optional)
cat > lifecycle.json <<EOF
{
  "lifecycle": {
    "rule": [{
      "action": {"type": "Delete"},
      "condition": {
        "age": 90,
        "numNewerVersions": 10
      }
    }]
  }
}
EOF

gsutil lifecycle set lifecycle.json gs://honua-terraform-state-dev
```

### Step 3: Configure Backend

```bash
cd infrastructure/terraform/environments/dev

cat > backend.tf <<EOF
terraform {
  backend "gcs" {
    bucket = "honua-terraform-state-dev"
    prefix = "terraform/state"
  }
}
EOF
```

### Step 4: Deploy

```bash
# Update main.tf for GCP
sed -i 's/cloud_provider = "aws"/cloud_provider = "gcp"/g' main.tf

# Configure variables
cat > terraform.tfvars <<EOF
gcp_project_id         = "your-project-id"
gcp_region             = "us-central1"
grafana_admin_password = "$(openssl rand -base64 32)"
alarm_emails           = ["devops@example.com"]
EOF

# Deploy
terraform init
terraform plan -out=tfplan
terraform apply tfplan

# Configure kubectl for GKE
CLUSTER_NAME=$(terraform output -raw gke_cluster_name)
REGION=$(terraform output -raw gcp_region)

gcloud container clusters get-credentials ${CLUSTER_NAME} --region ${REGION}

# Verify
kubectl get nodes
```

## Post-Deployment Configuration

### 1. Configure DNS (Optional)

```bash
# If using custom domain, create DNS records pointing to load balancer

# AWS
LB_HOSTNAME=$(kubectl get svc -n ingress-nginx ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')

# Azure/GCP
LB_IP=$(kubectl get svc -n ingress-nginx ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# Create DNS A/CNAME record
# honua.example.com -> ${LB_HOSTNAME} or ${LB_IP}
```

### 2. Configure TLS Certificates

```bash
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# Create Let's Encrypt issuer
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: admin@example.com
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
EOF
```

### 3. Deploy Honua Application

```bash
# Clone Honua repository
git clone https://github.com/YourOrg/honua.git
cd honua

# Build and push images
# (This would typically be done via CI/CD)

# Deploy using Kubernetes manifests
kubectl apply -f k8s/

# Or using Helm
helm install honua ./charts/honua -n honua --create-namespace
```

### 4. Configure Monitoring Dashboards

```bash
# Import pre-built Grafana dashboards
# Access Grafana UI and import dashboard IDs:
# - Kubernetes Cluster: 315
# - PostgreSQL: 9628
# - Redis: 11835
```

### 5. Set Up Alerting

```bash
# Configure Alertmanager for Slack/PagerDuty/Email
kubectl edit configmap -n monitoring alertmanager-prometheus-kube-prometheus-alertmanager

# Add your notification configuration
```

## Disaster Recovery Setup

### AWS Cross-Region Backup

Already configured in production Terraform. Verify:

```bash
# Check backup vault
aws backup list-backup-vaults

# Check backup plan
aws backup list-backup-plans

# Verify snapshots are being created
aws rds describe-db-snapshots \
  --db-instance-identifier honua-production
```

### Manual Backup

```bash
# Create manual RDS snapshot
aws rds create-db-snapshot \
  --db-instance-identifier honua-production \
  --db-snapshot-identifier honua-manual-$(date +%Y%m%d-%H%M%S)

# Export Kubernetes resources
kubectl get all -n honua -o yaml > honua-backup-$(date +%Y%m%d).yaml

# Backup Terraform state
cd infrastructure/terraform/environments/production
terraform state pull > terraform-state-backup-$(date +%Y%m%d).tfstate
```

## Validation and Testing

### Infrastructure Validation

```bash
# Run validation script
cd infrastructure/scripts

cat > validate-deployment.sh <<'EOF'
#!/bin/bash
set -euo pipefail

echo "Validating Honua Infrastructure..."

# Check Kubernetes cluster
echo "✓ Checking Kubernetes cluster..."
kubectl cluster-info
kubectl get nodes

# Check monitoring
echo "✓ Checking monitoring stack..."
kubectl get pods -n monitoring

# Check database connectivity
echo "✓ Checking database..."
kubectl run psql-test --rm -it --restart=Never \
  --image=postgres:15 \
  --env="PGPASSWORD=${DB_PASSWORD}" \
  -- psql -h ${DB_HOST} -U honua_admin -d honua_dev -c "SELECT version();"

# Check Redis connectivity
echo "✓ Checking Redis..."
kubectl run redis-test --rm -it --restart=Never \
  --image=redis:7 \
  -- redis-cli -h ${REDIS_HOST} ping

echo "All validations passed!"
EOF

chmod +x validate-deployment.sh
```

### Load Testing

```bash
# Deploy a test build job
kubectl apply -f - <<EOF
apiVersion: batch/v1
kind: Job
metadata:
  name: test-build
  namespace: honua
spec:
  template:
    spec:
      containers:
      - name: build
        image: honua/build-worker:latest
        command: ["./run-test-build.sh"]
      restartPolicy: Never
  backoffLimit: 3
EOF

# Monitor job
kubectl logs -f job/test-build -n honua
```

## Troubleshooting

### Common Issues

#### 1. Terraform State Lock

```bash
# If state is locked
terraform force-unlock <lock-id>
```

#### 2. kubectl Connection Issues

```bash
# Refresh kubeconfig
# AWS
aws eks update-kubeconfig --name honua-dev --region us-east-1

# Azure
az aks get-credentials --name honua-dev --resource-group honua-dev

# GCP
gcloud container clusters get-credentials honua-dev --region us-central1
```

#### 3. Pod Scheduling Failures

```bash
# Check node capacity
kubectl describe nodes

# Check pod events
kubectl describe pod <pod-name> -n <namespace>

# Scale nodes if needed
# AWS: Update node group in Terraform
# Azure: Scale AKS node pool
# GCP: Scale GKE node pool
```

#### 4. Database Connection Timeout

```bash
# Check security group/firewall rules
# Verify endpoints
# Test from within cluster:
kubectl run debug --rm -it --restart=Never \
  --image=nicolaka/netshoot \
  -- /bin/bash

# Then inside pod:
nc -zv <db-endpoint> 5432
```

### Getting Help

- Check logs: `kubectl logs -n <namespace> <pod-name>`
- Describe resources: `kubectl describe <resource> <name>`
- Cloud provider support console
- Honua documentation: https://docs.honua.io

## Next Steps

After successful deployment:

1. **Security Hardening**:
   - Review IAM policies
   - Enable audit logging
   - Configure network policies
   - Set up WAF/Cloud Armor

2. **Performance Tuning**:
   - Monitor resource usage
   - Adjust node counts
   - Optimize database queries
   - Configure caching

3. **Automation**:
   - Set up CI/CD pipelines
   - Automate deployments
   - Configure auto-scaling policies

4. **Documentation**:
   - Document custom configurations
   - Create runbooks for operations
   - Train team members

---

**Congratulations!** Your Honua infrastructure is now deployed and ready for use.
