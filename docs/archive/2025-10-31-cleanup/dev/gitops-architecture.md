# Honua GitOps Architecture

## Overview

GitOps-based deployment system for Honua that treats Git as the single source of truth for:
- Metadata configuration (services, layers, data sources)
- Database migrations (schema changes)
- Software version upgrades
- Deployment policies and safety rules

## Key Features

### 1. **Declarative Configuration**
All configuration in Git using YAML/JSON:
```
honua-config/
├── environments/
│   ├── dev/
│   │   ├── metadata.yaml
│   │   ├── datasources.yaml
│   │   └── deployment.yaml
│   ├── staging/
│   │   └── ... (same structure)
│   └── production/
│       └── ... (same structure)
├── migrations/
│   ├── 001_initial_schema.sql
│   ├── 002_add_parcels_layer.sql
│   └── metadata.yaml  # Migration metadata
├── policies/
│   ├── production-policy.yaml
│   └── staging-policy.yaml
└── .honua/
    └── deployment-history/  # Auto-generated
```

### 2. **Deployment Phases**

```
┌─────────────────┐
│  Git Commit     │
│  (Metadata)     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  GitOps Agent   │
│  Detects Change │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Validation     │
│  • Syntax       │
│  • Policy       │
│  • Safety       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Plan Phase     │
│  • Diff         │
│  • Dependencies │
│  • Migrations   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Approval       │  ← Human/Auto
│  (if required)  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Backup         │
│  • Metadata     │
│  • Database     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Apply          │
│  • Migrations   │
│  • Metadata     │
│  • Services     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Validation     │
│  • Health Check │
│  • OGC Tests    │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌────────┐ ┌──────────┐
│Success │ │  Failure │
└────────┘ └────┬─────┘
                │
                ▼
         ┌──────────────┐
         │  Rollback    │
         │  Automatic   │
         └──────────────┘
```

### 3. **Rollback Strategies**

#### Automatic Rollback Triggers:
- Health check failures
- OGC conformance test failures
- Service startup failures
- Database migration errors

#### Rollback Methods:
1. **Metadata Rollback**: Revert to previous Git commit
2. **Database Rollback**: Run down migrations
3. **Software Rollback**: Revert to previous container/binary
4. **Combined Rollback**: All of the above in coordinated fashion

### 4. **Deployment State Machine**

```csharp
public enum DeploymentState
{
    Pending,          // Detected change
    Validating,       // Running pre-checks
    Planning,         // Generating deployment plan
    AwaitingApproval, // Human approval required
    BackingUp,        // Creating backups
    Applying,         // Executing changes
    Validating,       // Post-deployment checks
    Completed,        // Success
    Failed,           // Error occurred
    RollingBack,      // Reverting changes
    RolledBack        // Rollback completed
}
```

### 5. **Safety Features**

#### Pre-Deployment Checks:
- ✅ Metadata syntax validation
- ✅ Policy compliance check
- ✅ Breaking change detection
- ✅ Resource availability check
- ✅ Concurrent deployment prevention
- ✅ Dependency validation

#### Post-Deployment Validation:
- ✅ Service health checks
- ✅ OGC conformance tests
- ✅ Data integrity checks
- ✅ Performance benchmarks

#### Deployment Policies:
```yaml
# policies/production-policy.yaml
apiVersion: honua.io/v1
kind: DeploymentPolicy
metadata:
  name: production-policy
  environment: production
spec:
  approval:
    required: true
    approvers:
      - team:gis-ops
      - role:admin
    timeout: 24h

  deploymentWindow:
    allowedDays: [Tuesday, Wednesday, Thursday]
    allowedHours: "09:00-17:00"
    timezone: America/Los_Angeles

  rollback:
    automatic: true
    timeout: 5m
    healthCheckInterval: 30s

  constraints:
    maxConcurrentDeployments: 1
    cooldownPeriod: 1h
    requireBackup: true

  validation:
    preDeployment:
      - syntaxCheck
      - policyCheck
      - breakingChangeDetection
    postDeployment:
      - healthCheck
      - ogcConformance
      - smokeTests
```

### 6. **AI Consultant Integration**

The AI consultant can:

1. **Propose Changes**:
   ```bash
   honua-ai propose "Add new parcels layer with PostGIS backend"
   # → Creates branch with proposed metadata changes
   # → Opens PR for human review
   ```

2. **Review Deployments**:
   ```bash
   honua-ai review deployment-123
   # → Analyzes deployment plan
   # → Identifies risks
   # → Suggests improvements
   ```

