# Outputs for Monitoring Module

output "prometheus_endpoint" {
  description = "Prometheus endpoint"
  value       = "http://prometheus-kube-prometheus-prometheus.monitoring:9090"
}

output "grafana_endpoint" {
  description = "Grafana endpoint"
  value       = "http://prometheus-grafana.monitoring:80"
}

# AWS CloudWatch Outputs
output "log_group_name" {
  description = "CloudWatch log group name"
  value       = var.cloud_provider == "aws" ? aws_cloudwatch_log_group.application[0].name : null
}

output "sns_topic_arn" {
  description = "SNS topic ARN for alarms"
  value       = var.cloud_provider == "aws" && var.create_sns_topic ? aws_sns_topic.alarms[0].arn : null
}

output "dashboard_url" {
  description = "CloudWatch dashboard URL"
  value       = var.cloud_provider == "aws" ? "https://console.aws.amazon.com/cloudwatch/home?region=${var.aws_region}#dashboards:name=${aws_cloudwatch_dashboard.honua[0].dashboard_name}" : null
}

# Azure Monitor Outputs
output "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID"
  value       = var.cloud_provider == "azure" ? azurerm_log_analytics_workspace.honua[0].id : null
}

output "application_insights_id" {
  description = "Application Insights ID"
  value       = var.cloud_provider == "azure" ? azurerm_application_insights.honua[0].id : null
}

output "application_insights_instrumentation_key" {
  description = "Application Insights instrumentation key"
  value       = var.cloud_provider == "azure" ? azurerm_application_insights.honua[0].instrumentation_key : null
  sensitive   = true
}

# GCP Monitoring Outputs
output "notification_channel_ids" {
  description = "GCP notification channel IDs"
  value       = var.cloud_provider == "gcp" ? google_monitoring_notification_channel.email[*].id : []
}
