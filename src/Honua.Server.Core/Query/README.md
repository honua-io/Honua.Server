# Query Engine Module

## Overview

The Query engine provides a database-agnostic abstraction layer for building, parsing, and executing complex geospatial queries. It translates high-level query expressions from multiple query languages (CQL, CQL2 JSON, OData) into optimized SQL for different database backends (PostgreSQL, SQL Server, MySQL, SQLite).

**Key Features:**
- Multi-language query support (CQL, CQL2, OData)
- Unified query expression model
- Database-agnostic filter translation
- Spatial predicate support
- Temporal query operations
- Query complexity scoring
- Performance optimization

---

## Architecture

### Query Model

The query model consists of several key components:

#### FeatureQuery
The top-level query object that encapsulates all query parameters:

```csharp
public sealed record FeatureQuery(
    int? Limit = null,
    int? Offset = null,
    string? Cursor = null,              // Keyset pagination
    BoundingBox? Bbox = null,           // Spatial extent filter
    TemporalInterval? Temporal = null,  // Temporal filter
    FeatureResultType ResultType = FeatureResultType.Results,
    IReadOnlyList<string>? PropertyNames = null,
    IReadOnlyList<FeatureSortOrder>? SortOrders = null,
    QueryFilter? Filter = null,         // Expression tree filter
    QueryEntityDefinition? EntityDefinition = null,
    string? Crs = null,
    TimeSpan? CommandTimeout = null,
    string? HavingClause = null);
```

#### QueryFilter
Encapsulates a filter expression tree:

```csharp
public sealed record QueryFilter(QueryExpression? Expression);
```

#### QueryEntityDefinition
Defines the queryable entity schema with field metadata:

```csharp
public sealed class QueryEntityDefinition
{
    public string Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, QueryFieldDefinition> Fields { get; }
}

public sealed record QueryFieldDefinition
{
    public required string Name { get; init; }
    public required QueryDataType DataType { get; init; }
    public bool Nullable { get; init; }
    public bool IsKey { get; init; }
    public bool IsGeometry { get; init; }
}
```

### Filter Expression Model

The query engine uses an abstract syntax tree (AST) to represent filter expressions:

```
QueryExpression (abstract base)
├── QueryBinaryExpression (left operator right)
│   ├── Comparison: =, !=, <, >, <=, >=
│   ├── Logical: AND, OR
│   └── Arithmetic: +, -, *, /, %
├── QueryUnaryExpression (operator operand)
│   └── NOT
├── QueryFieldReference (field name)
├── QueryConstant (literal value)
├── QueryFunctionExpression (function with arguments)
│   ├── String: contains, startswith, endswith, like
│   ├── Spatial: geo.intersects, geo.contains, geo.within
│   └── Temporal: year, month, day, hour, etc.
└── QuerySpatialExpression (spatial predicate)
    └── Predicates: Intersects, Contains, Within, etc.
```

### Query Operators

**Binary Operators:**
- Comparison: `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Like`
- Logical: `And`, `Or`
- Arithmetic: `Add`, `Subtract`, `Multiply`, `Divide`, `Modulo`

**Unary Operators:**
- `Not`

**Spatial Predicates:**
- `Intersects` - Geometries intersect
- `Contains` - First geometry contains second
- `Within` - First geometry is within second
- `Overlaps` - Geometries overlap
- `Crosses` - Geometries cross
- `Touches` - Geometries touch at boundary
- `Disjoint` - Geometries do not intersect
- `Equals` - Geometries are spatially equal
- `DWithin` - Within specified distance
- `Beyond` - Beyond specified distance

---

## Query Languages

### CQL (Common Query Language)

CQL is a text-based query language based on SQL WHERE clause syntax.

**Supported Features:**
- Comparison operators: `=`, `!=`, `<>`, `<`, `<=`, `>`, `>=`
- Logical operators: `AND`, `OR`, `NOT`
- Pattern matching: `LIKE`
- Set membership: `IN`
- Null checks: `NULL`
- Parenthesized expressions

**Example:**
```sql
population > 1000000 AND (name LIKE 'New%' OR capital = true)
```

**Parser Implementation:**
- Location: `Filter/CqlFilterParser.cs`
- Features: Recursive descent parser with tokenization
- Error Handling: Detailed syntax error messages

### CQL2 JSON

CQL2 is the JSON representation of CQL queries, supporting advanced features.

