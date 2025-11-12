// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration.V2.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command for previewing what would be configured from a Honua Configuration 2.0 file.
/// Shows services, data sources, layers, and endpoints without actually running the server.
/// Usage: honua config plan [path] [options]
/// </summary>
public sealed class ConfigPlanCommand : AsyncCommand<ConfigPlanCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public ConfigPlanCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var filePath = ResolveConfigurationPath(settings.Path);

        if (!File.Exists(filePath))
        {
            _console.MarkupLine($"[red]Error: Configuration file not found: {filePath}[/]");
            return 1;
        }

        _console.MarkupLine($"[blue]Planning configuration from:[/] {filePath}");
        _console.WriteLine();

        // Load and validate configuration
        HonuaConfig config;
        try
        {
            config = await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Loading configuration...", async ctx =>
                {
                    return await HonuaConfigLoader.LoadAsync(filePath, CancellationToken.None);
                });
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error loading configuration: {ex.Message}[/]");
            return 1;
        }

        // Optionally validate first
        if (settings.Validate)
        {
            var validationResult = await ConfigurationValidator.ValidateFileAsync(
                filePath,
                ValidationOptions.Default,
                connectionFactory: null,
                CancellationToken.None);

            if (!validationResult.IsValid)
            {
                _console.MarkupLine("[red]Configuration validation failed. Use 'honua config validate' for details.[/]");
                return 1;
            }

            _console.MarkupLine("[green]✓ Configuration is valid[/]");
            _console.WriteLine();
        }

        // Display plan
        DisplayGlobalSettings(config);
        DisplayDataSources(config);
        DisplayServices(config);
        DisplayLayers(config);
        DisplayCacheConfiguration(config);
        DisplayRateLimiting(config);

        if (settings.ShowEndpoints)
        {
            DisplayEndpoints(config);
        }

        DisplaySummary(config);

        return 0;
    }

    private string ResolveConfigurationPath(string? providedPath)
    {
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            return Path.GetFullPath(providedPath);
        }

        // Try common configuration file names
        var searchPaths = new[]
        {
            "honua.config.hcl",
            "honua.config.honua",
            "config.hcl",
            "config.honua"
        };

        foreach (var searchPath in searchPaths)
        {
            var fullPath = Path.GetFullPath(searchPath);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Default to honua.config.hcl
        return Path.GetFullPath("honua.config.hcl");
    }

    private void DisplayGlobalSettings(HonuaConfig config)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .AddColumn(new TableColumn("[bold]Setting[/]"))
            .AddColumn(new TableColumn("[bold]Value[/]"));

        table.AddRow("Environment", $"[cyan]{config.Honua.Environment}[/]");
        table.AddRow("Log Level", $"[cyan]{config.Honua.LogLevel}[/]");

        if (config.Honua.Cors != null)
        {
            var corsValue = config.Honua.Cors.AllowAnyOrigin
                ? "[yellow]Allow Any Origin: true[/]"
                : $"Allowed Origins: {string.Join(", ", config.Honua.Cors.AllowedOrigins)}";
            table.AddRow("CORS", corsValue);
        }

        _console.Write(new Panel(table)
            .Header("[bold blue]Global Settings[/]")
            .BorderColor(Color.Blue));
        _console.WriteLine();
    }

    private void DisplayDataSources(HonuaConfig config)
    {
        if (config.DataSources.Count == 0)
        {
            _console.MarkupLine("[dim]No data sources configured[/]");
            _console.WriteLine();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .AddColumn(new TableColumn("[bold]ID[/]"))
            .AddColumn(new TableColumn("[bold]Provider[/]"))
            .AddColumn(new TableColumn("[bold]Connection[/]"))
            .AddColumn(new TableColumn("[bold]Pool[/]"));

        foreach (var ds in config.DataSources.Values)
        {
            var connectionDisplay = ds.ConnectionString.Length > 50
                ? ds.ConnectionString.Substring(0, 47) + "..."
                : ds.ConnectionString;

            var poolInfo = ds.Pool != null
                ? $"{ds.Pool.MinSize}-{ds.Pool.MaxSize}"
                : "[dim]none[/]";

            table.AddRow(
                $"[cyan]{ds.Id}[/]",
                ds.Provider,
                $"[dim]{connectionDisplay.EscapeMarkup()}[/]",
                poolInfo);
        }

        _console.Write(new Panel(table)
            .Header("[bold green]Data Sources[/]")
            .BorderColor(Color.Green));
        _console.WriteLine();
    }

    private void DisplayServices(HonuaConfig config)
    {
        var enabledServices = config.Services.Values.Where(s => s.Enabled).ToList();

        if (enabledServices.Count == 0)
        {
            _console.MarkupLine("[dim]No services enabled[/]");
            _console.WriteLine();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .AddColumn(new TableColumn("[bold]Service ID[/]"))
            .AddColumn(new TableColumn("[bold]Type[/]"))
            .AddColumn(new TableColumn("[bold]Settings[/]"));

        foreach (var service in enabledServices)
        {
            var settingsCount = service.Settings.Count;
            var settingsDisplay = settingsCount > 0 ? $"{settingsCount} setting(s)" : "[dim]none[/]";

            table.AddRow(
                $"[cyan]{service.Id}[/]",
                service.Type,
                settingsDisplay);
        }

        _console.Write(new Panel(table)
            .Header($"[bold yellow]Services ({enabledServices.Count} enabled)[/]")
            .BorderColor(Color.Yellow));
        _console.WriteLine();
    }

    private void DisplayLayers(HonuaConfig config)
    {
        if (config.Layers.Count == 0)
        {
            _console.MarkupLine("[dim]No layers configured[/]");
            _console.WriteLine();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Magenta)
            .AddColumn(new TableColumn("[bold]Layer ID[/]"))
            .AddColumn(new TableColumn("[bold]Title[/]"))
            .AddColumn(new TableColumn("[bold]Data Source[/]"))
            .AddColumn(new TableColumn("[bold]Table[/]"))
            .AddColumn(new TableColumn("[bold]Geometry[/]"))
            .AddColumn(new TableColumn("[bold]Services[/]"));

        foreach (var layer in config.Layers.Values)
        {
            var geometryInfo = layer.Geometry != null
                ? $"{layer.Geometry.Type} (SRID:{layer.Geometry.Srid})"
                : "[dim]none[/]";

            var servicesList = layer.ServiceIds.Count > 0
                ? string.Join(", ", layer.ServiceIds)
                : "[dim]none[/]";

            table.AddRow(
                $"[cyan]{layer.Id}[/]",
                layer.Title ?? layer.Id,
                layer.DataSourceId,
                layer.Table,
                geometryInfo,
                $"[dim]{servicesList}[/]");
        }

        _console.Write(new Panel(table)
            .Header($"[bold magenta]Layers ({config.Layers.Count} configured)[/]")
            .BorderColor(Color.Magenta));
        _console.WriteLine();
    }

    private void DisplayCacheConfiguration(HonuaConfig config)
    {
        if (config.Caches.Count == 0)
        {
            _console.MarkupLine("[dim]No cache configured[/]");
            _console.WriteLine();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn(new TableColumn("[bold]Cache ID[/]"))
            .AddColumn(new TableColumn("[bold]Type[/]"))
            .AddColumn(new TableColumn("[bold]Enabled[/]"))
            .AddColumn(new TableColumn("[bold]Connection[/]"));

        foreach (var cache in config.Caches.Values)
        {
            var enabledIcon = cache.Enabled ? "[green]✓[/]" : "[red]✗[/]";
            var connection = cache.ConnectionString.Length > 40
                ? cache.ConnectionString.Substring(0, 37) + "..."
                : cache.ConnectionString;

            table.AddRow(
                $"[cyan]{cache.Id}[/]",
                cache.Type,
                enabledIcon,
                $"[dim]{connection.EscapeMarkup()}[/]");
        }

        _console.Write(new Panel(table)
            .Header("[bold cyan]Cache Configuration[/]")
            .BorderColor(Color.Cyan1));
        _console.WriteLine();
    }

    private void DisplayRateLimiting(HonuaConfig config)
    {
        if (config.RateLimit == null)
        {
            _console.MarkupLine("[dim]Rate limiting not configured[/]");
            _console.WriteLine();
            return;
        }

        var enabledIcon = config.RateLimit.Enabled ? "[green]✓ Enabled[/]" : "[red]✗ Disabled[/]";
        var store = !string.IsNullOrWhiteSpace(config.RateLimit.Store)
            ? config.RateLimit.Store
            : "memory";

        var grid = new Grid()
            .AddColumn()
            .AddColumn()
            .AddRow("[bold]Status:[/]", enabledIcon)
            .AddRow("[bold]Store:[/]", $"[cyan]{store}[/]");

        if (config.RateLimit.Rules.Count > 0)
        {
            var rulesText = string.Join("\n", config.RateLimit.Rules.Select(kvp =>
                $"  • {kvp.Key}: {kvp.Value.Requests} requests / {kvp.Value.Window}"));
            grid.AddRow("[bold]Rules:[/]", rulesText);
        }

        _console.Write(new Panel(grid)
            .Header("[bold orange1]Rate Limiting[/]")
            .BorderColor(Color.Orange1));
        _console.WriteLine();
    }

    private void DisplayEndpoints(HonuaConfig config)
    {
        var enabledServices = config.Services.Values.Where(s => s.Enabled).ToList();

        if (enabledServices.Count == 0)
        {
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green3)
            .AddColumn(new TableColumn("[bold]Service Type[/]"))
            .AddColumn(new TableColumn("[bold]Base Path[/]"));

        // Common endpoint mappings (based on plugin system)
        var endpointMappings = new System.Collections.Generic.Dictionary<string, string>
        {
            ["odata"] = "/odata",
            ["ogc_api"] = "/collections",
            ["wfs"] = "/wfs",
            ["wms"] = "/wms",
            ["wmts"] = "/wmts",
            ["csw"] = "/csw",
            ["wcs"] = "/wcs",
            ["carto"] = "/api/v1",
            ["geoservices_rest"] = "/rest/services",
            ["stac"] = "/stac",
            ["zarr_api"] = "/zarr"
        };

        foreach (var service in enabledServices)
        {
            var basePath = endpointMappings.TryGetValue(service.Type, out var path) ? path : $"/{service.Type}";

            table.AddRow(service.Type, $"[cyan]{basePath}[/]");
        }

        _console.Write(new Panel(table)
            .Header("[bold green3]Endpoints[/]")
            .BorderColor(Color.Green3));
        _console.WriteLine();
    }

    private void DisplaySummary(HonuaConfig config)
    {
        var enabledServicesCount = config.Services.Values.Count(s => s.Enabled);
        var totalLayersCount = config.Layers.Count;

        var rule = new Rule("[bold blue]Summary[/]")
            .RuleStyle(Style.Parse("blue"));
        _console.Write(rule);

        var grid = new Grid()
            .AddColumn()
            .AddColumn()
            .AddRow("[bold]Data Sources:[/]", $"[cyan]{config.DataSources.Count}[/]")
            .AddRow("[bold]Services:[/]", $"[cyan]{enabledServicesCount}[/] enabled")
            .AddRow("[bold]Layers:[/]", $"[cyan]{totalLayersCount}[/]")
            .AddRow("[bold]Caches:[/]", $"[cyan]{config.Caches.Count}[/]")
            .AddRow("[bold]Rate Limiting:[/]", config.RateLimit?.Enabled == true ? "[green]Enabled[/]" : "[dim]Disabled[/]");

        _console.Write(grid);
        _console.WriteLine();

        _console.MarkupLine("[green]✓ Configuration plan complete[/]");
        _console.MarkupLine("[dim]Use 'honua config validate' to check for errors before deploying[/]");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Path to configuration file (default: honua.config.hcl)")]
        public string? Path { get; init; }

        [CommandOption("--validate")]
        [Description("Validate configuration before showing plan")]
        public bool Validate { get; init; }

        [CommandOption("--show-endpoints")]
        [Description("Show HTTP endpoints that would be mapped")]
        public bool ShowEndpoints { get; init; }
    }
}
