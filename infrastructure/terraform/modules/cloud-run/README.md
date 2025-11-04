# Google Cloud Run Serverless Module

Comprehensive Terraform module for deploying Honua GIS Platform on Google Cloud Run with Cloud SQL PostgreSQL, Cloud CDN, and Cloud Load Balancer.

## Overview

This module deploys a fully managed, serverless Honua GIS stack on Google Cloud Platform featuring:

- **Cloud Run**: Fully managed serverless container platform
- **Cloud SQL**: PostgreSQL with PostGIS extension
- **Cloud CDN**: Global content delivery for GIS tiles
- **Cloud Load Balancer**: Global HTTPS load balancing with SSL
- **Secret Manager**: Secure credential management
- **Cloud Armor**: DDoS protection and rate limiting
- **VPC Connector**: Private database access

## Features

- **True Serverless**: Scale to zero when not in use
- **Auto-scaling**: 0-100 instances based on demand
- **Global CDN**: Sub-second tile delivery worldwide
- **Managed SSL**: Free SSL certificates via Google
- **Private Networking**: Database access via VPC connector
- **DDoS Protection**: Cloud Armor security policies
- **Connection Pooling**: Optimized database connections
- **Cost Optimized**: Pay only for actual usage

## Prerequisites

1. **GCP Project**: Active GCP project with billing enabled
2. **APIs Enabled**:
   ```bash
   gcloud services enable run.googleapis.com
   gcloud services enable sql-component.googleapis.com
   gcloud services enable sqladmin.googleapis.com
   gcloud services enable compute.googleapis.com
   gcloud services enable vpcaccess.googleapis.com
   gcloud services enable secretmanager.googleapis.com
   ```
3. **Container Image**: Honua container image in GCR or Artifact Registry
4. **Terraform**: Version >= 1.5.0
5. **GCP Credentials**: Service account with required permissions

### Required IAM Roles

```
roles/run.admin
roles/cloudsql.admin
roles/compute.admin
roles/secretmanager.admin
roles/iam.serviceAccountAdmin
roles/vpcaccess.admin
```

## Usage

### Basic Deployment

```hcl
module "honua_serverless" {
  source = "../../modules/cloud-run"

  project_id       = "my-gcp-project"
  region           = "us-central1"
  environment      = "production"
  container_image  = "gcr.io/my-project/honua:latest"

  # Custom domain with SSL
  custom_domains = ["api.honua.example.com"]

  # Serverless scaling
  min_instances = 0
  max_instances = 100

  # Database
  create_database = true
  db_tier         = "db-g1-small"
}
```

### Development Environment

```hcl
module "honua_dev" {
  source = "../../modules/cloud-run"

  project_id      = "honua-dev-12345"
  region          = "us-central1"
  environment     = "dev"
  container_image = "gcr.io/honua-dev-12345/honua:dev"

  # Minimal resources for dev
  min_instances   = 0
  max_instances   = 10
  cpu_limit       = "1"
  memory_limit    = "512Mi"

  # Smaller database
  db_tier                  = "db-f1-micro"
  db_availability_type     = "ZONAL"
  db_backup_retention_days = 3
  db_deletion_protection   = false

  # No custom domain in dev
  create_load_balancer = false

  # Less restrictive CORS for dev
  cors_origins = ["http://localhost:3000", "https://dev.honua.example.com"]
}
```

### Production with High Availability

```hcl
module "honua_production" {
  source = "../../modules/cloud-run"

  project_id      = "honua-prod-12345"
  region          = "us-central1"
  environment     = "production"
  container_image = "gcr.io/honua-prod-12345/honua:v1.2.3"

  # High availability scaling
  min_instances   = 2  # Always-on for low latency
  max_instances   = 100
  cpu_limit       = "4"
  memory_limit    = "4Gi"
  request_timeout = 300

  # Production database
  db_tier                  = "db-custom-4-16384"  # 4 vCPU, 16GB RAM
  db_availability_type     = "REGIONAL"
  db_backup_retention_days = 30
  db_point_in_time_recovery = true
  db_deletion_protection   = true

  # Custom domain with SSL
  create_load_balancer = true
  custom_domains       = ["api.honua.io", "www.honua.io"]

  # CDN for performance
  enable_cdn        = true
  cdn_default_ttl   = 3600   # 1 hour for tiles
  cdn_max_ttl       = 86400  # 24 hours

  # Security
  enable_cloud_armor         = true
  rate_limit_threshold       = 1000
  enable_adaptive_protection = true

  # Strict CORS
  cors_origins = ["https://honua.io", "https://app.honua.io"]

  labels = {
    team        = "platform"
    cost-center = "engineering"
    tier        = "production"
  }
}
```

### Using External Database

```hcl
module "honua_existing_db" {
  source = "../../modules/cloud-run"

  project_id      = "honua-project"
  region          = "us-central1"
  environment     = "production"
  container_image = "gcr.io/honua-project/honua:latest"

  # Use existing database
  create_database = false

  # Provide connection via environment variable
  additional_env_vars = {
    DATABASE_CONNECTION_STRING = "Host=10.1.2.3;Database=honua;Username=honua;Password=..."
  }

  # Still need VPC connector for private DB access
  enable_vpc_connector = true
  vpc_network_name     = "my-vpc"
  vpc_connector_cidr   = "10.8.0.0/28"
}
```

