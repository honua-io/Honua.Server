# SQL Views (Virtual Layers)

## Overview

SQL Views allow you to create virtual layers backed by custom SQL queries instead of physical database tables. This is one of the most powerful features for creating dynamic, parameterized layers from complex queries, joins, and computed fields.

SQL Views in Honua.Server are similar to GeoServer's SQL Views feature but with enhanced security, validation, and parameter handling.

## Key Features

1. **Virtual Layers**: Create layers from any SELECT query
2. **Parameterized Queries**: URL parameters → SQL parameters with type safety
3. **On-the-fly Transformation**: No data duplication or materialization
4. **Complex Joins**: Multi-table queries exposed as single layers
5. **Computed Fields**: SQL expressions as layer attributes
6. **Security First**: Built-in SQL injection prevention
7. **Parameter Validation**: Type checking, ranges, patterns, allowed values

## Security

### SQL Injection Prevention

Honua.Server implements multiple layers of defense against SQL injection:

1. **Parameterized Queries Only**: All user input is passed via parameterized queries, never string concatenation
2. **Query Validation**: SQL views must start with SELECT and cannot contain dangerous keywords
3. **Comment Blocking**: SQL comments (`--` and `/* */`) are not allowed to prevent comment-based injection
4. **Parameter Validation**: All parameters are validated and type-checked before use
5. **Keyword Blacklist**: Dangerous keywords (DROP, DELETE, INSERT, UPDATE, EXEC, etc.) are blocked
6. **Security Filters**: Optional WHERE clause that is always applied

### Validated Dangerous Keywords

The following SQL keywords are blocked in SQL view definitions:

- Data modification: `DROP`, `TRUNCATE`, `ALTER`, `CREATE`, `INSERT`, `UPDATE`, `DELETE`
- Execution: `EXEC`, `EXECUTE`, `xp_`, `sp_`
- Security: `GRANT`, `REVOKE`
- Transaction control: `COMMIT`, `ROLLBACK`, `BEGIN`
- System commands: `USE`, `SHUTDOWN`, `BACKUP`, `RESTORE`

## Usage

### Basic SQL View

```json
{
  "id": "high_population_cities",
  "serviceId": "demo",
  "title": "High Population Cities",
  "geometryType": "Point",
  "idField": "city_id",
  "geometryField": "location",
  "sqlView": {
    "sql": "SELECT city_id, name, population, location FROM cities WHERE population > :min_population",
    "parameters": [
      {
        "name": "min_population",
        "type": "integer",
        "defaultValue": "100000",
        "validation": {
          "min": 0,
          "max": 100000000
        }
      }
    ]
  }
}
```

**Query Example:**
```
GET /ogc/collections/high_population_cities/items?min_population=500000
```

### SQL View with Multiple Parameters

```json
{
  "id": "filtered_cities",
  "serviceId": "demo",
  "title": "Filtered Cities",
  "geometryType": "Point",
  "idField": "city_id",
  "geometryField": "location",
  "sqlView": {
    "sql": "SELECT city_id, name, population, region, location FROM cities WHERE population > :min_population AND region = :region AND country = :country",
    "parameters": [
      {
        "name": "min_population",
        "type": "integer",
        "defaultValue": "100000",
        "validation": {
          "min": 0,
          "max": 100000000
        }
      },
      {
        "name": "region",
        "type": "string",
        "defaultValue": "west",
        "required": true,
        "validation": {
          "allowedValues": ["north", "south", "east", "west", "central"]
        }
      },
      {
        "name": "country",
        "type": "string",
        "defaultValue": "USA",
        "validation": {
          "pattern": "^[A-Z]{2,3}$",
          "maxLength": 3
        }
      }
    ]
  }
}
```

**Query Example:**
```
GET /ogc/collections/filtered_cities/items?min_population=500000&region=east&country=USA
```

### SQL View with JOIN

