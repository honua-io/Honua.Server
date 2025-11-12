// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Licensing.Models;

namespace Honua.Server.Enterprise.Licensing;

/// <summary>
/// Interface for credential revocation data storage.
/// </summary>
public interface ICredentialRevocationStore
{
    /// <summary>
    /// Records a credential revocation event.
    /// </summary>
    Task RecordRevocationAsync(CredentialRevocation revocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all revocations for a customer.
    /// </summary>
    Task<CredentialRevocation[]> GetRevocationsByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
}