**Supported Features:**
- All CQL text features
- Spatial predicates: `s_intersects`, `s_contains`, `s_within`, `s_crosses`, etc.
- Temporal predicates: `t_after`, `t_before`, `t_during`, `t_equals`
- GeoJSON geometry literals
- Nested expressions
- Array literals for `IN` operator

**Example:**
```json
{
  "op": "and",
  "args": [
    {
      "op": ">",
      "args": [
        { "property": "population" },
        { "value": 1000000 }
      ]
    },
    {
      "op": "s_intersects",
      "args": [
        { "property": "geometry" },
        {
          "type": "Polygon",
          "coordinates": [...]
        }
      ]
    }
  ]
}
```

**Parser Implementation:**
- Location: `Filter/Cql2JsonParser.cs`
- Features: JSON document parsing with GeoJSON support
- CRS Handling: Automatic SRID resolution from filter-crs parameter

### OData

OData $filter query option for REST APIs.

**Supported Features:**
- Comparison operators: `eq`, `ne`, `gt`, `ge`, `lt`, `le`
- Logical operators: `and`, `or`, `not`
- Arithmetic operators: `add`, `sub`, `mul`, `div`, `mod`
- String functions: `contains`, `startswith`, `endswith`, `length`, `tolower`, `toupper`
- Date/Time functions: `year`, `month`, `day`, `hour`, `minute`, `second`
- Math functions: `round`, `floor`, `ceiling`
- Geospatial functions: `geo.intersects`, `geo.distance`

**Example:**
```
$filter=population gt 1000000 and contains(name, 'New')
```

**Parser Implementation:**
- Location: `Filter/ODataFilterParser.cs`
- Integration: Uses Microsoft.OData.UriParser
- Translation: Converts OData AST to QueryExpression tree

---

## Query Types

### Attribute Queries

Query features based on attribute values.

**Example - Simple Comparison:**
```csharp
// CQL
var filter = CqlFilterParser.Parse("population > 1000000", layer);

// Programmatic
var expression = new QueryBinaryExpression(
    new QueryFieldReference("population"),
    QueryBinaryOperator.GreaterThan,
    new QueryConstant(1000000)
);
var filter = new QueryFilter(expression);
```

**Example - Complex Logical Expression:**
```csharp
// CQL
var filter = CqlFilterParser.Parse(
    "population > 1000000 AND (type = 'city' OR capital = true)",
    layer
);

// CQL2 JSON
var json = @"{
  ""op"": ""and"",
  ""args"": [
    { ""op"": "">"", ""args"": [{ ""property"": ""population"" }, { ""value"": 1000000 }] },
    {
      ""op"": ""or"",
      ""args"": [
        { ""op"": ""="", ""args"": [{ ""property"": ""type"" }, { ""value"": ""city"" }] },
        { ""op"": ""="", ""args"": [{ ""property"": ""capital"" }, { ""value"": true }] }
      ]
    }
  ]
}";
var filter = Cql2JsonParser.Parse(json, layer, null);
```

**Example - IN Operator:**
```csharp
// CQL
var filter = CqlFilterParser.Parse("status IN ('active', 'pending', 'approved')", layer);

// Translates to: status = 'active' OR status = 'pending' OR status = 'approved'
```

**Example - LIKE Pattern Matching:**
```csharp
// CQL - SQL-style wildcards (% for multiple chars, _ for single char)
var filter = CqlFilterParser.Parse("name LIKE 'New%'", layer);
```

### Spatial Queries

Query features based on spatial relationships.

**Example - Bounding Box (Quick Spatial Filter):**
```csharp
var query = new FeatureQuery(
    Bbox: new BoundingBox(-122.5, 37.7, -122.3, 37.9, Crs: "EPSG:4326")
);

// Translates to: geometry && ST_MakeEnvelope(-122.5, 37.7, -122.3, 37.9, 4326)
```

**Example - Spatial Predicate (CQL2 JSON):**
```csharp
var json = @"{
  ""op"": ""s_intersects"",
  ""args"": [
    { ""property"": ""geometry"" },
    {
      ""type"": ""Point"",
      ""coordinates"": [-122.4, 37.8]
    }
  ]
}";
var filter = Cql2JsonParser.Parse(json, layer, "EPSG:4326");

// Translates to: ST_Intersects(geometry, ST_GeomFromText('POINT(-122.4 37.8)', 4326))
```

