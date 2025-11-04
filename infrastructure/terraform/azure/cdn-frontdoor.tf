# ============================================================================
# Azure Front Door CDN Configuration for Honua Server
# ============================================================================
# Azure Front Door Premium with integrated WAF and caching for global delivery.
# Features:
#   - Global anycast network with edge locations
#   - Integrated WAF with DDoS protection
#   - Multi-origin support (Container Apps, AKS, Storage)
#   - Optimized caching policies for tiles vs metadata
#   - SSL/TLS with managed certificates
#   - Custom domain support
#   - Real-time analytics and monitoring
# ============================================================================

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.80"
    }
    azapi = {
      source  = "Azure/azapi"
      version = "~> 1.10"
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "resource_group_name" {
  description = "Resource group name"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "eastus"
}

variable "custom_domain" {
  description = "Custom domain name (e.g., tiles.honua.io)"
  type        = string
  default     = ""
}

variable "origin_hostname" {
  description = "Origin hostname (Container App, AKS ingress, or App Service)"
  type        = string
}

variable "storage_account_name" {
  description = "Storage account name for raster cache (optional)"
  type        = string
  default     = ""
}

variable "enable_waf" {
  description = "Enable WAF Premium tier"
  type        = bool
  default     = true
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "prod"
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  frontdoor_name = "honua-fd-${var.environment}"
  unique_suffix  = substr(md5("${var.resource_group_name}-${var.environment}"), 0, 6)

  tags = {
    Environment = var.environment
    Project     = "HonuaIO"
    ManagedBy   = "Terraform"
    Component   = "CDN"
  }

  # Origin names
  origin_honua_server = "honua-server"
  origin_storage      = "storage-raster-cache"
}

# ============================================================================
# Azure Front Door Profile
# ============================================================================

resource "azurerm_cdn_frontdoor_profile" "honua" {
  name                = local.frontdoor_name
  resource_group_name = var.resource_group_name
  sku_name            = var.enable_waf ? "Premium_AzureFrontDoor" : "Standard_AzureFrontDoor"

  tags = local.tags
}

# ============================================================================
# Front Door Endpoint
# ============================================================================

resource "azurerm_cdn_frontdoor_endpoint" "honua" {
  name                     = "honua-endpoint-${local.unique_suffix}"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua.id

  tags = local.tags
}

# ============================================================================
# Origin Groups
# ============================================================================

# Honua Server Origin Group
resource "azurerm_cdn_frontdoor_origin_group" "honua_server" {
  name                     = local.origin_honua_server
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua.id

  load_balancing {
    sample_size                 = 4
    successful_samples_required = 3
    additional_latency_in_milliseconds = 50
  }

  health_probe {
    interval_in_seconds = 30
    path                = "/health"
    protocol            = "Https"
    request_type        = "GET"
  }

  session_affinity_enabled = false
}

# Storage Origin Group (for raster cache)
resource "azurerm_cdn_frontdoor_origin_group" "storage" {
  count                    = var.storage_account_name != "" ? 1 : 0
  name                     = local.origin_storage
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua.id

  load_balancing {
    sample_size                 = 4
    successful_samples_required = 3
    additional_latency_in_milliseconds = 50
  }

  health_probe {
    interval_in_seconds = 30
    path                = "/"
    protocol            = "Https"
    request_type        = "HEAD"
  }

  session_affinity_enabled = false
}

# ============================================================================
# Origins
# ============================================================================

# Honua Server Origin
resource "azurerm_cdn_frontdoor_origin" "honua_server" {
  name                          = local.origin_honua_server
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.honua_server.id

  enabled                        = true
  host_name                      = var.origin_hostname
  http_port                      = 80
  https_port                     = 443
  origin_host_header             = var.origin_hostname
  priority                       = 1
  weight                         = 1000
  certificate_name_check_enabled = true
}

# Storage Origin (for raster cache)
resource "azurerm_cdn_frontdoor_origin" "storage" {
  count                         = var.storage_account_name != "" ? 1 : 0
  name                          = local.origin_storage
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.storage[0].id

  enabled                        = true
  host_name                      = "${var.storage_account_name}.blob.core.windows.net"
  http_port                      = 80
  https_port                     = 443
  origin_host_header             = "${var.storage_account_name}.blob.core.windows.net"
  priority                       = 1
  weight                         = 1000
  certificate_name_check_enabled = true
}

# ============================================================================
# Caching Rule Sets
# ============================================================================

# Tiles Caching Rule Set
resource "azurerm_cdn_frontdoor_rule_set" "tiles_caching" {
  name                     = "TilesCaching"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua.id
}

resource "azurerm_cdn_frontdoor_rule" "tiles_cache_control" {
  name                      = "TilesCacheControl"
  cdn_frontdoor_rule_set_id = azurerm_cdn_frontdoor_rule_set.tiles_caching.id
  order                     = 1

  actions {
    route_configuration_override_action {
      cache_duration = "1.00:00:00" # 1 day
      cache_behavior = "OverrideAlways"
      compression_enabled = true
    }

    response_header_action {
      header_action = "Append"
      header_name   = "Cache-Control"
      value         = "public, max-age=86400, stale-while-revalidate=3600"
    }

    response_header_action {
      header_action = "Append"
      header_name   = "X-CDN-Provider"
      value         = "AzureFrontDoor"
    }
  }

  conditions {
    url_path_condition {
      operator         = "BeginsWith"
      match_values     = ["/wms", "/wmts", "/ogc/collections"]
      transforms       = ["Lowercase"]
      negate_condition = false
    }
  }
}

# Metadata Caching Rule Set
resource "azurerm_cdn_frontdoor_rule_set" "metadata_caching" {
  name                     = "MetadataCaching"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua.id
}

resource "azurerm_cdn_frontdoor_rule" "metadata_cache_control" {
  name                      = "MetadataCacheControl"
  cdn_frontdoor_rule_set_id = azurerm_cdn_frontdoor_rule_set.metadata_caching.id
  order                     = 1

  actions {
    route_configuration_override_action {
      cache_duration = "00:05:00" # 5 minutes
      cache_behavior = "OverrideAlways"
      compression_enabled = true
    }

    response_header_action {
      header_action = "Append"
      header_name   = "Cache-Control"
      value         = "public, max-age=300"
    }
  }

  conditions {
    url_path_condition {
      operator         = "BeginsWith"
      match_values     = ["/stac", "/ogc/conformance", "/ogc/api"]
      transforms       = ["Lowercase"]
      negate_condition = false
    }
  }
}

# Admin No-Cache Rule Set
resource "azurerm_cdn_frontdoor_rule_set" "admin_no_cache" {
  name                     = "AdminNoCache"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua.id
}

resource "azurerm_cdn_frontdoor_rule" "admin_bypass_cache" {
  name                      = "AdminBypassCache"
  cdn_frontdoor_rule_set_id = azurerm_cdn_frontdoor_rule_set.admin_no_cache.id
  order                     = 1

  actions {
    route_configuration_override_action {
      cache_behavior = "Disabled"
    }

    response_header_action {
      header_action = "Append"
      header_name   = "Cache-Control"
      value         = "no-store, no-cache, must-revalidate"
    }
  }

  conditions {
    url_path_condition {
      operator         = "BeginsWith"
      match_values     = ["/admin"]
      transforms       = ["Lowercase"]
      negate_condition = false
    }
  }
}

# ============================================================================
# Routes
# ============================================================================

# Default Route (Tiles)
resource "azurerm_cdn_frontdoor_route" "default" {
  name                          = "default-route"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.honua.id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.honua_server.id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.honua_server.id]

  patterns_to_match            = ["/*"]
  supported_protocols          = ["Http", "Https"]
  https_redirect_enabled       = true
  forwarding_protocol          = "HttpsOnly"
  link_to_default_domain       = true
  enabled                      = true

  cdn_frontdoor_rule_set_ids = [
    azurerm_cdn_frontdoor_rule_set.tiles_caching.id
  ]

  cache {
    query_string_caching_behavior = "IncludeSpecifiedQueryStrings"
    query_strings = [
      "TIME", "datetime", "LAYERS", "STYLES", "FORMAT", "CRS", "BBOX",
      "WIDTH", "HEIGHT", "TILEMATRIX", "TILEMATRIXSET", "TILEROW", "TILECOL",
      "f", "styleId"
    ]
    compression_enabled = true
  }
}

