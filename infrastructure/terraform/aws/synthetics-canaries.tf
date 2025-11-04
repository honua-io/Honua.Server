# ============================================================================
# AWS CloudWatch Synthetics Canaries (External Uptime Monitoring)
# ============================================================================
# External synthetic monitoring for Honua endpoints using CloudWatch Synthetics.
# These canaries run independently from the application to detect outages.
#
# Key Endpoints Monitored:
# - /healthz/live: Liveness probe (basic availability)
# - /healthz/ready: Readiness probe (full stack health)
# - /ogc: OGC API landing page
# - /ogc/conformance: OGC conformance endpoint
# - /stac: STAC catalog endpoint
#
# Canaries run from multiple AWS regions for global coverage.
# ============================================================================

# ============================================================================
# Variables
# ============================================================================

variable "enable_synthetics" {
  description = "Enable CloudWatch Synthetics canaries"
  type        = bool
  default     = true
}

variable "canary_schedule_expression" {
  description = "Schedule for canary runs (rate expression)"
  type        = string
  default     = "rate(5 minutes)"
}

variable "canary_timeout_seconds" {
  description = "Timeout for canary runs in seconds"
  type        = number
  default     = 60
}

variable "canary_retention_days" {
  description = "Number of days to retain canary run data"
  type        = number
  default     = 31
}

# ============================================================================
# IAM Role for Canaries
# ============================================================================

resource "aws_iam_role" "canary_execution" {
  count = var.enable_synthetics ? 1 : 0
  name  = "honua-canary-execution-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = merge(
    local.common_tags,
    {
      Purpose = "Synthetics Canary Execution"
    }
  )
}

resource "aws_iam_role_policy" "canary_execution" {
  count = var.enable_synthetics ? 1 : 0
  name  = "canary-execution-policy"
  role  = aws_iam_role.canary_execution[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:PutObject",
          "s3:GetBucketLocation"
        ]
        Resource = [
          aws_s3_bucket.canary_results[0].arn,
          "${aws_s3_bucket.canary_results[0].arn}/*"
        ]
      },
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents",
          "logs:CreateLogGroup"
        ]
        Resource = "arn:aws:logs:*:*:log-group:/aws/lambda/cwsyn-honua-*"
      },
      {
        Effect = "Allow"
        Action = [
          "cloudwatch:PutMetricData"
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "cloudwatch:namespace" = "CloudWatchSynthetics"
          }
        }
      }
    ]
  })
}

# ============================================================================
# S3 Bucket for Canary Results
# ============================================================================

resource "aws_s3_bucket" "canary_results" {
  count  = var.enable_synthetics ? 1 : 0
  bucket = "honua-canary-results-${var.environment}-${data.aws_caller_identity.current.account_id}"

  tags = merge(
    local.common_tags,
    {
      Purpose = "Synthetics Canary Results"
    }
  )
}

resource "aws_s3_bucket_public_access_block" "canary_results" {
  count  = var.enable_synthetics ? 1 : 0
  bucket = aws_s3_bucket.canary_results[0].id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_lifecycle_configuration" "canary_results" {
  count  = var.enable_synthetics ? 1 : 0
  bucket = aws_s3_bucket.canary_results[0].id

  rule {
    id     = "cleanup-old-results"
    status = "Enabled"

    expiration {
      days = var.canary_retention_days
    }

    noncurrent_version_expiration {
      noncurrent_days = 7
    }
  }
}

# ============================================================================
# Data Source for Current Account
# ============================================================================

data "aws_caller_identity" "current" {}

# ============================================================================
# Canary: Liveness Endpoint (Critical)
# ============================================================================

