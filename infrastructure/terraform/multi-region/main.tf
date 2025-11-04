# ============================================================================
# HonuaIO Multi-Region Deployment
# ============================================================================
# This configuration deploys HonuaIO across multiple regions for high
# availability and disaster recovery.
#
# Supports:
#   - AWS (Route53, ECS, RDS, S3)
#   - Azure (Front Door, Container Apps, PostgreSQL, Blob)
#   - GCP (Cloud Load Balancing, Cloud Run, Cloud SQL, GCS)
#
# Architecture:
#   - Primary region: Full deployment with read-write database
#   - DR region: Scaled-down deployment with read replica
#   - Global load balancer: Automatic failover based on health checks
# ============================================================================

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.80"
    }
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }

  # Uncomment for remote state
  # backend "s3" {
  #   bucket         = "honua-terraform-state"
  #   key            = "multi-region/terraform.tfstate"
  #   region         = "us-east-1"
  #   encrypt        = true
  #   dynamodb_table = "honua-terraform-locks"
  # }
}

# ============================================================================
# Provider Configuration
# ============================================================================

provider "aws" {
  region = var.primary_region

  default_tags {
    tags = merge(
      var.common_tags,
      var.tags,
      {
        Environment = var.environment
        Region      = "primary"
      }
    )
  }
}

provider "aws" {
  alias  = "dr"
  region = var.dr_region

  default_tags {
    tags = merge(
      var.common_tags,
      var.tags,
      {
        Environment = var.environment
        Region      = "dr"
      }
    )
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = var.environment != "prod"
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = var.environment == "prod"
    }
  }
}

provider "google" {
  project = var.cloud_provider == "gcp" ? var.project_name : null
  region  = var.cloud_provider == "gcp" ? var.primary_region : null
}

# ============================================================================
# Data Sources
# ============================================================================

data "aws_caller_identity" "current" {
  count = var.cloud_provider == "aws" ? 1 : 0
}

data "aws_region" "primary" {
  count = var.cloud_provider == "aws" ? 1 : 0
}

data "aws_region" "dr" {
  count    = var.cloud_provider == "aws" ? 1 : 0
  provider = aws.dr
}

data "azurerm_client_config" "current" {
  count = var.cloud_provider == "azure" ? 1 : 0
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  # Generate unique suffix for resource naming
  unique_suffix = random_id.suffix.hex

  # Resource naming convention
  primary_prefix = "${var.project_name}-${var.environment}-${var.primary_region}"
  dr_prefix      = "${var.project_name}-${var.environment}-${var.dr_region}"
  global_prefix  = "${var.project_name}-${var.environment}-global"

  # Default instance types per cloud provider
  instance_types = {
    aws = {
      primary = coalesce(var.primary_instance_type, "c6i.2xlarge")
      dr      = coalesce(var.dr_instance_type, "c6i.xlarge")
    }
    azure = {
      primary = coalesce(var.primary_instance_type, "Standard_D4s_v5")
      dr      = coalesce(var.dr_instance_type, "Standard_D2s_v5")
    }
    gcp = {
      primary = coalesce(var.primary_instance_type, "n2-standard-4")
      dr      = coalesce(var.dr_instance_type, "n2-standard-2")
    }
  }

  # Default database instance classes per cloud provider
  db_instance_classes = {
    aws = {
      primary = coalesce(var.primary_db_instance_class, "db.r6g.xlarge")
      dr      = coalesce(var.dr_db_instance_class, "db.r6g.large")
    }
    azure = {
      primary = coalesce(var.primary_db_instance_class, "GP_Standard_D4s_v3")
      dr      = coalesce(var.dr_db_instance_class, "GP_Standard_D2s_v3")
    }
    gcp = {
      primary = coalesce(var.primary_db_instance_class, "db-custom-4-16384")
      dr      = coalesce(var.dr_db_instance_class, "db-custom-2-8192")
    }
  }

  # Redis instance types per cloud provider
  redis_instance_types = {
    aws = {
      type = coalesce(var.redis_instance_type, "cache.r6g.large")
    }
    azure = {
      type = coalesce(var.redis_instance_type, "Standard")
      size = "C2"
    }
    gcp = {
      tier         = coalesce(var.redis_instance_type, "STANDARD_HA")
      memory_size_gb = 5
    }
  }

  # Common tags for all resources
  tags = merge(
    var.common_tags,
    var.tags,
    {
      Environment         = var.environment
      MultiRegion         = "true"
      PrimaryRegion       = var.primary_region
      DRRegion           = var.dr_region
      ManagedBy          = "Terraform"
      ComplianceFramework = var.compliance_framework
    }
  )
}

# ============================================================================
# Random Suffix for Resource Naming
# ============================================================================

resource "random_id" "suffix" {
  byte_length = 4
}

