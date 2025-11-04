# AI Consultant Safety & Guardrails

## The Challenge

Giving an AI agent DevOps control creates legitimate concerns:
- **Accidental production changes** - AI might misunderstand and deploy to wrong environment
- **Data loss** - Incorrect migrations or deletions
- **Service outages** - Breaking changes without proper validation
- **Security risks** - Exposing credentials or weakening security
- **Compliance violations** - Changes that violate policies or regulations
- **Cost overruns** - Spinning up expensive resources

## Safety Philosophy

**Principle: Trust but Verify**
- AI can **propose** anything
- AI can **execute** only safe, pre-approved operations
- Humans **approve** all high-risk changes
- System **automatically prevents** dangerous operations

## Multi-Layer Safety System

```
┌─────────────────────────────────────────────────────────────┐
│                     USER REQUEST                            │
└────────────────────────┬────────────────────────────────────┘
                         ▼
         ┌───────────────────────────────┐
         │  Layer 1: Intent Validation   │
         │  - Clarify ambiguous requests │
         │  - Confirm destructive ops    │
         └───────────────┬───────────────┘
                         ▼
         ┌───────────────────────────────┐
         │  Layer 2: Permission Check    │
         │  - User role verification     │
         │  - Operation authorization    │
         └───────────────┬───────────────┘
                         ▼
         ┌───────────────────────────────┐
         │  Layer 3: Environment Safety  │
         │  - Production requires approval│
         │  - Dev/staging auto-allowed   │
         └───────────────┬───────────────┘
                         ▼
         ┌───────────────────────────────┐
         │  Layer 4: Dry Run             │
         │  - Show what WOULD happen     │
         │  - User confirms before exec  │
         └───────────────┬───────────────┘
                         ▼
         ┌───────────────────────────────┐
         │  Layer 5: Policy Validation   │
         │  - Check deployment policies  │
         │  - Verify constraints         │
         └───────────────┬───────────────┘
                         ▼
         ┌───────────────────────────────┐
         │  Layer 6: Change Preview      │
         │  - Show diff                  │
         │  - Impact analysis            │
         │  - Risk assessment            │
         └───────────────┬───────────────┘
                         ▼
         ┌───────────────────────────────┐
         │  Layer 7: Approval Gate       │
         │  - Create PR (requires merge) │
         │  - Or explicit approval       │
         └───────────────┬───────────────┘
                         ▼
         ┌───────────────────────────────┐
         │  Layer 8: Execution Limits    │
         │  - Rate limiting              │
         │  - Concurrent change limits   │
         │  - Rollback on failure        │
         └───────────────┬───────────────┘
                         ▼
         ┌───────────────────────────────┐
         │  Layer 9: Audit Trail         │
         │  - Log all actions            │
         │  - Immutable history          │
         └───────────────────────────────┘
```

## Layer 1: Intent Validation

### Ambiguity Detection

```typescript
// AI Consultant behavior
User: "Deploy the changes"

AI: ⚠️  I need clarification:
    - Which environment? (dev, staging, production)
    - Which changes? (3 commits pending in staging, 5 in production)
    - When? (now, or schedule for later)

    Please specify, for example:
    - "Deploy staging changes to production"
    - "Deploy latest commit to dev"
```

### Destructive Operation Confirmation

```typescript
User: "Delete the old parcels layer"

AI: ⚠️  DESTRUCTIVE OPERATION DETECTED

    You're asking me to DELETE a layer. This will:
    - Remove layer configuration
    - Break existing API endpoints
    - Affect any applications using this layer

    This layer currently serves:
    - 2,341 requests/day
    - Used by: web-viewer, mobile-app, city-dashboard

    ❌ I cannot execute this directly.

    Instead, I can:
    1. Create a deprecation plan (recommended)
       - Mark layer as deprecated
       - Set sunset date
       - Notify API consumers
       - Create migration guide

    2. Create a PR to remove the layer
       - You review and approve
       - Requires approval from 2 team members

    Which would you prefer?
```

## Layer 2: Permission System

### Role-Based Access Control (RBAC)

