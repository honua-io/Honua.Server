// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Processes;

/// <summary>
/// Represents the status of a process execution job.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job is accepted and waiting to start.
    /// </summary>
    Accepted,

    /// <summary>
    /// Job is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Successful,

    /// <summary>
    /// Job failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Job was dismissed/cancelled.
    /// </summary>
    Dismissed
}
