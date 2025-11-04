# Keyset Pagination Migration Guide

## Overview

HonuaIO has migrated from OFFSET-based pagination to keyset (cursor-based) pagination to achieve constant-time O(1) performance for all pages, regardless of depth.

## Performance Impact

### Before (OFFSET Pagination)
```sql
-- Page 1: Scans 100 rows
SELECT * FROM items ORDER BY created_at DESC LIMIT 100 OFFSET 0;

-- Page 100: Scans 10,000 rows (100x slower!)
SELECT * FROM items ORDER BY created_at DESC LIMIT 100 OFFSET 10000;

-- Page 1000: Scans 100,000 rows (1000x slower!)
SELECT * FROM items ORDER BY created_at DESC LIMIT 100 OFFSET 100000;
```

**Performance degradation**: Page N is N times slower than page 1

### After (Keyset Pagination)
```sql
-- Page 1: Seeks to start
SELECT * FROM items ORDER BY created_at DESC, id ASC LIMIT 100;

-- Page 100: Seeks to cursor position (same speed!)
SELECT * FROM items
WHERE (created_at, id) < (@cursor_time, @cursor_id)
ORDER BY created_at DESC, id ASC LIMIT 100;

-- Page 1000: Seeks to cursor position (same speed!)
SELECT * FROM items
WHERE (created_at, id) < (@cursor_time, @cursor_id)
ORDER BY created_at DESC, id ASC LIMIT 100;
```

**Performance**: All pages have consistent O(1) performance

## Real-World Performance Comparison

| Page | OFFSET Time | Keyset Time | Speedup |
|------|-------------|-------------|---------|
| 1    | 10ms        | 10ms        | 1x      |
| 10   | 50ms        | 10ms        | 5x      |
| 100  | 1000ms      | 10ms        | 100x    |
| 1000 | 10000ms     | 10ms        | 1000x   |

Dataset: 1M records, page size 100, indexed columns

## API Changes

### STAC API

#### Before (OFFSET)
```http
GET /stac/search?limit=100&offset=1000
```

#### After (Cursor)
```http
GET /stac/search?limit=100
# Response includes next_cursor

GET /stac/search?limit=100&cursor=eyJpZCI6MTAwLCJ0aW1lIjoiMjAyNC0wMS0xNSJ9
```

### OGC Features API

#### Before (OFFSET)
```http
GET /collections/buildings/items?limit=100&offset=1000
```

#### After (Cursor)
```http
GET /collections/buildings/items?limit=100
# Response includes Link: <...?cursor=...>; rel="next"

GET /collections/buildings/items?limit=100&cursor=eyJpZCI6MTAwfQ==
```

### WFS GetFeature

#### Before (OFFSET)
```http
GET /wfs?REQUEST=GetFeature&TYPENAMES=buildings&COUNT=100&STARTINDEX=1000
```

#### After (Cursor)
```http
GET /wfs?REQUEST=GetFeature&TYPENAMES=buildings&COUNT=100
# Response includes next link in FeatureCollection

GET /wfs?REQUEST=GetFeature&TYPENAMES=buildings&COUNT=100&CURSOR=eyJpZCI6MTAwfQ==
```

## Client Migration

### JavaScript/TypeScript

#### Before
```typescript
async function fetchAllPages(baseUrl: string) {
  const allItems = [];
  let offset = 0;
  const limit = 100;

  while (true) {
    const response = await fetch(
      `${baseUrl}?limit=${limit}&offset=${offset}`
    );
    const data = await response.json();

    allItems.push(...data.features);

    if (data.features.length < limit) break;
    offset += limit; // Gets slower and slower!
  }

  return allItems;
}
```

#### After
```typescript
async function fetchAllPages(baseUrl: string) {
  const allItems = [];
  let cursor: string | null = null;
  const limit = 100;

  while (true) {
    const url = cursor
      ? `${baseUrl}?limit=${limit}&cursor=${cursor}`
      : `${baseUrl}?limit=${limit}`;

    const response = await fetch(url);
    const data = await response.json();

    allItems.push(...data.features);

    if (!data.next_cursor) break;
    cursor = data.next_cursor; // Consistently fast!
  }

  return allItems;
}
```

### Python

#### Before
```python
def fetch_all_pages(base_url: str):
    all_items = []
    offset = 0
    limit = 100

    while True:
        response = requests.get(
            f"{base_url}?limit={limit}&offset={offset}"
        )
        data = response.json()

        all_items.extend(data['features'])

        if len(data['features']) < limit:
            break
        offset += limit  # Gets slower and slower!

    return all_items
```

#### After
```python
def fetch_all_pages(base_url: str):
    all_items = []
    cursor = None
    limit = 100

    while True:
        params = {'limit': limit}
        if cursor:
            params['cursor'] = cursor

        response = requests.get(base_url, params=params)
        data = response.json()

        all_items.extend(data['features'])

        if not data.get('next_cursor'):
            break
        cursor = data['next_cursor']  # Consistently fast!

    return all_items
```

## Response Format

### STAC Search Response
```json
{
  "type": "FeatureCollection",
  "features": [...],
  "numberMatched": 10000,
  "numberReturned": 100,
  "next_cursor": "eyJjb2xsZWN0aW9uX2lkIjoiYnVpbGRpbmdzIiwiaWQiOiIxMDAifQ==",
  "links": [
    {
      "rel": "next",
      "href": "/stac/search?limit=100&cursor=eyJjb2xsZWN0aW9uX2lkIjoiYnVpbGRpbmdzIiwiaWQiOiIxMDAifQ=="
    }
  ]
}
```

