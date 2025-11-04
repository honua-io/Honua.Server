# Outputs for Database Module

# AWS RDS Outputs
output "rds_instance_endpoint" {
  description = "RDS instance endpoint"
  value       = var.cloud_provider == "aws" ? aws_db_instance.honua_primary[0].endpoint : null
}

output "rds_instance_address" {
  description = "RDS instance address"
  value       = var.cloud_provider == "aws" ? aws_db_instance.honua_primary[0].address : null
}

output "rds_replica_endpoints" {
  description = "RDS read replica endpoints"
  value       = var.cloud_provider == "aws" ? aws_db_instance.honua_replica[*].endpoint : []
}

output "rds_security_group_id" {
  description = "RDS security group ID"
  value       = var.cloud_provider == "aws" ? aws_security_group.rds[0].id : null
}

# Azure PostgreSQL Outputs
output "azure_postgres_fqdn" {
  description = "Azure PostgreSQL FQDN"
  value       = var.cloud_provider == "azure" ? azurerm_postgresql_flexible_server.honua[0].fqdn : null
}

output "azure_postgres_id" {
  description = "Azure PostgreSQL ID"
  value       = var.cloud_provider == "azure" ? azurerm_postgresql_flexible_server.honua[0].id : null
}

# GCP Cloud SQL Outputs
output "gcp_sql_instance_name" {
  description = "GCP Cloud SQL instance name"
  value       = var.cloud_provider == "gcp" ? google_sql_database_instance.honua[0].name : null
}

output "gcp_sql_connection_name" {
  description = "GCP Cloud SQL connection name"
  value       = var.cloud_provider == "gcp" ? google_sql_database_instance.honua[0].connection_name : null
}

output "gcp_sql_private_ip" {
  description = "GCP Cloud SQL private IP"
  value       = var.cloud_provider == "gcp" ? google_sql_database_instance.honua[0].private_ip_address : null
}

# Common Outputs
output "database_name" {
  description = "Database name"
  value       = var.database_name
}

output "master_username" {
  description = "Master username"
  value       = var.master_username
  sensitive   = true
}

output "password_secret_arn" {
  description = "ARN/ID of the secret containing database password"
  value = var.cloud_provider == "aws" ? aws_secretsmanager_secret.db_password[0].arn : (
    var.cloud_provider == "azure" ? azurerm_key_vault_secret.db_password[0].id : (
      var.cloud_provider == "gcp" ? google_secret_manager_secret.db_password[0].id : null
    )
  )
}

output "port" {
  description = "Database port"
  value       = 5432
}
