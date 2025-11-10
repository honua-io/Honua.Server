# OGC SensorThings API - Advanced Filtering Guide

This guide explains the advanced filtering capabilities of the Honua Server OGC SensorThings API v1.1 implementation.

## Overview

The SensorThings API supports sophisticated OData-style filtering through the `$filter` query parameter. Beyond basic comparison operators, the implementation includes:

- **Logical operators**: `and`, `or`, `not`
- **String functions**: `contains`, `startswith`, `endswith`, `length`, `tolower`, `toupper`, `trim`, `concat`, `substring`, `indexof`
- **Math functions**: `round`, `floor`, `ceiling`
- **Spatial functions**: `geo.distance`, `geo.intersects`, `geo.length`, `geo.within`
- **Temporal functions**: `year`, `month`, `day`, `hour`, `minute`, `second`

## Basic Comparison Operators

### Syntax

```
$filter={property} {operator} {value}
```

### Supported Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `eq` | Equals | `name eq 'Weather Station'` |
| `ne` | Not equals | `status ne 'inactive'` |
| `gt` | Greater than | `result gt 20.5` |
| `ge` | Greater than or equal | `result ge 20` |
| `lt` | Less than | `result lt 30` |
| `le` | Less than or equal | `result le 30.5` |

### Examples

```bash
# Get observations with result greater than 20
GET /sta/v1.1/Observations?$filter=result gt 20

# Get things with specific name
GET /sta/v1.1/Things?$filter=name eq 'Weather Station Alpha'

# Get observations before a specific time
GET /sta/v1.1/Observations?$filter=phenomenonTime lt 2025-01-01T00:00:00Z
```

## Logical Operators

### AND Operator

Combines two conditions - both must be true.

**Syntax:**
```
{condition1} and {condition2}
```

**Examples:**
```bash
# Temperature > 20 AND humidity < 80
GET /sta/v1.1/Observations?$filter=result gt 20 and result lt 30

# Active things named "Weather Station"
GET /sta/v1.1/Things?$filter=name eq 'Weather Station' and properties/status eq 'active'
```

### OR Operator

Combines two conditions - at least one must be true.

**Syntax:**
```
{condition1} or {condition2}
```

**Examples:**
```bash
# Status is either 'active' or 'pending'
GET /sta/v1.1/Things?$filter=properties/status eq 'active' or properties/status eq 'pending'

# Temperature extremes (very hot or very cold)
GET /sta/v1.1/Observations?$filter=result gt 35 or result lt 0
```

### NOT Operator

Negates a condition.

**Syntax:**
```
not ({condition})
```

**Examples:**
```bash
# Things NOT named 'Weather Station'
GET /sta/v1.1/Things?$filter=not (name eq 'Weather Station')

# Observations NOT in normal range
GET /sta/v1.1/Observations?$filter=not (result ge 15 and result le 25)
```

### Complex Logical Expressions

Use parentheses to control precedence.

**Examples:**
```bash
# (Hot and dry) OR (cold and wet)
GET /sta/v1.1/Observations?$filter=(result gt 30 and humidity lt 40) or (result lt 5 and humidity gt 80)

# Active or pending, but not disabled
GET /sta/v1.1/Things?$filter=(status eq 'active' or status eq 'pending') and not (status eq 'disabled')
```

## String Functions

### contains(property, substring)

Returns true if the property contains the substring.

**Examples:**
```bash
# Things with "Weather" in the name
GET /sta/v1.1/Things?$filter=contains(name, 'Weather')

# Sensors with "Temperature" in description
GET /sta/v1.1/Sensors?$filter=contains(description, 'Temperature')
```

### startswith(property, prefix)

Returns true if the property starts with the prefix.

**Examples:**
```bash
# Things starting with "Station"
GET /sta/v1.1/Things?$filter=startswith(name, 'Station')

# ObservedProperties starting with "Air"
GET /sta/v1.1/ObservedProperties?$filter=startswith(name, 'Air')
```

### endswith(property, suffix)

Returns true if the property ends with the suffix.

