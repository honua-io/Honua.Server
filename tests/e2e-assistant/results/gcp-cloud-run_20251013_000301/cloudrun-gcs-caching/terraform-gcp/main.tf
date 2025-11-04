terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

variable "project_id" {
  description = "GCP project ID"
  type        = string
}

variable "region" {
  description = "GCP region"
  default     = "us-central1"
}

variable "zone" {
  description = "GCP zone"
  default     = "us-central1-a"
}

variable "environment" {
  description = "Environment name"
  default     = "development"
}

# Enable required APIs
resource "google_project_service" "cloud_run" {
  project = var.project_id
  service = "run.googleapis.com"
}

resource "google_project_service" "sql_admin" {
  project = var.project_id
  service = "sqladmin.googleapis.com"
}

resource "google_project_service" "redis" {
  project = var.project_id
  service = "redis.googleapis.com"
}

# Cloud Run Service for Honua Server
resource "google_cloud_run_service" "honua" {
  name     = "honua-server-${var.environment}"
  location = var.region

  template {
    spec {
      containers {
        image = "honuaio/honua-server:latest"

        resources {
          limits = {
            cpu    = "1000m"
            memory = "2048Mi"
          }
        }

        env {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment
        }
      }
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = "10"
        "autoscaling.knative.dev/minScale" = "1"
      }
    }
  }

  traffic {
    percent         = 100
    latest_revision = true
  }

  depends_on = [
    google_project_service.cloud_run
  ]
}

# IAM Policy to allow public access
resource "google_cloud_run_service_iam_member" "public_access" {
  service  = google_cloud_run_service.honua.name
  location = google_cloud_run_service.honua.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}

# Storage Bucket for tile caching and static assets
resource "google_storage_bucket" "honua_cache" {
  name     = "honua-cache-${var.project_id}-${var.environment}"
  location = var.region

  uniform_bucket_level_access = true

  lifecycle_rule {
    condition {
      age = 30
    }
    action {
      type = "Delete"
    }
  }

  lifecycle_rule {
    condition {
      age = 90
    }
    action {
      type          = "SetStorageClass"
      storage_class = "NEARLINE"
    }
  }

  labels = {
    environment = var.environment
    application = "honua"
  }
}

# Service Account for Honua with least privilege
resource "google_service_account" "honua" {
  account_id   = "honua-${var.environment}"
  display_name = "Honua GIS Server Service Account"
  description  = "Service account for Honua with least-privilege access"
}

# IAM binding for Cloud Run to use service account
resource "google_service_account_iam_member" "run_sa_user" {
  service_account_id = google_service_account.honua.name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${google_service_account.honua.email}"
}

# Grant service account access to Cloud SQL
resource "google_project_iam_member" "cloudsql_client" {
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${google_service_account.honua.email}"
}

# Grant service account access to Storage
resource "google_project_iam_member" "storage_object_admin" {
  project = var.project_id
  role    = "roles/storage.objectAdmin"
  member  = "serviceAccount:${google_service_account.honua.email}"
}

# Cloud Logging
resource "google_logging_project_sink" "honua_logs" {
  name        = "honua-logs-${var.environment}"
  destination = "logging.googleapis.com/projects/${var.project_id}/locations/${var.region}/buckets/honua-logs"

  filter = "resource.type=cloud_run_revision AND resource.labels.service_name=~\\"honua-.*\\""

  unique_writer_identity = true
}

# Monitoring Alert Policy for high error rate
resource "google_monitoring_alert_policy" "high_error_rate" {
  display_name = "Honua High Error Rate - ${var.environment}"
  combiner     = "OR"

  conditions {
    display_name = "Error rate above 5%"

    condition_threshold {
      filter          = "resource.type=\\"cloud_run_revision\\" AND resource.labels.service_name=~\\"honua-.*\\" AND metric.type=\\"run.googleapis.com/request_count\\" AND metric.labels.response_code_class=\\"5xx\\""
      duration        = "300s"
      comparison      = "COMPARISON_GT"
      threshold_value = 0.05

      aggregations {
        alignment_period   = "60s"
        per_series_aligner = "ALIGN_RATE"
      }
    }
  }

  notification_channels = []

  alert_strategy {
    auto_close = "1800s"
  }
}

# VPC Access Connector for private VPC connectivity
resource "google_vpc_access_connector" "honua" {
  name          = "honua-vpc-connector-${var.environment}"
  region        = var.region
  network       = google_compute_network.vpc.name
  ip_cidr_range = "10.8.0.0/28"

  min_throughput = 200
  max_throughput = 300
}

# Load Balancer components for HA deployment
resource "google_compute_global_address" "honua_lb" {
  name = "honua-lb-ip-${var.environment}"
}

resource "google_compute_backend_service" "honua" {
  name          = "honua-backend-${var.environment}"
  protocol      = "HTTP"
  port_name     = "http"
  timeout_sec   = 30
  health_checks = [google_compute_health_check.honua.id]

  backend {
    group = google_compute_region_network_endpoint_group.honua.id
  }

  log_config {
    enable      = true
    sample_rate = 1.0
  }
}

resource "google_compute_region_network_endpoint_group" "honua" {
  name                  = "honua-neg-${var.environment}"
  network_endpoint_type = "SERVERLESS"
  region                = var.region

  cloud_run {
    service = google_cloud_run_service.honua.name
  }
}

resource "google_compute_health_check" "honua" {
  name = "honua-health-check-${var.environment}"

  http_health_check {
    port         = 8080
    request_path = "/health"
  }

  check_interval_sec  = 30
  timeout_sec         = 10
  healthy_threshold   = 2
  unhealthy_threshold = 3
}

resource "google_compute_url_map" "honua" {
  name            = "honua-url-map-${var.environment}"
  default_service = google_compute_backend_service.honua.id
}

resource "google_compute_target_http_proxy" "honua" {
  name    = "honua-http-proxy-${var.environment}"
  url_map = google_compute_url_map.honua.id
}

resource "google_compute_global_forwarding_rule" "honua" {
  name       = "honua-forwarding-rule-${var.environment}"
  target     = google_compute_target_http_proxy.honua.id
  port_range = "80"
  ip_address = google_compute_global_address.honua_lb.address
}


# Outputs
output "honua_server_url" {
  value = google_cloud_run_service.honua.status[0].url
}