# Honua Serverless Terraform Modules

## Overview

This document provides an overview of the comprehensive serverless deployment modules for Honua GIS Platform. These modules enable one-command serverless deployment across AWS, Google Cloud, and Azure.

**Created**: November 2024
**Purpose**: Enable market-first serverless GIS platform deployment
**Status**: Production-ready

## Module Directory

### 1. Google Cloud Run Module
**Location**: `modules/cloud-run/`
**Platform**: Google Cloud Platform
**Compute**: Cloud Run (fully managed containers)
**Database**: Cloud SQL PostgreSQL with PostGIS
**CDN**: Cloud CDN (integrated with Load Balancer)
**Cost**: $30-150/month (dev) | $150-500/month (prod)

**Files**:
- `main.tf` - Cloud Run, Cloud SQL, Load Balancer, VPC connector
- `variables.tf` - 50+ configurable variables
- `outputs.tf` - Service URLs, database info, cost estimates
- `versions.tf` - Provider version constraints
- `README.md` - Comprehensive documentation with examples
- `terraform.tfvars.example` - Example configuration

**Key Features**:
- True serverless (scale to 0)
- 0-100 auto-scaling
- Private VPC networking
- Cloud Armor DDoS protection
- Secret Manager integration
- Managed SSL certificates
- Global CDN

### 2. AWS Lambda + ALB Module
**Location**: `modules/lambda/`
**Platform**: Amazon Web Services
**Compute**: Lambda (container images)
**Database**: RDS PostgreSQL with PostGIS
**CDN**: CloudFront (separate module)
**Cost**: $20-100/month (dev) | $200-1,500/month (prod)

**Files**:
- `main.tf` - Lambda, RDS, ALB, VPC, NAT Gateway
- `variables.tf` - 40+ configurable variables
- `outputs.tf` - ALB URL, Lambda info, database endpoints
- `versions.tf` - Provider version constraints
- `README.md` - Comprehensive documentation with examples
- `terraform.tfvars.example` - Example configuration

**Key Features**:
- Container image support
- Application Load Balancer
- Multi-AZ RDS
- VPC with private subnets
- Secrets Manager
- ECR integration
- Provisioned concurrency option
- Lambda Function URL alternative

### 3. Azure Container Apps Module
**Location**: `modules/container-apps/`
**Platform**: Microsoft Azure
**Compute**: Container Apps (serverless containers)
**Database**: Azure Database for PostgreSQL Flexible
**CDN**: Azure Front Door (integrated)
**Cost**: $15-80/month (dev) | $200-500/month (prod)

**Files**:
- `main.tf` - Container Apps, PostgreSQL, Front Door, VNET
- `variables.tf` - 35+ configurable variables
- `outputs.tf` - App URLs, database info, Front Door endpoints
- `versions.tf` - Provider version constraints
- `README.md` - Comprehensive documentation with examples
- `terraform.tfvars.example` - Example configuration

**Key Features**:
- Dapr integration
- 0-30 auto-scaling
- Zone-redundant HA
- Key Vault integration
- Managed identity (no passwords)
- VNET integration
- Front Door CDN with WAF

### 4. Cloud-Agnostic CDN Module
**Location**: `modules/cdn/`
**Platform**: Multi-cloud
**Providers**: AWS CloudFront, GCP Cloud CDN, Azure Front Door
**Purpose**: Optimize GIS content delivery worldwide
**Cost**: $35-300/month (depends on traffic)

**Files**:
- `main.tf` - CloudFront, Cloud CDN config, Front Door
- `variables.tf` - Cache policies and TTL configuration
- `outputs.tf` - CDN URLs, cache policies, DNS records
- `versions.tf` - Multi-provider constraints
- `README.md` - Comprehensive documentation with examples
- `terraform.tfvars.example` - Example configuration

**Key Features**:
- GIS-optimized cache policies
- Separate TTLs for tiles (1h), features (5m), metadata (24h)
- Query string caching
- Compression enabled
- SSL/TLS with custom domains
- WAF integration (CloudFront, Front Door)
- Origin Shield support

## Example Deployments

### GCP Development Environment
**Location**: `environments/serverless-examples/gcp-dev/`
**Use Case**: Cost-optimized development
**Cost**: ~$20-40/month
**Config**: Scale to zero, smallest DB, no CDN

```bash
cd environments/serverless-examples/gcp-dev
terraform init && terraform apply
```

