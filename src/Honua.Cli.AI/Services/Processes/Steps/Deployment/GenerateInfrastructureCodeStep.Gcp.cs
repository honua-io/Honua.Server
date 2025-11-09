// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Cli.AI.Services.Guardrails;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// GCP-specific Terraform infrastructure code generation.
/// Contains methods for generating GCP resources including Cloud Run, Cloud SQL, Storage, etc.
/// </summary>
public partial class GenerateInfrastructureCodeStep
{
    private string GenerateGcpTerraform(ResourceEnvelope envelope)
    {
        var sanitizedName = SanitizeName(_state.DeploymentName);
        var minInstances = Math.Max(envelope.MinInstances, 1);
        var maxInstances = Math.Max(minInstances * 4, 10);
        var cpuLimit = envelope.MinVCpu >= 4 ? "4" : envelope.MinVCpu >= 2 ? "2" : "1";
        var memoryLimit = envelope.MinMemoryGb >= 16 ? "16Gi" : envelope.MinMemoryGb >= 4 ? "4Gi" : "2Gi";

        return $@"
terraform {{
  required_providers {{
    google = {{
      source  = ""hashicorp/google""
      version = ""~> 5.0""
    }}
  }}
}}

provider ""google"" {{
  project = var.project_id
  region  = ""{_state.Region}""
}}

# Guardrail envelope {envelope.Id} ({envelope.WorkloadProfile})
locals {{
  honua_guardrail_envelope      = ""{envelope.Id}""
  honua_guardrail_min_vcpu      = {envelope.MinVCpu}
  honua_guardrail_min_memory    = {envelope.MinMemoryGb}
  honua_guardrail_min_instances = {envelope.MinInstances}
  sanitized_name                = ""{sanitizedName}""
  min_instances                 = {minInstances}
  max_instances                 = {maxInstances}
}}

check ""honua_guardrail_instances"" {{
  assert {{
    condition     = local.honua_guardrail_min_instances >= 1
    error_message = ""Guardrail min instances must be >= 1 for envelope ${{local.honua_guardrail_envelope}}""
  }}
}}

# VPC Network for private Cloud SQL access
resource ""google_compute_network"" ""honua_vpc"" {{
  name                    = ""${{local.sanitized_name}}-vpc""
  auto_create_subnetworks = true
}}

# Reserve IP range for VPC peering with Cloud SQL
resource ""google_compute_global_address"" ""private_ip_address"" {{
  name          = ""${{local.sanitized_name}}-private-ip""
  purpose       = ""VPC_PEERING""
  address_type  = ""INTERNAL""
  prefix_length = 16
  network       = google_compute_network.honua_vpc.id
}}

# Create VPC peering connection for Cloud SQL
resource ""google_service_networking_connection"" ""private_vpc_connection"" {{
  network                 = google_compute_network.honua_vpc.id
  service                 = ""servicenetworking.googleapis.com""
  reserved_peering_ranges = [google_compute_global_address.private_ip_address.name]
}}

# Cloud SQL Database with root credentials
resource ""google_sql_database_instance"" ""honua_db"" {{
  name             = ""${{local.sanitized_name}}-db""
  database_version = ""POSTGRES_16""
  region           = ""{_state.Region}""

  settings {{
    tier              = ""{GetGcpTier(_state.Tier)}""
    availability_type = ""{(_state.Tier.ToLower() == "production" ? "REGIONAL" : "ZONAL")}""
    disk_size         = {GetStorageSize(_state.Tier)}
    disk_type         = ""PD_SSD""

    backup_configuration {{
      enabled            = true
      point_in_time_recovery_enabled = {(_state.Tier.ToLower() == "production" ? "true" : "false")}
      start_time         = ""02:00""
      backup_retention_settings {{
        retained_backups = {(_state.Tier.ToLower() == "production" ? "7" : "1")}
      }}
    }}

    ip_configuration {{
      # Disable public IPv4 access for security
      # Cloud Run will connect via Cloud SQL Proxy or Private IP
      ipv4_enabled = false

      # Enable private IP for VPC connectivity
      private_network = google_compute_network.honua_vpc.id

      # Only allow authorized networks in non-production for development access
      # Production should use Cloud SQL Proxy or Private Service Connect
      dynamic ""authorized_networks"" {{
        for_each = {(_state.Tier.ToLower() == "production" ? "[]" : "[{ name = \"dev-access\", value = var.dev_authorized_network }]")}
        content {{
          name  = authorized_networks.value.name
          value = authorized_networks.value.value
        }}
      }}
    }}
  }}

  deletion_protection = {(_state.Tier.ToLower() == "production" ? "true" : "false")}

  # Ensure VPC peering is established before creating the instance
  depends_on = [google_service_networking_connection.private_vpc_connection]
}}

resource ""google_sql_user"" ""honua_root"" {{
  name     = ""honua_admin""
  instance = google_sql_database_instance.honua_db.name
  password = var.db_root_password
}}

resource ""google_sql_database"" ""honua"" {{
  name     = ""honua""
  instance = google_sql_database_instance.honua_db.name
}}

# Storage Bucket with sanitized name
resource ""google_storage_bucket"" ""honua_rasters"" {{
  name     = ""${{local.sanitized_name}}-rasters-${{var.project_id}}""
  location = ""{_state.Region}""

  uniform_bucket_level_access = true

  cors {{
    origin          = var.cors_allowed_origins
    method          = [""GET"", ""HEAD""]
    response_header = [""Content-Type""]
    max_age_seconds = 3600
  }}

  labels = {{
    purpose = ""rasters""
  }}
}}

# Secret Manager for Database Password
resource ""google_secret_manager_secret"" ""db_password"" {{
  secret_id = ""${{local.sanitized_name}}-db-password""

  replication {{
    auto {{}}
  }}
}}

resource ""google_secret_manager_secret_version"" ""db_password"" {{
  secret      = google_secret_manager_secret.db_password.id
  secret_data = var.db_root_password
}}

# Grant Cloud Run service account access to the secret
resource ""google_secret_manager_secret_iam_member"" ""cloud_run_secret_accessor"" {{
  secret_id = google_secret_manager_secret.db_password.id
  role      = ""roles/secretmanager.secretAccessor""
  member    = ""serviceAccount:${{google_service_account.cloud_run.email}}""
}}

# Service account for Cloud Run
resource ""google_service_account"" ""cloud_run"" {{
  account_id   = ""${{local.sanitized_name}}-cloud-run""
  display_name = ""Cloud Run Service Account""
}}

# Serverless VPC Access Connector for Cloud Run
resource ""google_vpc_access_connector"" ""honua_connector"" {{
  name          = ""${{local.sanitized_name}}-connector""
  region        = ""{_state.Region}""
  network       = google_compute_network.honua_vpc.name
  ip_cidr_range = ""10.8.0.0/28""

  machine_type   = ""e2-micro""
  min_instances  = 2
  max_instances  = 3
}}

# Cloud Run Service
resource ""google_cloud_run_v2_service"" ""honua"" {{
  name     = ""${{local.sanitized_name}}-api""
  location = ""{_state.Region}""

  template {{
    scaling {{
      min_instance_count = local.min_instances
      max_instance_count = local.max_instances
    }}

    service_account = google_service_account.cloud_run.email

    containers {{
      image = var.app_version != ""latest"" ? ""gcr.io/honua-public/api:${{var.app_version}}"" : ""gcr.io/honua-public/api:v1.0.0""

      ports {{
        container_port = 8080
      }}

      resources {{
        limits = {{
          cpu    = ""{cpuLimit}""
          memory = ""{memoryLimit}""
        }}
      }}

      env {{
        name  = ""DATABASE_HOST""
        value = google_sql_database_instance.honua_db.public_ip_address
      }}

      env {{
        name  = ""DATABASE_NAME""
        value = ""honua""
      }}

      env {{
        name  = ""DATABASE_USER""
        value = ""honua_admin""
      }}

      env {{
        name = ""DATABASE_PASSWORD""
        value_source {{
          secret_key_ref {{
            secret  = google_secret_manager_secret.db_password.secret_id
            version = ""latest""
          }}
        }}
      }}

      env {{
        name  = ""GCS_BUCKET""
        value = google_storage_bucket.honua_rasters.name
      }}

      env {{
        name  = ""GCP_PROJECT_ID""
        value = var.project_id
      }}

      env {{
        name  = ""GCP_REGION""
        value = ""{_state.Region}""
      }}

      startup_probe {{
        http_get {{
          path = ""/health""
          port = 8080
        }}
        initial_delay_seconds = 10
        timeout_seconds       = 3
        period_seconds        = 10
        failure_threshold     = 3
      }}

      liveness_probe {{
        http_get {{
          path = ""/health""
          port = 8080
        }}
        initial_delay_seconds = 30
        timeout_seconds       = 3
        period_seconds        = 30
      }}
    }}

    vpc_access {{
      connector = google_vpc_access_connector.honua_connector.id
      egress    = ""PRIVATE_RANGES_ONLY""
    }}
  }}

  traffic {{
    type    = ""TRAFFIC_TARGET_ALLOCATION_TYPE_LATEST""
    percent = 100
  }}
}}

# Allow public access to Cloud Run service
resource ""google_cloud_run_service_iam_member"" ""public_access"" {{
  service  = google_cloud_run_v2_service.honua.name
  location = google_cloud_run_v2_service.honua.location
  role     = ""roles/run.invoker""
  member   = ""allUsers""
}}

output ""honua_guardrail_envelope"" {{
  value = local.honua_guardrail_envelope
}}

output ""honua_guardrail_policy"" {{
  value = {{
    envelope_id   = local.honua_guardrail_envelope
    min_vcpu      = local.honua_guardrail_min_vcpu
    min_memory_gb = local.honua_guardrail_min_memory
    min_instances = local.honua_guardrail_min_instances
  }}
}}

output ""service_url"" {{
  description = ""URL to access the Honua API""
  value       = google_cloud_run_v2_service.honua.uri
}}

output ""database_ip"" {{
  description = ""PostgreSQL database IP address""
  value       = google_sql_database_instance.honua_db.public_ip_address
  sensitive   = true
}}

output ""bucket_name"" {{
  description = ""Storage bucket name for raster storage""
  value       = google_storage_bucket.honua_rasters.name
}}
";
    }

    private string GetGcpTier(string tier) => tier.ToLower() switch
    {
        "development" => "db-f1-micro",
        "staging" => "db-n1-standard-1",
        "production" => "db-n1-standard-4",
        _ => "db-f1-micro"
    };

    private int GetStorageSize(string tier) => tier.ToLower() switch
    {
        "development" => 20,
        "staging" => 100,
        "production" => 500,
        _ => 20
    };
}