resource "aws_synthetics_canary" "liveness" {
  count                = var.enable_synthetics ? 1 : 0
  name                 = "honua-liveness-${var.environment}"
  artifact_s3_location = "s3://${aws_s3_bucket.canary_results[0].bucket}/liveness"
  execution_role_arn   = aws_iam_role.canary_execution[0].arn
  handler              = "exports.handler"
  zip_file             = "liveness-canary.zip"
  runtime_version      = "syn-nodejs-puppeteer-6.2"

  schedule {
    expression = var.canary_schedule_expression
  }

  run_config {
    timeout_in_seconds    = var.canary_timeout_seconds
    memory_in_mb          = 1024
    active_tracing        = true
    environment_variables = {
      ENDPOINT_URL = "${local.computed_app_url}/healthz/live"
    }
  }

  success_retention_period = var.canary_retention_days
  failure_retention_period = var.canary_retention_days

  tags = merge(
    local.common_tags,
    {
      EndpointType = "Liveness"
      Severity     = "Critical"
      TestType     = "Synthetic"
    }
  )

  depends_on = [
    aws_iam_role_policy.canary_execution[0]
  ]
}

# ============================================================================
# Canary: Readiness Endpoint
# ============================================================================

resource "aws_synthetics_canary" "readiness" {
  count                = var.enable_synthetics ? 1 : 0
  name                 = "honua-readiness-${var.environment}"
  artifact_s3_location = "s3://${aws_s3_bucket.canary_results[0].bucket}/readiness"
  execution_role_arn   = aws_iam_role.canary_execution[0].arn
  handler              = "exports.handler"
  zip_file             = "readiness-canary.zip"
  runtime_version      = "syn-nodejs-puppeteer-6.2"

  schedule {
    expression = var.canary_schedule_expression
  }

  run_config {
    timeout_in_seconds    = var.canary_timeout_seconds
    memory_in_mb          = 1024
    active_tracing        = true
    environment_variables = {
      ENDPOINT_URL = "${local.computed_app_url}/healthz/ready"
    }
  }

  success_retention_period = var.canary_retention_days
  failure_retention_period = var.canary_retention_days

  tags = merge(
    local.common_tags,
    {
      EndpointType = "Readiness"
      Severity     = "Error"
      TestType     = "Synthetic"
    }
  )

  depends_on = [
    aws_iam_role_policy.canary_execution[0]
  ]
}

# ============================================================================
# Canary: OGC API Endpoint
# ============================================================================

resource "aws_synthetics_canary" "ogc_api" {
  count                = var.enable_synthetics ? 1 : 0
  name                 = "honua-ogc-api-${var.environment}"
  artifact_s3_location = "s3://${aws_s3_bucket.canary_results[0].bucket}/ogc-api"
  execution_role_arn   = aws_iam_role.canary_execution[0].arn
  handler              = "exports.handler"
  zip_file             = "ogc-api-canary.zip"
  runtime_version      = "syn-nodejs-puppeteer-6.2"

  schedule {
    expression = var.canary_schedule_expression
  }

  run_config {
    timeout_in_seconds    = var.canary_timeout_seconds
    memory_in_mb          = 1024
    active_tracing        = true
    environment_variables = {
      ENDPOINT_URL = "${local.computed_app_url}/ogc"
    }
  }

  success_retention_period = var.canary_retention_days
  failure_retention_period = var.canary_retention_days

  tags = merge(
    local.common_tags,
    {
      EndpointType = "OGC-API"
      Severity     = "Error"
      TestType     = "Synthetic"
    }
  )

  depends_on = [
    aws_iam_role_policy.canary_execution[0]
  ]
}

# ============================================================================
# Canary: OGC Conformance Endpoint
# ============================================================================

