using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Honua.Server.Core.Security.Secrets;

/// <summary>
/// AWS Secrets Manager implementation of the secrets provider.
/// Supports IAM roles, access keys, profiles, and the default credential provider chain.
/// </summary>
public sealed class AwsSecretsManagerProvider : ISecretsProvider
{
    private readonly IAmazonSecretsManager _client;
    private readonly IMemoryCache? _cache;
    private readonly ILogger<AwsSecretsManagerProvider> _logger;
    private readonly SecretsConfiguration _configuration;
    private readonly string _cacheKeyPrefix;

    public string ProviderName => SecretsProviders.AwsSecretsManager;

    public AwsSecretsManagerProvider(
        IOptions<SecretsConfiguration> configuration,
        ILogger<AwsSecretsManagerProvider> logger,
        IMemoryCache? cache = null)
    {
        _configuration = configuration.Value;
        _logger = logger;
        _cache = cache;
        _cacheKeyPrefix = $"{ProviderName}:";

        var awsConfig = _configuration.AwsSecretsManager;

        if (string.IsNullOrWhiteSpace(awsConfig.Region))
        {
            throw new SecretProviderException(
                "AWS region is not configured. Set Secrets:AwsSecretsManager:Region in configuration.",
                ProviderName);
        }

        var clientConfig = new AmazonSecretsManagerConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(awsConfig.Region),
            Timeout = TimeSpan.FromSeconds(awsConfig.TimeoutSeconds),
            MaxErrorRetry = awsConfig.MaxRetries
        };

        var credentials = CreateCredentials(awsConfig);
        _client = credentials != null
            ? new AmazonSecretsManagerClient(credentials, clientConfig)
            : new AmazonSecretsManagerClient(clientConfig);

