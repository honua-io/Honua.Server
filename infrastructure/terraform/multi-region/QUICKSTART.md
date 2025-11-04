# Multi-Region Deployment Quick Start

Get your HonuaIO multi-region deployment up and running in 15 minutes.

## Prerequisites

- [ ] Terraform >= 1.5.0 installed
- [ ] Cloud CLI tools installed (aws-cli, az, or gcloud)
- [ ] Cloud account credentials configured
- [ ] Git repository cloned

## Step 1: Choose Your Configuration (2 minutes)

### Option A: AWS Production
```bash
cd /home/mike/projects/HonuaIO/infrastructure/terraform/multi-region
cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars`:
```hcl
cloud_provider = "aws"
primary_region = "us-east-1"
dr_region      = "us-west-2"
environment    = "prod"

db_admin_username = "honuaadmin"
db_admin_password = "YOUR_SECURE_PASSWORD_HERE"  # Use AWS Secrets Manager in production
alert_email = "ops@yourcompany.com"
```

### Option B: Azure Production
```hcl
cloud_provider = "azure"
primary_region = "eastus"
dr_region      = "westus2"
environment    = "prod"

db_admin_username = "honuaadmin"
db_admin_password = "YOUR_SECURE_PASSWORD_HERE"  # Use Azure Key Vault in production
alert_email = "ops@yourcompany.com"
```

### Option C: Development (Minimal Cost)
```hcl
cloud_provider            = "aws"
primary_region            = "us-east-2"
dr_region                 = "us-west-2"
environment               = "dev"
primary_instance_count    = 1
dr_instance_count         = 1
primary_instance_type     = "t3.medium"
dr_instance_type          = "t3.small"
enable_db_replication     = false
enable_storage_replication = false
enable_auto_scaling       = false
enable_waf                = false
enable_cdn                = false
```

## Step 2: Initialize Terraform (1 minute)

```bash
terraform init
```

Expected output:
```
Initializing the backend...
Initializing provider plugins...
- Installing hashicorp/aws v5.x.x...
- Installing hashicorp/random v3.x.x...
Terraform has been successfully initialized!
```

## Step 3: Validate Configuration (1 minute)

```bash
terraform validate
```

Expected output:
```
Success! The configuration is valid.
```

## Step 4: Review Deployment Plan (3 minutes)

```bash
terraform plan -out=tfplan
```

Review the output to ensure:
- [ ] Correct regions are configured
- [ ] Instance counts match expectations
- [ ] Database and storage configuration is correct
- [ ] Cost estimates are acceptable

## Step 5: Apply Configuration (5-10 minutes)

```bash
terraform apply tfplan
```

This will create:
- VPCs and networking in both regions
- Security groups and IAM roles
- Compute resources (placeholder outputs)
- Database infrastructure (placeholder outputs)
- Storage buckets (placeholder outputs)
- Monitoring and alerting (placeholder outputs)

## Step 6: Verify Deployment (2 minutes)

```bash
# Get outputs
terraform output -json > deployment.json

# Display key endpoints
terraform output primary_endpoint
terraform output dr_endpoint
terraform output global_endpoint

# Test endpoints (when fully implemented)
PRIMARY=$(terraform output -raw primary_endpoint)
curl -I http://$PRIMARY/health

DR=$(terraform output -raw dr_endpoint)
curl -I http://$DR/health
```

## Step 7: Configure Application (3 minutes)

Use the Terraform outputs to configure your application:

```bash
# Get database connection info
terraform output primary_db_endpoint
terraform output primary_db_identifier

# Get storage bucket names
terraform output primary_storage_bucket
terraform output dr_storage_bucket

# Get Redis endpoint
terraform output primary_redis_endpoint
```

Update your application configuration:
```bash
export DB_HOST=$(terraform output -raw primary_db_endpoint)
export DB_NAME="honua"
export DB_USER="honuaadmin"
export DB_PASSWORD="YOUR_PASSWORD"
export STORAGE_BUCKET=$(terraform output -raw primary_storage_bucket)
export REDIS_HOST=$(terraform output -raw primary_redis_endpoint)
```

## Step 8: Deploy Application (varies)

Deploy your HonuaIO application containers to the infrastructure:

### AWS ECS
```bash
# Build and push container image
docker build -t honuaio/honua-server:latest .
docker push honuaio/honua-server:latest

# Update ECS service (when fully implemented)
aws ecs update-service \
  --cluster honua-prod-us-east-1-cluster \
  --service honua-prod-us-east-1-service \
  --force-new-deployment
```

### Azure Container Apps
```bash
# Build and push container image
docker build -t honuaio/honua-server:latest .
docker push honuaio/honua-server:latest

# Update container app (when fully implemented)
az containerapp update \
  --name honua-prod-eastus-app \
  --resource-group honua-prod-eastus \
  --image honuaio/honua-server:latest
```

## Step 9: Test Deployment (2 minutes)

```bash
# Run validation script
./scripts/validate-deployment.sh

# Test primary endpoint
curl http://$(terraform output -raw primary_endpoint)/health

# Test DR endpoint
curl http://$(terraform output -raw dr_endpoint)/health

# Test global endpoint (if configured)
curl http://$(terraform output -raw global_endpoint)/health
```

## Step 10: Set Up Monitoring (2 minutes)

