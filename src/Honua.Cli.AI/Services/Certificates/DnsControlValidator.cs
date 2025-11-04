// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Route53;
using Amazon.Route53.Model;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Services.Certificates.DnsChallenge;
using Honua.Cli.AI.Services.Dns;

namespace Honua.Cli.AI.Services.Certificates;

/// <summary>
/// Validates DNS control for domains by attempting to interact with DNS provider APIs.
/// Supports Cloudflare, Azure DNS, and AWS Route53.
/// </summary>
public sealed class DnsControlValidator : IDnsControlValidator
{
    private readonly ILogger<DnsControlValidator> _logger;
    private readonly CloudflareDnsProviderOptions? _cloudflareOptions;
    private readonly AzureDnsOptions? _azureOptions;
    private readonly Route53DnsOptions? _route53Options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenCredential? _azureCredential;

    public DnsControlValidator(
        ILogger<DnsControlValidator> logger,
        IHttpClientFactory httpClientFactory,
        CloudflareDnsProviderOptions? cloudflareOptions = null,
        AzureDnsOptions? azureOptions = null,
        Route53DnsOptions? route53Options = null,
        TokenCredential? azureCredential = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _cloudflareOptions = cloudflareOptions;
        _azureOptions = azureOptions;
        _route53Options = route53Options;
        _azureCredential = azureCredential;
    }

    public async Task<DnsControlValidationResult> ValidateDnsControlAsync(string domain)
    {
        if (domain.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Domain cannot be null or whitespace", nameof(domain));
        }

        _logger.LogInformation("Validating DNS control for domain: {Domain}", domain);

        // Try Cloudflare first
        if (_cloudflareOptions != null && !_cloudflareOptions.ApiToken.IsNullOrWhiteSpace())
        {
            _logger.LogDebug("Attempting DNS control validation via Cloudflare for {Domain}", domain);
            var result = await ValidateCloudflareControlAsync(domain);
            if (result.HasControl)
            {
                return result;
            }
            _logger.LogDebug("Cloudflare validation failed: {Reason}", result.FailureReason);
        }

        // Try Azure DNS
        if (_azureOptions != null && _azureCredential != null)
        {
            _logger.LogDebug("Attempting DNS control validation via Azure DNS for {Domain}", domain);
            var result = await ValidateAzureDnsControlAsync(domain);
            if (result.HasControl)
            {
                return result;
            }
            _logger.LogDebug("Azure DNS validation failed: {Reason}", result.FailureReason);
        }

        // Try Route53
        if (_route53Options != null && !_route53Options.HostedZoneId.IsNullOrWhiteSpace())
        {
            _logger.LogDebug("Attempting DNS control validation via Route53 for {Domain}", domain);
            var result = await ValidateRoute53ControlAsync(domain);
            if (result.HasControl)
            {
                return result;
            }
            _logger.LogDebug("Route53 validation failed: {Reason}", result.FailureReason);
        }

        // No provider was able to validate control
        return DnsControlValidationResult.Failure(
            "None",
            $"No DNS provider could establish control for domain: {domain}. " +
            "Configure Cloudflare, Azure DNS, or Route53 credentials.");
    }

    private async Task<DnsControlValidationResult> ValidateCloudflareControlAsync(string domain)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var cloudflareLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CloudflareDnsProvider>();
            var provider = new CloudflareDnsProvider(
                httpClient,
                _cloudflareOptions!,
                cloudflareLogger);

            // Test creating a test TXT record
            var testRecordName = $"_acme-test.{domain}";
            var testValue = $"validation-test-{Guid.NewGuid():N}";

            try
            {
                // Attempt to deploy a test challenge record
                await provider.DeployChallengeAsync(
                    domain,
                    "test-token",
                    testValue,
                    "Dns01",
                    default);

                // Clean up the test record
                await provider.CleanupChallengeAsync(
                    domain,
                    "test-token",
                    testValue,
                    "Dns01",
                    default);

                _logger.LogInformation("Successfully validated Cloudflare DNS control for domain: {Domain}", domain);
                return DnsControlValidationResult.Success("Cloudflare", _cloudflareOptions!.ZoneId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate Cloudflare DNS control for {Domain}", domain);
                return DnsControlValidationResult.Failure(
                    "Cloudflare",
                    $"Unable to create test TXT record: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while validating Cloudflare DNS control");
            return DnsControlValidationResult.Failure(
                "Cloudflare",
                $"Provider initialization failed: {ex.Message}");
        }
    }

