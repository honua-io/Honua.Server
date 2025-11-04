# CQL2-JSON Operators: BETWEEN, IN, IS NULL

This document provides examples of the newly implemented CQL2-JSON operators.

## BETWEEN Operator

The `BETWEEN` operator filters values within a specified range (inclusive).

### Syntax

```json
{
  "op": "between",
  "args": [
    {"property": "field_name"},
    lower_bound,
    upper_bound
  ]
}
```

### Examples

**Numeric Range:**
```json
{
  "op": "between",
  "args": [
    {"property": "age"},
    18,
    65
  ]
}
```
Equivalent SQL: `age >= 18 AND age <= 65`

**Date Range:**
```json
{
  "op": "between",
  "args": [
    {"property": "created"},
    "2020-01-01T00:00:00Z",
    "2024-12-31T23:59:59Z"
  ]
}
```
Equivalent SQL: `created >= '2020-01-01T00:00:00Z' AND created <= '2024-12-31T23:59:59Z'`

**String Range:**
```json
{
  "op": "between",
  "args": [
    {"property": "name"},
    "A",
    "M"
  ]
}
```
Equivalent SQL: `name >= 'A' AND name <= 'M'`

## IN Operator

The `IN` operator checks if a value matches any value in a list.

### Syntax

```json
{
  "op": "in",
  "args": [
    {"property": "field_name"},
    [value1, value2, value3, ...]
  ]
}
```

### Examples

**String List:**
```json
{
  "op": "in",
  "args": [
    {"property": "status"},
    ["active", "pending", "approved"]
  ]
}
```
Equivalent SQL: `status IN ('active', 'pending', 'approved')`
Or expanded: `status = 'active' OR status = 'pending' OR status = 'approved'`

**Numeric List:**
```json
{
  "op": "in",
  "args": [
    {"property": "priority"},
    [1, 2, 3]
  ]
}
```
Equivalent SQL: `priority IN (1, 2, 3)`

**Single Value (Optimized):**
```json
{
  "op": "in",
  "args": [
    {"property": "status"},
    ["active"]
  ]
}
```
Optimized to: `status = 'active'`

## IS NULL Operator

The `IS NULL` operator checks if a field value is null.

### Syntax

```json
{
  "op": "isNull",
  "args": [
    {"property": "field_name"}
  ]
}
```

### Examples

**Check for Null:**
```json
{
  "op": "isNull",
  "args": [
    {"property": "email"}
  ]
}
```
Equivalent SQL: `email IS NULL`

**Check for Not Null (using NOT):**
```json
{
  "op": "not",
  "args": [
    {
      "op": "isNull",
      "args": [
        {"property": "email"}
      ]
    }
  ]
}
```
Equivalent SQL: `email IS NOT NULL`

## Complex Queries

### Combining Multiple Operators

```json
{
  "op": "and",
  "args": [
    {
      "op": "between",
      "args": [
        {"property": "age"},
        25,
        45
      ]
    },
    {
      "op": "in",
      "args": [
        {"property": "status"},
        ["active", "pending", "approved"]
      ]
    },
    {
      "op": "not",
      "args": [
        {
          "op": "isNull",
          "args": [
            {"property": "email"}
          ]
        }
      ]
    }
  ]
}
```

Equivalent SQL:
```sql
(age >= 25 AND age <= 45)
AND (status IN ('active', 'pending', 'approved'))
AND (email IS NOT NULL)
```

### BETWEEN with OR

```json
{
  "op": "or",
  "args": [
    {
      "op": "between",
      "args": [
        {"property": "temperature"},
        -10,
        0
      ]
    },
    {
      "op": "between",
      "args": [
        {"property": "temperature"},
        30,
        40
      ]
    }
  ]
}
```

Equivalent SQL:
```sql
(temperature >= -10 AND temperature <= 0)
OR (temperature >= 30 AND temperature <= 40)
```

## Implementation Details

### BETWEEN
- Expanded to two comparisons: `property >= lower AND property <= upper`
- Supports numeric, date, and string ranges
- Values are coerced to the field's data type

### IN
- Expanded to OR chain: `property = value1 OR property = value2 OR ...`
- Single value is optimized to simple equality
- Empty arrays throw an error
- Values are coerced to the field's data type

### IS NULL
- Represented as `property = NULL` in expression tree
- SQL translator converts to `IS NULL` syntax
- Can be negated with NOT operator for `IS NOT NULL`

## Performance Considerations

### BETWEEN
- Generates optimal SQL with native comparison operators
- Database indexes on the property can be utilized
- More efficient than using two separate comparison operators

### IN
- Uses parameterized queries to prevent SQL injection
- Large IN lists (100+ values) may impact performance
- Consider alternatives for very large lists (temp tables, joins)

### IS NULL
- Efficiently translated to native `IS NULL` SQL
- Database indexes may not be used (depends on DBMS)
- Very fast operation in most databases

## Error Handling

All three operators perform validation:

- **Missing arguments**: Throws `InvalidOperationException`
- **Wrong argument types**: Throws `InvalidOperationException`
- **Empty arrays (IN)**: Throws `InvalidOperationException`
- **Invalid property references**: Throws `InvalidOperationException`

## Database Support

All operators are supported by all database providers:
- PostgreSQL
- SQL Server
- MySQL
- SQLite

The SQL translation handles provider-specific syntax differences automatically.
