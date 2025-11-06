# Honua AI Consultant - Specialized Agent Architecture

**Status**: Design Document
**Last Updated**: 2025-10-05
**Version**: 1.0

## Executive Summary

The Honua AI Consultant uses a **multi-agent architecture** where specialized agents handle distinct operational domains. Each agent is an expert in its domain and can be invoked independently or orchestrated together for complex workflows.

**Key Principle**: GitOps workflow is **limited to metadata and service configuration changes** across environments. Infrastructure upgrades, performance tuning, and security hardening use different workflows appropriate to their domain.

---

## Agent Taxonomy

### 1. Deployment Configuration Agent
**Domain**: Metadata and service configuration deployment across environments
**GitOps**: ✅ Yes - Full GitOps workflow with PR-based approval
**Use Cases**:
- Adding/modifying layers in metadata
- Changing service configurations (OData, WFS, WMS settings)
- Environment promotions (dev → staging → production)
- Feature flag toggles
- Authentication configuration updates

**Workflow**:
```
User Request → Analyze Metadata → Generate Config Changes → Create PR →
Review → Merge → Auto-Deploy to Environments (dev → staging → prod with approval gates)
```

**Example**:
```bash
$ honua assistant "Add bike lanes layer from table public.bike_lanes"

AI: I'll create a metadata configuration change for the bike lanes layer.

Generated changes:
  - environments/dev/layers/bike-lanes.yaml
  - environments/staging/layers/bike-lanes.yaml
  - environments/production/layers/bike-lanes.yaml

Creating PR #42: "Add bike lanes layer"
✓ Validation passed
✓ No breaking changes detected
✓ Deploy impact: Low (new layer, no existing dependencies)

PR created: https://github.com/city/honua-config/pull/42
```

**Constraints**:
- Only modifies metadata and service configuration
- Does NOT modify infrastructure (databases, servers, load balancers)
- Does NOT upgrade Honua server versions
- Changes are declarative and version-controlled

---

### 2. Performance Optimization Agent
**Domain**: Query optimization, indexing, caching strategies
**GitOps**: ❌ No - Direct database/system operations with plan/apply workflow
**Use Cases**:
- Creating spatial indexes
- Query optimization
- Cache configuration tuning
- Database statistics analysis
- Geometry simplification
- Multi-resolution geometry setup
- Connection pool tuning

**Workflow**:
```
User Request → Analyze Performance → Generate Optimization Plan →
Review Plan → User Approval → Execute Directly → Validate Results → Rollback if Failed
```

**Example**:
```bash
$ honua assistant optimize "My parcels layer is slow"

AI: Analyzing performance...

Current State:
  • P95 latency: 2.3s
  • No spatial indexes found
  • Geometry avg 1,200 vertices
  • 245k features

Root Causes:
  1. ❌ No GIST index on geometry
  2. ❌ Complex geometries at all zoom levels
  3. ⚠️  No response caching

Optimization Plan (3 phases):

Phase 1: Quick Wins (5 min, zero downtime)
  ✓ CREATE INDEX parcels_geom_idx USING GIST(geometry)
  ✓ Enable Brotli compression
  → Expected: P95 800ms (65% improvement)

Phase 2: Geometry Optimization (5 min)
  ✓ Create simplified geometries for zoom 0-10
  ✓ Multi-resolution storage
  → Expected: P95 200ms (91% improvement)

Phase 3: Caching (5 min)
  ✓ Redis query cache
  ✓ CDN edge caching
  → Expected: P95 50ms (98% improvement)

Proceed? [Y/n]: y

⏳ Executing Phase 1...
   ✓ CREATE INDEX parcels_geom_idx USING GIST(geometry) [3.2s]
   ✓ Compression enabled
   Performance test: P95 now 780ms ✓ 66% improvement

[Continues through phases 2 and 3]

✨ Optimization complete!
   • Final P95: 48ms (98% improvement)
   • Throughput: 40 → 850 req/s
   • Cost savings: $340/mo (avoided scaling)
```

**Constraints**:
- Changes are **imperative operations** (CREATE INDEX, ANALYZE, etc.)
- Requires database DDL permissions
- Uses plan/apply workflow (like Terraform)
- Can be rolled back via snapshots
- NOT stored in Git (operations are logged in audit trail)

---

### 3. Security Hardening Agent
**Domain**: Authentication, authorization, CORS, rate limiting, security policies
**GitOps**: 🟡 Hybrid - Config changes via GitOps, infrastructure changes via plan/apply
**Use Cases**:
- OAuth/JWT configuration
- CORS policy setup
- Rate limiting rules
- API key management
- Network policies
- SSL/TLS certificate management
- Security header configuration
- Firewall rules

