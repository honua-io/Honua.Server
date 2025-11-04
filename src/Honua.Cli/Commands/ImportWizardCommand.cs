// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.Configuration;
using Honua.Cli.Utilities;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// Interactive wizard for importing geospatial data into Honua
/// </summary>
public sealed class ImportWizardCommand : AsyncCommand<ImportWizardCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliConfigStore _configStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImportWizardCommand> _logger;

    public ImportWizardCommand(IAnsiConsole console, IHonuaCliConfigStore configStore, IHttpClientFactory httpClientFactory, ILogger<ImportWizardCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                _console.Write(new FigletText("Honua Import").Color(Color.Blue));
                _console.WriteLine();

                // Get server URL
                var serverUrl = settings.ServerUrl;
                var token = settings.Token;

                if (serverUrl.IsNullOrWhiteSpace() || token.IsNullOrWhiteSpace())
                {
                    try
                    {
                        var config = await _configStore.LoadAsync(CancellationToken.None);
                        serverUrl ??= config.Host;
                        token ??= config.Token;
                    }
                    catch
                    {
                        // Ignore if no config exists
                    }
                }

                if (serverUrl.IsNullOrWhiteSpace())
                {
                    _console.MarkupLine("[red]No server URL configured. Use --server or run 'honua config init'[/]");
                    return 1;
                }

                // Create HTTP client with authentication
                using var httpClient = _httpClientFactory.CreateClient("ImportWizard");
                if (token.HasValue())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Determine import source type
                var importType = settings.SourceType ?? _console.Prompt(
                    new SelectionPrompt<string>()
                        .Title("What would you like to import?")
                        .AddChoices("Local File", "URL", "Database Table"));

                return importType switch
                {
                    "Local File" => await ImportFromFileAsync(httpClient, serverUrl, settings),
                    "URL" => await ImportFromUrlAsync(httpClient, serverUrl, settings),
                    "Database Table" => await ImportFromDatabaseAsync(httpClient, serverUrl, settings),
                    _ => 1
                };
            },
            _logger,
            "import-wizard");
    }

    private async Task<int> ImportFromFileAsync(HttpClient httpClient, string serverUrl, Settings settings)
    {
        // Get file path
        var filePath = settings.SourcePath ?? _console.Ask<string>("File path:");

        if (!File.Exists(filePath))
        {
            _console.MarkupLine($"[red]File not found: {filePath}[/]");
            return 1;
        }

        var fileInfo = new FileInfo(filePath);
        var extension = fileInfo.Extension.ToLowerInvariant();

        // Detect format
        var format = DetectFormat(extension);
        _console.MarkupLine($"Detected format: [cyan]{format}[/]");

        if (format == "Unknown")
        {
            _console.MarkupLine("[yellow]Unsupported file format. Supported: .geojson, .shp, .gpkg, .kml, .csv[/]");
            return 1;
        }

        // Get target service
        var serviceId = await SelectServiceAsync(httpClient, serverUrl, settings.TargetService);
        if (serviceId == null)
        {
            return 1;
        }

        // Get layer name
        var layerName = settings.LayerName ?? _console.Prompt(
            new TextPrompt<string>("Layer name:")
                .DefaultValue(Path.GetFileNameWithoutExtension(filePath)));

        // Confirm import
        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Source", filePath);
        table.AddRow("Format", format);
        table.AddRow("Service", serviceId);
        table.AddRow("Layer", layerName);
        table.AddRow("Size", FormatFileSize(fileInfo.Length));

        _console.Write(table);
        _console.WriteLine();

        if (!_console.Confirm("Proceed with import?", true))
        {
            _console.MarkupLine("[yellow]Import cancelled[/]");
            return 0;
        }

        // Upload and import
        return await UploadAndImportAsync(httpClient, serverUrl, filePath, serviceId, layerName, format);
    }

    private async Task<int> ImportFromUrlAsync(HttpClient httpClient, string serverUrl, Settings settings)
    {
        var url = settings.SourcePath ?? _console.Ask<string>("Source URL:");

        // Get target service
        var serviceId = await SelectServiceAsync(httpClient, serverUrl, settings.TargetService);
        if (serviceId == null)
        {
            return 1;
        }

        var layerName = settings.LayerName ?? _console.Ask<string>("Layer name:");

        // Trigger import from URL
        _console.MarkupLine($"Importing from [cyan]{url}[/]...");

        var payload = new
        {
            url,
            serviceId,
            layerName,
            overwrite = settings.Overwrite
        };

        var response = await httpClient.PostAsJsonAsync($"{serverUrl}/api/data/import/url", payload);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ImportResponse>();
            _console.MarkupLine($"[green]✓ Import started. Job ID: {result?.JobId}[/]");
            _console.MarkupLine($"  Monitor status: [cyan]honua data status {result?.JobId}[/]");
            return 0;
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _console.MarkupLine($"[red]Import failed: {error}[/]");
            return 1;
        }
    }

    private Task<int> ImportFromDatabaseAsync(HttpClient httpClient, string serverUrl, Settings settings)
    {
        _console.MarkupLine("[yellow]Database import requires metadata configuration[/]");
        _console.MarkupLine("Add a layer definition to your metadata.yaml pointing to the table,");
        _console.MarkupLine("then run: [cyan]honua metadata validate[/]");
        _console.WriteLine();
        _console.MarkupLine("Example metadata.yaml:");
        _console.WriteLine();
        _console.MarkupLine("[dim]layers:[/]");
        _console.MarkupLine("[dim]  - id: my-layer[/]");
        _console.MarkupLine("[dim]    serviceId: my-service[/]");
        _console.MarkupLine("[dim]    title: My Layer[/]");
        _console.MarkupLine("[dim]    storage:[/]");
        _console.MarkupLine("[dim]      table: schema.table_name[/]");
        _console.MarkupLine("[dim]      geometryColumn: geom[/]");
        _console.MarkupLine("[dim]      primaryKey: id[/]");
        return Task.FromResult(0);
    }

    private async Task<string?> SelectServiceAsync(HttpClient httpClient, string serverUrl, string? preselected)
    {
        if (preselected.HasValue())
        {
            return preselected;
        }

        var response = await httpClient.GetAsync($"{serverUrl}/api/services");

        if (!response.IsSuccessStatusCode)
        {
            _console.MarkupLine($"[red]Failed to fetch services: {response.StatusCode}[/]");
            return null;
        }

        var services = await response.Content.ReadFromJsonAsync<ServiceInfo[]>();

        if (services == null || services.Length == 0)
        {
            _console.MarkupLine("[yellow]No services found. Create a service first.[/]");
            return null;
        }

        var serviceId = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Select target service:")
                .AddChoices(services.Select(s => s.Id)));

        return serviceId;
    }

    private async Task<int> UploadAndImportAsync(HttpClient httpClient, string serverUrl, string filePath, string serviceId, string layerName, string format)
    {
        await _console.Status()
            .StartAsync("Uploading file...", async ctx =>
            {
                await using var fileStream = File.OpenRead(filePath);
                using var content = new MultipartFormDataContent();

                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "file", Path.GetFileName(filePath));
                content.Add(new StringContent(serviceId), "serviceId");
                content.Add(new StringContent(layerName), "layerName");
                content.Add(new StringContent(format), "format");

                ctx.Status("Uploading and processing...");
                var response = await httpClient.PostAsync($"{serverUrl}/api/data/import", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ImportResponse>();
                    _console.MarkupLine($"[green]✓ Import successful! Job ID: {result?.JobId}[/]");
                    _console.MarkupLine($"  Imported {result?.RecordCount} features");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _console.MarkupLine($"[red]Import failed: {error}[/]");
                }
            });

        return 0;
    }

    private static string DetectFormat(string extension)
    {
        return extension switch
        {
            ".geojson" or ".json" => "GeoJSON",
            ".shp" => "Shapefile",
            ".gpkg" => "GeoPackage",
            ".kml" => "KML",
            ".csv" => "CSV",
            ".gml" => "GML",
            ".parquet" => "GeoParquet",
            _ => "Unknown"
        };
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    private sealed record ServiceInfo(string Id, string Title);
    private sealed record ImportResponse(string? JobId, int RecordCount);

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--server <URL>")]
        [Description("Honua server URL")]
        public string? ServerUrl { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authentication")]
        public string? Token { get; init; }

        [CommandOption("--source-type <TYPE>")]
        [Description("Import source: 'Local File', 'URL', 'Database Table'")]
        public string? SourceType { get; init; }

        [CommandOption("--source <PATH>")]
        [Description("Source file path or URL")]
        public string? SourcePath { get; init; }

        [CommandOption("--service <ID>")]
        [Description("Target service ID")]
        public string? TargetService { get; init; }

        [CommandOption("--layer <NAME>")]
        [Description("Layer name")]
        public string? LayerName { get; init; }

        [CommandOption("--overwrite")]
        [Description("Overwrite existing layer")]
        public bool Overwrite { get; init; }
    }
}
