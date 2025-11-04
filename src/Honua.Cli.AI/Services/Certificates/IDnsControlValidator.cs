// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Certificates;

/// <summary>
/// Interface for validating DNS control before ACME DNS-01 challenges.
/// Implementations should verify that the DNS provider API is accessible
/// and that TXT records can be created/deleted for the domain.
/// </summary>
public interface IDnsControlValidator
{
    /// <summary>
    /// Validates that we have DNS control for the specified domain.
    /// Tests creating and deleting a test TXT record to ensure the provider is accessible.
    /// </summary>
    /// <param name="domain">The domain to validate control for</param>
    /// <returns>A result indicating whether DNS control is established</returns>
    Task<DnsControlValidationResult> ValidateDnsControlAsync(string domain);
}

/// <summary>
/// Result of DNS control validation.
/// </summary>
public sealed class DnsControlValidationResult
{
    /// <summary>
    /// Whether DNS control is established for the domain.
    /// </summary>
    public bool HasControl { get; set; }

    /// <summary>
    /// Name of the DNS provider used for validation.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Reason for validation failure, if HasControl is false.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// The zone ID or name that was found for the domain.
    /// </summary>
    public string? ZoneIdentifier { get; set; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static DnsControlValidationResult Success(string providerName, string? zoneIdentifier = null)
    {
        return new DnsControlValidationResult
        {
            HasControl = true,
            ProviderName = providerName,
            ZoneIdentifier = zoneIdentifier
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static DnsControlValidationResult Failure(string providerName, string failureReason)
    {
        return new DnsControlValidationResult
        {
            HasControl = false,
            ProviderName = providerName,
            FailureReason = failureReason
        };
    }
}
