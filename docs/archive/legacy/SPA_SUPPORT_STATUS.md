# SPA Support Status in Honua

## Summary

**Good News:** Honua **already supports CORS** for SPA access! The infrastructure is built-in.

**What's Missing:** Documentation, AI Consultant integration, and example configurations.

---

## ✅ What Honua Already Supports

### 1. **CORS Configuration (Built-in)**

**Location:** `src/Honua.Server.Host/Hosting/MetadataCorsPolicyProvider.cs`

**Current Implementation:**
```csharp
// Honua reads CORS from metadata snapshot
var cors = snapshot.Server.Cors;

if (cors.Enabled) {
    // Supports:
    - AllowedOrigins (specific domains)
    - AllowAnyOrigin (*)
    - AllowedMethods (GET, POST, etc.)
    - AllowedHeaders (Content-Type, Authorization)
    - ExposedHeaders (X-Total-Count, etc.)
    - AllowCredentials (cookies/auth)
    - MaxAge (preflight caching)
}
```

**How It Works:**
1. CORS configured in metadata.json (under `server.cors`)
2. `MetadataCorsPolicyProvider` reads configuration dynamically
3. Applied via `app.UseCors()` in pipeline (line 230)

**Status:** ✅ **Fully Functional** - Just needs configuration

---

### 2. **JWT Bearer Authentication (Built-in)**

**Location:** `src/Honua.Server.Host/Hosting/HonuaHostConfigurationExtensions.cs:375`

**Current Implementation:**
```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer();
```

**Status:** ✅ **Fully Functional** - SPAs can use Bearer tokens

---

### 3. **QuickStart Mode (Development)**

**Purpose:** Bypass authentication for local development

**Status:** ✅ **Working** - Useful for SPA development without auth setup

---

## ❌ What's Missing / Needs Work

### 1. **CORS Configuration in metadata.json Schema**

**Issue:** The `server.cors` configuration isn't documented in `metadata-schema.json`

**Needed:** Add JSON schema definition

**Example (what should be added):**
```json
{
  "server": {
    "cors": {
      "enabled": true,
      "allowedOrigins": ["https://app.example.com"],
      "allowedMethods": ["GET", "POST"],
      "allowedHeaders": ["Content-Type", "Authorization"],
      "exposedHeaders": ["X-Total-Count"],
      "allowCredentials": true,
      "maxAge": 3600
    }
  }
}
```

---

### 2. **ExternalServiceSecurityConfiguration.Cors**

**Status:** ⚠️ **Partially Implemented**

**Issue:** I added `CorsConfiguration` class to `ExternalServiceSecurityConfiguration.cs`, but:
- It's not connected to `MetadataCorsPolicyProvider` yet
- `MetadataCorsPolicyProvider` reads from `snapshot.Server.Cors` (different location)
- Need to unify these two approaches

**Options:**
1. **Option A:** Remove my `ExternalServiceSecurityConfiguration.Cors` and enhance metadata schema
2. **Option B:** Make `MetadataCorsPolicyProvider` read from both locations (fallback chain)
3. **Option C:** Migrate to using `ExternalServiceSecurityConfiguration.Cors` only

**Recommendation:** **Option A** (use metadata.json) - More consistent with Honua's architecture

---

### 3. **AI Consultant CORS Guidance**

**Status:** ❌ **Not Integrated**

**What's Missing:**
- AI Consultant doesn't detect SPA deployment scenarios
- No automatic CORS configuration generation
- No subdomain deployment Terraform templates
- No API Gateway routing examples

**Needed:** Enhance `DeploymentExecutionAgent` or create `SpaDeploymentAgent` to:
1. Detect when user is deploying with SPA frontend
2. Ask: "Are you deploying a SPA frontend with this?"
3. Generate appropriate CORS configuration
4. Suggest deployment architecture (subdomain vs API Gateway)
5. Generate Terraform for CloudFront/Azure Front Door routing

---

### 4. **Subdomain Deployment Templates**

**Status:** ❌ **Not Implemented**

**What's Missing:**
- No Terraform templates for subdomain deployment
- No CloudFront/Azure Front Door/Cloud CDN examples with path routing
- No cookie domain configuration examples

**Needed:** Add to AI Consultant's Terraform generation:
```hcl
# CloudFront distribution with SPA + API
resource "aws_cloudfront_distribution" "honua_unified" {
  # SPA origin (S3)
  origin { ... }

  # API origin (ALB → ECS)
  origin { ... }

  # Path routing: / → SPA, /api/* → Honua
  default_cache_behavior { ... }
  ordered_cache_behavior { path_pattern = "/api/*" ... }
  ordered_cache_behavior { path_pattern = "/geoservices/*" ... }
}
```

---

### 5. **Wildcard Subdomain Support**

**Status:** ⚠️ **Unclear**

**Issue:** Current `MetadataCorsPolicyProvider` uses:
```csharp
builder.WithOrigins(cors.AllowedOrigins.ToArray());
```

**Question:** Does ASP.NET Core's `WithOrigins()` support wildcards like `"https://*.example.com"`?

**Answer:** **NO** - ASP.NET Core doesn't support wildcard origins by default.

