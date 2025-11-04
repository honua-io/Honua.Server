# Monitoring Module - Multi-Cloud Support
# Supports CloudWatch, Azure Monitor, and GCP Cloud Monitoring

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.23"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.11"
    }
  }
}

# ==================== Prometheus Stack (Common) ====================
resource "kubernetes_namespace" "monitoring" {
  metadata {
    name = "monitoring"
    labels = {
      name = "monitoring"
    }
  }
}

# Prometheus using Helm
resource "helm_release" "prometheus" {
  name       = "prometheus"
  repository = "https://prometheus-community.github.io/helm-charts"
  chart      = "kube-prometheus-stack"
  namespace  = kubernetes_namespace.monitoring.metadata[0].name
  version    = "55.0.0"

  values = [
    yamlencode({
      prometheus = {
        prometheusSpec = {
          retention               = var.prometheus_retention
          storageSpec = {
            volumeClaimTemplate = {
              spec = {
                storageClassName = var.storage_class
                accessModes      = ["ReadWriteOnce"]
                resources = {
                  requests = {
                    storage = var.prometheus_storage_size
                  }
                }
              }
            }
          }
          resources = {
            requests = {
              cpu    = "500m"
              memory = "2Gi"
            }
            limits = {
              cpu    = "2000m"
              memory = "8Gi"
            }
          }
        }
      }
      grafana = {
        enabled = true
        adminPassword = var.grafana_admin_password
        persistence = {
          enabled          = true
          storageClassName = var.storage_class
          size             = "10Gi"
        }
        datasources = {
          "datasources.yaml" = {
            apiVersion = 1
            datasources = [
              {
                name      = "Prometheus"
                type      = "prometheus"
                url       = "http://prometheus-kube-prometheus-prometheus.monitoring:9090"
                access    = "proxy"
                isDefault = true
              }
            ]
          }
        }
        dashboardProviders = {
          "dashboardproviders.yaml" = {
            apiVersion = 1
            providers = [
              {
                name            = "default"
                orgId           = 1
                folder          = ""
                type            = "file"
                disableDeletion = false
                editable        = true
                options = {
                  path = "/var/lib/grafana/dashboards/default"
                }
              }
            ]
          }
        }
      }
      alertmanager = {
        enabled = true
        config = {
          global = {
            resolve_timeout = "5m"
          }
          route = {
            group_by        = ["alertname", "cluster", "service"]
            group_wait      = "10s"
            group_interval  = "10s"
            repeat_interval = "12h"
            receiver        = "default"
          }
          receivers = [
            {
              name = "default"
            }
          ]
        }
      }
    })
  ]
}

# ==================== AWS CloudWatch ====================
resource "aws_cloudwatch_log_group" "application" {
  count             = var.cloud_provider == "aws" ? 1 : 0
  name              = "/aws/${var.service_name}/${var.environment}"
  retention_in_days = var.log_retention_days

  tags = var.tags
}

# CloudWatch Dashboard
resource "aws_cloudwatch_dashboard" "honua" {
  count          = var.cloud_provider == "aws" ? 1 : 0
  dashboard_name = "${var.service_name}-${var.environment}"

  dashboard_body = jsonencode({
    widgets = [
      {
        type = "metric"
        properties = {
          metrics = [
            ["AWS/EKS", "cluster_failed_node_count", { stat = "Average" }],
            [".", "cluster_node_count", { stat = "Average" }]
          ]
          period = 300
          stat   = "Average"
          region = var.aws_region
          title  = "EKS Cluster Nodes"
        }
      },
      {
        type = "metric"
        properties = {
          metrics = [
            ["AWS/RDS", "CPUUtilization", { stat = "Average" }],
            [".", "DatabaseConnections", { stat = "Sum" }],
            [".", "FreeableMemory", { stat = "Average" }]
          ]
          period = 300
          stat   = "Average"
          region = var.aws_region
          title  = "RDS Metrics"
        }
      },
      {
        type = "metric"
        properties = {
          metrics = [
            ["AWS/ElastiCache", "CPUUtilization", { stat = "Average" }],
            [".", "NetworkBytesIn", { stat = "Sum" }],
            [".", "NetworkBytesOut", { stat = "Sum" }]
          ]
          period = 300
          stat   = "Average"
          region = var.aws_region
          title  = "Redis Metrics"
        }
      }
    ]
  })
}

# CloudWatch Alarms
resource "aws_cloudwatch_metric_alarm" "high_cpu" {
  count               = var.cloud_provider == "aws" ? 1 : 0
  alarm_name          = "${var.service_name}-${var.environment}-high-cpu"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = "2"
  metric_name         = "CPUUtilization"
  namespace           = "AWS/EKS"
  period              = "300"
  statistic           = "Average"
  threshold           = "80"
  alarm_description   = "This metric monitors EKS cluster CPU utilization"
  alarm_actions       = var.sns_topic_arn != "" ? [var.sns_topic_arn] : []

  tags = var.tags
}

resource "aws_cloudwatch_metric_alarm" "high_memory" {
  count               = var.cloud_provider == "aws" ? 1 : 0
  alarm_name          = "${var.service_name}-${var.environment}-high-memory"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = "2"
  metric_name         = "MemoryUtilization"
  namespace           = "AWS/EKS"
  period              = "300"
  statistic           = "Average"
  threshold           = "80"
  alarm_description   = "This metric monitors EKS cluster memory utilization"
  alarm_actions       = var.sns_topic_arn != "" ? [var.sns_topic_arn] : []

  tags = var.tags
}

