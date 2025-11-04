# Issue #2: N+1 Query Fix - Before/After Comparison

## Before (N+1 Query Problem)

```
Client Request: POST /ogc/collections/cities/items
Body: { type: "FeatureCollection", features: [feature1, feature2, feature3] }
     │
     ├─> Edit Orchestrator: Batch insert 3 features
     │   └─> Database: INSERT 3 rows [1 query]
     │
     ├─> For each feature (loop 3 times):
     │   ├─> GetAsync(featureId1) → Database: SELECT * WHERE id = 1 [query 2]
     │   ├─> GetAsync(featureId2) → Database: SELECT * WHERE id = 2 [query 3]
     │   └─> GetAsync(featureId3) → Database: SELECT * WHERE id = 3 [query 4]
     │
     └─> Response: FeatureCollection with 3 features

Total Queries: 4 (1 + N where N = 3)
```

**Problem:** Each additional feature adds one more database query.
- 10 features = 11 queries
- 100 features = 101 queries
- 1000 features = 1001 queries

---

## After (Batch Retrieval)

```
Client Request: POST /ogc/collections/cities/items
Body: { type: "FeatureCollection", features: [feature1, feature2, feature3] }
     │
     ├─> Edit Orchestrator: Batch insert 3 features
     │   └─> Database: INSERT 3 rows [1 query]
     │
     ├─> Collect all IDs: [id1, id2, id3]
     │
     ├─> Build filter: id IN (id1, id2, id3)
     │   Implementation: (id = id1 OR id = id2 OR id = id3)
     │
     ├─> QueryAsync with filter
     │   └─> Database: SELECT * WHERE id IN (1, 2, 3) [query 2]
     │
     ├─> Store results in Dictionary<id, record>
     │
     ├─> Match results back to original order
     │
     └─> Response: FeatureCollection with 3 features

Total Queries: 2 (constant, regardless of N)
```

**Solution:** Number of queries remains constant at 2, regardless of batch size.
- 10 features = 2 queries
- 100 features = 2 queries
- 1000 features = 2 queries

---

## Performance Improvement

| Batch Size | Before (queries) | After (queries) | Improvement |
|------------|------------------|-----------------|-------------|
| 1          | 2                | 2               | 0%          |
| 10         | 11               | 2               | 81.8%       |
| 50         | 51               | 2               | 96.1%       |
| 100        | 101              | 2               | 98.0%       |
| 500        | 501              | 2               | 99.6%       |
| 1000       | 1001             | 2               | 99.8%       |

---

## Code Changes

### Location
`src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs` (lines 1069-1170)

### Before
```csharp
for (var index = 0; index < editResult.Results.Count; index++)
{
    var result = editResult.Results[index];
    var featureId = result.FeatureId ?? fallbackIds.ElementAtOrDefault(index);
    if (string.IsNullOrWhiteSpace(featureId))
    {
        continue;
    }

    var record = await repository.GetAsync(context.Service.Id, layer.Id, featureId!, featureQuery, cancellationToken);
    // ❌ N+1 query problem - one database query per feature

    if (record is null)
    {
        continue;
    }

    var payload = OgcSharedHandlers.ToFeature(request, collectionId, layer, record, featureQuery);
    var etag = OgcSharedHandlers.ComputeFeatureEtag(layer, record);
    created.Add((featureId, payload, etag));
}
```

### After
```csharp
// Step 1: Collect all feature IDs
var allFeatureIds = new List<string>();
for (var index = 0; index < editResult.Results.Count; index++)
{
    var result = editResult.Results[index];
    var featureId = result.FeatureId ?? fallbackIds.ElementAtOrDefault(index);
    if (!string.IsNullOrWhiteSpace(featureId))
    {
        allFeatureIds.Add(featureId!);
    }
}

if (allFeatureIds.Count > 0)
{
    // Step 2: Build ID filter (id = id1 OR id = id2 OR ...)
    QueryExpression? expression = null;
    var resolved = CqlFilterParserUtils.ResolveField(layer, layer.IdField);

    foreach (var featureId in allFeatureIds)
    {
        var typedValue = CqlFilterParserUtils.ConvertToFieldValue(resolved.fieldType, featureId);
        var comparison = new QueryBinaryExpression(
            new QueryFieldReference(resolved.fieldName),
            QueryBinaryOperator.Equal,
            new QueryConstant(typedValue));

        expression = expression is null
            ? comparison
            : new QueryBinaryExpression(expression, QueryBinaryOperator.Or, comparison);
    }

    // Step 3: Execute single batch query ✅ One query for all features
    var batchFilter = new QueryFilter(expression!);
    var batchQuery = featureQuery with { Filter = batchFilter };

    var recordsById = new Dictionary<string, FeatureRecord>(StringComparer.OrdinalIgnoreCase);
    await foreach (var record in repository.QueryAsync(context.Service.Id, layer.Id, batchQuery, cancellationToken))
    {
        if (record.Attributes.TryGetValue(layer.IdField, out var idValue) && idValue is not null)
        {
            var recordId = idValue.ToString();
            if (!string.IsNullOrWhiteSpace(recordId))
            {
                recordsById[recordId!] = record;
            }
        }
    }

    // Step 4: Match results back to original order
    for (var index = 0; index < editResult.Results.Count; index++)
    {
        var result = editResult.Results[index];
        var featureId = result.FeatureId ?? fallbackIds.ElementAtOrDefault(index);
        if (!string.IsNullOrWhiteSpace(featureId) && recordsById.TryGetValue(featureId!, out var record))
        {
            var payload = OgcSharedHandlers.ToFeature(request, collectionId, layer, record, featureQuery);
            var etag = OgcSharedHandlers.ComputeFeatureEtag(layer, record);
            created.Add((featureId, payload, etag));
        }
    }
}
```

---

## Database Query Examples

### Before (100 features)
```sql
-- Query 1: Batch insert
INSERT INTO cities (id, name, geometry) VALUES
  (1, 'City1', ...),
  (2, 'City2', ...),
  ...
  (100, 'City100', ...);

-- Query 2-101: Individual SELECTs (N+1 problem)
SELECT * FROM cities WHERE id = 1;
SELECT * FROM cities WHERE id = 2;
SELECT * FROM cities WHERE id = 3;
...
SELECT * FROM cities WHERE id = 100;
```

### After (100 features)
```sql
-- Query 1: Batch insert
INSERT INTO cities (id, name, geometry) VALUES
  (1, 'City1', ...),
  (2, 'City2', ...),
  ...
  (100, 'City100', ...);

-- Query 2: Single batch SELECT
SELECT * FROM cities
WHERE id = 1 OR id = 2 OR id = 3 OR ... OR id = 100;
-- Or with database-specific optimization:
-- SELECT * FROM cities WHERE id IN (1, 2, 3, ..., 100);
```

---

## Benefits

1. **Performance:** 50-500x reduction in database queries for typical batch sizes
2. **Scalability:** Constant O(2) queries regardless of batch size
3. **Network:** Reduced database network roundtrips
4. **Latency:** Lower overall response time for batch operations
5. **Database Load:** Significantly reduced query load on the database server
6. **Connection Pool:** Fewer connections held for shorter periods

## Backward Compatibility

✅ Response structure remains identical
✅ Error handling preserved
✅ Transaction semantics unchanged
✅ Fallback mechanism for edge cases
✅ No breaking API changes
