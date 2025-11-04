# Variables for Cloud Run Unit Tests

variable "project_id" {
  description = "GCP project ID for testing"
  type        = string
  default     = "test-project-123456"
}

variable "region" {
  description = "GCP region for testing"
  type        = string
  default     = "us-central1"
}
