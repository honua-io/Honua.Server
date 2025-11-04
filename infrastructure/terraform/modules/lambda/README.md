# AWS Lambda + ALB Serverless Module

Comprehensive Terraform module for deploying Honua GIS Platform on AWS Lambda with Application Load Balancer, RDS PostgreSQL, and CloudFront CDN integration.

## Overview

This module deploys a serverless Honua GIS stack on AWS featuring:

- **AWS Lambda**: Container-based Lambda function with .NET 8
- **Application Load Balancer**: HTTPS load balancing with SSL
- **RDS PostgreSQL**: Managed PostgreSQL with PostGIS extension
- **VPC**: Private networking for Lambda and database
- **Secrets Manager**: Secure credential management
- **CloudWatch**: Logs and metrics
- **ECR**: Optional container registry

## Features

- **Serverless Compute**: Pay only for actual request time
- **Container Support**: Run Honua as Lambda container image
- **Auto-scaling**: Scale to handle thousands of concurrent requests
- **Private Networking**: Lambda in VPC with RDS
- **Managed SSL**: Use ACM certificates for HTTPS
- **Cost Optimized**: No charges when idle
- **High Availability**: Multi-AZ deployment available

## Prerequisites

1. **AWS Account**: Active AWS account
2. **Container Image**: Honua container image in ECR
3. **Terraform**: Version >= 1.5.0
4. **AWS CLI**: For deployment and management
5. **SSL Certificate**: ACM certificate for custom domain (optional)

### Required IAM Permissions

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "lambda:*",
        "iam:*",
        "ec2:*",
        "rds:*",
        "secretsmanager:*",
        "elasticloadbalancing:*",
        "logs:*"
      ],
      "Resource": "*"
    }
  ]
}
```

## Usage

### Basic Deployment with ALB

```hcl
module "honua_lambda" {
  source = "../../modules/lambda"

  environment         = "production"
  aws_region          = "us-east-1"
  container_image_uri = "123456789012.dkr.ecr.us-east-1.amazonaws.com/honua:latest"

  # Create infrastructure
  create_vpc      = true
  create_database = true
  create_alb      = true

  # SSL certificate from ACM
  ssl_certificate_arn = "arn:aws:acm:us-east-1:123456789012:certificate/..."

  # Lambda configuration
  lambda_memory_size = 2048
  lambda_timeout     = 300
}
```

### Development Environment (Minimal Cost)

```hcl
module "honua_dev" {
  source = "../../modules/lambda"

  environment         = "dev"
  aws_region          = "us-east-1"
  container_image_uri = "123456789012.dkr.ecr.us-east-1.amazonaws.com/honua:dev"

  # Minimal resources
  lambda_memory_size      = 1024
  lambda_timeout          = 180
  lambda_ephemeral_storage = 512

  # Smaller database
  db_instance_class       = "db.t4g.micro"
  db_allocated_storage    = 20
  db_multi_az             = false
  db_deletion_protection  = false

  # No NAT Gateway to save costs (Lambda can't reach internet)
  enable_nat_gateway = false

  # Use Function URL instead of ALB (simpler, cheaper)
  create_alb         = false
  create_function_url = true
  function_url_auth_type = "NONE"
}
```

### Production with High Availability

```hcl
module "honua_production" {
  source = "../../modules/lambda"

  environment         = "production"
  aws_region          = "us-east-1"
  container_image_uri = "123456789012.dkr.ecr.us-east-1.amazonaws.com/honua:v1.2.3"

  # Production Lambda config
  lambda_memory_size       = 4096
  lambda_timeout           = 300
  lambda_ephemeral_storage = 4096

  # Provisioned concurrency for low latency
  enable_lambda_autoscaling        = true
  lambda_min_provisioned_concurrency = 5
  lambda_max_provisioned_concurrency = 50

  # High availability database
  db_instance_class         = "db.r6g.xlarge"
  db_allocated_storage      = 100
  db_max_allocated_storage  = 500
  db_multi_az               = true
  db_backup_retention_days  = 30
  db_deletion_protection    = true
  db_enable_performance_insights = true

  # ALB with deletion protection
  create_alb             = true
  alb_deletion_protection = true
  ssl_certificate_arn    = "arn:aws:acm:us-east-1:123456789012:certificate/..."

  # Enable access logs
  alb_enable_access_logs  = true
  alb_access_logs_bucket  = "honua-alb-logs-prod"

  # Longer log retention
  log_retention_days = 30

  tags = {
    Team       = "Platform"
    CostCenter = "Engineering"
    Tier       = "Production"
  }
}
```

### Using Existing VPC

```hcl
module "honua_existing_vpc" {
  source = "../../modules/lambda"

  environment         = "production"
  aws_region          = "us-east-1"
  container_image_uri = "123456789012.dkr.ecr.us-east-1.amazonaws.com/honua:latest"

  # Use existing VPC
  create_vpc = false
  vpc_id     = "vpc-12345678"

