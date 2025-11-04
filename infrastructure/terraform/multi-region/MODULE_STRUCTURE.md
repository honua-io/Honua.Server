# Multi-Region Terraform Module Structure

This document describes the complete module structure for the multi-region deployment.

## Directory Structure

```
multi-region/
├── README.md                           # Main documentation
├── FAILOVER.md                         # Failover procedures
├── MODULE_STRUCTURE.md                 # This file
├── SAMPLE_PLAN_OUTPUT.md              # Sample terraform plan output
├── main.tf                            # Root module configuration
├── variables.tf                       # Input variables
├── outputs.tf                         # Outputs
├── terraform.tfvars.example           # Example configuration
├── .terraform.lock.hcl                # Provider lock file (generated)
├── terraform.tfstate                  # State file (generated, do not commit)
├── terraform.tfstate.backup           # State backup (generated)
├── tfplan                             # Plan file (generated)
│
├── modules/                           # Reusable modules
│   ├── aws/                           # AWS-specific modules
│   │   ├── regional-stack/            # Complete regional deployment
│   │   │   ├── main.tf               # Regional resources
│   │   │   ├── variables.tf          # Regional variables
│   │   │   ├── outputs.tf            # Regional outputs
│   │   │   ├── networking.tf         # VPC, subnets, routing
│   │   │   ├── compute.tf            # ECS cluster and services
│   │   │   ├── database.tf           # RDS PostgreSQL
│   │   │   ├── storage.tf            # S3 buckets
│   │   │   ├── cache.tf              # ElastiCache Redis
│   │   │   ├── security.tf           # Security groups, IAM roles
│   │   │   ├── monitoring.tf         # CloudWatch, alarms
│   │   │   └── README.md             # Module documentation
│   │   │
│   │   ├── global-lb/                 # Route53 and CloudFront
│   │   │   ├── main.tf               # Global load balancing
│   │   │   ├── variables.tf          # LB variables
│   │   │   ├── outputs.tf            # LB outputs
│   │   │   ├── route53.tf            # DNS configuration
│   │   │   ├── health-checks.tf      # Health check configuration
│   │   │   ├── cloudfront.tf         # CDN configuration
│   │   │   ├── waf.tf                # WAF rules
│   │   │   └── README.md             # Module documentation
│   │   │
│   │   └── cross-region-replication/  # S3 replication
│   │       ├── main.tf               # Replication configuration
│   │       ├── variables.tf          # Replication variables
│   │       ├── outputs.tf            # Replication outputs
│   │       └── README.md             # Module documentation
│   │
│   ├── azure/                         # Azure-specific modules
│   │   ├── regional-stack/            # Complete regional deployment
│   │   │   ├── main.tf               # Regional resources
│   │   │   ├── variables.tf          # Regional variables
│   │   │   ├── outputs.tf            # Regional outputs
│   │   │   ├── networking.tf         # VNet, subnets, NSGs
│   │   │   ├── compute.tf            # Container Apps
│   │   │   ├── database.tf           # PostgreSQL Flexible Server
│   │   │   ├── storage.tf            # Blob Storage
│   │   │   ├── cache.tf              # Azure Cache for Redis
│   │   │   ├── security.tf           # Managed identities, RBAC
│   │   │   ├── monitoring.tf         # Application Insights
│   │   │   └── README.md             # Module documentation
│   │   │
│   │   ├── global-lb/                 # Azure Front Door
│   │   │   ├── main.tf               # Global load balancing
│   │   │   ├── variables.tf          # Front Door variables
│   │   │   ├── outputs.tf            # Front Door outputs
│   │   │   ├── front-door.tf         # Front Door configuration
│   │   │   ├── waf.tf                # WAF policies
│   │   │   └── README.md             # Module documentation
│   │   │
│   │   └── cross-region-replication/  # Blob replication
│   │       ├── main.tf               # GRS/RA-GRS configuration
│   │       ├── variables.tf          # Replication variables
│   │       ├── outputs.tf            # Replication outputs
│   │       └── README.md             # Module documentation
│   │
│   ├── gcp/                           # GCP-specific modules
│   │   ├── regional-stack/            # Complete regional deployment
│   │   │   ├── main.tf               # Regional resources
│   │   │   ├── variables.tf          # Regional variables
│   │   │   ├── outputs.tf            # Regional outputs
│   │   │   ├── networking.tf         # VPC, subnets, firewall rules
│   │   │   ├── compute.tf            # Cloud Run services
│   │   │   ├── database.tf           # Cloud SQL PostgreSQL
│   │   │   ├── storage.tf            # Cloud Storage buckets
│   │   │   ├── cache.tf              # Memorystore Redis
│   │   │   ├── security.tf           # IAM, service accounts
│   │   │   ├── monitoring.tf         # Cloud Monitoring
│   │   │   └── README.md             # Module documentation
│   │   │
│   │   ├── global-lb/                 # Cloud Load Balancing
│   │   │   ├── main.tf               # Global load balancing
│   │   │   ├── variables.tf          # LB variables
│   │   │   ├── outputs.tf            # LB outputs
│   │   │   ├── load-balancer.tf      # HTTP(S) load balancer
│   │   │   ├── health-checks.tf      # Health check configuration
│   │   │   ├── cdn.tf                # Cloud CDN configuration
│   │   │   ├── armor.tf              # Cloud Armor (WAF)
│   │   │   └── README.md             # Module documentation
│   │   │
│   │   └── cross-region-replication/  # GCS replication
│   │       ├── main.tf               # Multi-region bucket config
│   │       ├── variables.tf          # Replication variables
│   │       ├── outputs.tf            # Replication outputs
│   │       └── README.md             # Module documentation
│   │
│   └── monitoring/                    # Cross-cloud monitoring
│       ├── main.tf                   # Monitoring configuration
│       ├── variables.tf              # Monitoring variables
│       ├── outputs.tf                # Monitoring outputs
│       ├── dashboards.tf             # Dashboard definitions
│       ├── alerts.tf                 # Alert rules
│       └── README.md                 # Module documentation
│
├── scripts/                           # Helper scripts
│   ├── validate-deployment.sh        # Validate Terraform config
│   ├── failover-dns-aws.sh           # AWS DNS failover
│   ├── failover-dns-azure.sh         # Azure DNS failover
│   ├── promote-rds-replica.sh        # Promote RDS read replica
│   ├── promote-azure-replica.sh      # Promote Azure replica
│   ├── test-dr-endpoint.sh           # Test DR endpoint
│   ├── check-replica-lag.sh          # Check replication lag
│   ├── simulate-failover.sh          # Simulate failover (dry-run)
│   ├── rollback-to-primary.sh        # Rollback to primary
│   ├── dr-drill.sh                   # DR drill automation
│   ├── smoke-test.sh                 # Application smoke tests
│   └── README.md                     # Scripts documentation
│
└── examples/                          # Example configurations
    ├── aws-minimal/                  # Minimal AWS deployment
    │   ├── main.tf                   # Minimal config
    │   ├── terraform.tfvars          # Minimal variables
    │   └── README.md                 # Minimal deployment docs
    │
    ├── aws-production/               # Production AWS deployment
    │   ├── main.tf                   # Production config
    │   ├── terraform.tfvars          # Production variables
    │   └── README.md                 # Production deployment docs
    │
    ├── azure-minimal/                # Minimal Azure deployment
    │   ├── main.tf                   # Minimal config
    │   ├── terraform.tfvars          # Minimal variables
    │   └── README.md                 # Minimal deployment docs
    │
    ├── azure-production/             # Production Azure deployment
    │   ├── main.tf                   # Production config
    │   ├── terraform.tfvars          # Production variables
    │   └── README.md                 # Production deployment docs
    │
    ├── gcp-minimal/                  # Minimal GCP deployment
    │   ├── main.tf                   # Minimal config
    │   ├── terraform.tfvars          # Minimal variables
    │   └── README.md                 # Minimal deployment docs
    │
    └── gcp-production/               # Production GCP deployment
        ├── main.tf                   # Production config
        ├── terraform.tfvars          # Production variables
        └── README.md                 # Production deployment docs
```

