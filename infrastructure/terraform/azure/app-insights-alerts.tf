# ============================================================================
# Application Insights Alert Rules
# ============================================================================
# Comprehensive monitoring alerts for Honua AI Deployment Consultant
#
# Alert Categories:
# 1. Availability Monitoring (Low availability, service down)
# 2. Performance Monitoring (High latency, slow responses)
# 3. Error Rate Monitoring (High error rate, failed requests)
# 4. Resource Monitoring (High memory, CPU usage)
# 5. AI/LLM Monitoring (Token usage, rate limits)
# 6. Database Monitoring (Connection failures, slow queries)
#
# Alert Severity Levels:
# - 0: Critical (immediate action required)
# - 1: Error (requires attention within hours)
# - 2: Warning (requires attention within days)
# - 3: Informational (awareness only)
# ============================================================================

# ============================================================================
# Action Groups for Notifications
# ============================================================================

# Primary action group for critical alerts
resource "azurerm_monitor_action_group" "critical_alerts" {
  name                = "ag-honua-critical-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  short_name          = "critical"

  email_receiver {
    name                    = "admin-email"
    email_address           = var.admin_email
    use_common_alert_schema = true
  }

  # SMS notification for critical alerts (uncomment and configure if needed)
  # sms_receiver {
  #   name         = "admin-sms"
  #   country_code = "1"
  #   phone_number = "5551234567"
  # }

  # Azure Mobile App push notification
  azure_app_push_receiver {
    name          = "azure-app"
    email_address = var.admin_email
  }

  # Webhook for integration with external systems
  webhook_receiver {
    name        = "alert-webhook"
    service_uri = "https://hooks.slack.com/services/YOUR/WEBHOOK/URL" # Replace with actual webhook
    use_common_alert_schema = true
  }

  tags = local.tags
}

# Secondary action group for warning-level alerts
resource "azurerm_monitor_action_group" "warning_alerts" {
  name                = "ag-honua-warning-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  short_name          = "warning"

  email_receiver {
    name                    = "admin-email"
    email_address           = var.admin_email
    use_common_alert_schema = true
  }

  tags = local.tags
}

# ============================================================================
# 1. AVAILABILITY ALERTS
# ============================================================================

# Alert: Low Availability (<99%)
resource "azurerm_monitor_metric_alert" "low_availability" {
  name                = "honua-low-availability-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Alert when application availability falls below 99%"
  severity            = 0 # Critical
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "availabilityResults/availabilityPercentage"
    aggregation      = "Average"
    operator         = "LessThan"
    threshold        = 99.0
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Availability"
      Severity  = "Critical"
      SLA       = "99%"
    }
  )
}

# Alert: Service Completely Down (0 requests)
resource "azurerm_monitor_metric_alert" "service_down" {
  name                = "honua-service-down-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Alert when no requests are being processed (service down)"
  severity            = 0 # Critical
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "requests/count"
    aggregation      = "Count"
    operator         = "LessThan"
    threshold        = 1
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Availability"
      Severity  = "Critical"
    }
  )
}

# ============================================================================
# 2. PERFORMANCE ALERTS
# ============================================================================

# Alert: High Response Time (P95 > 1s)
resource "azurerm_monitor_metric_alert" "high_response_time" {
  name                = "honua-high-response-time-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Alert when P95 response time exceeds 1 second"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "requests/duration"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 1000 # 1 second in milliseconds
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Performance"
      Severity  = "Warning"
      Threshold = "1000ms"
    }
  )
}

# Alert: Very High Response Time (P95 > 5s) - Critical
resource "azurerm_monitor_metric_alert" "critical_response_time" {
  name                = "honua-critical-response-time-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Critical alert when P95 response time exceeds 5 seconds"
  severity            = 1 # Error
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "requests/duration"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 5000 # 5 seconds in milliseconds
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Performance"
      Severity  = "Error"
      Threshold = "5000ms"
    }
  )
}

# Alert: Slow Dependency Calls (AI/Database)
resource "azurerm_monitor_metric_alert" "slow_dependencies" {
  name                = "honua-slow-dependencies-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Alert when dependency calls (AI, DB) are slow"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "dependencies/duration"
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
    }
  )
}

# ============================================================================
# 3. ERROR RATE ALERTS
# ============================================================================

# Alert: High Error Rate (>5%)
resource "azurerm_monitor_metric_alert" "high_error_rate" {
  name                = "honua-high-error-rate-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Alert when error rate exceeds 5%"
  severity            = 1 # Error
  frequency           = "PT5M"
  window_size         = "PT15M"

  dynamic_criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "requests/failed"
    aggregation      = "Count"
    operator         = "GreaterThan"
    alert_sensitivity = "Medium"

    # Dynamic threshold based on historical data
    evaluation_total_count = 4
    evaluation_failure_count = 3
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "ErrorRate"
      Severity  = "Error"
      Threshold = "5%"
    }
  )
}