  # Provide subnet IDs
  lambda_subnet_ids   = ["subnet-private1", "subnet-private2"]
  alb_subnet_ids      = ["subnet-public1", "subnet-public2"]
  database_subnet_ids = ["subnet-db1", "subnet-db2"]

  # Provide security groups
  lambda_security_group_ids = ["sg-lambda"]

  create_database = true
  create_alb      = true
}
```

## Input Variables

### Required Variables

| Name | Description | Type |
|------|-------------|------|
| `environment` | Environment (dev/staging/production) | `string` |
| `container_image_uri` | ECR image URI | `string` |

### Lambda Configuration

| Name | Description | Default |
|------|-------------|---------|
| `lambda_timeout` | Timeout in seconds (max 900) | `300` |
| `lambda_memory_size` | Memory in MB (128-10240) | `2048` |
| `lambda_ephemeral_storage` | Storage in MB (512-10240) | `2048` |
| `enable_lambda_autoscaling` | Enable provisioned concurrency | `false` |

### Database Configuration

| Name | Description | Default |
|------|-------------|---------|
| `create_database` | Create RDS instance | `true` |
| `db_instance_class` | RDS instance type | `"db.t4g.small"` |
| `db_multi_az` | Multi-AZ deployment | `false` |
| `db_backup_retention_days` | Backup retention | `7` |

### Network Configuration

| Name | Description | Default |
|------|-------------|---------|
| `create_vpc` | Create new VPC | `true` |
| `vpc_cidr` | VPC CIDR block | `"10.0.0.0/16"` |
| `enable_nat_gateway` | Enable NAT Gateway | `true` |

### ALB Configuration

| Name | Description | Default |
|------|-------------|---------|
| `create_alb` | Create Application Load Balancer | `true` |
| `ssl_certificate_arn` | ACM certificate ARN | `""` |
| `alb_deletion_protection` | Enable deletion protection | `false` |

## Outputs

- `alb_dns_name` - DNS name of the ALB
- `alb_url` - Full URL (http/https) of the ALB
- `lambda_function_name` - Name of the Lambda function
- `lambda_function_url` - Function URL (if enabled)
- `database_endpoint` - RDS endpoint
- `monitoring_urls` - AWS Console URLs for monitoring

## Post-Deployment Steps

### 1. Build and Push Container Image

```bash
# Authenticate to ECR
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin 123456789012.dkr.ecr.us-east-1.amazonaws.com

# Build container optimized for Lambda
docker build -f Dockerfile -t honua:latest .

# Tag for ECR
docker tag honua:latest 123456789012.dkr.ecr.us-east-1.amazonaws.com/honua:latest

# Push to ECR
docker push 123456789012.dkr.ecr.us-east-1.amazonaws.com/honua:latest
```

### 2. Install PostGIS Extension

```bash
# Connect to RDS instance
psql -h DATABASE_ENDPOINT -U honua -d honua

# Install PostGIS
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;
```

### 3. Configure DNS

```bash
# Get ALB DNS name
terraform output alb_dns_name

# Create CNAME record in Route 53 or your DNS provider
# api.honua.io -> honua-production-1234567890.us-east-1.elb.amazonaws.com
```

### 4. Test Deployment

```bash
# Test health endpoint via ALB
curl https://api.honua.io/health

# Test via Function URL (if enabled)
curl $(terraform output -raw lambda_function_url)/health

# Test GIS endpoint
curl https://api.honua.io/api/v1/layers
```

### 5. Monitor Performance

```bash
# View Lambda logs
aws logs tail /aws/lambda/honua-production --follow

# View Lambda metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Duration \
  --dimensions Name=FunctionName,Value=honua-production \
  --start-time 2024-01-01T00:00:00Z \
  --end-time 2024-01-01T23:59:59Z \
  --period 3600 \
  --statistics Average
```

## Cost Estimation

### Development Environment
- **Lambda**: $5-10/month (low traffic, free tier eligible)
- **RDS** (db.t4g.micro): $12/month
- **Function URL**: Free
- **Secrets Manager**: $1/month
- **CloudWatch**: $2/month
- **Total**: ~$20-30/month

### Production (Low-Medium Traffic)
- **Lambda**: $30-100/month (depends on requests)
- **RDS** (db.t4g.small, Multi-AZ): $50/month
- **ALB**: $22/month
- **NAT Gateway**: $33/month
- **Provisioned Concurrency** (5 units): $35/month
- **Total**: ~$170-250/month

### Production (High Traffic)
- **Lambda**: $200-500/month
- **RDS** (db.r6g.xlarge, Multi-AZ): $500/month
- **ALB**: $50/month (including LCU charges)
- **NAT Gateway**: $50/month (with data transfer)
- **Provisioned Concurrency** (20 units): $140/month
- **Total**: ~$1,000-1,500/month

**Note**: Lambda costs scale with usage. Consider CloudFront CDN to reduce Lambda invocations.

## Troubleshooting

### Lambda Timeout Errors

```bash
# Increase timeout
lambda_timeout = 900  # Max for Lambda

