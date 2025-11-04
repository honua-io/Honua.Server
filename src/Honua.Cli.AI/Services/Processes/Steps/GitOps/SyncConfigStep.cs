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
/// Syncs configuration from Git repository to deployment.
/// </summary>
public class SyncConfigStep : KernelProcessStep<GitOpsState>
{
    private readonly ILogger<SyncConfigStep> _logger;
    private GitOpsState _state = new();

    public SyncConfigStep(ILogger<SyncConfigStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<GitOpsState> state)
    {
        _state = state.State ?? new GitOpsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("SyncConfig")]
    public async Task SyncConfigAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Syncing config from {RepoUrl} branch {Branch}",
            _state.RepoUrl, _state.Branch);

        _state.Status = "SyncingConfig";

        try
        {
            // Clone/pull repository
            await PullRepository();

            // Parse configuration
            await ParseConfig();

            // Apply configuration
            await ApplyConfig();

            _state.ConfigSynced = true;
            // LastCommitHash is already set by PullRepository - don't overwrite it

            _logger.LogInformation("Config synced from {RepoUrl} commit {CommitHash}",
                _state.RepoUrl, _state.LastCommitHash);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ConfigSynced",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync config from {RepoUrl}", _state.RepoUrl);
            _state.Status = "SyncFailed";
            _state.ConfigSynced = false;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "SyncFailed",
                Data = new { _state.RepoUrl, Error = ex.Message }
            });
        }
    }

    private async Task PullRepository()
    {
        _logger.LogInformation("Pulling repository {RepoUrl} branch {Branch}",
            _state.RepoUrl, _state.Branch);

        try
        {
            // Create temporary directory for cloning
            var tempDir = Path.Combine(Path.GetTempPath(), $"gitops-sync-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            // Store temp dir in state for cleanup
            _state.DeployedConfig["_tempDir"] = tempDir;

            // Clone repository
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone --depth 1 --branch {_state.Branch} {_state.RepoUrl} {tempDir}",
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

            // Get the current commit SHA
            var commitProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            commitProcess.Start();
            var commitOutput = await commitProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await commitProcess.WaitForExitAsync().ConfigureAwait(false);

            if (commitProcess.ExitCode == 0)
            {
                _state.LastCommitHash = commitOutput.Trim();
                _state.CommitSha = _state.LastCommitHash;
                _logger.LogInformation("Repository cloned successfully. Commit: {CommitSha}", _state.LastCommitHash);
            }
            else
            {
                _logger.LogWarning("Failed to get commit SHA");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull repository");
            throw;
        }
    }

    private async Task ParseConfig()
    {
        _logger.LogInformation("Parsing configuration files from {ConfigPath}", _state.ConfigPath);

        try
        {
            if (!_state.DeployedConfig.TryGetValue("_tempDir", out var tempDir) || string.IsNullOrEmpty(tempDir))
            {
                throw new InvalidOperationException("Repository not cloned");
            }

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
            else
            {
                throw new FileNotFoundException($"Configuration path not found: {_state.ConfigPath}");
            }

            if (yamlFiles.Count == 0)
            {
                throw new InvalidOperationException($"No YAML files found in {_state.ConfigPath}");
            }

            _logger.LogInformation("Found {Count} YAML configuration files", yamlFiles.Count);

            // Parse and validate each YAML file
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            _state.ChangedFiles.Clear();

            foreach (var yamlFile in yamlFiles)
            {
                var relativePath = Path.GetRelativePath(tempDir, yamlFile);

                try
                {
                    var yamlContent = await File.ReadAllTextAsync(yamlFile).ConfigureAwait(false);

                    // Parse to validate syntax
                    var resource = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                    if (resource != null)
                    {
                        // Store file path for application
                        _state.ChangedFiles.Add(yamlFile);

                        // Store content in state
                        _state.DeployedConfig[relativePath] = yamlContent;

                        _logger.LogDebug("Parsed configuration file: {File}", relativePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse configuration file {File}", relativePath);
                    throw new InvalidOperationException($"Failed to parse {relativePath}: {ex.Message}", ex);
                }
            }

            _logger.LogInformation("Successfully parsed {Count} configuration files", yamlFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse configuration files");
            throw;
        }
    }

    private async Task ApplyConfig()
    {
        _logger.LogInformation("Applying configuration to deployment");

        try
        {
            if (_state.ChangedFiles.Count == 0)
            {
                _logger.LogWarning("No configuration files to apply");
                return;
            }

            // Check if kubectl is available
            var kubectlAvailable = await IsKubectlAvailable();

            if (!kubectlAvailable)
            {
                _logger.LogWarning("kubectl not available. Configuration parsed but not applied.");
                _logger.LogInformation("Install kubectl to enable automatic deployment");
                return;
            }

            // Apply each configuration file
            var appliedCount = 0;
            var failedCount = 0;

            foreach (var yamlFile in _state.ChangedFiles)
            {
                var relativePath = Path.GetFileName(yamlFile);

                try
                {
                    await ApplyKubernetesConfig(yamlFile);
                    appliedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply configuration {File}", relativePath);
                    failedCount++;
                }
            }

            if (failedCount > 0)
            {
                throw new InvalidOperationException(
                    $"Failed to apply {failedCount} out of {_state.ChangedFiles.Count} configuration files");
            }

            _logger.LogInformation("Successfully applied {Count} configuration files", appliedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply configuration");
            throw;
        }
        finally
        {
            // Cleanup temporary directory
            if (_state.DeployedConfig.TryGetValue("_tempDir", out var tempDir) && !string.IsNullOrEmpty(tempDir))
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                        _state.DeployedConfig.Remove("_tempDir");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temporary directory {TempDir}", tempDir);
                }
            }
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

    private async Task ApplyKubernetesConfig(string yamlFile)
    {
        var relativePath = Path.GetFileName(yamlFile);

        try
        {
            _logger.LogInformation("Applying configuration: {File}", relativePath);

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
                    $"kubectl apply failed for {relativePath}: {error}");
            }

            if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogInformation("kubectl output for {File}: {Output}", relativePath, output.Trim());
            }

            _logger.LogInformation("Successfully applied configuration: {File}", relativePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply configuration {File}", relativePath);
            throw;
        }
    }
}
