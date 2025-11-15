// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Events.CEP.Models;

namespace Honua.Server.Enterprise.Events.CEP.Repositories;

/// <summary>
/// Repository for pattern definitions
/// </summary>
public interface IPatternRepository
{
    /// <summary>
    /// Get all enabled patterns
    /// </summary>
    Task<List<GeofenceEventPattern>> GetEnabledPatternsAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pattern by ID
    /// </summary>
    Task<GeofenceEventPattern?> GetByIdAsync(
        Guid patternId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all patterns
    /// </summary>
    Task<List<GeofenceEventPattern>> GetAllAsync(
        string? tenantId = null,
        bool? enabledOnly = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new pattern
    /// </summary>
    Task<Guid> CreateAsync(
        GeofenceEventPattern pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing pattern
    /// </summary>
    Task<bool> UpdateAsync(
        GeofenceEventPattern pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a pattern
    /// </summary>
    Task<bool> DeleteAsync(
        Guid patternId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pattern statistics
    /// </summary>
    Task<List<PatternMatchStatistics>> GetPatternStatisticsAsync(
        Guid? patternId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
