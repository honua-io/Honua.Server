// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using UpgradeState = Honua.Cli.AI.Services.Processes.State.UpgradeState;

namespace Honua.Cli.AI.Services.Processes.Steps.Upgrade;

/// <summary>
/// Creates a database backup before upgrade.
/// </summary>
public class BackupDatabaseStep : KernelProcessStep<UpgradeState>
{
    private readonly ILogger<BackupDatabaseStep> _logger;
    private UpgradeState _state = new();

    public BackupDatabaseStep(ILogger<BackupDatabaseStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<UpgradeState> state)
    {
        _state = state.State ?? new UpgradeState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("BackupDatabase")]
    public async Task BackupDatabaseAsync(KernelProcessStepContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating database backup for {DeploymentName}", _state.DeploymentName);

        _state.Status = "BackingUpDatabase";

        try
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var backupFileName = $"db-backup-{_state.DeploymentName}-{timestamp}.sql.gz";
            var localBackupPath = Path.Combine(Path.GetTempPath(), backupFileName);

            // Step 1: Create PostgreSQL dump using pg_dump
            _logger.LogInformation("Running pg_dump to create database backup");
            await ExecutePgDump(localBackupPath, cancellationToken);

            // Step 2: Compress the backup if not already compressed
            _logger.LogInformation("Compressing backup file");
            var compressedPath = await CompressBackup(localBackupPath, cancellationToken);

            // Step 3: Upload to cloud storage
            _logger.LogInformation("Uploading backup to cloud storage");
            var cloudLocation = await UploadBackupToCloud(compressedPath, backupFileName, cancellationToken);

            // Step 4: Verify backup integrity
            _logger.LogInformation("Verifying backup integrity");
            await VerifyBackup(compressedPath, cancellationToken);

            _state.BackupLocation = cloudLocation;
            _state.CanRollback = true;

            // Clean up local backup file
            try
            {
                if (File.Exists(localBackupPath))
                    File.Delete(localBackupPath);
                if (File.Exists(compressedPath) && compressedPath != localBackupPath)
                    File.Delete(compressedPath);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up temporary backup files");
            }

            _logger.LogInformation("Database backup created at {BackupLocation}", _state.BackupLocation);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "BackupCompleted",
                Data = _state
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Database backup cancelled for {DeploymentName}", _state.DeploymentName);
            _state.Status = "Cancelled";
            _state.CanRollback = false;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "BackupCancelled",
                Data = new { _state.DeploymentName }
            });
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backup database for {DeploymentName}", _state.DeploymentName);
            _state.Status = "BackupFailed";
            _state.CanRollback = false;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "BackupFailed",
                Data = new { _state.DeploymentName, Error = ex.Message }
            });
        }
    }

    private async Task ExecutePgDump(string outputPath, CancellationToken cancellationToken)
    {
        // Get database connection details from environment variables
        var dbHost = Environment.GetEnvironmentVariable("HONUA_DB_HOST") ?? "localhost";
        var dbPort = Environment.GetEnvironmentVariable("HONUA_DB_PORT") ?? "5432";
        var dbName = Environment.GetEnvironmentVariable("HONUA_DB_NAME") ?? "honua";
        var dbUser = Environment.GetEnvironmentVariable("HONUA_DB_USER") ?? "postgres";
        var dbPassword = Environment.GetEnvironmentVariable("HONUA_DB_PASSWORD");

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pg_dump",
                Arguments = $"-h {dbHost} -p {dbPort} -U {dbUser} -d {dbName} -F c -f \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // Set password via environment variable to avoid password prompt
        if (!string.IsNullOrEmpty(dbPassword))
        {
            process.StartInfo.Environment["PGPASSWORD"] = dbPassword;
        }

        _logger.LogDebug("Executing pg_dump: {Arguments}", process.StartInfo.Arguments.Replace(dbPassword ?? "", "***"));

        process.Start();

        // Read output and errors
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"pg_dump failed with exit code {process.ExitCode}: {error}");
        }

        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException($"Backup file was not created at {outputPath}");
        }

        _logger.LogInformation("Database backup created: {Size} bytes", new FileInfo(outputPath).Length);
    }

    private async Task<string> CompressBackup(string inputPath, CancellationToken cancellationToken)
    {
        // If pg_dump was run with -F c (custom format), it's already compressed
        // Check if file is already compressed
        if (inputPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            return inputPath;
        }

        var compressedPath = $"{inputPath}.gz";

        try
        {
            // Use gzip to compress the file
            // Fixed: Use stdout redirection to file instead of shell redirection with UseShellExecute=false
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "gzip",
                    Arguments = $"-c \"{inputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Write compressed output to file
            using (var outputFile = File.Create(compressedPath))
            {
                await process.StandardOutput.BaseStream.CopyToAsync(outputFile, cancellationToken);
            }

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && File.Exists(compressedPath))
            {
                _logger.LogInformation("Backup compressed successfully: {Size} bytes", new FileInfo(compressedPath).Length);
                return compressedPath;
            }

            _logger.LogWarning("gzip failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compress backup with gzip");
        }

        // If gzip is not available or failed, return original path
        _logger.LogWarning("Using uncompressed backup");
        return inputPath;
    }

    private async Task<string> UploadBackupToCloud(string localPath, string fileName, CancellationToken cancellationToken)
    {
        // Determine cloud provider and upload accordingly
        var cloudProvider = Environment.GetEnvironmentVariable("HONUA_CLOUD_PROVIDER") ?? "aws";
        var bucketName = Environment.GetEnvironmentVariable("HONUA_BACKUP_BUCKET") ?? $"{_state.DeploymentName}-backups";

        string cloudLocation;

        switch (cloudProvider.ToLowerInvariant())
        {
            case "aws":
                cloudLocation = await UploadToS3(localPath, bucketName, fileName, cancellationToken);
                break;

            case "azure":
                cloudLocation = await UploadToAzureBlob(localPath, bucketName, fileName, cancellationToken);
                break;

            case "gcp":
                cloudLocation = await UploadToGCS(localPath, bucketName, fileName, cancellationToken);
                break;

            default:
                // Fallback: store locally or in a default location
                _logger.LogWarning("Unknown cloud provider {Provider}, storing backup locally", cloudProvider);
                cloudLocation = $"file://{localPath}";
                break;
        }

        return cloudLocation;
    }

    private async Task<string> UploadToS3(string localPath, string bucketName, string fileName, CancellationToken cancellationToken)
    {
        var s3Key = $"backups/{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";

        // Use AWS CLI for S3 upload
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "aws",
                Arguments = $"s3 cp \"{localPath}\" s3://{bucketName}/{s3Key}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to upload backup to S3: {error}");
        }

        _logger.LogInformation("Backup uploaded to S3: s3://{Bucket}/{Key}", bucketName, s3Key);
        return $"s3://{bucketName}/{s3Key}";
    }

    private async Task<string> UploadToAzureBlob(string localPath, string containerName, string fileName, CancellationToken cancellationToken)
    {
        var blobPath = $"backups/{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";

        // Use Azure CLI for blob upload
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "az",
                Arguments = $"storage blob upload --file \"{localPath}\" --container-name {containerName} --name {blobPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to upload backup to Azure Blob: {error}");
        }

        _logger.LogInformation("Backup uploaded to Azure Blob: {Container}/{Path}", containerName, blobPath);
        return $"azure://{containerName}/{blobPath}";
    }

    private async Task<string> UploadToGCS(string localPath, string bucketName, string fileName, CancellationToken cancellationToken)
    {
        var gcsPath = $"backups/{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";

        // Use Google Cloud CLI for GCS upload
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "gsutil",
                Arguments = $"cp \"{localPath}\" gs://{bucketName}/{gcsPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to upload backup to GCS: {error}");
        }

        _logger.LogInformation("Backup uploaded to GCS: gs://{Bucket}/{Path}", bucketName, gcsPath);
        return $"gs://{bucketName}/{gcsPath}";
    }

    private async Task VerifyBackup(string backupPath, CancellationToken cancellationToken)
    {
        // Verify the backup file exists and is not empty
        if (!File.Exists(backupPath))
        {
            throw new InvalidOperationException($"Backup file not found at {backupPath}");
        }

        var fileInfo = new FileInfo(backupPath);
        if (fileInfo.Length == 0)
        {
            throw new InvalidOperationException("Backup file is empty");
        }

        // For PostgreSQL custom format, we can use pg_restore --list to verify
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "pg_restore",
                    Arguments = $"--list \"{backupPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                _logger.LogInformation("Backup verification successful");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "pg_restore verification failed, backup may be in different format");
        }

        // Basic verification passed (file exists and has content)
        _logger.LogInformation("Basic backup verification passed: {Size} bytes", fileInfo.Length);
        await Task.CompletedTask;
    }
}
