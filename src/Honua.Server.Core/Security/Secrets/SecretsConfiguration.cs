namespace Honua.Server.Core.Security.Secrets;

/// <summary>
/// Configuration options for secrets management.
/// </summary>
public sealed class SecretsConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Secrets";

    /// <summary>
    /// The secrets provider to use. Supported values: AzureKeyVault, AwsSecretsManager, HashiCorpVault, Local.
    /// </summary>
    public string Provider { get; set; } = "Local";

    /// <summary>
    /// Azure Key Vault configuration.
    /// </summary>
    public AzureKeyVaultConfiguration AzureKeyVault { get; set; } = new();

    /// <summary>
    /// AWS Secrets Manager configuration.
    /// </summary>
    public AwsSecretsManagerConfiguration AwsSecretsManager { get; set; } = new();

    /// <summary>
    /// HashiCorp Vault configuration.
    /// </summary>
    public HashiCorpVaultConfiguration Vault { get; set; } = new();

    /// <summary>
    /// Local development secrets configuration.
    /// </summary>
    public LocalSecretsConfiguration Local { get; set; } = new();

    /// <summary>
    /// Whether to cache secrets in memory (default: true).
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration in seconds (default: 300 = 5 minutes).
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to throw exceptions on secret retrieval failures or return null (default: false = return null).
    /// </summary>
    public bool ThrowOnError { get; set; } = false;
}

/// <summary>
/// Azure Key Vault specific configuration.
/// </summary>
public sealed class AzureKeyVaultConfiguration
{
    /// <summary>
    /// The URI of the Azure Key Vault (e.g., "https://myvault.vault.azure.net/").
    /// </summary>
    public string? VaultUri { get; set; }

    /// <summary>
    /// The tenant ID for authentication (optional - uses default credential if not specified).
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Client ID for service principal authentication (optional).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client secret for service principal authentication (optional).
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Whether to use managed identity (default: true).
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Timeout in seconds for Key Vault operations (default: 30).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts (default: 3).
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// AWS Secrets Manager specific configuration.
/// </summary>
public sealed class AwsSecretsManagerConfiguration
{
    /// <summary>
    /// AWS region (e.g., "us-east-1").
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// AWS access key ID (optional - uses default credential provider chain if not specified).
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// AWS secret access key (optional).
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// AWS session token for temporary credentials (optional).
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// AWS profile name from credentials file (optional).
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// ARN of the role to assume (optional).
    /// </summary>
    public string? RoleArn { get; set; }

    /// <summary>
    /// Timeout in seconds for Secrets Manager operations (default: 30).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts (default: 3).
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// HashiCorp Vault specific configuration.
/// </summary>
public sealed class HashiCorpVaultConfiguration
{
    /// <summary>
    /// Vault server address (e.g., "https://vault.example.com:8200").
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Vault authentication token.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Vault namespace (for Vault Enterprise).
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Mount point for the KV secrets engine (default: "secret").
    /// </summary>
    public string MountPoint { get; set; } = "secret";

    /// <summary>
    /// KV version (1 or 2, default: 2).
    /// </summary>
    public int KvVersion { get; set; } = 2;

    /// <summary>
    /// Role ID for AppRole authentication (alternative to Token).
    /// </summary>
    public string? RoleId { get; set; }

    /// <summary>
    /// Secret ID for AppRole authentication.
    /// </summary>
    public string? SecretId { get; set; }

    /// <summary>
    /// Path to Kubernetes service account token for Kubernetes authentication.
    /// </summary>
    public string? KubernetesTokenPath { get; set; }

    /// <summary>
    /// Kubernetes role for authentication.
    /// </summary>
    public string? KubernetesRole { get; set; }

    /// <summary>
    /// Timeout in seconds for Vault operations (default: 30).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts (default: 3).
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Whether to skip TLS verification (only for development, default: false).
    /// </summary>
    public bool SkipTlsVerify { get; set; } = false;
}

/// <summary>
/// Local development secrets configuration.
/// </summary>
public sealed class LocalSecretsConfiguration
{
    /// <summary>
    /// Path to the secrets file (default: "secrets.json" in app directory).
    /// </summary>
    public string? SecretsFilePath { get; set; }

    /// <summary>
    /// Whether to encrypt the local secrets file (default: true).
    /// </summary>
    public bool EncryptFile { get; set; } = true;

    /// <summary>
    /// Whether to watch the secrets file for changes (default: true).
    /// </summary>
    public bool WatchForChanges { get; set; } = true;

    /// <summary>
    /// Whether to use user secrets for development (default: true).
    /// </summary>
    public bool UseUserSecrets { get; set; } = true;

    /// <summary>
    /// User secrets ID (optional - uses assembly name if not specified).
    /// </summary>
    public string? UserSecretsId { get; set; }
}

/// <summary>
/// Supported secrets providers.
/// </summary>
public static class SecretsProviders
{
    public const string AzureKeyVault = "AzureKeyVault";
    public const string AwsSecretsManager = "AwsSecretsManager";
    public const string HashiCorpVault = "HashiCorpVault";
    public const string Local = "Local";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        AzureKeyVault,
        AwsSecretsManager,
        HashiCorpVault,
        Local
    };

    public static bool IsValid(string provider)
    {
        return All.Contains(provider);
    }
}
