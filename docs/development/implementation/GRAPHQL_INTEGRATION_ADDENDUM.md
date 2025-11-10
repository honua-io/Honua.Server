# GraphQL Integration Addendum
**AEC Technical Enablers - Phase 1.1B**

**Date:** November 10, 2025
**Status:** Addendum to AEC Technical Enablers Proposal
**Related:** Apache AGE Integration (Phase 1.1)

---

## Executive Summary

With the addition of Apache AGE graph database capabilities, **GraphQL becomes a natural and expected API standard** to expose graph data. GraphQL is particularly well-suited for:
- Complex hierarchical queries (building → floor → room → equipment)
- Relationship traversal (get all connected nodes)
- Flexible data fetching (avoid over/under-fetching)
- Self-documenting APIs (schema introspection)
- Real-time subscriptions (construction progress updates)

**Recommendation:** Add GraphQL as **Phase 1.1B** to run in parallel with Apache AGE integration.

---

## Why GraphQL for AEC?

### Problem with REST for Graph Data

**REST Limitations:**
```
# To get building with floors with rooms with equipment:
GET /api/buildings/123
GET /api/buildings/123/floors
GET /api/floors/1/rooms
GET /api/floors/2/rooms
...
GET /api/rooms/101/equipment
GET /api/rooms/102/equipment
...
# Result: 20+ HTTP requests, massive over-fetching
```

**GraphQL Solution:**
```graphql
query {
  building(id: "123") {
    name
    floors {
      level
      rooms {
        number
        equipment {
          id
          type
          status
        }
      }
    }
  }
}
# Result: Single request, exact data needed
```

### AEC Use Cases for GraphQL

1. **Building Information Queries**
   - Get entire building hierarchy in one request
   - Traverse relationships (walls connected to rooms, doors connecting spaces)
   - Filter by properties (all mechanical equipment on floor 3)

2. **Construction Progress Tracking**
   - Real-time subscriptions for status updates
   - Query work packages with dependencies
   - Get critical path for scheduling

3. **Utility Network Traversal**
   - Trace water/power/gas networks
   - Find all downstream components from a valve
   - Identify impact zones for maintenance

4. **Document Relationships**
   - Get all drawings that reference a specific element
   - Find documents by spatial extent
   - Version history queries

---

## Proposed Implementation

### Technology Stack

**Recommended: HotChocolate 13+**
- Modern .NET GraphQL server
- Excellent .NET 9 support
- Strong typing with C# classes
- Built-in subscriptions (WebSocket/SSE)
- Apollo Federation support
- Authorization directives
- Open source (MIT license)

**Alternatives Considered:**
- ❌ GraphQL.NET - Less active, older API
- ❌ Raw implementation - Too much work

### Architecture

```
┌─────────────────────────────────────────────────┐
│            GraphQL API Layer                     │
│  (HotChocolate Server + Schema Definition)      │
└────────────┬────────────────────────────────────┘
             │
    ┌────────┴────────┐
    │                 │
┌───▼──────────┐  ┌──▼──────────────┐
│   REST API   │  │   Apache AGE    │
│  (existing)  │  │  Graph Database │
└──────────────┘  └─────────────────┘
    │                 │
    └────────┬────────┘
             │
    ┌────────▼────────┐
    │   PostgreSQL    │
    │  + PostGIS      │
    └─────────────────┘
```

### Core GraphQL Schema

