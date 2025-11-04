# Honua AI Consultant - User Experience Design

**Status**: Design Document
**Last Updated**: 2025-10-05
**Version**: 1.0

## Executive Summary

Users interact with a **single, unified CLI interface** (`honua consultant`) that provides intelligent, context-aware assistance for all GIS operations. The underlying multi-agent architecture is completely transparent - users don't need to know which agent is handling their request.

**Design Principle**: **One CLI, Many Experts** - The user talks to "the consultant," but behind the scenes, specialized agents collaborate to deliver the best solution.

---

## User Interface

### Single Entry Point

```bash
# Users ALWAYS use this command
$ honua consultant [command]

# Or interactive mode
$ honua consultant
> Your request here...
```

**Users never see or choose agents.** The system automatically:
1. Analyzes the user's intent
2. Routes to appropriate agent(s)
3. Coordinates multi-agent workflows when needed
4. Presents unified results

---

## Example User Interactions

### Example 1: Simple Request (Single Agent)

**User sees:**
```bash
$ honua consultant "Add bike lanes layer from table public.bike_lanes"

Analyzing request...
✓ Found table public.bike_lanes (12,453 features, LineString geometry)
✓ Generating metadata configuration
✓ Creating deployment plan

Changes:
  • New layer: bike-lanes
  • Service: transportation
  • Endpoints: OGC API, WFS, WMS

Creating pull request...
✓ PR #42 created: "Add bike lanes layer"

Review at: https://github.com/city/honua-config/pull/42

What happens after merge:
  1. Auto-deploy to dev (2 min)
  2. Auto-deploy to staging (5 min)
  3. Awaits approval for production

Done! ✨
```

**What actually happened (hidden from user):**
- Agent Coordinator analyzed intent → routed to **DeploymentConfigurationAgent**
- DeploymentConfigurationAgent:
  - Connected to database
  - Introspected table schema
  - Generated YAML metadata
  - Created Git PR via GitHub API
- Result presented to user as single unified response

---

### Example 2: Complex Request (Multi-Agent Orchestration)

**User sees:**
```bash
$ honua consultant "My parcels layer is slow, can you fix it?"

Analyzing performance...
⏳ Running diagnostics on parcels layer...

Found issues:
  ❌ No spatial index (CRITICAL)
  ❌ Complex geometries (1,200 vertices avg)
  ⚠️  No caching configured
  • Current P95: 2.3 seconds
  • Target P95: <200ms

I can fix this with a 3-phase optimization plan (15 min total):

Phase 1: Spatial Indexing (5 min)
  ✓ CREATE INDEX parcels_geom_idx USING GIST(geometry)
  → Expected improvement: 65% faster

Phase 2: Geometry Simplification (5 min)
  ✓ Create multi-resolution geometries
  ✓ Use simplified versions for zoom 0-10
  → Expected improvement: 91% faster

Phase 3: Caching (5 min)
  ✓ Enable Redis query cache
  ✓ Configure CDN edge caching
  → Expected improvement: 98% faster

Estimated final P95: 48ms (98% improvement)

Proceed? [Y/n]: y

⏳ Executing optimization plan...

Phase 1: Spatial Indexing
  ✓ CREATE INDEX parcels_geom_idx USING GIST(geometry) [3.2s]
  ✓ Performance test: P95 now 780ms ✓ 66% improvement

Phase 2: Geometry Simplification
  ✓ Generated simplified geometries (zoom 0-10)
  ✓ Updated metadata configuration
  ✓ Performance test: P95 now 215ms ✓ 91% improvement

Phase 3: Caching
  ✓ Redis cache enabled (TTL: 300s)
  ✓ CDN cache rules configured
  ✓ Performance test: P95 now 48ms ✓ 98% improvement

✨ Optimization complete!

Results:
  • P95 latency: 2.3s → 48ms (98% faster)
  • Throughput: 40 → 850 req/s
  • Cost savings: $340/mo (avoided scaling)

Configuration changes:
  • Created PR #43: "Optimize parcels layer caching"
  • Review at: https://github.com/city/honua-config/pull/43
```

**What actually happened (hidden from user):**
- Agent Coordinator analyzed "slow" → routed to **TroubleshootingAgent**
- TroubleshootingAgent:
  - Detected performance issue
  - Determined root causes
  - Delegated to **PerformanceOptimizationAgent** for fixes
- PerformanceOptimizationAgent:
  - Created spatial index (direct database operation)
  - Generated simplified geometries (database operation)
  - Requested **DeploymentConfigurationAgent** to update cache config
