# Terraform State Backend with State Locking

This directory contains infrastructure-as-code for setting up Terraform remote state backends with state locking for both AWS and Azure.

## Overview

Terraform state files contain sensitive information about your infrastructure. Storing state remotely with locking prevents:

- **Data Loss**: State is backed up and versioned in cloud storage
- **Concurrent Modifications**: Locking prevents multiple users from modifying state simultaneously
- **Security Issues**: Encrypted storage protects sensitive data
- **Collaboration Problems**: Team members can share state securely

## Architecture

### AWS Backend
- **S3 Bucket**: Stores Terraform state files with encryption and versioning
- **DynamoDB Table**: Provides distributed state locking mechanism
- **IAM Policies**: Controls access to state files
- **Optional**: Cross-region replication for disaster recovery

### Azure Backend
- **Storage Account**: Stores Terraform state files with encryption and versioning
- **Blob Container**: Organizes state files
- **Blob Leases**: Native Azure mechanism for state locking (no additional resource needed!)
- **Key Vault**: Securely stores access keys
- **Optional**: Geo-redundant storage for disaster recovery

## Quick Start

### AWS Backend Setup

1. **Navigate to AWS backend directory**
   ```bash
   cd infrastructure/terraform/state-backend/aws
   ```

2. **Configure variables**
   ```bash
   cp terraform.tfvars.example terraform.tfvars
   # Edit terraform.tfvars with your values
   ```

3. **Initialize and deploy**
   ```bash
   terraform init
   terraform plan
   terraform apply
   ```

4. **Save the outputs**
   ```bash
   terraform output -json > backend-config.json
   ```

5. **Use in your projects**
   - Copy `backend.tf.example` to your project
   - Update with values from outputs
   - Run `terraform init -migrate-state`

### Azure Backend Setup

1. **Navigate to Azure backend directory**
   ```bash
   cd infrastructure/terraform/state-backend/azure
   ```

2. **Login to Azure**
   ```bash
   az login
   ```

3. **Configure variables**
   ```bash
   cp terraform.tfvars.example terraform.tfvars
   # Edit terraform.tfvars with your values
   ```

4. **Initialize and deploy**
   ```bash
   terraform init
   terraform plan
   terraform apply
   ```

5. **Save the outputs**
   ```bash
   terraform output -json > backend-config.json
   # Save the access key for later use
   terraform output -raw storage_account_primary_access_key > access_key.txt
   ```

6. **Use in your projects**
   - Copy `backend.tf.example` to your project
   - Update with values from outputs
   - Set environment variable: `export ARM_ACCESS_KEY=$(cat access_key.txt)`
   - Run `terraform init -migrate-state`

## State Locking Mechanisms

### AWS: DynamoDB Locking

How it works:
1. Before making changes, Terraform attempts to create an item in DynamoDB table
2. Item key is based on the state file path
3. If item already exists, another process has the lock → operation fails
4. Lock includes metadata: who, when, why
5. Lock is automatically released when operation completes

Lock attributes:
```json
{
  "LockID": "bucket-name/path/to/state",
  "Info": {
    "ID": "lock-uuid",
    "Operation": "OperationTypeApply",
    "Who": "user@host",
    "Version": "1.5.0",
    "Created": "2025-10-18T10:30:00Z",
    "Path": "path/to/state"
  }
}
```

### Azure: Blob Lease Locking

How it works:
1. Before making changes, Terraform acquires a lease on the state blob
2. Lease is held for 60 seconds and automatically renewed
3. If another process tries to acquire lease, it waits or fails
4. Lease includes metadata stored as blob metadata
5. Lease is automatically released when operation completes
6. If Terraform crashes, lease expires after 60 seconds

Advantages over DynamoDB:
- No additional resource needed
- Built into Azure Blob Storage
- Automatic lease expiration prevents indefinite locks
- Lower cost (no separate service)

## State Management Procedures

### Viewing Current Locks

**AWS:**
```bash
# Using AWS CLI
aws dynamodb scan \
  --table-name honua-terraform-locks \
  --region us-east-1

# Or check in AWS Console → DynamoDB → Tables → honua-terraform-locks
```

**Azure:**
```bash
# Using Azure CLI
az storage blob show \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --name path/to/terraform.tfstate \
  --query "properties.lease"

# Or check in Azure Portal → Storage Account → Containers → tfstate → Blob properties
```

### Force Unlocking State

**Important**: Only force unlock if you're certain no one else is running Terraform!

**AWS:**
```bash
# Terraform will show the Lock ID in error message
terraform force-unlock <lock-id>
```

**Azure:**
```bash
# Terraform will show the Lease ID in error message
terraform force-unlock <lease-id>
```

### Migrating to Remote State

1. **Backup current state**
   ```bash
   cp terraform.tfstate terraform.tfstate.backup
   ```

2. **Add backend configuration**
   ```bash
   cp backend.tf.example backend.tf
   # Edit backend.tf with your values
   ```

3. **Initialize with migration**
   ```bash
   terraform init -migrate-state
   ```

4. **Verify migration**
   ```bash
   # AWS: Check S3 bucket for state file
   aws s3 ls s3://bucket-name/path/to/

   # Azure: Check storage account
   az storage blob list \
     --account-name sthonuatfstate \
     --container-name tfstate
   ```

5. **Delete local state** (after verification)
   ```bash
   rm terraform.tfstate*
   ```

### Accessing State from CI/CD

**AWS (GitHub Actions, GitLab CI, etc.):**
```yaml
env:
  AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
  AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
  AWS_REGION: us-east-1

steps:
  - name: Terraform Init
    run: terraform init
```

