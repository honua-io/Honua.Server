# Placeholder GCP Global Load Balancer Module
variable "project_name" { type = string }
variable "environment" { type = string }
variable "unique_suffix" { type = string }
variable "primary_backend" { type = string }
variable "primary_region" { type = string }
variable "dr_backend" { type = string }
variable "dr_region" { type = string }
variable "health_check_path" { type = string
  default = "/health"
}
variable "check_interval_sec" { type = number
  default = 30
}
variable "timeout_sec" { type = number
  default = 10
}
variable "unhealthy_threshold" { type = number
  default = 3
}
variable "enable_cdn" { type = bool
  default = true
}
variable "enable_ssl" { type = bool
  default = true
}
variable "tags" {
  type = map(string)
  default = {}
}

output "load_balancer_ip" { value = "203.0.113.1" }
output "forwarding_rule_name" { value = "${var.project_name}-${var.environment}-global-lb" }
