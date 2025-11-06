using Honua.Server.Enterprise.Events.Models;
using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Events.Services;

/// <summary>
/// Service for evaluating locations against geofences and generating events
/// </summary>
public interface IGeofenceEvaluationService
{
    /// <summary>
    /// Evaluate a location and generate enter/exit events
    /// </summary>
    /// <param name="entityId">Entity identifier</param>
    /// <param name="location">Location to evaluate</param>
    /// <param name="eventTime">When the event occurred</param>
    /// <param name="properties">Additional event properties</param>
    /// <param name="entityType">Optional entity type</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenancy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation result with generated events</returns>
    Task<GeofenceEvaluationResult> EvaluateLocationAsync(
        string entityId,
        Point location,
        DateTime eventTime,
        Dictionary<string, object>? properties = null,
        string? entityType = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of geofence evaluation
/// </summary>
public class GeofenceEvaluationResult
{
    /// <summary>
    /// Entity ID that was evaluated
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Events that were generated (enter, exit)
    /// </summary>
    public List<GeofenceEvent> Events { get; set; } = new();

    /// <summary>
    /// Current geofences the entity is inside
    /// </summary>
    public List<Geofence> CurrentGeofences { get; set; } = new();

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// Number of geofences evaluated
    /// </summary>
    public int GeofencesEvaluated { get; set; }
}