# Alert: High Exception Rate
resource "azurerm_monitor_metric_alert" "high_exception_rate" {
  name                = "honua-high-exception-rate-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Alert when exception rate is abnormally high"
  severity            = 1 # Error
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "exceptions/count"
    aggregation      = "Count"
    operator         = "GreaterThan"
    threshold        = 10 # More than 10 exceptions in 15 minutes
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "ErrorRate"
      Severity  = "Error"
    }
  )
}

# Alert: Failed Dependency Calls
resource "azurerm_monitor_metric_alert" "failed_dependencies" {
  name                = "honua-failed-dependencies-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Alert when dependency calls (AI, DB) are failing"
  severity            = 1 # Error
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "dependencies/failed"
    aggregation      = "Count"
    operator         = "GreaterThan"
    threshold        = 5 # More than 5 failed dependencies in 15 minutes
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "ErrorRate"
      Severity  = "Error"
    }
  )
}

# ============================================================================
# 4. RESOURCE MONITORING ALERTS
# ============================================================================

# Alert: High Memory Usage (>90%)
resource "azurerm_monitor_metric_alert" "high_memory_usage" {
  name                = "honua-high-memory-usage-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_linux_function_app.main.id]
  description         = "Alert when memory usage exceeds 90%"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "MemoryPercentage"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 90
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Resource"
      Severity  = "Warning"
      Threshold = "90%"
    }
  )
}

# Alert: Critical Memory Usage (>95%)
resource "azurerm_monitor_metric_alert" "critical_memory_usage" {
  name                = "honua-critical-memory-usage-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_linux_function_app.main.id]
  description         = "Critical alert when memory usage exceeds 95%"
  severity            = 0 # Critical
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "MemoryPercentage"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 95
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Resource"
      Severity  = "Critical"
      Threshold = "95%"
    }
  )
}

# Alert: High CPU Usage (>80%)
resource "azurerm_monitor_metric_alert" "high_cpu_usage" {
  name                = "honua-high-cpu-usage-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_linux_function_app.main.id]
  description         = "Alert when CPU usage exceeds 80%"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "CpuPercentage"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Resource"
      Severity  = "Warning"
      Threshold = "80%"
    }
  )
}

# Alert: Function App HTTP Queue Length (backlog)
resource "azurerm_monitor_metric_alert" "http_queue_length" {
  name                = "honua-http-queue-length-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_linux_function_app.main.id]
  description         = "Alert when HTTP request queue is building up"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "HttpQueueLength"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 10 # More than 10 requests queued
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Resource"
      Severity  = "Warning"
    }
  )
}

# ============================================================================
# 5. AI/LLM MONITORING ALERTS
# ============================================================================

# Alert: Azure OpenAI Rate Limit Errors
resource "azurerm_monitor_metric_alert" "openai_rate_limit" {
  name                = "honua-openai-rate-limit-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_cognitive_account.openai.id]
  description         = "Alert when OpenAI API rate limits are being hit"
  severity            = 1 # Error
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.CognitiveServices/accounts"
    metric_name      = "RateLimitExceeded"
    aggregation      = "Count"
    operator         = "GreaterThan"
    threshold        = 5 # More than 5 rate limit errors
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "AI/LLM"
      Severity  = "Error"
    }
  )
}

# Alert: High Token Usage (approaching quota)
resource "azurerm_monitor_metric_alert" "high_token_usage" {
  name                = "honua-high-token-usage-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_cognitive_account.openai.id]
  description         = "Alert when token usage is approaching quota limits"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT1H"

  criteria {
    metric_namespace = "Microsoft.CognitiveServices/accounts"
    metric_name      = "TokensGenerated"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 500000 # 500K tokens per hour (adjust based on quota)
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "AI/LLM"
      Severity  = "Warning"
    }
  )
}

# Alert: AI Search Service Throttling
resource "azurerm_monitor_metric_alert" "search_throttling" {
  name                = "honua-search-throttling-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_search_service.main.id]
  description         = "Alert when AI Search is being throttled"
  severity            = 1 # Error
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.Search/searchServices"
    metric_name      = "ThrottledSearchQueriesPercentage"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 10 # More than 10% throttled
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "AI/LLM"
      Severity  = "Error"
    }
  )
}

# ============================================================================
# 6. DATABASE MONITORING ALERTS
# ============================================================================

