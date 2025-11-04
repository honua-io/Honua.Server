# Honua Infrastructure as Code

Comprehensive multi-cloud infrastructure provisioning for the Honua build orchestration system.

## Overview

This infrastructure supports deployment across AWS, Azure, and GCP with the following components:

- **Kubernetes Clusters**: EKS (AWS), AKS (Azure), GKE (GCP) with ARM node support
- **Databases**: PostgreSQL with read replicas and automated backups
- **Caching**: Redis with cluster mode support
- **Container Registries**: ECR, ACR, Artifact Registry with lifecycle policies
- **Networking**: VPC/VNet with public, private, and database subnets
- **IAM**: Service accounts, customer access, OIDC federation
- **Monitoring**: Prometheus, Grafana, CloudWatch/Azure Monitor/Cloud Monitoring
- **Security**: Encryption at rest/transit, KMS, secrets management

## Directory Structure

```
infrastructure/
├── terraform/
│   ├── modules/               # Reusable Terraform modules
│   │   ├── kubernetes-cluster/   # K8s cluster (EKS/AKS/GKE)
│   │   ├── database/             # PostgreSQL databases
│   │   ├── redis/                # Redis clusters
│   │   ├── registry/             # Container registries
│   │   ├── networking/           # VPC/VNet/VPC
│   │   ├── iam/                  # Identity and access management
│   │   └── monitoring/           # Observability stack
│   └── environments/          # Environment-specific configs
│       ├── dev/                  # Development environment
│       ├── staging/              # Staging environment
│       └── production/           # Production environment
├── pulumi/                    # Pulumi alternative IaC
├── ansible/                   # Configuration management
├── cloud-specific/            # CloudFormation/ARM/DM templates
└── scripts/                   # Operational scripts
    ├── provision-all.sh          # Provision complete infrastructure
    ├── destroy-env.sh            # Safely destroy environment
    ├── provision-customer.sh     # Create customer resources
    └── rotate-credentials.sh     # Rotate credentials
```

## Prerequisites

### Required Tools

- **Terraform**: >= 1.5.0
- **kubectl**: >= 1.28
- **jq**: For JSON processing
- **Cloud CLI tools**:
  - AWS CLI >= 2.0 (for AWS deployments)
  - Azure CLI >= 2.50 (for Azure deployments)
  - gcloud SDK >= 450.0 (for GCP deployments)

### Required Permissions

#### AWS
- Administrator access or specific IAM permissions for:
  - VPC, EC2, EKS, RDS, ElastiCache, ECR
  - IAM, KMS, Secrets Manager
  - CloudWatch, SNS
  - S3 (for Terraform state)
  - DynamoDB (for state locking)

#### Azure
- Owner or Contributor role on subscription
- Permissions to create:
  - Resource Groups, VNets, AKS
  - Database for PostgreSQL, Cache for Redis, ACR
  - Key Vault, Log Analytics, Application Insights
  - Storage Account (for Terraform state)

#### GCP
- Project Owner or Editor role
- APIs enabled:
  - Kubernetes Engine API
  - Cloud SQL Admin API
  - Artifact Registry API
  - Cloud Memorystore for Redis API
  - Secret Manager API
  - Cloud Monitoring API

### Pre-Deployment Setup

#### 1. Create Terraform State Backend (AWS Example)

```bash
# Create S3 bucket for state
aws s3api create-bucket \
  --bucket honua-terraform-state-dev \
  --region us-east-1

# Enable versioning
aws s3api put-bucket-versioning \
  --bucket honua-terraform-state-dev \
  --versioning-configuration Status=Enabled

# Enable encryption
aws s3api put-bucket-encryption \
  --bucket honua-terraform-state-dev \
  --server-side-encryption-configuration '{
    "Rules": [{
      "ApplyServerSideEncryptionByDefault": {
        "SSEAlgorithm": "AES256"
      }
    }]
  }'

# Create DynamoDB table for state locking
aws dynamodb create-table \
  --table-name honua-terraform-locks \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5
```

#### 2. Configure Variables

Copy and customize the example variables file:

```bash
cd infrastructure/terraform/environments/dev
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your values
```

## Deployment

### Quick Start - Development Environment

```bash
# Navigate to infrastructure directory
cd infrastructure

# Provision complete dev environment on AWS
./scripts/provision-all.sh dev aws

# This will:
# 1. Initialize Terraform
# 2. Validate configuration
# 3. Plan changes
# 4. Apply infrastructure (after confirmation)
# 5. Configure kubectl
# 6. Output access information
```

### Step-by-Step Deployment

#### 1. Initialize Terraform

```bash
cd infrastructure/terraform/environments/dev
terraform init
```

#### 2. Review and Customize Configuration

Edit `terraform.tfvars`:

```hcl
aws_region             = "us-east-1"
github_org             = "YourOrg"
github_repo            = "honua"
grafana_admin_password = "secure-password-here"
alarm_emails           = ["devops@example.com"]
```

#### 3. Plan Infrastructure

```bash
terraform plan -out=tfplan
```

#### 4. Apply Infrastructure

```bash
terraform apply tfplan
```

#### 5. Configure Cluster Access

```bash
# AWS EKS
aws eks update-kubeconfig --name honua-dev --region us-east-1

# Verify connectivity
kubectl get nodes
```

### Production Deployment

Production requires additional considerations:

```bash
# Use production environment
cd infrastructure/terraform/environments/production

# Copy and configure variables
cp terraform.tfvars.example terraform.tfvars

# IMPORTANT: Review all settings for production
# - Use on-demand instances (not spot)
# - Enable multi-AZ deployment
# - Configure read replicas
# - Set up DR backups
# - Restrict public access

# Deploy
terraform init
terraform plan -out=tfplan

# CAREFUL: Review plan thoroughly
terraform apply tfplan
```

