# Honua Build Orchestrator - Registry Provisioning Infrastructure

This directory contains the complete infrastructure-as-code and IAM policy definitions for provisioning and managing the Honua build orchestrator system across AWS, Azure, and GCP.

## Quick Start

```bash
# 1. Review and customize variables
cp terraform/registry-provisioning.tfvars.example terraform/terraform.tfvars
vim terraform/terraform.tfvars

# 2. Initialize Terraform
cd terraform
terraform init

# 3. Review the plan
terraform plan

# 4. Deploy infrastructure
terraform apply

# 5. Store GitHub PAT in secrets
# AWS
aws secretsmanager put-secret-value \
  --secret-id honua/github-pat-build-orchestrator \
  --secret-string "ghp_your_token_here"

# Azure
az keyvault secret set \
  --vault-name honua-orchestrator-kv \
  --name github-pat \
  --value "ghp_your_token_here"

# GCP
echo -n "ghp_your_token_here" | gcloud secrets versions add \
  honua-github-pat-build-orchestrator \
  --data-file=-
```

For detailed setup instructions, see [IAM Setup Guide](/home/mike/projects/HonuaIO/docs/IAM_SETUP.md).

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                   Honua Build Orchestrator                  │
│                                                             │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐  │
│  │   AWS IAM     │  │ Azure Managed │  │ GCP Service   │  │
│  │     Role      │  │   Identity    │  │   Account     │  │
│  └───────┬───────┘  └───────┬───────┘  └───────┬───────┘  │
│          │                  │                  │          │
└──────────┼──────────────────┼──────────────────┼──────────┘
           │                  │                  │
           │                  │                  │
    ┌──────▼──────┐    ┌──────▼──────┐    ┌──────▼──────┐
    │  AWS ECR    │    │  Azure ACR  │    │   GCP AR    │
    │ Repositories│    │ Repositories│    │ Repositories│
    └──────┬──────┘    └──────┬──────┘    └──────┬──────┘
           │                  │                  │
           │                  │                  │
    ┌──────▼──────┐    ┌──────▼──────┐    ┌──────▼──────┐
    │  Customer   │    │  Customer   │    │  Customer   │
    │  IAM Users  │    │   Tokens    │    │   Service   │
    │             │    │             │    │  Accounts   │
    └─────────────┘    └─────────────┘    └─────────────┘
