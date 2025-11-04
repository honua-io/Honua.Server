// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Linq;
using System.Text;

namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Helper class containing GCP Terraform generation logic.
/// </summary>
internal static class TerraformGcpContent
{
    public static string GenerateCloudRun(DeploymentAnalysis analysis)
    {
        var hasDatabase = analysis.InfrastructureNeeds.NeedsDatabase ||
                         analysis.RequiredServices.Any(s => s.Contains("postgis", StringComparison.OrdinalIgnoreCase));
        var hasCache = analysis.InfrastructureNeeds.NeedsCache ||
                      analysis.RequiredServices.Any(s => s.Contains("redis", StringComparison.OrdinalIgnoreCase));

        var tf = new System.Text.StringBuilder();
        tf.AppendLine(@"terraform {
  required_providers {
    google = {
      source  = ""hashicorp/google""
      version = ""~> 5.0""
    }
  }
}

provider ""google"" {
  project = var.project_id
  region  = var.region
}

variable ""project_id"" {
  description = ""GCP project ID""
  type        = string
}

variable ""region"" {
  description = ""GCP region""
  default     = ""us-central1""
}

variable ""zone"" {
  description = ""GCP zone""
  default     = ""us-central1-a""
}

variable ""environment"" {
  description = ""Environment name""
  default     = """ + analysis.TargetEnvironment + @"""
}

# Enable required APIs
resource ""google_project_service"" ""cloud_run"" {
  project = var.project_id
  service = ""run.googleapis.com""
}

resource ""google_project_service"" ""sql_admin"" {
  project = var.project_id
  service = ""sqladmin.googleapis.com""
}

resource ""google_project_service"" ""redis"" {
  project = var.project_id
  service = ""redis.googleapis.com""
}

# Cloud Run Service for Honua Server
resource ""google_cloud_run_service"" ""honua"" {
  name     = ""honua-server-${var.environment}""
  location = var.region

  template {
    spec {
      containers {
        image = ""honuaio/honua-server:latest""

        resources {
          limits = {
            cpu    = ""1000m""
            memory = ""2048Mi""
          }
        }

        env {
          name  = ""ASPNETCORE_ENVIRONMENT""
          value = var.environment
        }");

        if (hasDatabase)
        {
            tf.AppendLine(@"
        env {
          name  = ""HONUA__DATABASE__HOST""
          value = google_sql_database_instance.postgis.private_ip_address
        }

        env {
          name  = ""HONUA__DATABASE__PORT""
          value = ""5432""
        }

        env {
          name  = ""HONUA__DATABASE__DATABASE""
          value = google_sql_database.honua.name
        }

        env {
          name  = ""HONUA__DATABASE__USERNAME""
          value = ""postgres""
        }

        env {
          name  = ""HONUA__DATABASE__PASSWORD""
          value = random_password.db_password.result
        }");
        }

        if (hasCache)
        {
            tf.AppendLine(@"
        env {
          name  = ""HONUA__CACHE__PROVIDER""
          value = ""redis""
        }

        env {
          name  = ""HONUA__CACHE__REDIS__HOST""
          value = google_redis_instance.cache.host
        }

        env {
          name  = ""HONUA__CACHE__REDIS__PORT""
          value = ""6379""
        }");
        }

        tf.AppendLine(@"      }
    }

    metadata {
      annotations = {
        ""autoscaling.knative.dev/maxScale"" = ""10""
        ""autoscaling.knative.dev/minScale"" = ""1""
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
resource ""google_cloud_run_service_iam_member"" ""public_access"" {
  service  = google_cloud_run_service.honua.name
  location = google_cloud_run_service.honua.location
  role     = ""roles/run.invoker""
  member   = ""allUsers""
}");

        if (hasDatabase)
        {
            tf.AppendLine(@"
# Cloud SQL PostgreSQL Instance
resource ""google_sql_database_instance"" ""postgis"" {
  name             = ""honua-postgis-${var.environment}""
  database_version = ""POSTGRES_15""
  region           = var.region

  settings {
    tier              = ""db-f1-micro""
    availability_type = var.environment == ""production"" ? ""REGIONAL"" : ""ZONAL""

    backup_configuration {
      enabled                        = true
      start_time                     = ""03:00""
      point_in_time_recovery_enabled = var.environment == ""production""
    }

    database_flags {
      name  = ""max_connections""
      value = ""100""
    }

    ip_configuration {
      ipv4_enabled    = true
      private_network = google_compute_network.vpc.id

      authorized_networks {
        name  = ""allow-all""
        value = ""0.0.0.0/0""
      }
    }
  }

  deletion_protection = var.environment == ""production""

  depends_on = [
    google_project_service.sql_admin,
    google_service_networking_connection.private_vpc_connection
  ]
}

resource ""google_sql_database"" ""honua"" {
  name     = ""honua""
  instance = google_sql_database_instance.postgis.name
}

resource ""google_sql_user"" ""postgres"" {
  name     = ""postgres""
  instance = google_sql_database_instance.postgis.name
  password = random_password.db_password.result
}

resource ""random_password"" ""db_password"" {
  length  = 32
  special = true
}

# VPC for private database access
resource ""google_compute_network"" ""vpc"" {
  name                    = ""honua-vpc-${var.environment}""
  auto_create_subnetworks = false
}

resource ""google_compute_global_address"" ""private_ip"" {
  name          = ""honua-private-ip-${var.environment}""
  purpose       = ""VPC_PEERING""
  address_type  = ""INTERNAL""
  prefix_length = 16
  network       = google_compute_network.vpc.id
}

resource ""google_service_networking_connection"" ""private_vpc_connection"" {
  network                 = google_compute_network.vpc.id
  service                 = ""servicenetworking.googleapis.com""
  reserved_peering_ranges = [google_compute_global_address.private_ip.name]
}");
        }

        if (hasCache)
        {
            tf.AppendLine(@"
# Cloud Memorystore Redis Instance
resource ""google_redis_instance"" ""cache"" {
  name           = ""honua-redis-${var.environment}""
  tier           = ""BASIC""
  memory_size_gb = 1
  region         = var.region

  redis_version = ""REDIS_7_0""
  display_name  = ""Honua Redis Cache""

  labels = {
    environment = var.environment
    application = ""honua""
  }

  depends_on = [
    google_project_service.redis
  ]
}");
        }

        tf.AppendLine(@"
# Storage Bucket for tile caching and static assets
resource ""google_storage_bucket"" ""honua_cache"" {
  name     = ""honua-cache-${var.project_id}-${var.environment}""
  location = var.region

  uniform_bucket_level_access = true

  lifecycle_rule {
    condition {
      age = 30
    }
    action {
      type = ""Delete""
    }
  }

  lifecycle_rule {
    condition {
      age = 90
    }
    action {
      type          = ""SetStorageClass""
      storage_class = ""NEARLINE""
    }
  }

  labels = {
    environment = var.environment
    application = ""honua""
  }
}

# Service Account for Honua with least privilege
resource ""google_service_account"" ""honua"" {
  account_id   = ""honua-${var.environment}""
  display_name = ""Honua GIS Server Service Account""
  description  = ""Service account for Honua with least-privilege access""
}

# IAM binding for Cloud Run to use service account
resource ""google_service_account_iam_member"" ""run_sa_user"" {
  service_account_id = google_service_account.honua.name
  role               = ""roles/iam.serviceAccountUser""
  member             = ""serviceAccount:${google_service_account.honua.email}""
}

# Grant service account access to Cloud SQL
resource ""google_project_iam_member"" ""cloudsql_client"" {
  project = var.project_id
  role    = ""roles/cloudsql.client""
  member  = ""serviceAccount:${google_service_account.honua.email}""
}

# Grant service account access to Storage
resource ""google_project_iam_member"" ""storage_object_admin"" {
  project = var.project_id
  role    = ""roles/storage.objectAdmin""
  member  = ""serviceAccount:${google_service_account.honua.email}""
}

# Cloud Logging
resource ""google_logging_project_sink"" ""honua_logs"" {
  name        = ""honua-logs-${var.environment}""
  destination = ""logging.googleapis.com/projects/${var.project_id}/locations/${var.region}/buckets/honua-logs""

  filter = ""resource.type=cloud_run_revision AND resource.labels.service_name=~\\""honua-.*\\""""

  unique_writer_identity = true
}

# Monitoring Alert Policy for high error rate
resource ""google_monitoring_alert_policy"" ""high_error_rate"" {
  display_name = ""Honua High Error Rate - ${var.environment}""
  combiner     = ""OR""

  conditions {
    display_name = ""Error rate above 5%""

    condition_threshold {
      filter          = ""resource.type=\\""cloud_run_revision\\"" AND resource.labels.service_name=~\\""honua-.*\\"" AND metric.type=\\""run.googleapis.com/request_count\\"" AND metric.labels.response_code_class=\\""5xx\\""""
      duration        = ""300s""
      comparison      = ""COMPARISON_GT""
      threshold_value = 0.05

      aggregations {
        alignment_period   = ""60s""
        per_series_aligner = ""ALIGN_RATE""
      }
    }
  }

  notification_channels = []

  alert_strategy {
    auto_close = ""1800s""
  }
}

# VPC Access Connector for private VPC connectivity
resource ""google_vpc_access_connector"" ""honua"" {
  name          = ""honua-vpc-connector-${var.environment}""
  region        = var.region
  network       = google_compute_network.vpc.name
  ip_cidr_range = ""10.8.0.0/28""

  min_throughput = 200
  max_throughput = 300
}

# Load Balancer components for HA deployment
resource ""google_compute_global_address"" ""honua_lb"" {
  name = ""honua-lb-ip-${var.environment}""
}

resource ""google_compute_backend_service"" ""honua"" {
  name          = ""honua-backend-${var.environment}""
  protocol      = ""HTTP""
  port_name     = ""http""
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

resource ""google_compute_region_network_endpoint_group"" ""honua"" {
  name                  = ""honua-neg-${var.environment}""
  network_endpoint_type = ""SERVERLESS""
  region                = var.region

  cloud_run {
    service = google_cloud_run_service.honua.name
  }
}

resource ""google_compute_health_check"" ""honua"" {
  name = ""honua-health-check-${var.environment}""

  http_health_check {
    port         = 8080
    request_path = ""/health""
  }

  check_interval_sec  = 30
  timeout_sec         = 10
  healthy_threshold   = 2
  unhealthy_threshold = 3
}

resource ""google_compute_url_map"" ""honua"" {
  name            = ""honua-url-map-${var.environment}""
  default_service = google_compute_backend_service.honua.id
}

resource ""google_compute_target_http_proxy"" ""honua"" {
  name    = ""honua-http-proxy-${var.environment}""
  url_map = google_compute_url_map.honua.id
}

resource ""google_compute_global_forwarding_rule"" ""honua"" {
  name       = ""honua-forwarding-rule-${var.environment}""
  target     = google_compute_target_http_proxy.honua.id
  port_range = ""80""
  ip_address = google_compute_global_address.honua_lb.address
}
");

        tf.AppendLine(@"
# Outputs
output ""honua_server_url"" {
  value = google_cloud_run_service.honua.status[0].url
}");

        if (hasDatabase)
        {
            tf.AppendLine(@"
output ""database_ip"" {
  value = google_sql_database_instance.postgis.private_ip_address
}

output ""database_password"" {
  value     = random_password.db_password.result
  sensitive = true
}");
        }

        if (hasCache)
        {
            tf.AppendLine(@"
output ""redis_host"" {
  value = google_redis_instance.cache.host
}");
        }

        return tf.ToString();
    }


    public static string GenerateCloudFunctions(DeploymentAnalysis analysis)
    {
        var tf = new System.Text.StringBuilder();

        tf.AppendLine(@"terraform {
  required_providers {
    google = {
      source  = ""hashicorp/google""
      version = ""~> 5.0""
    }
  }
}

provider ""google"" {
  project = var.project_id
  region  = var.region
}

variable ""project_id"" {
  description = ""GCP project ID""
  type        = string
}

variable ""region"" {
  description = ""GCP region""
  default     = ""us-central1""
}

variable ""environment"" {
  description = ""Environment name""
  default     = """ + analysis.TargetEnvironment + @"""
}

# Enable required APIs
resource ""google_project_service"" ""cloud_functions"" {
  project = var.project_id
  service = ""cloudfunctions.googleapis.com""
}

resource ""google_project_service"" ""cloud_build"" {
  project = var.project_id
  service = ""cloudbuild.googleapis.com""
}

resource ""google_project_service"" ""firestore"" {
  project = var.project_id
  service = ""firestore.googleapis.com""
}

resource ""google_project_service"" ""storage"" {
  project = var.project_id
  service = ""storage.googleapis.com""
}

# Service Account for Cloud Functions
resource ""google_service_account"" ""honua_function"" {
  account_id   = ""honua-function-${var.environment}""
  display_name = ""Honua Cloud Functions Service Account""
  description  = ""Service account for Honua Cloud Functions with least-privilege access""
}

# Grant service account access to Firestore
resource ""google_project_iam_member"" ""firestore_user"" {
  project = var.project_id
  role    = ""roles/datastore.user""
  member  = ""serviceAccount:${google_service_account.honua_function.email}""
}

# Grant service account access to Storage
resource ""google_project_iam_member"" ""storage_object_admin"" {
  project = var.project_id
  role    = ""roles/storage.objectAdmin""
  member  = ""serviceAccount:${google_service_account.honua_function.email}""
}

# Grant service account access to Cloud Logging
resource ""google_project_iam_member"" ""log_writer"" {
  project = var.project_id
  role    = ""roles/logging.logWriter""
  member  = ""serviceAccount:${google_service_account.honua_function.email}""
}

# Grant service account access to Cloud Monitoring
resource ""google_project_iam_member"" ""monitoring_metric_writer"" {
  project = var.project_id
  role    = ""roles/monitoring.metricWriter""
  member  = ""serviceAccount:${google_service_account.honua_function.email}""
}

# Storage Bucket for Cloud Function source code
resource ""google_storage_bucket"" ""function_source"" {
  name     = ""honua-function-source-${var.project_id}-${var.environment}""
  location = var.region

  uniform_bucket_level_access = true

  labels = {
    environment = var.environment
    application = ""honua""
  }
}

# Storage Bucket for tile caching
resource ""google_storage_bucket"" ""tiles"" {
  name     = ""honua-tiles-${var.project_id}-${var.environment}""
  location = var.region

  uniform_bucket_level_access = true

  lifecycle_rule {
    condition {
      age = 30
    }
    action {
      type = ""Delete""
    }
  }

  lifecycle_rule {
    condition {
      age = 90
    }
    action {
      type          = ""SetStorageClass""
      storage_class = ""NEARLINE""
    }
  }

  labels = {
    environment = var.environment
    application = ""honua""
  }
}

# Firestore database for data persistence
resource ""google_firestore_database"" ""honua"" {
  project     = var.project_id
  name        = ""(default)""
  location_id = var.region
  type        = ""FIRESTORE_NATIVE""

  depends_on = [
    google_project_service.firestore
  ]
}

# Cloud Function (2nd gen) with container support
resource ""google_cloudfunctions2_function"" ""honua"" {
  name        = ""honua-server-${var.environment}""
  location    = var.region
  description = ""Honua GIS Server running on Cloud Functions""

  build_config {
    runtime     = ""dotnet8""
    entry_point = ""HonuaServer""

    source {
      storage_source {
        bucket = google_storage_bucket.function_source.name
        object = ""honua-server-latest.zip""
      }
    }
  }

  service_config {
    max_instance_count    = 10
    min_instance_count    = 1
    available_memory      = ""2048Mi""
    timeout_seconds       = 60
    service_account_email = google_service_account.honua_function.email

    environment_variables = {
      ASPNETCORE_ENVIRONMENT = var.environment
      HONUA__STORAGE__PROVIDER = ""gcs""
      HONUA__STORAGE__GCS__BUCKET = google_storage_bucket.tiles.name
      HONUA__STORAGE__GCS__PROJECT = var.project_id
      HONUA__DATABASE__PROVIDER = ""firestore""
      HONUA__DATABASE__FIRESTORE__PROJECT = var.project_id
      HONUA__DATABASE__FIRESTORE__DATABASE = google_firestore_database.honua.name
    }

    ingress_settings = ""ALLOW_ALL""
  }

  depends_on = [
    google_project_service.cloud_functions,
    google_project_service.cloud_build
  ]
}

# IAM binding to allow public invocation
resource ""google_cloudfunctions2_function_iam_member"" ""public_access"" {
  project        = google_cloudfunctions2_function.honua.project
  location       = google_cloudfunctions2_function.honua.location
  cloud_function = google_cloudfunctions2_function.honua.name
  role           = ""roles/cloudfunctions.invoker""
  member         = ""allUsers""
}

# Cloud Logging
resource ""google_logging_project_sink"" ""honua_logs"" {
  name        = ""honua-function-logs-${var.environment}""
  destination = ""logging.googleapis.com/projects/${var.project_id}/locations/${var.region}/buckets/honua-function-logs""

  filter = ""resource.type=cloud_function AND resource.labels.function_name=~\\""honua-.*\\""""

  unique_writer_identity = true
}

# Monitoring Alert Policy for high error rate
resource ""google_monitoring_alert_policy"" ""high_error_rate"" {
  display_name = ""Honua Cloud Functions High Error Rate - ${var.environment}""
  combiner     = ""OR""

  conditions {
    display_name = ""Error rate above 5%""

    condition_threshold {
      filter          = ""resource.type=\\""cloud_function\\"" AND resource.labels.function_name=~\\""honua-.*\\"" AND metric.type=\\""cloudfunctions.googleapis.com/function/execution_count\\"" AND metric.labels.status!=\\""ok\\""""
      duration        = ""300s""
      comparison      = ""COMPARISON_GT""
      threshold_value = 0.05

      aggregations {
        alignment_period   = ""60s""
        per_series_aligner = ""ALIGN_RATE""
      }
    }
  }

  notification_channels = []

  alert_strategy {
    auto_close = ""1800s""
  }
}

# Monitoring Alert Policy for high latency
resource ""google_monitoring_alert_policy"" ""high_latency"" {
  display_name = ""Honua Cloud Functions High Latency - ${var.environment}""
  combiner     = ""OR""

  conditions {
    display_name = ""Latency above 1 second""

    condition_threshold {
      filter          = ""resource.type=\\""cloud_function\\"" AND resource.labels.function_name=~\\""honua-.*\\"" AND metric.type=\\""cloudfunctions.googleapis.com/function/execution_times\\""""
      duration        = ""300s""
      comparison      = ""COMPARISON_GT""
      threshold_value = 1000

      aggregations {
        alignment_period     = ""60s""
        per_series_aligner   = ""ALIGN_DELTA""
        cross_series_reducer = ""REDUCE_PERCENTILE_95""
      }
    }
  }

  notification_channels = []

  alert_strategy {
    auto_close = ""1800s""
  }
}

# Outputs
output ""honua_function_url"" {
  value       = google_cloudfunctions2_function.honua.service_config[0].uri
  description = ""URL of the Honua Cloud Function""
}

output ""tiles_bucket"" {
  value       = google_storage_bucket.tiles.name
  description = ""Name of the Cloud Storage bucket for tiles""
}

output ""firestore_database"" {
  value       = google_firestore_database.honua.name
  description = ""Name of the Firestore database""
}

output ""service_account_email"" {
  value       = google_service_account.honua_function.email
  description = ""Email of the service account used by Cloud Functions""
}");

        return tf.ToString();
    }

}
