# OData Operators and Functions - Quick Reference

**Last Updated:** 2025-10-30
**OData Version:** v4.01
**Implementation Status:** 85% Complete

---

## Comparison Operators

| Operator | Symbol | Example | SQL | Status |
|----------|--------|---------|-----|--------|
| Equal | `eq` | `price eq 100` | `price = 100` | WORKING |
| Not Equal | `ne` | `price ne 100` | `price <> 100` | WORKING |
| Greater Than | `gt` | `price gt 100` | `price > 100` | WORKING |
| Greater or Equal | `ge` | `price ge 100` | `price >= 100` | WORKING |
| Less Than | `lt` | `price lt 100` | `price < 100` | WORKING |
| Less or Equal | `le` | `price le 100` | `price <= 100` | WORKING |

### NULL Handling

```http
# Equals NULL becomes IS NULL
GET /odata/parcels?$filter=owner eq null
# SQL: owner IS NULL

# Not equals NULL becomes IS NOT NULL
GET /odata/parcels?$filter=owner ne null
# SQL: owner IS NOT NULL
```

---

## Logical Operators

| Operator | Example | SQL | Status |
|----------|---------|-----|--------|
| And | `active eq true and price gt 100` | `(active = true) AND (price > 100)` | WORKING |
| Or | `status eq 'new' or status eq 'pending'` | `(status = 'new') OR (status = 'pending')` | WORKING |
| Not | `not (active eq true)` | `NOT (active = true)` | WORKING |

### Precedence

```http
# Parentheses control evaluation order
GET /odata/parcels?$filter=(status eq 'new' or status eq 'pending') and active eq true

# Without parentheses, AND has higher precedence than OR
GET /odata/parcels?$filter=status eq 'new' or status eq 'pending' and active eq true
# Evaluates as: (status eq 'new') OR ((status eq 'pending') AND (active eq true))
```

---

## Arithmetic Operators

| Operator | Symbol | Example | SQL | Status |
|----------|--------|---------|-----|--------|
| Addition | `add` | `price add 10 gt 100` | `(price + 10) > 100` | WORKING |
| Subtraction | `sub` | `price sub 10 lt 50` | `(price - 10) < 50` | WORKING |
| Multiplication | `mul` | `price mul 2 eq 100` | `(price * 2) = 100` | WORKING |
| Division | `div` | `price div 2 gt 25` | `(price / 2) > 25` | WORKING |
| Modulo | `mod` | `count mod 2 eq 0` | `(count % 2) = 0` | WORKING |

### Examples

```http
# Calculate total price with tax
GET /odata/orders?$filter=price mul 1.08 gt 1000

# Check for even numbers
GET /odata/parcels?$filter=parcel_id mod 2 eq 0

# Apply discount
GET /odata/products?$filter=price sub discount le 50

# Complex calculation
GET /odata/orders?$filter=price mul quantity sub discount gt 10000
```

---

## String Functions

### Pattern Matching

| Function | Syntax | Example | Status |
|----------|--------|---------|--------|
| **contains** | `contains(field, 'substring')` | `contains(name, 'Park')` | WORKING |
| **startswith** | `startswith(field, 'prefix')` | `startswith(name, 'Main')` | WORKING |
| **endswith** | `endswith(field, 'suffix')` | `endswith(name, 'Street')` | WORKING |

```http
# Find all streets containing 'Oak'
GET /odata/streets?$filter=contains(name, 'Oak')

# Find all streets starting with 'Main'
GET /odata/streets?$filter=startswith(name, 'Main')

# Find all streets ending with 'Avenue'
GET /odata/streets?$filter=endswith(name, 'Avenue')
```

### String Manipulation

| Function | Syntax | Example | Status |
|----------|--------|---------|--------|
| **length** | `length(field)` | `length(name) gt 10` | WORKING |
| **tolower** | `tolower(field)` | `tolower(city) eq 'seattle'` | WORKING |
| **toupper** | `toupper(field)` | `toupper(state) eq 'WA'` | WORKING |
| **trim** | `trim(field)` | `trim(name) eq 'test'` | WORKING |
| **concat** | `concat(field1, field2)` | `concat(first, last) eq 'JohnDoe'` | WORKING |

