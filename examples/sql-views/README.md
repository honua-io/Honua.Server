# SQL Views Examples

This directory contains example SQL view configurations for Honua.Server.

## Overview

SQL Views (Virtual Layers) allow you to create dynamic layers from SQL queries instead of physical database tables. This is useful for:

- **Filtering**: Create pre-filtered views of large datasets
- **Aggregation**: Expose aggregate statistics as layers
- **Joins**: Combine multiple tables into a single layer
- **Computed Fields**: Include calculated values in your layers
- **Parameterized Queries**: Allow users to customize queries via URL parameters

## Examples

### high-population-cities.json

Complete example demonstrating three different SQL view use cases:

1. **Simple Filtering**: `high_population_cities`
   - Filters cities by minimum population and region
   - Demonstrates basic parameter validation (min/max, allowed values)
   - Query: `/ogc/collections/high_population_cities/items?min_population=500000&region=east`

2. **Aggregation with JOIN**: `city_statistics`
   - Aggregates city data by country
   - Demonstrates GROUP BY and aggregate functions
   - Shows temporal filtering by continent
   - Query: `/ogc/collections/city_statistics/items?continent=Asia&min_cities=10`

3. **Temporal Filtering**: `temporal_events`
   - Filters events by date range
   - Demonstrates date parameters
   - Shows security filters (`deleted_at IS NULL AND published = true`)
   - Query: `/ogc/collections/temporal_events/items?start_date=2024-01-01&end_date=2024-12-31&event_type=conference`

## Prerequisites

Before using these examples, you need:

1. **Database Setup**: Create the required tables:

```sql
-- Cities table
CREATE TABLE cities (
    city_id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    population INTEGER,
    region VARCHAR(50),
    country VARCHAR(100),
    country_code VARCHAR(3),
    location GEOMETRY(Point, 4326) NOT NULL
);

CREATE INDEX idx_cities_location ON cities USING GIST (location);
CREATE INDEX idx_cities_population ON cities (population);
CREATE INDEX idx_cities_region ON cities (region);

-- Countries table
CREATE TABLE countries (
    country_code VARCHAR(3) PRIMARY KEY,
    country_name VARCHAR(255) NOT NULL,
    continent VARCHAR(50),
    centroid GEOMETRY(Point, 4326) NOT NULL
);

CREATE INDEX idx_countries_centroid ON countries USING GIST (centroid);

-- Events table
CREATE TABLE events (
    event_id SERIAL PRIMARY KEY,
    event_name VARCHAR(255) NOT NULL,
    event_type VARCHAR(50),
    event_date DATE,
    location GEOMETRY(Point, 4326) NOT NULL,
    deleted_at TIMESTAMP,
    published BOOLEAN DEFAULT true
);

CREATE INDEX idx_events_location ON events USING GIST (location);
CREATE INDEX idx_events_date ON events (event_date);
```

2. **Sample Data**: Insert some test data:

```sql
-- Insert sample cities
INSERT INTO cities (name, population, region, country, country_code, location) VALUES
('San Francisco', 873965, 'west', 'USA', 'USA', ST_SetSRID(ST_MakePoint(-122.4194, 37.7749), 4326)),
('New York', 8336817, 'east', 'USA', 'USA', ST_SetSRID(ST_MakePoint(-74.0060, 40.7128), 4326)),
('Chicago', 2693976, 'central', 'USA', 'USA', ST_SetSRID(ST_MakePoint(-87.6298, 41.8781), 4326)),
('Los Angeles', 3979576, 'west', 'USA', 'USA', ST_SetSRID(ST_MakePoint(-118.2437, 34.0522), 4326)),
('Houston', 2320268, 'south', 'USA', 'USA', ST_SetSRID(ST_MakePoint(-95.3698, 29.7604), 4326));

-- Insert sample countries
INSERT INTO countries (country_code, country_name, continent, centroid) VALUES
('USA', 'United States', 'North America', ST_SetSRID(ST_MakePoint(-95.7129, 37.0902), 4326)),
('CAN', 'Canada', 'North America', ST_SetSRID(ST_MakePoint(-106.3468, 56.1304), 4326)),
('MEX', 'Mexico', 'North America', ST_SetSRID(ST_MakePoint(-102.5528, 23.6345), 4326));

-- Insert sample events
INSERT INTO events (event_name, event_type, event_date, location, published) VALUES
('Tech Conference 2024', 'conference', '2024-06-15', ST_SetSRID(ST_MakePoint(-122.4194, 37.7749), 4326), true),
('GIS Workshop', 'workshop', '2024-08-20', ST_SetSRID(ST_MakePoint(-74.0060, 40.7128), 4326), true),
('Developer Meetup', 'meetup', '2024-09-10', ST_SetSRID(ST_MakePoint(-118.2437, 34.0522), 4326), true);
```

