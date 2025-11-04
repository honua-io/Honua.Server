# Integration Test for Google Cloud Run Module
# Tests full production-like configuration

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

# Provider configuration
provider "google" {
  project = var.project_id
  region  = var.region
}

provider "google-beta" {
  project = var.project_id
  region  = var.region
}

# Random suffix to avoid naming conflicts
resource "random_id" "test" {
  byte_length = 4
}

# Test module with full configuration
module "cloud_run_integration_test" {
  source = "../.."

  # Project configuration
  project_id   = var.project_id
  region       = var.region
  environment  = "dev"
  service_name = "honua-int-test-${random_id.test.hex}"

  # Container configuration
  container_image       = var.container_image
  aspnetcore_environment = "Development"
  additional_env_vars = {
    TEST_MODE    = "true"
    LOG_LEVEL    = "Debug"
    FEATURE_FLAG = "integration-testing"
  }

  # Scaling configuration
  min_instances = 0
  max_instances = 5
  cpu_limit     = "2"
  memory_limit  = "1Gi"

  # Database configuration
  create_database             = true
  database_name               = "honua_test"
  db_username                 = "honua_test_user"
  postgres_version            = "POSTGRES_15"
  db_tier                     = "db-f1-micro"
  db_availability_type        = "ZONAL"
  db_disk_size                = 10
  db_max_disk_size            = 50
  db_backup_retention_days    = 7
  db_point_in_time_recovery   = true
  db_deletion_protection      = false  # Allow deletion for testing

  # VPC configuration
  enable_vpc_connector        = true
  vpc_network_name            = var.vpc_network_name
  vpc_network_id              = var.vpc_network_id
  vpc_connector_cidr          = var.vpc_connector_cidr
  vpc_connector_machine_type  = "e2-micro"
  vpc_connector_max_instances = 3

  # Load balancer configuration
  create_load_balancer = true
  custom_domains       = []  # No custom domains for testing
  lb_log_sample_rate   = 1.0

  # CDN configuration
  enable_cdn        = true
  cdn_default_ttl   = 3600
  cdn_max_ttl       = 86400
  cdn_client_ttl    = 3600

  # Security configuration
  allow_unauthenticated   = true
  enable_cloud_armor      = true
  rate_limit_threshold    = 1000
  blocked_ip_ranges       = []
  enable_adaptive_protection = false

  # Health check
  health_check_path = "/health"

  # Additional features
  enable_gcs_access = true

  # CORS configuration
  cors_origins = ["http://localhost:3000", "https://test.honua.io"]

  # Labels
  labels = {
    test_type    = "integration"
    terraform    = "true"
    ephemeral    = "true"
    auto_cleanup = "true"
  }
}

# Test all outputs
output "service_url" {
  description = "Cloud Run service URL"
  value       = module.cloud_run_integration_test.service_url
}

output "service_name" {
  description = "Cloud Run service name"
  value       = module.cloud_run_integration_test.service_name
}

output "service_id" {
  description = "Cloud Run service ID"
  value       = module.cloud_run_integration_test.service_id
}

output "load_balancer_ip" {
  description = "Global load balancer IP"
  value       = module.cloud_run_integration_test.load_balancer_ip
}

output "load_balancer_url" {
  description = "Load balancer URL"
  value       = module.cloud_run_integration_test.load_balancer_url
}

output "database_instance_name" {
  description = "Cloud SQL instance name"
  value       = module.cloud_run_integration_test.database_instance_name
}

output "database_connection_name" {
  description = "Cloud SQL connection name"
  value       = module.cloud_run_integration_test.database_connection_name
}

output "database_name" {
  description = "Database name"
  value       = module.cloud_run_integration_test.database_name
}

output "service_account_email" {
  description = "Service account email"
  value       = module.cloud_run_integration_test.service_account_email
}

output "vpc_connector_name" {
  description = "VPC connector name"
  value       = module.cloud_run_integration_test.vpc_connector_name
}

output "security_policy_id" {
  description = "Cloud Armor security policy ID"
  value       = module.cloud_run_integration_test.security_policy_id
}

output "monitoring_urls" {
  description = "Console URLs for monitoring"
  value       = module.cloud_run_integration_test.monitoring_urls
}

output "deployment_info" {
  description = "Deployment information"
  value       = module.cloud_run_integration_test.deployment_info
}

output "estimated_monthly_cost" {
  description = "Estimated monthly cost"
  value       = module.cloud_run_integration_test.estimated_monthly_cost
}

# Integration test validations
output "integration_test_results" {
  description = "Integration test validation results"
  value = {
    service_created           = module.cloud_run_integration_test.service_url != null
    database_created          = module.cloud_run_integration_test.database_instance_name != null
    load_balancer_created     = module.cloud_run_integration_test.load_balancer_ip != null
    vpc_connector_created     = module.cloud_run_integration_test.vpc_connector_name != null
    security_policy_created   = module.cloud_run_integration_test.security_policy_id != null
    service_account_created   = module.cloud_run_integration_test.service_account_email != null
    cdn_enabled               = module.cloud_run_integration_test.deployment_info.cdn_enabled == true
    cloud_armor_enabled       = module.cloud_run_integration_test.deployment_info.cloud_armor_enabled == true
    environment_correct       = module.cloud_run_integration_test.deployment_info.environment == "dev"
    min_instances_correct     = module.cloud_run_integration_test.deployment_info.min_instances == 0
    max_instances_correct     = module.cloud_run_integration_test.deployment_info.max_instances == 5
  }
}

# DNS configuration for reference
output "dns_configuration" {
  description = "Required DNS records if custom domain is used"
  value       = module.cloud_run_integration_test.dns_records_required
}