## Input Variables

### Required Variables

| Name | Description | Type | Default |
|------|-------------|------|---------|
| `project_id` | GCP project ID | `string` | - |
| `container_image` | Full container image path | `string` | - |

### Scaling Configuration

| Name | Description | Type | Default |
|------|-------------|------|---------|
| `min_instances` | Minimum instances (0 for serverless) | `number` | `0` |
| `max_instances` | Maximum instances | `number` | `100` |
| `cpu_limit` | CPU limit (1, 2, 4, or 8) | `string` | `"2"` |
| `memory_limit` | Memory limit (e.g., 2Gi) | `string` | `"2Gi"` |
| `max_concurrent_requests` | Concurrent requests per instance | `number` | `80` |
| `request_timeout` | Request timeout in seconds (max 3600) | `number` | `300` |

### Database Configuration

| Name | Description | Type | Default |
|------|-------------|------|---------|
| `create_database` | Create Cloud SQL instance | `bool` | `true` |
| `database_name` | Database name | `string` | `"honua"` |
| `postgres_version` | PostgreSQL version | `string` | `"POSTGRES_15"` |
| `db_tier` | Cloud SQL tier | `string` | `"db-g1-small"` |
| `db_availability_type` | ZONAL or REGIONAL | `string` | `"ZONAL"` |
| `db_backup_retention_days` | Backup retention days | `number` | `7` |

### Network Configuration

| Name | Description | Type | Default |
|------|-------------|------|---------|
| `enable_vpc_connector` | Enable VPC connector | `bool` | `true` |
| `vpc_network_name` | VPC network name | `string` | `"default"` |
| `vpc_connector_cidr` | VPC connector CIDR | `string` | `"10.8.0.0/28"` |

### CDN Configuration

| Name | Description | Type | Default |
|------|-------------|------|---------|
| `enable_cdn` | Enable Cloud CDN | `bool` | `true` |
| `cdn_default_ttl` | Default cache TTL (seconds) | `number` | `3600` |
| `cdn_max_ttl` | Maximum cache TTL (seconds) | `number` | `86400` |

### Security Configuration

| Name | Description | Type | Default |
|------|-------------|------|---------|
| `allow_unauthenticated` | Allow public access | `bool` | `true` |
| `enable_cloud_armor` | Enable Cloud Armor | `bool` | `true` |
| `rate_limit_threshold` | Rate limit (req/min) | `number` | `1000` |
| `blocked_ip_ranges` | IPs to block | `list(string)` | `[]` |

## Outputs

### Service Information

- `service_url` - Cloud Run service URL
- `service_name` - Service name
- `load_balancer_ip` - Global IP address
- `load_balancer_url` - HTTPS URL with custom domain

### Database Information

- `database_connection_name` - Cloud SQL connection name
- `database_name` - Database name
- `database_instance_name` - Instance name

### Secrets

- `jwt_secret_id` - JWT secret in Secret Manager
- `db_connection_secret_id` - Database connection secret

### Monitoring

- `monitoring_urls` - Links to GCP Console dashboards

## Post-Deployment Steps

### 1. Configure DNS

After deployment, configure DNS for custom domains:

```bash
# Get the load balancer IP
terraform output load_balancer_ip

# Add A records for your domains pointing to this IP
# Example:
# api.honua.io.  300  IN  A  35.244.x.x
```

### 2. Install PostGIS Extension

Connect to the database and enable PostGIS:

```bash
# Connect via Cloud SQL Proxy
gcloud sql connect INSTANCE_NAME --user=honua --database=honua

# In psql:
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;
```

### 3. Run Database Migrations

Deploy your schema:

```bash
# Example using Cloud Run Jobs
gcloud run jobs create honua-migrate \
  --image gcr.io/PROJECT/honua:latest \
  --command migrate \
  --region us-central1
```

### 4. Verify Deployment

```bash
# Test health endpoint
curl https://api.honua.example.com/health

# Test with authenticated request
curl https://api.honua.example.com/api/v1/layers
```

## Cost Estimation

### Development Environment
- **Cloud Run**: $10-20/month (low traffic)
- **Cloud SQL** (db-f1-micro): $7/month
- **VPC Connector**: $10/month
- **Secrets**: $1/month
- **Total**: ~$30-40/month

### Production Environment (Medium Traffic)
- **Cloud Run**: $50-100/month (2 min instances + bursting)
- **Cloud SQL** (db-g1-small): $26/month
- **Load Balancer**: $18/month
- **Cloud CDN**: $20-50/month (depends on cache hit ratio)
- **VPC Connector**: $10/month
- **Cloud Armor**: $10/month
- **Total**: ~$150-250/month

### Production Environment (High Traffic)
- **Cloud Run**: $300-500/month (10+ min instances)
- **Cloud SQL** (db-custom-4-16384, HA): $450/month
- **Load Balancer**: $30/month
- **Cloud CDN**: $100-300/month
- **Cloud Armor**: $20/month
- **Total**: ~$1,000-1,500/month

