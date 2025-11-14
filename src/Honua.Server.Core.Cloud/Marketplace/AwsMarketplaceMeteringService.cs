using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.MarketplaceMetering;
using Amazon.MarketplaceMetering.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Cloud.Marketplace;

/// <summary>
/// AWS Marketplace metering service for usage-based billing
/// </summary>
public class AwsMarketplaceMeteringService : IMarketplaceMeteringService, IHostedService
{
    private readonly ILogger<AwsMarketplaceMeteringService> _logger;
    private readonly MarketplaceOptions _options;
    private readonly IAmazonMarketplaceMetering _meteringClient;
    private Timer? _registrationTimer;
    private bool _isRegistered;

    public AwsMarketplaceMeteringService(
        ILogger<AwsMarketplaceMeteringService> logger,
        IOptions<MarketplaceOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Use IAM role credentials when running in EKS with IRSA
        var credentials = new InstanceProfileAWSCredentials();
        var region = RegionEndpoint.GetBySystemName(_options.AwsRegion ?? "us-east-1");
        _meteringClient = new AmazonMarketplaceMeteringClient(credentials, region);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrEmpty(_options.AwsProductCode))
        {
            _logger.LogInformation("AWS Marketplace metering is disabled");
            return;
        }

        _logger.LogInformation("Starting AWS Marketplace metering service");

        // Register usage immediately on startup
        await RegisterUsageAsync(cancellationToken);

        // Set up periodic registration (every hour as recommended by AWS)
        _registrationTimer = new Timer(
            async _ => await RegisterUsageAsync(CancellationToken.None),
            null,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping AWS Marketplace metering service");
        _registrationTimer?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Register usage with AWS Marketplace (required for container products)
    /// </summary>
    private async Task RegisterUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new RegisterUsageRequest
            {
                ProductCode = _options.AwsProductCode,
                PublicKeyVersion = 1 // Version of the public key to use
            };

            var response = await _meteringClient.RegisterUsageAsync(request, cancellationToken);

            _isRegistered = true;
            _logger.LogInformation(
                "Successfully registered with AWS Marketplace. Signature: {Signature}",
                response.Signature);
        }
        catch (CustomerNotEntitledException ex)
        {
            _logger.LogError(ex,
                "Customer is not entitled to use this product. AWS Account may not have an active subscription.");
            _isRegistered = false;
        }
        catch (InvalidProductCodeException ex)
        {
            _logger.LogError(ex,
                "Invalid AWS Marketplace product code: {ProductCode}",
                _options.AwsProductCode);
            _isRegistered = false;
        }
        catch (ThrottlingException ex)
        {
            _logger.LogWarning(ex, "AWS Marketplace metering API throttled. Will retry on next cycle.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register usage with AWS Marketplace");
            _isRegistered = false;
        }
    }

    /// <summary>
    /// Report custom metered usage to AWS Marketplace
    /// </summary>
    public async Task<bool> ReportUsageAsync(
        string dimension,
        int quantity,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_isRegistered)
        {
            _logger.LogWarning("Cannot report usage: service not enabled or not registered");
            return false;
        }

        try
        {
            var request = new MeterUsageRequest
            {
                ProductCode = _options.AwsProductCode,
                Timestamp = timestamp,
                UsageDimension = dimension,
                UsageQuantity = quantity,
                DryRun = false,
                // Allocations can be used for multi-tenant scenarios
                UsageAllocations = new List<UsageAllocation>()
            };

            var response = await _meteringClient.MeterUsageAsync(request, cancellationToken);

            _logger.LogInformation(
                "Successfully reported usage to AWS Marketplace. " +
                "Dimension: {Dimension}, Quantity: {Quantity}, MeteringRecordId: {RecordId}",
                dimension, quantity, response.MeteringRecordId);

            return true;
        }
        catch (DuplicateRequestException ex)
        {
            _logger.LogWarning(ex,
                "Duplicate usage record. Dimension: {Dimension}, Quantity: {Quantity}",
                dimension, quantity);
            return false;
        }
        catch (ThrottlingException ex)
        {
            _logger.LogWarning(ex, "AWS Marketplace metering API throttled");
            return false;
        }
        catch (CustomerNotEntitledException ex)
        {
            _logger.LogError(ex, "Customer is not entitled to use this product");
            _isRegistered = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to report usage to AWS Marketplace. Dimension: {Dimension}, Quantity: {Quantity}",
                dimension, quantity);
            return false;
        }
    }

    /// <summary>
    /// Report usage with multi-tenant allocation tags
    /// </summary>
    public async Task<bool> ReportUsageWithAllocationsAsync(
        string dimension,
        Dictionary<string, int> allocations,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_isRegistered)
        {
            _logger.LogWarning("Cannot report usage: service not enabled or not registered");
            return false;
        }

        try
        {
            var totalQuantity = 0;
            var usageAllocations = new List<UsageAllocation>();

            foreach (var allocation in allocations)
            {
                totalQuantity += allocation.Value;
                usageAllocations.Add(new UsageAllocation
                {
                    AllocatedUsageQuantity = allocation.Value,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "TenantId", Value = allocation.Key }
                    }
                });
            }

            var request = new MeterUsageRequest
            {
                ProductCode = _options.AwsProductCode,
                Timestamp = timestamp,
                UsageDimension = dimension,
                UsageQuantity = totalQuantity,
                UsageAllocations = usageAllocations,
                DryRun = false
            };

            var response = await _meteringClient.MeterUsageAsync(request, cancellationToken);

            _logger.LogInformation(
                "Successfully reported allocated usage to AWS Marketplace. " +
                "Dimension: {Dimension}, Tenants: {TenantCount}, Total: {Total}, MeteringRecordId: {RecordId}",
                dimension, allocations.Count, totalQuantity, response.MeteringRecordId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report allocated usage to AWS Marketplace");
            return false;
        }
    }

    /// <summary>
    /// Batch report multiple usage records
    /// </summary>
    public async Task<int> BatchReportUsageAsync(
        IEnumerable<UsageRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_isRegistered)
        {
            _logger.LogWarning("Cannot report usage: service not enabled or not registered");
            return 0;
        }

        try
        {
            var usageRecords = new List<Amazon.MarketplaceMetering.Model.UsageRecord>();

            foreach (var record in records)
            {
                usageRecords.Add(new Amazon.MarketplaceMetering.Model.UsageRecord
                {
                    Timestamp = record.Timestamp,
                    CustomerIdentifier = record.CustomerId,
                    Dimension = record.Dimension,
                    Quantity = record.Quantity
                });
            }

            var request = new BatchMeterUsageRequest
            {
                ProductCode = _options.AwsProductCode,
                UsageRecords = usageRecords
            };

            var response = await _meteringClient.BatchMeterUsageAsync(request, cancellationToken);

            var successCount = response.Results.Count;
            var failureCount = response.UnprocessedRecords.Count;

            _logger.LogInformation(
                "Batch reported usage to AWS Marketplace. Success: {Success}, Failed: {Failed}",
                successCount, failureCount);

            if (failureCount > 0)
            {
                foreach (var unprocessed in response.UnprocessedRecords)
                {
                    _logger.LogWarning(
                        "Failed to process usage record. " +
                        "Dimension: {Dimension}, Quantity: {Quantity}, Error: {Error}",
                        unprocessed.UsageRecord.Dimension,
                        unprocessed.UsageRecord.Quantity,
                        unprocessed.Status);
                }
            }

            return successCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch report usage to AWS Marketplace");
            return 0;
        }
    }

    public bool IsRegistered => _isRegistered;
}

