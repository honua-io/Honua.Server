using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;

namespace Honua.Server.Core.Security.Secrets;

/// <summary>
/// Azure Key Vault implementation of the secrets provider.
/// Supports managed identity, service principal, and default Azure credentials.
/// </summary>
public sealed class AzureKeyVaultSecretsProvider : ISecretsProvider
{
    private readonly SecretClient _secretClient;
    private readonly CertificateClient _certificateClient;
    private readonly IMemoryCache? _cache;
    private readonly ILogger<AzureKeyVaultSecretsProvider> _logger;
    private readonly SecretsConfiguration _configuration;
    private readonly string _cacheKeyPrefix;

    public string ProviderName => SecretsProviders.AzureKeyVault;

    public AzureKeyVaultSecretsProvider(
        IOptions<SecretsConfiguration> configuration,
        ILogger<AzureKeyVaultSecretsProvider> logger,
        IMemoryCache? cache = null)
    {
        _configuration = configuration.Value;
        _logger = logger;
        _cache = cache;
        _cacheKeyPrefix = $"{ProviderName}:";

        var azureConfig = _configuration.AzureKeyVault;

        if (string.IsNullOrWhiteSpace(azureConfig.VaultUri))
        {
            throw new SecretProviderException(
                "Azure Key Vault URI is not configured. Set Secrets:AzureKeyVault:VaultUri in configuration.",
                ProviderName);
        }

        var vaultUri = new Uri(azureConfig.VaultUri);
        var credential = CreateCredential(azureConfig);

        var secretClientOptions = new SecretClientOptions
        {
            Retry =
            {
                MaxRetries = azureConfig.MaxRetries,
                NetworkTimeout = TimeSpan.FromSeconds(azureConfig.TimeoutSeconds)
            }
        };

        _secretClient = new SecretClient(vaultUri, credential, secretClientOptions);
        _certificateClient = new CertificateClient(vaultUri, credential);

        _logger.LogInformation(
            "Azure Key Vault secrets provider initialized for vault: {VaultUri}",
            azureConfig.VaultUri);
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
            _logger.LogDebug("Retrieving secret '{SecretName}' from Azure Key Vault", secretName);

            var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            var value = response.Value?.Value;

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
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret '{SecretName}' not found in Azure Key Vault", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' from Azure Key Vault", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve secret '{secretName}' from Azure Key Vault",
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
            _logger.LogDebug("Retrieving secret '{SecretName}' version '{Version}' from Azure Key Vault", secretName, version);

            var response = await _secretClient.GetSecretAsync(secretName, version, cancellationToken);
            var value = response.Value?.Value;

            _logger.LogInformation("Successfully retrieved secret '{SecretName}' version '{Version}'", secretName, version);
            return value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret '{SecretName}' version '{Version}' not found in Azure Key Vault", secretName, version);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' version '{Version}' from Azure Key Vault", secretName, version);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve secret '{secretName}' version '{version}' from Azure Key Vault",
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
            _logger.LogDebug("Setting secret '{SecretName}' in Azure Key Vault", secretName);

            await _secretClient.SetSecretAsync(secretName, secretValue, cancellationToken);

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
            _logger.LogError(ex, "Failed to set secret '{SecretName}' in Azure Key Vault", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to set secret '{secretName}' in Azure Key Vault",
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
            _logger.LogDebug("Deleting secret '{SecretName}' from Azure Key Vault", secretName);

            var operation = await _secretClient.StartDeleteSecretAsync(secretName, cancellationToken);
            await operation.WaitForCompletionAsync(cancellationToken);

            // Invalidate cache
            if (_configuration.EnableCaching && _cache != null)
            {
                _cache.Remove($"{_cacheKeyPrefix}{secretName}");
            }

            _logger.LogInformation("Successfully deleted secret '{SecretName}'", secretName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret '{SecretName}' from Azure Key Vault", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to delete secret '{secretName}' from Azure Key Vault",
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
            _logger.LogDebug("Retrieving certificate '{CertificateName}' from Azure Key Vault", certificateName);

            var response = await _certificateClient.DownloadCertificateAsync(certificateName, cancellationToken: cancellationToken);
            var certificate = response.Value;

            _logger.LogInformation("Successfully retrieved certificate '{CertificateName}'", certificateName);
            return certificate;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Certificate '{CertificateName}' not found in Azure Key Vault", certificateName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve certificate '{CertificateName}' from Azure Key Vault", certificateName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve certificate '{certificateName}' from Azure Key Vault",
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
            _logger.LogDebug("Listing secrets from Azure Key Vault");

            var secretNames = new List<string>();
            await foreach (var secret in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
            {
                secretNames.Add(secret.Name);
            }

            _logger.LogInformation("Successfully listed {Count} secrets from Azure Key Vault", secretNames.Count);
            return secretNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list secrets from Azure Key Vault");

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    "Failed to list secrets from Azure Key Vault",
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
            await foreach (var _ in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken).Take(1))
            {
                break;
            }

            _logger.LogDebug("Azure Key Vault health check passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Key Vault health check failed");
            return false;
        }
    }

    public async Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        try
        {
            _logger.LogDebug("Retrieving metadata for secret '{SecretName}' from Azure Key Vault", secretName);

            var properties = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            var secretProperties = properties.Value.Properties;

            var metadata = new SecretMetadata
            {
                Name = secretProperties.Name,
                Version = secretProperties.Version,
                CreatedOn = secretProperties.CreatedOn,
                UpdatedOn = secretProperties.UpdatedOn,
                ExpiresOn = secretProperties.ExpiresOn,
                Enabled = secretProperties.Enabled ?? true,
                ContentType = secretProperties.ContentType,
                Tags = secretProperties.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            _logger.LogInformation("Successfully retrieved metadata for secret '{SecretName}'", secretName);
            return metadata;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret '{SecretName}' not found in Azure Key Vault", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve metadata for secret '{SecretName}' from Azure Key Vault", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve metadata for secret '{secretName}' from Azure Key Vault",
                    ex,
                    ProviderName);
            }

            return null;
        }
    }

    private static Azure.Core.TokenCredential CreateCredential(AzureKeyVaultConfiguration config)
    {
        // If client ID and secret are provided, use ClientSecretCredential
        if (!string.IsNullOrWhiteSpace(config.ClientId) && !string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            return new ClientSecretCredential(
                config.TenantId ?? throw new SecretProviderException("TenantId is required when using ClientId and ClientSecret"),
                config.ClientId,
                config.ClientSecret);
        }

        // If managed identity is explicitly requested or nothing else is configured, use ManagedIdentityCredential
        if (config.UseManagedIdentity)
        {
            return !string.IsNullOrWhiteSpace(config.ClientId)
                ? new ManagedIdentityCredential(config.ClientId)
                : new ManagedIdentityCredential();
        }

        // Fall back to DefaultAzureCredential (tries multiple credential types)
        return new DefaultAzureCredential();
    }
}
