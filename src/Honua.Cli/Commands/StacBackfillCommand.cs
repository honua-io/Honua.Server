// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class StacBackfillCommand : AsyncCommand<StacBackfillCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;
    private readonly RasterStacCatalogBuilder _builder;

    public StacBackfillCommand(IAnsiConsole console, IHonuaCliEnvironment environment, RasterStacCatalogBuilder builder)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        string workspace;
        try
        {
            workspace = _environment.ResolveWorkspacePath(settings.Workspace);
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]{ex.Message}[/]");
            return 1;
        }

        var metadataPath = ResolveMetadataPath(workspace, settings.MetadataPath);
        if (metadataPath is null)
        {
            _console.MarkupLine("[red]Could not locate metadata.* file in the workspace. Use --metadata to specify the path explicitly.[/]");
            return 1;
        }

        var provider = settings.Provider?.Trim().ToLowerInvariant();
        if (provider.IsNullOrWhiteSpace())
        {
            provider = "sqlite";
        }

        var connectionString = settings.ConnectionString;
        var filePath = settings.OutputPath;

        if (provider == "sqlite")
        {
            filePath = ResolveSqlitePath(workspace, filePath);
            if (connectionString.IsNullOrWhiteSpace())
            {
                connectionString = $"Data Source={filePath};Cache=Shared;Pooling=true";
            }
        }
        else if (connectionString.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]--connection-string is required for non-sqlite providers.[/]");
            return 1;
        }

        var configuration = new StacCatalogConfiguration
        {
            Enabled = true,
            Provider = provider,
            ConnectionString = connectionString,
            FilePath = filePath
        };

        MetadataSnapshot snapshot;
        try
        {
            var metadataProvider = new JsonMetadataProvider(metadataPath);
            snapshot = await metadataProvider.LoadAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to load metadata: {ex.Message}[/]");
            return 1;
        }

        var datasets = snapshot.RasterDatasets.Where(_builder.Supports).ToList();
        if (datasets.Count == 0)
        {
            _console.MarkupLine("[yellow]No COG-backed raster datasets were found in the metadata. Nothing to backfill.[/]");
            return 0;
        }

        _console.MarkupLine($"Found [green]{datasets.Count}[/] COG raster dataset(s). Rebuilding STAC catalog...");

        var factory = new StacCatalogStoreFactory();
        var store = factory.Create(configuration);
        try
        {
            if (store is IAsyncDisposable asyncDisposable)
            {
                await using (asyncDisposable.ConfigureAwait(false))
                {
                    return await BackfillAsync(store, snapshot, datasets);
                }
            }

            if (store is IDisposable disposable)
            {
                using (disposable)
                {
                    return await BackfillAsync(store, snapshot, datasets);
                }
            }

            return await BackfillAsync(store, snapshot, datasets);
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to backfill STAC catalog: {ex.Message}[/]");
            return 1;
        }
    }

    private async Task<int> BackfillAsync(IStacCatalogStore store, MetadataSnapshot snapshot, IReadOnlyList<RasterDatasetDefinition> datasets)
    {
        await store.EnsureInitializedAsync().ConfigureAwait(false);

        var updated = 0;
        foreach (var dataset in datasets)
        {
            var (collection, items) = _builder.Build(dataset, snapshot);
            await store.DeleteCollectionAsync(collection.Id).ConfigureAwait(false);
            await store.UpsertCollectionAsync(collection).ConfigureAwait(false);

            foreach (var item in items)
            {
                await store.UpsertItemAsync(item).ConfigureAwait(false);
            }

            _console.MarkupLine($"[green]✔[/] {collection.Id}");
            updated++;
        }

        _console.MarkupLine($"[green]Completed[/] STAC backfill for {updated} collection(s).");
        return 0;
    }

    private static string? ResolveMetadataPath(string workspace, string? overridePath)
    {
        if (overridePath.HasValue())
        {
            var explicitPath = Path.GetFullPath(overridePath);
            return File.Exists(explicitPath) ? explicitPath : null;
        }

        var candidates = Directory.EnumerateFiles(workspace, "metadata.*", SearchOption.TopDirectoryOnly)
            .Where(file => string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return candidates.Count > 0 ? candidates[0] : null;
    }

    private static string ResolveSqlitePath(string workspace, string? outputPath)
    {
        if (outputPath.HasValue())
        {
            return Path.GetFullPath(outputPath);
        }

        var dataDirectory = Path.Combine(workspace, "data");
        Directory.CreateDirectory(dataDirectory);
        return Path.Combine(dataDirectory, "stac-catalog.db");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--workspace <PATH>")]
        [Description("Path to the Honua workspace containing metadata.* (defaults to current directory).")]
        public string? Workspace { get; init; }

        [CommandOption("--metadata <PATH>")]
        [Description("Explicit path to metadata JSON file (defaults to metadata.json under the workspace root).")]
        public string? MetadataPath { get; init; }

        [CommandOption("--provider <NAME>")]
        [Description("STAC catalog provider (sqlite, postgres, sqlserver, mysql). Defaults to sqlite.")]
        public string? Provider { get; init; }

        [CommandOption("--connection-string <CS>")]
        [Description("Connection string for the STAC catalog provider (optional for sqlite).")]
        public string? ConnectionString { get; init; }

        [CommandOption("--output <PATH>")]
        [Description("Path to the SQLite database file when using sqlite provider (defaults to <workspace>/data/stac-catalog.db).")]
        public string? OutputPath { get; init; }
    }
}