## Module Responsibilities

### Regional Stack Module

The regional stack module (`modules/{cloud}/regional-stack/`) is responsible for:

1. **Networking**: VPC/VNet, subnets, routing, NAT gateways, security groups
2. **Compute**: ECS/Container Apps/Cloud Run, auto-scaling, load balancers
3. **Database**: RDS/PostgreSQL Flexible/Cloud SQL, read replicas, backups
4. **Storage**: S3/Blob/GCS buckets, encryption, lifecycle policies
5. **Cache**: ElastiCache/Azure Cache/Memorystore Redis, replication
6. **Security**: IAM roles, managed identities, encryption keys
7. **Monitoring**: CloudWatch/App Insights/Cloud Monitoring, log aggregation

### Global Load Balancer Module

The global LB module (`modules/{cloud}/global-lb/`) is responsible for:

1. **DNS**: Route53/Traffic Manager/Cloud DNS, health-based routing
2. **Health Checks**: Endpoint monitoring, automatic failover triggers
3. **CDN**: CloudFront/Front Door/Cloud CDN, edge caching
4. **WAF**: Web application firewall rules, DDoS protection
5. **SSL/TLS**: Certificate management, automatic renewal

### Cross-Region Replication Module

The replication module (`modules/{cloud}/cross-region-replication/`) is responsible for:

1. **Storage Replication**: S3 CRR/Blob GRS/GCS multi-region buckets
2. **Database Replication**: Read replica configuration, lag monitoring
3. **Cache Replication**: Redis replication group setup
4. **Replication Monitoring**: Lag alerts, replication health checks

### Monitoring Module

The monitoring module (`modules/monitoring/`) is responsible for:

1. **Unified Dashboards**: Cross-region monitoring dashboards
2. **Alerting**: Email, Slack, PagerDuty integration
3. **Metrics**: Custom metrics for replication lag, failover status
4. **Logs**: Centralized log aggregation and analysis

## Resource Naming Convention

All resources follow this naming convention:

```
{project}-{environment}-{region}-{resource-type}-{unique-suffix}

Examples:
  honua-prod-us-east-1-vpc-a1b2c3
  honua-prod-us-west-2-postgres-a1b2c3
  honua-prod-global-route53-a1b2c3
```

## Variable Inheritance

Variables flow from root to modules:

```
Root Module (main.tf)
  ├─> AWS Primary (modules/aws/regional-stack)
  │     ├─> Inherits: region, environment, instance_count, etc.
  │     └─> Sets: is_primary = true
  │
  ├─> AWS DR (modules/aws/regional-stack)
  │     ├─> Inherits: region, environment, instance_count, etc.
  │     └─> Sets: is_primary = false
  │
  └─> AWS Global LB (modules/aws/global-lb)
        └─> Inherits: DNS config, health check config, etc.
```

## State Management

### Local State (Development)

```bash
# Default - state stored locally
terraform init
terraform plan
terraform apply
```

### Remote State (Production)

```hcl
# main.tf backend configuration
backend "s3" {
  bucket         = "honua-terraform-state"
  key            = "multi-region/terraform.tfstate"
  region         = "us-east-1"
  encrypt        = true
  dynamodb_table = "honua-terraform-locks"
  kms_key_id     = "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012"
}
```

```bash
# Initialize with remote backend
terraform init -backend-config="bucket=honua-terraform-state"
```

### State Locking

State locking prevents concurrent modifications:

- **AWS**: DynamoDB table for state locking
- **Azure**: Azure Blob Storage lease mechanism
- **GCP**: Cloud Storage object versioning

## Dependency Management

Modules have explicit dependencies:

```hcl
# DR region depends on primary region resources
module "aws_dr" {
  # ...
  source_db_identifier   = module.aws_primary[0].db_identifier
  source_bucket          = module.aws_primary[0].storage_bucket_id
  primary_redis_endpoint = module.aws_primary[0].redis_endpoint

  depends_on = [
    module.aws_primary
  ]
}

# Global LB depends on both regions
module "aws_global_lb" {
  # ...
  primary_endpoint = module.aws_primary[0].load_balancer_dns
  dr_endpoint      = module.aws_dr[0].load_balancer_dns

  depends_on = [
    module.aws_primary,
    module.aws_dr
  ]
}
```

