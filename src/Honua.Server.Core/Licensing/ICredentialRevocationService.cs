// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Licensing;

/// <summary>
/// Service for revoking registry credentials when licenses expire or are revoked.
/// </summary>
public interface ICredentialRevocationService
{
    /// <summary>
    /// Revokes all expired credentials (background job).
    /// </summary>
    Task RevokeExpiredCredentialsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all credentials for a specific customer.
    /// </summary>
    Task RevokeCustomerCredentialsAsync(string customerId, string reason, string revokedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes AWS IAM credentials for a customer.
    /// </summary>
    Task RevokeAwsCredentialsAsync(string customerId, string reason, string revokedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes GitHub Personal Access Token for a customer.
    /// </summary>
    Task RevokeGitHubCredentialsAsync(string customerId, string reason, string revokedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes Azure service principal for a customer.
    /// </summary>
    Task RevokeAzureCredentialsAsync(string customerId, string reason, string revokedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes GCP service account for a customer.
    /// </summary>
    Task RevokeGcpCredentialsAsync(string customerId, string reason, string revokedBy, CancellationToken cancellationToken = default);
}