3. **Coordinate Multi-Environment Rollout**:
   ```bash
   honua-ai promote --from staging --to production
   # → Validates staging deployment
   # → Creates production deployment plan
   # → Schedules deployment within policy window
   ```

4. **Automated Rollback Decision**:
   - AI monitors deployment health
   - Automatically triggers rollback on failure
   - Generates incident report

### 7. **Migration Management**

```yaml
# migrations/002_add_parcels_layer.yaml
apiVersion: honua.io/v1
kind: Migration
metadata:
  id: 002
  description: Add parcels layer with spatial index
  author: ai-consultant
  created: 2025-10-04T12:00:00Z
spec:
  dependencies:
    - 001_initial_schema

  up:
    - type: sql
      file: 002_add_parcels_layer.sql
    - type: metadata
      action: add
      resource: layers/parcels.yaml

  down:
    - type: metadata
      action: remove
      resource: layers/parcels
    - type: sql
      file: 002_add_parcels_layer_down.sql

  validation:
    - type: query
      query: "SELECT COUNT(*) FROM spatial_ref_sys WHERE srid = 4326"
      expected: "> 0"
    - type: ogc
      endpoint: /ogc/collections/parcels
      expectedStatus: 200
```

### 8. **Deployment Metrics**

Track and visualize:
- Deployment frequency
- Success/failure rate
- Mean time to recovery (MTTR)
- Rollback frequency
- Deployment duration
- Change lead time

### 9. **Example Workflow**

#### Scenario: Add New Layer to Production

1. **Development**:
   ```bash
   # AI or developer creates feature branch
   git checkout -b feature/add-zoning-layer

   # Add metadata configuration
   honua-ai generate layer --name zoning --type polygon \
     --datasource postgis-primary --table public.zoning

   # Test locally
   honua dev apply --environment dev
   honua dev test

   # Commit
   git add environments/dev/layers/zoning.yaml
   git commit -m "Add zoning layer"
   git push origin feature/add-zoning-layer
   ```

2. **CI/CD Pipeline**:
   ```yaml
   # .github/workflows/honua-deploy.yml
   on:
     push:
       paths:
         - 'environments/**'

   jobs:
     validate:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v3
         - name: Validate metadata
           run: honua validate --strict
         - name: Check policies
           run: honua policy-check --environment dev

     deploy-dev:
       needs: validate
       if: github.ref == 'refs/heads/main'
       runs-on: ubuntu-latest
       steps:
         - name: Deploy to dev
           run: honua deploy --environment dev --auto-approve

     deploy-staging:
       needs: deploy-dev
       runs-on: ubuntu-latest
       steps:
         - name: Deploy to staging
           run: honua deploy --environment staging --auto-approve
         - name: Run integration tests
           run: honua test --environment staging

     deploy-production:
       needs: deploy-staging
       environment: production  # GitHub environment protection
       runs-on: ubuntu-latest
       steps:
         - name: Deploy to production
           run: honua deploy --environment production
           # Requires manual approval via GitHub
   ```

3. **Automated Promotion**:
   ```bash
   # After staging validates successfully
   honua-ai promote staging production \
     --schedule "next-tuesday 10:00" \
     --with-validation \
     --auto-rollback
   ```

### 10. **Disaster Recovery**

```bash
# Complete environment restore from Git
honua restore \
  --environment production \
  --commit abc123 \
  --include-database \
  --verify
```

## Implementation Phases

### Phase 1: Core GitOps (Week 1-2)
- [ ] Git repository watcher
- [ ] Metadata validation
- [ ] Basic deployment state machine
- [ ] Manual approval workflow

### Phase 2: Safety & Rollback (Week 3-4)
- [ ] Backup/restore system
- [ ] Automatic rollback
- [ ] Health checks
- [ ] Deployment policies

### Phase 3: Migrations (Week 5-6)
- [ ] Migration framework
- [ ] Up/down migration support
- [ ] Migration dependencies
- [ ] State tracking

### Phase 4: AI Integration (Week 7-8)
- [ ] AI proposal system
- [ ] Automated review
- [ ] Multi-environment promotion
- [ ] Incident response

### Phase 5: Monitoring & Metrics (Week 9-10)
- [ ] Deployment dashboard
- [ ] Alerting
- [ ] Audit logging
- [ ] Performance tracking

## Benefits

1. **Reliability**: Git history provides complete audit trail
2. **Safety**: Multi-stage validation and automatic rollback
3. **Speed**: Automated deployment reduces manual errors
4. **Consistency**: Same process across all environments
5. **Visibility**: Clear deployment history and status
6. **Collaboration**: PR-based workflow for changes
7. **Disaster Recovery**: One-command environment restoration
