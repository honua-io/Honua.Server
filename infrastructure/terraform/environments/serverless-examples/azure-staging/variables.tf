# Variables for Azure Staging Environment

variable "location" {
  description = "Azure region"
  type        = string
  default     = "eastus"
}

variable "container_image" {
  description = "Container image"
  type        = string
}

variable "custom_domains" {
  description = "Custom domains for Front Door"
  type        = list(string)
  default     = []
}

variable "cors_origins" {
  description = "CORS origins"
  type        = list(string)
  default     = []
}