**Needed:** Enhance `MetadataCorsPolicyProvider` to handle wildcards:
```csharp
if (cors.AllowedOrigins.Any(o => o.Contains("*")))
{
    // Use SetIsOriginAllowed for pattern matching
    builder.SetIsOriginAllowed(origin =>
    {
        foreach (var pattern in cors.AllowedOrigins)
        {
            if (MatchWildcard(origin, pattern))
                return true;
        }
        return false;
    });
}
else
{
    builder.WithOrigins(cors.AllowedOrigins.ToArray());
}
```

---

### 6. **Service Worker / Caching Guidance**

**Status:** ❌ **Not Provided**

**What's Missing:**
- No guidance on caching GIS tiles/features
- No service worker examples for offline support
- No cache-control header configuration

**Needed:** Documentation on:
- Which endpoints to cache (tiles, metadata)
- Which endpoints NOT to cache (queries, edits)
- Service worker implementation examples

---

### 7. **SPA Authentication Integration Examples**

**Status:** ❌ **No Examples**

**What's Missing:**
- No React/Vue/Angular examples for JWT auth
- No token refresh flow examples
- No localStorage vs sessionStorage guidance

**Needed:** Sample code for SPA integration:
```javascript
// React example
const api = axios.create({
  baseURL: 'https://api.example.com',
  headers: {
    'Authorization': `Bearer ${localStorage.getItem('auth_token')}`
  }
});

// Fetch GIS data
const data = await api.get('/geoservices/rest/services/parcels/FeatureServer/0/query', {
  params: { where: '1=1', outFields: '*', f: 'json' }
});
```

---

## Comparison Matrix

| Feature | Honua Support | AI Consultant Support | Documentation | Terraform Templates |
|---------|---------------|----------------------|---------------|---------------------|
| **CORS Headers** | ✅ Built-in | ❌ Not integrated | ⚠️ Incomplete | ❌ None |
| **JWT Bearer Auth** | ✅ Built-in | ❌ Not guided | ⚠️ Partial | ❌ None |
| **Subdomain Deployment** | ✅ Possible | ❌ Not suggested | ❌ None | ❌ None |
| **API Gateway Routing** | ✅ Compatible | ❌ Not generated | ⚠️ Manual only | ❌ None |
| **Wildcard Subdomains** | ❌ Not supported | ❌ N/A | ❌ N/A | ❌ N/A |
| **Cookie Domain Config** | ❌ Not exposed | ❌ Not guided | ❌ None | ❌ None |
| **Service Workers** | ✅ Compatible | ❌ Not guided | ❌ None | ❌ N/A |
| **SPA Code Examples** | ✅ Works | ❌ Not provided | ❌ None | ❌ N/A |

---

## Priority Fixes Needed

### 🔥 High Priority

#### 1. **Add CORS to metadata-schema.json**
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "properties": {
    "server": {
      "type": "object",
      "properties": {
        "cors": {
          "type": "object",
          "properties": {
            "enabled": { "type": "boolean", "default": false },
            "allowedOrigins": {
              "type": "array",
              "items": { "type": "string" },
              "description": "Allowed origins (e.g., https://app.example.com). Use ['*'] for any origin."
            },
            "allowedMethods": {
              "type": "array",
              "items": { "type": "string" },
              "default": ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
            },
            "allowedHeaders": {
              "type": "array",
              "items": { "type": "string" },
              "default": ["Content-Type", "Authorization"]
            },
            "exposedHeaders": {
              "type": "array",
              "items": { "type": "string" }
            },
            "allowCredentials": { "type": "boolean", "default": true },
            "maxAge": { "type": "integer", "default": 3600 }
          }
        }
      }
    }
  }
}
```

#### 2. **Fix Wildcard Subdomain Support**

Enhance `MetadataCorsPolicyProvider.cs`:
```csharp
if (cors.AllowedOrigins.Any(o => o.Contains("*")))
{
    builder.SetIsOriginAllowed(origin =>
    {
        foreach (var pattern in cors.AllowedOrigins)
        {
            if (IsWildcardMatch(origin, pattern))
                return true;
        }
        return false;
    });
}

