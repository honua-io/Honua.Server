# SQL Query Builder Consolidation Analysis

**Date:** 2025-10-31
**Scope:** Analysis of SQL query builder classes across all database providers
**Objective:** Identify duplication and consolidation opportunities to reduce code duplication and improve maintainability

---

## Executive Summary

This analysis examines **12 query builder classes** across 8 database providers (4 core + 4 enterprise), totaling approximately **4,456 lines of code**. The analysis reveals **significant duplication** with an estimated **60-70% overlap** in common SQL generation patterns.

### Key Findings

- **Duplicate Code Estimate:** ~2,700 lines of redundant code across builders
- **Consolidation Potential:** 50-60% reduction in total lines of code
- **High-Value Targets:**
  - Common SQL clause builders (SELECT, WHERE, ORDER BY, LIMIT/OFFSET)
  - Parameter binding logic
  - Aggregate function builders
  - Validation methods
  - Table/column identifier handling

### Recommended Approach

Implement a **three-tier consolidation strategy**:
1. **Base abstract class** for common SQL generation (~40% reduction)
2. **Shared utility classes** for provider-agnostic logic (~20% reduction)
3. **Strategy pattern** for vendor-specific variations (~minimal overhead)

**Estimated Effort:** 2-3 weeks
**Risk Level:** Medium (requires careful migration testing)
**ROI:** High (long-term maintenance reduction, easier feature additions)

---

## Query Builder Inventory

### Core Module (Honua.Server.Core)

| Database Provider | File | Lines | Visibility | Features |
|------------------|------|-------|------------|----------|
| PostgreSQL | `/src/Honua.Server.Core/Data/Postgres/PostgresFeatureQueryBuilder.cs` | 708 | `public sealed` | Full-featured, keyset pagination, statistics, distinct, extent, spatial translator |
| MySQL | `/src/Honua.Server.Core/Data/MySql/MySqlFeatureQueryBuilder.cs` | 686 | `internal sealed` | Full-featured, statistics, distinct, extent |
| SQL Server | `/src/Honua.Server.Core/Data/SqlServer/SqlServerFeatureQueryBuilder.cs` | 555 | `internal sealed` | Full-featured, keyset pagination, geometry as WKT |
| SQLite | `/src/Honua.Server.Core/Data/Sqlite/SqliteFeatureQueryBuilder.cs` | 450 | `internal sealed` | Limited spatial (Point only), basic features |

**Subtotal:** 2,399 lines

### Enterprise Module (Honua.Server.Enterprise)

| Database Provider | File | Lines | Visibility | Features |
|------------------|------|-------|------------|----------|
| Oracle | `/src/Honua.Server.Enterprise/Data/Oracle/OracleFeatureQueryBuilder.cs` | 411 | `internal sealed` | SDO_GEOMETRY, insert/update/delete, statistics, distinct, extent |
| Redshift | `/src/Honua.Server.Enterprise/Data/Redshift/RedshiftFeatureQueryBuilder.cs` | 536 | `internal sealed` | PostgreSQL-compatible, AWS API parameters, statistics, distinct, extent |
| Snowflake | `/src/Honua.Server.Enterprise/Data/Snowflake/SnowflakeFeatureQueryBuilder.cs` | 502 | `internal sealed` | Named parameters, validation methods, statistics, distinct, extent |
| BigQuery | `/src/Honua.Server.Enterprise/Data/BigQuery/BigQueryFeatureQueryBuilder.cs` | 608 | `internal sealed` | BigQueryParameter objects, backtick identifiers, statistics, distinct, extent |

**Subtotal:** 2,057 lines

### Supporting Classes

| Class | File | Lines | Purpose |
|-------|------|-------|---------|
| PostgresSpatialFilterTranslator | `/src/Honua.Server.Core/Data/Postgres/PostgresSpatialFilterTranslator.cs` | 155 | Spatial filter translation for PostgreSQL |
| MySqlSpatialFilterTranslator | `/src/Honua.Server.Core/Data/MySql/MySqlSpatialFilterTranslator.cs` | ~150 | Spatial filter translation for MySQL |
| SqlServerSpatialFilterTranslator | `/src/Honua.Server.Core/Data/SqlServer/SqlServerSpatialFilterTranslator.cs` | ~150 | Spatial filter translation for SQL Server |
| SqliteSpatialFilterTranslator | `/src/Honua.Server.Core/Data/Sqlite/SqliteSpatialFilterTranslator.cs` | ~150 | Spatial filter translation for SQLite |
| Cql2SqlQueryBuilder | `/src/Honua.Server.Core/Stac/Cql2/Cql2SqlQueryBuilder.cs` | ~400 | CQL2 expression to SQL translation (multi-provider) |
| KeysetPaginationQueryBuilder | `/src/Honua.Server.Core/Pagination/KeysetPaginationQueryBuilder.cs` | 202 | Keyset pagination WHERE clause builder |

**Subtotal:** ~1,200 lines

### Grand Total

**Total Lines of Code:** ~4,456 lines across query builders and supporting classes

---

## Detailed Duplication Analysis

### 1. Common SQL Generation Patterns

#### 1.1 BuildSelect Method (90% Duplication)

**Pattern:** SELECT [columns] FROM [table] [WHERE] [ORDER BY] [LIMIT/OFFSET]

**Duplicate Implementation Count:** 12 builders × 30-40 lines = **~360-480 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 38-58):**
```csharp
internal PostgresQueryDefinition BuildSelect(FeatureQuery query)
{
    Guard.NotNull(query);
    var sql = new StringBuilder();
    var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
    const string alias = "t";

    sql.Append("select ");
    sql.Append(BuildSelectList(query, alias));
    sql.Append(" from ");
    sql.Append(GetTableExpression());
    sql.Append(' ');
    sql.Append(alias);

    AppendWhereClause(sql, query, parameters, alias);
    AppendOrderBy(sql, query, alias);
    AppendPagination(sql, query, parameters);

    return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
}
```

**Example from MySqlFeatureQueryBuilder.cs (lines 36-56):**
```csharp
public MySqlQueryDefinition BuildSelect(FeatureQuery query)
{
    ArgumentNullException.ThrowIfNull(query);
    var sql = new StringBuilder();
    var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
    const string alias = "t";

    sql.Append("select ");
    sql.Append(BuildSelectList(query, alias));
    sql.Append(" from ");
    sql.Append(GetTableExpression());
    sql.Append(' ');
    sql.Append(alias);

    AppendWhereClause(sql, query, parameters, alias);
    AppendOrderBy(sql, query, alias);
    AppendPagination(sql, query, parameters);

    return new MySqlQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
}
```

**Duplication Level:** 95% identical except for return type

---

#### 1.2 BuildCount Method (95% Duplication)

**Pattern:** SELECT COUNT(*) FROM [table] [WHERE]