# Metadata Route
resource "azurerm_cdn_frontdoor_route" "metadata" {
  name                          = "metadata-route"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.honua.id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.honua_server.id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.honua_server.id]

  patterns_to_match            = ["/stac/*", "/ogc/conformance", "/ogc/api"]
  supported_protocols          = ["Http", "Https"]
  https_redirect_enabled       = true
  forwarding_protocol          = "HttpsOnly"
  link_to_default_domain       = true
  enabled                      = true

  cdn_frontdoor_rule_set_ids = [
    azurerm_cdn_frontdoor_rule_set.metadata_caching.id
  ]

  cache {
    query_string_caching_behavior = "IncludeSpecifiedQueryStrings"
    query_strings                 = ["SERVICE", "VERSION", "REQUEST", "f", "limit", "offset"]
    compression_enabled           = true
  }
}

# Admin Route
resource "azurerm_cdn_frontdoor_route" "admin" {
  name                          = "admin-route"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.honua.id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.honua_server.id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.honua_server.id]

  patterns_to_match            = ["/admin/*"]
  supported_protocols          = ["Https"]
  https_redirect_enabled       = true
  forwarding_protocol          = "HttpsOnly"
  link_to_default_domain       = true
  enabled                      = true

  cdn_frontdoor_rule_set_ids = [
    azurerm_cdn_frontdoor_rule_set.admin_no_cache.id
  ]
}

