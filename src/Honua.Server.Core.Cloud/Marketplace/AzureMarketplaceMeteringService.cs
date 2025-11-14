using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Cloud.Marketplace;

/// <summary>
/// Azure Marketplace metering service for usage-based billing
/// </summary>
public class AzureMarketplaceMeteringService : IMarketplaceMeteringService, IHostedService
{
    private readonly ILogger<AzureMarketplaceMeteringService> _logger;
    private readonly MarketplaceOptions _options;
    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private const string MeteringApiUrl = "https://marketplaceapi.microsoft.com/api/usageEvent";
    private const string BatchMeteringApiUrl = "https://marketplaceapi.microsoft.com/api/batchUsageEvent";
    private const string ApiVersion = "2018-08-31";

    public AzureMarketplaceMeteringService(
        ILogger<AzureMarketplaceMeteringService> logger,
        IOptions<MarketplaceOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient("AzureMarketplace");

        // Use Managed Identity for authentication
        _credential = new DefaultAzureCredential();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrEmpty(_options.AzureOfferId))
        {
            _logger.LogInformation("Azure Marketplace metering is disabled");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Azure Marketplace metering service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Azure Marketplace metering service stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get access token for Azure Marketplace API
    /// </summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokenRequestContext = new TokenRequestContext(
            new[] { "20e940b3-4c77-4b0b-9a53-9e16a1b010a7/.default" }); // Azure Marketplace resource ID

        var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
        return token.Token;
    }

    /// <summary>
    /// Report usage for a specific dimension
    /// </summary>
    public async Task<bool> ReportUsageAsync(
        string dimension,
        int quantity,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Azure Marketplace metering is disabled");
            return false;
        }

        try
        {
            var resourceId = Environment.GetEnvironmentVariable("AZURE_RESOURCE_ID");
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogWarning("AZURE_RESOURCE_ID environment variable not set");
                return false;
            }

            var usageEvent = new
            {
                resourceId = resourceId,
                quantity = (double)quantity,
                dimension = dimension,
                effectiveStartTime = timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                planId = _options.AzurePlanId
            };

            var accessToken = await GetAccessTokenAsync(cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{MeteringApiUrl}?api-version={ApiVersion}")
            {
                Content = JsonContent.Create(usageEvent)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("x-ms-requestid", Guid.NewGuid().ToString());
            request.Headers.Add("x-ms-correlationid", Guid.NewGuid().ToString());

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UsageEventResponse>(cancellationToken);

                _logger.LogInformation(
                    "Successfully reported usage to Azure Marketplace. " +
                    "Dimension: {Dimension}, Quantity: {Quantity}, Status: {Status}, UsageEventId: {UsageEventId}",
                    dimension, quantity, result?.Status, result?.UsageEventId);

                return result?.Status == "Accepted";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to report usage to Azure Marketplace. " +
                    "StatusCode: {StatusCode}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception while reporting usage to Azure Marketplace. Dimension: {Dimension}, Quantity: {Quantity}",
                dimension, quantity);
            return false;
        }
    }

    /// <summary>
    /// Report usage with multi-tenant allocation (not directly supported by Azure, so we report per tenant)
    /// </summary>
    public async Task<bool> ReportUsageWithAllocationsAsync(
        string dimension,
        Dictionary<string, int> allocations,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Azure Marketplace metering is disabled");
            return false;
        }

        // Azure Marketplace doesn't support allocations in the same way as AWS
        // We'll report the total usage
        var totalQuantity = allocations.Values.Sum();

        _logger.LogInformation(
            "Reporting aggregated usage for Azure Marketplace. " +
            "Dimension: {Dimension}, Tenants: {TenantCount}, Total: {Total}",
            dimension, allocations.Count, totalQuantity);

        return await ReportUsageAsync(dimension, totalQuantity, timestamp, cancellationToken);
    }

    /// <summary>
    /// Batch report multiple usage records
    /// </summary>
    public async Task<int> BatchReportUsageAsync(
        IEnumerable<UsageRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Azure Marketplace metering is disabled");
            return 0;
        }

        try
        {
            var resourceId = Environment.GetEnvironmentVariable("AZURE_RESOURCE_ID");
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogWarning("AZURE_RESOURCE_ID environment variable not set");
                return 0;
            }

            var usageEvents = records.Select(r => new
            {
                resourceId = resourceId,
                quantity = (double)r.Quantity,
                dimension = r.Dimension,
                effectiveStartTime = r.Timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                planId = _options.AzurePlanId
            }).ToList();

            var batchRequest = new
            {
                request = usageEvents
            };

            var accessToken = await GetAccessTokenAsync(cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{BatchMeteringApiUrl}?api-version={ApiVersion}")
            {
                Content = JsonContent.Create(batchRequest)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("x-ms-requestid", Guid.NewGuid().ToString());
            request.Headers.Add("x-ms-correlationid", Guid.NewGuid().ToString());

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BatchUsageEventResponse>(cancellationToken);

                var acceptedCount = result?.Result?.Count(r => r.Status == "Accepted") ?? 0;
                var rejectedCount = result?.Result?.Count(r => r.Status != "Accepted") ?? 0;

                _logger.LogInformation(
                    "Batch reported usage to Azure Marketplace. Accepted: {Accepted}, Rejected: {Rejected}",
                    acceptedCount, rejectedCount);

                if (rejectedCount > 0)
                {
                    foreach (var rejected in result?.Result?.Where(r => r.Status != "Accepted") ?? [])
                    {
                        _logger.LogWarning(
                            "Usage event rejected. UsageEventId: {UsageEventId}, Status: {Status}, Error: {Error}",
                            rejected.UsageEventId, rejected.Status, rejected.Error?.Message);
                    }
                }

                return acceptedCount;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to batch report usage to Azure Marketplace. StatusCode: {StatusCode}, Error: {Error}",
                    response.StatusCode, error);
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while batch reporting usage to Azure Marketplace");
            return 0;
        }
    }

    public bool IsRegistered => _options.Enabled && !string.IsNullOrEmpty(_options.AzureOfferId);
}

/// <summary>
/// Response from Azure Marketplace usage event API
/// </summary>
internal class UsageEventResponse
{
    public string? UsageEventId { get; set; }
    public string? Status { get; set; }
    public string? MessageTime { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceUri { get; set; }
    public double? Quantity { get; set; }
    public string? Dimension { get; set; }
    public string? EffectiveStartTime { get; set; }
    public string? PlanId { get; set; }
    public UsageEventError? Error { get; set; }
}

/// <summary>
/// Response from Azure Marketplace batch usage event API
/// </summary>
internal class BatchUsageEventResponse
{
    public List<UsageEventResponse>? Result { get; set; }
    public int? Count { get; set; }
}

/// <summary>
/// Error details from Azure Marketplace API
/// </summary>
internal class UsageEventError
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? Target { get; set; }
    public List<UsageEventError>? Details { get; set; }
}
