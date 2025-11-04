# Placeholder Azure Global Load Balancer Module
variable "project_name" { type = string }
variable "environment" { type = string }
variable "unique_suffix" { type = string }
variable "resource_group_name" { type = string }
variable "primary_backend" { type = string }
variable "primary_location" { type = string }
variable "dr_backend" { type = string }
variable "dr_location" { type = string }
variable "health_probe_path" { type = string
  default = "/health"
}
variable "health_probe_interval" { type = number
  default = 30
}
variable "health_probe_threshold" { type = number
  default = 3
}
variable "enable_waf" { type = bool
  default = true
}
variable "enable_cdn" { type = bool
  default = true
}
variable "tags" {
  type = map(string)
  default = {}
}

output "front_door_name" { value = "${var.project_name}-${var.environment}-fd" }
output "front_door_endpoint" { value = "https://${var.project_name}-${var.environment}-fd.azurefd.net" }
output "front_door_id" { value = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/${var.resource_group_name}/providers/Microsoft.Network/frontDoors/${var.project_name}-${var.environment}-fd" }
