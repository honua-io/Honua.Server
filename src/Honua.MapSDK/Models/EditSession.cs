namespace Honua.MapSDK.Models;

/// <summary>
/// Represents an editing session with undo/redo support and dirty tracking
/// </summary>
public class EditSession
{
    /// <summary>
    /// Unique identifier for this session
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Name/description of the session
    /// </summary>
    public string Name { get; set; } = "Edit Session";

    /// <summary>
    /// All operations performed in this session
    /// </summary>
    public List<EditOperation> Operations { get; set; } = new();

    /// <summary>
    /// Current position in the undo/redo stack
    /// </summary>
    public int CurrentIndex { get; set; } = -1;

    /// <summary>
    /// Whether there are unsaved changes
    /// </summary>
    public bool IsDirty => Operations.Any(op => !op.IsSynced);

    /// <summary>
    /// When the session was started
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the session was last modified
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// When the session was ended
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Whether the session is currently active
    /// </summary>
    public bool IsActive => EndTime == null;

    /// <summary>
    /// User who owns this session
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Layers that can be edited in this session
    /// </summary>
    public List<string> EditableLayers { get; set; } = new();

    /// <summary>
    /// Maximum number of operations to keep in history
    /// </summary>
    public int MaxHistorySize { get; set; } = 100;

    /// <summary>
    /// Session configuration
    /// </summary>
    public EditSessionConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// Validation errors encountered during editing
    /// </summary>
    public List<ValidationError> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Statistics about the session
    /// </summary>
    public EditSessionStatistics Statistics => new()
    {
        TotalOperations = Operations.Count,
        CreateCount = Operations.Count(op => op.Type == EditOperationType.Create),
        UpdateCount = Operations.Count(op => op.Type == EditOperationType.Update),
        DeleteCount = Operations.Count(op => op.Type == EditOperationType.Delete),
        UnsyncedCount = Operations.Count(op => !op.IsSynced),
        Duration = EndTime.HasValue
            ? EndTime.Value - StartTime
            : DateTime.UtcNow - StartTime
    };

    /// <summary>
    /// Whether undo is available
    /// </summary>
    public bool CanUndo => CurrentIndex >= 0;

    /// <summary>
    /// Whether redo is available
    /// </summary>
    public bool CanRedo => CurrentIndex < Operations.Count - 1;

    /// <summary>
    /// Add an operation to the session
    /// </summary>
    public void AddOperation(EditOperation operation)
    {
        // Remove any forward history when adding a new operation
        if (CurrentIndex < Operations.Count - 1)
        {
            Operations.RemoveRange(CurrentIndex + 1, Operations.Count - CurrentIndex - 1);
        }

        Operations.Add(operation);
        CurrentIndex = Operations.Count - 1;
        LastModified = DateTime.UtcNow;

        // Trim history if too large
        if (Operations.Count > MaxHistorySize)
        {
            var toRemove = Operations.Count - MaxHistorySize;
            Operations.RemoveRange(0, toRemove);
            CurrentIndex -= toRemove;
        }
    }

    /// <summary>
    /// Get the current operation
    /// </summary>
    public EditOperation? GetCurrentOperation()
    {
        if (CurrentIndex >= 0 && CurrentIndex < Operations.Count)
        {
            return Operations[CurrentIndex];
        }
        return null;
    }

    /// <summary>
    /// Get all operations that need to be synced
    /// </summary>
    public List<EditOperation> GetUnsyncedOperations()
    {
        return Operations.Where(op => !op.IsSynced).ToList();
    }

    /// <summary>
    /// Mark all operations as synced
    /// </summary>
    public void MarkAllSynced()
    {
        foreach (var operation in Operations)
        {
            operation.IsSynced = true;
        }
    }

    /// <summary>
    /// Clear all operations
    /// </summary>
    public void Clear()
    {
        Operations.Clear();
        CurrentIndex = -1;
        ValidationErrors.Clear();
        LastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// End the editing session
    /// </summary>
    public void End()
    {
        EndTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Configuration for an edit session
/// </summary>
public class EditSessionConfiguration
{
    /// <summary>
    /// Allow creating new features
    /// </summary>
    public bool AllowCreate { get; set; } = true;

    /// <summary>
    /// Allow updating existing features
    /// </summary>
    public bool AllowUpdate { get; set; } = true;

    /// <summary>
    /// Allow deleting features
    /// </summary>
    public bool AllowDelete { get; set; } = true;

    /// <summary>
    /// Require attribute validation before saving
    /// </summary>
    public bool RequireValidation { get; set; } = true;

    /// <summary>
    /// Enable geometry validation
    /// </summary>
    public bool ValidateGeometry { get; set; } = true;

    /// <summary>
    /// Auto-save changes periodically
    /// </summary>
    public bool AutoSave { get; set; }

    /// <summary>
    /// Auto-save interval in seconds
    /// </summary>
    public int AutoSaveInterval { get; set; } = 60;

    /// <summary>
    /// Enable snapping to other features
    /// </summary>
    public bool EnableSnapping { get; set; } = true;

    /// <summary>
    /// Snap tolerance in pixels
    /// </summary>
    public int SnapTolerance { get; set; } = 10;

    /// <summary>
    /// Show vertex handles when editing
    /// </summary>
    public bool ShowVertexHandles { get; set; } = true;

    /// <summary>
    /// Allow editing vertices (add, move, delete)
    /// </summary>
    public bool AllowVertexEditing { get; set; } = true;

    /// <summary>
    /// Detect and warn about conflicts
    /// </summary>
    public bool EnableConflictDetection { get; set; } = true;
}

/// <summary>
/// Statistics about an edit session
/// </summary>
public class EditSessionStatistics
{
    /// <summary>
    /// Total number of operations
    /// </summary>
    public int TotalOperations { get; init; }

    /// <summary>
    /// Number of create operations
    /// </summary>
    public int CreateCount { get; init; }

    /// <summary>
    /// Number of update operations
    /// </summary>
    public int UpdateCount { get; init; }

    /// <summary>
    /// Number of delete operations
    /// </summary>
    public int DeleteCount { get; init; }

    /// <summary>
    /// Number of unsynced operations
    /// </summary>
    public int UnsyncedCount { get; init; }

    /// <summary>
    /// Duration of the session
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Validation error encountered during editing
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Feature ID where error occurred
    /// </summary>
    public required string FeatureId { get; set; }

    /// <summary>
    /// Field name (null for geometry errors)
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Error severity
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    /// <summary>
    /// When the error was detected
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional error details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Validation error severity levels
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message
    /// </summary>
    Info,

    /// <summary>
    /// Warning that should be reviewed
    /// </summary>
    Warning,

    /// <summary>
    /// Error that prevents saving
    /// </summary>
    Error
}
