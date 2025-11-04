# Honua AI Consultant - Complete System Overview

## Executive Summary

The Honua AI Consultant is an intelligent DevOps automation system that combines GitOps workflows, deployment state management, and topology-aware orchestration to provide safe, reliable, and automated GIS infrastructure management.

## How Everything Fits Together

```
┌────────────────────────────────────────────────────────────────────┐
│                      HONUA AI CONSULTANT                            │
│                                                                      │
│  ┌──────────────┐    ┌─────────────┐    ┌──────────────────┐      │
│  │   Chat UI    │───▶│ AI Agent    │───▶│ Action Executor  │      │
│  │   CLI Tool   │    │ (Claude)    │    │ (GitOps Engine)  │      │
│  └──────────────┘    └─────────────┘    └──────────────────┘      │
│                              │                      │                │
└──────────────────────────────┼──────────────────────┼───────────────┘
                               │                      │
                               ▼                      ▼
                    ┌──────────────────┐   ┌──────────────────┐
                    │   Git Repository │   │ State Management │
                    │  (honua-config)  │   │ (FileStore/      │
                    │                  │   │  GitHub API)     │
                    │ - metadata.yaml  │   │                  │
                    │ - topology.yaml  │   │ - Deployments    │
                    │ - migrations/    │   │ - Health Status  │
                    │ - policies/      │   │ - Sync Status    │
                    └──────────────────┘   └──────────────────┘
                               │                      │
                               ▼                      ▼
                    ┌──────────────────────────────────────────┐
                    │     GitHub Actions / CI/CD Pipeline      │
                    │                                           │
                    │  Validate → Plan → Approve → Deploy      │
                    └──────────────────────────────────────────┘
                               │
                               ▼
         ┌─────────────────────────────────────────────────┐
         │           DEPLOYMENT TOPOLOGY                   │
         │                                                 │
         │  ┌────┐ ┌────┐ ┌────┐ ┌─────┐ ┌──────────┐   │
         │  │DNS │→│CDN │→│WAF │→│ LB  │→│  Honua   │   │
         │  └────┘ └────┘ └────┘ └─────┘ │  Servers │   │
         │                        │       └──────────┘   │
         │                        ▼              │        │
         │                  ┌──────────┐         ▼        │
         │                  │  Tile    │   ┌──────────┐  │
         │                  │  Cache   │   │ Database │  │
         │                  └──────────┘   └──────────┘  │
         └─────────────────────────────────────────────────┘
```

## Core Components

### 1. AI Consultant (User Interface)

**What it does:**
- Conversational interface for GIS infrastructure management
- Understands natural language requests
- Proposes changes with context and rationale
- Creates PRs for human review

**Example interactions:**
```bash
User: "Add a new bike lanes layer from PostGIS table public.bike_lanes"

AI: I'll help you add that layer. Let me:
1. Analyze the PostGIS table schema
2. Generate metadata configuration
3. Create deployment plan
4. Show you the proposed changes

[Creates PR with metadata changes]
```

### 2. GitOps Workflow (Version Control)

**What it does:**
- Git as single source of truth
- All changes tracked in version control
- PR-based approval workflow
- Automatic deployment on merge

**Repository structure:**
```
honua-config/
├── environments/
│   ├── dev/
│   │   ├── layers/
│   │   │   └── bike-lanes.yaml
│   │   ├── services/
│   │   └── datasources/
│   ├── staging/
│   └── production/
├── migrations/
│   ├── 001_initial.sql
│   ├── 002_add_bike_lanes.sql
│   └── 002_add_bike_lanes.yaml  # Migration metadata
├── policies/
│   ├── dev-policy.yaml
│   ├── staging-policy.yaml
│   └── production-policy.yaml
└── topologies/
    ├── dev-topology.yaml
    ├── staging-topology.yaml
    └── production-topology.yaml
```

**Documents:**
- `docs/dev/gitops-architecture.md` - Architecture and state machine
- `docs/dev/gitops-example-workflow.md` - Real-world examples

### 3. Deployment State Machine (Coordination)

**What it does:**
- Coordinates multi-step deployments
- Tracks state transitions
- Enables automatic rollback
- Provides audit trail

**State Flow:**
```
Pending → Validating → Planning → AwaitingApproval → BackingUp →
Applying → PostValidating → Completed
                              ↓
                        (on failure)
                              ↓
                         RollingBack → RolledBack
```

**State Stores:**
- **FileStateStore** - Simple JSON files (good for development/single-server)
- **GitHubStateStore** - Uses GitHub Deployments API (best for CI/CD)
- **Azure CosmosDB** - For production multi-region (future)

**Implementation:**
- `src/Honua.Server.Core/Deployment/DeploymentState.cs` - State enums
- `src/Honua.Server.Core/Deployment/Models.cs` - Data models
- `src/Honua.Server.Core/Deployment/FileStateStore.cs` - File-based implementation
- `src/Honua.Server.Core/Deployment/IDeploymentStateStore.cs` - Interface