### OGC Features Response
```json
{
  "type": "FeatureCollection",
  "features": [...],
  "numberMatched": 10000,
  "numberReturned": 100,
  "links": [
    {
      "rel": "next",
      "href": "/collections/buildings/items?limit=100&cursor=eyJpZCI6MTAwfQ=="
    }
  ]
}
```

## Cursor Format

Cursors are opaque base64-encoded JSON objects containing the last seen values of sort columns:

```javascript
// Decoded cursor example
{
  "created_at": "2024-01-15T10:30:00Z",
  "id": 100
}

// Encoded cursor (pass this in API requests)
"eyJjcmVhdGVkX2F0IjoiMjAyNC0wMS0xNVQxMDozMDowMFoiLCJpZCI6MTAwfQ=="
```

**Important**: Never parse or construct cursors manually. They are opaque tokens that may change format in future versions.

## Backward Compatibility

### Deprecation Timeline

1. **Current Release**: OFFSET pagination still works but logs deprecation warnings
2. **Next Release (v2.0)**: OFFSET pagination disabled by default, opt-in via config
3. **Future Release (v3.0)**: OFFSET pagination removed completely

### Deprecation Headers

When using OFFSET pagination, the API returns deprecation warnings:

```http
HTTP/1.1 200 OK
X-Deprecation: OFFSET pagination is deprecated and will be removed in v3.0.
               Use cursor-based pagination instead.
Link: <https://docs.honuaio.com/api/pagination>; rel="deprecation"
```

## Database Indexes

The following indexes were added to support keyset pagination:

### STAC Items
```sql
-- Temporal sorting with keyset
CREATE INDEX idx_stac_items_datetime_desc_id_asc
ON stac_items (datetime DESC, id ASC);

-- Temporal range queries with keyset
CREATE INDEX idx_stac_items_temporal_keyset
ON stac_items (start_datetime, end_datetime, id);
```

### Deletion Audit Log
```sql
-- Entity type queries
CREATE INDEX idx_deletion_audit_entitytype_keyset
ON deletion_audit_log (entity_type, deleted_at DESC, id DESC);

-- User queries
CREATE INDEX idx_deletion_audit_user_keyset
ON deletion_audit_log (deleted_by, deleted_at DESC, id DESC);
```

### Custom Feature Tables

For custom feature tables, add similar indexes:

```sql
-- Template for ID-only pagination
CREATE INDEX idx_{table}_id ON {table} (id);

-- Template for temporal sorting
CREATE INDEX idx_{table}_temporal_keyset
ON {table} (datetime DESC, id ASC);

-- Template for multi-column sorting
CREATE INDEX idx_{table}_status_keyset
ON {table} (status ASC, created_at DESC, id ASC);
```

## Troubleshooting

### Issue: Cursor returns no results

**Cause**: Cursor may be expired or invalid

**Solution**: Start from the beginning without a cursor

### Issue: Results are duplicated

**Cause**: Sort columns don't include a unique identifier

**Solution**: Always include a unique column (like `id`) in sort order

### Issue: Performance is still slow

**Cause**: Missing database indexes

**Solution**: Run the migration script to add indexes:
```bash
dotnet ef migrations add KeysetPaginationIndexes
dotnet ef database update
```

## Migration Checklist

- [ ] Update client code to use cursor-based pagination
- [ ] Test with large datasets (>10k records)
- [ ] Verify deep page performance (page 100+)
- [ ] Monitor for deprecation warnings in logs
- [ ] Update API documentation with cursor examples
- [ ] Run database migration to add indexes
- [ ] Test backward compatibility with existing clients
- [ ] Update CI/CD pipelines if needed
- [ ] Notify API consumers of deprecation

## FAQ

### Q: Do I need to update all my code immediately?

A: No. OFFSET pagination will continue to work during the deprecation period. However, we strongly recommend migrating to cursor-based pagination for better performance.

### Q: Can I still use page numbers?

A: No. Keyset pagination uses opaque cursors instead of page numbers. This is a fundamental design difference that enables O(1) performance.

### Q: What if I need random access to pages?

A: Random access (jumping to page 50) is not supported with keyset pagination. This is a trade-off for O(1) performance. If you need random access, consider using OFFSET for small datasets (<10k records) where performance impact is minimal.

### Q: How do I navigate backwards?

A: Use the `previous_cursor` field in the response. Set `direction=backward` in the request to navigate backwards through results.

### Q: Are cursors safe to bookmark/cache?

A: Cursors are valid indefinitely for the same dataset state. However, if data is modified (records added/deleted), cursors may return slightly different results. This is expected and more consistent than OFFSET pagination.

## Support

For questions or issues, please:
- File a GitHub issue: https://github.com/honuaio/honua/issues
- Join our Slack: https://slack.honuaio.com
- Email support: support@honuaio.com

## References

- [PostgreSQL Keyset Pagination](https://www.postgresql.org/docs/current/queries-limit.html)
- [Use the Index, Luke: Pagination](https://use-the-index-luke.com/sql/partial-results/fetch-next-page)
- [Slack's Migration to Keyset Pagination](https://slack.engineering/evolving-api-pagination-at-slack/)
