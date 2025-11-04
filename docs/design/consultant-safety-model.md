# Consultant Safety Model & Plan/Apply Workflow

**Status**: Draft
**Version**: 1.0
**Last Updated**: 2025-10-02
**Author**: Consultant + Mike

## Executive Summary

The Honua Consultant follows a **Terraform-inspired plan/apply model** to ensure users maintain complete control and trust. The AI **never executes actions directly**—instead, it generates detailed plans that users review and explicitly approve before execution.

**Core Principle**: AI can suggest anything, but can only execute with explicit, informed user consent.

---

## 1. Plan/Apply Workflow (Terraform Model)

### 1.1 Overview

```
┌──────────────┐
│ User Request │
└──────┬───────┘
       │
       ▼
┌─────────────────────────────────────────┐
│ PLAN PHASE (AI generates execution plan)│
│ • Analyzes current state                │
│ • Determines required actions           │
│ • Identifies credentials needed         │
│ • Calculates risk assessment            │
└──────┬──────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────┐
│ REVIEW PHASE (User examines plan)       │
│ • Shows exactly what will change        │
│ • Displays credential access required   │
│ • Presents risk analysis                │
│ • Offers dry-run simulation             │
└──────┬──────────────────────────────────┘
       │
       ▼
   ┌───────┐
   │Approve│ No ───► [Rejected - No action taken]
   └───┬───┘
       │ Yes
       ▼
┌─────────────────────────────────────────┐
│ APPLY PHASE (Executes approved plan)    │
│ • Creates rollback snapshot             │
│ • Requests scoped credentials           │
│ • Executes each step with logging       │
│ • Validates results                     │
└──────┬──────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────┐
│ VERIFY PHASE (Confirms success)         │
│ • Validates state changes               │
│ • Reports results                       │
│ • Maintains audit trail                 │
└─────────────────────────────────────────┘
```

### 1.2 Example: Optimize Database Performance

#### User Request
```bash
$ honua assistant optimize database --workspace production
```

#### AI Generated Plan
```
═══════════════════════════════════════════════════════════
EXECUTION PLAN: Optimize Database Performance
═══════════════════════════════════════════════════════════

Current State Analysis:
✓ Workspace: production
✓ Database: PostgreSQL 15.3 @ db.example.com
✓ Tables analyzed: 5 layers, 2.3M features
✓ Issues detected: 3 missing indexes, suboptimal queries

Proposed Changes:

  + CREATE INDEX idx_parcels_geom_gist
      ON parcels USING GIST (geometry)
      └─ Estimated benefit: 85% faster spatial queries
      └─ Index size: ~45 MB
      └─ Build time: ~2 minutes

  + CREATE INDEX idx_roads_name_btree
      ON roads (name)
      └─ Estimated benefit: 95% faster name lookups
      └─ Index size: ~12 MB
      └─ Build time: ~30 seconds

  ~ UPDATE pg_settings
      SET shared_buffers = '2GB'
      └─ Current: 256MB
      └─ Requires: Database restart
      └─ Downtime: ~10 seconds

  + CREATE STATISTICS parcels_geom_stats
      ON geometry FROM parcels
      └─ Improves query planner accuracy
      └─ Build time: ~1 minute

Credentials Required:

  🔑 postgres-production (DDL access)
     └─ Permissions: CREATE INDEX, ALTER SYSTEM
     └─ Duration: 5 minutes (auto-revoke)
     └─ Actions: Read schema + Execute DDL

What Gets Sent to LLM:
  ✓ Table schemas (structure only)
  ✓ Index definitions
  ✓ Query execution plans
  ✗ Your data values
  ✗ Connection credentials

Risk Assessment: LOW
  ✓ All operations are reversible
  ✓ No data modification
  ✓ Minimal downtime (<30 seconds)
  ⚠ Requires database restart for config change

Rollback Plan:
  1. Snapshot current indexes
  2. Store original pg_settings
  3. On failure: DROP new indexes, restore settings

═══════════════════════════════════════════════════════════

Would you like to proceed?

[A]pply  [M]odify Plan  [D]ry Run  [R]eject

```