        _logger.LogInformation(
            "AWS Secrets Manager provider initialized for region: {Region}",
            awsConfig.Region);
    }

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        var cacheKey = $"{_cacheKeyPrefix}{secretName}";

        if (_configuration.EnableCaching && _cache?.TryGetValue(cacheKey, out string? cachedValue) == true)
        {
            _logger.LogDebug("Retrieved secret '{SecretName}' from cache", secretName);
            return cachedValue;
        }

        try
        {
            _logger.LogDebug("Retrieving secret '{SecretName}' from AWS Secrets Manager", secretName);

            var request = new GetSecretValueRequest
            {
                SecretId = secretName
            };

            var response = await _client.GetSecretValueAsync(request, cancellationToken);
            var value = response.SecretString;

            if (value != null && _configuration.EnableCaching && _cache != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_configuration.CacheDurationSeconds)
                };
                _cache.Set(cacheKey, value, cacheOptions);
            }

            _logger.LogInformation("Successfully retrieved secret '{SecretName}'", secretName);
            return value;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Secret '{SecretName}' not found in AWS Secrets Manager", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' from AWS Secrets Manager", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve secret '{secretName}' from AWS Secrets Manager",
                    ex,
                    ProviderName);
            }

            return null;
        }
    }

    public async Task<string?> GetSecretVersionAsync(string secretName, string version, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        try
        {
            _logger.LogDebug("Retrieving secret '{SecretName}' version '{Version}' from AWS Secrets Manager", secretName, version);

            var request = new GetSecretValueRequest
            {
                SecretId = secretName,
                VersionId = version
            };

            var response = await _client.GetSecretValueAsync(request, cancellationToken);
            var value = response.SecretString;

            _logger.LogInformation("Successfully retrieved secret '{SecretName}' version '{Version}'", secretName, version);
            return value;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Secret '{SecretName}' version '{Version}' not found in AWS Secrets Manager", secretName, version);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' version '{Version}' from AWS Secrets Manager", secretName, version);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve secret '{secretName}' version '{version}' from AWS Secrets Manager",
                    ex,
                    ProviderName);
            }

            return null;
        }
    }

    public async Task<Dictionary<string, string>> GetSecretsAsync(IEnumerable<string> secretNames, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, string>();
        var tasks = secretNames.Select(async name =>
        {
            var value = await GetSecretAsync(name, cancellationToken);
            return (name, value);
        });

        var secretValues = await Task.WhenAll(tasks);

        foreach (var (name, value) in secretValues)
        {
            if (value != null)
            {
                results[name] = value;
            }
        }

        return results;
    }

    public async Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretValue);

        try
        {
            _logger.LogDebug("Setting secret '{SecretName}' in AWS Secrets Manager", secretName);

            // Try to update first
            try
            {
                var updateRequest = new PutSecretValueRequest
                {
                    SecretId = secretName,
                    SecretString = secretValue
                };

                await _client.PutSecretValueAsync(updateRequest, cancellationToken);
            }
            catch (ResourceNotFoundException)
            {
                // If secret doesn't exist, create it
                var createRequest = new CreateSecretRequest
                {
                    Name = secretName,
                    SecretString = secretValue
                };

                await _client.CreateSecretAsync(createRequest, cancellationToken);
            }

            // Invalidate cache
            if (_configuration.EnableCaching && _cache != null)
            {
                _cache.Remove($"{_cacheKeyPrefix}{secretName}");
            }

            _logger.LogInformation("Successfully set secret '{SecretName}'", secretName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret '{SecretName}' in AWS Secrets Manager", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to set secret '{secretName}' in AWS Secrets Manager",
                    ex,
                    ProviderName);
            }

            return false;
        }
    }

    public async Task<bool> DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        try
        {
            _logger.LogDebug("Deleting secret '{SecretName}' from AWS Secrets Manager", secretName);

            var request = new DeleteSecretRequest
            {
                SecretId = secretName,
                ForceDeleteWithoutRecovery = false, // Allow recovery for 30 days
                RecoveryWindowInDays = 30
            };

            await _client.DeleteSecretAsync(request, cancellationToken);

            // Invalidate cache
            if (_configuration.EnableCaching && _cache != null)
            {
                _cache.Remove($"{_cacheKeyPrefix}{secretName}");
            }

            _logger.LogInformation("Successfully deleted secret '{SecretName}' (30-day recovery window)", secretName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret '{SecretName}' from AWS Secrets Manager", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to delete secret '{secretName}' from AWS Secrets Manager",
                    ex,
                    ProviderName);
            }

            return false;
        }
    }

    public async Task<X509Certificate2?> GetCertificateAsync(string certificateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateName);

        try
        {
            _logger.LogDebug("Retrieving certificate '{CertificateName}' from AWS Secrets Manager", certificateName);

            var secretValue = await GetSecretAsync(certificateName, cancellationToken);
            if (secretValue == null)
            {
                return null;
            }

            // Try to parse as JSON first (in case it's a structured secret with certificate data)
            try
            {
                var jsonDoc = JsonDocument.Parse(secretValue);
                if (jsonDoc.RootElement.TryGetProperty("certificate", out var certElement))
                {
                    var certData = certElement.GetString();
                    if (certData != null)
                    {
                        var certBytes = Convert.FromBase64String(certData);
                        return new X509Certificate2(certBytes);
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON, try to parse as raw certificate data
            }

            // Try to parse as base64-encoded certificate
            try
            {
                var certBytes = Convert.FromBase64String(secretValue);
                return new X509Certificate2(certBytes);
            }
            catch (FormatException)
            {
                _logger.LogWarning("Certificate '{CertificateName}' is not in a recognized format", certificateName);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve certificate '{CertificateName}' from AWS Secrets Manager", certificateName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve certificate '{certificateName}' from AWS Secrets Manager",
                    ex,
                    ProviderName);
            }

            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListSecretsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Listing secrets from AWS Secrets Manager");

            var secretNames = new List<string>();
            var request = new ListSecretsRequest();

            do
            {
                var response = await _client.ListSecretsAsync(request, cancellationToken);
                secretNames.AddRange(response.SecretList.Select(s => s.Name));
                request.NextToken = response.NextToken;
            }
            while (!string.IsNullOrEmpty(request.NextToken));

            _logger.LogInformation("Successfully listed {Count} secrets from AWS Secrets Manager", secretNames.Count);
            return secretNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list secrets from AWS Secrets Manager");

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    "Failed to list secrets from AWS Secrets Manager",
                    ex,
                    ProviderName);
            }

            return Array.Empty<string>();
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to list secrets as a health check
            var request = new ListSecretsRequest { MaxResults = 1 };
            await _client.ListSecretsAsync(request, cancellationToken);

            _logger.LogDebug("AWS Secrets Manager health check passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AWS Secrets Manager health check failed");
            return false;
        }
    }

    public async Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        try
        {
            _logger.LogDebug("Retrieving metadata for secret '{SecretName}' from AWS Secrets Manager", secretName);

            var request = new DescribeSecretRequest { SecretId = secretName };
            var response = await _client.DescribeSecretAsync(request, cancellationToken);

            var metadata = new SecretMetadata
            {
                Name = response.Name,
                Version = response.VersionIdsToStages?.FirstOrDefault(kvp => kvp.Value.Contains("AWSCURRENT")).Key,
                CreatedOn = response.CreatedDate,
                UpdatedOn = response.LastChangedDate,
                Enabled = !response.DeletedDate.HasValue,
                Tags = response.Tags?.ToDictionary(t => t.Key, t => t.Value)
            };

            _logger.LogInformation("Successfully retrieved metadata for secret '{SecretName}'", secretName);
            return metadata;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Secret '{SecretName}' not found in AWS Secrets Manager", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve metadata for secret '{SecretName}' from AWS Secrets Manager", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve metadata for secret '{secretName}' from AWS Secrets Manager",
                    ex,
                    ProviderName);
            }

            return null;
        }
    }

    private static AWSCredentials? CreateCredentials(AwsSecretsManagerConfiguration config)
    {
        // If access key and secret are provided
        if (!string.IsNullOrWhiteSpace(config.AccessKeyId) && !string.IsNullOrWhiteSpace(config.SecretAccessKey))
        {
            if (!string.IsNullOrWhiteSpace(config.SessionToken))
            {
                return new SessionAWSCredentials(config.AccessKeyId, config.SecretAccessKey, config.SessionToken);
            }

            return new BasicAWSCredentials(config.AccessKeyId, config.SecretAccessKey);
        }

        // If profile name is provided
        if (!string.IsNullOrWhiteSpace(config.ProfileName))
        {
            return new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain().TryGetAWSCredentials(config.ProfileName, out var credentials)
                ? credentials
                : null;
        }

        // If role ARN is provided, assume role
        if (!string.IsNullOrWhiteSpace(config.RoleArn))
        {
            return new Amazon.Runtime.AssumeRoleAWSCredentials(
                FallbackCredentialsFactory.GetCredentials(),
                config.RoleArn,
                $"honua-secrets-{Guid.NewGuid():N}");
        }

        // Use default credential provider chain (environment variables, instance profile, etc.)
        return null;
    }
}