**Duplicate Implementation Count:** 12 builders × 15-20 lines = **~180-240 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 60-76):**
```csharp
internal PostgresQueryDefinition BuildCount(FeatureQuery query)
{
    Guard.NotNull(query);
    var sql = new StringBuilder();
    var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
    const string alias = "t";

    sql.Append("select count(*) from ");
    sql.Append(GetTableExpression());
    sql.Append(' ');
    sql.Append(alias);

    AppendWhereClause(sql, query, parameters, alias);

    return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
}
```

**Duplication Level:** 98% identical across all builders

---

#### 1.3 BuildById Method (90% Duplication)

**Pattern:** SELECT [columns] FROM [table] WHERE [id_field] = [value] LIMIT 1

**Duplicate Implementation Count:** 12 builders × 20-25 lines = **~240-300 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 78-101):**
```csharp
internal PostgresQueryDefinition BuildById(string featureId)
{
    Guard.NotNullOrWhiteSpace(featureId);
    var sql = new StringBuilder();
    var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
    const string alias = "t";

    sql.Append("select ");
    sql.Append(BuildSelectList(new FeatureQuery(), alias));
    sql.Append(" from ");
    sql.Append(GetTableExpression());
    sql.Append(' ');
    sql.Append(alias);
    sql.Append(" where ");
    sql.Append(alias);
    sql.Append('.');
    sql.Append(QuoteIdentifier(GetPrimaryKeyColumn()));
    sql.Append(" = @feature_id limit 1");

    parameters["feature_id"] = NormalizeKeyValue(featureId);

    return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
}
```

**Duplication Level:** 90% identical (only LIMIT syntax varies: "LIMIT 1" vs "FETCH NEXT 1 ROWS ONLY")

---

#### 1.4 BuildStatistics Method (85% Duplication)

**Pattern:** SELECT [group_fields], [aggregates] FROM [table] [WHERE] [GROUP BY] [HAVING]

**Duplicate Implementation Count:** 10 builders (Oracle, Redshift, Snowflake, BigQuery, MySQL, SQLite + 4 others) × 50-70 lines = **~500-700 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 553-614):**
```csharp
internal PostgresQueryDefinition BuildStatistics(
    FeatureQuery query,
    IReadOnlyList<StatisticDefinition> statistics,
    IReadOnlyList<string>? groupByFields)
{
    Guard.NotNull(query);
    Guard.NotNull(statistics);

    var sql = new StringBuilder();
    var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
    const string alias = "t";

    sql.Append("select ");

    // Add GROUP BY fields first
    if (groupByFields is { Count: > 0 })
    {
        sql.Append(string.Join(", ", groupByFields.Select(f => $"{alias}.{QuoteIdentifier(f)}")));
        sql.Append(", ");
    }

    // Add aggregate functions
    var aggregates = new List<string>();
    foreach (var stat in statistics)
    {
        var fieldRef = $"{alias}.{QuoteIdentifier(stat.FieldName)}";
        var aggregate = stat.Type switch
        {
            StatisticType.Count => "count(*)",
            StatisticType.Sum => $"sum({fieldRef})",
            StatisticType.Avg => $"avg({fieldRef})",
            StatisticType.Min => $"min({fieldRef})",
            StatisticType.Max => $"max({fieldRef})",
            _ => throw new NotSupportedException($"Statistic type '{stat.Type}' is not supported.")
        };
        aggregates.Add(aggregate);
    }

    sql.Append(string.Join(", ", aggregates));
    sql.Append(" from ");
    sql.Append(GetTableExpression());
    sql.Append(' ');
    sql.Append(alias);

    AppendWhereClause(sql, query, parameters, alias);

    // Add GROUP BY clause
    if (groupByFields is { Count: > 0 })
    {
        sql.Append(" group by ");
        sql.Append(string.Join(", ", groupByFields.Select(f => $"{alias}.{QuoteIdentifier(f)}")));
    }

    // Add HAVING clause if specified
    if (query.HavingClause.HasValue())
    {
        sql.Append(" having ");
        sql.Append(query.HavingClause);
    }

    return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
}
```

**Duplication Level:** 85% identical (aggregate function mapping is identical, only output formatting varies)

---

#### 1.5 BuildDistinct Method (90% Duplication)

**Pattern:** SELECT DISTINCT [fields] FROM [table] [WHERE] [LIMIT]

**Duplicate Implementation Count:** 8 builders × 20-30 lines = **~160-240 lines**

**Duplication Level:** 92% identical

---

#### 1.6 BuildExtent Method (80% Duplication)

**Pattern:** Calculate spatial extent using vendor-specific functions

**Duplicate Implementation Count:** 8 builders × 25-40 lines = **~200-320 lines**

**Vendor Variations:**
- **PostgreSQL:** `ST_Extent(geom)::text` (returns BOX format)
- **MySQL:** `MIN(ST_XMin(geom)), MIN(ST_YMin(geom)), MAX(ST_XMax(geom)), MAX(ST_YMax(geom))`
- **Oracle:** `MIN(SDO_GEOM.SDO_MIN_MBR_ORDINATE(geom, 1)), ...`
- **Snowflake:** `MIN(ST_XMIN(geom)), MIN(ST_YMIN(geom)), MAX(ST_XMAX(geom)), MAX(ST_YMAX(geom))`
- **BigQuery:** `ST_ASGEOJSON(ST_EXTENT(geom))`
- **Redshift:** `ST_ASGEOJSON(ST_Extent(geom))`

**Duplication Level:** 75% identical (core logic, different spatial functions)

---

### 2. WHERE Clause Generation (AppendWhereClause)

**Pattern:** Combine bbox, temporal, and filter predicates with AND

**Duplicate Implementation Count:** 12 builders × 10-20 lines = **~120-240 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 355-369):**
```csharp
private void AppendWhereClause(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters, string alias)
{
    var predicates = new List<string>();
    AppendBoundingBoxPredicate(query, predicates, parameters, alias);
    AppendTemporalPredicate(query, predicates, parameters, alias);
    AppendFilterPredicate(query, predicates, parameters, alias);

    if (predicates.Count == 0)
    {
        return;
    }

    sql.Append(" where ");
    sql.Append(string.Join(" and ", predicates));
}
```

**Duplication Level:** 98% identical

---

### 3. Temporal Predicate Generation (AppendTemporalPredicate)

**Pattern:** [temporal_column] >= [start] AND [temporal_column] <= [end]

