# Example: Google Cloud Run Development Environment
# Minimal cost configuration for development/testing

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
  }

  # Configure backend for state storage
  # backend "gcs" {
  #   bucket = "honua-terraform-state-dev"
  #   prefix = "serverless/gcp-dev"
  # }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

provider "google-beta" {
  project = var.project_id
  region  = var.region
}

# ==================== Cloud Run Deployment ====================
module "honua_serverless" {
  source = "../../../modules/cloud-run"

  project_id      = var.project_id
  region          = var.region
  environment     = "dev"
  container_image = var.container_image

  # Minimal serverless configuration
  min_instances   = 0  # True serverless - scale to zero
  max_instances   = 10 # Limited for dev
  cpu_limit       = "1"
  memory_limit    = "1Gi"
  request_timeout = 300

  # Development database
  create_database           = true
  db_tier                   = "db-f1-micro"  # Smallest tier
  db_availability_type      = "ZONAL"
  db_disk_size              = 10
  db_max_disk_size          = 50
  db_backup_retention_days  = 3  # Shorter retention
  db_point_in_time_recovery = false
  db_deletion_protection    = false  # Allow easy cleanup

  # VPC connector for database access
  enable_vpc_connector = true
  vpc_network_name     = "default"
  vpc_connector_cidr   = "10.8.0.0/28"

  # No load balancer or custom domain for dev
  create_load_balancer = false

  # CDN enabled but will use Cloud Run URL
  enable_cdn = false

  # No Cloud Armor in dev
  enable_cloud_armor = false

  # Allow public access
  allow_unauthenticated = true

  # Development CORS
  cors_origins = ["http://localhost:3000", "http://localhost:8080"]

  # Storage access for GCS data
  enable_gcs_access = true

  labels = {
    environment = "dev"
    team        = "engineering"
    cost_center = "development"
  }
}

# ==================== Outputs ====================
output "service_url" {
  description = "Cloud Run service URL"
  value       = module.honua_serverless.service_url
}

output "database_connection_name" {
  description = "Database connection name"
  value       = module.honua_serverless.database_connection_name
}

output "estimated_monthly_cost" {
  description = "Estimated monthly cost"
  value       = "$20-40/month for low dev traffic (within free tier limits)"
}

output "next_steps" {
  description = "Next steps after deployment"
  value = <<-EOT
    1. Install PostGIS extension:
       gcloud sql connect ${module.honua_serverless.database_instance_name} --user=honua --database=honua
       CREATE EXTENSION IF NOT EXISTS postgis;

    2. Test the service:
       curl ${module.honua_serverless.service_url}/health

    3. Monitor logs:
       gcloud logging read "resource.type=cloud_run_revision AND resource.labels.service_name=${module.honua_serverless.service_name}" --limit 50
  EOT
}
