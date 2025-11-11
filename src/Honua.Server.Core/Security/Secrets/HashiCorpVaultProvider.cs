using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Security.Secrets;

/// <summary>
/// HashiCorp Vault implementation of the secrets provider.
/// Supports KV v1 and v2, multiple authentication methods (token, AppRole, Kubernetes).
/// </summary>
public sealed class HashiCorpVaultProvider : ISecretsProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache? _cache;
    private readonly ILogger<HashiCorpVaultProvider> _logger;
    private readonly SecretsConfiguration _configuration;
    private readonly HashiCorpVaultConfiguration _vaultConfig;
    private readonly string _cacheKeyPrefix;
    private string? _authToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public string ProviderName => SecretsProviders.HashiCorpVault;

    public HashiCorpVaultProvider(
        IOptions<SecretsConfiguration> configuration,
        ILogger<HashiCorpVaultProvider> logger,
        IHttpClientFactory httpClientFactory,
        IMemoryCache? cache = null)
    {
        _configuration = configuration.Value;
        _vaultConfig = _configuration.Vault;
        _logger = logger;
        _cache = cache;
        _cacheKeyPrefix = $"{ProviderName}:";

        if (string.IsNullOrWhiteSpace(_vaultConfig.Address))
        {
            throw new SecretProviderException(
                "Vault address is not configured. Set Secrets:Vault:Address in configuration.",
                ProviderName);
        }

        _httpClient = httpClientFactory.CreateClient("VaultClient");
        _httpClient.BaseAddress = new Uri(_vaultConfig.Address);
        _httpClient.Timeout = TimeSpan.FromSeconds(_vaultConfig.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(_vaultConfig.Namespace))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Vault-Namespace", _vaultConfig.Namespace);
        }

        // Configure TLS verification
        if (_vaultConfig.SkipTlsVerify)
        {
            _logger.LogWarning("TLS verification is disabled for Vault. This should only be used in development.");
        }

        _logger.LogInformation(
            "HashiCorp Vault secrets provider initialized for address: {Address}",
            _vaultConfig.Address);
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
            await EnsureAuthenticatedAsync(cancellationToken);

            _logger.LogDebug("Retrieving secret '{SecretName}' from HashiCorp Vault", secretName);

            var path = BuildSecretPath(secretName);
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Vault-Token", _authToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Secret '{SecretName}' not found in HashiCorp Vault", secretName);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var vaultResponse = await response.Content.ReadFromJsonAsync<VaultSecretResponse>(cancellationToken);
            var value = ExtractSecretValue(vaultResponse, secretName);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' from HashiCorp Vault", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve secret '{secretName}' from HashiCorp Vault",
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

        if (_vaultConfig.KvVersion != 2)
        {
            _logger.LogWarning("Version retrieval is only supported for KV v2. Using current version.");
            return await GetSecretAsync(secretName, cancellationToken);
        }

        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            _logger.LogDebug("Retrieving secret '{SecretName}' version '{Version}' from HashiCorp Vault", secretName, version);

            var path = $"/v1/{_vaultConfig.MountPoint}/data/{secretName}?version={version}";
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Vault-Token", _authToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Secret '{SecretName}' version '{Version}' not found in HashiCorp Vault", secretName, version);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var vaultResponse = await response.Content.ReadFromJsonAsync<VaultSecretResponse>(cancellationToken);
            var value = ExtractSecretValue(vaultResponse, secretName);

            _logger.LogInformation("Successfully retrieved secret '{SecretName}' version '{Version}'", secretName, version);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' version '{Version}' from HashiCorp Vault", secretName, version);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve secret '{secretName}' version '{version}' from HashiCorp Vault",
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
            await EnsureAuthenticatedAsync(cancellationToken);

            _logger.LogDebug("Setting secret '{SecretName}' in HashiCorp Vault", secretName);

            var path = BuildSecretPath(secretName);
            var payload = BuildSecretPayload(secretName, secretValue);

            var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add("X-Vault-Token", _authToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

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
            _logger.LogError(ex, "Failed to set secret '{SecretName}' in HashiCorp Vault", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to set secret '{secretName}' in HashiCorp Vault",
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
            await EnsureAuthenticatedAsync(cancellationToken);

            _logger.LogDebug("Deleting secret '{SecretName}' from HashiCorp Vault", secretName);

            var path = _vaultConfig.KvVersion == 2
                ? $"/v1/{_vaultConfig.MountPoint}/metadata/{secretName}"
                : $"/v1/{_vaultConfig.MountPoint}/{secretName}";

            var request = new HttpRequestMessage(HttpMethod.Delete, path);
            request.Headers.Add("X-Vault-Token", _authToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

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
            _logger.LogError(ex, "Failed to delete secret '{SecretName}' from HashiCorp Vault", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to delete secret '{secretName}' from HashiCorp Vault",
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
            _logger.LogDebug("Retrieving certificate '{CertificateName}' from HashiCorp Vault", certificateName);

            var secretValue = await GetSecretAsync(certificateName, cancellationToken);
            if (secretValue == null)
            {
                return null;
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
            _logger.LogError(ex, "Failed to retrieve certificate '{CertificateName}' from HashiCorp Vault", certificateName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve certificate '{certificateName}' from HashiCorp Vault",
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
            await EnsureAuthenticatedAsync(cancellationToken);

            _logger.LogDebug("Listing secrets from HashiCorp Vault");

            var path = _vaultConfig.KvVersion == 2
                ? $"/v1/{_vaultConfig.MountPoint}/metadata?list=true"
                : $"/v1/{_vaultConfig.MountPoint}?list=true";

            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Vault-Token", _authToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var vaultResponse = await response.Content.ReadFromJsonAsync<VaultListResponse>(cancellationToken);
            var secretNames = vaultResponse?.Data?.Keys ?? Array.Empty<string>();

            _logger.LogInformation("Successfully listed {Count} secrets from HashiCorp Vault", secretNames.Length);
            return secretNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list secrets from HashiCorp Vault");

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    "Failed to list secrets from HashiCorp Vault",
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
            var response = await _httpClient.GetAsync("/v1/sys/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HashiCorp Vault health check failed");
            return false;
        }
    }

    public async Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        if (_vaultConfig.KvVersion != 2)
        {
            _logger.LogWarning("Metadata retrieval is only supported for KV v2.");
            return null;
        }

        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            _logger.LogDebug("Retrieving metadata for secret '{SecretName}' from HashiCorp Vault", secretName);

            var path = $"/v1/{_vaultConfig.MountPoint}/metadata/{secretName}";
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Vault-Token", _authToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Secret '{SecretName}' not found in HashiCorp Vault", secretName);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var vaultResponse = await response.Content.ReadFromJsonAsync<VaultMetadataResponse>(cancellationToken);

            var metadata = new SecretMetadata
            {
                Name = secretName,
                Version = vaultResponse?.Data?.CurrentVersion?.ToString(),
                CreatedOn = vaultResponse?.Data?.CreatedTime,
                UpdatedOn = vaultResponse?.Data?.UpdatedTime,
                Enabled = true
            };

            _logger.LogInformation("Successfully retrieved metadata for secret '{SecretName}'", secretName);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve metadata for secret '{SecretName}' from HashiCorp Vault", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve metadata for secret '{secretName}' from HashiCorp Vault",
                    ex,
                    ProviderName);
            }

            return null;
        }
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        // If we have a valid token, use it
        if (!string.IsNullOrWhiteSpace(_authToken) && DateTimeOffset.UtcNow < _tokenExpiry)
        {
            return;
        }

        // Direct token authentication
        if (!string.IsNullOrWhiteSpace(_vaultConfig.Token))
        {
            _authToken = _vaultConfig.Token;
            _tokenExpiry = DateTimeOffset.MaxValue; // Token auth doesn't expire
            return;
        }

        // AppRole authentication
        if (!string.IsNullOrWhiteSpace(_vaultConfig.RoleId) && !string.IsNullOrWhiteSpace(_vaultConfig.SecretId))
        {
            await AuthenticateWithAppRoleAsync(cancellationToken);
            return;
        }

        // Kubernetes authentication
        if (!string.IsNullOrWhiteSpace(_vaultConfig.KubernetesRole))
        {
            await AuthenticateWithKubernetesAsync(cancellationToken);
            return;
        }

        throw new SecretProviderException(
            "No authentication method configured for HashiCorp Vault. Set Token, RoleId/SecretId, or KubernetesRole.",
            ProviderName);
    }

    private async Task AuthenticateWithAppRoleAsync(CancellationToken cancellationToken)
    {
        var payload = new
        {
            role_id = _vaultConfig.RoleId,
            secret_id = _vaultConfig.SecretId
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/auth/approle/login", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<VaultAuthResponse>(cancellationToken);
        _authToken = authResponse?.Auth?.ClientToken ?? throw new SecretProviderException("Failed to get auth token from Vault", ProviderName);
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(authResponse.Auth.LeaseDuration);

        _logger.LogInformation("Successfully authenticated with HashiCorp Vault using AppRole");
    }

    private async Task AuthenticateWithKubernetesAsync(CancellationToken cancellationToken)
    {
        var tokenPath = _vaultConfig.KubernetesTokenPath ?? "/var/run/secrets/kubernetes.io/serviceaccount/token";
        var jwt = await File.ReadAllTextAsync(tokenPath, cancellationToken);

        var payload = new
        {
            role = _vaultConfig.KubernetesRole,
            jwt
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/auth/kubernetes/login", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<VaultAuthResponse>(cancellationToken);
        _authToken = authResponse?.Auth?.ClientToken ?? throw new SecretProviderException("Failed to get auth token from Vault", ProviderName);
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(authResponse.Auth.LeaseDuration);

        _logger.LogInformation("Successfully authenticated with HashiCorp Vault using Kubernetes");
    }

    private string BuildSecretPath(string secretName)
    {
        return _vaultConfig.KvVersion == 2
            ? $"/v1/{_vaultConfig.MountPoint}/data/{secretName}"
            : $"/v1/{_vaultConfig.MountPoint}/{secretName}";
    }

    private object BuildSecretPayload(string secretName, string secretValue)
    {
        if (_vaultConfig.KvVersion == 2)
        {
            return new
            {
                data = new Dictionary<string, string>
                {
                    ["value"] = secretValue
                }
            };
        }

        return new Dictionary<string, string>
        {
            ["value"] = secretValue
        };
    }

    private static string? ExtractSecretValue(VaultSecretResponse? response, string secretName)
    {
        if (response?.Data == null)
        {
            return null;
        }

        // KV v2 stores data in data.data
        if (response.Data.TryGetValue("data", out var dataObj) && dataObj is JsonElement dataElement)
        {
            if (dataElement.TryGetProperty("value", out var valueElement))
            {
                return valueElement.GetString();
            }
        }

        // KV v1 stores data directly in data
        if (response.Data.TryGetValue("value", out var valueObj) && valueObj is JsonElement valueJsonElement)
        {
            return valueJsonElement.GetString();
        }

        return null;
    }

    #region Vault Response Models

    private sealed class VaultSecretResponse
    {
        [JsonPropertyName("data")]
        public Dictionary<string, object>? Data { get; set; }
    }

    private sealed class VaultListResponse
    {
        [JsonPropertyName("data")]
        public VaultListData? Data { get; set; }
    }

    private sealed class VaultListData
    {
        [JsonPropertyName("keys")]
        public string[]? Keys { get; set; }
    }

    private sealed class VaultAuthResponse
    {
        [JsonPropertyName("auth")]
        public required VaultAuthData Auth { get; set; }
    }

    private sealed class VaultAuthData
    {
        [JsonPropertyName("client_token")]
        public required string ClientToken { get; set; }

        [JsonPropertyName("lease_duration")]
        public int LeaseDuration { get; set; }
    }

    private sealed class VaultMetadataResponse
    {
        [JsonPropertyName("data")]
        public VaultMetadataData? Data { get; set; }
    }

    private sealed class VaultMetadataData
    {
        [JsonPropertyName("created_time")]
        public DateTimeOffset? CreatedTime { get; set; }

        [JsonPropertyName("updated_time")]
        public DateTimeOffset? UpdatedTime { get; set; }

        [JsonPropertyName("current_version")]
        public int? CurrentVersion { get; set; }
    }

    #endregion
}
