# Integration Test for AWS Lambda + ALB Module
# Tests full production-like configuration

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }
}

# Provider configuration
provider "aws" {
  region = var.aws_region

  # For testing without actual AWS resources
  skip_credentials_validation = var.skip_aws_validation
  skip_requesting_account_id  = var.skip_aws_validation
  skip_metadata_api_check     = var.skip_aws_validation
}

# Random suffix to avoid naming conflicts
resource "random_id" "test" {
  byte_length = 4
}

# Test module with full configuration
module "lambda_integration_test" {
  source = "../.."

  # General configuration
  environment  = "dev"
  service_name = "honua-int-test-${random_id.test.hex}"
  aws_region   = var.aws_region

  # Container configuration
  container_image_uri    = var.container_image_uri
  aspnetcore_environment = "Development"
  additional_env_vars = {
    TEST_MODE    = "true"
    LOG_LEVEL    = "Debug"
    FEATURE_FLAG = "integration-testing"
  }

  # Lambda configuration
  lambda_timeout           = 300
  lambda_memory_size       = 2048
  lambda_ephemeral_storage = 2048

  # Lambda autoscaling
  enable_lambda_autoscaling        = true
  lambda_min_provisioned_concurrency = 0
  lambda_max_provisioned_concurrency = 10

  # Function URL
  create_function_url    = true
  function_url_auth_type = "NONE"

  # VPC configuration
  create_vpc         = true
  vpc_cidr           = "10.0.0.0/16"
  availability_zones = ["us-east-1a", "us-east-1b"]
  enable_nat_gateway = true

  # Database configuration
  create_database              = true
  database_name                = "honua_test"
  db_username                  = "honua_test_user"
  postgres_version             = "15.4"
  db_instance_class            = "db.t4g.micro"
  db_allocated_storage         = 20
  db_max_allocated_storage     = 100
  db_multi_az                  = false
  db_backup_retention_days     = 7
  db_enable_performance_insights = true
  db_deletion_protection       = false  # Allow deletion for testing

  # ALB configuration
  create_alb              = true
  alb_deletion_protection = false
  alb_enable_access_logs  = false
  ssl_certificate_arn     = ""  # No SSL for testing

  # ECR configuration
  create_ecr_repository = true

  # Security
  kms_key_arn = ""

  # S3 access
  enable_s3_access = true
  s3_bucket_arns   = ["arn:aws:s3:::test-bucket/*"]

  # Logging
  log_retention_days = 7

  # Health check
  health_check_path = "/health"

  # CORS configuration
  cors_origins = ["http://localhost:3000", "https://test.honua.io"]

  # Tags
  tags = {
    test_type    = "integration"
    terraform    = "true"
    ephemeral    = "true"
    auto_cleanup = "true"
  }
}

# Test all outputs
output "lambda_function_name" {
  description = "Lambda function name"
  value       = module.lambda_integration_test.lambda_function_name
}

output "lambda_function_arn" {
  description = "Lambda function ARN"
  value       = module.lambda_integration_test.lambda_function_arn
}

output "lambda_function_url" {
  description = "Lambda Function URL"
  value       = module.lambda_integration_test.lambda_function_url
}

output "lambda_role_arn" {
  description = "Lambda execution role ARN"
  value       = module.lambda_integration_test.lambda_role_arn
}

output "alb_dns_name" {
  description = "ALB DNS name"
  value       = module.lambda_integration_test.alb_dns_name
}

output "alb_url" {
  description = "ALB URL"
  value       = module.lambda_integration_test.alb_url
}

output "database_endpoint" {
  description = "RDS instance endpoint"
  value       = module.lambda_integration_test.database_endpoint
}

output "database_name" {
  description = "Database name"
  value       = module.lambda_integration_test.database_name
}

output "jwt_secret_arn" {
  description = "JWT secret ARN"
  value       = module.lambda_integration_test.jwt_secret_arn
}

output "db_connection_secret_arn" {
  description = "Database connection secret ARN"
  value       = module.lambda_integration_test.db_connection_secret_arn
}

output "vpc_id" {
  description = "VPC ID"
  value       = module.lambda_integration_test.vpc_id
}

output "public_subnet_ids" {
  description = "Public subnet IDs"
  value       = module.lambda_integration_test.public_subnet_ids
}

output "private_subnet_ids" {
  description = "Private subnet IDs"
  value       = module.lambda_integration_test.private_subnet_ids
}

output "nat_gateway_ip" {
  description = "NAT Gateway IP"
  value       = module.lambda_integration_test.nat_gateway_ip
}

output "ecr_repository_url" {
  description = "ECR repository URL"
  value       = module.lambda_integration_test.ecr_repository_url
}

output "cloudwatch_log_group_name" {
  description = "CloudWatch log group name"
  value       = module.lambda_integration_test.cloudwatch_log_group_name
}

output "monitoring_urls" {
  description = "Console URLs for monitoring"
  value       = module.lambda_integration_test.monitoring_urls
}

output "deployment_info" {
  description = "Deployment information"
  value       = module.lambda_integration_test.deployment_info
}

output "estimated_monthly_cost" {
  description = "Estimated monthly cost"
  value       = module.lambda_integration_test.estimated_monthly_cost
}

# Integration test validations
output "integration_test_results" {
  description = "Integration test validation results"
  value = {
    lambda_created         = module.lambda_integration_test.lambda_function_name != null
    function_url_created   = module.lambda_integration_test.lambda_function_url != null
    alb_created            = module.lambda_integration_test.alb_dns_name != null
    database_created       = module.lambda_integration_test.database_endpoint != null
    vpc_created            = module.lambda_integration_test.vpc_id != null
    nat_gateway_created    = module.lambda_integration_test.nat_gateway_ip != null
    ecr_created            = module.lambda_integration_test.ecr_repository_url != null
    secrets_created        = module.lambda_integration_test.jwt_secret_arn != null
    logs_configured        = module.lambda_integration_test.cloudwatch_log_group_name != null
    environment_correct    = module.lambda_integration_test.deployment_info.environment == "dev"
    database_enabled       = module.lambda_integration_test.deployment_info.database_enabled == true
  }
}

# DNS configuration for reference
output "dns_configuration" {
  description = "Required DNS records if custom domain is used"
  value       = module.lambda_integration_test.dns_records_required
}

# Connection information
output "connection_info" {
  description = "Connection information for applications"
  value       = module.lambda_integration_test.connection_info
  sensitive   = true
}
