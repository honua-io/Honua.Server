# ============================================================================
# Terraform State Backend Infrastructure - AWS
# ============================================================================
# This module creates the necessary AWS infrastructure for Terraform remote
# state storage with state locking:
#   - S3 bucket for state storage (encrypted, versioned)
#   - DynamoDB table for state locking
#   - IAM policies for state access
#
# WARNING: This must be deployed BEFORE using remote state
# Run: terraform init && terraform apply
# Then update your backend configuration to use these resources
# ============================================================================

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Initially, use local state to create the backend infrastructure
  # After creation, you can migrate to remote state
}

# ============================================================================
# Variables
# ============================================================================

variable "state_bucket_name" {
  description = "Name of the S3 bucket for Terraform state (must be globally unique)"
  type        = string
  default     = ""
}

variable "dynamodb_table_name" {
  description = "Name of the DynamoDB table for state locking"
  type        = string
  default     = "honua-terraform-locks"
}

variable "region" {
  description = "AWS region for state backend resources"
  type        = string
  default     = "us-east-1"
}

variable "environment" {
  description = "Environment name (used for tagging)"
  type        = string
  default     = "shared"
}

variable "enable_point_in_time_recovery" {
  description = "Enable Point-in-Time Recovery for DynamoDB table (recommended for prod)"
  type        = bool
  default     = true
}

variable "enable_bucket_replication" {
  description = "Enable cross-region replication for state bucket (recommended for prod)"
  type        = bool
  default     = false
}

variable "replication_region" {
  description = "Region for state bucket replication (if enabled)"
  type        = string
  default     = "us-west-2"
}

variable "force_destroy" {
  description = "Allow Terraform to destroy bucket even if it contains objects (use with caution)"
  type        = bool
  default     = false
}

# ============================================================================
# Data Sources
# ============================================================================

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  account_id = data.aws_caller_identity.current.account_id
  region     = data.aws_region.current.name

  # Generate bucket name if not provided
  state_bucket_name = coalesce(
    var.state_bucket_name,
    "honua-terraform-state-${local.account_id}-${local.region}"
  )

  tags = {
    Environment = var.environment
    Project     = "HonuaIO"
    ManagedBy   = "Terraform"
    Component   = "StateBackend"
    Purpose     = "TerraformStateStorage"
  }
}

# ============================================================================
# S3 Bucket for Terraform State
# ============================================================================

resource "aws_s3_bucket" "terraform_state" {
  bucket        = local.state_bucket_name
  force_destroy = var.force_destroy

  tags = merge(
    local.tags,
    {
      Name = local.state_bucket_name
    }
  )
}

# Enable versioning for state history and recovery
resource "aws_s3_bucket_versioning" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  versioning_configuration {
    status = "Enabled"
  }
}

# Enable server-side encryption by default
resource "aws_s3_bucket_server_side_encryption_configuration" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

# Block all public access
resource "aws_s3_bucket_public_access_block" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Enable bucket logging for audit trail
resource "aws_s3_bucket" "terraform_state_logs" {
  bucket        = "${local.state_bucket_name}-logs"
  force_destroy = var.force_destroy

  tags = merge(
    local.tags,
    {
      Name = "${local.state_bucket_name}-logs"
    }
  )
}

resource "aws_s3_bucket_logging" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  target_bucket = aws_s3_bucket.terraform_state_logs.id
  target_prefix = "state-access-logs/"
}

# Lifecycle policy to manage old versions
resource "aws_s3_bucket_lifecycle_configuration" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  rule {
    id     = "expire-old-versions"
    status = "Enabled"

    noncurrent_version_expiration {
      noncurrent_days = 90
    }

    noncurrent_version_transition {
      noncurrent_days = 30
      storage_class   = "STANDARD_IA"
    }
  }

  rule {
    id     = "delete-incomplete-uploads"
    status = "Enabled"

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}

# Bucket policy to enforce SSL/TLS
resource "aws_s3_bucket_policy" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "EnforcedTLS"
        Effect = "Deny"
        Principal = "*"
        Action = "s3:*"
        Resource = [
          aws_s3_bucket.terraform_state.arn,
          "${aws_s3_bucket.terraform_state.arn}/*"
        ]
        Condition = {
          Bool = {
            "aws:SecureTransport" = "false"
          }
        }
      }
    ]
  })
}

# ============================================================================
# Cross-Region Replication (Optional)
# ============================================================================

resource "aws_s3_bucket" "terraform_state_replica" {
  count    = var.enable_bucket_replication ? 1 : 0
  provider = aws.replica
  bucket   = "${local.state_bucket_name}-replica"

  force_destroy = var.force_destroy

  tags = merge(
    local.tags,
    {
      Name = "${local.state_bucket_name}-replica"
    }
  )
}

resource "aws_s3_bucket_versioning" "terraform_state_replica" {
  count    = var.enable_bucket_replication ? 1 : 0
  provider = aws.replica
  bucket   = aws_s3_bucket.terraform_state_replica[0].id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_iam_role" "replication" {
  count = var.enable_bucket_replication ? 1 : 0
  name  = "honua-terraform-state-replication"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "s3.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = local.tags
}

