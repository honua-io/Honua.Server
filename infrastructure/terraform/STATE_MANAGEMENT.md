# Terraform State Management Guide

## Overview

This document provides comprehensive guidance on Terraform state management for the Honua project, including remote state storage, state locking, and best practices.

## Why Remote State?

### Problems with Local State

❌ **No Collaboration**: Team members can't share state
❌ **Data Loss Risk**: Local files can be accidentally deleted
❌ **No Locking**: Multiple users can corrupt state
❌ **No Versioning**: Can't recover from mistakes
❌ **Security Risk**: Secrets stored in plaintext locally

### Benefits of Remote State

✅ **Team Collaboration**: Shared state accessible to all team members
✅ **Automatic Backups**: Cloud storage with versioning
✅ **State Locking**: Prevents concurrent modifications
✅ **Encryption**: State encrypted at rest and in transit
✅ **Access Control**: IAM/RBAC for state access
✅ **Disaster Recovery**: Point-in-time recovery and replication

## State Backend Options

### AWS Backend (S3 + DynamoDB)

**Best for**:
- AWS-native deployments
- Multi-region deployments with Route53
- Teams using AWS services

**Features**:
- S3 bucket for state storage (encrypted, versioned)
- DynamoDB table for distributed state locking
- Cross-region replication support
- CloudWatch integration for monitoring

**Setup**: 5 minutes with automated script
**Cost**: ~$0.30/month

### Azure Backend (Storage Account + Blob Leases)

**Best for**:
- Azure-native deployments
- Teams using Azure services
- Cost-conscious projects

**Features**:
- Storage Account for state storage (encrypted, versioned)
- Built-in blob lease locking (no extra resource!)
- Geo-redundant storage support
- Azure Monitor integration

**Setup**: 5 minutes with automated script
**Cost**: ~$0.06/month

## Quick Start

### 1. Deploy State Backend

Choose your cloud provider and run the setup script:

**AWS**:
```bash
cd infrastructure/terraform/state-backend/scripts
./setup-aws-backend.sh
```

**Azure**:
```bash
cd infrastructure/terraform/state-backend/scripts
az login
./setup-azure-backend.sh
```

### 2. Configure Your Projects

Copy the backend configuration to your Terraform projects:

**AWS**:
```bash
# Copy example
cp infrastructure/terraform/aws/backend.tf.example infrastructure/terraform/aws/backend.tf

# Edit with your values (from setup script output)
vim infrastructure/terraform/aws/backend.tf
```

**Azure**:
```bash
# Set access key
export ARM_ACCESS_KEY=$(cat infrastructure/terraform/state-backend/azure/.access_key)

# Copy example
cp infrastructure/terraform/azure/backend.tf.example infrastructure/terraform/azure/backend.tf

# Edit with your values (from setup script output)
vim infrastructure/terraform/azure/backend.tf
```

### 3. Migrate to Remote State

```bash
cd your-terraform-project

# Backup local state (just in case)
cp terraform.tfstate terraform.tfstate.backup

# Initialize with backend migration
terraform init -migrate-state

# Verify migration
terraform state list

# Delete local state (after verification)
rm terraform.tfstate*
```

## State Locking Deep Dive

### How State Locking Works

State locking prevents two or more processes from modifying Terraform state simultaneously, which could lead to corruption.

#### AWS DynamoDB Locking

```
terraform apply
    ↓
1. Attempt to create item in DynamoDB table
   Key: "bucket-name/path/to/state"
   Value: { ID, Who, When, Operation }
    ↓
2a. Success → Lock acquired → Proceed with operation
2b. Item exists → Lock held by another process → Fail
    ↓
3. Operation completes → Delete item → Lock released
```

**Lock information stored**:
- Lock ID (unique identifier)
- Who acquired lock (user@host)
- When acquired (timestamp)
- Operation type (apply/plan/destroy)
- Terraform version

#### Azure Blob Lease Locking

```
terraform apply
    ↓
1. Attempt to acquire lease on state blob
   Lease duration: 60 seconds (auto-renewed)
    ↓
2a. Success → Lease acquired → Proceed with operation
2b. Lease held → Wait or fail
    ↓
3. Operation completes → Release lease → Lock released
4. If crashed → Lease expires after 60 seconds
```

**Advantages**:
- No additional resource needed (built into Azure Storage)
- Automatic expiration prevents indefinite locks
- Lower cost

### Viewing Active Locks