**Workflow (Config Changes)**:
```
Security Config Request → Generate Config → Create PR → Review → Merge → Deploy
```

**Workflow (Infrastructure Changes)**:
```
Security Request → Analyze Topology → Generate Plan → Review → User Approval → Execute → Verify
```

**Example (Config Change - GitOps)**:
```bash
$ honua assistant security "Enable CORS for https://maps.city.gov"

AI: I'll add a CORS configuration for maps.city.gov.

Generated changes:
  - environments/production/security/cors-policy.yaml

Changes:
  allowedOrigins:
    + - "https://maps.city.gov"
  allowedMethods:
    - GET
    - POST
    - OPTIONS

Creating PR #43: "Add CORS policy for maps.city.gov"
Risk Level: LOW
Breaking Changes: None

PR created: https://github.com/city/honua-config/pull/43
```

**Example (Infrastructure Change - Plan/Apply)**:
```bash
$ honua assistant security "Enable WAF with OWASP rules"

AI: Analyzing current security posture...

Current State:
  • WAF: Not configured
  • DDoS Protection: CloudFlare (basic)
  • Rate Limiting: Application-level only

Security Enhancement Plan:

Phase 1: WAF Configuration (10 min)
  ✓ Enable AWS WAF on Application Load Balancer
  ✓ Apply OWASP Core Rule Set
  ✓ Custom rule: Block bad bots
  → Impact: ~5-10% traffic may be blocked initially

Phase 2: Testing (5 min)
  ✓ Whitelist known good clients
  ✓ Monitor false positives
  ✓ Adjust rules based on logs

Cost Impact: +$5/month (WAF) + $0.60 per million requests

Proceed? [Y/n]: y

⏳ Applying WAF configuration...
   ✓ WAF Web ACL created: honua-production-waf
   ✓ OWASP rule set applied
   ✓ Associated with ALB
   ✓ Monitoring dashboard created

✨ Security enhancement complete!
   • WAF enabled with OWASP rules
   • Monitoring: CloudWatch dashboard
   • False positive rate: 0.02% (monitor for 24h)
```

---

### 4. Honua Upgrade & Patch Agent
**Domain**: Honua server version upgrades, patch management, migration execution
**GitOps**: ❌ No - Infrastructure orchestration with blue/green or canary deployments
**Use Cases**:
- Honua server version upgrades
- Security patch application
- Database schema migrations
- Breaking change migrations
- Dependency updates
- Rollback to previous versions

**Workflow**:
```
Upgrade Request → Check Compatibility → Download Release → Test in Staging →
Generate Rollback Plan → Blue/Green or Canary Deploy → Monitor Health →
Rollback if Failed OR Promote if Successful
```

**Example**:
```bash
$ honua assistant upgrade "Upgrade to Honua 2.0"

AI: Analyzing upgrade path from 1.8.3 to 2.0.0...

Compatibility Check:
  ✓ .NET 9.0 runtime compatible
  ✓ Database schema compatible (auto-migration available)
  ⚠️  Breaking change: OData endpoint changed from /odata to /api/odata
  ⚠️  Breaking change: Authentication config structure changed

Migration Plan:

Phase 1: Staging Upgrade (30 min)
  ✓ Deploy 2.0.0 to staging environment
  ✓ Run database migration (adds 3 tables, modifies 2)
  ✓ Update authentication config (OAuth2 → OAuth2 + OIDC)
  ✓ Run integration tests
  ✓ Verify OData endpoint migration

Phase 2: Production Blue/Green Deployment (45 min)
  ✓ Deploy 2.0.0 to "green" environment
  ✓ Run database migration (zero downtime)
  ✓ Warm up caches
  ✓ Route 10% traffic to green
  ✓ Monitor error rates for 15 minutes
  ✓ If healthy: Route 100% traffic to green
  ✓ Keep blue running for 1 hour (rollback window)
  ✓ If successful: Decommission blue

Rollback Plan (if needed):
  ✓ Route traffic back to blue
  ✓ Rollback database migration (migrations/002_rollback_v2.sql)
  ✓ Revert authentication config

Client Impact:
  ⚠️  Clients using /odata endpoint must update to /api/odata
  ⚠️  API clients may need to refresh auth tokens

Proceed with staging first? [Y/n]: y

⏳ Deploying to staging...
   ✓ Image pulled: honua:2.0.0
   ✓ Database migration applied (3.2s)
   ✓ Authentication config migrated
   ✓ Health check: PASSED
   ✓ Integration tests: 47/47 PASSED

Staging upgrade successful! Deploy to production? [Y/n]: y

⏳ Blue/Green deployment to production...
   ✓ Green environment deployed
   ✓ Database migration applied (zero downtime)
   ✓ 10% traffic → green
   ⏳ Monitoring (15 min)...
      • Error rate: 0.01% (normal)
      • P95 latency: 142ms (improved from 158ms)
      • Health checks: 100% passing
   ✓ 100% traffic → green
   ⏳ Keeping blue alive for 1 hour (rollback window)

✨ Upgrade complete!
   • Version: 1.8.3 → 2.0.0
   • Downtime: 0 seconds
   • Performance: 10% improvement
   • Rollback available until: 2025-10-05 15:30 UTC

Post-upgrade actions:
  • Update API documentation
  • Notify clients of /odata → /api/odata change
  • Monitor error logs for 24 hours
  • Decommission blue environment after 1 hour
```

