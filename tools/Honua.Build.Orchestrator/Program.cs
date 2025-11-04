using System.CommandLine;
using System.Text.Json;
using Honua.Build.Orchestrator;
using Honua.Build.Orchestrator.Models;
using Microsoft.Extensions.Logging;

// Configure logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();

// Root command
var rootCommand = new RootCommand("Honua Build Orchestrator - Cross-repository GIS server build system");

// Build command
var buildCommand = new Command("build", "Execute a build from a manifest file");

var manifestOption = new Option<FileInfo>(
    aliases: new[] { "--manifest", "-m" },
    description: "Path to the build manifest JSON file")
{
    IsRequired = true
};
manifestOption.AddValidator(result =>
{
    var file = result.GetValueForOption(manifestOption);
    if (file != null && !file.Exists)
    {
        result.ErrorMessage = $"Manifest file not found: {file.FullName}";
    }
});

var outputOption = new Option<DirectoryInfo>(
    aliases: new[] { "--output", "-o" },
    description: "Output directory for build artifacts")
{
    IsRequired = true
};

var workspaceOption = new Option<DirectoryInfo>(
    aliases: new[] { "--workspace", "-w" },
    description: "Workspace directory for cloning repositories",
    getDefaultValue: () => new DirectoryInfo(Path.Combine(Path.GetTempPath(), "honua-builds")));

var verboseOption = new Option<bool>(
    aliases: new[] { "--verbose", "-v" },
    description: "Enable verbose logging",
    getDefaultValue: () => false);

buildCommand.AddOption(manifestOption);
buildCommand.AddOption(outputOption);
buildCommand.AddOption(workspaceOption);
buildCommand.AddOption(verboseOption);

buildCommand.SetHandler(async (manifestFile, outputDir, workspaceDir, verbose) =>
{
    try
    {
        // Adjust logging level if verbose
        if (verbose)
        {
            loggerFactory.Dispose();
            using var verboseLoggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });
            var verboseLogger = verboseLoggerFactory.CreateLogger<BuildOrchestrator>();

            await ExecuteBuildAsync(manifestFile, outputDir, workspaceDir, verboseLogger);
        }
        else
        {
            var buildLogger = loggerFactory.CreateLogger<BuildOrchestrator>();
            await ExecuteBuildAsync(manifestFile, outputDir, workspaceDir, buildLogger);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Build failed");
        Environment.Exit(1);
    }
}, manifestOption, outputOption, workspaceOption, verboseOption);

// Validate command
var validateCommand = new Command("validate", "Validate a build manifest file");

var validateManifestOption = new Option<FileInfo>(
    aliases: new[] { "--manifest", "-m" },
    description: "Path to the build manifest JSON file to validate")
{
    IsRequired = true
};

validateCommand.AddOption(validateManifestOption);

validateCommand.SetHandler(async (manifestFile) =>
{
    try
    {
        logger.LogInformation("Validating manifest: {Path}", manifestFile.FullName);

        var manifest = await LoadManifestAsync(manifestFile);

        // Perform validation
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(manifest.Id))
            errors.Add("Manifest ID is required");

        if (string.IsNullOrWhiteSpace(manifest.Version))
            errors.Add("Manifest version is required");

        if (string.IsNullOrWhiteSpace(manifest.Name))
            errors.Add("Manifest name is required");

        if (manifest.Repositories == null || manifest.Repositories.Count == 0)
            errors.Add("At least one repository is required");

        if (manifest.Modules == null || manifest.Modules.Count == 0)
            errors.Add("At least one module is required");

        if (manifest.Targets == null || manifest.Targets.Count == 0)
            errors.Add("At least one build target is required");

        // Validate repositories
        foreach (var repo in manifest.Repositories ?? new List<RepositoryReference>())
        {
            if (string.IsNullOrWhiteSpace(repo.Name))
                errors.Add("Repository name is required");

            if (string.IsNullOrWhiteSpace(repo.Url))
                errors.Add($"Repository URL is required for {repo.Name}");

            if (repo.Access == "private" && string.IsNullOrWhiteSpace(repo.Credentials))
                errors.Add($"Credentials variable required for private repository {repo.Name}");
        }

        // Validate targets
        foreach (var target in manifest.Targets ?? new List<CloudTarget>())
        {
            if (string.IsNullOrWhiteSpace(target.Id))
                errors.Add("Target ID is required");

            if (string.IsNullOrWhiteSpace(target.Provider))
                errors.Add($"Target provider is required for {target.Id}");

            if (string.IsNullOrWhiteSpace(target.Compute))
                errors.Add($"Target compute type is required for {target.Id}");

            if (string.IsNullOrWhiteSpace(target.Architecture))
                errors.Add($"Target architecture is required for {target.Id}");

            // Validate architecture format
            var validArchitectures = new[] { "linux-x64", "linux-arm64", "linux-musl-x64", "linux-musl-arm64", "win-x64", "win-arm64", "osx-x64", "osx-arm64" };
            if (!validArchitectures.Contains(target.Architecture))
                errors.Add($"Invalid architecture '{target.Architecture}' for target {target.Id}. Must be one of: {string.Join(", ", validArchitectures)}");
        }

        if (errors.Count > 0)
        {
            logger.LogError("Manifest validation failed with {Count} errors:", errors.Count);
            foreach (var error in errors)
            {
                logger.LogError("  - {Error}", error);
            }
            Environment.Exit(1);
        }

        // Compute manifest hash
        var hash = ManifestHasher.ComputeHash(manifest);

        logger.LogInformation("Manifest validation succeeded!");
        logger.LogInformation("Manifest hash: {Hash}", hash);
        logger.LogInformation("Repositories: {Count}", manifest.Repositories.Count);
        logger.LogInformation("Modules: {Count}", manifest.Modules.Count);
        logger.LogInformation("Build targets: {Count}", manifest.Targets.Count);

        // Display target details
        foreach (var target in manifest.Targets)
        {
            logger.LogInformation("  - {Id}: {Provider}/{Compute} ({Arch})",
                target.Id, target.Provider, target.Compute, target.Architecture);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Manifest validation failed");
        Environment.Exit(1);
    }
}, validateManifestOption);

