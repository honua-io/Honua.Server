# GitOps Workflow Examples

## Example 1: AI Consultant Proposes New Layer

### Scenario
A city wants to add a new "bike lanes" layer to their GIS system.

### Workflow

```bash
# 1. User asks AI consultant
$ honua-ai chat

User: "We need to add bike lanes data from our PostGIS database.
       The table is public.bike_lanes and has a geometry column."

AI: I'll help you add the bike lanes layer. Let me create a proposal.

# AI creates feature branch and metadata
Analyzing current metadata...
Creating deployment plan...
Validating configuration...

Created proposal: feature/add-bike-lanes-layer
Files changed:
  - environments/dev/layers/bike-lanes.yaml (new)
  - environments/staging/layers/bike-lanes.yaml (new)
  - environments/production/layers/bike-lanes.yaml (new)

Branch pushed to: origin/feature/add-bike-lanes-layer
Pull Request: https://github.com/city/honua-config/pull/42

# 2. Review the proposed changes
$ git checkout feature/add-bike-lanes-layer
$ cat environments/dev/layers/bike-lanes.yaml
```

```yaml
# environments/dev/layers/bike-lanes.yaml
apiVersion: honua.io/v1
kind: Layer
metadata:
  name: bike-lanes
  service: transportation
  environment: dev
  created: 2025-10-04T12:00:00Z
  createdBy: ai-consultant
  description: "Bicycle lane infrastructure"
spec:
  title: "Bike Lanes"
  description: "City bicycle lane network"
  geometryType: LineString
  idField: id
  displayField: name
  geometryField: geom

  crs:
    - EPSG:4326
    - EPSG:3857

  storage:
    table: bike_lanes
    geometryColumn: geom
    primaryKey: id
    srid: 4326

  fields:
    - name: id
      type: integer
      alias: "ID"
      nullable: false
    - name: name
      type: string
      alias: "Street Name"
      maxLength: 200
    - name: lane_type
      type: string
      alias: "Lane Type"
      maxLength: 50
    - name: width_ft
      type: double
      alias: "Width (feet)"
    - name: last_updated
      type: datetime
      alias: "Last Updated"

  ogc:
    itemLimit: 1000
    defaultCrs: EPSG:4326

  validation:
    healthChecks:
      - type: query
        query: "SELECT COUNT(*) FROM bike_lanes"
        expected: "> 0"
      - type: geometry
        query: "SELECT ST_IsValid(geom) FROM bike_lanes LIMIT 10"
        expected: "all true"
```

```bash
# 3. Test locally
$ honua dev apply --environment dev
Applying changes to dev environment...
✓ Validated metadata syntax
✓ Connected to datasource: postgis-primary
✓ Verified table exists: bike_lanes
✓ Validated geometry column: geom
✓ Created layer: bike-lanes

Testing layer...
✓ Health check passed
✓ OGC endpoint: http://localhost:5555/ogc/collections/transportation::bike-lanes
✓ Sample query returned 42 features

# 4. Run automated tests
$ honua test --layer bike-lanes
Running layer tests for bike-lanes...
✓ Metadata validation
✓ Data source connectivity
✓ Geometry validation
✓ OGC API conformance
✓ Performance benchmark (avg 45ms)

All tests passed!

# 5. Merge to main (triggers deployment pipeline)
$ git checkout main
$ git merge feature/add-bike-lanes-layer
$ git push origin main

# GitHub Actions automatically:
# - Validates changes
# - Deploys to dev
# - Runs integration tests
# - Deploys to staging (on success)
# - Awaits approval for production
```

## Example 2: Database Migration with Rollback

### Scenario
Need to add spatial index and optimize geometry column.

