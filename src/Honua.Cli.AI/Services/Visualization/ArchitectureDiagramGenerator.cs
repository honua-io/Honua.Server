// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Honua.Cli.AI.Services.Visualization;

/// <summary>
/// Generates ASCII architecture diagrams for cloud infrastructure.
/// </summary>
public sealed class ArchitectureDiagramGenerator
{
    public string GenerateDiagram(ArchitectureSpec spec)
    {
        return spec.Type switch
        {
            "serverless" => GenerateServerlessDiagram(spec),
            "kubernetes" => GenerateKubernetesDiagram(spec),
            "docker" => GenerateDockerDiagram(spec),
            "hybrid" => GenerateHybridDiagram(spec),
            _ => GenerateGenericDiagram(spec)
        };
    }

    private string GenerateServerlessDiagram(ArchitectureSpec spec)
    {
        var sb = new StringBuilder();
        var provider = spec.CloudProvider.ToLowerInvariant();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│                      Serverless Architecture                    │");
        sb.AppendLine($"│                           ({provider.ToUpper()})                               │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine("                         ┌──────────────┐");
        sb.AppendLine("    Internet ───────────▶│     CDN      │◀────── Global Edge Caching");
        sb.AppendLine("                         └──────┬───────┘");
        sb.AppendLine("                                │");
        sb.AppendLine("                         ┌──────▼───────┐");
        sb.AppendLine($"                         │ {GetComputeService(provider)} │◀────── Auto-scaling");
        sb.AppendLine("                         └──────┬───────┘");
        sb.AppendLine("                                │");
        sb.AppendLine("                ┌───────────────┼───────────────┐");
        sb.AppendLine("                │               │               │");
        sb.AppendLine("         ┌──────▼──────┐ ┌──────▼──────┐ ┌─────▼──────┐");
        sb.AppendLine($"         │  {GetDatabaseService(provider)}  │ │   Storage   │ │   Redis    │");
        sb.AppendLine("         │   (PostGIS) │ │   (Rasters) │ │   (Cache)  │");
        sb.AppendLine("         └─────────────┘ └─────────────┘ └────────────┘");
        sb.AppendLine();
        sb.AppendLine("Characteristics:");
        sb.AppendLine($"  • Auto-scaling: 0 to ∞ instances");
        sb.AppendLine($"  • Cold start: ~1-2s");
        sb.AppendLine($"  • Pay-per-request pricing");
        sb.AppendLine($"  • 99.95% SLA");

        return sb.ToString();
    }

    private string GenerateKubernetesDiagram(ArchitectureSpec spec)
    {
        var sb = new StringBuilder();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│                    Kubernetes Architecture                      │");
        sb.AppendLine($"│                           ({spec.CloudProvider})                              │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine("                         ┌──────────────┐");
        sb.AppendLine("    Internet ───────────▶│ Load Balancer│");
        sb.AppendLine("                         └──────┬───────┘");
        sb.AppendLine("                                │");
        sb.AppendLine("                         ┌──────▼───────┐");
        sb.AppendLine("                         │Ingress (Nginx│");
        sb.AppendLine("                         └──────┬───────┘");
        sb.AppendLine("                                │");
        sb.AppendLine("              ┌─────────────────┴─────────────────┐");
        sb.AppendLine("              │        Kubernetes Cluster          │");
        sb.AppendLine("              │                                    │");
        sb.AppendLine("              │  ┌────────────────────────────┐   │");
        sb.AppendLine("              │  │   Honua Pods (3 replicas)  │   │");
        sb.AppendLine("              │  └────────────────────────────┘   │");
        sb.AppendLine("              │              │                     │");
        sb.AppendLine("              │  ┌───────────┼───────────┐        │");
        sb.AppendLine("              │  │           │           │        │");
        sb.AppendLine("              │  ▼           ▼           ▼        │");
        sb.AppendLine("              │ ┌──────┐ ┌───────┐  ┌───────┐    │");
        sb.AppendLine("              │ │PostGIS│ │ Redis │  │ Storage│   │");
        sb.AppendLine("              │ │  Pod  │ │  Pod  │  │   PV   │   │");
        sb.AppendLine("              │ └──────┘ └───────┘  └───────┘    │");
        sb.AppendLine("              └────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine("Characteristics:");
        sb.AppendLine($"  • Horizontal Pod Autoscaler (HPA)");
        sb.AppendLine($"  • Self-healing & rolling updates");
        sb.AppendLine($"  • Multi-AZ deployment");
        sb.AppendLine($"  • Full control over resources");

        return sb.ToString();
    }

    private string GenerateDockerDiagram(ArchitectureSpec spec)
    {
        var sb = new StringBuilder();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│                  Docker Compose Architecture                    │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine("                      ┌──────────────┐");
        sb.AppendLine("  Internet ──────────▶│   VM / Host  │");
        sb.AppendLine("                      └──────┬───────┘");
        sb.AppendLine("                             │");
        sb.AppendLine("              ┌──────────────┴──────────────┐");
        sb.AppendLine("              │      Docker Network         │");
        sb.AppendLine("              │                             │");
        sb.AppendLine("              │  ┌────────────────────┐     │");
        sb.AppendLine("              │  │  Nginx Container   │     │");
        sb.AppendLine("              │  │  (Reverse Proxy)   │     │");
        sb.AppendLine("              │  └─────────┬──────────┘     │");
        sb.AppendLine("              │            │                │");
        sb.AppendLine("              │  ┌─────────▼──────────┐     │");
        sb.AppendLine("              │  │ Honua Container    │     │");
        sb.AppendLine("              │  └─────────┬──────────┘     │");
        sb.AppendLine("              │            │                │");
        sb.AppendLine("              │  ┌─────────┼─────────┐      │");
        sb.AppendLine("              │  │         │         │      │");
        sb.AppendLine("              │  ▼         ▼         ▼      │");
        sb.AppendLine("              │┌────────┐┌──────┐┌───────┐  │");
        sb.AppendLine("              ││PostGIS ││Redis ││Volumes│  │");
        sb.AppendLine("              ││Container│Container│       │  │");
        sb.AppendLine("              │└────────┘└──────┘└───────┘  │");
        sb.AppendLine("              └─────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine("Characteristics:");
        sb.AppendLine($"  • Simple deployment & management");
        sb.AppendLine($"  • Local development parity");
        sb.AppendLine($"  • Single host (scale up)");
        sb.AppendLine($"  • Manual updates");

        return sb.ToString();
    }

    private string GenerateHybridDiagram(ArchitectureSpec spec)
    {
        var sb = new StringBuilder();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│              Hybrid Multi-Region Architecture                   │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine("                    ┌──────────────────┐");
        sb.AppendLine("  Internet ────────▶│  Global CDN      │◀────── Edge caching");
        sb.AppendLine("                    │  (CloudFront)    │        worldwide");
        sb.AppendLine("                    └─────────┬────────┘");
        sb.AppendLine("                              │");
        sb.AppendLine("              ┌───────────────┼───────────────┐");
        sb.AppendLine("              │               │               │");
        sb.AppendLine("     ┌────────▼───────┐ ┌─────▼──────┐ ┌─────▼──────┐");
        sb.AppendLine("     │  Region: US    │ │ Region: EU │ │Region: APAC│");
        sb.AppendLine("     │ ┌────────────┐ │ │┌──────────┐│ │┌──────────┐│");
        sb.AppendLine("     │ │  Compute   │ │ ││ Compute  ││ ││ Compute  ││");
        sb.AppendLine("     │ └─────┬──────┘ │ │└────┬─────┘│ │└────┬─────┘│");
        sb.AppendLine("     │       │        │ │     │      │ │     │      │");
        sb.AppendLine("     │ ┌─────▼──────┐ │ │┌────▼─────┐│ │┌────▼─────┐│");
        sb.AppendLine("     │ │Read Replica│ │ ││  Replica ││ ││  Replica ││");
        sb.AppendLine("     │ └────────────┘ │ │└──────────┘│ │└──────────┘│");
        sb.AppendLine("     └────────────────┘ └────────────┘ └────────────┘");
        sb.AppendLine("              │               │               │");
        sb.AppendLine("              └───────────────┼───────────────┘");
        sb.AppendLine("                              │");
        sb.AppendLine("                    ┌─────────▼────────┐");
        sb.AppendLine("                    │  Primary Database│");
        sb.AppendLine("                    │   (Multi-region) │");
        sb.AppendLine("                    └──────────────────┘");
        sb.AppendLine();
        sb.AppendLine("Characteristics:");
        sb.AppendLine($"  • Global low-latency access");
        sb.AppendLine($"  • 99.99% availability");
        sb.AppendLine($"  • Disaster recovery built-in");
        sb.AppendLine($"  • ~3x cost of single-region");

        return sb.ToString();
    }

    private string GenerateGenericDiagram(ArchitectureSpec spec)
    {
        var sb = new StringBuilder();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│                    Cloud Architecture                           │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine("    ┌──────────┐     ┌──────────┐     ┌──────────┐");
        sb.AppendLine("    │  Users   │────▶│   CDN    │────▶│   App    │");
        sb.AppendLine("    └──────────┘     └──────────┘     └─────┬────┘");
        sb.AppendLine("                                            │");
        sb.AppendLine("                                  ┌─────────┼─────────┐");
        sb.AppendLine("                                  │         │         │");
        sb.AppendLine("                           ┌──────▼──┐ ┌────▼────┐ ┌─▼──────┐");
        sb.AppendLine("                           │Database│ │ Storage │ │ Cache  │");
        sb.AppendLine("                           └─────────┘ └─────────┘ └────────┘");

        return sb.ToString();
    }

    private string GetComputeService(string provider)
    {
        return provider switch
        {
            "gcp" => "Cloud Run   ",
            "aws" => "AWS Fargate",
            "azure" => "Azure ACI  ",
            _ => "Compute    "
        };
    }

    private string GetDatabaseService(string provider)
    {
        return provider switch
        {
            "gcp" => "Cloud SQL",
            "aws" => "RDS      ",
            "azure" => "Azure DB ",
            _ => "Database "
        };
    }

    public string GenerateComparisonTable(List<ArchitectureOption> options)
    {
        var sb = new StringBuilder();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│                       Architecture Comparison                               │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // Calculate column widths
        var nameWidth = Math.Max(15, options.Max(o => o.Name.Length) + 2);
        var costWidth = 12;
        var complexityWidth = 12;
        var scaleWidth = 12;
        var opsWidth = 10;

        // Header
        sb.Append("┌");
        sb.Append(new string('─', nameWidth));
        sb.Append("┬");
        sb.Append(new string('─', costWidth));
        sb.Append("┬");
        sb.Append(new string('─', complexityWidth));
        sb.Append("┬");
        sb.Append(new string('─', scaleWidth));
        sb.Append("┬");
        sb.Append(new string('─', opsWidth));
        sb.AppendLine("┐");

        sb.Append("│ ");
        sb.Append("Option".PadRight(nameWidth - 2));
        sb.Append(" │ ");
        sb.Append("Cost/mo".PadRight(costWidth - 2));
        sb.Append(" │ ");
        sb.Append("Complexity".PadRight(complexityWidth - 2));
        sb.Append(" │ ");
        sb.Append("Scalability".PadRight(scaleWidth - 2));
        sb.Append(" │ ");
        sb.Append("Ops".PadRight(opsWidth - 2));
        sb.AppendLine(" │");

        sb.Append("├");
        sb.Append(new string('─', nameWidth));
        sb.Append("┼");
        sb.Append(new string('─', costWidth));
        sb.Append("┼");
        sb.Append(new string('─', complexityWidth));
        sb.Append("┼");
        sb.Append(new string('─', scaleWidth));
        sb.Append("┼");
        sb.Append(new string('─', opsWidth));
        sb.AppendLine("┤");

        // Rows
        foreach (var option in options)
        {
            sb.Append("│ ");
            sb.Append(TruncateOrPad(option.Name, nameWidth - 2));
            sb.Append(" │ ");
            sb.Append($"${option.EstimatedMonthlyCost:N0}".PadLeft(costWidth - 2));
            sb.Append(" │ ");
            sb.Append(RenderRating(option.ComplexityRating).PadRight(complexityWidth - 2));
            sb.Append(" │ ");
            sb.Append(RenderRating(option.ScalabilityRating).PadRight(scaleWidth - 2));
            sb.Append(" │ ");
            sb.Append(RenderRating(option.OperationalBurden).PadRight(opsWidth - 2));
            sb.AppendLine(" │");
        }

        sb.Append("└");
        sb.Append(new string('─', nameWidth));
        sb.Append("┴");
        sb.Append(new string('─', costWidth));
        sb.Append("┴");
        sb.Append(new string('─', complexityWidth));
        sb.Append("┴");
        sb.Append(new string('─', scaleWidth));
        sb.Append("┴");
        sb.Append(new string('─', opsWidth));
        sb.AppendLine("┘");

        sb.AppendLine();
        sb.AppendLine("Rating: ★★★★★ = 10/10 (best)  •  ★☆☆☆☆ = 2/10 (worst)");

        return sb.ToString();
    }

    private string RenderRating(int rating)
    {
        var stars = rating / 2; // Convert 0-10 to 0-5 stars
        var filled = new string('★', Math.Max(0, stars));
        var empty = new string('☆', Math.Max(0, 5 - stars));
        return filled + empty;
    }

    private string TruncateOrPad(string text, int width)
    {
        if (text.Length > width)
        {
            return text.Substring(0, width - 3) + "...";
        }
        return text.PadRight(width);
    }
}

public class ArchitectureSpec
{
    public string Type { get; set; } = string.Empty;
    public string CloudProvider { get; set; } = string.Empty;
    public List<string> Components { get; set; } = new();
}

public class ArchitectureOption
{
    public string Name { get; set; } = string.Empty;
    public decimal EstimatedMonthlyCost { get; set; }
    public int ComplexityRating { get; set; }
    public int ScalabilityRating { get; set; }
    public int OperationalBurden { get; set; }
}
