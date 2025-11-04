# Variables for Production Environment

variable "aws_region" {
  description = "Primary AWS region"
  type        = string
  default     = "us-east-1"
}

variable "dr_region" {
  description = "Disaster recovery AWS region"
  type        = string
  default     = "us-west-2"
}

variable "allowed_public_cidrs" {
  description = "CIDR blocks allowed to access the cluster API"
  type        = list(string)
  default     = ["10.0.0.0/8"] # Internal only
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

variable "customer_repositories" {
  description = "Customer-specific repository mappings"
  type        = map(string)
  default     = {}
}

variable "customer_users" {
  description = "Customer IAM user mappings"
  type        = map(string)
  default     = {}
}
