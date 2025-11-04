// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using UpgradeState = Honua.Cli.AI.Services.Processes.State.UpgradeState;

namespace Honua.Cli.AI.Services.Processes.Steps.Upgrade;

/// <summary>
/// Detects the currently running version of Honua deployment.
/// </summary>
public class DetectCurrentVersionStep : KernelProcessStep<UpgradeState>
{
    private readonly ILogger<DetectCurrentVersionStep> _logger;
    private UpgradeState _state = new();

    public DetectCurrentVersionStep(ILogger<DetectCurrentVersionStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<UpgradeState> state)
    {
        _state = state.State ?? new UpgradeState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("DetectVersion")]
    public async Task DetectVersionAsync(
        KernelProcessStepContext context,
        UpgradeRequest request)
    {
        _logger.LogInformation("Detecting current version for deployment {DeploymentName}",
            request.DeploymentName);

        _state.UpgradeId = Guid.NewGuid().ToString();
        _state.DeploymentName = request.DeploymentName;
        _state.TargetVersion = request.TargetVersion;
        _state.StartTime = DateTime.UtcNow;
        _state.Status = "DetectingVersion";

        try
        {
            // Detect version from multiple sources in order of preference
            string? detectedVersion = null;

            // 1. Try Kubernetes deployment annotations
            detectedVersion = await TryDetectFromKubernetes(request.DeploymentName);

            // 2. Try Docker container labels
            if (string.IsNullOrEmpty(detectedVersion))
            {
                detectedVersion = await TryDetectFromDocker(request.DeploymentName);
            }

            // 3. Try database metadata table
            if (string.IsNullOrEmpty(detectedVersion))
            {
                detectedVersion = await TryDetectFromDatabase(request.DeploymentName);
            }

            // 4. Try HTTP endpoint health check
            if (string.IsNullOrEmpty(detectedVersion))
            {
                detectedVersion = await TryDetectFromHealthEndpoint(request.DeploymentName);
            }

            // Set detected version or fallback
            _state.CurrentVersion = detectedVersion ?? "unknown";
            _state.GreenEnvironment = $"{request.DeploymentName}-green";
            _state.BlueEnvironment = $"{request.DeploymentName}-blue";

            _logger.LogInformation("Detected version {CurrentVersion} for {DeploymentName}",
                _state.CurrentVersion, _state.DeploymentName);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "VersionDetected",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect version for {DeploymentName}", request.DeploymentName);
            _state.Status = "DetectionFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DetectionFailed",
                Data = new { request.DeploymentName, Error = ex.Message }
            });
        }
    }

    private async Task<string?> TryDetectFromKubernetes(string deploymentName)
    {
        try
        {
            _logger.LogDebug("Attempting to detect version from Kubernetes deployment {DeploymentName}", deploymentName);

            // Execute kubectl to get deployment annotations
            // Use ArgumentList for proper cross-platform argument handling (avoids quote issues on Windows)
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "kubectl",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // Add arguments individually to avoid shell escaping issues
            process.StartInfo.ArgumentList.Add("get");
            process.StartInfo.ArgumentList.Add("deployment");
            process.StartInfo.ArgumentList.Add(deploymentName);
            process.StartInfo.ArgumentList.Add("-o");
            process.StartInfo.ArgumentList.Add("jsonpath={.metadata.annotations.version}");

            process.Start();
            var version = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(version))
            {
                _logger.LogInformation("Detected version {Version} from Kubernetes", version.Trim());
                return version.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to detect version from Kubernetes");
        }

        return null;
    }

    private async Task<string?> TryDetectFromDocker(string deploymentName)
    {
        try
        {
            _logger.LogDebug("Attempting to detect version from Docker container {DeploymentName}", deploymentName);

            // Execute docker inspect to get container labels
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"inspect --format='{{{{.Config.Labels.version}}}}' {deploymentName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var version = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(version))
            {
                _logger.LogInformation("Detected version {Version} from Docker", version.Trim());
                return version.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to detect version from Docker");
        }

        return null;
    }

    private async Task<string?> TryDetectFromDatabase(string deploymentName)
    {
        // Database version detection is not implemented
        // Fail fast instead of wasting time with delays and false logging
        _logger.LogDebug("Database version detection not configured - skipping");
        return await Task.FromResult<string?>(null);
    }

    private async Task<string?> TryDetectFromHealthEndpoint(string deploymentName)
    {
        try
        {
            _logger.LogDebug("Attempting to detect version from health endpoint");

            // Try common health endpoint URLs
            var healthUrls = new[]
            {
                $"http://{deploymentName}/health",
                $"http://{deploymentName}:8080/health",
                $"https://{deploymentName}/health",
                $"http://localhost:8080/health"
            };

            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            foreach (var url in healthUrls)
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();

                        // Try to parse version from JSON response
                        // Expected format: { "version": "1.2.3", ... }
                        if (content.Contains("\"version\""))
                        {
                            var versionMatch = System.Text.RegularExpressions.Regex.Match(
                                content,
                                @"""version""\s*:\s*""([^""]+)""");

                            if (versionMatch.Success)
                            {
                                var version = versionMatch.Groups[1].Value;
                                _logger.LogInformation("Detected version {Version} from health endpoint {Url}", version, url);
                                return version;
                            }
                        }
                    }
                }
                catch
                {
                    // Continue to next URL
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to detect version from health endpoint");
        }

        return null;
    }
}

/// <summary>
/// Request object for upgrade.
/// </summary>
public record UpgradeRequest(
    string DeploymentName,
    string TargetVersion);