```json
{
  "id": "city_statistics",
  "serviceId": "demo",
  "title": "City Statistics by Country",
  "geometryType": "Point",
  "idField": "country_code",
  "geometryField": "centroid",
  "sqlView": {
    "sql": "SELECT c.country_code, c.country_name, c.centroid, COUNT(ci.city_id) as city_count, SUM(ci.population) as total_population FROM countries c LEFT JOIN cities ci ON c.country_code = ci.country_code WHERE c.continent = :continent GROUP BY c.country_code, c.country_name, c.centroid",
    "parameters": [
      {
        "name": "continent",
        "type": "string",
        "defaultValue": "North America",
        "validation": {
          "allowedValues": ["Africa", "Asia", "Europe", "North America", "South America", "Oceania", "Antarctica"]
        }
      }
    ],
    "timeoutSeconds": 60
  }
}
```

### SQL View with Security Filter

Security filters are always applied and cannot be bypassed by user parameters:

```json
{
  "id": "active_users",
  "serviceId": "demo",
  "title": "Active Users",
  "geometryType": "Point",
  "idField": "user_id",
  "geometryField": "location",
  "sqlView": {
    "sql": "SELECT user_id, username, email, location FROM users WHERE role = :role",
    "parameters": [
      {
        "name": "role",
        "type": "string",
        "defaultValue": "user",
        "validation": {
          "allowedValues": ["user", "admin", "moderator"]
        }
      }
    ],
    "securityFilter": "deleted_at IS NULL AND active = true"
  }
}
```

This ensures that all queries include `WHERE deleted_at IS NULL AND active = true`, even if the user tries to manipulate parameters.

## Parameter Types

SQL views support the following parameter types:

| Type      | Description                    | Example                          |
|-----------|--------------------------------|----------------------------------|
| `string`  | Text values                    | `"San Francisco"`                |
| `integer` | 32-bit integers                | `100000`                         |
| `long`    | 64-bit integers                | `9223372036854775807`            |
| `double`  | Floating point numbers         | `123.456`                        |
| `decimal` | Precise decimal numbers        | `99.99`                          |
| `boolean` | True/false values              | `true` or `false`                |
| `date`    | Date values (no time)          | `2024-01-15`                     |
| `datetime`| Date and time values           | `2024-01-15T10:30:00Z`           |

## Parameter Validation

### Numeric Validation

```json
{
  "name": "population",
  "type": "integer",
  "validation": {
    "min": 0,
    "max": 100000000
  }
}
```

### String Length Validation

```json
{
  "name": "city_name",
  "type": "string",
  "validation": {
    "minLength": 2,
    "maxLength": 100
  }
}
```

### Pattern Validation (Regex)

```json
{
  "name": "country_code",
  "type": "string",
  "validation": {
    "pattern": "^[A-Z]{2,3}$"
  }
}
```

### Allowed Values (Enum)

```json
{
  "name": "region",
  "type": "string",
  "validation": {
    "allowedValues": ["north", "south", "east", "west"]
  }
}
```

### Custom Error Messages

```json
{
  "name": "priority",
  "type": "integer",
  "validation": {
    "min": 1,
    "max": 5,
    "errorMessage": "Priority must be between 1 (low) and 5 (critical)"
  }
}
```

## Parameter Syntax

Parameters in SQL are referenced using colon notation: `:paramName`

```sql
SELECT * FROM cities WHERE population > :min_population AND region = :region
```

During execution, these are replaced with database-specific parameter placeholders:
- PostgreSQL: `@sqlview_min_population`, `@sqlview_region`
- SQL Server: `@sqlview_min_population`, `@sqlview_region`
- MySQL: `@sqlview_min_population`, `@sqlview_region`

## Layer Requirements

Layers using SQL views must:

1. Have either `storage.table` OR `sqlView` (not both)
2. Include the `idField` in the SELECT clause
3. Include the `geometryField` in the SELECT clause
4. Use only SELECT statements (no DML/DDL)
5. Not contain SQL comments

## Performance Considerations

### Query Timeout

Set appropriate timeout values for complex queries:

```json
{
  "sqlView": {
    "sql": "SELECT ... complex query ...",
    "timeoutSeconds": 120
  }
}
```

### Geometry Indexing

Ensure your base tables have spatial indexes:

```sql
CREATE INDEX idx_cities_location ON cities USING GIST (location);
```

### Query Hints

Provider-specific query hints can be added:

```json
{
  "sqlView": {
    "sql": "SELECT ...",
    "hints": "USE INDEX (idx_geometry)",
    "providerSettings": {
      "postgres": "SET work_mem = '256MB'",
      "sqlserver": "OPTION (MAXDOP 4)"
    }
  }
}
```

