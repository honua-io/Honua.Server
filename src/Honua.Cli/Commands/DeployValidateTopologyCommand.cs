// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.Services;
using Honua.Server.Core.Performance;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to validate a deployment topology configuration.
/// </summary>
public sealed class DeployValidateTopologyCommand : AsyncCommand<DeployValidateTopologyCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;

    public DeployValidateTopologyCommand(
        IAnsiConsole console,
        IHonuaCliEnvironment environment)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            _console.MarkupLine("[bold cyan]HonuaIO Topology Validator[/]");
            _console.WriteLine();

            // Load topology
            DeploymentTopology topology;
            if (settings.TopologyFile.HasValue())
            {
                _console.MarkupLine($"[dim]Loading topology from {settings.TopologyFile}...[/]");
                var json = await File.ReadAllTextAsync(settings.TopologyFile);
                topology = JsonSerializer.Deserialize<DeploymentTopology>(json, JsonSerializerOptionsRegistry.DevTooling)
                    ?? throw new InvalidOperationException("Failed to deserialize topology file");
            }
            else
            {
                _console.MarkupLine("[red]Error: No topology file specified. Use --topology to specify a file.[/]");
                return 1;
            }

            _console.WriteLine();
            _console.Write(new Rule("[bold]Validation Results[/]").LeftJustified());
            _console.WriteLine();

            // Validate topology
            var validationResults = ValidateTopology(topology);

            // Display results
            var hasErrors = false;
            var hasWarnings = false;

            foreach (var result in validationResults)
            {
                var icon = result.Severity switch
                {
                    ValidationSeverity.Error => "[red]✗[/]",
                    ValidationSeverity.Warning => "[yellow]⚠[/]",
                    _ => "[green]✓[/]"
                };

                _console.MarkupLine($"{icon} [[{result.Category.EscapeMarkup()}]] {result.Message.EscapeMarkup()}");

                if (result.Severity == ValidationSeverity.Error)
                    hasErrors = true;
                if (result.Severity == ValidationSeverity.Warning)
                    hasWarnings = true;
            }

            _console.WriteLine();

            if (!hasErrors && !hasWarnings)
            {
                _console.MarkupLine("[green]✓ Topology is valid! No issues found.[/]");
                return 0;
            }
            else if (hasErrors)
            {
                _console.MarkupLine($"[red]✗ Topology validation failed with {validationResults.Count(r => r.Severity == ValidationSeverity.Error)} error(s).[/]");
                return 1;
            }
            else
            {
                _console.MarkupLine($"[yellow]⚠ Topology is valid with {validationResults.Count(r => r.Severity == ValidationSeverity.Warning)} warning(s).[/]");
                return settings.WarningsAsErrors ? 1 : 0;
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            if (settings.Verbose)
            {
                _console.WriteException(ex);
            }
            return 1;
        }
    }

    private System.Collections.Generic.List<ValidationResult> ValidateTopology(DeploymentTopology topology)
    {
        var results = new System.Collections.Generic.List<ValidationResult>();

        // Validate cloud provider
        if (!new[] { "aws", "azure", "gcp", "kubernetes", "docker" }.Contains(topology.CloudProvider?.ToLowerInvariant()))
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                "CloudProvider",
                $"Invalid cloud provider '{topology.CloudProvider}'. Must be: aws, azure, gcp, kubernetes, or docker"));
        }

        // Validate region
        if (topology.Region.IsNullOrWhiteSpace())
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                "Region",
                "Region is required"));
        }

        // Validate environment
        if (!new[] { "dev", "staging", "prod" }.Contains(topology.Environment?.ToLowerInvariant()))
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Warning,
                "Environment",
                $"Non-standard environment '{topology.Environment}'. Recommended: dev, staging, or prod"));
        }

        // Validate database config
        if (topology.Database != null)
        {
            if (topology.Database.StorageGB < 10)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Database",
                    $"Database storage ({topology.Database.StorageGB}GB) is very small. Minimum recommended: 20GB"));
            }

            if (topology.Environment == "prod" && !topology.Database.HighAvailability)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Database",
                    "High availability is recommended for production databases"));
            }
        }

        // Validate compute config
        if (topology.Compute != null)
        {
            if (topology.Compute.InstanceCount < 1)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    "Compute",
                    "At least 1 compute instance is required"));
            }

            if (topology.Environment == "prod" && topology.Compute.InstanceCount < 2)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Compute",
                    "Multiple instances recommended for production high availability"));
            }

            if (topology.Environment == "prod" && !topology.Compute.AutoScaling)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Compute",
                    "Auto-scaling is recommended for production environments"));
            }
        }
        else
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                "Compute",
                "Compute configuration is required"));
        }

        // Validate storage config
        if (topology.Storage != null)
        {
            var totalStorage = topology.Storage.AttachmentStorageGB + topology.Storage.RasterCacheGB;
            if (totalStorage > 10000)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Storage",
                    $"Total storage allocation ({totalStorage}GB) is very large. Verify this is intentional."));
            }

            if (topology.Environment == "prod" && topology.Storage.Replication == "single-region")
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Storage",
                    "Cross-region replication recommended for production data durability"));
            }
        }

        // Validate networking
        if (topology.Networking != null)
        {
            if (topology.Compute?.InstanceCount > 1 && !topology.Networking.LoadBalancer)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Networking",
                    "Load balancer recommended when running multiple compute instances"));
            }

            if (topology.Environment == "prod" && topology.Networking.PublicAccess && !topology.Networking.VpnRequired)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Security",
                    "Consider requiring VPN access for production environments"));
            }
        }

        // Validate monitoring
        if (topology.Monitoring == null)
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Warning,
                "Monitoring",
                "Monitoring configuration not specified. Observability is recommended for all environments."));
        }
        else if (topology.Environment == "prod")
        {
            if (!topology.Monitoring.EnableMetrics)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Monitoring",
                    "Metrics collection is recommended for production"));
            }

            if (!topology.Monitoring.EnableLogs)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Monitoring",
                    "Log collection is recommended for production"));
            }
        }

        // If no issues found, add success message
        if (results.Count == 0)
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Info,
                "Overall",
                "Topology configuration is valid"));
        }

        return results;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--topology <FILE>")]
        [Description("Path to topology JSON file")]
        public string? TopologyFile { get; init; }

        [CommandOption("--warnings-as-errors")]
        [Description("Treat warnings as errors")]
        public bool WarningsAsErrors { get; init; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        public bool Verbose { get; init; }
    }

    private sealed record ValidationResult(ValidationSeverity Severity, string Category, string Message);

    private enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }
}
