# ============================================================================
# GCP Cloud Monitoring Uptime Checks (External Uptime Monitoring)
# ============================================================================
# External synthetic monitoring for Honua endpoints using Cloud Monitoring.
# These uptime checks run independently from the application to detect outages.
#
# Key Endpoints Monitored:
# - /healthz/live: Liveness probe (basic availability)
# - /healthz/ready: Readiness probe (full stack health)
# - /ogc: OGC API landing page
# - /ogc/conformance: OGC conformance endpoint
# - /stac: STAC catalog endpoint
#
# Checks run from multiple GCP regions for global coverage.
# ============================================================================

# ============================================================================
# Variables
# ============================================================================

variable "enable_uptime_checks" {
  description = "Enable Cloud Monitoring uptime checks"
  type        = bool
  default     = true
}

variable "uptime_check_period" {
  description = "Period between uptime checks (60s, 300s, 600s, or 900s)"
  type        = string
  default     = "300s"
  validation {
    condition     = contains(["60s", "300s", "600s", "900s"], var.uptime_check_period)
    error_message = "Uptime check period must be 60s, 300s, 600s, or 900s."
  }
}

variable "uptime_check_timeout" {
  description = "Timeout for uptime checks"
  type        = string
  default     = "10s"
}

variable "alert_notification_channels" {
  description = "List of notification channel IDs for alerts"
  type        = list(string)
  default     = []
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  # Compute app URL
  computed_app_url = var.app_url != null ? var.app_url : (
    try(google_compute_global_address.main.address, null) != null
    ? "https://${google_compute_global_address.main.address}"
    : "https://honua-${var.environment}.example.com"
  )

  # Extract hostname for uptime checks
  app_hostname = replace(replace(local.computed_app_url, "https://", ""), "http://", "")

  # Uptime check regions (global coverage)
  uptime_regions = [
    "USA",
    "EUROPE",
    "SOUTH_AMERICA",
    "ASIA_PACIFIC"
  ]

  # Monitored endpoints
  monitored_endpoints = {
    liveness = {
      path        = "/healthz/live"
      description = "Liveness probe - basic availability check"
      content_match = "Healthy"
      severity    = "CRITICAL"
    }
    readiness = {
      path        = "/healthz/ready"
      description = "Readiness probe - full stack health check"
      content_match = "Healthy"
      severity    = "ERROR"
    }
    ogc_landing = {
      path        = "/ogc"
      description = "OGC API landing page"
      content_match = "\"links\""
      severity    = "ERROR"
    }
    ogc_conformance = {
      path        = "/ogc/conformance"
      description = "OGC API conformance endpoint"
      content_match = "\"conformsTo\""
      severity    = "WARNING"
    }
    stac_catalog = {
      path        = "/stac"
      description = "STAC catalog endpoint"
      content_match = "\"type\""
      severity    = "WARNING"
    }
  }

  common_labels = {
    environment = var.environment
    project     = "honua"
    managed_by  = "terraform"
    component   = "uptime-monitoring"
  }
}

# ============================================================================
# Uptime Check: Liveness Endpoint
# ============================================================================

resource "google_monitoring_uptime_check_config" "liveness" {
  count        = var.enable_uptime_checks ? 1 : 0
  display_name = "honua-liveness-${var.environment}"
  timeout      = var.uptime_check_timeout
  period       = var.uptime_check_period

  http_check {
    path         = local.monitored_endpoints.liveness.path
    port         = 443
    use_ssl      = true
    validate_ssl = true

    accepted_response_status_codes {
      status_class = "STATUS_CLASS_2XX"
    }

    content_matchers {
      content = local.monitored_endpoints.liveness.content_match
      matcher = "CONTAINS_STRING"
    }
  }

  monitored_resource {
    type = "uptime_url"
    labels = {
      project_id = var.project_id
      host       = local.app_hostname
    }
  }

  selected_regions = local.uptime_regions

  user_labels = merge(
    local.common_labels,
    {
      endpoint_type = "liveness"
      severity      = lower(local.monitored_endpoints.liveness.severity)
    }
  )
}

