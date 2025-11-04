# Outputs for Google Cloud Run Serverless Module

# ==================== Cloud Run Service ====================
output "service_name" {
  description = "Name of the Cloud Run service"
  value       = google_cloud_run_v2_service.honua.name
}

output "service_id" {
  description = "Full service ID"
  value       = google_cloud_run_v2_service.honua.id
}

output "service_url" {
  description = "URL of the Cloud Run service"
  value       = google_cloud_run_v2_service.honua.uri
}

output "service_location" {
  description = "Location where the service is deployed"
  value       = google_cloud_run_v2_service.honua.location
}

output "service_project" {
  description = "GCP project containing the service"
  value       = google_cloud_run_v2_service.honua.project
}

# ==================== Load Balancer ====================
output "load_balancer_ip" {
  description = "Global IP address for the load balancer"
  value       = var.create_load_balancer ? google_compute_global_address.honua[0].address : null
}

output "load_balancer_url" {
  description = "URL for the load balancer"
  value       = var.create_load_balancer && length(var.custom_domains) > 0 ? "https://${var.custom_domains[0]}" : null
}

output "ssl_certificate_id" {
  description = "ID of the managed SSL certificate"
  value       = var.create_load_balancer && length(var.custom_domains) > 0 ? google_compute_managed_ssl_certificate.honua[0].id : null
}

output "backend_service_id" {
  description = "ID of the backend service"
  value       = var.create_load_balancer ? google_compute_backend_service.honua[0].id : null
}

# ==================== Database ====================
output "database_instance_name" {
  description = "Name of the Cloud SQL instance"
  value       = var.create_database ? google_sql_database_instance.honua[0].name : null
}

output "database_connection_name" {
  description = "Connection name for Cloud SQL instance"
  value       = var.create_database ? google_sql_database_instance.honua[0].connection_name : null
}

output "database_private_ip" {
  description = "Private IP address of Cloud SQL instance"
  value       = var.create_database ? google_sql_database_instance.honua[0].private_ip_address : null
  sensitive   = true
}

output "database_name" {
  description = "Name of the database"
  value       = var.create_database ? google_sql_database.honua[0].name : null
}

output "database_username" {
  description = "Database username"
  value       = var.create_database ? google_sql_user.honua[0].name : null
  sensitive   = true
}

# ==================== Secrets ====================
output "jwt_secret_id" {
  description = "Secret Manager secret ID for JWT secret"
  value       = google_secret_manager_secret.jwt_secret.secret_id
}

output "jwt_secret_name" {
  description = "Full name of JWT secret"
  value       = google_secret_manager_secret.jwt_secret.name
}

output "db_connection_secret_id" {
  description = "Secret Manager secret ID for database connection"
  value       = var.create_database ? google_secret_manager_secret.db_connection[0].secret_id : null
}

output "db_connection_secret_name" {
  description = "Full name of database connection secret"
  value       = var.create_database ? google_secret_manager_secret.db_connection[0].name : null
}

# ==================== Service Account ====================
output "service_account_email" {
  description = "Email of the Cloud Run service account"
  value       = google_service_account.cloud_run.email
}

output "service_account_id" {
  description = "ID of the Cloud Run service account"
  value       = google_service_account.cloud_run.id
}

output "service_account_name" {
  description = "Name of the Cloud Run service account"
  value       = google_service_account.cloud_run.name
}

# ==================== VPC Connector ====================
output "vpc_connector_name" {
  description = "Name of the VPC connector"
  value       = var.enable_vpc_connector ? google_vpc_access_connector.honua[0].name : null
}

output "vpc_connector_id" {
  description = "ID of the VPC connector"
  value       = var.enable_vpc_connector ? google_vpc_access_connector.honua[0].id : null
}

# ==================== Security ====================
output "security_policy_id" {
  description = "ID of the Cloud Armor security policy"
  value       = var.enable_cloud_armor ? google_compute_security_policy.honua[0].id : null
}

# ==================== DNS Configuration ====================
output "dns_records_required" {
  description = "DNS records required for custom domains"
  value = var.create_load_balancer && length(var.custom_domains) > 0 ? {
    for domain in var.custom_domains :
    domain => {
      type  = "A"
      name  = domain
      value = google_compute_global_address.honua[0].address
      ttl   = 300
    }
  } : null
}

# ==================== Cost Estimation ====================
output "estimated_monthly_cost" {
  description = "Estimated monthly cost breakdown (USD, approximate)"
  value = {
    cloud_run = {
      description = "Cloud Run service (highly variable based on usage)"
      cpu_memory  = "~$24/vCPU-month, ~$2.50/GB-month"
      requests    = "$0.40 per million requests"
      note        = "First 2M requests free per month"
    }
    database = var.create_database ? {
      description = "Cloud SQL PostgreSQL ${var.db_tier}"
      instance    = var.db_tier == "db-g1-small" ? "~$26/month" : "varies by tier"
      storage     = "~$0.17/GB-month for SSD"
      note        = "High availability doubles cost"
    } : null
    load_balancer = var.create_load_balancer ? {
      description = "Global Load Balancer with SSL"
      forwarding  = "$18/month (per rule)"
      data        = "$0.008-0.12/GB depending on region"
    } : null
    cdn = var.enable_cdn ? {
      description = "Cloud CDN"
      cache_fill  = "$0.02-0.04/GB"
      cache_hit   = "$0.02-0.08/GB depending on region"
      note        = "Reduces origin load and costs"
    } : null
    vpc_connector = var.enable_vpc_connector ? {
      description = "VPC Access Connector"
      cost        = "~$10/month (e2-micro instances)"
    } : null
    secrets = {
      description = "Secret Manager"
      cost        = "$0.06 per 10K accesses (first 6 versions free)"
    }
    total_estimate = var.create_database ? "$80-150/month for low traffic dev/staging" : "$50-100/month for low traffic"
    production_note = "Production with high traffic: $500-2000+/month depending on scale"
  }
}

# ==================== Monitoring URLs ====================
output "monitoring_urls" {
  description = "URLs for monitoring and management"
  value = {
    cloud_run_console = "https://console.cloud.google.com/run/detail/${var.region}/${google_cloud_run_v2_service.honua.name}/metrics?project=${var.project_id}"
    cloud_sql_console = var.create_database ? "https://console.cloud.google.com/sql/instances/${google_sql_database_instance.honua[0].name}/overview?project=${var.project_id}" : null
    load_balancer_console = var.create_load_balancer ? "https://console.cloud.google.com/net-services/loadbalancing/details/http/${google_compute_backend_service.honua[0].name}?project=${var.project_id}" : null
    logs = "https://console.cloud.google.com/logs/query;query=resource.type%3D%22cloud_run_revision%22%0Aresource.labels.service_name%3D%22${google_cloud_run_v2_service.honua.name}%22?project=${var.project_id}"
  }
}

# ==================== Deployment Information ====================
output "deployment_info" {
  description = "Information for deployment and integration"
  value = {
    service_url          = google_cloud_run_v2_service.honua.uri
    health_check_url     = "${google_cloud_run_v2_service.honua.uri}${var.health_check_path}"
    container_image      = var.container_image
    min_instances        = var.min_instances
    max_instances        = var.max_instances
    cdn_enabled          = var.enable_cdn
    cloud_armor_enabled  = var.enable_cloud_armor
    database_enabled     = var.create_database
    environment          = var.environment
  }
}
