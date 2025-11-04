# GitOps End-to-End Testing Guide

This directory contains everything you need to test Honua's GitOps functionality end-to-end in a local environment.

## Quick Start

```bash
# 1. Initialize the test Git repository
./init-test-repo.sh

# 2. Configure Honua to use the test repository
cp appsettings.test.json ../../src/Honua.Server.Host/appsettings.Development.json

# 3. Start Honua server (from project root)
cd ../../src/Honua.Server.Host
dotnet run

# 4. In another terminal, validate GitOps is working
cd ../../samples/gitops-e2e-test
./validate.sh
```

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Setup](#setup)
3. [Testing Workflow](#testing-workflow)
4. [Test Scenarios](#test-scenarios)
5. [Validation](#validation)
6. [Troubleshooting](#troubleshooting)
7. [Cleanup](#cleanup)

## Prerequisites

- **Git**: 2.0 or higher
- **.NET 9.0 SDK**: For running Honua
- **Bash**: For running setup and validation scripts
- **PostgreSQL**: Optional, for testing with real datasources (can use mock data)
- **Text editor**: For modifying configuration files

## Setup

### Step 1: Initialize Test Repository

Run the initialization script to create a local Git repository with test data:

```bash
./init-test-repo.sh
```

This creates:
- `/tmp/honua-gitops-test-repo/` - Git repository with test configuration
- `/tmp/honua-gitops-state/` - Directory for deployment state files
- Sample metadata, datasources, and configuration files
- Initial Git commits for tracking changes

**Expected output:**
```
[GitOps E2E Test Setup] Creating test repository at /tmp/honua-gitops-test-repo
[GitOps E2E Test Setup] Initialized empty Git repository
[GitOps E2E Test Setup] Creating directory structure...
[GitOps E2E Test Setup] Creating sample metadata files...
[GitOps E2E Test Setup] Creating sample datasource files...
[GitOps E2E Test Setup] Making initial commits...
[GitOps E2E Test Setup] Test repository ready!
```

### Step 2: Configure Honua

Copy the test configuration to your development settings:

```bash
# Option 1: Copy the full test configuration
cp appsettings.test.json ../../src/Honua.Server.Host/appsettings.Development.json

# Option 2: Manually add GitOps section to your existing appsettings.Development.json
```

The key configuration section:
```json
{
  "GitOps": {
    "Enabled": true,
    "RepositoryPath": "/tmp/honua-gitops-test-repo",
    "StateDirectory": "/tmp/honua-gitops-state",
    "DryRun": false,
    "Watcher": {
      "Branch": "main",
      "Environment": "development",
      "PollIntervalSeconds": 10
    }
  }
}
```

### Step 3: Start Honua Server

From the project root:

```bash
cd ../../src/Honua.Server.Host
dotnet run
```

**Expected log output:**
```
[Info] FileApprovalService initialized with approval directory: /tmp/honua-gitops-state/approvals
[Info] Initializing Git repository at '/tmp/honua-gitops-test-repo' without authentication
[Info] Initializing deployment state store at '/tmp/honua-gitops-state'
[Info] GitWatcher started. Watching branch 'main' every 10 seconds
[Info] Initial commit: abc123def456...
```

## Testing Workflow

### Workflow Overview

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Modify files in test repository                          │
│    cd /tmp/honua-gitops-test-repo                          │
│    vim environments/development/metadata.json               │
└──────────────────┬──────────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Commit changes to Git                                    │
│    git add environments/development/metadata.json           │
│    git commit -m "Add new layer"                           │
└──────────────────┬──────────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. GitWatcher detects change (within 10 seconds)           │
│    [Info] Detected new commit: abc123 -> def456            │
│    [Info] Found 1 relevant files for 'development'         │
└──────────────────┬──────────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. HonuaReconciler applies changes                         │
│    [Info] Starting reconciliation for 'development'        │
│    [Info] Successfully reconciled 'metadata'               │
└──────────────────┬──────────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────────┐
│ 5. Verify changes applied                                  │
│    ./validate.sh                                           │
│    curl http://localhost:5000/api/v1/metadata             │
└─────────────────────────────────────────────────────────────┘
```

### Basic Testing Steps

1. **Monitor Honua logs** in one terminal:
   ```bash
   cd src/Honua.Server.Host
   dotnet run | grep -i gitops
   ```

2. **Make a configuration change** in another terminal:
   ```bash
   cd /tmp/honua-gitops-test-repo
   # Edit a file (see test scenarios below)
   vim environments/development/metadata.json
   ```

3. **Commit the change**:
   ```bash
   git add .
   git commit -m "Test: Add new layer"
   ```

4. **Wait for GitWatcher to detect** (10 seconds max):
   - Look for "Detected new commit" in logs
   - Look for "Starting reconciliation" message

5. **Validate changes applied**:
   ```bash
   cd samples/gitops-e2e-test
   ./validate.sh
   ```

## Test Scenarios

This section provides specific scenarios to test. Each scenario has its own file in `test-scenarios/` with detailed instructions.

### Scenario 1: Add a New Layer

**File**: `test-scenarios/01-add-layer.yaml`

**What to test**: Adding a completely new layer to an existing service

**Steps**:
```bash
# Copy the scenario template
cp test-scenarios/01-add-layer.yaml /tmp/scenario.yaml

# Navigate to test repo
cd /tmp/honua-gitops-test-repo

# Add the new layer definition to metadata.json
# (See 01-add-layer.yaml for exact JSON to add)

git add environments/development/metadata.json
git commit -m "Add facilities layer"
```

**Expected behavior**:
- GitWatcher detects change within 10 seconds
- Reconciler processes metadata.json
- MetadataRegistry reloads with new layer
- New layer appears in service metadata

**Validation**:
```bash
# Check logs for successful reconciliation
grep "Successfully reconciled 'metadata'" logs/honua-*.log

# Verify layer exists in deployment state
cat /tmp/honua-gitops-state/deployments/*.json | jq '.ValidationResults'
```

### Scenario 2: Modify Existing Layer

**File**: `test-scenarios/02-modify-layer.yaml`

**What to test**: Changing properties of an existing layer (e.g., title, description, geometry type)

**Steps**:
```bash
cd /tmp/honua-gitops-test-repo

# Modify an existing layer in metadata.json
# Change title from "Sample Points" to "Updated Sample Points"

git add environments/development/metadata.json
git commit -m "Update layer title"
```

**Expected behavior**:
- Change detected and reconciled
- No errors (non-breaking change)
- Updated metadata reflects changes

**Validation**:
```bash
./validate.sh
# Should show successful reconciliation with 0 errors
```

### Scenario 3: Remove a Layer

**File**: `test-scenarios/03-delete-layer.yaml`

**What to test**: Removing a layer from the service

**Steps**:
```bash
cd /tmp/honua-gitops-test-repo

# Remove a layer from the services array in metadata.json

git add environments/development/metadata.json
git commit -m "Remove deprecated layer"
```

**Expected behavior**:
- Change detected and reconciled
- Layer no longer appears in metadata registry
- Deployment plan shows resource removal

**Validation**:
```bash
# Check deployment state for removed resources
cat /tmp/honua-gitops-state/deployments/*.json | jq '.Plan.Removed'
```

### Scenario 4: Breaking Change (Production)

**File**: `test-scenarios/04-breaking-change.yaml`

**What to test**: Making a breaking change that requires approval (if configured for production)

**Steps**:
```bash
cd /tmp/honua-gitops-test-repo

# Switch to production environment in appsettings or test with production branch
# Make a breaking change (e.g., change geometry type, SRID)

# Edit environments/production/metadata.json
# Change geometryType from "Point" to "Polygon" (breaking change)

git add environments/production/metadata.json
git commit -m "Breaking: Change geometry type"
```

**Expected behavior** (if approval is enabled):
- Reconciler detects breaking change
- Approval is requested
- Deployment waits in "PendingApproval" state
- Logs show approval required message

**Validation**:
```bash
# Check for approval record
ls /tmp/honua-gitops-state/approvals/
cat /tmp/honua-gitops-state/approvals/*.json

# Manually approve (if testing approval workflow)
# This would typically be done via API or CLI
```

## Validation

### Automated Validation Script

The `validate.sh` script checks:
- Git repository is accessible
- State directory exists and has deployment records
- Recent reconciliation activity
- No critical errors in latest deployment

```bash
./validate.sh
```

**Example output:**
```
=== GitOps Validation Report ===
Date: 2025-10-23 10:30:45

[✓] Git repository exists at /tmp/honua-gitops-test-repo
[✓] Git repository is valid
[✓] State directory exists at /tmp/honua-gitops-state
[✓] Found 3 deployment records
[✓] Latest deployment: dep_1234567890 (development, Completed)
[✓] Last reconciliation: 2 minutes ago
[✓] No critical errors detected

=== Summary ===
Status: HEALTHY
All GitOps components are functioning correctly.
```

### Manual Validation Steps

1. **Check Git repository**:
   ```bash
   cd /tmp/honua-gitops-test-repo
   git log --oneline -5
   git status
   ```

2. **Check deployment state**:
   ```bash
   ls -la /tmp/honua-gitops-state/deployments/
   cat /tmp/honua-gitops-state/deployments/latest.json | jq '.'
   ```

3. **Check Honua logs**:
   ```bash
   cd src/Honua.Server.Host
   tail -f logs/honua-*.log | grep -i "gitops\|reconcil"
   ```

4. **Check metadata endpoint** (if server is running):
   ```bash
   curl http://localhost:5000/api/v1/metadata | jq '.services'
   ```

### What to Look For

**Successful reconciliation logs**:
```
[Info] Detected new commit: abc123 -> def456
[Info] Found 1 relevant files for environment 'development'
[Info] Starting reconciliation for environment 'development' at commit 'def456'
[Info] Retrieving 'metadata' configuration from Git: environments/development/metadata.json
[Info] Successfully parsed JSON for 'metadata'
[Info] Applying 'metadata' configuration for environment 'development'
[Info] Successfully reconciled 'metadata' for environment 'development'
[Info] Successfully completed reconciliation for environment 'development' in 234ms
```

**Deployment state file structure**:
```json
{
  "id": "dep_1234567890",
  "environment": "development",
  "commit": "def456...",
  "branch": "main",
  "state": "Completed",
  "health": "Healthy",
  "syncStatus": "Synced",
  "startedAt": "2025-10-23T10:30:00Z",
  "completedAt": "2025-10-23T10:30:01Z",
  "initiatedBy": "GitWatcher",
  "validationResults": [
    {
      "type": "metadata",
      "success": true,
      "message": "Metadata configuration applied successfully",
      "timestamp": "2025-10-23T10:30:01Z"
    }
  ]
}
```

## Troubleshooting

### Issue: GitWatcher not detecting changes

**Symptoms**:
- Commits made but no "Detected new commit" logs
- Poll interval passes but nothing happens

**Solutions**:
1. Verify GitOps is enabled:
   ```bash
   grep -A 10 '"GitOps"' src/Honua.Server.Host/appsettings.Development.json
   ```

2. Check repository path is correct:
   ```bash
   ls -la /tmp/honua-gitops-test-repo
   ```

3. Verify Honua has read access:
   ```bash
   # Check file permissions
   ls -la /tmp/honua-gitops-test-repo/.git
   ```

4. Increase logging verbosity:
   ```json
   "Serilog": {
     "MinimumLevel": {
       "Default": "Debug"
     }
   }
   ```

### Issue: Reconciliation fails

**Symptoms**:
- "Error during reconciliation" in logs
- Deployment state shows "Failed"

**Solutions**:
1. Check for invalid JSON:
   ```bash
   cd /tmp/honua-gitops-test-repo
   cat environments/development/metadata.json | jq '.'
   ```

2. Verify metadata structure matches Honua schema

3. Check reconciliation error in deployment state:
   ```bash
   cat /tmp/honua-gitops-state/deployments/*.json | jq '.ErrorMessage'
   ```

### Issue: Changes not reflected in metadata

**Symptoms**:
- Reconciliation succeeds but metadata unchanged
- No errors in logs

**Solutions**:
1. Check if MetadataRegistry is configured:
   ```bash
   grep "MetadataRegistry" logs/honua-*.log
   ```

2. Verify reconciled configuration was written:
   ```bash
   ls -la /tmp/honua-gitops-test-repo/reconciled/development/
   cat /tmp/honua-gitops-test-repo/reconciled/development/metadata.json
   ```

3. Check if metadata reload succeeded:
   ```bash
   grep "reloaded metadata registry" logs/honua-*.log
   ```

### Issue: State directory permission errors

**Symptoms**:
- "Access denied" errors when writing deployment state
- Missing deployment records

**Solutions**:
```bash
# Ensure directory exists and is writable
sudo mkdir -p /tmp/honua-gitops-state
sudo chown $USER:$USER /tmp/honua-gitops-state
chmod 755 /tmp/honua-gitops-state
```

### Issue: Poll interval too long

**Symptoms**:
- Changes take too long to detect during testing

**Solutions**:
Reduce poll interval in appsettings.test.json:
```json
"Watcher": {
  "PollIntervalSeconds": 5
}
```

## Cleanup

### Quick Cleanup

Remove all test data and reset:

```bash
./cleanup.sh
```

This removes:
- `/tmp/honua-gitops-test-repo/` - Test Git repository
- `/tmp/honua-gitops-state/` - Deployment state directory
- Any reconciled configuration files

### Selective Cleanup

**Keep repository, clear state only**:
```bash
rm -rf /tmp/honua-gitops-state/*
```

**Reset repository to initial state**:
```bash
cd /tmp/honua-gitops-test-repo
git reset --hard HEAD~10  # Go back 10 commits
```

**Clear Honua logs**:
```bash
cd src/Honua.Server.Host
rm -rf logs/honua-*.log
```

## Advanced Testing

### Test with Multiple Environments

1. Configure watcher for different environments:
   ```json
   "Watcher": {
     "Environment": "staging"
   }
   ```

2. Make changes to staging environment:
   ```bash
   cd /tmp/honua-gitops-test-repo
   vim environments/staging/metadata.json
   git commit -m "Staging: Add layer"
   ```

### Test Approval Workflow

1. Enable production environment with approval:
   ```json
   "Watcher": {
     "Environment": "production"
   }
   ```

2. Make breaking change to production

3. Check approval state:
   ```bash
   ls /tmp/honua-gitops-state/approvals/
   ```

### Test Rollback

1. Note current commit hash
2. Make a change and commit
3. Manually trigger rollback by reverting commit:
   ```bash
   cd /tmp/honua-gitops-test-repo
   git revert HEAD
   ```

### Test DryRun Mode

1. Enable dry run:
   ```json
   "GitOps": {
     "DryRun": true
   }
   ```

2. Make changes and verify they are detected but not applied
3. Look for "DRY RUN:" prefix in logs

## Next Steps

- **Production setup**: See `/docs/dev/gitops-getting-started.md`
- **Advanced configuration**: See `/docs/dev/gitops-architecture.md`
- **Approval workflow**: See `/docs/dev/gitops-controller-design.md`
- **Integration with CI/CD**: See `/docs/dev/deployment-strategy-simplified.md`

## Support

If you encounter issues not covered here:
1. Check `/docs/dev/gitops-e2e-testing.md` for comprehensive troubleshooting
2. Review GitOps logs with increased verbosity
3. Check GitHub issues for similar problems
4. Consult the development team

---

**Last Updated**: 2025-10-23
**Tested With**: Honua v1.0.0, .NET 9.0, LibGit2Sharp v0.30.0