# SNS Topic for Alarms
resource "aws_sns_topic" "alarms" {
  count = var.cloud_provider == "aws" && var.create_sns_topic ? 1 : 0
  name  = "${var.service_name}-alarms-${var.environment}"

  tags = var.tags
}

resource "aws_sns_topic_subscription" "alarms_email" {
  count     = var.cloud_provider == "aws" && var.create_sns_topic && length(var.alarm_emails) > 0 ? length(var.alarm_emails) : 0
  topic_arn = aws_sns_topic.alarms[0].arn
  protocol  = "email"
  endpoint  = var.alarm_emails[count.index]
}

# ==================== Azure Monitor ====================
resource "azurerm_log_analytics_workspace" "honua" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.service_name}-logs-${var.environment}"
  location            = var.azure_location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days

  tags = var.tags
}

resource "azurerm_application_insights" "honua" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.service_name}-insights-${var.environment}"
  location            = var.azure_location
  resource_group_name = var.resource_group_name
  application_type    = "web"
  workspace_id        = azurerm_log_analytics_workspace.honua[0].id

  tags = var.tags
}

# Azure Monitor Action Group
resource "azurerm_monitor_action_group" "honua" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.service_name}-action-group-${var.environment}"
  resource_group_name = var.resource_group_name
  short_name          = "honua"

  dynamic "email_receiver" {
    for_each = var.alarm_emails
    content {
      name          = "email-${email_receiver.key}"
      email_address = email_receiver.value
    }
  }

  tags = var.tags
}

# Azure Monitor Metric Alerts
resource "azurerm_monitor_metric_alert" "aks_cpu" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.service_name}-aks-high-cpu-${var.environment}"
  resource_group_name = var.resource_group_name
  scopes              = [var.aks_cluster_id]
  description         = "Alert when AKS cluster CPU is high"
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.ContainerService/managedClusters"
    metric_name      = "node_cpu_usage_percentage"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }

  action {
    action_group_id = azurerm_monitor_action_group.honua[0].id
  }

  tags = var.tags
}

# ==================== GCP Cloud Monitoring ====================
resource "google_monitoring_notification_channel" "email" {
  count        = var.cloud_provider == "gcp" ? length(var.alarm_emails) : 0
  display_name = "Email ${count.index + 1}"
  type         = "email"
  project      = var.gcp_project_id

  labels = {
    email_address = var.alarm_emails[count.index]
  }
}

# GCP Uptime Check
resource "google_monitoring_uptime_check_config" "api" {
  count        = var.cloud_provider == "gcp" ? 1 : 0
  display_name = "${var.service_name}-api-uptime-${var.environment}"
  project      = var.gcp_project_id
  timeout      = "10s"
  period       = "60s"

  http_check {
    path           = "/health"
    port           = 443
    use_ssl        = true
    validate_ssl   = true
  }

  monitored_resource {
    type = "uptime_url"
    labels = {
      project_id = var.gcp_project_id
      host       = var.api_endpoint
    }
  }
}

# GCP Alert Policy - High CPU
resource "google_monitoring_alert_policy" "high_cpu" {
  count        = var.cloud_provider == "gcp" ? 1 : 0
  display_name = "${var.service_name}-high-cpu-${var.environment}"
  project      = var.gcp_project_id
  combiner     = "OR"

  conditions {
    display_name = "CPU utilization above 80%"

    condition_threshold {
      filter          = "metric.type=\"compute.googleapis.com/instance/cpu/utilization\" resource.type=\"gce_instance\""
      duration        = "300s"
      comparison      = "COMPARISON_GT"
      threshold_value = 0.8

      aggregations {
        alignment_period   = "60s"
        per_series_aligner = "ALIGN_MEAN"
      }
    }
  }

  notification_channels = google_monitoring_notification_channel.email[*].id

  alert_strategy {
    auto_close = "604800s"
  }
}

# ==================== Budget Alerts ====================
resource "aws_budgets_budget" "monthly" {
  count         = var.cloud_provider == "aws" && var.enable_budget_alerts ? 1 : 0
  name          = "${var.service_name}-monthly-budget-${var.environment}"
  budget_type   = "COST"
  limit_amount  = var.monthly_budget_limit
  limit_unit    = "USD"
  time_unit     = "MONTHLY"

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = var.alarm_emails
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = var.alarm_emails
  }
}

resource "azurerm_consumption_budget_resource_group" "monthly" {
  count               = var.cloud_provider == "azure" && var.enable_budget_alerts ? 1 : 0
  name                = "${var.service_name}-monthly-budget"
  resource_group_id   = var.resource_group_id
  amount              = var.monthly_budget_limit
  time_grain          = "Monthly"

  time_period {
    start_date = formatdate("YYYY-MM-01'T'00:00:00Z", timestamp())
  }

  notification {
    enabled   = true
    threshold = 80
    operator  = "GreaterThan"

    contact_emails = var.alarm_emails
  }

  notification {
    enabled   = true
    threshold = 100
    operator  = "GreaterThan"

    contact_emails = var.alarm_emails
  }
}

resource "google_billing_budget" "monthly" {
  count          = var.cloud_provider == "gcp" && var.enable_budget_alerts ? 1 : 0
  billing_account = var.gcp_billing_account
  display_name    = "${var.service_name}-monthly-budget-${var.environment}"

  budget_filter {
    projects = ["projects/${var.gcp_project_id}"]
  }

  amount {
    specified_amount {
      currency_code = "USD"
      units         = tostring(var.monthly_budget_limit)
    }
  }

  threshold_rules {
    threshold_percent = 0.8
  }

  threshold_rules {
    threshold_percent = 1.0
  }

  all_updates_rule {
    monitoring_notification_channels = google_monitoring_notification_channel.email[*].id
  }
}