#### User Chooses: [D] Dry Run
```
═══════════════════════════════════════════════════════════
DRY RUN SIMULATION
═══════════════════════════════════════════════════════════

Simulating changes (no actual modifications):

✓ [SIMULATED] CREATE INDEX idx_parcels_geom_gist
  └─ Would create: GIST index on 450,000 geometries
  └─ Estimated disk usage: +45 MB

✓ [SIMULATED] CREATE INDEX idx_roads_name_btree
  └─ Would create: B-tree index on 85,000 names
  └─ Estimated disk usage: +12 MB

✓ [SIMULATED] UPDATE pg_settings
  └─ Would change: shared_buffers from 256MB to 2GB
  └─ Estimated memory impact: +1.75 GB RAM usage

✓ [SIMULATED] CREATE STATISTICS parcels_geom_stats
  └─ Would create: Extended statistics on geometry column

Performance Impact Prediction:
  📊 Spatial queries: 2.3s → 0.3s (87% improvement)
  📊 Name lookups: 450ms → 23ms (95% improvement)
  📊 Memory usage: +1.8 GB
  📊 Disk usage: +57 MB

No actual changes made. Re-run with [A]pply to execute.

[A]pply  [M]odify Plan  [R]eject
```

#### User Chooses: [A] Apply
```
═══════════════════════════════════════════════════════════
APPLYING PLAN
═══════════════════════════════════════════════════════════

[1/5] Creating rollback snapshot...
  ✓ Snapshot created: rollback-20251002-143022

[2/5] Requesting scoped credentials...
  🔑 Token requested: postgres-production (DDL access, 5min TTL)
  ⏳ Waiting for approval...
  ✓ Token granted: tok_abc123xyz (expires: 14:35:22)

[3/5] Creating spatial index on parcels...
  ⏳ CREATE INDEX idx_parcels_geom_gist...
  ✓ Index created (2m 15s, 47.3 MB)

[4/5] Creating name index on roads...
  ⏳ CREATE INDEX idx_roads_name_btree...
  ✓ Index created (28s, 11.8 MB)

[5/5] Creating geometry statistics...
  ⏳ CREATE STATISTICS parcels_geom_stats...
  ✓ Statistics created (1m 02s)

[6/5] Skipping pg_settings update (requires restart)
  ℹ️  Run 'honua assistant apply --restart-db' to apply this change

═══════════════════════════════════════════════════════════
EXECUTION COMPLETE
═══════════════════════════════════════════════════════════

✓ 3 of 4 changes applied successfully
⚠ 1 change deferred (requires restart)

Performance validation:
  ✓ Spatial query test: 2.1s → 0.28s (86.7% improvement)
  ✓ Name lookup test: 438ms → 19ms (95.7% improvement)

Rollback available: rollback-20251002-143022
  Run 'honua assistant rollback' to undo these changes

Audit log: ~/.honua/audit/2025-10-02-session-143015.log
```

---

## 2. Credential Safety Architecture

### 2.1 Never Send Secrets to LLM

**The Golden Rule**: Raw credentials NEVER leave the secrets manager and NEVER get sent to any LLM.

```csharp
// GOOD: AI sees redacted placeholder
public class RedactedConnectionInfo
{
    public string Type { get; init; } = "PostgreSQL";
    public string Host { get; init; } = "***REDACTED***";
    public string Database { get; init; } = "production";
    public string SecretRef { get; init; } = "postgres-production";  // Reference only!
}

// BAD: NEVER do this
public class ConnectionInfo
{
    public string ConnectionString { get; init; } = "postgres://admin:password123@...";
}
```

### 2.2 Scoped Token Architecture

```
┌─────────────────────────────┐
│   User's Secrets Storage    │
│   (OS Keychain/Vault)        │
│                              │
│  postgres-prod: "user:pass"  │
│  aws-s3-key: "AKIA..."       │
└──────────┬──────────────────┘
           │
           │ Only via Secrets Manager
           ▼
┌─────────────────────────────┐
│    Secrets Manager           │
│  (Honua.Cli.AI.Secrets)      │
│                              │
│  RequestScopedAccess()       │
└──────────┬──────────────────┘
           │
           │ Issues temporary token
           ▼
┌─────────────────────────────┐
│      Scoped Token            │
│                              │
│  Token: tok_abc123xyz        │
│  Scope: DDL only             │
│  Operations: [CREATE INDEX]  │
│  Expires: 2025-10-02 14:35   │
│  Revokable: Yes              │
└──────────┬──────────────────┘
           │
           │ AI uses token (never sees secret)
           ▼
┌─────────────────────────────┐
│   Execution Engine           │
│  (Applies plan with token)   │
└─────────────────────────────┘
```

### 2.3 Implementation

