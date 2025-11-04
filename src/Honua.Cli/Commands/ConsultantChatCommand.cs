// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Visualization;
using Honua.Cli.AI.Services.Cost;
using Honua.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// Interactive chat command for the AI consultant.
/// Provides a conversational interface for architecture design, deployment, and troubleshooting.
/// </summary>
public sealed class ConsultantChatCommand : AsyncCommand<ConsultantChatCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IAgentCoordinator? _agentCoordinator;
    private readonly ArchitectureDiagramGenerator _diagramGenerator;
    private CostTrackingService? _costTrackingService;

    public ConsultantChatCommand(
        IAnsiConsole console,
        IHonuaCliEnvironment environment,
        IAgentCoordinator? agentCoordinator = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _agentCoordinator = agentCoordinator;
        _diagramGenerator = new ArchitectureDiagramGenerator();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (_agentCoordinator == null)
        {
            _console.MarkupLine("[red]Error: AI consultant not configured. Multi-agent mode is required for chat.[/]");
            return 1;
        }

        var workspace = _environment.ResolveWorkspacePath(settings.Workspace);
        _costTrackingService = new CostTrackingService(workspace);

        var chatContext = new ChatContext
        {
            WorkspacePath = workspace,
            ConversationHistory = new List<ConversationTurn>(),
            DryRun = settings.DryRun,
            RequireApproval = !settings.AutoApprove,
            Verbose = settings.Verbose
        };

        // Print welcome message
        RenderWelcomeBanner();

        // Check if there's an initial prompt
        if (settings.Prompt.HasValue())
        {
            await ProcessUserInputAsync(settings.Prompt, chatContext, CancellationToken.None);
        }

        // Main chat loop
        await RunChatLoopAsync(chatContext, CancellationToken.None);

        return 0;
    }

    private void RenderWelcomeBanner()
    {
        _console.Clear();
        _console.Write(
            new FigletText("Honua AI")
                .LeftJustified()
                .Color(Color.Blue));

        _console.WriteLine();
        _console.MarkupLine("[bold]Cloud GIS Consultant[/] [dim](powered by AI)[/]");
        _console.WriteLine();
        _console.MarkupLine("[dim]I can help you with:[/]");
        _console.MarkupLine("  • [cyan]Architecture design[/] and cost analysis");
        _console.MarkupLine("  • [green]Deployment[/] to AWS, Azure, GCP, Kubernetes, or Docker");
        _console.MarkupLine("  • [yellow]Performance optimization[/] and troubleshooting");
        _console.MarkupLine("  • [magenta]Security hardening[/] and best practices");
        _console.MarkupLine("  • [blue]Data migration[/] and infrastructure upgrades");
        _console.WriteLine();
        _console.MarkupLine("[dim]Special commands: /help /cost /deploy /diagram /cost-report /explain /history /clear /exit[/]");
        _console.WriteLine();
        _console.Write(new Rule("[dim]Let's get started[/]").LeftJustified());
        _console.WriteLine();
    }

    private async Task RunChatLoopAsync(ChatContext chatContext, CancellationToken cancellationToken)
    {
        while (true)
        {
            _console.WriteLine();
            _console.Markup("[bold cyan]You:[/] ");
            var input = _console.Ask<string>("");

            if (input.IsNullOrWhiteSpace())
            {
                continue;
            }

            // Handle special commands
            if (input.StartsWith("/"))
            {
                var shouldExit = await HandleSpecialCommandAsync(input, chatContext, cancellationToken);
                if (shouldExit)
                {
                    break;
                }
                continue;
            }

            // Process user input
            await ProcessUserInputAsync(input, chatContext, cancellationToken);
        }

        _console.WriteLine();
        _console.MarkupLine("[dim]Thanks for using Honua AI Consultant! 👋[/]");
    }

    private async Task ProcessUserInputAsync(
        string input,
        ChatContext chatContext,
        CancellationToken cancellationToken)
    {
        // Add to conversation history
        chatContext.ConversationHistory.Add(new ConversationTurn
        {
            Role = "user",
            Content = input,
            Timestamp = DateTime.UtcNow
        });

        _console.WriteLine();

        // Build context from conversation history
        var contextBuilder = new StringBuilder();
        if (chatContext.ConversationHistory.Count > 1)
        {
            contextBuilder.AppendLine("Previous conversation:");
            foreach (var turn in chatContext.ConversationHistory.TakeLast(5).SkipLast(1))
            {
                contextBuilder.AppendLine($"{turn.Role}: {turn.Content.Substring(0, Math.Min(100, turn.Content.Length))}...");
            }
            contextBuilder.AppendLine();
        }

        contextBuilder.AppendLine($"Current request: {input}");

        // Create agent execution context
        var agentContext = new AgentExecutionContext
        {
            WorkspacePath = chatContext.WorkspacePath,
            DryRun = chatContext.DryRun,
            RequireApproval = chatContext.RequireApproval,
            ConversationHistory = chatContext.ConversationHistory.Select(t => $"{t.Role}: {t.Content}").ToList()
        };

        // Show thinking indicator
        await _console.Status()
            .StartAsync("🤔 Thinking...", async ctx =>
            {
                try
                {
                    // Call the agent coordinator
                    var result = await _agentCoordinator!.ProcessRequestAsync(
                        contextBuilder.ToString(),
                        agentContext,
                        cancellationToken);

                    ctx.Status("✓ Done");

                    _console.WriteLine();
                    _console.MarkupLine("[bold green]Consultant:[/]");
                    _console.WriteLine();

                    // Render the response
                    if (result.Success)
                    {
                        RenderAgentResponse(result);

                        // Add to conversation history
                        chatContext.ConversationHistory.Add(new ConversationTurn
                        {
                            Role = "assistant",
                            Content = result.Response,
                            Timestamp = DateTime.UtcNow
                        });

                        // Store the last result for special commands
                        chatContext.LastResult = result;
                    }
                    else
                    {
                        _console.MarkupLine($"[red]I encountered an error: {result.ErrorMessage}[/]");
                        _console.WriteLine();
                        _console.MarkupLine("[yellow]Could you rephrase your request or try something else?[/]");
                    }
                }
                catch (Exception ex)
                {
                    ctx.Status("✗ Error");
                    _console.WriteLine();
                    _console.MarkupLine($"[red]Error: {ex.Message}[/]");
                }
            });
    }

    private void RenderAgentResponse(AgentCoordinatorResult result)
    {
        // Render the main message with markdown-style formatting
        RenderMarkdown(result.Response);

        _console.WriteLine();

        // Auto-show architecture diagram if this was an architecture consulting response
        var isArchitectureResponse = result.Steps.Any(a =>
            a.AgentName.Contains("Architecture", StringComparison.OrdinalIgnoreCase) ||
            result.Response.Contains("architecture", StringComparison.OrdinalIgnoreCase) &&
            (result.Response.Contains("serverless") || result.Response.Contains("kubernetes") ||
             result.Response.Contains("docker") || result.Response.Contains("k8s")));

        if (isArchitectureResponse)
        {
            // Infer architecture type from message
            var message = result.Response.ToLowerInvariant();
            string architectureType = "serverless";

            if (message.Contains("kubernetes") || message.Contains("k8s"))
                architectureType = "kubernetes";
            else if (message.Contains("docker") && !message.Contains("kubernetes"))
                architectureType = "docker";
            else if (message.Contains("hybrid"))
                architectureType = "hybrid";

            try
            {
                var spec = new ArchitectureSpec
                {
                    Type = architectureType,
                    CloudProvider = "gcp",
                    Components = new List<string> { "api", "database", "storage", "cache", "cdn" }
                };

                var diagram = _diagramGenerator.GenerateDiagram(spec);

                _console.WriteLine();
                _console.Write(new Rule($"[dim]Architecture Visualization[/]").LeftJustified());
                _console.WriteLine();
                _console.WriteLine(diagram);
                _console.WriteLine();
                _console.MarkupLine("[dim]💡 Use /diagram [type] to see other architecture diagrams[/]");
            }
            catch
            {
                // Silently skip if diagram generation fails
            }
        }

        // Show agent execution summary if verbose or if there were multiple agents
        if (result.Steps.Count > 1)
        {
            _console.WriteLine();
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn("[dim]Agent[/]")
                .AddColumn("[dim]Action[/]")
                .AddColumn("[dim]Status[/]")
                .AddColumn("[dim]Duration[/]");

            foreach (var step in result.Steps)
            {
                var statusMarkup = step.Success ? "[green]✓[/]" : "[red]✗[/]";

                table.AddRow(
                    step.AgentName,
                    step.Action,
                    statusMarkup,
                    "N/A");
            }

            _console.Write(table);
        }

        // Show suggested next steps if available
        if (result.Steps.Any(a => a.Message.Contains("Next Steps") || a.Message.Contains("Would you like")))
        {
            _console.WriteLine();
            _console.MarkupLine("[dim]💡 Tip: You can ask me to elaborate, compare options, or proceed with deployment.[/]");
        }
    }

    private void RenderMarkdown(string content)
    {
        // Simple markdown rendering for console
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("### "))
            {
                _console.MarkupLine($"[bold yellow]{line.Substring(4)}[/]");
            }
            else if (line.StartsWith("## "))
            {
                _console.MarkupLine($"[bold cyan]{line.Substring(3)}[/]");
            }
            else if (line.StartsWith("# "))
            {
                _console.MarkupLine($"[bold blue]{line.Substring(2)}[/]");
            }
            else if (line.StartsWith("**") && line.EndsWith("**"))
            {
                _console.MarkupLine($"[bold]{line.Trim('*')}[/]");
            }
            else if (line.StartsWith("- ✅"))
            {
                _console.MarkupLine($"  [green]✓[/] {line.Substring(4)}");
            }
            else if (line.StartsWith("- ⚠️"))
            {
                _console.MarkupLine($"  [yellow]⚠[/] {line.Substring(4)}");
            }
            else if (line.StartsWith("- ❌"))
            {
                _console.MarkupLine($"  [red]✗[/] {line.Substring(4)}");
            }
            else if (line.StartsWith("- "))
            {
                _console.MarkupLine($"  • {line.Substring(2)}");
            }
            else if (line.Contains("$") && line.Contains("/month"))
            {
                // Highlight costs
                var highlighted = line.Replace("$", "[green]$").Replace("/month", "/month[/]");
                _console.MarkupLine(highlighted);
            }
            else if (line.HasValue())
            {
                _console.WriteLine(line);
            }
            else
            {
                _console.WriteLine();
            }
        }
    }

    private async Task<bool> HandleSpecialCommandAsync(
        string command,
        ChatContext chatContext,
        CancellationToken cancellationToken)
    {
        var parts = command.ToLowerInvariant().Split(' ', 2);
        var cmd = parts[0];
        var args = parts.Length > 1 ? parts[1] : string.Empty;

        switch (cmd)
        {
            case "/help":
                ShowHelp();
                return false;

            case "/cost":
                await ShowCostBreakdownAsync(chatContext, cancellationToken);
                return false;

            case "/deploy":
                await InitiateDeploymentAsync(args, chatContext, cancellationToken);
                return false;

            case "/explain":
                await ExplainLastResponseAsync(chatContext, cancellationToken);
                return false;

            case "/history":
                ShowConversationHistory(chatContext);
                return false;

            case "/clear":
                _console.Clear();
                RenderWelcomeBanner();
                return false;

            case "/exit":
            case "/quit":
                return true;

            case "/refine":
                _console.MarkupLine("[yellow]Please specify what you'd like to refine (e.g., 'refine for lower cost')[/]");
                return false;

            case "/options":
                await ShowAlternativeOptionsAsync(chatContext, cancellationToken);
                return false;

            case "/diagram":
                await ShowArchitectureDiagramAsync(args, chatContext, cancellationToken);
                return false;

            case "/cost-report":
                await ShowCostTrackingReportAsync(cancellationToken);
                return false;

            case "/track-cost":
                await TrackActualCostAsync(args, cancellationToken);
                return false;

            default:
                _console.MarkupLine($"[red]Unknown command: {cmd}[/]");
                _console.MarkupLine("[dim]Type /help to see available commands[/]");
                return false;
        }
    }

    private void ShowHelp()
    {
        _console.WriteLine();
        _console.Write(new Rule("[yellow]Available Commands[/]").LeftJustified());
        _console.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Command[/]")
            .AddColumn("[bold]Description[/]");

        table.AddRow("/help", "Show this help message");
        table.AddRow("/cost", "Show detailed cost breakdown for last recommendation");
        table.AddRow("/deploy", "Deploy the recommended architecture");
        table.AddRow("/diagram [type]", "Show ASCII architecture diagram (serverless/k8s/docker/hybrid)");
        table.AddRow("/cost-report", "Show cost tracking report comparing estimated vs actual costs");
        table.AddRow("/track-cost <id> <amt>", "Record actual cost for a deployment");
        table.AddRow("/explain", "Get more details about the last response");
        table.AddRow("/options", "Show alternative architecture options");
        table.AddRow("/history", "Show conversation history");
        table.AddRow("/clear", "Clear the screen");
        table.AddRow("/exit", "Exit the chat");

        _console.Write(table);
        _console.WriteLine();
        _console.MarkupLine("[dim]You can also just chat naturally - I'll understand your intent![/]");
    }

    private async Task ShowCostBreakdownAsync(ChatContext chatContext, CancellationToken cancellationToken)
    {
        if (chatContext.LastResult == null)
        {
            _console.MarkupLine("[yellow]No previous recommendation to show costs for.[/]");
            return;
        }

        _console.WriteLine();
        _console.MarkupLine("[bold cyan]Detailed Cost Breakdown:[/]");
        _console.WriteLine();

        // Re-query the architecture consulting agent for detailed costs
        await ProcessUserInputAsync(
            "Show me a detailed cost breakdown for the recommended option, including compute, storage, database, CDN, and data transfer costs",
            chatContext,
            cancellationToken);
    }

    private async Task InitiateDeploymentAsync(
        string args,
        ChatContext chatContext,
        CancellationToken cancellationToken)
    {
        _console.WriteLine();

        if (chatContext.LastResult == null || !chatContext.LastResult.Success)
        {
            _console.MarkupLine("[yellow]Nothing to deploy yet. Please ask me to design an architecture first.[/]");
            return;
        }

        // Confirm deployment
        if (chatContext.RequireApproval)
        {
            _console.MarkupLine("[yellow]⚠️  You're about to deploy infrastructure that will incur costs.[/]");
            _console.WriteLine();

            if (!_console.Confirm("Proceed with deployment?", false))
            {
                _console.MarkupLine("[dim]Deployment cancelled.[/]");
                return;
            }
        }

        _console.WriteLine();
        await ProcessUserInputAsync(
            "Deploy the recommended architecture now",
            chatContext,
            cancellationToken);
    }

    private async Task ExplainLastResponseAsync(ChatContext chatContext, CancellationToken cancellationToken)
    {
        if (chatContext.ConversationHistory.Count < 2)
        {
            _console.MarkupLine("[yellow]Nothing to explain yet.[/]");
            return;
        }

        var lastAssistantMessage = chatContext.ConversationHistory
            .Where(t => t.Role == "assistant")
            .LastOrDefault();

        if (lastAssistantMessage == null)
        {
            _console.MarkupLine("[yellow]No previous response to explain.[/]");
            return;
        }

        _console.WriteLine();
        await ProcessUserInputAsync(
            "Can you explain your last response in more detail? Why did you recommend that option?",
            chatContext,
            cancellationToken);
    }

    private async Task ShowAlternativeOptionsAsync(ChatContext chatContext, CancellationToken cancellationToken)
    {
        _console.WriteLine();
        await ProcessUserInputAsync(
            "Show me all the alternative architecture options with their trade-offs",
            chatContext,
            cancellationToken);
    }

    private void ShowConversationHistory(ChatContext chatContext)
    {
        _console.WriteLine();
        _console.Write(new Rule("[cyan]Conversation History[/]").LeftJustified());
        _console.WriteLine();

        if (chatContext.ConversationHistory.Count == 0)
        {
            _console.MarkupLine("[dim]No conversation history yet.[/]");
            return;
        }

        foreach (var turn in chatContext.ConversationHistory)
        {
            var roleColor = turn.Role == "user" ? "cyan" : "green";
            var roleLabel = turn.Role == "user" ? "You" : "Consultant";

            _console.MarkupLine($"[dim]{turn.Timestamp:HH:mm:ss}[/] [bold {roleColor}]{roleLabel}:[/]");

            var preview = turn.Content.Length > 200
                ? turn.Content.Substring(0, 200) + "..."
                : turn.Content;

            _console.MarkupLine($"[dim]{preview}[/]");
            _console.WriteLine();
        }
    }

    private Task ShowArchitectureDiagramAsync(
        string args,
        ChatContext chatContext,
        CancellationToken cancellationToken)
    {
        _console.WriteLine();

        // Determine architecture type from args or from last result
        var architectureType = "serverless"; // default
        if (args.HasValue())
        {
            architectureType = args.ToLowerInvariant().Trim();
        }
        else if (chatContext.LastResult != null)
        {
            // Try to infer from last response
            var lastMessage = chatContext.LastResult.Response.ToLowerInvariant();
            if (lastMessage.Contains("kubernetes") || lastMessage.Contains("k8s"))
                architectureType = "kubernetes";
            else if (lastMessage.Contains("docker"))
                architectureType = "docker";
            else if (lastMessage.Contains("hybrid"))
                architectureType = "hybrid";
        }

        var spec = new ArchitectureSpec
        {
            Type = architectureType,
            CloudProvider = "gcp", // Default, could be inferred from context
            Components = new List<string> { "api", "database", "storage", "cache", "cdn" }
        };

        try
        {
            var diagram = _diagramGenerator.GenerateDiagram(spec);

            _console.Write(new Rule($"[cyan]Architecture Diagram - {architectureType.ToUpper()}[/]").LeftJustified());
            _console.WriteLine();
            _console.WriteLine(diagram);
            _console.WriteLine();
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error generating diagram: {ex.Message}[/]");
        }

        return Task.CompletedTask;
    }

    private async Task ShowCostTrackingReportAsync(CancellationToken cancellationToken)
    {
        _console.WriteLine();

        if (_costTrackingService == null)
        {
            _console.MarkupLine("[red]Cost tracking service not initialized.[/]");
            return;
        }

        try
        {
            var report = await _costTrackingService.GenerateCostReportAsync(cancellationToken);
            _console.WriteLine(report);
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error generating cost report: {ex.Message}[/]");
        }
    }

    private async Task TrackActualCostAsync(string args, CancellationToken cancellationToken)
    {
        _console.WriteLine();

        if (_costTrackingService == null)
        {
            _console.MarkupLine("[red]Cost tracking service not initialized.[/]");
            return;
        }

        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            _console.MarkupLine("[yellow]Usage: /track-cost <deployment-id> <amount>[/]");
            _console.MarkupLine("[dim]Example: /track-cost my-deployment-123 450.75[/]");
            return;
        }

        var deploymentId = parts[0];
        if (!decimal.TryParse(parts[1], out var amount))
        {
            _console.MarkupLine("[red]Invalid amount. Please provide a numeric value.[/]");
            return;
        }

        try
        {
            await _costTrackingService.RecordActualCostAsync(
                deploymentId,
                amount,
                DateTime.UtcNow,
                cancellationToken);

            _console.MarkupLine($"[green]✓[/] Recorded ${amount:N2} for deployment {deploymentId}");

            // Show comparison
            var comparison = await _costTrackingService.GetCostComparisonAsync(deploymentId, cancellationToken);
            if (comparison.HasData && comparison.AverageActualCost > 0)
            {
                _console.WriteLine();
                _console.MarkupLine($"[dim]Average actual cost: ${comparison.AverageActualCost:N2}/month[/]");
                _console.MarkupLine($"[dim]Estimated cost: ${comparison.EstimatedMonthlyCost:N2}/month[/]");
                _console.MarkupLine($"[dim]Variance: {comparison.VariancePercent:+0.0;-0.0}% {comparison.Status}[/]");
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error tracking cost: {ex.Message}[/]");
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--prompt <PROMPT>")]
        [Description("Initial prompt to start the conversation")]
        public string? Prompt { get; init; }

        [CommandOption("--workspace <PATH>")]
        [Description("Path to the Honua workspace; defaults to the current directory")]
        public string? Workspace { get; init; }

        [CommandOption("--dry-run")]
        [Description("Preview actions without executing deployments")]
        public bool DryRun { get; init; }

        [CommandOption("--auto-approve")]
        [Description("Automatically approve deployments without confirmation")]
        public bool AutoApprove { get; init; }

        [CommandOption("--verbose")]
        [Description("Show additional details about agent execution")]
        public bool Verbose { get; init; }
    }
}

// Supporting types

internal class ChatContext
{
    public string WorkspacePath { get; set; } = string.Empty;
    public List<ConversationTurn> ConversationHistory { get; set; } = new();
    public AgentCoordinatorResult? LastResult { get; set; }
    public bool DryRun { get; set; }
    public bool RequireApproval { get; set; }
    public bool Verbose { get; set; }
}

internal class ConversationTurn
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
