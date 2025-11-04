# ============================================================================
# Multi-Region Deployment Outputs
# ============================================================================

# ============================================================================
# Global Endpoints
# ============================================================================

output "global_endpoint" {
  description = "Global load balancer endpoint"
  value = var.cloud_provider == "aws" && var.enable_global_lb ? (
    var.domain_name != null ?
      "https://${var.subdomain}.${var.domain_name}" :
      module.aws_global_lb[0].load_balancer_dns
  ) : var.cloud_provider == "azure" && var.enable_global_lb ? (
    module.azure_global_lb[0].front_door_endpoint
  ) : var.cloud_provider == "gcp" && var.enable_global_lb ? (
    module.gcp_global_lb[0].load_balancer_ip
  ) : "N/A"
}

output "global_load_balancer_name" {
  description = "Name of the global load balancer"
  value = var.cloud_provider == "aws" && var.enable_global_lb ? (
    module.aws_global_lb[0].hosted_zone_name
  ) : var.cloud_provider == "azure" && var.enable_global_lb ? (
    module.azure_global_lb[0].front_door_name
  ) : var.cloud_provider == "gcp" && var.enable_global_lb ? (
    module.gcp_global_lb[0].forwarding_rule_name
  ) : "N/A"
}

# ============================================================================
# Primary Region Outputs
# ============================================================================

output "primary_region" {
  description = "Primary region"
  value       = var.primary_region
}

output "primary_endpoint" {
  description = "Primary region application endpoint"
  value = var.cloud_provider == "aws" ? (
    try(module.aws_primary[0].load_balancer_dns, "N/A")
  ) : var.cloud_provider == "azure" ? (
    try(module.azure_primary[0].app_fqdn, "N/A")
  ) : var.cloud_provider == "gcp" ? (
    try(module.gcp_primary[0].service_url, "N/A")
  ) : "N/A"
}

output "primary_db_endpoint" {
  description = "Primary region database endpoint"
  value = var.cloud_provider == "aws" ? (
    try(module.aws_primary[0].db_endpoint, "N/A")
  ) : var.cloud_provider == "azure" ? (
    try(module.azure_primary[0].db_fqdn, "N/A")
  ) : var.cloud_provider == "gcp" ? (
    try(module.gcp_primary[0].db_connection_name, "N/A")
  ) : "N/A"
  sensitive = true
}

output "primary_storage_endpoint" {
  description = "Primary region storage endpoint"
  value = var.cloud_provider == "aws" ? (
    try(module.aws_primary[0].storage_bucket_regional_domain, "N/A")
  ) : var.cloud_provider == "azure" ? (
    try(module.azure_primary[0].storage_primary_endpoint, "N/A")
  ) : var.cloud_provider == "gcp" ? (
    try(module.gcp_primary[0].storage_bucket_url, "N/A")
  ) : "N/A"
}

output "primary_redis_endpoint" {
  description = "Primary region Redis endpoint"
  value = var.enable_redis_cache && var.cloud_provider == "aws" ? (
    try(module.aws_primary[0].redis_endpoint, "N/A")
  ) : var.enable_redis_cache && var.cloud_provider == "azure" ? (
    try(module.azure_primary[0].redis_hostname, "N/A")
  ) : var.enable_redis_cache && var.cloud_provider == "gcp" ? (
    try(module.gcp_primary[0].redis_host, "N/A")
  ) : "N/A"
  sensitive = true
}

# ============================================================================
# DR Region Outputs
# ============================================================================

output "dr_region" {
  description = "DR region"
  value       = var.dr_region
}

output "dr_endpoint" {
  description = "DR region application endpoint"
  value = var.cloud_provider == "aws" ? (
    try(module.aws_dr[0].load_balancer_dns, "N/A")
  ) : var.cloud_provider == "azure" ? (
    try(module.azure_dr[0].app_fqdn, "N/A")
  ) : var.cloud_provider == "gcp" ? (
    try(module.gcp_dr[0].service_url, "N/A")
  ) : "N/A"
}

