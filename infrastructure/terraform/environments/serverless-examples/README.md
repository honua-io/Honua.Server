# Honua Serverless Deployment Examples

This directory contains example Terraform configurations demonstrating how to deploy Honua GIS Platform in serverless mode across AWS, Google Cloud, and Azure.

## Available Examples

### 1. GCP Development (`gcp-dev/`)
**Use Case**: Cost-optimized development environment
**Platform**: Google Cloud Run
**Cost**: ~$20-40/month
**Features**:
- True serverless (scale to zero)
- Small database (db-f1-micro)
- No custom domain
- No CDN
- Ideal for development and testing

**Deploy**:
```bash
cd gcp-dev
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars
terraform init
terraform apply
```

### 2. AWS Production (`aws-prod/`)
**Use Case**: High-performance production deployment
**Platform**: AWS Lambda + ALB + CloudFront
**Cost**: ~$1,000-1,200/month
**Features**:
- Provisioned concurrency for low latency
- Multi-AZ RDS with high availability
- CloudFront CDN with Origin Shield
- WAF protection
- Comprehensive monitoring
- Suitable for production workloads

**Deploy**:
```bash
cd aws-prod
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars
terraform init
terraform apply
```

### 3. Azure Staging (`azure-staging/`)
**Use Case**: Staging/pre-production environment
**Platform**: Azure Container Apps + Front Door
**Cost**: ~$200-250/month
**Features**:
- Auto-scaling (0-20 replicas)
- General Purpose database
- Azure Front Door Standard
- Virtual Network integration
- Managed identity
- Perfect for staging and UAT

**Deploy**:
```bash
cd azure-staging
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars
terraform init
terraform apply
```

## Quick Start Guide

### Prerequisites

1. **Cloud Account**: Active account with appropriate provider
2. **Terraform**: Version >= 1.5.0
3. **Cloud CLI**: gcloud, aws, or az configured
4. **Container Image**: Honua image in container registry
5. **SSL Certificate**: (Optional) For custom domains

### Step 1: Choose Your Platform

Select the example that matches your needs:
- **Development/Testing**: Use `gcp-dev`
- **Production**: Use `aws-prod` with provisioned capacity
- **Staging/UAT**: Use `azure-staging`

### Step 2: Configure Variables

```bash
cd <example-directory>
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your values
```

### Step 3: Initialize Terraform

```bash
terraform init
```

### Step 4: Review Plan

```bash
terraform plan
```

### Step 5: Deploy

```bash
terraform apply
```

### Step 6: Post-Deployment

1. **Install PostGIS**:
   ```bash
   # GCP
   gcloud sql connect <instance> --user=honua
   CREATE EXTENSION postgis;

   # AWS
   psql -h <endpoint> -U honua -d honua
   CREATE EXTENSION postgis;

   # Azure
   az postgres flexible-server execute --name <server> --database-name honua --query "CREATE EXTENSION postgis;"
   ```

2. **Run Migrations**:
   ```bash
   # Run your schema migrations
   ```

3. **Test Deployment**:
   ```bash
   curl https://<your-url>/health
   ```

## Architecture Comparison

| Feature | GCP Dev | AWS Prod | Azure Staging |
|---------|---------|----------|---------------|
| **Compute** | Cloud Run | Lambda | Container Apps |
| **Database** | Cloud SQL (Micro) | RDS Multi-AZ | PostgreSQL Flexible |
| **CDN** | None | CloudFront | Front Door |
| **Cost/Month** | $20-40 | $1,000-1,200 | $200-250 |
| **Min Instances** | 0 | 10 (provisioned) | 0 |
| **Max Instances** | 10 | 100 | 20 |
| **HA Database** | No | Yes | No |
| **Custom Domain** | No | Yes | Yes |
| **Best For** | Dev/Test | Production | Staging/UAT |

## Cost Optimization Tips