- DeploymentConfigurationAgent:
  - Generated caching configuration changes
  - Created Git PR for cache settings
- Agent Coordinator synthesized results into single user-facing response

**User doesn't know:**
- That 3 different agents were involved
- That some operations were direct database DDL
- That some operations went through GitOps workflow
- How the agents coordinated

---

### Example 3: Ambiguous Request (Coordinator Clarifies Intent)

**User sees:**
```bash
$ honua consultant "Make it faster"

I need more context to help optimize performance. What would you like to make faster?

1. A specific layer or collection
2. Overall server response time
3. Database query performance
4. Tile generation speed
5. Something else

Please specify, or provide more details: _
```

**User responds:**
```bash
> The roads layer

Got it! Analyzing roads layer performance...

[Continues with optimization workflow]
```

**What actually happened:**
- Agent Coordinator couldn't determine intent from "make it faster"
- Used **clarification strategy** to gather more information
- Once clarified, routed to **PerformanceOptimizationAgent**

---

### Example 4: Complex Multi-Domain Request

**User sees:**
```bash
$ honua consultant "Set up production environment for 500 req/s with OAuth authentication"

This is a complex deployment. I'll handle:
  1. Infrastructure setup (AWS ECS + RDS + S3)
  2. Security configuration (OAuth + WAF)
  3. Performance optimization (auto-scaling for 500 req/s)
  4. Monitoring setup

Estimated time: 45 minutes
Estimated cost: $320/month

Proceed? [Y/n]: y

⏳ Phase 1: Infrastructure Setup (15 min)
  ✓ Generating CloudFormation template
  ✓ Configuring VPC and subnets
  ✓ Creating ECS cluster
  ✓ Setting up RDS PostgreSQL with PostGIS
  ✓ Creating S3 buckets (tiles, attachments)
  ✓ Configuring Application Load Balancer
  ✓ Deploying infrastructure...
  ✓ Infrastructure ready

⏳ Phase 2: Security Configuration (10 min)
  ✓ Enabling AWS WAF with OWASP rules
  ✓ Configuring OAuth 2.0 provider
  ✓ Setting up SSL/TLS certificates
  ✓ Configuring security groups
  ✓ Enabling CloudWatch logging
  ✓ Security hardening complete

⏳ Phase 3: Performance Tuning (10 min)
  ✓ Configuring auto-scaling (3-20 instances)
  ✓ Setting up Redis cache
  ✓ Configuring CDN
  ✓ Optimizing connection pools
  ✓ Load testing @ 500 req/s... P95: 142ms ✓
  ✓ Performance targets met

⏳ Phase 4: Monitoring Setup (5 min)
  ✓ CloudWatch dashboards created
  ✓ Alarms configured (CPU, memory, errors)
  ✓ Log aggregation enabled
  ✓ Monitoring ready

✨ Production environment ready!

Access:
  • URL: https://gis.example.com
  • Admin: https://gis.example.com/admin
  • Metrics: CloudWatch dashboard

Next steps:
  • Deploy your metadata configuration
  • Import data from staging
  • Update DNS records
  • Run final smoke tests

Configuration saved to:
  • CloudFormation: infrastructure/production.yaml
  • Security: security/production-oauth.yaml
  • Monitoring: monitoring/production-dashboard.json

Total time: 38 minutes
```

**What actually happened (hidden from user):**
- Agent Coordinator analyzed complex request → created multi-agent workflow
- **DeploymentConfigurationAgent**: Generated CloudFormation infrastructure
- **SecurityHardeningAgent**: Configured OAuth, WAF, TLS
- **PerformanceOptimizationAgent**: Tuned auto-scaling, caching, load tested
- Monitoring setup: Configured CloudWatch (could be separate agent or part of deployment)
- Agent Coordinator orchestrated sequential and parallel execution
- Presented unified progress updates and final summary

**User experience:**
- Single, coherent workflow
- Clear progress updates
- Unified result
- No awareness of 3-4 agents collaborating

---

## CLI Interface Design

### Interactive Mode

```bash
$ honua consultant

╭─────────────────────────────────────────╮
│  Honua AI Consultant                   │
│  Intelligent GIS Operations Assistant  │
╰─────────────────────────────────────────╯

Type your request or question. Examples:
  • "Add a new layer from table cities"
  • "Why is my parcels layer slow?"
  • "Upgrade to latest version"
  • "Deploy to production"

Type 'help' for more examples, 'exit' to quit.

> _
```

