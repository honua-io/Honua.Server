// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Stores consultant sessions for conversational refinement.
/// Enables users to refine plans without starting from scratch.
/// </summary>
public interface IConsultantSessionStore
{
    /// <summary>
    /// Saves a consultant session for later refinement.
    /// </summary>
    Task SaveSessionAsync(
        string sessionId,
        ConsultantPlan plan,
        ConsultantPlanningContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a saved session for refinement.
    /// </summary>
    Task<(ConsultantPlan Plan, ConsultantPlanningContext Context)?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets list of recent session IDs.
    /// </summary>
    Task<List<string>> GetRecentSessionsAsync(
        int count = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a session from storage.
    /// </summary>
    Task DeleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
