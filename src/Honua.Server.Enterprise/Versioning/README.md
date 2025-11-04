# Honua Data Versioning - "Git for Data"

Enterprise feature providing complete version control for geospatial data with temporal tables, conflict detection, merge capabilities, and branching.

## Features

- **Temporal Tables** - Track all changes to data over time with PostgreSQL temporal tables
- **Version History** - Complete audit trail of every change to every record
- **Change Tracking** - Field-level diff showing exactly what changed between versions
- **Three-Way Merge** - Git-like merge with automatic conflict detection
- **Conflict Resolution** - Multiple strategies for resolving conflicts (ours, theirs, manual)
- **Rollback** - Restore to any previous version instantly
- **Branching** - Create experimental branches and merge back to main
- **Time Travel** - Query data as it was at any point in time
- **Soft Deletes** - Deleted records remain in history

## Architecture

### Core Components

```
┌─────────────────────────────────────────┐
│     IVersioningService<T>               │
│  - Create, Update, Delete (versioned)  │
│  - GetVersion, GetAtTimestamp           │
│  - GetHistory, GetChanges               │
│  - Merge, DetectConflicts, Rollback    │
│  - CreateBranch, GetVersionTree         │
└──────────────┬──────────────────────────┘
               │
               ├── PostgresVersioningService<T>
               │   └── Temporal table operations
               │
               ├── IMergeEngine<T>
               │   ├── DefaultMergeEngine (three-way merge)
               │   └── SemanticMergeEngine (custom merge logic)
               │
               └── Models
                   ├── ChangeSet, FieldChange
                   ├── MergeResult, MergeConflict
                   ├── VersionHistory, VersionTree
                   └── RollbackRequest, RollbackResult
```

### Database Schema

```sql
-- Temporal versioned table (example: collections)
collections_versioned (
  id UUID,                        -- Stable ID across versions
  version BIGINT,                 -- Version number
  content_hash VARCHAR(64),       -- SHA256 of content
  version_created_at TIMESTAMPTZ, -- When this version was created
  version_created_by VARCHAR,     -- Who created this version
  version_valid_from TIMESTAMPTZ, -- When version became active
  version_valid_to TIMESTAMPTZ,   -- When superseded (NULL = current)
  parent_version BIGINT,          -- Parent for branching/merging
  branch VARCHAR,                 -- Branch name (default: main)
  commit_message TEXT,            -- Description of changes
  is_deleted BOOLEAN,             -- Soft delete flag
  -- ... entity-specific columns ...
  PRIMARY KEY (id, version)
)

-- Change tracking
version_changes (
  entity_id UUID,
  from_version BIGINT,
  to_version BIGINT,
  field_name VARCHAR,
  old_value JSONB,
  new_value JSONB,
  change_type VARCHAR  -- 'added', 'removed', 'modified'
)

-- Merge conflicts
merge_conflicts (
  entity_id UUID,
  base_version, current_version, incoming_version BIGINT,
  field_name VARCHAR,
  base_value, current_value, incoming_value JSONB,
  conflict_type VARCHAR,
  is_resolved BOOLEAN,
  resolved_value JSONB
)

-- Merge audit log
merge_operations (
  entity_id UUID,
  base_version, current_version, incoming_version BIGINT,
  merge_strategy VARCHAR,
  status VARCHAR,
  merged_by VARCHAR
)
```

## Usage

### 1. Make Your Entity Versionable

```csharp
public class MyCollection : VersionedEntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();

    public override string CalculateContentHash()
    {
        var content = JsonSerializer.Serialize(new { Name, Description, Properties });
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}
```

### 2. Register the Versioning Service

```csharp
// Program.cs
services.AddSingleton<IVersioningService<MyCollection>>(sp =>
    new PostgresVersioningService<MyCollection>(
        connectionString: configuration.GetConnectionString("Postgres"),
        tableName: "collections_versioned"
    ));
```

### 3. Basic Operations

```csharp
var versioningService = serviceProvider.GetRequiredService<IVersioningService<MyCollection>>();

// Create new versioned entity
var collection = new MyCollection
{
    Id = Guid.NewGuid(),
    Name = "My Dataset",
    Description = "Initial version"
};
await versioningService.CreateAsync(collection, createdBy: "user@example.com", commitMessage: "Initial commit");

// Update (creates new version)
collection.Description = "Updated description";
await versioningService.UpdateAsync(collection, updatedBy: "user@example.com", commitMessage: "Updated description");

// Get current version
var current = await versioningService.GetCurrentAsync(collection.Id);

// Get specific version
var version1 = await versioningService.GetVersionAsync(collection.Id, version: 1);

// Time travel - get as it was at specific time
var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
var historical = await versioningService.GetAtTimestampAsync(collection.Id, yesterday);
```

### 4. Version History and Changes