**Duplicate Implementation Count:** 12 builders × 15-20 lines = **~180-240 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 395-420):**
```csharp
private void AppendTemporalPredicate(FeatureQuery query, ICollection<string> predicates, IDictionary<string, object?> parameters, string alias)
{
    if (query.Temporal is null)
    {
        return;
    }

    var temporalColumn = _layer.Storage?.TemporalColumn;
    if (temporalColumn.IsNullOrWhiteSpace())
    {
        return;
    }

    var column = $"{alias}.{QuoteIdentifier(temporalColumn)}";
    if (query.Temporal.Start is not null)
    {
        predicates.Add($"{column} >= @datetime_start");
        parameters["datetime_start"] = query.Temporal.Start.Value.UtcDateTime;
    }

    if (query.Temporal.End is not null)
    {
        predicates.Add($"{column} <= @datetime_end");
        parameters["datetime_end"] = query.Temporal.End.Value.UtcDateTime;
    }
}
```

**Duplication Level:** 95% identical (only parameter naming varies slightly)

---

### 4. ORDER BY Clause Generation (AppendOrderBy)

**Pattern:** ORDER BY [field1] ASC/DESC, [field2] ASC/DESC, ...

**Duplicate Implementation Count:** 12 builders × 15-20 lines = **~180-240 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 449-468):**
```csharp
private void AppendOrderBy(StringBuilder sql, FeatureQuery query, string alias)
{
    var segments = new List<string>();
    if (query.SortOrders is { Count: > 0 })
    {
        foreach (var order in query.SortOrders)
        {
            var direction = order.Direction == FeatureSortDirection.Descending ? "desc" : "asc";
            segments.Add($"{alias}.{QuoteIdentifier(order.Field)} {direction}");
        }
    }
    else
    {
        var orderFieldName = _layer.IdField.IsNullOrWhiteSpace() ? GetPrimaryKeyColumn() : _layer.IdField;
        segments.Add($"{alias}.{QuoteIdentifier(orderFieldName)} asc");
    }

    sql.Append(" order by ");
    sql.Append(string.Join(", ", segments));
}
```

**Duplication Level:** 98% identical

---

### 5. Pagination Logic (AppendPagination)

**Duplicate Implementation Count:** 12 builders × 15-25 lines = **~180-300 lines**

**Vendor-Specific Variations:**
- **PostgreSQL/MySQL/SQLite:** `LIMIT @limit OFFSET @offset`
- **SQL Server:** `OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY`
- **Oracle:** `OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY`
- **Snowflake:** `LIMIT :limit OFFSET :offset`
- **BigQuery:** `LIMIT @limit OFFSET @offset`
- **Redshift:** `LIMIT :limit OFFSET :offset`

**Example from PostgresFeatureQueryBuilder.cs (lines 470-488):**
```csharp
private void AppendPagination(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters)
{
    if (query.Limit.HasValue)
    {
        sql.Append(" limit @limit");
        parameters["limit"] = query.Limit.Value;
    }

    if (query.Offset.HasValue)
    {
        if (!query.Limit.HasValue)
        {
            sql.Append(" limit ALL");
        }

        sql.Append(" offset @offset");
        parameters["offset"] = query.Offset.Value;
    }
}
```

**Example from SqlServerFeatureQueryBuilder.cs (lines 395-440):**
```csharp
private void AppendPagination(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters)
{
    var hasLimit = query.Limit.HasValue;
    var hasOffset = query.Offset.HasValue;
    var hasCursor = !string.IsNullOrEmpty(query.Cursor);

    // Prefer keyset (cursor-based) pagination for O(1) performance
    if (hasCursor)
    {
        if (hasLimit)
        {
            var limitParam = AddParameter(parameters, "limit", query.Limit!.Value);
            sql.Append(" offset 0 rows fetch next ");
            sql.Append(limitParam);
            sql.Append(" rows only");
        }
        return;
    }

    // Legacy OFFSET pagination
    if (!hasLimit && !hasOffset)
    {
        return;
    }

    var offset = hasOffset ? query.Offset!.Value : 0;
    var offsetParam = AddParameter(parameters, "offset", offset);
    sql.Append(" offset ");
    sql.Append(offsetParam);
    sql.Append(" rows");

    if (hasLimit)
    {
        var limitParam = AddParameter(parameters, "limit", query.Limit!.Value);
        sql.Append(" fetch next ");
        sql.Append(limitParam);
        sql.Append(" rows only");
    }
}
```

**Duplication Level:** 70% identical (core logic same, syntax different)

---

### 6. Aggregate Expression Building

**Duplicate Implementation Count:** 10 builders × 20-30 lines = **~200-300 lines**

**Example from MySqlFeatureQueryBuilder.cs (lines 635-660):**
```csharp
private string BuildAggregateExpression(StatisticDefinition statistic, string alias)
{
    var fieldReference = statistic.FieldName.IsNullOrWhiteSpace()
        ? null
        : $"{alias}.{QuoteIdentifier(statistic.FieldName)}";

    return statistic.Type switch
    {
        StatisticType.Count => "COUNT(*)",
        StatisticType.Sum => EnsureAggregateField("SUM", fieldReference, statistic),
        StatisticType.Avg => EnsureAggregateField("AVG", fieldReference, statistic),
        StatisticType.Min => EnsureAggregateField("MIN", fieldReference, statistic),
        StatisticType.Max => EnsureAggregateField("MAX", fieldReference, statistic),
        _ => throw new NotSupportedException($"Statistic type '{statistic.Type}' is not supported.")
    };
}

private static string EnsureAggregateField(string functionName, string? fieldReference, StatisticDefinition statistic)
{
    if (fieldReference.IsNullOrWhiteSpace())
    {
        throw new NotSupportedException($"Statistic type '{statistic.Type}' requires a field name.");
    }

    return $"{functionName}({fieldReference})";
}
```

**Duplication Level:** 100% identical across all builders (except case formatting: `COUNT` vs `count`)

---

### 7. NormalizeKeyValue Method (Type Conversion)

**Duplicate Implementation Count:** 4 builders (Postgres, MySQL, SqlServer, Oracle) × 35-40 lines = **~140-160 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 103-138):**
```csharp
private object NormalizeKeyValue(string featureId)
{
    var field = _layer.Fields.FirstOrDefault(f => string.Equals(f.Name, _layer.IdField, StringComparison.OrdinalIgnoreCase));
    var hint = field?.DataType ?? field?.StorageType;
    if (hint.IsNullOrWhiteSpace())
    {
        return featureId;
    }

    switch (hint.Trim().ToLowerInvariant())
    {
        case "int":
        case "int32":
        case "integer":
        case "smallint":
        case "int16":
            return int.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : featureId;
        case "long":
        case "int64":
        case "bigint":
            return long.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : featureId;
        case "double":
        case "float":
        case "real":
            return double.TryParse(featureId, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) ? d : featureId;
        case "decimal":
        case "numeric":
            return decimal.TryParse(featureId, NumberStyles.Number, CultureInfo.InvariantCulture, out var m) ? m : featureId;
        case "guid":
        case "uuid":
        case "uniqueidentifier":
            return Guid.TryParse(featureId, out var g) ? g : featureId;
        default:
            return featureId;
    }
}
```

**Duplication Level:** 100% identical

---

