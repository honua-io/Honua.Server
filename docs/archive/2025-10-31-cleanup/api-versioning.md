# API Versioning Strategy

## Overview

The Honua API uses **URL-based versioning** to manage API evolution and breaking changes. All API endpoints include a version identifier in the URL path.

**Current Version:** `v1`

## Why URL-Based Versioning?

We chose URL-based versioning because it provides:

- ✅ **Clear and visible versioning** - Version is explicit in the URL
- ✅ **Easy testing** - Different versions can be tested side-by-side
- ✅ **Browser-friendly** - Works in browsers without custom headers
- ✅ **Universal compatibility** - Works with all HTTP clients
- ✅ **OGC API compliance** - Compatible with OGC API standards

## URL Format

All API endpoints follow this pattern:

```
https://api.example.com/{version}/{service}/{resource}
```

### Examples

```
# OGC API Features
https://api.example.com/v1/ogc/collections
https://api.example.com/v1/ogc/collections/my-layer/items

# STAC API
https://api.example.com/v1/stac
https://api.example.com/v1/stac/collections

# Administration API
https://api.example.com/v1/api/admin/ingestion/jobs

# Records API
https://api.example.com/v1/records
```

## Supported Versions

| Version | Status | Sunset Date | Notes |
|---------|--------|-------------|-------|
| v1 | **Current** | None | Production version |

## Version Lifecycle

### Version States

1. **Current (GA)** - Generally available, fully supported
2. **Deprecated** - Still functional but scheduled for removal
3. **Sunset** - No longer available

### Deprecation Policy

When a version is deprecated:

1. **Deprecation headers** are added to all responses:
   - `Deprecation: true` - Indicates the version is deprecated
   - `Sunset: 2026-12-31T23:59:59Z` - Date when version will be removed
   - `Link: <url>; rel="deprecation"` - Link to migration guide

2. **Migration period** of at least 6 months is provided

3. **Documentation** is updated with migration instructions

4. **Notifications** are sent to registered API consumers

### Example Deprecation Response

```http
HTTP/1.1 200 OK
Deprecation: true
Sunset: Wed, 31 Dec 2026 23:59:59 GMT
Link: <https://docs.honua.io/api/migration/v1-to-v2>; rel="deprecation"
X-API-Version: v1
Content-Type: application/json

{
  "collections": [...]
}
```

## Backward Compatibility

### Legacy URL Support (Phase 1)

Non-versioned URLs are currently **redirected** to v1 endpoints:

```http
GET /ogc/collections
HTTP/1.1 308 Permanent Redirect
Location: /v1/ogc/collections

{
  "type": "https://tools.ietf.org/html/rfc7538",
  "title": "API Endpoint Moved Permanently",
  "status": 308,
  "detail": "This endpoint has moved to /v1/ogc/collections. Please update your client to use versioned URLs.",
  "newLocation": "/v1/ogc/collections"
}
```

### Migration Timeline

| Phase | Timeline | Action | Impact |
|-------|----------|--------|--------|
| **Phase 1** | Current | Add `/v1/` routes, redirect legacy URLs | No breaking changes |
| **Phase 2** | +3 months | Add deprecation warnings to legacy URLs | Warnings only |
| **Phase 3** | +6 months | Remove legacy URL support | Legacy URLs return 404 |

## Client Implementation

### Recommended Approach

Always include the version in your requests:

```javascript
// ✅ GOOD - Explicit version
const response = await fetch('https://api.example.com/v1/ogc/collections');

// ❌ BAD - No version (will redirect in Phase 1, fail in Phase 3)
const response = await fetch('https://api.example.com/ogc/collections');
```

### Version Detection

Clients can detect the API version from:

1. **URL path** - Primary method
2. **Response header** - `X-API-Version` header in all responses

```http
GET /v1/ogc/collections
HTTP/1.1 200 OK
X-API-Version: v1
Content-Type: application/json
```

### Client Libraries

Update base URLs to include version:

```python
# Python example
from honua import HonuaClient

# ✅ GOOD
client = HonuaClient(base_url="https://api.example.com/v1")

# ❌ BAD
client = HonuaClient(base_url="https://api.example.com")
```

