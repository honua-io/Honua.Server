// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Honua.Server.Core.Performance;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class ConsultantPatternsIngestCommand : AsyncCommand<ConsultantPatternsIngestCommand.Settings>
{
    private readonly IDeploymentPatternKnowledgeStore _knowledgeStore;
    private readonly IAnsiConsole _console;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-f|--file <FILE>")]
        [Description("Path to a JSON file containing an array of deployment patterns.")]
        public string FilePath { get; init; } = string.Empty;

        [CommandOption("--approved-by <NAME>")]
        [Description("Name to record as the approver for ingested patterns.")]
        public string ApprovedBy { get; init; } = "automation";

        [CommandOption("--dry-run")]
        [Description("Validate the file and display patterns without indexing them.")]
        public bool DryRun { get; init; }

        public override ValidationResult Validate()
        {
            if (FilePath.IsNullOrWhiteSpace())
            {
                return ValidationResult.Error("File path is required.");
            }

            if (!File.Exists(FilePath))
            {
                return ValidationResult.Error($"File '{FilePath}' was not found.");
            }

            return ValidationResult.Success();
        }
    }

    public ConsultantPatternsIngestCommand(IDeploymentPatternKnowledgeStore knowledgeStore, IAnsiConsole console)
    {
        _knowledgeStore = knowledgeStore ?? throw new ArgumentNullException(nameof(knowledgeStore));
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var patterns = await LoadPatternsAsync(settings.FilePath).ConfigureAwait(false);
            if (patterns.Count == 0)
            {
                _console.MarkupLine("[yellow]No deployment patterns found in file.[/]");
                return 0;
            }

            _console.MarkupLine($"Loaded [green]{patterns.Count}[/] patterns from [blue]{settings.FilePath}[/].");

            if (settings.DryRun)
            {
                RenderPreview(patterns);
                _console.MarkupLine("[yellow]Dry run enabled. No patterns were indexed.[/]");
                return 0;
            }

            foreach (var pattern in patterns)
            {
                pattern.HumanApproved = true;
                pattern.ApprovedBy = settings.ApprovedBy;
                pattern.ApprovedDate = DateTime.UtcNow;

                await _knowledgeStore.IndexApprovedPatternAsync(pattern).ConfigureAwait(false);
                _console.MarkupLine($"[green]Indexed pattern[/] [blue]{pattern.Name}[/] ({pattern.Id}).");
            }

            _console.MarkupLine("[green]Pattern ingestion complete.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Pattern ingestion failed:[/] {Markup.Escape(ex.Message)}");
            return -1;
        }
    }

    private static async Task<List<DeploymentPattern>> LoadPatternsAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var patterns = await JsonSerializer.DeserializeAsync<List<DeploymentPattern>>(stream, JsonSerializerOptionsRegistry.DevTooling).ConfigureAwait(false);

        return patterns?.Where(p => p is not null).Select(NormalizePattern).ToList() ?? new List<DeploymentPattern>();
    }

    private static DeploymentPattern NormalizePattern(DeploymentPattern pattern)
    {
        if (pattern.Id.IsNullOrWhiteSpace())
        {
            pattern.Id = Guid.NewGuid().ToString();
        }

        if (pattern.Name.IsNullOrWhiteSpace())
        {
            pattern.Name = pattern.Id;
        }

        return pattern;
    }

    private void RenderPreview(IEnumerable<DeploymentPattern> patterns)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Id");
        table.AddColumn("Name");
        table.AddColumn("Cloud");
        table.AddColumn("Success Rate");
        table.AddColumn("Deployments");

        foreach (var pattern in patterns)
        {
            table.AddRow(
                pattern.Id,
                pattern.Name,
                pattern.CloudProvider,
                (pattern.SuccessRate * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%",
                pattern.DeploymentCount.ToString(CultureInfo.InvariantCulture));
        }

        _console.Write(table);
    }
}