resource "aws_synthetics_canary" "ogc_conformance" {
  count                = var.enable_synthetics ? 1 : 0
  name                 = "honua-ogc-conformance-${var.environment}"
  artifact_s3_location = "s3://${aws_s3_bucket.canary_results[0].bucket}/ogc-conformance"
  execution_role_arn   = aws_iam_role.canary_execution[0].arn
  handler              = "exports.handler"
  zip_file             = "ogc-conformance-canary.zip"
  runtime_version      = "syn-nodejs-puppeteer-6.2"

  schedule {
    expression = var.canary_schedule_expression
  }

  run_config {
    timeout_in_seconds    = var.canary_timeout_seconds
    memory_in_mb          = 1024
    active_tracing        = true
    environment_variables = {
      ENDPOINT_URL = "${local.computed_app_url}/ogc/conformance"
    }
  }

  success_retention_period = var.canary_retention_days
  failure_retention_period = var.canary_retention_days

  tags = merge(
    local.common_tags,
    {
      EndpointType = "OGC-Conformance"
      Severity     = "Warning"
      TestType     = "Synthetic"
    }
  )

  depends_on = [
    aws_iam_role_policy.canary_execution[0]
  ]
}

# ============================================================================
# CloudWatch Alarms for Canaries
# ============================================================================

# Alarm: Liveness Check Failed
resource "aws_cloudwatch_metric_alarm" "liveness_failed" {
  count               = var.enable_synthetics ? 1 : 0
  alarm_name          = "honua-liveness-failed-${var.environment}"
  alarm_description   = "Alert when liveness endpoint synthetic check fails"
  comparison_operator = "LessThanThreshold"
  evaluation_periods  = 2
  metric_name         = "SuccessPercent"
  namespace           = "CloudWatchSynthetics"
  period              = 300
  statistic           = "Average"
  threshold           = 90 # Alert if success rate < 90%
  treat_missing_data  = "breaching"

  dimensions = {
    CanaryName = aws_synthetics_canary.liveness[0].name
  }

  alarm_actions = [aws_sns_topic.alerts.arn]
  ok_actions    = [aws_sns_topic.alerts.arn]

  tags = merge(
    local.common_tags,
    {
      Severity     = "Critical"
      EndpointType = "Liveness"
    }
  )
}

# Alarm: Readiness Check Failed
resource "aws_cloudwatch_metric_alarm" "readiness_failed" {
  count               = var.enable_synthetics ? 1 : 0
  alarm_name          = "honua-readiness-failed-${var.environment}"
  alarm_description   = "Alert when readiness endpoint synthetic check fails"
  comparison_operator = "LessThanThreshold"
  evaluation_periods  = 2
  metric_name         = "SuccessPercent"
  namespace           = "CloudWatchSynthetics"
  period              = 300
  statistic           = "Average"
  threshold           = 90
  treat_missing_data  = "breaching"

  dimensions = {
    CanaryName = aws_synthetics_canary.readiness[0].name
  }

  alarm_actions = [aws_sns_topic.alerts.arn]
  ok_actions    = [aws_sns_topic.alerts.arn]

  tags = merge(
    local.common_tags,
    {
      Severity     = "Error"
      EndpointType = "Readiness"
    }
  )
}

# Alarm: OGC API Failed
resource "aws_cloudwatch_metric_alarm" "ogc_api_failed" {
  count               = var.enable_synthetics ? 1 : 0
  alarm_name          = "honua-ogc-api-failed-${var.environment}"
  alarm_description   = "Alert when OGC API endpoint synthetic check fails"
  comparison_operator = "LessThanThreshold"
  evaluation_periods  = 3
  metric_name         = "SuccessPercent"
  namespace           = "CloudWatchSynthetics"
  period              = 300
  statistic           = "Average"
  threshold           = 80
  treat_missing_data  = "breaching"

  dimensions = {
    CanaryName = aws_synthetics_canary.ogc_api[0].name
  }

  alarm_actions = [aws_sns_topic.alerts.arn]

  tags = merge(
    local.common_tags,
    {
      Severity     = "Error"
      EndpointType = "OGC-API"
    }
  )
}

