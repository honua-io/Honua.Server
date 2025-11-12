// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2.Introspection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command for introspecting database schemas and generating Honua Configuration 2.0 files.
/// Usage: honua config introspect <connection-string> [options]
/// </summary>
public sealed class ConfigIntrospectCommand : AsyncCommand<ConfigIntrospectCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public ConfigIntrospectCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            _console.MarkupLine("[red]Error: Connection string is required[/]");
            _console.MarkupLine("[dim]Usage: honua config introspect <connection-string> [options][/]");
            return 1;
        }

        // Determine provider
        var provider = settings.Provider ?? SchemaReaderFactory.DetectProvider(settings.ConnectionString);
        if (provider == null)
        {
            _console.MarkupLine("[red]Error: Could not detect database provider from connection string[/]");
            _console.MarkupLine($"[dim]Supported providers: {string.Join(", ", SchemaReaderFactory.GetSupportedProviders())}[/]");
            _console.MarkupLine("[dim]Use --provider to specify explicitly[/]");
            return 1;
        }

        _console.MarkupLine($"[blue]Introspecting database:[/] [cyan]{provider}[/]");
        _console.WriteLine();

        // Create schema reader
        ISchemaReader reader;
        try
        {
            reader = SchemaReaderFactory.CreateReader(provider);
        }
        catch (NotSupportedException ex)
        {
            _console.MarkupLine($"[red]{ex.Message}[/]");
            return 1;
        }

        // Test connection first
        _console.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Testing database connection...", ctx =>
            {
                var canConnect = reader.TestConnectionAsync(settings.ConnectionString).GetAwaiter().GetResult();
                if (!canConnect)
                {
                    throw new InvalidOperationException("Failed to connect to database");
                }
            });

        _console.MarkupLine("[green]✓ Database connection successful[/]");
        _console.WriteLine();

        // Build introspection options
        var introspectionOptions = new IntrospectionOptions
        {
            TableNamePattern = settings.TablePattern,
            SchemaName = settings.SchemaName,
            IncludeSystemTables = settings.IncludeSystemTables,
            IncludeRowCounts = settings.IncludeRowCounts,
            IncludeViews = settings.IncludeViews,
            MaxTables = settings.MaxTables
        };

        // Introspect schema
        IntrospectionResult introspectionResult;
        try
        {
            introspectionResult = await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Introspecting database schema...", async ctx =>
                {
                    return await reader.IntrospectAsync(
                        settings.ConnectionString,
                        introspectionOptions,
                        CancellationToken.None);
                });
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error during introspection: {ex.Message}[/]");
            return 1;
        }

        if (!introspectionResult.Success || introspectionResult.Schema == null)
        {
            _console.MarkupLine("[red]Introspection failed[/]");
            foreach (var error in introspectionResult.Errors)
            {
                _console.MarkupLine($"[red]  • {error.EscapeMarkup()}[/]");
            }
            return 1;
        }

        var schema = introspectionResult.Schema;

        // Display schema summary
        DisplaySchemaSummary(schema);

        // Generate configuration
        var generationOptions = new GenerationOptions
        {
            DataSourceId = settings.DataSourceId ?? "db",
            IncludeDataSourceBlock = !settings.LayersOnly,
            IncludeServiceBlocks = !settings.LayersOnly && settings.IncludeServices,
            EnabledServices = settings.Services?.Split(',').Select(s => s.Trim()).ToHashSet() ?? new() { "odata" },
            UseEnvironmentVariable = settings.UseEnvVar,
            ConnectionStringEnvVar = settings.EnvVarName ?? "DATABASE_URL",
            IncludeConnectionPool = settings.IncludeConnectionPool,
            GenerateExplicitFields = settings.ExplicitFields
        };

        string configContent;
        try
        {
            configContent = await Task.Run(() =>
                ConfigurationGenerator.GenerateConfiguration(schema, generationOptions));
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error generating configuration: {ex.Message}[/]");
            return 1;
        }

        // Write to output file
        var outputPath = settings.Output ?? "layers.hcl";
        try
        {
            if (File.Exists(outputPath) && !settings.Force)
            {
                _console.MarkupLine($"[yellow]⚠ File already exists: {outputPath}[/]");
                _console.MarkupLine("[dim]Use --force to overwrite[/]");
                return 1;
            }

            await File.WriteAllTextAsync(outputPath, configContent);
            _console.WriteLine();
            _console.MarkupLine($"[green]✓ Configuration generated:[/] {outputPath}");
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error writing file: {ex.Message}[/]");
            return 1;
        }

        // Show next steps
        DisplayNextSteps(outputPath);

        return 0;
    }

    private void DisplaySchemaSummary(DatabaseSchema schema)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .AddColumn(new TableColumn("[bold]Property[/]"))
            .AddColumn(new TableColumn("[bold]Value[/]"));

        table.AddRow("Database", $"[cyan]{schema.DatabaseName}[/]");
        table.AddRow("Provider", $"[cyan]{schema.Provider}[/]");
        table.AddRow("Tables Found", $"[cyan]{schema.Tables.Count}[/]");

        var tablesWithGeometry = schema.Tables.Count(t => t.GeometryColumn != null);
        if (tablesWithGeometry > 0)
        {
            table.AddRow("Tables with Geometry", $"[green]{tablesWithGeometry}[/]");
        }

        _console.Write(new Panel(table)
            .Header("[bold blue]Schema Summary[/]")
            .BorderColor(Color.Blue));
        _console.WriteLine();

        // List tables
        if (schema.Tables.Count > 0)
        {
            var tablesTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Green)
                .AddColumn(new TableColumn("[bold]Table[/]"))
                .AddColumn(new TableColumn("[bold]Rows[/]"))
                .AddColumn(new TableColumn("[bold]Columns[/]"))
                .AddColumn(new TableColumn("[bold]Geometry[/]"));

            foreach (var tbl in schema.Tables.Take(20)) // Limit display to first 20
            {
                var rowCount = tbl.RowCount.HasValue ? tbl.RowCount.Value.ToString("N0") : "[dim]unknown[/]";
                var geometryInfo = tbl.GeometryColumn != null
                    ? $"[green]{tbl.GeometryColumn.GeometryType}[/]"
                    : "[dim]none[/]";

                tablesTable.AddRow(
                    $"[cyan]{tbl.FullyQualifiedName}[/]",
                    rowCount,
                    tbl.Columns.Count.ToString(),
                    geometryInfo);
            }

            if (schema.Tables.Count > 20)
            {
                tablesTable.AddRow("[dim]...[/]", $"[dim]({schema.Tables.Count - 20} more)[/]", "", "");
            }

            _console.Write(new Panel(tablesTable)
                .Header("[bold green]Tables[/]")
                .BorderColor(Color.Green));
            _console.WriteLine();
        }
    }

    private void DisplayNextSteps(string outputPath)
    {
        var panel = new Panel(new Markup(
            $"""
            [bold]Next Steps:[/]

            1. [cyan]Review the generated configuration:[/]
               {outputPath}

            2. [cyan]Edit as needed[/] (update service settings, layer titles, etc.)

            3. [cyan]Validate the configuration:[/]
               [dim]honua config validate {outputPath}[/]

            4. [cyan]Preview what would be configured:[/]
               [dim]honua config plan {outputPath} --show-endpoints[/]

            5. [cyan]Integrate into your main configuration[/] or use directly
            """))
        {
            Header = new PanelHeader("[bold green]Configuration Generated Successfully[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };

        _console.Write(panel);
        _console.WriteLine();
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<connection-string>")]
        [Description("Database connection string")]
        public string? ConnectionString { get; init; }

        [CommandOption("--provider|-p <PROVIDER>")]
        [Description("Database provider (postgresql, sqlite). Auto-detected if not specified.")]
        public string? Provider { get; init; }

        [CommandOption("--output|-o <PATH>")]
        [Description("Output file path (default: layers.hcl)")]
        public string? Output { get; init; }

        [CommandOption("--data-source-id <ID>")]
        [Description("Data source ID to use in generated config (default: db)")]
        public string? DataSourceId { get; init; }

        [CommandOption("--table-pattern <PATTERN>")]
        [Description("SQL LIKE pattern to filter tables (e.g., 'roads%')")]
        public string? TablePattern { get; init; }

        [CommandOption("--schema-name <SCHEMA>")]
        [Description("Filter tables by schema name (PostgreSQL only)")]
        public string? SchemaName { get; init; }

        [CommandOption("--include-system-tables")]
        [Description("Include system tables")]
        public bool IncludeSystemTables { get; init; }

        [CommandOption("--no-row-counts")]
        [Description("Skip counting rows (faster for large databases)")]
        public bool NoRowCounts { get; init; }

        public bool IncludeRowCounts => !NoRowCounts;

        [CommandOption("--include-views")]
        [Description("Include views in addition to tables")]
        public bool IncludeViews { get; init; }

        [CommandOption("--max-tables <COUNT>")]
        [Description("Maximum number of tables to introspect")]
        public int? MaxTables { get; init; }

        [CommandOption("--layers-only")]
        [Description("Generate only layer blocks (no data source or service blocks)")]
        public bool LayersOnly { get; init; }

        [CommandOption("--include-services")]
        [Description("Include service blocks in generated config")]
        [DefaultValue(true)]
        public bool IncludeServices { get; init; } = true;

        [CommandOption("--services <SERVICES>")]
        [Description("Comma-separated list of services to enable (default: odata)")]
        public string? Services { get; init; }

        [CommandOption("--use-env-var")]
        [Description("Use environment variable for connection string")]
        [DefaultValue(true)]
        public bool UseEnvVar { get; init; } = true;

        [CommandOption("--env-var-name <NAME>")]
        [Description("Environment variable name for connection string (default: DATABASE_URL)")]
        public string? EnvVarName { get; init; }

        [CommandOption("--include-connection-pool")]
        [Description("Include connection pool configuration")]
        [DefaultValue(true)]
        public bool IncludeConnectionPool { get; init; } = true;

        [CommandOption("--explicit-fields")]
        [Description("Generate explicit field definitions instead of introspect_fields = true")]
        public bool ExplicitFields { get; init; }

        [CommandOption("--force|-f")]
        [Description("Overwrite existing file")]
        public bool Force { get; init; }
    }
}