```csharp
namespace Honua.Cli.AI.Secrets;

public interface ISecretsManager
{
    /// <summary>
    /// AI NEVER calls this - only user-initiated operations
    /// </summary>
    Task<Secret> GetSecretAsync(string name);

    /// <summary>
    /// AI calls this to request scoped, temporary access
    /// </summary>
    Task<ScopedToken> RequestScopedAccessAsync(
        string secretName,
        AccessScope scope,
        TimeSpan duration,
        bool requireUserApproval = true);

    /// <summary>
    /// User can revoke AI access at any time
    /// </summary>
    Task RevokeTokenAsync(string tokenId);

    /// <summary>
    /// List all active tokens (for user visibility)
    /// </summary>
    Task<IReadOnlyList<ScopedToken>> ListActiveTokensAsync();
}

public class ScopedToken
{
    /// <summary>
    /// Temporary credential (e.g., JWT, session token, temp password)
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Unique token identifier (for revocation)
    /// </summary>
    public required string TokenId { get; init; }

    /// <summary>
    /// What operations are allowed
    /// </summary>
    public required AccessScope Scope { get; init; }

    /// <summary>
    /// Automatic expiration
    /// </summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Can be revoked by user
    /// </summary>
    public bool IsRevoked { get; set; }

    // Audit trail
    public required string RequestedBy { get; init; }  // "Consultant"
    public required string Purpose { get; init; }      // "Create database indexes"
    public required DateTime CreatedAt { get; init; }
}

public class AccessScope
{
    public AccessLevel Level { get; init; }
    public string[] AllowedOperations { get; init; } = [];
    public string[] DeniedOperations { get; init; } = [];
    public string[] AllowedResources { get; init; } = [];
}

public enum AccessLevel
{
    ReadOnly,      // SELECT, SHOW, EXPLAIN
    DDL,           // CREATE INDEX, CREATE STATISTICS (no DROP)
    DML,           // INSERT, UPDATE, DELETE (dangerous!)
    Admin          // Full access (very rare, requires explicit approval)
}
```

---

## 3. What Gets Sent to LLM (Privacy Model)

### 3.1 Allowed Information

The AI assistant sends **structure and metadata only**, never sensitive data:

```json
{
  "context": {
    "workspace": {
      "name": "production",
      "layers": [
        {
          "id": "parcels",
          "geometryType": "Polygon",
          "featureCount": 450000,
          "fields": [
            { "name": "parcel_id", "type": "string" },
            { "name": "owner_name", "type": "string" },
            { "name": "area_sqm", "type": "double" }
          ],
          "indexes": [
            { "name": "parcels_pkey", "columns": ["parcel_id"], "type": "btree" }
          ]
        }
      ]
    },
    "database": {
      "type": "PostgreSQL",
      "version": "15.3",
      "secretRef": "postgres-production",
      "settings": {
        "shared_buffers": "256MB",
        "work_mem": "64MB"
      }
    },
    "performance": {
      "slowQueries": [
        {
          "query": "SELECT * FROM parcels WHERE ST_Intersects(geometry, $1)",
          "avgDuration": "2.3s",
          "calls": 1250,
          "indexUsed": false
        }
      ]
    }
  }
}
```

### 3.2 Never Sent to LLM

```
❌ Connection strings
❌ Passwords / API keys
❌ Actual feature data (values from database)
❌ User PII (names, addresses, emails)
❌ Session tokens
❌ File contents (only file paths/structure)
```

### 3.3 Redaction Example

```csharp
public class LlmContextBuilder
{
    public object BuildSafeContext(Workspace workspace, DbConnection connection)
    {
        return new
        {
            Workspace = new
            {
                Name = workspace.Name,
                Layers = workspace.Layers.Select(layer => new
                {
                    layer.Id,
                    layer.GeometryType,
                    FeatureCount = GetFeatureCount(layer),  // Count only, no data
                    Fields = layer.Fields.Select(f => new
                    {
                        f.Name,
                        f.DataType,
                        f.Nullable
                    }),
                    // REDACTED: No actual values
                }),
            },
            Database = new
            {
                Type = connection.DatabaseType,
                Version = connection.ServerVersion,
                SecretRef = connection.SecretName,  // Reference, not credential!
                // REDACTED: No connection string
            }
        };
    }
}
```

---

## 4. Safety Rails & Validation

### 4.1 Pre-execution Checks