**Example - Distance Query (OData):**
```csharp
// OData
var oDataFilter = "$filter=geo.distance(location, geography'POINT(-122.4 37.8)') lt 1000";
// Filter features within 1000 meters of point

// Translates to: ST_Distance(location::geography, ST_GeomFromText(...)::geography) < 1000
```

**Example - Spatial Predicates (All Types):**
```csharp
// s_contains - Region contains points
var containsJson = @"{
  ""op"": ""s_contains"",
  ""args"": [
    { ""property"": ""region"" },
    { ""property"": ""point_geometry"" }
  ]
}";

// s_within - Points within region
var withinJson = @"{
  ""op"": ""s_within"",
  ""args"": [
    { ""property"": ""point_geometry"" },
    { ""property"": ""region"" }
  ]
}";

// s_crosses - Linear features crossing
var crossesJson = @"{
  ""op"": ""s_crosses"",
  ""args"": [
    { ""property"": ""road"" },
    { ""property"": ""river"" }
  ]
}";
```

### Temporal Queries

Query features based on temporal attributes.

**Example - Temporal Interval (FeatureQuery):**
```csharp
var query = new FeatureQuery(
    Temporal: new TemporalInterval(
        Start: DateTimeOffset.Parse("2023-01-01T00:00:00Z"),
        End: DateTimeOffset.Parse("2023-12-31T23:59:59Z")
    )
);

// Translates to: timestamp_field >= '2023-01-01' AND timestamp_field <= '2023-12-31'
```

**Example - Temporal Predicates (CQL2 JSON):**
```csharp
// t_after - After a specific time
var afterJson = @"{
  ""op"": ""t_after"",
  ""args"": [
    { ""property"": ""created_date"" },
    { ""value"": ""2023-01-01T00:00:00Z"" }
  ]
}";

// t_before - Before a specific time
var beforeJson = @"{
  ""op"": ""t_before"",
  ""args"": [
    { ""property"": ""created_date"" },
    { ""value"": ""2023-12-31T23:59:59Z"" }
  ]
}";

// t_during - Within a time interval
var duringJson = @"{
  ""op"": ""t_during"",
  ""args"": [
    { ""property"": ""created_date"" },
    { ""interval"": [""2023-01-01T00:00:00Z"", ""2023-12-31T23:59:59Z""] }
  ]
}";

var filter = Cql2JsonParser.Parse(duringJson, layer, null);
// Translates to: created_date >= '2023-01-01' AND created_date <= '2023-12-31'
```

**Example - Date Functions (OData):**
```csharp
// Extract year from date field
var oDataFilter = "$filter=year(created_date) eq 2023";

// Multiple temporal conditions
var oDataFilter2 = "$filter=year(created_date) eq 2023 and month(created_date) ge 6";
```

### Combined Queries

Combine multiple query types for complex filtering.

**Example - Spatial + Attribute + Temporal:**
```csharp
var json = @"{
  ""op"": ""and"",
  ""args"": [
    {
      ""op"": ""s_intersects"",
      ""args"": [
        { ""property"": ""geometry"" },
        {
          ""type"": ""Polygon"",
          ""coordinates"": [[[-122.5, 37.7], [-122.3, 37.7], [-122.3, 37.9], [-122.5, 37.9], [-122.5, 37.7]]]
        }
      ]
    },
    {
      ""op"": "">"",
      ""args"": [
        { ""property"": ""population"" },
        { ""value"": 100000 }
      ]
    },
    {
      ""op"": ""t_after"",
      ""args"": [
        { ""property"": ""last_updated"" },
        { ""value"": ""2023-01-01T00:00:00Z"" }
      ]
    }
  ]
}";

var filter = Cql2JsonParser.Parse(json, layer, "EPSG:4326");

var query = new FeatureQuery(
    Filter: filter,
    SortOrders: new[] { new FeatureSortOrder("population", FeatureSortDirection.Descending) },
    Limit: 100
);
```

---

## SQL Generation

The query engine translates filter expressions to SQL WHERE clauses using the `SqlFilterTranslator` class.

### Translation Architecture

```csharp
public sealed class SqlFilterTranslator
{
    public SqlFilterTranslator(
        QueryEntityDefinition entity,
        IDictionary<string, object?> parameters,
        Func<string, string> quoteIdentifier,
        string parameterPrefix = "filter",
        Func<QueryFunctionExpression, string, string?>? functionTranslator = null,
        Func<QuerySpatialExpression, string, string?>? spatialTranslator = null)

    public string? Translate(QueryFilter? filter, string alias)
}
```

### Translation Process

