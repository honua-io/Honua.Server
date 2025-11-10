// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Secrets;

/// <summary>
/// Manages secure storage and retrieval of secrets (credentials, API keys, etc.).
/// The AI assistant NEVER calls GetSecretAsync directly - only requests scoped tokens.
/// </summary>
public interface ISecretsManager
{
    /// <summary>
    /// Retrieves a secret by name.
    /// ⚠️ AI NEVER calls this - only user-initiated operations.
    /// </summary>
    Task<Secret> GetSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a secret securely.
    /// </summary>
    Task SetSecretAsync(string name, string value, SecretMetadata? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret.
    /// </summary>
    Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available secret names (values are never exposed).
    /// </summary>
    Task<IReadOnlyList<string>> ListSecretsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// AI calls this to request temporary, scoped access to a secret.
    /// Returns a limited-access token instead of the actual secret.
    /// </summary>
    Task<ScopedToken> RequestScopedAccessAsync(
        string secretName,
        AccessScope scope,
        TimeSpan duration,
        string purpose,
        bool requireUserApproval = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a token immediately (user can cancel AI access at any time).
    /// </summary>
    Task RevokeTokenAsync(string tokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all active tokens (for user visibility and control).
    /// </summary>
    Task<IReadOnlyList<ScopedToken>> ListActiveTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all active tokens (emergency kill switch).
    /// </summary>
    Task RevokeAllTokensAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a stored secret.
/// </summary>
public sealed class Secret
{
    /// <summary>
    /// Name/identifier for the secret.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The actual secret value (password, API key, connection string, etc.).
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Optional metadata about the secret.
    /// </summary>
    public SecretMetadata? Metadata { get; init; }

    /// <summary>
    /// When the secret was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the secret was last modified.
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Metadata about a secret (for display/management, not security).
/// </summary>
public sealed class SecretMetadata
{
    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Tags for organization (e.g., "production", "database", "aws").
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Type of secret (for UI hints).
    /// </summary>
    public SecretType Type { get; init; } = SecretType.Generic;

    /// <summary>
    /// When the secret expires (for rotation reminders).
    /// </summary>
    public DateTime? ExpiresAt { get; init; }
}

public enum SecretType
{
    Generic,
    DatabaseConnection,
    ApiKey,
    Certificate,
    SshKey,
    AccessToken
}

/// <summary>
/// Temporary, scoped token for AI access to secrets.
/// The AI gets this instead of the actual secret.
/// </summary>
public sealed class ScopedToken
{
    /// <summary>
    /// Unique token identifier (for revocation).
    /// </summary>
    public required string TokenId { get; init; }

    /// <summary>
    /// The temporary credential to use.
    /// This could be:
    /// - A temporary database password
    /// - A short-lived JWT
    /// - A session token
    /// - A restricted API key
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Reference to the original secret (for auditing).
    /// </summary>
    public required string SecretRef { get; init; }

    /// <summary>
    /// Scope of access granted.
    /// </summary>
    public required AccessScope Scope { get; init; }

    /// <summary>
    /// When the token expires.
    /// </summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Whether the token has been revoked.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// When the token was revoked (null if still active).
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    // Audit trail
    public required string RequestedBy { get; init; }  // "AI Assistant"
    public required string Purpose { get; init; }      // "Create database indexes"
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Number of times this token has been used.
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Last time the token was used.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Checks if the token is still valid.
    /// </summary>
    public bool IsValid => !IsRevoked && DateTime.UtcNow < ExpiresAt;
}

/// <summary>
/// Defines the scope of access for a token.
/// </summary>
public sealed class AccessScope
{
    /// <summary>
    /// Access level (read-only, DDL, admin, etc.).
    /// </summary>
    public required AccessLevel Level { get; init; }

    /// <summary>
    /// Specific operations allowed (e.g., ["CREATE INDEX", "CREATE STATISTICS"]).
    /// Empty list = all operations for the access level.
    /// </summary>
    public List<string> AllowedOperations { get; init; } = new();

    /// <summary>
    /// Operations explicitly denied.
    /// </summary>
    public List<string> DeniedOperations { get; init; } = new();

    /// <summary>
    /// Resources the token can access (e.g., ["table:users", "table:products"]).
    /// Empty list = all resources for the access level.
    /// </summary>
    public List<string> AllowedResources { get; init; } = new();
}

public enum AccessLevel
{
    /// <summary>
    /// Read-only access (SELECT, SHOW, EXPLAIN).
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Data Definition Language (CREATE INDEX, CREATE STATISTICS, ALTER TABLE ADD COLUMN).
    /// Does NOT include DROP operations.
    /// </summary>
    DDL,

    /// <summary>
    /// Data Manipulation Language (INSERT, UPDATE, DELETE).
    /// ⚠️ Dangerous - requires explicit user approval.
    /// </summary>
    DML,

    /// <summary>
    /// Configuration changes (ALTER SYSTEM, UPDATE pg_settings).
    /// </summary>
    Config,

    /// <summary>
    /// Full administrative access.
    /// ⚠️ Very dangerous - rarely granted to AI.
    /// </summary>
    Admin
}

/// <summary>
/// Options for configuring the secrets manager.
/// </summary>
public sealed class SecretsManagerOptions
{
    /// <summary>
    /// Backend to use for storing secrets.
    /// </summary>
    public SecretsBackend Backend { get; set; } = SecretsBackend.OSKeychain;

    /// <summary>
    /// Path for file-based storage (development only).
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Azure Key Vault URL (if using Azure backend).
    /// </summary>
    public string? AzureKeyVaultUrl { get; set; }

    /// <summary>
    /// AWS Secrets Manager region (if using AWS backend).
    /// </summary>
    public string? AwsRegion { get; set; }

    /// <summary>
    /// HashiCorp Vault address (if using Vault backend).
    /// </summary>
    public string? VaultAddress { get; set; }

    /// <summary>
    /// Default token duration if not specified in request.
    /// </summary>
    public TimeSpan DefaultTokenDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Maximum allowed token duration.
    /// </summary>
    public TimeSpan MaxTokenDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether to require user approval for all token requests.
    /// </summary>
    public bool RequireUserApproval { get; set; } = true;
}

public enum SecretsBackend
{
    /// <summary>
    /// Use OS keychain (macOS Keychain, Windows Credential Manager, Linux Secret Service).
    /// Best for local development.
    /// </summary>
    OSKeychain,

    /// <summary>
    /// File-based storage (ENCRYPTED).
    /// ⚠️ Development only - not for production.
    /// </summary>
    EncryptedFile,

    /// <summary>
    /// Azure Key Vault.
    /// </summary>
    AzureKeyVault,

    /// <summary>
    /// AWS Secrets Manager.
    /// </summary>
    AWSSecretsManager,

    /// <summary>
    /// HashiCorp Vault.
    /// </summary>
    HashiCorpVault
}