### Direct Command Mode

```bash
# Quick one-off requests
$ honua consultant "create index on parcels layer"

# With flags for non-interactive approval
$ honua consultant "optimize performance" --auto-approve

# With specific context
$ honua consultant "add layer" --table cities --service municipal

# Get status of ongoing operations
$ honua consultant status

# Review past operations
$ honua consultant history
```

### Status and History

```bash
$ honua consultant status

Current Operations:
  ⏳ Optimization: parcels layer (Phase 2/3, 3 min remaining)

Recent Completed:
  ✓ Added bike-lanes layer (2 min ago)
  ✓ Optimized roads layer (1 hour ago)

Pending Approvals:
  ⚠️ PR #42: Add bike-lanes layer (awaiting merge)
  ⚠️ Production deployment: Awaiting approval
```

```bash
$ honua consultant history --last 10

Recent Operations:
  1. 2025-10-05 14:30 - Add bike-lanes layer ✓
  2. 2025-10-05 13:15 - Optimize parcels layer ✓
  3. 2025-10-05 11:45 - Security: Enable WAF ✓
  4. 2025-10-05 10:20 - Upgrade to v2.0.0 ✓
  5. 2025-10-04 16:30 - Import from ArcGIS ✓
  [...]

View details: honua consultant history --id 1
```

---

## Agent Coordinator Architecture

### Coordinator Responsibilities

The **Agent Coordinator** is the "brain" that sits behind the `honua consultant` CLI:

```
┌────────────────────────────────────────────────────┐
│              User (CLI Interface)                  │
└────────────────────┬───────────────────────────────┘
                     │
                     ↓
┌────────────────────────────────────────────────────┐
│          Agent Coordinator (Hidden)                │
│                                                     │
│  • Intent analysis (NLP)                           │
│  • Agent selection & routing                       │
│  • Multi-agent orchestration                       │
│  • Context management                              │
│  • Response synthesis                              │
│  • Error handling & fallback                       │
└────────────────────┬───────────────────────────────┘
                     │
         ┌───────────┼───────────┐
         ↓           ↓           ↓
┌─────────────┐ ┌─────────┐ ┌─────────────┐
│ Deployment  │ │ Perf    │ │ Security    │
│ Config      │ │ Opt     │ │ Hardening   │
│ Agent       │ │ Agent   │ │ Agent       │
└─────────────┘ └─────────┘ └─────────────┘
         ↓           ↓           ↓
┌─────────────┐ ┌─────────┐ ┌─────────────┐
│ Upgrade     │ │ Migration│ │ Troubleshoot│
│ Agent       │ │ Agent   │ │ Agent       │
└─────────────┘ └─────────┘ └─────────────┘
```

### Intent Analysis Examples

```csharp
// Coordinator analyzes user input and routes to appropriate agent(s)

"Add bike lanes layer"
  → DeploymentConfigurationAgent

"My parcels layer is slow"
  → TroubleshootingAgent
  → (delegates to) PerformanceOptimizationAgent

"Upgrade to version 2.0"
  → HonuaUpgradeAgent

"Import data from ArcGIS at https://gis.city.gov/arcgis"
  → MigrationAgent

"Set up production with OAuth and WAF for 500 req/s"
  → Multi-agent workflow:
     - DeploymentConfigurationAgent (infrastructure)
     - SecurityHardeningAgent (OAuth + WAF)
     - PerformanceOptimizationAgent (scaling for 500 req/s)

"Why am I getting 500 errors?"
  → TroubleshootingAgent
  → (auto-diagnoses and may delegate to other agents for remediation)
```

### Context Management

The coordinator maintains conversation context:

```bash
$ honua consultant "add a new layer"

What table should the layer use?

> public.parks

Got it. What should the layer be named?

> city-parks

Creating layer 'city-parks' from table 'public.parks'...
✓ Layer created
```

The coordinator remembers:
- Previous questions asked
- User preferences
- Active operations
- Environment context (dev vs production)

---

## Agent Communication Protocol (Hidden from User)

While invisible to users, agents communicate via structured messages:

