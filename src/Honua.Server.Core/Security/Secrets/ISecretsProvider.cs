using System.Security.Cryptography.X509Certificates;

namespace Honua.Server.Core.Security.Secrets;

/// <summary>
/// Provides a unified interface for retrieving secrets from various secret management providers.
/// Implementations include Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, and local development providers.
/// </summary>
public interface ISecretsProvider
{
    /// <summary>
    /// Gets the name of the provider (e.g., "AzureKeyVault", "AwsSecretsManager", "HashiCorpVault", "Local").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Retrieves a secret string value by name.
    /// </summary>
    /// <param name="secretName">The name/identifier of the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value, or null if not found.</returns>
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a secret string value by name with a specific version.
    /// </summary>
    /// <param name="secretName">The name/identifier of the secret.</param>
    /// <param name="version">The version of the secret to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value, or null if not found.</returns>
    Task<string?> GetSecretVersionAsync(string secretName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves multiple secrets by name in a single call for efficiency.
    /// </summary>
    /// <param name="secretNames">The names of the secrets to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of secret name to value. Missing secrets will not be included.</returns>
    Task<Dictionary<string, string>> GetSecretsAsync(IEnumerable<string> secretNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores or updates a secret value.
    /// </summary>
    /// <param name="secretName">The name/identifier of the secret.</param>
    /// <param name="secretValue">The secret value to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret.
    /// </summary>
    /// <param name="secretName">The name/identifier of the secret to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a certificate from the secret store.
    /// </summary>
    /// <param name="certificateName">The name of the certificate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The certificate, or null if not found.</returns>
    Task<X509Certificate2?> GetCertificateAsync(string certificateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all secret names available in the provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of secret names.</returns>
    Task<IReadOnlyList<string>> ListSecretsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the provider is healthy and can access the secret store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if healthy, false otherwise.</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata about a secret without retrieving its value.
    /// </summary>
    /// <param name="secretName">The name of the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Secret metadata, or null if not found.</returns>
    Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata about a secret without exposing its value.
/// </summary>
public sealed class SecretMetadata
{
    /// <summary>
    /// The name of the secret.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The current version of the secret.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// When the secret was created.
    /// </summary>
    public DateTimeOffset? CreatedOn { get; init; }

    /// <summary>
    /// When the secret was last updated.
    /// </summary>
    public DateTimeOffset? UpdatedOn { get; init; }

    /// <summary>
    /// When the secret expires (if applicable).
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; init; }

    /// <summary>
    /// Whether the secret is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Tags or metadata associated with the secret.
    /// </summary>
    public Dictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// The content type of the secret (e.g., "application/json", "text/plain").
    /// </summary>
    public string? ContentType { get; init; }
}

/// <summary>
/// Exception thrown when a secret operation fails.
/// </summary>
public sealed class SecretProviderException : Exception
{
    public string? ProviderName { get; }

    public SecretProviderException(string message, string? providerName = null)
        : base(message)
    {
        ProviderName = providerName;
    }

    public SecretProviderException(string message, Exception innerException, string? providerName = null)
        : base(message, innerException)
    {
        ProviderName = providerName;
    }
}