output "dr_db_endpoint" {
  description = "DR region database endpoint"
  value = var.enable_db_replication && var.cloud_provider == "aws" ? (
    try(module.aws_dr[0].db_endpoint, "N/A")
  ) : var.enable_db_replication && var.cloud_provider == "azure" ? (
    try(module.azure_dr[0].db_fqdn, "N/A")
  ) : var.enable_db_replication && var.cloud_provider == "gcp" ? (
    try(module.gcp_dr[0].db_connection_name, "N/A")
  ) : "N/A"
  sensitive = true
}

output "dr_storage_endpoint" {
  description = "DR region storage endpoint"
  value = var.enable_storage_replication && var.cloud_provider == "aws" ? (
    try(module.aws_dr[0].storage_bucket_regional_domain, "N/A")
  ) : var.enable_storage_replication && var.cloud_provider == "azure" ? (
    try(module.azure_primary[0].storage_secondary_endpoint, "N/A")
  ) : var.enable_storage_replication && var.cloud_provider == "gcp" ? (
    try(module.gcp_dr[0].storage_bucket_url, "N/A")
  ) : "N/A"
}

# ============================================================================
# Resource Identifiers
# ============================================================================

output "primary_db_identifier" {
  description = "Primary database identifier"
  value = var.cloud_provider == "aws" ? (
    try(module.aws_primary[0].db_identifier, "N/A")
  ) : var.cloud_provider == "azure" ? (
    try(module.azure_primary[0].db_server_name, "N/A")
  ) : var.cloud_provider == "gcp" ? (
    try(module.gcp_primary[0].db_instance_name, "N/A")
  ) : "N/A"
}

output "dr_db_identifier" {
  description = "DR database identifier"
  value = var.enable_db_replication && var.cloud_provider == "aws" ? (
    try(module.aws_dr[0].db_identifier, "N/A")
  ) : var.enable_db_replication && var.cloud_provider == "azure" ? (
    try(module.azure_dr[0].db_server_name, "N/A")
  ) : var.enable_db_replication && var.cloud_provider == "gcp" ? (
    try(module.gcp_dr[0].db_instance_name, "N/A")
  ) : "N/A"
}

output "primary_storage_bucket" {
  description = "Primary storage bucket name"
  value = var.cloud_provider == "aws" ? (
    try(module.aws_primary[0].storage_bucket_id, "N/A")
  ) : var.cloud_provider == "azure" ? (
    try(module.azure_primary[0].storage_account_name, "N/A")
  ) : var.cloud_provider == "gcp" ? (
    try(module.gcp_primary[0].storage_bucket_name, "N/A")
  ) : "N/A"
}

output "dr_storage_bucket" {
  description = "DR storage bucket name"
  value = var.enable_storage_replication && var.cloud_provider == "aws" ? (
    try(module.aws_dr[0].storage_bucket_id, "N/A")
  ) : var.enable_storage_replication && var.cloud_provider == "azure" ? (
    try(module.azure_primary[0].storage_account_name, "N/A") # Same account for Azure GRS
  ) : var.enable_storage_replication && var.cloud_provider == "gcp" ? (
    try(module.gcp_dr[0].storage_bucket_name, "N/A")
  ) : "N/A"
}

# ============================================================================
# Health Check Identifiers
# ============================================================================

output "primary_health_check_id" {
  description = "Primary region health check ID"
  value = var.cloud_provider == "aws" && var.enable_global_lb ? (
    try(module.aws_global_lb[0].primary_health_check_id, "N/A")
  ) : "N/A"
}

output "dr_health_check_id" {
  description = "DR region health check ID"
  value = var.cloud_provider == "aws" && var.enable_global_lb ? (
    try(module.aws_global_lb[0].dr_health_check_id, "N/A")
  ) : "N/A"
}

# ============================================================================
# Monitoring
# ============================================================================

output "monitoring_dashboard_url" {
  description = "Monitoring dashboard URL"
  value = var.enable_monitoring && var.cloud_provider == "aws" ? (
    "https://console.aws.amazon.com/cloudwatch/home?region=${var.primary_region}#dashboards:name=${var.project_name}-${var.environment}"
  ) : var.enable_monitoring && var.cloud_provider == "azure" ? (
    "https://portal.azure.com/#@/dashboard/arm/subscriptions/${try(data.azurerm_client_config.current[0].subscription_id, "N/A")}"
  ) : var.enable_monitoring && var.cloud_provider == "gcp" ? (
    "https://console.cloud.google.com/monitoring/dashboards?project=${var.project_name}"
  ) : "N/A"
}