### Development
- Set `min_instances = 0` (scale to zero)
- Use smallest database tier
- Disable CDN
- Shorter backup retention
- No high availability
- **Estimated**: $20-50/month

### Staging
- Scale to zero during off-hours
- Moderate database tier
- Enable CDN for testing
- Standard backup retention
- **Estimated**: $150-300/month

### Production
- Provisioned capacity for latency
- High availability database
- Global CDN with origin shield
- WAF and security features
- Extended backup retention
- **Estimated**: $500-2,000/month (depends on traffic)

## Monitoring and Observability

### GCP Cloud Run
```bash
# View logs
gcloud logging read "resource.type=cloud_run_revision" --limit 50

# Metrics
gcloud monitoring dashboards list
```

### AWS Lambda
```bash
# View logs
aws logs tail /aws/lambda/honua-production --follow

# Metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Duration \
  --dimensions Name=FunctionName,Value=honua-production
```

### Azure Container Apps
```bash
# View logs
az containerapp logs show --name honua-staging --resource-group honua-staging-rg

# Metrics
az monitor metrics list --resource <container-app-id>
```

## Troubleshooting

### Container Won't Start

**GCP**:
```bash
gcloud run revisions describe <revision> --region <region>
gcloud logging read "resource.type=cloud_run_revision" --limit 10
```

**AWS**:
```bash
aws lambda get-function-configuration --function-name honua-production
aws logs tail /aws/lambda/honua-production --limit 10
```

**Azure**:
```bash
az containerapp revision list --name honua-staging --resource-group honua-staging-rg
az containerapp logs show --name honua-staging --resource-group honua-staging-rg
```

### Database Connection Issues

**Check VPC/Network**:
- GCP: Verify VPC connector is running
- AWS: Check security groups and NAT gateway
- Azure: Verify VNET integration and delegated subnets

**Test Connectivity**:
```bash
# GCP
gcloud sql instances describe <instance>

# AWS
aws rds describe-db-instances --db-instance-identifier <instance>

# Azure
az postgres flexible-server show --name <server> --resource-group <rg>
```

### High Costs

**Check**:
1. NAT Gateway usage (AWS: ~$33/month per AZ)
2. Provisioned concurrency (AWS: $0.015/hr per GB)
3. Database tier (use smallest needed)
4. Data transfer costs
5. CloudWatch/Log Analytics ingestion

**Optimize**:
- Use VPC endpoints instead of NAT
- Reduce provisioned concurrency
- Downsize database
- Increase cache TTLs
- Archive old logs

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Deploy Serverless

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v2

      - name: Terraform Init
        run: |
          cd infrastructure/terraform/environments/aws-prod
          terraform init

      - name: Terraform Apply
        run: |
          cd infrastructure/terraform/environments/aws-prod
          terraform apply -auto-approve \
            -var="container_image_uri=${{ github.sha }}"
```

## Migration Paths

### From Kubernetes to Serverless

1. **Export Data**: Dump database
2. **Deploy Serverless**: Use appropriate example
3. **Import Data**: Restore to managed database
4. **Update DNS**: Point to new endpoint
5. **Monitor**: Watch metrics carefully
6. **Decommission**: Remove K8s cluster

### From EC2/VMs to Serverless

1. **Containerize**: Package as Docker image
2. **Push Image**: To cloud container registry
3. **Deploy Module**: Using examples
4. **Test**: Verify functionality
5. **Switch Traffic**: Update DNS/load balancer
6. **Cleanup**: Terminate old instances

## Support and Resources

- [Module Documentation](../../modules/)
  - [Cloud Run Module](../../modules/cloud-run/README.md)
  - [Lambda Module](../../modules/lambda/README.md)
  - [Container Apps Module](../../modules/container-apps/README.md)
  - [CDN Module](../../modules/cdn/README.md)
- [Honua Platform](https://github.com/HonuaIO/honua)
- [Report Issues](https://github.com/HonuaIO/honua/issues)

## License

These examples are part of the Honua platform, licensed under Elastic License 2.0.
