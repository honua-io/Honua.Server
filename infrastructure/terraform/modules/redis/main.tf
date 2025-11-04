# Redis Module - Multi-Cloud Support
# Supports AWS ElastiCache, Azure Cache for Redis, and GCP Memorystore

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

# ==================== AWS ElastiCache for Redis ====================
resource "aws_elasticache_subnet_group" "honua" {
  count      = var.cloud_provider == "aws" ? 1 : 0
  name       = "${var.redis_name}-${var.environment}"
  subnet_ids = var.subnet_ids

  tags = merge(
    var.tags,
    {
      Name        = "${var.redis_name}-${var.environment}"
      Environment = var.environment
    }
  )
}

resource "aws_elasticache_parameter_group" "honua" {
  count  = var.cloud_provider == "aws" ? 1 : 0
  name   = "${var.redis_name}-redis7-${var.environment}"
  family = "redis7"

  parameter {
    name  = "maxmemory-policy"
    value = "allkeys-lru"
  }

  parameter {
    name  = "timeout"
    value = "300"
  }

  parameter {
    name  = "tcp-keepalive"
    value = "300"
  }

  tags = var.tags
}

resource "aws_elasticache_replication_group" "honua" {
  count                      = var.cloud_provider == "aws" ? 1 : 0
  replication_group_id       = "${var.redis_name}-${var.environment}"
  description                = "Redis cluster for Honua ${var.environment}"
  engine                     = "redis"
  engine_version             = var.redis_version
  node_type                  = var.redis_node_type
  port                       = 6379
  parameter_group_name       = aws_elasticache_parameter_group.honua[0].name
  subnet_group_name          = aws_elasticache_subnet_group.honua[0].name
  security_group_ids         = [aws_security_group.redis[0].id]

  # Cluster mode configuration
  automatic_failover_enabled = var.enable_cluster_mode
  multi_az_enabled          = var.environment == "production"
  num_cache_clusters        = var.enable_cluster_mode ? null : var.num_cache_nodes

  # For cluster mode
  num_node_groups         = var.enable_cluster_mode ? var.num_node_groups : null
  replicas_per_node_group = var.enable_cluster_mode ? var.replicas_per_node_group : null

  # Encryption
  at_rest_encryption_enabled = true
  kms_key_id                = var.kms_key_arn
  transit_encryption_enabled = true
  auth_token_enabled        = true

  # Maintenance and backups
  maintenance_window       = "mon:03:00-mon:04:00"
  snapshot_window         = "02:00-03:00"
  snapshot_retention_limit = var.backup_retention_days
  auto_minor_version_upgrade = true

  # Logging
  log_delivery_configuration {
    destination      = aws_cloudwatch_log_group.redis_slow_log[0].name
    destination_type = "cloudwatch-logs"
    log_format       = "json"
    log_type         = "slow-log"
  }

  log_delivery_configuration {
    destination      = aws_cloudwatch_log_group.redis_engine_log[0].name
    destination_type = "cloudwatch-logs"
    log_format       = "json"
    log_type         = "engine-log"
  }

  tags = merge(
    var.tags,
    {
      Name        = "${var.redis_name}-${var.environment}"
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  )
}

resource "aws_cloudwatch_log_group" "redis_slow_log" {
  count             = var.cloud_provider == "aws" ? 1 : 0
  name              = "/aws/elasticache/${var.redis_name}-${var.environment}/slow-log"
  retention_in_days = 7

  tags = var.tags
}

resource "aws_cloudwatch_log_group" "redis_engine_log" {
  count             = var.cloud_provider == "aws" ? 1 : 0
  name              = "/aws/elasticache/${var.redis_name}-${var.environment}/engine-log"
  retention_in_days = 7

  tags = var.tags
}

# Redis Security Group
resource "aws_security_group" "redis" {
  count       = var.cloud_provider == "aws" ? 1 : 0
  name        = "${var.redis_name}-redis-${var.environment}"
  description = "Security group for ElastiCache Redis"
  vpc_id      = var.vpc_id

  ingress {
    description = "Redis from VPC"
    from_port   = 6379
    to_port     = 6379
    protocol    = "tcp"
    cidr_blocks = [var.vpc_cidr]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(
    var.tags,
    {
      Name = "${var.redis_name}-redis-${var.environment}"
    }
  )
}

# ==================== Azure Cache for Redis ====================
resource "azurerm_redis_cache" "honua" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.redis_name}-${var.environment}"
  location            = var.azure_location
  resource_group_name = var.resource_group_name
  capacity            = var.azure_redis_capacity
  family              = var.azure_redis_family
  sku_name            = var.azure_redis_sku

  enable_non_ssl_port = false
  minimum_tls_version = "1.2"

  redis_version = var.redis_version

  # For Premium SKU
  shard_count = var.azure_redis_sku == "Premium" ? var.num_node_groups : null

  # Network
  subnet_id = var.azure_redis_sku == "Premium" ? var.subnet_ids[0] : null

  redis_configuration {
    maxmemory_policy                     = "allkeys-lru"
    maxmemory_reserved                   = 50
    maxmemory_delta                      = 50
    enable_authentication                = true
    notify_keyspace_events              = "Ex"
    rdb_backup_enabled                   = var.environment == "production"
    rdb_backup_frequency                 = var.environment == "production" ? 60 : null
    rdb_backup_max_snapshot_count        = var.environment == "production" ? var.backup_retention_days : null
    rdb_storage_connection_string        = var.environment == "production" ? var.azure_storage_connection_string : null
  }

  patch_schedule {
    day_of_week    = "Monday"
    start_hour_utc = 3
  }

  tags = merge(
    var.tags,
    {
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  )
}

# Store Redis auth token in Key Vault
resource "azurerm_key_vault_secret" "redis_auth" {
  count        = var.cloud_provider == "azure" ? 1 : 0
  name         = "${var.redis_name}-auth-token"
  value        = azurerm_redis_cache.honua[0].primary_access_key
  key_vault_id = var.key_vault_id

  tags = var.tags
}

# ==================== GCP Memorystore for Redis ====================
resource "google_redis_instance" "honua" {
  count              = var.cloud_provider == "gcp" ? 1 : 0
  name               = "${var.redis_name}-${var.environment}"
  tier               = var.environment == "production" ? "STANDARD_HA" : "BASIC"
  memory_size_gb     = var.gcp_redis_memory_size
  region             = var.gcp_region
  project            = var.gcp_project_id
  redis_version      = "REDIS_${replace(var.redis_version, ".", "_")}"
  display_name       = "${var.redis_name}-${var.environment}"

  authorized_network = var.vpc_id
  connect_mode       = "PRIVATE_SERVICE_ACCESS"

  auth_enabled       = true
  transit_encryption_mode = "SERVER_AUTHENTICATION"

  redis_configs = {
    maxmemory-policy = "allkeys-lru"
    timeout          = "300"
  }

  maintenance_policy {
    weekly_maintenance_window {
      day = "MONDAY"
      start_time {
        hours   = 3
        minutes = 0
        seconds = 0
        nanos   = 0
      }
    }
  }

  labels = merge(
    var.tags,
    {
      environment = var.environment
      managed_by  = "terraform"
    }
  )
}

# Store Redis auth string in Secret Manager
resource "google_secret_manager_secret" "redis_auth" {
  count     = var.cloud_provider == "gcp" ? 1 : 0
  secret_id = "${var.redis_name}-auth-string-${var.environment}"
  project   = var.gcp_project_id

  replication {
    automatic = true
  }

  labels = var.tags
}

resource "google_secret_manager_secret_version" "redis_auth" {
  count  = var.cloud_provider == "gcp" ? 1 : 0
  secret = google_secret_manager_secret.redis_auth[0].id
  secret_data = google_redis_instance.honua[0].auth_string
}

# ==================== Monitoring and Alarms ====================
# CloudWatch Alarms for AWS
resource "aws_cloudwatch_metric_alarm" "redis_cpu" {
  count               = var.cloud_provider == "aws" ? 1 : 0
  alarm_name          = "${var.redis_name}-${var.environment}-high-cpu"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = "2"
  metric_name         = "CPUUtilization"
  namespace           = "AWS/ElastiCache"
  period              = "300"
  statistic           = "Average"
  threshold           = "75"
  alarm_description   = "This metric monitors Redis CPU utilization"

  dimensions = {
    ReplicationGroupId = aws_elasticache_replication_group.honua[0].id
  }

  tags = var.tags
}

resource "aws_cloudwatch_metric_alarm" "redis_memory" {
  count               = var.cloud_provider == "aws" ? 1 : 0
  alarm_name          = "${var.redis_name}-${var.environment}-high-memory"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = "2"
  metric_name         = "DatabaseMemoryUsagePercentage"
  namespace           = "AWS/ElastiCache"
  period              = "300"
  statistic           = "Average"
  threshold           = "80"
  alarm_description   = "This metric monitors Redis memory usage"

  dimensions = {
    ReplicationGroupId = aws_elasticache_replication_group.honua[0].id
  }

  tags = var.tags
}