# ============================================================================
# Connection Information
# ============================================================================

output "connection_info" {
  description = "Connection information for all services"
  value = {
    global = {
      endpoint = var.enable_global_lb ? (
        var.cloud_provider == "aws" && var.domain_name != null ?
          "https://${var.subdomain}.${var.domain_name}" :
        var.cloud_provider == "aws" ?
          try(module.aws_global_lb[0].load_balancer_dns, "N/A") :
        var.cloud_provider == "azure" ?
          try(module.azure_global_lb[0].front_door_endpoint, "N/A") :
        var.cloud_provider == "gcp" ?
          try(module.gcp_global_lb[0].load_balancer_ip, "N/A") :
          "N/A"
      ) : "N/A"
    }
    primary = {
      region      = var.primary_region
      app_url     = var.cloud_provider == "aws" ? try(module.aws_primary[0].load_balancer_dns, "N/A") : var.cloud_provider == "azure" ? try(module.azure_primary[0].app_fqdn, "N/A") : var.cloud_provider == "gcp" ? try(module.gcp_primary[0].service_url, "N/A") : "N/A"
      db_host     = var.cloud_provider == "aws" ? try(module.aws_primary[0].db_endpoint, "N/A") : var.cloud_provider == "azure" ? try(module.azure_primary[0].db_fqdn, "N/A") : var.cloud_provider == "gcp" ? try(module.gcp_primary[0].db_connection_name, "N/A") : "N/A"
      storage     = var.cloud_provider == "aws" ? try(module.aws_primary[0].storage_bucket_id, "N/A") : var.cloud_provider == "azure" ? try(module.azure_primary[0].storage_account_name, "N/A") : var.cloud_provider == "gcp" ? try(module.gcp_primary[0].storage_bucket_name, "N/A") : "N/A"
    }
    dr = {
      region      = var.dr_region
      app_url     = var.cloud_provider == "aws" ? try(module.aws_dr[0].load_balancer_dns, "N/A") : var.cloud_provider == "azure" ? try(module.azure_dr[0].app_fqdn, "N/A") : var.cloud_provider == "gcp" ? try(module.gcp_dr[0].service_url, "N/A") : "N/A"
      db_host     = var.enable_db_replication && var.cloud_provider == "aws" ? try(module.aws_dr[0].db_endpoint, "N/A") : var.enable_db_replication && var.cloud_provider == "azure" ? try(module.azure_dr[0].db_fqdn, "N/A") : var.enable_db_replication && var.cloud_provider == "gcp" ? try(module.gcp_dr[0].db_connection_name, "N/A") : "N/A"
      storage     = var.enable_storage_replication && var.cloud_provider == "aws" ? try(module.aws_dr[0].storage_bucket_id, "N/A") : var.enable_storage_replication && var.cloud_provider == "gcp" ? try(module.gcp_dr[0].storage_bucket_name, "N/A") : "N/A"
    }
  }
  sensitive = true
}

# ============================================================================
# Deployment Summary
# ============================================================================

output "deployment_summary" {
  description = "Complete deployment summary"
  value = {
    cloud_provider = var.cloud_provider
    environment    = var.environment
    project_name   = var.project_name
    regions = {
      primary = var.primary_region
      dr      = var.dr_region
    }
    features = {
      multi_region           = true
      db_replication         = var.enable_db_replication
      storage_replication    = var.enable_storage_replication
      redis_replication      = var.enable_redis_replication
      global_load_balancer   = var.enable_global_lb
      auto_scaling          = var.enable_auto_scaling
      monitoring            = var.enable_monitoring
      waf                   = var.enable_waf
      cdn                   = var.enable_cdn
    }
    capacity = {
      primary_instances = var.primary_instance_count
      dr_instances      = var.dr_instance_count
      min_instances     = var.min_instances
      max_instances     = var.max_instances
    }
    sla = {
      rto_minutes       = var.rto_minutes
      rpo_seconds       = var.rpo_seconds
      target_availability = var.failover_routing_policy == "latency" ? "99.95%" : "99.99%"
    }
  }
}