```http
# Find long descriptions
GET /odata/parcels?$filter=length(description) gt 100

# Case-insensitive search
GET /odata/cities?$filter=tolower(name) eq 'san francisco'

# Uppercase state codes
GET /odata/addresses?$filter=toupper(state) eq 'CA'
```

### String Position

| Function | Syntax | Example | Status |
|----------|--------|---------|--------|
| **indexof** | `indexof(field, 'substring')` | `indexof(address, 'Main') gt 0` | WORKING |
| **substring** | `substring(field, start, length)` | `substring(code, 0, 2) eq 'CA'` | WORKING |

```http
# Find addresses with 'Main' in them (returns position)
GET /odata/addresses?$filter=indexof(address, 'Main') ge 0

# Extract first 2 characters of postal code
GET /odata/addresses?$filter=substring(postal_code, 0, 2) eq '98'
```

### Legacy (OData v3)

| Function | Syntax | Example | Status |
|----------|--------|---------|--------|
| **substringof** | `substringof('substring', field)` | `substringof('Park', name)` | WORKING |

```http
# OData v3 syntax (arguments reversed from contains)
GET /odata/streets?$filter=substringof('Oak', name)
```

---

## Date/Time Functions

### Date Part Extraction

| Function | Returns | Example | Status |
|----------|---------|---------|--------|
| **year** | Integer | `year(sale_date) eq 2024` | WORKING |
| **month** | Integer (1-12) | `month(sale_date) ge 6` | WORKING |
| **day** | Integer (1-31) | `day(sale_date) eq 15` | WORKING |
| **hour** | Integer (0-23) | `hour(timestamp) eq 14` | WORKING |
| **minute** | Integer (0-59) | `minute(timestamp) eq 30` | WORKING |
| **second** | Integer (0-59) | `second(timestamp) eq 45` | WORKING |
| **fractionalseconds** | Decimal | `fractionalseconds(timestamp) gt 0.5` | WORKING |

```http
# Find sales in 2024
GET /odata/parcels?$filter=year(sale_date) eq 2024

# Find sales in Q3-Q4
GET /odata/parcels?$filter=month(sale_date) ge 7

# Find transactions on the 15th
GET /odata/transactions?$filter=day(created_date) eq 15

# Find afternoon appointments
GET /odata/appointments?$filter=hour(scheduled_time) ge 12
```

### Date/Time Manipulation

| Function | Returns | Example | Status |
|----------|---------|---------|--------|
| **date** | Date | `date(timestamp) eq 2024-06-15` | WORKING |
| **time** | Time | `time(timestamp) gt 12:00:00` | WORKING |
| **now** | DateTimeOffset | `created_date gt now()` | WORKING |
| **totaloffsetminutes** | Integer | `totaloffsetminutes(timestamp)` | WORKING |
| **mindatetime** | DateTimeOffset | `date ne mindatetime()` | WORKING |
| **maxdatetime** | DateTimeOffset | `date ne maxdatetime()` | WORKING |

```http
# Find records created today
GET /odata/records?$filter=date(created_date) eq date(now())

# Find future appointments
GET /odata/appointments?$filter=scheduled_time gt now()

# Find afternoon time slots
GET /odata/timeslots?$filter=time(slot_time) ge 12:00:00

# Filter by timezone offset
GET /odata/events?$filter=totaloffsetminutes(event_time) eq -480
```

### Date Range Examples

```http
# Year range
GET /odata/parcels?$filter=year(sale_date) ge 2020 and year(sale_date) le 2024

# Month range (Q1)
GET /odata/sales?$filter=month(sale_date) ge 1 and month(sale_date) le 3

# Last 30 days
GET /odata/records?$filter=created_date ge now() sub duration'P30D'
```

---

## Math Functions

| Function | Description | Example | Status |
|----------|-------------|---------|--------|
| **round** | Round to nearest integer | `round(price) eq 100` | WORKING |
| **floor** | Round down | `floor(price) lt 100` | WORKING |
| **ceiling** | Round up | `ceiling(price) gt 100` | WORKING |

```http
# Find prices that round to 100
GET /odata/products?$filter=round(price) eq 100

# Find prices with floor less than 100
GET /odata/products?$filter=floor(price) lt 100

# Find prices with ceiling greater than 100
GET /odata/products?$filter=ceiling(price) gt 100

# Calculate price per unit (rounded)
GET /odata/products?$filter=round(price div quantity) le 10
```

---

## Geospatial Functions