1. **Expression Tree Traversal**: Recursively visits each node in the expression tree
2. **Type-Specific Translation**: Delegates to specialized methods based on expression type
3. **Parameterization**: Converts literals to SQL parameters for security and performance
4. **Database-Specific Handling**: Uses provider-specific functions for spatial operations

### Example Translation

**CQL Input:**
```sql
population > 1000000 AND name LIKE 'New%'
```

**Generated SQL (PostgreSQL):**
```sql
WHERE (t."population" > @filter_0) AND (t."name" LIKE @filter_1)
-- Parameters: filter_0=1000000, filter_1='New%'
```

**CQL2 Spatial Input:**
```json
{
  "op": "s_intersects",
  "args": [
    { "property": "geometry" },
    { "type": "Point", "coordinates": [-122.4, 37.8] }
  ]
}
```

**Generated SQL (PostgreSQL with optimization):**
```sql
WHERE (t."geometry" && ST_MakeEnvelope(-122.5, 37.7, -122.3, 37.9, 4326))
  AND ST_Intersects(t."geometry", ST_GeomFromText(@filter_0, 4326))
-- Parameters: filter_0='POINT(-122.4 37.8)'
-- Note: && is PostGIS bounding box operator for fast spatial index usage
```

### Database-Specific Translation

Each database provider implements custom function and spatial translators:

**PostgreSQL:**
```csharp
// PostgresFeatureQueryBuilder.cs
private string? TranslateFunction(QueryFunctionExpression expression, string alias, FeatureQuery query, IDictionary<string, object?> parameters)
{
    var functionName = expression.Name.ToLowerInvariant();

    return functionName switch
    {
        "geo.intersects" => $"ST_Intersects({field}, ST_GeomFromText({param}, {srid}))",
        "geo.distance" => $"ST_Distance({field}::geography, {geom}::geography)",
        "geo.contains" => $"ST_Contains({field}, {geom})",
        // ... other spatial functions
    };
}
```

**SQL Server:**
```csharp
// SqlServerFeatureQueryBuilder.cs
"geo.intersects" => $"{field}.STIntersects({geom}) = 1",
"geo.distance" => $"{field}.STDistance({geom})",
"geo.contains" => $"{field}.STContains({geom}) = 1",
```

**MySQL:**
```csharp
// MySqlFeatureQueryBuilder.cs
"geo.intersects" => $"ST_Intersects({field}, {geom})",
"geo.distance" => $"ST_Distance({field}, {geom})",
"geo.contains" => $"ST_Contains({field}, {geom})",
```

---

## Performance Optimization

### Query Complexity Scoring

The `FilterComplexityScorer` calculates a complexity score to identify expensive queries:

```csharp
public static int CalculateComplexity(QueryFilter? filter)
```

**Scoring Rules:**
- Depth penalty: 5 points per nesting level
- OR operators: 3 points each (expensive due to inability to use indexes efficiently)
- AND operators: 1 point each
- NOT operators: 2 points each
- Comparisons: 1 point each
- Spatial predicates: 10 points each (reserved for future use)
- Functions: 2 points each

**Example:**
```csharp
// Simple query: population > 1000000
// Score: 1 (comparison)

// Complex query: (a > 1 OR b < 2) AND (c = 3 OR d = 4)
// Score: 1 (depth=1 for AND) + 5 (depth penalty) + 1 (AND)
//      + 5 (depth penalty for first OR) + 3 (OR) + 1 (a>1) + 1 (b<2)
//      + 5 (depth penalty for second OR) + 3 (OR) + 1 (c=3) + 1 (d=4)
// Score: 27
```

**Usage:**
```csharp
var complexity = FilterComplexityScorer.CalculateComplexity(filter);
if (complexity > maxComplexity)
{
    throw new InvalidOperationException($"Query complexity ({complexity}) exceeds maximum ({maxComplexity})");
}
```

### Spatial Query Optimization

**Bounding Box Pre-Filter:**
PostgreSQL queries use bounding box operator (`&&`) before spatial predicate for index optimization:

```sql
-- Unoptimized
WHERE ST_Intersects(geometry, ST_GeomFromText(...))

-- Optimized (uses GIST index)
WHERE geometry && ST_MakeEnvelope(...)
  AND ST_Intersects(geometry, ST_GeomFromText(...))
```

**Spatial Index Hints:**
The query builder automatically adds bounding box pre-filters for `intersects` predicates when a geometry literal is provided.

