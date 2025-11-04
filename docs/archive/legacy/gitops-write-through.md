# GitOps Write-Through for Runtime Configuration

## Overview

The GitOps write-through pattern ensures that runtime configuration changes are **persistent, versioned, and auditable** by committing them to Git before applying them in-memory.

### Problem Solved

**Before GitOps write-through:**
- Runtime configuration changes were in-memory only
- Changes were lost on restart
- No audit trail or version control
- Configuration drift between deployments

**After GitOps write-through:**
- Runtime changes are committed to Git
- Full audit trail with commit messages
- Changes persist across restarts
- GitOps reconciliation creates a closed loop
- Version controlled configuration

## Architecture

### Write-Through Flow

1. **Admin makes runtime change** via API
2. **Write to Git repository files** (appsettings.json, metadata.json)
3. **Git commit with audit message** (author, timestamp, description)
4. **Git push to remote** (optional, configurable)
5. **Update in-memory state** (only if Git operations succeed)
6. **Trigger reconciliation** (optional, closes the GitOps loop)

### Rollback on Failure

If Git operations fail at any step:
- In-memory state is **NOT updated**
- Error is returned to caller
- System remains in previous stable state

## Configuration

### Enable GitOps Write-Through

**appsettings.json:**
```json
{
  "GitOps": {
    "Enabled": true,
    "RepositoryPath": "/path/to/git/repo",
    "Branch": "main",
    "AppsettingsPath": "appsettings.json",
    "MetadataPath": "metadata.json",
    "AutoPush": true,
    "TriggerReconciliation": true
  }
}
```

### Service Registration

**Program.cs / Startup.cs:**
```csharp
// Option 1: Manual configuration
services.AddGitOpsConfiguration(options =>
{
    options.Enabled = true;
    options.RepositoryPath = "/var/honua/config";
    options.Branch = "main";
    options.AppsettingsPath = "appsettings.json";
    options.MetadataPath = "metadata.json";
    options.GitUsername = "honua-bot";
    options.GitPassword = Environment.GetEnvironmentVariable("GIT_TOKEN");
});

// Option 2: Auto-detect Git repository
services.AddGitOpsConfigurationWithAutoDetect(
    appsettingsPath: "appsettings.json",
    metadataPath: "metadata.json");
```

## API Changes

### Global Protocol Toggle

**Request:**
```http
PATCH /admin/config/services/wfs
Content-Type: application/json

{
  "enabled": false
}
```

**Response (GitOps enabled):**
```json
{
  "status": "updated",
  "protocol": "wfs",
  "enabled": false,
  "message": "WFS globally disabled.",
  "mode": "GitOps",
  "commitSha": "a1b2c3d",
  "note": "ALL services are now blocked from serving this protocol. Change committed to Git."
}
```

**Response (GitOps disabled):**
```json
{
  "status": "updated",
  "protocol": "wfs",
  "enabled": false,
  "message": "WFS globally disabled.",
  "mode": "InMemoryOnly",
  "note": "This change is in-memory only. To persist, update appsettings.json and reload or restart."
}
```

### Service-Level Protocol Toggle

**Request:**
```http
PATCH /admin/config/services/roads/wms
Content-Type: application/json

{
  "enabled": true
}
```

**Response (GitOps enabled):**
```json
{
  "status": "updated",
  "serviceId": "roads",
  "protocol": "wms",
  "enabled": true,
  "message": "WMS enabled for service 'roads'.",
  "mode": "GitOps",
  "commitSha": "d4e5f6g",
  "note": "Change committed to Git and applied to in-memory state."
}
```

### Log Level Changes

**Request:**
```http
PATCH /admin/logging/categories/Honua.Server.Core.Data
Content-Type: application/json

{
  "level": "Trace"
}
```

**Response (GitOps enabled):**
```json
{
  "status": "updated",
  "category": "Honua.Server.Core.Data",
  "level": "Trace",
  "levelValue": 0,
  "message": "Log level for 'Honua.Server.Core.Data' set to Trace",
  "mode": "GitOps",
  "commitSha": "g7h8i9j",
  "note": "Change committed to Git and applied to in-memory state.",
  "effective": {
    "trace": true,
    "debug": true,
    "information": true,
    "warning": true,
    "error": true,
    "critical": true
  }
}
```

## Git Commit Messages

### Format

All commits include:
- **Human-readable description** of the change
- **Author** who initiated the change
- **Timestamp** in ISO 8601 format

**Example commit message:**
```
Toggle WFS globally to disabled

Author: Admin API
Timestamp: 2025-10-16T14:30:00.000Z
```

### Audit Trail

View configuration change history:
```bash
git log --oneline --grep="Author: Admin API" appsettings.json metadata.json

a1b2c3d Toggle WFS globally to disabled
d4e5f6g Toggle WMS for service 'roads' to enabled
g7h8i9j Set log level for 'Honua.Server.Core.Data' to Trace
```

## GitOps Reconciliation

### Closed Loop

When `TriggerReconciliation: true`:

1. Admin makes change via API
2. Change committed to Git
3. **Reconciler detects new commit**
4. Reconciler reads from Git (source of truth)
5. Reconciler applies to in-memory state
6. System converges to desired state

### Manual Reconciliation

Force reconciliation without API changes:
```http
POST /admin/gitops/reconcile
```

This reads the latest Git state and applies to in-memory configuration.

## Security Considerations

### Git Credentials

**Best practices:**
- Use environment variables for credentials
- Use Git tokens, not passwords
- Rotate credentials regularly
- Use read-only credentials for reconciliation
- Use write credentials only for runtime changes