Costs scale with actual usage. First 2M Cloud Run requests are free each month.

## Troubleshooting

### Database Connection Failures

```bash
# Check VPC connector status
gcloud compute networks vpc-access connectors describe \
  CONNECTOR_NAME --region REGION

# Check Cloud SQL connectivity
gcloud sql operations list --instance INSTANCE_NAME

# Verify service account permissions
gcloud projects get-iam-policy PROJECT_ID \
  --flatten="bindings[].members" \
  --filter="bindings.members:serviceAccount:SERVICE_ACCOUNT_EMAIL"
```

### Slow Cold Starts

```
# Set minimum instances to keep service warm
min_instances = 1

# Enable CPU always allocated
cpu_always_allocated = true
```

### High Database Connection Count

```sql
-- Check active connections
SELECT count(*) FROM pg_stat_activity;

-- Reduce max_concurrent_requests or increase db_max_connections
```

### SSL Certificate Provisioning

SSL certificates can take up to 24 hours to provision. Check status:

```bash
gcloud compute ssl-certificates describe CERT_NAME --global
```

Ensure DNS is correctly configured and propagated.

## Security Best Practices

1. **Never commit secrets**: Use Secret Manager
2. **Enable deletion protection**: For production databases
3. **Use Cloud Armor**: Enable rate limiting and DDoS protection
4. **Restrict CORS**: Only allow trusted origins
5. **Use private networking**: VPC connector for database access
6. **Enable audit logging**: Monitor all API calls
7. **Rotate secrets regularly**: Use secret rotation
8. **Review IAM permissions**: Principle of least privilege

## Monitoring and Observability

### Key Metrics to Monitor

- **Request latency**: P50, P95, P99
- **Error rate**: 4xx and 5xx responses
- **Instance count**: Track scaling patterns
- **Database connections**: Avoid connection exhaustion
- **CDN hit ratio**: Higher is better
- **Cold start frequency**: Optimize with min instances

### Logs

View logs in Cloud Console or via gcloud:

```bash
gcloud logging read "resource.type=cloud_run_revision \
  AND resource.labels.service_name=honua-production" \
  --limit 50 --format json
```

### Alerts

Configure Cloud Monitoring alerts for:
- High error rates (>5%)
- High latency (P95 > 2s)
- Database CPU > 80%
- High connection count
- Low CDN hit ratio (<70%)

## Migration from Kubernetes

To migrate from existing Kubernetes deployment:

1. **Export data**: Dump PostgreSQL database
2. **Deploy module**: Create Cloud Run service
3. **Import data**: Restore to Cloud SQL
4. **Update DNS**: Point to new load balancer IP
5. **Monitor**: Watch logs and metrics
6. **Cutover**: Update health checks
7. **Decommission**: Remove old Kubernetes cluster

## Integration with CI/CD

### GitHub Actions Example

```yaml
- name: Deploy to Cloud Run
  run: |
    cd infrastructure/terraform/environments/production
    terraform init
    terraform apply -auto-approve \
      -var="container_image=gcr.io/$PROJECT_ID/honua:${{ github.sha }}"
```

### Cloud Build Example

```yaml
steps:
  - name: 'gcr.io/cloud-builders/docker'
    args: ['build', '-t', 'gcr.io/$PROJECT_ID/honua:$SHORT_SHA', '.']

  - name: 'hashicorp/terraform:1.5'
    dir: 'infrastructure/terraform/environments/production'
    args:
      - apply
      - -auto-approve
      - -var=container_image=gcr.io/$PROJECT_ID/honua:$SHORT_SHA
```

## Advanced Configuration

### Custom Health Checks

```hcl
health_check_path = "/api/health/ready"

additional_env_vars = {
  HEALTH_CHECK_TIMEOUT = "5"
  HEALTH_CHECK_ENDPOINTS = "/health,/health/ready,/health/live"
}
```

### Multiple Regions

Deploy to multiple regions for global coverage:

```hcl
module "honua_us" {
  source = "../../modules/cloud-run"
  region = "us-central1"
  # ... other config
}

module "honua_eu" {
  source = "../../modules/cloud-run"
  region = "europe-west1"
  # ... other config
}

# Use Cloud Load Balancer to route traffic to nearest region
```

### Custom VPC

```hcl
vpc_network_name    = "honua-vpc"
vpc_network_id      = "projects/PROJECT/global/networks/honua-vpc"
vpc_connector_cidr  = "10.8.0.0/28"
```

## Support and Resources

- [Cloud Run Documentation](https://cloud.google.com/run/docs)
- [Cloud SQL Documentation](https://cloud.google.com/sql/docs)
- [Cloud CDN Documentation](https://cloud.google.com/cdn/docs)
- [Honua Platform Documentation](https://github.com/HonuaIO/honua)
- [Report Issues](https://github.com/HonuaIO/honua/issues)

## License

This module is part of the Honua platform, licensed under Elastic License 2.0.