```yaml
# ai-consultant-rbac.yaml
apiVersion: honua.io/v1
kind: AIConsultantPermissions

roles:
  - name: developer
    permissions:
      # Read permissions
      - view:environments
      - view:deployments
      - view:topology

      # Dev environment - full access
      - propose:dev:*
      - execute:dev:deploy
      - execute:dev:rollback

      # Staging - propose only
      - propose:staging:*

      # Production - read only
      ❌ No production access

  - name: gis-ops
    permissions:
      # All developer permissions
      - inherit:developer

      # Staging - full access
      - execute:staging:deploy
      - execute:staging:rollback

      # Production - propose + approve
      - propose:production:*
      - approve:production:deploy
      - execute:production:rollback  # Emergency only

  - name: admin
    permissions:
      - "*"  # Full access

  - name: ai-consultant
    # What the AI can do WITHOUT human approval
    auto_execute:
      # Safe read operations
      - view:*
      - analyze:*
      - validate:*
      - dry-run:*

      # Dev environment - auto deploy
      - deploy:dev
      - rollback:dev

      # Everything else - propose only

    requires_approval:
      # Staging changes
      - deploy:staging
      - rollback:staging

      # Production changes
      - deploy:production
      - rollback:production

      # Destructive operations
      - delete:*
      - drop:*
      - truncate:*

      # Security changes
      - security:*
      - credentials:*

      # Cost-impacting changes
      - scale-up:*
      - provision:*

  - name: ai-consultant-restricted
    # Extra-safe mode for initial rollout
    auto_execute:
      # Read-only operations
      - view:*
      - analyze:*
      - validate:*
      - dry-run:*

    requires_approval:
      # EVERYTHING else requires approval
      - "*"
```

### Permission Checks in Action

```typescript
User: "Deploy to production"

AI: 🔒 Permission Check:
    Operation: deploy:production
    Your role: gis-ops
    AI permission: requires_approval

    ✓ You have permission to approve this
    ✗ I cannot auto-execute this

    I'll create a deployment plan for your approval.

    [Creating plan...]

    Deployment plan ready. Options:
    1. Review and approve now (immediate deployment)
    2. Create PR for team review
    3. Schedule for maintenance window

    What would you like to do?
```

## Layer 3: Environment-Based Safety

### Environment Tiers

```yaml
environments:
  dev:
    safety_level: low
    auto_approve: true
    requires_pr: false
    rollback: automatic

  staging:
    safety_level: medium
    auto_approve: false  # Requires explicit approval
    requires_pr: false
    rollback: automatic
    approval_timeout: 1h

  production:
    safety_level: critical
    auto_approve: false
    requires_pr: true  # Always create PR
    rollback: automatic
    approval_timeout: 24h

    # Additional production safeguards
    require_successful_staging: true
    minimum_staging_soak_time: 24h
    require_approvers: 2
    require_different_approvers: true  # Can't approve your own change
    block_friday_deployments: true  # No weekend heroics
    deployment_window:
      days: [Tuesday, Wednesday, Thursday]
      hours: "09:00-17:00"
      timezone: America/Los_Angeles
```

## Layer 4: Dry Run & Preview

### Always Show Impact First

```bash
User: "Add zoning layer to production"

AI: I'll prepare to add the zoning layer to production.
    Running dry-run first...

┌─ DRY RUN RESULTS ─────────────────────────────────────────────────┐
│ Environment: production                                           │
│ Operation: Add layer (zoning)                                     │
│ Mode: DRY RUN (no changes will be made)                           │
└───────────────────────────────────────────────────────────────────┘

Changes that WOULD be made:

📝 Metadata Changes:
  + environments/production/layers/zoning.yaml (new file)
  ~ environments/production/services/planning.yaml (modified)

🗄️ Database Changes:
  ✓ Table exists: zoning_districts
  ✓ Spatial index exists: idx_zoning_geom
  ⚠ Table has 145,234 rows (large dataset - queries may be slow)

🔧 Infrastructure Changes:
  - App servers: Rolling restart (3 servers, 0 downtime)
  - CDN: Selective cache purge (~150 objects)
  - Tile cache: Regenerate zoom 0-5 (est. 2h background job)

📊 Impact Analysis:
  New API endpoint: /ogc/collections/planning::zoning
  Estimated QPS: 50 (based on similar layers)
  Storage impact: +12MB metadata
  Database load: +2% (estimated)

🎯 Risk Assessment:
  Overall risk: MEDIUM
  Reasons:
    ⚠ Large dataset (145K rows) - first query may be slow
    ⚠ No spatial index on zoning_type column (filtered queries will be slow)

🔍 Recommendations:
  1. Add index on zoning_type column before deployment
  2. Pre-warm cache with common queries
  3. Set maxRecordCount: 5000 (prevent huge queries)

⏱️ Estimated Deployment Time: 6 minutes
🔄 Automatic Rollback: Enabled

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

This is a DRY RUN. No changes have been made.

What would you like to do?
1. ✓ Apply recommendations and proceed
2. 📝 Proceed without recommendations (not recommended)
3. 🔍 Show detailed diff
4. ❌ Cancel

Your choice:
```

