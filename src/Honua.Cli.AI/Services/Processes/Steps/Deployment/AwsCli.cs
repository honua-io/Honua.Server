// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Abstraction for invoking AWS CLI commands so tests can substitute emulator-aware implementations.
/// </summary>
public interface IAwsCli
{
    /// <summary>
    /// Executes an AWS CLI command.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="arguments">Command arguments (e.g. s3api, put-bucket-versioning, ...).</param>
    /// <returns>Captured standard output.</returns>
    Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments);
}

/// <summary>
/// Default AWS CLI runner that shells out to the AWS CLI binary (aws/awslocal).
/// </summary>
public sealed class DefaultAwsCli : IAwsCli
{
    private readonly string _executable;

    /// <summary>
    /// Shared singleton instance using default resolution rules.
    /// </summary>
    public static DefaultAwsCli Shared { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAwsCli"/> class.
    /// </summary>
    /// <param name="executableOverride">
    /// Optional override for the CLI executable path. When null or whitespace,
    /// the value is resolved from HONUA_AWS_CLI, AWS_CLI_PATH, then defaults to "aws".
    /// </param>
    public DefaultAwsCli(string? executableOverride = null)
    {
        _executable = ResolveExecutable(executableOverride);
    }

    /// <inheritdoc />
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

        var candidate = Environment.GetEnvironmentVariable("HONUA_AWS_CLI");
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate!;
        }

        candidate = Environment.GetEnvironmentVariable("AWS_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate!;
        }

        return "aws";
    }
}
