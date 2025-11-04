// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Agents;

public interface IAgentCritic
{
    Task<IReadOnlyList<string>> EvaluateAsync(ConsultantRequestSnapshot request, AgentCoordinatorResult result, CancellationToken cancellationToken);
}

public sealed record ConsultantRequestSnapshot(string Prompt, bool DryRun, string Mode);

public sealed class PlanSafetyCritic : IAgentCritic
{
    public Task<IReadOnlyList<string>> EvaluateAsync(ConsultantRequestSnapshot request, AgentCoordinatorResult result, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        if (result.Steps.Count == 0)
        {
            warnings.Add("No agent steps were recorded; verify automation ran as expected.");
        }

        if (result.Steps.Exists(step => !step.Success))
        {
            warnings.Add("One or more agent steps reported failure.");
        }

        if (result.Success && result.NextSteps.Count == 0 && !request.DryRun)
        {
            warnings.Add("Multi-agent execution completed without next steps; confirm follow-up tasks.");
        }

        return Task.FromResult<IReadOnlyList<string>>(warnings);
    }
}
