# Unit Test for AWS Lambda + ALB Module
# Tests basic Terraform validation with minimal configuration

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

# Provider configuration (backend disabled for testing)
provider "aws" {
  region = var.aws_region

  # Skip credentials for validation-only testing
  skip_credentials_validation = true
  skip_requesting_account_id  = true
  skip_metadata_api_check     = true

  # Use mock endpoints for testing
  endpoints {
    lambda = "http://localhost:4566"  # LocalStack endpoint
    iam    = "http://localhost:4566"
    ec2    = "http://localhost:4566"
  }
}

# Test module with minimal configuration
module "lambda_test" {
  source = "../.."

  # Required variables
  environment         = "dev"
  service_name        = "honua-unit-test"
  aws_region          = var.aws_region
  container_image_uri = var.container_image_uri

  # Database configuration - disabled for unit test
  create_database = false

  # VPC configuration - use existing
  create_vpc         = false
  vpc_id             = "vpc-12345678"
  lambda_subnet_ids  = ["subnet-12345678", "subnet-87654321"]
  lambda_security_group_ids = ["sg-12345678"]

  # ALB - disabled for unit test
  create_alb = false

  # Function URL enabled for simple testing
  create_function_url   = true
  function_url_auth_type = "NONE"

  # Lambda configuration
  lambda_timeout          = 60
  lambda_memory_size      = 512
  lambda_ephemeral_storage = 512

  # Minimal autoscaling
  enable_lambda_autoscaling = false

  # Security
  enable_s3_access = false

  # Logging
  log_retention_days = 1

  # Tags
  tags = {
    test_type = "unit"
    terraform = "true"
  }
}

# Test outputs are generated correctly
output "lambda_function_name" {
  description = "Lambda function name from test module"
  value       = module.lambda_test.lambda_function_name
}

output "lambda_function_arn" {
  description = "Lambda function ARN from test module"
  value       = module.lambda_test.lambda_function_arn
}

output "lambda_function_url" {
  description = "Lambda function URL from test module"
  value       = module.lambda_test.lambda_function_url
}

output "lambda_role_arn" {
  description = "Lambda role ARN from test module"
  value       = module.lambda_test.lambda_role_arn
}

output "jwt_secret_arn" {
  description = "JWT secret ARN from test module"
  value       = module.lambda_test.jwt_secret_arn
}

output "deployment_info" {
  description = "Deployment information"
  value       = module.lambda_test.deployment_info
}

# Validation tests
output "test_validation" {
  description = "Test validation checks"
  value = {
    environment_valid       = module.lambda_test.deployment_info.environment == "dev"
    database_disabled       = module.lambda_test.deployment_info.database_enabled == false
    function_url_created    = module.lambda_test.lambda_function_url != null
    lambda_function_created = module.lambda_test.lambda_function_name != null
  }
}