```

### Key Components

1. **Build Orchestrator Identity**
   - AWS: IAM Role with scoped permissions
   - Azure: User-assigned Managed Identity with custom RBAC role
   - GCP: Service Account with custom IAM role

2. **Container Registries**
   - AWS ECR: Repositories under `honua/*` namespace
   - Azure ACR: Repositories within shared registry
   - GCP Artifact Registry: Docker repositories per customer

3. **Customer Access Credentials**
   - AWS: IAM users with repository-scoped policies
   - Azure: Registry tokens with scope maps
   - GCP: Service accounts with repository-level IAM bindings

4. **Secret Management**
   - AWS Secrets Manager: GitHub PAT, license keys
   - Azure Key Vault: GitHub PAT, orchestrator secrets
   - GCP Secret Manager: GitHub PAT, build credentials

## Directory Structure

```
infrastructure/
├── iam/                                    # IAM policy definitions
│   ├── README.md                           # IAM documentation
│   ├── aws-build-orchestrator-policy.json  # AWS orchestrator policy
│   ├── aws-customer-template-policy.json   # AWS customer policy template
│   ├── azure-build-orchestrator-role.json  # Azure custom RBAC role
│   └── gcp-build-orchestrator-role.yaml    # GCP custom IAM role
│
├── terraform/                              # Terraform configurations
│   ├── registry-provisioning.tf            # Main Terraform configuration
│   └── registry-provisioning.tfvars.example # Example variables file
│
├── github/                                 # GitHub App configuration
│   └── app-manifest.json                   # GitHub App manifest
│
└── REGISTRY_PROVISIONING_README.md         # This file

docs/
└── IAM_SETUP.md                            # Detailed setup guide
```

## Infrastructure Components

### AWS Resources

Created by Terraform:

- **IAM Policy**: `honua-build-orchestrator-policy`
  - ECR repository management (create, delete, configure)
  - ECR image operations (push, pull)
  - IAM user/policy management for customers
  - Secrets Manager access (GitHub PAT, license keys)
  - KMS decryption for secrets
  - CloudWatch Logs for orchestrator activity

- **IAM Role**: `honua-build-orchestrator`
  - Assumable by ECS tasks, Lambda functions, EC2 instances
  - Attached policy: `honua-build-orchestrator-policy`

- **KMS Key**: `alias/honua/build-orchestrator`
  - Encrypts Secrets Manager secrets
  - Automatic key rotation enabled

- **Secrets Manager Secret**: `honua/github-pat-build-orchestrator`
  - Stores GitHub PAT (manual population required)

**Outputs:**
- `aws_orchestrator_role_arn`: ARN of the IAM role
- `aws_orchestrator_policy_arn`: ARN of the IAM policy
- `aws_kms_key_id`: KMS key ID for secrets
- `aws_github_pat_secret_arn`: Secret ARN for GitHub PAT

### Azure Resources

Created by Terraform:

- **Resource Group**: `honua-registries-{environment}`
  - Contains all customer ACR resources

- **Managed Identity**: `honua-build-orchestrator`
  - User-assigned identity for orchestrator service

- **Custom RBAC Role**: `Honua Build Orchestrator`
  - Container registry management
  - Token and scope map management
  - Managed identity management
  - Key Vault secret access

- **Role Assignment**: Links managed identity to custom role

- **Key Vault**: `honua-orchestrator-kv`
  - Stores GitHub PAT and orchestrator secrets
  - Access policy for managed identity

**Outputs:**
- `azure_managed_identity_client_id`: Client ID of managed identity
- `azure_managed_identity_principal_id`: Principal ID for role assignments
- `azure_resource_group_name`: Resource group name
- `azure_key_vault_uri`: Key Vault URI

### GCP Resources

Created by Terraform:

- **Service Account**: `honua-build-orchestrator@PROJECT.iam.gserviceaccount.com`
  - Identity for orchestrator operations

- **Custom IAM Role**: `honuaBuildOrchestrator`
  - Artifact Registry management
  - Service account management
  - Secret Manager access
  - Project-level resource access

- **IAM Binding**: Links service account to custom role

- **Secret Manager Secret**: `honua-github-pat-build-orchestrator`
  - Stores GitHub PAT (manual population required)

- **Secret IAM Binding**: Grants service account access to GitHub PAT

**Outputs:**
- `gcp_service_account_email`: Service account email
- `gcp_custom_role_id`: Custom IAM role ID
- `gcp_github_pat_secret_id`: Secret Manager secret ID

## Security Features

### Least Privilege

All policies implement strict least-privilege access:

1. **Resource Patterns**: Only resources matching specific patterns are accessible
   - AWS: `honua/*`, `honua-customer-*`
   - Azure: Scoped to specific resource group
   - GCP: Repository-specific permissions

2. **Action Restrictions**: Only necessary actions are granted
   - Read/write/delete for owned resources only
   - No wildcard permissions on sensitive operations

3. **Tag-Based Controls** (AWS): Resources must have `ManagedBy: honua-orchestrator` tag
   ```json
   "Condition": {
     "StringEquals": {
       "iam:ResourceTag/ManagedBy": "honua-orchestrator"
     }
   }
   ```

4. **Regional Restrictions** (AWS): Operations limited to approved regions
   ```json
   "Condition": {
     "StringEquals": {
       "aws:RequestedRegion": ["us-east-1", "us-west-2", "eu-west-1", "ap-southeast-1"]
     }
   }
   ```

### Secret Management

1. **Encryption at Rest**
   - AWS: KMS encryption for Secrets Manager
   - Azure: Key Vault with soft delete and purge protection
   - GCP: Secret Manager with automatic encryption

2. **Access Control**
   - Scoped access to specific secret patterns
   - Audit logging for all secret access
   - No direct secret exposure in Terraform state

3. **Rotation**
   - Support for secret rotation without downtime
   - Version management for secret updates

### Network Security

Recommendations for production deployments:

1. **AWS**: Use VPC endpoints for ECR and Secrets Manager
2. **Azure**: Enable private endpoints for ACR and Key Vault
3. **GCP**: Enable Private Google Access for VPC subnets

See [IAM Setup Guide - Network Security](/home/mike/projects/HonuaIO/docs/IAM_SETUP.md#network-security) for configuration examples.

## Customer Provisioning Workflow

When provisioning a new customer:

### 1. Create Container Registry

```bash
# AWS ECR
honua-orchestrator create-registry \
  --provider aws \
  --customer-id customer-123 \
  --region us-east-1

# Azure ACR
honua-orchestrator create-registry \
  --provider azure \
  --customer-id customer-123

# GCP Artifact Registry
honua-orchestrator create-registry \
  --provider gcp \
  --customer-id customer-123 \
  --location us-central1
```

### 2. Create Customer Access Credentials

```bash
# AWS IAM User
honua-orchestrator create-credentials \
  --provider aws \
  --customer-id customer-123 \
  --registry-arn arn:aws:ecr:us-east-1:ACCOUNT:repository/honua/customer-123

# Azure Token
honua-orchestrator create-credentials \
  --provider azure \
  --customer-id customer-123 \
  --registry-name honuaregistry

# GCP Service Account
honua-orchestrator create-credentials \
  --provider gcp \
  --customer-id customer-123 \
  --repository projects/PROJECT/locations/us-central1/repositories/customer-123
```

### 3. Build and Push Customer Image

```bash
# Clone customer repository
honua-orchestrator build \
  --customer-id customer-123 \
  --github-repo https://github.com/customer/repo \
  --dockerfile Dockerfile \
  --tag v1.0.0 \
  --push-to aws,azure,gcp
```

### 4. Deliver Credentials to Customer

Credentials are returned in a secure format:

```json
{
  "customer_id": "customer-123",
  "registries": {
    "aws": {
      "registry": "123456789012.dkr.ecr.us-east-1.amazonaws.com",
      "repository": "honua/customer-123",
      "access_key_id": "AKIA...",
      "secret_access_key": "...",
      "region": "us-east-1"
    },
    "azure": {
      "registry": "honuaregistry.azurecr.io",
      "repository": "customer-123",
      "username": "customer-123-token",
      "password": "..."
    },
    "gcp": {
      "registry": "us-central1-docker.pkg.dev",
      "repository": "PROJECT/customer-123",
      "service_account_key": "{...}"
    }
  }
}
```

## Usage Examples

### Deploying Infrastructure

```bash
cd /home/mike/projects/HonuaIO/infrastructure/terraform

# Initialize providers
terraform init

# Review changes
terraform plan \
  -var="gcp_project_id=honua-prod-12345" \
  -var="github_org=honua-io" \
  -var="environment=production"

# Deploy
terraform apply \
  -var="gcp_project_id=honua-prod-12345" \
  -var="github_org=honua-io" \
  -var="environment=production"
```

### Populating Secrets

```bash
# Generate GitHub PAT at https://github.com/settings/tokens
# Scopes needed: repo (for private repos), read:packages, write:packages

# Store in AWS
aws secretsmanager put-secret-value \
  --secret-id honua/github-pat-build-orchestrator \
  --secret-string "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" \
  --region us-east-1

# Store in Azure
az keyvault secret set \
  --vault-name honua-orchestrator-kv \
  --name github-pat \
  --value "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"

# Store in GCP
echo -n "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" | \
  gcloud secrets versions add honua-github-pat-build-orchestrator \
    --data-file=- \
    --project=honua-prod-12345
```

### Testing Permissions

```bash
# Test AWS orchestrator role
aws sts assume-role \
  --role-arn $(terraform output -raw aws_orchestrator_role_arn) \
  --role-session-name test

# Test Azure managed identity (from Azure VM/AKS)
az login --identity --username $(terraform output -raw azure_managed_identity_client_id)
az acr list --resource-group $(terraform output -raw azure_resource_group_name)

# Test GCP service account
gcloud auth activate-service-account \
  --key-file=sa-key.json
gcloud artifacts repositories list --project=honua-prod-12345
```

## Monitoring and Compliance

### Audit Logging

Enable comprehensive audit logging:

**AWS CloudTrail:**
```bash
aws cloudtrail create-trail \
  --name honua-orchestrator-audit \
  --s3-bucket-name honua-audit-logs \
  --include-global-service-events \
  --is-multi-region-trail
```

**Azure Activity Log:**
```bash
az monitor log-analytics workspace create \
  --resource-group honua-registries-production \
  --workspace-name honua-orchestrator-logs

az monitor diagnostic-settings create \
  --name orchestrator-diagnostics \
  --resource $(az identity show -n honua-build-orchestrator -g honua-registries-production --query id -o tsv) \
  --workspace $(az monitor log-analytics workspace show -n honua-orchestrator-logs -g honua-registries-production --query id -o tsv) \
  --logs '[{"category": "AuditEvent", "enabled": true}]'
```

**GCP Cloud Audit Logs:**
```bash
# Audit logs are enabled by default for Admin Activity
# Enable Data Access logs for Artifact Registry
gcloud logging sinks create honua-artifact-registry-audit \
  bigquery.googleapis.com/projects/honua-prod-12345/datasets/audit_logs \
  --log-filter='protoPayload.serviceName="artifactregistry.googleapis.com"'
```

### Compliance

This infrastructure supports compliance with:

- **SOC 2 Type II**: Audit trails, access controls, encryption at rest
- **ISO 27001**: IAM policies, secret management, monitoring
- **GDPR**: Customer data isolation, access logging
- **HIPAA** (if applicable): Encryption, access controls, audit logging

### Cost Monitoring

Tag all resources for cost allocation:

```hcl
tags = {
  Project     = "Honua"
  ManagedBy   = "Terraform"
  Component   = "BuildOrchestrator"
  Environment = "production"
  CostCenter  = "engineering"
}
```

Set up cost alerts:

```bash
# AWS Budget
aws budgets create-budget \
  --account-id 123456789012 \
  --budget file://budget.json \
  --notifications-with-subscribers file://notifications.json

# Azure Cost Management
az consumption budget create \
  --budget-name honua-orchestrator-budget \
  --amount 1000 \
  --time-grain Monthly \
  --resource-group honua-registries-production

# GCP Budget
gcloud billing budgets create \
  --billing-account=BILLING_ACCOUNT_ID \
  --display-name="Honua Orchestrator Budget" \
  --budget-amount=1000USD
```

## Maintenance

### Regular Tasks

**Daily:**
- Review audit logs for unauthorized access attempts
- Monitor secret access patterns

**Weekly:**
- Review customer registry usage
- Clean up unused customer credentials
- Check for failed build operations

**Monthly:**
- Review IAM permissions for least privilege
- Audit customer IAM users/service accounts
- Update documentation if policies changed

**Quarterly:**
- Rotate GitHub PAT and app private keys
- Review and update regional restrictions
- Audit all customer resources
- Update Terraform provider versions

### Secret Rotation

Rotate secrets every 90 days:

```bash
# 1. Generate new GitHub PAT
# 2. Store in secret managers (all three clouds)
# 3. Restart orchestrator service
# 4. Verify new PAT works
# 5. Delete old PAT from GitHub

# Automated rotation (AWS Secrets Manager)
aws secretsmanager rotate-secret \
  --secret-id honua/github-pat-build-orchestrator \
  --rotation-lambda-arn arn:aws:lambda:us-east-1:ACCOUNT:function:rotate-github-pat
```

### Cleanup

Remove unused customer resources:

```bash
# List all customer resources older than 90 days
honua-orchestrator audit \
  --provider all \
  --older-than 90d \
  --unused

# Delete specific customer resources
honua-orchestrator delete \
  --customer-id customer-123 \
  --provider all \
  --confirm
```

## Troubleshooting

For detailed troubleshooting, see the [IAM Setup Guide - Troubleshooting](/home/mike/projects/HonuaIO/docs/IAM_SETUP.md#troubleshooting).

### Common Issues

**Issue**: Terraform fails with permission errors

**Solution**:
- Verify AWS/Azure/GCP credentials have sufficient permissions
- Check provider authentication: `aws sts get-caller-identity`, `az account show`, `gcloud auth list`
- Review required permissions in IAM Setup Guide

**Issue**: Cannot push images to customer registries

**Solution**:
- Verify ECR/ACR/Artifact Registry exists
- Check orchestrator role/identity has push permissions
- Test authentication: `docker login` with orchestrator credentials

**Issue**: Secret access fails

**Solution**:
- Verify secret exists and has correct name pattern
- Check KMS/Key Vault permissions
- Review audit logs for access denials

## Support and Contributing

For questions, issues, or contributions:

1. **Documentation**: Review [IAM Setup Guide](/home/mike/projects/HonuaIO/docs/IAM_SETUP.md)
2. **Issues**: Open a GitHub issue with `component: build-orchestrator` label
3. **Security**: Report security issues to security@honua.io

## License

Copyright (c) 2025 HonuaIO. All rights reserved.