```csharp
public class SafetyValidator
{
    public ValidationResult ValidatePlan(ExecutionPlan plan)
    {
        var issues = new List<string>();

        // Check for dangerous operations
        foreach (var step in plan.Steps)
        {
            if (step.Operation.Contains("DROP", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"DANGER: {step.Operation} can cause data loss");
            }

            if (step.Operation.Contains("DELETE FROM") &&
                !step.Operation.Contains("WHERE"))
            {
                issues.Add($"DANGER: Unqualified DELETE can remove all data");
            }

            if (step.RequiresDowntime && plan.DowntimeWindow == null)
            {
                issues.Add($"Step '{step.Name}' requires downtime but no window specified");
            }
        }

        return new ValidationResult
        {
            IsValid = issues.Count == 0,
            Issues = issues,
            RiskLevel = CalculateRiskLevel(plan)
        };
    }

    private RiskLevel CalculateRiskLevel(ExecutionPlan plan)
    {
        var score = 0;

        // Increase risk for data modifications
        if (plan.Steps.Any(s => s.ModifiesData)) score += 50;

        // Increase risk for production environments
        if (plan.Environment == "production") score += 30;

        // Increase risk for irreversible operations
        if (plan.Steps.Any(s => !s.IsReversible)) score += 40;

        // Decrease risk if rollback plan exists
        if (plan.RollbackPlan != null) score -= 20;

        return score switch
        {
            < 20 => RiskLevel.Low,
            < 50 => RiskLevel.Medium,
            < 80 => RiskLevel.High,
            _ => RiskLevel.Critical
        };
    }
}
```

### 4.2 Operation Allowlists/Denylists

```yaml
# ~/.honua/ai-safety-rules.yaml
safety:
  # Operations that never require approval (fully automated)
  allowlist:
    - SELECT
    - EXPLAIN
    - SHOW
    - DESCRIBE

  # Operations that always require explicit approval
  requireApproval:
    - CREATE INDEX
    - ALTER TABLE
    - UPDATE pg_settings
    - VACUUM
    - ANALYZE

  # Operations that are completely blocked
  denylist:
    - DROP DATABASE
    - DROP TABLE
    - TRUNCATE
    - DELETE FROM * WHERE 1=1  # Unqualified deletes
    - ALTER USER
    - GRANT ALL

  # Environment-specific rules
  environments:
    production:
      # Extra cautious in prod
      requireApproval:
        - CREATE INDEX  # Even safe operations need approval

      # Require explicit confirmation for risky ops
      requireExplicitConfirmation:
        - ALTER TABLE
        - Any operation causing downtime

    development:
      # More permissive in dev
      allowlist:
        - CREATE INDEX
        - CREATE STATISTICS
```

---

## 5. Rollback & Recovery

### 5.1 Automatic Snapshot Creation

Before any execution, the system creates a rollback point:

```csharp
public class RollbackManager
{
    public async Task<RollbackSnapshot> CreateSnapshotAsync(ExecutionPlan plan)
    {
        var snapshot = new RollbackSnapshot
        {
            Id = $"rollback-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            CreatedAt = DateTime.UtcNow,
            PlanId = plan.Id,
        };

        // Capture current state for each change
        foreach (var step in plan.Steps)
        {
            if (step.Type == StepType.CreateIndex)
            {
                // Record: "index does not exist, can safely DROP"
                snapshot.AddReversalStep(new RollbackStep
                {
                    Description = $"DROP INDEX {step.IndexName}",
                    Action = () => DropIndexAsync(step.IndexName)
                });
            }

            if (step.Type == StepType.UpdateConfig)
            {
                // Capture current value
                var currentValue = await GetConfigValueAsync(step.ConfigKey);
                snapshot.AddReversalStep(new RollbackStep
                {
                    Description = $"Restore {step.ConfigKey} = {currentValue}",
                    Action = () => SetConfigValueAsync(step.ConfigKey, currentValue)
                });
            }
        }

        await SaveSnapshotAsync(snapshot);
        return snapshot;
    }

    public async Task RollbackAsync(string snapshotId)
    {
        var snapshot = await LoadSnapshotAsync(snapshotId);

        Console.WriteLine($"Rolling back to: {snapshot.Id}");
        Console.WriteLine($"Created: {snapshot.CreatedAt}");
        Console.WriteLine($"Steps to reverse: {snapshot.Steps.Count}");
        Console.WriteLine();

        // Execute reversal steps in reverse order
        for (int i = snapshot.Steps.Count - 1; i >= 0; i--)
        {
            var step = snapshot.Steps[i];
            Console.Write($"[{i+1}/{snapshot.Steps.Count}] {step.Description}...");

            try
            {
                await step.Action();
                Console.WriteLine(" ✓");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" ✗ Failed: {ex.Message}");
                throw;
            }
        }

        Console.WriteLine();
        Console.WriteLine("✓ Rollback complete");
    }
}
```

