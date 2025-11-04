// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for DNS configuration management.
/// Supports Cloudflare, Azure DNS, and other providers for A, AAAA, CNAME, TXT, and MX records.
/// </summary>
public sealed class DnsConfigurationAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;
    private readonly ILogger<DnsConfigurationAgent> _logger;

    public DnsConfigurationAgent(Kernel kernel, ILlmProvider? llmProvider = null, ILogger<DnsConfigurationAgent>? logger = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DnsConfigurationAgent>.Instance;
    }

    /// <summary>
    /// Processes a DNS configuration request by analyzing requirements and generating configuration.
    /// </summary>
    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Analyze DNS requirements
            var analysis = await AnalyzeDnsRequirementsAsync(request, context, cancellationToken);

            // Generate DNS configuration based on analysis
            var configuration = await GenerateDnsConfigurationAsync(analysis, context, cancellationToken);

            // Validate configuration
            var validation = await ValidateDnsConfigurationAsync(configuration, context, cancellationToken);

            if (!validation.IsValid)
            {
                return new AgentStepResult
                {
                    AgentName = "DnsConfiguration",
                    Action = "ProcessDnsRequest",
                    Success = false,
                    Message = $"DNS configuration validation failed: {validation.ErrorMessage}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Save DNS configuration
            await SaveDnsConfigurationAsync(configuration, context, cancellationToken);

            var message = context.DryRun
                ? $"Generated DNS configuration (dry-run): {configuration.Summary}. Provider: {configuration.Provider}"
                : $"Applied DNS configuration: {configuration.Summary}. Provider: {configuration.Provider}";

            return new AgentStepResult
            {
                AgentName = "DnsConfiguration",
                Action = "ProcessDnsRequest",
                Success = true,
                Message = message,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "DnsConfiguration",
                Action = "ProcessDnsRequest",
                Success = false,
                Message = $"Error processing DNS request: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Analyzes DNS requirements from user request using LLM inference")]
    public async Task<DnsAnalysis> AnalyzeDnsRequirementsAsync(
        [Description("User's DNS configuration request")] string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("AnalyzeDnsRequirementsAsync called with request: {Request}", request);

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine("You are an expert in DNS, domain management, and DNS providers (Cloudflare, Azure DNS, Route53).");
        promptBuilder.AppendLine("Analyze the following DNS configuration request and provide structured analysis.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("User Request:");
        promptBuilder.AppendLine(request);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Provide your analysis in the following JSON structure:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"provider\": \"<cloudflare|azure-dns|manual>\",");
        promptBuilder.AppendLine("  \"zone\": \"<DNS zone/domain name>\",");
        promptBuilder.AppendLine("  \"records\": [");
        promptBuilder.AppendLine("    {");
        promptBuilder.AppendLine("      \"type\": \"<A|AAAA|CNAME|TXT|MX>\",");
        promptBuilder.AppendLine("      \"name\": \"<record name or @ for apex>\",");
        promptBuilder.AppendLine("      \"value\": \"<record value>\",");
        promptBuilder.AppendLine("      \"ttl\": <TTL in seconds, default 3600>");
        promptBuilder.AppendLine("    }");
        promptBuilder.AppendLine("  ],");
        promptBuilder.AppendLine("  \"proxyEnabled\": <true|false, for Cloudflare>,");
        promptBuilder.AppendLine("  \"summary\": \"<brief summary of DNS setup>\"");
        promptBuilder.AppendLine("}");

        var prompt = promptBuilder.ToString();

        if (_llmProvider != null)
        {
            // Use LLM provider directly for more control
            var llmRequest = new LlmRequest
            {
                UserPrompt = prompt,
                MaxTokens = 1024,
                Temperature = 0.3
            };

            var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);
            _logger.LogDebug("LLM response: {Response}", response.Content);

            // Extract JSON from response
            var jsonStart = response.Content.IndexOf('{');
            var jsonEnd = response.Content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var analysis = JsonSerializer.Deserialize<DnsAnalysis>(jsonStr,
                    CliJsonOptions.DevTooling) ?? new DnsAnalysis();

                return analysis;
            }
        }

        // Fallback: Parse request for keywords and infer configuration
        return InferDnsConfiguration(request);
    }

    private DnsAnalysis InferDnsConfiguration(string request)
    {
        var analysis = new DnsAnalysis
        {
            Provider = "cloudflare",
            Zone = "example.com",
            Records = new List<DnsRecord>(),
            ProxyEnabled = false,
            Summary = "DNS configuration"
        };

        // Extract zone/domain
        var words = request.Split(new[] { ' ', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var cleanWord = word.Trim('.', '/', ':', ';');
            if (cleanWord.Contains('.') && !cleanWord.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // Possible domain - extract root domain
                var parts = cleanWord.Split('.');
                if (parts.Length >= 2)
                {
                    analysis.Zone = string.Join(".", parts[^2], parts[^1]);
                    break;
                }
            }
        }

        // Detect record type
        var recordType = "A";
        if (request.Contains("AAAA", StringComparison.OrdinalIgnoreCase))
            recordType = "AAAA";
        else if (request.Contains("CNAME", StringComparison.OrdinalIgnoreCase))
            recordType = "CNAME";
        else if (request.Contains("TXT", StringComparison.OrdinalIgnoreCase))
            recordType = "TXT";
        else if (request.Contains("MX", StringComparison.OrdinalIgnoreCase))
            recordType = "MX";

        // Extract IP address or value
        string? recordValue = null;
        var ipv4Pattern = @"\b(?:\d{1,3}\.){3}\d{1,3}\b";
        var ipMatch = System.Text.RegularExpressions.Regex.Match(request, ipv4Pattern);
        if (ipMatch.Success)
        {
            recordValue = ipMatch.Value;
        }

        // Extract record name
        var recordName = "@";
        if (request.Contains("www", StringComparison.OrdinalIgnoreCase))
            recordName = "www";
        else if (request.Contains("api", StringComparison.OrdinalIgnoreCase))
            recordName = "api";

        analysis.Records.Add(new DnsRecord
        {
            Type = recordType,
            Name = recordName,
            Value = recordValue ?? "203.0.113.1",
            Ttl = 3600
        });

        // Detect provider
        if (request.Contains("azure", StringComparison.OrdinalIgnoreCase))
            analysis.Provider = "azure-dns";
        else if (request.Contains("cloudflare", StringComparison.OrdinalIgnoreCase))
            analysis.Provider = "cloudflare";

        // Detect proxy
        if (request.Contains("proxy", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("cdn", StringComparison.OrdinalIgnoreCase))
        {
            analysis.ProxyEnabled = true;
        }

        return analysis;
    }

    [KernelFunction, Description("Generates DNS configuration")]
    public async Task<DnsConfiguration> GenerateDnsConfigurationAsync(
        [Description("DNS analysis results")] DnsAnalysis analysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var config = new DnsConfiguration
        {
            Provider = analysis.Provider,
            Zone = analysis.Zone,
            Records = analysis.Records,
            ProxyEnabled = analysis.ProxyEnabled,
            Summary = analysis.Summary
        };

        return config;
    }

    [KernelFunction, Description("Validates DNS configuration")]
    public async Task<DnsValidationResult> ValidateDnsConfigurationAsync(
        [Description("DNS configuration to validate")] DnsConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var result = new DnsValidationResult { IsValid = true };

        if (configuration.Zone.IsNullOrWhiteSpace())
        {
            result.IsValid = false;
            result.ErrorMessage = "DNS zone is required";
            return result;
        }

        if (configuration.Records == null || configuration.Records.Count == 0)
        {
            result.IsValid = false;
            result.ErrorMessage = "At least one DNS record is required";
            return result;
        }

        foreach (var record in configuration.Records)
        {
            if (record.Type.IsNullOrWhiteSpace())
            {
                result.IsValid = false;
                result.ErrorMessage = "Record type is required";
                return result;
            }

            var validTypes = new[] { "A", "AAAA", "CNAME", "TXT", "MX", "NS", "SOA", "SRV" };
            if (!validTypes.Contains(record.Type, StringComparer.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.ErrorMessage = $"Invalid record type: {record.Type}";
                return result;
            }

            if (record.Value.IsNullOrWhiteSpace())
            {
                result.IsValid = false;
                result.ErrorMessage = "Record value is required";
                return result;
            }
        }

        return result;
    }

    private async Task SaveDnsConfigurationAsync(
        DnsConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(context.WorkspacePath, "dns-config.json");
        var json = JsonSerializer.Serialize(configuration,
            CliJsonOptions.Indented);

        await File.WriteAllTextAsync(configPath, json, cancellationToken);
        _logger.LogInformation("Saved DNS configuration to {ConfigPath}", configPath);
    }
}

/// <summary>
/// Analysis results from DNS requirements
/// </summary>
public class DnsAnalysis
{
    public string Provider { get; set; } = "cloudflare";
    public string Zone { get; set; } = string.Empty;
    public List<DnsRecord> Records { get; set; } = new();
    public bool ProxyEnabled { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// DNS record definition
/// </summary>
public class DnsRecord
{
    public string Type { get; set; } = "A";
    public string Name { get; set; } = "@";
    public string Value { get; set; } = string.Empty;
    public int Ttl { get; set; } = 3600;
}

/// <summary>
/// Generated DNS configuration
/// </summary>
public class DnsConfiguration
{
    public string Provider { get; set; } = "cloudflare";
    public string Zone { get; set; } = string.Empty;
    public List<DnsRecord> Records { get; set; } = new();
    public bool ProxyEnabled { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// DNS configuration validation result
/// </summary>
public class DnsValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
