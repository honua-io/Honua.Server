# Variables for GCP Development Environment

variable "project_id" {
  description = "GCP project ID"
  type        = string
}

variable "region" {
  description = "GCP region"
  type        = string
  default     = "us-central1"
}

variable "container_image" {
  description = "Container image (e.g., gcr.io/PROJECT/honua:dev)"
  type        = string
}