# Alert: High Database CPU Usage (>80%)
resource "azurerm_monitor_metric_alert" "database_high_cpu" {
  name                = "honua-database-high-cpu-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_postgresql_flexible_server.main.id]
  description         = "Alert when database CPU usage exceeds 80%"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "cpu_percent"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Database"
      Severity  = "Warning"
      Threshold = "80%"
    }
  )
}

# Alert: High Database Memory Usage (>90%)
resource "azurerm_monitor_metric_alert" "database_high_memory" {
  name                = "honua-database-high-memory-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_postgresql_flexible_server.main.id]
  description         = "Alert when database memory usage exceeds 90%"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "memory_percent"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 90
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Database"
      Severity  = "Warning"
      Threshold = "90%"
    }
  )
}

# Alert: High Active Connections (>80% of max)
resource "azurerm_monitor_metric_alert" "database_high_connections" {
  name                = "honua-database-high-connections-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_postgresql_flexible_server.main.id]
  description         = "Alert when active connections exceed 80% of maximum"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "active_connections"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80 # Adjust based on your max_connections setting
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Database"
      Severity  = "Warning"
    }
  )
}

# Alert: Database Connection Failed
resource "azurerm_monitor_metric_alert" "database_connection_failed" {
  name                = "honua-database-connection-failed-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_postgresql_flexible_server.main.id]
  description         = "Alert when database connection attempts are failing"
  severity            = 0 # Critical
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "connections_failed"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 5 # More than 5 failed connections
  }

  action {
    action_group_id = azurerm_monitor_action_group.critical_alerts.id
  }

  tags = merge(
    local.tags,
    {
      AlertType = "Database"
      Severity  = "Critical"
    }
  )
}

# ============================================================================
# 7. SMART DETECTION (Anomaly Detection)
# ============================================================================

# Enable Application Insights Smart Detection for anomalies
resource "azurerm_application_insights_smart_detection_rule" "failure_anomalies" {
  name                    = "Failure Anomalies"
  application_insights_id = azurerm_application_insights.main.id
  enabled                 = true
  send_emails_to_subscription_owners = false

  additional_email_recipients = [var.admin_email]
}

resource "azurerm_application_insights_smart_detection_rule" "slow_page_load" {
  name                    = "Slow page load time"
  application_insights_id = azurerm_application_insights.main.id
  enabled                 = true
  send_emails_to_subscription_owners = false

  additional_email_recipients = [var.admin_email]
}

resource "azurerm_application_insights_smart_detection_rule" "slow_dependency" {
  name                    = "Slow server response time"
  application_insights_id = azurerm_application_insights.main.id
  enabled                 = true
  send_emails_to_subscription_owners = false

  additional_email_recipients = [var.admin_email]
}

# ============================================================================
# Outputs
# ============================================================================

output "alert_configuration" {
  value = {
    action_groups = {
      critical = azurerm_monitor_action_group.critical_alerts.id
      warning  = azurerm_monitor_action_group.warning_alerts.id
    }
    alert_counts = {
      availability_alerts = 2
      performance_alerts  = 3
      error_rate_alerts   = 3
      resource_alerts     = 4
      ai_llm_alerts      = 3
      database_alerts     = 4
      smart_detection     = 3
      total_alerts        = 22
    }
    notification_channels = {
      email         = var.admin_email
      azure_app     = "enabled"
      webhook       = "configured"
      smart_detect  = "enabled"
    }
  }
  description = "Application Insights alert configuration summary"
}

output "alert_rules" {
  value = {
    critical_alerts = [
      azurerm_monitor_metric_alert.low_availability.name,
      azurerm_monitor_metric_alert.service_down.name,
      azurerm_monitor_metric_alert.critical_memory_usage.name,
      azurerm_monitor_metric_alert.database_connection_failed.name
    ]
    error_alerts = [
      azurerm_monitor_metric_alert.high_error_rate.name,
      azurerm_monitor_metric_alert.high_exception_rate.name,
      azurerm_monitor_metric_alert.failed_dependencies.name,
      azurerm_monitor_metric_alert.openai_rate_limit.name,
      azurerm_monitor_metric_alert.search_throttling.name
    ]
    warning_alerts = [
      azurerm_monitor_metric_alert.high_response_time.name,
      azurerm_monitor_metric_alert.slow_dependencies.name,
      azurerm_monitor_metric_alert.high_memory_usage.name,
      azurerm_monitor_metric_alert.high_cpu_usage.name,
      azurerm_monitor_metric_alert.http_queue_length.name,
      azurerm_monitor_metric_alert.high_token_usage.name,
      azurerm_monitor_metric_alert.database_high_cpu.name,
      azurerm_monitor_metric_alert.database_high_memory.name,
      azurerm_monitor_metric_alert.database_high_connections.name
    ]
  }
  description = "List of configured alert rules by severity"
}