## Layer 5: Policy Enforcement

### Deployment Policies as Code

```yaml
# policies/production-safety-policy.yaml
apiVersion: honua.io/v1
kind: SafetyPolicy
metadata:
  name: production-safety
  environment: production

spec:
  # Prevent accidental dangerous operations
  blocked_operations:
    - operation: delete_layer
      message: "Layers cannot be deleted in production. Use deprecation workflow instead."

    - operation: drop_table
      message: "Table drops are not allowed. Contact DBA team."

    - operation: truncate_table
      message: "Data truncation is not allowed in production."

    - operation: disable_authentication
      message: "Authentication cannot be disabled in production."

    - operation: expose_credentials
      message: "Credentials cannot be exposed in metadata."

  # Require additional checks for certain operations
  enhanced_validation:
    - operation: add_layer
      checks:
        - type: table_size
          threshold: 1000000
          action: require_index_check
          message: "Large tables require spatial index verification"

        - type: breaking_change
          action: block
          message: "Breaking changes require migration plan"

        - type: cost_impact
          threshold: high
          action: require_approval
          approvers: ["finance-team"]

  # Automatic safety improvements
  auto_fixes:
    - issue: missing_max_record_count
      fix: set_default
      value: 5000
      message: "Setting maxRecordCount to 5000 to prevent large queries"

    - issue: missing_cache_header
      fix: add_cache_control
      value: "public, max-age=300"

    - issue: weak_authentication
      fix: upgrade_to_jwt
      message: "Upgrading to JWT authentication"

  # Rate limits
  rate_limits:
    deployments_per_day: 3
    deployments_per_hour: 1
    concurrent_deployments: 1

  # Required validations
  required_validations:
    - health_check
    - ogc_conformance
    - performance_benchmark
    - security_scan
    - breaking_change_detection

  # Rollback requirements
  rollback:
    automatic: true
    triggers:
      - health_check_failure
      - error_rate > 5%
      - latency_p95 > 2000ms
    max_rollback_time: 5m
```

### Policy Violation Handling

```typescript
User: "Delete the parcels layer from production"

AI: 🚫 POLICY VIOLATION

    Policy: production-safety-policy
    Violation: blocked_operations.delete_layer

    Reason: Layers cannot be deleted in production.
            Use deprecation workflow instead.

    ❌ I cannot proceed with this operation.

    📋 Recommended Alternative:
    I can help you deprecate this layer safely:

    1. Mark layer as deprecated (with sunset date)
    2. Add deprecation notice to API responses
    3. Notify consumers via:
       - API headers (Sunset, Deprecation)
       - Email to registered API users
       - Documentation update
    4. Monitor usage decrease
    5. Remove after 90-day deprecation period

    Would you like me to start the deprecation workflow?
```

## Layer 6: Approval Workflows

### Multiple Approval Patterns

#### 1. Inline Approval (for ops team)

