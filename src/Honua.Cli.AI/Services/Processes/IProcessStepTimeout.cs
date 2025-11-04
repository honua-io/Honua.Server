// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Interface for process steps that support configurable timeout behavior.
/// Steps implementing this interface can specify custom timeout values,
/// preventing long-running operations from hanging indefinitely.
/// </summary>
public interface IProcessStepTimeout
{
    /// <summary>
    /// Gets the default timeout for this process step.
    /// If the step does not complete within this duration, it will be cancelled.
    /// </summary>
    TimeSpan DefaultTimeout { get; }
}
