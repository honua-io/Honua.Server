// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Executes approved consultant plans by invoking plugin functions.
/// </summary>
public interface IConsultantExecutor
{
    /// <summary>
    /// Executes a validated and approved plan.
    /// </summary>
    /// <param name="plan">The plan to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result with status and details.</returns>
    Task<ExecutionResult> ExecuteAsync(ConsultantPlan plan, CancellationToken cancellationToken);
}
