using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.CloudCommerceProcurement.v1;
using Google.Apis.CloudCommerceProcurement.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Cloud.Marketplace;

/// <summary>
/// Google Cloud Marketplace metering service for usage-based billing
/// </summary>
public class GcpMarketplaceMeteringService : IMarketplaceMeteringService, IHostedService
{
    private readonly ILogger<GcpMarketplaceMeteringService> _logger;
    private readonly MarketplaceOptions _options;
    private readonly HttpClient _httpClient;
    private CloudCommerceProcurementService? _procurementService;
    private const string ServiceUsageApiUrl = "https://servicecontrol.googleapis.com/v1/services";

    public GcpMarketplaceMeteringService(
        ILogger<GcpMarketplaceMeteringService> logger,
        IOptions<MarketplaceOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient("GcpMarketplace");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrEmpty(_options.GcpProductId))
        {
            _logger.LogInformation("GCP Marketplace metering is disabled");
            return;
        }

        try
        {
            // Initialize GCP client with Application Default Credentials
            var credential = await GoogleCredential.GetApplicationDefaultAsync();

            _procurementService = new CloudCommerceProcurementService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Honua Server"
            });

            _logger.LogInformation("GCP Marketplace metering service started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize GCP Marketplace metering service");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GCP Marketplace metering service stopped");
        _procurementService?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Report usage for a specific dimension using Service Control API
    /// </summary>
    public async Task<bool> ReportUsageAsync(
        string dimension,
        int quantity,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _procurementService == null)
        {
            _logger.LogWarning("GCP Marketplace metering is disabled or not initialized");
            return false;
        }

        try
        {
            var consumerId = Environment.GetEnvironmentVariable("GCP_CONSUMER_ID");
            if (string.IsNullOrEmpty(consumerId))
            {
                _logger.LogWarning("GCP_CONSUMER_ID environment variable not set");
                return false;
            }

            // Use Service Control API to report usage
            var serviceName = $"honua-server-{_options.GcpProductId}.endpoints.cloudcommerceprocurement.googleapis.com";

            var reportRequest = new
            {
                operations = new[]
                {
                    new
                    {
                        operationId = Guid.NewGuid().ToString(),
                        operationName = $"services/{serviceName}/operations/report",
                        consumerId = $"project:{consumerId}",
                        startTime = timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        endTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        metricValueSets = new[]
                        {
                            new
                            {
                                metricName = $"servicecontrol.googleapis.com/service/consumer/{dimension}",
                                metricValues = new[]
                                {
                                    new
                                    {
                                        int64Value = quantity.ToString(),
                                        startTime = timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                                        endTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                                    }
                                }
                            }
                        }
                    }
                },
                serviceConfigId = _options.GcpProductId
            };

            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            var accessToken = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(
                "https://www.googleapis.com/auth/cloud-platform",
                cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{ServiceUsageApiUrl}/{serviceName}:report")
            {
                Content = JsonContent.Create(reportRequest)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully reported usage to GCP Marketplace. Dimension: {Dimension}, Quantity: {Quantity}",
                    dimension, quantity);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to report usage to GCP Marketplace. StatusCode: {StatusCode}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception while reporting usage to GCP Marketplace. Dimension: {Dimension}, Quantity: {Quantity}",
                dimension, quantity);
            return false;
        }
    }

    /// <summary>
    /// Report usage with multi-tenant allocations
    /// </summary>
    public async Task<bool> ReportUsageWithAllocationsAsync(
        string dimension,
        Dictionary<string, int> allocations,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("GCP Marketplace metering is disabled");
            return false;
        }

        // GCP Service Control API aggregates usage, so we report total
        var totalQuantity = allocations.Values.Sum();

        _logger.LogInformation(
            "Reporting aggregated usage for GCP Marketplace. " +
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
            _logger.LogWarning("GCP Marketplace metering is disabled");
            return 0;
        }

        try
        {
            var consumerId = Environment.GetEnvironmentVariable("GCP_CONSUMER_ID");
            if (string.IsNullOrEmpty(consumerId))
            {
                _logger.LogWarning("GCP_CONSUMER_ID environment variable not set");
                return 0;
            }

            var serviceName = $"honua-server-{_options.GcpProductId}.endpoints.cloudcommerceprocurement.googleapis.com";

            var operations = records.Select(r => new
            {
                operationId = Guid.NewGuid().ToString(),
                operationName = $"services/{serviceName}/operations/report",
                consumerId = $"project:{consumerId}",
                startTime = r.Timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                endTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                metricValueSets = new[]
                {
                    new
                    {
                        metricName = $"servicecontrol.googleapis.com/service/consumer/{r.Dimension}",
                        metricValues = new[]
                        {
                            new
                            {
                                int64Value = r.Quantity.ToString(),
                                startTime = r.Timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                                endTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                            }
                        }
                    }
                }
            }).ToArray();

            var reportRequest = new
            {
                operations = operations,
                serviceConfigId = _options.GcpProductId
            };

            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            var accessToken = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(
                "https://www.googleapis.com/auth/cloud-platform",
                cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{ServiceUsageApiUrl}/{serviceName}:report")
            {
                Content = JsonContent.Create(reportRequest)
            {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Batch reported usage to GCP Marketplace. Records: {RecordCount}",
                    operations.Length);
                return operations.Length;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to batch report usage to GCP Marketplace. StatusCode: {StatusCode}, Error: {Error}",
                    response.StatusCode, error);
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while batch reporting usage to GCP Marketplace");
            return 0;
        }
    }

    public bool IsRegistered => _options.Enabled &&
                               !string.IsNullOrEmpty(_options.GcpProductId) &&
                               _procurementService != null;
}
