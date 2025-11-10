# Apache AGE Integration for Honua.Server

**Status:** Proof-of-Concept Implementation
**Date:** November 10, 2025
**Phase:** 1.1 - AEC Technical Enablers

---

## Overview

This document describes the Apache AGE (A Graph Extension for PostgreSQL) integration into Honua.Server, providing native graph database capabilities for managing complex hierarchical relationships in AEC (Architecture, Engineering, Construction) workflows.

### What is Apache AGE?

Apache AGE is an open-source PostgreSQL extension that adds graph database capabilities using the Cypher query language (from Neo4j). It allows storing and querying graph data alongside traditional relational and spatial data in a single PostgreSQL database.

### Why Apache AGE for Honua?

- **No Cloud Dependency**: Runs entirely within your existing PostgreSQL database
- **No Additional Infrastructure**: No separate graph database server required
- **Unified Queries**: Combine spatial (PostGIS), relational (SQL), and graph (Cypher) queries
- **Industry Standard**: Cypher is the most widely-used graph query language
- **Open Source**: Apache 2.0 licensed, no vendor lock-in

---

## Installation & Setup

### 1. Install Apache AGE Extension (PostgreSQL 15+)

Apache AGE requires PostgreSQL 15 or higher with the AGE extension installed.

#### Option A: Using Docker (Recommended for Development)

```bash
# Use the official Apache AGE Docker image
docker run -d \
  --name honua-postgres-age \
  -e POSTGRES_PASSWORD=yourpassword \
  -e POSTGRES_DB=honua \
  -p 5432:5432 \
  apache/age:PG15

# Or add to existing docker-compose.yml
services:
  postgres:
    image: apache/age:PG15
    environment:
      POSTGRES_PASSWORD: yourpassword
      POSTGRES_DB: honua
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
```

#### Option B: Install AGE on Existing PostgreSQL

**Ubuntu/Debian:**
```bash
# Install PostgreSQL 15+ if not already installed
sudo apt-get install postgresql-15 postgresql-server-dev-15

# Clone and build Apache AGE
git clone https://github.com/apache/age.git
cd age
git checkout PG15
make
sudo make install

# Enable the extension in your database
psql -U postgres -d honua -c "CREATE EXTENSION IF NOT EXISTS age;"
```

**macOS (via Homebrew):**
```bash
# Install PostgreSQL 15+
brew install postgresql@15

# Build Apache AGE from source
git clone https://github.com/apache/age.git
cd age
git checkout PG15
make PG_CONFIG=/opt/homebrew/opt/postgresql@15/bin/pg_config
sudo make install PG_CONFIG=/opt/homebrew/opt/postgresql@15/bin/pg_config

# Enable the extension
psql -U postgres -d honua -c "CREATE EXTENSION IF NOT EXISTS age;"
```

