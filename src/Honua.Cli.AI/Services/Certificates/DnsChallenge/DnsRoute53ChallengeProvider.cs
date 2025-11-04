// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Route53;
using Amazon.Route53.Model;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Certificates.DnsChallenge;

/// <summary>
/// AWS Route53 DNS challenge provider for ACME DNS-01 validation.
/// </summary>
public sealed class DnsRoute53ChallengeProvider : IChallengeProvider
{
    private readonly IAmazonRoute53 _route53Client;
    private readonly ILogger<DnsRoute53ChallengeProvider> _logger;
    private readonly string _hostedZoneId;

    public DnsRoute53ChallengeProvider(
        IAmazonRoute53 route53Client,
        string hostedZoneId,
        ILogger<DnsRoute53ChallengeProvider> logger)
    {
        _route53Client = route53Client ?? throw new ArgumentNullException(nameof(route53Client));
        _hostedZoneId = hostedZoneId ?? throw new ArgumentNullException(nameof(hostedZoneId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DeployChallengeAsync(
        string domain,
        string token,
        string keyAuthz,
        string challengeType,
        CancellationToken cancellationToken)
    {
        if (challengeType != "Dns01")
        {
            throw new ArgumentException("This provider only supports DNS-01 challenges", nameof(challengeType));
        }

        _logger.LogInformation("Deploying DNS-01 challenge for domain {Domain}", domain);

        var recordName = $"_acme-challenge.{domain}";
        var recordValue = $"\"{keyAuthz}\"";

        var changeRequest = new ChangeResourceRecordSetsRequest
        {
            HostedZoneId = _hostedZoneId,
            ChangeBatch = new ChangeBatch
            {
                Changes = new System.Collections.Generic.List<Change>
                {
                    new Change
                    {
                        Action = ChangeAction.UPSERT,
                        ResourceRecordSet = new ResourceRecordSet
                        {
                            Name = recordName,
                            Type = RRType.TXT,
                            TTL = 60,
                            ResourceRecords = new System.Collections.Generic.List<ResourceRecord>
                            {
                                new ResourceRecord { Value = recordValue }
                            }
                        }
                    }
                }
            }
        };

        var response = await _route53Client.ChangeResourceRecordSetsAsync(changeRequest, cancellationToken);
        _logger.LogInformation("DNS record created: {ChangeId}, waiting for propagation...", response.ChangeInfo.Id);

        // Wait for DNS propagation
        await WaitForDnsPropagationAsync(response.ChangeInfo.Id, cancellationToken);

        // Additional wait for global DNS propagation
        _logger.LogInformation("Waiting additional 30 seconds for global DNS propagation");
        await Task.Delay(30000, cancellationToken).ConfigureAwait(false);
    }

    public async Task CleanupChallengeAsync(
        string domain,
        string token,
        string keyAuthz,
        string challengeType,
        CancellationToken cancellationToken)
    {
        if (challengeType != "Dns01")
        {
            return;
        }

        _logger.LogInformation("Cleaning up DNS-01 challenge for domain {Domain}", domain);

        var recordName = $"_acme-challenge.{domain}";
        var recordValue = $"\"{keyAuthz}\"";

        var changeRequest = new ChangeResourceRecordSetsRequest
        {
            HostedZoneId = _hostedZoneId,
            ChangeBatch = new ChangeBatch
            {
                Changes = new System.Collections.Generic.List<Change>
                {
                    new Change
                    {
                        Action = ChangeAction.DELETE,
                        ResourceRecordSet = new ResourceRecordSet
                        {
                            Name = recordName,
                            Type = RRType.TXT,
                            TTL = 60,
                            ResourceRecords = new System.Collections.Generic.List<ResourceRecord>
                            {
                                new ResourceRecord { Value = recordValue }
                            }
                        }
                    }
                }
            }
        };

        try
        {
            await _route53Client.ChangeResourceRecordSetsAsync(changeRequest, cancellationToken);
            _logger.LogInformation("DNS challenge record deleted for {Domain}", domain);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup DNS challenge record for {Domain}", domain);
        }
    }

    private async Task WaitForDnsPropagationAsync(string changeId, CancellationToken cancellationToken)
    {
        var request = new GetChangeRequest { Id = changeId };

        for (int i = 0; i < 30; i++)
        {
            var response = await _route53Client.GetChangeAsync(request, cancellationToken);

            if (response.ChangeInfo.Status == ChangeStatus.INSYNC)
            {
                _logger.LogInformation("DNS change propagated successfully");
                return;
            }

            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("DNS change propagation check timed out, proceeding anyway");
    }
}