```graphql
# Building hierarchy types
type Building {
  id: ID!
  name: String!
  address: String
  floors: [Floor!]!
  totalArea: Float

  # Relationships
  documents: [Document!]!
  equipment: [Equipment!]!
}

type Floor {
  id: ID!
  level: Int!
  area: Float!
  building: Building!
  rooms: [Room!]!
}

type Room {
  id: ID!
  number: String!
  type: RoomType!
  area: Float!
  floor: Floor!
  equipment: [Equipment!]!
  walls: [Wall!]!
}

type Equipment {
  id: ID!
  type: String!
  manufacturer: String
  model: String
  status: EquipmentStatus!
  location: Room

  # Relationships
  feeds: [Equipment!]! # For utility networks
  fedBy: Equipment
}

# Query root
type Query {
  # Single entity queries
  building(id: ID!): Building
  room(id: ID!): Room
  equipment(id: ID!): Equipment

  # List queries with filtering
  buildings(filter: BuildingFilter, limit: Int, offset: Int): [Building!]!

  # Graph traversal
  findPath(from: ID!, to: ID!, relationshipType: String): [GraphNode!]!

  # Spatial queries
  buildingsInExtent(bbox: BoundingBoxInput!): [Building!]!
}

# Mutations
type Mutation {
  createBuilding(input: CreateBuildingInput!): Building!
  updateRoom(id: ID!, input: UpdateRoomInput!): Room!
  deleteEquipment(id: ID!): Boolean!

  # Relationship management
  connectEquipment(from: ID!, to: ID!, type: String!): Equipment!
}

# Subscriptions for real-time updates
type Subscription {
  buildingUpdated(buildingId: ID!): Building!
  equipmentStatusChanged(equipmentId: ID!): Equipment!
  constructionProgressUpdated: ConstructionProgress!
}

# Enums
enum RoomType {
  OFFICE
  CONFERENCE_ROOM
  BATHROOM
  STORAGE
  MECHANICAL
}

enum EquipmentStatus {
  OPERATIONAL
  MAINTENANCE
  FAILED
  OFFLINE
}

# Input types
input BuildingFilter {
  name: String
  minArea: Float
  maxArea: Float
}

input BoundingBoxInput {
  minX: Float!
  minY: Float!
  maxX: Float!
  maxY: Float!
}
```

### Implementation Files

#### 1. GraphQL Schema Types
**Location:** `src/Honua.Server.GraphQL/Types/`

```csharp
// BuildingType.cs
public class BuildingType : ObjectType<Building>
{
    protected override void Configure(IObjectTypeDescriptor<Building> descriptor)
    {
        descriptor.Field(b => b.Id).Type<NonNullType<IdType>>();
        descriptor.Field(b => b.Name).Type<NonNullType<StringType>>();

        // Relationship resolver
        descriptor.Field("floors")
            .Type<NonNullType<ListType<NonNullType<FloorType>>>>()
            .ResolveWith<BuildingResolvers>(r => r.GetFloorsAsync(default!, default!));
    }
}

// BuildingResolvers.cs
public class BuildingResolvers
{
    public async Task<IEnumerable<Floor>> GetFloorsAsync(
        [Parent] Building building,
        [Service] IGraphDatabaseService graphService)
    {
        var result = await graphService.ExecuteCypherQueryAsync($@"
            MATCH (b:Building {{id: '{building.Id}'}})-[:CONTAINS]->(f:Floor)
            RETURN f
        ");

        return result.Nodes.Select(n => MapToFloor(n));
    }
}
```

#### 2. Query Root
**Location:** `src/Honua.Server.GraphQL/Queries/QueryType.cs`

```csharp
public class QueryType : ObjectType
{
    protected override void Configure(IObjectTypeDescriptor descriptor)
    {
        descriptor.Field("building")
            .Argument("id", a => a.Type<NonNullType<IdType>>())
            .Type<BuildingType>()
            .ResolveWith<QueryResolvers>(r => r.GetBuildingAsync(default!, default!));

        descriptor.Field("findPath")
            .Argument("from", a => a.Type<NonNullType<IdType>>())
            .Argument("to", a => a.Type<NonNullType<IdType>>())
            .Argument("relationshipType", a => a.Type<StringType>())
            .Type<ListType<GraphNodeType>>()
            .ResolveWith<QueryResolvers>(r => r.FindPathAsync(default!, default!, default!, default!));
    }
}
```

#### 3. Subscription Support
**Location:** `src/Honua.Server.GraphQL/Subscriptions/SubscriptionType.cs`

```csharp
public class SubscriptionType : ObjectType
{
    protected override void Configure(IObjectTypeDescriptor descriptor)
    {
        descriptor.Field("buildingUpdated")
            .Argument("buildingId", a => a.Type<NonNullType<IdType>>())
            .Type<BuildingType>()
            .Subscribe(async context =>
            {
                var eventReceiver = context.Service<IEventReceiver>();
                return await eventReceiver.SubscribeAsync<Building>(
                    $"building_{context.ArgumentValue<string>("buildingId")}"
                );
            })
            .Resolve(context => context.GetEventMessage<Building>());
    }
}
```

#### 4. Startup Configuration
**Location:** `src/Honua.Server.Host/Program.cs`

```csharp
// Add HotChocolate GraphQL
builder.Services
    .AddGraphQLServer()
    .AddQueryType<QueryType>()
    .AddMutationType<MutationType>()
    .AddSubscriptionType<SubscriptionType>()
    .AddType<BuildingType>()
    .AddType<FloorType>()
    .AddType<RoomType>()
    .AddType<EquipmentType>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddAuthorization()
    .AddInMemorySubscriptions();

// Map GraphQL endpoint
app.MapGraphQL("/graphql");
```