```bash
AI: Ready to deploy. This requires your approval.

┌─ APPROVAL REQUIRED ───────────────────────────────────────────────┐
│ Operation: Deploy to production                                   │
│ Commit: abc123d                                                   │
│ Risk: MEDIUM                                                      │
│ Changes: +1 layer, ~2 layers                                      │
└───────────────────────────────────────────────────────────────────┘

Type 'approve' to proceed, or 'deny' to cancel: approve

⏳ Verifying your identity...
✓ Identity confirmed: john@example.com (gis-ops)
✓ Authorization verified
✓ Audit log created

Proceeding with deployment...
```

#### 2. PR-Based Approval (for production)

```typescript
AI: I've created a pull request for this change.

┌─ PULL REQUEST CREATED ────────────────────────────────────────────┐
│ Title: Add zoning layer to production                             │
│ URL: https://github.com/city/honua-config/pull/156                │
│ Reviewers: @gis-ops-team                                          │
│ Status: ⏳ Awaiting review (requires 2 approvals)                  │
└───────────────────────────────────────────────────────────────────┘

The PR includes:
✓ Complete deployment plan
✓ Impact analysis
✓ Risk assessment
✓ Rollback plan
✓ Automated tests

I'll monitor this PR and automatically deploy once approved.

Commands you can use in the PR:
  /honua dry-run    - Run another dry-run
  /honua diff       - Show detailed diff
  /honua schedule   - Schedule deployment
  /honua cancel     - Cancel this deployment
```

#### 3. Scheduled Approval (maintenance windows)

```typescript
AI: This deployment is scheduled for the next maintenance window.

┌─ SCHEDULED DEPLOYMENT ────────────────────────────────────────────┐
│ Scheduled: Tuesday, Oct 8, 2025 10:00 AM PDT                      │
│ Window: 10:00 AM - 12:00 PM (2 hours)                             │
│ Status: ⏳ Pending approval                                        │
│ Approvers: 0/2 required                                           │
└───────────────────────────────────────────────────────────────────┘

Approval deadline: Monday, Oct 7, 2025 5:00 PM PDT
If not approved by deadline, deployment will be cancelled.

Notifications sent to:
  - @gis-ops-team (Slack)
  - gis-ops@example.com (Email)
  - PagerDuty on-call

Approve at: https://deployments.honua.io/deploy-abc123
```

## Layer 7: Blast Radius Limiting

### Prevent Cascading Failures

```yaml
# Configuration
blast_radius_limits:
  max_concurrent_deployments: 1
  max_environments_affected: 1
  max_services_affected: 3
  max_database_tables_affected: 5

  # Circuit breakers
  error_rate_threshold: 5%
  latency_threshold_p95: 2000ms
  failed_deployments_threshold: 2

  # Cool-down periods
  after_failure: 1h
  after_rollback: 30m
  between_production_deploys: 2h
```

### Circuit Breaker in Action

```typescript
AI: ⚠️ CIRCUIT BREAKER TRIPPED

    2 deployments have failed in the last hour:
    - production-20251004-143022 (failed: health check)
    - production-20251004-150133 (failed: validation)

    🛑 Production deployments are BLOCKED for 1 hour.

    Reason: Circuit breaker protection
    Unlocks: 2025-10-04 16:01:33 PDT

    What I can do:
    ✓ Deploy to dev/staging (unaffected)
    ✓ Investigate failures
    ✓ Prepare fixes
    ✗ Deploy to production (blocked)

    Would you like me to investigate the failures?
```

## Layer 8: Read-Only Mode

### Safe Exploration Without Risk

```typescript
User: "I want to explore what the AI can do without any risk"

AI: I'll enable READ-ONLY mode.

┌─ READ-ONLY MODE ENABLED ──────────────────────────────────────────┐
│ 🔒 All write operations are disabled                              │
│ ✓ You can explore any features safely                             │
│ ✓ I can show you what WOULD happen                                │
│ ✗ No actual changes will be made                                  │
│                                                                    │
│ To disable: type 'exit read-only mode'                            │
└───────────────────────────────────────────────────────────────────┘

Feel free to try any commands. I'll show you the plan but won't execute.

User: "Deploy zoning layer to production"

AI: [READ-ONLY MODE]
    I would create this deployment plan:

    [Shows full plan with diff, impact analysis, etc.]

    In normal mode, I would:
    1. Create a PR
    2. Request approval from gis-ops team
    3. Execute after approval

    But we're in READ-ONLY mode, so no changes are made.

    This is exactly what would happen if you ran this command normally.
```

