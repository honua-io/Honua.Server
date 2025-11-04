// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Authentication;

public interface ILocalSigningKeyProvider
{
    byte[] GetSigningKey();

    Task<byte[]> GetSigningKeyAsync(CancellationToken cancellationToken = default);
}

public sealed class LocalSigningKeyProvider : ILocalSigningKeyProvider
{
    private const int SigningKeyLengthBytes = 32;

    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _options;
    private readonly ILogger<LocalSigningKeyProvider> _logger;
    private readonly object _sync = new();

    private byte[]? _cachedKey;

    public LocalSigningKeyProvider(IOptionsMonitor<HonuaAuthenticationOptions> options, ILogger<LocalSigningKeyProvider> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public byte[] GetSigningKey()
    {
        var signingKey = EnsureSigningKey();
        return CloneKey(signingKey);
    }

    public Task<byte[]> GetSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<byte[]>(cancellationToken);
        }

        try
        {
            var signingKey = EnsureSigningKey();
            return Task.FromResult(CloneKey(signingKey));
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromException<byte[]>(new OperationCanceledException("Signing key retrieval cancelled.", ex, cancellationToken));
        }
    }

    private byte[] EnsureSigningKey()
    {
        if (_cachedKey is { Length: > 0 })
        {
            return _cachedKey;
        }

        lock (_sync)
        {
            if (_cachedKey is { Length: > 0 })
            {
                return _cachedKey;
            }

            var path = ResolveKeyPath();
            _cachedKey = LoadOrCreateKey(path);
            return _cachedKey;
        }
    }

    // BUG FIX #21: Use proper async file I/O instead of Task.Run wrapper
    private byte[] LoadOrCreateKey(string path)
    {
        // Note: This method is called from a constructor/startup path with lock protection
        // Using GetAwaiter().GetResult() is acceptable here since it's not on a request hot path
        return LoadOrCreateKeyAsync(path).GetAwaiter().GetResult();
    }

    private async Task<byte[]> LoadOrCreateKeyAsync(string path)
    {
        if (FileOperationHelper.FileExists(path))
        {
            try
            {
                EnsureFilePermissions(path);
                // BUG FIX #21: Properly await async file read instead of Task.Run wrapper
                var existing = await FileOperationHelper.SafeReadAllBytesAsync(path).ConfigureAwait(false);
                if (existing.Length == SigningKeyLengthBytes)
                {
                    return existing;
                }

                _logger.LogWarning(
                    "Existing signing key at {Path} has unexpected length {Length}. Regenerating a new key.",
                    path,
                    existing.Length);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to read existing signing key at {Path}. Regenerating a new key.", path);
            }
        }

        var directory = Path.GetDirectoryName(path);
        if (directory.HasValue())
        {
            EnsureDirectoryPermissions(directory);
        }

        var generated = RandomNumberGenerator.GetBytes(SigningKeyLengthBytes);

        // BUG FIX #21: Use async file write
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous
        };

        await using (var stream = new FileStream(path, options))
        {
            await stream.WriteAsync(generated, 0, generated.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        EnsureFilePermissions(path);
        _logger.LogInformation("Generated new local signing key at {Path}", path);
        return generated;
    }

    private static byte[] CloneKey(byte[] source)
    {
        if (source is null || source.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var clone = new byte[source.Length];
        Buffer.BlockCopy(source, 0, clone, 0, source.Length);
        return clone;
    }

    private string ResolveKeyPath()
    {
        var configured = _options.CurrentValue.Local.SigningKeyPath;
        if (configured.IsNullOrWhiteSpace())
        {
            configured = Path.Combine("data", "auth", "signing.key");
        }

        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
    }

    private static void EnsureDirectoryPermissions(string directory)
    {
        FileOperationHelper.EnsureDirectoryExists(directory);

#if NET8_0_OR_GREATER
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
#endif
    }

    private static void EnsureFilePermissions(string path)
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
