using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

/// <summary>
/// Test implementation of <see cref="IAwsCli"/> that prefers the LocalStack AWS CLI tooling
/// (<c>awslocal</c> or <c>aws --endpoint-url</c>) but gracefully falls back to AWS SDK calls when the CLI
/// binaries are unavailable. This keeps emulator tests aligned with production behaviour while remaining
/// portable in CI environments that do not ship the CLI wrappers.
/// </summary>
internal sealed class LocalStackAwsCli : IAwsCli, IDisposable
{
    private readonly Uri _serviceEndpoint;
    private readonly string _region;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string? _cliBinary;
    private readonly bool _useCli;
    private readonly AmazonS3Client _s3Client;
    private bool _disposed;

    public LocalStackAwsCli(Uri serviceEndpoint, string region, string accessKey, string secretKey)
    {
        _serviceEndpoint = serviceEndpoint ?? throw new ArgumentNullException(nameof(serviceEndpoint));
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
        _cliBinary = ResolveCliBinary();
        _useCli = !string.IsNullOrEmpty(_cliBinary);

        var config = new AmazonS3Config
        {
            ServiceURL = _serviceEndpoint.ToString(),
            ForcePathStyle = true,
            AuthenticationRegion = _region,
            UseHttp = _serviceEndpoint.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        };

        _s3Client = new AmazonS3Client(_accessKey, _secretKey, config);
    }

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        if (arguments.Length < 2)
        {
            throw new InvalidOperationException("AWS CLI command requires at least a service and operation.");
        }

        var service = arguments[0];
        var operation = arguments[1];

        if (!string.Equals(service, "s3api", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"LocalStackAwsCli only supports s3api commands, received '{service}'.");
        }

        if (_useCli && _cliBinary is not null)
        {
            return await ExecuteViaCliAsync(arguments, cancellationToken).ConfigureAwait(false);
        }