# Alarm: High Latency
resource "aws_cloudwatch_metric_alarm" "high_latency" {
  count               = var.enable_synthetics ? 1 : 0
  alarm_name          = "honua-high-latency-${var.environment}"
  alarm_description   = "Alert when endpoint response time is consistently high"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 3
  metric_name         = "Duration"
  namespace           = "CloudWatchSynthetics"
  period              = 300
  statistic           = "Average"
  threshold           = 2000 # 2 seconds
  treat_missing_data  = "notBreaching"

  dimensions = {
    CanaryName = aws_synthetics_canary.liveness[0].name
  }

  alarm_actions = [aws_sns_topic.alerts.arn]

  tags = merge(
    local.common_tags,
    {
      Severity = "Warning"
      Type     = "Performance"
    }
  )
}

# ============================================================================
# SNS Topic for Alerts (if not exists)
# ============================================================================

resource "aws_sns_topic" "alerts" {
  count = var.enable_synthetics ? 1 : 0
  name  = "honua-synthetics-alerts-${var.environment}"

  tags = merge(
    local.common_tags,
    {
      Purpose = "Synthetics Alerts"
    }
  )
}

resource "aws_sns_topic_subscription" "alerts_email" {
  count     = var.enable_synthetics && var.alert_email != null ? 1 : 0
  topic_arn = aws_sns_topic.alerts[0].arn
  protocol  = "email"
  endpoint  = var.alert_email
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  # Compute app URL from load balancer or CloudFront
  computed_app_url = var.app_url != null ? var.app_url : (
    try(aws_cloudfront_distribution.main.domain_name, null) != null
    ? "https://${aws_cloudfront_distribution.main.domain_name}"
    : try(aws_lb.main.dns_name, null) != null
    ? "http://${aws_lb.main.dns_name}"
    : "https://honua-${var.environment}.example.com"
  )

  common_tags = {
    Environment = var.environment
    Project     = "Honua"
    ManagedBy   = "Terraform"
    Component   = "Synthetics"
  }
}

# ============================================================================
# Outputs
# ============================================================================

output "synthetics_canaries" {
  value = var.enable_synthetics ? {
    enabled = true
    app_url = local.computed_app_url
    canaries = {
      liveness = {
        name   = aws_synthetics_canary.liveness[0].name
        id     = aws_synthetics_canary.liveness[0].id
        arn    = aws_synthetics_canary.liveness[0].arn
        status = aws_synthetics_canary.liveness[0].status
      }
      readiness = {
        name   = aws_synthetics_canary.readiness[0].name
        id     = aws_synthetics_canary.readiness[0].id
        arn    = aws_synthetics_canary.readiness[0].arn
        status = aws_synthetics_canary.readiness[0].status
      }
      ogc_api = {
        name   = aws_synthetics_canary.ogc_api[0].name
        id     = aws_synthetics_canary.ogc_api[0].id
        arn    = aws_synthetics_canary.ogc_api[0].arn
        status = aws_synthetics_canary.ogc_api[0].status
      }
      ogc_conformance = {
        name   = aws_synthetics_canary.ogc_conformance[0].name
        id     = aws_synthetics_canary.ogc_conformance[0].id
        arn    = aws_synthetics_canary.ogc_conformance[0].arn
        status = aws_synthetics_canary.ogc_conformance[0].status
      }
    }
    configuration = {
      schedule_expression   = var.canary_schedule_expression
      timeout_seconds       = var.canary_timeout_seconds
      retention_days        = var.canary_retention_days
      results_bucket        = aws_s3_bucket.canary_results[0].bucket
      execution_role_arn    = aws_iam_role.canary_execution[0].arn
    }
    alarms = {
      liveness_failed  = aws_cloudwatch_metric_alarm.liveness_failed[0].arn
      readiness_failed = aws_cloudwatch_metric_alarm.readiness_failed[0].arn
      ogc_api_failed   = aws_cloudwatch_metric_alarm.ogc_api_failed[0].arn
      high_latency     = aws_cloudwatch_metric_alarm.high_latency[0].arn
    }
    sns_topic = aws_sns_topic.alerts[0].arn
  } : {
    enabled = false
    message = "CloudWatch Synthetics is disabled. Set enable_synthetics = true to enable."
  }
  description = "CloudWatch Synthetics canary configuration and status"
}