```csharp
// Get complete history
var history = await versioningService.GetHistoryAsync(collection.Id);
Console.WriteLine($"Total versions: {history.TotalVersions}");
Console.WriteLine($"First version: {history.FirstVersionAt}");
Console.WriteLine($"Current version: {history.Current.Version}");

// Get changes between versions
var changes = await versioningService.GetChangesAsync(collection.Id, fromVersion: 1, toVersion: 3);
foreach (var change in changes.FieldChanges)
{
    Console.WriteLine($"{change.FieldName}: {change.OldValue} -> {change.NewValue}");
}

// Compare any version with current
var diff = await versioningService.CompareWithCurrentAsync(collection.Id, compareVersion: 2);
Console.WriteLine($"Changed {diff.ChangeCount} fields since version 2");
```

### 5. Branching and Merging

```csharp
// Create a feature branch from current version
var branchedVersion = await versioningService.CreateBranchAsync(
    collection.Id,
    fromVersion: current.Version,
    branchName: "feature/new-properties",
    createdBy: "developer@example.com"
);

// Make changes on branch
branchedVersion.Properties["new_field"] = "new_value";
await versioningService.UpdateAsync(branchedVersion, "developer@example.com", "Added new field");

// Meanwhile, main branch also gets updated (creates conflict)
var mainVersion = await versioningService.GetCurrentAsync(collection.Id);
mainVersion.Properties["new_field"] = "different_value";
await versioningService.UpdateAsync(mainVersion, "user@example.com", "Updated same field");

// Detect conflicts
var conflicts = await versioningService.DetectConflictsAsync(
    collection.Id,
    baseVersion: current.Version,
    currentVersion: mainVersion.Version,
    incomingVersion: branchedVersion.Version
);

foreach (var conflict in conflicts)
{
    Console.WriteLine($"Conflict on {conflict.FieldName}:");
    Console.WriteLine($"  Base: {conflict.BaseValue}");
    Console.WriteLine($"  Current: {conflict.CurrentValue}");
    Console.WriteLine($"  Incoming: {conflict.IncomingValue}");
}

// Merge with conflict resolution
var mergeRequest = new MergeRequest
{
    EntityId = collection.Id,
    BaseVersion = current.Version,
    CurrentVersion = mainVersion.Version,
    IncomingVersion = branchedVersion.Version,
    Strategy = MergeStrategy.AutoMerge,
    FieldResolutions = new Dictionary<string, ResolutionStrategy>
    {
        ["new_field"] = ResolutionStrategy.UseTheirs // Use branch version
    },
    CommitMessage = "Merged feature/new-properties into main",
    MergedBy = "developer@example.com"
};

var mergeResult = await versioningService.MergeAsync(mergeRequest);

if (mergeResult.Success && !mergeResult.HasConflicts)
{
    Console.WriteLine($"Merge successful! Auto-merged {mergeResult.AutoMergedChanges.Count} fields");
}
else
{
    Console.WriteLine($"Merge failed or has conflicts: {mergeResult.GetSummary()}");
}
```

### 6. Rollback

```csharp
// Rollback to previous version
var rollbackRequest = new RollbackRequest
{
    EntityId = collection.Id,
    ToVersion = 2,
    Reason = "Reverting bad changes",
    RolledBackBy = "admin@example.com",
    CreateBranch = false // Create new version on main branch
};

var rollbackResult = await versioningService.RollbackAsync(rollbackRequest);

if (rollbackResult.Success)
{
    Console.WriteLine($"Rolled back to version {rollbackRequest.ToVersion}");
    Console.WriteLine($"New version: {rollbackResult.NewVersion}");
}
```

### 7. Version Tree Visualization

```csharp
// Get version tree for visualizing branches and merges
var tree = await versioningService.GetVersionTreeAsync(collection.Id);

Console.WriteLine($"Root version: {tree.Root?.Entity.Version}");
Console.WriteLine($"Branches: {string.Join(", ", tree.Branches.Keys)}");

foreach (var node in tree.Nodes)
{
    if (node.IsBranchPoint)
    {
        Console.WriteLine($"Version {node.Entity.Version} is a branch point with {node.ChildVersions.Count} children");
    }
}
```

## Merge Strategies

### AutoMerge (Default)
- Automatically merges non-conflicting changes
- Requires manual resolution only for conflicts

```csharp
MergeStrategy.AutoMerge
```

### Ours
- On conflict, always use current version

```csharp
MergeStrategy.Ours
```

### Theirs
- On conflict, always use incoming version

```csharp
MergeStrategy.Theirs
```

### Manual
- Require explicit resolution for all conflicts

```csharp
MergeStrategy.Manual
```

## Conflict Resolution Strategies

### Use Ours
Keep the current version's value

### Use Theirs
Accept the incoming version's value

### Use Base
Revert to the base version's value

### Custom
Provide a custom merged value

### Keep Both
For arrays/lists, combine both values

## Advanced: Custom Merge Functions

