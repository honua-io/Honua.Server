// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Configuration;

public interface IHonuaCliConfigStore
{
    Task<HonuaCliConfig> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(HonuaCliConfig config, CancellationToken cancellationToken = default);
}

public sealed record HonuaCliConfig(string? Host, string? Token)
{
    public static HonuaCliConfig Empty { get; } = new(null, null);
}

public sealed class HonuaCliConfigStore : IHonuaCliConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private const string TokenProtectionScheme = "dpapi-current-user";
    private const string AesGcmProtectionScheme = "aes-gcm-local";
    private static readonly byte[] TokenEntropy = Encoding.UTF8.GetBytes("HonuaCli::TokenProtection::v1");

    private readonly IHonuaCliEnvironment _environment;

    public HonuaCliConfigStore(IHonuaCliEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task<HonuaCliConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        _environment.EnsureInitialized();
        var path = ResolveConfigPath();
        if (!File.Exists(path))
        {
            return HonuaCliConfig.Empty;
        }

        var configDirectory = Path.GetDirectoryName(path) ?? _environment.ConfigRoot;

        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        string? host = null;
        if (root.TryGetProperty("host", out var hostProperty) && hostProperty.ValueKind == JsonValueKind.String)
        {
            host = hostProperty.GetString();
        }

        string? token = null;
        if (root.TryGetProperty("protectedToken", out var protectedTokenProperty) &&
            protectedTokenProperty.ValueKind == JsonValueKind.String)
        {
            var protection = root.TryGetProperty("tokenProtection", out var protectionProperty) && protectionProperty.ValueKind == JsonValueKind.String
                ? protectionProperty.GetString()
                : null;

            token = TryDecryptToken(protectedTokenProperty.GetString(), protection, configDirectory);
        }
        else if (root.TryGetProperty("token", out var tokenProperty) && tokenProperty.ValueKind == JsonValueKind.String)
        {
            token = tokenProperty.GetString();
        }

        return new HonuaCliConfig(host, token);
    }

    public async Task SaveAsync(HonuaCliConfig config, CancellationToken cancellationToken = default)
    {
        _environment.EnsureInitialized();
        var path = ResolveConfigPath();
        var configDirectory = Path.GetDirectoryName(path) ?? _environment.ConfigRoot;

        await using var stream = File.Create(path);
        string? protectedToken = null;
        string? protectionScheme = null;
        if (config.Token.HasValue())
        {
            (protectedToken, protectionScheme) = ProtectToken(config.Token, configDirectory);
        }

        var fileModel = new HonuaCliConfigFileModel
        {
            Host = config.Host,
            ProtectedToken = protectedToken,
            TokenProtection = protectionScheme
        };

        await JsonSerializer.SerializeAsync(stream, fileModel, SerializerOptions, cancellationToken).ConfigureAwait(false);
        FilePermissionHelper.ApplyFilePermissions(path);
    }

    private string ResolveConfigPath()
    {
        return Path.Combine(_environment.ConfigRoot, "config.json");
    }

    private static (string CipherText, string Scheme) ProtectToken(string token, string configDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return (ProtectTokenWindows(token), TokenProtectionScheme);
        }

        try
        {
            var key = LoadOrCreateEncryptionKey(configDirectory, createIfMissing: true);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var plainBytes = Encoding.UTF8.GetBytes(token);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[16];

#pragma warning disable SYSLIB0053
            using (var aesGcm = new AesGcm(key))
#pragma warning restore SYSLIB0053
            {
                aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
            }

            var payload = new byte[nonce.Length + cipherBytes.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
            Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length, cipherBytes.Length);
            Buffer.BlockCopy(tag, 0, payload, nonce.Length + cipherBytes.Length, tag.Length);

            return (Convert.ToBase64String(payload), AesGcmProtectionScheme);
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException)
        {
            throw new InvalidOperationException("Failed to protect Honua CLI token using local encryption.", ex);
        }
    }

    private static string? TryDecryptToken(string? cipherText, string? protection, string configDirectory)
    {
        if (cipherText.IsNullOrWhiteSpace())
        {
            return null;
        }

        if (string.Equals(protection, TokenProtectionScheme, StringComparison.OrdinalIgnoreCase))
        {
            return OperatingSystem.IsWindows() ? TryDecryptTokenWindows(cipherText) : null;
        }

        if (string.Equals(protection, AesGcmProtectionScheme, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var payload = Convert.FromBase64String(cipherText);
                if (payload.Length < 28)
                {
                    return null;
                }

                var nonce = payload.AsSpan(0, 12).ToArray();
                var tag = payload.AsSpan(payload.Length - 16).ToArray();
                var cipher = payload.AsSpan(12, payload.Length - 28).ToArray();
                var key = LoadOrCreateEncryptionKey(configDirectory, createIfMissing: false);

                var plainBytes = new byte[cipher.Length];
#pragma warning disable SYSLIB0053
                using (var aesGcm = new AesGcm(key))
#pragma warning restore SYSLIB0053
                {
                    aesGcm.Decrypt(nonce, cipher, tag, plainBytes);
                }

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                return null;
            }
        }

        // Unknown or legacy scheme - treat the stored value as plaintext.
        return cipherText;
    }

    private static byte[] LoadOrCreateEncryptionKey(string directory, bool createIfMissing)
    {
        var keyPath = Path.Combine(directory, "honua-cli.token.key");
        if (File.Exists(keyPath))
        {
            return File.ReadAllBytes(keyPath);
        }

        if (!createIfMissing)
        {
            throw new FileNotFoundException("CLI encryption key was not found.", keyPath);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(keyPath, key);
        FilePermissionHelper.ApplyFilePermissions(keyPath);
        return key;
    }

    [SupportedOSPlatform("windows")]
    private static string ProtectTokenWindows(string token)
    {
        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(token);
            var protectedBytes = ProtectedData.Protect(plainBytes, TokenEntropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException)
        {
            throw new InvalidOperationException("Failed to protect Honua CLI token with Windows data protection.", ex);
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? TryDecryptTokenWindows(string cipherText)
    {
        try
        {
            var protectedBytes = Convert.FromBase64String(cipherText);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, TokenEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private sealed class HonuaCliConfigFileModel
    {
        public string? Host { get; set; }

        public string? ProtectedToken { get; set; }

        public string? TokenProtection { get; set; }
    }
}
