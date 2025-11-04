// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for security hardening including authentication setup,
/// authorization policies, CORS configuration, and WAF rules.
/// </summary>
public sealed class SecurityHardeningAgent
{
    private readonly Kernel _kernel;

    public SecurityHardeningAgent(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var securityPlugin = _kernel.Plugins["Security"];

            // Analyze security requirements
            var requirements = AnalyzeSecurityRequirements(request);

            // Generate security configurations
            var configurations = await GenerateSecurityConfigurationsAsync(requirements, context, cancellationToken);

            // Save security configuration if not in dry-run mode
            if (!context.DryRun)
            {
                await SaveSecurityConfigurationAsync(requirements, configurations, context, cancellationToken);
            }

            var message = context.DryRun
                ? $"Security analysis complete (dry-run). {configurations.Count} security measures recommended"
                : $"Applied {configurations.Count} security configurations successfully. Saved to {context.WorkspacePath}/security.json";

            return new AgentStepResult
            {
                AgentName = "SecurityHardening",
                Action = "ProcessSecurityRequest",
                Success = true,
                Message = message,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "SecurityHardening",
                Action = "ProcessSecurityRequest",
                Success = false,
                Message = $"Error processing security request: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private SecurityRequirements AnalyzeSecurityRequirements(string request)
    {
        var lower = request.ToLowerInvariant();

        return new SecurityRequirements
        {
            NeedsAuthentication = lower.Contains("auth") || lower.Contains("login"),
            NeedsAuthorization = lower.Contains("permission") || lower.Contains("role"),
            NeedsCORS = lower.Contains("cors") || lower.Contains("cross-origin"),
            NeedsRateLimiting = lower.Contains("rate limit") || lower.Contains("throttle"),
            NeedsSSL = lower.Contains("https") || lower.Contains("ssl") || lower.Contains("tls")
        };
    }

    private Task<List<SecurityConfiguration>> GenerateSecurityConfigurationsAsync(
        SecurityRequirements requirements,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var configurations = new List<SecurityConfiguration>();

        if (requirements.NeedsAuthentication)
        {
            configurations.Add(new SecurityConfiguration
            {
                Type = "Authentication",
                Description = "JWT-based authentication with refresh tokens",
                Priority = "High"
            });
        }

        if (requirements.NeedsCORS)
        {
            configurations.Add(new SecurityConfiguration
            {
                Type = "CORS",
                Description = "Restrictive CORS policy for production",
                Priority = "High"
            });
        }

        if (requirements.NeedsRateLimiting)
        {
            configurations.Add(new SecurityConfiguration
            {
                Type = "RateLimiting",
                Description = "Rate limiting: 100 req/min per IP",
                Priority = "Medium"
            });
        }

        if (requirements.NeedsSSL)
        {
            configurations.Add(new SecurityConfiguration
            {
                Type = "SSL/TLS",
                Description = "TLS 1.3 with strong cipher suites",
                Priority = "Critical"
            });
        }

        return Task.FromResult(configurations);
    }

    private async Task SaveSecurityConfigurationAsync(
        SecurityRequirements requirements,
        List<SecurityConfiguration> configurations,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var securityConfig = new
        {
            authentication = requirements.NeedsAuthentication ? new
            {
                mode = "JWT",
                issuer = "https://honua.io",
                audience = "honua-api",
                tokenLifetime = "15m",
                refreshTokenLifetime = "7d",
                requireHttps = true
            } : null,
            authorization = requirements.NeedsAuthorization ? new
            {
                mode = "PolicyBased",
                defaultPolicy = "RequireAuthenticatedUser",
                policies = new[]
                {
                    new { name = "AdminOnly", requirement = "role:admin" },
                    new { name = "ReadWrite", requirement = "permission:read,write" }
                }
            } : null,
            cors = requirements.NeedsCORS ? new
            {
                allowedOrigins = new[] { "https://app.honua.io" },
                allowedMethods = new[] { "GET", "POST", "PUT", "DELETE" },
                allowedHeaders = new[] { "Authorization", "Content-Type" },
                allowCredentials = true,
                maxAge = 3600
            } : null,
            rateLimit = requirements.NeedsRateLimiting ? new
            {
                policy = "SlidingWindow",
                windowSize = "1m",
                maxRequests = 100,
                perIpAddress = true,
                retryAfterHeader = true
            } : null,
            ssl = requirements.NeedsSSL ? new
            {
                enforceHttps = true,
                tlsVersion = "1.3",
                cipherSuites = new[]
                {
                    "TLS_AES_256_GCM_SHA384",
                    "TLS_AES_128_GCM_SHA256"
                },
                hsts = new
                {
                    maxAge = 31536000,
                    includeSubDomains = true,
                    preload = true
                }
            } : null
        };

        var filePath = Path.Combine(context.WorkspacePath, "security.json");
        var json = JsonSerializer.Serialize(securityConfig, CliJsonOptions.Indented);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}

public sealed class SecurityRequirements
{
    public bool NeedsAuthentication { get; set; }
    public bool NeedsAuthorization { get; set; }
    public bool NeedsCORS { get; set; }
    public bool NeedsRateLimiting { get; set; }
    public bool NeedsSSL { get; set; }
}

public sealed class SecurityConfiguration
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}