### 4. Deployment Topology (Infrastructure Awareness)

**What it does:**
- Maps your entire deployment architecture
- Understands dependencies between components
- Coordinates changes across all layers
- Handles post-deployment tasks

**Topology Definition:**
```yaml
apiVersion: honua.io/v1
kind: Topology
metadata:
  name: production-us-west
  environment: production
spec:
  cdn:
    provider: cloudflare
    purgeStrategy:
      onDeploy: selective  # Flush CDN after deploy

  applicationServers:
    count: 3
    deploymentStrategy:
      type: rolling
      maxUnavailable: 0  # Zero-downtime

  tileCache:
    provider: s3
    invalidationStrategy:
      onDeploy: selective  # Invalidate tile cache

  database:
    provider: postgresql
    deploymentActions:
      postDeploy:
        - action: reindex      # Rebuild spatial indexes
          concurrent: true
```

**Document:**
- `docs/dev/deployment-topology.md` - Complete topology management system

### 5. GitHub Actions Integration (Automation)

**What it does:**
- Automatic validation on PR
- Deployment to dev on merge
- Promotion to staging after validation
- Human approval gate for production

**Workflow:**
```yaml
# .github/workflows/honua-deploy.yml
on:
  push:
    branches: [main]
    paths: ['environments/**']

jobs:
  validate:
    - Syntax validation
    - Policy checks
    - Breaking change detection

  deploy-dev:
    - Auto-deploy to dev
    - Run health checks

  deploy-staging:
    - Auto-deploy to staging
    - Run integration tests

  deploy-production:
    environment: production  # Requires approval
    - Human approval via GitHub
    - Deploy with auto-rollback
    - Post-deployment validation
```

## End-to-End Example: Adding a New Layer

Let's walk through a complete workflow:

### Step 1: User Request

```bash
$ honua-ai chat

User: "Add bike lanes layer from PostGIS table public.bike_lanes"
```

### Step 2: AI Analysis & Planning

```
AI: Analyzing request...

I'll help you add the bike lanes layer. Here's what I found:
- Table: public.bike_lanes (PostGIS)
- Geometry: LineString (SRID 4326)
- Fields: id, name, lane_type, width_ft, last_updated

Creating deployment plan:

Dev Environment:
  - Create layer metadata (bike-lanes.yaml)
  - No migration needed (table exists)
  - Estimated deployment: 30 seconds

Staging Environment:
  - Same as dev

Production Environment:
  - Same as dev
  - Requires approval
  - Post-deployment: Regenerate overview tiles (zoom 0-5)

Risk Level: LOW
Breaking Changes: None

Shall I create a PR with these changes?
```

### Step 3: AI Creates PR

```
AI: Creating feature branch and PR...

Created:
  - environments/dev/layers/bike-lanes.yaml
  - environments/staging/layers/bike-lanes.yaml
  - environments/production/layers/bike-lanes.yaml

Pull Request: https://github.com/city/honua-config/pull/42
Title: "Add bike lanes layer"

The PR includes:
- Full metadata configuration
- Validation results
- Deployment impact analysis
```

### Step 4: Human Review

```bash
$ gh pr view 42

# Review shows:
- Diff of all changes
- CI/CD validation ✓ passed
- No breaking changes
- Deployment plan for all environments
```

### Step 5: Merge & Auto-Deploy

```bash
$ gh pr merge 42

# GitHub Actions automatically:
1. Validates changes
2. Creates deployment record (state: Pending)
3. Deploys to dev environment
   - State: Validating → Planning → Applying → PostValidating
   - Health checks pass
   - State: Completed
4. Deploys to staging environment
   - Same state flow
   - Integration tests pass
   - State: Completed
5. Awaits approval for production
   - State: AwaitingApproval
```

### Step 6: Production Deployment

```bash
# GIS Ops team approves in GitHub UI

# GitHub Actions resumes:
1. Checks topology definition
2. Identifies components to update:
   - Honua servers (3): Rolling update
   - CDN: Selective cache purge
   - Tile cache: Queue regeneration for zoom 0-5
   - Spatial indexes: Not affected (table unchanged)
3. Creates backup (state: BackingUp)
4. Executes deployment (state: Applying)
   - Updates server 1 → health check → ✓
   - Updates server 2 → health check → ✓
   - Updates server 3 → health check → ✓
5. Post-deployment (state: PostValidating)
   - Purges CDN cache for /ogc/collections
   - Queues tile regeneration (background)
   - Runs OGC conformance tests → ✓
6. Completes successfully (state: Completed)

Deployment completed in 4m 32s
Zero downtime ✓
```

## Key Patterns from ArgoCD

1. **Sync Status**: Track whether deployed state matches Git
   ```csharp
   public enum SyncStatus {
       Synced,      // Git == Deployed
       OutOfSync,   // Git != Deployed
       Syncing      // Deployment in progress
   }
   ```

