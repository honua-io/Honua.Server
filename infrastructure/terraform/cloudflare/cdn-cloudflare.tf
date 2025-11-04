# ============================================================================
# Cloudflare CDN Configuration for Honua Server
# ============================================================================
# Cloudflare with integrated WAF, DDoS protection, and global edge network.
# Features:
#   - 200+ edge locations globally
#   - Integrated WAF and DDoS protection (Pro plan and above)
#   - Optimized caching rules for tiles vs metadata
#   - Page Rules for fine-grained control
#   - SSL/TLS with flexible encryption modes
#   - Custom domain support
#   - Real-time analytics
#   - Argo Smart Routing (optional, Business plan+)
# ============================================================================

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 4.0"
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "cloudflare_api_token" {
  description = "Cloudflare API token with Zone.Edit permissions"
  type        = string
  sensitive   = true
}

variable "zone_name" {
  description = "Cloudflare zone name (e.g., honua.io)"
  type        = string
}

variable "subdomain" {
  description = "Subdomain for tiles (e.g., tiles, cdn, api)"
  type        = string
  default     = "tiles"
}

variable "origin_hostname" {
  description = "Origin server hostname or IP"
  type        = string
}

variable "enable_waf" {
  description = "Enable Cloudflare WAF (requires Pro plan or above)"
  type        = bool
  default     = true
}

variable "enable_argo" {
  description = "Enable Argo Smart Routing (requires Business plan or above)"
  type        = bool
  default     = false
}

variable "enable_rate_limiting" {
  description = "Enable rate limiting (requires Enterprise plan for advanced rules)"
  type        = bool
  default     = true
}

variable "ssl_mode" {
  description = "SSL mode: off, flexible, full, strict"
  type        = string
  default     = "full"

  validation {
    condition     = contains(["off", "flexible", "full", "strict"], var.ssl_mode)
    error_message = "SSL mode must be off, flexible, full, or strict."
  }
}

variable "min_tls_version" {
  description = "Minimum TLS version: 1.0, 1.1, 1.2, 1.3"
  type        = string
  default     = "1.2"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "prod"
}

# ============================================================================
# Provider Configuration
# ============================================================================

provider "cloudflare" {
  api_token = var.cloudflare_api_token
}

# ============================================================================
# Data Sources
# ============================================================================

data "cloudflare_zone" "honua" {
  name = var.zone_name
}

# ============================================================================
# DNS Records
# ============================================================================

# Main CDN subdomain
resource "cloudflare_record" "cdn" {
  zone_id = data.cloudflare_zone.honua.id
  name    = var.subdomain
  value   = var.origin_hostname
  type    = "CNAME"
  proxied = true
  comment = "Honua CDN endpoint - ${var.environment}"
}

# ============================================================================
# Page Rules (for granular caching control)
# ============================================================================

# Rule 1: Tiles - Cache Everything with long TTL
resource "cloudflare_page_rule" "tiles_cache" {
  zone_id  = data.cloudflare_zone.honua.id
  target   = "${var.subdomain}.${var.zone_name}/wms*"
  priority = 1

  actions {
    cache_level         = "cache_everything"
    edge_cache_ttl      = 2592000 # 30 days
    browser_cache_ttl   = 86400   # 1 day
    cache_on_cookie     = ""
    cache_deception_armor = "on"
    security_level      = "medium"
  }
}

resource "cloudflare_page_rule" "wmts_cache" {
  zone_id  = data.cloudflare_zone.honua.id
  target   = "${var.subdomain}.${var.zone_name}/wmts*"
  priority = 2

  actions {
    cache_level         = "cache_everything"
    edge_cache_ttl      = 2592000 # 30 days
    browser_cache_ttl   = 86400   # 1 day
    cache_on_cookie     = ""
    cache_deception_armor = "on"
    security_level      = "medium"
  }
}

resource "cloudflare_page_rule" "ogc_tiles_cache" {
  zone_id  = data.cloudflare_zone.honua.id
  target   = "${var.subdomain}.${var.zone_name}/ogc/collections/*/tiles/*"
  priority = 3

  actions {
    cache_level         = "cache_everything"
    edge_cache_ttl      = 2592000 # 30 days
    browser_cache_ttl   = 86400   # 1 day
    cache_on_cookie     = ""
    cache_deception_armor = "on"
    security_level      = "medium"
  }
}

