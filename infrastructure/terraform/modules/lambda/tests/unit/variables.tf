# Variables for Lambda Unit Tests

variable "aws_region" {
  description = "AWS region for testing"
  type        = string
  default     = "us-east-1"
}

variable "container_image_uri" {
  description = "Container image URI for testing"
  type        = string
  default     = "123456789012.dkr.ecr.us-east-1.amazonaws.com/honua:test"
}