---

## API Endpoints

### GraphQL Endpoint
- **POST** `/graphql` - Main GraphQL endpoint
- **GET** `/graphql?query={...}` - GET queries (for caching)
- **WS** `/graphql` - WebSocket for subscriptions

### GraphQL Playground/IDE
- **GET** `/graphql/ui` - Banana Cake Pop IDE (HotChocolate)
- **GET** `/graphql/schema.graphql` - Download SDL schema

---

## Example Queries

### 1. Get Building Hierarchy
```graphql
query GetBuildingDetails($id: ID!) {
  building(id: $id) {
    name
    address
    floors {
      level
      area
      rooms {
        number
        type
        equipment {
          id
          type
          status
        }
      }
    }
  }
}
```

### 2. Find Equipment Path (Utility Network)
```graphql
query TraceWaterNetwork($valveId: ID!) {
  equipment(id: $valveId) {
    id
    type
    feeds {
      id
      type
      feeds {
        id
        type
      }
    }
  }
}
```

### 3. Spatial Search
```graphql
query FindBuildingsInArea($bbox: BoundingBoxInput!) {
  buildingsInExtent(bbox: $bbox) {
    id
    name
    floors {
      level
    }
  }
}
```

### 4. Subscription for Real-Time Updates
```graphql
subscription WatchConstruction {
  constructionProgressUpdated {
    taskId
    percentComplete
    status
    updatedAt
  }
}
```

---

## Integration with Existing APIs

### Coexistence Strategy

**GraphQL and REST will coexist:**
- **REST:** CRUD operations, file uploads, exports, integrations
- **GraphQL:** Complex queries, relationship traversal, real-time subscriptions

**Example:**
```
POST /api/buildings           (REST - Create building)
POST /graphql                  (GraphQL - Query building with floors)
POST /api/ifc/import          (REST - Upload IFC file)
POST /graphql                  (GraphQL - Query imported elements)
```

### DataLoader Pattern (N+1 Prevention)

```csharp
// Prevent N+1 query problem
public class FloorDataLoader : BatchDataLoader<Guid, Floor>
{
    private readonly IGraphDatabaseService _graphService;

    public FloorDataLoader(IGraphDatabaseService graphService)
    {
        _graphService = graphService;
    }

    protected override async Task<IReadOnlyDictionary<Guid, Floor>> LoadBatchAsync(
        IReadOnlyList<Guid> keys,
        CancellationToken cancellationToken)
    {
        // Batch load all floors in single query
        var floors = await _graphService.GetNodesByIdsAsync(keys);
        return floors.ToDictionary(f => f.Id);
    }
}
```

---

## Standards Compliance

### GraphQL Specification
- ✅ GraphQL June 2018 specification
- ✅ Schema introspection
- ✅ Queries, mutations, subscriptions
- ✅ Fragments and inline fragments
- ✅ Variables and directives

### Apollo Federation (Future)
- Support for federated GraphQL gateway
- Compose multiple GraphQL services
- Useful for microservices architecture

### Relay Specification (Optional)
- Cursor-based pagination
- Node interface for global IDs
- Connection pattern for relationships

---

## Implementation Plan

### Week 1: Setup & Core Schema
- [ ] Install HotChocolate packages
- [ ] Define core GraphQL types (Building, Floor, Room)
- [ ] Implement Query root
- [ ] Basic resolvers with graph database

### Week 2: Relationship Resolvers
- [ ] Implement relationship traversal
- [ ] Add DataLoaders for N+1 prevention
- [ ] Test complex queries

### Week 3: Mutations & Subscriptions
- [ ] Implement create/update/delete mutations
- [ ] Add real-time subscriptions
- [ ] Integrate with Apache AGE events

### Week 4: Testing & Documentation
- [ ] Unit tests for resolvers
- [ ] Integration tests for queries
- [ ] API documentation
- [ ] GraphQL Playground setup

**Total Effort:** 4 weeks (can run in parallel with Apache AGE Phase 1.1)

---

## Resource Requirements

**Dependencies:**
```xml
<PackageReference Include="HotChocolate.AspNetCore" Version="13.9.0" />
<PackageReference Include="HotChocolate.Data" Version="13.9.0" />
<PackageReference Include="HotChocolate.Subscriptions.InMemory" Version="13.9.0" />
<PackageReference Include="HotChocolate.AspNetCore.Authorization" Version="13.9.0" />
```