**Example:**
```csharp
services.AddGitOpsConfiguration(options =>
{
    options.GitUsername = "honua-bot";
    options.GitPassword = Environment.GetEnvironmentVariable("HONUA_GIT_TOKEN");
});
```

### Repository Access

- **Local repository**: No authentication needed
- **Remote repository**: Requires credentials for push
- **Read-only mode**: Disable `AutoPush` for read-only repos

### RBAC Integration

GitOps write-through respects existing authorization:
- Endpoints require `RequireAdministrator` policy
- QuickStart mode disables configuration changes
- All changes are logged with author information

## Error Handling

### Git Operation Failures

**Scenario**: Git commit fails due to conflicts
```json
{
  "error": "Failed to persist configuration: merge conflict in appsettings.json"
}
```

**Recovery**:
1. In-memory state is NOT changed
2. Admin resolves conflict manually
3. Admin retries the change

### Repository Unavailable

**Scenario**: Git repository path is invalid
```json
{
  "error": "Failed to persist metadata: repository not found at /invalid/path"
}
```

**Recovery**:
1. Fix GitOps configuration
2. Restart service
3. Changes will use GitOps write-through

### Network Failures

**Scenario**: Cannot push to remote
```json
{
  "error": "Failed to persist configuration: remote push failed - network timeout"
}
```

**Recovery**:
- Commit is created locally
- Push will be retried on next change
- Or manually push: `git push origin main`

## Backward Compatibility

### Legacy Mode (GitOps Disabled)

When `GitOps.Enabled: false`:
- API endpoints work as before
- Changes are in-memory only
- Response includes `mode: "InMemoryOnly"`
- Warning message about persistence

### Migration Path

**Step 1**: Enable GitOps in read-only mode
```json
{
  "GitOps": {
    "Enabled": true,
    "AutoPush": false
  }
}
```

**Step 2**: Test configuration changes locally

**Step 3**: Enable auto-push
```json
{
  "GitOps": {
    "AutoPush": true
  }
}
```

## Monitoring and Observability

### Logs

GitOps operations are logged:
```
[Information] GitOps write-through enabled. Changes will be committed to Git branch 'main'
[Information] Global configuration committed to Git: a1b2c3d. Applying in-memory update
[Information] Metadata committed to Git: d4e5f6g. Applying in-memory update
[Error] Failed to write global configuration to Git: merge conflict
```

### Metrics

Track GitOps operations:
- Configuration changes committed
- Git push successes/failures
- Reconciliation triggers
- Rollback events

### Alerts

Monitor for:
- Repeated Git operation failures
- Configuration drift detection
- Unauthorized configuration changes
- Push failures to remote

## Testing

### Unit Tests

See `ConfigurationWriterTests.cs`:
- GitOps enabled vs disabled behavior
- Commit message formatting
- Rollback on failure
- In-memory state updates

### Integration Tests

1. **Setup**: Initialize Git repository
2. **Change**: Make runtime configuration change
3. **Verify**: Check Git commit exists
4. **Restart**: Restart service
5. **Assert**: Configuration persists

### End-to-End Tests

1. Admin toggles protocol via API
2. GitOps commits to Git
3. GitOps reconciler detects change
4. Reconciler applies from Git
5. System converges to desired state

## Comparison with Alternatives

### GitOps Write-Through vs Manual Edit

| Aspect | GitOps Write-Through | Manual appsettings.json Edit |
|--------|---------------------|------------------------------|
| Audit Trail | Full Git history with author/timestamp | No automatic tracking |
| Persistence | Automatic | Manual file edit + restart |
| Validation | API validates before commit | No validation until restart |
| Rollback | Git revert | Manual undo |
| API-driven | Yes | No |
| Zero-downtime | Yes | Requires restart |

### GitOps Write-Through vs Database Configuration

| Aspect | GitOps Write-Through | Database Config |
|--------|---------------------|-----------------|
| Version Control | Native Git | Custom versioning |
| Diff/Review | Standard Git tools | Custom UI |
| Audit Trail | Git log | Database audit table |
| Backup/Restore | Git clone | Database backup |
| Multi-environment | Git branches | Database schema |

## Troubleshooting

### Issue: Changes not persisting

**Symptom**: Runtime changes lost on restart

**Diagnosis**:
1. Check if GitOps is enabled: `GET /admin/config/status` shows `mode: "InMemoryOnly"`
2. Check logs for Git errors

**Solution**: Enable GitOps in configuration

### Issue: Git conflicts

**Symptom**: API returns "merge conflict" error

**Diagnosis**: Multiple admins changed same configuration

**Solution**:
1. Pull latest changes: `git pull origin main`
2. Resolve conflicts in appsettings.json
3. Commit resolution: `git commit`
4. Retry API change

### Issue: Push failures

**Symptom**: Commits exist locally but not on remote

**Diagnosis**: Check network connectivity, Git credentials

**Solution**:
1. Verify credentials: `git push origin main` manually
2. Update Git password/token in configuration
3. Check firewall rules

## Best Practices

1. **Enable GitOps in production** for audit trail and persistence
2. **Use separate Git branches** for different environments (dev, staging, prod)
3. **Protect main branch** with pull request reviews for manual changes
4. **Monitor Git operations** for failures and alerts
5. **Backup Git repository** regularly
6. **Use read-only credentials** for reconciliation
7. **Rotate Git tokens** periodically
8. **Test GitOps in staging** before enabling in production

## Future Enhancements

- **Pull request workflow**: Create PR instead of direct commit
- **Approval workflow**: Require approval before applying changes
- **Multi-repository**: Support separate repos for different config types
- **Webhook integration**: Trigger reconciliation on Git push
- **Configuration validation**: Schema validation before commit
- **Automated rollback**: Auto-revert on validation failures