## Provider Configuration

Multiple provider configurations for multi-region:

```hcl
# Primary region provider
provider "aws" {
  region = var.primary_region
  alias  = "primary"
}

# DR region provider
provider "aws" {
  region = var.dr_region
  alias  = "dr"
}

# Global services provider (us-east-1 for Route53)
provider "aws" {
  region = "us-east-1"
  alias  = "global"
}
```

## Testing Strategy

### Unit Tests

Test individual modules:

```bash
cd modules/aws/regional-stack
terraform init
terraform validate
terraform plan
```

### Integration Tests

Test complete deployment:

```bash
cd examples/aws-minimal
terraform init
terraform validate
terraform plan
```

### DR Drill Tests

Test failover procedures:

```bash
./scripts/dr-drill.sh --cloud aws --dry-run
```

## Security Considerations

1. **Secrets Management**: Never commit secrets to version control
   - Use environment variables
   - Use AWS Secrets Manager / Azure Key Vault / GCP Secret Manager
   - Use terraform.tfvars (add to .gitignore)

2. **State File Security**: State files contain sensitive data
   - Enable encryption at rest for remote state
   - Restrict access to state buckets/containers
   - Enable versioning for state files

3. **Network Security**: Implement defense in depth
   - Use private subnets for compute and databases
   - Implement security groups / NSGs / firewall rules
   - Enable VPC endpoints / Private Link

4. **IAM / RBAC**: Follow least privilege principle
   - Use separate IAM roles per service
   - Enable MFA for privileged operations
   - Audit IAM policy changes

## Cost Optimization

1. **Right-Sizing**: Start with smaller resources in DR region
   - Scale up DR resources during failover
   - Use reserved instances / savings plans for primary region

2. **Storage Tiering**: Use appropriate storage classes
   - Standard for frequently accessed data
   - Infrequent Access for backups
   - Glacier for long-term archival

3. **Auto-Scaling**: Scale compute resources based on demand
   - Define appropriate thresholds
   - Use spot instances for non-critical workloads

4. **Monitoring**: Track costs with tags and budgets
   - Set up billing alerts
   - Review cost allocation reports monthly

## Maintenance and Updates

### Terraform Version Updates

```bash
# Check current version
terraform version

# Update provider versions in main.tf
required_version = ">= 1.6.0"

# Re-initialize
terraform init -upgrade
```

### Module Updates

```bash
# Update module source versions
module "aws_primary" {
  source = "./modules/aws/regional-stack"
  # source = "git::https://github.com/honuaio/terraform-modules.git//aws/regional-stack?ref=v2.0.0"
}

# Re-initialize
terraform init -upgrade
```

### Resource Updates

```bash
# Show changes
terraform plan

# Apply changes
terraform apply

# Refresh state
terraform refresh
```

## Troubleshooting

### Common Issues

1. **Provider Authentication Failures**
   ```bash
   # AWS
   aws configure
   aws sts get-caller-identity

   # Azure
   az login
   az account show

   # GCP
   gcloud auth application-default login
   gcloud config get-value project
   ```

2. **State Lock Errors**
   ```bash
   # Force unlock (use with caution!)
   terraform force-unlock <LOCK_ID>
   ```

3. **Plan/Apply Failures**
   ```bash
   # Enable debug logging
   export TF_LOG=DEBUG
   terraform plan
   ```

4. **Module Not Found**
   ```bash
   # Re-initialize modules
   terraform init -upgrade
   ```

## Next Steps

1. Complete the regional stack modules with full resource definitions
2. Implement global load balancer modules
3. Add cross-region replication configuration
4. Create monitoring dashboards and alerts
5. Write comprehensive tests
6. Document deployment procedures
7. Conduct DR drills

## References

- [Terraform AWS Provider Docs](https://registry.terraform.io/providers/hashicorp/aws/latest/docs)
- [Terraform Azure Provider Docs](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)
- [Terraform Google Provider Docs](https://registry.terraform.io/providers/hashicorp/google/latest/docs)
- [HonuaIO Documentation](../../docs/README.md)
- [FAILOVER.md](./FAILOVER.md)
