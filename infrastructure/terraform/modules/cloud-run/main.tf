# Google Cloud Run Serverless Module for Honua GIS Platform
# Deploys Honua as a fully managed serverless service on Cloud Run

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

# ==================== Local Variables ====================
locals {
  service_name = "${var.service_name}-${var.environment}"

  # Environment variables for the container
  env_vars = merge(
    {
      ASPNETCORE_ENVIRONMENT      = var.aspnetcore_environment
      ASPNETCORE_URLS             = "http://+:8080"
      PORT                        = "8080"
      DOTNET_RUNNING_IN_CONTAINER = "true"
      DOTNET_TieredPGO            = "1"
      DOTNET_ReadyToRun           = "1"
      GDAL_CACHEMAX               = var.gdal_cache_max
      CORS_ORIGINS                = join(",", var.cors_origins)
    },
    var.additional_env_vars
  )

  labels = merge(
    {
      environment = var.environment
      managed_by  = "terraform"
      application = "honua"
      tier        = "serverless"
    },
    var.labels
  )
}

# ==================== VPC Connector for Cloud SQL ====================
# Enables Cloud Run to connect to Cloud SQL privately
resource "google_vpc_access_connector" "honua" {
  count         = var.enable_vpc_connector ? 1 : 0
  name          = "${local.service_name}-vpc-connector"
  region        = var.region
  project       = var.project_id
  network       = var.vpc_network_name
  ip_cidr_range = var.vpc_connector_cidr

  machine_type = var.vpc_connector_machine_type
  min_instances = 2
  max_instances = var.vpc_connector_max_instances
}

# ==================== Cloud SQL PostgreSQL with PostGIS ====================
resource "google_sql_database_instance" "honua" {
  count            = var.create_database ? 1 : 0
  name             = local.service_name
  database_version = var.postgres_version
  region           = var.region
  project          = var.project_id

  settings {
    tier              = var.db_tier
    availability_type = var.db_availability_type
    disk_type         = "PD_SSD"
    disk_size         = var.db_disk_size
    disk_autoresize       = true
    disk_autoresize_limit = var.db_max_disk_size

    backup_configuration {
      enabled                        = true
      start_time                     = "03:00"
      point_in_time_recovery_enabled = var.db_point_in_time_recovery
      transaction_log_retention_days = var.db_backup_retention_days
      backup_retention_settings {
        retained_backups = var.db_backup_retention_days
        retention_unit   = "COUNT"
      }
    }

    ip_configuration {
      ipv4_enabled                                  = false
      private_network                               = var.vpc_network_id
      enable_private_path_for_google_cloud_services = true
      require_ssl                                   = true
    }

    database_flags {
      name  = "max_connections"
      value = var.db_max_connections
    }

    database_flags {
      name  = "shared_preload_libraries"
      value = "pg_stat_statements"
    }

    database_flags {
      name  = "cloudsql.enable_pg_stat_statements"
      value = "on"
    }

    maintenance_window {
      day          = 1
      hour         = 4
      update_track = "stable"
    }

    insights_config {
      query_insights_enabled  = true
      query_plans_per_minute  = 5
      query_string_length     = 1024
      record_application_tags = true
    }

    user_labels = local.labels
  }

  deletion_protection = var.db_deletion_protection
}

# Create PostGIS-enabled database
resource "google_sql_database" "honua" {
  count    = var.create_database ? 1 : 0
  name     = var.database_name
  instance = google_sql_database_instance.honua[0].name
  project  = var.project_id
}

# Generate secure database password
resource "random_password" "db_password" {
  count   = var.create_database ? 1 : 0
  length  = 32
  special = true
}

# Create database user
resource "google_sql_user" "honua" {
  count    = var.create_database ? 1 : 0
  name     = var.db_username
  instance = google_sql_database_instance.honua[0].name
  password = random_password.db_password[0].result
  project  = var.project_id
}

