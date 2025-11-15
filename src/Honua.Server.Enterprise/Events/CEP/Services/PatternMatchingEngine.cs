// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Events.CEP.Models;
using Honua.Server.Enterprise.Events.CEP.Repositories;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Repositories;
using Honua.Server.Enterprise.Events.Services;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Events.CEP.Services;

/// <summary>
/// Implementation of pattern matching engine for complex event processing
/// </summary>
public class PatternMatchingEngine : IPatternMatchingEngine
{
    private readonly IPatternRepository _patternRepository;
    private readonly IPatternStateRepository _stateRepository;
    private readonly IGeofenceEventRepository _eventRepository;
    private readonly IGeofenceToAlertBridgeService? _alertBridgeService;
    private readonly ILogger<PatternMatchingEngine> _logger;

    public PatternMatchingEngine(
        IPatternRepository patternRepository,
        IPatternStateRepository stateRepository,
        IGeofenceEventRepository eventRepository,
        ILogger<PatternMatchingEngine> logger,
        IGeofenceToAlertBridgeService? alertBridgeService = null)
    {
        _patternRepository = patternRepository ?? throw new ArgumentNullException(nameof(patternRepository));
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alertBridgeService = alertBridgeService;
    }

    public async Task<List<PatternMatchResult>> EvaluateEventAsync(
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PatternMatchResult>();

        try
        {
            // Get all enabled patterns
            var patterns = await _patternRepository.GetEnabledPatternsAsync(
                geofenceEvent.TenantId,
                cancellationToken);

            _logger.LogDebug(
                "Evaluating event {EventId} against {PatternCount} active CEP patterns",
                geofenceEvent.Id,
                patterns.Count);

            // Evaluate each pattern
            foreach (var pattern in patterns.OrderByDescending(p => p.Priority))
            {
                try
                {
                    var result = await EvaluatePatternAsync(pattern, geofenceEvent, cancellationToken);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error evaluating pattern {PatternName} ({PatternId}) for event {EventId}",
                        pattern.Name,
                        pattern.Id,
                        geofenceEvent.Id);
                }
            }

            _logger.LogInformation(
                "CEP evaluation complete for event {EventId}: {PartialMatches} partial, {CompleteMatches} complete",
                geofenceEvent.Id,
                results.Count(r => r.MatchType == MatchType.Partial),
                results.Count(r => r.MatchType == MatchType.Complete));

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating event {EventId} against CEP patterns", geofenceEvent.Id);
            throw;
        }
    }

    private async Task<PatternMatchResult?> EvaluatePatternAsync(
        GeofenceEventPattern pattern,
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken)
    {
        return pattern.PatternType switch
        {
            PatternType.Sequence => await EvaluateSequencePatternAsync(pattern, geofenceEvent, cancellationToken),
            PatternType.Count => await EvaluateCountPatternAsync(pattern, geofenceEvent, cancellationToken),
            PatternType.Correlation => await EvaluateCorrelationPatternAsync(pattern, geofenceEvent, cancellationToken),
            PatternType.Absence => await EvaluateAbsencePatternAsync(pattern, geofenceEvent, cancellationToken),
            _ => null
        };
    }

    private async Task<PatternMatchResult?> EvaluateSequencePatternAsync(
        GeofenceEventPattern pattern,
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken)
    {
        // Sequence pattern: A then B then C within time window
        // Partition by entity_id
        var partitionKey = geofenceEvent.EntityId;

        // Get or create state
        var state = await _stateRepository.GetOrCreateStateAsync(
            pattern.Id,
            partitionKey,
            geofenceEvent.EventTime,
            TimeSpan.FromSeconds(pattern.WindowDurationSeconds),
            pattern.WindowType,
            geofenceEvent.TenantId,
            cancellationToken);

        // Check if window expired
        if (DateTime.UtcNow > state.WindowEnd)
        {
            // Window expired without completing pattern, delete state
            await _stateRepository.DeleteStateAsync(state.Id, cancellationToken);
            return null;
        }

        // Find current condition to match
        var currentCondition = pattern.Conditions.ElementAtOrDefault(state.CurrentConditionIndex);
        if (currentCondition == null)
        {
            // All conditions already matched
            return null;
        }

        // Check if event matches current condition
        if (!EventMatchesCondition(geofenceEvent, currentCondition, state))
        {
            // Event doesn't match current condition
            return null;
        }

        // Event matches! Update state
        state.MatchedEventIds.Add(geofenceEvent.Id);
        state.CurrentConditionIndex++;
        state.LastEventTime = geofenceEvent.EventTime;
        UpdateContext(state.Context, geofenceEvent);

        // Check if pattern is complete
        if (state.CurrentConditionIndex >= pattern.Conditions.Count)
        {
            // Pattern complete!
            return await CompletePatternMatchAsync(pattern, state, cancellationToken);
        }

        // Pattern partially matched, save state
        await _stateRepository.UpdateStateAsync(state, cancellationToken);

        return new PatternMatchResult
        {
            PatternId = pattern.Id,
            PatternName = pattern.Name,
            MatchType = MatchType.Partial,
            StateId = state.Id
        };
    }

    private async Task<PatternMatchResult?> EvaluateCountPatternAsync(
        GeofenceEventPattern pattern,
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken)
    {
        // Count pattern: N occurrences within window
        // Use sliding window or tumbling window based on pattern config

        if (pattern.WindowType == WindowType.Tumbling)
        {
            return await EvaluateTumblingCountPatternAsync(pattern, geofenceEvent, cancellationToken);
        }

        // Sliding window count pattern
        var partitionKey = DeterminePartitionKey(pattern, geofenceEvent);

        // Get or create state
        var state = await _stateRepository.GetOrCreateStateAsync(
            pattern.Id,
            partitionKey,
            geofenceEvent.EventTime,
            TimeSpan.FromSeconds(pattern.WindowDurationSeconds),
            WindowType.Sliding,
            geofenceEvent.TenantId,
            cancellationToken);

        // Check if event matches condition
        var condition = pattern.Conditions.FirstOrDefault();
        if (condition == null || !EventMatchesCondition(geofenceEvent, condition, state))
        {
            return null;
        }

        // Add event to state
        state.MatchedEventIds.Add(geofenceEvent.Id);
        state.LastEventTime = geofenceEvent.EventTime;
        UpdateContext(state.Context, geofenceEvent);

        // Check if count threshold reached
        var minOccurrences = condition.MinOccurrences ?? 1;
        if (state.MatchedEventIds.Count >= minOccurrences)
        {
            // Pattern complete!
            return await CompletePatternMatchAsync(pattern, state, cancellationToken);
        }

        // Save partial state
        await _stateRepository.UpdateStateAsync(state, cancellationToken);

        return new PatternMatchResult
        {
            PatternId = pattern.Id,
            PatternName = pattern.Name,
            MatchType = MatchType.Partial,
            StateId = state.Id
        };
    }

    private async Task<PatternMatchResult?> EvaluateTumblingCountPatternAsync(
        GeofenceEventPattern pattern,
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken)
    {
        // Tumbling window: fixed, non-overlapping windows
        var partitionKey = DeterminePartitionKey(pattern, geofenceEvent);
        var windowDuration = TimeSpan.FromSeconds(pattern.WindowDurationSeconds);

        // Calculate window boundaries (aligned to wall clock)
        var windowStart = AlignToTumblingWindow(geofenceEvent.EventTime, windowDuration);
        var windowEnd = windowStart.Add(windowDuration);

        // Get or create tumbling window state
        var windowState = await _stateRepository.GetOrCreateTumblingWindowAsync(
            pattern.Id,
            partitionKey,
            windowStart,
            windowEnd,
            geofenceEvent.TenantId,
            cancellationToken);

        // Check if window is still open
        if (DateTime.UtcNow > windowEnd)
        {
            // Window closed, evaluate and clean up
            return await CloseTumblingWindowAsync(pattern, windowState, cancellationToken);
        }

        // Add event to window
        windowState.EventIds.Add(geofenceEvent.Id);
        windowState.EventCount++;
        UpdateContext(windowState.Context, geofenceEvent);

        // Check if threshold reached
        var condition = pattern.Conditions.FirstOrDefault();
        var minOccurrences = condition?.MinOccurrences ?? 1;

        if (windowState.EventCount >= minOccurrences)
        {
            // Threshold reached, close window
            return await CloseTumblingWindowAsync(pattern, windowState, cancellationToken);
        }

        // Update window state
        await _stateRepository.UpdateTumblingWindowAsync(windowState, cancellationToken);

        return new PatternMatchResult
        {
            PatternId = pattern.Id,
            PatternName = pattern.Name,
            MatchType = MatchType.Partial,
            StateId = windowState.Id
        };
    }

    private async Task<PatternMatchResult?> EvaluateCorrelationPatternAsync(
        GeofenceEventPattern pattern,
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken)
    {
        // Correlation pattern: multiple entities performing same action
        // Partition by geofence_id (looking for multiple entities in same geofence)
        var partitionKey = geofenceEvent.GeofenceId.ToString();

        // Get or create state
        var state = await _stateRepository.GetOrCreateStateAsync(
            pattern.Id,
            partitionKey,
            geofenceEvent.EventTime,
            TimeSpan.FromSeconds(pattern.WindowDurationSeconds),
            pattern.WindowType,
            geofenceEvent.TenantId,
            cancellationToken);

        // Check if event matches condition
        var condition = pattern.Conditions.FirstOrDefault();
        if (condition == null || !EventMatchesCondition(geofenceEvent, condition, state))
        {
            return null;
        }

        // Check if this is a unique entity (for correlation patterns)
        if (condition.UniqueEntities && state.Context.EntityIds.Contains(geofenceEvent.EntityId))
        {
            // Same entity, don't count again
            return null;
        }

        // Add event to state
        state.MatchedEventIds.Add(geofenceEvent.Id);
        state.LastEventTime = geofenceEvent.EventTime;
        UpdateContext(state.Context, geofenceEvent);

        // Update unique entity count
        state.Context.UniqueEntityCount = state.Context.EntityIds.Distinct().Count();

        // Check if threshold reached
        var minOccurrences = condition.MinOccurrences ?? 1;
        if (state.Context.UniqueEntityCount >= minOccurrences)
        {
            // Pattern complete!
            return await CompletePatternMatchAsync(pattern, state, cancellationToken);
        }

        // Save partial state
        await _stateRepository.UpdateStateAsync(state, cancellationToken);

        return new PatternMatchResult
        {
            PatternId = pattern.Id,
            PatternName = pattern.Name,
            MatchType = MatchType.Partial,
            StateId = state.Id
        };
    }

    private async Task<PatternMatchResult?> EvaluateAbsencePatternAsync(
        GeofenceEventPattern pattern,
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken)
    {
        // Absence pattern: A occurred but B did not occur within time window
        // This requires background job to check for timeout
        // For now, we track positive conditions and schedule timeout check

        var partitionKey = geofenceEvent.EntityId;

        // Get or create state
        var state = await _stateRepository.GetOrCreateStateAsync(
            pattern.Id,
            partitionKey,
            geofenceEvent.EventTime,
            TimeSpan.FromSeconds(pattern.WindowDurationSeconds),
            pattern.WindowType,
            geofenceEvent.TenantId,
            cancellationToken);

        // Find first positive condition (expected event)
        var positiveCondition = pattern.Conditions.FirstOrDefault(c => c.Expected);
        if (positiveCondition != null && EventMatchesCondition(geofenceEvent, positiveCondition, state))
        {
            // Positive condition matched, start window
            state.MatchedEventIds.Add(geofenceEvent.Id);
            state.LastEventTime = geofenceEvent.EventTime;
            UpdateContext(state.Context, geofenceEvent);
            await _stateRepository.UpdateStateAsync(state, cancellationToken);

            // Schedule timeout check (would be done by background job)
            return new PatternMatchResult
            {
                PatternId = pattern.Id,
                PatternName = pattern.Name,
                MatchType = MatchType.Partial,
                StateId = state.Id
            };
        }

        // Check negative condition (event that should NOT occur)
        var negativeCondition = pattern.Conditions.FirstOrDefault(c => !c.Expected);
        if (negativeCondition != null && EventMatchesCondition(geofenceEvent, negativeCondition, state))
        {
            // Negative event occurred, pattern failed
            await _stateRepository.DeleteStateAsync(state.Id, cancellationToken);
            return null;
        }

        return null;
    }

    private bool EventMatchesCondition(
        GeofenceEvent geofenceEvent,
        EventCondition condition,
        PatternMatchState state)
    {
        // Event type matching
        if (condition.EventType != null &&
            !geofenceEvent.EventType.ToString().Equals(condition.EventType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Geofence ID matching
        if (condition.GeofenceId.HasValue && geofenceEvent.GeofenceId != condition.GeofenceId.Value)
        {
            return false;
        }

        // Geofence name pattern matching
        if (condition.GeofenceNamePattern != null &&
            !Regex.IsMatch(geofenceEvent.GeofenceName, condition.GeofenceNamePattern))
        {
            return false;
        }

        // Entity ID matching
        if (condition.EntityId != null && geofenceEvent.EntityId != condition.EntityId)
        {
            return false;
        }

        // Entity ID pattern matching
        if (condition.EntityIdPattern != null &&
            !Regex.IsMatch(geofenceEvent.EntityId, condition.EntityIdPattern))
        {
            return false;
        }

        // Entity type matching
        if (condition.EntityType != null && geofenceEvent.EntityType != condition.EntityType)
        {
            return false;
        }

        // Dwell time matching (for exit events)
        if (condition.MinDwellTimeSeconds.HasValue &&
            (!geofenceEvent.DwellTimeSeconds.HasValue ||
             geofenceEvent.DwellTimeSeconds.Value < condition.MinDwellTimeSeconds.Value))
        {
            return false;
        }

        if (condition.MaxDwellTimeSeconds.HasValue &&
            (!geofenceEvent.DwellTimeSeconds.HasValue ||
             geofenceEvent.DwellTimeSeconds.Value > condition.MaxDwellTimeSeconds.Value))
        {
            return false;
        }

        // Sequence timing (max time since previous condition)
        if (condition.MaxTimeSincePreviousSeconds.HasValue &&
            state.Context.EventTimes.Any())
        {
            var timeSincePrevious = (geofenceEvent.EventTime - state.Context.EventTimes.Last()).TotalSeconds;
            if (timeSincePrevious > condition.MaxTimeSincePreviousSeconds.Value)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateContext(PatternMatchContext context, GeofenceEvent geofenceEvent)
    {
        if (!context.EntityIds.Contains(geofenceEvent.EntityId))
        {
            context.EntityIds.Add(geofenceEvent.EntityId);
        }

        if (!context.GeofenceIds.Contains(geofenceEvent.GeofenceId))
        {
            context.GeofenceIds.Add(geofenceEvent.GeofenceId);
        }

        if (!context.GeofenceNames.Contains(geofenceEvent.GeofenceName))
        {
            context.GeofenceNames.Add(geofenceEvent.GeofenceName);
        }

        context.EventTypes.Add(geofenceEvent.EventType.ToString());
        context.EventTimes.Add(geofenceEvent.EventTime);
        context.DwellTimesSeconds.Add(geofenceEvent.DwellTimeSeconds);
    }

    private async Task<PatternMatchResult> CompletePatternMatchAsync(
        GeofenceEventPattern pattern,
        PatternMatchState state,
        CancellationToken cancellationToken)
    {
        // Create match history record
        var matchHistory = new PatternMatchHistory
        {
            Id = Guid.NewGuid(),
            PatternId = pattern.Id,
            PatternName = pattern.Name,
            MatchedEventIds = state.MatchedEventIds,
            PartitionKey = state.PartitionKey,
            MatchContext = state.Context,
            WindowStart = state.WindowStart,
            WindowEnd = state.WindowEnd,
            AlertSeverity = pattern.AlertSeverity,
            TenantId = state.TenantId
        };

        // Generate alert fingerprint
        var fingerprint = GenerateAlertFingerprint(pattern, state);
        matchHistory.AlertFingerprint = fingerprint;

        // Save match history
        await _stateRepository.CreateMatchHistoryAsync(matchHistory, cancellationToken);

        // Delete pattern state (no longer needed)
        await _stateRepository.DeleteStateAsync(state.Id, cancellationToken);

        // Generate alert if bridge service is available
        if (_alertBridgeService != null)
        {
            try
            {
                await GeneratePatternAlertAsync(pattern, matchHistory, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate alert for pattern match {MatchId}", matchHistory.Id);
            }
        }

        _logger.LogInformation(
            "Pattern {PatternName} ({PatternId}) matched for partition {PartitionKey}: {EventCount} events",
            pattern.Name,
            pattern.Id,
            state.PartitionKey,
            state.MatchedEventIds.Count);

        return new PatternMatchResult
        {
            PatternId = pattern.Id,
            PatternName = pattern.Name,
            MatchType = MatchType.Complete,
            MatchHistoryId = matchHistory.Id,
            AlertFingerprint = fingerprint
        };
    }

    private async Task<PatternMatchResult?> CloseTumblingWindowAsync(
        GeofenceEventPattern pattern,
        TumblingWindowState windowState,
        CancellationToken cancellationToken)
    {
        windowState.Status = TumblingWindowStatus.Closed;

        var condition = pattern.Conditions.FirstOrDefault();
        var minOccurrences = condition?.MinOccurrences ?? 1;

        if (windowState.EventCount >= minOccurrences)
        {
            // Pattern matched!
            windowState.Status = TumblingWindowStatus.Matched;

            var matchHistory = new PatternMatchHistory
            {
                Id = Guid.NewGuid(),
                PatternId = pattern.Id,
                PatternName = pattern.Name,
                MatchedEventIds = windowState.EventIds,
                PartitionKey = windowState.PartitionKey,
                MatchContext = windowState.Context,
                WindowStart = windowState.WindowStart,
                WindowEnd = windowState.WindowEnd,
                AlertSeverity = pattern.AlertSeverity,
                TenantId = windowState.TenantId
            };

            var fingerprint = GenerateAlertFingerprint(pattern, windowState.PartitionKey, windowState.WindowStart);
            matchHistory.AlertFingerprint = fingerprint;

            await _stateRepository.CreateMatchHistoryAsync(matchHistory, cancellationToken);

            // Generate alert
            if (_alertBridgeService != null)
            {
                await GeneratePatternAlertAsync(pattern, matchHistory, cancellationToken);
            }

            return new PatternMatchResult
            {
                PatternId = pattern.Id,
                PatternName = pattern.Name,
                MatchType = MatchType.Complete,
                MatchHistoryId = matchHistory.Id,
                AlertFingerprint = fingerprint
            };
        }

        await _stateRepository.UpdateTumblingWindowAsync(windowState, cancellationToken);
        return null;
    }

    private string GenerateAlertFingerprint(GeofenceEventPattern pattern, PatternMatchState state)
    {
        return GenerateAlertFingerprint(pattern, state.PartitionKey, state.WindowStart);
    }

    private string GenerateAlertFingerprint(GeofenceEventPattern pattern, string partitionKey, DateTime windowStart)
    {
        var data = $"cep-pattern:{pattern.Id}:partition:{partitionKey}:window:{windowStart:O}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return $"cep-{Convert.ToHexString(hash)[..40]}";
    }

    private async Task GeneratePatternAlertAsync(
        GeofenceEventPattern pattern,
        PatternMatchHistory matchHistory,
        CancellationToken cancellationToken)
    {
        // Create a synthetic geofence event for alert generation
        // Use the first event from the match as the base
        var firstEventId = matchHistory.MatchedEventIds.FirstOrDefault();
        if (firstEventId == Guid.Empty)
        {
            return;
        }

        var firstEvent = await _eventRepository.GetByIdAsync(
            firstEventId,
            matchHistory.TenantId,
            cancellationToken);

        if (firstEvent == null)
        {
            return;
        }

        // Create synthetic event with pattern match details
        var syntheticEvent = new GeofenceEvent
        {
            Id = matchHistory.Id,
            EventType = GeofenceEventType.Enter, // Placeholder
            EventTime = matchHistory.MatchContext.EventTimes.FirstOrDefault(),
            GeofenceId = matchHistory.MatchContext.GeofenceIds.FirstOrDefault(),
            GeofenceName = matchHistory.MatchContext.GeofenceNames.FirstOrDefault() ?? "Multiple",
            EntityId = matchHistory.MatchContext.EntityIds.FirstOrDefault() ?? matchHistory.PartitionKey,
            Location = firstEvent.Location,
            Properties = new Dictionary<string, object>
            {
                ["pattern_id"] = pattern.Id,
                ["pattern_name"] = pattern.Name,
                ["pattern_type"] = pattern.PatternType.ToString(),
                ["matched_event_count"] = matchHistory.MatchedEventIds.Count,
                ["unique_entity_count"] = matchHistory.MatchContext.UniqueEntityCount,
                ["window_start"] = matchHistory.WindowStart,
                ["window_end"] = matchHistory.WindowEnd
            },
            TenantId = matchHistory.TenantId
        };

        // Process through alert bridge
        await _alertBridgeService!.ProcessGeofenceEventAsync(syntheticEvent, cancellationToken);
    }

    private string DeterminePartitionKey(GeofenceEventPattern pattern, GeofenceEvent geofenceEvent)
    {
        return pattern.PatternType switch
        {
            PatternType.Correlation => geofenceEvent.GeofenceId.ToString(),
            _ => geofenceEvent.EntityId
        };
    }

    private DateTime AlignToTumblingWindow(DateTime timestamp, TimeSpan windowDuration)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var elapsedTicks = (timestamp - epoch).Ticks;
        var windowTicks = windowDuration.Ticks;
        var alignedTicks = (elapsedTicks / windowTicks) * windowTicks;
        return epoch.AddTicks(alignedTicks);
    }

    public async Task<CleanupResult> CleanupExpiredStatesAsync(
        int retentionHours = 24,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _stateRepository.CleanupExpiredStatesAsync(
                retentionHours,
                cancellationToken);

            _logger.LogInformation(
                "CEP cleanup complete: {PatternStates} pattern states, {TumblingWindows} tumbling windows deleted",
                result.PatternStatesDeleted,
                result.TumblingWindowsDeleted);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired CEP states");
            throw;
        }
    }

    public async Task<List<ActivePatternState>> GetActiveStatesAsync(
        Guid? patternId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        return await _stateRepository.GetActiveStatesAsync(
            patternId,
            tenantId,
            cancellationToken);
    }

    public async Task<List<PatternMatchHistory>> TestPatternAsync(
        Guid patternId,
        DateTime startTime,
        DateTime endTime,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // This is a simplified implementation
        // Full implementation would replay historical events through the pattern matching engine
        return await _stateRepository.GetMatchHistoryAsync(
            patternId,
            startTime,
            endTime,
            tenantId,
            cancellationToken);
    }
}