# ============================================================================
# AWS Multi-Region Deployment
# ============================================================================

module "aws_primary" {
  count  = var.cloud_provider == "aws" ? 1 : 0
  source = "./modules/aws/regional-stack"

  region               = var.primary_region
  environment          = var.environment
  project_name         = var.project_name
  unique_suffix        = local.unique_suffix
  is_primary           = true

  # Compute configuration
  instance_count       = var.primary_instance_count
  instance_type        = local.instance_types.aws.primary
  container_image      = var.container_image
  container_port       = var.container_port
  cpu_limit           = var.cpu_limit
  memory_limit        = var.memory_limit

  # Database configuration
  db_instance_class    = local.db_instance_classes.aws.primary
  db_engine_version    = var.db_engine_version
  db_storage_size      = var.db_storage_size
  db_admin_username    = var.db_admin_username
  db_admin_password    = var.db_admin_password
  db_backup_retention  = var.db_backup_retention_days
  enable_multi_az      = true

  # Storage configuration
  enable_storage_encryption = var.storage_encryption_enabled
  storage_tier             = var.storage_tier

  # Caching
  enable_redis         = var.enable_redis_cache
  redis_instance_type  = local.redis_instance_types.aws.type

  # Networking
  allowed_cidr_blocks  = var.allowed_cidr_blocks
  enable_vpc_endpoints = var.enable_vpc_endpoints

  # Monitoring
  enable_monitoring    = var.enable_monitoring
  enable_logging       = var.enable_logging
  log_retention_days   = var.log_retention_days
  alert_email          = var.alert_email

  # Security
  enable_waf           = var.enable_waf
  enable_encryption_in_transit = var.enable_encryption_in_transit

  # Auto-scaling
  enable_auto_scaling       = var.enable_auto_scaling
  min_instances             = var.min_instances
  max_instances             = var.max_instances
  target_cpu_utilization    = var.target_cpu_utilization

  tags = local.tags
}

module "aws_dr" {
  count  = var.cloud_provider == "aws" ? 1 : 0
  source = "./modules/aws/regional-stack"

  providers = {
    aws = aws.dr
  }

  region               = var.dr_region
  environment          = var.environment
  project_name         = var.project_name
  unique_suffix        = local.unique_suffix
  is_primary           = false

  # Compute configuration (scaled down for DR)
  instance_count       = var.dr_instance_count
  instance_type        = local.instance_types.aws.dr
  container_image      = var.container_image
  container_port       = var.container_port
  cpu_limit           = var.cpu_limit
  memory_limit        = var.memory_limit

  # Database configuration (read replica)
  db_instance_class    = local.db_instance_classes.aws.dr
  db_engine_version    = var.db_engine_version
  db_storage_size      = var.db_storage_size
  enable_read_replica  = var.enable_db_replication
  source_db_identifier = var.enable_db_replication ? module.aws_primary[0].db_identifier : null

  # Storage configuration (replicated from primary)
  enable_storage_encryption = var.storage_encryption_enabled
  source_bucket            = var.enable_storage_replication ? module.aws_primary[0].storage_bucket_id : null

  # Caching
  enable_redis         = var.enable_redis_cache
  redis_instance_type  = local.redis_instance_types.aws.type
  enable_redis_replica = var.enable_redis_replication
  primary_redis_endpoint = var.enable_redis_replication ? module.aws_primary[0].redis_endpoint : null

  # Networking
  allowed_cidr_blocks  = var.allowed_cidr_blocks
  enable_vpc_endpoints = var.enable_vpc_endpoints

  # Monitoring
  enable_monitoring    = var.enable_monitoring
  enable_logging       = var.enable_logging
  log_retention_days   = var.log_retention_days
  alert_email          = var.alert_email

  # Security
  enable_waf           = var.enable_waf
  enable_encryption_in_transit = var.enable_encryption_in_transit

  # Auto-scaling (more conservative in DR)
  enable_auto_scaling       = var.enable_auto_scaling
  min_instances             = 1
  max_instances             = var.max_instances
  target_cpu_utilization    = var.target_cpu_utilization

  tags = local.tags
}

module "aws_global_lb" {
  count  = var.cloud_provider == "aws" && var.enable_global_lb ? 1 : 0
  source = "./modules/aws/global-lb"

  project_name         = var.project_name
  environment          = var.environment
  unique_suffix        = local.unique_suffix

  # DNS configuration
  domain_name          = var.domain_name
  subdomain            = var.subdomain
  create_dns_zone      = var.create_dns_zone
  dns_zone_id          = var.dns_zone_id
  dns_ttl              = var.dns_ttl

  # Primary region configuration
  primary_region       = var.primary_region
  primary_endpoint     = module.aws_primary[0].load_balancer_dns
  primary_hosted_zone_id = module.aws_primary[0].load_balancer_zone_id