# ============================================================================
# Uptime Check: Readiness Endpoint
# ============================================================================

resource "google_monitoring_uptime_check_config" "readiness" {
  count        = var.enable_uptime_checks ? 1 : 0
  display_name = "honua-readiness-${var.environment}"
  timeout      = var.uptime_check_timeout
  period       = var.uptime_check_period

  http_check {
    path         = local.monitored_endpoints.readiness.path
    port         = 443
    use_ssl      = true
    validate_ssl = true

    accepted_response_status_codes {
      status_class = "STATUS_CLASS_2XX"
    }

    content_matchers {
      content = local.monitored_endpoints.readiness.content_match
      matcher = "CONTAINS_STRING"
    }
  }

  monitored_resource {
    type = "uptime_url"
    labels = {
      project_id = var.project_id
      host       = local.app_hostname
    }
  }

  selected_regions = local.uptime_regions

  user_labels = merge(
    local.common_labels,
    {
      endpoint_type = "readiness"
      severity      = lower(local.monitored_endpoints.readiness.severity)
    }
  )
}

# ============================================================================
# Uptime Check: OGC API Landing Page
# ============================================================================

resource "google_monitoring_uptime_check_config" "ogc_landing" {
  count        = var.enable_uptime_checks ? 1 : 0
  display_name = "honua-ogc-landing-${var.environment}"
  timeout      = var.uptime_check_timeout
  period       = var.uptime_check_period

  http_check {
    path         = local.monitored_endpoints.ogc_landing.path
    port         = 443
    use_ssl      = true
    validate_ssl = true

    headers = {
      "Accept" = "application/json"
    }

    accepted_response_status_codes {
      status_class = "STATUS_CLASS_2XX"
    }

    content_matchers {
      content = local.monitored_endpoints.ogc_landing.content_match
      matcher = "CONTAINS_STRING"
    }
  }

  monitored_resource {
    type = "uptime_url"
    labels = {
      project_id = var.project_id
      host       = local.app_hostname
    }
  }

  selected_regions = local.uptime_regions

  user_labels = merge(
    local.common_labels,
    {
      endpoint_type = "ogc-api"
      severity      = lower(local.monitored_endpoints.ogc_landing.severity)
    }
  )
}

# ============================================================================
# Uptime Check: OGC Conformance Endpoint
# ============================================================================

resource "google_monitoring_uptime_check_config" "ogc_conformance" {
  count        = var.enable_uptime_checks ? 1 : 0
  display_name = "honua-ogc-conformance-${var.environment}"
  timeout      = var.uptime_check_timeout
  period       = var.uptime_check_period

  http_check {
    path         = local.monitored_endpoints.ogc_conformance.path
    port         = 443
    use_ssl      = true
    validate_ssl = true

    headers = {
      "Accept" = "application/json"
    }

    accepted_response_status_codes {
      status_class = "STATUS_CLASS_2XX"
    }

    content_matchers {
      content = local.monitored_endpoints.ogc_conformance.content_match
      matcher = "CONTAINS_STRING"
    }
  }

  monitored_resource {
    type = "uptime_url"
    labels = {
      project_id = var.project_id
      host       = local.app_hostname
    }
  }

  selected_regions = local.uptime_regions

  user_labels = merge(
    local.common_labels,
    {
      endpoint_type = "ogc-conformance"
      severity      = lower(local.monitored_endpoints.ogc_conformance.severity)
    }
  )
}

# ============================================================================
# Uptime Check: STAC Catalog
# ============================================================================

