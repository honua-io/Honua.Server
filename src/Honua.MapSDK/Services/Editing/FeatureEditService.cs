// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Editing;

/// <summary>
/// Service for managing feature editing operations including CRUD operations,
/// validation, and backend synchronization
/// </summary>
public class FeatureEditService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FeatureEditService>? _logger;
    private readonly Dictionary<string, EditSession> _sessions = new();
    private readonly Dictionary<string, List<ValidationRule>> _validationRules = new();

    public FeatureEditService(HttpClient httpClient, ILogger<FeatureEditService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Start a new editing session
    /// </summary>
    public EditSession StartSession(string sessionId, EditSessionConfiguration? config = null)
    {
        var session = new EditSession
        {
            Id = sessionId,
            Configuration = config ?? new EditSessionConfiguration()
        };

        _sessions[sessionId] = session;
        _logger?.LogInformation("Started edit session {SessionId}", sessionId);

        return session;
    }

    /// <summary>
    /// Get an existing editing session
    /// </summary>
    public EditSession? GetSession(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    /// <summary>
    /// End an editing session
    /// </summary>
    public void EndSession(string sessionId, bool saveChanges = true)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            if (saveChanges && session.IsDirty)
            {
                _ = SaveSessionAsync(sessionId);
            }

            session.End();
            _logger?.LogInformation("Ended edit session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Set validation rules for a layer
    /// </summary>
    public void SetValidationRules(string layerId, List<ValidationRule> rules)
    {
        _validationRules[layerId] = rules;
        _logger?.LogDebug("Set {Count} validation rules for layer {LayerId}", rules.Count, layerId);
    }

    /// <summary>
    /// Get validation rules for a layer
    /// </summary>
    public List<ValidationRule> GetValidationRules(string layerId)
    {
        return _validationRules.TryGetValue(layerId, out var rules) ? rules : new List<ValidationRule>();
    }

    /// <summary>
    /// Validate a feature's attributes
    /// </summary>
    public List<ValidationError> ValidateFeature(Feature feature)
    {
        var errors = new List<ValidationError>();

        if (feature.LayerId == null)
        {
            return errors;
        }

        var rules = GetValidationRules(feature.LayerId);

        foreach (var rule in rules.Where(r => r.IsEnabled))
        {
            var value = feature.Attributes.TryGetValue(rule.FieldName, out var val) ? val : null;
            var result = rule.Validate(value);

            if (!result.IsValid)
            {
                errors.Add(new ValidationError
                {
                    FeatureId = feature.Id,
                    Field = rule.FieldName,
                    Message = result.ErrorMessage ?? "Validation failed",
                    Severity = rule.IsRequired ? ValidationSeverity.Error : ValidationSeverity.Warning
                });
            }
        }

        return errors;
    }

    /// <summary>
    /// Validate geometry for topological correctness
    /// </summary>
    public List<ValidationError> ValidateGeometry(Feature feature)
    {
        var errors = new List<ValidationError>();

        try
        {
            // Basic geometry validation
            // In a real implementation, this would use a geometry library like NetTopologySuite

            if (feature.Geometry == null)
            {
                errors.Add(new ValidationError
                {
                    FeatureId = feature.Id,
                    Message = "Geometry cannot be null",
                    Severity = ValidationSeverity.Error
                });
                return errors;
            }

            // Add more geometry validation as needed:
            // - Self-intersection checks
            // - Ring orientation
            // - Minimum vertices
            // - Coordinate validity
            // etc.

        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError
            {
                FeatureId = feature.Id,
                Message = $"Geometry validation error: {ex.Message}",
                Severity = ValidationSeverity.Error
            });
        }

        return errors;
    }

    /// <summary>
    /// Create a new feature
    /// </summary>
    public async Task<Feature> CreateFeatureAsync(string sessionId, Feature feature, string? apiEndpoint = null)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        if (!session.Configuration.AllowCreate)
        {
            throw new InvalidOperationException("Create operations are not allowed in this session");
        }

        // Validate feature
        if (session.Configuration.RequireValidation)
        {
            var errors = ValidateFeature(feature);
            if (errors.Any(e => e.Severity == ValidationSeverity.Error))
            {
                session.ValidationErrors.AddRange(errors);
                throw new ValidationException("Feature validation failed", errors);
            }
        }

        if (session.Configuration.ValidateGeometry)
        {
            var geoErrors = ValidateGeometry(feature);
            if (geoErrors.Any(e => e.Severity == ValidationSeverity.Error))
            {
                session.ValidationErrors.AddRange(geoErrors);
                throw new ValidationException("Geometry validation failed", geoErrors);
            }
        }

        // Send to backend if endpoint provided
        if (!string.IsNullOrEmpty(apiEndpoint))
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(apiEndpoint, feature.ToGeoJson());
                response.EnsureSuccessStatusCode();

                var createdFeature = await response.Content.ReadFromJsonAsync<Feature>();
                if (createdFeature != null)
                {
                    feature = createdFeature;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating feature on server");
                throw;
            }
        }

        // Add to session history
        var operation = new EditOperation
        {
            Id = Guid.NewGuid().ToString(),
            Type = EditOperationType.Create,
            Feature = feature,
            IsSynced = !string.IsNullOrEmpty(apiEndpoint),
            LayerId = feature.LayerId
        };

        session.AddOperation(operation);
        _logger?.LogDebug("Created feature {FeatureId} in session {SessionId}", feature.Id, sessionId);

        return feature;
    }

    /// <summary>
    /// Update an existing feature
    /// </summary>
    public async Task<Feature> UpdateFeatureAsync(string sessionId, Feature feature, Feature? previousState = null, string? apiEndpoint = null)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        if (!session.Configuration.AllowUpdate)
        {
            throw new InvalidOperationException("Update operations are not allowed in this session");
        }

        // Validate feature
        if (session.Configuration.RequireValidation)
        {
            var errors = ValidateFeature(feature);
            if (errors.Any(e => e.Severity == ValidationSeverity.Error))
            {
                session.ValidationErrors.AddRange(errors);
                throw new ValidationException("Feature validation failed", errors);
            }
        }

        if (session.Configuration.ValidateGeometry)
        {
            var geoErrors = ValidateGeometry(feature);
            if (geoErrors.Any(e => e.Severity == ValidationSeverity.Error))
            {
                session.ValidationErrors.AddRange(geoErrors);
                throw new ValidationException("Geometry validation failed", geoErrors);
            }
        }

        // Send to backend if endpoint provided
        if (!string.IsNullOrEmpty(apiEndpoint))
        {
            try
            {
                var endpoint = $"{apiEndpoint.TrimEnd('/')}/{feature.Id}";
                var response = await _httpClient.PutAsJsonAsync(endpoint, feature.ToGeoJson());
                response.EnsureSuccessStatusCode();

                var updatedFeature = await response.Content.ReadFromJsonAsync<Feature>();
                if (updatedFeature != null)
                {
                    feature = updatedFeature;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating feature on server");
                throw;
            }
        }

        feature.ModifiedAt = DateTime.UtcNow;
        feature.Version++;

        // Add to session history
        var operation = new EditOperation
        {
            Id = Guid.NewGuid().ToString(),
            Type = EditOperationType.Update,
            Feature = feature,
            PreviousState = previousState,
            IsSynced = !string.IsNullOrEmpty(apiEndpoint),
            LayerId = feature.LayerId
        };

        session.AddOperation(operation);
        _logger?.LogDebug("Updated feature {FeatureId} in session {SessionId}", feature.Id, sessionId);

        return feature;
    }

    /// <summary>
    /// Delete a feature
    /// </summary>
    public async Task DeleteFeatureAsync(string sessionId, Feature feature, string? apiEndpoint = null)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        if (!session.Configuration.AllowDelete)
        {
            throw new InvalidOperationException("Delete operations are not allowed in this session");
        }

        // Send to backend if endpoint provided
        if (!string.IsNullOrEmpty(apiEndpoint))
        {
            try
            {
                var endpoint = $"{apiEndpoint.TrimEnd('/')}/{feature.Id}";
                var response = await _httpClient.DeleteAsync(endpoint);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting feature on server");
                throw;
            }
        }

        // Add to session history
        var operation = new EditOperation
        {
            Id = Guid.NewGuid().ToString(),
            Type = EditOperationType.Delete,
            Feature = feature,
            PreviousState = feature.Clone(),
            IsSynced = !string.IsNullOrEmpty(apiEndpoint),
            LayerId = feature.LayerId
        };

        session.AddOperation(operation);
        _logger?.LogDebug("Deleted feature {FeatureId} in session {SessionId}", feature.Id, sessionId);
    }

    /// <summary>
    /// Undo the last operation
    /// </summary>
    public EditOperation? Undo(string sessionId)
    {
        var session = GetSession(sessionId);
        if (session == null || !session.CanUndo)
        {
            return null;
        }

        var operation = session.Operations[session.CurrentIndex];
        session.CurrentIndex--;

        _logger?.LogDebug("Undo operation {OperationId} in session {SessionId}", operation.Id, sessionId);
        return operation;
    }

    /// <summary>
    /// Redo the next operation
    /// </summary>
    public EditOperation? Redo(string sessionId)
    {
        var session = GetSession(sessionId);
        if (session == null || !session.CanRedo)
        {
            return null;
        }

        session.CurrentIndex++;
        var operation = session.Operations[session.CurrentIndex];

        _logger?.LogDebug("Redo operation {OperationId} in session {SessionId}", operation.Id, sessionId);
        return operation;
    }

    /// <summary>
    /// Save all unsynced changes in a session
    /// </summary>
    public async Task<SaveResult> SaveSessionAsync(string sessionId, string? apiEndpoint = null)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        var result = new SaveResult();
        var unsyncedOps = session.GetUnsyncedOperations();

        if (unsyncedOps.Count == 0)
        {
            result.Success = true;
            result.Message = "No changes to save";
            return result;
        }

        if (string.IsNullOrEmpty(apiEndpoint))
        {
            // Just mark as synced locally
            session.MarkAllSynced();
            result.Success = true;
            result.SavedCount = unsyncedOps.Count;
            return result;
        }

        // Batch save to server
        try
        {
            var batchEndpoint = $"{apiEndpoint.TrimEnd('/')}/batch";
            var payload = new
            {
                operations = unsyncedOps.Select(op => new
                {
                    type = op.Type.ToString().ToLowerInvariant(),
                    feature = op.Feature.ToGeoJson()
                })
            };

            var response = await _httpClient.PostAsJsonAsync(batchEndpoint, payload);

            if (response.IsSuccessStatusCode)
            {
                session.MarkAllSynced();
                result.Success = true;
                result.SavedCount = unsyncedOps.Count;
                _logger?.LogInformation("Saved {Count} operations from session {SessionId}", unsyncedOps.Count, sessionId);
            }
            else
            {
                result.Success = false;
                result.Message = $"Server returned {response.StatusCode}";
                result.Errors.Add($"Failed to save changes: {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            result.Errors.Add(ex.ToString());
            _logger?.LogError(ex, "Error saving session {SessionId}", sessionId);
        }

        return result;
    }

    /// <summary>
    /// Rollback/cancel all changes in a session
    /// </summary>
    public void RollbackSession(string sessionId)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            return;
        }

        session.Clear();
        _logger?.LogInformation("Rolled back session {SessionId}", sessionId);
    }

    /// <summary>
    /// Detect conflicts between local and server versions
    /// </summary>
    public async Task<List<EditConflict>> DetectConflictsAsync(string sessionId, string apiEndpoint)
    {
        var conflicts = new List<EditConflict>();
        var session = GetSession(sessionId);

        if (session == null || !session.Configuration.EnableConflictDetection)
        {
            return conflicts;
        }

        // Check each unsynced operation for conflicts
        foreach (var operation in session.GetUnsyncedOperations())
        {
            try
            {
                var endpoint = $"{apiEndpoint.TrimEnd('/')}/{operation.Feature.Id}";
                var response = await _httpClient.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    var serverFeature = await response.Content.ReadFromJsonAsync<Feature>();
                    if (serverFeature != null && serverFeature.Version > operation.Feature.Version)
                    {
                        conflicts.Add(new EditConflict
                        {
                            FeatureId = operation.Feature.Id,
                            LocalVersion = operation.Feature.Version,
                            ServerVersion = serverFeature.Version,
                            LocalFeature = operation.Feature,
                            ServerFeature = serverFeature,
                            ConflictType = ConflictType.VersionMismatch
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking for conflicts on feature {FeatureId}", operation.Feature.Id);
            }
        }

        return conflicts;
    }
}

/// <summary>
/// Result of a save operation
/// </summary>
public class SaveResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int SavedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Represents an edit conflict between local and server versions
/// </summary>
public class EditConflict
{
    public required string FeatureId { get; set; }
    public int LocalVersion { get; set; }
    public int ServerVersion { get; set; }
    public Feature? LocalFeature { get; set; }
    public Feature? ServerFeature { get; set; }
    public ConflictType ConflictType { get; set; }
}

/// <summary>
/// Types of edit conflicts
/// </summary>
public enum ConflictType
{
    VersionMismatch,
    FeatureDeleted,
    FeatureLocked,
    PermissionDenied
}

/// <summary>
/// Exception thrown when validation fails
/// </summary>
public class ValidationException : Exception
{
    public List<ValidationError> Errors { get; }

    public ValidationException(string message, List<ValidationError> errors) : base(message)
    {
        Errors = errors;
    }
}