**AWS**:
```bash
# Using AWS CLI
aws dynamodb scan \
  --table-name honua-terraform-locks \
  --region us-east-1

# Using AWS Console
# Navigate to: DynamoDB → Tables → honua-terraform-locks → Items
```

**Azure**:
```bash
# Using Azure CLI
az storage blob show \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --name path/to/terraform.tfstate \
  --query "properties.lease"

# Using Azure Portal
# Navigate to: Storage Account → Containers → tfstate → Blob → Properties
```

### Force Unlocking

⚠️ **WARNING**: Only force unlock if you're absolutely certain no other process is running Terraform!

```bash
# Terraform will display the lock/lease ID in the error message
terraform force-unlock <lock-id>

# Confirm when prompted
```

**When to force unlock**:
- Terraform crashed and didn't release lock
- Lock is stuck (rare)
- Previous operation was interrupted

**When NOT to force unlock**:
- Someone else is running Terraform
- Unsure if operation is still running
- "Just to see what happens"

## State File Organization

### Recommended Structure

```
s3://honua-terraform-state/ (or Azure equivalent)
├── aws/
│   ├── cloudfront-cdn/terraform.tfstate
│   ├── vpc/terraform.tfstate
│   └── ecs-cluster/terraform.tfstate
├── azure/
│   ├── ai-consultant/terraform.tfstate
│   └── container-apps/terraform.tfstate
├── multi-region/
│   ├── terraform.tfstate
│   └── dr/terraform.tfstate
└── shared/
    ├── dns/terraform.tfstate
    └── monitoring/terraform.tfstate
```

### Key Naming Convention

Use descriptive, hierarchical paths:

```
{cloud-provider}/{environment}/{component}/terraform.tfstate

Examples:
  aws/prod/networking/terraform.tfstate
  azure/dev/ai-services/terraform.tfstate
  gcp/staging/cloud-run/terraform.tfstate
```

## Workspaces vs. Multiple State Files

### Terraform Workspaces

**Use when**: Same infrastructure, different environments (dev/staging/prod)

```bash
# Create workspaces
terraform workspace new dev
terraform workspace new staging
terraform workspace new prod

# Switch workspace
terraform workspace select prod

# State files stored as:
# AWS: s3://bucket/env:/prod/path/terraform.tfstate
# Azure: tfstate/env:/prod/path/terraform.tfstate
```

**Pros**:
- Single configuration
- Easy to switch between environments
- Consistent across environments

**Cons**:
- Easy to apply to wrong environment
- All environments share backend configuration
- Limited isolation

### Multiple State Files

**Use when**: Different infrastructure configurations

```bash
# Different backend configs
terraform/
├── prod/
│   ├── backend.tf  (key = "prod/...")
│   └── main.tf
├── staging/
│   ├── backend.tf  (key = "staging/...")
│   └── main.tf
└── dev/
    ├── backend.tf  (key = "dev/...")
    └── main.tf
```

**Pros**:
- Clear separation
- Different backend permissions per environment
- Harder to make mistakes

**Cons**:
- More configuration files
- Requires careful naming

## Best Practices

### 1. Always Use Remote State in Production

Never use local state for production infrastructure.

### 2. Enable Versioning

Both S3 and Azure Storage support versioning - always enable it!

**Why**: Allows recovery from accidental state corruption or deletion.

### 3. Encrypt State Files

Both backends encrypt state at rest by default. Ensure encryption in transit:

**AWS**: Set `encrypt = true` in backend config ✅
**Azure**: Set `enable_https_traffic_only = true` ✅

### 4. Restrict Access with IAM/RBAC

**AWS**: Use IAM policies to control state access
```hcl
# Attach to users/roles
resource "aws_iam_policy" "terraform_state" {
  name   = "TerraformStateAccess"
  policy = data.aws_iam_policy_document.terraform_state.json
}
```

**Azure**: Use RBAC roles to control state access
```bash
az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee user@example.com \
  --scope /subscriptions/.../storageAccounts/sthonuatfstate
```

### 5. Use Backend Configuration Files

Don't hardcode backend config in Terraform files. Use backend config files:

```bash
# backend-prod.tfvars
bucket = "honua-terraform-state"
key    = "prod/terraform.tfstate"
region = "us-east-1"

# Initialize with config file
terraform init -backend-config=backend-prod.tfvars
```

### 6. Regular State Backups

Even with versioning, create periodic backups:

**AWS**:
```bash
# Backup script
aws s3 sync \
  s3://honua-terraform-state \
  ./state-backups/$(date +%Y%m%d) \
  --region us-east-1
```

