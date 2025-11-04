# Variables for Google Cloud Run Serverless Module

# ==================== Project Configuration ====================
variable "project_id" {
  description = "GCP project ID"
  type        = string
}

variable "region" {
  description = "GCP region for Cloud Run deployment"
  type        = string
  default     = "us-central1"
}

variable "environment" {
  description = "Environment name (dev, staging, production)"
  type        = string
  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "Environment must be dev, staging, or production."
  }
}

variable "service_name" {
  description = "Name of the Cloud Run service"
  type        = string
  default     = "honua"
}

# ==================== Container Configuration ====================
variable "container_image" {
  description = "Full container image path (e.g., gcr.io/PROJECT/honua:tag)"
  type        = string
}

variable "aspnetcore_environment" {
  description = "ASP.NET Core environment (Development, Staging, Production)"
  type        = string
  default     = "Production"
}

variable "additional_env_vars" {
  description = "Additional environment variables for the container"
  type        = map(string)
  default     = {}
}

variable "cors_origins" {
  description = "Allowed CORS origins"
  type        = list(string)
  default     = ["*"]
}

variable "gdal_cache_max" {
  description = "GDAL cache size in MB"
  type        = string
  default     = "512"
}

# ==================== Scaling Configuration ====================
variable "min_instances" {
  description = "Minimum number of instances (0 for true serverless)"
  type        = number
  default     = 0
  validation {
    condition     = var.min_instances >= 0 && var.min_instances <= 100
    error_message = "min_instances must be between 0 and 100."
  }
}

variable "max_instances" {
  description = "Maximum number of instances"
  type        = number
  default     = 100
  validation {
    condition     = var.max_instances >= 1 && var.max_instances <= 1000
    error_message = "max_instances must be between 1 and 1000."
  }
}

variable "max_concurrent_requests" {
  description = "Maximum concurrent requests per instance"
  type        = number
  default     = 80
  validation {
    condition     = var.max_concurrent_requests >= 1 && var.max_concurrent_requests <= 1000
    error_message = "max_concurrent_requests must be between 1 and 1000."
  }
}

variable "cpu_limit" {
  description = "CPU limit (e.g., '1', '2', '4', '8')"
  type        = string
  default     = "2"
  validation {
    condition     = contains(["1", "2", "4", "8"], var.cpu_limit)
    error_message = "CPU limit must be 1, 2, 4, or 8."
  }
}

variable "memory_limit" {
  description = "Memory limit (e.g., '512Mi', '1Gi', '2Gi', '4Gi', '8Gi')"
  type        = string
  default     = "2Gi"
}

variable "cpu_always_allocated" {
  description = "Always allocate CPU (true for request-heavy workloads)"
  type        = bool
  default     = true
}

variable "request_timeout" {
  description = "Request timeout in seconds (max 3600)"
  type        = number
  default     = 300
  validation {
    condition     = var.request_timeout >= 1 && var.request_timeout <= 3600
    error_message = "request_timeout must be between 1 and 3600 seconds."
  }
}

# ==================== Database Configuration ====================
variable "create_database" {
  description = "Create Cloud SQL PostgreSQL instance"
  type        = bool
  default     = true
}

variable "database_name" {
  description = "Name of the PostgreSQL database"
  type        = string
  default     = "honua"
}

variable "db_username" {
  description = "Database username"
  type        = string
  default     = "honua"
}

variable "postgres_version" {
  description = "PostgreSQL version"
  type        = string
  default     = "POSTGRES_15"
  validation {
    condition     = contains(["POSTGRES_14", "POSTGRES_15", "POSTGRES_16"], var.postgres_version)
    error_message = "postgres_version must be POSTGRES_14, POSTGRES_15, or POSTGRES_16."
  }
}

variable "db_tier" {
  description = "Cloud SQL tier (e.g., db-f1-micro, db-g1-small, db-custom-1-3840)"
  type        = string
  default     = "db-g1-small"
}