2. **Health Status**: Monitor application health
   ```csharp
   public enum DeploymentHealth {
       Healthy,      // All checks passing
       Progressing,  // Deployment ongoing
       Degraded,     // Partial functionality
       Unhealthy     // Failed
   }
   ```

3. **Sync Waves**: Ordered deployment phases
   ```yaml
   deploymentCoordination:
     phases:
       - name: database
         syncWave: 0
       - name: application
         syncWave: 1
       - name: edge
         syncWave: 2
   ```

## Key Patterns from JenkinsX

1. **GitOps Promotion**: Automatic PR creation for environment promotion
   ```bash
   $ honua promote staging production
   # Creates PR to merge staging config → production config
   ```

2. **Preview Environments**: Temporary environments for PRs
   ```yaml
   on:
     pull_request:
       paths: ['environments/**']

   jobs:
     preview:
       # Deploy to preview-pr-42 environment
       # Run tests
       # Comment results on PR
   ```

3. **ChatOps**: Bot commands in PRs
   ```
   # In PR comments:
   /honua deploy dev
   /honua diff staging production
   /honua rollback production
   ```

## Safety Features

1. **Multi-stage validation**
   - Syntax (YAML/JSON)
   - Policy compliance
   - Breaking change detection
   - Resource availability

2. **Automatic rollback**
   - On health check failure
   - On validation failure
   - Restores from backup
   - Updates state: Failed → RollingBack → RolledBack

3. **Deployment policies**
   - Time windows (e.g., Tue-Thu 9am-5pm)
   - Approval requirements
   - Cooldown periods
   - Concurrent deployment limits

4. **Audit trail**
   - Git history (what changed)
   - Deployment history (when deployed)
   - State transitions (how it progressed)
   - Health checks (validation results)

## State Persistence Strategies

### Development: FileStateStore
```bash
.honua/state/
├── dev.json
├── staging.json
└── production.json
```
- Simple JSON files
- Great for local development
- Easy to inspect and debug

### CI/CD: GitHub Deployments API
```csharp
// Uses GitHub's built-in deployment tracking
await github.Repository.Deployment.Create(...)
await github.Repository.Deployment.Status.Create(...)
```
- No extra infrastructure
- Integrates with GitHub UI
- Automatic deployment history

### Production: Future - Cosmos DB / DynamoDB
- Multi-region replication
- High availability
- Query capabilities
- Real-time updates

## Topology-Aware Deployment

The topology definition tells the deployment engine:

1. **What components exist**
   - CDN, WAF, load balancer, app servers, databases, caches

2. **How they're connected**
   - DNS → CDN → WAF → LB → App → DB
   - CDN → Tile Cache (S3)

3. **What needs to happen on deployment**
   - Flush CDN cache
   - Rolling restart of app servers
   - Invalidate tile cache
   - Rebuild spatial indexes
   - Regenerate overview tiles

4. **In what order**
   - Phase 1: Database (migrations, indexes)
   - Phase 2: Application (rolling update)
   - Phase 3: Edge (CDN, DNS)
   - Phase 4: Background (tile generation)

## Documents Reference

| Document | Purpose |
|----------|---------|
| `gitops-architecture.md` | GitOps system design and state machine |
| `gitops-example-workflow.md` | Real-world scenarios and examples |
| `deployment-topology.md` | Topology definition and management |
| `honua-ai-consultant-overview.md` | This document - complete system overview |

## Implementation Status

### ✅ Completed
- GitOps architecture design
- Deployment state machine design
- Topology definition language
- FileStateStore implementation
- State models and interfaces

### 🚧 In Progress
- GitHubStateStore implementation
- Honua CLI deployment commands
- GitHub Actions workflows

### 📋 Planned
- Topology discovery
- Migration framework
- AI consultant integration
- ChatOps commands
- Backup/restore system

## Next Steps

1. **Finish GitHubStateStore**
   - Implement using Octokit
   - Test with real GitHub repo

2. **Create Honua CLI**
   - `honua deploy`
   - `honua rollback`
   - `honua status`
   - `honua promote`

3. **Build GitHub Actions workflows**
   - Validation workflow
   - Deployment workflow
   - Promotion workflow

4. **Implement topology discovery**
   - Auto-detect infrastructure
   - Generate topology.yaml
   - Validate configuration

## Conclusion

This system provides:
- ✅ **Reliability** - Git history + state tracking + automatic rollback
- ✅ **Safety** - Multi-stage validation + approval gates + policies
- ✅ **Visibility** - Complete audit trail + deployment history + health monitoring
- ✅ **Automation** - GitOps + CI/CD + AI assistance
- ✅ **Intelligence** - Topology awareness + risk assessment + AI recommendations

The AI Consultant orchestrates all of this, providing a conversational interface for GIS teams to safely manage complex infrastructure without needing deep DevOps expertise.
