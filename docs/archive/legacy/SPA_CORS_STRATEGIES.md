# SPA Same-Domain Access Strategies for Honua GIS Services

## The Problem

Single Page Applications (SPAs) often face same-domain restrictions when accessing GIS services:
- **CORS (Cross-Origin Resource Sharing)** blocks requests from different domains
- Browser security prevents `https://myapp.com` from accessing `https://gis-api.example.com`
- Common with ArcGIS REST API, WFS, WMS, OGC API Features

---

## Solutions (Without Inline Proxy)

### 1. ✅ **CORS Headers (Recommended)**

Enable CORS headers on the Honua server to allow cross-origin requests.

#### Implementation in Honua

**Configure in `appsettings.json` or environment variables:**

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://myapp.com",
      "https://staging.myapp.com",
      "http://localhost:3000"
    ],
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    "AllowedHeaders": ["Content-Type", "Authorization", "X-Requested-With"],
    "AllowCredentials": true,
    "MaxAge": 3600
  }
}
```

**ASP.NET Core Startup Configuration:**

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCors(options =>
    {
        options.AddPolicy("HonuaSpaPolicy", builder =>
        {
            builder
                .WithOrigins(Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>())
                .WithMethods(Configuration.GetSection("Cors:AllowedMethods").Get<string[]>())
                .WithHeaders(Configuration.GetSection("Cors:AllowedHeaders").Get<string[]>())
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromSeconds(3600));
        });
    });
}

public void Configure(IApplicationBuilder app)
{
    app.UseCors("HonuaSpaPolicy");
    // ... rest of middleware
}
```

**Pros:**
- ✅ No infrastructure changes needed
- ✅ Standard browser security mechanism
- ✅ Fine-grained control over origins
- ✅ Works with all HTTP methods (GET, POST, etc.)

**Cons:**
- ⚠️ Requires server configuration
- ⚠️ Still subject to browser preflight requests (OPTIONS)

---

### 2. ✅ **Subdomain Deployment (Recommended for Production)**

Deploy SPA and API on same root domain using subdomains.

#### Architecture

```
┌─────────────────────────────────────────────────────┐
│            Root Domain: example.com                  │
│                                                      │
│  ┌──────────────────┐       ┌───────────────────┐  │
│  │  SPA Frontend    │       │   Honua GIS API   │  │
│  │                  │       │                   │  │
│  │  app.example.com │◄──────│ api.example.com   │  │
│  │  (React/Vue/etc) │       │  (Honua Server)   │  │
│  └──────────────────┘       └───────────────────┘  │
│                                                      │
│  Same root domain = relaxed security restrictions   │
└─────────────────────────────────────────────────────┘
```

#### Implementation

**DNS Configuration:**
```
app.example.com  →  CloudFront/Azure CDN/Cloud CDN  →  S3/Blob/GCS (SPA)
api.example.com  →  ALB/App Gateway/Load Balancer  →  ECS/AKS/Cloud Run (Honua)
```

**SPA Configuration:**
```javascript
// config.js
const API_BASE_URL = `https://api.${window.location.hostname.split('.').slice(-2).join('.')}`;

// Or hardcoded
const API_BASE_URL = 'https://api.example.com';

// API calls
fetch(`${API_BASE_URL}/geoservices/rest/services/parcels/FeatureServer/0/query`, {
  credentials: 'include'  // Send cookies across subdomains
});
```

**Cookie Configuration (for auth):**
```csharp
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Domain = ".example.com";  // Wildcard subdomain
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
```

**Pros:**
- ✅ No CORS preflight overhead
- ✅ Shared cookies for authentication
- ✅ Cleaner architecture
- ✅ Better for production

**Cons:**
- ⚠️ Requires DNS configuration
- ⚠️ Requires wildcard SSL certificate or multiple certificates
- ⚠️ More complex infrastructure

---

### 3. ✅ **API Gateway / Reverse Proxy (External)**

Use cloud-native API gateway to route both SPA and API through single domain.

#### Architecture (AWS Example)

```
User Browser
    │
    ▼
https://app.example.com
    │
    ▼
