// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Utility methods for infrastructure code generation.
/// Contains name sanitization, password generation, and secure file operations.
/// </summary>
public partial class GenerateInfrastructureCodeStep
{
    /// <summary>
    /// Sanitizes deployment name for use in resource identifiers.
    /// Converts to lowercase, removes special characters, and ensures length limits.
    /// </summary>
    private static string SanitizeName(string name, int maxLength = 32)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = "honua";
        }

        // Convert to lowercase
        var sanitized = name.ToLowerInvariant();

        // Replace invalid characters with hyphens
        var sb = new StringBuilder();
        foreach (var c in sanitized)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (c == '-' || c == '_')
            {
                sb.Append('-');
            }
        }

        sanitized = sb.ToString();

        // Remove consecutive hyphens
        while (sanitized.Contains("--"))
        {
            sanitized = sanitized.Replace("--", "-");
        }

        // Trim hyphens from start and end
        sanitized = sanitized.Trim('-');

        // Ensure minimum length
        if (sanitized.Length == 0)
        {
            sanitized = "honua";
        }

        // Truncate to max length
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized.Substring(0, maxLength).TrimEnd('-');
        }

        return sanitized;
    }

    /// <summary>
    /// Sanitizes deployment name for Azure storage account names.
    /// Azure storage accounts must be 3-24 chars, lowercase alphanumeric only, globally unique.
    /// </summary>
    private static string SanitizeStorageAccountName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = "honua";
        }

        // Convert to lowercase and remove all non-alphanumeric characters
        var sanitized = new string(name.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray());

        // Ensure minimum length
        if (sanitized.Length == 0)
        {
            sanitized = "honua";
        }

        // Truncate to 18 chars to leave room for suffixes (fn/data) and still be under 24 char limit
        if (sanitized.Length > 18)
        {
            sanitized = sanitized.Substring(0, 18);
        }

        // Ensure it's at least 3 chars (minimum for storage account)
        if (sanitized.Length < 3)
        {
            sanitized = sanitized.PadRight(3, 'x');
        }

        return sanitized;
    }

    /// <summary>
    /// Generates a cryptographically secure random password for database authentication.
    /// </summary>
    private static string GenerateSecureDatabasePassword()
    {
        Span<byte> buffer = stackalloc byte[DatabasePasswordLength];
        RandomNumberGenerator.Fill(buffer);

        // Use a safe alphabet that excludes ambiguous characters and includes required character types
        // Includes: uppercase, lowercase, digits, and special characters safe for shell/config files
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_+=";
        var builder = new StringBuilder(DatabasePasswordLength);
        foreach (var b in buffer)
        {
            builder.Append(alphabet[b % alphabet.Length]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Creates a secure temporary directory with restricted permissions.
    /// </summary>
    private string CreateSecureTempDirectory(string deploymentId)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "honua-terraform", deploymentId);
        var dirInfo = Directory.CreateDirectory(tempPath);

        // On Unix systems, restrict directory permissions to owner only (700)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                _logger.LogInformation("Set secure permissions (700) on directory {Path}", tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set Unix file permissions on {Path}", tempPath);
            }
        }

        return tempPath;
    }

    /// <summary>
    /// Sets restrictive permissions on a file containing sensitive data.
    /// </summary>
    private void SetRestrictiveFilePermissions(string filePath)
    {
        // On Unix systems, restrict file permissions to owner read/write only (600)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                _logger.LogDebug("Set secure permissions (600) on file {Path}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set Unix file permissions on {Path}", filePath);
            }
        }
    }

    /// <summary>
    /// Securely cleans up temporary directory by overwriting sensitive files before deletion.
    /// </summary>
    private void CleanupSecureTempDirectory(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
                return;

            _logger.LogInformation("Cleaning up secure temporary directory: {Path}", directoryPath);

            // Overwrite sensitive files (tfvars) with random data before deletion
            var sensitiveFiles = Directory.GetFiles(directoryPath, "*.tfvars", SearchOption.AllDirectories);
            foreach (var file in sensitiveFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Exists && fileInfo.Length > 0)
                    {
                        // Overwrite with random data
                        var randomData = new byte[fileInfo.Length];
                        RandomNumberGenerator.Fill(randomData);
                        File.WriteAllBytes(file, randomData);
                        _logger.LogDebug("Securely overwritten sensitive file: {File}", file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to securely overwrite file {File}", file);
                }
            }

            // Delete the directory and all contents
            Directory.Delete(directoryPath, recursive: true);
            _logger.LogInformation("Cleaned up temporary directory: {Path}", directoryPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up temporary directory {Path}", directoryPath);
        }
    }
}
