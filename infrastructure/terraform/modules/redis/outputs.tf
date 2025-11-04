# Outputs for Redis Module

# AWS ElastiCache Outputs
output "redis_endpoint" {
  description = "Redis endpoint"
  value = var.cloud_provider == "aws" ? (
    var.enable_cluster_mode ?
      aws_elasticache_replication_group.honua[0].configuration_endpoint_address :
      aws_elasticache_replication_group.honua[0].primary_endpoint_address
  ) : (
    var.cloud_provider == "azure" ? azurerm_redis_cache.honua[0].hostname : (
      var.cloud_provider == "gcp" ? google_redis_instance.honua[0].host : null
    )
  )
}

output "redis_port" {
  description = "Redis port"
  value = var.cloud_provider == "gcp" ? google_redis_instance.honua[0].port : 6379
}

output "redis_auth_token_secret" {
  description = "Secret ARN/ID containing Redis auth token"
  value = var.cloud_provider == "azure" ? azurerm_key_vault_secret.redis_auth[0].id : (
    var.cloud_provider == "gcp" ? google_secret_manager_secret.redis_auth[0].id : null
  )
}

# AWS-specific outputs
output "aws_redis_reader_endpoint" {
  description = "AWS ElastiCache reader endpoint"
  value       = var.cloud_provider == "aws" && !var.enable_cluster_mode ? aws_elasticache_replication_group.honua[0].reader_endpoint_address : null
}

output "aws_security_group_id" {
  description = "AWS Redis security group ID"
  value       = var.cloud_provider == "aws" ? aws_security_group.redis[0].id : null
}

# Azure-specific outputs
output "azure_redis_ssl_port" {
  description = "Azure Redis SSL port"
  value       = var.cloud_provider == "azure" ? azurerm_redis_cache.honua[0].ssl_port : null
}

# GCP-specific outputs
output "gcp_redis_current_location_id" {
  description = "GCP Redis current location"
  value       = var.cloud_provider == "gcp" ? google_redis_instance.honua[0].current_location_id : null
}
