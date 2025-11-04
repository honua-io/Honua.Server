# Outputs for Registry Module

# AWS ECR Outputs
output "ecr_repository_urls" {
  description = "ECR repository URLs"
  value = var.cloud_provider == "aws" ? {
    for k, v in aws_ecr_repository.honua : k => v.repository_url
  } : {}
}

output "ecr_repository_arns" {
  description = "ECR repository ARNs"
  value = var.cloud_provider == "aws" ? {
    for k, v in aws_ecr_repository.honua : k => v.arn
  } : {}
}

output "ecr_customer_repository_urls" {
  description = "Customer-specific ECR repository URLs"
  value = var.cloud_provider == "aws" && var.enable_customer_registries ? {
    for k, v in aws_ecr_repository.customer : k => v.repository_url
  } : {}
}

# Azure ACR Outputs
output "acr_login_server" {
  description = "ACR login server"
  value       = var.cloud_provider == "azure" ? azurerm_container_registry.honua[0].login_server : null
}

output "acr_id" {
  description = "ACR resource ID"
  value       = var.cloud_provider == "azure" ? azurerm_container_registry.honua[0].id : null
}

output "acr_identity_principal_id" {
  description = "ACR managed identity principal ID"
  value       = var.cloud_provider == "azure" ? azurerm_container_registry.honua[0].identity[0].principal_id : null
}

# GCP Artifact Registry Outputs
output "gar_repository_ids" {
  description = "GCP Artifact Registry repository IDs"
  value = var.cloud_provider == "gcp" ? {
    for k, v in google_artifact_registry_repository.honua : k => v.id
  } : {}
}

output "gar_repository_urls" {
  description = "GCP Artifact Registry repository URLs"
  value = var.cloud_provider == "gcp" ? {
    for k, v in google_artifact_registry_repository.honua : k => "${var.gcp_region}-docker.pkg.dev/${var.gcp_project_id}/${v.repository_id}"
  } : {}
}

# Common output
output "registry_prefix" {
  description = "Registry prefix"
  value       = var.registry_prefix
}