# Storage Route (for raster cache)
resource "azurerm_cdn_frontdoor_route" "storage" {
  count                         = var.storage_account_name != "" ? 1 : 0
  name                          = "storage-route"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.honua.id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.storage[0].id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.storage[0].id]

  patterns_to_match            = ["/raster-cache/*"]
  supported_protocols          = ["Http", "Https"]
  https_redirect_enabled       = true
  forwarding_protocol          = "HttpsOnly"
  link_to_default_domain       = true
  enabled                      = true

  cdn_frontdoor_rule_set_ids = [
    azurerm_cdn_frontdoor_rule_set.tiles_caching.id
  ]

  cache {
    query_string_caching_behavior = "IgnoreQueryString"
    compression_enabled           = true
  }
}

# ============================================================================
# Custom Domain (Optional)
# ============================================================================

resource "azurerm_cdn_frontdoor_custom_domain" "honua" {
  count                    = var.custom_domain != "" ? 1 : 0
  name                     = replace(var.custom_domain, ".", "-")
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua.id
  host_name                = var.custom_domain

  tls {
    certificate_type    = "ManagedCertificate"
    minimum_tls_version = "TLS12"
  }
}

resource "azurerm_cdn_frontdoor_custom_domain_association" "honua" {
  count                          = var.custom_domain != "" ? 1 : 0
  cdn_frontdoor_custom_domain_id = azurerm_cdn_frontdoor_custom_domain.honua[0].id
  cdn_frontdoor_route_ids = [
    azurerm_cdn_frontdoor_route.default.id,
    azurerm_cdn_frontdoor_route.metadata.id,
    azurerm_cdn_frontdoor_route.admin.id
  ]
}

# ============================================================================
# WAF Policy (Premium Tier)
# ============================================================================