# Rule 2: Metadata - Cache with shorter TTL
resource "cloudflare_page_rule" "metadata_cache" {
  zone_id  = data.cloudflare_zone.honua.id
  target   = "${var.subdomain}.${var.zone_name}/stac*"
  priority = 4

  actions {
    cache_level       = "cache_everything"
    edge_cache_ttl    = 300  # 5 minutes
    browser_cache_ttl = 300  # 5 minutes
    cache_on_cookie   = ""
  }
}

resource "cloudflare_page_rule" "ogc_collections_cache" {
  zone_id  = data.cloudflare_zone.honua.id
  target   = "${var.subdomain}.${var.zone_name}/ogc/collections"
  priority = 5

  actions {
    cache_level       = "cache_everything"
    edge_cache_ttl    = 300  # 5 minutes
    browser_cache_ttl = 300  # 5 minutes
    cache_on_cookie   = ""
  }
}

# Rule 3: Admin endpoints - No caching
resource "cloudflare_page_rule" "admin_no_cache" {
  zone_id  = data.cloudflare_zone.honua.id
  target   = "${var.subdomain}.${var.zone_name}/admin/*"
  priority = 6

  actions {
    cache_level     = "bypass"
    security_level  = "high"
    disable_performance = false
  }
}

# Rule 4: Health check - No caching
resource "cloudflare_page_rule" "health_no_cache" {
  zone_id  = data.cloudflare_zone.honua.id
  target   = "${var.subdomain}.${var.zone_name}/health"
  priority = 7

  actions {
    cache_level = "bypass"
  }
}

# ============================================================================
# Cache Rules (Modern replacement for Page Rules)
# ============================================================================

resource "cloudflare_ruleset" "cache_rules" {
  zone_id     = data.cloudflare_zone.honua.id
  name        = "Honua Cache Rules"
  description = "Advanced caching rules for Honua tiles and metadata"
  kind        = "zone"
  phase       = "http_request_cache_settings"

  # Rule 1: Tiles - Long cache
  rules {
    description = "Cache tiles with long TTL"
    expression  = "(http.request.uri.path matches \"^/(wms|wmts|ogc/collections/.*/tiles).*\")"
    action      = "set_cache_settings"

    action_parameters {
      cache = true
      edge_ttl {
        mode    = "override_origin"
        default = 2592000 # 30 days
      }
      browser_ttl {
        mode    = "override_origin"
        default = 86400 # 1 day
      }
      cache_key {
        ignore_query_strings_order = false
        query_string {
          include = [
            "TIME", "datetime", "LAYERS", "STYLES", "FORMAT", "CRS", "BBOX",
            "WIDTH", "HEIGHT", "TILEMATRIX", "TILEMATRIXSET", "TILEROW", "TILECOL",
            "f", "styleId"
          ]
        }
      }
      origin_error_page_passthru = false
      respect_strong_etags       = true
    }
  }

  # Rule 2: Metadata - Short cache
  rules {
    description = "Cache metadata with short TTL"
    expression  = "(http.request.uri.path matches \"^/(stac|ogc/(collections|conformance|api)).*\")"
    action      = "set_cache_settings"

    action_parameters {
      cache = true
      edge_ttl {
        mode    = "override_origin"
        default = 300 # 5 minutes
      }
      browser_ttl {
        mode    = "override_origin"
        default = 300 # 5 minutes
      }
      cache_key {
        query_string {
          include = ["SERVICE", "VERSION", "REQUEST", "f", "limit", "offset"]
        }
      }
      origin_error_page_passthru = false
    }
  }

  # Rule 3: Admin - No cache
  rules {
    description = "Bypass cache for admin endpoints"
    expression  = "(http.request.uri.path matches \"^/admin.*\")"
    action      = "set_cache_settings"

    action_parameters {
      cache = false
    }
  }
}

# ============================================================================
# Firewall Rules (WAF)
# ============================================================================