**Azure**:
```bash
# Backup script
az storage blob download-batch \
  --account-name sthonuatfstate \
  --source tfstate \
  --destination ./state-backups/$(date +%Y%m%d)
```

### 7. Document State Structure

Maintain a README documenting your state file organization.

## Disaster Recovery

### Scenario 1: Corrupted State File

**Recovery**: Restore from previous version

**AWS**:
```bash
# List versions
aws s3api list-object-versions \
  --bucket honua-terraform-state \
  --prefix path/to/terraform.tfstate

# Download specific version
aws s3api get-object \
  --bucket honua-terraform-state \
  --key path/to/terraform.tfstate \
  --version-id <version-id> \
  terraform.tfstate.restored
```

**Azure**:
```bash
# List versions
az storage blob list \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --include v

# Download specific version
az storage blob download \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --name path/to/terraform.tfstate \
  --version-id <version-id> \
  --file terraform.tfstate.restored
```

### Scenario 2: Accidentally Deleted State File

**Recovery**: Restore from soft delete (Azure) or versions (AWS)

**Azure**:
```bash
# Undelete blob (within retention period)
az storage blob undelete \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --name path/to/terraform.tfstate
```

**AWS**:
```bash
# Restore from version (versioning enabled)
aws s3api copy-object \
  --copy-source honua-terraform-state/path/to/terraform.tfstate?versionId=<version-id> \
  --bucket honua-terraform-state \
  --key path/to/terraform.tfstate
```

### Scenario 3: Complete Backend Loss

**Recovery**: Restore from periodic backups

```bash
# Copy backup to backend
# AWS
aws s3 cp \
  ./state-backups/20251018/terraform.tfstate \
  s3://honua-terraform-state/path/to/terraform.tfstate

# Azure
az storage blob upload \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --name path/to/terraform.tfstate \
  --file ./state-backups/20251018/terraform.tfstate
```

## CI/CD Integration

### GitHub Actions

**AWS**:
```yaml
name: Terraform
on: [push]

env:
  AWS_REGION: us-east-1

jobs:
  terraform:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - uses: aws-actions/configure-aws-credentials@v2
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ env.AWS_REGION }}

      - uses: hashicorp/setup-terraform@v2

      - run: terraform init
      - run: terraform plan
      - run: terraform apply -auto-approve
        if: github.ref == 'refs/heads/main'
```

**Azure**:
```yaml
name: Terraform
on: [push]

jobs:
  terraform:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - uses: hashicorp/setup-terraform@v2

      - run: terraform init
      - run: terraform plan
      - run: terraform apply -auto-approve
        if: github.ref == 'refs/heads/main'
```

## Monitoring and Alerts

### AWS CloudWatch Alarms

Monitor state bucket access:

```hcl
resource "aws_cloudwatch_metric_alarm" "state_access" {
  alarm_name          = "terraform-state-access"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "NumberOfObjects"
  namespace           = "AWS/S3"
  period              = 300
  statistic           = "Average"
  threshold           = 100
  alarm_description   = "Alert on unusual state access"
}
```

### Azure Monitor Alerts

Monitor storage account access:

```bash
az monitor metrics alert create \
  --name terraform-state-access \
  --resource-group rg-honua-tfstate \
  --scopes /subscriptions/.../storageAccounts/sthonuatfstate \
  --condition "avg Transactions > 100" \
  --description "Alert on unusual state access"
```

## Troubleshooting

### Common Issues

1. **"Backend configuration changed"**
   - Run: `terraform init -reconfigure`

2. **"Error acquiring state lock" (persists)**
   - Check for running processes
   - Wait for timeout (Azure: 60s)
   - Force unlock if certain: `terraform force-unlock <id>`

3. **"AccessDenied" or "403 Forbidden"**
   - Check IAM/RBAC permissions
   - Verify credentials are configured
   - Check network ACLs/firewall rules

4. **"State file version too new"**
   - Upgrade Terraform to match or newer version

## Additional Resources

- [State Backend Infrastructure](state-backend/): Setup scripts and modules
- [Quick Start Guide](state-backend/QUICKSTART.md): 5-minute setup
- [Full Documentation](state-backend/README.md): Detailed guide
- [Terraform Docs: Backends](https://www.terraform.io/language/settings/backends)
- [Terraform Docs: State](https://www.terraform.io/language/state)

## Support

For questions or issues:
1. Check [Troubleshooting](#troubleshooting) section
2. Review [state-backend/README.md](state-backend/README.md)
3. Consult Terraform documentation
4. Open an issue in the project repository
