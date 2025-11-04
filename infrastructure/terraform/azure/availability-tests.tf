# ============================================================================
# Azure Monitor Availability Tests (Synthetic Monitoring)
# ============================================================================
# External uptime monitoring for Honua endpoints from multiple Azure regions.
# These tests run independently from the application to detect outages.
#
# Key Endpoints Monitored:
# - /healthz/live: Liveness probe (basic availability)
# - /healthz/ready: Readiness probe (full stack health)
# - /ogc: OGC API landing page
# - /ogc/conformance: OGC conformance endpoint
# - /stac: STAC catalog endpoint
#
# Test Regions (Multi-region probing):
# - East US (us-va-ash-azr)
# - West Europe (emea-nl-ams-azr)
# - Southeast Asia (apac-sg-sin-azr)
# - Australia East (apac-au-syd-azr)
# - UK South (emea-gb-db3-azr)
# ============================================================================

# ============================================================================
# Variables for Availability Tests
# ============================================================================

variable "app_url" {
  description = "The base URL of the Honua application to monitor"
  type        = string
  default     = null
}

variable "enable_availability_tests" {
  description = "Enable Azure Monitor availability tests"
  type        = bool
  default     = true
}

variable "availability_test_frequency" {
  description = "Frequency of availability tests in seconds (300 or 900)"
  type        = number
  default     = 300 # 5 minutes
  validation {
    condition     = contains([300, 900], var.availability_test_frequency)
    error_message = "Availability test frequency must be either 300 (5 min) or 900 (15 min) seconds."
  }
}

variable "availability_test_timeout" {
  description = "Timeout for availability tests in seconds"
  type        = number
  default     = 30
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  # Construct app URL from Front Door endpoint if not provided
  computed_app_url = var.app_url != null ? var.app_url : (
    try(azurerm_cdn_frontdoor_endpoint.main.host_name, null) != null
    ? "https://${azurerm_cdn_frontdoor_endpoint.main.host_name}"
    : "https://honua-${var.environment}.azurewebsites.net"
  )

  # Multi-region test locations
  test_locations = [
    {
      id   = "us-va-ash-azr"
      name = "East US"
    },
    {
      id   = "emea-nl-ams-azr"
      name = "West Europe"
    },
    {
      id   = "apac-sg-sin-azr"
      name = "Southeast Asia"
    },
    {
      id   = "apac-au-syd-azr"
      name = "Australia East"
    },
    {
      id   = "emea-gb-db3-azr"
      name = "UK South"
    }
  ]

  # Endpoints to monitor
  monitored_endpoints = {
    liveness = {
      path        = "/healthz/live"
      description = "Liveness probe - basic availability check"
      expected_status = 200
      severity    = 0 # Critical
    }
    readiness = {
      path        = "/healthz/ready"
      description = "Readiness probe - full stack health check"
      expected_status = 200
      severity    = 1 # Error
    }
    ogc_landing = {
      path        = "/ogc"
      description = "OGC API landing page"
      expected_status = 200
      severity    = 1 # Error
    }
    ogc_conformance = {
      path        = "/ogc/conformance"
      description = "OGC API conformance endpoint"
      expected_status = 200
      severity    = 2 # Warning
    }
    stac_catalog = {
      path        = "/stac"
      description = "STAC catalog endpoint"
      expected_status = 200
      severity    = 2 # Warning
    }
  }
}

# ============================================================================
# Availability Tests
# ============================================================================

# Liveness Endpoint Test (Most Critical)
resource "azurerm_application_insights_standard_web_test" "liveness" {
  count               = var.enable_availability_tests ? 1 : 0
  name                = "honua-liveness-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  application_insights_id = azurerm_application_insights.main.id

  description = "External synthetic monitoring for liveness endpoint"
  enabled     = true
  frequency   = var.availability_test_frequency
  timeout     = var.availability_test_timeout
  retry_enabled = true

  geo_locations = [for loc in local.test_locations : loc.id]

  request {
    url                              = "${local.computed_app_url}${local.monitored_endpoints.liveness.path}"
    http_verb                        = "GET"
    parse_dependent_requests_enabled = false

    # Validate response
    validation_rules {
      expected_status_code        = local.monitored_endpoints.liveness.expected_status
      ssl_check_enabled           = true
      ssl_cert_remaining_lifetime = 7 # Alert if cert expires in < 7 days

      content {
        content_match      = "Healthy"
        pass_if_text_found = true
        ignore_case        = true
      }
    }
  }

  lifecycle {
    ignore_changes = [tags]
  }

  tags = merge(
    local.tags,
    {
      EndpointType = "Liveness"
      Severity     = "Critical"
      TestType     = "Synthetic"
    }
  )
}