```csharp
var semanticMerge = new SemanticMergeEngine<MyCollection>();

// Merge arrays by combining unique elements
semanticMerge.RegisterArrayMerge<string>("Tags");

// Merge dictionaries by combining keys
semanticMerge.RegisterDictionaryMerge<string, object>("Properties");

// Custom merge function
semanticMerge.RegisterMergeFunction("Priority", (baseVal, currentVal, incomingVal) =>
{
    // Take the highest priority
    var current = (int?)currentVal ?? 0;
    var incoming = (int?)incomingVal ?? 0;
    return Math.Max(current, incoming);
});

var service = new PostgresVersioningService<MyCollection>(
    connectionString,
    tableName,
    mergeEngine: semanticMerge
);
```

## Database Migrations

Apply the versioning migration:

```bash
psql -h localhost -U postgres -d honua < src/Honua.Server.Core/Data/Migrations/007_DataVersioning.sql
```

This creates:
- `collections_versioned` temporal table
- `version_changes` change tracking table
- `merge_conflicts` conflict storage
- `merge_operations` audit log
- Helper functions for version management
- Automatic change tracking triggers
- Convenient views (`collections_current`, `version_history_summary`)

## Querying Historical Data

### Direct SQL Queries

```sql
-- Get current versions only
SELECT * FROM collections_current;

-- Get all versions of an entity
SELECT * FROM collections_versioned
WHERE id = '123e4567-e89b-12d3-a456-426614174000'
ORDER BY version DESC;

-- Get version at specific time
SELECT * FROM collections_versioned
WHERE id = '123e4567-e89b-12d3-a456-426614174000'
  AND version_valid_from <= '2025-01-15 10:00:00'
  AND (version_valid_to IS NULL OR version_valid_to > '2025-01-15 10:00:00')
ORDER BY version DESC
LIMIT 1;

-- Get all changes to a specific field
SELECT * FROM version_changes
WHERE entity_id = '123e4567-e89b-12d3-a456-426614174000'
  AND field_name = 'description'
ORDER BY to_version;

-- Find unresolved merge conflicts
SELECT * FROM merge_conflicts
WHERE entity_id = '123e4567-e89b-12d3-a456-426614174000'
  AND is_resolved = FALSE;

-- Audit: Who changed what and when
SELECT
    version,
    version_created_by,
    version_created_at,
    commit_message,
    COUNT(vc.id) as changes_count
FROM collections_versioned cv
LEFT JOIN version_changes vc ON cv.id = vc.entity_id AND cv.version = vc.to_version
WHERE cv.id = '123e4567-e89b-12d3-a456-426614174000'
GROUP BY version, version_created_by, version_created_at, commit_message
ORDER BY version DESC;
```

## Performance Considerations

1. **Indexes** - Properly indexed for fast queries on id, version, timestamp
2. **Partitioning** - Consider partitioning by date for very large tables
3. **Archiving** - Old versions can be archived to separate tables
4. **Cleanup** - Implement retention policies for version history
5. **Caching** - Cache current versions and recent history

## Use Cases

### Collaborative Editing
Multiple users can edit data simultaneously, with automatic conflict detection and merge.

### Audit Compliance
Complete audit trail of who changed what and when, required for regulatory compliance.

### Experimentation
Create branches for testing changes without affecting production data.

### Data Recovery
Instantly rollback to any previous version in case of errors or data corruption.

### Change Review
Review all changes before accepting them (approval workflows).

### Time-Series Analysis
Analyze how data evolved over time by querying historical versions.

## Security Considerations

1. **Access Control** - Implement authorization for version operations
2. **Encryption** - Use PostgreSQL transparent data encryption for sensitive data
3. **Audit Logging** - All version operations are automatically logged
4. **Soft Deletes** - Deleted data remains in history for recovery

## Troubleshooting

### Merge conflicts
If automatic merge fails, use manual resolution:

```csharp
var conflicts = await versioningService.DetectConflictsAsync(...);
foreach (var conflict in conflicts)
{
    // Resolve each conflict
    conflict.ResolvedValue = /* your resolution logic */;
    conflict.IsResolved = true;
    conflict.ResolutionStrategy = ResolutionStrategy.Custom;
}
```

### Version not found
Ensure the version exists and is not on a different branch:

```csharp
var history = await versioningService.GetHistoryAsync(entityId);
var allVersions = history.Versions.Select(v => v.Entity.Version);
```

### Performance issues
Check indexes and consider:
- Partitioning large history tables
- Archiving old versions
- Using materialized views for complex queries

## Related Features

- [Licensing System](../../Honua.Server.Core/Licensing/README.md) - Enforce versioning based on license tier
- [Multitenancy](../Multitenancy/README.md) - Tenant-isolated version history
- [GitOps](../GitOps/README.md) - Version control for infrastructure configuration

## License

Copyright (c) 2025 HonuaIO. All rights reserved.

This is an **Enterprise module** - requires Honua Enterprise license.