## Read-Only Mode

SQL views are read-only by default for security. Editing operations are not allowed:

```json
{
  "sqlView": {
    "sql": "SELECT ...",
    "readOnly": true  // Default
  }
}
```

## WFS Integration

SQL views work seamlessly with WFS:

### GetCapabilities

SQL view layers appear as normal feature types in WFS GetCapabilities. Parameters are documented in the layer metadata.

### GetFeature

Parameters are passed via URL query parameters:

```xml
<wfs:GetFeature service="WFS" version="2.0.0">
  <wfs:Query typeNames="high_population_cities">
    <fes:Filter>
      <!-- Additional CQL filters can be applied on top of SQL view -->
    </fes:Filter>
  </wfs:Query>
</wfs:GetFeature>
```

URL: `?typeName=high_population_cities&min_population=500000&region=east`

## OGC API Features Integration

SQL views are fully supported in OGC API Features:

### Collections Endpoint

```
GET /ogc/collections/high_population_cities
```

Returns collection metadata including parameter definitions.

### Items Endpoint

```
GET /ogc/collections/high_population_cities/items?min_population=500000&region=east
```

Parameters are validated and passed to the SQL view.

## Schema Detection

SQL Views support automatic schema detection, allowing you to discover field types from your SQL query without manually defining them.

### Overview

Schema detection executes your SQL query with default parameter values and inspects the result metadata to determine:
- Field names
- Field data types (mapped to Esri field types)
- Nullable status
- Field lengths (for string fields)

This is particularly useful for:
- Complex queries with many fields
- Quickly prototyping SQL views
- Ensuring field type accuracy

### Using Schema Detection

#### Via Admin API

```bash
POST /admin/metadata/layers/{layerId}/sqlview/detect-schema
Content-Type: application/json

{
  "sql": "SELECT id, name, population, created_at FROM cities WHERE region = :region",
  "parameters": {
    "region": "west"
  }
}
```

#### Response

```json
{
  "success": true,
  "fields": [
    {
      "name": "id",
      "type": "esriFieldTypeInteger",
      "nullable": false,
      "isGeometry": false
    },
    {
      "name": "name",
      "type": "esriFieldTypeString",
      "nullable": true,
      "isGeometry": false
    },
    {
      "name": "population",
      "type": "esriFieldTypeInteger",
      "nullable": true,
      "isGeometry": false
    },
    {
      "name": "created_at",
      "type": "esriFieldTypeDate",
      "nullable": true,
      "isGeometry": false
    }
  ],
  "geometryField": "location",
  "geometryType": "Point",
  "idField": "id"
}
```

### Supported Providers

Schema detection is available for the following database providers:
- PostgreSQL/PostGIS
- SQL Server
- MySQL
- SQLite/SpatiaLite

### Type Mapping

.NET types from the database are automatically mapped to Esri field types:

| Database Type        | .NET Type  | Esri Field Type              |
|----------------------|------------|------------------------------|
| INTEGER, INT         | int        | esriFieldTypeInteger         |
| BIGINT               | long       | esriFieldTypeBigInteger      |
| REAL, FLOAT          | float      | esriFieldTypeDouble          |
| DOUBLE PRECISION     | double     | esriFieldTypeDouble          |
| NUMERIC, DECIMAL     | decimal    | esriFieldTypeDouble          |
| VARCHAR, TEXT        | string     | esriFieldTypeString          |
| TIMESTAMP, DATETIME  | DateTime   | esriFieldTypeDate            |
| BOOLEAN, BIT         | bool       | esriFieldTypeSmallInteger    |
| UUID, UNIQUEIDENTIFIER | Guid     | esriFieldTypeGUID            |
| BLOB, BYTEA          | byte[]     | esriFieldTypeBlob            |

### How It Works

1. **Query Wrapping**: The SQL query is wrapped in a LIMIT 0 query (or TOP 0 for SQL Server) to fetch only schema metadata without actual data
2. **Parameter Substitution**: Default parameter values are used for the schema query
3. **Schema Inspection**: The database returns column metadata including names, types, and constraints
4. **Type Mapping**: .NET types are mapped to Esri field types
5. **Result**: A list of detected fields is returned

### Example Workflow

