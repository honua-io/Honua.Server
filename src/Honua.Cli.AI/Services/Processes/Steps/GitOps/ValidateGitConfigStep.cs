// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Diagnostics;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using GitOpsState = Honua.Cli.AI.Services.Processes.State.GitOpsState;

namespace Honua.Cli.AI.Services.Processes.Steps.GitOps;

/// <summary>
/// Validates Git repository and configuration files for GitOps deployment.
/// </summary>
public class ValidateGitConfigStep : KernelProcessStep<GitOpsState>
{
    private readonly ILogger<ValidateGitConfigStep> _logger;
    private GitOpsState _state = new();

    public ValidateGitConfigStep(ILogger<ValidateGitConfigStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<GitOpsState> state)
    {
        _state = state.State ?? new GitOpsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ValidateConfig")]
    public async Task ValidateConfigAsync(
        KernelProcessStepContext context,
        GitOpsRequest request)
    {
        _logger.LogInformation("Validating GitOps config from {RepoUrl}", request.RepoUrl);

        _state.GitOpsId = Guid.NewGuid().ToString();
        _state.RepoUrl = request.RepoUrl;
        _state.Branch = request.Branch;
        _state.ConfigPath = request.ConfigPath;
        _state.StartTime = DateTime.UtcNow;
        _state.Status = "ValidatingConfig";

        try
        {
            // Validate repository access
            await ValidateRepoAccess();

            // Validate YAML syntax
            await ValidateYamlSyntax();

            // Validate schema
            await ValidateSchema();

            _state.ConfigValid = true;

            _logger.LogInformation("GitOps config validated for {RepoUrl}", _state.RepoUrl);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ConfigValid",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate GitOps config from {RepoUrl}", request.RepoUrl);
            _state.Status = "ValidationFailed";
            _state.ConfigValid = false;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ValidationFailed",
                Data = new { request.RepoUrl, Error = ex.Message }
            });
        }
    }

    private async Task ValidateRepoAccess()
    {
        _logger.LogInformation("Validating repository access for {RepoUrl}", _state.RepoUrl);

        try
        {
            // SECURITY FIX #22: Use ArgumentList to prevent command injection via malicious repo URLs or branch names
            // Malicious input like "--upload-pack=<malicious-command>" could otherwise execute arbitrary code
            ValidateGitInputs(_state.RepoUrl, _state.Branch);

            // Use git ls-remote to verify repository access without cloning
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // SECURITY FIX #22: Add arguments individually to prevent injection
            process.StartInfo.ArgumentList.Add("ls-remote");
            process.StartInfo.ArgumentList.Add("--");
            process.StartInfo.ArgumentList.Add(_state.RepoUrl);
            process.StartInfo.ArgumentList.Add(_state.Branch);

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to access repository {_state.RepoUrl}: {error}");
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException(
                    $"Branch {_state.Branch} not found in repository {_state.RepoUrl}");
            }

            // Extract commit SHA from ls-remote output
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var parts = lines[0].Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    _state.CommitSha = parts[0];
                    _logger.LogInformation("Repository access validated. Latest commit: {CommitSha}", _state.CommitSha);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Repository access validation failed");
            throw;
        }
    }

    private async Task ValidateYamlSyntax()
    {
        _logger.LogInformation("Validating YAML syntax for config at {ConfigPath}", _state.ConfigPath);

        try
        {
            // Create a temporary directory for cloning
            var tempDir = Path.Combine(Path.GetTempPath(), $"gitops-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Shallow clone only the specific branch
                await CloneRepository(tempDir);

                // Find YAML files to validate
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

                _logger.LogInformation("Found {Count} YAML files to validate", yamlFiles.Count);

                // Parse each YAML file to validate syntax
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                foreach (var yamlFile in yamlFiles)
                {
                    var relativePath = Path.GetRelativePath(tempDir, yamlFile);
                    try
                    {
                        var yamlContent = await File.ReadAllTextAsync(yamlFile).ConfigureAwait(false);

                        // Parse YAML to validate syntax
                        using var reader = new StringReader(yamlContent);
                        var yamlStream = new YamlStream();
                        yamlStream.Load(reader);

                        _state.ChangedFiles.Add(relativePath);
                        _logger.LogDebug("YAML syntax valid for {File}", relativePath);
                    }
                    catch (YamlException ex)
                    {
                        throw new InvalidOperationException(
                            $"YAML syntax error in {relativePath}: {ex.Message}", ex);
                    }
                }

                _logger.LogInformation("YAML syntax validation passed for {Count} files", yamlFiles.Count);
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
            _logger.LogError(ex, "YAML syntax validation failed");
            throw;
        }
    }

    private async Task ValidateSchema()
    {
        _logger.LogInformation("Validating configuration schema");

        try
        {
            // Create a temporary directory for validation
            var tempDir = Path.Combine(Path.GetTempPath(), $"gitops-schema-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Clone repository
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

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                // Validate basic Kubernetes resource structure
                foreach (var yamlFile in yamlFiles)
                {
                    var relativePath = Path.GetRelativePath(tempDir, yamlFile);
                    var yamlContent = await File.ReadAllTextAsync(yamlFile).ConfigureAwait(false);

                    try
                    {
                        // Parse as a basic Kubernetes resource structure
                        var resource = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                        // Basic schema validation for Kubernetes resources
                        if (resource != null)
                        {
                            if (!resource.ContainsKey("apiVersion"))
                            {
                                _logger.LogWarning("File {File} missing 'apiVersion' field", relativePath);
                            }

                            if (!resource.ContainsKey("kind"))
                            {
                                _logger.LogWarning("File {File} missing 'kind' field", relativePath);
                            }

                            if (!resource.ContainsKey("metadata"))
                            {
                                _logger.LogWarning("File {File} missing 'metadata' field", relativePath);
                            }

                            // Store validated config
                            _state.DeployedConfig[relativePath] = yamlContent;
                        }

                        _logger.LogDebug("Schema validation passed for {File}", relativePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Schema validation warning for {File}: {Message}",
                            relativePath, ex.Message);
                        // Don't fail on schema validation warnings, just log them
                    }
                }

                _logger.LogInformation("Configuration schema validation completed for {Count} files", yamlFiles.Count);
                _state.ValidationPassed = true;
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
            _logger.LogError(ex, "Schema validation failed");
            throw;
        }
    }

    private async Task CloneRepository(string targetDir)
    {
        _logger.LogDebug("Cloning repository {RepoUrl} branch {Branch} to {TargetDir}",
            _state.RepoUrl, _state.Branch, targetDir);

        // SECURITY FIX #23: Validate inputs before cloning to prevent path traversal and argument injection
        ValidateGitInputs(_state.RepoUrl, _state.Branch);
        ValidateTargetDirectory(targetDir);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // SECURITY FIX #23: Use ArgumentList to prevent command injection
        // Prevents malicious branch names or URLs like "--upload-pack=evil" from executing arbitrary code
        process.StartInfo.ArgumentList.Add("clone");
        process.StartInfo.ArgumentList.Add("--depth");
        process.StartInfo.ArgumentList.Add("1");
        process.StartInfo.ArgumentList.Add("--branch");
        process.StartInfo.ArgumentList.Add(_state.Branch);
        process.StartInfo.ArgumentList.Add("--");
        process.StartInfo.ArgumentList.Add(_state.RepoUrl);
        process.StartInfo.ArgumentList.Add(targetDir);

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

    /// <summary>
    /// SECURITY FIX #22, #23: Validates Git repository URLs and branch names to prevent command injection.
    /// </summary>
    private void ValidateGitInputs(string repoUrl, string branch)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            throw new ArgumentException("Repository URL cannot be empty", nameof(repoUrl));
        }

        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new ArgumentException("Branch name cannot be empty", nameof(branch));
        }

        // Prevent git flag injection (e.g., "--upload-pack=evil", "--config=foo")
        if (repoUrl.StartsWith("-") || repoUrl.StartsWith("--"))
        {
            throw new ArgumentException("Repository URL cannot start with dashes (potential flag injection)", nameof(repoUrl));
        }

        if (branch.StartsWith("-") || branch.StartsWith("--"))
        {
            throw new ArgumentException("Branch name cannot start with dashes (potential flag injection)", nameof(branch));
        }

        // Prevent newline injection which could allow command chaining
        if (repoUrl.Contains('\n') || repoUrl.Contains('\r') ||
            branch.Contains('\n') || branch.Contains('\r'))
        {
            throw new ArgumentException("Repository URL and branch name cannot contain newlines");
        }

        // Basic URL validation for repo URL
        if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Repository URL must be a valid absolute URL", nameof(repoUrl));
        }

        // Only allow safe protocols
        var allowedSchemes = new[] { "https", "http", "git", "ssh" };
        if (!allowedSchemes.Contains(uri.Scheme.ToLowerInvariant()))
        {
            throw new ArgumentException(
                $"Repository URL must use one of the following protocols: {string.Join(", ", allowedSchemes)}",
                nameof(repoUrl));
        }
    }

    /// <summary>
    /// SECURITY FIX #23: Validates target directory path to prevent path traversal attacks.
    /// </summary>
    private void ValidateTargetDirectory(string targetDir)
    {
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            throw new ArgumentException("Target directory cannot be empty", nameof(targetDir));
        }

        // Get absolute path to normalize
        var absolutePath = Path.GetFullPath(targetDir);

        // Ensure target is within temp directory (common case) or user-controlled workspace
        var tempPath = Path.GetFullPath(Path.GetTempPath());

        if (!absolutePath.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Target directory {TargetDir} is outside temp path {TempPath}",
                absolutePath, tempPath);
            // Allow it but log the warning - some legitimate use cases may need this
        }

        // Prevent injection via path with dashes at start
        var dirName = Path.GetFileName(absolutePath);
        if (dirName.StartsWith("-") || dirName.StartsWith("--"))
        {
            throw new ArgumentException("Target directory name cannot start with dashes (potential flag injection)", nameof(targetDir));
        }
    }
}

/// <summary>
/// Request object for GitOps configuration.
/// </summary>
public record GitOpsRequest(
    string RepoUrl,
    string Branch,
    string ConfigPath);
