// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Validation;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to sync layer metadata with current database schemas.
/// Discovers schema changes and updates layer field definitions to match the database.
/// </summary>
public sealed class MetadataSyncSchemaCommand : AsyncCommand<MetadataSyncSchemaCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            AnsiConsole.MarkupLine("[bold blue]Schema Sync[/] - Synchronizing layer metadata with database schemas");
            AnsiConsole.WriteLine();

            // Load metadata
            var metadataPath = ResolveMetadataPath(settings.MetadataPath);
            if (!File.Exists(metadataPath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Metadata file not found at '{metadataPath}'");
                return 1;
            }

            var jsonContent = await File.ReadAllTextAsync(metadataPath);
            var snapshot = JsonMetadataProvider.Parse(jsonContent);

            AnsiConsole.MarkupLine($"[green]✓[/] Loaded metadata from {metadataPath}");
            AnsiConsole.MarkupLine($"  Catalog: {snapshot.Catalog.Id}");
            AnsiConsole.MarkupLine($"  Services: {snapshot.Services.Count}");
            AnsiConsole.MarkupLine($"  Layers: {snapshot.Layers.Count}");
            AnsiConsole.WriteLine();

            // Initialize schema discovery service
            var logger = new ConsoleLogger<PostgresSchemaDiscoveryService>();
            var schemaDiscoveryService = new PostgresSchemaDiscoveryService(logger);

            var syncOptions = new SchemaSyncOptions
            {
                AddMissingFields = settings.AddMissingFields,
                RemoveOrphanedFields = settings.RemoveOrphanedFields,
                UpdateFieldTypes = settings.UpdateFieldTypes,
                UpdateNullability = settings.UpdateNullability,
                PreserveCustomMetadata = true
            };

            AnsiConsole.MarkupLine("[bold]Sync Options:[/]");
            AnsiConsole.MarkupLine($"  Add missing fields: {(syncOptions.AddMissingFields ? "[green]yes[/]" : "[dim]no[/]")}");
            AnsiConsole.MarkupLine($"  Remove orphaned fields: {(syncOptions.RemoveOrphanedFields ? "[yellow]yes[/]" : "[dim]no[/]")}");
            AnsiConsole.MarkupLine($"  Update field types: {(syncOptions.UpdateFieldTypes ? "[green]yes[/]" : "[dim]no[/]")}");
            AnsiConsole.MarkupLine($"  Update nullability: {(syncOptions.UpdateNullability ? "[green]yes[/]" : "[dim]no[/]")}");
            AnsiConsole.WriteLine();

            // Sync layers
            var updatedLayers = snapshot.Layers.ToList();
            var totalAdded = 0;
            var totalUpdated = 0;
            var totalRemoved = 0;
            var layersWithChanges = 0;

            foreach (var layer in snapshot.Layers)
            {
                var service = snapshot.Services.FirstOrDefault(s => s.Id == layer.ServiceId);
                if (service is null)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] Layer '{layer.Id}' references unknown service '{layer.ServiceId}' - skipping");
                    continue;
                }

                var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id == service.DataSourceId);
                if (dataSource is null)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] Service '{service.Id}' references unknown data source '{service.DataSourceId}' - skipping");
                    continue;
                }

                // Skip non-database providers
                if (dataSource.ConnectionString.IsNullOrWhiteSpace())
                {
                    continue;
                }

                try
                {
                    var result = await schemaDiscoveryService.SyncLayerFieldsAsync(
                        layer,
                        dataSource,
                        syncOptions,
                        CancellationToken.None);

                    if (result.HasChanges)
                    {
                        layersWithChanges++;
                        totalAdded += result.AddedFields.Count;
                        totalUpdated += result.UpdatedFields.Count;
                        totalRemoved += result.RemovedFields.Count;

                        AnsiConsole.MarkupLine($"[green]✓[/] {layer.Id}");

                        if (result.AddedFields.Count > 0)
                        {
                            AnsiConsole.MarkupLine($"    [green]+[/] Added {result.AddedFields.Count} fields: {string.Join(", ", result.AddedFields)}");
                        }

                        if (result.UpdatedFields.Count > 0)
                        {
                            AnsiConsole.MarkupLine($"    [yellow]~[/] Updated {result.UpdatedFields.Count} fields: {string.Join(", ", result.UpdatedFields)}");
                        }

                        if (result.RemovedFields.Count > 0)
                        {
                            AnsiConsole.MarkupLine($"    [red]-[/] Removed {result.RemovedFields.Count} fields: {string.Join(", ", result.RemovedFields)}");
                        }

                        foreach (var warning in result.Warnings)
                        {
                            AnsiConsole.MarkupLine($"    [yellow]⚠[/] {warning}");
                        }

                        // Update layer in list
                        var index = updatedLayers.FindIndex(l => l.Id == layer.Id);
                        if (index >= 0)
                        {
                            updatedLayers[index] = result.UpdatedLayer;
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[dim]○[/] {layer.Id} - no changes");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] {layer.Id} - {ex.Message}");
                }
            }

            AnsiConsole.WriteLine();

            if (layersWithChanges == 0)
            {
                AnsiConsole.MarkupLine("[green]✓[/] All layer schemas are up to date - no changes needed");
                return 0;
            }

            // Show summary
            AnsiConsole.MarkupLine($"[bold]Summary:[/]");
            AnsiConsole.MarkupLine($"  Layers with changes: {layersWithChanges}");
            AnsiConsole.MarkupLine($"  Fields added: {totalAdded}");
            AnsiConsole.MarkupLine($"  Fields updated: {totalUpdated}");
            AnsiConsole.MarkupLine($"  Fields removed: {totalRemoved}");
            AnsiConsole.WriteLine();

            // Confirm before saving
            if (!settings.DryRun)
            {
                if (!settings.Yes && !AnsiConsole.Confirm("Save changes to metadata file?", defaultValue: false))
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled[/] - no changes saved");
                    return 1;
                }

                // Create updated snapshot
                var updatedSnapshot = new MetadataSnapshot(
                    snapshot.Catalog,
                    snapshot.Folders,
                    snapshot.DataSources,
                    snapshot.Services,
                    updatedLayers,
                    snapshot.RasterDatasets,
                    snapshot.Styles,
                    snapshot.Server);

                // Serialize and save
                var options = new JsonSerializerOptions(JsonSerializerOptionsRegistry.WebIndented)
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var updatedJson = JsonSerializer.Serialize(updatedSnapshot, options);
                await File.WriteAllTextAsync(metadataPath, updatedJson);

                AnsiConsole.MarkupLine($"[green]✓[/] Saved changes to {metadataPath}");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Dry run mode[/] - no changes saved");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static string ResolveMetadataPath(string? path)
    {
        if (path.HasValue())
        {
            return Path.GetFullPath(path);
        }

        // Look for metadata.json in current directory or parent directories
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null)
        {
            var candidatePath = Path.Combine(currentDir, "metadata.json");
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        // Default to current directory
        return Path.Combine(Directory.GetCurrentDirectory(), "metadata.json");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--metadata <PATH>")]
        [Description("Path to metadata.json file (default: auto-discover from current directory)")]
        public string? MetadataPath { get; init; }

        [CommandOption("--add-fields")]
        [Description("Add new fields found in database to metadata (default: false)")]
        [DefaultValue(false)]
        public bool AddMissingFields { get; init; }

        [CommandOption("--remove-orphaned")]
        [Description("Remove fields from metadata that no longer exist in database (default: true)")]
        [DefaultValue(true)]
        public bool RemoveOrphanedFields { get; init; }

        [CommandOption("--update-types")]
        [Description("Update field types to match database (default: true)")]
        [DefaultValue(true)]
        public bool UpdateFieldTypes { get; init; }

        [CommandOption("--update-nullable")]
        [Description("Update field nullability to match database (default: true)")]
        [DefaultValue(true)]
        public bool UpdateNullability { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show what would change without saving")]
        public bool DryRun { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        public bool Yes { get; init; }
    }
}

/// <summary>
/// Simple console logger for CLI usage.
/// </summary>
internal sealed class ConsoleLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        Microsoft.Extensions.Logging.EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Only log warnings and errors to keep CLI output clean
        if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Warning)
        {
            var message = formatter(state, exception);
            var prefix = logLevel switch
            {
                Microsoft.Extensions.Logging.LogLevel.Error => "[red]ERROR[/]",
                Microsoft.Extensions.Logging.LogLevel.Warning => "[yellow]WARN[/]",
                _ => "[dim]INFO[/]"
            };
            AnsiConsole.MarkupLine($"{prefix}: {message}");
        }
    }
}