**Team:**
- 1 Backend Engineer (familiar with GraphQL)
- Shares resources with Apache AGE implementation

**Infrastructure:**
- Redis (for distributed subscriptions in production)
- No additional database needed

---

## Benefits

### For Developers
- ✅ Self-documenting API (schema introspection)
- ✅ Strongly typed queries
- ✅ GraphQL Playground for exploration
- ✅ Single endpoint for all queries

### For Frontend
- ✅ Fetch exact data needed (no over/under-fetching)
- ✅ Single request for complex hierarchies
- ✅ Real-time updates via subscriptions
- ✅ Generated TypeScript types

### For AEC Users
- ✅ Fast, complex building queries
- ✅ Real-time construction progress
- ✅ Intuitive relationship traversal
- ✅ Flexible filtering and sorting

---

## Security Considerations

### Authorization
```graphql
type Mutation {
  deleteBuilding(id: ID!): Boolean!
    @authorize(policy: "AdminOnly")

  updateEquipment(id: ID!, input: UpdateEquipmentInput!): Equipment!
    @authorize(roles: ["Operator", "Admin"])
}
```

### Query Complexity Limits
```csharp
builder.Services
    .AddGraphQLServer()
    .AddMaxExecutionDepthRule(15)
    .AddMaxComplexityRule(1000)
    .AddCostDirective();
```

### Rate Limiting
- Use existing ASP.NET Core rate limiting
- Apply per-user query complexity budgets
- Monitor and alert on expensive queries

---

## Performance Optimization

### Caching Strategies
1. **Query result caching** - Cache frequently requested data
2. **DataLoader batching** - Prevent N+1 queries
3. **Persisted queries** - Send query ID instead of full query
4. **CDN caching** - Cache GET requests

### Monitoring
- Query execution time tracking
- Complexity scoring
- Error rate monitoring
- Subscription connection count

---

## Migration Path

### Phase 1: Core GraphQL (Weeks 1-4)
- Basic queries for buildings, floors, rooms
- Relationship traversal
- Simple mutations

### Phase 2: Advanced Features (Weeks 5-6)
- Real-time subscriptions
- Complex filtering and sorting
- Spatial query integration

### Phase 3: Production Hardening (Weeks 7-8)
- Performance optimization
- Security hardening
- Monitoring and alerting
- Documentation

**Total Timeline:** 8 weeks (partially parallel with Phase 1.1)

---

## Decision Points

### Immediate Decision Required
**Should we add GraphQL to Phase 1?**
- ✅ **Yes:** Natural complement to graph database
- ✅ **Yes:** Industry expectation for graph data APIs
- ✅ **Yes:** Significant value for frontend developers
- ⚠️ **Consider:** Adds 4-8 weeks to timeline
- ⚠️ **Consider:** Requires GraphQL expertise

### Alternative: Phase 2
If resources are constrained, GraphQL could move to Phase 2:
- Phase 1: Focus on Apache AGE with REST API
- Phase 2: Add GraphQL on top of working graph database

---

## Recommendation

**Add GraphQL as Phase 1.1B**

**Rationale:**
1. Graph database without GraphQL API feels incomplete
2. AEC customers will expect GraphQL for BIM/graph data
3. Implementation is straightforward with HotChocolate
4. Can be developed in parallel with Apache AGE
5. Strong competitive differentiator

**Updated Timeline:**
- Phase 1.1A: Apache AGE Integration (3-4 weeks)
- Phase 1.1B: GraphQL API (4 weeks, partially parallel)
- **Combined:** 5-6 weeks total

---

## Success Metrics

| Metric | Target |
|--------|--------|
| Query Complexity Support | 1000+ operations |
| Response Time (simple) | <100ms |
| Response Time (complex) | <500ms |
| Subscription Connections | 1000+ concurrent |
| Schema Size | 50+ types |
| API Documentation | 100% coverage |

---

## Conclusion

**GraphQL is the industry-standard API for graph databases.** Adding it alongside Apache AGE creates a complete, modern graph data platform that meets AEC industry expectations.

**Recommendation:** Approve Phase 1.1B and allocate 4-6 weeks for implementation starting Week 3 of Phase 1.1A.

---

**Prepared by:** Claude
**Review Required:** Engineering Lead, Product Manager
**Related Documents:**
- `AEC_TECHNICAL_ENABLERS_PROPOSAL.md`
- `APACHE_AGE_INTEGRATION.md`
- `PHASE_1_1_IMPLEMENTATION_SUMMARY.md`