---

## 6. Audit Trail & Logging

### 6.1 Comprehensive Logging

Every AI action is logged with cryptographic signatures:

```json
{
  "session": {
    "id": "session-20251002-143015",
    "user": "mike@example.com",
    "startedAt": "2025-10-02T14:30:15Z",
    "endedAt": "2025-10-02T14:38:42Z"
  },
  "plan": {
    "id": "plan-abc123",
    "type": "database-optimization",
    "generatedAt": "2025-10-02T14:30:18Z",
    "approvedAt": "2025-10-02T14:32:05Z",
    "appliedAt": "2025-10-02T14:32:08Z"
  },
  "credentials": {
    "tokensIssued": [
      {
        "tokenId": "tok_abc123xyz",
        "secretRef": "postgres-production",
        "scope": "DDL",
        "operations": ["CREATE INDEX", "CREATE STATISTICS"],
        "issuedAt": "2025-10-02T14:32:08Z",
        "expiresAt": "2025-10-02T14:37:08Z",
        "revokedAt": null,
        "usageCount": 3
      }
    ]
  },
  "execution": {
    "steps": [
      {
        "stepId": 1,
        "description": "CREATE INDEX idx_parcels_geom_gist",
        "startedAt": "2025-10-02T14:32:10Z",
        "completedAt": "2025-10-02T14:34:25Z",
        "duration": "2m15s",
        "status": "success",
        "changes": {
          "objectCreated": "idx_parcels_geom_gist",
          "diskUsage": "+47.3MB"
        }
      }
    ]
  },
  "llmCalls": [
    {
      "callId": 1,
      "provider": "OpenAI",
      "model": "gpt-4o",
      "timestamp": "2025-10-02T14:30:18Z",
      "purpose": "Generate optimization plan",
      "tokensUsed": 3542,
      "dataSent": {
        "schemaInfo": true,
        "performanceMetrics": true,
        "actualData": false,
        "credentials": false
      }
    }
  ],
  "signature": "SHA256:abc123...",  // Cryptographic signature
  "rollbackAvailable": "rollback-20251002-143022"
}
```

### 6.2 User Access to Audit Logs

```bash
# View today's audit logs
$ honua assistant audit --today

# View specific session
$ honua assistant audit --session session-20251002-143015

# Verify log integrity
$ honua assistant audit --verify

# Export logs for compliance
$ honua assistant audit --export compliance-2025-Q4.json
```

---

## 7. User Education & Transparency

### 7.1 First-Run Setup

```
═══════════════════════════════════════════════════════════
Welcome to Honua Consultant
═══════════════════════════════════════════════════════════

This AI assistant can help you configure, deploy, and optimize
Honua infrastructure. Before we begin, please understand:

✓ HOW IT WORKS:
  • AI analyzes your workspace and suggests improvements
  • You review and approve all plans before execution
  • No actions are taken without your explicit consent

✓ YOUR CONTROL:
  • AI never accesses secrets directly
  • You grant temporary, scoped credentials for each task
  • You can revoke access at any time
  • Full audit trail of all actions

✓ WHAT GETS SHARED:
  ✓ Database schemas (structure, not data)
  ✓ Performance metrics
  ✓ Error messages (sanitized)
  ✗ Your credentials
  ✗ Your data
  ✗ Personal information

✓ SAFETY FEATURES:
  • Automatic rollback snapshots
  • Dangerous operations blocked
  • Dry-run mode available
  • All logs cryptographically signed

Would you like to configure the AI assistant now? [Y/n]
```

### 7.2 In-Context Help

```bash
$ honua assistant optimize --help

Usage: honua assistant optimize [options]

Analyzes your workspace and suggests performance optimizations.

SAFETY INFORMATION:
  • AI will generate a plan showing exactly what will change
  • You can review with --dry-run before applying
  • All changes can be rolled back
  • See ~/.honua/audit/ for complete logs

CREDENTIALS:
  • AI never sees your actual credentials
  • Temporary tokens are issued for plan execution
  • Tokens auto-expire after use
  • You can revoke access: honua assistant revoke-tokens

PRIVACY:
  • AI sees schema structure, not your data
  • No credentials sent to OpenAI/Anthropic
  • Telemetry is opt-in only

Options:
  --dry-run          Show what would change without executing
  --auto-approve     Skip approval (use with caution!)
  --rollback         Undo the last applied plan

Examples:
  # Safe exploration (no changes)
  honua assistant optimize --dry-run

  # Apply changes with approval
  honua assistant optimize

  # Undo last changes
  honua assistant rollback
```