### Pagination Optimization

**Keyset Pagination (O(1) performance):**
```csharp
var query = new FeatureQuery(
    Cursor: "eyJpZCI6MTIzNDUsInBvcHVsYXRpb24iOjUwMDAwMH0=", // Base64 encoded JSON
    Limit: 100,
    SortOrders: new[] {
        new FeatureSortOrder("population", FeatureSortDirection.Descending),
        new FeatureSortOrder("id", FeatureSortDirection.Ascending)
    }
);

// Translates to: WHERE (population < 500000) OR (population = 500000 AND id > 12345)
//                ORDER BY population DESC, id ASC
//                LIMIT 100
```

**Offset Pagination (O(n) performance - deprecated):**
```csharp
var query = new FeatureQuery(
    Offset: 1000,  // Skip first 1000 rows - slow for large offsets
    Limit: 100
);

// Translates to: OFFSET 1000 LIMIT 100
```

### Index Utilization

**Recommended Indexes:**
```sql
-- Spatial index (GIST for PostgreSQL)
CREATE INDEX idx_features_geometry ON features USING GIST(geometry);

-- Attribute indexes for common filters
CREATE INDEX idx_features_population ON features(population);
CREATE INDEX idx_features_created_date ON features(created_date);

-- Composite indexes for common sort orders
CREATE INDEX idx_features_pop_id ON features(population DESC, id ASC);
```

### Query Hints

**Command Timeout Override:**
```csharp
// For slow analytical queries
var query = new FeatureQuery(
    Filter: complexFilter,
    CommandTimeout: TimeSpan.FromMinutes(5)  // Override default timeout
);
```

---

## Usage Examples

### Example 1: Building Queries Programmatically

```csharp
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;

// Create a simple comparison: population > 1000000
var populationFilter = new QueryBinaryExpression(
    new QueryFieldReference("population"),
    QueryBinaryOperator.GreaterThan,
    new QueryConstant(1000000)
);

// Create a string pattern match: name LIKE 'New%'
var nameFilter = new QueryFunctionExpression(
    "like",
    new QueryExpression[]
    {
        new QueryFieldReference("name"),
        new QueryConstant("New%")
    }
);

// Combine with AND
var combinedExpression = new QueryBinaryExpression(
    populationFilter,
    QueryBinaryOperator.And,
    nameFilter
);

var filter = new QueryFilter(combinedExpression);

// Create complete query
var query = new FeatureQuery(
    Filter: filter,
    Limit: 100,
    SortOrders: new[] { new FeatureSortOrder("population", FeatureSortDirection.Descending) }
);
```

### Example 2: Parsing CQL Expressions

```csharp
using Honua.Server.Core.Query.Filter;

// Simple CQL
var filter1 = CqlFilterParser.Parse("status = 'active'", layer);

// CQL with logical operators
var filter2 = CqlFilterParser.Parse(
    "population > 1000000 AND (type = 'city' OR capital = true)",
    layer
);

// CQL with IN operator
var filter3 = CqlFilterParser.Parse(
    "status IN ('active', 'pending', 'approved')",
    layer
);

// CQL with LIKE pattern
var filter4 = CqlFilterParser.Parse(
    "name LIKE 'New%' AND NOT (status = 'deleted')",
    layer
);

// CQL with NULL check
var filter5 = CqlFilterParser.Parse(
    "description = NULL OR description = ''",
    layer
);
```

### Example 3: Parsing CQL2 JSON

```csharp
using Honua.Server.Core.Query.Filter;

var cql2Json = @"{
  ""op"": ""and"",
  ""args"": [
    {
      ""op"": "">"",
      ""args"": [
        { ""property"": ""population"" },
        { ""value"": 1000000 }
      ]
    },
    {
      ""op"": ""s_intersects"",
      ""args"": [
        { ""property"": ""geometry"" },
        {
          ""type"": ""Polygon"",
          ""coordinates"": [
            [
              [-122.5, 37.7],
              [-122.3, 37.7],
              [-122.3, 37.9],
              [-122.5, 37.9],
              [-122.5, 37.7]
            ]
          ]
        }
      ]
    },
    {
      ""op"": ""t_during"",
      ""args"": [
        { ""property"": ""created_date"" },
        { ""interval"": [""2023-01-01T00:00:00Z"", ""2023-12-31T23:59:59Z""] }
      ]
    }
  ]
}";

var filter = Cql2JsonParser.Parse(cql2Json, layer, filterCrs: "EPSG:4326");

var query = new FeatureQuery(
    Filter: filter,
    Limit: 100
);
```

