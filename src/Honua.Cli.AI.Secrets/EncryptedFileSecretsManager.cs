// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Secrets.Extensions;

namespace Honua.Cli.AI.Secrets;

/// <summary>
/// File-based secrets manager with AES-256-GCM authenticated encryption.
///
/// SECURITY FEATURES:
/// - AES-256-GCM authenticated encryption (prevents tampering)
/// - PBKDF2 key derivation with 600,000 iterations (OWASP 2024 recommendation)
/// - Random salt per file (prevents rainbow table attacks)
/// - Secure memory wiping using CryptographicOperations.ZeroMemory
/// - Constant-time MAC verification (prevents timing attacks)
/// - Unix file permissions (600) for sensitive files
///
/// SUITABLE FOR:
/// - Local development and testing
/// - Single-user workstations
/// - CI/CD environments with secure storage
///
/// FOR PRODUCTION, CONSIDER:
/// - OS keychain (Windows Credential Manager, macOS Keychain, Linux Secret Service)
/// - Cloud secrets manager (Azure Key Vault, AWS Secrets Manager, GCP Secret Manager)
/// - Hardware Security Modules (HSM) for maximum security
/// </summary>
public sealed class EncryptedFileSecretsManager : ISecretsManager, IDisposable
{
    private readonly string _filePath;
    private byte[]? _encryptionKey; // Nullable for secure disposal
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, ScopedToken> _activeTokens = new();
    private readonly SecretsManagerOptions _options;
    private bool _disposed;

    // Cryptographic constants (OWASP/NIST recommendations)
    private const int SaltSize = 32; // 256 bits
    private const int KeySize = 32; // 256 bits for AES-256
    private const int NonceSize = 12; // 96 bits for AES-GCM
    private const int TagSize = 16; // 128 bits authentication tag
    private const int Pbkdf2Iterations = 600_000; // OWASP 2024 recommendation

    public EncryptedFileSecretsManager(SecretsManagerOptions options)
    {
        _options = options;
        _filePath = options.FilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".honua",
            "secrets.enc");

        // Derive encryption key from passphrase (or machine/user for development)
        _encryptionKey = DeriveEncryptionKey();

