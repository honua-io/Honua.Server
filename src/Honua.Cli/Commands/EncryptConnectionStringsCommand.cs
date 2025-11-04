// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Security;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to encrypt connection strings in metadata files.
/// This is a migration tool for existing deployments.
/// </summary>
[Description("Encrypt connection strings in metadata file")]
public sealed class EncryptConnectionStringsCommand : AsyncCommand<EncryptConnectionStringsCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IConnectionStringEncryptionService? _encryptionService;
    private readonly ILogger<EncryptConnectionStringsCommand> _logger;

    public EncryptConnectionStringsCommand(
        IAnsiConsole console,
        IConnectionStringEncryptionService? encryptionService,
        ILogger<EncryptConnectionStringsCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _encryptionService = encryptionService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (_encryptionService == null)
        {
            _console.MarkupLine("[red]Connection string encryption service is not configured. Please configure encryption in appsettings.json[/]");
            return 1;
        }

        return await ExecuteAsync(
            settings.Input,
            settings.Output,
            settings.InPlace,
            settings.DryRun,
            _encryptionService,
            _logger,
            CancellationToken.None);
    }

    private async Task<int> ExecuteAsync(
        string inputPath,
        string? outputPath,
        bool inPlace,
        bool dryRun,
        IConnectionStringEncryptionService encryptionService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(inputPath))
            {
                _console.MarkupLine($"[red]Input file not found: {inputPath}[/]");
                logger.LogError("Input file not found: {Path}", inputPath);
                return 1;
            }

            // Read input file
            _console.MarkupLine($"[cyan]Reading metadata file: {inputPath}[/]");
            logger.LogInformation("Reading metadata file: {Path}", inputPath);
            var json = await File.ReadAllTextAsync(inputPath, cancellationToken);
            var doc = JsonNode.Parse(json);

            if (doc == null)
            {
                _console.MarkupLine("[red]Failed to parse JSON file[/]");
                logger.LogError("Failed to parse JSON file");
                return 1;
            }

            // Find and encrypt connection strings
            var dataSources = doc["dataSources"];
            if (dataSources is not JsonArray dataSourcesArray)
            {
                _console.MarkupLine("[yellow]No dataSources array found in metadata[/]");
                logger.LogWarning("No dataSources array found in metadata");
                return 0;
            }

            var encryptedCount = 0;
            var skippedCount = 0;

            foreach (var dataSource in dataSourcesArray)
            {
                if (dataSource == null) continue;

                var id = dataSource["id"]?.GetValue<string>();
                var connectionString = dataSource["connectionString"]?.GetValue<string>();

                if (connectionString.IsNullOrWhiteSpace())
                {
                    _console.MarkupLine($"[yellow]Data source {id ?? "unknown"} has no connection string[/]");
                    logger.LogWarning("Data source {Id} has no connection string", id ?? "unknown");
                    continue;
                }

                if (encryptionService.IsEncrypted(connectionString))
                {
                    _console.MarkupLine($"[dim]Data source {id} connection string is already encrypted, skipping[/]");
                    logger.LogInformation("Data source {Id} connection string is already encrypted, skipping", id);
                    skippedCount++;
                    continue;
                }

                if (dryRun)
                {
                    _console.MarkupLine($"[yellow][DRY RUN] Would encrypt connection string for data source: {id}[/]");
                    logger.LogInformation("[DRY RUN] Would encrypt connection string for data source: {Id}", id);
                    encryptedCount++;
                }
                else
                {
                    var encrypted = await encryptionService.EncryptAsync(connectionString, cancellationToken);
                    dataSource["connectionString"] = encrypted;
                    _console.MarkupLine($"[green]Encrypted connection string for data source: {id}[/]");
                    logger.LogInformation("Encrypted connection string for data source: {Id}", id);
                    encryptedCount++;
                }
            }

            _console.WriteLine();
            _console.MarkupLine($"[bold]Encryption summary:[/] {encryptedCount} encrypted, {skippedCount} skipped");
            logger.LogInformation(
                "Encryption summary: {Encrypted} encrypted, {Skipped} skipped",
                encryptedCount,
                skippedCount);

            if (dryRun)
            {
                _console.MarkupLine("[yellow][DRY RUN] No files were modified[/]");
                logger.LogInformation("[DRY RUN] No files were modified");
                return 0;
            }

            if (encryptedCount == 0)
            {
                _console.MarkupLine("[cyan]No connection strings needed encryption[/]");
                logger.LogInformation("No connection strings needed encryption");
                return 0;
            }

            // Determine output path
            string finalOutputPath;
            if (inPlace)
            {
                // Create backup
                var backupPath = inputPath + ".bak";
                File.Copy(inputPath, backupPath, overwrite: true);
                _console.MarkupLine($"[dim]Created backup: {backupPath}[/]");
                logger.LogInformation("Created backup: {Path}", backupPath);
                finalOutputPath = inputPath;
            }
            else
            {
                finalOutputPath = outputPath ?? Path.ChangeExtension(inputPath, ".encrypted.json");
            }

            // Write output file
            var outputJson = doc.ToJsonString(JsonSerializerOptionsRegistry.WebIndented);
            await File.WriteAllTextAsync(finalOutputPath, outputJson, cancellationToken);

            _console.MarkupLine($"[green]Wrote encrypted metadata to: {finalOutputPath}[/]");
            logger.LogInformation("Wrote encrypted metadata to: {Path}", finalOutputPath);
            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to encrypt connection strings: {ex.Message}[/]");
            logger.LogError(ex, "Failed to encrypt connection strings");
            return 1;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Path to input metadata JSON file")]
        [CommandArgument(0, "[input]")]
        public string Input { get; init; } = null!;

        [Description("Path to output metadata JSON file (defaults to input file with .encrypted suffix)")]
        [CommandOption("-o|--output")]
        public string? Output { get; init; }

        [Description("Modify the input file in place (creates backup with .bak suffix)")]
        [CommandOption("--in-place")]
        [DefaultValue(false)]
        public bool InPlace { get; init; }

        [Description("Show what would be encrypted without modifying files")]
        [CommandOption("--dry-run")]
        [DefaultValue(false)]
        public bool DryRun { get; init; }
    }
}