## Layer 9: Audit Trail & Monitoring

### Complete Transparency

```yaml
# Every AI action is logged
audit_log:
  - timestamp: 2025-10-04T15:33:44Z
    user: john@example.com
    ai_session: session-abc123
    intent: "Deploy zoning layer to production"
    action: create_deployment_plan
    resources:
      - type: layer
        name: zoning
        operation: create
    risk_level: medium
    approval_required: true
    approval_status: pending
    policy_checks:
      - production-safety-policy: passed
      - deployment-window: passed
      - rate-limit: passed

  - timestamp: 2025-10-04T15:35:12Z
    user: john@example.com
    ai_session: session-abc123
    action: approve_deployment
    deployment_id: production-20251004-153344
    approval_method: inline
    ip_address: 192.168.1.100
    user_agent: honua-cli/1.0.0
```

### Anomaly Detection

```typescript
AI: ⚠️ ANOMALY DETECTED

    Unusual pattern in your requests:
    - 15 deployment attempts in last 10 minutes
    - 12 failed approval validations
    - 3 different environments targeted

    This doesn't match your normal behavior.

    Possible causes:
    1. Automated script running?
    2. Account compromised?
    3. Testing/learning the system?

    🔒 I've temporarily paused AI operations for your account.

    To resume:
    - Confirm your identity (MFA)
    - Explain the activity
    - Or wait 30 minutes (automatic unlock)

    Security team has been notified.
```

## Layer 10: Graduated Rollout

### Start Conservative, Expand Gradually

```yaml
# Phase 1: Initial Rollout (Week 1-2)
phase_1:
  mode: read_only
  users:
    - ai-pilot-team  # 3-5 early adopters
  permissions:
    - view:*
    - dry-run:*
    - propose:dev
  restrictions:
    - no_auto_execute
    - all_operations_require_approval

# Phase 2: Dev Auto-Deploy (Week 3-4)
phase_2:
  mode: dev_auto_deploy
  users:
    - ai-pilot-team
    - gis-ops-team
  permissions:
    - view:*
    - dry-run:*
    - execute:dev
    - propose:staging
    - propose:production

# Phase 3: Staging Access (Week 5-8)
phase_3:
  mode: staging_access
  users:
    - all_developers
    - gis-ops-team
  permissions:
    - inherit:phase_2
    - execute:staging  # with approval

# Phase 4: Production (Month 3+)
phase_4:
  mode: full_production
  users:
    - gis-ops-team
    - admin
  permissions:
    - inherit:phase_3
    - propose:production  # PR required
```

## Implementation Checklist

### Must-Have for Launch
- [ ] Intent validation (ambiguity detection)
- [ ] RBAC with role-based permissions
- [ ] Environment-based safety levels
- [ ] Dry-run for all operations
- [ ] Deployment policy enforcement
- [ ] PR-based approval for production
- [ ] Automatic rollback
- [ ] Complete audit logging
- [ ] Read-only mode
- [ ] Rate limiting

### Nice-to-Have for v2
- [ ] Anomaly detection
- [ ] Circuit breakers
- [ ] Scheduled deployments
- [ ] Cost impact analysis
- [ ] ChatOps integration
- [ ] Real-time monitoring dashboard

## User Communication

### Set Clear Expectations

```typescript
// First-time user experience
AI: Welcome to Honua AI Consultant!

    Before we start, let me explain how I work:

    ✓ What I CAN do automatically:
      - Answer questions
      - Analyze your configuration
      - Run dry-runs and show plans
      - Deploy to dev environment

    ⚠ What requires YOUR approval:
      - Deploying to staging
      - Deploying to production
      - Deleting resources
      - Changing security settings

    🛡️ Safety features:
      - All actions are logged
      - Automatic rollback on failure
      - Policy enforcement
      - PR-based reviews for production

    📚 Learn more: https://docs.honua.io/ai-consultant/safety

    Ready to get started? What can I help you with?
```

This multi-layered approach provides defense in depth while maintaining usability!