# ==================== Secret Manager for Credentials ====================
# JWT Secret
resource "random_password" "jwt_secret" {
  length  = 64
  special = true
}

resource "google_secret_manager_secret" "jwt_secret" {
  secret_id = "${local.service_name}-jwt-secret"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = local.labels
}

resource "google_secret_manager_secret_version" "jwt_secret" {
  secret      = google_secret_manager_secret.jwt_secret.id
  secret_data = random_password.jwt_secret.result
}

# Database Connection String
resource "google_secret_manager_secret" "db_connection" {
  count     = var.create_database ? 1 : 0
  secret_id = "${local.service_name}-db-connection"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = local.labels
}

resource "google_secret_manager_secret_version" "db_connection" {
  count  = var.create_database ? 1 : 0
  secret = google_secret_manager_secret.db_connection[0].id
  secret_data = "Host=/cloudsql/${google_sql_database_instance.honua[0].connection_name};Database=${var.database_name};Username=${var.db_username};Password=${random_password.db_password[0].result}"
}

# ==================== Service Account for Cloud Run ====================
resource "google_service_account" "cloud_run" {
  account_id   = "${local.service_name}-sa"
  display_name = "Service Account for ${local.service_name}"
  project      = var.project_id
}

# Grant Cloud SQL Client role
resource "google_project_iam_member" "cloud_sql_client" {
  count   = var.create_database ? 1 : 0
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${google_service_account.cloud_run.email}"
}

# Grant Secret Manager access
resource "google_secret_manager_secret_iam_member" "jwt_secret_access" {
  secret_id = google_secret_manager_secret.jwt_secret.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.cloud_run.email}"
}

resource "google_secret_manager_secret_iam_member" "db_connection_access" {
  count     = var.create_database ? 1 : 0
  secret_id = google_secret_manager_secret.db_connection[0].id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.cloud_run.email}"
}

# Grant Storage access for GCS raster data
resource "google_project_iam_member" "storage_viewer" {
  count   = var.enable_gcs_access ? 1 : 0
  project = var.project_id
  role    = "roles/storage.objectViewer"
  member  = "serviceAccount:${google_service_account.cloud_run.email}"
}

# ==================== Cloud Run Service ====================
resource "google_cloud_run_v2_service" "honua" {
  name     = local.service_name
  location = var.region
  project  = var.project_id

  labels = local.labels

  template {
    service_account = google_service_account.cloud_run.email

    # Scaling configuration
    scaling {
      min_instance_count = var.min_instances
      max_instance_count = var.max_instances
    }

    # VPC connector for private Cloud SQL access
    dynamic "vpc_access" {
      for_each = var.enable_vpc_connector ? [1] : []
      content {
        connector = google_vpc_access_connector.honua[0].id
        egress    = "PRIVATE_RANGES_ONLY"
      }
    }

    # Container configuration
    containers {
      image = var.container_image

      # Resource limits
      resources {
        limits = {
          cpu    = var.cpu_limit
          memory = var.memory_limit
        }
        cpu_idle          = var.cpu_always_allocated
        startup_cpu_boost = true
      }

      # Health check
      startup_probe {
        http_get {
          path = var.health_check_path
          port = 8080
        }
        initial_delay_seconds = 10
        timeout_seconds       = 3
        period_seconds        = 10
        failure_threshold     = 3
      }

      liveness_probe {
        http_get {
          path = var.health_check_path
          port = 8080
        }
        initial_delay_seconds = 30
        timeout_seconds       = 3
        period_seconds        = 30
        failure_threshold     = 3
      }

      # Environment variables
      dynamic "env" {
        for_each = local.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      # JWT Secret from Secret Manager
      env {
        name = "JWT_SECRET"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.jwt_secret.secret_id
            version = "latest"
          }
        }
      }

      # Database Connection from Secret Manager
      dynamic "env" {
        for_each = var.create_database ? [1] : []
        content {
          name = "DATABASE_CONNECTION_STRING"
          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.db_connection[0].secret_id
              version = "latest"
            }
          }
        }
      }

      # Cloud SQL connection
      dynamic "env" {
        for_each = var.create_database ? [1] : []
        content {
          name  = "INSTANCE_CONNECTION_NAME"
          value = google_sql_database_instance.honua[0].connection_name
        }
      }
    }

    # Timeout for long-running GIS queries
    timeout = "${var.request_timeout}s"

    # Container concurrency
    max_instance_request_concurrency = var.max_concurrent_requests
  }

  traffic {
    type    = "TRAFFIC_TARGET_ALLOCATION_TYPE_LATEST"
    percent = 100
  }

  depends_on = [
    google_project_iam_member.cloud_sql_client,
    google_secret_manager_secret_iam_member.jwt_secret_access,
    google_secret_manager_secret_iam_member.db_connection_access
  ]
}

