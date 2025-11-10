// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Cli.Services.Consultant;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Interactive setup wizard for first-time Honua configuration.
/// Guides users through deployment planning, database setup, and service configuration.
/// </summary>
public sealed class SetupWizardCommand : AsyncCommand<SetupWizardCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IConsultantWorkflow _workflow;
    private readonly IHonuaCliEnvironment _environment;

    public SetupWizardCommand(
        IAnsiConsole console,
        IConsultantWorkflow workflow,
        IHonuaCliEnvironment environment)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        // Display welcome banner
        _console.Write(new Rule("[bold cyan]Honua Setup Wizard[/]"));
        _console.WriteLine();
        _console.MarkupLine("[dim]This wizard will guide you through setting up a Honua geospatial server.[/]");
        _console.WriteLine();

        // Step 1: Deployment Target
        var deploymentTarget = settings.DeploymentTarget ?? PromptDeploymentTarget();

        // Step 2: Database Selection
        var databaseType = settings.DatabaseType ?? PromptDatabaseType(deploymentTarget);

        // Step 3: Data Source
        var dataSource = settings.DataSource ?? PromptDataSource();

        // Step 4: Workspace
        var workspace = _environment.ResolveWorkspacePath(settings.Workspace);

        // Display configuration summary
        _console.WriteLine();
        _console.Write(new Rule("[bold yellow]Configuration Summary[/]"));
        _console.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Setting[/]")
            .AddColumn("[bold]Value[/]");

        table.AddRow("Deployment Target", GetDeploymentTargetDisplay(deploymentTarget));
        table.AddRow("Database Type", GetDatabaseTypeDisplay(databaseType));
        table.AddRow("Data Source", GetDataSourceDisplay(dataSource));
        table.AddRow("Workspace", workspace);

        _console.Write(table);
        _console.WriteLine();

        // Confirm before proceeding
        if (!settings.AutoApprove)
        {
            var confirm = new ConfirmationPrompt("Proceed with AI-generated setup plan?")
            {
                DefaultValue = true
            };

            if (!_console.Prompt(confirm))
            {
                _console.MarkupLine("[yellow]Setup wizard cancelled.[/]");
                return 1;
            }
        }

        // Generate AI-powered setup plan
        _console.WriteLine();
        _console.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("[yellow]Generating personalized setup plan...[/]", ctx =>
            {
                Thread.Sleep(800); // Brief pause for UX
            });

        var prompt = BuildConsultantPrompt(deploymentTarget, databaseType, dataSource, workspace);
        var request = new ConsultantRequest(
            Prompt: prompt,
            DryRun: settings.DryRun,
            AutoApprove: true, // We already confirmed above
            SuppressLogging: settings.SuppressLogs,
            WorkspacePath: workspace,
            Mode: ConsultantExecutionMode.Plan);

        var cancellationToken = CancellationToken.None;
        var result = await _workflow.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        // Display next steps
        if (result.Success && result.Plan != null)
        {
            _console.WriteLine();
            _console.Write(new Rule("[bold green]Next Steps[/]"));
            _console.WriteLine();

            if (settings.DryRun)
            {
                _console.MarkupLine("[dim]This was a dry-run. Review the plan above and run again without --dry-run to execute.[/]");
            }
            else
            {
                _console.MarkupLine("[green]✓[/] Setup plan generated successfully!");
                _console.MarkupLine("[dim]Execute the commands above step-by-step to complete your Honua setup.[/]");
            }

            _console.WriteLine();
            _console.MarkupLine("[bold]Helpful commands:[/]");
            _console.MarkupLine("  [cyan]honua devsecops[/]          - Interactive AI Devsecops for custom tasks");
            _console.MarkupLine("  [cyan]honua metadata validate[/]   - Validate metadata configuration");
            _console.MarkupLine("  [cyan]honua status[/]              - Check server and data source status");
            _console.MarkupLine("  [cyan]honua --help[/]              - View all available commands");
        }

        return result.Success ? 0 : 1;
    }

    private string PromptDeploymentTarget()
    {
        _console.MarkupLine("[bold]Step 1: Deployment Target[/]");
        _console.MarkupLine("[dim]Choose your deployment environment:[/]");
        _console.WriteLine();

        var choice = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("What is your [green]deployment target[/]?")
                .AddChoices(new[] {
                    "Development - Local testing and experimentation",
                    "Staging - Pre-production validation environment",
                    "Production - Production-ready deployment"
                }));

        var target = choice.Split(" - ")[0].Trim().ToLowerInvariant();
        _console.WriteLine();
        return target;
    }

    private string PromptDatabaseType(string deploymentTarget)
    {
        _console.MarkupLine("[bold]Step 2: Database Selection[/]");

        if (deploymentTarget == "production")
        {
            _console.MarkupLine("[yellow]⚠[/]  [dim]PostGIS is strongly recommended for production deployments.[/]");
        }
        else
        {
            _console.MarkupLine("[dim]Choose your spatial database backend:[/]");
        }

        _console.WriteLine();

        var choices = deploymentTarget == "production"
            ? new[] {
                "PostGIS - Enterprise PostgreSQL with spatial extensions (recommended)",
                "SpatiaLite - Lightweight file-based SQLite (not recommended for production)"
              }
            : new[] {
                "PostGIS - Enterprise PostgreSQL with spatial extensions",
                "SpatiaLite - Lightweight file-based SQLite"
              };

        var choice = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Which [green]database[/] do you want to use?")
                .AddChoices(choices));

        var dbType = choice.Split(" - ")[0].Trim().ToLowerInvariant();
        _console.WriteLine();
        return dbType;
    }

    private string PromptDataSource()
    {
        _console.MarkupLine("[bold]Step 3: Data Source[/]");
        _console.MarkupLine("[dim]How will you provide geospatial data?[/]");
        _console.WriteLine();

        var choice = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("What is your [green]data source[/]?")
                .AddChoices(new[] {
                    "Files - GeoPackage, Shapefiles, GeoJSON, GeoTIFF",
                    "Existing Database - Connect to existing PostGIS/SpatiaLite",
                    "Cloud Storage - AWS S3, Azure Blob Storage"
                }));

        var source = choice.Split(" - ")[0].Trim().ToLowerInvariant();
        _console.WriteLine();
        return source;
    }

    private string BuildConsultantPrompt(
        string deploymentTarget,
        string databaseType,
        string dataSource,
        string workspace)
    {
        return $@"I need help setting up a Honua geospatial server with the following configuration:

**Deployment Target:** {deploymentTarget}
**Database:** {databaseType}
**Data Source:** {dataSource}
**Workspace:** {workspace}

Please generate a comprehensive, step-by-step setup plan that includes:
1. Prerequisites and dependency checks
2. Database provisioning and configuration
3. Data ingestion strategy
4. Service metadata configuration
5. Security and authentication setup
6. Performance optimization recommendations
7. Deployment and testing steps

Prioritize {(deploymentTarget == "production" ? "production-ready security and reliability" : "ease of setup and rapid iteration")}.
Include specific commands I can run and explain the rationale for each major step.";
    }

    private string GetDeploymentTargetDisplay(string target)
    {
        return target.ToLowerInvariant() switch
        {
            "development" => "[cyan]Development[/] - Local testing",
            "staging" => "[yellow]Staging[/] - Pre-production",
            "production" => "[red]Production[/] - Live deployment",
            _ => target
        };
    }

    private string GetDatabaseTypeDisplay(string dbType)
    {
        return dbType.ToLowerInvariant() switch
        {
            "postgis" => "[green]PostGIS[/] - PostgreSQL + spatial extensions",
            "spatialite" => "[blue]SpatiaLite[/] - SQLite + spatial extensions",
            _ => dbType
        };
    }

    private string GetDataSourceDisplay(string source)
    {
        return source.ToLowerInvariant() switch
        {
            "files" => "[cyan]Files[/] - GPKG, SHP, GeoJSON, TIFF",
            "existing database" => "[yellow]Existing Database[/] - Connect to existing",
            "existing-database" => "[yellow]Existing Database[/] - Connect to existing",
            "cloud storage" => "[magenta]Cloud Storage[/] - S3, Azure Blob",
            "cloud-storage" => "[magenta]Cloud Storage[/] - S3, Azure Blob",
            _ => source
        };
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--deployment-target <TARGET>")]
        [Description("Deployment target: development, staging, or production. Prompted if omitted.")]
        public string? DeploymentTarget { get; init; }

        [CommandOption("--database-type <TYPE>")]
        [Description("Database type: postgis or spatialite. Prompted if omitted.")]
        public string? DatabaseType { get; init; }

        [CommandOption("--data-source <SOURCE>")]
        [Description("Data source: files, existing-database, or cloud-storage. Prompted if omitted.")]
        public string? DataSource { get; init; }

        [CommandOption("--workspace <PATH>")]
        [Description("Path to the Honua workspace; defaults to the current directory.")]
        public string? Workspace { get; init; }

        [CommandOption("--dry-run")]
        [Description("Plan actions without executing deployment steps.")]
        public bool DryRun { get; init; }

        [CommandOption("--auto-approve")]
        [Description("Skip confirmation prompts and proceed directly to plan generation.")]
        public bool AutoApprove { get; init; }

        [CommandOption("--no-log")]
        [Description("Skip writing an audit log entry for this session.")]
        public bool SuppressLogs { get; init; }
    }
}