    private async Task<DnsControlValidationResult> ValidateAzureDnsControlAsync(string domain)
    {
        try
        {
            var armClient = new ArmClient(_azureCredential);
            var azureLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<AzureDnsProvider>();
            var azureProvider = new AzureDnsProvider(azureLogger);

            // Find the zone for this domain
            var zones = await azureProvider.ListDnsZonesAsync(
                armClient,
                _azureOptions!.SubscriptionId,
                _azureOptions.ResourceGroupName);

            var matchingZone = FindMatchingZone(domain, zones);
            if (matchingZone == null)
            {
                return DnsControlValidationResult.Failure(
                    "Azure DNS",
                    $"No Azure DNS zone found for domain: {domain}");
            }

            // Test creating a test TXT record
            var testRecordName = $"_acme-test";
            var testValue = $"validation-test-{Guid.NewGuid():N}";

            try
            {
                var result = await azureProvider.UpsertTxtRecordAsync(
                    armClient,
                    _azureOptions.SubscriptionId,
                    _azureOptions.ResourceGroupName,
                    matchingZone,
                    testRecordName,
                    new System.Collections.Generic.List<string> { testValue },
                    60,
                    default);

                if (!result.Success)
                {
                    return DnsControlValidationResult.Failure(
                        "Azure DNS",
                        $"Failed to create test TXT record: {result.Message}");
                }

                // Clean up the test record
                await azureProvider.DeleteTxtRecordAsync(
                    armClient,
                    _azureOptions.SubscriptionId,
                    _azureOptions.ResourceGroupName,
                    matchingZone,
                    testRecordName,
                    default);

                _logger.LogInformation("Successfully validated Azure DNS control for domain: {Domain}", domain);
                return DnsControlValidationResult.Success("Azure DNS", matchingZone);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate Azure DNS control for {Domain}", domain);
                return DnsControlValidationResult.Failure(
                    "Azure DNS",
                    $"Unable to create test TXT record: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while validating Azure DNS control");
            return DnsControlValidationResult.Failure(
                "Azure DNS",
                $"Provider initialization failed: {ex.Message}");
        }
    }

    private async Task<DnsControlValidationResult> ValidateRoute53ControlAsync(string domain)
    {
        try
        {
            // Verify that the hosted zone ID is configured
            if (_route53Options!.HostedZoneId.IsNullOrWhiteSpace())
            {
                return DnsControlValidationResult.Failure(
                    "Route53",
                    "Route53 hosted zone ID is not configured");
            }

            using var route53Client = CreateRoute53Client();

            // Test creating a test TXT record
            var testRecordName = $"_acme-test.{domain}";
            var testValue = $"\"validation-test-{Guid.NewGuid():N}\"";

            try
            {
                // Attempt to create a test TXT record
                var createRequest = new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = _route53Options.HostedZoneId,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = new System.Collections.Generic.List<Change>
                        {
                            new Change
                            {
                                Action = ChangeAction.UPSERT,
                                ResourceRecordSet = new ResourceRecordSet
                                {
                                    Name = testRecordName,
                                    Type = RRType.TXT,
                                    TTL = 60,
                                    ResourceRecords = new System.Collections.Generic.List<ResourceRecord>
                                    {
                                        new ResourceRecord { Value = testValue }
                                    }
                                }
                            }
                        }
                    }
                };

                var createResponse = await route53Client.ChangeResourceRecordSetsAsync(createRequest);
                _logger.LogDebug("Test TXT record created in Route53. Change ID: {ChangeId}", createResponse.ChangeInfo.Id);

                // Clean up the test record
                var deleteRequest = new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = _route53Options.HostedZoneId,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = new System.Collections.Generic.List<Change>
                        {
                            new Change
                            {
                                Action = ChangeAction.DELETE,
                                ResourceRecordSet = new ResourceRecordSet
                                {
                                    Name = testRecordName,
                                    Type = RRType.TXT,
                                    TTL = 60,
                                    ResourceRecords = new System.Collections.Generic.List<ResourceRecord>
                                    {
                                        new ResourceRecord { Value = testValue }
                                    }
                                }
                            }
                        }
                    }
                };

                await route53Client.ChangeResourceRecordSetsAsync(deleteRequest);
                _logger.LogDebug("Test TXT record cleaned up from Route53");

                _logger.LogInformation(
                    "Successfully validated Route53 DNS control for domain: {Domain}, Zone ID: {ZoneId}",
                    domain,
                    _route53Options.HostedZoneId);

                return DnsControlValidationResult.Success("Route53", _route53Options.HostedZoneId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate Route53 DNS control for {Domain}", domain);
                return DnsControlValidationResult.Failure(
                    "Route53",
                    $"Unable to create test TXT record: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while validating Route53 DNS control");
            return DnsControlValidationResult.Failure(
                "Route53",
                $"Provider initialization failed: {ex.Message}");
        }
    }

    private IAmazonRoute53 CreateRoute53Client()
    {
        if (_route53Options is null)
        {
            return new AmazonRoute53Client();
        }

        if (!string.IsNullOrWhiteSpace(_route53Options.ServiceUrl))
        {
            var config = new AmazonRoute53Config
            {
                ServiceURL = _route53Options.ServiceUrl,
                AuthenticationRegion = _route53Options.Region ?? "us-east-1",
                UseHttp = _route53Options.UseHttp ?? _route53Options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            };

            if (!string.IsNullOrWhiteSpace(_route53Options.AccessKeyId) &&
                !string.IsNullOrWhiteSpace(_route53Options.SecretAccessKey))
            {
                return new AmazonRoute53Client(_route53Options.AccessKeyId, _route53Options.SecretAccessKey, config);
            }

            return new AmazonRoute53Client(config);
        }

        return new AmazonRoute53Client();
    }

    /// <summary>
    /// Finds the most specific DNS zone that matches the domain.
    /// For example, for "api.example.com", prefers "example.com" over "com".
    /// </summary>
    private string? FindMatchingZone(string domain, System.Collections.Generic.List<string> zones)
    {
        var domainParts = domain.Split('.');

        // Try to find the most specific zone match
        for (int i = 0; i < domainParts.Length - 1; i++)
        {
            var candidate = string.Join('.', domainParts.Skip(i));
            var match = zones.FirstOrDefault(z =>
                z.Equals(candidate, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}

/// <summary>
/// Configuration options for Azure DNS provider.
/// </summary>
public sealed class AzureDnsOptions
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
}

/// <summary>
/// Configuration options for Route53 DNS provider.
/// </summary>
public sealed class Route53DnsOptions
{
    public string HostedZoneId { get; set; } = string.Empty;
    public string? ServiceUrl { get; set; }
    public string? Region { get; set; }
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public bool? UseHttp { get; set; }
}