1. Create a layer with a SQL view:

```json
{
  "id": "my_sql_view",
  "serviceId": "demo",
  "title": "My SQL View",
  "geometryType": "Point",
  "idField": "id",
  "geometryField": "location",
  "sqlView": {
    "sql": "SELECT id, name, population, location FROM cities WHERE region = :region",
    "parameters": [
      {
        "name": "region",
        "type": "string",
        "defaultValue": "west"
      }
    ]
  }
}
```

2. Detect schema via API:

```bash
curl -X POST http://localhost:5000/admin/metadata/layers/my_sql_view/sqlview/detect-schema \
  -H "Content-Type: application/json" \
  -d '{}'
```

3. Use the detected fields to populate your layer's `fields` property

### Limitations

- **Read-Only Detection**: Schema detection does not modify your layer configuration
- **Default Values Required**: Parameters without default values need to be provided in the request
- **Timeout**: Schema queries timeout after 5 seconds
- **Complex Types**: Some complex types may be mapped to generic types

### Error Handling

If schema detection fails, the response will include an error message:

```json
{
  "success": false,
  "errorMessage": "Failed to retrieve schema from SQL view: Syntax error near 'FROM'"
}
```

Common errors:
- **Invalid SQL**: Check your SQL syntax
- **Missing Parameters**: Provide default values for all parameters
- **Connection Issues**: Verify the data source connection
- **Unsupported Provider**: Check if your database provider supports schema detection

## Best Practices

### 1. Use Parameter Validation

Always validate parameters to prevent invalid queries:

```json
{
  "validation": {
    "allowedValues": ["option1", "option2"],
    "min": 0,
    "max": 1000
  }
}
```

### 2. Set Reasonable Timeouts

Prevent long-running queries from blocking resources:

```json
{
  "timeoutSeconds": 30
}
```

### 3. Document Parameters

Use descriptive titles and descriptions:

```json
{
  "name": "min_population",
  "title": "Minimum Population",
  "description": "Filter cities with population greater than this value"
}
```

### 4. Use Security Filters

Add security filters for sensitive data:

```json
{
  "securityFilter": "deleted_at IS NULL AND tenant_id = current_tenant_id()"
}
```

### 5. Test Thoroughly

Test SQL views with various parameter combinations before production deployment.

### 6. Monitor Performance

Use database query logs to monitor SQL view performance and optimize as needed.

## Limitations

1. **Read-Only**: SQL views are read-only by default
2. **No DDL/DML**: Only SELECT statements are allowed
3. **No Comments**: SQL comments are not permitted for security
4. **Parameter Count**: Keep parameter count reasonable (< 20)
5. **Allowed Values**: Maximum 1000 allowed values per parameter

## Examples

See `/examples/sql-views/` for complete working examples:

- `high-population-cities.json`: Basic parameterized filtering
- `city-statistics.json`: JOIN queries with aggregation
- `temporal-events.json`: Date range filtering

## Migration from GeoServer

Honua.Server SQL views are similar to GeoServer's SQL views with some differences:

| Feature                | GeoServer          | Honua.Server       |
|------------------------|--------------------|--------------------|
| Parameter syntax       | `%paramName%`      | `:paramName`       |
| Validation             | Limited            | Comprehensive      |
| Type safety            | String-based       | Strongly typed     |
| Security filter        | Not built-in       | Built-in           |
| Allowed values         | Not built-in       | Built-in           |
| Regex validation       | Not built-in       | Built-in           |

## Troubleshooting

### "Layer must have either Storage.Table or SqlView defined"

Ensure your layer has a `sqlView` property with a valid SQL query.

### "SQL view contains potentially dangerous keyword"

Remove any DML/DDL keywords from your SQL. Only SELECT queries are allowed.

### "Parameter validation failed"

Check that parameter values match the validation rules (type, range, pattern, allowed values).

### "SQL view must include the 'id' field"

Ensure your SELECT clause includes the field specified in `idField`.

### Query timeout

Increase `timeoutSeconds` for complex queries or optimize your SQL.

## Security Audit

All SQL view executions are logged for security audit purposes. Logs include:

- Layer ID
- SQL query (with parameter placeholders)
- Parameter values
- Execution time
- User/session information

Monitor these logs for unusual patterns or potential security issues.
