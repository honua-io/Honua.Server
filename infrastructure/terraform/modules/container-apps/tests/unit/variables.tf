# Variables for Container Apps Unit Tests

variable "location" {
  description = "Azure location for testing"
  type        = string
  default     = "eastus"
}

variable "container_image" {
  description = "Container image for testing"
  type        = string
  default     = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
}
