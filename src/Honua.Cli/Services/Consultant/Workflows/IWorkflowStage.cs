// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.Consultant.Workflows;

/// <summary>
/// Represents a stage in the consultant workflow pipeline.
/// </summary>
public interface IWorkflowStage<TInput, TOutput>
{
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken);
}
