// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command for initializing new Honua Configuration 2.0 files from templates.
/// Usage: honua config init:v2 [options]
/// </summary>
public sealed class ConfigInitV2Command : AsyncCommand<ConfigInitV2Command.Settings>
{
    private readonly IAnsiConsole _console;

    public ConfigInitV2Command(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var outputPath = settings.Output ?? "honua.config.hcl";

        // Check if file already exists
        if (File.Exists(outputPath) && !settings.Force)
        {
            _console.MarkupLine($"[red]Error: File already exists: {outputPath}[/]");
            _console.MarkupLine("[dim]Use --force to overwrite[/]");
            return 1;
        }

        // Determine template
        var template = settings.Template?.ToLowerInvariant() ?? "minimal";

        _console.MarkupLine($"[blue]Initializing configuration with template:[/] [cyan]{template}[/]");
        _console.WriteLine();

        // Generate configuration content
        string content = template switch
        {
            "minimal" or "basic" => GenerateMinimalTemplate(),
            "production" or "prod" => GenerateProductionTemplate(),
            "test" => GenerateTestTemplate(),
            "multi-service" or "multiservice" => GenerateMultiServiceTemplate(),
            _ => throw new InvalidOperationException($"Unknown template: {template}")
        };

        // Write file
        try
        {
            await File.WriteAllTextAsync(outputPath, content);
            _console.MarkupLine($"[green]✓ Created configuration file:[/] {outputPath}");
            _console.WriteLine();

            // Show next steps
            DisplayNextSteps(outputPath, template);

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error writing file: {ex.Message}[/]");
            return 1;
        }
    }

    private string GenerateMinimalTemplate()
    {
        return @"# Honua Configuration 2.0 - Minimal Development Setup
# Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + @" UTC

honua {
  version     = ""1.0""
  environment = ""development""
  log_level   = ""information""

  cors {
    allow_any_origin = true
  }
}

# SQLite data source for local development
data_source ""local_sqlite"" {
  provider   = ""sqlite""
  connection = ""Data Source=./dev.db""
}

# Enable OData service
service ""odata"" {
  enabled       = true
  allow_writes  = true
  max_page_size = 1000
}

# Example layer - update with your actual table
layer ""test_features"" {
  title            = ""Test Features""
  data_source      = data_source.local_sqlite
  table            = ""features""
  id_field         = ""id""
  display_field    = ""name""
  introspect_fields = true

  geometry {
    column = ""geom""
    type   = ""Point""
    srid   = 4326
  }

  services = [service.odata]
}
";
    }

