// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Defines the interface for an OGC API - Processes process implementation.
/// </summary>
public interface IProcess
{
    /// <summary>
    /// Gets the process description.
    /// </summary>
    ProcessDescription Description { get; }

    /// <summary>
    /// Executes the process with the given inputs.
    /// </summary>
    /// <param name="inputs">The input parameters.</param>
    /// <param name="job">The job context for progress reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process outputs.</returns>
    Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object>? inputs,
        ProcessJob job,
        CancellationToken cancellationToken = default);
}
