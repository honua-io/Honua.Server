// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
#pragma warning disable SKEXP0080 // Suppress experimental API warnings for SK Process Framework

using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.SemanticKernel;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to extract and publish geospatial dataset metadata using Semantic Kernel Process Framework.
/// Orchestrates metadata workflow: extract metadata → generate STAC → publish.
/// </summary>
[Description("Extract and publish geospatial dataset metadata")]
public sealed class ProcessMetadataCommand : AsyncCommand<ProcessMetadataCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly Kernel _kernel;

    public ProcessMetadataCommand(IAnsiConsole console, Kernel kernel)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            _console.MarkupLine("[bold cyan]Honua Metadata Extraction and Publishing[/]");
            _console.WriteLine();

            // Validate required parameters
            if (settings.DatasetPath.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Error: Dataset path is required (--dataset-path)[/]");
                return 1;
            }

            if (!File.Exists(settings.DatasetPath) && !Directory.Exists(settings.DatasetPath))
            {
                _console.MarkupLine($"[red]Error: Dataset path does not exist: {settings.DatasetPath}[/]");
                return 1;
            }

            // Build metadata state from settings
            var metadataState = new MetadataState
            {
                ProcessId = Guid.NewGuid().ToString(),
                MetadataId = settings.MetadataId ?? Guid.NewGuid().ToString(),
                DatasetName = settings.DatasetName ?? Path.GetFileNameWithoutExtension(settings.DatasetPath),
                DatasetPath = Path.GetFullPath(settings.DatasetPath),
                StartTime = DateTime.UtcNow,
                Status = "Starting"
            };

            // Display metadata extraction configuration
            var configTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Configuration")
                .AddColumn("Value");

            configTable.AddRow("[cyan]Dataset Name[/]", metadataState.DatasetName);
            configTable.AddRow("[cyan]Dataset Path[/]", metadataState.DatasetPath);
            configTable.AddRow("[cyan]Metadata ID[/]", metadataState.MetadataId);
            configTable.AddRow("[cyan]Process ID[/]", metadataState.ProcessId);
            configTable.AddRow("[cyan]Publish to STAC[/]", settings.PublishStac ? "[green]Yes[/]" : "[yellow]No[/]");
            configTable.AddRow("[cyan]STAC Catalog URL[/]", settings.StacCatalogUrl ?? "[dim]Not specified[/]");

            _console.Write(configTable);
            _console.WriteLine();

            if (!settings.AutoConfirm && !_console.Confirm("Start metadata extraction with these settings?"))
            {
                _console.MarkupLine("[yellow]Metadata extraction cancelled by user[/]");
                return 0;
            }

            // Build and start the metadata process
            var processBuilder = MetadataProcess.BuildProcess();
            var process = processBuilder.Build();

            // Start process with initial event
            await _console.Status()
                .StartAsync("Initializing metadata extraction process...", async ctx =>
                {
                    ctx.Status("Starting metadata extraction workflow...");

                    // Start the process with the metadata state
                    var processHandle = await process.StartAsync(
                        new KernelProcessEvent
                        {
                            Id = "StartMetadataExtraction",
                            Data = metadataState
                        },
                        Guid.NewGuid().ToString());

                    ctx.Status("Metadata extraction process started successfully");

                    _console.MarkupLine($"[green]Process started successfully[/]");
                    _console.MarkupLine($"[dim]Process ID: {processHandle}[/]");
                });

            _console.WriteLine();
            _console.MarkupLine("[bold green]Metadata extraction process initiated successfully[/]");
            _console.WriteLine();

            _console.MarkupLine("[bold]Next Steps:[/]");
            _console.MarkupLine($"  1. Monitor extraction: [cyan]honua process status {metadataState.ProcessId}[/]");
            _console.MarkupLine("  2. View metadata snapshots: [cyan]honua metadata snapshots[/]");
            _console.MarkupLine("  3. Validate metadata: [cyan]honua metadata validate[/]");

            if (settings.PublishStac)
            {
                _console.WriteLine();
                _console.MarkupLine("[yellow]Note: Metadata will be published to STAC catalog after extraction.[/]");
            }

            return 0;
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

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dataset-path")]
        [Description("Path to geospatial dataset file or directory (required)")]
        public string DatasetPath { get; set; } = string.Empty;

        [CommandOption("--dataset-name")]
        [Description("Name for the dataset (defaults to filename)")]
        public string? DatasetName { get; set; }

        [CommandOption("--metadata-id")]
        [Description("Metadata identifier (auto-generated if not provided)")]
        public string? MetadataId { get; set; }

        [CommandOption("--publish-stac")]
        [Description("Publish metadata to STAC catalog after extraction")]
        [DefaultValue(true)]
        public bool PublishStac { get; set; } = true;

        [CommandOption("--stac-catalog-url")]
        [Description("STAC catalog URL for publishing")]
        public string? StacCatalogUrl { get; set; }

        [CommandOption("--auto-confirm")]
        [Description("Skip confirmation prompt")]
        [DefaultValue(false)]
        public bool AutoConfirm { get; set; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }
    }
}
