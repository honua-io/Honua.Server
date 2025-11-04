// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Security review agent that critiques generated infrastructure code for security vulnerabilities.
/// Implements the Review & Critique pattern for security validation.
/// </summary>
public sealed class SecurityReviewAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;

    public SecurityReviewAgent(Kernel kernel, ILlmProvider llmProvider)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
    }

    /// <summary>
    /// Reviews infrastructure code for security issues.
    /// </summary>
    public async Task<SecurityReviewResult> ReviewAsync(
        string artifactType,
        string content,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Quick heuristic checks first (fast)
        var heuristicIssues = PerformHeuristicChecks(artifactType, content);

        // LLM-powered deep analysis (slower but more comprehensive)
        var llmIssues = await PerformLlmReviewAsync(artifactType, content, context, cancellationToken);

        var allIssues = heuristicIssues.Concat(llmIssues).ToList();

        // Determine overall severity
        var criticalCount = allIssues.Count(i => i.Severity == "critical");
        var highCount = allIssues.Count(i => i.Severity == "high");

        var approved = criticalCount == 0 && highCount == 0;
        var overallSeverity = criticalCount > 0 ? "critical" :
                              highCount > 0 ? "high" :
                              allIssues.Any() ? "medium" : "none";

        return new SecurityReviewResult
        {
            Approved = approved,
            OverallSeverity = overallSeverity,
            Issues = allIssues,
            Recommendation = approved
                ? "Security review passed. Artifact is safe to deploy."
                : $"Security review failed: {criticalCount} critical, {highCount} high severity issues found. Address before deployment.",
            ReviewedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Fast heuristic-based security checks using regex patterns.
    /// </summary>
    private List<SecurityIssue> PerformHeuristicChecks(string artifactType, string content)
    {
        var issues = new List<SecurityIssue>();

        // Check for hardcoded credentials
        var credentialPatterns = new[]
        {
            new Regex(@"password\s*=\s*[""'](?!(\$\{|<%=))[^""']{1,}[""']", RegexOptions.IgnoreCase),
            new Regex(@"api[_-]?key\s*=\s*[""'][^""']{8,}[""']", RegexOptions.IgnoreCase),
            new Regex(@"secret\s*=\s*[""'][^""']{8,}[""']", RegexOptions.IgnoreCase),
            new Regex(@"aws_secret_access_key\s*=\s*[""'][^""']+[""']", RegexOptions.IgnoreCase),
            new Regex(@"private[_-]?key\s*=\s*[""'][^""']+[""']", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in credentialPatterns)
        {
            if (pattern.IsMatch(content))
            {
                issues.Add(new SecurityIssue
                {
                    Severity = "critical",
                    Category = "Hardcoded Credentials",
                    Description = "Hardcoded credentials detected in configuration",
                    Recommendation = "Use environment variables, secrets manager, or HashiCorp Vault instead",
                    Location = "Multiple locations - scan for passwords, API keys, secrets"
                });
                break; // Only add once
            }
        }

        // Terraform-specific checks
        if (artifactType.Contains("terraform", StringComparison.OrdinalIgnoreCase))
        {
            // Check for missing encryption at rest
            if (!content.Contains("encrypt", StringComparison.OrdinalIgnoreCase) &&
                (content.Contains("aws_db_instance", StringComparison.OrdinalIgnoreCase) ||
                 content.Contains("aws_s3_bucket", StringComparison.OrdinalIgnoreCase) ||
                 content.Contains("azurerm_storage_account", StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new SecurityIssue
                {
                    Severity = "high",
                    Category = "Missing Encryption",
                    Description = "Storage resources configured without encryption at rest",
                    Recommendation = "Enable encryption at rest for all storage resources (RDS, S3, Storage Accounts)",
                    Location = "Database and storage resource blocks"
                });
            }

            // Check for public access
            if (Regex.IsMatch(content, @"publicly_accessible\s*=\s*true", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(content, @"public_access_enabled\s*=\s*true", RegexOptions.IgnoreCase))
            {
                issues.Add(new SecurityIssue
                {
                    Severity = "high",
                    Category = "Public Exposure",
                    Description = "Resources configured with public internet access",
                    Recommendation = "Restrict access to private networks or specific IP ranges using security groups/NSGs",
                    Location = "Resource configuration with publicly_accessible = true"
                });
            }

            // Check for missing TLS enforcement
            if (content.Contains("postgresql", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("require_secure_transport", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("ssl_enforcement", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new SecurityIssue
                {
                    Severity = "medium",
                    Category = "Missing TLS",
                    Description = "Database connections not enforcing TLS/SSL",
                    Recommendation = "Enable SSL enforcement for PostgreSQL connections",
                    Location = "Database parameter groups"
                });
            }

            // Check for overly permissive security groups
            if (Regex.IsMatch(content, @"cidr_blocks\s*=\s*\[\s*[""']0\.0\.0\.0/0[""']", RegexOptions.IgnoreCase))
            {
                issues.Add(new SecurityIssue
                {
                    Severity = "high",
                    Category = "Overly Permissive Network",
                    Description = "Security group allows access from 0.0.0.0/0 (entire internet)",
                    Recommendation = "Restrict CIDR blocks to specific IP ranges or VPN/bastion access",
                    Location = "Security group ingress rules"
                });
            }
        }

        // Kubernetes-specific checks
        if (artifactType.Contains("kubernetes", StringComparison.OrdinalIgnoreCase) ||
            artifactType.Contains("k8s", StringComparison.OrdinalIgnoreCase))
        {
            // Check for privileged containers
            if (Regex.IsMatch(content, @"privileged:\s*true", RegexOptions.IgnoreCase))
            {
                issues.Add(new SecurityIssue
                {
                    Severity = "high",
                    Category = "Privileged Container",
                    Description = "Container running in privileged mode",
                    Recommendation = "Remove privileged mode unless absolutely necessary. Use specific capabilities instead",
                    Location = "SecurityContext with privileged: true"
                });
            }

            // Check for missing resource limits
            if (!content.Contains("limits:", StringComparison.Ordinal) &&
                content.Contains("kind: Deployment", StringComparison.Ordinal))
            {
                issues.Add(new SecurityIssue
                {
                    Severity = "medium",
                    Category = "Missing Resource Limits",
                    Description = "Pod/container configured without resource limits",
                    Recommendation = "Set CPU and memory limits to prevent resource exhaustion attacks",
                    Location = "Container spec resources section"
                });
            }

            // Check for host network usage
            if (Regex.IsMatch(content, @"hostNetwork:\s*true", RegexOptions.IgnoreCase))
            {
                issues.Add(new SecurityIssue
                {
                    Severity = "high",
                    Category = "Host Network Access",
                    Description = "Pod using host network namespace",
                    Recommendation = "Avoid hostNetwork unless required for specific network plugins",
                    Location = "Pod spec with hostNetwork: true"
                });
            }
        }

        // Docker Compose checks
        if (artifactType.Contains("docker", StringComparison.OrdinalIgnoreCase))
        {
            // Check for privileged mode
            if (content.Contains("privileged: true", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new SecurityIssue
                {
                    Severity = "high",
                    Category = "Privileged Container",
                    Description = "Docker container running in privileged mode",
                    Recommendation = "Remove privileged mode. Use specific capabilities with cap_add instead",
                    Location = "Service definition with privileged: true"
                });
            }

            // Check for exposed ports
            if (Regex.IsMatch(content, @"ports:\s*-\s*[""']?\d+:\d+[""']?", RegexOptions.IgnoreCase))
            {
                // Only warn if it looks like direct port mapping without reverse proxy
                if (!content.Contains("nginx", StringComparison.OrdinalIgnoreCase) &&
                    !content.Contains("traefik", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new SecurityIssue
                    {
                        Severity = "low",
                        Category = "Direct Port Exposure",
                        Description = "Services directly exposing ports without reverse proxy",
                        Recommendation = "Consider using a reverse proxy (nginx, Traefik) for TLS termination and rate limiting",
                        Location = "Service ports configuration"
                    });
                }
            }
        }

        return issues;
    }

    /// <summary>
    /// LLM-powered security review for deeper analysis.
    /// </summary>
    private async Task<List<SecurityIssue>> PerformLlmReviewAsync(
        string artifactType,
        string content,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var systemPrompt = @"You are a security expert specializing in cloud infrastructure and DevSecOps.

Your task is to review infrastructure-as-code artifacts for security vulnerabilities and misconfigurations.

Focus on:
1. **Authentication & Authorization**: Missing auth, weak policies, overly permissive roles
2. **Network Security**: Public exposure, missing firewalls, overly broad CIDR blocks
3. **Encryption**: Missing encryption at rest/transit, weak cipher suites
4. **Secrets Management**: Hardcoded credentials, exposed API keys
5. **Resource Isolation**: Shared networks, missing namespaces, privilege escalation risks
6. **Compliance**: GDPR, HIPAA, SOC2 requirements
7. **Supply Chain**: Vulnerable base images, unverified sources
8. **Observability**: Missing audit logging, insufficient monitoring

Severity levels:
- **critical**: Immediate security risk (exposed credentials, public databases, no encryption)
- **high**: Significant risk (overly permissive access, missing TLS, privileged containers)
- **medium**: Moderate risk (missing resource limits, weak password policies)
- **low**: Best practice violation (missing labels, suboptimal configuration)

Return findings as JSON array:
[
  {
    ""severity"": ""critical"" | ""high"" | ""medium"" | ""low"",
    ""category"": ""string"",
    ""description"": ""string"",
    ""recommendation"": ""string"",
    ""location"": ""string (specific line/block reference)""
  }
]

If no issues found, return empty array: []";

        var userPrompt = $@"Review this {artifactType} for security issues:

```
{content}
```

Context:
- Deployment mode: {(context.DryRun ? "planning" : "production")}
- Workspace: {context.WorkspacePath}

Return findings as JSON array.";

        var llmRequest = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Temperature = 0.2, // Lower temperature for consistent security analysis
            MaxTokens = 1500
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            // If LLM fails, return empty list (heuristics already ran)
            return new List<SecurityIssue>();
        }

        try
        {
            var json = ExtractJson(response.Content);
            var issues = System.Text.Json.JsonSerializer.Deserialize<List<SecurityIssue>>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return issues ?? new List<SecurityIssue>();
        }
        catch
        {
            // Parsing failed, return empty
            return new List<SecurityIssue>();
        }
    }

    private string ExtractJson(string text)
    {
        if (text.Contains("```json"))
        {
            var start = text.IndexOf("```json") + 7;
            var end = text.IndexOf("```", start);
            if (end > start)
            {
                return text.Substring(start, end - start).Trim();
            }
        }
        else if (text.Contains("```"))
        {
            var start = text.IndexOf("```") + 3;
            var end = text.IndexOf("```", start);
            if (end > start)
            {
                return text.Substring(start, end - start).Trim();
            }
        }

        return text.Trim();
    }
}

/// <summary>
/// Result of a security review.
/// </summary>
public sealed class SecurityReviewResult
{
    public bool Approved { get; init; }
    public string OverallSeverity { get; init; } = "none";
    public List<SecurityIssue> Issues { get; init; } = new();
    public string Recommendation { get; init; } = string.Empty;
    public DateTime ReviewedAt { get; init; }
}

/// <summary>
/// Individual security issue found during review.
/// </summary>
public sealed class SecurityIssue
{
    public string Severity { get; init; } = "medium";
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
}
