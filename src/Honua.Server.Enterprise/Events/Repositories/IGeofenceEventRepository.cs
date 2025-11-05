using Honua.Server.Enterprise.Events.Models;

namespace Honua.Server.Enterprise.Events.Repositories;

/// <summary>
/// Repository for geofence event storage
/// </summary>
public interface IGeofenceEventRepository
{
    /// <summary>
    /// Create a new geofence event
    /// </summary>
    Task<GeofenceEvent> CreateAsync(
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create multiple events in batch
    /// </summary>
    Task<List<GeofenceEvent>> CreateBatchAsync(
        List<GeofenceEvent> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get event by ID
    /// </summary>
    Task<GeofenceEvent?> GetByIdAsync(
        Guid id,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query events with filters
    /// </summary>
    Task<List<GeofenceEvent>> QueryEventsAsync(
        GeofenceEventQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of events matching query
    /// </summary>
    Task<int> GetCountAsync(
        GeofenceEventQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query parameters for geofence events
/// </summary>
public class GeofenceEventQuery
{
    public Guid? GeofenceId { get; set; }
    public string? EntityId { get; set; }
    public GeofenceEventType? EventType { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? TenantId { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}
