// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class SandboxUpCommand : AsyncCommand<SandboxUpCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public SandboxUpCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var composeFile = ResolveComposePath(settings.ComposeFile);
        if (composeFile is null)
        {
            _console.MarkupLine("[red]Unable to locate docker compose file. Use --file to specify a path.[/]");
            return 1;
        }

        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("compose");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(composeFile);

        startInfo.ArgumentList.Add("up");
        if (settings.Detach)
        {
            startInfo.ArgumentList.Add("-d");
        }

        if (settings.ProjectName.HasValue())
        {
            startInfo.ArgumentList.Add("--project-name");
            startInfo.ArgumentList.Add(settings.ProjectName!);
        }

        if (settings.ForceRecreate)
        {
            startInfo.ArgumentList.Add("--force-recreate");
        }

        _console.MarkupLineInterpolated($"[grey]Running:[/] docker compose -f [grey]{composeFile}[/] up{(settings.Detach ? " -d" : string.Empty)}");

        // BUG FIX #47: Use cancellation token source to allow cancellation of the command
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // BUG FIX #38: Store handler delegate to unsubscribe in finally block
        ConsoleCancelEventHandler? cancelHandler = null;
        cancelHandler = (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Register console cancellation
        Console.CancelKeyPress += cancelHandler;

        Process? process = null;
        try
        {
            process = Process.Start(startInfo);
            if (process is null)
            {
                _console.MarkupLine("[red]Failed to start docker process.[/]");
                return 1;
            }

            // Register cancellation callback to kill the process
            await using var _ = cancellationToken.Register(() =>
            {
                try
                {
                    if (process != null && !process.HasExited)
                    {
                        _console.MarkupLine("[yellow]Terminating docker compose process...[/]");
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    _console.MarkupLineInterpolated($"[red]Failed to kill process: {ex.Message}[/]");
                }
            });

            // Thread the cancellation token through output forwarding
            var outputTask = ForwardOutputAsync(process, cancellationToken).ConfigureAwait(false);
            var waitTask = process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            await outputTask;
            await waitTask;

            if (process.ExitCode == 0)
            {
                _console.MarkupLine("[green]Sandbox environment is running.[/]");
            }
            else
            {
                _console.MarkupLineInterpolated($"[red]docker compose exited with code {process.ExitCode}.[/]");
            }

            return process.ExitCode;
        }
        catch (Win32Exception ex)
        {
            _console.MarkupLineInterpolated($"[red]Failed to start docker: {ex.Message}[/]");
            return ex.NativeErrorCode == 0 ? 1 : ex.NativeErrorCode;
        }
        catch (OperationCanceledException)
        {
            _console.MarkupLine("[yellow]Sandbox startup cancelled.[/]");
            return 1;
        }
        finally
        {
            // BUG FIX #38: Unsubscribe from console cancel event to prevent duplicate handlers
            if (cancelHandler != null)
            {
                Console.CancelKeyPress -= cancelHandler;
            }
            process?.Dispose();
        }
    }

    private static async Task ForwardOutputAsync(Process process, CancellationToken cancellationToken)
    {
        var stdout = StreamReaderToConsole(process.StandardOutput, Console.Out, cancellationToken);
        var stderr = StreamReaderToConsole(process.StandardError, Console.Error, cancellationToken);
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
    }

    private static async Task StreamReaderToConsole(StreamReader reader, TextWriter target, CancellationToken cancellationToken)
    {
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            await target.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    private static string? ResolveComposePath(string? overridePath)
    {
        if (overridePath.HasValue())
        {
            var absolute = Path.GetFullPath(overridePath);
            return File.Exists(absolute) ? absolute : null;
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "docker", "docker-compose.yml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--file <PATH>")]
        public string? ComposeFile { get; init; }

        [CommandOption("--project-name <NAME>")]
        public string? ProjectName { get; init; }

        [CommandOption("--force-recreate")]
        [Description("Recreate containers even if configuration and image haven't changed.")]
        public bool ForceRecreate { get; init; }

        [CommandOption("--no-detach")]
        [Description("Run docker compose in the foreground.")]
        public bool NoDetach { get; init; }

        public bool Detach => !NoDetach;
    }
}