resource "google_monitoring_uptime_check_config" "stac_catalog" {
  count        = var.enable_uptime_checks ? 1 : 0
  display_name = "honua-stac-catalog-${var.environment}"
  timeout      = var.uptime_check_timeout
  period       = var.uptime_check_period

  http_check {
    path         = local.monitored_endpoints.stac_catalog.path
    port         = 443
    use_ssl      = true
    validate_ssl = true

    headers = {
      "Accept" = "application/json"
    }

    accepted_response_status_codes {
      status_class = "STATUS_CLASS_2XX"
    }

    content_matchers {
      content = local.monitored_endpoints.stac_catalog.content_match
      matcher = "CONTAINS_STRING"
    }
  }

  monitored_resource {
    type = "uptime_url"
    labels = {
      project_id = var.project_id
      host       = local.app_hostname
    }
  }

  selected_regions = local.uptime_regions

  user_labels = merge(
    local.common_labels,
    {
      endpoint_type = "stac"
      severity      = lower(local.monitored_endpoints.stac_catalog.severity)
    }
  )
}

# ============================================================================
# Alert Policy: Liveness Check Failed
# ============================================================================

resource "google_monitoring_alert_policy" "liveness_failed" {
  count        = var.enable_uptime_checks ? 1 : 0
  display_name = "Honua Liveness Check Failed - ${var.environment}"
  combiner     = "OR"

  conditions {
    display_name = "Liveness endpoint unreachable"

    condition_threshold {
      filter          = "metric.type=\"monitoring.googleapis.com/uptime_check/check_passed\" AND resource.type=\"uptime_url\" AND metric.label.check_id=\"${google_monitoring_uptime_check_config.liveness[0].uptime_check_id}\""
      duration        = "300s"
      comparison      = "COMPARISON_LT"
      threshold_value = 1.0
      aggregations {
        alignment_period   = "300s"
        per_series_aligner = "ALIGN_FRACTION_TRUE"
      }
    }
  }

  documentation {
    content = <<-EOT
      ## Liveness Check Failed

      The Honua liveness endpoint (/healthz/live) is failing uptime checks.

      **Severity**: Critical

      **Endpoint**: ${local.computed_app_url}${local.monitored_endpoints.liveness.path}

      **Action Required**:
      1. Check application logs for errors
      2. Verify the service is running
      3. Check for infrastructure issues
      4. Review recent deployments

      **Runbook**: https://docs.honua.io/runbooks/liveness-check-failed
    EOT
    mime_type = "text/markdown"
  }

  notification_channels = var.alert_notification_channels

  alert_strategy {
    auto_close = "1800s"
  }

  user_labels = merge(
    local.common_labels,
    {
      severity      = "critical"
      endpoint_type = "liveness"
    }
  )
}

# ============================================================================
# Alert Policy: Readiness Check Failed
# ============================================================================

resource "google_monitoring_alert_policy" "readiness_failed" {
  count        = var.enable_uptime_checks ? 1 : 0
  display_name = "Honua Readiness Check Failed - ${var.environment}"
  combiner     = "OR"

  conditions {
    display_name = "Readiness endpoint unhealthy"

    condition_threshold {
      filter          = "metric.type=\"monitoring.googleapis.com/uptime_check/check_passed\" AND resource.type=\"uptime_url\" AND metric.label.check_id=\"${google_monitoring_uptime_check_config.readiness[0].uptime_check_id}\""
      duration        = "300s"
      comparison      = "COMPARISON_LT"
      threshold_value = 1.0
      aggregations {
        alignment_period   = "300s"
        per_series_aligner = "ALIGN_FRACTION_TRUE"
      }
    }
  }

  documentation {
    content = <<-EOT
      ## Readiness Check Failed

      The Honua readiness endpoint (/healthz/ready) is failing uptime checks.

      **Severity**: Error

      **Endpoint**: ${local.computed_app_url}${local.monitored_endpoints.readiness.path}

      **Possible Causes**:
      - Database connection issues
      - Dependency service failures
      - Configuration problems

      **Action Required**:
      1. Check dependent services (PostgreSQL, Redis, etc.)
      2. Review application health check logs
      3. Verify network connectivity
      4. Check resource utilization

      **Runbook**: https://docs.honua.io/runbooks/readiness-check-failed
    EOT
    mime_type = "text/markdown"
  }

  notification_channels = var.alert_notification_channels

  alert_strategy {
    auto_close = "1800s"
  }

  user_labels = merge(
    local.common_labels,
    {
      severity      = "error"
      endpoint_type = "readiness"
    }
  )
}

