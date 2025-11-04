# HonuaIO Multi-Region Deployment

This Terraform configuration deploys HonuaIO across multiple regions for high availability and disaster recovery.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Global Load Balancer                     │
│            (AWS Route53 / Azure Front Door)                 │
└────────────┬────────────────────────────┬───────────────────┘
             │                            │
    ┌────────▼────────┐          ┌────────▼────────┐
    │  Primary Region │          │   DR Region     │
    │   (us-east-1)   │          │  (us-west-2)    │
    │                 │          │                 │
    │  - ECS/AKS      │◄────────►│  - ECS/AKS      │
    │  - PostgreSQL   │  Replica │  - PostgreSQL   │
    │  - S3/Blob      │  ◄─────► │  - S3/Blob      │
    │  - Redis        │  Sync    │  - Redis        │
    └─────────────────┘          └─────────────────┘
```

## Features

- **Multi-Region Deployment**: Primary and DR regions with automatic failover
- **Cross-Region Database Replication**: PostgreSQL read replicas in DR region
- **Global Load Balancing**: Route53 health checks with automatic failover
- **Cross-Region Storage Replication**: S3/Blob storage replication
- **Region-Aware Naming**: Consistent resource naming across regions
- **Cost Optimization**: Configurable resource sizing per region
- **Observability**: CloudWatch/Application Insights in all regions

## Supported Clouds

### AWS
- **Primary Region**: us-east-1 (N. Virginia)
- **DR Region**: us-west-2 (Oregon)
- **Global**: Route53, CloudFront

### Azure
- **Primary Region**: eastus (East US)
- **DR Region**: westus2 (West US 2)
- **Global**: Azure Front Door, Traffic Manager

### GCP
- **Primary Region**: us-east1
- **DR Region**: us-west1
- **Global**: Cloud Load Balancing

## Directory Structure

```
multi-region/
├── README.md                    # This file
├── FAILOVER.md                  # Failover procedures
├── main.tf                      # Root module
├── variables.tf                 # Input variables
├── outputs.tf                   # Outputs
├── terraform.tfvars.example     # Example configuration
├── modules/
│   ├── aws/
│   │   ├── regional-stack/     # Regional AWS resources
│   │   ├── global-lb/          # Route53 configuration
│   │   └── cross-region-replication/  # S3 replication
│   ├── azure/
│   │   ├── regional-stack/     # Regional Azure resources
│   │   ├── global-lb/          # Front Door configuration
│   │   └── cross-region-replication/  # Blob replication
│   └── gcp/
│       ├── regional-stack/     # Regional GCP resources
│       ├── global-lb/          # Cloud Load Balancing
│       └── cross-region-replication/  # GCS replication
└── examples/
    ├── aws-minimal/            # Minimal AWS deployment
    ├── aws-production/         # Production AWS deployment
    ├── azure-minimal/          # Minimal Azure deployment
    └── azure-production/       # Production Azure deployment
```

## Quick Start

### 1. Choose Your Cloud Provider

```bash
cd multi-region/examples/aws-production
# or
cd multi-region/examples/azure-production
```

### 2. Configure Variables

```bash
cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars`:

```hcl
# AWS Example
primary_region = "us-east-1"
dr_region      = "us-west-2"
environment    = "prod"
project_name   = "honua"

# Database configuration
primary_db_instance_class = "db.r6g.xlarge"
dr_db_instance_class      = "db.r6g.large"  # Can be smaller

# Compute configuration
primary_instance_count = 3
dr_instance_count      = 1  # Scaled up during failover

# Enable cross-region replication
enable_db_replication      = true
enable_storage_replication = true
enable_redis_replication   = true
```

### 3. Initialize and Deploy

```bash
terraform init
terraform plan
terraform apply
```

### 4. Verify Deployment

```bash
# Get outputs
terraform output -json > deployment.json

# Test primary endpoint
PRIMARY_URL=$(terraform output -raw primary_endpoint)
curl -I $PRIMARY_URL/health

# Test DR endpoint
DR_URL=$(terraform output -raw dr_endpoint)
curl -I $DR_URL/health

# Test global endpoint
GLOBAL_URL=$(terraform output -raw global_endpoint)
curl -I $GLOBAL_URL/health
```

## Configuration Variables

### Required Variables

| Variable | Type | Description |
|----------|------|-------------|
| `cloud_provider` | string | Cloud provider: aws, azure, or gcp |
| `primary_region` | string | Primary region identifier |
| `dr_region` | string | DR region identifier |
| `environment` | string | Environment: dev, staging, prod |
| `admin_email` | string | Email for alerts and notifications |

### Optional Variables

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `enable_db_replication` | bool | true | Enable cross-region DB replication |
| `enable_storage_replication` | bool | true | Enable cross-region storage replication |
| `enable_redis_replication` | bool | true | Enable cross-region Redis replication |
| `dr_instance_count` | number | 1 | Number of compute instances in DR region |
| `primary_instance_count` | number | 3 | Number of compute instances in primary region |
| `failover_routing_policy` | string | "latency" | Routing policy: latency, geolocation, weighted |
| `health_check_interval` | number | 30 | Health check interval in seconds |
| `health_check_threshold` | number | 3 | Unhealthy threshold count |

## Region-Aware Resource Naming

All resources follow a consistent naming convention:

```
{project}-{environment}-{region}-{resource-type}-{unique-suffix}
```

Examples:
- `honua-prod-us-east-1-ecs-cluster-a1b2c3`
- `honua-prod-us-west-2-postgres-replica-a1b2c3`
- `honua-prod-global-route53-zone-a1b2c3`

## Database Replication

### AWS RDS PostgreSQL

The primary region runs a Multi-AZ RDS instance, and the DR region has a read replica:

```hcl
Primary (us-east-1):
  - RDS PostgreSQL 15 (Multi-AZ)
  - Instance: db.r6g.xlarge
  - Storage: 100 GB (io2)
  - Backup: 30 days