**Constraints**:
- NOT a GitOps workflow (infrastructure orchestration)
- Requires careful planning and rollback strategies
- Uses blue/green or canary deployment patterns
- Database migrations must be reversible
- Keeps previous version running during rollback window

---

### 5. Migration & Import Agent
**Domain**: Data import, ArcGIS/GeoServer migration, schema mapping
**GitOps**: ❌ No - Data operations with validation and rollback
**Use Cases**:
- Importing GeoPackage, Shapefile, GeoJSON
- Migrating from ArcGIS Server
- Migrating from GeoServer
- Schema transformation
- Coded domain conversion
- Attachment migration
- Bulk data import

**Workflow**:
```
Import Request → Analyze Source → Schema Mapping → Preview → User Confirmation →
Import with Progress → Validate → Create Indexes → Update Metadata
```

**Example**:
```bash
$ honua assistant migrate "Migrate from ArcGIS Server at https://gis.city.gov/arcgis"

AI: Discovering ArcGIS Server...

Found:
  • Planning Service (FeatureServer)
    - 8 layers
    - 1.2M total features
    - 4.2GB data

Compatibility Analysis:

┌─────────────────────────────────────────────┐
│ Layer: Parcels (245k features)              │
├─────────────────────────────────────────────┤
│ ✓ Geometry: Polygon (compatible)            │
│ ✓ Fields: All types supported               │
│ ⚠️ Coded domains → will convert to CHECK   │
│ ⚠️ Attachments: 12k files (1.2GB)          │
│ ✓ Spatial reference: EPSG:2227 → 4326      │
│ Est. migration time: 8 minutes              │
└─────────────────────────────────────────────┘

Migration Strategy:

1. Schema Migration (5 min)
   - Create PostGIS tables
   - Convert coded domains to constraints
   - Map GeoServices REST types to PostgreSQL

2. Data Transfer (15 min)
   - Parallel bulk load (8 workers)
   - Streaming to minimize memory
   - Reproject to EPSG:4326

3. Attachment Migration (10 min)
   - Download 12k attachments
   - Upload to S3
   - Create attachment links

4. Optimization (5 min)
   - Create spatial indexes
   - Generate statistics
   - Analyze query performance

5. Metadata Generation (2 min)
   - Generate OGC API configuration
   - Preserve field aliases
   - Create WFS/WMS services

6. Validation (3 min)
   - Compare 1000 random features
   - Performance benchmark
   - OGC conformance tests

Total time: ~40 minutes
Downtime: Zero (parallel operation)

Proceed? [Y/n]: y

⏳ Phase 1: Schema Migration...
   ✓ Tables created (8 layers)
   ✓ Domain 'ZoningType' → CHECK constraint

⏳ Phase 2: Data Transfer...
   ⏳ Parcels: [████████▌    ] 73% (178k/245k) 2m 15s

💡 Insight: Polygon complexity >1000 vertices
   Recommend: ST_Simplify(geometry, 0.5) → 60% size reduction
   Apply now? [y/N]: y

   ✓ Parcels: Complete (245k features, 2.8GB after simplification)
   ✓ Zoning: Complete (8k features)
   [continues for all layers]

⏳ Phase 3: Attachment Migration...
   ✓ Downloaded 12k attachments (1.2GB)
   ✓ Uploaded to S3: honua-attachments-production
   ✓ Links created in database

⏳ Phase 4: Optimization...
   ✓ Spatial indexes created (8 layers)
   ✓ Statistics updated

⏳ Phase 5: Metadata Generation...
   ✓ OGC API configured
   ✓ WFS service enabled
   ✓ WMS service enabled
   ✓ Field aliases preserved

⏳ Phase 6: Validation...
   ✓ Feature comparison: 100% match (1000 samples)
   ✓ Performance: P95 120ms (was 850ms in ArcGIS!)
   ✓ OGC conformance: PASSED

✨ Migration Complete!

Results:
  • 8 layers migrated successfully
  • 1.2M total features
  • 12k attachments
  • Performance: 7x faster than ArcGIS
  • Cost: $0 licensing (was $12k/year)

New endpoints:
  http://localhost:5000/ogc/collections/parcels
  http://localhost:5000/ogc/collections/zoning
  [8 total collections]

Next steps:
  • Update client applications to new URLs
  • Test all features
  • Decommission ArcGIS Server (after validation period)
```

