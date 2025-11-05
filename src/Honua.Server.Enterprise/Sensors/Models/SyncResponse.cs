namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Response from a sync operation indicating what was processed and any errors encountered.
/// </summary>
public sealed record SyncResponse
{
    /// <summary>
    /// The server's current timestamp. Client should store this for the next sync operation.
    /// </summary>
    public DateTime ServerTimestamp { get; init; }

    /// <summary>
    /// Number of observations successfully created on the server.
    /// </summary>
    public int ObservationsCreated { get; init; }

    /// <summary>
    /// Number of observations successfully updated on the server.
    /// </summary>
    public int ObservationsUpdated { get; init; }

    /// <summary>
    /// Collection of errors encountered during sync.
    /// </summary>
    public IReadOnlyList<SyncError> Errors { get; init; } = Array.Empty<SyncError>();
}

/// <summary>
/// Represents an error that occurred during a sync operation.
/// </summary>
public sealed record SyncError
{
    /// <summary>
    /// The index of the observation in the sync request that caused the error.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Error code indicating the type of error.
    /// </summary>
    public string Code { get; init; } = default!;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; init; } = default!;

    /// <summary>
    /// Optional details about the error.
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }
}
