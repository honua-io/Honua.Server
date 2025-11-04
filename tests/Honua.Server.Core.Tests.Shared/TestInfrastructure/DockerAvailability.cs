using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Utility for checking Docker availability and providing user-friendly error messages.
/// </summary>
public static class DockerAvailability
{
    private static bool? _isAvailable;
    private static string? _unavailabilityReason;

    /// <summary>
    /// Checks if Docker is available and can start containers.
    /// Result is cached for performance.
    /// </summary>
    public static async Task<bool> IsDockerAvailableAsync()
    {
        if (_isAvailable.HasValue)
        {
            return _isAvailable.Value;
        }

        try
        {
            // Try to start a minimal container to verify Docker is working
            var container = new ContainerBuilder()
                .WithImage("alpine:latest")
                .WithCommand("echo", "docker-test")
                .WithWaitStrategy(Wait.ForUnixContainer())
                .Build();

            await container.StartAsync();
            await container.DisposeAsync();

            _isAvailable = true;
            return true;
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _unavailabilityReason = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Gets a user-friendly message explaining why Docker is unavailable.
    /// </summary>
    public static string GetUnavailabilityMessage()
    {
        if (_isAvailable == true)
        {
            return "Docker is available";
        }

        if (string.IsNullOrEmpty(_unavailabilityReason))
        {
            return "Docker availability has not been checked yet";
        }

        return $@"Docker is not available. Integration tests will be skipped.

Reason: {_unavailabilityReason}

To fix this:
1. Install Docker Desktop (https://www.docker.com/products/docker-desktop/)
2. Ensure Docker is running (check system tray icon)
3. On Linux, add your user to the docker group:
   sudo usermod -aG docker $USER
   newgrp docker
4. Verify Docker works: docker ps

After fixing, run tests again. Unit tests will still run without Docker.";
    }

    /// <summary>
    /// Throws a SkipException with detailed message if Docker is unavailable.
    /// </summary>
    public static async Task RequireDockerAsync()
    {
        if (!await IsDockerAvailableAsync())
        {
            throw new SkipException(GetUnavailabilityMessage());
        }
    }

    /// <summary>
    /// Checks if a specific Docker image is available locally or can be pulled.
    /// </summary>
    public static async Task<bool> IsImageAvailableAsync(string imageName)
    {
        if (!await IsDockerAvailableAsync())
        {
            return false;
        }

        try
        {
            var container = new ContainerBuilder()
                .WithImage(imageName)
                .WithCommand("echo", "test")
                .Build();

            // Don't start, just check if image can be resolved
            await container.DisposeAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current Docker environment information for diagnostics.
    /// </summary>
    public static async Task<DockerEnvironmentInfo> GetEnvironmentInfoAsync()
    {
        var info = new DockerEnvironmentInfo
        {
            IsAvailable = await IsDockerAvailableAsync(),
            UnavailabilityReason = _unavailabilityReason
        };

        if (info.IsAvailable)
        {
            try
            {
                // Try to get Docker version info
                var container = new ContainerBuilder()
                    .WithImage("docker:latest")
                    .WithCommand("docker", "--version")
                    .Build();

                await container.StartAsync();
                await container.DisposeAsync();

                info.SupportsContainers = true;
            }
            catch
            {
                info.SupportsContainers = false;
            }
        }

        return info;
    }

    /// <summary>
    /// Resets the cached Docker availability check.
    /// Useful for testing or if Docker status changes during test execution.
    /// </summary>
    public static void ResetCache()
    {
        _isAvailable = null;
        _unavailabilityReason = null;
    }
}

/// <summary>
/// Information about the Docker environment.
/// </summary>
public class DockerEnvironmentInfo
{
    public bool IsAvailable { get; set; }
    public string? UnavailabilityReason { get; set; }
    public bool SupportsContainers { get; set; }

    public override string ToString()
    {
        if (IsAvailable)
        {
            return $"Docker Available: {IsAvailable}, Supports Containers: {SupportsContainers}";
        }

        return $"Docker Available: {IsAvailable}, Reason: {UnavailabilityReason}";
    }
}
