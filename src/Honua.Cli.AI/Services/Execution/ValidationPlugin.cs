// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Execution;

public class ValidationPlugin
{
    private readonly IPluginExecutionContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public ValidationPlugin(IPluginExecutionContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    [KernelFunction, Description("Check if a Docker container is running")]
    public async Task<string> CheckDockerContainer(
        [Description("Container name or ID")] string container)
    {
        try
        {
            CommandArgumentValidator.ValidateContainerName(container, nameof(container));

            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("inspect");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("{{.State.Running}}");
            psi.ArgumentList.Add(container);

            var output = await ExecuteCommandAsync(psi);
            var isRunning = output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

            _context.RecordAction("Validation", "CheckContainer", $"Container {container} running: {isRunning}", true);

            return JsonSerializer.Serialize(new { success = true, container, isRunning, status = isRunning ? "running" : "stopped" });
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Validation", "CheckContainer", $"Failed to check container {container}", false, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _context.RecordAction("Validation", "CheckContainer", $"Failed to check container {container}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, container, error = ex.Message });
        }
    }

    [KernelFunction, Description("Check if an HTTP endpoint is accessible")]
    public async Task<string> CheckHttpEndpoint(
        [Description("URL to check")] string url,
        [Description("Timeout in seconds")] int timeoutSeconds = 30)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            _context.RecordAction("Validation", "CheckEndpoint", $"Endpoint {url} responded with {response.StatusCode}", true);

            return JsonSerializer.Serialize(new
            {
                success = true,
                url,
                statusCode = (int)response.StatusCode,
                statusText = response.StatusCode.ToString(),
                contentLength = content.Length,
                headers = response.Headers.ToString()
            });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Validation", "CheckEndpoint", $"Failed to reach endpoint {url}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, url, error = ex.Message });
        }
    }

    [KernelFunction, Description("Check if a file exists")]
    public Task<string> CheckFileExists(
        [Description("Path to file relative to workspace")] string path)
    {
        var fullPath = Path.Combine(_context.WorkspacePath, path);
        
        try
        {
            var exists = File.Exists(fullPath);
            long? size = exists ? new FileInfo(fullPath).Length : null;
            var modified = exists ? File.GetLastWriteTimeUtc(fullPath) : (DateTime?)null;

            _context.RecordAction("Validation", "CheckFile", $"File {path} exists: {exists}", true);

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, path, exists, size, modified }));
        }
        catch (Exception ex)
        {
            _context.RecordAction("Validation", "CheckFile", $"Failed to check file {path}", false, ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, path, error = ex.Message }));
        }
    }

    [KernelFunction, Description("Check database connection")]
    public async Task<string> CheckDatabaseConnection(
        [Description("Connection string or container name")] string connection,
        [Description("Database type: postgres, mysql, sqlserver")] string dbType = "postgres")
    {
        try
        {
            ProcessStartInfo psi;

            if (connection.StartsWith("postgres://") || connection.Contains("@"))
            {
                // Direct connection string - validate it
                CommandArgumentValidator.ValidateConnectionString(connection, nameof(connection));

                psi = new ProcessStartInfo
                {
                    FileName = "psql",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add(connection);
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("SELECT version();");
            }
            else
            {
                // Container name - validate it
                CommandArgumentValidator.ValidateContainerName(connection, nameof(connection));

                psi = new ProcessStartInfo
                {
                    FileName = "docker",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("exec");
                psi.ArgumentList.Add(connection);
                psi.ArgumentList.Add("psql");
                psi.ArgumentList.Add("-U");
                psi.ArgumentList.Add("postgres");
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("SELECT version();");
            }

            var output = await ExecuteCommandAsync(psi);
            var isConnected = output.HasValue();

            _context.RecordAction("Validation", "CheckDatabase", $"Database {connection} connected: {isConnected}", true);

            return JsonSerializer.Serialize(new { success = true, connection, isConnected, version = output.Trim() });
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Validation", "CheckDatabase", $"Failed to connect to {connection}", false, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _context.RecordAction("Validation", "CheckDatabase", $"Failed to connect to {connection}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, connection, error = ex.Message });
        }
    }

    [KernelFunction, Description("Validate JSON structure")]
    public Task<string> ValidateJsonStructure(
        [Description("JSON content to validate")] string jsonContent,
        [Description("Expected fields as comma-separated list")] string expectedFields)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            var fields = expectedFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var missingFields = new System.Collections.Generic.List<string>();

            foreach (var field in fields)
            {
                if (!json.TryGetProperty(field, out _))
                    missingFields.Add(field);
            }

            var isValid = missingFields.Count == 0;
            _context.RecordAction("Validation", "ValidateJSON", $"JSON validation: {(isValid ? "passed" : $"missing {missingFields.Count} fields")}", true);

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, isValid, missingFields }));
        }
        catch (Exception ex)
        {
            _context.RecordAction("Validation", "ValidateJSON", "JSON validation failed", false, ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private async Task<string> ExecuteCommandAsync(ProcessStartInfo psi)
    {
        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start process");

        // Read stdout and stderr concurrently to prevent pipe buffer deadlocks
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Command failed: {error}");

        return output;
    }
}