---

### 6. Troubleshooting & Diagnostics Agent
**Domain**: Issue detection, root cause analysis, remediation
**GitOps**: ❌ No - Diagnostic operations with optional auto-remediation
**Use Cases**:
- Performance degradation analysis
- Memory leak detection
- Error spike investigation
- Health check failures
- Database connection issues
- Cache invalidation problems
- Log analysis

**Workflow**:
```
Issue Detection → Analyze Symptoms → Root Cause Analysis →
Recommend Fix → User Approval → Apply Remediation → Verify → Monitor
```

**Example**:
```bash
$ honua assistant troubleshoot --server https://gis.prod.example.com

AI: Connecting to production...
   ⚠️ Elevated error rate detected!

Analysis (last 15 minutes):
  • Requests: 45,234
  • Errors: 1,247 (2.76%)
  • P95: 3.2s (normally 150ms)
  • Memory: 87% (normally 45%)

Root Cause Analysis:

┌─────────────────────────────────────────────┐
│ 🚨 CRITICAL: Memory Leak Detected          │
├─────────────────────────────────────────────┤
│ Memory growing 15MB/min for 2 hours        │
│ Current: 6.8GB / 8GB (86%)                 │
│ Projected OOM in: 8 minutes                 │
│                                             │
│ Root cause:                                 │
│ Large queries without pagination on layer   │
│ 'building-permits' (450k features)          │
│                                             │
│ Evidence:                                   │
│ • Query returning 450k features             │
│ • Client not using pagination               │
│ • GeoJSON serialization: 3.2GB              │
│                                             │
│ Immediate fix:                              │
│ 1. Restart service (clear memory)           │
│ 2. Enable default pagination (limit: 1000)  │
│ 3. Add alert for large queries              │
│                                             │
│ Long-term:                                  │
│ 1. Enforce max 10k features per request     │
│ 2. Require pagination for large collections │
│ 3. Add query result caching                 │
└─────────────────────────────────────────────┘

Auto-execute fix in 30s... [press any key to cancel]

⏳ Applying mitigation...
   ✓ Rolling restart (zero downtime)
   ✓ Pagination enabled (max 1000)
   ✓ Alert configured (CloudWatch)

✅ Issue resolved!

Monitoring:
   • Memory: 42% (stable)
   • P95: 145ms (normal)
   • Error rate: 0.1% (normal)

Configuration saved to prevent recurrence.
```

---

## Agent Orchestration

### Multi-Agent Workflows

Agents can work together for complex scenarios:

**Example: Complete Production Deployment**

```bash
$ honua assistant "Set up production GIS infrastructure for 500 req/s with bike lanes layer"

AI: This is a complex multi-agent task. I'll coordinate:
  1. Deployment Configuration Agent - Infrastructure setup
  2. Migration Agent - Import bike lanes data
  3. Performance Agent - Optimize for 500 req/s
  4. Security Agent - Harden production environment

Phase 1: Infrastructure Setup (Deployment Configuration Agent)
  ✓ Generate CloudFormation template (AWS ECS + RDS + S3)
  ✓ Configure auto-scaling (3-20 instances)
  ✓ Set up Application Load Balancer
  ✓ Configure CloudWatch monitoring
  [deploys infrastructure]

Phase 2: Data Import (Migration Agent)
  ✓ Analyze bike_lanes table (12k features)
  ✓ Import to PostGIS
  ✓ Create spatial indexes
  ✓ Generate metadata

Phase 3: Performance Optimization (Performance Agent)
  ✓ Load testing (500 req/s sustained)
  ✓ Query optimization (P95 < 200ms)
  ✓ Cache configuration (Redis + CDN)
  ✓ Connection pool tuning

Phase 4: Security Hardening (Security Agent)
  ✓ Enable WAF with OWASP rules
  ✓ Configure OAuth authentication
  ✓ Set up rate limiting
  ✓ Enable SSL/TLS
  ✓ Network security groups

✨ Production environment ready!

Summary:
  • Infrastructure: AWS ECS Fargate (3 instances)
  • Database: RDS PostgreSQL with PostGIS
  • Performance: P95 142ms @ 500 req/s
  • Security: WAF, OAuth, TLS 1.3
  • Cost: ~$320/month
  • Uptime SLA: 99.9%

Access:
  https://gis.city.gov/ogc
```

