# ============================================================================
# AWS Secret Rotation Infrastructure
# ============================================================================
# Terraform configuration for automated secret rotation in AWS
#
# Resources:
# - Lambda function for rotation logic
# - EventBridge schedule for automatic rotation
# - SNS topic for notifications
# - IAM roles and policies
# - Secrets Manager secrets with rotation enabled
# ============================================================================

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.4"
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "prod"
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "honua"
}

variable "rotation_days" {
  description = "Number of days between automatic rotations"
  type        = number
  default     = 90
}

variable "notification_email" {
  description = "Email address for rotation notifications"
  type        = string
}

variable "postgres_host" {
  description = "PostgreSQL server hostname"
  type        = string
}

variable "postgres_port" {
  description = "PostgreSQL server port"
  type        = number
  default     = 5432
}

variable "postgres_database" {
  description = "PostgreSQL database name"
  type        = string
  default     = "honua"
}

variable "postgres_master_username" {
  description = "PostgreSQL master username (for password rotation)"
  type        = string
  sensitive   = true
}

variable "postgres_master_password" {
  description = "PostgreSQL master password (for password rotation)"
  type        = string
  sensitive   = true
}

variable "postgres_app_username" {
  description = "PostgreSQL application username"
  type        = string
  default     = "honua_app"
}

variable "api_endpoint" {
  description = "API endpoint for testing rotated API keys"
  type        = string
}

variable "tags" {
  description = "Additional tags for resources"
  type        = map(string)
  default     = {}
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  function_name = "${var.project_name}-secret-rotation-${var.environment}"

  common_tags = merge(
    var.tags,
    {
      Environment = var.environment
      Project     = var.project_name
      ManagedBy   = "Terraform"
      Component   = "SecretRotation"
    }
  )
}

# ============================================================================
# Lambda Function Package
# ============================================================================

data "archive_file" "lambda_package" {
  type        = "zip"
  source_dir  = "${path.module}/../../functions/secret-rotation/aws"
  output_path = "${path.module}/lambda-package.zip"

  excludes = [
    "node_modules",
    "*.zip",
    ".git"
  ]
}

# ============================================================================
# IAM Role for Lambda
# ============================================================================

resource "aws_iam_role" "lambda_role" {
  name = "${local.function_name}-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })

  tags = local.common_tags
}

# IAM Policy for Lambda
resource "aws_iam_role_policy" "lambda_policy" {
  name = "${local.function_name}-policy"
  role = aws_iam_role.lambda_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "arn:aws:logs:*:*:*"
      },
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret",
          "secretsmanager:PutSecretValue",
          "secretsmanager:UpdateSecretVersionStage",
          "secretsmanager:ListSecrets",
          "secretsmanager:RotateSecret"
        ]
        Resource = "*"
      },
      {
        Effect = "Allow"
        Action = [
          "ssm:GetParameter",
          "ssm:PutParameter"
        ]
        Resource = "arn:aws:ssm:*:*:parameter/${var.project_name}/*"
      },
      {
        Effect = "Allow"
        Action = [
          "sns:Publish"
        ]
        Resource = aws_sns_topic.rotation_notifications.arn
      },
      {
        Effect = "Allow"
        Action = [
          "kms:Decrypt",
          "kms:DescribeKey",
          "kms:GenerateDataKey"
        ]
        Resource = aws_kms_key.secrets_key.arn
      }
    ]
  })
}