**Azure (GitHub Actions, GitLab CI, etc.):**
```yaml
env:
  ARM_CLIENT_ID: ${{ secrets.ARM_CLIENT_ID }}
  ARM_CLIENT_SECRET: ${{ secrets.ARM_CLIENT_SECRET }}
  ARM_SUBSCRIPTION_ID: ${{ secrets.ARM_SUBSCRIPTION_ID }}
  ARM_TENANT_ID: ${{ secrets.ARM_TENANT_ID }}

steps:
  - name: Terraform Init
    run: terraform init
```

## Security Best Practices

### AWS

1. **Enable bucket encryption** ✅ (Configured)
2. **Enable bucket versioning** ✅ (Configured)
3. **Block public access** ✅ (Configured)
4. **Enable access logging** ✅ (Configured)
5. **Use IAM roles** instead of access keys where possible
6. **Enable DynamoDB point-in-time recovery** (Optional, configurable)
7. **Enable cross-region replication** for critical deployments (Optional)
8. **Restrict IAM policies** to principle of least privilege

### Azure

1. **Enable storage encryption** ✅ (Configured)
2. **Enable blob versioning** ✅ (Configured)
3. **Enable soft delete** ✅ (Configured)
4. **Use Azure AD authentication** instead of access keys where possible
5. **Store access keys in Key Vault** ✅ (Configured)
6. **Enable geo-redundant storage** for critical deployments (Optional)
7. **Use private endpoints** in production (Optional, configurable)
8. **Enable change feed** for audit trail ✅ (Configured)

## Cost Estimates

### AWS Backend

| Resource | Configuration | Monthly Cost |
|----------|--------------|--------------|
| S3 Bucket | 1 GB storage, versioning | ~$0.03 |
| S3 Requests | 1000 operations/month | ~$0.01 |
| DynamoDB Table | On-demand, PITR enabled | ~$0.25 |
| Data Transfer | Minimal | ~$0.01 |
| **Total** | | **~$0.30/month** |

### Azure Backend

| Resource | Configuration | Monthly Cost |
|----------|--------------|--------------|
| Storage Account | LRS, 1 GB | ~$0.02 |
| Blob Operations | 1000 operations/month | ~$0.01 |
| Key Vault | Standard tier | ~$0.03 |
| **Total** | | **~$0.06/month** |

**Note**: Costs are minimal because state files are small and operations are infrequent.

## Troubleshooting

### "Error acquiring the state lock"

**Cause**: Another Terraform process is running or previous run didn't clean up

**Solution**:
1. Check if anyone else is running Terraform
2. Wait a few minutes (Azure leases expire automatically)
3. If certain no one else is running: `terraform force-unlock <lock-id>`

### "Error loading state: AccessDenied"

**AWS**: Check IAM permissions, ensure correct AWS credentials
**Azure**: Check RBAC roles, ensure `ARM_ACCESS_KEY` is set or Azure CLI is logged in

### "Backend configuration changed"

**Cause**: Backend configuration in code doesn't match initialized backend

**Solution**:
```bash
terraform init -reconfigure
```

### State file version mismatch

**Cause**: State file created with newer Terraform version

**Solution**:
```bash
# Upgrade Terraform to match or higher version
terraform version
# Upgrade: https://www.terraform.io/downloads
```

## Disaster Recovery

### Restoring from S3 Versions (AWS)

```bash
# List versions
aws s3api list-object-versions \
  --bucket honua-terraform-state \
  --prefix path/to/terraform.tfstate

# Restore specific version
aws s3api get-object \
  --bucket honua-terraform-state \
  --key path/to/terraform.tfstate \
  --version-id <version-id> \
  terraform.tfstate.restored
```

### Restoring from Blob Versions (Azure)

```bash
# List versions
az storage blob list \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --prefix path/to/terraform.tfstate \
  --include v

# Restore specific version
az storage blob download \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --name path/to/terraform.tfstate \
  --version-id <version-id> \
  --file terraform.tfstate.restored
```

### Restoring from Soft Delete (Azure)

```bash
# List deleted blobs
az storage blob list \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --include d

# Undelete blob
az storage blob undelete \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --name path/to/terraform.tfstate
```

## Multi-Team Usage

### Workspace Strategy

Use Terraform workspaces for environment isolation:

```bash
# Create workspaces
terraform workspace new dev
terraform workspace new staging
terraform workspace new prod

# Switch between workspaces
terraform workspace select dev

# List workspaces
terraform workspace list
```

State files are stored separately:
- AWS: `s3://bucket/env:/dev/path/terraform.tfstate`
- Azure: `tfstate/env:/dev/path/terraform.tfstate`

### Access Control

**AWS**: Create separate IAM roles per team/environment
**Azure**: Use Azure RBAC roles per team/environment

Example Azure RBAC:
```bash
# Grant team access to specific container/path
az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee-object-id <team-group-id> \
  --scope /subscriptions/.../storageAccounts/.../blobServices/default/containers/tfstate
```

## Additional Resources

- [Terraform Backend Configuration](https://www.terraform.io/language/settings/backends/configuration)
- [AWS S3 Backend](https://www.terraform.io/language/settings/backends/s3)
- [Azure Backend](https://www.terraform.io/language/settings/backends/azurerm)
- [State Locking](https://www.terraform.io/language/state/locking)
- [Terraform Workspaces](https://www.terraform.io/language/state/workspaces)

## Support

For issues or questions:
1. Check troubleshooting section above
2. Review Terraform documentation
3. Open an issue in the project repository