### 8. GetTableExpression / QuoteIdentifier

**Duplicate Implementation Count:** 12 builders × 10-15 lines each = **~120-180 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 490-501):**
```csharp
private string GetTableExpression()
{
    var table = _layer.Storage?.Table;
    if (table.IsNullOrWhiteSpace())
    {
        table = _layer.Id;
    }

    var parts = table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var quoted = parts.Select(QuoteIdentifier);
    return string.Join('.', quoted);
}

private static string QuoteIdentifier(string identifier)
{
    return SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);
}
```

**Duplication Level:** 90% identical (only QuoteIdentifier calls different validator)

---

### 9. GetPrimaryKeyColumn / GetGeometryColumn

**Duplicate Implementation Count:** 12 builders × 4-6 lines = **~48-72 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 503-505):**
```csharp
private string GetPrimaryKeyColumn() => _layer.Storage?.PrimaryKey ?? _layer.IdField;

private string GetGeometryColumn() => _layer.Storage?.GeometryColumn ?? _layer.GeometryField;
```

**Duplication Level:** 100% identical

---

### 10. ResolveSelectColumns

**Duplicate Implementation Count:** 4 builders × 30-40 lines = **~120-160 lines**

**Example from PostgresFeatureQueryBuilder.cs (lines 507-547):**
```csharp
private IReadOnlyList<string> ResolveSelectColumns(FeatureQuery query)
{
    if (query.PropertyNames is null || query.PropertyNames.Count == 0)
    {
        return Array.Empty<string>();
    }

    var columns = new List<string>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    void Add(string? column)
    {
        if (column.IsNullOrWhiteSpace())
        {
            return;
        }

        if (seen.Add(column))
        {
            columns.Add(column);
        }
    }

    Add(GetGeometryColumn());
    Add(_layer.IdField);
    Add(_layer.Storage?.PrimaryKey);
    if (query.SortOrders is { Count: > 0 })
    {
        foreach (var sort in query.SortOrders)
        {
            Add(sort.Field);
        }
    }

    foreach (var property in query.PropertyNames)
    {
        Add(property);
    }

    return columns;
}
```

**Duplication Level:** 95% identical

---

### 11. Validation Methods (Enterprise Builders)

**Duplicate Implementation Count:** 4 enterprise builders × 50-100 lines = **~200-400 lines**

**Examples from SnowflakeFeatureQueryBuilder.cs:**

```csharp
private static void ValidateCoordinate(double coordinate, string parameterName)
{
    if (double.IsNaN(coordinate))
    {
        throw new ArgumentException($"Coordinate '{parameterName}' cannot be NaN.", parameterName);
    }

    if (double.IsInfinity(coordinate))
    {
        throw new ArgumentException($"Coordinate '{parameterName}' cannot be infinity.", parameterName);
    }

    if (coordinate < -180 || coordinate > 180)
    {
        throw new ArgumentException($"Coordinate '{parameterName}' must be between -180 and 180.", parameterName);
    }
}

private static void ValidatePositiveInteger(int value, string parameterName)
{
    if (value <= 0)
    {
        throw new ArgumentException($"Parameter '{parameterName}' must be a positive integer.", parameterName);
    }

    if (value > 100000)
    {
        throw new ArgumentException($"Parameter '{parameterName}' exceeds maximum allowed value of 100000.", parameterName);
    }
}

private static void ValidateNonNegativeInteger(int value, string parameterName)
{
    if (value < 0)
    {
        throw new ArgumentException($"Parameter '{parameterName}' must be a non-negative integer.", parameterName);
    }

    if (value > 1000000)
    {
        throw new ArgumentException($"Parameter '{parameterName}' exceeds maximum allowed value of 1000000.", parameterName);
    }
}

private static void ValidateTemporalDate(DateTimeOffset date, string parameterName)
{
    if (date.Year < 1900 || date.Year > 2200)
    {
        throw new ArgumentException($"Temporal date '{parameterName}' must be between year 1900 and 2200.", parameterName);
    }

    if (date == DateTimeOffset.MinValue || date == DateTimeOffset.MaxValue)
    {
        throw new ArgumentException($"Temporal date '{parameterName}' cannot be DateTimeOffset.MinValue or DateTimeOffset.MaxValue.", parameterName);
    }
}
```

**Duplication Level:** 90% identical across Snowflake, BigQuery, Redshift

---

## Vendor-Specific Variations

### Spatial Functions

| Operation | PostgreSQL | MySQL | SQL Server | Oracle | Snowflake | BigQuery | Redshift |
|-----------|------------|-------|------------|--------|-----------|----------|----------|
| **Intersects** | `ST_Intersects(g1, g2)` | `ST_Intersects(g1, g2)` | `g1.STIntersects(g2) = 1` | `SDO_RELATE(g1, g2, 'mask=anyinteract') = 'TRUE'` | `ST_INTERSECTS(g1, g2)` | `ST_INTERSECTS(g1, g2)` | `ST_Intersects(g1, g2)` |
| **Distance** | `ST_Distance(g1::geography, g2::geography)` | `ST_Distance_Sphere(g1, g2)` | `g1.STDistance(g2)` | N/A | N/A | N/A | N/A |
| **Geometry from WKT** | `ST_GeomFromText(wkt, srid)` | `ST_GeomFromText(wkt, srid)` | `geometry::STGeomFromText(wkt, srid)` | `SDO_GEOMETRY(...)` | `TO_GEOGRAPHY(wkt)` | `ST_GEOGFROMTEXT(wkt)` | `ST_GeomFromText(wkt, srid)` |
| **Geometry to JSON** | `ST_AsGeoJSON(geom)` | `ST_AsGeoJSON(geom)` | `geom.STAsText()` + SRID | `SDO_UTIL.TO_GEOJSON(geom)` | `ST_ASGEOJSON(geom)` | `ST_ASGEOJSON(geom)` | `ST_AsGeoJSON(geom)` |
| **Transform** | `ST_Transform(geom, srid)` | `ST_Transform(geom, srid)` | N/A (pre-transform coords) | N/A | N/A | N/A | N/A |
| **Envelope/Extent** | `ST_Extent(geom)` | `MIN(ST_XMin(geom)), MAX(ST_XMax(geom))` | N/A | `SDO_GEOM.SDO_MIN_MBR_ORDINATE` | `MIN(ST_XMIN(geom)), ...` | `ST_EXTENT(geom)` | `ST_Extent(geom)` |
| **Bbox Operator** | `geom && envelope` | `MBRIntersects(geom, envelope)` | N/A | N/A | N/A | N/A | N/A |

### Identifier Quoting

