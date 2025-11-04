// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.GitOps;

/// <summary>
/// Service for managing certificate renewals during GitOps reconciliation
/// </summary>
public interface ICertificateRenewalService
{
    /// <summary>
    /// Update certificate configuration from application settings
    /// </summary>
    /// <param name="appSettingsJson">The application settings JSON</param>
    /// <param name="environment">The target environment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateCertificateConfigurationAsync(string appSettingsJson, string environment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify certificates are valid and not expired
    /// </summary>
    /// <param name="environment">The target environment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> VerifyCertificatesAsync(string environment, CancellationToken cancellationToken = default);
}
