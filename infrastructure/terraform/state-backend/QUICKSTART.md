# Terraform State Backend - Quick Start Guide

This guide will help you set up Terraform remote state with locking in under 5 minutes.

## Choose Your Cloud Provider

### AWS Setup (Recommended for AWS deployments)

```bash
# 1. Navigate to scripts directory
cd infrastructure/terraform/state-backend/scripts

# 2. Run setup script (auto-configures everything)
./setup-aws-backend.sh

# 3. Copy the output - you'll need it for step 4

# 4. In your Terraform project, add backend configuration
cat > backend.tf <<'EOF'
terraform {
  backend "s3" {
    bucket         = "YOUR-BUCKET-NAME"  # From step 3
    key            = "my-project/terraform.tfstate"  # Update this path
    region         = "us-east-1"
    encrypt        = true
    dynamodb_table = "honua-terraform-locks"
  }
}
EOF

# 5. Initialize with state migration
terraform init -migrate-state

# Done! State is now remote with locking enabled
```

### Azure Setup (Recommended for Azure deployments)

```bash
# 1. Login to Azure
az login

# 2. Navigate to scripts directory
cd infrastructure/terraform/state-backend/scripts

# 3. Run setup script (auto-configures everything)
./setup-azure-backend.sh

# 4. Set access key environment variable (from script output)
export ARM_ACCESS_KEY=$(cat ../azure/.access_key)

# 5. In your Terraform project, add backend configuration
cat > backend.tf <<'EOF'
terraform {
  backend "azurerm" {
    resource_group_name  = "YOUR-RG-NAME"     # From step 3
    storage_account_name = "YOUR-STORAGE"     # From step 3
    container_name       = "tfstate"
    key                  = "my-project/terraform.tfstate"  # Update this path
  }
}
EOF

# 6. Initialize with state migration
terraform init -migrate-state

# Done! State is now remote with locking enabled
```

## Advanced Options

### AWS with Custom Configuration

```bash
./setup-aws-backend.sh \
  --region us-west-2 \
  --bucket my-company-terraform-state \
  --environment prod \
  --enable-replication \
  --replication-region us-east-1
```

### Azure with Custom Configuration

```bash
./setup-azure-backend.sh \
  --location westus2 \
  --resource-group rg-my-tfstate \
  --storage-account mystorageaccount \
  --environment prod \
  --enable-geo-replication
```

## Verifying State Locking

### Test AWS Locking

```bash
# Terminal 1: Run a long operation
cd your-project
terraform apply -auto-approve

# Terminal 2: Try to run another operation (should fail with lock error)
terraform plan
# Error: Error acquiring the state lock
```

### Test Azure Locking

```bash
# Terminal 1: Run a long operation
cd your-project
terraform apply -auto-approve

# Terminal 2: Try to run another operation (should fail with lease error)
terraform plan
# Error: Error acquiring the state lock
# Lease is already present
```

## Common Commands

### View Current Lock (AWS)

```bash
aws dynamodb scan \
  --table-name honua-terraform-locks \
  --region us-east-1
```

### Force Unlock (if needed)

```bash
# Get lock ID from error message, then:
terraform force-unlock <lock-id>
```

### List State Versions (AWS)

```bash
aws s3api list-object-versions \
  --bucket honua-terraform-state \
  --prefix my-project/terraform.tfstate
```

### List State Versions (Azure)

```bash
az storage blob list \
  --account-name sthonuatfstate \
  --container-name tfstate \
  --prefix my-project/ \
  --include v
```

## Troubleshooting

### "Error: Failed to get existing workspaces"

**Solution**: Initialize backend first
```bash
terraform init
```

### "Error: Error acquiring the state lock" (Won't release)

**Solution**: Wait a few minutes or force unlock
```bash
# AWS/Azure: Lock expires automatically after timeout
# Or force unlock (use with caution):
terraform force-unlock <lock-id>
```

### AWS: "Error: AccessDenied"

**Solution**: Check IAM permissions
```bash
# Verify credentials
aws sts get-caller-identity

# Attach policy
aws iam attach-user-policy \
  --user-name your-user \
  --policy-arn arn:aws:iam::ACCOUNT:policy/HonuaTerraformStateAccess
```

### Azure: "Error: storage: service returned error: StatusCode=403"

**Solution**: Set access key or login with Azure CLI
```bash
# Option 1: Set access key
export ARM_ACCESS_KEY=$(cat infrastructure/terraform/state-backend/azure/.access_key)

# Option 2: Use Azure CLI auth
az login
```

## What Happens Behind the Scenes

### AWS (S3 + DynamoDB)

1. **State Storage**: S3 bucket stores state files with versioning
2. **Locking**: DynamoDB table tracks locks
3. **Process**:
   - Terraform creates item in DynamoDB with lock details
   - If item exists → lock held → operation fails
   - Lock released when operation completes

### Azure (Storage Account + Blob Leases)

1. **State Storage**: Blob container stores state files with versioning
2. **Locking**: Blob leases provide locking mechanism
3. **Process**:
   - Terraform acquires 60-second lease on state blob
   - Lease automatically renewed during operation
   - If lease held → operation waits or fails
   - Lease released when operation completes or expires

## Cost

Both backends are extremely cost-effective:

- **AWS**: ~$0.30/month (S3 + DynamoDB)
- **Azure**: ~$0.06/month (Storage Account + Key Vault)

## Next Steps

1. ✅ Set up state backend (you just did this!)
2. 🔄 Migrate existing projects to remote state
3. 📚 Read full documentation: [README.md](README.md)
4. 🔐 Review security best practices
5. 🚀 Set up CI/CD with remote state

## Support

- Full documentation: [README.md](README.md)
- AWS Backend docs: [aws/](aws/)
- Azure Backend docs: [azure/](azure/)
- Terraform docs: https://www.terraform.io/language/settings/backends