```bash
# View monitoring dashboard
terraform output monitoring_dashboard_url

# Test alerting
terraform output alert_email
```

## Common Issues and Solutions

### Issue: "Error acquiring state lock"
**Solution**: Another Terraform process is running. Wait for it to complete or force unlock:
```bash
terraform force-unlock <LOCK_ID>
```

### Issue: "Error: Insufficient IAM permissions"
**Solution**: Ensure your cloud credentials have sufficient permissions:
```bash
# AWS
aws sts get-caller-identity
aws iam get-user

# Azure
az account show
az ad signed-in-user show

# GCP
gcloud auth list
gcloud config get-value project
```

### Issue: "Error: Resource already exists"
**Solution**: Import existing resources or use a different unique suffix:
```bash
# AWS example
terraform import module.aws_primary[0].aws_vpc.main vpc-12345678
```

### Issue: "Error: Database password too weak"
**Solution**: Use a strong password with at least:
- 16 characters
- Uppercase and lowercase letters
- Numbers
- Special characters

### Issue: Plan shows unexpected changes
**Solution**: Check if resources were modified outside Terraform:
```bash
terraform refresh
terraform plan
```

## Cost Management

### View Estimated Costs
```bash
terraform output estimated_monthly_cost
```

### Reduce Costs for Development
```hcl
# In terraform.tfvars
environment               = "dev"
primary_instance_count    = 1
dr_instance_count         = 1
enable_db_replication     = false
enable_storage_replication = false
enable_auto_scaling       = false
db_backup_retention_days  = 7
log_retention_days        = 7
```

### Set Up Cost Alerts

**AWS**:
```bash
aws budgets create-budget \
  --account-id 123456789012 \
  --budget file://budget.json
```

**Azure**:
```bash
az consumption budget create \
  --budget-name honua-monthly-budget \
  --amount 2000 \
  --time-grain Monthly
```

## Next Steps

- [ ] Review [FAILOVER.md](FAILOVER.md) for disaster recovery procedures
- [ ] Schedule monthly DR drills
- [ ] Set up automated backups
- [ ] Configure monitoring alerts
- [ ] Review security settings
- [ ] Enable audit logging
- [ ] Document custom configuration
- [ ] Train team on failover procedures

## Security Checklist

- [ ] Rotate database passwords regularly (90 days)
- [ ] Enable MFA for cloud console access
- [ ] Restrict network access to known IPs
- [ ] Enable encryption at rest for all data
- [ ] Enable encryption in transit (TLS 1.3)
- [ ] Review IAM roles and policies monthly
- [ ] Enable CloudTrail / Activity Log / Audit Logs
- [ ] Set up security scanning
- [ ] Configure WAF rules
- [ ] Enable DDoS protection

## Maintenance Schedule

| Task | Frequency | Owner |
|------|-----------|-------|
| Review security groups | Weekly | Security Team |
| Check replication lag | Daily (automated) | Platform Team |
| DR drill | Quarterly | Engineering Team |
| Cost review | Monthly | Finance Team |
| Update Terraform modules | Quarterly | Platform Team |
| Rotate secrets | Every 90 days | Security Team |
| Review access logs | Weekly | Security Team |
| Backup testing | Monthly | Engineering Team |

## Support and Documentation

- **Main Documentation**: [README.md](README.md)
- **Failover Procedures**: [FAILOVER.md](FAILOVER.md)
- **Module Structure**: [MODULE_STRUCTURE.md](MODULE_STRUCTURE.md)
- **Sample Output**: [SAMPLE_PLAN_OUTPUT.md](SAMPLE_PLAN_OUTPUT.md)
- **GitHub Issues**: https://github.com/honuaio/honua/issues
- **Slack Channel**: #honua-infrastructure

## Clean Up

To destroy all resources (careful!):

```bash
# Review what will be destroyed
terraform plan -destroy

# Destroy all resources
terraform destroy

# Confirm when prompted
# Type: yes
```

**Note**: This is irreversible. Back up all data before destroying resources.

## Terraform Commands Reference

```bash
# Initialize
terraform init

# Validate
terraform validate

# Format code
terraform fmt -recursive

# Show current state
terraform show

# List resources
terraform state list

# Show specific resource
terraform state show module.aws_primary[0].aws_vpc.main

# Refresh state
terraform refresh

# Plan changes
terraform plan

# Apply changes
terraform apply

# Destroy all resources
terraform destroy

# Import existing resource
terraform import <resource_address> <resource_id>

# Unlock state
terraform force-unlock <LOCK_ID>

# Output values
terraform output
terraform output -json
terraform output primary_endpoint
```

## Additional Resources

- [Terraform AWS Provider](https://registry.terraform.io/providers/hashicorp/aws/latest/docs)
- [Terraform Azure Provider](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)
- [Terraform GCP Provider](https://registry.terraform.io/providers/hashicorp/google/latest/docs)
- [HonuaIO Documentation](../../docs/README.md)
- [AWS Multi-Region Architecture](https://aws.amazon.com/solutions/implementations/multi-region-application-architecture/)
- [Azure Multi-Region Architecture](https://learn.microsoft.com/en-us/azure/architecture/reference-architectures/app-service-web-app/multi-region)

---

**Last Updated**: 2025-10-18
**Version**: 1.0.0
**Maintainer**: Platform Engineering Team
