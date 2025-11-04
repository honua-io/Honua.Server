// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using CertificateRenewalState = Honua.Cli.AI.Services.Processes.State.CertificateRenewalState;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Honua.Cli.AI.Services.Certificates;
using Honua.Cli.AI.Services.Certificates.DnsChallenge;
using DnsClient;
using DnsClient.Protocol;

namespace Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

/// <summary>
/// Completes ACME DNS challenge by creating _acme-challenge TXT records.
/// Waits for DNS propagation before notifying ACME provider.
/// </summary>
public class ValidateDomainStep : KernelProcessStep<CertificateRenewalState>
{
    private readonly ILogger<ValidateDomainStep> _logger;
    private readonly IChallengeProvider? _challengeProvider;
    private CertificateRenewalState _state = new();

    public ValidateDomainStep(ILogger<ValidateDomainStep> logger, IChallengeProvider? challengeProvider = null)
    {
        _logger = logger;
        _challengeProvider = challengeProvider;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<CertificateRenewalState> state)
    {
        _state = state.State ?? new CertificateRenewalState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("CompleteDNSChallenge")]
    public async Task CompleteDNSChallengeAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Completing DNS challenge for {Count} domains",
            _state.DnsChallengeRecords.Count);

        _state.Status = "Completing DNS Challenge";

        var challengesDeployed = new List<(string domain, string challengeToken)>();

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // If a challenge provider is configured, use it to deploy challenges
                    if (_challengeProvider != null)
                    {
                        _logger.LogInformation("Using configured challenge provider to deploy DNS challenges");

                        foreach (var (domain, challengeToken) in _state.DnsChallengeRecords)
                        {
                            // Skip the order URL entry and other metadata
                            if (domain.StartsWith("_") || domain.Contains("_token") || domain.Contains("_challengeType"))
                                continue;

                            // Get token and challenge type from state
                            var token = _state.DnsChallengeRecords.GetValueOrDefault($"{domain}_token", "");
                            var challengeType = _state.DnsChallengeRecords.GetValueOrDefault($"{domain}_challengeType", _state.ValidationMethod);

                            _logger.LogInformation("Deploying {ChallengeType} challenge for {Domain}", challengeType, domain);

                            await _challengeProvider.DeployChallengeAsync(
                                domain,
                                token,
                                challengeToken,
                                challengeType,
                                CancellationToken.None);

                            // Track deployed challenges for cleanup
                            challengesDeployed.Add((domain, challengeToken));
                        }

                        _logger.LogInformation("All DNS challenges deployed via provider");
                    }
                    else
                    {
                        // Fallback: Log instructions for manual DNS record creation
                        _logger.LogWarning("No challenge provider configured. Manual DNS record creation required:");

                        foreach (var (domain, challengeToken) in _state.DnsChallengeRecords)
                        {
                            // Skip the order URL entry
                            if (domain.StartsWith("_"))
                                continue;

                            var recordName = $"_acme-challenge.{domain}";
                            _logger.LogInformation("Create TXT record: {RecordName} = {Value}",
                                recordName, challengeToken);
                        }

                        // Wait for manual DNS propagation
                        _logger.LogInformation("Waiting 60 seconds for manual DNS propagation...");
                        await Task.Delay(60000);
                    }

                    // Verify DNS propagation using DNS queries
                    await VerifyDnsPropagationAsync();

                    _logger.LogInformation("DNS challenge completed for all domains");

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "DNSChallengeComplete",
                        Data = _state
                    });
                },
                _logger,
                "CompleteDNSChallenge",
                maxRetries: 3,
                initialDelayMs: 5000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete DNS challenge after retries");
            _state.Status = "DNS Challenge Failed";
            _state.ErrorMessage = ex.Message;

            // Clean up deployed challenges on failure
            await CleanupDeployedChallenges(challengesDeployed);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DNSChallengeFailed",
                Data = new { Error = ex.Message }
            });
        }
    }

    /// <summary>
    /// Cleans up DNS challenge records after validation.
    /// Should be called after certificate is issued or on failure.
    /// </summary>
    [KernelFunction("CleanupDNSChallenge")]
    public async Task CleanupDNSChallengeAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Cleaning up DNS challenges for {Count} domains",
            _state.DnsChallengeRecords.Count);

        var challengesToCleanup = new List<(string domain, string challengeToken)>();

        foreach (var (domain, challengeToken) in _state.DnsChallengeRecords)
        {
            // Skip the order URL entry and other metadata
            if (!domain.StartsWith("_") && !domain.Contains("_token") && !domain.Contains("_challengeType"))
            {
                challengesToCleanup.Add((domain, challengeToken));
            }
        }

        await CleanupDeployedChallenges(challengesToCleanup);

        _logger.LogInformation("DNS challenge cleanup completed");

        await context.EmitEventAsync(new KernelProcessEvent
        {
            Id = "DNSChallengeCleanedUp",
            Data = _state
        });
    }

    private async Task CleanupDeployedChallenges(List<(string domain, string challengeToken)> challenges)
    {
        if (_challengeProvider == null || !challenges.Any())
        {
            _logger.LogInformation("No challenges to clean up or no challenge provider configured");
            return;
        }

        _logger.LogInformation("Cleaning up {Count} DNS challenge records", challenges.Count);

        foreach (var (domain, challengeToken) in challenges)
        {
            try
            {
                // Get token and challenge type from state
                var token = _state.DnsChallengeRecords.GetValueOrDefault($"{domain}_token", "");
                var challengeType = _state.DnsChallengeRecords.GetValueOrDefault($"{domain}_challengeType", _state.ValidationMethod);

                await _challengeProvider.CleanupChallengeAsync(
                    domain,
                    token,
                    challengeToken,
                    challengeType,
                    CancellationToken.None);

                _logger.LogInformation("Cleaned up {ChallengeType} challenge for {Domain}", challengeType, domain);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup {ChallengeType} challenge for {Domain}",
                    _state.DnsChallengeRecords.GetValueOrDefault($"{domain}_challengeType", _state.ValidationMethod), domain);
            }
        }
    }

    private async Task VerifyDnsPropagationAsync()
    {
        _logger.LogInformation("Verifying DNS propagation for challenge records");

        var lookup = new LookupClient();
        var maxRetries = 10;
        var retryDelay = TimeSpan.FromSeconds(10);

        foreach (var (domain, expectedValue) in _state.DnsChallengeRecords)
        {
            // Skip the order URL entry
            if (domain.StartsWith("_"))
                continue;

            var recordName = $"_acme-challenge.{domain}";
            var verified = false;

            // Retry verification for each domain
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Querying DNS for {RecordName} (attempt {Attempt}/{MaxRetries})", recordName, attempt + 1, maxRetries);

                    var result = await lookup.QueryAsync(recordName, QueryType.TXT);
                    var txtRecords = result.Answers.TxtRecords().ToList();

                    if (txtRecords.Any(r => r.Text.Any(t => t == expectedValue)))
                    {
                        _logger.LogInformation("DNS propagation verified for {Domain}", domain);
                        verified = true;
                        break;
                    }
                    else
                    {
                        _logger.LogWarning("DNS record not yet propagated for {Domain}. Expected: {Expected}, Found: {Found}",
                            domain, expectedValue, string.Join(", ", txtRecords.SelectMany(r => r.Text)));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DNS query failed for {RecordName} (attempt {Attempt}/{MaxRetries})", recordName, attempt + 1, maxRetries);
                }

                // Wait before retrying (except on last attempt)
                if (attempt < maxRetries - 1)
                {
                    _logger.LogInformation("Waiting {Seconds} seconds before retry...", retryDelay.TotalSeconds);
                    await Task.Delay(retryDelay);
                }
            }

            if (!verified)
            {
                var errorMsg = $"DNS record verification failed for {domain}. The _acme-challenge TXT record is not resolvable after {maxRetries} attempts. " +
                               "ACME validation will fail without proper DNS records. Please verify DNS provider configuration and record creation.";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }
        }

        _logger.LogInformation("All DNS challenge records verified successfully");
    }
}