# ==================== IAM Policy for Public/Private Access ====================
resource "google_cloud_run_service_iam_member" "public_access" {
  count    = var.allow_unauthenticated ? 1 : 0
  service  = google_cloud_run_v2_service.honua.name
  location = google_cloud_run_v2_service.honua.location
  project  = var.project_id
  role     = "roles/run.invoker"
  member   = "allUsers"
}

# Grant specific users/groups access
resource "google_cloud_run_service_iam_member" "authorized_access" {
  for_each = toset(var.authorized_members)
  service  = google_cloud_run_v2_service.honua.name
  location = google_cloud_run_v2_service.honua.location
  project  = var.project_id
  role     = "roles/run.invoker"
  member   = each.value
}

# ==================== Load Balancer with SSL ====================
# Global IP address
resource "google_compute_global_address" "honua" {
  count   = var.create_load_balancer ? 1 : 0
  name    = "${local.service_name}-ip"
  project = var.project_id
}

# Cloud Run NEG (Network Endpoint Group)
resource "google_compute_region_network_endpoint_group" "honua" {
  count                 = var.create_load_balancer ? 1 : 0
  name                  = "${local.service_name}-neg"
  network_endpoint_type = "SERVERLESS"
  region                = var.region
  project               = var.project_id

  cloud_run {
    service = google_cloud_run_v2_service.honua.name
  }
}

# Backend service
resource "google_compute_backend_service" "honua" {
  count                 = var.create_load_balancer ? 1 : 0
  name                  = "${local.service_name}-backend"
  project               = var.project_id
  protocol              = "HTTPS"
  port_name             = "http"
  timeout_sec           = var.request_timeout
  enable_cdn            = var.enable_cdn
  compression_mode      = "AUTOMATIC"

  backend {
    group = google_compute_region_network_endpoint_group.honua[0].id
  }

  # CDN configuration for GIS tiles
  dynamic "cdn_policy" {
    for_each = var.enable_cdn ? [1] : []
    content {
      cache_mode                   = "CACHE_ALL_STATIC"
      default_ttl                  = var.cdn_default_ttl
      max_ttl                      = var.cdn_max_ttl
      client_ttl                   = var.cdn_client_ttl
      negative_caching             = true
      serve_while_stale            = 86400

      cache_key_policy {
        include_host           = true
        include_protocol       = true
        include_query_string   = true
        query_string_whitelist = ["bbox", "width", "height", "layers", "srs", "crs", "format"]
      }
    }
  }

  log_config {
    enable      = true
    sample_rate = var.lb_log_sample_rate
  }
}

# URL Map
resource "google_compute_url_map" "honua" {
  count           = var.create_load_balancer ? 1 : 0
  name            = "${local.service_name}-urlmap"
  project         = var.project_id
  default_service = google_compute_backend_service.honua[0].id
}

