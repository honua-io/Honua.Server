# Example: AWS Lambda Production Environment
# Production configuration with high availability and CDN

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Configure backend for state storage
  # backend "s3" {
  #   bucket         = "honua-terraform-state-prod"
  #   key            = "serverless/aws-prod/terraform.tfstate"
  #   region         = "us-east-1"
  #   encrypt        = true
  #   dynamodb_table = "honua-terraform-locks"
  # }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Environment = "production"
      Project     = "Honua"
      ManagedBy   = "Terraform"
      Tier        = "Serverless"
    }
  }
}

# ==================== KMS Key for Encryption ====================
resource "aws_kms_key" "honua" {
  description             = "KMS key for Honua production"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = {
    Name = "honua-production-key"
  }
}

resource "aws_kms_alias" "honua" {
  name          = "alias/honua-production"
  target_key_id = aws_kms_key.honua.key_id
}

# ==================== Lambda Deployment ====================
module "honua_serverless" {
  source = "../../../modules/lambda"

  environment         = "production"
  aws_region          = var.aws_region
  container_image_uri = var.container_image_uri

  # Production Lambda configuration
  lambda_timeout          = 300
  lambda_memory_size      = 4096  # More memory = more CPU
  lambda_ephemeral_storage = 4096

  # Provisioned concurrency for low latency
  enable_lambda_autoscaling        = true
  lambda_min_provisioned_concurrency = 10  # Always warm
  lambda_max_provisioned_concurrency = 100

  # Create full VPC infrastructure
  create_vpc         = true
  vpc_cidr           = "10.0.0.0/16"
  availability_zones = ["${var.aws_region}a", "${var.aws_region}b"]
  enable_nat_gateway = true

  # Production database with high availability
  create_database              = true
  db_instance_class            = "db.r6g.xlarge"
  db_allocated_storage         = 100
  db_max_allocated_storage     = 500
  db_multi_az                  = true  # High availability
  db_backup_retention_days     = 30
  db_enable_performance_insights = true
  db_deletion_protection       = true

  # Application Load Balancer
  create_alb             = true
  alb_deletion_protection = true
  ssl_certificate_arn    = var.ssl_certificate_arn

  # Enable access logs
  alb_enable_access_logs = true
  alb_access_logs_bucket = var.alb_logs_bucket

  # Encryption
  kms_key_arn = aws_kms_key.honua.arn

  # S3 access for GIS data
  enable_s3_access = true
  s3_bucket_arns = [
    "arn:aws:s3:::honua-raster-data-prod",
    "arn:aws:s3:::honua-raster-data-prod/*"
  ]

  # Longer log retention
  log_retention_days = 30

  # Production CORS
  cors_origins = var.cors_origins

  tags = {
    Team       = "Platform"
    CostCenter = "Engineering"
  }
}

# ==================== CloudFront CDN ====================
module "cdn" {
  source = "../../../modules/cdn"

  cloud_provider = "aws"
  environment    = "production"
  origin_domain  = module.honua_serverless.alb_dns_name

  # Custom domain
  custom_domains      = var.custom_domains
  ssl_certificate_arn = var.cloudfront_certificate_arn  # Must be in us-east-1

  # Optimized cache policies for GIS
  tiles_ttl    = 3600   # 1 hour
  features_ttl = 300    # 5 minutes
  metadata_ttl = 86400  # 24 hours

  # Global distribution
  cloudfront_price_class = "PriceClass_All"

  # Origin Shield for cost savings
  enable_origin_shield  = true
  origin_shield_region  = var.aws_region

  # Logging
  cloudfront_logging_bucket = var.cloudfront_logs_bucket

  # WAF protection
  waf_web_acl_arn = var.waf_web_acl_arn

  tags = {
    Team = "Platform"
  }
}

# ==================== Outputs ====================
output "alb_url" {
  description = "Application Load Balancer URL (for direct access)"
  value       = module.honua_serverless.alb_url
}

output "cdn_url" {
  description = "CloudFront CDN URL (use this for production traffic)"
  value       = module.cdn.cdn_url
}

output "lambda_function_name" {
  description = "Lambda function name"
  value       = module.honua_serverless.lambda_function_name
}

output "database_endpoint" {
  description = "RDS database endpoint"
  value       = module.honua_serverless.database_endpoint
  sensitive   = true
}

output "estimated_monthly_cost" {
  description = "Estimated monthly cost"
  value = <<-EOT
    Lambda (10 provisioned):     $350/month
    RDS (r6g.xlarge, Multi-AZ):  $600/month
    ALB:                         $25/month
    NAT Gateway:                 $35/month
    CloudFront:                  $50-200/month (depends on traffic)
    Secrets Manager:             $2/month
    CloudWatch Logs:             $10/month
    -------------------------------------------
    Total Estimate:              $1,072-1,222/month

    Note: Actual costs vary with traffic. CloudFront and Lambda costs scale with usage.
  EOT
}

output "deployment_checklist" {
  description = "Post-deployment checklist"
  value = <<-EOT
    Production Deployment Checklist:

    1. Database Setup:
       - Install PostGIS extension
       - Run schema migrations
       - Configure backup retention
       - Set up automated backups

    2. DNS Configuration:
       - Create CNAME: ${var.custom_domains[0]} -> ${module.cdn.cdn_domain}
       - Wait for SSL certificate validation
       - Test HTTPS access

    3. Monitoring Setup:
       - Configure CloudWatch alarms
       - Set up SNS notifications
       - Enable X-Ray tracing
       - Create dashboards

    4. Security Review:
       - Review IAM policies
       - Enable CloudTrail logging
       - Configure WAF rules
       - Test security groups

    5. Performance Testing:
       - Load test endpoints
       - Verify Lambda warm-up
       - Check CDN cache hit ratio
       - Monitor RDS connections

    6. Documentation:
       - Update runbook
       - Document rollback procedure
       - Update architecture diagrams
  EOT
}
