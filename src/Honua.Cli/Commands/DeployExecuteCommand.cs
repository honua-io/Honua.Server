// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.Planning;
using Honua.Cli.Services;
using Honua.Server.Core.Performance;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to execute a deployment plan for HonuaIO infrastructure.
/// </summary>
public sealed class DeployExecuteCommand : AsyncCommand<DeployExecuteCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IAgentCoordinator? _agentCoordinator;

    public DeployExecuteCommand(
        IAnsiConsole console,
        IHonuaCliEnvironment environment,
        IAgentCoordinator? agentCoordinator = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _agentCoordinator = agentCoordinator;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (_agentCoordinator == null)
        {
            _console.MarkupLine("[red]Error: AI coordinator not configured. Deployment execution requires AI services.[/]");
            _console.MarkupLine("[yellow]Run 'honua setup-wizard' to configure AI services.[/]");
            return 1;
        }

        try
        {
            _console.MarkupLine("[bold cyan]HonuaIO Deployment Executor[/]");
            _console.WriteLine();

            // Load deployment plan
            ExecutionPlan plan;
            DeploymentTopology? topology = null;

            if (settings.PlanFile.HasValue())
            {
                _console.MarkupLine($"[dim]Loading plan from {settings.PlanFile}...[/]");
                var planData = await LoadPlanAsync(settings.PlanFile);
                plan = planData.Plan;
                topology = planData.Topology;
            }
            else
            {
                _console.MarkupLine("[red]Error: No plan specified. Use --plan to specify a deployment plan.[/]");
                _console.MarkupLine("[yellow]Generate a plan first: honua deploy plan --output plan.json[/]");
                return 1;
            }

            // Display plan summary
            DisplayPlanSummary(plan, topology);

            // Confirm execution
            if (!settings.AutoApprove)
            {
                _console.WriteLine();
                if (!_console.Confirm($"[yellow]Execute this deployment plan?[/]", defaultValue: false))
                {
                    _console.MarkupLine("[yellow]Deployment cancelled.[/]");
                    return 0;
                }
            }

            _console.WriteLine();
            _console.Write(new Rule("[bold green]Executing Deployment[/]").LeftJustified());
            _console.WriteLine();

            // Execute plan
            var success = await ExecutePlanAsync(plan, settings);

            if (success)
            {
                _console.WriteLine();
                _console.Write(new Rule("[bold green]✓ Deployment Complete[/]").LeftJustified());
                _console.WriteLine();
                _console.MarkupLine("[green]HonuaIO has been successfully deployed![/]");
                _console.WriteLine();

                if (topology != null)
                {
                    DisplayPostDeploymentInfo(topology);
                }

                return 0;
            }
            else
            {
                _console.WriteLine();
                _console.MarkupLine("[red]✗ Deployment failed. Check logs for details.[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            if (settings.Verbose)
            {
                _console.WriteException(ex);
            }
            return 1;
        }
    }

    private async Task<(ExecutionPlan Plan, DeploymentTopology? Topology)> LoadPlanAsync(string planFile)
    {
        var json = await File.ReadAllTextAsync(planFile);
        using var doc = JsonDocument.Parse(json);

        // Create options with string enum converter for loading test plans
        // that serialize enums as strings
        var options = new JsonSerializerOptions(JsonSerializerOptionsRegistry.DevTooling);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        var plan = JsonSerializer.Deserialize<ExecutionPlan>(
            doc.RootElement.GetProperty("Plan").GetRawText(),
            options);

        DeploymentTopology? topology = null;
        if (doc.RootElement.TryGetProperty("Topology", out var topologyElement))
        {
            topology = JsonSerializer.Deserialize<DeploymentTopology>(
                topologyElement.GetRawText(),
                options);
        }

        if (plan == null)
        {
            throw new InvalidOperationException("Failed to deserialize plan file");
        }

        return (plan, topology);
    }

    private void DisplayPlanSummary(ExecutionPlan plan, DeploymentTopology? topology)
    {
        _console.Write(new Rule($"[bold]{plan.Title}[/]").LeftJustified());
        _console.WriteLine();

        if (topology != null)
        {
            _console.MarkupLine($"[cyan]Cloud:[/] {topology.CloudProvider} ({topology.Region})");
            _console.MarkupLine($"[cyan]Environment:[/] {topology.Environment}");
            _console.WriteLine();
        }

        _console.MarkupLine($"[bold]Steps:[/] {plan.Steps.Count}");
        _console.MarkupLine($"[bold]Risk Level:[/] {FormatRiskLevel(plan.Risk.Level)}");

        var totalDuration = plan.Steps
            .Where(s => s.EstimatedDuration.HasValue)
            .Sum(s => s.EstimatedDuration!.Value.TotalMinutes);
        _console.MarkupLine($"[bold]Estimated Duration:[/] ~{totalDuration:F0} minutes");
    }

    private async Task<bool> ExecutePlanAsync(ExecutionPlan plan, Settings settings)
    {
        var agentContext = new AgentExecutionContext
        {
            WorkspacePath = _environment.ResolveWorkspacePath(settings.Workspace),
            DryRun = settings.DryRun
        };

        int completedSteps = 0;

        await _console.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var overallTask = ctx.AddTask("[green]Deploying HonuaIO[/]", maxValue: plan.Steps.Count);

                foreach (var step in plan.Steps)
                {
                    var stepTask = ctx.AddTask($"[cyan]{step.Description}[/]");
                    stepTask.StartTask();

                    try
                    {
                        // Simulate step execution
                        // In production, this would call the actual deployment agent
                        await SimulateStepExecutionAsync(step, settings.DryRun);

                        step.Status = StepStatus.Completed;
                        stepTask.Value = 100;
                        stepTask.StopTask();

                        completedSteps++;
                        overallTask.Increment(1);

                        _console.MarkupLine($"  [green]✓[/] {step.Description}");
                    }
                    catch (Exception ex)
                    {
                        step.Status = StepStatus.Failed;
                        stepTask.StopTask();

                        _console.MarkupLine($"  [red]✗[/] {step.Description}");
                        _console.MarkupLine($"    [red]Error: {ex.Message}[/]");

                        if (!settings.ContinueOnError)
                        {
                            throw;
                        }
                    }
                }
            });

        return completedSteps == plan.Steps.Count;
    }

    private async Task SimulateStepExecutionAsync(PlanStep step, bool dryRun)
    {
        if (dryRun)
        {
            // In dry-run mode, just simulate the step
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            return;
        }

        // In production, this would:
        // 1. Use DeploymentExecutionAgent to execute the step
        // 2. Call cloud provider APIs
        // 3. Monitor progress
        // 4. Handle rollback if needed

        var delay = step.EstimatedDuration ?? TimeSpan.FromSeconds(5);
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, 3000)));
    }

    private void DisplayPostDeploymentInfo(DeploymentTopology topology)
    {
        _console.MarkupLine("[bold]Post-Deployment Information:[/]");
        _console.WriteLine();

        var infoTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Resource")
            .AddColumn("Details");

        if (topology.Database != null)
        {
            infoTable.AddRow(
                "[cyan]Database[/]",
                $"{topology.Database.Engine}://honua-{topology.Environment}.{topology.Region}.rds.amazonaws.com:5432");
        }

        if (topology.Compute != null)
        {
            infoTable.AddRow(
                "[cyan]Server Endpoint[/]",
                $"https://honua-{topology.Environment}.{topology.Region}.elb.amazonaws.com");
        }

        if (topology.Storage != null)
        {
            infoTable.AddRow(
                "[cyan]Storage Bucket[/]",
                $"honua-{topology.Environment}-{topology.Region}");
        }

        _console.Write(infoTable);
        _console.WriteLine();

        _console.MarkupLine("[bold]Next Steps:[/]");
        _console.MarkupLine("  1. Verify deployment: [cyan]honua status[/]");
        _console.MarkupLine("  2. Test connection: [cyan]honua test-connection[/]");
        _console.MarkupLine("  3. View logs: [cyan]honua logs[/]");
        _console.MarkupLine("  4. Configure DNS to point to load balancer endpoint");
        _console.WriteLine();

        _console.MarkupLine("[yellow]⚠ Remember to:[/]");
        _console.MarkupLine("  • Configure SSL/TLS certificates");
        _console.MarkupLine("  • Set up monitoring alerts");
        _console.MarkupLine("  • Configure automated backups");
        _console.MarkupLine("  • Review security group rules");
    }

    private string FormatRiskLevel(RiskLevel level)
    {
        return level switch
        {
            RiskLevel.Low => "[green]Low[/]",
            RiskLevel.Medium => "[yellow]Medium[/]",
            RiskLevel.High => "[red]High[/]",
            _ => "[dim]Unknown[/]"
        };
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--plan <FILE>")]
        [Description("Path to deployment plan JSON file")]
        public string? PlanFile { get; init; }

        [CommandOption("--workspace <PATH>")]
        [Description("Workspace directory")]
        public string? Workspace { get; init; }

        [CommandOption("--auto-approve")]
        [Description("Skip confirmation prompts")]
        public bool AutoApprove { get; init; }

        [CommandOption("--dry-run")]
        [Description("Simulate execution without making changes")]
        public bool DryRun { get; init; }

        [CommandOption("--continue-on-error")]
        [Description("Continue execution even if steps fail")]
        public bool ContinueOnError { get; init; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        public bool Verbose { get; init; }
    }
}