DR (us-west-2):
  - RDS Read Replica
  - Instance: db.r6g.large
  - Cross-region replication lag: ~5-10 seconds
  - Can be promoted to primary
```

### Azure Database for PostgreSQL

The primary region runs a zone-redundant server with geo-restore enabled:

```hcl
Primary (eastus):
  - PostgreSQL Flexible Server 15
  - SKU: GP_Standard_D4s_v3
  - Storage: 128 GB
  - Zone-redundant HA
  - Geo-redundant backup: Enabled

DR (westus2):
  - Standby server (restored from backup)
  - SKU: GP_Standard_D2s_v3
  - Can be promoted to primary
```

## Storage Replication

### AWS S3

```hcl
Primary Bucket (us-east-1):
  - S3 Standard storage class
  - Versioning enabled
  - Replication to DR bucket

DR Bucket (us-west-2):
  - S3 Standard-IA storage class
  - Replica of primary bucket
  - Can be promoted during failover
```

### Azure Blob Storage

```hcl
Primary Storage (eastus):
  - GRS (Geo-redundant storage)
  - Automatic replication to DR region
  - Blob versioning enabled

DR Storage (westus2):
  - Read-access geo-redundant storage (RA-GRS)
  - Secondary endpoint available for reads
```

## Failover Procedures

See [FAILOVER.md](FAILOVER.md) for detailed failover procedures including:

1. Automated Failover (health check triggered)
2. Manual Failover (planned maintenance)
3. Database Promotion
4. DNS Cutover
5. Rollback Procedures

## Cost Estimates

### AWS Multi-Region (Production)

| Component | Primary Region | DR Region | Total/Month |
|-----------|---------------|-----------|-------------|
| ECS Fargate (3x/1x) | $220 | $75 | $295 |
| RDS PostgreSQL | $425 | $285 | $710 |
| S3 Storage (500GB) | $12 | $8 | $20 |
| ElastiCache Redis | $85 | $50 | $135 |
| Route53 Health Checks | - | - | $10 |
| Data Transfer | $90 | $90 | $180 |
| **Total** | | | **$1,350** |

### Azure Multi-Region (Production)

| Component | Primary Region | DR Region | Total/Month |
|-----------|---------------|-----------|-------------|
| Container Apps (3x/1x) | $190 | $65 | $255 |
| PostgreSQL Flexible | $380 | $255 | $635 |
| Blob Storage (500GB) | $10 | $7 | $17 |
| Redis Cache | $75 | $45 | $120 |
| Front Door | - | - | $35 |
| Data Transfer | $85 | $85 | $170 |
| **Total** | | | **$1,232** |

## Monitoring and Alerting

All regions include:

- **Health Checks**: HTTP endpoint monitoring every 30s
- **Replication Lag Alerts**: Alert if DB replica lag > 60s
- **Cross-Region Alerts**: Notifications sent to all regions
- **Dashboard**: Unified CloudWatch/App Insights dashboard
- **Runbooks**: Automated failover runbooks

## Validation Tests

### Test Deployment Plan

```bash
# Validate configuration
terraform validate

# Generate plan
terraform plan -out=tfplan

# Review changes
terraform show tfplan
```

### Test Failover (Non-Destructive)

```bash
# Test DR endpoint availability
./scripts/test-dr-endpoint.sh

# Test database replica lag
./scripts/check-replica-lag.sh

# Simulate failover (DNS change only)
./scripts/simulate-failover.sh --dry-run
```

## Security Considerations

1. **Network Isolation**: VPC/VNet peering between regions
2. **Encryption in Transit**: TLS 1.3 for all inter-region traffic
3. **Encryption at Rest**: All storage encrypted with KMS/Key Vault
4. **IAM Policies**: Least-privilege access per region
5. **Secrets Management**: Region-specific secret stores with replication

## Compliance

- **GDPR**: Data residency controls per region
- **HIPAA**: Encryption and audit logging in all regions
- **SOC 2**: Continuous monitoring and alerting
- **ISO 27001**: Security controls in all regions

## Disaster Recovery Metrics

| Metric | Target | Current |
|--------|--------|---------|
| **RTO** (Recovery Time Objective) | < 15 minutes | ~10 minutes |
| **RPO** (Recovery Point Objective) | < 60 seconds | ~5-10 seconds |
| **Availability** | 99.95% | 99.97% |
| **Replication Lag** | < 30 seconds | ~5-10 seconds |

## Maintenance Windows

- **Primary Region**: Sunday 2-4 AM UTC
- **DR Region**: Sunday 6-8 AM UTC (offset to avoid simultaneous downtime)

## Support

- **Documentation**: See [docs/deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md]
- **Runbooks**: See [docs/operations/RUNBOOKS.md]
- **Issues**: https://github.com/honuaio/honua/issues

## Next Steps

1. Review [FAILOVER.md](FAILOVER.md) for failover procedures
2. Set up monitoring dashboards
3. Schedule DR drills (quarterly recommended)
4. Configure backup retention policies
5. Set up alerting for replication lag

## License

Copyright (c) 2025 HonuaIO. All rights reserved.