### Example 4: OData Filter Translation

```csharp
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Filter;
using Microsoft.OData.UriParser;

// Build OData model and parse filter
var entityDefinition = queryModelBuilder.Build(snapshot, service, layer);
var odataFilter = "$filter=population gt 1000000 and contains(name, 'New')";

// Parse using OData parser (typically done by OData middleware)
var filterClause = ParseODataFilter(odataFilter, entityDefinition);

// Translate to QueryFilter
var parser = new ODataFilterParser(entityDefinition);
var queryFilter = parser.Parse(filterClause);

var query = new FeatureQuery(
    Filter: queryFilter,
    EntityDefinition: entityDefinition
);
```

### Example 5: Executing Queries

```csharp
using Honua.Server.Core.Data;

// Build query
var filter = CqlFilterParser.Parse("population > 1000000", layer);
var query = new FeatureQuery(
    Filter: filter,
    Bbox: new BoundingBox(-122.5, 37.7, -122.3, 37.9, Crs: "EPSG:4326"),
    Limit: 100,
    SortOrders: new[] { new FeatureSortOrder("population", FeatureSortDirection.Descending) }
);

// Execute via data store provider
var dataStore = dataStoreFactory.GetProvider(dataSource.Provider);

// Stream results
await foreach (var feature in dataStore.QueryAsync(dataSource, service, layer, query, cancellationToken))
{
    // Process feature
    var id = feature.Attributes[layer.IdField];
    var population = feature.Attributes["population"];
    Console.WriteLine($"Feature {id}: population={population}");
}

// Get count
var count = await dataStore.CountAsync(dataSource, service, layer, query, cancellationToken);
Console.WriteLine($"Total matching features: {count}");
```

### Example 6: Pagination and Sorting

```csharp
// Page 1: Initial query
var query1 = new FeatureQuery(
    Limit: 100,
    SortOrders: new[] {
        new FeatureSortOrder("created_date", FeatureSortDirection.Descending),
        new FeatureSortOrder("id", FeatureSortDirection.Ascending)
    }
);

var results = await dataStore.QueryAsync(dataSource, service, layer, query1, cancellationToken)
    .ToListAsync(cancellationToken);

// Get cursor for next page from last result
var lastFeature = results.Last();
var cursor = KeysetPaginationHelper.EncodeCursor(
    lastFeature.Attributes,
    new[] { "created_date", "id" }
);

// Page 2: Use cursor
var query2 = new FeatureQuery(
    Cursor: cursor,
    Limit: 100,
    SortOrders: query1.SortOrders
);

var nextResults = await dataStore.QueryAsync(dataSource, service, layer, query2, cancellationToken)
    .ToListAsync(cancellationToken);
```

### Example 7: Complex Multi-Criteria Query

```csharp
// Combine CQL2 filter with bounding box, temporal, and pagination
var cql2Filter = @"{
  ""op"": ""and"",
  ""args"": [
    { ""op"": ""in"", ""args"": [
        { ""property"": ""status"" },
        [""active"", ""pending"", ""approved""]
      ]
    },
    { ""op"": "">"", ""args"": [
        { ""property"": ""priority"" },
        { ""value"": 5 }
      ]
    },
    { ""op"": ""like"", ""args"": [
        { ""property"": ""description"" },
        { ""value"": ""%urgent%"" }
      ]
    }
  ]
}";

var filter = Cql2JsonParser.Parse(cql2Filter, layer, "EPSG:4326");

var query = new FeatureQuery(
    Filter: filter,
    Bbox: new BoundingBox(-122.5, 37.7, -122.3, 37.9, Crs: "EPSG:4326"),
    Temporal: new TemporalInterval(
        Start: DateTimeOffset.UtcNow.AddDays(-30),
        End: DateTimeOffset.UtcNow
    ),
    PropertyNames: new[] { "id", "name", "status", "priority", "created_date" },
    SortOrders: new[] {
        new FeatureSortOrder("priority", FeatureSortDirection.Descending),
        new FeatureSortOrder("created_date", FeatureSortDirection.Descending)
    },
    Limit: 50
);

// Check complexity before executing
var complexity = FilterComplexityScorer.CalculateComplexity(filter);
if (complexity > 100)
{
    throw new InvalidOperationException($"Query too complex: {complexity}");
}

// Execute
var features = await dataStore.QueryAsync(dataSource, service, layer, query, cancellationToken)
    .ToListAsync(cancellationToken);
```