    private string GenerateProductionTemplate()
    {
        return @"# Honua Configuration 2.0 - Production Setup
# Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + @" UTC

honua {
  version     = ""1.0""
  environment = ""production""
  log_level   = ""warning""

  cors {
    allow_any_origin = false
    allowed_origins  = [""https://yourdomain.com""]
  }
}

# PostgreSQL data source (connection from environment variable)
data_source ""postgres_primary"" {
  provider   = ""postgresql""
  connection = env(""DATABASE_URL"")

  pool {
    min_size = 10
    max_size = 50
    timeout  = 30
  }

  health_check = ""SELECT 1""
}

# Redis cache for distributed scenarios
cache ""redis"" {
  enabled    = true
  connection = env(""REDIS_URL"")
}

# OData service (read-only in production)
service ""odata"" {
  enabled       = true
  allow_writes  = false
  max_page_size = 500
}

# OGC API Features service
service ""ogc_api"" {
  enabled     = true
  conformance = [""core"", ""geojson"", ""crs""]
}

# Example layer - update with your actual schema
layer ""your_layer"" {
  title            = ""Your Layer""
  data_source      = data_source.postgres_primary
  table            = ""public.your_table""
  id_field         = ""id""
  display_field    = ""name""
  introspect_fields = true

  geometry {
    column = ""geom""
    type   = ""Polygon""
    srid   = 3857
  }

  services = [service.odata, service.ogc_api]
}

# Rate limiting
rate_limit {
  enabled = true
  store   = ""redis""

  rules {
    default = {
      requests = 1000
      window   = ""1m""
    }

    authenticated = {
      requests = 10000
      window   = ""1m""
    }
  }
}
";
    }

    private string GenerateTestTemplate()
    {
        return @"# Honua Configuration 2.0 - Test Configuration
# Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + @" UTC

honua {
  version     = ""1.0""
  environment = ""test""
  log_level   = ""debug""
}

# In-memory SQLite for testing
data_source ""test_db"" {
  provider   = ""sqlite""
  connection = ""Data Source=:memory:""
}

# OData service for testing
service ""odata"" {
  enabled      = true
  allow_writes = true
}

# Test layer with explicit fields (no introspection for test predictability)
layer ""test_roads"" {
  title            = ""Test Roads""
  data_source      = data_source.test_db
  table            = ""roads_primary""
  id_field         = ""road_id""
  display_field    = ""name""
  introspect_fields = false

  geometry {
    column = ""geom""
    type   = ""LineString""
    srid   = 4326
  }

  fields {
    road_id    = { type = ""int"", nullable = false }
    name       = { type = ""string"", nullable = true }
    status     = { type = ""string"", nullable = true }
    created_at = { type = ""datetime"", nullable = false }
    geom       = { type = ""geometry"", nullable = false }
  }

  services = [service.odata]
}
";
    }

    private string GenerateMultiServiceTemplate()
    {
        return @"# Honua Configuration 2.0 - Multi-Service Setup
# Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + @" UTC

honua {
  version     = ""1.0""
  environment = ""development""
  log_level   = ""information""

  cors {
    allow_any_origin = true
  }
}

# PostgreSQL data source
data_source ""postgres"" {
  provider   = ""postgresql""
  connection = env(""DATABASE_URL"")

  pool {
    min_size = 5
    max_size = 20
  }
}

# Enable multiple OGC services
service ""odata"" {
  enabled       = true
  allow_writes  = true
  max_page_size = 1000
}

service ""ogc_api"" {
  enabled     = true
  conformance = [""core"", ""geojson"", ""crs"", ""filter""]
}

service ""wfs"" {
  enabled = true
  version = ""2.0.0""
}

service ""wms"" {
  enabled = true
  version = ""1.3.0""
}

# Example layer exposed through multiple services
layer ""parcels"" {
  title            = ""Land Parcels""
  data_source      = data_source.postgres
  table            = ""public.parcels""
  id_field         = ""parcel_id""
  display_field    = ""parcel_number""
  introspect_fields = true

  geometry {
    column = ""geom""
    type   = ""Polygon""
    srid   = 3857
  }

  # Expose via all services
  services = [
    service.odata,
    service.ogc_api,
    service.wfs,
    service.wms
  ]
}

layer ""roads"" {
  title            = ""Road Network""
  data_source      = data_source.postgres
  table            = ""public.roads""
  id_field         = ""road_id""
  display_field    = ""name""
  introspect_fields = true

  geometry {
    column = ""geom""
    type   = ""LineString""
    srid   = 3857
  }

  services = [service.odata, service.ogc_api]
}

# Rate limiting
rate_limit {
  enabled = true
  store   = ""memory""

  rules {
    default = {
      requests = 1000
      window   = ""1m""
    }
  }
}
";
    }

    private void DisplayNextSteps(string outputPath, string template)
    {
        var panel = new Panel(new Markup(
            $"""
            [bold]Next Steps:[/]

            1. [cyan]Edit the configuration file[/] to match your database schema:
               {outputPath}

            2. [cyan]Validate your configuration:[/]
               [dim]honua config validate {outputPath}[/]

            3. [cyan]Preview what would be configured:[/]
               [dim]honua config plan {outputPath} --show-endpoints[/]

            4. [cyan]Generate configuration from your database (optional):[/]
               [dim]honua config introspect ""<connection-string>"" --output layers.hcl[/]

            5. [cyan]Start your Honua server[/] with the new configuration
            """))
        {
            Header = new PanelHeader("[bold green]Configuration Created Successfully[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };

        _console.Write(panel);
        _console.WriteLine();

        // Template-specific tips
        if (template == "production" || template == "prod")
        {
            _console.MarkupLine("[yellow]⚠ Don't forget to set environment variables:[/]");
            _console.MarkupLine("   • [dim]DATABASE_URL[/] - PostgreSQL connection string");
            _console.MarkupLine("   • [dim]REDIS_URL[/] - Redis connection string");
            _console.WriteLine();
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--template|-t <TEMPLATE>")]
        [Description("Configuration template: minimal (default), production, test, multi-service")]
        [DefaultValue("minimal")]
        public string? Template { get; init; }

        [CommandOption("--output|-o <PATH>")]
        [Description("Output file path (default: honua.config.hcl)")]
        public string? Output { get; init; }

        [CommandOption("--force|-f")]
        [Description("Overwrite existing file")]
        public bool Force { get; init; }
    }
}
