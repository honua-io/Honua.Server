# GitOps End-to-End Testing Guide

**Last Updated**: 2025-10-23
**Status**: Complete
**Audience**: Developers, QA Engineers, DevOps Engineers

## Overview

This comprehensive guide covers end-to-end testing of Honua's GitOps implementation. It provides detailed instructions for setting up, executing, validating, and troubleshooting GitOps workflows in a local development environment.

## Table of Contents

1. [Introduction](#introduction)
2. [Prerequisites](#prerequisites)
3. [Quick Start](#quick-start)
4. [Detailed Setup](#detailed-setup)
5. [Test Execution](#test-execution)
6. [Validation Procedures](#validation-procedures)
7. [Test Scenarios](#test-scenarios)
8. [Troubleshooting](#troubleshooting)
9. [Advanced Testing](#advanced-testing)
10. [CI/CD Integration](#cicd-integration)
11. [Best Practices](#best-practices)

---

## Introduction

### What is GitOps?

GitOps is a declarative approach to infrastructure and application configuration management where Git serves as the single source of truth. Honua's GitOps implementation follows the pull-based model pioneered by ArgoCD and FluxCD.

### Why E2E Testing?

End-to-end testing validates the complete GitOps workflow:
- Git repository monitoring
- Change detection
- Reconciliation logic
- Approval workflows
- Error handling and rollback
- State management

### Components Under Test

This E2E testing suite validates:

1. **GitWatcher**: Background service that polls Git repository
2. **HonuaReconciler**: Applies configuration changes to running system
3. **FileStateStore**: Tracks deployment history and state
4. **FileApprovalService**: Manages deployment approvals
5. **LibGit2SharpRepository**: Git operations abstraction
6. **MetadataRegistry**: Dynamic metadata reloading
7. **Integration**: End-to-end workflow coordination

---

## Prerequisites

### Required Software

| Software | Minimum Version | Purpose |
|----------|----------------|---------|
| Git | 2.0+ | Version control |
| .NET SDK | 9.0 | Honua runtime |
| Bash | 4.0+ | Test scripts |
| jq | 1.6+ | JSON processing (optional but recommended) |

### System Requirements

- **OS**: Linux, macOS, or Windows with WSL
- **Disk Space**: 100MB for test repository and state files
- **Memory**: 512MB minimum for Honua process
- **Network**: Not required for local testing

### Knowledge Prerequisites

- Basic Git operations (commit, push, pull, log)
- Understanding of JSON configuration files
- Familiarity with Honua metadata structure
- Command-line proficiency
- Basic understanding of GitOps principles

### Installation Verification

```bash
# Check Git
git --version
# Expected: git version 2.x.x

# Check .NET
dotnet --version
# Expected: 9.0.x

# Check Bash
bash --version
# Expected: GNU bash, version 4.x.x or higher

# Check jq (optional)
jq --version
# Expected: jq-1.6 or higher
```

---

## Quick Start

For impatient developers who want to get testing immediately:

```bash
# 1. Navigate to test directory
cd /path/to/HonuaIO/samples/gitops-e2e-test

# 2. Initialize test environment
./init-test-repo.sh

# 3. Configure Honua
cp appsettings.test.json ../../src/Honua.Server.Host/appsettings.Development.json

# 4. Start Honua (in separate terminal)
cd ../../src/Honua.Server.Host
dotnet run

# 5. Wait 30 seconds for initialization

# 6. Run validation
cd ../../samples/gitops-e2e-test
./validate.sh

# 7. Make a test change
cd /tmp/honua-gitops-test-repo
vim environments/development/metadata.json
# (modify layer title)
git add -A && git commit -m "Test change"

# 8. Wait 10 seconds and validate again
cd /path/to/HonuaIO/samples/gitops-e2e-test
./validate.sh --verbose

# 9. Cleanup when done
./cleanup.sh --force
```

**Expected Result**: All validation checks pass, change is detected and reconciled within 10 seconds.

---

## Detailed Setup

### Step 1: Initialize Test Repository

The initialization script creates a complete test environment:

```bash
cd samples/gitops-e2e-test
./init-test-repo.sh
```

**What This Creates**:

```
/tmp/honua-gitops-test-repo/
├── .git/                          # Git repository
├── .gitignore                     # Git ignore rules
├── README.md                      # Repository documentation
├── .gitops/
│   └── deployment-policy.yaml     # Approval policies
└── environments/
    ├── development/
    │   ├── metadata.json          # Service/layer definitions
    │   ├── datasources.json       # Database connections
    │   └── appsettings.json       # App settings (optional)
    ├── staging/
    │   ├── metadata.json
    │   └── datasources.json
    ├── production/
    │   ├── metadata.json
    │   └── datasources.json
    └── common/
        └── shared-config.json     # Cross-environment config

/tmp/honua-gitops-state/
├── deployments/                   # Deployment records
└── approvals/                     # Approval records
```

**Script Options**:

```bash
# Use custom paths
./init-test-repo.sh /custom/repo/path /custom/state/path

# Non-interactive mode (useful for CI)
echo "y" | ./init-test-repo.sh
```

**Verification**:

```bash
# Verify repository
cd /tmp/honua-gitops-test-repo
git log --oneline
# Expected: 6-7 commits with environment setup

# Verify structure
ls -la environments/*/
# Expected: metadata.json and datasources.json in each environment

# Verify state directory
ls -la /tmp/honua-gitops-state/
# Expected: deployments/ and approvals/ subdirectories
```

### Step 2: Configure Honua

There are two approaches to configuration:

#### Option A: Full Replacement (Recommended for Testing)

```bash
cd samples/gitops-e2e-test
cp appsettings.test.json ../../src/Honua.Server.Host/appsettings.Development.json
```

**Pros**: Clean, complete test configuration
**Cons**: Overwrites existing development settings

#### Option B: Merge GitOps Section

Manually add the GitOps configuration to your existing `appsettings.Development.json`:

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
    },
    "Credentials": {
      "Username": "",
      "Password": ""
    }
  }
}
```

**Pros**: Preserves existing settings
**Cons**: Manual merge required

#### Configuration Reference

| Setting | Default | Purpose |
|---------|---------|---------|
| `Enabled` | `false` | Enable/disable GitOps |
| `RepositoryPath` | - | Path to Git repository (required) |
| `StateDirectory` | - | Path for deployment state (required) |
| `DryRun` | `false` | Detect changes but don't apply |
| `Watcher.Branch` | `main` | Git branch to watch |
| `Watcher.Environment` | `production` | Environment to reconcile |
| `Watcher.PollIntervalSeconds` | `30` | Polling frequency |

**Important Notes**:
- `RepositoryPath` must be an absolute path to a valid Git repository
- `StateDirectory` must be writable by the Honua process
- `PollIntervalSeconds` set to 10 for testing (30 recommended for production)
- `DryRun: true` is useful for observing behavior without applying changes

### Step 3: Start Honua Server

Start Honua with GitOps enabled:

```bash
cd src/Honua.Server.Host
dotnet run
```

**Expected Startup Logs**:

```
[Info] [FileApprovalService] initialized with approval directory: /tmp/honua-gitops-state/approvals
[Info] [LibGit2SharpRepository] Initializing Git repository at '/tmp/honua-gitops-test-repo' without authentication
[Info] [FileStateStore] Initializing deployment state store at '/tmp/honua-gitops-state'
[Info] [GitWatcher] started. Watching branch 'main' every 10 seconds
[Info] [GitWatcher] Initial commit: abc123def456...
```

**Verification**:

```bash
# Check GitWatcher is running
curl http://localhost:5000/health
# Expected: HTTP 200 with healthy status

# Check logs contain GitOps startup
grep -i "gitwatcher started" logs/honua-*.log
# Expected: At least one match

# Verify Git polling
sleep 15
grep -i "polling for changes" logs/honua-*.log
# Expected: Multiple poll attempts
```

**Troubleshooting Startup Issues**:

| Issue | Cause | Solution |
|-------|-------|----------|
| "RepositoryPath is not a valid Git repository" | Path doesn't exist or isn't a Git repo | Run `./init-test-repo.sh` |
| "StateDirectory does not exist" | Directory not created | Create manually: `mkdir -p /tmp/honua-gitops-state` |
| "Access denied" | Permission issues | Check file permissions: `chmod 755 /tmp/honua-gitops-state` |
| GitWatcher not starting | GitOps.Enabled is false | Verify configuration |

---

## Test Execution

### Understanding the Test Flow

```
┌─────────────────┐
│ Developer       │
│ Makes Change    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Commit to Git   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐     Poll every 10s
│ GitWatcher      ├──────────┐
│ Polls Repo      │          │
└────────┬────────┘          │
         │                   │
         │ Change Detected   │
         ▼                   │
┌─────────────────┐          │
│ Get Changed     │          │
│ Files           │          │
└────────┬────────┘          │
         │                   │
         ▼                   │
┌─────────────────┐          │
│ Filter by       │          │
│ Environment     │          │
└────────┬────────┘          │
         │                   │
         │ Relevant Change   │
         ▼                   │
┌─────────────────┐          │
│ Trigger         │          │
│ Reconciliation  │          │
└────────┬────────┘          │
         │                   │
         ▼                   │
┌─────────────────┐          │
│ Create          │          │
│ Deployment      │          │
│ Record          │          │
└────────┬────────┘          │
         │                   │
         ▼                   │
┌─────────────────┐          │
│ Retrieve Files  │          │
│ from Git        │          │
└────────┬────────┘          │
         │                   │
         ▼                   │
┌─────────────────┐          │
│ Validate JSON   │          │
└────────┬────────┘          │
         │                   │
         ▼                   │
┌─────────────────┐          │
│ Apply to        │          │
│ Metadata        │          │
│ Registry        │          │
└────────┬────────┘          │
         │                   │
         ▼                   │
┌─────────────────┐          │
│ Update          │          │
│ Deployment      │          │
│ State           │          │
└────────┬────────┘          │
         │                   │
         │ Success           │
         └───────────────────┘
```

### Basic Test Workflow

#### 1. Monitor Logs

In one terminal, monitor Honua logs in real-time:

```bash
cd src/Honua.Server.Host
tail -f logs/honua-*.log | grep -i --color "gitops\|reconcil\|gitwatcher"
```

#### 2. Make Configuration Change

In another terminal, modify the test repository:

```bash
cd /tmp/honua-gitops-test-repo

# Edit metadata
vim environments/development/metadata.json
# Change: "title": "Sample Points" → "title": "Updated Sample Points"

# Commit the change
git add environments/development/metadata.json
git commit -m "Test: Update layer title"
```

#### 3. Observe Detection

Within 10 seconds, you should see in the log monitor:

```
[Info] [GitWatcher] Polling for changes on branch 'main'
[Info] [GitWatcher] Detected new commit: abc123 -> def456
[Info] [GitWatcher] Commit by GitOps Test User: Test: Update layer title
[Info] [GitWatcher] Changed files: 1
[Debug] [GitWatcher]   - environments/development/metadata.json
[Info] [GitWatcher] Found 1 relevant files for environment 'development'
```

#### 4. Observe Reconciliation

Immediately after detection:

```
[Info] [HonuaReconciler] Starting reconciliation for environment 'development' at commit 'def456' initiated by 'GitWatcher'
[Debug] [HonuaReconciler] Retrieving 'metadata' configuration from Git: environments/development/metadata.json
[Debug] [HonuaReconciler] Successfully parsed JSON for 'metadata'
[Info] [HonuaReconciler] Applying 'metadata' configuration for environment 'development'
[Info] [HonuaReconciler] Reloading metadata registry from updated configuration
[Info] [HonuaReconciler] Successfully reloaded metadata registry with 1 services
[Info] [HonuaReconciler] Successfully reconciled 'metadata' for environment 'development'
[Info] [HonuaReconciler] Successfully completed reconciliation for environment 'development' in 234ms
```

#### 5. Validate Changes Applied

```bash
# Run validation script
cd samples/gitops-e2e-test
./validate.sh

# Check deployment state
cat /tmp/honua-gitops-state/deployments/*.json | jq '.State' | tail -1
# Expected: "Completed"

# Check reconciled metadata
cat /tmp/honua-gitops-test-repo/reconciled/development/metadata.json | jq '.services[0].layers[0].title'
# Expected: "Updated Sample Points"
```

### Using the Validation Script

The `validate.sh` script automates validation:

```bash
# Basic validation
./validate.sh

# Verbose output with detailed checks
./validate.sh --verbose

# Custom paths
./validate.sh --repo-path /custom/path --state-path /custom/state

# Save output to file
./validate.sh --verbose > validation-report.txt
```

**Validation Checks**:

1. Prerequisites (Git, jq installed)
2. Repository exists and is valid
3. State directory exists and is writable
4. Deployment records present
5. Recent reconciliation activity
6. Log files contain expected entries
7. No critical errors

**Exit Codes**:
- `0`: All checks passed
- `1`: One or more checks failed

**Example Output**:

```
================================================
    GitOps E2E Validation Report
================================================

Date: 2025-10-23 10:30:45
Repository: /tmp/honua-gitops-test-repo
State Directory: /tmp/honua-gitops-state

=== Checking Prerequisites ===
[✓] Git is installed (version 2.39.1)
[✓] jq is installed (version 1.6)

=== Checking Git Repository ===
[✓] Repository directory exists
[✓] Valid Git repository
[✓] Current commit: def456
[✓] Repository has 7 commits

=== Checking State Directory ===
[✓] State directory exists
[✓] Deployments subdirectory exists
[✓] Found 3 deployment records
[✓] Approvals subdirectory exists
[✓] State directory is writable

=== Checking Latest Deployment ===
[✓] Latest deployment found: dep_1698234567890.json
  Deployment ID: dep_1698234567890
  Environment: development
  State: Completed
  Commit: def456
  Started: 2025-10-23T10:28:00Z
  Completed: 2025-10-23T10:28:01Z
[✓] Deployment completed successfully

=== Checking Recent Reconciliation Activity ===
[✓] Last reconciliation: 2 minutes ago

=== Checking Log Files ===
[✓] Log directory exists
[✓] Latest log file: honua-20251023.log
[✓] GitWatcher is running
[✓] Git changes detected (3 times)
[✓] Successful reconciliations: 3
[✓] No errors found in logs

=== Checking Reconciled Configuration Files ===
[✓] Reconciled configuration directory exists

=== Validation Summary ===

Total checks: 22
Passed: 22
Warnings: 0
Failed: 0

Status: HEALTHY
All GitOps components are functioning correctly.
```

---

## Test Scenarios

Detailed test scenarios are provided in the `test-scenarios/` directory:

### Scenario 01: Add a Layer

**File**: `test-scenarios/01-add-layer.yaml`

**Objective**: Test adding a completely new layer to a service

**Steps**:
1. Add layer definition to `metadata.json`
2. Commit and push
3. Verify detection and reconciliation
4. Validate layer appears in metadata registry

**Validation Points**:
- Change detected within poll interval
- JSON validation passes
- MetadataRegistry reloads successfully
- New layer accessible via API
- No errors in logs

**Expected Duration**: 10-15 seconds

**Risk Level**: Low

### Scenario 02: Modify Layer

**File**: `test-scenarios/02-modify-layer.yaml`

**Objective**: Test non-breaking modifications to existing layer

**Steps**:
1. Modify layer properties (title, description, field aliases)
2. Commit and push
3. Verify reconciliation
4. Validate changes reflected

**Validation Points**:
- Non-breaking changes correctly identified
- No approval required
- Changes applied atomically
- Other layers unaffected

**Expected Duration**: 10-15 seconds

**Risk Level**: Low

### Scenario 03: Delete Layer

**File**: `test-scenarios/03-delete-layer.yaml`

**Objective**: Test layer removal

**Prerequisites**: Run Scenario 01 first to add a layer

**Steps**:
1. Remove layer from `metadata.json`
2. Commit and push
3. Verify reconciliation
4. Validate layer no longer appears

**Validation Points**:
- Deletion detected
- Deployment plan shows removal
- Layer not in reconciled metadata
- No orphaned references
- Valid JSON maintained

**Expected Duration**: 10-15 seconds

**Risk Level**: Medium

### Scenario 04: Breaking Change with Approval

**File**: `test-scenarios/04-breaking-change.yaml`

**Objective**: Test breaking change detection and approval workflow

**Prerequisites**: Production environment configured

**Steps**:
1. Make breaking change (geometry type, SRID, etc.)
2. Commit and push
3. Verify breaking change detected
4. Check deployment enters PendingApproval
5. Manually approve
6. Verify deployment proceeds

**Validation Points**:
- Breaking change correctly identified
- Approval request created
- Deployment paused
- Approval workflow functions
- Changes applied after approval

**Expected Duration**: 15-30 seconds (plus manual approval time)

**Risk Level**: High

### Running Test Scenarios

Each scenario includes:
- Detailed objectives
- Step-by-step instructions
- Expected behavior
- Validation commands
- Troubleshooting tips

**Example Usage**:

```bash
# Read the scenario
cat test-scenarios/01-add-layer.yaml

# Follow the steps in the scenario

# Validate using provided commands
cat /tmp/honua-gitops-state/deployments/*.json | jq '.State' | tail -1

# Run comprehensive validation
./validate.sh --verbose
```

---

## Troubleshooting

### Common Issues and Solutions

#### Issue: GitWatcher Not Detecting Changes

**Symptoms**:
- Commits made but no "Detected new commit" logs
- Poll interval passes but nothing happens
- No reconciliation triggered

**Diagnosis**:
```bash
# Check GitOps is enabled
grep -A 10 '"GitOps"' src/Honua.Server.Host/appsettings.Development.json

# Verify repository path
ls -la /tmp/honua-gitops-test-repo

# Check GitWatcher startup
grep "GitWatcher started" logs/honua-*.log

# Verify correct environment
grep "Watching.*environment" logs/honua-*.log
```

**Solutions**:
1. Verify `GitOps.Enabled` is `true`
2. Check repository path is correct and accessible
3. Ensure Honua server is running
4. Verify commit is in watched branch
5. Check file is in watched environment directory
6. Wait for full poll interval to elapse

**Prevention**:
- Use `./validate.sh` before making changes
- Monitor logs during testing
- Verify configuration with test commit

#### Issue: Reconciliation Fails

**Symptoms**:
- "Error during reconciliation" in logs
- Deployment state shows "Failed"
- Changes not applied to metadata

**Diagnosis**:
```bash
# Check reconciliation errors
grep -i "error during reconciliation" logs/honua-*.log

# Examine deployment state
cat /tmp/honua-gitops-state/deployments/*.json | jq '.ErrorMessage, .State' | tail -2

# Validate JSON syntax
cat /tmp/honua-gitops-test-repo/environments/development/metadata.json | jq '.'
```

**Solutions**:
1. Fix JSON syntax errors
2. Verify metadata schema is correct
3. Check datasource references are valid
4. Ensure required fields are present
5. Review error message for specifics

**Prevention**:
- Validate JSON before committing: `cat metadata.json | jq '.'`
- Use schema validation tools
- Test changes incrementally
- Keep commits small and focused

#### Issue: Changes Not Reflected in Metadata

**Symptoms**:
- Reconciliation succeeds but metadata unchanged
- No errors in logs
- Deployment shows "Completed"

**Diagnosis**:
```bash
# Check reconciled metadata
cat /tmp/honua-gitops-test-repo/reconciled/development/metadata.json | jq '.'

# Verify metadata registry reload
grep "reloaded metadata registry" logs/honua-*.log

# Check for registry errors
grep -i "metadata.*error" logs/honua-*.log
```

**Solutions**:
1. Verify MetadataRegistry is configured
2. Check reconciled configuration was written
3. Restart Honua if metadata not reloading
4. Verify metadata.configPath points to reconciled config
5. Clear any metadata caches

**Prevention**:
- Use `./validate.sh --verbose` to check reconciliation
- Monitor metadata reload logs
- Test with simple changes first

#### Issue: State Directory Permission Errors

**Symptoms**:
- "Access denied" when writing state
- Missing deployment records
- Approval files can't be created

**Diagnosis**:
```bash
# Check directory permissions
ls -ld /tmp/honua-gitops-state
ls -ld /tmp/honua-gitops-state/deployments
ls -ld /tmp/honua-gitops-state/approvals

# Check ownership
ls -l /tmp/honua-gitops-state/
```

**Solutions**:
```bash
# Fix permissions
sudo chown -R $USER:$USER /tmp/honua-gitops-state
chmod -R 755 /tmp/honua-gitops-state

# Recreate directories if needed
rm -rf /tmp/honua-gitops-state
mkdir -p /tmp/honua-gitops-state/{deployments,approvals}
chmod 755 /tmp/honua-gitops-state/*
```

**Prevention**:
- Run `./init-test-repo.sh` as your user, not root
- Don't manually create directories as root
- Use validation script to check permissions

#### Issue: Poll Interval Too Long

**Symptoms**:
- Changes take too long to detect during testing
- Feedback loop is slow

**Solutions**:
```json
{
  "GitOps": {
    "Watcher": {
      "PollIntervalSeconds": 5
    }
  }
}
```

**Note**: Restart Honua after configuration change

**Prevention**:
- Use test configuration with shorter poll interval
- Don't use 1-2 second intervals (too aggressive)
- 5-10 seconds is good for testing
- 30-60 seconds for production

### Debug Mode

Enable detailed GitOps logging:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Honua.Server.Core.GitOps": "Debug"
      }
    }
  }
}
```

Restart Honua after configuration change.

### Validation Failure Analysis

If `./validate.sh` reports failures:

1. Review failed checks in output
2. Follow suggested remediation steps
3. Check logs for detailed errors
4. Verify configuration is correct
5. Ensure all prerequisites are met
6. Try cleanup and reinitialize

---

## Advanced Testing

### Testing Multiple Environments

Test environment-specific behavior:

```json
{
  "GitOps": {
    "Watcher": {
      "Environment": "staging"
    }
  }
}
```

Then modify staging-specific files:
```bash
cd /tmp/honua-gitops-test-repo
vim environments/staging/metadata.json
git commit -am "Staging: Update configuration"
```

**Validation**: Changes to development/ should be ignored, only staging/ changes trigger reconciliation.

### Testing Approval Workflow

Configure production environment:

```json
{
  "GitOps": {
    "Watcher": {
      "Environment": "production"
    }
  }
}
```

Make breaking change:
```bash
cd /tmp/honua-gitops-test-repo
# Change geometry type in production/metadata.json
git commit -am "BREAKING: Change geometry type"
```

Verify approval required:
```bash
# Check deployment state
cat /tmp/honua-gitops-state/deployments/*.json | jq '.State' | tail -1
# Expected: "PendingApproval"

# Check approval record
ls /tmp/honua-gitops-state/approvals/
cat /tmp/honua-gitops-state/approvals/*.json | jq '.'
```

Manually approve:
```bash
# Edit approval file
vim /tmp/honua-gitops-state/approvals/dep_*.json
# Change "State": "Pending" to "State": "Approved"
# Add "RespondedAt": "2025-10-23T10:30:00Z"
# Add "Responder": "admin"
```

Wait for next reconciliation cycle (10 seconds) and verify deployment proceeds.

### Testing DryRun Mode

Enable dry run to observe behavior without applying changes:

```json
{
  "GitOps": {
    "DryRun": true
  }
}
```

Make changes and verify:
- Changes are detected
- Reconciliation is logged
- But changes are NOT applied
- Logs show "DRY RUN:" prefix

### Testing Rollback

1. Note current commit:
   ```bash
   cd /tmp/honua-gitops-test-repo
   git rev-parse HEAD
   ```

2. Make and commit change

3. Verify change is applied

4. Rollback via Git:
   ```bash
   git revert HEAD
   ```

5. Verify system reverts to previous state

### Performance Testing

Test with larger configurations:

```bash
# Generate large metadata file
cd /tmp/honua-gitops-test-repo
cat > environments/development/metadata.json <<EOF
{
  "services": [
    $(for i in {1..100}; do
      echo "    {"
      echo "      \"id\": \"service-$i\","
      echo "      \"title\": \"Service $i\","
      echo "      \"layers\": []"
      echo "    },"
    done | sed '$ s/,$//')
  ]
}
EOF

git commit -am "Performance test: 100 services"
```

Monitor reconciliation time:
```bash
grep "Successfully completed reconciliation.*in" logs/honua-*.log | tail -1
```

### Concurrent Changes Testing

Test multiple rapid changes:

```bash
cd /tmp/honua-gitops-test-repo

# Make rapid changes
for i in {1..5}; do
  echo "Change $i" >> environments/development/metadata.json
  git commit -am "Rapid change $i"
  sleep 2
done
```

Verify:
- All changes are eventually reconciled
- No race conditions
- Deployment records are sequential
- Final state matches latest commit

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: GitOps E2E Tests

on:
  pull_request:
    paths:
      - 'src/Honua.Server.Core/GitOps/**'
      - 'samples/gitops-e2e-test/**'

jobs:
  gitops-e2e-test:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Install jq
        run: sudo apt-get update && sudo apt-get install -y jq

      - name: Initialize test environment
        run: |
          cd samples/gitops-e2e-test
          ./init-test-repo.sh
          cp appsettings.test.json ../../src/Honua.Server.Host/appsettings.Development.json

      - name: Start Honua server
        run: |
          cd src/Honua.Server.Host
          dotnet run &
          HONUA_PID=$!
          echo "HONUA_PID=$HONUA_PID" >> $GITHUB_ENV
          sleep 30  # Wait for startup

      - name: Run validation
        run: |
          cd samples/gitops-e2e-test
          ./validate.sh --verbose

      - name: Test change detection
        run: |
          cd /tmp/honua-gitops-test-repo
          echo '{"test": true}' >> environments/development/metadata.json
          git add -A
          git commit -m "CI: Test change detection"
          sleep 15  # Wait for detection and reconciliation

      - name: Verify reconciliation
        run: |
          cd samples/gitops-e2e-test
          ./validate.sh --verbose

      - name: Cleanup
        if: always()
        run: |
          kill $HONUA_PID || true
          cd samples/gitops-e2e-test
          ./cleanup.sh --force

      - name: Upload logs
        if: failure()
        uses: actions/upload-artifact@v3
        with:
          name: gitops-logs
          path: src/Honua.Server.Host/logs/
```

### GitLab CI Example

```yaml
gitops-e2e-test:
  stage: test
  image: mcr.microsoft.com/dotnet/sdk:9.0
  before_script:
    - apt-get update && apt-get install -y git jq
  script:
    - cd samples/gitops-e2e-test
    - ./init-test-repo.sh
    - cp appsettings.test.json ../../src/Honua.Server.Host/appsettings.Development.json
    - cd ../../src/Honua.Server.Host
    - dotnet run &
    - sleep 30
    - cd ../../samples/gitops-e2e-test
    - ./validate.sh --verbose
    - cd /tmp/honua-gitops-test-repo
    - echo '{"test": true}' >> environments/development/metadata.json
    - git add -A && git commit -m "CI: Test"
    - sleep 15
    - cd /path/to/samples/gitops-e2e-test
    - ./validate.sh --verbose
  after_script:
    - cd samples/gitops-e2e-test
    - ./cleanup.sh --force
  artifacts:
    when: on_failure
    paths:
      - src/Honua.Server.Host/logs/
```

---

## Best Practices

### Testing Best Practices

1. **Start Simple**: Test basic scenarios before complex ones
2. **Incremental Changes**: Make small, focused changes
3. **Monitor Logs**: Always watch logs during testing
4. **Validate Frequently**: Run `./validate.sh` after each test
5. **Clean Environment**: Use `./cleanup.sh` between test runs
6. **Document Failures**: Capture logs when tests fail
7. **Test Edge Cases**: Try invalid JSON, missing files, etc.

### Development Best Practices

1. **Test Locally First**: Always test GitOps changes locally before production
2. **Use DryRun**: Test detection without applying changes
3. **Small Commits**: Keep commits focused and atomic
4. **Descriptive Messages**: Write clear commit messages
5. **Review Deployment Plans**: Check what will change before approving
6. **Monitor Reconciliation**: Watch first few reconciliations after deployment

### Configuration Best Practices

1. **Environment Separation**: Keep dev/staging/prod configurations separate
2. **Approval Policies**: Require approval for production
3. **Poll Intervals**: Use longer intervals in production (30-60s)
4. **State Retention**: Regularly backup deployment state
5. **Log Retention**: Keep logs for audit trail

### Security Best Practices

1. **Git Authentication**: Use SSH keys or tokens, not passwords
2. **State Directory**: Secure state directory with proper permissions
3. **Approval Audit**: Log all approval/rejection actions
4. **Secret Management**: Never commit secrets to Git
5. **Access Control**: Limit who can approve production deployments

---

## Appendix

### File Structure Reference

```
samples/gitops-e2e-test/
├── README.md                   # Quick start guide
├── appsettings.test.json       # Test configuration
├── init-test-repo.sh          # Repository initialization
├── validate.sh                # Validation script
├── cleanup.sh                 # Cleanup script
└── test-scenarios/
    ├── 01-add-layer.yaml      # Add layer scenario
    ├── 02-modify-layer.yaml   # Modify layer scenario
    ├── 03-delete-layer.yaml   # Delete layer scenario
    └── 04-breaking-change.yaml # Breaking change scenario
```

### Related Documentation

- [GitOps Getting Started](/docs/dev/gitops-getting-started.md)
- [GitOps Architecture](/docs/dev/gitops-architecture.md)
- [GitOps Controller Design](/docs/dev/gitops-controller-design.md)
- [GitOps Example Workflow](/docs/dev/gitops-example-workflow.md)
- [GitOps Implementation Summary](/docs/dev/gitops-implementation-summary.md)

### Support

For issues or questions:
1. Review troubleshooting section
2. Check GitHub issues
3. Consult development team
4. Refer to comprehensive documentation

---

**Document Version**: 1.0
**Last Updated**: 2025-10-23
**Maintained By**: Honua Development Team
