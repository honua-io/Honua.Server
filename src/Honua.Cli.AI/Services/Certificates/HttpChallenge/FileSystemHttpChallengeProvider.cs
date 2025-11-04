// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Certificates.HttpChallenge;

/// <summary>
/// File system HTTP challenge provider for ACME HTTP-01 validation.
/// Writes challenge files to a web-accessible directory (e.g., /.well-known/acme-challenge/).
/// </summary>
public sealed class FileSystemHttpChallengeProvider : IChallengeProvider
{
    private readonly string _webRootPath;
    private readonly ILogger<FileSystemHttpChallengeProvider> _logger;

    public FileSystemHttpChallengeProvider(
        string webRootPath,
        ILogger<FileSystemHttpChallengeProvider> logger)
    {
        _webRootPath = webRootPath ?? throw new ArgumentNullException(nameof(webRootPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DeployChallengeAsync(
        string domain,
        string token,
        string keyAuthz,
        string challengeType,
        CancellationToken cancellationToken)
    {
        if (challengeType != "Http01")
        {
            throw new ArgumentException("This provider only supports HTTP-01 challenges", nameof(challengeType));
        }

        _logger.LogInformation("Deploying HTTP-01 challenge for domain {Domain}", domain);

        var challengeDir = Path.Combine(_webRootPath, ".well-known", "acme-challenge");

        if (!Directory.Exists(challengeDir))
        {
            Directory.CreateDirectory(challengeDir);
            _logger.LogInformation("Created challenge directory: {Directory}", challengeDir);
        }

        var challengePath = Path.Combine(challengeDir, token);
        await File.WriteAllTextAsync(challengePath, keyAuthz, cancellationToken);

        // Set permissions for web server access on Unix systems
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(challengePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        _logger.LogInformation("Challenge file written to {Path}", challengePath);
        _logger.LogInformation("Challenge should be accessible at http://{Domain}/.well-known/acme-challenge/{Token}", domain, token);
    }

    public async Task CleanupChallengeAsync(
        string domain,
        string token,
        string keyAuthz,
        string challengeType,
        CancellationToken cancellationToken)
    {
        if (challengeType != "Http01")
        {
            return;
        }

        _logger.LogInformation("Cleaning up HTTP-01 challenge for domain {Domain}", domain);

        var challengePath = Path.Combine(_webRootPath, ".well-known", "acme-challenge", token);

        try
        {
            if (File.Exists(challengePath))
            {
                File.Delete(challengePath);
                _logger.LogInformation("Challenge file deleted: {Path}", challengePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup challenge file for {Domain}", domain);
        }

        await Task.CompletedTask;
    }
}