# Increase memory (also increases CPU)
lambda_memory_size = 4096

# Enable provisioned concurrency to reduce cold starts
enable_lambda_autoscaling = true
```

### Database Connection Issues

```bash
# Verify Lambda is in VPC
aws lambda get-function-configuration --function-name honua-production

# Check security group rules
aws ec2 describe-security-groups --group-ids sg-xxxxx

# Test connectivity from Lambda
aws lambda invoke \
  --function-name honua-production \
  --payload '{"test":"database"}' \
  response.json
```

### High NAT Gateway Costs

```bash
# Option 1: Use VPC Endpoints to avoid NAT
# Create endpoints for: S3, Secrets Manager, CloudWatch Logs

# Option 2: Disable NAT if Lambda doesn't need internet
enable_nat_gateway = false
```

### Cold Start Latency

```bash
# Enable provisioned concurrency
enable_lambda_autoscaling = true
lambda_min_provisioned_concurrency = 5

# Or use ALB + keep-alive to maintain warm instances
# ALB naturally keeps Lambda warm with traffic
```

## Integration with CloudFront CDN

Use the CDN module for optimal performance:

```hcl
module "honua_lambda" {
  source = "../../modules/lambda"
  # ... configuration
}

module "cdn" {
  source = "../../modules/cdn"

  cloud_provider = "aws"
  origin_domain  = module.honua_lambda.alb_dns_name
  origin_protocol_policy = "https-only"

  # Cache tiles for 1 hour
  default_ttl = 3600
}
```

## Lambda Container Image Requirements

Your Dockerfile should include:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
# Install AWS Lambda runtime interface emulator
ADD https://github.com/aws/aws-lambda-runtime-interface-emulator/releases/latest/download/aws-lambda-rie /usr/bin/aws-lambda-rie
RUN chmod +x /usr/bin/aws-lambda-rie

# Copy your application
COPY --from=build /app/publish .

# Entry point for Lambda
ENTRYPOINT ["/usr/bin/aws-lambda-rie", "dotnet", "Honua.Server.Host.dll"]
```

## Security Best Practices

1. **Use Secrets Manager**: Never hardcode credentials
2. **Enable encryption**: Use KMS for RDS, secrets, logs
3. **Private networking**: Lambda and RDS in private subnets
4. **Least privilege IAM**: Only grant required permissions
5. **Enable deletion protection**: For production databases
6. **Use VPC endpoints**: Reduce NAT Gateway exposure
7. **Enable CloudTrail**: Audit all API calls
8. **Rotate secrets**: Configure automatic rotation

## Monitoring and Observability

### Key Metrics

- **Lambda Duration**: Track P50, P95, P99
- **Lambda Errors**: Monitor error rate
- **Lambda Concurrent Executions**: Watch scaling
- **RDS CPU**: Alert on high CPU
- **RDS Connections**: Monitor connection pool
- **ALB Request Count**: Track traffic patterns
- **ALB Target Response Time**: Monitor latency

### CloudWatch Dashboards

```bash
# Create dashboard
aws cloudwatch put-dashboard \
  --dashboard-name honua-production \
  --dashboard-body file://dashboard.json
```

### Alarms

Configure CloudWatch Alarms for:
- Lambda error rate > 1%
- Lambda duration > 10s
- RDS CPU > 80%
- RDS connections > 80% of max
- ALB 5xx errors > 5%

## Migration from Kubernetes

1. **Package as container**: Lambda supports container images
2. **Export database**: Dump PostgreSQL
3. **Deploy module**: Create Lambda infrastructure
4. **Import data**: Restore to RDS
5. **Update DNS**: Point to ALB
6. **Monitor**: Watch Lambda metrics
7. **Optimize**: Adjust memory/timeout based on metrics
8. **Decommission**: Remove Kubernetes cluster

## Limitations

- **Request timeout**: Max 900 seconds (15 minutes)
- **Deployment package**: Max 10 GB container image
- **Ephemeral storage**: Max 10 GB
- **Payload size**: Max 6 MB request/response
- **Cold starts**: First request may be slow (use provisioned concurrency)

## Advanced Configuration

### Custom Lambda Runtime

```hcl
# Use Lambda layers for shared dependencies
additional_env_vars = {
  LD_LIBRARY_PATH = "/opt/lib"
}
```

### Multi-Region Deployment

```hcl
# Deploy to multiple regions
module "honua_us_east" {
  source     = "../../modules/lambda"
  aws_region = "us-east-1"
  # ... config
}

module "honua_eu_west" {
  source     = "../../modules/lambda"
  aws_region = "eu-west-1"
  # ... config
}

# Use Route 53 for global routing
```

## Support

- [AWS Lambda Documentation](https://docs.aws.amazon.com/lambda/)
- [RDS PostgreSQL Documentation](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/)
- [Honua Platform](https://github.com/HonuaIO/honua)
- [Report Issues](https://github.com/HonuaIO/honua/issues)

## License

This module is part of the Honua platform, licensed under Elastic License 2.0.