**Examples:**
```bash
# Sensors ending with "Sensor"
GET /sta/v1.1/Sensors?$filter=endswith(name, 'Sensor')

# Things ending with "Alpha"
GET /sta/v1.1/Things?$filter=endswith(name, 'Alpha')
```

### tolower(property) / toupper(property)

Converts property to lowercase/uppercase for case-insensitive comparison.

**Examples:**
```bash
# Case-insensitive name search
GET /sta/v1.1/Things?$filter=tolower(name) eq 'weather station'

# Case-insensitive status check
GET /sta/v1.1/Things?$filter=toupper(properties/status) eq 'ACTIVE'
```

### length(property)

Returns the length of a string property.

**Examples:**
```bash
# Names longer than 20 characters
GET /sta/v1.1/Things?$filter=length(name) gt 20

# Short descriptions
GET /sta/v1.1/Sensors?$filter=length(description) lt 50
```

### trim(property)

Removes leading and trailing whitespace.

**Examples:**
```bash
# Match trimmed names
GET /sta/v1.1/Things?$filter=trim(name) eq 'Weather Station'
```

### substring(property, startIndex, length)

Extracts a substring from a property.

**Examples:**
```bash
# First 3 characters equal "WS-"
GET /sta/v1.1/Things?$filter=substring(name, 0, 3) eq 'WS-'

# Year from phenomenon time (characters 0-4)
GET /sta/v1.1/Observations?$filter=substring(phenomenonTime, 0, 4) eq '2025'
```

### indexof(property, substring)

Returns the position of substring in property (-1 if not found).

**Examples:**
```bash
# Name contains "Weather" (position >= 0)
GET /sta/v1.1/Things?$filter=indexof(name, 'Weather') ge 0

# "Sensor" appears after position 5
GET /sta/v1.1/Sensors?$filter=indexof(name, 'Sensor') gt 5
```

## Math Functions

### round(property)

Rounds a numeric property to the nearest integer.

**Examples:**
```bash
# Rounded result equals 21
GET /sta/v1.1/Observations?$filter=round(result) eq 21

# Rounded temperature > 20
GET /sta/v1.1/Observations?$filter=round(result) gt 20
```

### floor(property)

Rounds down to the nearest integer.

**Examples:**
```bash
# Floor of result is 20
GET /sta/v1.1/Observations?$filter=floor(result) eq 20

# Temperature floor >= 15
GET /sta/v1.1/Observations?$filter=floor(result) ge 15
```

### ceiling(property)

Rounds up to the nearest integer.

**Examples:**
```bash
# Ceiling of result is 22
GET /sta/v1.1/Observations?$filter=ceiling(result) eq 22

# Temperature ceiling < 30
GET /sta/v1.1/Observations?$filter=ceiling(result) lt 30
```

## Spatial Functions

### geo.distance(location, geometry)

Returns the distance in meters between two geometries.

**Syntax:**
```
geo.distance(location, geometry'WKT') {operator} {distance_in_meters}
```

**Examples:**
```bash
# Locations within 1km of a point
GET /sta/v1.1/Locations?$filter=geo.distance(location, geometry'POINT(-122.4194 37.7749)') lt 1000

# Things more than 5km away
GET /sta/v1.1/Things?$filter=geo.distance(Locations/location, geometry'POINT(-122.5 37.8)') gt 5000
```

### geo.intersects(location, geometry)

Returns true if two geometries intersect.

**Examples:**
```bash
# Locations intersecting a polygon
GET /sta/v1.1/Locations?$filter=geo.intersects(location, geometry'POLYGON((-122.5 37.7, -122.4 37.7, -122.4 37.8, -122.5 37.8, -122.5 37.7))')

# Things within a bounding box
GET /sta/v1.1/Things?$filter=geo.intersects(Locations/location, geometry'POLYGON(...)')
```

### geo.length(geometry)

Returns the length of a LineString in meters.

**Examples:**
```bash
# Routes longer than 1km
GET /sta/v1.1/FeaturesOfInterest?$filter=geo.length(feature) gt 1000
```

