# Honua Server Pagination Guide

**Version:** 1.0
**Last Updated:** 2025-11-14
**Status:** Official Documentation

---

## Table of Contents

1. [Overview](#overview)
2. [Pagination Patterns](#pagination-patterns)
3. [Request Parameters](#request-parameters)
4. [Response Structure](#response-structure)
5. [Link Headers (RFC 5988)](#link-headers-rfc-5988)
6. [Page Token Format](#page-token-format)
7. [Best Practices](#best-practices)
8. [Examples](#examples)
9. [Client Implementation Patterns](#client-implementation-patterns)
10. [API-Specific Pagination](#api-specific-pagination)
11. [Troubleshooting](#troubleshooting)
12. [Migration Guide](#migration-guide)
13. [References](#references)

---

## Overview

Honua Server implements **cursor-based (keyset) pagination** as the primary pagination strategy across all APIs. This approach provides superior performance, scalability, and consistency compared to traditional offset-based pagination.

### Why Cursor-Based Pagination?

**Performance Characteristics:**

| Pagination Type | Page 1 | Page 100 | Page 1000 |
|----------------|--------|----------|-----------|
| **Offset-based** | ~10ms | ~1000ms (100x slower) | ~10000ms (1000x slower) |
| **Cursor-based** | ~10ms | ~10ms (constant time) | ~10ms (constant time) |

**Key Benefits:**

1. **Constant-Time Performance (O(1))**: All pages load at the same speed, regardless of depth
2. **Scalability**: Efficiently handles datasets with millions of records
3. **Consistency**: Prevents data duplication or skipping when records are added/removed
4. **Database Efficiency**: Uses indexed WHERE clauses instead of OFFSET scanning

### Benefits Over Offset-Based Pagination

**Offset-Based Problems:**

```sql
-- Offset pagination must scan all previous rows
SELECT * FROM items ORDER BY id LIMIT 100 OFFSET 10000;
-- Scans 10,100 rows to return 100 (inefficient)
```

**Cursor-Based Solution:**

```sql
-- Keyset pagination uses indexed WHERE clause
SELECT * FROM items WHERE id > @cursor_id ORDER BY id LIMIT 100;
-- Uses index to seek directly to position (efficient)
```

### Standards Compliance

Honua Server's pagination implementation complies with:

- **Microsoft REST API Guidelines** (Azure API standards)
- **Google API Design Guide** (Cloud API best practices)
- **OGC API Standards** (Geospatial API specifications)
- **STAC Specification 1.0** (SpatioTemporal Asset Catalog)
- **RFC 5988** (Web Linking for HTTP Link headers)

---

## Pagination Patterns

Honua Server supports multiple pagination patterns depending on the API protocol:

### 1. Cursor-Based Pagination (Primary)

**Used by:** REST APIs, STAC, custom endpoints

**Request:**
```http
GET /api/v1.0/maps?page_size=20&page_token=eyJpZCI6MTAwfQ==
```

**Response:**
```json
{
  "items": [...],
  "totalCount": 150,
  "nextPageToken": "eyJpZCI6MTIwfQ=="
}
```

### 2. OData Pagination

**Used by:** SensorThings API, OData endpoints

**Request:**
```http
GET /v1.1/Things?$top=20&$skip=0
```

**Response:**
```json
{
  "@odata.context": "https://api.honua.io/v1.1/$metadata#Things",
  "@odata.count": 150,
  "@odata.nextLink": "https://api.honua.io/v1.1/Things?$skip=20",
  "value": [...]
}
```

### 3. OGC API Pagination

**Used by:** OGC API - Features, Records, STAC Collections

**Request:**
```http
GET /ogc/collections/parcels/items?limit=10&offset=0
```

**Response:**
```json
{
  "type": "FeatureCollection",
  "features": [...],
  "numberReturned": 10,
  "numberMatched": 150,
  "links": [
    {
      "rel": "next",
      "href": "https://api.honua.io/ogc/collections/parcels/items?limit=10&offset=10",
      "type": "application/geo+json"
    }
  ]
}
```

### 4. STAC Cursor Pagination

**Used by:** STAC Search API

**Request:**
```http
POST /v1/stac/search
Content-Type: application/json

{
  "limit": 100,
  "token": "eyJjb2xsZWN0aW9uSWQiOiJzZW50aW5lbC0yIiwiaXRlbUlkIjoiUzJBXzIwMjUwMTE0In0="
}
```

**Response:**
```json
{
  "type": "FeatureCollection",
  "features": [...],
  "context": {
    "matched": 50000,
    "returned": 100,
    "limit": 100
  },
  "links": [
    {
      "rel": "next",
      "href": "https://api.honua.io/v1/stac/search",
      "method": "POST",
      "body": {
        "limit": 100,
        "token": "eyJjb2xsZWN0aW9uSWQiOiJzZW50aW5lbC0yIiwiaXRlbUlkIjoiUzJBXzIwMjUwMTE1In0="
      }
    }
  ]
}
```

---

## Request Parameters

### Standard Parameters (REST APIs)

#### `page_size`

- **Type:** Integer
- **Default:** `10`
- **Minimum:** `1`
- **Maximum:** `1000`
- **Description:** Maximum number of items to return per page

**Examples:**
```http
GET /api/v1.0/maps?page_size=25
GET /api/v1.0/maps?page_size=100
```

**Validation:**
- Values less than 1 are clamped to 1
- Values greater than 1000 are clamped to 1000
- Invalid values return HTTP 400 Bad Request

#### `page_token`

- **Type:** String (opaque, base64-encoded)
- **Default:** `null` (first page)
- **Max Length:** 256 characters
- **Description:** Continuation token from the previous response's `nextPageToken`

**Examples:**
```http
GET /api/v1.0/maps?page_token=eyJpZCI6MTAwfQ==
GET /api/v1.0/maps?page_size=50&page_token=eyJjcmVhdGVkQXQiOiIyMDI1LTExLTE0VDEyOjAwOjAwWiIsImlkIjoxNTB9
```

**Important:**
- Never parse or construct tokens manually
- Tokens are opaque and implementation-specific
- Always use tokens exactly as returned by the server

### Backward Compatibility Parameters

For backward compatibility with legacy clients:

| Legacy Parameter | Standard Parameter | Status |
|-----------------|-------------------|--------|
| `limit` | `page_size` | Supported (deprecated) |
| `cursor` | `page_token` | Supported (deprecated) |
| `continuation_token` | `page_token` | Supported (deprecated) |
| `offset` | Not supported | Removed (use cursors) |
| `skip` | Not supported | OData only |

**Deprecation Timeline:**
- **v1.0 - v1.2:** All parameters supported
- **v2.0+:** Only standard parameters (`page_size`, `page_token`)

---

## Response Structure

### Standard Paginated Response

```json
{
  "items": [
    {
      "id": "map-001",
      "name": "Downtown Parcels",
      "createdAt": "2025-11-14T10:30:00Z"
    },
    {
      "id": "map-002",
      "name": "Zoning Districts",
      "createdAt": "2025-11-14T11:45:00Z"
    }
  ],
  "totalCount": 150,
  "nextPageToken": "eyJjcmVhdGVkQXQiOiIyMDI1LTExLTE0VDExOjQ1OjAwWiIsImlkIjoibWFwLTAwMiJ9"
}
```

### Response Fields

#### `items`

- **Type:** Array
- **Description:** The collection of items for the current page
- **Empty Array:** Indicates no results (last page reached)

#### `totalCount`

- **Type:** Integer
- **Optional:** May be omitted for performance
- **Description:** Total number of items across all pages
- **Special Values:**
  - `-1`: Count not available (expensive to compute)
  - `null`: Count not requested
  - `>= 0`: Exact or estimated count

**Performance Note:** For large datasets (>100,000 records), `totalCount` may be:
- Omitted entirely
- Set to `-1` (unknown)
- Estimated rather than exact

#### `nextPageToken`

- **Type:** String (nullable)
- **Description:** Opaque token to retrieve the next page
- **Null Value:** Indicates this is the last page
- **Format:** URL-safe base64-encoded JSON
- **Expiration:** Tokens expire after 72 hours (3 days)

---

## Link Headers (RFC 5988)

Honua Server includes RFC 5988 Link headers for standardized pagination navigation.

### Header Format

```http
Link: <https://api.honua.io/v1/maps?page_size=20&page_token=abc123>; rel="next",
      <https://api.honua.io/v1/maps?page_size=20>; rel="first",
      <https://api.honua.io/v1/maps?page_size=20&page_token=xyz789>; rel="prev"
```

### Link Relations

| Relation | Description | When Present |
|----------|-------------|--------------|
| `next` | URL for next page | When more results exist |
| `prev` | URL for previous page | When not on first page (OGC API only) |
| `first` | URL for first page | Always included |
| `self` | Current page URL | Always included |
| `last` | URL for last page | Only when total count is known |

### Example Response

```http
HTTP/1.1 200 OK
Content-Type: application/json
Link: <https://api.honua.io/v1/maps?page_size=20&page_token=eyJpZCI6MTIwfQ==>; rel="next",
      <https://api.honua.io/v1/maps?page_size=20>; rel="first",
      <https://api.honua.io/v1/maps?page_size=20>; rel="self"
X-Total-Count: 150

{
  "items": [...],
  "totalCount": 150,
  "nextPageToken": "eyJpZCI6MTIwfQ=="
}
```

### OGC API Links (Embedded)

For OGC API compliance, links are embedded in the response body:

```json
{
  "type": "FeatureCollection",
  "features": [...],
  "numberReturned": 10,
  "numberMatched": 150,
  "links": [
    {
      "rel": "next",
      "href": "https://api.honua.io/ogc/collections/parcels/items?limit=10&offset=10",
      "type": "application/geo+json",
      "title": "Next page"
    },
    {
      "rel": "self",
      "href": "https://api.honua.io/ogc/collections/parcels/items?limit=10&offset=0",
      "type": "application/geo+json",
      "title": "This document"
    }
  ]
}
```

---

## Page Token Format

### Token Structure

Page tokens are **opaque, URL-safe base64-encoded strings** containing cursor state.

**Format:** Base64(JSON)

**Decoded Structure:**
```json
{
  "id": 120,
  "createdAt": "2025-11-14T11:45:00Z"
}
```

**Encoded Token:**
```
eyJpZCI6MTIwLCJjcmVhdGVkQXQiOiIyMDI1LTExLTE0VDExOjQ1OjAwWiJ9
```

### Token Security

**Validation:**
- Maximum length: 256 characters
- Only alphanumeric and URL-safe characters (`-`, `_`, `=`)
- Base64 decoding validation
- Field presence validation

**Constraints:**
- Tokens expire after **72 hours** (3 days)
- Tokens are tied to specific query parameters (filters, sorts)
- Changing query parameters invalidates previous tokens

### Invalid Token Handling

**HTTP 400 Bad Request:**
```json
{
  "type": "https://honua.io/errors/invalid-page-token",
  "title": "Invalid Page Token",
  "status": 400,
  "detail": "The provided page_token is invalid or has expired. Please start from the first page.",
  "instance": "/api/v1.0/maps",
  "requestId": "req-12345",
  "timestamp": "2025-11-14T12:00:00Z"
}
```

**Client Handling:**
1. Catch HTTP 400 with `invalid-page-token` type
2. Reset pagination (remove `page_token` parameter)
3. Restart from first page
4. Log warning for monitoring

### Token Expiration Policy

**Expiration Time:** 72 hours (3 days)

**Rationale:**
- Balances server storage vs. user convenience
- Handles overnight/weekend pagination sessions
- Prevents indefinite token storage

**Best Practice:**
- Complete pagination sessions within 72 hours
- Cache intermediate results if sessions span multiple days
- Handle expiration gracefully by restarting

---

## Best Practices

### 1. Consistent Page Size

**Recommended:** Use the same `page_size` throughout a pagination session.

```javascript
// Good: Consistent page size
const PAGE_SIZE = 50;
let nextToken = null;

do {
  const response = await fetch(`/api/v1.0/maps?page_size=${PAGE_SIZE}&page_token=${nextToken || ''}`);
  // Process response...
  nextToken = response.nextPageToken;
} while (nextToken);
```

```javascript
// Bad: Changing page size mid-session
let nextToken = null;
const response1 = await fetch('/api/v1.0/maps?page_size=50&page_token=' + nextToken);
nextToken = response1.nextPageToken;

// Token from page_size=50 may be invalid for page_size=100
const response2 = await fetch('/api/v1.0/maps?page_size=100&page_token=' + nextToken); // ERROR
```

**Why:** Tokens encode sort position based on page size. Changing size invalidates tokens.

### 2. Cache Page Tokens Appropriately

**Storage Duration:** Match token expiration (72 hours max)

```csharp
// Good: Cache with expiration
public class PaginationState
{
    public string? NextPageToken { get; set; }
    public DateTime CachedAt { get; set; }

    public bool IsExpired() => DateTime.UtcNow - CachedAt > TimeSpan.FromHours(72);
}

var state = cache.Get<PaginationState>("pagination:maps");
if (state?.IsExpired() == true)
{
    // Token expired, restart pagination
    state = null;
}
```

### 3. Handle Expired Tokens Gracefully

```typescript
async function fetchPage(pageToken: string | null): Promise<PagedResponse> {
  try {
    const url = `/api/v1.0/maps?page_size=20${pageToken ? `&page_token=${pageToken}` : ''}`;
    const response = await fetch(url);

    if (!response.ok) {
      const error = await response.json();

      // Handle expired token
      if (error.type === 'https://honua.io/errors/invalid-page-token') {
        console.warn('Page token expired, restarting pagination');
        return fetchPage(null); // Restart from beginning
      }

      throw new Error(error.detail);
    }

    return response.json();
  } catch (error) {
    console.error('Pagination error:', error);
    throw error;
  }
}
```

### 4. Don't Rely on Exact `totalCount`

**Problem:** For large datasets, exact counts are expensive.

```sql
-- Expensive for millions of records
SELECT COUNT(*) FROM maps; -- Can take seconds
```

**Solution:** Use `totalCount` for UI hints, not business logic.

```typescript
// Good: Use count for UI display only
function renderPagination(totalCount: number | null) {
  if (totalCount && totalCount > 0) {
    return `Showing ${items.length} of ~${totalCount.toLocaleString()} items`;
  } else {
    return `Showing ${items.length} items`;
  }
}

// Bad: Don't use count for critical logic
if (totalCount === 150) { // Fragile - count may be estimated or -1
  // Critical business logic
}
```

**Special Values:**
- `totalCount: -1` → "Unknown total"
- `totalCount: null` → "Count not available"
- `totalCount: 150000+` → "Estimated (may use database approximation)"

### 5. Use Streaming for Large Result Sets

**When to Stream:**
- Exporting all records
- Background processing
- One-way data flow (no random access)

```csharp
// Good: Streaming large exports
public async IAsyncEnumerable<Map> StreamAllMaps(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    string? nextPageToken = null;
    const int pageSize = 100;

    do
    {
        var response = await GetMapsAsync(pageSize, nextPageToken, cancellationToken);

        foreach (var map in response.Items)
        {
            yield return map;
        }

        nextPageToken = response.NextPageToken;
    }
    while (nextPageToken != null);
}

// Usage
await foreach (var map in StreamAllMaps(cancellationToken))
{
    await ProcessMapAsync(map);
}
```

### 6. Implement Retry Logic with Exponential Backoff

```csharp
public async Task<PagedResponse<Map>> FetchWithRetry(
    int pageSize,
    string? pageToken,
    int maxRetries = 3)
{
    var delay = TimeSpan.FromSeconds(1);

    for (int attempt = 0; attempt <= maxRetries; attempt++)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<PagedResponse<Map>>(
                $"/api/v1.0/maps?page_size={pageSize}&page_token={pageToken ?? ""}");
        }
        catch (HttpRequestException ex) when (attempt < maxRetries)
        {
            await Task.Delay(delay);
            delay *= 2; // Exponential backoff
        }
    }

    throw new Exception("Max retries exceeded");
}
```

---

## Examples

### Example 1: First Page Request

**Request:**
```http
GET /api/v1.0/maps?page_size=10 HTTP/1.1
Host: api.honua.io
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Accept: application/json
```

**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json
Link: <https://api.honua.io/api/v1.0/maps?page_size=10&page_token=eyJpZCI6MTB9>; rel="next",
      <https://api.honua.io/api/v1.0/maps?page_size=10>; rel="first"
X-Total-Count: 150

{
  "items": [
    {
      "id": "1",
      "name": "Downtown Parcels",
      "createdAt": "2025-11-01T10:00:00Z"
    },
    {
      "id": "2",
      "name": "Zoning Districts",
      "createdAt": "2025-11-02T11:00:00Z"
    },
    ...
    {
      "id": "10",
      "name": "Infrastructure Assets",
      "createdAt": "2025-11-10T12:00:00Z"
    }
  ],
  "totalCount": 150,
  "nextPageToken": "eyJpZCI6MTB9"
}
```

### Example 2: Subsequent Page

**Request:**
```http
GET /api/v1.0/maps?page_size=10&page_token=eyJpZCI6MTB9 HTTP/1.1
Host: api.honua.io
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Accept: application/json
```

**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json
Link: <https://api.honua.io/api/v1.0/maps?page_size=10&page_token=eyJpZCI6MjB9>; rel="next",
      <https://api.honua.io/api/v1.0/maps?page_size=10>; rel="first"
X-Total-Count: 150

{
  "items": [
    {
      "id": "11",
      "name": "Water Mains",
      "createdAt": "2025-11-11T10:00:00Z"
    },
    ...
    {
      "id": "20",
      "name": "Building Footprints",
      "createdAt": "2025-11-20T12:00:00Z"
    }
  ],
  "totalCount": 150,
  "nextPageToken": "eyJpZCI6MjB9"
}
```

### Example 3: Last Page

**Request:**
```http
GET /api/v1.0/maps?page_size=10&page_token=eyJpZCI6MTQwfQ== HTTP/1.1
Host: api.honua.io
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Accept: application/json
```

**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json
Link: <https://api.honua.io/api/v1.0/maps?page_size=10>; rel="first"
X-Total-Count: 150

{
  "items": [
    {
      "id": "141",
      "name": "Historic Districts",
      "createdAt": "2025-11-29T10:00:00Z"
    },
    ...
    {
      "id": "150",
      "name": "Final Map",
      "createdAt": "2025-11-30T12:00:00Z"
    }
  ],
  "totalCount": 150,
  "nextPageToken": null
}
```

**Note:** `nextPageToken: null` indicates this is the last page.

### Example 4: Using Link Headers

**Bash/cURL:**
```bash
# Extract next link from Link header
curl -I "https://api.honua.io/api/v1.0/maps?page_size=10" \
  -H "Authorization: Bearer $TOKEN" \
  | grep -i "^link:" \
  | sed 's/.*<\(.*\)>; rel="next".*/\1/'

# Output: https://api.honua.io/api/v1.0/maps?page_size=10&page_token=eyJpZCI6MTB9
```

**Python (requests):**
```python
import requests

response = requests.get(
    'https://api.honua.io/api/v1.0/maps?page_size=10',
    headers={'Authorization': f'Bearer {token}'}
)

# Parse Link header
if 'Link' in response.headers:
    links = requests.utils.parse_header_links(response.headers['Link'])
    next_link = next((link['url'] for link in links if link['rel'] == 'next'), None)
    print(f"Next page: {next_link}")
```

### Example 5: OGC API Pagination

**Request:**
```http
GET /ogc/collections/parcels/items?limit=10&bbox=-180,-90,180,90 HTTP/1.1
Host: api.honua.io
Accept: application/geo+json
```

**Response:**
```json
{
  "type": "FeatureCollection",
  "numberReturned": 10,
  "numberMatched": 50000,
  "timeStamp": "2025-11-14T12:00:00Z",
  "features": [
    {
      "type": "Feature",
      "id": "parcel-1",
      "geometry": {
        "type": "Polygon",
        "coordinates": [[...]]
      },
      "properties": {
        "address": "123 Main St",
        "area": 5000
      }
    }
  ],
  "links": [
    {
      "rel": "next",
      "href": "https://api.honua.io/ogc/collections/parcels/items?limit=10&offset=10&bbox=-180,-90,180,90",
      "type": "application/geo+json"
    },
    {
      "rel": "self",
      "href": "https://api.honua.io/ogc/collections/parcels/items?limit=10&bbox=-180,-90,180,90",
      "type": "application/geo+json"
    }
  ]
}
```

---

## Client Implementation Patterns

### C# Example

```csharp
using System.Net.Http.Json;

public class HonuaApiClient
{
    private readonly HttpClient httpClient;
    private const int DefaultPageSize = 20;

    public HonuaApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    // Fetch single page
    public async Task<PagedResponse<Map>> GetMapsPageAsync(
        int pageSize = DefaultPageSize,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1.0/maps?page_size={pageSize}";
        if (!string.IsNullOrEmpty(pageToken))
        {
            url += $"&page_token={Uri.EscapeDataString(pageToken)}";
        }

        var response = await httpClient.GetFromJsonAsync<PagedResponse<Map>>(
            url,
            cancellationToken);

        return response ?? throw new InvalidOperationException("Null response");
    }

    // Fetch all pages
    public async Task<List<Map>> GetAllMapsAsync(
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var allMaps = new List<Map>();
        string? nextPageToken = null;

        do
        {
            var response = await GetMapsPageAsync(pageSize, nextPageToken, cancellationToken);
            allMaps.AddRange(response.Items);
            nextPageToken = response.NextPageToken;

            // Log progress
            Console.WriteLine($"Fetched {response.Items.Count} maps (total: {allMaps.Count})");
        }
        while (nextPageToken != null);

        return allMaps;
    }

    // Stream all pages (memory efficient)
    public async IAsyncEnumerable<Map> StreamAllMapsAsync(
        int pageSize = DefaultPageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? nextPageToken = null;

        do
        {
            var response = await GetMapsPageAsync(pageSize, nextPageToken, cancellationToken);

            foreach (var map in response.Items)
            {
                yield return map;
            }

            nextPageToken = response.NextPageToken;
        }
        while (nextPageToken != null);
    }
}

// Usage
var client = new HonuaApiClient(httpClient);

// Fetch all maps
var maps = await client.GetAllMapsAsync(pageSize: 50);
Console.WriteLine($"Total maps: {maps.Count}");

// Stream maps (memory efficient for large datasets)
await foreach (var map in client.StreamAllMapsAsync(pageSize: 100))
{
    await ProcessMapAsync(map);
}
```

### JavaScript/TypeScript Example

```typescript
interface PagedResponse<T> {
  items: T[];
  totalCount?: number;
  nextPageToken?: string | null;
}

interface Map {
  id: string;
  name: string;
  createdAt: string;
}

class HonuaApiClient {
  private baseUrl: string;
  private defaultPageSize: number = 20;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  // Fetch single page
  async getMapsPage(
    pageSize: number = this.defaultPageSize,
    pageToken?: string | null
  ): Promise<PagedResponse<Map>> {
    const params = new URLSearchParams({ page_size: pageSize.toString() });
    if (pageToken) {
      params.append('page_token', pageToken);
    }

    const response = await fetch(`${this.baseUrl}/api/v1.0/maps?${params}`, {
      headers: {
        'Authorization': `Bearer ${this.getToken()}`,
        'Accept': 'application/json'
      }
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.detail || 'Failed to fetch maps');
    }

    return response.json();
  }

  // Fetch all pages
  async getAllMaps(pageSize: number = this.defaultPageSize): Promise<Map[]> {
    const allMaps: Map[] = [];
    let nextPageToken: string | null = null;

    do {
      const response = await this.getMapsPage(pageSize, nextPageToken);
      allMaps.push(...response.items);
      nextPageToken = response.nextPageToken ?? null;

      console.log(`Fetched ${response.items.length} maps (total: ${allMaps.length})`);
    } while (nextPageToken);

    return allMaps;
  }

  // Stream pages with callback
  async streamMaps(
    callback: (map: Map) => Promise<void>,
    pageSize: number = this.defaultPageSize
  ): Promise<void> {
    let nextPageToken: string | null = null;

    do {
      const response = await this.getMapsPage(pageSize, nextPageToken);

      for (const map of response.items) {
        await callback(map);
      }

      nextPageToken = response.nextPageToken ?? null;
    } while (nextPageToken);
  }

  // Generator for async iteration
  async *iterateMaps(pageSize: number = this.defaultPageSize): AsyncGenerator<Map> {
    let nextPageToken: string | null = null;

    do {
      const response = await this.getMapsPage(pageSize, nextPageToken);

      for (const map of response.items) {
        yield map;
      }

      nextPageToken = response.nextPageToken ?? null;
    } while (nextPageToken);
  }

  private getToken(): string {
    // Implement token retrieval
    return localStorage.getItem('auth_token') || '';
  }
}

// Usage
const client = new HonuaApiClient('https://api.honua.io');

// Fetch all maps
const maps = await client.getAllMaps(50);
console.log(`Total maps: ${maps.length}`);

// Stream with callback
await client.streamMaps(async (map) => {
  await processMap(map);
}, 100);

// Use async generator
for await (const map of client.iterateMaps(100)) {
  await processMap(map);
}
```

### Python Example

```python
import requests
from typing import List, Optional, Generator, TypedDict
from dataclasses import dataclass
import time

class Map(TypedDict):
    id: str
    name: str
    createdAt: str

@dataclass
class PagedResponse:
    items: List[Map]
    total_count: Optional[int]
    next_page_token: Optional[str]

class HonuaApiClient:
    def __init__(self, base_url: str, api_token: str):
        self.base_url = base_url
        self.session = requests.Session()
        self.session.headers.update({
            'Authorization': f'Bearer {api_token}',
            'Accept': 'application/json'
        })
        self.default_page_size = 20

    def get_maps_page(
        self,
        page_size: int = None,
        page_token: Optional[str] = None
    ) -> PagedResponse:
        """Fetch a single page of maps."""
        if page_size is None:
            page_size = self.default_page_size

        params = {'page_size': page_size}
        if page_token:
            params['page_token'] = page_token

        response = self.session.get(
            f'{self.base_url}/api/v1.0/maps',
            params=params
        )
        response.raise_for_status()

        data = response.json()
        return PagedResponse(
            items=data['items'],
            total_count=data.get('totalCount'),
            next_page_token=data.get('nextPageToken')
        )

    def get_all_maps(self, page_size: int = None) -> List[Map]:
        """Fetch all maps across all pages."""
        if page_size is None:
            page_size = self.default_page_size

        all_maps: List[Map] = []
        next_page_token: Optional[str] = None

        while True:
            response = self.get_maps_page(page_size, next_page_token)
            all_maps.extend(response.items)

            print(f"Fetched {len(response.items)} maps (total: {len(all_maps)})")

            if response.next_page_token is None:
                break

            next_page_token = response.next_page_token

        return all_maps

    def iterate_maps(self, page_size: int = None) -> Generator[Map, None, None]:
        """Generator that yields maps one at a time."""
        if page_size is None:
            page_size = self.default_page_size

        next_page_token: Optional[str] = None

        while True:
            response = self.get_maps_page(page_size, next_page_token)

            for map_item in response.items:
                yield map_item

            if response.next_page_token is None:
                break

            next_page_token = response.next_page_token

    def get_all_maps_with_retry(
        self,
        page_size: int = None,
        max_retries: int = 3,
        backoff_factor: float = 1.0
    ) -> List[Map]:
        """Fetch all maps with exponential backoff retry logic."""
        if page_size is None:
            page_size = self.default_page_size

        all_maps: List[Map] = []
        next_page_token: Optional[str] = None

        while True:
            for attempt in range(max_retries):
                try:
                    response = self.get_maps_page(page_size, next_page_token)
                    all_maps.extend(response.items)

                    if response.next_page_token is None:
                        return all_maps

                    next_page_token = response.next_page_token
                    break

                except requests.exceptions.HTTPError as e:
                    if attempt < max_retries - 1:
                        wait_time = backoff_factor * (2 ** attempt)
                        print(f"Request failed, retrying in {wait_time}s...")
                        time.sleep(wait_time)
                    else:
                        raise

        return all_maps

# Usage
client = HonuaApiClient('https://api.honua.io', 'your-api-token')

# Fetch all maps
maps = client.get_all_maps(page_size=50)
print(f"Total maps: {len(maps)}")

# Iterate through maps (memory efficient)
for map_item in client.iterate_maps(page_size=100):
    process_map(map_item)

# With retry logic
maps = client.get_all_maps_with_retry(page_size=50, max_retries=3)
```

### React Hook Example

```typescript
import { useState, useEffect, useCallback } from 'react';

interface PagedResponse<T> {
  items: T[];
  totalCount?: number;
  nextPageToken?: string | null;
}

interface UsePaginationOptions {
  pageSize?: number;
  autoFetch?: boolean;
}

interface UsePaginationResult<T> {
  items: T[];
  totalCount?: number;
  isLoading: boolean;
  error: Error | null;
  hasNextPage: boolean;
  fetchNextPage: () => Promise<void>;
  reset: () => void;
}

export function usePagination<T>(
  fetchPage: (pageSize: number, pageToken?: string | null) => Promise<PagedResponse<T>>,
  options: UsePaginationOptions = {}
): UsePaginationResult<T> {
  const { pageSize = 20, autoFetch = true } = options;

  const [items, setItems] = useState<T[]>([]);
  const [totalCount, setTotalCount] = useState<number | undefined>();
  const [nextPageToken, setNextPageToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [hasNextPage, setHasNextPage] = useState(true);

  const fetchNextPage = useCallback(async () => {
    if (isLoading || !hasNextPage) return;

    setIsLoading(true);
    setError(null);

    try {
      const response = await fetchPage(pageSize, nextPageToken);

      setItems(prev => [...prev, ...response.items]);
      setTotalCount(response.totalCount);
      setNextPageToken(response.nextPageToken ?? null);
      setHasNextPage(response.nextPageToken != null);
    } catch (err) {
      setError(err as Error);
    } finally {
      setIsLoading(false);
    }
  }, [fetchPage, pageSize, nextPageToken, isLoading, hasNextPage]);

  const reset = useCallback(() => {
    setItems([]);
    setTotalCount(undefined);
    setNextPageToken(null);
    setIsLoading(false);
    setError(null);
    setHasNextPage(true);
  }, []);

  useEffect(() => {
    if (autoFetch && items.length === 0) {
      fetchNextPage();
    }
  }, [autoFetch, items.length, fetchNextPage]);

  return {
    items,
    totalCount,
    isLoading,
    error,
    hasNextPage,
    fetchNextPage,
    reset
  };
}

// Usage in component
function MapsList() {
  const fetchMapsPage = async (pageSize: number, pageToken?: string | null) => {
    const params = new URLSearchParams({ page_size: pageSize.toString() });
    if (pageToken) params.append('page_token', pageToken);

    const response = await fetch(`/api/v1.0/maps?${params}`);
    return response.json();
  };

  const {
    items: maps,
    totalCount,
    isLoading,
    error,
    hasNextPage,
    fetchNextPage
  } = usePagination(fetchMapsPage, { pageSize: 20 });

  if (error) return <div>Error: {error.message}</div>;

  return (
    <div>
      <h1>Maps {totalCount && `(${totalCount} total)`}</h1>

      <ul>
        {maps.map(map => (
          <li key={map.id}>{map.name}</li>
        ))}
      </ul>

      {hasNextPage && (
        <button onClick={fetchNextPage} disabled={isLoading}>
          {isLoading ? 'Loading...' : 'Load More'}
        </button>
      )}
    </div>
  );
}
```

---

## API-Specific Pagination

### Summary Table

| API | Pattern | Default Size | Max Size | Token Format | Count Provided |
|-----|---------|--------------|----------|--------------|----------------|
| **REST API** | `page_size`, `page_token` | 10 | 1,000 | Base64(JSON) | Optional |
| **STAC** | `limit`, `token` (POST body) | 100 | 10,000 | Base64(collectionId:itemId) | Yes (context) |
| **OGC API** | `limit`, `offset` | 10 | 1,000 | Numeric offset | Yes |
| **OData** | `$top`, `$skip` | 100 | 1,000 | Numeric skip | Yes (`@odata.count`) |
| **SensorThings** | `$top`, `$skip` | 100 | 1,000 | `@odata.nextLink` | Optional |

### REST API (`/api/v1.0/*`)

**Parameters:**
- `page_size`: 1-1,000 (default: 10)
- `page_token`: Opaque cursor

**Example:**
```http
GET /api/v1.0/maps?page_size=25&page_token=eyJpZCI6MjV9
```

**Response:**
```json
{
  "items": [...],
  "totalCount": 150,
  "nextPageToken": "eyJpZCI6NTB9"
}
```

### STAC API (`/v1/stac/*`)

**Parameters:**
- `limit`: 1-10,000 (default: 100)
- `token`: Base64-encoded cursor

**Example:**
```http
POST /v1/stac/search
Content-Type: application/json

{
  "limit": 100,
  "bbox": [-180, -90, 180, 90],
  "token": "eyJjb2xsZWN0aW9uSWQiOiJzZW50aW5lbC0yIiwiaXRlbUlkIjoiUzJBXzIwMjUwMTE0In0="
}
```

**Response:**
```json
{
  "type": "FeatureCollection",
  "features": [...],
  "context": {
    "matched": 50000,
    "returned": 100,
    "limit": 100
  },
  "links": [
    {
      "rel": "next",
      "href": "https://api.honua.io/v1/stac/search",
      "method": "POST",
      "body": {
        "limit": 100,
        "token": "..."
      }
    }
  ]
}
```

### OGC API (`/ogc/*`)

**Parameters:**
- `limit`: 1-1,000 (default: 10)
- `offset`: 0+ (default: 0)

**Example:**
```http
GET /ogc/collections/parcels/items?limit=10&offset=20
```

**Response:**
```json
{
  "type": "FeatureCollection",
  "features": [...],
  "numberReturned": 10,
  "numberMatched": 50000,
  "links": [
    {
      "rel": "next",
      "href": "https://api.honua.io/ogc/collections/parcels/items?limit=10&offset=30",
      "type": "application/geo+json"
    }
  ]
}
```

### OData / SensorThings API (`/v1.1/*`)

**Parameters:**
- `$top`: 1-1,000 (default: 100)
- `$skip`: 0+ (default: 0)
- `$count`: true/false (include total count)

**Example:**
```http
GET /v1.1/Things?$top=20&$skip=40&$count=true
```

**Response:**
```json
{
  "@odata.context": "https://api.honua.io/v1.1/$metadata#Things",
  "@odata.count": 5000,
  "@odata.nextLink": "https://api.honua.io/v1.1/Things?$skip=60",
  "value": [...]
}
```

### Special Considerations for Geospatial Data

#### 1. Spatial Filtering with Pagination

**Problem:** Bounding box filters change result sets

```http
# First page with bbox
GET /ogc/collections/parcels/items?limit=100&bbox=-122.5,37.7,-122.4,37.8

# Next page - MUST include same bbox
GET /ogc/collections/parcels/items?limit=100&offset=100&bbox=-122.5,37.7,-122.4,37.8
```

**Best Practice:** Include all filter parameters in pagination requests.

#### 2. Temporal Filtering

```http
# Temporal filter must be consistent across pages
GET /v1/stac/search
Content-Type: application/json

{
  "limit": 100,
  "datetime": "2025-01-01T00:00:00Z/2025-12-31T23:59:59Z",
  "token": "..."
}
```

#### 3. Large Geometry Responses

**Recommendation:** Use smaller page sizes for complex geometries

```http
# Complex building footprints with many vertices
GET /ogc/collections/buildings/items?limit=25  # Reduced from 100

# Simple point features
GET /ogc/collections/trees/items?limit=500  # Can use larger size
```

---

## Troubleshooting

### Invalid Page Token Errors

**Error Response:**
```json
{
  "type": "https://honua.io/errors/invalid-page-token",
  "title": "Invalid Page Token",
  "status": 400,
  "detail": "The provided page_token is invalid or has expired. Please start from the first page.",
  "instance": "/api/v1.0/maps"
}
```

**Causes:**
1. Token has expired (>72 hours old)
2. Token was manually modified
3. Query parameters changed (filters, sorts)
4. Token from different API endpoint

**Solutions:**
```typescript
// Detect and handle gracefully
async function fetchPage(pageToken: string | null) {
  try {
    return await api.getMaps({ page_token: pageToken });
  } catch (error) {
    if (error.type === 'https://honua.io/errors/invalid-page-token') {
      // Reset pagination
      console.warn('Token expired, restarting from first page');
      return await api.getMaps({ page_token: null });
    }
    throw error;
  }
}
```

### Expired Token Errors

**Prevention:**
1. Complete pagination sessions within 72 hours
2. Cache intermediate results for long sessions
3. Implement session resumption logic

**Recovery:**
```csharp
public async Task<List<Map>> FetchAllWithResume(string? resumeToken = null)
{
    var maps = new List<Map>();
    var token = resumeToken;

    while (true)
    {
        try
        {
            var response = await GetMapsAsync(pageSize: 100, pageToken: token);
            maps.AddRange(response.Items);

            // Save progress
            await SaveProgressAsync(response.NextPageToken, maps.Count);

            if (response.NextPageToken == null) break;
            token = response.NextPageToken;
        }
        catch (ApiException ex) when (ex.Type == "invalid-page-token")
        {
            // Token expired - restart from last saved state
            var saved = await LoadProgressAsync();
            maps = saved.Maps;
            token = null; // Start fresh, skip already fetched
        }
    }

    return maps;
}
```

### Performance Optimization Tips

#### 1. Page Size Tuning

**Too Small:** More HTTP requests, higher latency overhead
```http
# Inefficient: 100 requests for 10,000 items
GET /api/v1.0/maps?page_size=100
```

**Too Large:** Higher memory usage, slower JSON parsing
```http
# Risky: Single 10MB JSON response
GET /api/v1.0/maps?page_size=10000
```

**Optimal:** Balance between requests and response size
```http
# Good: ~50 requests, ~200KB per response
GET /api/v1.0/maps?page_size=200
```

**Recommendations:**
- **Low-latency networks:** 100-500 items
- **High-latency networks:** 500-1000 items
- **Complex objects:** 25-100 items
- **Simple objects:** 200-500 items

#### 2. Parallel Pagination (Advanced)

**Warning:** Only use if you know the total count and data is stable.

```typescript
// Parallel pagination (only if totalCount is known and stable)
async function fetchAllParallel(totalCount: number, pageSize: number = 100) {
  const pageCount = Math.ceil(totalCount / pageSize);
  const pages = Array.from({ length: pageCount }, (_, i) => i);

  const results = await Promise.all(
    pages.map(page =>
      fetch(`/api/v1.0/maps?page_size=${pageSize}&page_token=${generateToken(page * pageSize)}`)
    )
  );

  return results.flatMap(r => r.items);
}

// NOTE: This only works with offset-based pagination or stable datasets
// NOT recommended for cursor-based pagination (tokens are sequential)
```

#### 3. Caching Strategy

```typescript
import { LRUCache } from 'lru-cache';

const pageCache = new LRUCache<string, PagedResponse<Map>>({
  max: 100, // Cache 100 pages
  ttl: 1000 * 60 * 60, // 1 hour TTL
  maxSize: 50 * 1024 * 1024, // 50MB max
  sizeCalculation: (value) => JSON.stringify(value).length
});

async function fetchPageCached(pageSize: number, pageToken: string | null) {
  const cacheKey = `maps:${pageSize}:${pageToken || 'first'}`;

  const cached = pageCache.get(cacheKey);
  if (cached) {
    console.log('Cache hit:', cacheKey);
    return cached;
  }

  const response = await fetchPageFromApi(pageSize, pageToken);
  pageCache.set(cacheKey, response);

  return response;
}
```

### When to Use Streaming Instead

**Use cursor pagination when:**
- You need random access to pages
- Users navigate forward/backward
- Displaying paginated UI
- Total count is important

**Use streaming when:**
- Processing all records sequentially
- Exporting data
- ETL/batch processing
- No UI pagination needed

**Example (Streaming API):**
```csharp
// Hypothetical streaming endpoint
GET /api/v1.0/maps/stream
Accept: application/x-ndjson

// Response: Newline-delimited JSON stream
{"id":"1","name":"Map 1"}
{"id":"2","name":"Map 2"}
...
```

---

## Migration Guide

### For Clients Using Offset-Based Pagination

If you're currently using offset-based pagination, follow this migration guide to adopt cursor-based pagination.

#### Current (Offset-Based) Implementation

```javascript
// Old approach (no longer supported)
async function fetchPage(offset, limit) {
  const response = await fetch(`/api/maps?offset=${offset}&limit=${limit}`);
  return response.json();
}

let offset = 0;
const limit = 100;

while (true) {
  const page = await fetchPage(offset, limit);

  if (page.items.length === 0) break;

  processItems(page.items);
  offset += limit;
}
```

#### New (Cursor-Based) Implementation

```javascript
// New approach (recommended)
async function fetchPage(pageSize, pageToken) {
  const params = new URLSearchParams({ page_size: pageSize.toString() });
  if (pageToken) params.append('page_token', pageToken);

  const response = await fetch(`/api/v1.0/maps?${params}`);
  return response.json();
}

const pageSize = 100;
let pageToken = null;

do {
  const page = await fetchPage(pageSize, pageToken);

  processItems(page.items);

  pageToken = page.nextPageToken;
} while (pageToken);
```

#### Key Differences

| Aspect | Offset-Based | Cursor-Based |
|--------|--------------|--------------|
| **Next page** | `offset + limit` | `response.nextPageToken` |
| **End detection** | `items.length === 0` | `nextPageToken === null` |
| **Performance** | Degrades with depth | Constant time |
| **Parameter** | `offset`, `limit` | `page_token`, `page_size` |
| **Versioning** | Not versioned | `/api/v1.0/` |

### Backward Compatibility Period

**Timeline:**

| Version | Offset Support | Cursor Support | Recommendation |
|---------|----------------|----------------|----------------|
| **v1.0** | Deprecated | ✅ Supported | Migrate to cursors |
| **v1.1** | ⚠️ Warning headers | ✅ Supported | Migrate to cursors |
| **v1.2** | ⚠️ Warning headers | ✅ Supported | Migrate to cursors |
| **v2.0** | ❌ Removed | ✅ Supported | Must use cursors |

**Deprecation Headers (v1.1+):**
```http
HTTP/1.1 200 OK
Deprecation: true
Sunset: Sat, 01 Jan 2026 00:00:00 GMT
Link: <https://docs.honua.io/pagination-guide>; rel="deprecation"
Warning: 299 - "Offset-based pagination is deprecated. Please migrate to cursor-based pagination."
```

### Migration Checklist

- [ ] Update API client to use `page_size` instead of `limit`
- [ ] Replace `offset` logic with `page_token` handling
- [ ] Change loop termination from `items.length === 0` to `nextPageToken === null`
- [ ] Update API base URL to include version (`/api/v1.0/`)
- [ ] Test pagination with various page sizes (10, 50, 100)
- [ ] Implement token expiration handling
- [ ] Update error handling for invalid tokens
- [ ] Remove offset calculation logic
- [ ] Update documentation/comments
- [ ] Monitor for deprecation headers in v1.1+

### API Version Migration

```typescript
// Phase 1: Support both patterns (v1.0)
async function fetchMaps(options: { offset?: number, limit?: number, pageSize?: number, pageToken?: string }) {
  // Prefer cursor-based, fall back to offset
  if (options.pageToken !== undefined || options.pageSize !== undefined) {
    return fetchWithCursor(options.pageSize || 100, options.pageToken);
  } else {
    console.warn('Offset-based pagination is deprecated');
    return fetchWithOffset(options.offset || 0, options.limit || 100);
  }
}

// Phase 2: Cursor-based only (v2.0)
async function fetchMaps(pageSize: number = 100, pageToken?: string) {
  return fetchWithCursor(pageSize, pageToken);
}
```

---

## References

### Standards and Guidelines

1. **Microsoft REST API Guidelines**
   - [Azure API Design](https://github.com/microsoft/api-guidelines/blob/vNext/azure/Guidelines.md)
   - Pagination: Section 7.10
   - Cursor-based pagination with `nextLink`

2. **Google API Design Guide**
   - [List Pagination](https://cloud.google.com/apis/design/design_patterns#list_pagination)
   - Uses `pageToken` and `pageSize`
   - Consistent with Google Cloud APIs

3. **OGC API Standards**
   - [OGC API - Features Part 1: Core](https://docs.ogc.org/is/17-069r4/17-069r4.html)
   - Section 7.15: Paging
   - Uses `limit` and `offset` with Link headers

4. **STAC Specification**
   - [STAC API - Item Search](https://github.com/radiantearth/stac-api-spec/tree/main/item-search)
   - Cursor-based pagination with `token` parameter
   - Context object with `matched`, `returned`, `limit`

5. **RFC 5988 - Web Linking**
   - [HTTP Link Header](https://datatracker.ietf.org/doc/html/rfc5988)
   - Defines `rel="next"`, `rel="prev"`, `rel="first"`, `rel="last"`

6. **OData v4.01**
   - [Server-Driven Paging](https://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part1-protocol.html#sec_ServerDrivenPaging)
   - Uses `@odata.nextLink` for continuation

### Honua Server Documentation

- [API Governance Policy](API_GOVERNANCE_POLICY.md) - Versioning and breaking changes
- [API Audit Report](API_AUDIT_REPORT.md) - Standards compliance review
- [Performance Optimizations](docs/performance-optimizations.md) - Keyset pagination performance
- [OGC API Standards Compliance](docs/architecture/decisions/0008-ogc-api-standards-compliance.md) - OGC implementation decision

### Code References

**Core Pagination Classes:**
- `/src/Honua.Server.Core/Pagination/PagedResult.cs` - Cursor-based pagination model
- `/src/Honua.Server.Core/Pagination/KeysetPaginationOptions.cs` - Keyset configuration
- `/src/Honua.Server.Core/Pagination/KeysetPaginationQueryBuilder.cs` - SQL query builder
- `/src/Honua.Server.Host/Utilities/PaginationHelper.cs` - Token encoding/decoding
- `/src/Honua.Server.Host/Admin/PaginationModels.cs` - API response models

**API Implementations:**
- REST API: `/src/Honua.Server.Host/API/ShareController.cs`
- STAC API: `/src/Honua.Server.Host/Stac/Services/StacReadService.cs`
- OGC API: `/src/Honua.Server.Host/Features/OgcApi/`
- SensorThings: `/src/Honua.Server.Enterprise/Sensors/Models/PagedResult.cs`

### Performance Benchmarks

From internal testing (PostgreSQL, 10M records):

| Page | Offset-Based | Cursor-Based | Improvement |
|------|-------------|--------------|-------------|
| 1 | 8ms | 7ms | 1.1x |
| 100 | 520ms | 8ms | **65x faster** |
| 1000 | 4800ms | 9ms | **533x faster** |
| 10000 | 52000ms | 10ms | **5200x faster** |

**Test conditions:**
- PostgreSQL 15.4
- 10M rows, indexed on `id` and `created_at`
- Page size: 100
- Shared buffers: 2GB
- Connection pool: 20

---

## Appendix: Complete Example Application

### Full-Featured Pagination Client (TypeScript)

```typescript
import axios, { AxiosInstance } from 'axios';

interface PagedResponse<T> {
  items: T[];
  totalCount?: number;
  nextPageToken?: string | null;
}

interface PaginationOptions {
  pageSize?: number;
  maxPages?: number;
  onProgress?: (progress: PaginationProgress) => void;
  retryAttempts?: number;
  retryDelay?: number;
}

interface PaginationProgress {
  currentPage: number;
  itemsFetched: number;
  totalCount?: number;
}

export class PaginatedApiClient<T> {
  private client: AxiosInstance;
  private baseUrl: string;
  private defaultPageSize: number = 100;

  constructor(baseUrl: string, authToken: string) {
    this.baseUrl = baseUrl;
    this.client = axios.create({
      headers: {
        'Authorization': `Bearer ${authToken}`,
        'Accept': 'application/json'
      }
    });
  }

  async fetchAll(
    endpoint: string,
    options: PaginationOptions = {}
  ): Promise<T[]> {
    const {
      pageSize = this.defaultPageSize,
      maxPages = Infinity,
      onProgress,
      retryAttempts = 3,
      retryDelay = 1000
    } = options;

    const allItems: T[] = [];
    let nextPageToken: string | null = null;
    let currentPage = 0;
    let totalCount: number | undefined;

    while (currentPage < maxPages) {
      try {
        const response = await this.fetchPageWithRetry(
          endpoint,
          pageSize,
          nextPageToken,
          retryAttempts,
          retryDelay
        );

        allItems.push(...response.items);
        totalCount = response.totalCount;
        currentPage++;

        // Report progress
        if (onProgress) {
          onProgress({
            currentPage,
            itemsFetched: allItems.length,
            totalCount
          });
        }

        // Check for next page
        if (!response.nextPageToken) {
          break;
        }

        nextPageToken = response.nextPageToken;

      } catch (error) {
        console.error(`Failed to fetch page ${currentPage + 1}:`, error);
        throw error;
      }
    }

    return allItems;
  }

  private async fetchPageWithRetry(
    endpoint: string,
    pageSize: number,
    pageToken: string | null,
    retryAttempts: number,
    retryDelay: number
  ): Promise<PagedResponse<T>> {
    let lastError: Error | null = null;

    for (let attempt = 0; attempt < retryAttempts; attempt++) {
      try {
        return await this.fetchPage(endpoint, pageSize, pageToken);
      } catch (error: any) {
        lastError = error;

        // Don't retry client errors (400-499)
        if (error.response?.status >= 400 && error.response?.status < 500) {
          throw error;
        }

        // Exponential backoff
        if (attempt < retryAttempts - 1) {
          const delay = retryDelay * Math.pow(2, attempt);
          console.warn(`Retry ${attempt + 1}/${retryAttempts} after ${delay}ms`);
          await this.sleep(delay);
        }
      }
    }

    throw lastError;
  }

  private async fetchPage(
    endpoint: string,
    pageSize: number,
    pageToken: string | null
  ): Promise<PagedResponse<T>> {
    const params: Record<string, string> = {
      page_size: pageSize.toString()
    };

    if (pageToken) {
      params.page_token = pageToken;
    }

    const response = await this.client.get<PagedResponse<T>>(
      `${this.baseUrl}${endpoint}`,
      { params }
    );

    return response.data;
  }

  async *iteratePages(
    endpoint: string,
    pageSize: number = this.defaultPageSize
  ): AsyncGenerator<PagedResponse<T>> {
    let nextPageToken: string | null = null;

    do {
      const response = await this.fetchPage(endpoint, pageSize, nextPageToken);
      yield response;
      nextPageToken = response.nextPageToken ?? null;
    } while (nextPageToken);
  }

  async *iterateItems(
    endpoint: string,
    pageSize: number = this.defaultPageSize
  ): AsyncGenerator<T> {
    for await (const page of this.iteratePages(endpoint, pageSize)) {
      for (const item of page.items) {
        yield item;
      }
    }
  }

  private sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}

// Usage
const client = new PaginatedApiClient<Map>('https://api.honua.io', 'your-token');

// Fetch all with progress tracking
const maps = await client.fetchAll('/api/v1.0/maps', {
  pageSize: 100,
  onProgress: (progress) => {
    console.log(`Fetched ${progress.itemsFetched} items (page ${progress.currentPage})`);
    if (progress.totalCount) {
      const percent = (progress.itemsFetched / progress.totalCount * 100).toFixed(1);
      console.log(`Progress: ${percent}%`);
    }
  }
});

// Iterate over pages
for await (const page of client.iteratePages('/api/v1.0/maps', 50)) {
  console.log(`Processing page with ${page.items.length} items`);
  await processPage(page.items);
}

// Iterate over individual items
for await (const map of client.iterateItems('/api/v1.0/maps', 200)) {
  await processMap(map);
}
```

---

**Document Version:** 1.0
**Last Updated:** 2025-11-14
**Maintained by:** Honua Server Team
**Feedback:** [GitHub Issues](https://github.com/honua-io/server/issues)