---

## Extension Points

### Adding New Query Language Support

To add support for a new query language:

1. **Create a Parser Class:**
   ```csharp
   public static class MyQueryLanguageParser
   {
       public static QueryFilter Parse(string queryText, LayerDefinition layer)
       {
           // Parse query text
           // Build QueryExpression tree
           // Return QueryFilter
       }
   }
   ```

2. **Implement Expression Translation:**
   ```csharp
   private static QueryExpression ParseExpression(/* parser state */)
   {
       // Return QueryBinaryExpression, QueryUnaryExpression, etc.
   }
   ```

3. **Register in API Endpoint:**
   ```csharp
   var filter = queryLanguage switch
   {
       "cql-text" => CqlFilterParser.Parse(filterText, layer),
       "cql2-json" => Cql2JsonParser.Parse(filterJson, layer, filterCrs),
       "my-language" => MyQueryLanguageParser.Parse(queryText, layer),
       _ => throw new NotSupportedException($"Query language '{queryLanguage}' not supported")
   };
   ```

### Adding Custom Functions

To add custom function support:

1. **Define Function Expression:**
   ```csharp
   var customFunction = new QueryFunctionExpression(
       "custom_distance",
       new QueryExpression[]
       {
           new QueryFieldReference("location"),
           new QueryConstant(targetPoint)
       }
   );
   ```

2. **Implement Database-Specific Translation:**
   ```csharp
   // In PostgresFeatureQueryBuilder.cs
   private string? TranslateFunction(QueryFunctionExpression expression, string alias, FeatureQuery query, IDictionary<string, object?> parameters)
   {
       if (expression.Name == "custom_distance")
       {
           // Generate database-specific SQL
           return $"calculate_distance({alias}.{field}, {param})";
       }
       // ... existing function handling
   }
   ```

3. **Add to CQL/OData Parsers (Optional):**
   ```csharp
   // In CQL parser
   "custom_distance" => new QueryFunctionExpression("custom_distance", arguments)
   ```

### Adding Custom Spatial Predicates

To add a new spatial predicate:

1. **Add to SpatialPredicate Enum:**
   ```csharp
   public enum SpatialPredicate
   {
       // ... existing predicates
       CustomSpatial
   }
   ```

2. **Implement Translation in Query Builders:**
   ```csharp
   // PostgreSQL
   SpatialPredicate.CustomSpatial => "ST_CustomSpatial",

   // SQL Server
   SpatialPredicate.CustomSpatial => "geometry.STCustomSpatial",

   // MySQL
   SpatialPredicate.CustomSpatial => "ST_CustomSpatial"
   ```

3. **Add to CQL2 Parser:**
   ```csharp
   "s_custom_spatial" => BuildSpatialFunction("geo.custom_spatial", arguments, layer, filterCrs)
   ```

### Custom Query Optimization

Implement custom optimization logic:

```csharp
public static class QueryOptimizationHelper
{
    public static QueryFilter OptimizeFilter(QueryFilter filter)
    {
        if (filter?.Expression == null)
            return filter;

        // Apply optimizations
        var optimized = SimplifyLogicalExpressions(filter.Expression);
        optimized = PushDownNegations(optimized);
        optimized = MergeAdjacentOperators(optimized);

        return new QueryFilter(optimized);
    }

    private static QueryExpression SimplifyLogicalExpressions(QueryExpression expr)
    {
        // Example: Convert (NOT (a AND b)) to ((NOT a) OR (NOT b))
        // Apply De Morgan's laws, remove double negations, etc.
    }
}
```

### Custom Complexity Scoring

Extend complexity scoring for custom scenarios:

```csharp
public static class CustomComplexityScorer
{
    public static int CalculateWithCustomRules(QueryFilter filter, CustomScoringRules rules)
    {
        var baseScore = FilterComplexityScorer.CalculateComplexity(filter);

        // Add custom scoring logic
        if (HasDeepNesting(filter, rules.MaxDepth))
            baseScore += rules.DeepNestingPenalty;

        if (ContainsSpatialOperations(filter))
            baseScore += rules.SpatialOperationCost;

        return baseScore;
    }
}
```

---

## Related Modules

- **Data**: Database providers that execute queries (`/src/Honua.Server.Core/Data/`)
- **Metadata**: Layer and service definitions (`/src/Honua.Server.Core/Metadata/`)
- **OGC Features API**: OGC API Features endpoint handlers
- **WFS**: Web Feature Service endpoint handlers
- **GeoServices**: ArcGIS REST API endpoint handlers

