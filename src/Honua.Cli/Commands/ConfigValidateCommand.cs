// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration.V2.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command for validating Honua Configuration 2.0 files.
/// Usage: honua config validate [path] [options]
/// </summary>
public sealed class ConfigValidateCommand : AsyncCommand<ConfigValidateCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public ConfigValidateCommand(IAnsiConsole console)
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

        _console.MarkupLine($"[blue]Validating configuration:[/] {filePath}");
        _console.WriteLine();

        // Determine validation options
        var options = BuildValidationOptions(settings);

        // Run validation
        ValidationResult result;
        try
        {
            result = await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Validating configuration...", async ctx =>
                {
                    return await ConfigurationValidator.ValidateFileAsync(
                        filePath,
                        options,
                        connectionFactory: null, // TODO: Inject connection factory for runtime validation
                        CancellationToken.None);
                });
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error during validation: {ex.Message}[/]");
            return 1;
        }

        // Display results
        DisplayValidationResults(result, settings.Verbose);

        return result.IsValid ? 0 : 1;
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
                _console.MarkupLine($"[dim]Found configuration file: {searchPath}[/]");
                return fullPath;
            }
        }

        // Default to honua.config.hcl
        return Path.GetFullPath("honua.config.hcl");
    }

    private ValidationOptions BuildValidationOptions(Settings settings)
    {
        if (settings.SyntaxOnly)
        {
            return ValidationOptions.SyntaxOnly;
        }

        if (settings.Full)
        {
            return ValidationOptions.Full with
            {
                RuntimeValidationTimeoutSeconds = settings.Timeout
            };
        }

        return ValidationOptions.Default;
    }

    private void DisplayValidationResults(Honua.Server.Core.Configuration.V2.Validation.ValidationResult result, bool verbose)
    {
        if (result.IsValid && result.Warnings.Count == 0)
        {
            _console.MarkupLine("[green]✓ Configuration is valid[/]");
            _console.WriteLine();
            return;
        }

        // Display errors
        if (result.Errors.Count > 0)
        {
            _console.MarkupLine($"[red]✗ Validation failed with {result.Errors.Count} error(s)[/]");
            _console.WriteLine();

            foreach (var error in result.Errors)
            {
                DisplayError(error, verbose);
            }
        }

        // Display warnings
        if (result.Warnings.Count > 0)
        {
            _console.MarkupLine($"[yellow]⚠ {result.Warnings.Count} warning(s)[/]");
            _console.WriteLine();

            foreach (var warning in result.Warnings)
            {
                DisplayWarning(warning, verbose);
            }
        }

        // Final summary
        if (result.IsValid)
        {
            _console.MarkupLine("[green]✓ Configuration is valid (with warnings)[/]");
        }
        else
        {
            _console.MarkupLine("[red]Fix the errors above and try again.[/]");
        }

        _console.WriteLine();
    }

    private void DisplayError(ValidationError error, bool verbose)
    {
        var panel = new Panel(new Markup($"[red]{error.Message.EscapeMarkup()}[/]"))
        {
            Header = new PanelHeader($"[red]ERROR[/]{(error.Location != null ? $" at {error.Location}" : "")}"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Red)
        };

        _console.Write(panel);

        if (!string.IsNullOrWhiteSpace(error.Suggestion))
        {
            _console.MarkupLine($"  [dim]→ Suggestion: {error.Suggestion.EscapeMarkup()}[/]");
        }

        _console.WriteLine();
    }

    private void DisplayWarning(ValidationWarning warning, bool verbose)
    {
        var panel = new Panel(new Markup($"[yellow]{warning.Message.EscapeMarkup()}[/]"))
        {
            Header = new PanelHeader($"[yellow]WARNING[/]{(warning.Location != null ? $" at {warning.Location}" : "")}"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };

        _console.Write(panel);

        if (!string.IsNullOrWhiteSpace(warning.Suggestion))
        {
            _console.MarkupLine($"  [dim]→ Suggestion: {warning.Suggestion.EscapeMarkup()}[/]");
        }

        _console.WriteLine();
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Path to configuration file (default: honua.config.hcl)")]
        public string? Path { get; init; }

        [CommandOption("--syntax-only")]
        [Description("Validate syntax only (fast, no database checks)")]
        public bool SyntaxOnly { get; init; }

        [CommandOption("--full")]
        [Description("Full validation including runtime checks (database connectivity, table existence)")]
        public bool Full { get; init; }

        [CommandOption("--timeout <SECONDS>")]
        [Description("Timeout for runtime validation checks (default: 10)")]
        [DefaultValue(10)]
        public int Timeout { get; init; } = 10;

        [CommandOption("--verbose")]
        [Description("Show detailed validation information")]
        public bool Verbose { get; init; }
    }
}
