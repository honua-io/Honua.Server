# GitOps Getting Started Guide

**Last Updated**: 2025-10-23

This guide will help you set up and configure GitOps for Honua. GitOps enables automated, pull-based deployments from Git repositories across multiple environments.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Architecture Overview](#architecture-overview)
3. [Git Repository Setup](#git-repository-setup)
4. [Configuration](#configuration)
5. [Enabling GitOps](#enabling-gitops)
6. [First Deployment](#first-deployment)
7. [Troubleshooting](#troubleshooting)
8. [CLI Commands (Future)](#cli-commands-future)

## Prerequisites

### Required Software

- **Git**: Version 2.0 or higher
- **.NET 9.0**: Required for Honua server
- **LibGit2Sharp**: Already included in `Honua.Server.Core` (v0.30.0)

### Required Knowledge

- Basic Git operations (clone, commit, push, pull)
- YAML/JSON configuration file formats
- Basic understanding of Honua metadata structure

### Access Requirements

- Git repository access (GitHub, GitLab, Bitbucket, or self-hosted)
- SSH key or HTTPS credentials for Git authentication
- Write access to create deployment state files (if using FileStateStore)

## Architecture Overview

Honua's GitOps implementation follows the ArgoCD/FluxCD pull-based model:

```
┌─────────────────┐
│   Git Repo      │
│  (Source of     │
│   Truth)        │
└────────┬────────┘
         │
         │ Pull every 30s
         ▼
┌─────────────────┐
│   GitWatcher    │
│  (Background    │
│   Service)      │
└────────┬────────┘
         │
         │ Detects Changes
         ▼
┌─────────────────┐
│  HonuaReconciler│
│  (Compares      │
│   Desired vs    │
│   Actual)       │
└────────┬────────┘
         │
         │ Applies Changes
         ▼
┌─────────────────┐
│  Metadata       │
│  Registry       │
│  (Reloads)      │
└─────────────────┘
```

**Key Components**:
- **GitWatcher**: Polls Git repository for changes
- **HonuaReconciler**: Compares desired (Git) vs actual (deployed) state and applies changes
- **DeploymentStateStore**: Tracks deployment history and status
- **MetadataRegistry**: Reloads configuration without server restart

## Git Repository Setup

### 1. Create Git Repository

Create a new Git repository (or use an existing one) with the following structure:

```
honua-config/
├── environments/
│   ├── development/
│   │   ├── metadata.json
│   │   ├── datasources.json
│   │   └── appsettings.json (optional)
│   ├── staging/
│   │   ├── metadata.json
│   │   ├── datasources.json
│   │   └── appsettings.json (optional)
│   ├── production/
│   │   ├── metadata.json
│   │   ├── datasources.json
│   │   └── appsettings.json (optional)
│   └── common/
│       └── shared-config.json (optional)
├── .gitops/
│   └── deployment-policy.yaml (optional)
└── README.md
```

### 2. Environment Structure

Each environment folder should contain:

#### metadata.json
Contains Honua service and layer definitions:

```json
{
  "services": [
    {
      "id": "my-service",
      "title": "My GIS Service",
      "description": "Description of the service",
      "layers": [
        {
          "id": "my-layer",
          "title": "My Layer",
          "description": "Layer description",
          "datasource": "my-datasource",
          "table": "public.my_table",
          "geometryColumn": "geom",
          "geometryType": "Point",
          "srid": 4326
        }
      ]
    }
  ]
}
```

#### datasources.json
Contains database connection strings:

```json
{
  "datasources": [
    {
      "id": "my-datasource",
      "type": "PostgreSQL",
      "connectionString": "Host=localhost;Database=mydb;Username=user;Password=pass",
      "description": "Primary database"
    }
  ]
}
```

**Security Note**: Use environment variables or secret management for production credentials:
```json
{
  "datasources": [
    {
      "id": "prod-db",
      "type": "PostgreSQL",
      "connectionString": "${DB_CONNECTION_STRING}",
      "description": "Production database"
    }
  ]
}
```

### 3. Example Configuration Files

See the `samples/gitops/` directory in the Honua repository for complete examples:
- `/samples/gitops/environments/development/metadata.yaml`
- `/samples/gitops/environments/development/datasources.yaml`
- `/samples/gitops/environments/production/metadata.yaml`
- `/samples/gitops/environments/production/datasources.yaml`

## Configuration

### 1. appsettings.json Configuration

**Note**: This is the planned configuration structure. Dependency injection setup is not yet complete.

Add the following section to your `appsettings.json` or `appsettings.Production.json`:

```json
{
  "GitOps": {
    "Enabled": true,
    "RepositoryUrl": "https://github.com/your-org/honua-config.git",
    "Branch": "main",
    "LocalPath": "/var/honua/gitops-repo",
    "PollingInterval": "00:00:30",
    "Environment": "production",
    "Authentication": {
      "Type": "SSH",
      "SshKeyPath": "/home/honua/.ssh/id_rsa",
      "SshKeyPassphrase": ""
    },
    "StateStore": {
      "Type": "File",
      "FilePath": "/var/honua/deployments"
    },
    "AutoApprove": false,
    "DryRun": false
  }
}
```

#### Configuration Options

| Option | Type | Description | Default |
|--------|------|-------------|---------|
| `Enabled` | bool | Enable/disable GitOps | `false` |
| `RepositoryUrl` | string | Git repository URL (HTTPS or SSH) | Required |
| `Branch` | string | Git branch to track | `"main"` |
| `LocalPath` | string | Local path to clone repository | Required |
| `PollingInterval` | TimeSpan | How often to poll for changes | `"00:00:30"` |
| `Environment` | string | Environment name (dev/staging/production) | Required |
| `Authentication.Type` | string | "SSH" or "HTTPS" | `"SSH"` |
| `Authentication.SshKeyPath` | string | Path to SSH private key | null |
| `Authentication.Username` | string | Git username (for HTTPS) | null |
| `Authentication.Password` | string | Git password or token (for HTTPS) | null |
| `StateStore.Type` | string | "File" or "GitHub" | `"File"` |
| `StateStore.FilePath` | string | Path for FileStateStore | `/var/honua/deployments` |
| `AutoApprove` | bool | Auto-approve deployments (dev only!) | `false` |
| `DryRun` | bool | Simulate deployments without applying | `false` |

### 2. Dependency Injection Setup

**Status**: ⚠️ Not yet implemented. This is planned work.

The GitOps services need to be registered in `ServiceCollectionExtensions.cs`:

```csharp
// Planned implementation (not yet complete)
services.AddGitOps(configuration);
```

This will register:
- `IGitRepository` → `LibGit2SharpRepository`
- `IReconciler` → `HonuaReconciler`
- `IDeploymentStateStore` → `FileStateStore` or `GitHubStateStore`
- `GitWatcher` as a hosted service

### 3. Authentication Setup

#### SSH Authentication (Recommended)

1. Generate SSH key pair:
   ```bash
   ssh-keygen -t ed25519 -C "honua-gitops@example.com" -f ~/.ssh/honua_gitops
   ```

2. Add public key to Git provider (GitHub/GitLab/etc.)

3. Configure in `appsettings.json`:
   ```json
   {
     "GitOps": {
       "Authentication": {
         "Type": "SSH",
         "SshKeyPath": "/home/honua/.ssh/honua_gitops",
         "SshKeyPassphrase": ""
       }
     }
   }
   ```

#### HTTPS Authentication

For HTTPS authentication, use a personal access token:

```json
{
  "GitOps": {
    "RepositoryUrl": "https://github.com/your-org/honua-config.git",
    "Authentication": {
      "Type": "HTTPS",
      "Username": "git",
      "Password": "${GITHUB_TOKEN}"
    }
  }
}
```

## Enabling GitOps

**Status**: ⚠️ Integration not yet complete. These are the planned steps.

### 1. Start the Server

Once dependency injection is configured, start the Honua server:

```bash
cd /path/to/HonuaIO
dotnet run --project src/Honua.Server.Host
```

### 2. Verify GitWatcher is Running

Check the logs for GitWatcher startup:

```
[INFO] GitWatcher starting for repository: https://github.com/your-org/honua-config.git
[INFO] Polling interval: 00:00:30
[INFO] Environment: production
[INFO] Initial clone completed: /var/honua/gitops-repo
```

### 3. Monitor Reconciliation

The reconciler will run on the polling interval. Watch the logs:

```
[INFO] GitWatcher detected new commit: abc123
[INFO] Starting reconciliation for environment 'production' at commit 'abc123'
[INFO] Reconciling environment-specific configuration from 'environments/production'
[INFO] Successfully reconciled 'metadata' for environment 'production'
[INFO] Successfully completed reconciliation in 1234ms
```

## First Deployment

### 1. Make a Configuration Change

Edit your metadata file in the Git repository:

```bash
cd honua-config
vim environments/production/metadata.json
# Add a new layer or modify existing configuration
git add environments/production/metadata.json
git commit -m "Add new bike lanes layer"
git push origin main
```

### 2. Wait for Polling (or trigger manually)

GitWatcher polls every 30 seconds by default. Wait for the next poll cycle.

**Future**: Manual trigger via CLI:
```bash
honua gitops sync --now
```

### 3. Monitor Deployment

Watch the logs for reconciliation:

```
[INFO] GitWatcher detected new commit: def456
[INFO] Commit by: John Doe
[INFO] Message: Add new bike lanes layer
[INFO] Starting reconciliation for environment 'production'
[INFO] Reconciling environment-specific configuration
[INFO] Retrieving 'metadata' configuration from Git
[INFO] Successfully parsed JSON for 'metadata'
[INFO] Applying 'metadata' configuration
[INFO] Reloading metadata registry
[INFO] Successfully reloaded metadata registry with 5 services
[INFO] Successfully completed reconciliation in 2345ms
```

### 4. Verify Changes

Check that the new layer is available:

```bash
curl https://your-honua-server.com/ogc/collections
# Should include your new layer
```

### 5. Check Deployment State

**Future**: CLI command to view deployment history:
```bash
honua deployments list --environment production
```

## Troubleshooting

### GitWatcher Not Starting

**Symptom**: No GitWatcher logs on server startup

**Possible Causes**:
1. GitOps not enabled in configuration
2. Dependency injection not set up
3. Configuration validation failed

**Solution**:
```bash
# Check configuration
cat appsettings.json | grep -A 20 "GitOps"

# Check for errors in logs
grep -i "gitops\|gitwatcher" logs/honua-*.log
```

### Authentication Failures

**Symptom**: `Authentication failed` or `Permission denied` errors

**Possible Causes**:
1. SSH key not configured correctly
2. SSH key not added to Git provider
3. Incorrect credentials for HTTPS

**Solution**:
```bash
# Test SSH connection
ssh -i /path/to/key git@github.com

# For HTTPS, verify token has correct permissions
# GitHub: repo (full control)
# GitLab: api, read_repository, write_repository
```

### Repository Clone Failures

**Symptom**: `Failed to clone repository` errors

**Possible Causes**:
1. Invalid repository URL
2. Network connectivity issues
3. Local path permissions

**Solution**:
```bash
# Test repository access
git clone https://github.com/your-org/honua-config.git /tmp/test-clone

# Check local path permissions
ls -la /var/honua/
sudo chown -R honua:honua /var/honua/gitops-repo
```

### Reconciliation Errors

**Symptom**: `Error during reconciliation` in logs

**Possible Causes**:
1. Invalid JSON/YAML in configuration files
2. Missing required fields
3. Database connection failures

**Solution**:
```bash
# Validate JSON
cat environments/production/metadata.json | jq .

# Check deployment state
cat /var/honua/deployments/production/current.json | jq .

# Review reconciliation logs
grep -i "reconcil" logs/honua-*.log | tail -50
```

### Metadata Not Reloading

**Symptom**: Configuration changes not taking effect

**Possible Causes**:
1. Metadata registry not wired up
2. Reconciler not configured with metadata registry
3. File paths incorrect

**Solution**:
```bash
# Check reconciler configuration
# Verify IMetadataRegistry is injected into HonuaReconciler

# Enable debug logging for reconciliation
# In appsettings.json:
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Honua.Server.Core.GitOps": "Debug"
      }
    }
  }
}
```

### Common Configuration Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `Repository not found` | Invalid URL or access denied | Verify repository exists and credentials are correct |
| `Failed to parse metadata.json` | Invalid JSON | Validate JSON with `jq` or online validator |
| `Unknown configuration type` | Unsupported config file | Stick to: `metadata.json`, `datasources.json`, `appsettings.json` |
| `Deployment state file not found` | State store not initialized | Check `StateStore.FilePath` exists and is writable |

## CLI Commands (Future)

**Status**: ⚠️ Not yet implemented

The following CLI commands are planned for managing GitOps deployments:

### Status Commands

```bash
# View GitOps status
honua gitops status

# View current environment configuration
honua gitops config show

# View deployment history
honua deployments list --environment production

# View specific deployment details
honua deployment <deployment-id>
```

### Deployment Commands

```bash
# Trigger immediate sync (instead of waiting for poll)
honua gitops sync --now

# Approve pending deployment
honua deployment approve <deployment-id>

# Rollback to previous deployment
honua rollback production

# Rollback to specific deployment
honua rollback production --to <deployment-id>
```

### Configuration Commands

```bash
# Validate configuration files
honua gitops validate

# Dry run a deployment
honua gitops sync --dry-run

# View deployment plan
honua deployment plan --environment production
```

## Next Steps

1. **Review Architecture**: Read [gitops-architecture.md](./gitops-architecture.md) for design details
2. **Implementation Status**: Check [gitops-implementation-status.md](./gitops-implementation-status.md) for current progress
3. **Complete Integration**: Follow the integration checklist to enable GitOps
4. **Test Thoroughly**: Verify reconciliation works with your configuration
5. **Monitor Deployments**: Set up logging and metrics for GitOps operations

## Resources

- [GitOps Architecture](./gitops-architecture.md)
- [GitOps Controller Design](./gitops-controller-design.md)
- [GitOps Implementation Status](./gitops-implementation-status.md)
- [GitOps Implementation Summary](./gitops-implementation-summary.md)
- [Example Workflow](./gitops-example-workflow.md)
- [Sample Configurations](../../samples/gitops/)

## Support

For issues or questions:
1. Check [gitops-implementation-status.md](./gitops-implementation-status.md) for known limitations
2. Review logs in `logs/honua-*.log`
3. Enable debug logging for detailed troubleshooting
4. Consult the architecture documentation

---

**Last Updated**: 2025-10-23
**Status**: Documentation complete, implementation integration in progress