```yaml
# migrations/003_optimize_bike_lanes.yaml
apiVersion: honua.io/v1
kind: Migration
metadata:
  id: "003"
  description: "Add spatial index to bike lanes and optimize geometry"
  author: "gis-team"
  created: "2025-10-04T14:00:00Z"
  environments: ["dev", "staging", "production"]

spec:
  dependencies:
    - "002_add_parcels_layer"

  preConditions:
    - type: table-exists
      table: bike_lanes
    - type: column-exists
      table: bike_lanes
      column: geom

  up:
    - name: Create spatial index
      type: sql
      content: |
        CREATE INDEX idx_bike_lanes_geom
        ON bike_lanes
        USING GIST (geom);

    - name: Analyze table
      type: sql
      content: |
        ANALYZE bike_lanes;

    - name: Update metadata
      type: metadata
      action: update
      path: environments/{{env}}/layers/bike-lanes.yaml
      changes:
        spec.storage.indexes:
          - name: idx_bike_lanes_geom
            type: gist
            column: geom

  down:
    - name: Drop spatial index
      type: sql
      content: |
        DROP INDEX IF EXISTS idx_bike_lanes_geom;

    - name: Revert metadata
      type: metadata
      action: update
      path: environments/{{env}}/layers/bike-lanes.yaml
      changes:
        spec.storage.indexes: []

  validation:
    post-up:
      - type: query
        query: |
          SELECT indexname
          FROM pg_indexes
          WHERE tablename = 'bike_lanes'
          AND indexname = 'idx_bike_lanes_geom'
        expected: "1 row"

      - type: performance
        query: |
          EXPLAIN ANALYZE
          SELECT * FROM bike_lanes
          WHERE ST_Intersects(geom, ST_MakeEnvelope(-122.5, 45.5, -122.4, 45.6, 4326))
        expected: "Index Scan using idx_bike_lanes_geom"

    post-down:
      - type: query
        query: |
          SELECT indexname
          FROM pg_indexes
          WHERE tablename = 'bike_lanes'
          AND indexname = 'idx_bike_lanes_geom'
        expected: "0 rows"

  rollback:
    automatic: true
    triggers:
      - validation-failure
      - health-check-failure
    timeout: 5m
```

### Running the Migration

```bash
# 1. AI consultant plans the migration
$ honua-ai migrate plan

Analyzing migration: 003_optimize_bike_lanes
Dependencies: ✓ All satisfied
Pre-conditions: ✓ All met

Deployment Plan:
─────────────────────────────────────────────
Environment: dev
  Step 1: Create spatial index (estimated: 2s)
  Step 2: Analyze table (estimated: 1s)
  Step 3: Update metadata (estimated: <1s)

  Rollback Plan:
    - Drop spatial index
    - Revert metadata

  Risk Level: LOW
  Estimated Duration: 5s
  Automatic Rollback: YES

Environment: staging
  [Same as dev]
  Risk Level: LOW

Environment: production
  [Same as dev]
  Risk Level: MEDIUM (production environment)
  Approval Required: YES

Continue? (yes/no): yes

# 2. Execute migration on dev
$ honua deploy migration 003 --environment dev

Deploying migration 003 to dev...
✓ Pre-conditions verified
✓ Backup created: dev-backup-20251004-140530

Executing:
  [1/3] Creating spatial index... ✓ (1.8s)
  [2/3] Analyzing table... ✓ (0.9s)
  [3/3] Updating metadata... ✓ (0.2s)

Validating:
  ✓ Index exists
  ✓ Performance improved (250ms → 12ms)
  ✓ Health check passed

Migration 003 completed successfully on dev (3.2s)

# 3. Auto-promote to staging
$ honua-ai promote dev staging --migration 003

Validating dev deployment...
✓ All health checks passed
✓ No errors in last 30 minutes
✓ Performance metrics normal

Deploying to staging...
✓ Pre-conditions verified
✓ Backup created: staging-backup-20251004-140615
✓ Migration executed (3.1s)
✓ Validation passed

Migration 003 completed successfully on staging

# 4. Schedule production deployment
$ honua-ai schedule migration 003 \
    --environment production \
    --window "next-tuesday 10:00-12:00" \
    --approval-required

Scheduled deployment for migration 003:
  Environment: production
  Date: Tuesday, Oct 8, 2025
  Time: 10:00 AM PDT
  Duration: Up to 2 hours
  Approval: Required from gis-ops team

  Pre-deployment checklist:
    - Backup database: ✓ Automated
    - Health check: ✓ Automated
    - Rollback plan: ✓ Ready
    - Notification: ✓ Stakeholders notified
```

### Automatic Rollback Scenario

```bash
# Production deployment with failure
$ honua deploy migration 003 --environment production

Deploying migration 003 to production...
✓ Approval verified (approved by: john@city.gov)
✓ Pre-conditions verified
✓ Backup created: production-backup-20251008-100015

Executing:
  [1/3] Creating spatial index... ✓ (45s)
  [2/3] Analyzing table... ✓ (12s)
  [3/3] Updating metadata... ✓ (1s)

Validating:
  ✓ Index exists
  ✗ Performance test FAILED: Query timeout after 30s

⚠️  Validation failed! Automatic rollback initiated...

Rolling back:
  [1/2] Dropping spatial index... ✓ (2s)
  [2/2] Reverting metadata... ✓ (1s)

Validating rollback:
  ✓ Index removed
  ✓ Original performance restored
  ✓ Health check passed

Rollback completed successfully (5s)

Incident Report:
  Migration: 003_optimize_bike_lanes
  Environment: production
  Status: ROLLED BACK
  Reason: Performance validation failed
  Duration: 1m 5s
  Report: /deployments/003-production-rollback-20251008.md

Recommendations:
  1. Review query performance on production dataset
  2. Consider creating index with CONCURRENTLY option
  3. Test with production-scale data in staging

Next steps:
  - Incident logged: INC-20251008-001
  - Stakeholders notified
  - Database backup retained for 30 days
```