### geo.within(location, geometry)

Returns true if the first geometry is within the second.

**Examples:**
```bash
# Locations within a specific area
GET /sta/v1.1/Locations?$filter=geo.within(location, geometry'POLYGON(...)')
```

## Temporal Functions

### year(datetime) / month(datetime) / day(datetime)

Extracts date components from a datetime property.

**Examples:**
```bash
# Observations from 2025
GET /sta/v1.1/Observations?$filter=year(phenomenonTime) eq 2025

# Observations from November
GET /sta/v1.1/Observations?$filter=month(phenomenonTime) eq 11

# Observations from the 5th day of the month
GET /sta/v1.1/Observations?$filter=day(phenomenonTime) eq 5

# November 2025 observations
GET /sta/v1.1/Observations?$filter=year(phenomenonTime) eq 2025 and month(phenomenonTime) eq 11
```

### hour(datetime) / minute(datetime) / second(datetime)

Extracts time components from a datetime property.

**Examples:**
```bash
# Observations during business hours (9 AM - 5 PM)
GET /sta/v1.1/Observations?$filter=hour(phenomenonTime) ge 9 and hour(phenomenonTime) le 17

# Observations at the top of the hour
GET /sta/v1.1/Observations?$filter=minute(phenomenonTime) eq 0

# Observations in the first 30 minutes
GET /sta/v1.1/Observations?$filter=minute(phenomenonTime) lt 30
```

## Complex Query Examples

### Example 1: Weather Station with Recent High Temperatures

Find observations from stations with "Weather" in the name, where temperature exceeded 30°C in 2025:

```bash
GET /sta/v1.1/Observations?$filter=contains(Datastream/Thing/name, 'Weather') and result gt 30 and year(phenomenonTime) eq 2025
```

### Example 2: Active Sensors Near a Location

Find sensors within 5km of San Francisco that are currently active:

```bash
GET /sta/v1.1/Sensors?$filter=geo.distance(Datastreams/Thing/Locations/location, geometry'POINT(-122.4194 37.7749)') lt 5000 and contains(description, 'active')
```

### Example 3: Temperature Sensors with Extreme Readings

Find temperature sensors with readings above 40°C or below 0°C during November 2025:

```bash
GET /sta/v1.1/Observations?$filter=(result gt 40 or result lt 0) and year(phenomenonTime) eq 2025 and month(phenomenonTime) eq 11 and contains(Datastream/ObservedProperty/name, 'Temperature')
```

### Example 4: Case-Insensitive Name Search with Date Range

Find things with "station" in the name (case-insensitive) that have observations in November 2025:

```bash
GET /sta/v1.1/Things?$filter=contains(tolower(name), 'station') and Datastreams/Observations/year(phenomenonTime) eq 2025 and Datastreams/Observations/month(phenomenonTime) eq 11
```

### Example 5: Spatial and Temporal Combined

Find observations within 10km of a point, during daytime hours (6 AM - 6 PM), with high temperature:

```bash
GET /sta/v1.1/Observations?$filter=geo.distance(FeatureOfInterest/feature, geometry'POINT(-122.5 37.8)') lt 10000 and hour(phenomenonTime) ge 6 and hour(phenomenonTime) le 18 and result gt 25
```

## Performance Considerations

### Indexing

The following properties are indexed for optimal query performance:

- `name` - B-tree index
- `phenomenon_time` - B-tree index
- `result_time` - B-tree index
- `location` - GiST spatial index
- `feature` - GiST spatial index

### Best Practices

1. **Use indexes**: Filter on indexed columns when possible
2. **Limit results**: Combine filters with `$top` to limit result sets
3. **Avoid nested navigation**: Deep navigation properties can be slow
4. **Use spatial queries efficiently**: Spatial functions are computationally expensive
5. **Combine with $select**: Reduce payload size by selecting only needed properties

**Example - Optimized Query:**
```bash
GET /sta/v1.1/Observations?$filter=phenomenonTime gt 2025-11-01T00:00:00Z and result gt 20&$top=100&$select=result,phenomenonTime
```