---

## 8. Configuration

### 8.1 Safety Configuration

```yaml
# ~/.honua/consultant.yaml
ai:
  # LLM provider
  provider: openai
  model: gpt-4o

  safety:
    # Workflow
    requireApprovalForExecution: true     # Like terraform apply
    alwaysShowDryRun: false               # Optional pre-flight check
    allowAutoApprove: false               # Disable --auto-approve flag

    # Credential access
    maxTokenDuration: 10m                 # Tokens auto-expire
    requireUserApprovalForTokens: true    # User must approve each token

    # Dangerous operations (always require confirmation)
    dangerousOperations:
      - DROP
      - DELETE
      - TRUNCATE
      - ALTER TABLE DROP COLUMN
      - GRANT
      - REVOKE

    # Blocked operations (never allowed)
    blockedOperations:
      - DROP DATABASE
      - DROP TABLE
      - TRUNCATE TABLE

    # Access levels (least privilege)
    maxAccessLevel: SafeExecute           # ReadOnly | SafeExecute | FullExecute

  # Secrets (never share these with AI)
  secrets:
    neverShare:
      - "*-admin"           # Any secret ending in -admin
      - "production-*"      # Any production secret
      - "master-*"          # Master keys

    # Require additional confirmation
    requireExplicitConsent:
      - "postgres-production"
      - "aws-s3-production"

  # Privacy
  telemetry:
    enabled: false          # Opt-in only
    anonymousId: true       # Hash machine ID

  # Audit
  audit:
    enabled: true
    logPath: ~/.honua/audit/
    cryptographicSigning: true
    retentionDays: 90

  # Rollback
  rollback:
    autoSnapshot: true
    keepSnapshots: 10
    snapshotPath: ~/.honua/rollback/
```

---

## 9. Trust Building

### 9.1 Progressive Trust Model

Start restrictive, grant more permissions as trust builds:

```
Level 1: Observer (default)
  • AI can only analyze and suggest
  • No execution permissions
  • No credential access
  → User sees AI is helpful and accurate

Level 2: Safe Executor (user enables)
  • AI can execute pre-approved safe operations
  • Limited credential access (read-only tokens)
  • Still requires approval for risky operations
  → User sees AI executes plans correctly

Level 3: Trusted Advisor (user enables)
  • AI can execute broader range of operations
  • DDL access with scoped tokens
  • Still requires approval for dangerous operations
  → User trusts AI for day-to-day tasks

Level 4: Full Automation (rarely used)
  • AI can execute without approval (specific tasks only)
  • Useful for scheduled maintenance
  • Still blocked from dangerous operations
  → User has complete confidence in AI
```

### 9.2 Community Verification

- **Open source secrets layer** - Even if main AI is closed source
- **Public audit of LLM prompts** - Show what gets sent to AI
- **Third-party security audits** - Regular independent reviews
- **Bug bounty program** - Incentivize finding issues

---

## 10. Comparison to Terraform

| Feature | Terraform | Honua Consultant |
|---------|-----------|-------------------|
| **Plan Generation** | `terraform plan` | `honua assistant <action>` |
| **Shows Changes** | ✓ Detailed diff | ✓ Detailed plan with explanations |
| **Requires Approval** | ✓ Manual `apply` | ✓ Manual approval |
| **Dry Run** | ✓ `-dry-run` flag | ✓ `--dry-run` flag |
| **State Management** | ✓ `.tfstate` file | ✓ Rollback snapshots |
| **Rollback** | Manual state restore | ✓ `honua assistant rollback` |
| **Audit Trail** | Plan logs | ✓ Cryptographically signed logs |
| **Credential Safety** | User provides | ✓ Scoped, temporary tokens |

The AI assistant follows Terraform's proven model but **adds intelligence** to the plan generation phase.

---

## Summary

The Honua Consultant earns user trust through:

1. **Terraform-style plan/apply workflow** - Never surprises users
2. **Credential isolation** - AI never sees raw secrets
3. **Scoped, temporary tokens** - Least privilege access
4. **Privacy-first** - Only metadata sent to LLM, never data
5. **Automatic rollback** - Every change is reversible
6. **Complete audit trail** - Cryptographically signed logs
7. **Progressive trust** - Start restrictive, user grants more permissions
8. **Transparent operation** - Open source core, auditable prompts

**Users maintain complete control** while benefiting from AI's intelligence.