## Example 3: Multi-Environment Promotion

### Scenario
Successfully tested feature in staging, promote to production.

```bash
$ honua-ai promote staging production --auto-validate

Comparing environments...

Changes to be promoted:
─────────────────────────────────────────────
Metadata Changes:
  + layers/bike-lanes.yaml (new)
  ~ services/transportation.yaml (modified)

Migrations:
  + 003_optimize_bike_lanes (new)

Configuration Diff:
  staging → production
  - No incompatibilities detected
  - 2 new resources
  - 1 modified resource
  - 0 removals

Safety Checks:
  ✓ No breaking changes
  ✓ All dependencies available
  ✓ Staging healthy for 48 hours
  ✓ Zero errors in staging
  ✓ Performance metrics normal

Policy Check (production-policy.yaml):
  ✓ Within deployment window
  ✓ Approval requirement: Met
  ✓ Cooldown period: Satisfied
  ✓ Backup requirement: Configured

Deployment Plan:
─────────────────────────────────────────────
Phase 1: Backup (est. 2m)
  - Database snapshot
  - Metadata backup
  - Container images tagged

Phase 2: Apply (est. 5m)
  - Run migration 003
  - Update metadata
  - Reload services

Phase 3: Validate (est. 3m)
  - Health checks
  - OGC conformance
  - Performance tests

Total estimated time: 10 minutes
Rollback available: Yes (automatic on failure)

Proceed with promotion? (yes/no): yes

[10:00:00] Starting promotion staging → production
[10:00:05] ✓ Backup completed (5s)
[10:00:50] ✓ Migration 003 applied (45s)
[10:01:05] ✓ Metadata updated (15s)
[10:01:20] ✓ Services reloaded (15s)
[10:01:50] ✓ Health checks passed (30s)
[10:02:20] ✓ OGC conformance validated (30s)
[10:02:40] ✓ Performance benchmarks passed (20s)

Promotion completed successfully! (2m 40s)

Production Status:
  - Bike lanes layer: Active
  - Features available: 42
  - API endpoint: https://gis.city.gov/ogc/collections/transportation::bike-lanes
  - Health: 100%
```

## Example 4: Disaster Recovery

### Scenario
Production database corruption, need to restore from Git.

```bash
$ honua-ai disaster-recovery

⚠️  Disaster Recovery Mode Activated

What happened?
> Database corruption detected in production. Need to restore to last known good state.

AI: I'll help you recover. Let me analyze the situation.

Analyzing production state...
✗ Database health: CRITICAL
✗ Service availability: DEGRADED
✓ Git repository: INTACT
✓ Backups available: 7 days

Last successful deployment:
  - Commit: abc123def
  - Date: 2025-10-07 15:30:00 PDT
  - Environment: production
  - Status: HEALTHY

Recovery options:
  1. Restore from latest automated backup (2 hours ago)
  2. Restore from last successful deployment (1 day ago)
  3. Restore from specific Git commit

Which option? (1/2/3): 1

Initiating recovery from backup...

Recovery Plan:
─────────────────────────────────────────────
1. Put production in maintenance mode
2. Restore database from backup (2025-10-08 08:00:00)
3. Restore metadata from Git (commit: abc123def)
4. Validate data integrity
5. Run health checks
6. Exit maintenance mode

Estimated downtime: 15 minutes

Proceed? (yes/no): yes

[10:00:00] ✓ Maintenance mode activated
[10:00:05] ✓ Stopped accepting new requests
[10:02:30] ✓ Database restored from backup (2m 25s)
[10:03:00] ✓ Metadata restored from Git (30s)
[10:04:00] ✓ Data integrity validated (1m)
[10:04:30] ✓ Health checks passed (30s)
[10:04:35] ✓ Maintenance mode deactivated

Recovery completed successfully!

Status:
  - Database: HEALTHY
  - Services: HEALTHY
  - Data loss: 2 hours (08:00 - 10:00)
  - Downtime: 4m 35s (better than estimated!)

Post-recovery tasks:
  ✓ Incident logged: INC-20251008-002
  ✓ Stakeholders notified
  ✓ Recovery report generated

Next steps:
  1. Review logs for corruption cause
  2. Re-apply changes from last 2 hours (if any)
  3. Monitor for recurring issues
```

These examples show how the GitOps system provides:
- **Reliability**: Automated testing and validation
- **Safety**: Automatic rollback on failure
- **Auditability**: Complete history in Git
- **Consistency**: Same process across environments
- **Speed**: Automated deployments and promotions
- **Recovery**: One-command disaster recovery