## OData Conformance

### Supported

✅ Comparison operators (eq, ne, gt, ge, lt, le)
✅ Logical operators (and, or, not)
✅ String functions (contains, startswith, endswith, length, tolower, toupper, trim, substring, indexof)
✅ Math functions (round, floor, ceiling)
✅ Spatial functions (geo.distance, geo.intersects, geo.length, geo.within)
✅ Temporal functions (year, month, day, hour, minute, second)
✅ Parentheses for precedence
✅ Navigation properties

### Not Yet Supported

❌ Arithmetic operators (add, sub, mul, div, mod) as inline operators
❌ Date/time manipulation (date, time, totaloffsetminutes)
❌ Collection operators (any, all)
❌ Type casting (cast, isof)
❌ Null handling (null literal)

## Error Handling

### Invalid Filter Syntax

If a filter contains syntax errors, it will be ignored and all results will be returned:

```bash
# Invalid syntax - missing closing quote
GET /sta/v1.1/Things?$filter=name eq 'Weather

# Response: Returns all Things (filter ignored)
```

### Unsupported Functions

If a filter uses an unsupported function, a 400 Bad Request error is returned:

```bash
# Unsupported function
GET /sta/v1.1/Things?$filter=unsupportedfunction(name)

# Response: 400 Bad Request
{
  "error": "Function 'unsupportedfunction' is not supported"
}
```

### Property Not Found

If a filter references a non-existent property, the query may return no results or an error:

```bash
# Non-existent property
GET /sta/v1.1/Things?$filter=nonExistentProperty eq 'value'

# Response: Empty result set or 400 Bad Request
```

## Testing Advanced Filters

### Using curl

```bash
# Simple filter
curl "http://localhost:5000/sta/v1.1/Observations?\$filter=result gt 20"

# Complex filter with URL encoding
curl "http://localhost:5000/sta/v1.1/Observations?\$filter=contains(name%2C%20%27Temp%27)%20and%20result%20gt%2020"
```

### Using Postman

1. Create a GET request to the entity endpoint
2. Add query parameter `$filter`
3. Enter your filter expression (Postman will handle URL encoding)
4. Send request

### URL Encoding

Special characters must be URL-encoded:

| Character | Encoded |
|-----------|---------|
| Space | `%20` or `+` |
| Single quote (') | `%27` |
| Comma (,) | `%2C` |
| Parentheses () | `%28` `%29` |
| Dollar sign ($) | `%24` or `\$` (in shell) |

## Implementation Details

### Parser Architecture

The filter parser uses recursive descent parsing to handle complex expressions:

1. **Lexical Analysis**: Tokenizes the input string
2. **Syntax Analysis**: Builds an abstract syntax tree (AST)
3. **SQL Generation**: Converts AST to PostgreSQL WHERE clause

### SQL Generation

Advanced filters are converted to parameterized SQL queries:

**OData Filter:**
```
contains(name, 'Weather') and result gt 20
```

**Generated SQL:**
```sql
WHERE name LIKE @p0 AND result > @p1
-- Parameters: { "p0": "%Weather%", "p1": 20 }
```

### Security

All filter values are parameterized to prevent SQL injection:

✅ **Safe** - Uses parameterized queries
✅ **Validated** - Input is parsed and validated
✅ **Sanitized** - Special characters are escaped

## References

- **OGC SensorThings API Specification**: http://docs.opengeospatial.org/is/18-088/18-088.html
- **OData 4.0 URI Conventions**: http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part2-url-conventions/odata-v4.0-errata03-os-part2-url-conventions-complete.html
- **PostgreSQL Functions**: https://www.postgresql.org/docs/current/functions.html
- **PostGIS Spatial Functions**: https://postgis.net/docs/reference.html

## Support

For issues or questions about advanced filtering:

1. Review this guide and examples
2. Check the OGC SensorThings specification
3. Consult the OData URI conventions documentation
4. Open an issue on the Honua Server repository