```csharp
// C# example
var client = new HonuaClient(new Uri("https://api.example.com/v1"));
```

## Breaking Changes

### What Constitutes a Breaking Change?

A new major version (v1 → v2) is required when:

- **Removing** endpoints or parameters
- **Renaming** fields in responses
- **Changing** data types
- **Modifying** authentication requirements
- **Altering** response status codes
- **Breaking** existing client integrations

### Non-Breaking Changes

The following changes can be made within a version:

- **Adding** new optional parameters
- **Adding** new fields to responses
- **Adding** new endpoints
- **Improving** performance
- **Fixing** bugs
- **Enhancing** documentation

## Configuration

### Application Settings

Configure API versioning in `appsettings.json`:

```json
{
  "ApiVersioning": {
    "defaultVersion": "v1",
    "allowLegacyUrls": true,
    "legacyRedirectVersion": "v1",
    "deprecationWarnings": {
      "v1": "2026-12-31T23:59:59Z"
    },
    "deprecationDocumentationUrl": "https://docs.honua.io/api/versioning#deprecation"
  }
}
```

#### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `defaultVersion` | string | `v1` | Default version for responses |
| `allowLegacyUrls` | boolean | `true` | Enable legacy URL redirects |
| `legacyRedirectVersion` | string | `v1` | Version to redirect legacy URLs to |
| `deprecationWarnings` | object | `{}` | Map of version → sunset date |
| `deprecationDocumentationUrl` | string | null | URL to deprecation documentation |

### Disabling Legacy URL Support

To require versioned URLs:

```json
{
  "ApiVersioning": {
    "allowLegacyUrls": false
  }
}
```

This will return `404 Not Found` for non-versioned URLs instead of redirecting.

## API Discovery

### Version Listing

The root endpoint provides version information:

```http
GET /v1/ogc
HTTP/1.1 200 OK
X-API-Version: v1
Content-Type: application/json

{
  "title": "Honua OGC API",
  "apiVersion": "v1",
  "links": [
    {
      "rel": "self",
      "href": "https://api.example.com/v1/ogc",
      "type": "application/json"
    },
    {
      "rel": "service-desc",
      "href": "https://api.example.com/v1/ogc/api",
      "type": "application/vnd.oai.openapi+json;version=3.0"
    }
  ]
}
```

### OpenAPI Documentation

Each version has separate OpenAPI documentation:

```
# Version 1
https://api.example.com/swagger/v1/swagger.json

# Version 2 (future)
https://api.example.com/swagger/v2/swagger.json
```

Swagger UI provides a version selector:

```
https://api.example.com/swagger
```

## Best Practices

### For API Consumers

1. **Always use versioned URLs** - Don't rely on redirects
2. **Monitor deprecation headers** - Watch for `Deprecation` and `Sunset` headers
3. **Test against specific versions** - Pin to a version during development
4. **Update proactively** - Migrate before sunset dates
5. **Handle redirects gracefully** - Follow 308 redirects if using legacy URLs

### For API Developers

1. **Avoid breaking changes** - Maintain backward compatibility when possible
2. **Document all changes** - Update migration guides
3. **Provide migration period** - Minimum 6 months for deprecations
4. **Test both versions** - Ensure v1 and v2 work correctly
5. **Communicate early** - Announce deprecations well in advance

## Migration Guides

### Migrating from Legacy URLs to v1

**Old:**
```
GET /ogc/collections
```

**New:**
```
GET /v1/ogc/collections
```

**Steps:**
1. Update all base URLs to include `/v1`
2. Test thoroughly in development
3. Deploy updated clients
4. Monitor for any missed endpoints

### Future Migrations

When v2 is introduced, migration guides will be provided for:
- Breaking changes from v1 to v2
- Code examples for common patterns
- Timeline and sunset dates
- Support resources

## Monitoring and Metrics

### Version Usage Metrics

The API tracks version usage through:

- Request counts per version
- Deprecated version usage
- Legacy URL redirect counts

### Alerting

Set up alerts for:

- Deprecated version usage spikes
- Legacy URL usage after Phase 2
- Version header absence

## Support and Resources

### Documentation
- API Reference: https://docs.honua.io/api/reference
- Migration Guides: https://docs.honua.io/api/migration
- Version History: https://docs.honua.io/api/changelog

### Support Channels
- GitHub Issues: https://github.com/your-org/HonuaIO/issues
- Community Forum: https://community.honua.io
- Email: api-support@honua.io

### Version Status Page
- Current Status: https://status.honua.io/versions
- Deprecation Schedule: https://docs.honua.io/api/deprecation-schedule

## Examples

### cURL

```bash
# Fetch collections (versioned)
curl https://api.example.com/v1/ogc/collections

# With authentication
curl -H "Authorization: Bearer TOKEN" \
  https://api.example.com/v1/ogc/collections
```

### JavaScript/TypeScript

```typescript
// Using fetch
async function getCollections() {
  const response = await fetch('https://api.example.com/v1/ogc/collections');
  const data = await response.json();

  // Check version
  const version = response.headers.get('X-API-Version');
  console.log(`API Version: ${version}`);

  // Check deprecation
  if (response.headers.has('Deprecation')) {
    const sunset = response.headers.get('Sunset');
    console.warn(`API version deprecated. Sunset: ${sunset}`);
  }

  return data;
}
```

### Python

```python
import requests

def get_collections():
    response = requests.get('https://api.example.com/v1/ogc/collections')

    # Check version
    version = response.headers.get('X-API-Version')
    print(f"API Version: {version}")

    # Check deprecation
    if 'Deprecation' in response.headers:
        sunset = response.headers.get('Sunset')
        print(f"Warning: API deprecated. Sunset: {sunset}")

    return response.json()
```

### C#

```csharp
using System.Net.Http;
using System.Threading.Tasks;

public class ApiClient
{
    private readonly HttpClient _client;

    public ApiClient()
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri("https://api.example.com/v1")
        };
    }

    public async Task<CollectionsResponse> GetCollections()
    {
        var response = await _client.GetAsync("/ogc/collections");
        response.EnsureSuccessStatusCode();

        // Check version
        if (response.Headers.TryGetValues("X-API-Version", out var versions))
        {
            Console.WriteLine($"API Version: {versions.First()}");
        }

        // Check deprecation
        if (response.Headers.Contains("Deprecation"))
        {
            var sunset = response.Headers.GetValues("Sunset").First();
            Console.WriteLine($"Warning: API deprecated. Sunset: {sunset}");
        }

        return await response.Content.ReadAsAsync<CollectionsResponse>();
    }
}
```

## FAQ

### Q: Why not use header-based versioning?

**A:** While header-based versioning (e.g., `Accept: application/vnd.honua.v1+json`) is elegant, URL-based versioning is more visible, easier to test, and works better with browser-based tools and OGC standards.

### Q: Can I use query parameters for versioning?

**A:** No. Query parameter versioning (e.g., `/ogc/collections?version=1`) is not supported as it's less clean, harder to cache, and not aligned with REST best practices.

### Q: What happens to my bookmarks and saved URLs?

**A:** During Phase 1 and 2, legacy URLs redirect to versioned endpoints, so bookmarks continue to work. Update bookmarks to versioned URLs before Phase 3.

### Q: How do I test a new version before it's released?

**A:** Beta versions may be available at `/v2-beta/` endpoints. Contact support for early access.

### Q: Will health check endpoints be versioned?

**A:** No. Infrastructure endpoints like `/healthz/*` are not versioned as they serve operational purposes, not API consumers.

### Q: Can I negotiate versions dynamically?

**A:** No. Version must be explicitly specified in the URL. This ensures clarity and avoids ambiguity.

## Changelog

### 2025-10-29
- Initial implementation of URL-based versioning
- Added v1 endpoints for all API services
- Implemented legacy URL redirect middleware
- Added deprecation warning support
- Created comprehensive documentation

---

**Last Updated:** 2025-10-29
**Version:** 1.0
**Author:** Honua Development Team
