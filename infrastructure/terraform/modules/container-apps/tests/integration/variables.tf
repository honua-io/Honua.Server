# Variables for Container Apps Integration Tests

variable "location" {
  description = "Azure location for integration testing"
  type        = string
  default     = "eastus"
}

variable "container_image" {
  description = "Container image for testing"
  type        = string
  default     = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
}

variable "skip_azure_validation" {
  description = "Skip Azure validation for testing without credentials"
  type        = bool
  default     = false
}
