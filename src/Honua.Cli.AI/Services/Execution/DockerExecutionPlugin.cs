// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Execution;

public class DockerExecutionPlugin
{
    private readonly IPluginExecutionContext _context;

    public DockerExecutionPlugin(IPluginExecutionContext context)
    {
        _context = context;
    }

    [KernelFunction, Description("Run a Docker container")]
    public async Task<string> RunDockerContainer(
        [Description("Docker image name")] string image,
        [Description("Container name")] string containerName,
        [Description("Port mappings as JSON object {\"5432\":\"5432\"} or simple string \"5432:5432\"")] string ports = "{}",
        [Description("Environment variables as JSON object")] string environment = "{}")
    {
        // Validate inputs FIRST to prevent command injection
        try
        {
            CommandArgumentValidator.ValidateIdentifier(containerName, nameof(containerName));
            // Image name can contain slashes and colons (e.g., registry.io/repo/image:tag)
            if (image.IsNullOrWhiteSpace())
                throw new ArgumentException("Image name cannot be null or empty", nameof(image));
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Docker", "RunContainer", $"Validation failed: {ex.Message}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Build argument list safely
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add("--name");
        psi.ArgumentList.Add(containerName);

        // Parse and add port mappings
        if (ports.HasValue() && ports != "{}")
        {
            try
            {
                var portsObj = JsonSerializer.Deserialize<JsonElement>(ports);
                if (portsObj.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in portsObj.EnumerateObject())
                    {
                        // Strip /tcp, /udp, /sctp protocol suffixes from port mappings
                        var hostPort = prop.Name.Split('/')[0];
                        var containerPort = prop.Value.GetString()?.Split('/')[0] ?? prop.Value.GetString();
                        psi.ArgumentList.Add("-p");
                        psi.ArgumentList.Add($"{hostPort}:{containerPort}");
                    }
                }
            }
            catch (JsonException)
            {
                // Fall back to simple string format like "5432:5432" or "8080:80"
                var cleanPorts = ports.Trim();
                if (cleanPorts.Contains(":"))
                {
                    psi.ArgumentList.Add("-p");
                    psi.ArgumentList.Add(cleanPorts);
                }
            }
        }

        // Parse and add environment variables
        var envObj = JsonSerializer.Deserialize<JsonElement>(environment);
        if (envObj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in envObj.EnumerateObject())
            {
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add($"{prop.Name}={prop.Value.GetString()}");
            }
        }

        psi.ArgumentList.Add(image);

        if (_context.DryRun)
        {
            var commandPreview = string.Join(" ", psi.ArgumentList);
            _context.RecordAction("Docker", "RunContainer", $"[DRY-RUN] Would run: docker {commandPreview}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, containerName });
        }

        if (_context.RequireApproval)
        {
            var approved = await _context.RequestApprovalAsync(
                "Run Docker Container",
                $"Start container '{containerName}' from image '{image}'",
                new[] { containerName });

            if (!approved)
            {
                _context.RecordAction("Docker", "RunContainer", "User rejected Docker container creation", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            var result = await ExecuteDockerCommandAsync(psi);
            _context.RecordAction("Docker", "RunContainer", $"Started container {containerName}", true);

            return JsonSerializer.Serialize(new { success = true, containerName, containerId = result.Trim() });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Docker", "RunContainer", $"Failed to start container {containerName}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [KernelFunction, Description("Stop a Docker container")]
    public async Task<string> StopDockerContainer(
        [Description("Container name or ID")] string container)
    {
        // Validate input FIRST to prevent command injection
        try
        {
            CommandArgumentValidator.ValidateIdentifier(container, nameof(container));
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Docker", "StopContainer", $"Validation failed: {ex.Message}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }

        if (_context.DryRun)
        {
            _context.RecordAction("Docker", "StopContainer", $"[DRY-RUN] Would stop container: {container}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, container });
        }

        if (_context.RequireApproval)
        {
            var approved = await _context.RequestApprovalAsync(
                "Stop Docker Container",
                $"Stop container: {container}",
                new[] { container });

            if (!approved)
            {
                _context.RecordAction("Docker", "StopContainer", "User rejected stopping container", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("stop");
            psi.ArgumentList.Add(container);

            await ExecuteDockerCommandAsync(psi);
            _context.RecordAction("Docker", "StopContainer", $"Stopped container {container}", true);

            return JsonSerializer.Serialize(new { success = true, container });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Docker", "StopContainer", $"Failed to stop container {container}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [KernelFunction, Description("Execute docker-compose up")]
    public async Task<string> DockerComposeUp(
        [Description("Path to docker-compose.yml file relative to workspace")] string composePath,
        [Description("Run in detached mode")] bool detached = true)
    {
        // Validate path FIRST to prevent path traversal and command injection
        try
        {
            CommandArgumentValidator.ValidatePath(composePath, nameof(composePath));
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Docker", "ComposeUp", $"Validation failed: {ex.Message}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }

        var fullPath = System.IO.Path.Combine(_context.WorkspacePath, composePath);

        if (_context.DryRun)
        {
            _context.RecordAction("Docker", "ComposeUp", $"[DRY-RUN] Would run docker-compose up for: {fullPath}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, composePath, fullPath });
        }

        if (_context.RequireApproval)
        {
            var approved = await _context.RequestApprovalAsync(
                "Docker Compose Up",
                $"Start services from: {composePath}",
                new[] { fullPath });

            if (!approved)
            {
                _context.RecordAction("Docker", "ComposeUp", "User rejected docker-compose up", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker-compose",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(fullPath);
            psi.ArgumentList.Add("up");

            if (detached)
            {
                psi.ArgumentList.Add("-d");
            }

            var result = await ExecuteDockerCommandAsync(psi);
            _context.RecordAction("Docker", "ComposeUp", $"Started services from {composePath}", true);

            return JsonSerializer.Serialize(new { success = true, composePath, output = result });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Docker", "ComposeUp", $"Failed to start services from {composePath}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Execute docker command safely using ProcessStartInfo (preventing command injection)
    /// </summary>
    private async Task<string> ExecuteDockerCommandAsync(ProcessStartInfo psi)
    {
        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start docker process");

        // Read stdout and stderr concurrently to prevent deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdoutTask, stderrTask);

        var output = await stdoutTask;
        var error = await stderrTask;

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Docker command failed: {error}");

        return output;
    }
}