## Usage

1. **Load the configuration**:
   ```bash
   honua metadata load examples/sql-views/high-population-cities.json
   ```

2. **Test the layers**:

   Basic query:
   ```bash
   curl "http://localhost:5000/ogc/collections/high_population_cities/items"
   ```

   With parameters:
   ```bash
   curl "http://localhost:5000/ogc/collections/high_population_cities/items?min_population=2000000&region=east"
   ```

   Aggregated data:
   ```bash
   curl "http://localhost:5000/ogc/collections/city_statistics/items?continent=North%20America"
   ```

   Temporal filtering:
   ```bash
   curl "http://localhost:5000/ogc/collections/temporal_events/items?start_date=2024-06-01&end_date=2024-12-31&event_type=conference"
   ```

## Security Features Demonstrated

### Parameter Validation
- **Type checking**: Parameters are validated and converted to the correct type
- **Range validation**: Numeric parameters have min/max bounds
- **Allowed values**: Enum-like validation for region, continent, event_type
- **Pattern matching**: Could add regex patterns for codes (e.g., country codes)

### SQL Injection Prevention
- **Parameterized queries**: All parameters use parameterized queries
- **No string concatenation**: Values are never concatenated into SQL
- **SELECT only**: Only SELECT statements are allowed
- **Keyword blocking**: Dangerous keywords (DROP, DELETE, etc.) are blocked

### Security Filters
The `temporal_events` layer demonstrates security filters that are always applied:
```json
"securityFilter": "deleted_at IS NULL AND published = true"
```

This ensures:
- Deleted events are never shown
- Only published events are visible
- Users cannot bypass these filters via parameters

## Query Examples

### Simple Filtering

Get cities with population > 1 million in the east region:
```
GET /ogc/collections/high_population_cities/items?min_population=1000000&region=east
```

Response includes only cities matching both criteria.

### Aggregation

Get statistics for countries in Asia with at least 5 cities:
```
GET /ogc/collections/city_statistics/items?continent=Asia&min_cities=5
```

Response includes:
- Country code and name
- Number of cities
- Total population
- Average city population
- Country centroid geometry

### Temporal Filtering

Get all conferences in the second half of 2024:
```
GET /ogc/collections/temporal_events/items?start_date=2024-07-01&end_date=2024-12-31&event_type=conference
```

Response includes only published events (deleted events are filtered out by security filter).

## Customization

You can modify these examples to match your data:

1. **Change table names**: Update the SQL queries to reference your tables
2. **Add more parameters**: Add new parameter definitions for additional filters
3. **Modify validation**: Adjust min/max values, patterns, or allowed values
4. **Add security filters**: Add WHERE clauses that are always applied

## Performance Tips

1. **Create indexes** on fields used in WHERE clauses
2. **Set appropriate timeouts** for complex queries
3. **Use EXPLAIN** to analyze query performance
4. **Consider materialized views** for expensive aggregations
5. **Limit result sets** using the `limit` parameter

## Troubleshooting

### "Layer must have either Storage.Table or SqlView defined"
Make sure your layer has a `sqlView` property, not both `storage` and `sqlView`.

### "Parameter validation failed"
Check that:
- Parameter values match the expected type
- Numeric values are within min/max bounds
- String values are in the allowed values list
- Patterns match the regex validation

### "SQL view must include the 'city_id' field"
Ensure your SELECT clause includes the field specified in `idField`.

### Query timeout
Either:
- Increase `timeoutSeconds` in the SQL view definition
- Optimize the SQL query
- Add appropriate database indexes

## Next Steps

- Read the [SQL Views documentation](../../docs/SQL_VIEWS.md)
- Review [SQL View Security Tests](../../tests/Honua.Server.Core.Tests.Data/Data/SqlViewSecurityTests.cs)
- Check [SQL View Integration Tests](../../tests/Honua.Server.Core.Tests.Data/Data/SqlViewIntegrationTests.cs)

## Support

For questions or issues with SQL views, please:
1. Check the [documentation](../../docs/SQL_VIEWS.md)
2. Review the test files for examples
3. Open an issue on GitHub