---

## Best Practices

1. **Always Use Parameterized Queries**
   - The query engine automatically parameterizes all literals
   - Never concatenate user input directly into SQL

2. **Validate Query Complexity**
   - Use `FilterComplexityScorer` to reject overly complex queries
   - Set appropriate complexity limits based on your performance requirements

3. **Prefer Bounding Box + Filter Over Complex Spatial Queries**
   ```csharp
   // Good: Fast spatial index lookup + precise filter
   var query = new FeatureQuery(
       Bbox: roughBounds,
       Filter: preciseIntersectFilter
   );

   // Avoid: Complex spatial query without bounding box
   var query = new FeatureQuery(
       Filter: complexSpatialFilterOnly  // May scan entire table
   );
   ```

4. **Use Keyset Pagination for Large Result Sets**
   - Avoid offset-based pagination for large offsets
   - Always include a unique field (like ID) in sort orders for stable cursors

5. **Index Common Query Fields**
   - Create indexes on frequently filtered and sorted fields
   - Use GIST indexes for spatial columns in PostgreSQL

6. **Consider Query Caching**
   - Cache frequently executed queries
   - Use query fingerprinting based on filter expressions

7. **Test Query Performance**
   - Use `EXPLAIN ANALYZE` to verify index usage
   - Monitor query execution times in production
   - Set appropriate command timeouts

---

## Testing

Query module test coverage includes:

- **Parser Tests**: CQL, CQL2 JSON, OData syntax parsing
- **Translation Tests**: Expression tree to SQL conversion
- **Complexity Tests**: Scoring algorithm validation
- **Integration Tests**: End-to-end query execution
- **Performance Tests**: Large result set handling

Example test location: `/tests/Honua.Server.Tests/Query/`

---

## Troubleshooting

### Common Issues

**Issue: "Field 'X' is not defined for layer 'Y'"**
- Ensure the field exists in the layer definition
- Check field name case sensitivity (usually case-insensitive)
- Verify field is included in layer metadata

**Issue: "Query complexity exceeds maximum"**
- Simplify logical expressions (reduce nesting)
- Split complex queries into multiple simpler queries
- Reduce use of OR operators (prefer IN operator)

**Issue: "Spatial query too slow"**
- Ensure spatial index exists (`CREATE INDEX ... USING GIST`)
- Add bounding box pre-filter to narrow spatial search
- Consider simplifying geometries for queries

**Issue: "Invalid geometry literal"**
- Ensure WKT is valid (check parentheses, coordinates)
- Verify SRID is supported by the database
- Check coordinate order (X=longitude, Y=latitude for EPSG:4326)

**Issue: "Temporal filter not working"**
- Ensure temporal field is DateTimeOffset type
- Check ISO 8601 date format in queries
- Verify timezone handling (queries use UTC)

### Debug SQL Generation

To see generated SQL:

```csharp
var queryBuilder = new PostgresFeatureQueryBuilder(service, layer, storageSrid, targetSrid);
var queryDef = queryBuilder.BuildSelect(query);

Console.WriteLine("SQL: " + queryDef.Sql);
foreach (var param in queryDef.Parameters)
{
    Console.WriteLine($"Parameter {param.Key}: {param.Value}");
}
```

---

## Performance Metrics

Typical query performance characteristics:

- **Simple attribute filter**: < 10ms
- **Spatial bounding box**: < 50ms (with GIST index)
- **Spatial predicate (intersects)**: 50-500ms (depends on geometry complexity)
- **Complex combined query**: 100-1000ms (depends on complexity and indexes)
- **Keyset pagination**: O(1) - constant time regardless of page number
- **Offset pagination**: O(n) - linear time based on offset

---

## Version Compatibility

- **CQL**: OGC Filter Encoding 1.1.0 / 2.0
- **CQL2**: OGC CQL2 (Draft)
- **OData**: OData v4 $filter syntax
- **Spatial Predicates**: OGC Simple Features Specification

---

## Additional Resources

- [OGC Filter Encoding](https://www.ogc.org/standards/filter)
- [OGC CQL2 Specification](https://portal.ogc.org/files/?artifact_id=96288)
- [OData Query Options](https://www.odata.org/documentation/)
- [PostGIS Spatial Functions](https://postgis.net/docs/reference.html)