# ============================================================================
# Testing and Validation
# ============================================================================

output "validation_commands" {
  description = "Commands to validate deployment"
  value = {
    test_global_endpoint = var.enable_global_lb ? "curl -I ${var.cloud_provider == "aws" && var.domain_name != null ? "https://${var.subdomain}.${var.domain_name}" : var.cloud_provider == "aws" ? try(module.aws_global_lb[0].load_balancer_dns, "N/A") : var.cloud_provider == "azure" ? try(module.azure_global_lb[0].front_door_endpoint, "N/A") : try(module.gcp_global_lb[0].load_balancer_ip, "N/A")}/health" : "N/A"
    test_primary_endpoint = "curl -I ${var.cloud_provider == "aws" ? try(module.aws_primary[0].load_balancer_dns, "N/A") : var.cloud_provider == "azure" ? try(module.azure_primary[0].app_fqdn, "N/A") : try(module.gcp_primary[0].service_url, "N/A")}/health"
    test_dr_endpoint = "curl -I ${var.cloud_provider == "aws" ? try(module.aws_dr[0].load_balancer_dns, "N/A") : var.cloud_provider == "azure" ? try(module.azure_dr[0].app_fqdn, "N/A") : try(module.gcp_dr[0].service_url, "N/A")}/health"
    check_replication = var.enable_db_replication && var.cloud_provider == "aws" ? "aws rds describe-db-instances --db-instance-identifier ${try(module.aws_dr[0].db_identifier, "N/A")} --query 'DBInstances[0].StatusInfos'" : "N/A"
  }
}

# ============================================================================
# Failover Procedures
# ============================================================================

output "failover_info" {
  description = "Information for failover procedures"
  value = {
    documentation     = "See FAILOVER.md for detailed procedures"
    automated_failover = var.enable_global_lb ? "Enabled via health checks" : "Disabled"
    health_check_interval = "${var.health_check_interval} seconds"
    health_check_threshold = "${var.health_check_threshold} failures"
    estimated_failover_time = "${var.rto_minutes} minutes"
    estimated_data_loss = "${var.rpo_seconds} seconds"
    primary_health_check = var.cloud_provider == "aws" && var.enable_global_lb ? try(module.aws_global_lb[0].primary_health_check_id, "N/A") : "N/A"
    dr_health_check = var.cloud_provider == "aws" && var.enable_global_lb ? try(module.aws_global_lb[0].dr_health_check_id, "N/A") : "N/A"
  }
}

# ============================================================================
# Cost Estimates
# ============================================================================

output "estimated_monthly_cost" {
  description = "Estimated monthly cost breakdown"
  value = {
    note = "Costs are estimates and may vary based on usage"
    primary_region = {
      compute  = var.cloud_provider == "aws" ? "$${var.primary_instance_count * 150}" : "$${var.primary_instance_count * 140}"
      database = var.cloud_provider == "aws" ? "$400-600" : "$350-550"
      storage  = "$10-50"
      redis    = var.enable_redis_cache ? "$80-120" : "$0"
      data_transfer = "$50-150"
    }
    dr_region = {
      compute  = var.cloud_provider == "aws" ? "$${var.dr_instance_count * 150}" : "$${var.dr_instance_count * 140}"
      database = var.enable_db_replication ? "$250-400" : "$0"
      storage  = var.enable_storage_replication ? "$5-25" : "$0"
      redis    = var.enable_redis_replication && var.enable_redis_cache ? "$50-80" : "$0"
      data_transfer = "$50-150"
    }
    global = {
      load_balancer = var.enable_global_lb ? "$30-50" : "$0"
      cdn          = var.enable_cdn ? "$20-100" : "$0"
      dns          = var.enable_global_lb ? "$10-20" : "$0"
      waf          = var.enable_waf ? "$20-50" : "$0"
    }
  }
}
