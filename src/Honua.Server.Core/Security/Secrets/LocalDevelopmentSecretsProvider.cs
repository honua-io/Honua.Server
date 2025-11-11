using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Honua.Server.Core.Security.Secrets;

/// <summary>
/// Local development secrets provider that uses user secrets and file-based storage.
/// Should only be used for development environments, not production.
/// </summary>
public sealed class LocalDevelopmentSecretsProvider : ISecretsProvider
{
    private readonly ILogger<LocalDevelopmentSecretsProvider> _logger;
    private readonly SecretsConfiguration _configuration;
    private readonly IDataProtector? _dataProtector;
    private readonly string _secretsFilePath;
    private readonly FileSystemWatcher? _fileWatcher;
    private Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IConfiguration? _userSecretsConfig;

    public string ProviderName => SecretsProviders.Local;

    public LocalDevelopmentSecretsProvider(
        IOptions<SecretsConfiguration> configuration,
        ILogger<LocalDevelopmentSecretsProvider> logger,
        IDataProtectionProvider? dataProtectionProvider = null,
        IConfiguration? userSecretsConfig = null)
    {
        _configuration = configuration.Value;
        _logger = logger;
        _userSecretsConfig = userSecretsConfig;

        var localConfig = _configuration.Local;

        // Determine secrets file path
        _secretsFilePath = localConfig.SecretsFilePath
            ?? Path.Combine(AppContext.BaseDirectory, "secrets.json");

        // Set up data protection if encryption is enabled
        if (localConfig.EncryptFile && dataProtectionProvider != null)
        {
            _dataProtector = dataProtectionProvider.CreateProtector("LocalSecretsProvider");
        }

        // Load secrets from file
        LoadSecretsFromFile();

        // Set up file watcher if enabled
        if (localConfig.WatchForChanges && File.Exists(_secretsFilePath))
        {
            var directory = Path.GetDirectoryName(_secretsFilePath);
            var fileName = Path.GetFileName(_secretsFilePath);

            if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(fileName))
            {
                _fileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _fileWatcher.Changed += OnSecretsFileChanged;
                _fileWatcher.EnableRaisingEvents = true;
            }
        }

        _logger.LogInformation(
            "Local development secrets provider initialized. Secrets file: {FilePath}",
            _secretsFilePath);
    }

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Try local secrets file first
            if (_secrets.TryGetValue(secretName, out var value))
            {
                _logger.LogDebug("Retrieved secret '{SecretName}' from local file", secretName);
                return value;
            }

            // Try user secrets if enabled
            if (_configuration.Local.UseUserSecrets && _userSecretsConfig != null)
            {
                var userSecretValue = _userSecretsConfig[secretName];
                if (!string.IsNullOrEmpty(userSecretValue))
                {
                    _logger.LogDebug("Retrieved secret '{SecretName}' from user secrets", secretName);
                    return userSecretValue;
                }
            }

            _logger.LogDebug("Secret '{SecretName}' not found", secretName);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<string?> GetSecretVersionAsync(string secretName, string version, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Version retrieval is not supported in local development provider. Returning current version.");
        return GetSecretAsync(secretName, cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetSecretsAsync(IEnumerable<string> secretNames, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, string>();

        foreach (var name in secretNames)
        {
            var value = await GetSecretAsync(name, cancellationToken);
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

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Setting secret '{SecretName}' in local file", secretName);

            _secrets[secretName] = secretValue;
            SaveSecretsToFile();

            _logger.LogInformation("Successfully set secret '{SecretName}'", secretName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret '{SecretName}'", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to set secret '{secretName}' in local provider",
                    ex,
                    ProviderName);
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Deleting secret '{SecretName}' from local file", secretName);

            var removed = _secrets.Remove(secretName);
            if (removed)
            {
                SaveSecretsToFile();
                _logger.LogInformation("Successfully deleted secret '{SecretName}'", secretName);
            }

            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret '{SecretName}'", secretName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to delete secret '{secretName}' from local provider",
                    ex,
                    ProviderName);
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<X509Certificate2?> GetCertificateAsync(string certificateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateName);

        try
        {
            _logger.LogDebug("Retrieving certificate '{CertificateName}' from local provider", certificateName);

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
                // Try as file path
                if (File.Exists(secretValue))
                {
                    return new X509Certificate2(secretValue);
                }

                _logger.LogWarning("Certificate '{CertificateName}' is not in a recognized format", certificateName);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve certificate '{CertificateName}'", certificateName);

            if (_configuration.ThrowOnError)
            {
                throw new SecretProviderException(
                    $"Failed to retrieve certificate '{certificateName}' from local provider",
                    ex,
                    ProviderName);
            }

            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListSecretsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var secretNames = _secrets.Keys.ToList();

            // Add user secrets if enabled
            if (_configuration.Local.UseUserSecrets && _userSecretsConfig != null)
            {
                foreach (var kvp in _userSecretsConfig.AsEnumerable())
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && !secretNames.Contains(kvp.Key))
                    {
                        secretNames.Add(kvp.Key);
                    }
                }
            }

            _logger.LogDebug("Listed {Count} secrets from local provider", secretNames.Count);
            return secretNames;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        // Local provider is always healthy
        return Task.FromResult(true);
    }

    public async Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_secrets.ContainsKey(secretName))
            {
                return null;
            }

            var fileInfo = new FileInfo(_secretsFilePath);
            return new SecretMetadata
            {
                Name = secretName,
                CreatedOn = fileInfo.CreationTimeUtc,
                UpdatedOn = fileInfo.LastWriteTimeUtc,
                Enabled = true
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    private void LoadSecretsFromFile()
    {
        if (!File.Exists(_secretsFilePath))
        {
            _logger.LogInformation("Secrets file not found at {FilePath}. Starting with empty secrets.", _secretsFilePath);
            _secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            var fileContent = File.ReadAllText(_secretsFilePath);

            if (_configuration.Local.EncryptFile && _dataProtector != null)
            {
                try
                {
                    fileContent = _dataProtector.Unprotect(fileContent);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt secrets file. File may not be encrypted. Trying to read as plain text.");
                }
            }

            _secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(fileContent)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Loaded {Count} secrets from file", _secrets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load secrets from file: {FilePath}", _secretsFilePath);
            _secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveSecretsToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_secrets, new JsonSerializerOptions { WriteIndented = true });

            if (_configuration.Local.EncryptFile && _dataProtector != null)
            {
                json = _dataProtector.Protect(json);
            }

            var directory = Path.GetDirectoryName(_secretsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_secretsFilePath, json);
            _logger.LogDebug("Saved {Count} secrets to file", _secrets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save secrets to file: {FilePath}", _secretsFilePath);
            throw;
        }
    }

    private void OnSecretsFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Secrets file changed, reloading...");

        // Debounce: wait a bit for the file write to complete
        Task.Delay(100).ContinueWith(_ =>
        {
            _lock.Wait();
            try
            {
                LoadSecretsFromFile();
                _logger.LogInformation("Secrets reloaded from file");
            }
            finally
            {
                _lock.Release();
            }
        });
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _lock.Dispose();
    }
}
