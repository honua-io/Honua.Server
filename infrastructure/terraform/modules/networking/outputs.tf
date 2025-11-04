# Outputs for Networking Module

# AWS VPC Outputs
output "vpc_id" {
  description = "VPC ID"
  value = var.cloud_provider == "aws" ? aws_vpc.honua[0].id : (
    var.cloud_provider == "azure" ? azurerm_virtual_network.honua[0].id : (
      var.cloud_provider == "gcp" ? google_compute_network.honua[0].id : null
    )
  )
}

output "vpc_cidr" {
  description = "VPC CIDR block"
  value       = var.vpc_cidr
}

output "public_subnet_ids" {
  description = "Public subnet IDs"
  value = var.cloud_provider == "aws" ? aws_subnet.public[*].id : (
    var.cloud_provider == "azure" ? azurerm_subnet.public[*].id : (
      var.cloud_provider == "gcp" ? google_compute_subnetwork.public[*].id : []
    )
  )
}

output "private_subnet_ids" {
  description = "Private subnet IDs"
  value = var.cloud_provider == "aws" ? aws_subnet.private[*].id : (
    var.cloud_provider == "azure" ? azurerm_subnet.private[*].id : (
      var.cloud_provider == "gcp" ? google_compute_subnetwork.private[*].id : []
    )
  )
}

output "database_subnet_ids" {
  description = "Database subnet IDs"
  value = var.cloud_provider == "aws" ? aws_subnet.database[*].id : (
    var.cloud_provider == "azure" ? azurerm_subnet.database[*].id : []
  )
}

output "nat_gateway_ips" {
  description = "NAT Gateway public IPs"
  value = var.cloud_provider == "aws" ? aws_eip.nat[*].public_ip : (
    var.cloud_provider == "azure" ? azurerm_public_ip.nat[*].ip_address : []
  )
}

# AWS-specific outputs
output "internet_gateway_id" {
  description = "Internet Gateway ID"
  value       = var.cloud_provider == "aws" ? aws_internet_gateway.honua[0].id : null
}

# Azure-specific outputs
output "network_security_group_id" {
  description = "Network Security Group ID"
  value       = var.cloud_provider == "azure" ? azurerm_network_security_group.honua[0].id : null
}

# GCP-specific outputs
output "gcp_network_self_link" {
  description = "GCP network self link"
  value       = var.cloud_provider == "gcp" ? google_compute_network.honua[0].self_link : null
}
