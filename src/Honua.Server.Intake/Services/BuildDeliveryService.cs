// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Intake.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Honua.Server.Intake.Services;

/// <summary>
/// Interface for build delivery operations.
/// </summary>
public interface IBuildDeliveryService
{
    /// <summary>
    /// Delivers a build to the customer's registry.
    /// Checks cache first, builds if needed, and copies to customer registry.
    /// </summary>
    Task<BuildDeliveryResult> DeliverBuildAsync(
        BuildCacheKey cacheKey,
        RegistryType targetRegistry,
        string? sourceBuildPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies an image between registries without local pull.
    /// </summary>
    Task<bool> CopyImageAsync(
        string sourceImage,
        string targetImage,
        RegistryCredential? sourceCredential = null,
        RegistryCredential? targetCredential = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates additional tags for an image.
    /// </summary>
    Task<IReadOnlyList<string>> TagImageAsync(
        string imageReference,
        IEnumerable<string> additionalTags,
        RegistryCredential? credential = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for build delivery.
/// </summary>
public sealed class BuildDeliveryOptions
{
    /// <summary>
    /// Path to crane binary (Google's container tool).
    /// </summary>
    public string CranePath { get; init; } = "crane";

    /// <summary>
    /// Path to skopeo binary (alternative image copy tool).
    /// </summary>
    public string SkopeoPath { get; init; } = "skopeo";

    /// <summary>
    /// Preferred tool for image operations (crane or skopeo).
    /// </summary>
    public string PreferredTool { get; init; } = "crane";

    /// <summary>
    /// Timeout for image copy operations in seconds.
    /// </summary>
    public int CopyTimeoutSeconds { get; init; } = 600;

    /// <summary>
    /// Whether to automatically tag with 'latest'.
    /// </summary>
    public bool AutoTagLatest { get; init; } = true;

    /// <summary>
    /// Whether to automatically create architecture-specific tags.
    /// </summary>
    public bool AutoTagArchitecture { get; init; } = true;
}

/// <summary>
/// Delivers builds to customer registries with caching and optimization.
/// </summary>
public sealed class BuildDeliveryService : IBuildDeliveryService
{
    private readonly ILogger<BuildDeliveryService> logger;
    private readonly BuildDeliveryOptions options;
    private readonly IRegistryCacheChecker cacheChecker;
    private readonly IRegistryAccessManager accessManager;
    private readonly RegistryProvisioningOptions registryOptions;
    private readonly AsyncRetryPolicy retryPolicy;

    public BuildDeliveryService(
        ILogger<BuildDeliveryService> logger,
        IOptions<BuildDeliveryOptions> options,
        IRegistryCacheChecker cacheChecker,
        IRegistryAccessManager accessManager,
        IOptions<RegistryProvisioningOptions> registryOptions)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.Value ?? new BuildDeliveryOptions();
        this.cacheChecker = cacheChecker ?? throw new ArgumentNullException(nameof(cacheChecker));
        this.accessManager = accessManager ?? throw new ArgumentNullException(nameof(accessManager));
        this.registryOptions = registryOptions?.Value ?? throw new ArgumentNullException(nameof(registryOptions));

        // Configure retry policy with exponential backoff
        this.retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    this.logger.LogWarning(
                        exception,
                        "Retry {RetryCount} after {Delay}ms due to transient error",
                        retryCount,
                        timeSpan.TotalMilliseconds);
                });
    }

    /// <inheritdoc/>
    public async Task<BuildDeliveryResult> DeliverBuildAsync(
        BuildCacheKey cacheKey,
        RegistryType targetRegistry,
        string? sourceBuildPath = null,
        CancellationToken cancellationToken = default)
    {
        if (cacheKey == null)
        {
            throw new ArgumentNullException(nameof(cacheKey));
        }

        this.logger.LogInformation(
            "Starting build delivery for {BuildName}:{Version} to {RegistryType}",
            cacheKey.BuildName,
            cacheKey.Version,
            targetRegistry);

        // 1. Validate access
        var accessResult = await this.accessManager.ValidateAccessAsync(
            cacheKey.CustomerId,
            targetRegistry,
            cancellationToken);

        if (!accessResult.AccessGranted)
        {
            this.logger.LogWarning(
                "Access denied for customer {CustomerId}: {Reason}",
                cacheKey.CustomerId,
                accessResult.DenialReason);

            return new BuildDeliveryResult
            {
                Success = false,
                CacheKey = cacheKey,
                TargetRegistry = targetRegistry,
                ErrorMessage = accessResult.DenialReason ?? "Access denied",
                CompletedAt = DateTimeOffset.UtcNow
            };
        }

        // 2. Check if build exists in cache
        var cacheResult = await this.cacheChecker.CheckCacheAsync(
            cacheKey,
            targetRegistry,
            cancellationToken);

        if (cacheResult.Exists)
        {
            this.logger.LogInformation(
                "Build {BuildName}:{Version} found in cache: {ImageReference}",
                cacheKey.BuildName,
                cacheKey.Version,
                cacheResult.ImageReference);

            return new BuildDeliveryResult
            {
                Success = true,
                CacheKey = cacheKey,
                TargetRegistry = targetRegistry,
                ImageReference = cacheResult.ImageReference,
                Digest = cacheResult.Digest,
                WasCached = true,
                CompletedAt = DateTimeOffset.UtcNow
            };
        }

        // 3. Build is not cached, need to build or copy
        this.logger.LogInformation(
            "Build {BuildName}:{Version} not in cache, preparing to build/copy",
            cacheKey.BuildName,
            cacheKey.Version);

        try
        {
            // In a real implementation, this would:
            // a) Build the container image if sourceBuildPath is provided
            // b) Copy from a source registry to the target registry
            // For now, we'll simulate the copy operation

            var targetImageReference = GenerateImageReference(cacheKey, targetRegistry);

            // Simulate building or copying
            string? sourceImage = null;
            if (!string.IsNullOrWhiteSpace(sourceBuildPath))
            {
                this.logger.LogInformation(
                    "Building image from {SourcePath}",
                    sourceBuildPath);

                // In production, this would run docker build or similar
                sourceImage = $"local-build:{cacheKey.GenerateTag()}";
            }
            else
            {
                // Assume we're copying from a central build registry
                sourceImage = $"registry.honua.io/builds/{cacheKey.BuildName}:{cacheKey.Version}";
            }

            // Copy image to target registry
            var copySuccess = await CopyImageAsync(
                sourceImage,
                targetImageReference,
                null, // Source credential would be provided in production
                null, // Target credential would be obtained from access manager
                cancellationToken);

            if (!copySuccess)
            {
                return new BuildDeliveryResult
                {
                    Success = false,
                    CacheKey = cacheKey,
                    TargetRegistry = targetRegistry,
                    ErrorMessage = "Failed to copy image to target registry",
                    CompletedAt = DateTimeOffset.UtcNow
                };
            }

            // 4. Create additional tags
            var additionalTags = new List<string>();
            if (this.options.AutoTagLatest)
            {
                additionalTags.Add("latest");
            }

            if (this.options.AutoTagArchitecture && !string.IsNullOrWhiteSpace(cacheKey.Architecture))
            {
                additionalTags.Add($"{cacheKey.Version}-{cacheKey.Architecture}");
            }

            var appliedTags = await TagImageAsync(
                targetImageReference,
                additionalTags,
                null, // Credential would be obtained from access manager
                cancellationToken);

            this.logger.LogInformation(
                "Successfully delivered build {BuildName}:{Version} to {ImageReference}",
                cacheKey.BuildName,
                cacheKey.Version,
                targetImageReference);

            return new BuildDeliveryResult
            {
                Success = true,
                CacheKey = cacheKey,
                TargetRegistry = targetRegistry,
                ImageReference = targetImageReference,
                WasCached = false,
                AdditionalTags = appliedTags.ToList(),
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to deliver build {BuildName}", cacheKey.BuildName);
            return new BuildDeliveryResult
            {
                Success = false,
                CacheKey = cacheKey,
                TargetRegistry = targetRegistry,
                ErrorMessage = ex.Message,
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CopyImageAsync(
        string sourceImage,
        string targetImage,
        RegistryCredential? sourceCredential = null,
        RegistryCredential? targetCredential = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceImage))
        {
            throw new ArgumentException("Source image cannot be null or empty", nameof(sourceImage));
        }

        if (string.IsNullOrWhiteSpace(targetImage))
        {
            throw new ArgumentException("Target image cannot be null or empty", nameof(targetImage));
        }

        this.logger.LogInformation(
            "Copying image from {SourceImage} to {TargetImage}",
            sourceImage,
            targetImage);

        return await this.retryPolicy.ExecuteAsync(async () =>
        {
            if (this.options.PreferredTool.Equals("crane", StringComparison.OrdinalIgnoreCase))
            {
                return await CopyWithCraneAsync(
                    sourceImage,
                    targetImage,
                    sourceCredential,
                    targetCredential,
                    cancellationToken);
            }
            else
            {
                return await CopyWithSkopeoAsync(
                    sourceImage,
                    targetImage,
                    sourceCredential,
                    targetCredential,
                    cancellationToken);
            }
        });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> TagImageAsync(
        string imageReference,
        IEnumerable<string> additionalTags,
        RegistryCredential? credential = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
        {
            throw new ArgumentException("Image reference cannot be null or empty", nameof(imageReference));
        }

        if (additionalTags == null || !additionalTags.Any())
        {
            return Array.Empty<string>();
        }

        this.logger.LogInformation(
            "Tagging image {ImageReference} with {TagCount} additional tags",
            imageReference,
            additionalTags.Count());

        var appliedTags = new List<string>();

        foreach (var tag in additionalTags)
        {
            try
            {
                // Extract base reference without tag
                var lastColonIndex = imageReference.LastIndexOf(':');
                var baseReference = lastColonIndex > 0
                    ? imageReference[..lastColonIndex]
                    : imageReference;

                var newReference = $"{baseReference}:{tag}";

                if (this.options.PreferredTool.Equals("crane", StringComparison.OrdinalIgnoreCase))
                {
                    await TagWithCraneAsync(imageReference, newReference, credential, cancellationToken);
                }
                else
                {
                    await CopyWithSkopeoAsync(imageReference, newReference, credential, credential, cancellationToken);
                }

                appliedTags.Add(tag);
                this.logger.LogInformation("Applied tag {Tag} to image", tag);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to apply tag {Tag}", tag);
            }
        }

        return appliedTags;
    }

    /// <summary>
    /// Copies an image using crane tool.
    /// </summary>
    private async Task<bool> CopyWithCraneAsync(
        string sourceImage,
        string targetImage,
        RegistryCredential? sourceCredential,
        RegistryCredential? targetCredential,
        CancellationToken cancellationToken)
    {
        var args = new StringBuilder("copy");
        args.Append($" {sourceImage}");
        args.Append($" {targetImage}");

        // Add authentication if provided
        var envVars = new Dictionary<string, string>();
        if (sourceCredential != null)
        {
            envVars["CRANE_SOURCE_USERNAME"] = sourceCredential.Username;
            envVars["CRANE_SOURCE_PASSWORD"] = sourceCredential.Password;
        }

        if (targetCredential != null)
        {
            envVars["CRANE_TARGET_USERNAME"] = targetCredential.Username;
            envVars["CRANE_TARGET_PASSWORD"] = targetCredential.Password;
        }

        this.logger.LogDebug("Executing crane copy: {Args}", args.ToString());

        var result = await ExecuteProcessAsync(
            this.options.CranePath,
            args.ToString(),
            envVars,
            this.options.CopyTimeoutSeconds,
            cancellationToken);

        if (result.ExitCode == 0)
        {
            this.logger.LogInformation("Successfully copied image with crane");
            return true;
        }

        this.logger.LogError(
            "Crane copy failed with exit code {ExitCode}: {Error}",
            result.ExitCode,
            result.StandardError);

        return false;
    }

    /// <summary>
    /// Copies an image using skopeo tool.
    /// </summary>
    private async Task<bool> CopyWithSkopeoAsync(
        string sourceImage,
        string targetImage,
        RegistryCredential? sourceCredential,
        RegistryCredential? targetCredential,
        CancellationToken cancellationToken)
    {
        string? authFilePath = null;

        try
        {
            var args = new StringBuilder("copy");
            var envVars = new Dictionary<string, string>();

            // Use secure credential file instead of command-line arguments
            if (sourceCredential != null || targetCredential != null)
            {
                authFilePath = await CreateSecureAuthFileAsync(
                    sourceImage,
                    targetImage,
                    sourceCredential,
                    targetCredential,
                    cancellationToken);

                envVars["REGISTRY_AUTH_FILE"] = authFilePath;
            }

            args.Append($" docker://{sourceImage}");
            args.Append($" docker://{targetImage}");

            // Log sanitized command (credentials are not in args)
            this.logger.LogDebug("Executing skopeo copy: {Args}", args.ToString());

            var result = await ExecuteProcessAsync(
                this.options.SkopeoPath,
                args.ToString(),
                envVars.Count > 0 ? envVars : null,
                this.options.CopyTimeoutSeconds,
                cancellationToken);

            if (result.ExitCode == 0)
            {
                this.logger.LogInformation("Successfully copied image with skopeo");
                return true;
            }

            this.logger.LogError(
                "Skopeo copy failed with exit code {ExitCode}: {Error}",
                result.ExitCode,
                result.StandardError);

            return false;
        }
        finally
        {
            // Clean up temporary auth file
            if (authFilePath != null)
            {
                await CleanupAuthFileAsync(authFilePath);
            }
        }
    }

    /// <summary>
    /// Tags an image using crane tool.
    /// </summary>
    private async Task<bool> TagWithCraneAsync(
        string sourceImage,
        string targetImage,
        RegistryCredential? credential,
        CancellationToken cancellationToken)
    {
        var args = $"tag {sourceImage} {targetImage}";

        var envVars = new Dictionary<string, string>();
        if (credential != null)
        {
            envVars["CRANE_USERNAME"] = credential.Username;
            envVars["CRANE_PASSWORD"] = credential.Password;
        }

        this.logger.LogDebug("Executing crane tag: {Args}", args);

        var result = await ExecuteProcessAsync(
            this.options.CranePath,
            args,
            envVars,
            30, // Tagging is quick
            cancellationToken);

        return result.ExitCode == 0;
    }

    /// <summary>
    /// Executes a process and captures output.
    /// </summary>
    private async Task<ProcessResult> ExecuteProcessAsync(
        string fileName,
        string arguments,
        Dictionary<string, string>? environmentVariables,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                process.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException(
                $"Process '{fileName}' timed out after {timeoutSeconds} seconds");
        }

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = outputBuilder.ToString(),
            StandardError = errorBuilder.ToString()
        };
    }

    /// <summary>
    /// Creates a secure authentication file for skopeo registry credentials.
    /// </summary>
    /// <remarks>
    /// Creates a temporary JSON file with registry credentials in Docker config format.
    /// The file is created with restricted permissions (0600 on Unix) to prevent unauthorized access.
    /// </remarks>
    private async Task<string> CreateSecureAuthFileAsync(
        string sourceImage,
        string targetImage,
        RegistryCredential? sourceCredential,
        RegistryCredential? targetCredential,
        CancellationToken cancellationToken)
    {
        // Create temporary file with unique name
        var authFilePath = Path.Combine(
            Path.GetTempPath(),
            $"skopeo-auth-{Guid.NewGuid()}.json");

        try
        {
            // Build auth configuration in Docker config format
            var auths = new Dictionary<string, object>();

            if (sourceCredential != null)
            {
                var sourceRegistry = ExtractRegistry(sourceImage);
                var sourceAuth = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{sourceCredential.Username}:{sourceCredential.Password}"));
                auths[sourceRegistry] = new { auth = sourceAuth };
            }

            if (targetCredential != null)
            {
                var targetRegistry = ExtractRegistry(targetImage);
                var targetAuth = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{targetCredential.Username}:{targetCredential.Password}"));
                auths[targetRegistry] = new { auth = targetAuth };
            }

            var authConfig = new { auths };

            // Write to temporary file
            var json = JsonSerializer.Serialize(authConfig, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            await File.WriteAllTextAsync(authFilePath, json, cancellationToken);

            // Set restricted file permissions on Unix systems
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    // Set permissions to 0600 (owner read/write only)
                    // Using chmod via process since Mono.Unix may not be available
                    var chmodProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"600 \"{authFilePath}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    chmodProcess.Start();
                    await chmodProcess.WaitForExitAsync(cancellationToken);

                    if (chmodProcess.ExitCode != 0)
                    {
                        this.logger.LogWarning(
                            "Failed to set secure permissions on auth file: {ExitCode}",
                            chmodProcess.ExitCode);
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to set secure permissions on auth file");
                }
            }

            this.logger.LogDebug("Created secure auth file at {Path}", authFilePath);
            return authFilePath;
        }
        catch (Exception ex)
        {
            // Clean up on failure
            try
            {
                if (File.Exists(authFilePath))
                {
                    File.Delete(authFilePath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            this.logger.LogError(ex, "Failed to create secure auth file");
            throw;
        }
    }

    /// <summary>
    /// Extracts the registry hostname from a full image reference.
    /// </summary>
    private static string ExtractRegistry(string imageReference)
    {
        // Remove docker:// prefix if present
        var image = imageReference.StartsWith("docker://")
            ? imageReference[9..]
            : imageReference;

        // Registry is everything before the first slash
        var firstSlash = image.IndexOf('/');
        if (firstSlash > 0)
        {
            var registry = image[..firstSlash];
            // Check if it contains a dot or port (indicates registry hostname)
            if (registry.Contains('.') || registry.Contains(':'))
            {
                return registry;
            }
        }

        // Default to docker.io for official images
        return "docker.io";
    }

    /// <summary>
    /// Securely deletes a temporary authentication file.
    /// </summary>
    private async Task CleanupAuthFileAsync(string authFilePath)
    {
        try
        {
            if (File.Exists(authFilePath))
            {
                // Overwrite file content before deletion for security
                var fileInfo = new FileInfo(authFilePath);
                if (fileInfo.Length > 0)
                {
                    await File.WriteAllBytesAsync(authFilePath, new byte[fileInfo.Length]);
                }

                File.Delete(authFilePath);
                this.logger.LogDebug("Cleaned up auth file at {Path}", authFilePath);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to clean up auth file at {Path}", authFilePath);
        }
    }

    /// <summary>
    /// Generates the full image reference for a cache key and target registry.
    /// </summary>
    private string GenerateImageReference(BuildCacheKey cacheKey, RegistryType registryType)
    {
        var (registryUrl, organization) = registryType switch
        {
            RegistryType.GitHubContainerRegistry => ("ghcr.io", this.registryOptions.GitHubOrganization ?? "honua"),
            RegistryType.AwsEcr => ($"{this.registryOptions.AwsAccountId}.dkr.ecr.{this.registryOptions.AwsRegion}.amazonaws.com", "honua"),
            RegistryType.AzureAcr => ($"{this.registryOptions.AzureRegistryName}.azurecr.io", "customers"),
            RegistryType.GcpArtifactRegistry => ($"{this.registryOptions.GcpRegion}-docker.pkg.dev", $"{this.registryOptions.GcpProjectId}/{this.registryOptions.GcpRepositoryName}"),
            _ => throw new NotSupportedException($"Registry type {registryType} is not supported")
        };

        return cacheKey.GenerateImageReference(registryUrl, organization);
    }

    /// <summary>
    /// Determines if an exception is transient and should be retried.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        return ex is TimeoutException
            || ex is IOException
            || ex is System.Net.Sockets.SocketException;
    }

    /// <summary>
    /// Result of a process execution.
    /// </summary>
    private sealed class ProcessResult
    {
        public int ExitCode { get; init; }
        public string StandardOutput { get; init; } = string.Empty;
        public string StandardError { get; init; } = string.Empty;
    }
}
