// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure;
using Azure.DigitalTwins.Core;
using Honua.Server.Enterprise.IoT.Azure.Configuration;
using Honua.Server.Enterprise.IoT.Azure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Honua.Server.Enterprise.IoT.Azure.Services;

/// <summary>
/// Service for synchronizing Honua features with Azure Digital Twins.
/// </summary>
public interface ITwinSynchronizationService
{
    /// <summary>
    /// Synchronizes a single Honua feature to Azure Digital Twins.
    /// </summary>
    Task<TwinSyncResult> SyncFeatureToTwinAsync(
        string serviceId,
        string layerId,
        string featureId,
        Dictionary<string, object?> attributes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes multiple Honua features to Azure Digital Twins in batch.
    /// </summary>
    Task<BatchSyncStatistics> SyncFeaturesToTwinsAsync(
        string serviceId,
        string layerId,
        IEnumerable<(string featureId, Dictionary<string, object?> attributes)> features,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes an Azure Digital Twin back to Honua.
    /// </summary>
    Task<TwinSyncResult> SyncTwinToFeatureAsync(
        string twinId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a twin corresponding to a deleted Honua feature.
    /// </summary>
    Task<TwinSyncResult> DeleteTwinAsync(
        string serviceId,
        string layerId,
        string featureId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes relationships based on foreign keys.
    /// </summary>
    Task<TwinSyncResult> SyncRelationshipsAsync(
        string serviceId,
        string layerId,
        string featureId,
        Dictionary<string, object?> attributes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a full batch synchronization of all features in a layer.
    /// </summary>
    Task<BatchSyncStatistics> PerformBatchSyncAsync(
        string serviceId,
        string layerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of twin synchronization service.
/// </summary>
public sealed class TwinSynchronizationService : ITwinSynchronizationService
{
    private readonly IAzureDigitalTwinsClient _adtClient;
    private readonly IDtdlModelMapper _modelMapper;
    private readonly ILogger<TwinSynchronizationService> _logger;
    private readonly AzureDigitalTwinsOptions _options;

    public TwinSynchronizationService(
        IAzureDigitalTwinsClient adtClient,
        IDtdlModelMapper modelMapper,
        ILogger<TwinSynchronizationService> logger,
        IOptions<AzureDigitalTwinsOptions> options)
    {
        _adtClient = adtClient;
        _modelMapper = modelMapper;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<TwinSyncResult> SyncFeatureToTwinAsync(
        string serviceId,
        string layerId,
        string featureId,
        Dictionary<string, object?> attributes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var mapping = GetLayerMapping(serviceId, layerId);
            if (mapping == null)
            {
                _logger.LogWarning(
                    "No layer mapping found for {ServiceId}/{LayerId}, skipping sync",
                    serviceId,
                    layerId);

                return new TwinSyncResult
                {
                    Success = false,
                    Operation = SyncOperationType.Skipped,
                    ErrorMessage = "No layer mapping configured"
                };
            }

            var twinId = GenerateTwinId(mapping, featureId, serviceId, layerId);
            var twinProperties = _modelMapper.MapFeatureToTwinProperties(attributes, mapping);

            // Add sync metadata
            twinProperties["honuaServiceId"] = serviceId;
            twinProperties["honuaLayerId"] = layerId;
            twinProperties["honuaFeatureId"] = featureId;
            twinProperties["lastSyncTime"] = DateTimeOffset.UtcNow;

            // Check if twin exists
            bool twinExists = await TwinExistsAsync(twinId, cancellationToken);
            SyncOperationType operation;

            if (twinExists && _options.Sync.ConflictStrategy != ConflictResolution.HonuaAuthoritative)
            {
                // Check for conflicts
                var existingTwin = await _adtClient.GetDigitalTwinAsync(twinId, cancellationToken);
                var conflictResult = await HandleConflictAsync(
                    twinId,
                    existingTwin.Value,
                    twinProperties,
                    cancellationToken);

                if (conflictResult != null)
                {
                    return conflictResult;
                }

                // Update existing twin
                await UpdateTwinAsync(twinId, twinProperties, existingTwin.Value.ETag, cancellationToken);
                operation = SyncOperationType.Updated;
            }
            else
            {
                // Create new twin
                var twin = new BasicDigitalTwin
                {
                    Id = twinId,
                    Metadata = { ModelId = mapping.ModelId },
                    Contents = twinProperties
                };

                await _adtClient.CreateOrReplaceDigitalTwinAsync(twinId, twin, cancellationToken: cancellationToken);
                operation = twinExists ? SyncOperationType.Updated : SyncOperationType.Created;
            }

            _logger.LogInformation(
                "Successfully synced feature {FeatureId} to twin {TwinId} ({Operation})",
                featureId,
                twinId,
                operation);

            return new TwinSyncResult
            {
                Success = true,
                TwinId = twinId,
                Operation = operation
            };
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Azure Digital Twins request failed for feature {FeatureId}: {ErrorCode}",
                featureId,
                ex.ErrorCode);

            return new TwinSyncResult
            {
                Success = false,
                TwinId = null,
                Operation = SyncOperationType.Skipped,
                ErrorCode = ex.ErrorCode,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing feature {FeatureId} to twin", featureId);

            return new TwinSyncResult
            {
                Success = false,
                Operation = SyncOperationType.Skipped,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<BatchSyncStatistics> SyncFeaturesToTwinsAsync(
        string serviceId,
        string layerId,
        IEnumerable<(string featureId, Dictionary<string, object?> attributes)> features,
        CancellationToken cancellationToken = default)
    {
        var stats = new BatchSyncStatistics
        {
            StartTime = DateTimeOffset.UtcNow
        };

        var tasks = new List<Task<TwinSyncResult>>();
        var semaphore = new SemaphoreSlim(_options.MaxBatchSize);

        foreach (var (featureId, attributes) in features)
        {
            await semaphore.WaitAsync(cancellationToken);

            var task = Task.Run(async () =>
            {
                try
                {
                    return await SyncFeatureToTwinAsync(
                        serviceId,
                        layerId,
                        featureId,
                        attributes,
                        cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);

        stats.TotalProcessed = results.Length;
        stats.Succeeded = results.Count(r => r.Success);
        stats.Failed = results.Count(r => !r.Success);
        stats.Skipped = results.Count(r => r.Operation == SyncOperationType.Skipped);
        stats.Conflicts = results.Count(r => r.Operation == SyncOperationType.Conflict);
        stats.EndTime = DateTimeOffset.UtcNow;

        foreach (var result in results)
        {
            if (!stats.OperationBreakdown.ContainsKey(result.Operation))
            {
                stats.OperationBreakdown[result.Operation] = 0;
            }
            stats.OperationBreakdown[result.Operation]++;
        }

        _logger.LogInformation(
            "Batch sync completed: {Total} processed, {Succeeded} succeeded, {Failed} failed, " +
            "{Skipped} skipped, {Conflicts} conflicts in {Duration}",
            stats.TotalProcessed,
            stats.Succeeded,
            stats.Failed,
            stats.Skipped,
            stats.Conflicts,
            stats.Duration);

        return stats;
    }

    public async Task<TwinSyncResult> SyncTwinToFeatureAsync(
        string twinId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // This would require integration with Honua's feature editing API
            // For now, we'll return a placeholder
            var twin = await _adtClient.GetDigitalTwinAsync(twinId, cancellationToken);

            // Extract Honua metadata
            if (!twin.Value.Contents.TryGetValue("honuaServiceId", out var serviceIdObj) ||
                !twin.Value.Contents.TryGetValue("honuaLayerId", out var layerIdObj) ||
                !twin.Value.Contents.TryGetValue("honuaFeatureId", out var featureIdObj))
            {
                return new TwinSyncResult
                {
                    Success = false,
                    TwinId = twinId,
                    Operation = SyncOperationType.Skipped,
                    ErrorMessage = "Twin is missing Honua metadata"
                };
            }

            var serviceId = serviceIdObj?.ToString() ?? string.Empty;
            var layerId = layerIdObj?.ToString() ?? string.Empty;
            var featureId = featureIdObj?.ToString() ?? string.Empty;

            var mapping = GetLayerMapping(serviceId, layerId);
            if (mapping == null)
            {
                return new TwinSyncResult
                {
                    Success = false,
                    TwinId = twinId,
                    Operation = SyncOperationType.Skipped,
                    ErrorMessage = "No layer mapping configured"
                };
            }

            var featureAttributes = _modelMapper.MapTwinToFeatureProperties(
                twin.Value.Contents,
                mapping);

            // TODO: Call Honua feature update API
            _logger.LogInformation(
                "Would sync twin {TwinId} back to feature {ServiceId}/{LayerId}/{FeatureId}",
                twinId,
                serviceId,
                layerId,
                featureId);

            return new TwinSyncResult
            {
                Success = true,
                TwinId = twinId,
                Operation = SyncOperationType.Updated
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing twin {TwinId} to feature", twinId);

            return new TwinSyncResult
            {
                Success = false,
                TwinId = twinId,
                Operation = SyncOperationType.Skipped,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<TwinSyncResult> DeleteTwinAsync(
        string serviceId,
        string layerId,
        string featureId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var mapping = GetLayerMapping(serviceId, layerId);
            if (mapping == null)
            {
                return new TwinSyncResult
                {
                    Success = false,
                    Operation = SyncOperationType.Skipped,
                    ErrorMessage = "No layer mapping configured"
                };
            }

            var twinId = GenerateTwinId(mapping, featureId, serviceId, layerId);

            // Delete relationships first
            if (_options.Sync.SyncRelationships)
            {
                await DeleteAllRelationshipsAsync(twinId, cancellationToken);
            }

            // Delete the twin
            await _adtClient.DeleteDigitalTwinAsync(twinId, cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully deleted twin {TwinId}", twinId);

            return new TwinSyncResult
            {
                Success = true,
                TwinId = twinId,
                Operation = SyncOperationType.Deleted
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting twin for feature {FeatureId}", featureId);

            return new TwinSyncResult
            {
                Success = false,
                Operation = SyncOperationType.Skipped,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<TwinSyncResult> SyncRelationshipsAsync(
        string serviceId,
        string layerId,
        string featureId,
        Dictionary<string, object?> attributes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var mapping = GetLayerMapping(serviceId, layerId);
            if (mapping == null || mapping.Relationships.Count == 0)
            {
                return new TwinSyncResult
                {
                    Success = true,
                    Operation = SyncOperationType.Skipped,
                    ErrorMessage = "No relationships configured"
                };
            }

            var twinId = GenerateTwinId(mapping, featureId, serviceId, layerId);

            foreach (var relationshipMapping in mapping.Relationships)
            {
                if (!attributes.TryGetValue(relationshipMapping.ForeignKeyColumn, out var foreignKeyValue)
                    || foreignKeyValue == null)
                {
                    continue;
                }

                var targetTwinId = GenerateTargetTwinId(
                    relationshipMapping,
                    foreignKeyValue.ToString()!);

                var relationship = new BasicRelationship
                {
                    Id = $"{twinId}-{relationshipMapping.RelationshipName}-{targetTwinId}",
                    SourceId = twinId,
                    TargetId = targetTwinId,
                    Name = relationshipMapping.RelationshipName,
                    Properties = new Dictionary<string, object>(relationshipMapping.Properties)
                };

                await _adtClient.CreateOrReplaceRelationshipAsync(
                    twinId,
                    relationship.Id,
                    relationship,
                    cancellationToken: cancellationToken);

                _logger.LogDebug(
                    "Created relationship {RelationshipName} from {SourceId} to {TargetId}",
                    relationshipMapping.RelationshipName,
                    twinId,
                    targetTwinId);
            }

            return new TwinSyncResult
            {
                Success = true,
                TwinId = twinId,
                Operation = SyncOperationType.Updated
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing relationships for feature {FeatureId}", featureId);

            return new TwinSyncResult
            {
                Success = false,
                Operation = SyncOperationType.Skipped,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<BatchSyncStatistics> PerformBatchSyncAsync(
        string serviceId,
        string layerId,
        CancellationToken cancellationToken = default)
    {
        // This would require integration with Honua's data access layer
        // For now, we'll return a placeholder
        _logger.LogInformation(
            "Batch sync requested for layer {ServiceId}/{LayerId} (not yet implemented)",
            serviceId,
            layerId);

        return new BatchSyncStatistics
        {
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow,
            TotalProcessed = 0,
            Succeeded = 0,
            Failed = 0,
            Skipped = 0
        };
    }

    private LayerModelMapping? GetLayerMapping(string serviceId, string layerId)
    {
        return _options.LayerMappings.FirstOrDefault(m =>
            m.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase) &&
            m.LayerId.Equals(layerId, StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateTwinId(
        LayerModelMapping mapping,
        string featureId,
        string serviceId,
        string layerId)
    {
        return mapping.TwinIdTemplate
            .Replace("{featureId}", featureId)
            .Replace("{layerId}", layerId)
            .Replace("{serviceId}", serviceId);
    }

    private static string GenerateTargetTwinId(RelationshipMapping mapping, string targetFeatureId)
    {
        return mapping.TargetTwinIdTemplate
            .Replace("{targetFeatureId}", targetFeatureId);
    }

    private async Task<bool> TwinExistsAsync(string twinId, CancellationToken cancellationToken)
    {
        try
        {
            await _adtClient.GetDigitalTwinAsync(twinId, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private async Task<TwinSyncResult?> HandleConflictAsync(
        string twinId,
        BasicDigitalTwin existingTwin,
        Dictionary<string, object> newProperties,
        CancellationToken cancellationToken)
    {
        switch (_options.Sync.ConflictStrategy)
        {
            case ConflictResolution.AdtAuthoritative:
                _logger.LogInformation("Skipping update due to ADT authoritative conflict strategy");
                return new TwinSyncResult
                {
                    Success = true,
                    TwinId = twinId,
                    Operation = SyncOperationType.Skipped,
                    Conflict = new ConflictInfo
                    {
                        DetectedAt = DateTimeOffset.UtcNow,
                        ResolutionAction = "ADT_AUTHORITATIVE_SKIP"
                    }
                };

            case ConflictResolution.Manual:
                _logger.LogWarning("Manual conflict resolution required for twin {TwinId}", twinId);
                return new TwinSyncResult
                {
                    Success = false,
                    TwinId = twinId,
                    Operation = SyncOperationType.Conflict,
                    ErrorMessage = "Manual conflict resolution required",
                    Conflict = new ConflictInfo
                    {
                        DetectedAt = DateTimeOffset.UtcNow,
                        ResolutionAction = "MANUAL_REQUIRED"
                    }
                };

            case ConflictResolution.LastWriteWins:
            case ConflictResolution.HonuaAuthoritative:
            default:
                // Proceed with update
                return null;
        }
    }

    private async Task UpdateTwinAsync(
        string twinId,
        Dictionary<string, object> properties,
        ETag? etag,
        CancellationToken cancellationToken)
    {
        var patchDocument = new List<object>();

        foreach (var (key, value) in properties)
        {
            patchDocument.Add(new
            {
                op = "replace",
                path = $"/{key}",
                value = value
            });
        }

        var jsonPatch = JsonSerializer.Serialize(patchDocument);
        await _adtClient.UpdateDigitalTwinAsync(twinId, jsonPatch, etag, cancellationToken);
    }

    private async Task DeleteAllRelationshipsAsync(string twinId, CancellationToken cancellationToken)
    {
        var relationships = _adtClient.GetRelationshipsAsync(twinId, cancellationToken: cancellationToken);

        await foreach (var relationship in relationships)
        {
            try
            {
                await _adtClient.DeleteRelationshipAsync(
                    twinId,
                    relationship.Id,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error deleting relationship {RelationshipId} for twin {TwinId}",
                    relationship.Id,
                    twinId);
            }
        }
    }
}
