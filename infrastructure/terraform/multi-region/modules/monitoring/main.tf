# Placeholder Monitoring Module
variable "cloud_provider" { type = string }
variable "environment" { type = string }
variable "project_name" { type = string }
variable "alert_email" { type = string
  default = null
}
variable "alert_slack_webhook" {
  type = string
  default = null
  sensitive = true
}
variable "alert_pagerduty_key" {
  type = string
  default = null
  sensitive = true
}
variable "replication_lag_threshold" { type = number
  default = 60
}
variable "error_rate_threshold" { type = number
  default = 1.0
}
variable "latency_threshold_ms" { type = number
  default = 500
}
variable "primary_resources" { type = any
  default = null
}
variable "dr_resources" { type = any
  default = null
}
variable "tags" {
  type = map(string)
  default = {}
}

output "dashboard_url" { value = "https://monitoring.example.com/dashboard" }
output "alert_topic" { value = "arn:aws:sns:us-east-1:123456789012:honua-alerts" }