## Customer Provisioning

Create isolated resources for each customer:

```bash
# Provision customer resources
./scripts/provision-customer.sh production customer-123 "Acme Corp"

# This creates:
# - Dedicated ECR repository
# - IAM user with limited permissions
# - Access credentials
# - Secrets in Secrets Manager

# Credentials are output and stored in AWS Secrets Manager
```

## Operations

### View Infrastructure Outputs

```bash
cd infrastructure/terraform/environments/dev
terraform output

# Specific output
terraform output eks_cluster_endpoint
terraform output database_endpoint
```

### Access Monitoring

```bash
# Port-forward Grafana
kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80

# Access at http://localhost:3000
# Default credentials: admin / <grafana_admin_password>

# Port-forward Prometheus
kubectl port-forward -n monitoring svc/prometheus-kube-prometheus-prometheus 9090:9090

# Access at http://localhost:9090
```

### Scale Kubernetes Nodes

```bash
# AWS EKS - Modify node group in Terraform
# Edit environments/dev/main.tf:
node_group_min_size = 3
node_group_max_size = 10

# Apply changes
terraform apply
```

### Rotate Credentials

```bash
# Rotate specific customer credentials
./scripts/rotate-credentials.sh production customer-123

# Rotate all credentials
./scripts/rotate-credentials.sh production
```

### Backup and Disaster Recovery

```bash
# Backup Terraform state
terraform state pull > backup-$(date +%Y%m%d).tfstate

# Database backups are automatic
# View RDS snapshots:
aws rds describe-db-snapshots --db-instance-identifier honua-production

# Restore from snapshot (if needed):
# 1. Identify snapshot
# 2. Update Terraform to restore from snapshot
# 3. Apply changes
```

### Destroy Environment

```bash
# Destroy development environment
./scripts/destroy-env.sh dev

# For production (requires multiple confirmations):
./scripts/destroy-env.sh production
```

## Cost Optimization

### Development Environment
- Uses smaller instance types
- Spot/preemptible instances where possible
- Single-node Redis
- No read replicas
- Shorter backup retention
- **Estimated monthly cost: $500-800**

### Production Environment
- Production-grade instances
- On-demand for reliability
- Multi-AZ deployment
- Read replicas for HA
- Extended backup retention
- Cross-region DR
- **Estimated monthly cost: $5,000-10,000**

### Cost Saving Tips

1. **Stop non-production environments when not in use**:
   ```bash
   # Scale down EKS nodes
   kubectl scale deployment --replicas=0 --all -n honua
   ```

2. **Use Reserved Instances for production** (save up to 72%)

3. **Enable cost allocation tags**:
   - All resources tagged with Environment, CostCenter, Owner
   - Use AWS Cost Explorer or Azure Cost Management

4. **Set budget alerts** (automatically configured)

5. **Review and delete unused resources**:
   - Old container images (lifecycle policies configured)
   - Unused snapshots
   - Detached volumes

## Security Best Practices

### Secrets Management

- All secrets stored in AWS Secrets Manager / Azure Key Vault / GCP Secret Manager
- Automatic rotation supported
- Never commit secrets to version control

### Network Security

- Private subnets for workloads
- Database in isolated subnets
- Network policies enabled in Kubernetes
- VPC flow logs enabled

### Encryption

- Encryption at rest (KMS/CMK)
- Encryption in transit (TLS)
- Encrypted Terraform state

### IAM Best Practices

- Least privilege access
- Service accounts for workload identity
- OIDC federation for GitHub Actions
- Customer isolation with dedicated IAM users/service principals

### Compliance

- SOC2 and ISO27001 compliant
- Audit logging enabled
- Compliance tags on all resources

## Troubleshooting

### Terraform Errors

**State Lock Error**:
```bash
# Force unlock (use with caution)
terraform force-unlock <lock-id>
```

**Resource Already Exists**:
```bash
# Import existing resource
terraform import module.networking.aws_vpc.honua vpc-xxxxx
```

### Kubernetes Issues

**Nodes Not Ready**:
```bash
kubectl get nodes
kubectl describe node <node-name>

# Check EKS cluster status
aws eks describe-cluster --name honua-dev
```

**Pod Scheduling Issues**:
```bash
kubectl get pods -A
kubectl describe pod <pod-name> -n <namespace>

# Check cluster capacity
kubectl top nodes
```

### Database Connection Issues

**Connection Timeout**:
- Verify security group rules
- Check if in correct VPC/subnet
- Verify database endpoint

```bash
# Test connectivity from pod
kubectl run -it --rm psql-test --image=postgres:15 --restart=Never -- \
  psql -h <db-endpoint> -U honua_admin -d honua_dev
```

## Multi-Cloud Deployment

### Azure Deployment

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription <subscription-id>

# Deploy
cd infrastructure/terraform/environments/dev
# Edit main.tf to set cloud_provider = "azure"
terraform init
terraform plan -out=tfplan
terraform apply tfplan
```

### GCP Deployment

```bash
# Login to GCP
gcloud auth login
gcloud config set project <project-id>

# Deploy
cd infrastructure/terraform/environments/dev
# Edit main.tf to set cloud_provider = "gcp"
terraform init
terraform plan -out=tfplan
terraform apply tfplan
```

## Support

For issues or questions:

1. Check the [Troubleshooting](#troubleshooting) section
2. Review Terraform logs: `TF_LOG=DEBUG terraform apply`
3. Check cloud provider documentation
4. Open an issue in the Honua repository

## License

Copyright (c) 2024 Honua. All rights reserved.