  # DR region configuration
  dr_region            = var.dr_region
  dr_endpoint          = module.aws_dr[0].load_balancer_dns
  dr_hosted_zone_id    = module.aws_dr[0].load_balancer_zone_id

  # Health check configuration
  health_check_path     = var.health_check_path
  health_check_interval = var.health_check_interval
  health_check_timeout  = var.health_check_timeout
  health_check_threshold = var.health_check_threshold

  # Routing policy
  routing_policy        = var.failover_routing_policy

  # CDN
  enable_cdn            = var.enable_cdn

  tags = local.tags
}

# ============================================================================
# Azure Multi-Region Deployment
# ============================================================================

module "azure_primary" {
  count  = var.cloud_provider == "azure" ? 1 : 0
  source = "./modules/azure/regional-stack"

  location             = var.primary_region
  environment          = var.environment
  project_name         = var.project_name
  unique_suffix        = local.unique_suffix
  is_primary           = true

  # Compute configuration
  instance_count       = var.primary_instance_count
  instance_type        = local.instance_types.azure.primary
  container_image      = var.container_image
  container_port       = var.container_port
  cpu_limit           = var.cpu_limit
  memory_limit        = var.memory_limit

  # Database configuration
  db_sku_name          = local.db_instance_classes.azure.primary
  db_version           = var.db_engine_version
  db_storage_size_gb   = var.db_storage_size
  db_admin_username    = var.db_admin_username
  db_admin_password    = var.db_admin_password
  db_backup_retention_days = var.db_backup_retention_days
  enable_zone_redundant = true
  enable_geo_backup    = var.enable_db_replication

  # Storage configuration
  storage_replication_type = var.enable_storage_replication ? "GRS" : "LRS"
  enable_storage_encryption = var.storage_encryption_enabled

  # Caching
  enable_redis         = var.enable_redis_cache
  redis_sku_name       = local.redis_instance_types.azure.type
  redis_capacity       = local.redis_instance_types.azure.size

  # Monitoring
  enable_monitoring    = var.enable_monitoring
  enable_logging       = var.enable_logging
  log_retention_days   = var.log_retention_days
  alert_email          = var.alert_email

  # Security
  enable_waf           = var.enable_waf
  enable_ddos_protection = var.enable_ddos_protection

  tags = local.tags
}

module "azure_dr" {
  count  = var.cloud_provider == "azure" ? 1 : 0
  source = "./modules/azure/regional-stack"

  location             = var.dr_region
  environment          = var.environment
  project_name         = var.project_name
  unique_suffix        = local.unique_suffix
  is_primary           = false

  # Compute configuration (scaled down)
  instance_count       = var.dr_instance_count
  instance_type        = local.instance_types.azure.dr
  container_image      = var.container_image
  container_port       = var.container_port
  cpu_limit           = var.cpu_limit
  memory_limit        = var.memory_limit

  # Database configuration (replica)
  db_sku_name          = local.db_instance_classes.azure.dr
  db_version           = var.db_engine_version
  db_storage_size_gb   = var.db_storage_size
  enable_read_replica  = var.enable_db_replication
  primary_server_id    = var.enable_db_replication ? module.azure_primary[0].db_server_id : null

  # Storage configuration (replicated)
  storage_replication_type = "LRS" # Primary handles GRS
  enable_storage_encryption = var.storage_encryption_enabled

  # Caching
  enable_redis         = var.enable_redis_cache
  redis_sku_name       = local.redis_instance_types.azure.type
  redis_capacity       = local.redis_instance_types.azure.size

  # Monitoring
  enable_monitoring    = var.enable_monitoring
  enable_logging       = var.enable_logging
  log_retention_days   = var.log_retention_days
  alert_email          = var.alert_email

  tags = local.tags
}

module "azure_global_lb" {
  count  = var.cloud_provider == "azure" && var.enable_global_lb ? 1 : 0
  source = "./modules/azure/global-lb"

  project_name         = var.project_name
  environment          = var.environment
  unique_suffix        = local.unique_suffix
  resource_group_name  = module.azure_primary[0].resource_group_name

  # Primary region configuration
  primary_backend      = module.azure_primary[0].app_fqdn
  primary_location     = var.primary_region

  # DR region configuration
  dr_backend           = module.azure_dr[0].app_fqdn
  dr_location          = var.dr_region

  # Health probe configuration
  health_probe_path    = var.health_check_path
  health_probe_interval = var.health_check_interval
  health_probe_threshold = var.health_check_threshold

  # WAF and security
  enable_waf           = var.enable_waf

  # CDN
  enable_cdn           = var.enable_cdn

  tags = local.tags
}

# ============================================================================
# GCP Multi-Region Deployment
# ============================================================================

module "gcp_primary" {
  count  = var.cloud_provider == "gcp" ? 1 : 0
  source = "./modules/gcp/regional-stack"