/// <summary>
/// Marketplace configuration options
/// </summary>
public class MarketplaceOptions
{
    public const string SectionName = "Marketplace";

    /// <summary>
    /// Enable marketplace metering
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// AWS Marketplace product code
    /// </summary>
    public string? AwsProductCode { get; set; }

    /// <summary>
    /// AWS region
    /// </summary>
    public string? AwsRegion { get; set; }

    /// <summary>
    /// Azure Marketplace offer ID
    /// </summary>
    public string? AzureOfferId { get; set; }

    /// <summary>
    /// Azure Marketplace plan ID
    /// </summary>
    public string? AzurePlanId { get; set; }

    /// <summary>
    /// GCP Marketplace product ID
    /// </summary>
    public string? GcpProductId { get; set; }
}

/// <summary>
/// Interface for marketplace metering services
/// </summary>
public interface IMarketplaceMeteringService
{
    /// <summary>
    /// Report usage for a specific dimension
    /// </summary>
    Task<bool> ReportUsageAsync(
        string dimension,
        int quantity,
        DateTime timestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Report usage with multi-tenant allocations
    /// </summary>
    Task<bool> ReportUsageWithAllocationsAsync(
        string dimension,
        Dictionary<string, int> allocations,
        DateTime timestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch report multiple usage records
    /// </summary>
    Task<int> BatchReportUsageAsync(
        IEnumerable<UsageRecord> records,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if service is registered with marketplace
    /// </summary>
    bool IsRegistered { get; }
}

/// <summary>
/// Usage record for batch reporting
/// </summary>
public record UsageRecord(
    string CustomerId,
    string Dimension,
    int Quantity,
    DateTime Timestamp);
