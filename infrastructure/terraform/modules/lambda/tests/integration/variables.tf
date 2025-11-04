# Variables for Lambda Integration Tests

variable "aws_region" {
  description = "AWS region for integration testing"
  type        = string
  default     = "us-east-1"
}

variable "container_image_uri" {
  description = "Container image URI for testing"
  type        = string
  default     = "123456789012.dkr.ecr.us-east-1.amazonaws.com/honua:latest"
}

variable "skip_aws_validation" {
  description = "Skip AWS validation for testing without credentials"
  type        = bool
  default     = true
}
