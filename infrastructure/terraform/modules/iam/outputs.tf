# Outputs for IAM Module

# AWS IAM Outputs
output "orchestrator_role_arn" {
  description = "Orchestrator IAM role ARN"
  value = var.cloud_provider == "aws" ? aws_iam_role.orchestrator[0].arn : (
    var.cloud_provider == "azure" ? azurerm_user_assigned_identity.orchestrator[0].id : (
      var.cloud_provider == "gcp" ? google_service_account.orchestrator[0].email : null
    )
  )
}

output "k8s_service_account_roles" {
  description = "Kubernetes service account IAM roles"
  value = var.cloud_provider == "aws" ? {
    for k, v in aws_iam_role.k8s_service_account : k => v.arn
  } : {}
}

output "github_actions_role_arn" {
  description = "GitHub Actions IAM role ARN"
  value       = var.cloud_provider == "aws" && var.enable_github_oidc ? aws_iam_role.github_actions[0].arn : null
}

output "customer_credentials_secrets" {
  description = "Customer credentials secret ARNs/IDs"
  value = var.cloud_provider == "aws" && var.enable_customer_iam ? {
    for k, v in aws_secretsmanager_secret.customer_credentials : k => v.arn
  } : (
    var.cloud_provider == "azure" && var.enable_customer_iam ? {
      for k, v in azurerm_key_vault_secret.customer_credentials : k => v.id
    } : (
      var.cloud_provider == "gcp" && var.enable_customer_iam ? {
        for k, v in google_secret_manager_secret.customer_credentials : k => v.id
      } : {}
    )
  )
}

# AWS-specific outputs
output "customer_user_arns" {
  description = "Customer IAM user ARNs"
  value = var.cloud_provider == "aws" && var.enable_customer_iam ? {
    for k, v in aws_iam_user.customer : k => v.arn
  } : {}
}

# Azure-specific outputs
output "orchestrator_principal_id" {
  description = "Azure orchestrator principal ID"
  value       = var.cloud_provider == "azure" ? azurerm_user_assigned_identity.orchestrator[0].principal_id : null
}

output "customer_service_principal_ids" {
  description = "Customer service principal IDs"
  value = var.cloud_provider == "azure" && var.enable_customer_iam ? {
    for k, v in azuread_service_principal.customer : k => v.object_id
  } : {}
}

# GCP-specific outputs
output "orchestrator_service_account_email" {
  description = "GCP orchestrator service account email"
  value       = var.cloud_provider == "gcp" ? google_service_account.orchestrator[0].email : null
}

output "customer_service_account_emails" {
  description = "Customer service account emails"
  value = var.cloud_provider == "gcp" && var.enable_customer_iam ? {
    for k, v in google_service_account.customer : k => v.email
  } : {}
}
