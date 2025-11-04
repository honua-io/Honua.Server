// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.Services.Consultant.Workflows;

/// <summary>
/// Shared context passed through workflow stages.
/// </summary>
public sealed class WorkflowContext
{
    public required ConsultantRequest Request { get; init; }
    public ConsultantPlanningContext? PlanningContext { get; set; }
    public ConsultantPlan? Plan { get; set; }
    public ExecutionResult? ExecutionResult { get; set; }
    public string? SessionId { get; set; }
    public bool IsMultiAgentMode { get; set; }
    public Honua.Cli.AI.Services.Agents.AgentCoordinatorResult? MultiAgentResult { get; set; }
}