| Function | Description | Example | Status |
|----------|-------------|---------|--------|
| **geo.distance** | Distance between points | `geo.distance(location, geography'POINT(-122 47)') lt 5000` | WORKING |
| **geo.length** | Length of line | `geo.length(path) gt 1000` | WORKING |
| **geo.intersects** | Spatial intersection | `geo.intersects(geom, geography'POLYGON(...)')` | WORKING |

```http
# Find features within 5km of a point
GET /odata/parcels?$filter=geo.distance(location, geography'POINT(-122.33 47.61)') le 5000

# Find long roads
GET /odata/roads?$filter=geo.length(geometry) gt 10000

# Find features intersecting a polygon
GET /odata/parcels?$filter=geo.intersects(geometry, geography'POLYGON((-122.5 47.5, ...))')
```

---

## Complex Filter Examples

### Multiple Conditions

```http
# AND + OR with precedence
GET /odata/parcels?$filter=(status eq 'active' or status eq 'pending') and assessed_value gt 500000

# Nested conditions
GET /odata/parcels?$filter=city eq 'Seattle' and (year(sale_date) eq 2024 or assessed_value gt 1000000)
```

### Combining Functions

```http
# String + Date
GET /odata/parcels?$filter=startswith(owner, 'Smith') and year(sale_date) eq 2024

# String + Arithmetic
GET /odata/products?$filter=contains(name, 'Premium') and price mul 1.1 lt 1000

# Geospatial + Date + String
GET /odata/incidents?$filter=geo.distance(location, geography'POINT(-122 47)') lt 1000 and year(created_date) eq 2024 and contains(type, 'Traffic')
```

### Case-Insensitive Searches

```http
# Convert to lowercase for comparison
GET /odata/cities?$filter=tolower(name) eq 'san francisco'

# Multiple case-insensitive conditions
GET /odata/parcels?$filter=tolower(city) eq 'seattle' and tolower(owner) eq 'smith'
```

---

## Not Implemented (Future)

### Collection Operators

```http
# These require lambda expressions - NOT YET IMPLEMENTED
GET /odata/customers?$filter=orders/any(o: o/total gt 1000)
GET /odata/customers?$filter=orders/all(o: o/status eq 'completed')
```

### Type Functions

```http
# Type checking - NOT YET IMPLEMENTED
GET /odata/entities?$filter=isof(Microsoft.Models.Customer)
GET /odata/entities?$filter=cast(Microsoft.Models.Customer)/vipStatus eq true
```

---

## Performance Tips

### Use Indexes

```sql
-- For string functions
CREATE INDEX idx_name_lower ON parcels (LOWER(name));

-- For date part extraction
CREATE INDEX idx_sale_year ON parcels ((EXTRACT(YEAR FROM sale_date)));

-- For geospatial queries
CREATE INDEX idx_location_gist ON parcels USING GIST (location);
```

### Avoid Over-Filtering

```http
# BAD: Multiple function calls
GET /odata/parcels?$filter=tolower(city) eq 'seattle' and tolower(owner) eq 'smith'

# BETTER: Use database collation
GET /odata/parcels?$filter=city eq 'Seattle' and owner eq 'Smith'
# (If database is case-insensitive)
```

### Combine Filters Efficiently

```http
# BAD: Separate requests
GET /odata/parcels?$filter=city eq 'Seattle'
GET /odata/parcels?$filter=year(sale_date) eq 2024

# GOOD: Single request
GET /odata/parcels?$filter=city eq 'Seattle' and year(sale_date) eq 2024
```

---

## Error Handling

### Invalid Operators

```http
# Unknown operator
GET /odata/parcels?$filter=price between 100 and 200
# ERROR: "Binary operator 'between' is not supported yet."
```

### Invalid Functions

```http
# Unknown function
GET /odata/parcels?$filter=reverse(name) eq 'elttaeS'
# ERROR: "Filter function 'reverse' is not supported yet."
```

### Type Mismatches

```http
# String comparison on number field
GET /odata/parcels?$filter=price eq 'expensive'
# ERROR: Type conversion failed
```

---

## Additional Resources

- [OData v4 Specification](https://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html)
- [OData URL Conventions](https://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_SystemQueryOptions)
- [Implementation Status](/docs/review/2025-02/ODATA_OPERATORS_FIX_COMPLETE.md)
