# Variables for Development Environment

variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

variable "github_org" {
  description = "GitHub organization for OIDC"
  type        = string
  default     = "HonuaIO"
}

variable "github_repo" {
  description = "GitHub repository for OIDC"
  type        = string
  default     = "honua"
}

variable "grafana_admin_password" {
  description = "Grafana admin password"
  type        = string
  sensitive   = true
}

variable "alarm_emails" {
  description = "Email addresses for alarms"
  type        = list(string)
  default     = []
}
