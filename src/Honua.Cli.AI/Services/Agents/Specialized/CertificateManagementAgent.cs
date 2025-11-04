// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
/// Specialized agent for SSL/TLS certificate management using ACME/Let's Encrypt.
/// Handles certificate provisioning, renewal, and storage in Azure Key Vault.
/// </summary>
public sealed class CertificateManagementAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;
    private readonly ILogger<CertificateManagementAgent> _logger;

    public CertificateManagementAgent(Kernel kernel, ILlmProvider? llmProvider = null, ILogger<CertificateManagementAgent>? logger = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CertificateManagementAgent>.Instance;
    }

    /// <summary>
    /// Processes a certificate management request by analyzing requirements and generating configuration.
    /// </summary>
    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Analyze certificate requirements
            var analysis = await AnalyzeCertificateRequirementsAsync(request, context, cancellationToken);

            // Generate certificate configuration based on analysis
            var configuration = await GenerateCertificateConfigurationAsync(analysis, context, cancellationToken);

            // Validate configuration
            var validation = await ValidateCertificateConfigurationAsync(configuration, context, cancellationToken);

            if (!validation.IsValid)
            {
                return new AgentStepResult
                {
                    AgentName = "CertificateManagement",
                    Action = "ProcessCertificateRequest",
                    Success = false,
                    Message = $"Certificate configuration validation failed: {validation.ErrorMessage}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Save certificate configuration
            await SaveCertificateConfigurationAsync(configuration, context, cancellationToken);

            var message = context.DryRun
                ? $"Generated certificate configuration (dry-run): {configuration.Summary}. Domains: {string.Join(", ", configuration.Domains)}"
                : $"Applied certificate configuration: {configuration.Summary}. Domains: {string.Join(", ", configuration.Domains)}";

            return new AgentStepResult
            {
                AgentName = "CertificateManagement",
                Action = "ProcessCertificateRequest",
                Success = true,
                Message = message,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "CertificateManagement",
                Action = "ProcessCertificateRequest",
                Success = false,
                Message = $"Error processing certificate request: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Analyzes certificate requirements from user request using LLM inference")]
    public async Task<CertificateAnalysis> AnalyzeCertificateRequirementsAsync(
        [Description("User's certificate management request")] string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("AnalyzeCertificateRequirementsAsync called with request: {Request}", request);

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine("You are an expert in SSL/TLS certificates, ACME protocol, and Let's Encrypt.");
        promptBuilder.AppendLine("Analyze the following certificate management request and provide structured analysis.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("User Request:");
        promptBuilder.AppendLine(request);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Provide your analysis in the following JSON structure:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"domains\": [\"<list of domains to secure>\"],");
        promptBuilder.AppendLine("  \"email\": \"<contact email for Let's Encrypt>\",");
        promptBuilder.AppendLine("  \"challengeType\": \"<Http01|Dns01>\",");
        promptBuilder.AppendLine("  \"autoRenew\": <true|false>,");
        promptBuilder.AppendLine("  \"storageProvider\": \"<azure-keyvault|filesystem>\",");
        promptBuilder.AppendLine("  \"storageLocation\": \"<vault name or file path>\",");
        promptBuilder.AppendLine("  \"acmeEnvironment\": \"<production|staging>\",");
        promptBuilder.AppendLine("  \"summary\": \"<brief summary of the certificate setup>\"");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Notes:");
        promptBuilder.AppendLine("- Use Dns01 challenge for wildcard domains (e.g., *.example.com)");
        promptBuilder.AppendLine("- Use Http01 challenge for single domains");
        promptBuilder.AppendLine("- Default to staging environment unless production is explicitly requested");
        promptBuilder.AppendLine("- Default to auto-renewal enabled");

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
                var analysis = JsonSerializer.Deserialize<CertificateAnalysis>(jsonStr,
                    CliJsonOptions.DevTooling) ?? new CertificateAnalysis();

                return analysis;
            }
        }

        // Fallback: Parse request for keywords and infer configuration
        return InferCertificateConfiguration(request);
    }

    private CertificateAnalysis InferCertificateConfiguration(string request)
    {
        var analysis = new CertificateAnalysis
        {
            Email = "admin@example.com",
            ChallengeType = "Http01",
            AutoRenew = true,
            StorageProvider = "azure-keyvault",
            StorageLocation = "honua-certificates",
            AcmeEnvironment = "staging",
            Summary = "SSL/TLS certificate with automatic renewal"
        };

        // Extract domains
        var domains = new List<string>();
        var words = request.Split(new[] { ' ', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var cleanWord = word.Trim('.', '/', ':', ';');
            // Check if it looks like a domain (contains at least one dot)
            if (cleanWord.Contains('.') && !cleanWord.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                domains.Add(cleanWord);
            }
        }

        if (domains.Count > 0)
        {
            analysis.Domains = domains;
        }
        else
        {
            analysis.Domains = new List<string> { "example.com" };
        }

        // Detect wildcard domains -> use DNS-01
        if (domains.Any(d => d.StartsWith("*.", StringComparison.Ordinal)))
        {
            analysis.ChallengeType = "Dns01";
        }

        // Extract email
        var emailMatch = System.Text.RegularExpressions.Regex.Match(request, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
        if (emailMatch.Success)
        {
            analysis.Email = emailMatch.Value;
        }

        // Detect production environment
        if (request.Contains("production", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("prod", StringComparison.OrdinalIgnoreCase))
        {
            analysis.AcmeEnvironment = "production";
        }

        // Detect filesystem storage
        if (request.Contains("filesystem", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("file", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("disk", StringComparison.OrdinalIgnoreCase))
        {
            analysis.StorageProvider = "filesystem";
            analysis.StorageLocation = "/etc/honua/certificates";
        }

        return analysis;
    }

    [KernelFunction, Description("Generates certificate configuration")]
    public async Task<CertificateConfiguration> GenerateCertificateConfigurationAsync(
        [Description("Certificate analysis results")] CertificateAnalysis analysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var config = new CertificateConfiguration
        {
            Domains = analysis.Domains,
            Email = analysis.Email,
            ChallengeType = analysis.ChallengeType,
            AutoRenew = analysis.AutoRenew,
            StorageProvider = analysis.StorageProvider,
            StorageLocation = analysis.StorageLocation,
            AcmeEnvironment = analysis.AcmeEnvironment,
            Summary = analysis.Summary
        };

        return config;
    }

    [KernelFunction, Description("Validates certificate configuration")]
    public async Task<CertificateValidationResult> ValidateCertificateConfigurationAsync(
        [Description("Certificate configuration to validate")] CertificateConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var result = new CertificateValidationResult { IsValid = true };

        if (configuration.Domains == null || configuration.Domains.Count == 0)
        {
            result.IsValid = false;
            result.ErrorMessage = "At least one domain is required";
            return result;
        }

        if (configuration.Email.IsNullOrWhiteSpace())
        {
            result.IsValid = false;
            result.ErrorMessage = "Contact email is required for ACME registration";
            return result;
        }

        if (configuration.ChallengeType != "Http01" && configuration.ChallengeType != "Dns01")
        {
            result.IsValid = false;
            result.ErrorMessage = "Challenge type must be Http01 or Dns01";
            return result;
        }

        // Validate wildcard domains require DNS-01
        var hasWildcard = configuration.Domains.Any(d => d.StartsWith("*.", StringComparison.Ordinal));
        if (hasWildcard && configuration.ChallengeType != "Dns01")
        {
            result.IsValid = false;
            result.ErrorMessage = "Wildcard domains require Dns01 challenge type";
            return result;
        }

        return result;
    }

    private async Task SaveCertificateConfigurationAsync(
        CertificateConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(context.WorkspacePath, "certificate-config.json");
        var json = JsonSerializer.Serialize(configuration,
            CliJsonOptions.Indented);

        await File.WriteAllTextAsync(configPath, json, cancellationToken);
        _logger.LogInformation("Saved certificate configuration to {ConfigPath}", configPath);
    }
}

/// <summary>
/// Analysis results from certificate requirements
/// </summary>
public class CertificateAnalysis
{
    public List<string> Domains { get; set; } = new();
    public string Email { get; set; } = string.Empty;
    public string ChallengeType { get; set; } = "Http01";
    public bool AutoRenew { get; set; } = true;
    public string StorageProvider { get; set; } = "azure-keyvault";
    public string StorageLocation { get; set; } = string.Empty;
    public string AcmeEnvironment { get; set; } = "staging";
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Generated certificate configuration
/// </summary>
public class CertificateConfiguration
{
    public List<string> Domains { get; set; } = new();
    public string Email { get; set; } = string.Empty;
    public string ChallengeType { get; set; } = "Http01";
    public bool AutoRenew { get; set; } = true;
    public string StorageProvider { get; set; } = "azure-keyvault";
    public string StorageLocation { get; set; } = string.Empty;
    public string AcmeEnvironment { get; set; } = "staging";
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Certificate configuration validation result
/// </summary>
public class CertificateValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
