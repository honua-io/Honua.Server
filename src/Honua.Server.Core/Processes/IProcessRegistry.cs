// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Defines the interface for a process registry.
/// </summary>
public interface IProcessRegistry
{
    /// <summary>
    /// Gets all registered processes.
    /// </summary>
    /// <returns>Collection of process descriptions.</returns>
    IReadOnlyList<ProcessDescription> GetAllProcesses();

    /// <summary>
    /// Gets a process by its identifier.
    /// </summary>
    /// <param name="processId">The process identifier.</param>
    /// <returns>The process, or null if not found.</returns>
    IProcess? GetProcess(string processId);

    /// <summary>
    /// Registers a process.
    /// </summary>
    /// <param name="process">The process to register.</param>
    void RegisterProcess(IProcess process);
}
