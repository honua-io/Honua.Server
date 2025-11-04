# Placeholder AWS Global Load Balancer Module
variable "project_name" { type = string }
variable "environment" { type = string }
variable "unique_suffix" { type = string }
variable "domain_name" { type = string
  default = null
}
variable "subdomain" { type = string
  default = "api"
}
variable "create_dns_zone" { type = bool
  default = false
}
variable "dns_zone_id" { type = string
  default = null
}
variable "dns_ttl" { type = number
  default = 60
}
variable "primary_region" { type = string }
variable "primary_endpoint" { type = string }
variable "primary_hosted_zone_id" { type = string }
variable "dr_region" { type = string }
variable "dr_endpoint" { type = string }
variable "dr_hosted_zone_id" { type = string }
variable "health_check_path" { type = string
  default = "/health"
}
variable "health_check_interval" { type = number
  default = 30
}
variable "health_check_timeout" { type = number
  default = 10
}
variable "health_check_threshold" { type = number
  default = 3
}
variable "routing_policy" { type = string
  default = "latency"
}
variable "enable_cdn" { type = bool
  default = true
}
variable "tags" {
  type = map(string)
  default = {}
}

output "load_balancer_dns" { value = "global-lb.example.com" }
output "hosted_zone_name" { value = "example.com" }
output "primary_health_check_id" { value = "hc-primary-123" }
output "dr_health_check_id" { value = "hc-dr-456" }