**Windows:**
See [Apache AGE Documentation](https://age.apache.org/age-manual/master/intro/setup.html) for Windows installation instructions.

### 2. Verify Installation

```sql
-- Connect to your database
psql -U postgres -d honua

-- Check if AGE is installed
SELECT * FROM pg_extension WHERE extname = 'age';

-- Load AGE into search path
SET search_path = ag_catalog, "$user", public;

-- Create a test graph
SELECT create_graph('test_graph');

-- Create a test node
SELECT * FROM cypher('test_graph', $$
    CREATE (n:Person {name: 'Alice', age: 30})
    RETURN n
$$) AS (result agtype);

-- Clean up
SELECT drop_graph('test_graph', true);
```

If all commands succeed, Apache AGE is properly installed!

### 3. Configure Honua.Server

Add the following to your `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Username=postgres;Password=yourpassword;Database=honua"
  },
  "GraphDatabase": {
    "Enabled": true,
    "ConnectionString": null,
    "DefaultGraphName": "honua_graph",
    "AutoCreateGraph": true,
    "EnableSchemaInitialization": true,
    "CommandTimeoutSeconds": 30,
    "MaxRetryAttempts": 3,
    "EnableQueryCache": true,
    "QueryCacheExpirationMinutes": 5,
    "LogQueries": false,
    "MaxTraversalDepth": 10
  }
}
```

**Configuration Options:**

- `Enabled`: Enable/disable graph database functionality
- `ConnectionString`: PostgreSQL connection string (uses `DefaultConnection` if null)
- `DefaultGraphName`: Default graph name for operations
- `AutoCreateGraph`: Automatically create the default graph on startup
- `EnableSchemaInitialization`: Initialize graph schema on startup
- `CommandTimeoutSeconds`: Timeout for graph operations
- `MaxRetryAttempts`: Number of retries for transient failures
- `EnableQueryCache`: Enable query result caching
- `QueryCacheExpirationMinutes`: Cache expiration time
- `LogQueries`: Log Cypher queries for debugging
- `MaxTraversalDepth`: Maximum depth for graph traversals (prevents infinite loops)

### 4. Register Services in Startup

The graph database service is automatically registered if the `GraphDatabase:Enabled` configuration is set to `true`. No additional code changes are required.

```csharp
// Services are auto-registered via dependency injection
// IGraphDatabaseService is available for injection
```

---

## Quick Start Examples

### Example 1: Building Hierarchy

Creating a building with floors and rooms:

```csharp
// Using the service directly
var buildingNode = await graphService.CreateNodeAsync(new GraphNode("Building")
{
    Properties = new Dictionary<string, object>
    {
        ["id"] = "bldg-001",
        ["name"] = "Office Tower A",
        ["address"] = "123 Main St",
        ["floors_count"] = 10
    }
});

var floorNode = await graphService.CreateNodeAsync(new GraphNode("Floor")
{
    Properties = new Dictionary<string, object>
    {
        ["level"] = 1,
        ["area_sqm"] = 1200
    }
});

// Create relationship
var containsEdge = await graphService.CreateEdgeAsync(new GraphEdge
{
    Type = "CONTAINS",
    StartNodeId = buildingNode.Id.Value,
    EndNodeId = floorNode.Id.Value,
    Properties = new Dictionary<string, object>
    {
        ["sequence_order"] = 1
    }
});
```

### Example 2: Using the REST API

**Create a building node:**
```bash
curl -X POST http://localhost:5000/api/graph/nodes \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Building",
    "properties": {
      "id": "bldg-001",
      "name": "Office Tower A",
      "address": "123 Main St"
    }
  }'
```

**Create a floor node:**
```bash
curl -X POST http://localhost:5000/api/graph/nodes \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Floor",
    "properties": {
      "level": 1,
      "area_sqm": 1200
    }
  }'
```

**Create a relationship:**
```bash
curl -X POST http://localhost:5000/api/graph/relationships \
  -H "Content-Type: application/json" \
  -d '{
    "sourceNodeId": 1,
    "targetNodeId": 2,
    "relationshipType": "CONTAINS",
    "properties": {
      "sequence_order": 1
    }
  }'
```

### Example 3: Cypher Queries

**Find all rooms in a building:**
```bash
curl -X POST http://localhost:5000/api/graph/query \
  -H "Content-Type: application/json" \
  -d '{
    "query": "MATCH (b:Building)-[:CONTAINS*]->(r:Room) WHERE b.id = '\''bldg-001'\'' RETURN r"
  }'
```

**Find all equipment on a specific floor:**
```bash
curl -X POST http://localhost:5000/api/graph/query \
  -H "Content-Type: application/json" \
  -d '{
    "query": "MATCH (f:Floor {level: 1})-[:CONTAINS*]->(e:Equipment) RETURN e"
  }'
```

### Example 4: Graph Traversal

**Traverse from a building to all contained entities:**
```bash
curl -X POST http://localhost:5000/api/graph/traverse \
  -H "Content-Type: application/json" \
  -d '{
    "startNodeId": 1,
    "relationshipTypes": ["CONTAINS", "HAS"],
    "direction": "Outgoing",
    "maxDepth": 5
  }'
```

**Find shortest path between two nodes:**
```bash
curl "http://localhost:5000/api/graph/shortest-path?startNodeId=1&endNodeId=10&maxDepth=10"
```

---

## Use Cases for AEC Workflows

### 1. Building Information Modeling (BIM)

**Hierarchy Example:**
```
Building → Floor → Room → Equipment
    ↓
Building → System → Subsystem → Component
```

**Cypher Query:**
```cypher
// Find all HVAC equipment in the building
MATCH (b:Building {id: 'bldg-001'})-[:CONTAINS*]->(e:Equipment)
WHERE e.type = 'hvac'
RETURN e
```

### 2. Utility Network Management

**Network Example:**
```
WaterMain → Valve → ServiceLine → Meter
    ↓
ElectricalSubstation → Transformer → DistributionLine → Customer
```

**Cypher Query:**
```cypher
// Find all downstream customers affected by a valve closure
MATCH (v:Valve {id: 'valve-123'})-[:FEEDS*]->(c:Customer)
RETURN c
```

### 3. Construction Dependencies

**Dependency Example:**
```
Foundation → Columns → Beams → Slab → Walls → Roof
```

**Cypher Query:**
```cypher
// Find all tasks that must be completed before starting walls
MATCH path = (start:Task {type: 'foundation'})-[:PRECEDES*]->(walls:Task {type: 'walls'})
RETURN path
```

### 4. Document Relationships

**Document Example:**
```
Project → DrawingSet → Sheet → Detail → Element
    ↓
Specification → Section → Clause
```

**Cypher Query:**
```cypher
// Find all drawings that reference a specific equipment type
MATCH (d:Drawing)-[:DEPICTS]->(e:Equipment {type: 'chiller'})
RETURN d
```

---

## API Reference

### Node Operations

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/graph/nodes` | POST | Create a new node |
| `/api/graph/nodes/{id}` | GET | Get node by ID |
| `/api/graph/nodes` | GET | Find nodes by label |
| `/api/graph/nodes/{id}` | PUT | Update node properties |
| `/api/graph/nodes/{id}` | DELETE | Delete node and relationships |
| `/api/graph/nodes/batch` | POST | Create multiple nodes |

### Relationship Operations

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/graph/relationships` | POST | Create a new relationship |
| `/api/graph/relationships/{id}` | GET | Get relationship by ID |
| `/api/graph/nodes/{nodeId}/relationships` | GET | Get node relationships |
| `/api/graph/relationships/{id}` | DELETE | Delete relationship |

### Query Operations

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/graph/query` | POST | Execute Cypher query |
| `/api/graph/traverse` | POST | Traverse graph from node |
| `/api/graph/shortest-path` | GET | Find shortest path |

### Graph Management

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/graph/graphs/{name}` | POST | Create a graph |
| `/api/graph/graphs/{name}/exists` | GET | Check if graph exists |
| `/api/graph/graphs/{name}` | DELETE | Delete a graph |

---

## Common Cypher Patterns

### Pattern 1: Create Hierarchy

```cypher
// Create a building hierarchy
CREATE (b:Building {id: 'bldg-1', name: 'Tower A'})
  -[:CONTAINS]->(f:Floor {level: 1})
  -[:CONTAINS]->(r:Room {number: '101'})
  -[:HAS]->(e:Equipment {id: 'hvac-001', type: 'air_handler'})
```

### Pattern 2: Find All Children

```cypher
// Find all descendants of a building (any depth)
MATCH (b:Building {id: 'bldg-1'})-[:CONTAINS*]->(child)
RETURN child
```

### Pattern 3: Find Specific Relationship

```cypher
// Find direct children only
MATCH (b:Building {id: 'bldg-1'})-[:CONTAINS]->(floor:Floor)
RETURN floor
```

### Pattern 4: Filter by Properties

```cypher
// Find all rooms with area > 50 sqm
MATCH (b:Building)-[:CONTAINS*]->(r:Room)
WHERE r.area_sqm > 50
RETURN r
```

### Pattern 5: Aggregation

```cypher
// Count equipment by type per floor
MATCH (f:Floor)-[:CONTAINS*]->(e:Equipment)
RETURN f.level, e.type, COUNT(e) AS count
```

### Pattern 6: Shortest Path

```cypher
// Find shortest path between two nodes
MATCH path = shortestPath((start:Building)-[:CONTAINS*]-(end:Equipment))
WHERE start.id = 'bldg-1' AND end.id = 'hvac-001'
RETURN path
```

### Pattern 7: Network Flow

```cypher
// Find all downstream connections from a source
MATCH (source:WaterMain {id: 'wm-001'})-[:FEEDS*]->(downstream)
RETURN downstream
```

---

## Performance Considerations

### 1. Indexing

Apache AGE automatically indexes node and edge IDs. For property-based queries, consider adding PostgreSQL indexes:

```sql
-- Add index for frequently queried properties
CREATE INDEX idx_building_id ON ag_catalog."honua_graph_vertex"
  ((properties->>'id')) WHERE label = 'Building';

CREATE INDEX idx_floor_level ON ag_catalog."honua_graph_vertex"
  ((properties->>'level')) WHERE label = 'Floor';
```

### 2. Query Optimization

- **Limit Traversal Depth**: Always specify a maximum depth (e.g., `[:CONTAINS*1..5]`)
- **Use Property Filters Early**: Filter nodes before traversing relationships
- **Return Only What You Need**: Don't return entire paths if you only need specific properties

### 3. Batch Operations

For large data imports, use batch operations:

```csharp
var nodes = new List<GraphNode>();
for (int i = 0; i < 1000; i++)
{
    nodes.Add(new GraphNode("Equipment")
    {
        Properties = new Dictionary<string, object>
        {
            ["id"] = $"eq-{i}",
            ["type"] = "sensor"
        }
    });
}

var createdNodes = await graphService.CreateNodesAsync(nodes);
```

---

## Testing

### Unit Tests

Example unit tests can be found in:
```
/tests/Honua.Server.Core.Tests/Services/GraphDatabaseServiceTests.cs
```

### Integration Tests

Test the full stack including PostgreSQL and Apache AGE:

```csharp
[Fact]
public async Task CreateBuildingHierarchy_ShouldSucceed()
{
    // Arrange
    var building = new GraphNode("Building") { Properties = new() { ["name"] = "Test Building" } };
    var floor = new GraphNode("Floor") { Properties = new() { ["level"] = 1 } };

    // Act
    var createdBuilding = await _graphService.CreateNodeAsync(building);
    var createdFloor = await _graphService.CreateNodeAsync(floor);
    var relationship = await _graphService.CreateEdgeAsync(new GraphEdge
    {
        Type = "CONTAINS",
        StartNodeId = createdBuilding.Id.Value,
        EndNodeId = createdFloor.Id.Value
    });

    // Assert
    Assert.NotNull(createdBuilding.Id);
    Assert.NotNull(createdFloor.Id);
    Assert.NotNull(relationship.Id);
}
```

---

## Troubleshooting

### Issue: "Extension 'age' not found"

**Solution:** Install Apache AGE extension in PostgreSQL:
```sql
CREATE EXTENSION IF NOT EXISTS age;
```

### Issue: "Graph does not exist"

**Solution:** Ensure `AutoCreateGraph` is enabled in configuration, or manually create the graph:
```sql
SELECT create_graph('honua_graph');
```

### Issue: "Connection timeout"

**Solution:** Increase `CommandTimeoutSeconds` in configuration or optimize your Cypher queries.

### Issue: "Out of memory during large traversal"

**Solution:** Reduce `MaxTraversalDepth` or add more specific filters to your queries.

---

## Next Steps

### Phase 1.2: Complex 3D Geometry Support

The next phase will integrate OpenCascade Technology (OCCT) for:
- Triangle mesh support (OBJ, STL, glTF)
- B-Rep solids
- Parametric surfaces (NURBS)
- CSG operations

### Phase 1.3: IFC (BIM) File Parsing

Integration of IfcOpenShell for:
- IFC file import/export
- Automatic graph relationship creation from IFC relationships
- Geometry extraction and conversion

---

## References

- [Apache AGE Official Documentation](https://age.apache.org/)
- [Apache AGE GitHub Repository](https://github.com/apache/age)
- [Apache AGE .NET Client Library](https://github.com/Allison-E/pg-age)
- [Cypher Query Language Reference](https://neo4j.com/docs/cypher-manual/current/)
- [AEC Technical Enablers Proposal](/AEC_TECHNICAL_ENABLERS_PROPOSAL.md)

---

## Support

For issues or questions:
1. Check the [Apache AGE Documentation](https://age.apache.org/)
2. Review existing [GitHub Issues](https://github.com/apache/age/issues)
3. Consult the [Honua.Server Documentation](/)

---

**Last Updated:** November 10, 2025
**Version:** 1.0.0 (Proof-of-Concept)
