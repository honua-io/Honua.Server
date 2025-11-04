// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Diagnostics;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using GitOpsState = Honua.Cli.AI.Services.Processes.State.GitOpsState;

namespace Honua.Cli.AI.Services.Processes.Steps.GitOps;

/// <summary>
/// Monitors for configuration drift between Git and live deployment.
/// </summary>
public class MonitorDriftStep : KernelProcessStep<GitOpsState>
{
    private readonly ILogger<MonitorDriftStep> _logger;
    private GitOpsState _state = new();

    public MonitorDriftStep(ILogger<MonitorDriftStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<GitOpsState> state)
    {
        _state = state.State ?? new GitOpsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("MonitorDrift")]
    public async Task MonitorDriftAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Monitoring configuration drift for {RepoUrl}", _state.RepoUrl);

        _state.Status = "MonitoringDrift";

        try
        {
            // Check for new commits
            await CheckForNewCommits();

            // Compare live vs desired state
            await CompareLiveVsDesired();

            // Auto-sync if configured
            if (_state.DriftDetected && _state.AutoSync)
            {
                await AutoSync();
            }

            _state.Status = "Completed";

            _logger.LogInformation("Drift monitoring completed for {RepoUrl}. Drift detected: {DriftDetected}",
                _state.RepoUrl, _state.DriftDetected);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "MonitoringCompleted",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to monitor drift for {RepoUrl}", _state.RepoUrl);
            _state.Status = "MonitoringFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "MonitoringFailed",
                Data = new { _state.RepoUrl, Error = ex.Message }
            });
        }
    }

    private async Task CheckForNewCommits()
    {
        _logger.LogInformation("Checking for new commits in {RepoUrl} branch {Branch}",
            _state.RepoUrl, _state.Branch);

        try
        {
            // Use git ls-remote to check latest commit hash
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"ls-remote {_state.RepoUrl} {_state.Branch}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to check remote commits for {_state.RepoUrl}: {error}");
            }

            // Extract current commit SHA from ls-remote output
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var parts = lines[0].Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var currentCommitSha = parts[0];

                    if (!string.IsNullOrEmpty(_state.LastCommitHash) &&
                        _state.LastCommitHash != currentCommitSha)
                    {
                        _logger.LogInformation(
                            "New commits detected. Previous: {PreviousCommit}, Current: {CurrentCommit}",
                            _state.LastCommitHash, currentCommitSha);
                        _state.DriftDetected = true;
                    }
                    else if (string.IsNullOrEmpty(_state.LastCommitHash))
                    {
                        _logger.LogInformation("First check, current commit: {CommitSha}", currentCommitSha);
                    }
                    else
                    {
                        _logger.LogInformation("No new commits detected. Current: {CommitSha}", currentCommitSha);
                    }

                    // Always update both CommitSha and LastCommitHash to track current state
                    _state.CommitSha = currentCommitSha;
                    _state.LastCommitHash = currentCommitSha;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for new commits");
            throw;
        }
    }

    private async Task CompareLiveVsDesired()
    {
        _logger.LogInformation("Comparing live state vs desired state from Git");

        try
        {
            // Create temporary directory for comparison
            var tempDir = Path.Combine(Path.GetTempPath(), $"gitops-drift-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Clone the repository to get desired state
                await CloneRepository(tempDir);

                var configFullPath = Path.Combine(tempDir, _state.ConfigPath);
                var yamlFiles = new List<string>();

                if (Directory.Exists(configFullPath))
                {
                    yamlFiles.AddRange(Directory.GetFiles(configFullPath, "*.yaml", SearchOption.AllDirectories));
                    yamlFiles.AddRange(Directory.GetFiles(configFullPath, "*.yml", SearchOption.AllDirectories));
                }
                else if (File.Exists(configFullPath))
                {
                    yamlFiles.Add(configFullPath);
                }

                if (yamlFiles.Count == 0)
                {
                    _logger.LogWarning("No YAML files found in {ConfigPath}", _state.ConfigPath);
                    return;
                }

                _logger.LogInformation("Comparing {Count} configuration files", yamlFiles.Count);

                // Check if kubectl is available
                if (await IsKubectlAvailable())
                {
                    // Use kubectl diff to detect drift
                    await DetectDriftWithKubectl(yamlFiles);
                }
                else
                {
                    // Fallback to comparing with stored state
                    await DetectDriftWithStoredState(yamlFiles, tempDir);
                }
            }
            finally
            {
                // Cleanup temporary directory
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare live vs desired state");
            throw;
        }
    }

    private async Task AutoSync()
    {
        _logger.LogInformation("Auto-syncing detected drift");

        try
        {
            // Create temporary directory for sync
            var tempDir = Path.Combine(Path.GetTempPath(), $"gitops-autosync-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Clone the repository
                await CloneRepository(tempDir);

                var configFullPath = Path.Combine(tempDir, _state.ConfigPath);
                var yamlFiles = new List<string>();

                if (Directory.Exists(configFullPath))
                {
                    yamlFiles.AddRange(Directory.GetFiles(configFullPath, "*.yaml", SearchOption.AllDirectories));
                    yamlFiles.AddRange(Directory.GetFiles(configFullPath, "*.yml", SearchOption.AllDirectories));
                }
                else if (File.Exists(configFullPath))
                {
                    yamlFiles.Add(configFullPath);
                }

                if (yamlFiles.Count == 0)
                {
                    _logger.LogWarning("No YAML files found for auto-sync");
                    return;
                }

                // Check if kubectl is available
                if (await IsKubectlAvailable())
                {
                    // Apply configurations using kubectl
                    foreach (var yamlFile in yamlFiles)
                    {
                        await ApplyKubernetesConfig(yamlFile);
                    }

                    _state.LastCommitHash = _state.CommitSha;
                    _logger.LogInformation("Auto-sync completed for {Count} files", yamlFiles.Count);
                }
                else
                {
                    _logger.LogWarning("kubectl not available, skipping auto-sync");
                }
            }
            finally
            {
                // Cleanup temporary directory
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-sync failed");
            throw;
        }
    }

    private async Task<bool> IsKubectlAvailable()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "kubectl",
                    Arguments = "version --client",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync().ConfigureAwait(false);

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task DetectDriftWithKubectl(List<string> yamlFiles)
    {
        foreach (var yamlFile in yamlFiles)
        {
            var relativePath = Path.GetFileName(yamlFile);

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "kubectl",
                        Arguments = $"diff -f \"{yamlFile}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                await process.WaitForExitAsync().ConfigureAwait(false);

                // kubectl diff returns exit code 1 if there are differences
                if (process.ExitCode == 1 && !string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogInformation("Drift detected in {File}", relativePath);
                    _state.DriftDetected = true;
                }
                else if (process.ExitCode > 1)
                {
                    _logger.LogWarning("Failed to check drift for {File}: {Error}", relativePath, error);
                }
                else
                {
                    _logger.LogDebug("No drift detected in {File}", relativePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking drift for {File}", relativePath);
            }
        }
    }

    private async Task DetectDriftWithStoredState(List<string> yamlFiles, string tempDir)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var yamlFile in yamlFiles)
        {
            var relativePath = Path.GetRelativePath(tempDir, yamlFile);
            var currentContent = await File.ReadAllTextAsync(yamlFile).ConfigureAwait(false);

            // Compare with previously deployed config
            if (_state.DeployedConfig.TryGetValue(relativePath, out var deployedContent))
            {
                if (currentContent != deployedContent)
                {
                    _logger.LogInformation("Drift detected in {File} (content changed)", relativePath);
                    _state.DriftDetected = true;
                }
            }
            else
            {
                _logger.LogInformation("Drift detected: new file {File}", relativePath);
                _state.DriftDetected = true;
            }
        }
    }

    private async Task ApplyKubernetesConfig(string yamlFile)
    {
        var relativePath = Path.GetFileName(yamlFile);

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "kubectl",
                    Arguments = $"apply -f \"{yamlFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to apply configuration {relativePath}: {error}");
            }

            _logger.LogInformation("Applied configuration: {File}", relativePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply configuration {File}", relativePath);
            throw;
        }
    }

    private async Task CloneRepository(string targetDir)
    {
        _logger.LogDebug("Cloning repository {RepoUrl} branch {Branch} to {TargetDir}",
            _state.RepoUrl, _state.Branch, targetDir);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone --depth 1 --branch {_state.Branch} {_state.RepoUrl} {targetDir}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to clone repository {_state.RepoUrl}: {error}");
        }

        _logger.LogDebug("Repository cloned successfully");
    }
}