# Readiness Endpoint Test
resource "azurerm_application_insights_standard_web_test" "readiness" {
  count               = var.enable_availability_tests ? 1 : 0
  name                = "honua-readiness-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  application_insights_id = azurerm_application_insights.main.id

  description = "External synthetic monitoring for readiness endpoint"
  enabled     = true
  frequency   = var.availability_test_frequency
  timeout     = var.availability_test_timeout
  retry_enabled = true

  geo_locations = [for loc in local.test_locations : loc.id]

  request {
    url                              = "${local.computed_app_url}${local.monitored_endpoints.readiness.path}"
    http_verb                        = "GET"
    parse_dependent_requests_enabled = false

    validation_rules {
      expected_status_code        = local.monitored_endpoints.readiness.expected_status
      ssl_check_enabled           = true
      ssl_cert_remaining_lifetime = 7

      content {
        content_match      = "Healthy"
        pass_if_text_found = true
        ignore_case        = true
      }
    }
  }

  lifecycle {
    ignore_changes = [tags]
  }

  tags = merge(
    local.tags,
    {
      EndpointType = "Readiness"
      Severity     = "Error"
      TestType     = "Synthetic"
    }
  )
}

# OGC API Landing Page Test
resource "azurerm_application_insights_standard_web_test" "ogc_landing" {
  count               = var.enable_availability_tests ? 1 : 0
  name                = "honua-ogc-landing-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  application_insights_id = azurerm_application_insights.main.id

  description = "External synthetic monitoring for OGC API landing page"
  enabled     = true
  frequency   = var.availability_test_frequency
  timeout     = var.availability_test_timeout
  retry_enabled = true

  geo_locations = [for loc in local.test_locations : loc.id]

  request {
    url                              = "${local.computed_app_url}${local.monitored_endpoints.ogc_landing.path}"
    http_verb                        = "GET"
    parse_dependent_requests_enabled = false

    header {
      name  = "Accept"
      value = "application/json"
    }

    validation_rules {
      expected_status_code        = local.monitored_endpoints.ogc_landing.expected_status
      ssl_check_enabled           = true
      ssl_cert_remaining_lifetime = 7

      content {
        content_match      = "\"links\""
        pass_if_text_found = true
        ignore_case        = false
      }
    }
  }

  lifecycle {
    ignore_changes = [tags]
  }

  tags = merge(
    local.tags,
    {
      EndpointType = "OGC-API"
      Severity     = "Error"
      TestType     = "Synthetic"
    }
  )
}

# OGC Conformance Endpoint Test
resource "azurerm_application_insights_standard_web_test" "ogc_conformance" {
  count               = var.enable_availability_tests ? 1 : 0
  name                = "honua-ogc-conformance-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  application_insights_id = azurerm_application_insights.main.id

  description = "External synthetic monitoring for OGC conformance endpoint"
  enabled     = true
  frequency   = var.availability_test_frequency
  timeout     = var.availability_test_timeout
  retry_enabled = true

  geo_locations = [for loc in local.test_locations : loc.id]

  request {
    url                              = "${local.computed_app_url}${local.monitored_endpoints.ogc_conformance.path}"
    http_verb                        = "GET"
    parse_dependent_requests_enabled = false

    header {
      name  = "Accept"
      value = "application/json"
    }

    validation_rules {
      expected_status_code        = local.monitored_endpoints.ogc_conformance.expected_status
      ssl_check_enabled           = true
      ssl_cert_remaining_lifetime = 7

      content {
        content_match      = "\"conformsTo\""
        pass_if_text_found = true
        ignore_case        = false
      }
    }
  }

  lifecycle {
    ignore_changes = [tags]
  }

  tags = merge(
    local.tags,
    {
      EndpointType = "OGC-Conformance"
      Severity     = "Warning"
      TestType     = "Synthetic"
    }
  )
}

# STAC Catalog Endpoint Test
resource "azurerm_application_insights_standard_web_test" "stac_catalog" {
  count               = var.enable_availability_tests ? 1 : 0
  name                = "honua-stac-catalog-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  application_insights_id = azurerm_application_insights.main.id

  description = "External synthetic monitoring for STAC catalog endpoint"
  enabled     = true
  frequency   = var.availability_test_frequency
  timeout     = var.availability_test_timeout
  retry_enabled = true

  geo_locations = [for loc in local.test_locations : loc.id]

  request {
    url                              = "${local.computed_app_url}${local.monitored_endpoints.stac_catalog.path}"
    http_verb                        = "GET"
    parse_dependent_requests_enabled = false

    header {
      name  = "Accept"
      value = "application/json"
    }

    validation_rules {
      expected_status_code        = local.monitored_endpoints.stac_catalog.expected_status
      ssl_check_enabled           = true
      ssl_cert_remaining_lifetime = 7

      content {
        content_match      = "\"type\""
        pass_if_text_found = true
        ignore_case        = false
      }
    }
  }

  lifecycle {
    ignore_changes = [tags]
  }

  tags = merge(
    local.tags,
    {
      EndpointType = "STAC"
      Severity     = "Warning"
      TestType     = "Synthetic"
    }
  )
}

# ============================================================================
# Availability Test Alerts
# ============================================================================

