using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Honua.Build.Orchestrator.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Honua.Build.Orchestrator;

/// <summary>
/// Main orchestrator for cross-repository GIS server builds.
/// Handles repository cloning, solution generation, multi-platform builds, and container publishing.
/// </summary>
public sealed class BuildOrchestrator
{
    private readonly ILogger<BuildOrchestrator> _logger;
    private readonly string _workspaceRoot;

    public BuildOrchestrator(ILogger<BuildOrchestrator> logger, string workspaceRoot)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workspaceRoot = workspaceRoot ?? throw new ArgumentNullException(nameof(workspaceRoot));
    }

    /// <summary>
    /// Executes a complete build orchestration from manifest.
    /// </summary>
    /// <param name="manifest">Build manifest specifying the configuration.</param>
    /// <param name="outputDir">Directory for build outputs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Build results for each target.</returns>
    public async Task<List<BuildTarget>> ExecuteBuildAsync(
        BuildManifest manifest,
        string outputDir,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);

        _logger.LogInformation("Starting build orchestration for manifest {ManifestId} ({ManifestName})",
            manifest.Id, manifest.Name);

        var manifestHash = ManifestHasher.ComputeHash(manifest);
        _logger.LogInformation("Manifest hash: {Hash}", manifestHash);

        var workspaceDir = Path.Combine(_workspaceRoot, manifest.Id);
        Directory.CreateDirectory(workspaceDir);
        Directory.CreateDirectory(outputDir);

        try
        {
            // Step 1: Clone repositories
            _logger.LogInformation("Step 1: Cloning {Count} repositories", manifest.Repositories.Count);
            await CloneRepositoriesAsync(manifest, workspaceDir, cancellationToken);

            // Step 2: Generate solution file
            _logger.LogInformation("Step 2: Generating solution file");
            var solutionPath = await GenerateSolutionAsync(manifest, workspaceDir, cancellationToken);

            // Step 3: Build for each cloud target
            _logger.LogInformation("Step 3: Building for {Count} targets", manifest.Targets.Count);
            var buildResults = new List<BuildTarget>();

            var parallelism = manifest.Deployment?.Parallelism ?? 1;
            var semaphore = new SemaphoreSlim(parallelism);

            var buildTasks = manifest.Targets.Select(async target =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await BuildForCloudTargetAsync(
                        target,
                        manifest,
                        solutionPath,
                        outputDir,
                        cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(buildTasks);
            buildResults.AddRange(results);

            // Step 4: Push to registries if configured
            foreach (var result in buildResults.Where(r => r.Success))
            {
                var target = manifest.Targets.First(t => t.Id == result.TargetId);
                if (target.Registry != null && result.ImageTag != null)
                {
                    _logger.LogInformation("Step 4: Pushing image for target {TargetId}", target.Id);
                    await PushToRegistryAsync(target, result.ImageTag, cancellationToken);
                }
            }

            var successCount = buildResults.Count(r => r.Success);
            _logger.LogInformation("Build orchestration completed: {Success}/{Total} targets succeeded",
                successCount, buildResults.Count);

            return buildResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build orchestration failed");
            throw;
        }
    }

    /// <summary>
    /// Clones all repositories specified in the manifest.
    /// </summary>
    private async Task CloneRepositoriesAsync(
        BuildManifest manifest,
        string workspaceDir,
        CancellationToken cancellationToken)
    {
        foreach (var repo in manifest.Repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var repoDir = Path.Combine(workspaceDir, repo.Name);

            if (Directory.Exists(repoDir))
            {
                _logger.LogInformation("Repository {Name} already exists at {Path}, pulling latest changes",
                    repo.Name, repoDir);

                try
                {
                    await PullRepositoryAsync(repoDir, repo, cancellationToken);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to pull repository {Name}, will re-clone", repo.Name);
                    Directory.Delete(repoDir, recursive: true);
                }
            }

            _logger.LogInformation("Cloning repository {Name} from {Url} (ref: {Ref})",
                repo.Name, repo.Url, repo.Ref);

            var cloneOptions = new CloneOptions
            {
                BranchName = repo.Ref,
                RecurseSubmodules = true
            };

            // Handle authentication for private repositories
            if (repo.Access == "private" && !string.IsNullOrWhiteSpace(repo.Credentials))
            {
                var token = Environment.GetEnvironmentVariable(repo.Credentials);
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException(
                        $"Credentials environment variable '{repo.Credentials}' not found for private repository '{repo.Name}'");
                }

                cloneOptions.FetchOptions.CredentialsProvider = (url, fromUrl, types) =>
                    new UsernamePasswordCredentials
                    {
                        Username = token,
                        Password = string.Empty
                    };
            }

            try
            {
                var clonedPath = Repository.Clone(repo.Url, repoDir, cloneOptions);
                _logger.LogInformation("Successfully cloned {Name} to {Path}", repo.Name, clonedPath);

                // Checkout specific ref if not a branch
                if (!string.IsNullOrWhiteSpace(repo.Ref) && repo.Ref != "main" && repo.Ref != "master")
                {
                    using var repository = new Repository(repoDir);
                    var commit = repository.Lookup<Commit>(repo.Ref);
                    if (commit != null)
                    {
                        Commands.Checkout(repository, commit);
                        _logger.LogInformation("Checked out commit {Ref} for {Name}", repo.Ref, repo.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clone repository {Name} from {Url}", repo.Name, repo.Url);
                throw;
            }

            await Task.Delay(100, cancellationToken); // Yield for cancellation check
        }
    }

    /// <summary>
    /// Pulls latest changes for an existing repository.
    /// </summary>
    private Task PullRepositoryAsync(string repoDir, RepositoryReference repoRef, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var repository = new Repository(repoDir);

            var options = new PullOptions
            {
                FetchOptions = new FetchOptions()
            };

            if (repoRef.Access == "private" && !string.IsNullOrWhiteSpace(repoRef.Credentials))
            {
                var token = Environment.GetEnvironmentVariable(repoRef.Credentials);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    options.FetchOptions.CredentialsProvider = (url, fromUrl, types) =>
                        new UsernamePasswordCredentials
                        {
                            Username = token,
                            Password = string.Empty
                        };
                }
            }

            var signature = new Signature("Build Orchestrator", "build@honua.io", DateTimeOffset.Now);
            Commands.Pull(repository, signature, options);

            _logger.LogInformation("Pulled latest changes for {Name}", repoRef.Name);
        }, cancellationToken);
    }

    /// <summary>
    /// Generates a solution file combining all projects from cloned repositories.
    /// </summary>
    private async Task<string> GenerateSolutionAsync(
        BuildManifest manifest,
        string workspaceDir,
        CancellationToken cancellationToken)
    {
        var solutionName = $"Honua.Generated.{manifest.Id}";
        var solutionPath = Path.Combine(workspaceDir, $"{solutionName}.sln");

        _logger.LogInformation("Generating solution file: {Path}", solutionPath);

        // Create new solution
        var result = await RunProcessAsync(
            "dotnet",
            $"new sln -n {solutionName} -o \"{workspaceDir}\"",
            workspaceDir,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to create solution: {result.Error}");
        }

        // Collect all project files to add
        var projectPaths = new List<string>();

        foreach (var repo in manifest.Repositories)
        {
            var repoDir = Path.Combine(workspaceDir, repo.Name);

            if (repo.Projects != null && repo.Projects.Count > 0)
            {
                // Add specific projects listed in manifest
                foreach (var projectPath in repo.Projects)
                {
                    var fullPath = Path.Combine(repoDir, projectPath);
                    if (File.Exists(fullPath))
                    {
                        projectPaths.Add(fullPath);
                    }
                    else
                    {
                        _logger.LogWarning("Project file not found: {Path}", fullPath);
                    }
                }
            }
            else
            {
                // Add all .csproj files in repository
                var csprojFiles = Directory.GetFiles(repoDir, "*.csproj", SearchOption.AllDirectories);
                projectPaths.AddRange(csprojFiles);
            }
        }

        // Filter projects based on included modules
        if (manifest.Modules.Count > 0)
        {
            projectPaths = projectPaths
                .Where(p => manifest.Modules.Any(m => p.Contains(m, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        _logger.LogInformation("Adding {Count} projects to solution", projectPaths.Count);

        // Add projects to solution
        foreach (var projectPath in projectPaths)
        {
            var addResult = await RunProcessAsync(
                "dotnet",
                $"sln \"{solutionPath}\" add \"{projectPath}\"",
                workspaceDir,
                cancellationToken);

            if (addResult.ExitCode == 0)
            {
                _logger.LogDebug("Added project: {Project}", Path.GetFileName(projectPath));
            }
            else
            {
                _logger.LogWarning("Failed to add project {Project}: {Error}",
                    Path.GetFileName(projectPath), addResult.Error);
            }
        }

        return solutionPath;
    }

    /// <summary>
    /// Builds the solution for a specific cloud target with platform optimizations.
    /// </summary>
    private async Task<BuildTarget> BuildForCloudTargetAsync(
        CloudTarget target,
        BuildManifest manifest,
        string solutionPath,
        string outputDir,
        CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.StartNew();
        var buildTarget = new BuildTarget
        {
            TargetId = target.Id,
            Success = false
        };

        try
        {
            _logger.LogInformation("Building for target {TargetId} ({Provider}/{Compute}/{Arch})",
                target.Id, target.Provider, target.Compute, target.Architecture);

            var targetOutputDir = Path.Combine(outputDir, target.Id);
            Directory.CreateDirectory(targetOutputDir);

            // Build MSBuild arguments
            var args = new List<string>
            {
                "publish",
                $"\"{solutionPath}\"",
                "-c", "Release",
                "-r", target.Architecture,
                "--self-contained", "true",
                "-p:PublishAot=true",
                "-p:CloudCompute=" + target.Compute,
                "-o", $"\"{targetOutputDir}\""
            };

            // Apply optimizations
            var optimizations = target.Optimizations ?? manifest.Optimizations ?? new BuildOptimizations();
            ApplyOptimizations(args, optimizations, target);

            // Apply custom properties from manifest
            if (manifest.Properties != null)
            {
                foreach (var prop in manifest.Properties)
                {
                    args.Add($"-p:{prop.Key}={prop.Value}");
                }
            }

            var argsString = string.Join(" ", args);
            _logger.LogDebug("Build command: dotnet {Args}", argsString);

            var result = await RunProcessAsync(
                "dotnet",
                argsString,
                Path.GetDirectoryName(solutionPath)!,
                cancellationToken,
                timeout: TimeSpan.FromMinutes(manifest.Deployment?.Resources?.Timeout ?? 60));

            if (result.ExitCode == 0)
            {
                buildTarget.Success = true;
                buildTarget.OutputPath = targetOutputDir;

                // Calculate binary size
                var binaryFiles = Directory.GetFiles(targetOutputDir, "*", SearchOption.AllDirectories);
                buildTarget.BinarySize = binaryFiles.Sum(f => new FileInfo(f).Length);

                // Generate image tag if registry configured
                if (target.Registry != null)
                {
                    buildTarget.ImageTag = target.Registry.TagStrategy switch
                    {
                        "manifest-hash" => ManifestHasher.GenerateImageTag(manifest, target.Id),
                        "semantic-version" => ManifestHasher.GenerateSemanticTag(manifest),
                        "commit-sha" => await GetCurrentCommitShaAsync(Path.GetDirectoryName(solutionPath)!),
                        _ => ManifestHasher.GenerateImageTag(manifest, target.Id)
                    };
                }

                _logger.LogInformation("Build succeeded for {TargetId}: {Size} bytes in {Duration}s",
                    target.Id, buildTarget.BinarySize, startTime.Elapsed.TotalSeconds);
            }
            else
            {
                buildTarget.Success = false;
                buildTarget.ErrorMessage = result.Error;
                _logger.LogError("Build failed for {TargetId}: {Error}", target.Id, result.Error);
            }
        }
        catch (Exception ex)
        {
            buildTarget.Success = false;
            buildTarget.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Build exception for {TargetId}", target.Id);
        }
        finally
        {
            startTime.Stop();
            buildTarget.Duration = startTime.Elapsed.TotalSeconds;
        }

        return buildTarget;
    }

    /// <summary>
    /// Applies build optimization flags to the MSBuild arguments.
    /// </summary>
    private void ApplyOptimizations(List<string> args, BuildOptimizations opts, CloudTarget target)
    {
        if (opts.Pgo)
        {
            args.Add("-p:TieredPGO=true");
        }

        if (opts.DynamicPgo)
        {
            args.Add("-p:DynamicPGO=true");
        }

        if (opts.TieredCompilation)
        {
            args.Add("-p:TieredCompilation=true");
        }

        if (opts.ReadyToRun)
        {
            args.Add("-p:PublishReadyToRun=true");
        }

        if (opts.Trim)
        {
            args.Add("-p:PublishTrimmed=true");
            args.Add("-p:TrimMode=link");
        }

        // Apply vectorization instruction sets
        if (!string.IsNullOrWhiteSpace(opts.Vectorization))
        {
            switch (opts.Vectorization.ToLowerInvariant())
            {
                case "neon":
                    args.Add("-p:IlcInstructionSet=neon");
                    break;
                case "avx2":
                    args.Add("-p:IlcInstructionSet=avx2");
                    break;
                case "avx512":
                    args.Add("-p:IlcInstructionSet=avx2,avx512f");
                    break;
            }
        }

        // Apply optimization preference
        switch (opts.OptimizationPreference.ToLowerInvariant())
        {
            case "size":
                args.Add("-p:OptimizationPreference=Size");
                args.Add("-p:IlcOptimizationPreference=Size");
                break;
            case "speed":
                args.Add("-p:OptimizationPreference=Speed");
                args.Add("-p:IlcOptimizationPreference=Speed");
                break;
        }

        // Apply custom IL compiler options
        if (opts.IlcOptions != null && opts.IlcOptions.Count > 0)
        {
            foreach (var option in opts.IlcOptions)
            {
                args.Add($"-p:IlcArg={option}");
            }
        }

        // Cloud-specific optimizations
        switch (target.Compute.ToLowerInvariant())
        {
            case "graviton2":
            case "graviton3":
                args.Add("-p:IlcInstructionSet=neon");
                break;
            case "ampere":
                args.Add("-p:IlcInstructionSet=neon");
                break;
        }
    }

    /// <summary>
    /// Pushes a container image to the configured registry.
    /// </summary>
    private async Task PushToRegistryAsync(
        CloudTarget target,
        string imageTag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target.Registry);

        _logger.LogInformation("Pushing image {Tag} to {Registry}",
            imageTag, target.Registry.Url);

        var fullImageName = $"{target.Registry.Url}/{target.Registry.Repository}:{imageTag}";

        // Authenticate to registry if credentials provided
        if (!string.IsNullOrWhiteSpace(target.Registry.CredentialsVariable))
        {
            var credentials = Environment.GetEnvironmentVariable(target.Registry.CredentialsVariable);
            if (!string.IsNullOrWhiteSpace(credentials))
            {
                // Handle different registry types
                if (target.Registry.Url.Contains("ecr"))
                {
                    await AuthenticateToEcrAsync(target.Registry.Url, cancellationToken);
                }
                else if (target.Registry.Url.Contains("gcr.io") || target.Registry.Url.Contains("artifact"))
                {
                    await AuthenticateToGcrAsync(credentials, cancellationToken);
                }
                else
                {
                    await AuthenticateToDockerRegistryAsync(target.Registry.Url, credentials, cancellationToken);
                }
            }
        }

        // Push image
        var result = await RunProcessAsync(
            "docker",
            $"push {fullImageName}",
            Environment.CurrentDirectory,
            cancellationToken);

        if (result.ExitCode == 0)
        {
            _logger.LogInformation("Successfully pushed image {Tag}", imageTag);

            // Push additional tags if configured
            if (target.Registry.Tags != null)
            {
                foreach (var additionalTag in target.Registry.Tags)
                {
                    var additionalImageName = $"{target.Registry.Url}/{target.Registry.Repository}:{additionalTag}";
                    await RunProcessAsync("docker", $"tag {fullImageName} {additionalImageName}",
                        Environment.CurrentDirectory, cancellationToken);
                    await RunProcessAsync("docker", $"push {additionalImageName}",
                        Environment.CurrentDirectory, cancellationToken);
                }
            }
        }
        else
        {
            throw new InvalidOperationException($"Failed to push image: {result.Error}");
        }
    }

    private async Task AuthenticateToEcrAsync(string registryUrl, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Authenticating to AWS ECR");
        var result = await RunProcessAsync(
            "aws",
            "ecr get-login-password",
            Environment.CurrentDirectory,
            cancellationToken);

        if (result.ExitCode == 0)
        {
            var password = result.Output.Trim();
            await RunProcessAsync(
                "docker",
                $"login --username AWS --password-stdin {registryUrl}",
                Environment.CurrentDirectory,
                cancellationToken,
                stdinInput: password);
        }
    }

    private Task AuthenticateToGcrAsync(string credentials, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Authenticating to GCR");
        return RunProcessAsync(
            "docker",
            "login -u _json_key --password-stdin https://gcr.io",
            Environment.CurrentDirectory,
            cancellationToken,
            stdinInput: credentials);
    }

    private Task AuthenticateToDockerRegistryAsync(string registryUrl, string credentials, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Authenticating to Docker registry");
        return RunProcessAsync(
            "docker",
            $"login --password-stdin {registryUrl}",
            Environment.CurrentDirectory,
            cancellationToken,
            stdinInput: credentials);
    }

    /// <summary>
    /// Gets the current git commit SHA from a repository.
    /// </summary>
    private Task<string> GetCurrentCommitShaAsync(string repositoryPath)
    {
        return Task.Run(() =>
        {
            using var repository = new Repository(repositoryPath);
            return repository.Head.Tip.Sha[..7];
        });
    }

    /// <summary>
    /// Runs an external process and captures output.
    /// </summary>
    private async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null,
        string? stdinInput = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdinInput != null,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                _logger.LogDebug("{Output}", e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _logger.LogDebug("{Error}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (stdinInput != null)
        {
            await process.StandardInput.WriteLineAsync(stdinInput);
            process.StandardInput.Close();
        }

        var timeoutMs = (int)(timeout?.TotalMilliseconds ?? 120000);

        try
        {
            await process.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Process {fileName} timed out after {timeoutMs}ms");
        }

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString()
        };
    }

    private sealed record ProcessResult
    {
        public required int ExitCode { get; init; }
        public required string Output { get; init; }
        public required string Error { get; init; }
    }
}
