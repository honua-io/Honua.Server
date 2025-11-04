// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Generates visual diagrams for deployment plans:
/// - ASCII art architecture diagrams for terminal display
/// - Terraform graph (DOT format) for infrastructure visualization
/// - Honua metadata hierarchy trees
/// - Network topology diagrams
/// </summary>
public sealed class DiagramGeneratorAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<DiagramGeneratorAgent> _logger;

    public DiagramGeneratorAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<DiagramGeneratorAgent> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates ASCII architecture diagram for deployment plan.
    /// </summary>
    public async Task<string> GenerateAsciiArchitectureDiagramAsync(
        string deploymentDescription,
        string cloudProvider,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate an ASCII art diagram for this cloud deployment:

Deployment: {deploymentDescription}
Cloud Provider: {cloudProvider}

Create a clear, simple ASCII diagram showing:
1. Cloud provider boundary (box around everything)
2. Major services (compute, database, storage, networking)
3. Connections between services (arrows: ──→, ──┬, └──)
4. Resource names in boxes

Use box-drawing characters: ┌─┐│└┘├┤┬┴┼

Example format:
┌─────────────────────────────────────────────┐
│           AWS Cloud (us-east-1)             │
│                                             │
│  ┌──────────────┐      ┌─────────────────┐ │
│  │  ECS Cluster │──────│  RDS PostgreSQL │ │
│  │   (Honua)    │      │   (PostGIS)     │ │
│  └──────┬───────┘      └─────────────────┘ │
│         │                                   │
│  ┌──────▼───────┐      ┌─────────────────┐ │
│  │  S3 Bucket   │      │  CloudWatch     │ │
│  │  (Raster)    │      │  (Logs)         │ │
│  └──────────────┘      └─────────────────┘ │
└─────────────────────────────────────────────┘

Respond with ONLY the ASCII diagram, no explanations.";

        try
        {
            var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1500,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogError("LLM request failed");
            return GenerateFallbackDiagram(deploymentDescription, cloudProvider);
        }

            // Clean up response (remove markdown code blocks if present)
            var diagram = response.Content
                .Replace("```", "")
                .Replace("ascii", "")
                .Trim();

            return diagram;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate ASCII architecture diagram");
            return GenerateFallbackDiagram(deploymentDescription, cloudProvider);
        }
    }

    /// <summary>
    /// Generates network topology ASCII diagram.
    /// </summary>
    public async Task<string> GenerateNetworkTopologyDiagramAsync(
        string vpcConfig,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate an ASCII art network topology diagram:

Network Configuration: {vpcConfig}

Show:
1. VPC boundary
2. Public and private subnets
3. Internet Gateway / NAT Gateway
4. Security groups (firewall rules)
5. Load balancers
6. Route tables

Use box-drawing characters: ┌─┐│└┘├┤┬┴┼

Example format:
┌──────────────────────────────────────────────────────────┐
│                    VPC: 10.0.0.0/16                      │
│                                                          │
│  ┌────────────────────┐    ┌────────────────────┐      │
│  │ Public Subnet      │    │ Private Subnet     │      │
│  │ 10.0.1.0/24        │    │ 10.0.2.0/24        │      │
│  │                    │    │                    │      │
│  │  ┌──────────────┐  │    │  ┌──────────────┐ │      │
│  │  │ Load Balancer│  │    │  │  ECS Tasks   │ │      │
│  │  └──────┬───────┘  │    │  └──────────────┘ │      │
│  └─────────┼──────────┘    └────────────────────┘      │
│            │                                            │
│     ┌──────▼──────┐              ┌──────────────┐      │
│     │   Internet  │              │ NAT Gateway  │      │
│     │   Gateway   │              └──────────────┘      │
│     └─────────────┘                                    │
└──────────────────────────────────────────────────────────┘

Respond with ONLY the ASCII diagram.";

        try
        {
            var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1500,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogError("LLM request failed");
            return "Network topology diagram generation failed.";
        }

            return response.Content.Replace("```", "").Replace("ascii", "").Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate network topology diagram");
            return "Network topology diagram generation failed.";
        }
    }

    /// <summary>
    /// Generates Honua metadata hierarchy tree diagram.
    /// </summary>
    public string GenerateMetadataTreeDiagram(string metadataJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Honua Metadata Hierarchy:");
        sb.AppendLine();
        sb.AppendLine("📊 Catalog");
        sb.AppendLine("│");
        sb.AppendLine("├─📁 Folders");
        sb.AppendLine("│  └─ root/");
        sb.AppendLine("│");
        sb.AppendLine("├─🔌 DataSources");
        sb.AppendLine("│  └─ postgis_main (Npgsql)");
        sb.AppendLine("│     └─ Host: localhost, Database: honua");
        sb.AppendLine("│");
        sb.AppendLine("├─🌐 Services");
        sb.AppendLine("│  └─ parcels (FeatureServer)");
        sb.AppendLine("│     ├─ folderId: root");
        sb.AppendLine("│     ├─ dataSourceId: postgis_main");
        sb.AppendLine("│     └─ OGC Protocols:");
        sb.AppendLine("│        ├─ WFS: ✓ enabled");
        sb.AppendLine("│        ├─ WMS: ✓ enabled");
        sb.AppendLine("│        ├─ WMTS: ✗ disabled");
        sb.AppendLine("│        └─ OGC API: ✓ enabled");
        sb.AppendLine("│");
        sb.AppendLine("└─📍 Layers");
        sb.AppendLine("   └─ parcel_boundaries");
        sb.AppendLine("      ├─ serviceId: parcels");
        sb.AppendLine("      ├─ geometryType: polygon");
        sb.AppendLine("      ├─ idField: id");
        sb.AppendLine("      ├─ geometryField: geom");
        sb.AppendLine("      ├─ displayField: parcel_id");
        sb.AppendLine("      ├─ CRS: EPSG:4326, EPSG:3857");
        sb.AppendLine("      └─ Fields: 15 total");

        return sb.ToString();
    }

    /// <summary>
    /// Generates Terraform graph in DOT format.
    /// Requires terraform to be installed and terraform files to exist.
    /// </summary>
    public async Task<TerraformGraphResult> GenerateTerraformGraphAsync(
        string terraformDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(terraformDirectory))
        {
            return new TerraformGraphResult
            {
                Success = false,
                Message = $"Terraform directory not found: {terraformDirectory}"
            };
        }

        // Check if terraform is installed
        if (!IsTerraformInstalled())
        {
            return new TerraformGraphResult
            {
                Success = false,
                Message = "Terraform CLI not found. Install from https://terraform.io"
            };
        }

        try
        {
            // Run terraform init first (required for graph)
            var initResult = await RunTerraformCommandAsync(
                "init",
                terraformDirectory,
                cancellationToken);

            if (!initResult.Success)
            {
                return new TerraformGraphResult
                {
                    Success = false,
                    Message = $"Terraform init failed: {initResult.Output}"
                };
            }

            // Generate graph
            var graphResult = await RunTerraformCommandAsync(
                "graph",
                terraformDirectory,
                cancellationToken);

            if (!graphResult.Success)
            {
                return new TerraformGraphResult
                {
                    Success = false,
                    Message = $"Terraform graph failed: {graphResult.Output}"
                };
            }

            // Save DOT file
            var dotFilePath = Path.Combine(terraformDirectory, "terraform-graph.dot");
            await File.WriteAllTextAsync(dotFilePath, graphResult.Output, cancellationToken);

            var instructions = new StringBuilder();
            instructions.AppendLine("Terraform graph generated successfully!");
            instructions.AppendLine();
            instructions.AppendLine($"DOT file: {dotFilePath}");
            instructions.AppendLine();
            instructions.AppendLine("To visualize:");
            instructions.AppendLine("1. Install Graphviz: https://graphviz.org/download/");
            instructions.AppendLine("2. Generate SVG:");
            instructions.AppendLine($"   dot -Tsvg {dotFilePath} -o terraform-graph.svg");
            instructions.AppendLine("3. Generate PNG:");
            instructions.AppendLine($"   dot -Tpng {dotFilePath} -o terraform-graph.png");
            instructions.AppendLine();
            instructions.AppendLine("Or view online:");
            instructions.AppendLine("   https://dreampuf.github.io/GraphvizOnline/");

            return new TerraformGraphResult
            {
                Success = true,
                DotFilePath = dotFilePath,
                DotContent = graphResult.Output,
                Message = instructions.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Terraform graph");
            return new TerraformGraphResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generates deployment plan summary diagram (ASCII).
    /// </summary>
    public string GenerateDeploymentPlanDiagram(
        List<string> planSteps,
        string cloudProvider)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Deployment Plan: {cloudProvider}");
        sb.AppendLine();
        sb.AppendLine("┌────────────────────────────────────────┐");
        sb.AppendLine("│         Deployment Workflow            │");
        sb.AppendLine("└────────────────────────────────────────┘");
        sb.AppendLine("         │");

        for (int i = 0; i < planSteps.Count; i++)
        {
            var isLast = i == planSteps.Count - 1;
            var connector = isLast ? "└" : "├";
            var verticalLine = isLast ? " " : "│";

            sb.AppendLine($"         {connector}──► Step {i + 1}: {planSteps[i]}");
            if (!isLast)
            {
                sb.AppendLine($"         {verticalLine}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("         ▼");
        sb.AppendLine("   ┌────────────┐");
        sb.AppendLine("   │  Success!  │");
        sb.AppendLine("   └────────────┘");

        return sb.ToString();
    }

    private bool IsTerraformInstalled()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "terraform",
                    Arguments = "version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<CommandResult> RunTerraformCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "terraform",
                Arguments = command,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        var success = process.ExitCode == 0;
        var output = success ? outputBuilder.ToString() : errorBuilder.ToString();

        return new CommandResult
        {
            Success = success,
            Output = output,
            ExitCode = process.ExitCode
        };
    }

    private string GenerateFallbackDiagram(string description, string provider)
    {
        var sb = new StringBuilder();
        sb.AppendLine("┌─────────────────────────────────────────────┐");
        sb.AppendLine($"│     {provider} Cloud Deployment                  │");
        sb.AppendLine("│                                             │");
        sb.AppendLine("│  ┌──────────────┐      ┌─────────────────┐ │");
        sb.AppendLine("│  │   Compute    │──────│    Database     │ │");
        sb.AppendLine("│  │   (Honua)    │      │   (PostGIS)     │ │");
        sb.AppendLine("│  └──────┬───────┘      └─────────────────┘ │");
        sb.AppendLine("│         │                                   │");
        sb.AppendLine("│  ┌──────▼───────┐      ┌─────────────────┐ │");
        sb.AppendLine("│  │   Storage    │      │   Monitoring    │ │");
        sb.AppendLine("│  │   (Raster)   │      │    (Logs)       │ │");
        sb.AppendLine("│  └──────────────┘      └─────────────────┘ │");
        sb.AppendLine("└─────────────────────────────────────────────┘");
        return sb.ToString();
    }
}

// Supporting types

public sealed class TerraformGraphResult
{
    public bool Success { get; init; }
    public string? DotFilePath { get; init; }
    public string? DotContent { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class CommandResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public int ExitCode { get; init; }
}