resource "cloudflare_ruleset" "waf" {
  count       = var.enable_waf ? 1 : 0
  zone_id     = data.cloudflare_zone.honua.id
  name        = "Honua WAF Rules"
  description = "Web Application Firewall rules for Honua"
  kind        = "zone"
  phase       = "http_request_firewall_managed"

  # Cloudflare Managed Ruleset
  rules {
    description = "Execute Cloudflare Managed Ruleset"
    action      = "execute"
    expression  = "true"

    action_parameters {
      id = "efb7b8c949ac4650a09736fc376e9aee" # Cloudflare Managed Ruleset ID
    }
  }

  # OWASP Core Ruleset
  rules {
    description = "Execute OWASP Core Ruleset"
    action      = "execute"
    expression  = "true"

    action_parameters {
      id = "4814384a9e5d4991b9815dcfc25d2f1f" # OWASP Core Ruleset ID
    }
  }
}

# Custom WAF Rules
resource "cloudflare_ruleset" "custom_waf" {
  count       = var.enable_waf ? 1 : 0
  zone_id     = data.cloudflare_zone.honua.id
  name        = "Honua Custom WAF Rules"
  description = "Custom firewall rules for Honua"
  kind        = "zone"
  phase       = "http_request_firewall_custom"

  # Block common attack patterns
  rules {
    description = "Block SQL injection attempts"
    action      = "block"
    expression  = "(http.request.uri.query contains \"union select\" or http.request.uri.query contains \"'; drop\")"
  }

  rules {
    description = "Block XSS attempts"
    action      = "block"
    expression  = "(http.request.uri.query contains \"<script\" or http.request.uri.query contains \"javascript:\")"
  }

  rules {
    description = "Block path traversal"
    action      = "block"
    expression  = "(http.request.uri.path contains \"../\" or http.request.uri.path contains \"..\\\")"
  }

  # Allow only GET, HEAD, OPTIONS for tile endpoints
  rules {
    description = "Block non-GET methods for tile endpoints"
    action      = "block"
    expression  = "(http.request.uri.path matches \"^/(wms|wmts|ogc/collections/.*/tiles).*\" and not http.request.method in {\"GET\" \"HEAD\" \"OPTIONS\"})"
  }
}

# ============================================================================
# Rate Limiting Rules
# ============================================================================

resource "cloudflare_ruleset" "rate_limiting" {
  count       = var.enable_rate_limiting ? 1 : 0
  zone_id     = data.cloudflare_zone.honua.id
  name        = "Honua Rate Limiting"
  description = "Rate limiting rules for Honua"
  kind        = "zone"
  phase       = "http_ratelimit"

  # General rate limit
  rules {
    description = "Rate limit general requests"
    action      = "block"
    expression  = "(http.request.uri.path matches \"^/.*\")"

    action_parameters {
      response {
        status_code  = 429
        content      = "Rate limit exceeded"
        content_type = "text/plain"
      }
    }

    ratelimit {
      characteristics = [
        "ip.src"
      ]
      period              = 60
      requests_per_period = 2000
      mitigation_timeout  = 600
    }
  }

  # Stricter rate limit for admin endpoints
  rules {
    description = "Rate limit admin requests"
    action      = "block"
    expression  = "(http.request.uri.path matches \"^/admin.*\")"

    action_parameters {
      response {
        status_code  = 429
        content      = "Rate limit exceeded"
        content_type = "text/plain"
      }
    }

    ratelimit {
      characteristics = [
        "ip.src"
      ]
      period              = 60
      requests_per_period = 100
      mitigation_timeout  = 600
    }
  }
}

# ============================================================================
# SSL/TLS Configuration
# ============================================================================

resource "cloudflare_zone_settings_override" "honua" {
  zone_id = data.cloudflare_zone.honua.id

  settings {
    # SSL
    ssl                      = var.ssl_mode
    min_tls_version          = var.min_tls_version
    tls_1_3                  = "on"
    automatic_https_rewrites = "on"
    always_use_https         = "on"

    # Security
    security_level           = "medium"
    challenge_ttl            = 1800
    browser_check            = "on"
    hotlink_protection       = "off"
    email_obfuscation        = "on"

    # Performance
    brotli                   = "on"
    early_hints              = "on"
    http2                    = "on"
    http3                    = "on"
    zero_rtt                 = "on"
    opportunistic_encryption = "on"
    polish                   = "lossless"
    webp                     = "on"

    # Caching
    cache_level              = "standard"
    browser_cache_ttl        = 14400 # 4 hours default

    # Network
    ipv6                     = "on"
    websockets               = "on"
    pseudo_ipv4              = "off"

    # DDoS
    prefetch_preload         = "off"
  }
}