variable "db_availability_type" {
  description = "Database availability type (ZONAL or REGIONAL)"
  type        = string
  default     = "ZONAL"
  validation {
    condition     = contains(["ZONAL", "REGIONAL"], var.db_availability_type)
    error_message = "db_availability_type must be ZONAL or REGIONAL."
  }
}

variable "db_disk_size" {
  description = "Initial database disk size in GB"
  type        = number
  default     = 10
}

variable "db_max_disk_size" {
  description = "Maximum database disk size for autoresize in GB"
  type        = number
  default     = 100
}

variable "db_max_connections" {
  description = "Maximum database connections"
  type        = string
  default     = "100"
}

variable "db_backup_retention_days" {
  description = "Database backup retention in days"
  type        = number
  default     = 7
}

variable "db_point_in_time_recovery" {
  description = "Enable point-in-time recovery"
  type        = bool
  default     = true
}

variable "db_deletion_protection" {
  description = "Prevent accidental database deletion"
  type        = bool
  default     = true
}

# ==================== VPC Configuration ====================
variable "enable_vpc_connector" {
  description = "Enable VPC connector for private Cloud SQL access"
  type        = bool
  default     = true
}

variable "vpc_network_name" {
  description = "VPC network name for VPC connector"
  type        = string
  default     = "default"
}

variable "vpc_network_id" {
  description = "Full VPC network ID (projects/PROJECT/global/networks/NETWORK)"
  type        = string
  default     = ""
}

variable "vpc_connector_cidr" {
  description = "CIDR range for VPC connector (e.g., 10.8.0.0/28)"
  type        = string
  default     = "10.8.0.0/28"
}

variable "vpc_connector_machine_type" {
  description = "VPC connector machine type"
  type        = string
  default     = "e2-micro"
}

variable "vpc_connector_max_instances" {
  description = "Maximum VPC connector instances"
  type        = number
  default     = 3
}

# ==================== Load Balancer Configuration ====================
variable "create_load_balancer" {
  description = "Create global load balancer with SSL"
  type        = bool
  default     = true
}

variable "custom_domains" {
  description = "Custom domains for SSL certificate"
  type        = list(string)
  default     = []
}

variable "lb_log_sample_rate" {
  description = "Load balancer log sampling rate (0.0 to 1.0)"
  type        = number
  default     = 1.0
}

# ==================== CDN Configuration ====================
variable "enable_cdn" {
  description = "Enable Cloud CDN for caching"
  type        = bool
  default     = true
}

variable "cdn_default_ttl" {
  description = "Default CDN TTL in seconds"
  type        = number
  default     = 3600 # 1 hour
}

variable "cdn_max_ttl" {
  description = "Maximum CDN TTL in seconds"
  type        = number
  default     = 86400 # 24 hours
}

variable "cdn_client_ttl" {
  description = "Client CDN TTL in seconds"
  type        = number
  default     = 3600 # 1 hour
}

# ==================== Security Configuration ====================
variable "allow_unauthenticated" {
  description = "Allow unauthenticated access to Cloud Run service"
  type        = bool
  default     = true
}

variable "authorized_members" {
  description = "List of authorized IAM members (e.g., user:email@example.com)"
  type        = list(string)
  default     = []
}

variable "enable_cloud_armor" {
  description = "Enable Cloud Armor security policy"
  type        = bool
  default     = true
}

variable "rate_limit_threshold" {
  description = "Rate limit threshold (requests per minute, 0 to disable)"
  type        = number
  default     = 1000
}

variable "blocked_ip_ranges" {
  description = "IP ranges to block via Cloud Armor"
  type        = list(string)
  default     = []
}

variable "enable_adaptive_protection" {
  description = "Enable Cloud Armor adaptive protection (DDoS)"
  type        = bool
  default     = false
}

# ==================== Health Check Configuration ====================
variable "health_check_path" {
  description = "HTTP path for health checks"
  type        = string
  default     = "/health"
}

# ==================== Additional Features ====================
variable "enable_gcs_access" {
  description = "Grant service account access to Google Cloud Storage"
  type        = bool
  default     = true
}

variable "labels" {
  description = "Additional labels for all resources"
  type        = map(string)
  default     = {}
}
