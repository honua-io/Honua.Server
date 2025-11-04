// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Data.Common;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.Configuration;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// Test connectivity to Honua server, database, and cloud storage
/// </summary>
public sealed class TestConnectionCommand : AsyncCommand<TestConnectionCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliConfigStore _configStore;
    private readonly IHttpClientFactory _httpClientFactory;

    public TestConnectionCommand(IAnsiConsole console, IHonuaCliConfigStore configStore, IHttpClientFactory httpClientFactory)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold]Testing Honua Connectivity[/]");
        _console.WriteLine();

        var allSuccess = true;

        // Test Server Connection
        if (!settings.SkipServer)
        {
            allSuccess &= await TestServerConnectionAsync(settings.ServerUrl, CancellationToken.None);
        }

        // Test Database Connection
        if (!settings.SkipDatabase && settings.ConnectionString.HasValue())
        {
            allSuccess &= await TestDatabaseConnectionAsync(settings.ConnectionString, settings.Provider);
        }

        // Test Cloud Storage
        if (!settings.SkipStorage && settings.StorageEndpoint.HasValue())
        {
            allSuccess &= await TestCloudStorageAsync(settings.StorageEndpoint);
        }

        _console.WriteLine();

        if (allSuccess)
        {
            _console.MarkupLine("[green]✓ All connectivity tests passed![/]");
            return 0;
        }
        else
        {
            _console.MarkupLine("[red]✗ Some connectivity tests failed. See details above.[/]");
            return 1;
        }
    }

    private async Task<bool> TestServerConnectionAsync(string? serverUrl, CancellationToken cancellationToken)
    {
        _console.MarkupLine("[bold]Server Connection[/]");

        var url = serverUrl;
        if (url.IsNullOrWhiteSpace())
        {
            try
            {
                var config = await _configStore.LoadAsync(cancellationToken);
                url = config.Host;
            }
            catch
            {
                // Ignore if no config exists
            }
        }

        if (url.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("  [yellow]⚠[/] No server URL configured. Use --server or run 'honua config init'");
            return false;
        }

        try
        {
            _console.Markup($"  Testing [cyan]{url}/health[/]... ");

            using var httpClient = _httpClientFactory.CreateClient("TestConnection");
            var response = await httpClient.GetAsync($"{url.TrimEnd('/')}/health");

            if (response.IsSuccessStatusCode)
            {
                _console.MarkupLine("[green]✓ Connected[/]");
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _console.MarkupLine($"  [dim]Status: {response.StatusCode}[/]");
                return true;
            }
            else
            {
                _console.MarkupLine($"[red]✗ Failed[/]");
                _console.MarkupLine($"  [red]HTTP {(int)response.StatusCode}: {response.ReasonPhrase}[/]");
                _console.MarkupLine("  [yellow]Troubleshooting:[/]");
                _console.MarkupLine("    - Verify the server is running");
                _console.MarkupLine("    - Check the URL is correct");
                _console.MarkupLine("    - Ensure no firewall is blocking the connection");
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _console.MarkupLine("[red]✗ Connection Failed[/]");
            _console.MarkupLine($"  [red]{ex.Message}[/]");
            _console.MarkupLine("  [yellow]Troubleshooting:[/]");
            _console.MarkupLine("    - Verify the server is running");
            _console.MarkupLine("    - Check network connectivity");
            _console.MarkupLine($"    - Try: curl {url}/health");
            return false;
        }
        catch (TaskCanceledException)
        {
            _console.MarkupLine("[red]✗ Timeout[/]");
            _console.MarkupLine("  [yellow]The server did not respond within 10 seconds[/]");
            _console.MarkupLine("  [yellow]Troubleshooting:[/]");
            _console.MarkupLine("    - Server may be overloaded or starting up");
            _console.MarkupLine("    - Check server logs for errors");
            return false;
        }
        catch (Exception ex)
        {
            _console.MarkupLine("[red]✗ Unexpected Error[/]");
            _console.MarkupLine($"  [red]{ex.GetType().Name}: {ex.Message}[/]");
            return false;
        }
    }

    private async Task<bool> TestDatabaseConnectionAsync(string connectionString, string? provider)
    {
        _console.MarkupLine("[bold]Database Connection[/]");

        var detectedProvider = provider ?? DetectProviderFromConnectionString(connectionString);
        _console.MarkupLine($"  Provider: [cyan]{detectedProvider}[/]");

        try
        {
            _console.Markup("  Testing connection... ");

            DbConnection? connection = detectedProvider.ToLowerInvariant() switch
            {
                "postgis" or "postgres" or "postgresql" => new NpgsqlConnection(connectionString),
                "sqlserver" or "mssql" => new SqlConnection(connectionString),
                "mysql" => new MySqlConnection(connectionString),
                _ => null
            };

            if (connection == null)
            {
                _console.MarkupLine("[red]✗ Unsupported Provider[/]");
                _console.MarkupLine($"  [yellow]Provider '{detectedProvider}' is not supported[/]");
                _console.MarkupLine("  [yellow]Supported providers: postgis, sqlserver, mysql[/]");
                return false;
            }

            await using var _ = connection.ConfigureAwait(false);

            connection.ConnectionString = connectionString;
            await connection.OpenAsync().ConfigureAwait(false);

            _console.MarkupLine("[green]✓ Connected[/]");
            _console.MarkupLine($"  [dim]Database: {connection.Database}[/]");
            _console.MarkupLine($"  [dim]Server Version: {connection.ServerVersion}[/]");

            // Test spatial capabilities for PostGIS
            if (detectedProvider.Contains("postgis", StringComparison.OrdinalIgnoreCase) ||
                detectedProvider.Contains("postgres", StringComparison.OrdinalIgnoreCase))
            {
                await TestPostGisExtensionAsync(connection);
            }

            return true;
        }
        catch (DbException ex)
        {
            _console.MarkupLine("[red]✗ Connection Failed[/]");
            _console.MarkupLine($"  [red]{ex.Message}[/]");
            _console.MarkupLine("  [yellow]Troubleshooting:[/]");
            _console.MarkupLine("    - Verify database server is running");
            _console.MarkupLine("    - Check connection string credentials");
            _console.MarkupLine("    - Ensure database exists");
            _console.MarkupLine("    - Check network/firewall settings");
            return false;
        }
        catch (Exception ex)
        {
            _console.MarkupLine("[red]✗ Unexpected Error[/]");
            _console.MarkupLine($"  [red]{ex.GetType().Name}: {ex.Message}[/]");
            return false;
        }
    }

    private async Task TestPostGisExtensionAsync(DbConnection connection)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT PostGIS_Version();";
            var version = await command.ExecuteScalarAsync().ConfigureAwait(false);

            if (version != null)
            {
                _console.MarkupLine($"  [dim]PostGIS Version: {version}[/]");
            }
        }
        catch
        {
            _console.MarkupLine("  [yellow]⚠ PostGIS extension not found[/]");
            _console.MarkupLine("  [yellow]Run: CREATE EXTENSION postgis;[/]");
        }
    }

    private async Task<bool> TestCloudStorageAsync(string endpoint)
    {
        _console.MarkupLine("[bold]Cloud Storage Connection[/]");
        _console.MarkupLine($"  Endpoint: [cyan]{endpoint}[/]");

        try
        {
            _console.Markup("  Testing connection... ");

            using var httpClient = _httpClientFactory.CreateClient("TestConnection");
            var response = await httpClient.GetAsync(endpoint);

            if (response.IsSuccessStatusCode || (int)response.StatusCode == 403)
            {
                // 403 is acceptable - means we reached the endpoint but need auth
                _console.MarkupLine("[green]✓ Reachable[/]");
                _console.MarkupLine($"  [dim]Status: {response.StatusCode}[/]");
                return true;
            }
            else
            {
                _console.MarkupLine($"[red]✗ Failed[/]");
                _console.MarkupLine($"  [red]HTTP {(int)response.StatusCode}: {response.ReasonPhrase}[/]");
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _console.MarkupLine("[red]✗ Connection Failed[/]");
            _console.MarkupLine($"  [red]{ex.Message}[/]");
            _console.MarkupLine("  [yellow]Troubleshooting:[/]");
            _console.MarkupLine("    - Verify storage endpoint URL");
            _console.MarkupLine("    - Check network connectivity");
            _console.MarkupLine("    - Ensure credentials are configured");
            return false;
        }
        catch (Exception ex)
        {
            _console.MarkupLine("[red]✗ Unexpected Error[/]");
            _console.MarkupLine($"  [red]{ex.GetType().Name}: {ex.Message}[/]");
            return false;
        }
    }

    private static string DetectProviderFromConnectionString(string connectionString)
    {
        var lower = connectionString.ToLowerInvariant();

        if (lower.Contains("host=") || lower.Contains("server=localhost") && lower.Contains("port=5432"))
        {
            return "postgis";
        }

        if (lower.Contains("server=") && (lower.Contains("database=") || lower.Contains("initial catalog=")))
        {
            return "sqlserver";
        }

        if (lower.Contains("server=") && lower.Contains("uid="))
        {
            return "mysql";
        }

        return "unknown";
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--server <URL>")]
        [Description("Honua server URL (defaults to configured host)")]
        public string? ServerUrl { get; init; }

        [CommandOption("--connection-string <STRING>")]
        [Description("Database connection string")]
        public string? ConnectionString { get; init; }

        [CommandOption("--provider <TYPE>")]
        [Description("Database provider: postgis, sqlserver, mysql")]
        public string? Provider { get; init; }

        [CommandOption("--storage <ENDPOINT>")]
        [Description("Cloud storage endpoint URL")]
        public string? StorageEndpoint { get; init; }

        [CommandOption("--skip-server")]
        [Description("Skip server connectivity test")]
        public bool SkipServer { get; init; }

        [CommandOption("--skip-database")]
        [Description("Skip database connectivity test")]
        public bool SkipDatabase { get; init; }

        [CommandOption("--skip-storage")]
        [Description("Skip storage connectivity test")]
        public bool SkipStorage { get; init; }
    }
}
