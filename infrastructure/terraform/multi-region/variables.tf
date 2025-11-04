# ============================================================================
# Multi-Region Deployment Variables
# ============================================================================

# ============================================================================
# Cloud Provider Configuration
# ============================================================================

variable "cloud_provider" {
  description = "Cloud provider to deploy to (aws, azure, gcp)"
  type        = string

  validation {
    condition     = contains(["aws", "azure", "gcp"], var.cloud_provider)
    error_message = "Cloud provider must be aws, azure, or gcp."
  }
}

# ============================================================================
# Region Configuration
# ============================================================================

variable "primary_region" {
  description = "Primary region for deployment"
  type        = string
}

variable "dr_region" {
  description = "Disaster recovery region"
  type        = string
}

variable "regions" {
  description = "Map of region configurations"
  type = map(object({
    name          = string
    is_primary    = bool
    instance_count = number
    instance_type = string
    db_instance_class = string
    enable_nat_gateway = bool
  }))
  default = null
}

# ============================================================================
# Environment Configuration
# ============================================================================

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be dev, staging, or prod."
  }
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "honua"
}

# ============================================================================
# Compute Configuration
# ============================================================================

variable "primary_instance_count" {
  description = "Number of compute instances in primary region"
  type        = number
  default     = 3
}

variable "dr_instance_count" {
  description = "Number of compute instances in DR region"
  type        = number
  default     = 1
}

variable "primary_instance_type" {
  description = "Instance type for primary region"
  type        = string
  default     = null # Will be set based on cloud provider
}

variable "dr_instance_type" {
  description = "Instance type for DR region"
  type        = string
  default     = null # Will be set based on cloud provider
}

variable "container_image" {
  description = "Container image to deploy"
  type        = string
  default     = "honuaio/honua-server:latest"
}

variable "container_port" {
  description = "Container port to expose"
  type        = number
  default     = 8080
}

variable "cpu_limit" {
  description = "CPU limit for container (in vCPUs)"
  type        = string
  default     = "2.0"
}

variable "memory_limit" {
  description = "Memory limit for container (in GB)"
  type        = string
  default     = "4.0"
}

# ============================================================================
# Database Configuration
# ============================================================================

variable "primary_db_instance_class" {
  description = "Database instance class for primary region"
  type        = string
  default     = null # Will be set based on cloud provider
}

variable "dr_db_instance_class" {
  description = "Database instance class for DR region"
  type        = string
  default     = null # Will be set based on cloud provider
}

variable "db_engine_version" {
  description = "PostgreSQL engine version"
  type        = string
  default     = "15"
}

variable "db_storage_size" {
  description = "Database storage size in GB"
  type        = number
  default     = 100
}

variable "db_backup_retention_days" {
  description = "Number of days to retain database backups"
  type        = number
  default     = 30
}

variable "db_admin_username" {
  description = "Database administrator username"
  type        = string
  default     = "honuaadmin"
  sensitive   = true
}

variable "db_admin_password" {
  description = "Database administrator password"
  type        = string
  sensitive   = true
}

# ============================================================================
# Storage Configuration
# ============================================================================

variable "storage_replication_enabled" {
  description = "Enable cross-region storage replication"
  type        = bool
  default     = true
}

variable "storage_tier" {
  description = "Storage tier (standard, infrequent_access, glacier)"
  type        = string
  default     = "standard"
}

variable "storage_encryption_enabled" {
  description = "Enable storage encryption at rest"
  type        = bool
  default     = true
}

# ============================================================================
# Replication Configuration
# ============================================================================

variable "enable_db_replication" {
  description = "Enable cross-region database replication"
  type        = bool
  default     = true
}

variable "enable_storage_replication" {
  description = "Enable cross-region storage replication"
  type        = bool
  default     = true
}

variable "enable_redis_replication" {
  description = "Enable cross-region Redis replication"
  type        = bool
  default     = true
}

variable "replication_lag_alert_threshold" {
  description = "Alert threshold for replication lag in seconds"
  type        = number
  default     = 60
}

# ============================================================================
# Load Balancer Configuration
# ============================================================================

variable "enable_global_lb" {
  description = "Enable global load balancer"
  type        = bool
  default     = true
}

variable "failover_routing_policy" {
  description = "Routing policy for failover (latency, geolocation, weighted)"
  type        = string
  default     = "latency"

  validation {
    condition     = contains(["latency", "geolocation", "weighted", "failover"], var.failover_routing_policy)
    error_message = "Routing policy must be latency, geolocation, weighted, or failover."
  }
}

variable "health_check_interval" {
  description = "Health check interval in seconds"
  type        = number
  default     = 30
}

variable "health_check_timeout" {
  description = "Health check timeout in seconds"
  type        = number
  default     = 10
}

variable "health_check_threshold" {
  description = "Number of consecutive health check failures before marking unhealthy"
  type        = number
  default     = 3
}

variable "health_check_path" {
  description = "Health check endpoint path"
  type        = string
  default     = "/health"
}

# ============================================================================
# DNS Configuration
# ============================================================================

variable "domain_name" {
  description = "Domain name for the application"
  type        = string
  default     = null
}

variable "create_dns_zone" {
  description = "Create a new DNS zone"
  type        = bool
  default     = false
}

