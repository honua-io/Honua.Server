// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Certificates.DnsChallenge;

/// <summary>
/// Cloudflare DNS challenge provider for ACME DNS-01 validation.
/// Uses Cloudflare API v4 to manage DNS TXT records for domain verification.
/// </summary>
public sealed class CloudflareDnsProvider : IChallengeProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudflareDnsProvider> _logger;
    private readonly CloudflareDnsProviderOptions _options;
    private const string CloudflareApiBaseUrl = "https://api.cloudflare.com/client/v4";

    public CloudflareDnsProvider(
        HttpClient httpClient,
        CloudflareDnsProviderOptions options,
        ILogger<CloudflareDnsProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_options.ApiToken.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("API token is required", nameof(options));
        }

        // Configure HttpClient with Cloudflare API authentication
        _httpClient.BaseAddress = new Uri(CloudflareApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiToken}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Honua-ACME-Client/1.0");
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

        // Get zone ID for the domain
        var zoneId = await GetZoneIdAsync(domain, cancellationToken);
        if (zoneId.IsNullOrEmpty())
        {
            throw new InvalidOperationException($"Failed to find Cloudflare zone for domain {domain}");
        }

        _logger.LogInformation("Found Cloudflare zone ID: {ZoneId}", zoneId);

        // Create TXT record for ACME challenge
        var recordName = $"_acme-challenge.{domain}";

        var createRequest = new CloudflareDnsRecordRequest
        {
            Type = "TXT",
            Name = recordName,
            Content = keyAuthz,
            Ttl = 60,
            Comment = "ACME DNS-01 challenge"
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/zones/{zoneId}/dns_records",
            createRequest,
            CliJsonOptions.Standard,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to create DNS record: {response.StatusCode} - {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<CloudflareApiResponse<CloudflareDnsRecord>>(
            CliJsonOptions.Standard,
            cancellationToken);

        if (result?.Success != true || result.Result == null)
        {
            var errors = result?.Errors != null ? string.Join(", ", result.Errors.Select(e => e.Message)) : "Unknown error";
            throw new InvalidOperationException($"Failed to create DNS record: {errors}");
        }

        _logger.LogInformation("DNS TXT record created: {RecordId}, Name: {RecordName}",
            result.Result.Id, result.Result.Name);

        // Wait for DNS propagation
        _logger.LogInformation("Waiting {Seconds} seconds for DNS propagation", _options.PropagationWaitSeconds);
        await Task.Delay(TimeSpan.FromSeconds(_options.PropagationWaitSeconds), cancellationToken);

        // Verify record propagation using Cloudflare API
        await VerifyRecordPropagationAsync(zoneId, result.Result.Id, cancellationToken);
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

        try
        {
            // Get zone ID
            var zoneId = await GetZoneIdAsync(domain, cancellationToken);
            if (zoneId.IsNullOrEmpty())
            {
                _logger.LogWarning("Could not find zone ID for cleanup of domain {Domain}", domain);
                return;
            }

            // Find the DNS record to delete
            var recordName = $"_acme-challenge.{domain}";
            var records = await ListDnsRecordsAsync(zoneId, recordName, "TXT", cancellationToken);

            foreach (var record in records.Where(r => r.Content == keyAuthz))
            {
                var response = await _httpClient.DeleteAsync(
                    $"/zones/{zoneId}/dns_records/{record.Id}",
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("DNS challenge record deleted: {RecordId}", record.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to delete DNS record {RecordId}: {StatusCode}",
                        record.Id, response.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup DNS challenge record for {Domain}", domain);
        }
    }

    /// <summary>
    /// Discovers the Cloudflare Zone ID for a given domain.
    /// Supports apex domains and subdomains by walking up the domain hierarchy.
    /// </summary>
    private async Task<string?> GetZoneIdAsync(string domain, CancellationToken cancellationToken)
    {
        // If zone ID is explicitly configured, use it
        if (_options.ZoneId.HasValue())
        {
            return _options.ZoneId;
        }

        // Try to find zone by domain name
        // Start with the full domain and work up to the root
        var domainParts = domain.Split('.');

        for (int i = 0; i < domainParts.Length - 1; i++)
        {
            var searchDomain = string.Join('.', domainParts.Skip(i));

            var response = await _httpClient.GetAsync(
                $"/zones?name={searchDomain}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            var result = await response.Content.ReadFromJsonAsync<CloudflareApiResponse<List<CloudflareZone>>>(
                CliJsonOptions.Standard,
                cancellationToken);

            if (result?.Success == true && result.Result?.Count > 0)
            {
                var zone = result.Result.FirstOrDefault();
                if (zone != null)
                {
                    _logger.LogInformation("Discovered zone {ZoneName} (ID: {ZoneId}) for domain {Domain}",
                        zone.Name, zone.Id, domain);
                    return zone.Id;
                }
            }
        }

        _logger.LogError("Could not find Cloudflare zone for domain {Domain}", domain);
        return null;
    }

    /// <summary>
    /// Lists DNS records in a zone with optional filtering.
    /// </summary>
    private async Task<List<CloudflareDnsRecord>> ListDnsRecordsAsync(
        string zoneId,
        string? name = null,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();
        if (!name.IsNullOrEmpty())
        {
            queryParams.Add($"name={Uri.EscapeDataString(name)}");
        }
        if (!type.IsNullOrEmpty())
        {
            queryParams.Add($"type={type}");
        }

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;

        var response = await _httpClient.GetAsync(
            $"/zones/{zoneId}/dns_records{query}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to list DNS records: {StatusCode}", response.StatusCode);
            return new List<CloudflareDnsRecord>();
        }

        var result = await response.Content.ReadFromJsonAsync<CloudflareApiResponse<List<CloudflareDnsRecord>>>(
            CliJsonOptions.Standard,
            cancellationToken);

        return result?.Result ?? new List<CloudflareDnsRecord>();
    }

    /// <summary>
    /// Verifies that the DNS record has propagated through Cloudflare's network.
    /// </summary>
    private async Task VerifyRecordPropagationAsync(
        string zoneId,
        string recordId,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;
        const int delaySeconds = 2;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"/zones/{zoneId}/dns_records/{recordId}",
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CloudflareApiResponse<CloudflareDnsRecord>>(
                        CliJsonOptions.Standard,
                        cancellationToken);

                    if (result?.Success == true && result.Result != null)
                    {
                        _logger.LogInformation("DNS record propagation verified (attempt {Attempt}/{MaxAttempts})",
                            attempt + 1, maxAttempts);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Record verification attempt {Attempt} failed", attempt + 1);
            }

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        _logger.LogWarning("Could not verify DNS record propagation after {Attempts} attempts", maxAttempts);
    }
}

/// <summary>
/// Configuration options for Cloudflare DNS provider.
/// </summary>
public sealed class CloudflareDnsProviderOptions
{
    /// <summary>
    /// Cloudflare API token with DNS edit permissions.
    /// Create at: https://dash.cloudflare.com/profile/api-tokens
    /// Required permissions: Zone.DNS (Edit)
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Explicit Zone ID to use. If not specified, will be auto-discovered from domain.
    /// Can be found in the Cloudflare dashboard under the domain overview.
    /// </summary>
    public string? ZoneId { get; set; }

    /// <summary>
    /// Time to wait (in seconds) for DNS propagation after creating the record.
    /// Default: 30 seconds
    /// </summary>
    public int PropagationWaitSeconds { get; set; } = 30;
}

#region Cloudflare API Models

internal sealed class CloudflareApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("errors")]
    public List<CloudflareError> Errors { get; set; } = new();

    [JsonPropertyName("messages")]
    public List<CloudflareMessage> Messages { get; set; } = new();
}

internal sealed class CloudflareError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

internal sealed class CloudflareMessage
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

internal sealed class CloudflareZone
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

internal sealed class CloudflareDnsRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("ttl")]
    public int Ttl { get; set; }

    [JsonPropertyName("proxied")]
    public bool Proxied { get; set; }
}

internal sealed class CloudflareDnsRecordRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("ttl")]
    public int Ttl { get; set; }

    [JsonPropertyName("proxied")]
    public bool Proxied { get; set; } = false;

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

#endregion