resource "azurerm_cdn_frontdoor_firewall_policy" "honua" {
  count                            = var.enable_waf ? 1 : 0
  name                             = "honuawafpolicy${local.unique_suffix}"
  resource_group_name              = var.resource_group_name
  sku_name                         = "Premium_AzureFrontDoor"
  enabled                          = true
  mode                             = "Prevention"
  custom_block_response_status_code = 403

  # Rate limiting rule
  custom_rule {
    name                           = "RateLimitRule"
    enabled                        = true
    priority                       = 1
    rate_limit_duration_in_minutes = 1
    rate_limit_threshold           = 2000
    type                           = "RateLimitRule"
    action                         = "Block"

    match_condition {
      match_variable     = "RemoteAddr"
      operator           = "IPMatch"
      match_values       = ["0.0.0.0/0", "::/0"]
    }
  }

  # Geo-filtering rule (optional)
  custom_rule {
    name     = "BlockByCountry"
    enabled  = false # Enable if needed
    priority = 2
    type     = "MatchRule"
    action   = "Block"

    match_condition {
      match_variable = "RemoteAddr"
      operator       = "GeoMatch"
      match_values   = ["CN", "RU"] # Example: block China, Russia
    }
  }

  # Azure Managed Rule Set - Default Rule Set
  managed_rule {
    type    = "DefaultRuleSet"
    version = "1.0"
    action  = "Block"

    override {
      rule_group_name = "PROTOCOL-ATTACK"
      rule {
        rule_id = "944240"
        enabled = true
        action  = "Block"
      }
    }
  }

  # Azure Managed Rule Set - Bot Manager
  managed_rule {
    type    = "Microsoft_BotManagerRuleSet"
    version = "1.0"
    action  = "Block"
  }

  tags = local.tags
}

# Associate WAF with Front Door
resource "azurerm_cdn_frontdoor_security_policy" "honua" {
  count                    = var.enable_waf ? 1 : 0
  name                     = "honua-security-policy"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua.id

  security_policies {
    firewall {
      cdn_frontdoor_firewall_policy_id = azurerm_cdn_frontdoor_firewall_policy.honua[0].id

      association {
        patterns_to_match = ["/*"]
        domain {
          cdn_frontdoor_domain_id = azurerm_cdn_frontdoor_endpoint.honua.id
        }
      }

      dynamic "association" {
        for_each = var.custom_domain != "" ? [1] : []
        content {
          patterns_to_match = ["/*"]
          domain {
            cdn_frontdoor_domain_id = azurerm_cdn_frontdoor_custom_domain.honua[0].id
          }
        }
      }
    }
  }
}

# ============================================================================
# Diagnostic Settings
# ============================================================================

resource "azurerm_monitor_diagnostic_setting" "frontdoor" {
  name                       = "frontdoor-diagnostics"
  target_resource_id         = azurerm_cdn_frontdoor_profile.honua.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.cdn.id

  enabled_log {
    category = "FrontDoorAccessLog"
  }

  enabled_log {
    category = "FrontDoorHealthProbeLog"
  }

  enabled_log {
    category = "FrontDoorWebApplicationFirewallLog"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}

resource "azurerm_log_analytics_workspace" "cdn" {
  name                = "honua-cdn-logs-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30

  tags = local.tags
}

# ============================================================================
# Outputs
# ============================================================================

output "frontdoor_id" {
  description = "Front Door profile ID"
  value       = azurerm_cdn_frontdoor_profile.honua.id
}

output "frontdoor_endpoint_hostname" {
  description = "Front Door endpoint hostname"
  value       = azurerm_cdn_frontdoor_endpoint.honua.host_name
}

output "custom_domain_validation_token" {
  description = "Custom domain validation token"
  value       = var.custom_domain != "" ? azurerm_cdn_frontdoor_custom_domain.honua[0].validation_token : null
}

output "cache_purge_command" {
  description = "Azure CLI command to purge cache"
  value       = "az afd endpoint purge --resource-group ${var.resource_group_name} --profile-name ${azurerm_cdn_frontdoor_profile.honua.name} --endpoint-name ${azurerm_cdn_frontdoor_endpoint.honua.name} --content-paths '/*'"
}

output "waf_policy_id" {
  description = "WAF policy ID"
  value       = var.enable_waf ? azurerm_cdn_frontdoor_firewall_policy.honua[0].id : null
}

output "deployment_summary" {
  description = "CDN deployment summary"
  value = {
    profile_name       = azurerm_cdn_frontdoor_profile.honua.name
    endpoint_hostname  = azurerm_cdn_frontdoor_endpoint.honua.host_name
    custom_domain      = var.custom_domain
    environment        = var.environment
    sku                = azurerm_cdn_frontdoor_profile.honua.sku_name
    waf_enabled        = var.enable_waf
    storage_integrated = var.storage_account_name != ""
  }
}