  region               = var.primary_region
  environment          = var.environment
  project_name         = var.project_name
  unique_suffix        = local.unique_suffix
  is_primary           = true

  # Compute configuration
  instance_count       = var.primary_instance_count
  machine_type         = local.instance_types.gcp.primary
  container_image      = var.container_image
  container_port       = var.container_port
  cpu_limit           = var.cpu_limit
  memory_limit        = var.memory_limit

  # Database configuration
  db_tier              = local.db_instance_classes.gcp.primary
  db_version           = "POSTGRES_${var.db_engine_version}"
  db_storage_size_gb   = var.db_storage_size
  enable_ha            = true
  enable_backup        = true
  backup_retention_days = var.db_backup_retention_days

  # Storage configuration
  storage_class        = "STANDARD"
  enable_versioning    = true

  # Caching
  enable_redis         = var.enable_redis_cache
  redis_tier           = local.redis_instance_types.gcp.tier
  redis_memory_size_gb = local.redis_instance_types.gcp.memory_size_gb

  # Monitoring
  enable_monitoring    = var.enable_monitoring
  enable_logging       = var.enable_logging

  tags = local.tags
}

module "gcp_dr" {
  count  = var.cloud_provider == "gcp" ? 1 : 0
  source = "./modules/gcp/regional-stack"

  region               = var.dr_region
  environment          = var.environment
  project_name         = var.project_name
  unique_suffix        = local.unique_suffix
  is_primary           = false

  # Compute configuration
  instance_count       = var.dr_instance_count
  machine_type         = local.instance_types.gcp.dr
  container_image      = var.container_image
  container_port       = var.container_port
  cpu_limit           = var.cpu_limit
  memory_limit        = var.memory_limit

  # Database configuration (replica)
  db_tier              = local.db_instance_classes.gcp.dr
  db_version           = "POSTGRES_${var.db_engine_version}"
  enable_read_replica  = var.enable_db_replication
  master_instance_name = var.enable_db_replication ? module.gcp_primary[0].db_instance_name : null

  # Storage configuration (replicated)
  storage_class        = "NEARLINE" # Cheaper for DR
  enable_versioning    = true
  source_bucket        = var.enable_storage_replication ? module.gcp_primary[0].storage_bucket_name : null

  # Caching
  enable_redis         = var.enable_redis_cache
  redis_tier           = "BASIC"
  redis_memory_size_gb = local.redis_instance_types.gcp.memory_size_gb

  # Monitoring
  enable_monitoring    = var.enable_monitoring
  enable_logging       = var.enable_logging

  tags = local.tags
}

module "gcp_global_lb" {
  count  = var.cloud_provider == "gcp" && var.enable_global_lb ? 1 : 0
  source = "./modules/gcp/global-lb"

  project_name         = var.project_name
  environment          = var.environment
  unique_suffix        = local.unique_suffix

  # Primary region configuration
  primary_backend      = module.gcp_primary[0].backend_service
  primary_region       = var.primary_region

  # DR region configuration
  dr_backend           = module.gcp_dr[0].backend_service
  dr_region            = var.dr_region

  # Health check configuration
  health_check_path    = var.health_check_path
  check_interval_sec   = var.health_check_interval
  timeout_sec          = var.health_check_timeout
  unhealthy_threshold  = var.health_check_threshold

  # CDN
  enable_cdn           = var.enable_cdn

  # SSL
  enable_ssl           = var.enable_encryption_in_transit

  tags = local.tags
}

# ============================================================================
# Monitoring and Alerting
# ============================================================================

module "monitoring" {
  count  = var.enable_monitoring ? 1 : 0
  source = "./modules/monitoring"

  cloud_provider       = var.cloud_provider
  environment          = var.environment
  project_name         = var.project_name

  # Alert configuration
  alert_email          = var.alert_email
  alert_slack_webhook  = var.alert_slack_webhook
  alert_pagerduty_key  = var.alert_pagerduty_key

  # Thresholds
  replication_lag_threshold = var.replication_lag_alert_threshold
  error_rate_threshold      = 1.0 # 1%
  latency_threshold_ms      = 500

  # Resource identifiers
  primary_resources = var.cloud_provider == "aws" ? {
    cluster_name = module.aws_primary[0].ecs_cluster_name
    db_identifier = module.aws_primary[0].db_identifier
    lb_arn_suffix = module.aws_primary[0].load_balancer_arn_suffix
  } : null

  dr_resources = var.cloud_provider == "aws" && var.enable_db_replication ? {
    cluster_name = module.aws_dr[0].ecs_cluster_name
    db_identifier = module.aws_dr[0].db_identifier
    lb_arn_suffix = module.aws_dr[0].load_balancer_arn_suffix
  } : null

  tags = local.tags
}
