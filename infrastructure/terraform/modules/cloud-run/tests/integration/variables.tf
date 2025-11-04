# Variables for Cloud Run Integration Tests

variable "project_id" {
  description = "GCP project ID for integration testing"
  type        = string
  default     = "test-project-123456"
}

variable "region" {
  description = "GCP region for testing"
  type        = string
  default     = "us-central1"
}

variable "container_image" {
  description = "Container image for testing"
  type        = string
  default     = "gcr.io/cloudrun/hello"
}

variable "vpc_network_name" {
  description = "VPC network name for testing"
  type        = string
  default     = "default"
}

variable "vpc_network_id" {
  description = "VPC network ID for testing"
  type        = string
  default     = ""
}

variable "vpc_connector_cidr" {
  description = "CIDR for VPC connector"
  type        = string
  default     = "10.8.0.0/28"
}