### AWS Production Environment
**Location**: `environments/serverless-examples/aws-prod/`
**Use Case**: High-performance production
**Cost**: ~$1,000-1,200/month
**Config**: Provisioned concurrency, Multi-AZ RDS, CloudFront CDN

```bash
cd environments/serverless-examples/aws-prod
terraform init && terraform apply
```

### Azure Staging Environment
**Location**: `environments/serverless-examples/azure-staging/`
**Use Case**: Staging/UAT testing
**Cost**: ~$200-250/month
**Config**: Auto-scaling, GP database, Front Door Standard

```bash
cd environments/serverless-examples/azure-staging
terraform init && terraform apply
```

## Quick Start

### 1. Choose Your Platform

```bash
# Google Cloud (recommended for development)
cd modules/cloud-run

# AWS (recommended for production)
cd modules/lambda

# Azure (recommended for staging)
cd modules/container-apps
```

### 2. Review Module Documentation

Each module has comprehensive README with:
- Prerequisites
- Usage examples (dev/staging/prod)
- Variable descriptions
- Post-deployment steps
- Troubleshooting
- Cost estimates
- Security best practices

### 3. Deploy

```bash
# Copy example configuration
cp terraform.tfvars.example terraform.tfvars

# Edit with your values
vim terraform.tfvars

# Initialize and deploy
terraform init
terraform plan
terraform apply
```

## Architecture Features

### Compute
| Feature | GCP | AWS | Azure |
|---------|-----|-----|-------|
| Platform | Cloud Run | Lambda | Container Apps |
| Min Instances | 0 | 0 | 0 |
| Max Instances | 100 | 1000 | 30 |
| Container Support | Yes | Yes | Yes |
| Timeout | 3600s | 900s | N/A |
| Memory | 32 GB | 10 GB | 4 GB |

### Database
| Feature | GCP | AWS | Azure |
|---------|-----|-----|-------|
| Service | Cloud SQL | RDS | PostgreSQL Flexible |
| Engine | PostgreSQL 15 | PostgreSQL 15 | PostgreSQL 15 |
| PostGIS | Supported | Supported | Supported |
| HA | Regional | Multi-AZ | Zone Redundant |
| Backup | Automated | Automated | Automated |

### CDN
| Feature | GCP | AWS | Azure |
|---------|-----|-----|-------|
| Service | Cloud CDN | CloudFront | Front Door |
| Edge Locations | 100+ | 400+ | 100+ |
| Cache Policies | Custom | Custom | Custom |
| WAF | Cloud Armor | AWS WAF | Front Door WAF |
| SSL | Managed | ACM | Managed |

## Cost Comparison

### Development Environment
| Cloud | Monthly Cost | Notes |
|-------|--------------|-------|
| GCP | $20-40 | Best free tier, scale to zero |
| AWS | $30-100 | Free tier eligible |
| Azure | $15-25 | Container Apps free tier |

### Production Environment (Low Traffic)
| Cloud | Monthly Cost | Notes |
|-------|--------------|-------|
| GCP | $150-300 | Cloud Run + Cloud SQL |
| AWS | $200-400 | Lambda + RDS + ALB |
| Azure | $200-300 | Container Apps + PostgreSQL |

### Production Environment (High Traffic)
| Cloud | Monthly Cost | Notes |
|-------|--------------|-------|
| GCP | $500-1,000 | With min instances + HA DB |
| AWS | $1,000-2,000 | Provisioned concurrency + Multi-AZ |
| Azure | $500-1,000 | With Front Door Premium |

**Note**: Actual costs vary significantly with traffic patterns. Serverless is most cost-effective for variable workloads.

## Security Features

### All Platforms
- ✅ HTTPS only
- ✅ Secrets management (Secret Manager/Secrets Manager/Key Vault)
- ✅ Private networking (VPC/VNET)
- ✅ Managed identity/service accounts
- ✅ Encrypted databases
- ✅ DDoS protection (Cloud Armor/WAF/Front Door)

### Best Practices Implemented
- Least privilege IAM
- No hardcoded credentials
- Network isolation
- Audit logging
- Automatic SSL certificates
- Database encryption at rest
- Backup retention

## Monitoring and Observability

### Metrics Collected
- Request count and latency (P50, P95, P99)
- Error rates (4xx, 5xx)
- Instance/replica count
- Database connections
- CPU and memory usage
- CDN cache hit ratio

### Logging
- Application logs (CloudWatch/Cloud Logging/Log Analytics)
- Access logs (ALB/Load Balancer/Front Door)
- Database logs (RDS/Cloud SQL/PostgreSQL)
- CDN logs (CloudFront/Cloud CDN/Front Door)