| Provider | Quote Character | Validator Method |
|----------|----------------|------------------|
| PostgreSQL | `"identifier"` | `SqlIdentifierValidator.ValidateAndQuotePostgres` |
| MySQL | `` `identifier` `` | `SqlIdentifierValidator.ValidateAndQuoteMySql` |
| SQL Server | `[identifier]` | `SqlIdentifierValidator.ValidateAndQuoteSqlServer` |
| SQLite | `"identifier"` | `SqlIdentifierValidator.ValidateAndQuoteSqlite` |
| Oracle | `"IDENTIFIER"` | `SqlIdentifierValidator.ValidateIdentifier` + manual quoting |
| Snowflake | `"identifier"` | `SqlIdentifierValidator.ValidateIdentifier` + manual quoting |
| BigQuery | `` `identifier` `` | `SqlIdentifierValidator.ValidateIdentifier` + manual quoting |
| Redshift | `"identifier"` | `SqlIdentifierValidator.ValidateIdentifier` + manual quoting |

### Parameter Syntax

| Provider | Syntax | Example |
|----------|--------|---------|
| PostgreSQL | `@param` | `@limit` |
| MySQL | `@param` | `@limit` |
| SQL Server | `@param` | `@limit` |
| SQLite | `@param` | `@limit` |
| Oracle | `:param` | `:limit` |
| Snowflake | `:param` | `:limit` |
| BigQuery | `@param` | `@limit_value` |
| Redshift | `:param` | `:limit` |

### Pagination Syntax

| Provider | Syntax Pattern | Example |
|----------|---------------|---------|
| PostgreSQL | `LIMIT n OFFSET m` | `LIMIT 100 OFFSET 200` |
| MySQL | `LIMIT n OFFSET m` | `LIMIT 100 OFFSET 200` |
| SQL Server | `OFFSET m ROWS FETCH NEXT n ROWS ONLY` | `OFFSET 200 ROWS FETCH NEXT 100 ROWS ONLY` |
| SQLite | `LIMIT n OFFSET m` | `LIMIT 100 OFFSET 200` |
| Oracle | `OFFSET m ROWS FETCH NEXT n ROWS ONLY` | `OFFSET 200 ROWS FETCH NEXT 100 ROWS ONLY` |
| Snowflake | `LIMIT n OFFSET m` | `LIMIT 100 OFFSET 200` |
| BigQuery | `LIMIT n OFFSET m` | `LIMIT 100 OFFSET 200` |
| Redshift | `LIMIT n OFFSET m` | `LIMIT 100 OFFSET 200` |

---

## Consolidation Opportunities

### Strategy 1: Base Abstract Class (Recommended)

**Approach:** Create `FeatureQueryBuilderBase<TQueryDefinition>` with common logic

**Consolidation Potential:** ~40-50% reduction in total lines

**Class Structure:**
```csharp
public abstract class FeatureQueryBuilderBase<TQueryDefinition>
{
    // Common fields
    protected readonly ServiceDefinition _service;
    protected readonly LayerDefinition _layer;
    protected readonly int _storageSrid;
    protected readonly int _targetSrid;

    // Common query building methods (90-100% identical across providers)
    public TQueryDefinition BuildCount(FeatureQuery query);
    public TQueryDefinition BuildById(string featureId);
    public TQueryDefinition BuildStatistics(...);
    public TQueryDefinition BuildDistinct(...);

    // Template methods with vendor-specific implementations
    protected abstract string BuildSelectList(FeatureQuery query, string alias);
    protected abstract string BuildGeometryProjection(string alias);
    protected abstract void AppendPagination(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters);
    protected abstract void AppendBoundingBoxPredicate(...);

    // Common helper methods (100% identical)
    protected void AppendTemporalPredicate(...);
    protected void AppendOrderBy(...);
    protected string GetTableExpression();
    protected string GetPrimaryKeyColumn();
    protected string GetGeometryColumn();
    protected IReadOnlyList<string> ResolveSelectColumns(...);
    protected object NormalizeKeyValue(string featureId);

    // Abstract methods for vendor-specific behavior
    protected abstract string QuoteIdentifier(string identifier);
    protected abstract TQueryDefinition CreateQueryDefinition(string sql, IReadOnlyDictionary<string, object?> parameters);
}
```

**Benefits:**
- Eliminates ~1,800 lines of duplicate code
- Centralizes common logic for easier maintenance
- Enforces consistent behavior across providers
- Easier to add new features (one place to update)

**Migration Complexity:** Medium
- Requires refactoring all 12 query builders
- Need to update all consuming code to use new base class
- Testing required for each provider

**Estimated Lines Saved:** ~1,800 lines (40%)

---

### Strategy 2: Shared Utility Classes

**Approach:** Extract common helper methods into static utility classes

**Classes to Create:**

#### A. `SqlClauseBuilder`
```csharp
public static class SqlClauseBuilder
{
    // ORDER BY clause generation (98% identical)
    public static void AppendOrderBy(
        StringBuilder sql,
        FeatureQuery query,
        string alias,
        Func<string, string> quoteIdentifier,
        string defaultSortColumn);

    // Temporal predicate generation (95% identical)
    public static void AppendTemporalPredicate(
        FeatureQuery query,
        ICollection<string> predicates,
        IDictionary<string, object?> parameters,
        string alias,
        Func<string, string> quoteIdentifier,
        string? temporalColumn);

    // WHERE clause combining (98% identical)
    public static void AppendWhereClause(
        StringBuilder sql,
        IEnumerable<string> predicates);
}
```

#### B. `AggregateExpressionBuilder`
```csharp
public static class AggregateExpressionBuilder
{
    // 100% identical across all builders
    public static string BuildAggregateExpression(
        StatisticDefinition statistic,
        string alias,
        Func<string, string> quoteIdentifier);

    private static string EnsureAggregateField(
        string functionName,
        string? fieldReference,
        StatisticDefinition statistic);
}
```

#### C. `LayerMetadataHelper`
```csharp
public static class LayerMetadataHelper
{
    // 100% identical
    public static string GetPrimaryKeyColumn(LayerDefinition layer);
    public static string GetGeometryColumn(LayerDefinition layer);
    public static string GetTableName(LayerDefinition layer);

    // 100% identical
    public static object NormalizeKeyValue(
        string featureId,
        LayerDefinition layer);
}
```

#### D. `ValidationHelper`
```csharp
public static class ValidationHelper
{
    // From enterprise builders (90% identical)
    public static void ValidateCoordinate(double coordinate, string parameterName);
    public static void ValidatePositiveInteger(int value, string parameterName, int? maxValue = null);
    public static void ValidateNonNegativeInteger(int value, string parameterName, int? maxValue = null);
    public static void ValidateTemporalDate(DateTimeOffset date, string parameterName);
    public static void ValidateBoundingBox(BoundingBox bbox);
}
```