---

## Agent Communication Protocol

Agents communicate via structured messages:

```csharp
public class AgentMessage
{
    public string FromAgent { get; set; }
    public string ToAgent { get; set; }
    public AgentMessageType Type { get; set; }
    public Dictionary<string, object> Data { get; set; }
    public string CorrelationId { get; set; } // For multi-agent workflows
}

public enum AgentMessageType
{
    Request,           // Request another agent's assistance
    Response,          // Respond to a request
    Notification,      // Notify of an event
    Coordination       // Coordinate multi-agent workflow
}
```

**Example Multi-Agent Coordination**:

```csharp
// Deployment Agent asks Performance Agent to optimize after deployment
var message = new AgentMessage
{
    FromAgent = "DeploymentConfigurationAgent",
    ToAgent = "PerformanceOptimizationAgent",
    Type = AgentMessageType.Request,
    CorrelationId = "deploy-12345",
    Data = new Dictionary<string, object>
    {
        ["action"] = "OptimizeAfterDeployment",
        ["layerName"] = "bike-lanes",
        ["environment"] = "production",
        ["targetP95"] = 200, // ms
        ["targetThroughput"] = 500 // req/s
    }
};
```

---

## Technology Stack

### Semantic Kernel Plugins

Each agent is implemented as a **Semantic Kernel plugin**:

```csharp
// Example: DeploymentConfigurationPlugin
public class DeploymentConfigurationPlugin
{
    [KernelFunction]
    [Description("Generate metadata configuration for a new layer")]
    public async Task<string> GenerateLayerConfigAsync(
        [Description("Layer name")] string layerName,
        [Description("PostGIS table name")] string tableName,
        [Description("Geometry type")] string geometryType,
        [Description("Target environment")] string environment)
    {
        // Generate YAML configuration
        // Create PR in Git repository
        // Return PR URL and status
    }
}

// Example: PerformanceOptimizationPlugin
public class PerformanceOptimizationPlugin
{
    [KernelFunction]
    [Description("Analyze query performance and recommend optimizations")]
    public async Task<string> AnalyzePerformanceAsync(
        [Description("Layer name")] string layerName)
    {
        // Query pg_stat_statements
        // Analyze slow queries
        // Check for missing indexes
        // Generate optimization plan
    }
}
```

---

## Summary

| Agent | Domain | GitOps | Primary Workflow |
|-------|--------|--------|------------------|
| **Deployment Configuration** | Metadata & service config | ✅ Yes | PR → Review → Merge → Auto-Deploy |
| **Performance Optimization** | Indexes, caching, queries | ❌ No | Plan → Approve → Execute → Validate |
| **Security Hardening** | Auth, CORS, WAF, policies | 🟡 Hybrid | Config: GitOps, Infra: Plan/Apply |
| **Honua Upgrade & Patch** | Version upgrades, migrations | ❌ No | Blue/Green or Canary Deployment |
| **Migration & Import** | Data import, ArcGIS migration | ❌ No | Analyze → Transform → Import → Validate |
| **Troubleshooting** | Diagnostics, root cause analysis | ❌ No | Detect → Analyze → Remediate → Monitor |

**Key Insights**:
- GitOps is **limited to metadata and service configuration**
- Infrastructure changes use **plan/apply workflow** (Terraform-style)
- Agents can **orchestrate together** for complex multi-step workflows
- Each agent has **domain expertise** and appropriate safety mechanisms
- All operations are **logged and auditable**

---

**Next Steps**:
1. Implement Semantic Kernel plugins for each agent
2. Define agent communication protocol
3. Build orchestration layer for multi-agent workflows
4. Create safety mechanisms for each agent type
5. Develop testing strategy for agent interactions
