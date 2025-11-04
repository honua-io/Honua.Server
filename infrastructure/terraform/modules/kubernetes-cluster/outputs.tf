# Outputs for Kubernetes Cluster Module

# AWS EKS Outputs
output "eks_cluster_id" {
  description = "EKS cluster ID"
  value       = var.cloud_provider == "aws" ? aws_eks_cluster.honua[0].id : null
}

output "eks_cluster_endpoint" {
  description = "EKS cluster endpoint"
  value       = var.cloud_provider == "aws" ? aws_eks_cluster.honua[0].endpoint : null
}

output "eks_cluster_certificate_authority" {
  description = "EKS cluster certificate authority"
  value       = var.cloud_provider == "aws" ? aws_eks_cluster.honua[0].certificate_authority[0].data : null
  sensitive   = true
}

output "eks_cluster_security_group_id" {
  description = "EKS cluster security group ID"
  value       = var.cloud_provider == "aws" ? aws_security_group.eks_cluster[0].id : null
}

output "eks_node_role_arn" {
  description = "EKS node IAM role ARN"
  value       = var.cloud_provider == "aws" ? aws_iam_role.eks_node_group[0].arn : null
}

# Azure AKS Outputs
output "aks_cluster_id" {
  description = "AKS cluster ID"
  value       = var.cloud_provider == "azure" ? azurerm_kubernetes_cluster.honua[0].id : null
}

output "aks_cluster_fqdn" {
  description = "AKS cluster FQDN"
  value       = var.cloud_provider == "azure" ? azurerm_kubernetes_cluster.honua[0].fqdn : null
}

output "aks_kube_config" {
  description = "AKS kubeconfig"
  value       = var.cloud_provider == "azure" ? azurerm_kubernetes_cluster.honua[0].kube_config_raw : null
  sensitive   = true
}

output "aks_principal_id" {
  description = "AKS managed identity principal ID"
  value       = var.cloud_provider == "azure" ? azurerm_kubernetes_cluster.honua[0].identity[0].principal_id : null
}

# GCP GKE Outputs
output "gke_cluster_id" {
  description = "GKE cluster ID"
  value       = var.cloud_provider == "gcp" ? google_container_cluster.honua[0].id : null
}

output "gke_cluster_endpoint" {
  description = "GKE cluster endpoint"
  value       = var.cloud_provider == "gcp" ? google_container_cluster.honua[0].endpoint : null
}

output "gke_cluster_ca_certificate" {
  description = "GKE cluster CA certificate"
  value       = var.cloud_provider == "gcp" ? google_container_cluster.honua[0].master_auth[0].cluster_ca_certificate : null
  sensitive   = true
}

# Common Outputs
output "cluster_name" {
  description = "Kubernetes cluster name"
  value       = "${var.cluster_name}-${var.environment}"
}

output "cluster_version" {
  description = "Kubernetes version"
  value       = var.kubernetes_version
}
