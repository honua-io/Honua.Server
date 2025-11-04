// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Initialize a new Honua project with scaffolded configuration files
/// </summary>
public sealed class InitProjectCommand : AsyncCommand<InitProjectCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;

    public InitProjectCommand(IAnsiConsole console, IHonuaCliEnvironment environment)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var targetPath = Path.GetFullPath(settings.Path ?? Directory.GetCurrentDirectory());

        _console.MarkupLine($"[bold]Initializing Honua project in:[/] {targetPath}");
        _console.WriteLine();

        // Check if directory exists and is empty
        if (Directory.Exists(targetPath))
        {
            var files = Directory.GetFiles(targetPath, "*", SearchOption.TopDirectoryOnly);
            var dirs = Directory.GetDirectories(targetPath);

            if ((files.Length > 0 || dirs.Length > 0) && !settings.Force)
            {
                _console.MarkupLine("[yellow]Directory is not empty. Use --force to initialize anyway.[/]");
                return 1;
            }
        }
        else
        {
            Directory.CreateDirectory(targetPath);
        }

        // Prompt for project configuration
        var projectName = settings.ProjectName ?? _console.Prompt(
            new TextPrompt<string>("Project name:")
                .DefaultValue(Path.GetFileName(targetPath)));
        var databaseType = settings.DatabaseType ?? PromptDatabaseType();
        var includeDocker = settings.IncludeDocker ?? _console.Confirm("Include Docker Compose setup?", true);
        var includeSample = settings.IncludeSampleData ?? _console.Confirm("Include sample dataset?", true);

        await _console.Status()
            .StartAsync("Creating project files...", async ctx =>
            {
                // Create directory structure
                ctx.Status("Creating directories...");
                Directory.CreateDirectory(Path.Combine(targetPath, "metadata"));
                Directory.CreateDirectory(Path.Combine(targetPath, "data"));
                Directory.CreateDirectory(Path.Combine(targetPath, "config"));

                // Create metadata.yaml
                ctx.Status("Generating metadata.yaml...");
                await CreateMetadataYaml(targetPath, projectName, databaseType);

                // Create appsettings.json
                ctx.Status("Generating appsettings.json...");
                await CreateAppSettings(targetPath, databaseType);

                // Create .env file
                ctx.Status("Generating .env file...");
                await CreateEnvFile(targetPath, databaseType);

                // Create Docker Compose if requested
                if (includeDocker)
                {
                    ctx.Status("Generating docker-compose.yml...");
                    await CreateDockerCompose(targetPath, databaseType);
                }

                // Create README
                ctx.Status("Generating README.md...");
                await CreateReadme(targetPath, projectName, databaseType, includeDocker);

                // Create .gitignore
                ctx.Status("Generating .gitignore...");
                await CreateGitIgnore(targetPath);

                // Create sample data if requested
                if (includeSample)
                {
                    ctx.Status("Creating sample dataset...");
                    await CreateSampleDataset(targetPath);
                }
            });

        _console.WriteLine();
        _console.MarkupLine("[green]✓[/] Project initialized successfully!");
        _console.WriteLine();
        _console.MarkupLine("[bold]Next steps:[/]");
        _console.MarkupLine("  1. cd " + Path.GetFileName(targetPath));
        _console.MarkupLine("  2. Review and edit metadata/metadata.yaml");
        _console.MarkupLine("  3. Update connection strings in .env");

        if (includeDocker)
        {
            _console.MarkupLine("  4. Start services: [cyan]docker-compose up -d[/]");
            _console.MarkupLine("  5. Bootstrap auth: [cyan]honua auth bootstrap[/]");
            _console.MarkupLine("  6. Validate metadata: [cyan]honua metadata validate[/]");
        }
        else
        {
            _console.MarkupLine("  4. Configure your database connection");
            _console.MarkupLine("  5. Bootstrap auth: [cyan]honua auth bootstrap[/]");
            _console.MarkupLine("  6. Validate metadata: [cyan]honua metadata validate[/]");
        }

        return 0;
    }

    private string PromptDatabaseType()
    {
        return _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Select database type:")
                .AddChoices("PostgreSQL (PostGIS)", "SQLite", "SQL Server", "MySQL"));
    }

    private async Task CreateMetadataYaml(string targetPath, string projectName, string dbType)
    {
        var yaml = new StringBuilder();
        yaml.AppendLine("# Honua Metadata Configuration");
        yaml.AppendLine($"# Project: {projectName}");
        yaml.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd}");
        yaml.AppendLine();
        yaml.AppendLine("catalog:");
        yaml.AppendLine($"  id: {SanitizeId(projectName)}");
        yaml.AppendLine($"  title: {projectName}");
        yaml.AppendLine("  description: Geospatial data catalog");
        yaml.AppendLine();
        yaml.AppendLine("folders:");
        yaml.AppendLine("  - id: services");
        yaml.AppendLine("    title: Services");
        yaml.AppendLine();
        yaml.AppendLine("dataSources:");
        yaml.AppendLine($"  - id: {GetDbId(dbType)}");
        yaml.AppendLine($"    provider: {GetDbProvider(dbType)}");
        yaml.AppendLine("    connectionString: ${DATABASE_CONNECTION}");
        yaml.AppendLine();
        yaml.AppendLine("services:");
        yaml.AppendLine("  - id: example-service");
        yaml.AppendLine("    title: Example Service");
        yaml.AppendLine("    folderId: services");
        yaml.AppendLine("    serviceType: feature");
        yaml.AppendLine($"    dataSourceId: {GetDbId(dbType)}");
        yaml.AppendLine("    enabled: true");
        yaml.AppendLine("    ogc:");
        yaml.AppendLine("      collectionsEnabled: true");
        yaml.AppendLine("      wfsEnabled: false");
        yaml.AppendLine("      exportFormats:");
        yaml.AppendLine("        geoJsonEnabled: true");
        yaml.AppendLine("        htmlEnabled: true");
        yaml.AppendLine();
        yaml.AppendLine("layers:");
        yaml.AppendLine("  - id: example-layer");
        yaml.AppendLine("    serviceId: example-service");
        yaml.AppendLine("    title: Example Layer");
        yaml.AppendLine("    geometryType: Point");
        yaml.AppendLine("    idField: id");
        yaml.AppendLine("    geometryField: geometry");
        yaml.AppendLine("    crs:");
        yaml.AppendLine("      - EPSG:4326");
        yaml.AppendLine("    fields:");
        yaml.AppendLine("      - name: id");
        yaml.AppendLine("        dataType: int64");
        yaml.AppendLine("        storageType: INTEGER");
        yaml.AppendLine("      - name: name");
        yaml.AppendLine("        dataType: string");
        yaml.AppendLine("        storageType: TEXT");

        await File.WriteAllTextAsync(Path.Combine(targetPath, "metadata", "metadata.yaml"), yaml.ToString());
    }

    private async Task CreateAppSettings(string targetPath, string dbType)
    {
        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine("  \"Logging\": {");
        json.AppendLine("    \"LogLevel\": {");
        json.AppendLine("      \"Default\": \"Information\",");
        json.AppendLine("      \"Microsoft.AspNetCore\": \"Warning\"");
        json.AppendLine("    }");
        json.AppendLine("  },");
        json.AppendLine("  \"AllowedHosts\": \"*\",");
        json.AppendLine("  \"Honua\": {");
        json.AppendLine("    \"WorkspacePath\": \"./metadata\",");
        json.AppendLine("    \"Authentication\": {");
        json.AppendLine("      \"Mode\": \"Local\",");
        json.AppendLine("      \"Local\": {");
        json.AppendLine("        \"SessionLifetime\": \"08:00:00\",");
        json.AppendLine("        \"RequireEmailVerification\": false");
        json.AppendLine("      }");
        json.AppendLine("    }");
        json.AppendLine("  }");
        json.AppendLine("}");

        await File.WriteAllTextAsync(Path.Combine(targetPath, "config", "appsettings.json"), json.ToString());
    }

    private async Task CreateEnvFile(string targetPath, string dbType)
    {
        var env = new StringBuilder();
        env.AppendLine("# Honua Environment Configuration");
        env.AppendLine("# DO NOT commit this file to version control!");
        env.AppendLine();
        env.AppendLine($"# Database Connection ({dbType})");
        env.AppendLine($"DATABASE_CONNECTION={GetDefaultConnectionString(dbType)}");
        env.AppendLine();
        env.AppendLine("# JWT Signing Key (generate with: openssl rand -base64 32)");
        env.AppendLine("JWT_SIGNING_KEY=CHANGE_ME_TO_RANDOM_VALUE");
        env.AppendLine();
        env.AppendLine("# CORS Origins (comma-separated)");
        env.AppendLine("CORS_ORIGINS=http://localhost:3000,http://localhost:5173");

        await File.WriteAllTextAsync(Path.Combine(targetPath, ".env"), env.ToString());
    }

    private async Task CreateDockerCompose(string targetPath, string dbType)
    {
        var yaml = new StringBuilder();
        yaml.AppendLine("version: '3.8'");
        yaml.AppendLine();
        yaml.AppendLine("services:");
        yaml.AppendLine("  honua:");
        yaml.AppendLine("    image: honua/honua-server:latest");
        yaml.AppendLine("    ports:");
        yaml.AppendLine("      - \"5000:8080\"");
        yaml.AppendLine("    volumes:");
        yaml.AppendLine("      - ./metadata:/app/metadata:ro");
        yaml.AppendLine("      - ./config:/app/config:ro");
        yaml.AppendLine("      - ./data:/app/data");
        yaml.AppendLine("    environment:");
        yaml.AppendLine("      - ASPNETCORE_ENVIRONMENT=Production");
        yaml.AppendLine("      - DATABASE_CONNECTION=${DATABASE_CONNECTION}");
        yaml.AppendLine("    depends_on:");

        if (dbType.Contains("PostgreSQL"))
        {
            yaml.AppendLine("      - postgres");
            yaml.AppendLine();
            yaml.AppendLine("  postgres:");
            yaml.AppendLine("    image: postgis/postgis:16-3.4");
            yaml.AppendLine("    environment:");
            yaml.AppendLine("      POSTGRES_DB: honua");
            yaml.AppendLine("      POSTGRES_USER: honua");
            yaml.AppendLine("      POSTGRES_PASSWORD: honua_dev");
            yaml.AppendLine("    ports:");
            yaml.AppendLine("      - \"5432:5432\"");
            yaml.AppendLine("    volumes:");
            yaml.AppendLine("      - postgres_data:/var/lib/postgresql/data");
            yaml.AppendLine();
            yaml.AppendLine("volumes:");
            yaml.AppendLine("  postgres_data:");
        }
        else
        {
            yaml.AppendLine("      - db");
        }

        await File.WriteAllTextAsync(Path.Combine(targetPath, "docker-compose.yml"), yaml.ToString());
    }

    private async Task CreateReadme(string targetPath, string projectName, string dbType, bool includeDocker)
    {
        var md = new StringBuilder();
        md.AppendLine($"# {projectName}");
        md.AppendLine();
        md.AppendLine("Honua geospatial server project.");
        md.AppendLine();
        md.AppendLine("## Quick Start");
        md.AppendLine();

        if (includeDocker)
        {
            md.AppendLine("### Using Docker Compose");
            md.AppendLine();
            md.AppendLine("1. Start services:");
            md.AppendLine("   ```bash");
            md.AppendLine("   docker-compose up -d");
            md.AppendLine("   ```");
            md.AppendLine();
        }

        md.AppendLine("2. Bootstrap authentication:");
        md.AppendLine("   ```bash");
        md.AppendLine("   honua auth bootstrap");
        md.AppendLine("   ```");
        md.AppendLine();
        md.AppendLine("3. Validate metadata:");
        md.AppendLine("   ```bash");
        md.AppendLine("   honua metadata validate");
        md.AppendLine("   ```");
        md.AppendLine();
        md.AppendLine("4. Access the server:");
        md.AppendLine("   - API: http://localhost:5000");
        md.AppendLine("   - OGC Landing Page: http://localhost:5000/ogc");
        md.AppendLine();
        md.AppendLine("## Configuration");
        md.AppendLine();
        md.AppendLine("- **Metadata**: Edit `metadata/metadata.yaml`");
        md.AppendLine("- **Connection**: Update `.env` file");
        md.AppendLine("- **Settings**: Modify `config/appsettings.json`");
        md.AppendLine();
        md.AppendLine("## Documentation");
        md.AppendLine();
        md.AppendLine("See the [Honua documentation](https://docs.honua.io) for more information.");

        await File.WriteAllTextAsync(Path.Combine(targetPath, "README.md"), md.ToString());
    }

    private async Task CreateGitIgnore(string targetPath)
    {
        var content = @".env
*.user
*.suo
bin/
obj/
data/auth/
data/cache/
*.log
.vs/
.vscode/
.idea/
";
        await File.WriteAllTextAsync(Path.Combine(targetPath, ".gitignore"), content);
    }

    private async Task CreateSampleDataset(string targetPath)
    {
        var sql = new StringBuilder();
        sql.AppendLine("-- Sample dataset for Honua");
        sql.AppendLine("-- Run this script in your database to create sample data");
        sql.AppendLine();
        sql.AppendLine("CREATE TABLE IF NOT EXISTS example_layer (");
        sql.AppendLine("    id SERIAL PRIMARY KEY,");
        sql.AppendLine("    name TEXT NOT NULL,");
        sql.AppendLine("    geometry GEOMETRY(Point, 4326)");
        sql.AppendLine(");");
        sql.AppendLine();
        sql.AppendLine("INSERT INTO example_layer (name, geometry) VALUES");
        sql.AppendLine("    ('Point A', ST_SetSRID(ST_MakePoint(-122.4194, 37.7749), 4326)),");
        sql.AppendLine("    ('Point B', ST_SetSRID(ST_MakePoint(-118.2437, 34.0522), 4326)),");
        sql.AppendLine("    ('Point C', ST_SetSRID(ST_MakePoint(-87.6298, 41.8781), 4326));");

        await File.WriteAllTextAsync(Path.Combine(targetPath, "data", "sample_data.sql"), sql.ToString());
    }

    private string SanitizeId(string input) =>
        input.ToLowerInvariant().Replace(" ", "-").Replace("_", "-");

    private string GetDbId(string dbType) => dbType.Contains("PostgreSQL") ? "postgres" :
        dbType.Contains("SQLite") ? "sqlite" :
        dbType.Contains("SQL Server") ? "sqlserver" : "mysql";

    private string GetDbProvider(string dbType) => dbType.Contains("PostgreSQL") ? "postgis" :
        dbType.Contains("SQLite") ? "sqlite" :
        dbType.Contains("SQL Server") ? "sqlserver" : "mysql";

    private string GetDefaultConnectionString(string dbType) => dbType.Contains("PostgreSQL") ?
        "Host=localhost;Database=honua;Username=honua;Password=CHANGE_ME" :
        dbType.Contains("SQLite") ? "Data Source=./data/honua.db" :
        dbType.Contains("SQL Server") ? "Server=localhost;Database=honua;User Id=honua;Password=CHANGE_ME;TrustServerCertificate=true" :
        "Server=localhost;Database=honua;Uid=honua;Pwd=CHANGE_ME";

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[PATH]")]
        [Description("Target directory for the new project (defaults to current directory)")]
        public string? Path { get; init; }

        [CommandOption("--name <NAME>")]
        [Description("Project name (prompted if not provided)")]
        public string? ProjectName { get; init; }

        [CommandOption("--database <TYPE>")]
        [Description("Database type: PostgreSQL, SQLite, SQLServer, MySQL (prompted if not provided)")]
        public string? DatabaseType { get; init; }

        [CommandOption("--docker")]
        [Description("Include Docker Compose configuration")]
        public bool? IncludeDocker { get; init; }

        [CommandOption("--sample-data")]
        [Description("Include sample dataset")]
        public bool? IncludeSampleData { get; init; }

        [CommandOption("--force")]
        [Description("Initialize even if directory is not empty")]
        public bool Force { get; init; }
    }
}