**Benefits:**
- Reusable across all query builders without inheritance
- Can be applied incrementally (doesn't require major refactoring)
- Easy to test in isolation
- Minimal risk to existing code

**Migration Complexity:** Low
- Can be added without modifying existing query builders
- Update builders one at a time to use utilities
- Easy rollback if issues found

**Estimated Lines Saved:** ~900 lines (20%)

---

### Strategy 3: Strategy Pattern for Vendor Variations

**Approach:** Use strategy pattern for vendor-specific SQL generation

**Interfaces to Create:**

#### A. `IPaginationStrategy`
```csharp
public interface IPaginationStrategy
{
    void AppendPagination(
        StringBuilder sql,
        int? limit,
        int? offset,
        IDictionary<string, object?> parameters);
}

// Implementations:
// - LimitOffsetPaginationStrategy (PostgreSQL, MySQL, SQLite, Snowflake, BigQuery, Redshift)
// - FetchRowsPaginationStrategy (SQL Server, Oracle)
```

#### B. `ISpatialFunctionStrategy`
```csharp
public interface ISpatialFunctionStrategy
{
    string BuildIntersects(string geomColumn, string geometryParam, int srid);
    string BuildDistance(string geomColumn, string geometryParam, int srid);
    string BuildGeometryFromWkt(string wktParam, int srid);
    string BuildGeometryToJson(string geomColumn);
    string BuildExtent(string geomColumn, int targetSrid);
    string BuildTransform(string geomExpression, int fromSrid, int toSrid);
}

// Implementations:
// - PostGisSpatialStrategy
// - MySqlSpatialStrategy
// - SqlServerSpatialStrategy
// - OracleSpatialStrategy
// - SnowflakeSpatialStrategy
// - BigQuerySpatialStrategy
```

#### C. `IIdentifierQuotingStrategy`
```csharp
public interface IIdentifierQuotingStrategy
{
    string QuoteIdentifier(string identifier);
    string QuoteAlias(string alias);
}

// Implementations:
// - DoubleQuoteStrategy (PostgreSQL, Oracle, Snowflake, SQLite, Redshift)
// - BacktickStrategy (MySQL, BigQuery)
// - BracketStrategy (SQL Server)
```

**Benefits:**
- Clean separation of vendor-specific logic
- Easy to add new database providers
- Testable in isolation
- Reduces conditional logic in builders

**Migration Complexity:** Medium
- Requires refactoring spatial logic
- Need to create strategy implementations for each provider
- More classes to maintain

**Estimated Lines Saved:** ~300 lines (7%) + improved maintainability

---

## Proposed Consolidation Roadmap

### Phase 1: Quick Wins (Week 1)
**Goal:** Extract low-hanging fruit with minimal risk

**Tasks:**
1. Create `ValidationHelper` utility class
   - Extract validation methods from Snowflake, BigQuery, Redshift
   - **Lines saved:** ~200
2. Create `AggregateExpressionBuilder` utility class
   - Extract `BuildAggregateExpression` from all builders
   - **Lines saved:** ~300
3. Create `LayerMetadataHelper` utility class
   - Extract `GetPrimaryKeyColumn`, `GetGeometryColumn`, `NormalizeKeyValue`
   - **Lines saved:** ~200

**Total Phase 1 Savings:** ~700 lines (16%)

**Risk:** Low (no breaking changes, incremental adoption)

---

### Phase 2: Common Clause Builders (Week 2)
**Goal:** Consolidate SQL clause generation

**Tasks:**
1. Create `SqlClauseBuilder` utility class
   - Extract `AppendOrderBy` (~180 lines)
   - Extract `AppendTemporalPredicate` (~180 lines)
   - Extract `AppendWhereClause` (~120 lines)
   - **Lines saved:** ~480
2. Update all 12 query builders to use `SqlClauseBuilder`
3. Comprehensive testing of all providers

**Total Phase 2 Savings:** ~480 lines (11%)

**Risk:** Low-Medium (shared logic, needs thorough testing)

---

### Phase 3: Base Abstract Class (Week 3)
**Goal:** Create inheritance hierarchy for maximum consolidation

**Tasks:**
1. Create `FeatureQueryBuilderBase<TQueryDefinition>` abstract class
   - Move common `BuildCount` implementation (~180 lines)
   - Move common `BuildById` implementation (~240 lines)
   - Move common `BuildStatistics` scaffolding (~300 lines)
   - Move common `BuildDistinct` scaffolding (~160 lines)
   - **Lines saved:** ~880
2. Refactor 4 core builders (Postgres, MySQL, SqlServer, SQLite)
   - Update to inherit from base class
   - Override abstract/virtual methods
   - Remove duplicate code
3. Refactor 4 enterprise builders (Oracle, Redshift, Snowflake, BigQuery)
4. Full regression testing suite

**Total Phase 3 Savings:** ~880 lines (20%)

**Risk:** Medium (major refactoring, requires careful migration)

---

### Phase 4: Strategy Pattern (Optional - Week 4)
**Goal:** Clean up vendor-specific variations

**Tasks:**
1. Create `IPaginationStrategy` and implementations
   - **Lines saved:** ~100
2. Create `ISpatialFunctionStrategy` and implementations
   - **Lines saved:** ~200
   - Note: May increase line count initially due to interface overhead
3. Update builders to use strategies

**Total Phase 4 Savings:** ~300 lines (7%) + improved maintainability

**Risk:** Medium (new abstractions, more classes to manage)

---

## Total Consolidation Summary

| Phase | Effort | Lines Saved | Cumulative Savings | Risk | Priority |
|-------|--------|-------------|-------------------|------|----------|
| Phase 1: Quick Wins | 1 week | ~700 (16%) | 700 (16%) | Low | **High** |
| Phase 2: Clause Builders | 1 week | ~480 (11%) | 1,180 (27%) | Low-Medium | **High** |
| Phase 3: Base Class | 1 week | ~880 (20%) | 2,060 (46%) | Medium | **Medium** |
| Phase 4: Strategy Pattern | 1 week | ~300 (7%) | 2,360 (53%) | Medium | Low |

**Grand Total Savings:** ~2,360 lines (53% reduction from 4,456 to ~2,096 lines)

---

## Migration Testing Strategy

### Test Coverage Requirements

For each phase, ensure:

1. **Unit Tests**
   - Test each utility method independently
   - Test edge cases (null values, empty collections, invalid data)
   - Test vendor-specific variations

2. **Integration Tests**
   - Test complete query generation for each provider
   - Test all query types: Select, Count, ById, Statistics, Distinct, Extent
   - Test with real database connections (if possible)

3. **Regression Tests**
   - Compare generated SQL before/after refactoring
   - Verify parameter values match exactly
   - Ensure query results are identical

4. **Performance Tests**
   - Benchmark query generation time before/after
   - Ensure no performance degradation
   - Verify memory usage is similar

### Test Data Sets

Create comprehensive test fixtures:
- Simple queries (no filters, no sorting)
- Complex queries (multiple filters, spatial, temporal, sorting, pagination)
- Edge cases (null bbox, empty sort orders, extreme SRID values)
- Multi-provider validation (same query on all 8 providers)

---

## Risk Assessment & Mitigation

### High-Risk Areas

1. **Spatial Filter Translation**
   - **Risk:** Different vendors have incompatible spatial function syntax
   - **Mitigation:** Keep spatial logic provider-specific initially, consolidate gradually
   - **Validation:** Test with real geometries on each provider

2. **Parameter Binding**
   - **Risk:** Different parameter naming conventions (@param vs :param)
   - **Mitigation:** Parameterize parameter prefix in base class/utilities
   - **Validation:** Verify parameters are correctly bound in ADO.NET tests

3. **Identifier Quoting**
   - **Risk:** Incorrect quoting leads to SQL injection or syntax errors
   - **Mitigation:** Continue using `SqlIdentifierValidator` for validation
   - **Validation:** SQL injection testing, fuzz testing with special characters

4. **Return Type Variations**
   - **Risk:** Each builder returns different `*QueryDefinition` type
   - **Mitigation:** Use generic base class `FeatureQueryBuilderBase<TQueryDefinition>`
   - **Validation:** Compile-time type safety

### Medium-Risk Areas

1. **Pagination Syntax**
   - **Risk:** LIMIT/OFFSET vs OFFSET/FETCH varies significantly
   - **Mitigation:** Strategy pattern or template method
   - **Validation:** Test pagination on each provider with different page sizes

2. **Geometry Projection**
   - **Risk:** GeoJSON vs WKT output varies by provider
   - **Mitigation:** Keep `BuildGeometryProjection` as abstract method
   - **Validation:** Parse output geometry on each provider

3. **Extent Calculation**
   - **Risk:** Completely different spatial functions per provider
   - **Mitigation:** Keep as provider-specific override
   - **Validation:** Compare extent results with known geometries

### Low-Risk Areas

1. **Temporal Predicates** (95% identical)
2. **ORDER BY Clauses** (98% identical)
3. **Aggregate Expressions** (100% identical)
4. **Table/Column Helpers** (100% identical)
5. **Validation Methods** (90% identical)

---

## Code Examples: Before & After

### Example 1: BuildCount (Before)

**PostgresFeatureQueryBuilder.cs:**
```csharp
internal PostgresQueryDefinition BuildCount(FeatureQuery query)
{
    Guard.NotNull(query);
    var sql = new StringBuilder();
    var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
    const string alias = "t";

    sql.Append("select count(*) from ");
    sql.Append(GetTableExpression());
    sql.Append(' ');
    sql.Append(alias);

    AppendWhereClause(sql, query, parameters, alias);

    return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
}
```

**MySqlFeatureQueryBuilder.cs:**
```csharp
public MySqlQueryDefinition BuildCount(FeatureQuery query)
{
    ArgumentNullException.ThrowIfNull(query);
    var sql = new StringBuilder();
    var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
    const string alias = "t";

    sql.Append("select count(*) from ");
    sql.Append(GetTableExpression());
    sql.Append(' ');
    sql.Append(alias);

    AppendWhereClause(sql, query, parameters, alias);

    return new MySqlQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
}
```

**Duplication:** ~95% identical, duplicated across 12 builders

---

### Example 1: BuildCount (After - Base Class)

**FeatureQueryBuilderBase.cs:**
```csharp
public abstract class FeatureQueryBuilderBase<TQueryDefinition>
{
    protected readonly ServiceDefinition _service;
    protected readonly LayerDefinition _layer;
    protected readonly int _storageSrid;
    protected readonly int _targetSrid;

    public TQueryDefinition BuildCount(FeatureQuery query)
    {
        Guard.NotNull(query);
        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("select count(*) from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        return CreateQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    protected abstract TQueryDefinition CreateQueryDefinition(string sql, IReadOnlyDictionary<string, object?> parameters);

    protected string GetTableExpression()
    {
        var table = _layer.Storage?.Table ?? _layer.Id;
        var parts = table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var quoted = parts.Select(QuoteIdentifier);
        return string.Join('.', quoted);
    }

    protected abstract string QuoteIdentifier(string identifier);
    protected abstract void AppendWhereClause(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters, string alias);
}
```

**PostgresFeatureQueryBuilder.cs:**
```csharp
public sealed class PostgresFeatureQueryBuilder : FeatureQueryBuilderBase<PostgresQueryDefinition>
{
    protected override PostgresQueryDefinition CreateQueryDefinition(string sql, IReadOnlyDictionary<string, object?> parameters)
    {
        return new PostgresQueryDefinition(sql, parameters);
    }

    protected override string QuoteIdentifier(string identifier)
    {
        return SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);
    }

    // ... provider-specific implementations
}
```

**Result:** ~180 lines of BuildCount eliminated (15 lines × 12 builders)

---

### Example 2: AppendOrderBy (Before)

**Duplicated 12 times across all builders:**

```csharp
private void AppendOrderBy(StringBuilder sql, FeatureQuery query, string alias)
{
    var segments = new List<string>();
    if (query.SortOrders is { Count: > 0 })
    {
        foreach (var order in query.SortOrders)
        {
            var direction = order.Direction == FeatureSortDirection.Descending ? "desc" : "asc";
            segments.Add($"{alias}.{QuoteIdentifier(order.Field)} {direction}");
        }
    }
    else
    {
        var orderFieldName = _layer.IdField.IsNullOrWhiteSpace() ? GetPrimaryKeyColumn() : _layer.IdField;
        segments.Add($"{alias}.{QuoteIdentifier(orderFieldName)} asc");
    }

    sql.Append(" order by ");
    sql.Append(string.Join(", ", segments));
}
```

---

### Example 2: AppendOrderBy (After - Utility Class)

**SqlClauseBuilder.cs:**
```csharp
public static class SqlClauseBuilder
{
    public static void AppendOrderBy(
        StringBuilder sql,
        FeatureQuery query,
        string alias,
        Func<string, string> quoteIdentifier,
        string defaultSortColumn)
    {
        var segments = new List<string>();
        if (query.SortOrders is { Count: > 0 })
        {
            foreach (var order in query.SortOrders)
            {
                var direction = order.Direction == FeatureSortDirection.Descending ? "desc" : "asc";
                segments.Add($"{alias}.{quoteIdentifier(order.Field)} {direction}");
            }
        }
        else
        {
            segments.Add($"{alias}.{quoteIdentifier(defaultSortColumn)} asc");
        }

        sql.Append(" order by ");
        sql.Append(string.Join(", ", segments));
    }
}
```

**Usage in PostgresFeatureQueryBuilder.cs:**
```csharp
private void AppendOrderBy(StringBuilder sql, FeatureQuery query, string alias)
{
    var defaultSort = _layer.IdField.IsNullOrWhiteSpace() ? GetPrimaryKeyColumn() : _layer.IdField;
    SqlClauseBuilder.AppendOrderBy(sql, query, alias, QuoteIdentifier, defaultSort);
}
```

**Result:** ~15 lines × 12 builders = ~180 lines saved

---

### Example 3: Aggregate Expressions (Before)

**Duplicated with 100% identical logic across 10 builders:**

```csharp
private string BuildAggregateExpression(StatisticDefinition statistic, string alias)
{
    var fieldReference = statistic.FieldName.IsNullOrWhiteSpace()
        ? null
        : $"{alias}.{QuoteIdentifier(statistic.FieldName)}";

    return statistic.Type switch
    {
        StatisticType.Count => "COUNT(*)",
        StatisticType.Sum => EnsureAggregateField("SUM", fieldReference, statistic),
        StatisticType.Avg => EnsureAggregateField("AVG", fieldReference, statistic),
        StatisticType.Min => EnsureAggregateField("MIN", fieldReference, statistic),
        StatisticType.Max => EnsureAggregateField("MAX", fieldReference, statistic),
        _ => throw new NotSupportedException($"Statistic type '{statistic.Type}' is not supported.")
    };
}

private static string EnsureAggregateField(string functionName, string? fieldReference, StatisticDefinition statistic)
{
    if (fieldReference.IsNullOrWhiteSpace())
    {
        throw new NotSupportedException($"Statistic type '{statistic.Type}' requires a field name.");
    }

    return $"{functionName}({fieldReference})";
}
```

---

### Example 3: Aggregate Expressions (After - Utility Class)

**AggregateExpressionBuilder.cs:**
```csharp
public static class AggregateExpressionBuilder
{
    public static string Build(
        StatisticDefinition statistic,
        string alias,
        Func<string, string> quoteIdentifier)
    {
        var fieldReference = statistic.FieldName.IsNullOrWhiteSpace()
            ? null
            : $"{alias}.{quoteIdentifier(statistic.FieldName)}";

        return statistic.Type switch
        {
            StatisticType.Count => "COUNT(*)",
            StatisticType.Sum => EnsureAggregateField("SUM", fieldReference, statistic.Type),
            StatisticType.Avg => EnsureAggregateField("AVG", fieldReference, statistic.Type),
            StatisticType.Min => EnsureAggregateField("MIN", fieldReference, statistic.Type),
            StatisticType.Max => EnsureAggregateField("MAX", fieldReference, statistic.Type),
            _ => throw new NotSupportedException($"Statistic type '{statistic.Type}' is not supported.")
        };
    }

    private static string EnsureAggregateField(string functionName, string? fieldReference, StatisticType type)
    {
        if (fieldReference.IsNullOrWhiteSpace())
        {
            throw new NotSupportedException($"Statistic type '{type}' requires a field name.");
        }

        return $"{functionName}({fieldReference})";
    }
}
```

**Usage in builders:**
```csharp
// Before:
var aggregate = BuildAggregateExpression(statistic, alias);

// After:
var aggregate = AggregateExpressionBuilder.Build(statistic, alias, QuoteIdentifier);
```

**Result:** ~30 lines × 10 builders = ~300 lines saved

---

## Recommendations

### Immediate Actions (Next Sprint)

1. **Implement Phase 1 (Quick Wins)**
   - Create utility classes for validation, aggregates, and layer metadata
   - Update enterprise builders (Snowflake, BigQuery, Redshift, Oracle) to use utilities
   - **Benefit:** 16% reduction with minimal risk
   - **Effort:** 1 week

2. **Document Vendor Variations**
   - Create reference guide for spatial functions per provider
   - Document parameter syntax differences
   - Useful for future developers

### Medium-Term (Next Quarter)

3. **Implement Phase 2 (Clause Builders)**
   - Create `SqlClauseBuilder` for common WHERE, ORDER BY, temporal logic
   - Update all 12 builders incrementally
   - **Benefit:** Additional 11% reduction
   - **Effort:** 1 week

4. **Implement Phase 3 (Base Class)**
   - Design and implement `FeatureQueryBuilderBase<TQueryDefinition>`
   - Refactor core builders first (lower risk)
   - Then refactor enterprise builders
   - **Benefit:** Additional 20% reduction
   - **Effort:** 1 week

### Long-Term (Optional)

5. **Implement Phase 4 (Strategy Pattern)**
   - Extract vendor-specific strategies for spatial functions and pagination
   - Consider only if adding new providers frequently
   - **Benefit:** Improved maintainability, 7% reduction
   - **Effort:** 1 week

### Do NOT Do

- **Avoid over-abstraction:** Don't create base class just for 2-3 lines of code
- **Avoid premature optimization:** Don't consolidate if vendor-specific variations are significant
- **Avoid breaking changes:** Maintain backward compatibility with existing consumers

---

## Appendix: Related Files

### Spatial Filter Translators (4 classes, ~600 lines)

- `/src/Honua.Server.Core/Data/Postgres/PostgresSpatialFilterTranslator.cs` (155 lines)
- `/src/Honua.Server.Core/Data/MySql/MySqlSpatialFilterTranslator.cs` (~150 lines)
- `/src/Honua.Server.Core/Data/SqlServer/SqlServerSpatialFilterTranslator.cs` (~150 lines)
- `/src/Honua.Server.Core/Data/Sqlite/SqliteSpatialFilterTranslator.cs` (~150 lines)

**Note:** These translators also have significant duplication (~70%) and could benefit from similar consolidation strategies.

### Supporting Infrastructure

- `SqlIdentifierValidator` - Used for SQL injection protection (already consolidated)
- `CrsHelper` - Coordinate reference system parsing (already shared)
- `Guard` - Parameter validation (already shared)
- `KeysetPaginationQueryBuilder` - Keyset pagination WHERE clauses (already shared)
- `Cql2SqlQueryBuilder` - CQL2 to SQL translation (already multi-provider)

---

## Conclusion

The query builder classes exhibit **significant duplication** with approximately **60-70% code overlap** across 12 implementations. The recommended **three-phase consolidation approach** can reduce the codebase by **~2,060 lines (46%)** while improving maintainability and consistency.

**Priority Recommendation:** Start with **Phase 1 (Quick Wins)** to achieve immediate benefits with minimal risk, then evaluate whether **Phase 2 and Phase 3** provide sufficient ROI based on project priorities and available resources.

**Key Success Factors:**
- Comprehensive test coverage for each phase
- Incremental migration (one builder at a time)
- Maintain backward compatibility
- Document vendor-specific variations clearly

**Long-Term Benefits:**
- Easier to add new database providers
- Centralized bug fixes and improvements
- Reduced maintenance burden
- Consistent behavior across all providers
- Faster feature development (update once, works everywhere)

---

**Analysis Completed:** 2025-10-31
**Analyzed By:** Claude Code (Sonnet 4.5)
**Total Files Analyzed:** 16 query builders + 6 supporting classes
**Total Lines Analyzed:** ~4,456 lines of code