variable "dns_zone_id" {
  description = "Existing DNS zone ID (if create_dns_zone is false)"
  type        = string
  default     = null
}

variable "subdomain" {
  description = "Subdomain for the application"
  type        = string
  default     = "api"
}

variable "dns_ttl" {
  description = "DNS record TTL in seconds"
  type        = number
  default     = 60
}

# ============================================================================
# Security Configuration
# ============================================================================

variable "enable_waf" {
  description = "Enable Web Application Firewall"
  type        = bool
  default     = true
}

variable "enable_ddos_protection" {
  description = "Enable DDoS protection"
  type        = bool
  default     = true
}

variable "allowed_cidr_blocks" {
  description = "List of CIDR blocks allowed to access the application"
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "enable_vpc_endpoints" {
  description = "Enable VPC endpoints for AWS services"
  type        = bool
  default     = true
}

# ============================================================================
# Monitoring and Logging
# ============================================================================

variable "enable_monitoring" {
  description = "Enable monitoring and alerting"
  type        = bool
  default     = true
}

variable "enable_logging" {
  description = "Enable centralized logging"
  type        = bool
  default     = true
}

variable "log_retention_days" {
  description = "Number of days to retain logs"
  type        = number
  default     = 30
}

variable "enable_tracing" {
  description = "Enable distributed tracing"
  type        = bool
  default     = true
}

variable "alert_email" {
  description = "Email address for alerts"
  type        = string
  default     = null
}

variable "alert_slack_webhook" {
  description = "Slack webhook URL for alerts"
  type        = string
  default     = null
  sensitive   = true
}

variable "alert_pagerduty_key" {
  description = "PagerDuty integration key for alerts"
  type        = string
  default     = null
  sensitive   = true
}

# ============================================================================
# Cost Optimization
# ============================================================================

variable "enable_auto_scaling" {
  description = "Enable auto-scaling for compute resources"
  type        = bool
  default     = true
}

variable "min_instances" {
  description = "Minimum number of instances for auto-scaling"
  type        = number
  default     = 1
}

variable "max_instances" {
  description = "Maximum number of instances for auto-scaling"
  type        = number
  default     = 10
}

variable "target_cpu_utilization" {
  description = "Target CPU utilization for auto-scaling (percentage)"
  type        = number
  default     = 70
}

variable "enable_spot_instances" {
  description = "Enable spot/preemptible instances for cost savings"
  type        = bool
  default     = false
}

# ============================================================================
# Backup and Disaster Recovery
# ============================================================================

variable "enable_automated_backups" {
  description = "Enable automated backups"
  type        = bool
  default     = true
}

variable "backup_retention_days" {
  description = "Number of days to retain backups"
  type        = number
  default     = 30
}

variable "enable_point_in_time_recovery" {
  description = "Enable point-in-time recovery for databases"
  type        = bool
  default     = true
}

variable "rto_minutes" {
  description = "Recovery Time Objective in minutes"
  type        = number
  default     = 15
}

variable "rpo_seconds" {
  description = "Recovery Point Objective in seconds"
  type        = number
  default     = 60
}

# ============================================================================
# Feature Flags
# ============================================================================

variable "enable_cdn" {
  description = "Enable CDN for static assets"
  type        = bool
  default     = true
}

variable "enable_redis_cache" {
  description = "Enable Redis cache"
  type        = bool
  default     = true
}

variable "redis_instance_type" {
  description = "Redis instance type"
  type        = string
  default     = null # Will be set based on cloud provider
}

variable "enable_search" {
  description = "Enable search service (OpenSearch/Elasticsearch)"
  type        = bool
  default     = false
}

# ============================================================================
# Tags and Labels
# ============================================================================

variable "tags" {
  description = "Additional tags to apply to all resources"
  type        = map(string)
  default     = {}
}

variable "common_tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default = {
    Project    = "HonuaIO"
    ManagedBy  = "Terraform"
    Repository = "github.com/honuaio/honua"
  }
}

# ============================================================================
# Advanced Configuration
# ============================================================================

variable "enable_blue_green_deployment" {
  description = "Enable blue/green deployment capability"
  type        = bool
  default     = false
}

variable "enable_canary_deployment" {
  description = "Enable canary deployment capability"
  type        = bool
  default     = false
}

variable "maintenance_window" {
  description = "Maintenance window (e.g., 'sun:02:00-sun:04:00')"
  type        = string
  default     = "sun:02:00-sun:04:00"
}

variable "enable_encryption_in_transit" {
  description = "Enable encryption in transit (TLS)"
  type        = bool
  default     = true
}

variable "tls_version" {
  description = "Minimum TLS version"
  type        = string
  default     = "1.3"
}

# ============================================================================
# Compliance and Governance
# ============================================================================

variable "enable_audit_logging" {
  description = "Enable audit logging"
  type        = bool
  default     = true
}

variable "compliance_framework" {
  description = "Compliance framework (hipaa, pci, soc2, gdpr)"
  type        = string
  default     = "soc2"

  validation {
    condition     = contains(["hipaa", "pci", "soc2", "gdpr", "none"], var.compliance_framework)
    error_message = "Compliance framework must be hipaa, pci, soc2, gdpr, or none."
  }
}

variable "data_residency_region" {
  description = "Data residency requirement (must match primary_region for strict compliance)"
  type        = string
  default     = null
}
