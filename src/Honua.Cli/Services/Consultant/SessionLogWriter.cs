// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;

namespace Honua.Cli.Services.Consultant;

public interface ISessionLogWriter
{
    Task<string> AppendAsync(string content, CancellationToken cancellationToken);
}

public sealed class SessionLogWriter : ISessionLogWriter
{
    private readonly IHonuaCliEnvironment _environment;
    private readonly ISystemClock _clock;

    public SessionLogWriter(IHonuaCliEnvironment environment, ISystemClock clock)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<string> AppendAsync(string content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        _environment.EnsureInitialized();

        var fileName = $"consultant-{_clock.UtcNow:yyyyMMdd}.md";
        var logPath = Path.Combine(_environment.LogsRoot, fileName);
        var entry = new StringBuilder()
            .AppendLine($"## Session {_clock.UtcNow:O}")
            .AppendLine(content)
            .AppendLine()
            .ToString();

        var sanitizedEntry = SessionLogSanitizer.Sanitize(entry);

        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.Append,
            Access = FileAccess.Write,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous
        };

        await using (var stream = new FileStream(logPath, streamOptions))
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(sanitizedEntry.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }

        RestrictFilePermissions(logPath);

        return logPath;
    }

    private static void RestrictFilePermissions(string path)
    {
        try
        {
#if NET8_0_OR_GREATER
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
#endif
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

}