# VPC Configuration for Lambda (if database is in VPC)
resource "aws_iam_role_policy_attachment" "lambda_vpc_execution" {
  role       = aws_iam_role.lambda_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

# ============================================================================
# KMS Key for Secret Encryption
# ============================================================================

resource "aws_kms_key" "secrets_key" {
  description             = "KMS key for ${var.project_name} secrets encryption"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = merge(
    local.common_tags,
    {
      Name = "${var.project_name}-secrets-key-${var.environment}"
    }
  )
}

resource "aws_kms_alias" "secrets_key_alias" {
  name          = "alias/${var.project_name}-secrets-${var.environment}"
  target_key_id = aws_kms_key.secrets_key.key_id
}

# ============================================================================
# SNS Topic for Notifications
# ============================================================================

resource "aws_sns_topic" "rotation_notifications" {
  name              = "${local.function_name}-notifications"
  kms_master_key_id = aws_kms_key.secrets_key.id

  tags = local.common_tags
}

resource "aws_sns_topic_subscription" "email_subscription" {
  topic_arn = aws_sns_topic.rotation_notifications.arn
  protocol  = "email"
  endpoint  = var.notification_email
}

# ============================================================================
# Lambda Function
# ============================================================================

resource "aws_lambda_function" "secret_rotation" {
  filename         = data.archive_file.lambda_package.output_path
  function_name    = local.function_name
  role            = aws_iam_role.lambda_role.arn
  handler         = "index.handler"
  source_code_hash = data.archive_file.lambda_package.output_base64sha256
  runtime         = "nodejs18.x"
  timeout         = 300 # 5 minutes
  memory_size     = 512

  environment {
    variables = {
      ENVIRONMENT                = var.environment
      SNS_TOPIC_ARN             = aws_sns_topic.rotation_notifications.arn
      POSTGRES_MASTER_SECRET_ID = aws_secretsmanager_secret.postgres_master.id
      DATABASE_SECRET_ID        = aws_secretsmanager_secret.postgres_app.id
      API_ENDPOINT              = var.api_endpoint
    }
  }

  # VPC configuration if needed
  # vpc_config {
  #   subnet_ids         = var.subnet_ids
  #   security_group_ids = var.security_group_ids
  # }

  tags = local.common_tags
}

# Lambda CloudWatch Log Group
resource "aws_cloudwatch_log_group" "lambda_logs" {
  name              = "/aws/lambda/${local.function_name}"
  retention_in_days = 30
  kms_key_id        = aws_kms_key.secrets_key.arn

  tags = local.common_tags
}

# ============================================================================
# Secrets Manager Secrets
# ============================================================================

# Master PostgreSQL credentials (for rotation)
resource "aws_secretsmanager_secret" "postgres_master" {
  name                    = "${var.project_name}/${var.environment}/postgres/master"
  description             = "PostgreSQL master credentials for ${var.project_name}"
  kms_key_id             = aws_kms_key.secrets_key.id
  recovery_window_in_days = 30

  tags = merge(
    local.common_tags,
    {
      Name       = "postgres-master"
      AutoRotate = "false" # Master credentials rotated manually
      Type       = "postgresql"
    }
  )
}

resource "aws_secretsmanager_secret_version" "postgres_master" {
  secret_id = aws_secretsmanager_secret.postgres_master.id
  secret_string = jsonencode({
    type     = "postgresql"
    host     = var.postgres_host
    port     = var.postgres_port
    database = var.postgres_database
    username = var.postgres_master_username
    password = var.postgres_master_password
  })
}

# Application PostgreSQL credentials (auto-rotated)
resource "aws_secretsmanager_secret" "postgres_app" {
  name                    = "${var.project_name}/${var.environment}/postgres/app"
  description             = "PostgreSQL application credentials for ${var.project_name}"
  kms_key_id             = aws_kms_key.secrets_key.id
  recovery_window_in_days = 30

  tags = merge(
    local.common_tags,
    {
      Name       = "postgres-app"
      AutoRotate = "true"
      Type       = "postgresql"
    }
  )
}

resource "aws_secretsmanager_secret_version" "postgres_app" {
  secret_id = aws_secretsmanager_secret.postgres_app.id
  secret_string = jsonencode({
    type     = "postgresql"
    host     = var.postgres_host
    port     = var.postgres_port
    database = var.postgres_database
    username = var.postgres_app_username
    password = "ChangeMe123!" # Will be rotated immediately
  })

  lifecycle {
    ignore_changes = [secret_string] # Managed by rotation function
  }
}

# Enable automatic rotation for application credentials
resource "aws_secretsmanager_secret_rotation" "postgres_app" {
  secret_id           = aws_secretsmanager_secret.postgres_app.id
  rotation_lambda_arn = aws_lambda_function.secret_rotation.arn

  rotation_rules {
    automatically_after_days = var.rotation_days
  }
}

# Lambda permission for Secrets Manager rotation
resource "aws_lambda_permission" "secretsmanager_invoke" {
  statement_id  = "AllowSecretsManagerInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.secret_rotation.function_name
  principal     = "secretsmanager.amazonaws.com"
}

# JWT Signing Key (auto-rotated)
resource "aws_secretsmanager_secret" "jwt_signing_key" {
  name                    = "${var.project_name}/${var.environment}/jwt/signing-key"
  description             = "JWT signing key for ${var.project_name}"
  kms_key_id             = aws_kms_key.secrets_key.id
  recovery_window_in_days = 30

  tags = merge(
    local.common_tags,
    {
      Name       = "jwt-signing-key"
      AutoRotate = "true"
      Type       = "jwt-signing-key"
    }
  )
}

resource "aws_secretsmanager_secret_version" "jwt_signing_key" {
  secret_id = aws_secretsmanager_secret.jwt_signing_key.id
  secret_string = jsonencode({
    type          = "jwt-signing-key"
    signingKey    = base64encode(random_bytes.jwt_key.result)
    parameterName = "/honua/jwt/signing-key"
  })

  lifecycle {
    ignore_changes = [secret_string] # Managed by rotation function
  }
}

resource "random_bytes" "jwt_key" {
  length = 32 # 256 bits
}

resource "aws_secretsmanager_secret_rotation" "jwt_signing_key" {
  secret_id           = aws_secretsmanager_secret.jwt_signing_key.id
  rotation_lambda_arn = aws_lambda_function.secret_rotation.arn

  rotation_rules {
    automatically_after_days = var.rotation_days
  }
}

# ============================================================================
# EventBridge Scheduled Rotation
# ============================================================================

resource "aws_cloudwatch_event_rule" "rotation_schedule" {
  name                = "${local.function_name}-schedule"
  description         = "Trigger secret rotation every ${var.rotation_days} days"
  schedule_expression = "rate(${var.rotation_days} days)"

  tags = local.common_tags
}

resource "aws_cloudwatch_event_target" "lambda_target" {
  rule      = aws_cloudwatch_event_rule.rotation_schedule.name
  target_id = "RotationLambda"
  arn       = aws_lambda_function.secret_rotation.arn
}

resource "aws_lambda_permission" "eventbridge_invoke" {
  statement_id  = "AllowEventBridgeInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.secret_rotation.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.rotation_schedule.arn
}

# ============================================================================
# CloudWatch Alarms
# ============================================================================

resource "aws_cloudwatch_metric_alarm" "rotation_errors" {
  alarm_name          = "${local.function_name}-errors"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "Errors"
  namespace           = "AWS/Lambda"
  period              = 300
  statistic           = "Sum"
  threshold           = 0
  alarm_description   = "Alert when secret rotation fails"
  alarm_actions       = [aws_sns_topic.rotation_notifications.arn]

  dimensions = {
    FunctionName = aws_lambda_function.secret_rotation.function_name
  }

  tags = local.common_tags
}

resource "aws_cloudwatch_metric_alarm" "rotation_duration" {
  alarm_name          = "${local.function_name}-duration"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "Duration"
  namespace           = "AWS/Lambda"
  period              = 300
  statistic           = "Average"
  threshold           = 240000 # 4 minutes (80% of 5-minute timeout)
  alarm_description   = "Alert when rotation takes too long"
  alarm_actions       = [aws_sns_topic.rotation_notifications.arn]

  dimensions = {
    FunctionName = aws_lambda_function.secret_rotation.function_name
  }

  tags = local.common_tags
}

# ============================================================================
# Outputs
# ============================================================================

output "lambda_function_name" {
  description = "Name of the rotation Lambda function"
  value       = aws_lambda_function.secret_rotation.function_name
}

output "lambda_function_arn" {
  description = "ARN of the rotation Lambda function"
  value       = aws_lambda_function.secret_rotation.arn
}

output "sns_topic_arn" {
  description = "ARN of the notification SNS topic"
  value       = aws_sns_topic.rotation_notifications.arn
}

output "postgres_app_secret_arn" {
  description = "ARN of the PostgreSQL application secret"
  value       = aws_secretsmanager_secret.postgres_app.arn
}

output "postgres_master_secret_arn" {
  description = "ARN of the PostgreSQL master secret"
  value       = aws_secretsmanager_secret.postgres_master.arn
}

output "jwt_secret_arn" {
  description = "ARN of the JWT signing key secret"
  value       = aws_secretsmanager_secret.jwt_signing_key.arn
}

output "kms_key_id" {
  description = "ID of the KMS key used for secret encryption"
  value       = aws_kms_key.secrets_key.id
}

output "rotation_schedule" {
  description = "Secret rotation schedule"
  value       = "Every ${var.rotation_days} days"
}
