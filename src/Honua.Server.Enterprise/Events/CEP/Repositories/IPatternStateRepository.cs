// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Events.CEP.Models;
using Honua.Server.Enterprise.Events.CEP.Services;

namespace Honua.Server.Enterprise.Events.CEP.Repositories;

/// <summary>
/// Repository for pattern matching state management
/// </summary>
public interface IPatternStateRepository
{
    // Pattern Match State operations
    Task<PatternMatchState> GetOrCreateStateAsync(
        Guid patternId,
        string partitionKey,
        DateTime eventTime,
        TimeSpan windowDuration,
        WindowType windowType,
        string? tenantId,
        CancellationToken cancellationToken = default);

    Task UpdateStateAsync(
        PatternMatchState state,
        CancellationToken cancellationToken = default);

    Task DeleteStateAsync(
        Guid stateId,
        CancellationToken cancellationToken = default);

    Task<List<PatternMatchState>> GetStatesForPatternAsync(
        Guid patternId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    // Tumbling Window operations
    Task<TumblingWindowState> GetOrCreateTumblingWindowAsync(
        Guid patternId,
        string partitionKey,
        DateTime windowStart,
        DateTime windowEnd,
        string? tenantId,
        CancellationToken cancellationToken = default);

    Task UpdateTumblingWindowAsync(
        TumblingWindowState windowState,
        CancellationToken cancellationToken = default);

    // Pattern Match History operations
    Task CreateMatchHistoryAsync(
        PatternMatchHistory matchHistory,
        CancellationToken cancellationToken = default);

    Task<List<PatternMatchHistory>> GetMatchHistoryAsync(
        Guid? patternId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    // Cleanup operations
    Task<CleanupResult> CleanupExpiredStatesAsync(
        int retentionHours,
        CancellationToken cancellationToken = default);

    // Monitoring operations
    Task<List<ActivePatternState>> GetActiveStatesAsync(
        Guid? patternId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