resource "aws_iam_role_policy" "replication" {
  count = var.enable_bucket_replication ? 1 : 0
  role  = aws_iam_role.replication[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:GetReplicationConfiguration",
          "s3:ListBucket"
        ]
        Resource = aws_s3_bucket.terraform_state.arn
      },
      {
        Effect = "Allow"
        Action = [
          "s3:GetObjectVersionForReplication",
          "s3:GetObjectVersionAcl",
          "s3:GetObjectVersionTagging"
        ]
        Resource = "${aws_s3_bucket.terraform_state.arn}/*"
      },
      {
        Effect = "Allow"
        Action = [
          "s3:ReplicateObject",
          "s3:ReplicateDelete",
          "s3:ReplicateTags"
        ]
        Resource = "${aws_s3_bucket.terraform_state_replica[0].arn}/*"
      }
    ]
  })
}

resource "aws_s3_bucket_replication_configuration" "terraform_state" {
  count = var.enable_bucket_replication ? 1 : 0
  depends_on = [
    aws_s3_bucket_versioning.terraform_state,
    aws_s3_bucket_versioning.terraform_state_replica[0]
  ]

  role   = aws_iam_role.replication[0].arn
  bucket = aws_s3_bucket.terraform_state.id

  rule {
    id     = "replicate-all"
    status = "Enabled"

    destination {
      bucket        = aws_s3_bucket.terraform_state_replica[0].arn
      storage_class = "STANDARD_IA"
    }
  }
}

# ============================================================================
# DynamoDB Table for State Locking
# ============================================================================

resource "aws_dynamodb_table" "terraform_locks" {
  name         = var.dynamodb_table_name
  billing_mode = "PAY_PER_REQUEST" # On-demand pricing (cost-effective for low usage)
  hash_key     = "LockID"

  attribute {
    name = "LockID"
    type = "S"
  }

  # Enable point-in-time recovery for production
  point_in_time_recovery {
    enabled = var.enable_point_in_time_recovery
  }

  # Enable encryption at rest
  server_side_encryption {
    enabled = true
  }

  tags = merge(
    local.tags,
    {
      Name = var.dynamodb_table_name
    }
  )
}

# ============================================================================
# IAM Policy for Terraform State Access
# ============================================================================

data "aws_iam_policy_document" "terraform_state_access" {
  # S3 bucket access
  statement {
    sid    = "TerraformStateS3Access"
    effect = "Allow"
    actions = [
      "s3:ListBucket",
      "s3:GetBucketVersioning"
    ]
    resources = [aws_s3_bucket.terraform_state.arn]
  }

  statement {
    sid    = "TerraformStateS3ObjectAccess"
    effect = "Allow"
    actions = [
      "s3:GetObject",
      "s3:PutObject",
      "s3:DeleteObject"
    ]
    resources = ["${aws_s3_bucket.terraform_state.arn}/*"]
  }

  # DynamoDB table access
  statement {
    sid    = "TerraformStateLockAccess"
    effect = "Allow"
    actions = [
      "dynamodb:DescribeTable",
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:DeleteItem"
    ]
    resources = [aws_dynamodb_table.terraform_locks.arn]
  }
}

resource "aws_iam_policy" "terraform_state_access" {
  name        = "HonuaTerraformStateAccess"
  description = "IAM policy for accessing Honua Terraform state backend"
  policy      = data.aws_iam_policy_document.terraform_state_access.json

  tags = local.tags
}

# ============================================================================
# Outputs
# ============================================================================

output "state_bucket_name" {
  description = "Name of the S3 bucket for Terraform state"
  value       = aws_s3_bucket.terraform_state.id
}

output "state_bucket_arn" {
  description = "ARN of the S3 bucket for Terraform state"
  value       = aws_s3_bucket.terraform_state.arn
}

output "state_bucket_region" {
  description = "Region of the S3 bucket for Terraform state"
  value       = aws_s3_bucket.terraform_state.region
}

output "dynamodb_table_name" {
  description = "Name of the DynamoDB table for state locking"
  value       = aws_dynamodb_table.terraform_locks.id
}

output "dynamodb_table_arn" {
  description = "ARN of the DynamoDB table for state locking"
  value       = aws_dynamodb_table.terraform_locks.arn
}

output "iam_policy_arn" {
  description = "ARN of the IAM policy for state access"
  value       = aws_iam_policy.terraform_state_access.arn
}

output "replica_bucket_name" {
  description = "Name of the replica S3 bucket (if replication is enabled)"
  value       = var.enable_bucket_replication ? aws_s3_bucket.terraform_state_replica[0].id : null
}

output "backend_config" {
  description = "Backend configuration to use in other Terraform projects"
  value = {
    bucket         = aws_s3_bucket.terraform_state.id
    region         = local.region
    dynamodb_table = aws_dynamodb_table.terraform_locks.id
    encrypt        = true
  }
}

output "backend_config_hcl" {
  description = "Ready-to-use backend configuration in HCL format"
  value = <<-EOT
    terraform {
      backend "s3" {
        bucket         = "${aws_s3_bucket.terraform_state.id}"
        key            = "path/to/terraform.tfstate"  # Update this for each project
        region         = "${local.region}"
        encrypt        = true
        dynamodb_table = "${aws_dynamodb_table.terraform_locks.id}"
      }
    }
  EOT
}