// Hash command
var hashCommand = new Command("hash", "Compute the hash of a build manifest");

var hashManifestOption = new Option<FileInfo>(
    aliases: new[] { "--manifest", "-m" },
    description: "Path to the build manifest JSON file")
{
    IsRequired = true
};

hashCommand.AddOption(hashManifestOption);

hashCommand.SetHandler(async (manifestFile) =>
{
    try
    {
        var manifest = await LoadManifestAsync(manifestFile);
        var hash = ManifestHasher.ComputeHash(manifest);

        Console.WriteLine(hash);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Manifest hash: {Hash}", hash);

            // Show cache keys for each target
            foreach (var target in manifest.Targets)
            {
                var cacheKey = ManifestHasher.ComputeCacheKey(manifest, target.Id);
                var imageTag = ManifestHasher.GenerateImageTag(manifest, target.Id);
                logger.LogInformation("Target {Id}:", target.Id);
                logger.LogInformation("  Cache key: {CacheKey}", cacheKey);
                logger.LogInformation("  Image tag: {ImageTag}", imageTag);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to compute manifest hash");
        Environment.Exit(1);
    }
}, hashManifestOption);

// Add commands to root
rootCommand.AddCommand(buildCommand);
rootCommand.AddCommand(validateCommand);
rootCommand.AddCommand(hashCommand);

// Execute
return await rootCommand.InvokeAsync(args);

// Helper methods

static async Task<BuildManifest> LoadManifestAsync(FileInfo manifestFile)
{
    var json = await File.ReadAllTextAsync(manifestFile.FullName);
    var manifest = JsonSerializer.Deserialize<BuildManifest>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    });

    if (manifest == null)
    {
        throw new InvalidOperationException("Failed to deserialize manifest");
    }

    return manifest;
}

static async Task ExecuteBuildAsync(
    FileInfo manifestFile,
    DirectoryInfo outputDir,
    DirectoryInfo workspaceDir,
    ILogger<BuildOrchestrator> buildLogger)
{
    buildLogger.LogInformation("Loading manifest from: {Path}", manifestFile.FullName);

    var manifest = await LoadManifestAsync(manifestFile);

    buildLogger.LogInformation("Loaded manifest: {Name} (v{Version})", manifest.Name, manifest.Version);

    // Create orchestrator
    var orchestrator = new BuildOrchestrator(buildLogger, workspaceDir.FullName);

    // Execute build
    var results = await orchestrator.ExecuteBuildAsync(manifest, outputDir.FullName);

    // Display results
    buildLogger.LogInformation("Build Results:");
    buildLogger.LogInformation("═══════════════════════════════════════════════════════");

    foreach (var result in results)
    {
        var status = result.Success ? "✓ SUCCESS" : "✗ FAILED";
        buildLogger.LogInformation("{Status} {Target}", status, result.TargetId);

        if (result.Success)
        {
            buildLogger.LogInformation("  Duration: {Duration:F2}s", result.Duration);
            if (result.BinarySize.HasValue)
            {
                buildLogger.LogInformation("  Size: {Size:N0} bytes ({SizeMB:F2} MB)",
                    result.BinarySize.Value, result.BinarySize.Value / 1024.0 / 1024.0);
            }
            if (!string.IsNullOrWhiteSpace(result.OutputPath))
            {
                buildLogger.LogInformation("  Output: {Path}", result.OutputPath);
            }
            if (!string.IsNullOrWhiteSpace(result.ImageTag))
            {
                buildLogger.LogInformation("  Image: {Tag}", result.ImageTag);
            }
        }
        else
        {
            buildLogger.LogError("  Error: {Error}", result.ErrorMessage);
        }

        buildLogger.LogInformation("");
    }

    var successCount = results.Count(r => r.Success);
    var failureCount = results.Count - successCount;

    buildLogger.LogInformation("═══════════════════════════════════════════════════════");
    buildLogger.LogInformation("Total: {Success} succeeded, {Failed} failed", successCount, failureCount);

    if (failureCount > 0)
    {
        Environment.Exit(1);
    }
}
