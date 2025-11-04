using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.Services.Consultant;

namespace Honua.Cli.Tests.Consultant;

internal sealed class AlwaysFailsAgentCoordinator : IAgentCoordinator
{
    private readonly List<string> _requests = new();
    public IReadOnlyList<string> Requests => _requests;
    public AgentExecutionContext? LastContext { get; private set; }

    public Task<AgentCoordinatorResult> ProcessRequestAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken = default)
    {
        _requests.Add(request);
        LastContext = context;

        return Task.FromResult(new AgentCoordinatorResult
        {
            Success = false,
            ErrorMessage = "Coordinator unavailable",
            Response = "Coordinator unavailable",
            Steps = new List<AgentStepResult>()
        });
    }

    public Task<AgentInteractionHistory> GetHistoryAsync()
    {
        return Task.FromResult(new AgentInteractionHistory
        {
            SessionId = Guid.NewGuid().ToString(),
            Interactions = new List<AgentInteraction>()
        });
    }
}

internal sealed class AlwaysSucceedsAgentCoordinator : IAgentCoordinator
{
    private readonly List<string> _requests = new();
    private readonly List<AgentInteraction> _interactions = new();

    public IReadOnlyList<string> Requests => _requests;
    public AgentExecutionContext? LastContext { get; private set; }

    public Task<AgentCoordinatorResult> ProcessRequestAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken = default)
    {
        _requests.Add(request);
        LastContext = context;

        var step = new AgentStepResult
        {
            AgentName = "automation",
            Action = "Deploy",
            Success = true,
            Message = "Automation complete",
            Duration = TimeSpan.FromSeconds(1)
        };

        _interactions.Add(new AgentInteraction
        {
            Timestamp = DateTime.UtcNow,
            UserRequest = request,
            AgentsUsed = new List<string> { "automation" },
            Success = true,
            Response = "Automation completed"
        });

        return Task.FromResult(new AgentCoordinatorResult
        {
            Success = true,
            Response = "Automation completed",
            Steps = new List<AgentStepResult> { step },
            AgentsInvolved = new List<string> { "automation" },
            NextSteps = new List<string> { "Review automation logs" }
        });
    }

    public Task<AgentInteractionHistory> GetHistoryAsync()
    {
        return Task.FromResult(new AgentInteractionHistory
        {
            SessionId = Guid.NewGuid().ToString(),
            Interactions = new List<AgentInteraction>(_interactions)
        });
    }
}
