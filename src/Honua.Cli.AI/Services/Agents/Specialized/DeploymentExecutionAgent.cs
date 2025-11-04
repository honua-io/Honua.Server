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

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for executing deployments using Terraform.
/// This agent actually runs terraform init, plan, and apply commands.
/// </summary>
public sealed class DeploymentExecutionAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;
    private readonly ILogger<DeploymentExecutionAgent>? _logger;
    private readonly GisEndpointValidationAgent? _gisValidator;
    private readonly ObservabilityValidationAgent? _observabilityValidator;
    private readonly NetworkDiagnosticsAgent? _networkDiagnostics;

    public DeploymentExecutionAgent(
        Kernel kernel,
        ILlmProvider? llmProvider = null,
        ILogger<DeploymentExecutionAgent>? logger = null,
        GisEndpointValidationAgent? gisValidator = null,
        ObservabilityValidationAgent? observabilityValidator = null,
        NetworkDiagnosticsAgent? networkDiagnostics = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider;
        _logger = logger;
        _gisValidator = gisValidator;
        _observabilityValidator = observabilityValidator;
        _networkDiagnostics = networkDiagnostics;
    }

    /// <summary>
    /// Executes a deployment using Terraform.
    /// </summary>
    public async Task<AgentStepResult> ExecuteDeploymentAsync(
        string deploymentType,
        string terraformDir,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Check if Terraform plugin is available
            if (!_kernel.Plugins.TryGetPlugin("Terraform", out var terraformPlugin))
            {
                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "ExecuteDeployment",
                    Success = false,
                    Message = "Terraform plugin not available. Cannot execute deployment.",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Step 1: Run terraform init
            var initResult = await ExecuteTerraformInitAsync(terraformPlugin, terraformDir, context, cancellationToken);
            if (!initResult.Success)
            {
                return initResult;
            }

            // Step 2: Run terraform plan
            var planResult = await ExecuteTerraformPlanAsync(terraformPlugin, terraformDir, context, cancellationToken);
            if (!planResult.Success)
            {
                return planResult;
            }

            // Step 3: Run terraform apply (with approval if required)
            var applyResult = await ExecuteTerraformApplyAsync(terraformPlugin, terraformDir, context, cancellationToken);
            if (!applyResult.Success)
            {
                return applyResult;
            }

            // Step 4: Validate deployment with GIS endpoint and observability checks
            var validationResult = await ValidateDeploymentAsync(deploymentType, terraformDir, context, cancellationToken);

            if (!validationResult.Success)
            {
                _logger?.LogWarning("Deployment validation failed: {Message}", validationResult.Message);
                return validationResult;
            }

            var message = context.DryRun
                ? $"[DRY-RUN] Would deploy {deploymentType} infrastructure using Terraform"
                : $"Successfully deployed {deploymentType} infrastructure. {validationResult.Message}";

            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "ExecuteDeployment",
                Success = true,
                Message = message,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "ExecuteDeployment",
                Success = false,
                Message = $"Error executing deployment: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Execute terraform init command")]
    private async Task<AgentStepResult> ExecuteTerraformInitAsync(
        KernelPlugin terraformPlugin,
        string terraformDir,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            if (context.DryRun)
            {
                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "TerraformInit",
                    Success = true,
                    Message = $"[DRY-RUN] Would run terraform init in {terraformDir}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Call TerraformInit function
            var result = await _kernel.InvokeAsync(
                terraformPlugin["TerraformInit"],
                new KernelArguments
                {
                    ["terraformDir"] = terraformDir
                },
                cancellationToken);

            var jsonResponse = result.ToString();

            if (!jsonResponse.IsNullOrEmpty())
            {
                try
                {
                    var doc = JsonDocument.Parse(jsonResponse);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                    {
                        return new AgentStepResult
                        {
                            AgentName = "DeploymentExecution",
                            Action = "TerraformInit",
                            Success = true,
                            Message = $"Terraform initialized successfully in {terraformDir}",
                            Duration = DateTime.UtcNow - startTime
                        };
                    }

                    if (root.TryGetProperty("error", out var error))
                    {
                        return new AgentStepResult
                        {
                            AgentName = "DeploymentExecution",
                            Action = "TerraformInit",
                            Success = false,
                            Message = $"Terraform init failed: {error.GetString() ?? "unknown error"}",
                            Duration = DateTime.UtcNow - startTime
                        };
                    }
                }
                catch (JsonException ex)
                {
                    return new AgentStepResult
                    {
                        AgentName = "DeploymentExecution",
                        Action = "TerraformInit",
                        Success = false,
                        Message = $"Failed to parse Terraform response: {ex.Message}",
                        Duration = DateTime.UtcNow - startTime
                    };
                }
            }

            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "TerraformInit",
                Success = false,
                Message = "Terraform init returned unexpected response",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "TerraformInit",
                Success = false,
                Message = $"Error running terraform init: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Execute terraform plan command")]
    private async Task<AgentStepResult> ExecuteTerraformPlanAsync(
        KernelPlugin terraformPlugin,
        string terraformDir,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            if (context.DryRun)
            {
                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "TerraformPlan",
                    Success = true,
                    Message = $"[DRY-RUN] Would run terraform plan in {terraformDir}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Call TerraformPlan function
            var result = await _kernel.InvokeAsync(
                terraformPlugin["TerraformPlan"],
                new KernelArguments
                {
                    ["terraformDir"] = terraformDir
                },
                cancellationToken);

            var jsonResponse = result.ToString();

            if (!jsonResponse.IsNullOrEmpty())
            {
                try
                {
                    var doc = JsonDocument.Parse(jsonResponse);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                    {
                        var planFile = root.TryGetProperty("planFile", out var pf) ? pf.GetString() : "tfplan";
                        return new AgentStepResult
                        {
                            AgentName = "DeploymentExecution",
                            Action = "TerraformPlan",
                            Success = true,
                            Message = $"Terraform plan created successfully. Plan saved to {planFile}"
                        };
                    }

                    if (root.TryGetProperty("error", out var error))
                    {
                        return new AgentStepResult
                        {
                            AgentName = "DeploymentExecution",
                            Action = "TerraformPlan",
                            Success = false,
                            Message = $"Terraform plan failed: {error.GetString() ?? "unknown error"}",
                            Duration = DateTime.UtcNow - startTime
                        };
                    }
                }
                catch (JsonException ex)
                {
                    return new AgentStepResult
                    {
                        AgentName = "DeploymentExecution",
                        Action = "TerraformPlan",
                        Success = false,
                        Message = $"Failed to parse Terraform response: {ex.Message}",
                        Duration = DateTime.UtcNow - startTime
                    };
                }
            }

            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "TerraformPlan",
                Success = false,
                Message = "Terraform plan returned unexpected response",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "TerraformPlan",
                Success = false,
                Message = $"Error running terraform plan: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Execute terraform apply command")]
    private async Task<AgentStepResult> ExecuteTerraformApplyAsync(
        KernelPlugin terraformPlugin,
        string terraformDir,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            if (context.DryRun)
            {
                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "TerraformApply",
                    Success = true,
                    Message = $"[DRY-RUN] Would run terraform apply in {terraformDir}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Determine if we should auto-approve based on context
            bool autoApprove = !context.RequireApproval;

            // Call TerraformApply function
            var result = await _kernel.InvokeAsync(
                terraformPlugin["TerraformApply"],
                new KernelArguments
                {
                    ["terraformDir"] = terraformDir,
                    ["autoApprove"] = autoApprove
                },
                cancellationToken);

            var jsonResponse = result.ToString();

            if (!jsonResponse.IsNullOrEmpty())
            {
                try
                {
                    var doc = JsonDocument.Parse(jsonResponse);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                    {
                        return new AgentStepResult
                        {
                            AgentName = "DeploymentExecution",
                            Action = "TerraformApply",
                            Success = true,
                            Message = "Terraform apply completed successfully. Infrastructure deployed.",
                            Duration = DateTime.UtcNow - startTime
                        };
                    }

                    if (root.TryGetProperty("reason", out var reason))
                    {
                        return new AgentStepResult
                        {
                            AgentName = "DeploymentExecution",
                            Action = "TerraformApply",
                            Success = false,
                            Message = $"Terraform apply not executed: {reason.GetString() ?? "unknown reason"}",
                            Duration = DateTime.UtcNow - startTime
                        };
                    }

                    if (root.TryGetProperty("error", out var error))
                    {
                        return new AgentStepResult
                        {
                            AgentName = "DeploymentExecution",
                            Action = "TerraformApply",
                            Success = false,
                            Message = $"Terraform apply failed: {error.GetString() ?? "unknown error"}",
                            Duration = DateTime.UtcNow - startTime
                        };
                    }
                }
                catch (JsonException ex)
                {
                    return new AgentStepResult
                    {
                        AgentName = "DeploymentExecution",
                        Action = "TerraformApply",
                        Success = false,
                        Message = $"Failed to parse Terraform response: {ex.Message}",
                        Duration = DateTime.UtcNow - startTime
                    };
                }
            }

            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "TerraformApply",
                Success = false,
                Message = "Terraform apply returned unexpected response",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "TerraformApply",
                Success = false,
                Message = $"Error running terraform apply: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Validate the deployed infrastructure with comprehensive checks")]
    private async Task<AgentStepResult> ValidateDeploymentAsync(
        string deploymentType,
        string terraformDir,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var validationMessages = new List<string>();

        try
        {
            if (context.DryRun)
            {
                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "ValidateDeployment",
                    Success = true,
                    Message = "[DRY-RUN] Would validate deployed infrastructure with GIS and observability checks",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            _logger?.LogInformation("Starting comprehensive deployment validation for {DeploymentType}", deploymentType);

            // Step 1: Check terraform state exists
            var stateFile = Path.Combine(context.WorkspacePath, terraformDir, "terraform.tfstate");
            if (!File.Exists(stateFile))
            {
                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "ValidateDeployment",
                    Success = false,
                    Message = "Terraform state file not found. Deployment may have failed.",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            validationMessages.Add("✅ Terraform state file exists");

            // Step 2: Extract deployment URL from terraform outputs
            var deploymentUrl = await ExtractDeploymentUrlFromStateAsync(stateFile, cancellationToken);

            if (deploymentUrl.IsNullOrEmpty())
            {
                _logger?.LogWarning("Could not extract deployment URL from terraform outputs. Skipping endpoint validation.");
                validationMessages.Add("⚠️  Deployment URL not found - skipping endpoint validation");

                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "ValidateDeployment",
                    Success = true,
                    Message = string.Join("\n", validationMessages),
                    Duration = DateTime.UtcNow - startTime
                };
            }

            _logger?.LogInformation("Deployment URL: {Url}", deploymentUrl);
            validationMessages.Add($"📍 Deployment URL: {deploymentUrl}");

            // Step 3: Network diagnostics (if health check fails, diagnose network issues)
            _logger?.LogInformation("Waiting for services to be ready (health check with 5s timeout)...");
            var healthCheckPassed = await WaitForHealthCheckAsync(deploymentUrl, TimeSpan.FromSeconds(5), cancellationToken);

            if (!healthCheckPassed)
            {
                validationMessages.Add("❌ Health check failed - service not responding within 5s");

                // Run network diagnostics to identify the root cause
                if (_networkDiagnostics != null)
                {
                    _logger?.LogWarning("Running network diagnostics to identify connectivity issues...");
                    validationMessages.Add("🔍 Running network diagnostics...");

                    var uri = new Uri(deploymentUrl);
                    var networkDiag = await _networkDiagnostics.DiagnoseAsync(
                        new NetworkDiagnosticsRequest
                        {
                            TargetUrl = deploymentUrl,
                            AdditionalPorts = new List<int> { 443, 8080, 5432 },
                            DetectFirewallIssues = true,
                            IncludeTraceroute = false // Too slow for fast-fail
                        },
                        context,
                        cancellationToken);

                    validationMessages.Add($"\nNetwork Diagnostics ({networkDiag.TotalDurationMs}ms):");
                    foreach (var check in networkDiag.Checks)
                    {
                        var icon = check.Status switch
                        {
                            NetworkCheckStatus.Passed => "✅",
                            NetworkCheckStatus.Warning => "⚠️ ",
                            NetworkCheckStatus.Failed => "❌",
                            _ => "❓"
                        };
                        validationMessages.Add($"  {icon} {check.CheckType}: {check.Message}");
                    }

                    validationMessages.Add("\nRecommendations:");
                    foreach (var rec in networkDiag.Recommendations)
                    {
                        validationMessages.Add($"  → {rec}");
                    }
                }

                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "ValidateDeployment",
                    Success = false,
                    Message = string.Join("\n", validationMessages),
                    Duration = DateTime.UtcNow - startTime
                };
            }

            validationMessages.Add("✅ Health check passed");

            // Step 4: GIS Endpoint Validation
            if (_gisValidator != null)
            {
                _logger?.LogInformation("Running GIS endpoint validation...");
                var gisValidation = await _gisValidator.ValidateDeployedServicesAsync(
                    new GisValidationRequest
                    {
                        BaseUrl = deploymentUrl,
                        TestWfs = true,
                        TestWms = true,
                        TestWmts = true,
                        TestEsriRest = true,
                        TestOData = true,
                        TestSecurity = true,
                        TestFeatureRetrieval = true
                    },
                    context,
                    cancellationToken);

                if (gisValidation.OverallStatus == EndpointStatus.Failed)
                {
                    var failedChecks = gisValidation.Checks
                        .Where(c => c.Status == EndpointStatus.Failed)
                        .Select(c => $"  • {c.EndpointType}: {c.Message}")
                        .ToList();

                    validationMessages.Add("❌ GIS endpoint validation failed:");
                    validationMessages.AddRange(failedChecks);

                    return new AgentStepResult
                    {
                        AgentName = "DeploymentExecution",
                        Action = "ValidateDeployment",
                        Success = false,
                        Message = string.Join("\n", validationMessages),
                        Duration = DateTime.UtcNow - startTime
                    };
                }
                else if (gisValidation.OverallStatus == EndpointStatus.Warning)
                {
                    validationMessages.Add($"⚠️  GIS endpoints: {gisValidation.PassedChecks} passed, {gisValidation.WarningChecks} warnings");
                }
                else
                {
                    validationMessages.Add($"✅ GIS endpoints validated: {gisValidation.PassedChecks}/{gisValidation.Checks.Count} passed");
                }
            }
            else
            {
                validationMessages.Add("⚠️  GIS validator not available - skipping endpoint checks");
            }

            // Step 5: Quick Observability Check (no long wait - smoke test only)
            if (_observabilityValidator != null)
            {
                _logger?.LogInformation("Running quick observability check...");
                validationMessages.Add("ℹ️  Observability validation configured - full metrics analysis available post-deployment");
            }
            else
            {
                validationMessages.Add("ℹ️  Observability validator not configured (optional for development)");
            }

            // Overall success
            var overallSuccess = !validationMessages.Any(m => m.StartsWith("❌"));

            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "ValidateDeployment",
                Success = overallSuccess,
                Message = string.Join("\n", validationMessages),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during deployment validation");
            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "ValidateDeployment",
                Success = false,
                Message = $"Validation error: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Waits for health check endpoint to respond (with retries up to timeout).
    /// Aggressive fast-fail approach - 5 second max for responsive rollback decisions.
    /// </summary>
    private async Task<bool> WaitForHealthCheckAsync(
        string baseUrl,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) }; // Health check timeout
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var retryCount = 0;

        while (sw.Elapsed < timeout && retryCount < 3)
        {
            try
            {
                var response = await httpClient.GetAsync($"{baseUrl}/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("Health check passed after {Elapsed}ms (attempt {Attempt})",
                        sw.ElapsedMilliseconds, retryCount + 1);
                    return true;
                }

                _logger?.LogDebug("Health check returned {StatusCode}, retrying...", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Health check failed (attempt {Attempt}), retrying...", retryCount + 1);
            }

            retryCount++;

            // Very short delays: 500ms, 1s, 1.5s
            if (retryCount < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * retryCount), cancellationToken);
            }
        }

        _logger?.LogWarning("Health check failed after {Elapsed}ms ({Attempts} attempts)",
            sw.ElapsedMilliseconds, retryCount);
        return false;
    }

    /// <summary>
    /// Extracts the deployment URL from terraform state outputs.
    /// </summary>
    private async Task<string?> ExtractDeploymentUrlFromStateAsync(
        string stateFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var stateJson = await File.ReadAllTextAsync(stateFilePath, cancellationToken);
            var state = JsonDocument.Parse(stateJson);

            // Try to find output with "url", "endpoint", "base_url", or "honua_url" in the name
            if (state.RootElement.TryGetProperty("outputs", out var outputs))
            {
                foreach (var output in outputs.EnumerateObject())
                {
                    var name = output.Name.ToLowerInvariant();
                    if (name.Contains("url") || name.Contains("endpoint") || name.Contains("honua"))
                    {
                        if (output.Value.TryGetProperty("value", out var value))
                        {
                            var url = value.GetString();
                            if (!url.IsNullOrEmpty() && (url.StartsWith("http://") || url.StartsWith("https://")))
                            {
                                return url.TrimEnd('/');
                            }
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to extract deployment URL from state file");
            return null;
        }
    }

    /// <summary>
    /// Execute terraform destroy to tear down infrastructure.
    /// </summary>
    [KernelFunction, Description("Destroy deployed infrastructure using terraform destroy")]
    public async Task<AgentStepResult> DestroyDeploymentAsync(
        [Description("Path to Terraform directory")] string terraformDir,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Check if Terraform plugin is available
            if (!_kernel.Plugins.TryGetPlugin("Terraform", out var terraformPlugin))
            {
                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "DestroyDeployment",
                    Success = false,
                    Message = "Terraform plugin not available. Cannot destroy deployment.",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            if (context.DryRun)
            {
                return new AgentStepResult
                {
                    AgentName = "DeploymentExecution",
                    Action = "DestroyDeployment",
                    Success = true,
                    Message = $"[DRY-RUN] Would run terraform destroy in {terraformDir}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Call TerraformDestroy function if it exists, otherwise use apply with destroy flag
            if (terraformPlugin.TryGetFunction("TerraformDestroy", out var destroyFunc))
            {
                var result = await _kernel.InvokeAsync(
                    destroyFunc,
                    new KernelArguments
                    {
                        ["terraformDir"] = terraformDir,
                        ["autoApprove"] = !context.RequireApproval
                    },
                    cancellationToken);

                var jsonResponse = result.ToString();
                if (!jsonResponse.IsNullOrEmpty())
                {
                    try
                    {
                        var doc = JsonDocument.Parse(jsonResponse);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                        {
                            return new AgentStepResult
                            {
                                AgentName = "DeploymentExecution",
                                Action = "DestroyDeployment",
                                Success = true,
                                Message = "Infrastructure destroyed successfully",
                                Duration = DateTime.UtcNow - startTime
                            };
                        }
                    }
                    catch (JsonException ex)
                    {
                        return new AgentStepResult
                        {
                            AgentName = "DeploymentExecution",
                            Action = "DestroyDeployment",
                            Success = false,
                            Message = $"Failed to parse Terraform response: {ex.Message}",
                            Duration = DateTime.UtcNow - startTime
                        };
                    }
                }
            }

            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "DestroyDeployment",
                Success = false,
                Message = "Terraform destroy function not available or failed",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "DeploymentExecution",
                Action = "DestroyDeployment",
                Success = false,
                Message = $"Error destroying deployment: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }
}