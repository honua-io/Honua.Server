# Unit Test for Google Cloud Run Module
# Tests basic Terraform validation with minimal configuration

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
    google-beta = {
      source  = "hashicorp/google-beta"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }
}

# Provider configuration (backend disabled for testing)
provider "google" {
  project = var.project_id
  region  = var.region
}

provider "google-beta" {
  project = var.project_id
  region  = var.region
}

# Test module with minimal configuration
module "cloud_run_test" {
  source = "../.."

  # Required variables
  project_id      = var.project_id
  region          = var.region
  environment     = "dev"
  service_name    = "honua-unit-test"
  container_image = "gcr.io/cloudrun/hello"

  # Database configuration - disabled for unit test
  create_database = false

  # Load balancer - disabled for unit test
  create_load_balancer = false

  # VPC connector - disabled for unit test
  enable_vpc_connector = false

  # Minimal scaling for testing
  min_instances = 0
  max_instances = 1

  # Basic resources
  cpu_limit    = "1"
  memory_limit = "512Mi"

  # Security - allow public access for testing
  allow_unauthenticated = true
  enable_cloud_armor    = false

  # Labels for testing
  labels = {
    test_type = "unit"
    terraform = "true"
  }
}

# Test outputs are generated correctly
output "service_url" {
  description = "Service URL from test module"
  value       = module.cloud_run_test.service_url
}

output "service_name" {
  description = "Service name from test module"
  value       = module.cloud_run_test.service_name
}

output "service_account_email" {
  description = "Service account email from test module"
  value       = module.cloud_run_test.service_account_email
}

output "jwt_secret_id" {
  description = "JWT secret ID from test module"
  value       = module.cloud_run_test.jwt_secret_id
}

output "deployment_info" {
  description = "Deployment information"
  value       = module.cloud_run_test.deployment_info
}

# Validation tests
output "test_validation" {
  description = "Test validation checks"
  value = {
    environment_valid     = module.cloud_run_test.deployment_info.environment == "dev"
    min_instances_valid   = module.cloud_run_test.deployment_info.min_instances == 0
    max_instances_valid   = module.cloud_run_test.deployment_info.max_instances == 1
    database_disabled     = module.cloud_run_test.deployment_info.database_enabled == false
    service_url_generated = module.cloud_run_test.service_url != null
  }
}