# ============================================================================
# Argo Smart Routing (Optional, requires Business plan)
# ============================================================================

resource "cloudflare_argo" "honua" {
  count              = var.enable_argo ? 1 : 0
  zone_id            = data.cloudflare_zone.honua.id
  tiered_caching     = "on"
  smart_routing      = "on"
}

# ============================================================================
# Custom SSL Certificate (Optional)
# ============================================================================

# Uncomment if using custom SSL certificate
# resource "cloudflare_certificate_pack" "honua" {
#   zone_id               = data.cloudflare_zone.honua.id
#   type                  = "advanced"
#   hosts                 = ["${var.subdomain}.${var.zone_name}"]
#   validation_method     = "txt"
#   validity_days         = 90
#   certificate_authority = "lets_encrypt"
# }

# ============================================================================
# Workers (for advanced caching logic - optional)
# ============================================================================

# Example: Cache API for programmatic purging
resource "cloudflare_worker_script" "cache_control" {
  count   = 0 # Set to 1 to enable
  zone_id = data.cloudflare_zone.honua.id
  name    = "honua-cache-control"
  content = file("${path.module}/workers/cache-control.js")
}

# ============================================================================
# Load Balancer (for multi-origin - optional)
# ============================================================================

# Uncomment for multi-origin load balancing
# resource "cloudflare_load_balancer_pool" "honua_origin" {
#   name = "honua-origin-pool"
#
#   origins {
#     name    = "origin-1"
#     address = var.origin_hostname
#     enabled = true
#   }
#
#   health_checks {
#     enabled          = true
#     method           = "GET"
#     path             = "/health"
#     interval         = 60
#     timeout          = 5
#     retries          = 2
#     expected_codes   = "200"
#   }
# }
#
# resource "cloudflare_load_balancer" "honua" {
#   zone_id          = data.cloudflare_zone.honua.id
#   name             = "${var.subdomain}.${var.zone_name}"
#   default_pool_ids = [cloudflare_load_balancer_pool.honua_origin.id]
#   proxied          = true
# }

# ============================================================================
# Analytics (optional - requires API token with Analytics.Read)
# ============================================================================

# Cloudflare Analytics can be accessed via API or dashboard
# No Terraform resource needed for basic analytics

# ============================================================================
# Outputs
# ============================================================================

output "zone_id" {
  description = "Cloudflare zone ID"
  value       = data.cloudflare_zone.honua.id
}

output "cdn_hostname" {
  description = "CDN hostname"
  value       = "${var.subdomain}.${var.zone_name}"
}

output "cdn_record_id" {
  description = "DNS record ID"
  value       = cloudflare_record.cdn.id
}

output "cache_purge_command" {
  description = "Cloudflare CLI command to purge cache"
  value       = "curl -X POST \"https://api.cloudflare.com/client/v4/zones/${data.cloudflare_zone.honua.id}/purge_cache\" -H \"Authorization: Bearer $CLOUDFLARE_API_TOKEN\" -H \"Content-Type: application/json\" --data '{\"purge_everything\":true}'"
}

output "selective_purge_command" {
  description = "Cloudflare CLI command to purge specific files"
  value       = "curl -X POST \"https://api.cloudflare.com/client/v4/zones/${data.cloudflare_zone.honua.id}/purge_cache\" -H \"Authorization: Bearer $CLOUDFLARE_API_TOKEN\" -H \"Content-Type: application/json\" --data '{\"files\":[\"https://${var.subdomain}.${var.zone_name}/wms\"]}'"
}

output "deployment_summary" {
  description = "CDN deployment summary"
  value = {
    zone_name        = var.zone_name
    cdn_hostname     = "${var.subdomain}.${var.zone_name}"
    environment      = var.environment
    ssl_mode         = var.ssl_mode
    min_tls_version  = var.min_tls_version
    waf_enabled      = var.enable_waf
    argo_enabled     = var.enable_argo
    rate_limiting    = var.enable_rate_limiting
    origin_hostname  = var.origin_hostname
  }
}

output "nameservers" {
  description = "Cloudflare nameservers (configure these at your domain registrar)"
  value       = data.cloudflare_zone.honua.name_servers
}