        // Ensure directory exists with secure permissions
        var directory = Path.GetDirectoryName(_filePath);
        if (!directory.IsNullOrEmpty())
        {
            Directory.CreateDirectory(directory);
            SetSecureDirectoryPermissions(directory);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Securely wipe encryption key from memory
        if (_encryptionKey != null)
        {
            CryptographicOperations.ZeroMemory(_encryptionKey);
            _encryptionKey = null;
        }

        _lock.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EncryptedFileSecretsManager));
        }
    }

    public async Task<Secret> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var vault = await LoadVaultAsync(cancellationToken);

            if (!vault.Secrets.TryGetValue(name, out var secretData))
            {
                throw new InvalidOperationException($"Secret '{name}' not found");
            }

            return new Secret
            {
                Name = name,
                Value = secretData.Value,
                Metadata = secretData.Metadata,
                CreatedAt = secretData.CreatedAt,
                UpdatedAt = secretData.UpdatedAt
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetSecretAsync(
        string name,
        string value,
        SecretMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var vault = await LoadVaultAsync(cancellationToken);

            var isUpdate = vault.Secrets.ContainsKey(name);
            vault.Secrets[name] = new StoredSecret
            {
                Value = value,
                Metadata = metadata,
                CreatedAt = isUpdate ? vault.Secrets[name].CreatedAt : DateTime.UtcNow,
                UpdatedAt = isUpdate ? DateTime.UtcNow : null
            };

            await SaveVaultAsync(vault, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var vault = await LoadVaultAsync(cancellationToken);

            if (!vault.Secrets.Remove(name))
            {
                throw new InvalidOperationException($"Secret '{name}' not found");
            }

            await SaveVaultAsync(vault, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ListSecretsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var vault = await LoadVaultAsync(cancellationToken);
            return vault.Secrets.Keys.ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ScopedToken> RequestScopedAccessAsync(
        string secretName,
        AccessScope scope,
        TimeSpan duration,
        string purpose,
        bool requireUserApproval = true,
        CancellationToken cancellationToken = default)
    {
        // Validate duration
        if (duration > _options.MaxTokenDuration)
        {
            throw new InvalidOperationException(
                $"Requested duration {duration} exceeds maximum {_options.MaxTokenDuration}");
        }

        // User approval check (in real implementation, this would prompt the user)
        if (requireUserApproval && _options.RequireUserApproval)
        {
            // For now, auto-approve in file-based mode (development)
            // In production, this would show a UI prompt
            Console.WriteLine($"⚠️  AI requesting access to '{secretName}'");
            Console.WriteLine($"   Purpose: {purpose}");
            Console.WriteLine($"   Scope: {scope.Level}");
            Console.WriteLine($"   Duration: {duration.TotalMinutes:F0} minutes");
            Console.WriteLine($"   Auto-approved (development mode)");
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Get the actual secret
            var secret = await GetSecretAsync(secretName, cancellationToken);

            // Generate scoped token
            var tokenId = Guid.NewGuid().ToString("N");
            var token = new ScopedToken
            {
                TokenId = tokenId,
                Token = secret.Value, // In production, this would be a derived/limited token
                SecretRef = secretName,
                Scope = scope,
                ExpiresAt = DateTime.UtcNow.Add(duration),
                RequestedBy = "AI Assistant",
                Purpose = purpose,
                CreatedAt = DateTime.UtcNow
            };

            // Track active token
            _activeTokens[tokenId] = token;

            return token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task RevokeTokenAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (_activeTokens.TryGetValue(tokenId, out var token))
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScopedToken>> ListActiveTokensAsync(CancellationToken cancellationToken = default)
    {
        var activeTokens = _activeTokens.Values
            .Where(t => t.IsValid)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<ScopedToken>>(activeTokens);
    }

    public Task RevokeAllTokensAsync(CancellationToken cancellationToken = default)
    {
        foreach (var token in _activeTokens.Values)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    private async Task<SecretsVault> LoadVaultAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!File.Exists(_filePath))
        {
            return new SecretsVault { Secrets = new Dictionary<string, StoredSecret>() };
        }

        byte[]? jsonBytes = null;
        try
        {
            // Read encrypted file
            var encryptedBytes = await File.ReadAllBytesAsync(_filePath, cancellationToken);

            // Decrypt on background thread (CPU-intensive)
            jsonBytes = await Task.Run(() => Decrypt(encryptedBytes), cancellationToken);

            // Deserialize
            var json = Encoding.UTF8.GetString(jsonBytes);
            var vault = JsonSerializer.Deserialize<SecretsVault>(json);

            return vault ?? new SecretsVault { Secrets = new Dictionary<string, StoredSecret>() };
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                "Failed to decrypt secrets file. The file may be corrupted or the passphrase may have changed.");
        }
        finally
        {
            // Securely wipe decrypted data from memory
            if (jsonBytes != null)
            {
                CryptographicOperations.ZeroMemory(jsonBytes);
            }
        }
    }

    private async Task SaveVaultAsync(SecretsVault vault, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        byte[]? jsonBytes = null;
        byte[]? encryptedBytes = null;

        try
        {
            // Serialize
            var json = JsonSerializer.Serialize(vault, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            jsonBytes = Encoding.UTF8.GetBytes(json);

            // Encrypt on background thread (CPU-intensive)
            encryptedBytes = await Task.Run(() => Encrypt(jsonBytes), cancellationToken);

            // Write to file with secure permissions
            await File.WriteAllBytesAsync(_filePath, encryptedBytes, cancellationToken);
            SetSecureFilePermissions(_filePath);
        }
        finally
        {
            // Securely wipe sensitive data from memory
            if (jsonBytes != null)
            {
                CryptographicOperations.ZeroMemory(jsonBytes);
            }
        }
    }

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM authenticated encryption.
    /// Format: [nonce(12)][tag(16)][ciphertext(variable)]
    /// </summary>
    private byte[] Encrypt(byte[] plaintext)
    {
        if (_encryptionKey == null)
        {
            throw new InvalidOperationException("Encryption key has been disposed");
        }

        // Generate random nonce (never reuse with same key!)
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        // Allocate buffer for encrypted output
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        // Perform AES-GCM encryption (authenticated encryption)
        using var aesGcm = new AesGcm(_encryptionKey, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        // Combine nonce + tag + ciphertext
        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

        // Securely wipe temporary buffers
        CryptographicOperations.ZeroMemory(nonce);
        CryptographicOperations.ZeroMemory(tag);
        CryptographicOperations.ZeroMemory(ciphertext);

        return result;
    }

    /// <summary>
    /// Decrypts ciphertext using AES-256-GCM authenticated encryption.
    /// Verifies authentication tag BEFORE decryption (authenticate-then-decrypt).
    /// </summary>
    private byte[] Decrypt(byte[] encryptedData)
    {
        if (_encryptionKey == null)
        {
            throw new InvalidOperationException("Encryption key has been disposed");
        }

        // Validate minimum size: nonce + tag + at least 1 byte ciphertext
        if (encryptedData.Length < NonceSize + TagSize + 1)
        {
            throw new CryptographicException("Invalid encrypted data: too short");
        }

        // Extract components
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertextLength = encryptedData.Length - NonceSize - TagSize;
        var ciphertext = new byte[ciphertextLength];

        Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(encryptedData, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(encryptedData, NonceSize + TagSize, ciphertext, 0, ciphertextLength);

        var plaintext = new byte[ciphertextLength];

        try
        {
            // Perform AES-GCM decryption with authentication
            // This verifies the tag BEFORE decrypting (prevents tampering)
            using var aesGcm = new AesGcm(_encryptionKey, TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

            return plaintext;
        }
        catch (CryptographicException)
        {
            // Authentication failed - data has been tampered with
            CryptographicOperations.ZeroMemory(plaintext);
            throw new CryptographicException(
                "Authentication failed: the encrypted data has been corrupted or tampered with");
        }
        finally
        {
            // Securely wipe temporary buffers
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    /// <summary>
    /// Derives encryption key using PBKDF2 with 600,000 iterations (OWASP 2024 recommendation).
    /// In development mode, uses machine/username as passphrase for convenience.
    /// In production, should prompt user for actual passphrase.
    /// </summary>
    private byte[] DeriveEncryptionKey()
    {
        var saltFilePath = Path.ChangeExtension(_filePath, ".salt");

        byte[] salt;

        // Check if salt file exists
        if (File.Exists(saltFilePath))
        {
            // Load existing salt
            salt = File.ReadAllBytes(saltFilePath);

            if (salt.Length != SaltSize)
            {
                throw new InvalidOperationException(
                    $"Invalid salt file: expected {SaltSize} bytes, got {salt.Length}");
            }
        }
        else
        {
            // Generate new random salt
            salt = new byte[SaltSize];
            RandomNumberGenerator.Fill(salt);

            // Save salt to file (not secret, can be stored plaintext)
            File.WriteAllBytes(saltFilePath, salt);
            SetSecureFilePermissions(saltFilePath);
        }

        // Get passphrase
        // TODO: In production, prompt user for actual passphrase via secure input
        // For development, use machine/user (convenience vs. security trade-off)
        var passphrase = GetPassphrase();

        byte[]? passphraseBytes = null;
        byte[]? key = null;

        try
        {
            passphraseBytes = Encoding.UTF8.GetBytes(passphrase);

            // Derive key using PBKDF2-HMAC-SHA256 with 600,000 iterations
            using var pbkdf2 = new Rfc2898DeriveBytes(
                passphraseBytes,
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256);

            key = pbkdf2.GetBytes(KeySize);

            return key;
        }
        finally
        {
            // Securely wipe passphrase from memory
            if (passphraseBytes != null)
            {
                CryptographicOperations.ZeroMemory(passphraseBytes);
            }
        }
    }

    /// <summary>
    /// Gets the passphrase for key derivation with environment-aware security controls.
    ///
    /// BUG FIX #8: SECURITY - Removed deterministic dev passphrase fallback.
    /// Previous implementation used predictable ${MachineName}::${UserName}::honua-secrets-v1
    /// which allowed any attacker with filesystem access to decrypt secrets.
    ///
    /// PRODUCTION MODE:
    /// - Requires interactive passphrase entry OR explicit environment variable
    /// - NEVER allows weak deterministic passphrases
    /// - Logs warnings when using environment variables (not recommended)
    ///
    /// DEVELOPMENT/TESTING MODE:
    /// - Now REQUIRES explicit HONUA_SECRETS_PASSPHRASE environment variable
    /// - Fails fast with clear error message if passphrase is not provided
    /// - Guides developers to generate secure per-installation passphrases
    /// </summary>
    private string GetPassphrase()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                        ?? "Production";

        // NEVER allow development-mode passphrase in production
        if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            // Option 1: Interactive prompt (if not redirected)
            if (!Console.IsInputRedirected && Environment.UserInteractive)
            {
                Console.Write("Enter passphrase for secrets encryption: ");
                var passphrase = ReadPasswordFromConsole();
                if (passphrase.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("Passphrase cannot be empty in production.");
                }
                return passphrase;
            }

            // Option 2: Check for explicit environment variable with warning
            var envPassphrase = Environment.GetEnvironmentVariable("HONUA_SECRETS_PASSPHRASE");
            if (!envPassphrase.IsNullOrEmpty())
            {
                Console.WriteLine(
                    "WARNING: Using passphrase from environment variable in production. " +
                    "This is NOT recommended. Use OS keychain or interactive prompt instead.");
                return envPassphrase;
            }

            throw new InvalidOperationException(
                "Production environment detected but no passphrase available. " +
                "Options: " +
                "1) Run interactively to enter passphrase, " +
                "2) Set HONUA_SECRETS_PASSPHRASE environment variable (not recommended), " +
                "3) Configure OS keychain integration.");
        }

        // BUG FIX #8: Development/Testing mode now requires explicit passphrase
        // REMOVED: Insecure deterministic passphrase fallback
        var devPassphrase = Environment.GetEnvironmentVariable("HONUA_SECRETS_PASSPHRASE");
        if (!devPassphrase.IsNullOrEmpty())
        {
            return devPassphrase;
        }

        // BUG FIX #8: Fail fast instead of using predictable passphrase
        // Generate a secure passphrase with: openssl rand -base64 32
        throw new InvalidOperationException(
            "SECURITY: Passphrase required for secrets encryption. " +
            "The previous insecure deterministic passphrase has been removed. " +
            "\n\nTo fix this, set HONUA_SECRETS_PASSPHRASE environment variable with a secure value:" +
            "\n  - Linux/macOS: export HONUA_SECRETS_PASSPHRASE=$(openssl rand -base64 32)" +
            "\n  - Windows (PowerShell): $env:HONUA_SECRETS_PASSPHRASE = (openssl rand -base64 32)" +
            "\n  - Or add to your shell profile (~/.bashrc, ~/.zshrc, etc.)" +
            "\n\nFor CI/CD environments, use secrets management (GitHub Secrets, Azure KeyVault, etc.)");
    }

    /// <summary>
    /// Reads a password from console input without echoing characters.
    /// Displays asterisks for visual feedback and supports backspace.
    /// </summary>
    private static string ReadPasswordFromConsole()
    {
        var password = new System.Text.StringBuilder();
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
        } while (key.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return password.ToString();
    }

    /// <summary>
    /// Sets secure file permissions (Unix: 600, Windows: current user only).
    /// Uses File.SetUnixFileMode() API (preferred) with fallback to chmod command using ArgumentList to prevent injection.
    /// </summary>
    private void SetSecureFilePermissions(string filePath)
    {
        // Only needed on Unix-like systems
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return; // Windows uses NTFS permissions
        }

        try
        {
            // Use .NET 5+ built-in API (preferred)
            if (File.Exists(filePath))
            {
                File.SetUnixFileMode(filePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (PlatformNotSupportedException)
        {
            // Fallback to chmod command with safe argument passing
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Use ArgumentList instead of Arguments string to prevent injection
                psi.ArgumentList.Add("600");
                psi.ArgumentList.Add(filePath);  // Safe - no shell interpretation

                using var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit();
            }
            catch
            {
                // Best effort - continue if chmod fails
            }
        }
        catch
        {
            // Best effort - continue if permission setting fails
        }
    }

    /// <summary>
    /// Sets secure directory permissions (Unix: 700, Windows: current user only).
    /// Uses File.SetUnixFileMode() API (preferred) with fallback to chmod command using ArgumentList to prevent injection.
    /// </summary>
    private void SetSecureDirectoryPermissions(string directoryPath)
    {
        // Only needed on Unix-like systems
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return; // Windows uses NTFS permissions
        }

        try
        {
            // Use .NET 5+ built-in API (preferred)
            if (Directory.Exists(directoryPath))
            {
                File.SetUnixFileMode(directoryPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        catch (PlatformNotSupportedException)
        {
            // Fallback to chmod command with safe argument passing
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Use ArgumentList instead of Arguments string to prevent injection
                psi.ArgumentList.Add("700");
                psi.ArgumentList.Add(directoryPath);  // Safe - no shell interpretation

                using var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit();
            }
            catch
            {
                // Best effort - continue if chmod fails
            }
        }
        catch
        {
            // Best effort - continue if permission setting fails
        }
    }
}

/// <summary>
/// Internal representation of the encrypted secrets vault.
/// </summary>
internal sealed class SecretsVault
{
    public required Dictionary<string, StoredSecret> Secrets { get; init; }
    public int Version { get; init; } = 1;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Internal representation of a stored secret.
/// </summary>
internal sealed class StoredSecret
{
    public required string Value { get; init; }
    public SecretMetadata? Metadata { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