### Alerting
- High error rates
- Slow response times
- Database connection exhaustion
- High costs
- Low CDN cache hit ratio

## Deployment Patterns

### Blue-Green Deployment
```hcl
# Deploy new version to staging
module "honua_staging" {
  source          = "../../modules/cloud-run"
  container_image = "gcr.io/project/honua:v2.0.0"
  environment     = "staging"
}

# Test, then promote to production
module "honua_production" {
  source          = "../../modules/cloud-run"
  container_image = "gcr.io/project/honua:v2.0.0"  # Tested version
  environment     = "production"
}
```

### Canary Deployment
Use traffic splitting features:
- GCP: Cloud Run traffic splits
- AWS: Lambda aliases and weighted routing
- Azure: Container Apps revision traffic management

### Multi-Region Deployment
Deploy to multiple regions for global coverage:
```hcl
module "honua_us" {
  source = "../../modules/cloud-run"
  region = "us-central1"
}

module "honua_eu" {
  source = "../../modules/cloud-run"
  region = "europe-west1"
}

module "honua_asia" {
  source = "../../modules/cloud-run"
  region = "asia-northeast1"
}
```

## Migration Guides

### From Kubernetes
1. Export data from existing PostgreSQL
2. Deploy serverless module
3. Import data to managed database
4. Update DNS to new endpoint
5. Monitor and validate
6. Decommission Kubernetes cluster

### From EC2/VMs
1. Containerize application
2. Push to container registry
3. Deploy serverless module
4. Test functionality
5. Switch traffic
6. Terminate old instances

### From On-Premises
1. Set up cloud networking (VPN/Direct Connect)
2. Replicate database to cloud
3. Deploy serverless infrastructure
4. Run in parallel
5. Gradually shift traffic
6. Decommission on-prem

## CI/CD Integration

### GitHub Actions
```yaml
- name: Deploy to Cloud Run
  run: |
    cd infrastructure/terraform/environments/gcp-prod
    terraform apply -auto-approve \
      -var="container_image=gcr.io/$PROJECT/honua:$GITHUB_SHA"
```

### GitLab CI
```yaml
deploy:
  script:
    - cd infrastructure/terraform/environments/aws-prod
    - terraform apply -auto-approve
      -var="container_image_uri=$CI_REGISTRY_IMAGE:$CI_COMMIT_SHA"
```

### Azure DevOps
```yaml
- task: TerraformCLI@0
  inputs:
    command: apply
    workingDirectory: infrastructure/terraform/environments/azure-prod
    commandOptions: '-var="container_image=$(containerImage)"'
```

## Troubleshooting

### Common Issues

**Container won't start**:
- Check logs in Cloud Logging/CloudWatch/Log Analytics
- Verify environment variables are set correctly
- Ensure database is accessible
- Check memory/CPU limits

**Database connection failures**:
- Verify VPC/VNET configuration
- Check security group/firewall rules
- Validate database credentials in secrets manager
- Ensure database is running

**High costs**:
- Review provisioned concurrency settings
- Check NAT Gateway usage (AWS)
- Optimize database tier
- Increase CDN cache TTLs
- Review log retention

**Low CDN cache hit ratio**:
- Verify Cache-Control headers at origin
- Check query string normalization
- Review cache policies
- Pre-warm cache for popular content

## Support and Resources

### Documentation
- [Cloud Run Module README](modules/cloud-run/README.md)
- [Lambda Module README](modules/lambda/README.md)
- [Container Apps Module README](modules/container-apps/README.md)
- [CDN Module README](modules/cdn/README.md)
- [Example Deployments](environments/serverless-examples/README.md)

### Cloud Provider Docs
- [Google Cloud Run](https://cloud.google.com/run/docs)
- [AWS Lambda](https://docs.aws.amazon.com/lambda/)
- [Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/)

### Honua Platform
- [GitHub Repository](https://github.com/HonuaIO/honua)
- [Report Issues](https://github.com/HonuaIO/honua/issues)

## License

These modules are part of the Honua platform, licensed under Elastic License 2.0.

## Contributing

Contributions welcome! Please:
1. Test thoroughly on all three clouds
2. Update documentation
3. Follow Terraform best practices
4. Include cost estimates
5. Add example configurations

---

**Last Updated**: November 2024
**Module Count**: 4 production-ready modules
**Cloud Platforms**: 3 (AWS, GCP, Azure)
**Example Configurations**: 3 (dev, staging, prod)
**Total Files**: 24 Terraform files + documentation