# ============================================================================
# Alert Policy: High Latency
# ============================================================================

resource "google_monitoring_alert_policy" "high_latency" {
  count        = var.enable_uptime_checks ? 1 : 0
  display_name = "Honua High Response Latency - ${var.environment}"
  combiner     = "OR"

  conditions {
    display_name = "High response time from uptime checks"

    condition_threshold {
      filter          = "metric.type=\"monitoring.googleapis.com/uptime_check/request_latency\" AND resource.type=\"uptime_url\" AND metric.label.check_id=\"${google_monitoring_uptime_check_config.liveness[0].uptime_check_id}\""
      duration        = "600s"
      comparison      = "COMPARISON_GT"
      threshold_value = 2000.0 # 2 seconds in milliseconds
      aggregations {
        alignment_period     = "300s"
        per_series_aligner   = "ALIGN_MEAN"
        cross_series_reducer = "REDUCE_MEAN"
        group_by_fields      = ["resource.host"]
      }
    }
  }

  documentation {
    content = <<-EOT
      ## High Response Latency

      The Honua application is experiencing high response latency.

      **Severity**: Warning

      **Threshold**: 2000ms average response time

      **Action Required**:
      1. Check application performance metrics
      2. Review database query performance
      3. Check for resource contention
      4. Verify CDN/cache configuration

      **Runbook**: https://docs.honua.io/runbooks/high-latency
    EOT
    mime_type = "text/markdown"
  }

  notification_channels = var.alert_notification_channels

  alert_strategy {
    auto_close = "3600s"
  }

  user_labels = merge(
    local.common_labels,
    {
      severity = "warning"
      type     = "performance"
    }
  )
}

# ============================================================================
# Outputs
# ============================================================================

output "uptime_checks" {
  value = var.enable_uptime_checks ? {
    enabled = true
    app_url = local.computed_app_url
    configuration = {
      period_seconds  = var.uptime_check_period
      timeout_seconds = var.uptime_check_timeout
      regions         = local.uptime_regions
    }
    checks = {
      liveness = {
        id   = google_monitoring_uptime_check_config.liveness[0].uptime_check_id
        name = google_monitoring_uptime_check_config.liveness[0].display_name
        path = local.monitored_endpoints.liveness.path
      }
      readiness = {
        id   = google_monitoring_uptime_check_config.readiness[0].uptime_check_id
        name = google_monitoring_uptime_check_config.readiness[0].display_name
        path = local.monitored_endpoints.readiness.path
      }
      ogc_landing = {
        id   = google_monitoring_uptime_check_config.ogc_landing[0].uptime_check_id
        name = google_monitoring_uptime_check_config.ogc_landing[0].display_name
        path = local.monitored_endpoints.ogc_landing.path
      }
      ogc_conformance = {
        id   = google_monitoring_uptime_check_config.ogc_conformance[0].uptime_check_id
        name = google_monitoring_uptime_check_config.ogc_conformance[0].display_name
        path = local.monitored_endpoints.ogc_conformance.path
      }
      stac_catalog = {
        id   = google_monitoring_uptime_check_config.stac_catalog[0].uptime_check_id
        name = google_monitoring_uptime_check_config.stac_catalog[0].display_name
        path = local.monitored_endpoints.stac_catalog.path
      }
    }
    alert_policies = {
      liveness_failed  = google_monitoring_alert_policy.liveness_failed[0].id
      readiness_failed = google_monitoring_alert_policy.readiness_failed[0].id
      high_latency     = google_monitoring_alert_policy.high_latency[0].id
    }
    dashboards = {
      cloud_console = "https://console.cloud.google.com/monitoring/uptime?project=${var.project_id}"
    }
  } : {
    enabled = false
    message = "Uptime checks are disabled. Set enable_uptime_checks = true to enable."
  }
  description = "GCP Cloud Monitoring uptime check configuration and status"
}