# Managed SSL Certificate
resource "google_compute_managed_ssl_certificate" "honua" {
  count   = var.create_load_balancer && length(var.custom_domains) > 0 ? 1 : 0
  name    = "${local.service_name}-cert"
  project = var.project_id

  managed {
    domains = var.custom_domains
  }
}

# HTTPS Proxy
resource "google_compute_target_https_proxy" "honua" {
  count            = var.create_load_balancer && length(var.custom_domains) > 0 ? 1 : 0
  name             = "${local.service_name}-https-proxy"
  project          = var.project_id
  url_map          = google_compute_url_map.honua[0].id
  ssl_certificates = [google_compute_managed_ssl_certificate.honua[0].id]
}

# HTTP Proxy (for redirect)
resource "google_compute_target_http_proxy" "honua" {
  count   = var.create_load_balancer ? 1 : 0
  name    = "${local.service_name}-http-proxy"
  project = var.project_id
  url_map = google_compute_url_map.honua[0].id
}

# Forwarding rules
resource "google_compute_global_forwarding_rule" "https" {
  count                 = var.create_load_balancer && length(var.custom_domains) > 0 ? 1 : 0
  name                  = "${local.service_name}-https-rule"
  project               = var.project_id
  ip_address            = google_compute_global_address.honua[0].address
  ip_protocol           = "TCP"
  load_balancing_scheme = "EXTERNAL_MANAGED"
  port_range            = "443"
  target                = google_compute_target_https_proxy.honua[0].id
}

resource "google_compute_global_forwarding_rule" "http" {
  count                 = var.create_load_balancer ? 1 : 0
  name                  = "${local.service_name}-http-rule"
  project               = var.project_id
  ip_address            = google_compute_global_address.honua[0].address
  ip_protocol           = "TCP"
  load_balancing_scheme = "EXTERNAL_MANAGED"
  port_range            = "80"
  target                = google_compute_target_http_proxy.honua[0].id
}

# ==================== Cloud Armor Security Policy ====================
resource "google_compute_security_policy" "honua" {
  count   = var.enable_cloud_armor ? 1 : 0
  name    = "${local.service_name}-policy"
  project = var.project_id

  # Default rule - allow all
  rule {
    action   = "allow"
    priority = 2147483647
    match {
      versioned_expr = "SRC_IPS_V1"
      config {
        src_ip_ranges = ["*"]
      }
    }
    description = "Default rule"
  }

  # Rate limiting for DDoS protection
  dynamic "rule" {
    for_each = var.rate_limit_threshold > 0 ? [1] : []
    content {
      action   = "rate_based_ban"
      priority = 1000
      match {
        versioned_expr = "SRC_IPS_V1"
        config {
          src_ip_ranges = ["*"]
        }
      }
      rate_limit_options {
        conform_action = "allow"
        exceed_action  = "deny(429)"
        enforce_on_key = "IP"
        ban_duration_sec = 600
        rate_limit_threshold {
          count        = var.rate_limit_threshold
          interval_sec = 60
        }
      }
      description = "Rate limit rule"
    }
  }

  # Block known bad IPs
  dynamic "rule" {
    for_each = var.blocked_ip_ranges
    content {
      action   = "deny(403)"
      priority = 500 + rule.key
      match {
        versioned_expr = "SRC_IPS_V1"
        config {
          src_ip_ranges = [rule.value]
        }
      }
      description = "Block IP range ${rule.value}"
    }
  }

  adaptive_protection_config {
    layer_7_ddos_defense_config {
      enable = var.enable_adaptive_protection
    }
  }
}

# Attach security policy to backend service
resource "google_compute_backend_service_iam_binding" "security_policy" {
  count   = var.create_load_balancer && var.enable_cloud_armor ? 1 : 0
  project = var.project_id
  backend_service = google_compute_backend_service.honua[0].name

  role = "roles/compute.securityAdmin"
  members = [
    "serviceAccount:${google_service_account.cloud_run.email}"
  ]
}