# Alert: Liveness Check Failed (Critical)
resource "azurerm_monitor_metric_alert" "liveness_failed" {
  count               = var.enable_availability_tests ? 1 : 0
  name                = "honua-liveness-failed-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes = [
    azurerm_application_insights.main.id,
    azurerm_application_insights_standard_web_test.liveness[0].id
  ]
  description = "Critical alert when liveness endpoint fails from multiple locations"
  severity    = 0 # Critical
  frequency   = "PT1M"
  window_size = "PT5M"

  application_insights_web_test_location_availability_criteria {
    web_test_id           = azurerm_application_insights_standard_web_test.liveness[0].id
    component_id          = azurerm_application_insights.main.id
    failed_location_count = 2 # Alert if failing from 2+ locations
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType    = "Availability"
      EndpointType = "Liveness"
      Severity     = "Critical"
    }
  )
}

# Alert: Readiness Check Failed (Error)
resource "azurerm_monitor_metric_alert" "readiness_failed" {
  count               = var.enable_availability_tests ? 1 : 0
  name                = "honua-readiness-failed-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes = [
    azurerm_application_insights.main.id,
    azurerm_application_insights_standard_web_test.readiness[0].id
  ]
  description = "Alert when readiness endpoint fails from multiple locations"
  severity    = 1 # Error
  frequency   = "PT1M"
  window_size = "PT5M"

  application_insights_web_test_location_availability_criteria {
    web_test_id           = azurerm_application_insights_standard_web_test.readiness[0].id
    component_id          = azurerm_application_insights.main.id
    failed_location_count = 2
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType    = "Availability"
      EndpointType = "Readiness"
      Severity     = "Error"
    }
  )
}

# Alert: OGC Landing Page Failed
resource "azurerm_monitor_metric_alert" "ogc_landing_failed" {
  count               = var.enable_availability_tests ? 1 : 0
  name                = "honua-ogc-landing-failed-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes = [
    azurerm_application_insights.main.id,
    azurerm_application_insights_standard_web_test.ogc_landing[0].id
  ]
  description = "Alert when OGC landing page fails from multiple locations"
  severity    = 1 # Error
  frequency   = "PT1M"
  window_size = "PT5M"

  application_insights_web_test_location_availability_criteria {
    web_test_id           = azurerm_application_insights_standard_web_test.ogc_landing[0].id
    component_id          = azurerm_application_insights.main.id
    failed_location_count = 3 # Less sensitive for API endpoint
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType    = "Availability"
      EndpointType = "OGC-API"
      Severity     = "Error"
    }
  )
}

# Alert: High Latency from Multiple Regions
resource "azurerm_monitor_metric_alert" "high_global_latency" {
  count               = var.enable_availability_tests ? 1 : 0
  name                = "honua-high-global-latency-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Alert when availability test response time is consistently high"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "availabilityResults/duration"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 2000 # 2 seconds
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Performance"
      Severity  = "Warning"
      TestType  = "Synthetic"
    }
  )
}

# ============================================================================
# Outputs
# ============================================================================

output "availability_tests" {
  value = var.enable_availability_tests ? {
    enabled = true
    app_url = local.computed_app_url
    test_configuration = {
      frequency_seconds = var.availability_test_frequency
      timeout_seconds   = var.availability_test_timeout
      retry_enabled     = true
      ssl_check_enabled = true
      cert_expiry_days  = 7
    }
    test_locations = [for loc in local.test_locations : {
      id   = loc.id
      name = loc.name
    }]
    monitored_endpoints = [for key, endpoint in local.monitored_endpoints : {
      name            = key
      path            = endpoint.path
      description     = endpoint.description
      expected_status = endpoint.expected_status
      severity        = endpoint.severity
      test_id         = try(azurerm_application_insights_standard_web_test.liveness[0].id, null)
    }]
    alert_configuration = {
      liveness_alert_threshold  = 2 # locations
      readiness_alert_threshold = 2 # locations
      ogc_alert_threshold       = 3 # locations
      latency_threshold_ms      = 2000
    }
    dashboards = {
      azure_portal = "https://portal.azure.com/#@/resource${azurerm_application_insights.main.id}/availability"
      application_insights = azurerm_application_insights.main.app_id
    }
  } : {
    enabled = false
    message = "Availability tests are disabled. Set enable_availability_tests = true to enable."
  }
  description = "Azure Monitor availability test configuration and status"
}

output "availability_test_urls" {
  value = var.enable_availability_tests ? {
    liveness       = "${local.computed_app_url}${local.monitored_endpoints.liveness.path}"
    readiness      = "${local.computed_app_url}${local.monitored_endpoints.readiness.path}"
    ogc_landing    = "${local.computed_app_url}${local.monitored_endpoints.ogc_landing.path}"
    ogc_conformance = "${local.computed_app_url}${local.monitored_endpoints.ogc_conformance.path}"
    stac_catalog   = "${local.computed_app_url}${local.monitored_endpoints.stac_catalog.path}"
  } : null
  description = "URLs being monitored by availability tests"
}