        await ExecuteViaSdkAsync(operation, arguments.Length > 2 ? arguments[2..] : Array.Empty<string>(), cancellationToken)
            .ConfigureAwait(false);
        return string.Empty;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _s3Client.Dispose();
        _disposed = true;
    }

    private async Task<string> ExecuteViaCliAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var psi = CreateProcessStartInfo(arguments);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {_cliBinary} process.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdOutTask, stdErrTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdout = await stdOutTask.ConfigureAwait(false);
        var stderr = await stdErrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{_cliBinary} command failed: {stderr}");
        }

        return stdout;
    }

    private async Task ExecuteViaSdkAsync(string operation, string[] options, CancellationToken cancellationToken)
    {
        switch (operation.ToLowerInvariant())
        {
            case "put-bucket-versioning":
                await HandlePutBucketVersioningAsync(options, cancellationToken).ConfigureAwait(false);
                break;
            case "put-bucket-cors":
                await HandlePutBucketCorsAsync(options, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"LocalStackAwsCli SDK fallback does not implement '{operation}'.");
        }
    }

    private ProcessStartInfo CreateProcessStartInfo(string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _cliBinary!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("--endpoint-url");
        psi.ArgumentList.Add(_serviceEndpoint.ToString());
        psi.ArgumentList.Add("--region");
        psi.ArgumentList.Add(_region);

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        psi.Environment["AWS_ACCESS_KEY_ID"] = _accessKey;
        psi.Environment["AWS_SECRET_ACCESS_KEY"] = _secretKey;
        psi.Environment["AWS_DEFAULT_REGION"] = _region;
        psi.Environment["AWS_REGION"] = _region;

        return psi;
    }

    private async Task HandlePutBucketVersioningAsync(string[] options, CancellationToken cancellationToken)
    {
        var bucketName = GetRequiredOptionValue(options, "--bucket");
        var versioningConfig = GetRequiredOptionValue(options, "--versioning-configuration");

        var statusValue = ExtractOptionComponent(versioningConfig, "Status");
        var status = Amazon.S3.VersionStatus.FindValue(statusValue);
        if (status is null)
        {
            throw new InvalidOperationException($"Unsupported bucket versioning status '{statusValue}'.");
        }

        var request = new PutBucketVersioningRequest
        {
            BucketName = bucketName,
            VersioningConfig = new S3BucketVersioningConfig
            {
                Status = status
            }
        };

        await _s3Client.PutBucketVersioningAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandlePutBucketCorsAsync(string[] options, CancellationToken cancellationToken)
    {
        var bucketName = GetRequiredOptionValue(options, "--bucket");
        var corsOption = GetRequiredOptionValue(options, "--cors-configuration");

        var configurationPath = ExtractFilePath(corsOption);
        if (!File.Exists(configurationPath))
        {
            throw new FileNotFoundException($"CORS configuration file not found: {configurationPath}");
        }

        var fileContents = await File.ReadAllTextAsync(configurationPath, cancellationToken).ConfigureAwait(false);
        var corsConfigDocument = JsonSerializer.Deserialize<CorsConfigurationDocument>(
            fileContents,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse CORS configuration JSON.");

        var corsConfiguration = new CORSConfiguration
        {
            Rules = new List<CORSRule>()
        };
        if (corsConfigDocument.CORSRules is not null)
        {
            foreach (var rule in corsConfigDocument.CORSRules)
            {
                corsConfiguration.Rules.Add(new CORSRule
                {
                    AllowedOrigins = rule.AllowedOrigins?.ToList() ?? new List<string>(),
                    AllowedMethods = rule.AllowedMethods?.ToList() ?? new List<string>(),
                    AllowedHeaders = rule.AllowedHeaders?.ToList() ?? new List<string>(),
                    MaxAgeSeconds = rule.MaxAgeSeconds
                });
            }
        }

        var request = new PutCORSConfigurationRequest
        {
            BucketName = bucketName,
            Configuration = corsConfiguration
        };

        await _s3Client.PutCORSConfigurationAsync(request, cancellationToken).ConfigureAwait(false);

        // LocalStack occasionally returns eventual consistency errors; retry fetch to ensure configuration is visible.
        await WaitForCorsPropagationAsync(bucketName, cancellationToken).ConfigureAwait(false);
    }

    private static string? ResolveCliBinary()
    {
        var candidates = new[] { "awslocal", "aws" };

        foreach (var candidate in candidates)
        {
            if (CommandExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool CommandExists(string command)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return false;
        }

        var pathSeparator = Path.PathSeparator;
        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        foreach (var path in pathEnv.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidatePath = Path.Combine(path, command + extension);
                if (File.Exists(candidatePath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetRequiredOptionValue(string[] options, string optionName)
    {
        for (var i = 0; i < options.Length; i++)
        {
            if (string.Equals(options[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= options.Length)
                {
                    throw new InvalidOperationException($"Missing value for option '{optionName}'.");
                }

                return options[i + 1];
            }
        }

        throw new InvalidOperationException($"Option '{optionName}' was not provided.");
    }

    private static string ExtractOptionComponent(string optionValue, string key)
    {
        var components = optionValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var component in components)
        {
            var kvp = component.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kvp.Length == 2 && string.Equals(kvp[0], key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp[1];
            }
        }

        throw new InvalidOperationException($"Option value '{optionValue}' does not contain '{key}='.");
    }

    private static string ExtractFilePath(string optionValue)
    {
        const string FilePrefix = "file://";
        if (optionValue.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return optionValue[FilePrefix.Length..];
        }

        return optionValue;
    }

    private async Task WaitForCorsPropagationAsync(string bucketName, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var response = await _s3Client.GetCORSConfigurationAsync(new GetCORSConfigurationRequest
                {
                    BucketName = bucketName
                }, cancellationToken).ConfigureAwait(false);

                if (response.Configuration is not null && response.Configuration.Rules.Count > 0)
                {
                    return;
                }
            }
            catch (AmazonS3Exception ex) when (string.Equals(ex.ErrorCode, "NoSuchCORSConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                // Retry on eventual consistency errors
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class CorsConfigurationDocument
    {
        [JsonPropertyName("CORSRules")]
        public List<CorsRuleDocument>? CORSRules { get; set; }
    }

    private sealed class CorsRuleDocument
    {
        [JsonPropertyName("AllowedOrigins")]
        public IEnumerable<string>? AllowedOrigins { get; set; }

        [JsonPropertyName("AllowedMethods")]
        public IEnumerable<string>? AllowedMethods { get; set; }

        [JsonPropertyName("AllowedHeaders")]
        public IEnumerable<string>? AllowedHeaders { get; set; }

        [JsonPropertyName("MaxAgeSeconds")]
        public int MaxAgeSeconds { get; set; }
    }
}