CloudFront Distribution
    │
    ├─ /                  → S3 Bucket (SPA)
    ├─ /api/*             → ALB → ECS (Honua)
    └─ /geoservices/*     → ALB → ECS (Honua)
```

#### CloudFormation/Terraform Configuration

```hcl
# CloudFront distribution
resource "aws_cloudfront_distribution" "spa_with_api" {
  origin {
    domain_name = aws_s3_bucket.spa.bucket_regional_domain_name
    origin_id   = "spa-origin"
  }

  origin {
    domain_name = aws_lb.honua.dns_name
    origin_id   = "api-origin"

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
    }
  }

  # Default behavior: serve SPA
  default_cache_behavior {
    target_origin_id = "spa-origin"
    # ... cache settings
  }

  # API behavior: route to Honua
  ordered_cache_behavior {
    path_pattern     = "/api/*"
    target_origin_id = "api-origin"

    allowed_methods  = ["GET", "HEAD", "OPTIONS", "PUT", "POST", "PATCH", "DELETE"]
    cached_methods   = ["GET", "HEAD", "OPTIONS"]

    # Disable caching for API calls
    min_ttl     = 0
    default_ttl = 0
    max_ttl     = 0

    forwarded_values {
      query_string = true
      headers      = ["Authorization", "Content-Type"]
      cookies {
        forward = "all"
      }
    }
  }

  ordered_cache_behavior {
    path_pattern     = "/geoservices/*"
    target_origin_id = "api-origin"
    # Same settings as above
  }
}
```

#### Azure Example (Application Gateway)

```hcl
resource "azurerm_application_gateway" "spa_with_api" {
  # ... gateway config

  # Backend pool for SPA (Azure CDN or Storage Account)
  backend_address_pool {
    name = "spa-backend"
    fqdns = [azurerm_cdn_endpoint.spa.fqdn]
  }

  # Backend pool for Honua API
  backend_address_pool {
    name = "api-backend"
    fqdns = [azurerm_container_app.honua.latest_revision_fqdn]
  }

  # URL path map
  url_path_map {
    name                               = "path-routing"
    default_backend_address_pool_name  = "spa-backend"
    default_backend_http_settings_name = "spa-http-settings"

    path_rule {
      name                       = "api-routes"
      paths                      = ["/api/*", "/geoservices/*"]
      backend_address_pool_name  = "api-backend"
      backend_http_settings_name = "api-http-settings"
    }
  }
}
```

**SPA Configuration:**
```javascript
// All API calls use relative paths
const API_BASE_URL = '';  // Same origin!

// Fetch from same domain
fetch('/geoservices/rest/services/parcels/FeatureServer/0/query')
  .then(response => response.json())
  .then(data => console.log(data));
```

**Pros:**
- ✅ True same-origin (no CORS needed)
- ✅ Single domain for users
- ✅ Unified SSL certificate
- ✅ CDN caching for SPA
- ✅ No preflight requests

**Cons:**
- ⚠️ More complex infrastructure
- ⚠️ Additional cost (API Gateway/CloudFront/App Gateway)
- ⚠️ Potential latency for API Gateway hops

---

### 4. ✅ **Service Workers with Cache-First Strategy**

Use service workers to cache API responses and serve from cache when possible.

#### Implementation

**Service Worker (`sw.js`):**
```javascript
// Cache API responses for offline/performance
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  // Cache GIS data (tiles, features)
  if (url.pathname.includes('/geoservices/') ||
      url.pathname.includes('/MapServer/') ||
      url.pathname.includes('/FeatureServer/')) {

    event.respondWith(
      caches.open('gis-cache-v1').then((cache) => {
        return cache.match(event.request).then((cachedResponse) => {
          if (cachedResponse) {
            // Return cached, update in background
            event.waitUntil(
              fetch(event.request).then((networkResponse) => {
                cache.put(event.request, networkResponse.clone());
              })
            );
            return cachedResponse;
          }

          // Fetch from network
          return fetch(event.request).then((networkResponse) => {
            cache.put(event.request, networkResponse.clone());
            return networkResponse;
          });
        });
      })
    );
  }
});
```

**Note:** This doesn't solve CORS, but **reduces API calls** and improves performance.

---

### 5. ⚠️ **JSONP (Legacy, Not Recommended)**

JSONP bypasses CORS by loading data as script tags (only for GET requests).

**Not recommended because:**
- ❌ Only supports GET
- ❌ Security vulnerabilities (XSS)
- ❌ No error handling
- ❌ Deprecated in modern apps

---

### 6. ✅ **OAuth 2.0 + Token-Based Auth (No Cookies)**

Avoid cookie-based authentication (which requires same-domain) by using tokens.

#### Implementation

**SPA Login Flow:**
```javascript
// 1. User logs in, receives JWT token
const response = await fetch('https://api.example.com/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username, password })
});

const { access_token } = await response.json();
localStorage.setItem('auth_token', access_token);

// 2. Include token in all API requests
const data = await fetch('https://api.example.com/geoservices/rest/services', {
  headers: {
    'Authorization': `Bearer ${access_token}`
  }
});
```

**Honua Server Configuration:**
```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Configuration["Jwt:Issuer"],
            ValidAudience = Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]))
        };
    });
```

**Pros:**
- ✅ Works across any domain
- ✅ Stateless authentication
- ✅ Mobile-friendly
- ✅ No cookie issues

**Cons:**
- ⚠️ Requires CORS headers
- ⚠️ Token storage security (XSS risks with localStorage)

---

## Recommended Approach by Deployment Type

### Development (Local)
**Option:** CORS headers + localhost origin
```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
  }
}
```

### Staging/Testing
**Option:** Subdomain deployment
- SPA: `https://staging-app.example.com`
- API: `https://staging-api.example.com`

### Production (Small/Medium Scale)
**Option:** Subdomain deployment + CORS
- SPA: `https://app.example.com`
- API: `https://api.example.com`
- Fallback CORS for external integrations

### Production (Large Scale / Enterprise)
**Option:** API Gateway / CloudFront with path routing
- Single domain: `https://app.example.com`
- SPA: `https://app.example.com/`
- API: `https://app.example.com/api/*`
- GIS: `https://app.example.com/geoservices/*`

---

## Honua Configuration Options

### Option 1: CORS Configuration File

**`cors-config.json`:**
```json
{
  "enabled": true,
  "allowedOrigins": [
    "https://myapp.com",
    "https://*.myapp.com"
  ],
  "allowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
  "allowedHeaders": ["Content-Type", "Authorization"],
  "allowCredentials": true,
  "maxAge": 3600,
  "exposeHeaders": ["X-Total-Count", "X-Page-Size"]
}
```

**Load in Honua startup:**
```csharp
var corsConfig = Configuration.GetSection("ExternalServiceSecurity:Cors")
    .Get<CorsConfiguration>();

if (corsConfig?.Enabled == true)
{
    services.AddCors(options =>
    {
        options.AddPolicy("HonuaCorsPolicy", builder =>
        {
            builder
                .WithOrigins(corsConfig.AllowedOrigins)
                .WithMethods(corsConfig.AllowedMethods)
                .WithHeaders(corsConfig.AllowedHeaders)
                .SetIsOriginAllowedToAllowWildcardSubdomains();

            if (corsConfig.AllowCredentials)
                builder.AllowCredentials();

            if (corsConfig.MaxAge > 0)
                builder.SetPreflightMaxAge(TimeSpan.FromSeconds(corsConfig.MaxAge));

            if (corsConfig.ExposeHeaders?.Any() == true)
                builder.WithExposedHeaders(corsConfig.ExposeHeaders);
        });
    });
}
```

### Option 2: Environment Variables

```bash
HONUA_CORS_ENABLED=true
HONUA_CORS_ALLOWED_ORIGINS=https://myapp.com,https://staging.myapp.com
HONUA_CORS_ALLOW_CREDENTIALS=true
```

---

## Terraform Deployment Examples

### AWS: CloudFront + ALB

```hcl
# SPA in S3 + Honua API via CloudFront
resource "aws_cloudfront_distribution" "honua_spa" {
  enabled = true
  aliases = ["app.example.com"]

  # SPA origin (S3)
  origin {
    domain_name = aws_s3_bucket.spa.bucket_regional_domain_name
    origin_id   = "spa"

    s3_origin_config {
      origin_access_identity = aws_cloudfront_origin_access_identity.spa.cloudfront_access_identity_path
    }
  }

  # API origin (ALB → ECS)
  origin {
    domain_name = aws_lb.honua.dns_name
    origin_id   = "api"

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
    }
  }

  # Default: serve SPA
  default_cache_behavior {
    target_origin_id       = "spa"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]

    forwarded_values {
      query_string = false
      cookies { forward = "none" }
    }
  }

  # API routes: forward to Honua
  ordered_cache_behavior {
    path_pattern           = "/api/*"
    target_origin_id       = "api"
    viewer_protocol_policy = "https-only"
    allowed_methods        = ["GET", "HEAD", "OPTIONS", "PUT", "POST", "PATCH", "DELETE"]
    cached_methods         = ["GET", "HEAD"]

    # No caching for API
    min_ttl     = 0
    default_ttl = 0
    max_ttl     = 0

    forwarded_values {
      query_string = true
      headers      = ["Authorization", "Content-Type", "Origin"]
      cookies { forward = "all" }
    }
  }

  ordered_cache_behavior {
    path_pattern           = "/geoservices/*"
    target_origin_id       = "api"
    viewer_protocol_policy = "https-only"

    # Cache tiles/features for performance
    min_ttl     = 300
    default_ttl = 3600
    max_ttl     = 86400

    allowed_methods = ["GET", "HEAD", "OPTIONS"]
    cached_methods  = ["GET", "HEAD"]

    forwarded_values {
      query_string = true
      headers      = ["Authorization"]
      cookies { forward = "none" }
    }
  }

  viewer_certificate {
    acm_certificate_arn = aws_acm_certificate.app.arn
    ssl_support_method  = "sni-only"
  }
}
```

### Azure: Application Gateway + CDN

```hcl
# Azure Front Door for unified routing
resource "azurerm_frontdoor" "honua_spa" {
  name                = "honua-app"
  resource_group_name = azurerm_resource_group.main.name

  # Frontend endpoint
  frontend_endpoint {
    name      = "app-frontend"
    host_name = "app.example.com"
  }

  # Backend pool: SPA (CDN)
  backend_pool {
    name = "spa-backend"

    backend {
      host_header = azurerm_cdn_endpoint.spa.fqdn
      address     = azurerm_cdn_endpoint.spa.fqdn
      http_port   = 80
      https_port  = 443
    }
  }

  # Backend pool: API (Container Apps)
  backend_pool {
    name = "api-backend"

    backend {
      host_header = azurerm_container_app.honua.latest_revision_fqdn
      address     = azurerm_container_app.honua.latest_revision_fqdn
      http_port   = 80
      https_port  = 443
    }
  }

  # Routing rule: default → SPA
  routing_rule {
    name               = "default-routing"
    accepted_protocols = ["Https"]
    patterns_to_match  = ["/*"]
    frontend_endpoints = ["app-frontend"]

    forwarding_configuration {
      backend_pool_name = "spa-backend"
    }
  }

  # Routing rule: /api/* → Honua
  routing_rule {
    name               = "api-routing"
    accepted_protocols = ["Https"]
    patterns_to_match  = ["/api/*", "/geoservices/*"]
    frontend_endpoints = ["app-frontend"]

    forwarding_configuration {
      backend_pool_name = "api-backend"
      cache_enabled     = false
    }
  }
}
```

---

## Performance Considerations

### Caching Strategy

**Cacheable GIS Endpoints:**
- Tile services: `/MapServer/tile/{z}/{x}/{y}` - Cache 1 day
- Feature metadata: `/FeatureServer/0` - Cache 1 hour
- Service definitions: `/rest/services` - Cache 1 hour

**Non-Cacheable:**
- Feature queries: `/FeatureServer/0/query` - No cache (dynamic)
- Edit operations: `/FeatureServer/0/addFeatures` - No cache

**CloudFront Cache Behavior Example:**
```hcl
ordered_cache_behavior {
  path_pattern = "/geoservices/*/MapServer/tile/*/*"

  # Cache tiles aggressively
  min_ttl     = 86400   # 1 day
  default_ttl = 2592000 # 30 days
  max_ttl     = 31536000 # 1 year

  compress = true  # Enable gzip
}

ordered_cache_behavior {
  path_pattern = "/geoservices/*/FeatureServer/*/query"

  # No caching for queries
  min_ttl     = 0
  default_ttl = 0
  max_ttl     = 0
}
```

---

## Security Checklist

✅ **CORS Configuration:**
- [ ] Whitelist specific origins (avoid `*`)
- [ ] Use `AllowCredentials` only if needed
- [ ] Set `MaxAge` to reduce preflight requests
- [ ] Specify allowed methods explicitly

✅ **Authentication:**
- [ ] Use Bearer tokens (not cookies for cross-domain)
- [ ] Implement token refresh mechanism
- [ ] Set appropriate token expiration (15min-1hr)

✅ **SSL/TLS:**
- [ ] Enforce HTTPS for all API calls
- [ ] Use TLS 1.2+ only
- [ ] Implement HSTS headers

✅ **API Gateway:**
- [ ] Rate limiting per IP/user
- [ ] Request size limits
- [ ] DDoS protection (CloudFront Shield, Azure DDoS)

---

## Recommended: Subdomain + CORS Fallback

**Best of both worlds:**

```
Production Architecture:
- SPA: https://app.example.com (CloudFront → S3)
- API: https://api.example.com (ALB → ECS)
- Enable CORS for external integrations (mobile apps, partners)
```

**Benefits:**
- ✅ Clean architecture
- ✅ Shared cookies for auth
- ✅ CORS available for external clients
- ✅ Scalable and performant
- ✅ Standard industry pattern

This is the approach used by: Mapbox, Esri ArcGIS Online, Google Maps, AWS Console, GitHub, etc.