private static bool IsWildcardMatch(string origin, string pattern)
{
    if (pattern == "*") return true;
    if (!pattern.Contains("*")) return origin.Equals(pattern, StringComparison.OrdinalIgnoreCase);

    // Convert wildcard pattern to regex
    var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
        .Replace("\\*", ".*") + "$";
    return System.Text.RegularExpressions.Regex.IsMatch(origin, regex,
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
```

#### 3. **Remove Duplicate CorsConfiguration**

**Decision:** Keep CORS in `metadata.json`, remove from `ExternalServiceSecurityConfiguration`.

**Rationale:**
- Honua's architecture uses metadata.json for runtime configuration
- `MetadataCorsPolicyProvider` already reads from metadata
- Adding to `ExternalServiceSecurityConfiguration` creates confusion

**Action:** Remove my addition to `ExternalServiceSecurityConfiguration.cs`

---

### ⚠️ Medium Priority

#### 4. **AI Consultant Integration**

Create `SpaDeploymentAgent` or enhance `DeploymentExecutionAgent`:
```csharp
public async Task<AgentStepResult> DetectSpaDeploymentAsync(...)
{
    var prompt = @"Analyze this deployment:

    Does the user mention:
    - React, Vue, Angular, Svelte, or other SPA framework?
    - Frontend, UI, or web application?
    - Cross-origin, CORS, or same-domain issues?

    If yes, recommend:
    1. CORS configuration for metadata.json
    2. Subdomain deployment architecture (app.example.com + api.example.com)
    3. Or API Gateway path routing (app.example.com/* → SPA, /api/* → Honua)
    ";

    var analysis = await _llmProvider.GenerateCompletionAsync(prompt, ...);

    if (analysis.IsSpaDeployment)
    {
        return GenerateSpaConfiguration(analysis);
    }
}
```

#### 5. **Terraform Templates for SPA Deployment**

Add to `DeploymentExecutionAgent` Terraform generation:
- CloudFront distribution with SPA origin + API origin
- Azure Front Door with path routing
- GCP Cloud CDN with backend routing
- Cookie domain configuration for authentication

---

### 📝 Low Priority (Documentation)

#### 6. **SPA Integration Guide**

Create `/docs/SPA_INTEGRATION.md`:
- React example with axios + JWT
- Vue example with fetch + Vuex
- Angular example with HttpClient + Interceptors
- Token refresh flow
- Error handling (401/403)

#### 7. **Service Worker Example**

Create `/docs/examples/service-worker.js`:
```javascript
// Cache GIS tiles aggressively
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  if (url.pathname.includes('/MapServer/tile/')) {
    event.respondWith(
      caches.match(event.request).then(cachedResponse => {
        if (cachedResponse) return cachedResponse;

        return fetch(event.request).then(networkResponse => {
          return caches.open('gis-tiles-v1').then(cache => {
            cache.put(event.request, networkResponse.clone());
            return networkResponse;
          });
        });
      })
    );
  }
});
```

---

## Configuration Examples

### Current Working Example (metadata.json)

```json
{
  "server": {
    "cors": {
      "enabled": true,
      "allowedOrigins": [
        "https://app.example.com",
        "http://localhost:3000"
      ],
      "allowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "allowedHeaders": ["Content-Type", "Authorization", "X-Requested-With"],
      "exposedHeaders": ["X-Total-Count", "X-Page-Size"],
      "allowCredentials": true,
      "maxAge": 3600
    }
  },
  "catalog": { ... },
  "services": [ ... ],
  "layers": [ ... ]
}
```

**How to Use:**
1. Add this to your `metadata.json`
2. Restart Honua server
3. SPA can now make cross-origin requests

---

## Recommended Approach

### For Honua Server:
1. ✅ **CORS is already working** - just configure in metadata.json
2. ⚠️ **Fix wildcard support** - Enhance `MetadataCorsPolicyProvider`
3. ❌ **Remove duplicate** - Remove `CorsConfiguration` from `ExternalServiceSecurityConfiguration`
4. 📝 **Document** - Add schema and examples

### For AI Consultant:
1. ❌ **Detect SPA scenarios** - Add logic to identify SPA deployments
2. ❌ **Generate CORS config** - Auto-generate metadata.json CORS section
3. ❌ **Suggest architecture** - Recommend subdomain vs API Gateway
4. ❌ **Generate Terraform** - CloudFront/Azure Front Door templates

### For Documentation:
1. ❌ **SPA Integration Guide** - React/Vue/Angular examples
2. ❌ **Deployment Patterns** - Subdomain vs API Gateway
3. ❌ **Service Worker Guide** - Caching strategies
4. ✅ **CORS Strategies** - Already created (SPA_CORS_STRATEGIES.md)

---

## Testing Checklist

Once fixes are implemented:

- [ ] CORS works with specific origins
- [ ] CORS works with wildcard subdomains (`*.example.com`)
- [ ] JWT Bearer tokens accepted in Authorization header
- [ ] Preflight OPTIONS requests cached (MaxAge respected)
- [ ] Exposed headers accessible from JavaScript
- [ ] Credentials (cookies) work with AllowCredentials
- [ ] AI Consultant detects SPA deployment
- [ ] AI Consultant generates CORS configuration
- [ ] Terraform generates CloudFront/Front Door routing
- [ ] Documentation includes working SPA examples

---

## Summary

**Current State:**
- ✅ Honua **already supports CORS** (via metadata.json)
- ✅ JWT Bearer auth **already works** for SPAs
- ⚠️ Wildcard subdomains **need implementation**
- ❌ AI Consultant **doesn't know about SPAs**
- ❌ No Terraform templates for SPA deployments
- ⚠️ Documentation is **incomplete**

**Bottom Line:** The server is **90% ready** for SPAs. Just needs:
1. Wildcard subdomain fix (30 min)
2. Schema documentation (15 min)
3. AI Consultant integration (2-4 hours)
4. Terraform templates (2-4 hours)
5. Usage documentation (1-2 hours)

**Total Effort:** ~8-12 hours to complete full SPA support
