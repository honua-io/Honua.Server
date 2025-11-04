// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Abstraction for invoking gcloud CLI commands.
/// </summary>
public interface IGcloudCli
{
    Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments);
}

/// <summary>
/// Default implementation that shells out to the gcloud CLI binary.
/// </summary>
public sealed class DefaultGcloudCli : IGcloudCli
{
    private readonly string _executable;

    public static DefaultGcloudCli Shared { get; } = new();

    public DefaultGcloudCli(string? executableOverride = null)
    {
        _executable = ResolveExecutable(executableOverride);
    }

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start {_executable} process");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var output = await stdoutTask.ConfigureAwait(false);
        var error = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{_executable} command failed: {error}");
        }

        return output;
    }

    private static string ResolveExecutable(string? executableOverride)
    {
        if (!string.IsNullOrWhiteSpace(executableOverride))
        {
            return executableOverride!;
        }

        var candidate = Environment.GetEnvironmentVariable("HONUA_GCLOUD_CLI");
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate!;
        }

        candidate = Environment.GetEnvironmentVariable("GCLOUD_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate!;
        }

        return "gcloud";
    }
}