```csharp
// Example: Coordinator → PerformanceOptimizationAgent
var message = new AgentRequest
{
    CorrelationId = "req-12345",
    Intent = "OptimizePerformance",
    Context = new {
        LayerName = "parcels",
        CurrentP95 = 2300,  // ms
        TargetP95 = 200,    // ms
        Environment = "production"
    }
};

// Agent responds with plan
var response = new AgentResponse
{
    CorrelationId = "req-12345",
    Status = "PlanGenerated",
    Plan = new {
        Phases = [
            { Name = "Spatial Indexing", EstimatedTime = "5m", Impact = "65% improvement" },
            { Name = "Geometry Simplification", EstimatedTime = "5m", Impact = "91% improvement" },
            { Name = "Caching", EstimatedTime = "5m", Impact = "98% improvement" }
        ],
        TotalTime = "15m",
        ExpectedP95 = 48 // ms
    }
};

// Coordinator presents plan to user for approval
// If approved, coordinator tells agent to execute
```

---

## User Experience Principles

### 1. **Single Interface, Zero Agent Awareness**
Users never:
- Choose which agent to use
- See agent names in output
- Know how many agents are involved
- Understand the orchestration

Users only:
- Describe what they want
- Get results
- Approve plans when needed

### 2. **Intelligent Routing**
The coordinator:
- Analyzes natural language intent
- Routes to appropriate agent(s)
- Handles clarification when intent is ambiguous
- Orchestrates multi-agent workflows transparently

### 3. **Unified Responses**
Even when multiple agents collaborate:
- Single, coherent response
- Consistent formatting
- Clear progress updates
- Unified error handling

### 4. **Conversational Context**
The coordinator maintains context:
- Remembers previous requests
- Handles follow-up questions
- Preserves user preferences
- Tracks ongoing operations

### 5. **Progressive Disclosure**
Simple requests → Simple responses:
```bash
$ honua consultant "add layer from table cities"
✓ Layer 'cities' created
```

Complex requests → Detailed progress:
```bash
$ honua consultant "set up production"
⏳ Phase 1/4: Infrastructure...
⏳ Phase 2/4: Security...
[detailed progress]
```

---

## Error Handling (User Perspective)

### Clear Error Messages

**Bad** (exposes internal agent details):
```bash
❌ DeploymentConfigurationAgent failed: GitHubApiException at line 127
```

**Good** (user-friendly, actionable):
```bash
❌ Unable to create pull request

Reason: GitHub API rate limit exceeded
Solution: Please try again in 15 minutes, or configure a GitHub personal access token

Configure token: honua config set github.token <your-token>
```

### Graceful Degradation

If an agent fails, coordinator tries alternatives:

```bash
$ honua consultant "optimize parcels layer"

⏳ Analyzing performance...
⚠️ Unable to connect to pg_stat_statements extension
   Falling back to query plan analysis...

✓ Found optimization opportunities using alternative method
[continues with optimization]
```

### User-Friendly Fallback

When coordinator can't determine intent:

```bash
$ honua consultant "fix the thing"

I'm not sure what you'd like me to fix. Can you provide more details?

For example:
  • "Fix slow performance on parcels layer"
  • "Fix 500 errors in production"
  • "Fix broken authentication"

Or describe the issue you're experiencing: _
```

---

## Summary

**User Mental Model:**
```
I have a GIS consultant that I talk to.
It understands my requests and gets things done.
I don't need to know how it works internally.
```

**Actual Architecture:**
```
Sophisticated multi-agent system with:
  • Agent coordinator (intent analysis, routing, orchestration)
  • Specialized agents (deployment, performance, security, etc.)
  • Inter-agent communication protocols
  • GitOps and plan/apply workflows
  • State management and rollback capabilities
```

**Key Insight:** The power of the multi-agent architecture is completely hidden behind a simple, conversational CLI interface. Users get expert-level assistance without needing to understand the complexity underneath.

---

## Implementation Notes

### CLI Framework
- Use **Spectre.Console** for rich terminal UI
- Support both interactive and command modes
- Progress bars, spinners, tables for visual feedback
- Color coding for status (green ✓, red ❌, yellow ⚠️)

### Agent Coordinator Implementation
- **Semantic Kernel** for LLM-based intent analysis
- Pattern matching for common intents (fast path)
- LLM fallback for complex/ambiguous requests (slow path)
- Context store (in-memory + persistent for history)
- Workflow orchestration (sequential + parallel agent execution)

### Communication Protocol
- Request/response pattern with correlation IDs
- Async agent execution with progress callbacks
- Error propagation and retry logic
- Agent capability registration and discovery

---

**Next Steps:**
1. Implement CLI shell with Spectre.Console
2. Build Agent Coordinator with intent analysis
3. Implement first agent (DeploymentConfiguration) and integrate
4. Add multi-agent orchestration layer
5. Create comprehensive test suite with mock agents
