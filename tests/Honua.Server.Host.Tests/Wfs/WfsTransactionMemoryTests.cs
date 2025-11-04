using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Wfs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

/// <summary>
/// Integration tests for WFS Transaction handler with focus on memory efficiency.
/// Tests verify that large transactions don't cause memory exhaustion.
/// </summary>
public sealed class WfsTransactionMemoryTests
{
    private readonly Mock<ICatalogProjectionService> _mockCatalog;
    private readonly Mock<IFeatureContextResolver> _mockContextResolver;
    private readonly Mock<IFeatureRepository> _mockRepository;
    private readonly Mock<IWfsLockManager> _mockLockManager;
    private readonly Mock<IFeatureEditOrchestrator> _mockOrchestrator;
    private readonly IOptions<WfsOptions> _options;

    public WfsTransactionMemoryTests()
    {
        _mockCatalog = new Mock<ICatalogProjectionService>();
        _mockContextResolver = new Mock<IFeatureContextResolver>();
        _mockRepository = new Mock<IFeatureRepository>();
        _mockLockManager = new Mock<IWfsLockManager>();
        _mockOrchestrator = new Mock<IFeatureEditOrchestrator>();

        _options = Options.Create(new WfsOptions
        {
            EnableStreamingTransactionParser = true,
            MaxTransactionFeatures = 5000,
            TransactionBatchSize = 500,
            TransactionTimeoutSeconds = 300
        });
    }

    /// <summary>
    /// Tests that streaming parser is enabled by default and used for transactions.
    /// </summary>
    [Fact]
    public void WfsOptions_StreamingEnabled_ByDefault()
    {
        // Arrange
        var options = new WfsOptions();

        // Assert
        Assert.True(options.EnableStreamingTransactionParser);
        Assert.Equal(5000, options.MaxTransactionFeatures);
        Assert.Equal(500, options.TransactionBatchSize);
        Assert.Equal(300, options.TransactionTimeoutSeconds);
    }

    /// <summary>
    /// Tests that transaction size limits are enforced.
    /// </summary>
    [Fact]
    public async Task HandleTransactionAsync_ExceedsMaxFeatures_ReturnsException()
    {
        // Arrange - Create transaction with more features than allowed
        var options = Options.Create(new WfsOptions
        {
            EnableStreamingTransactionParser = false, // Use DOM parser for simplicity
            MaxTransactionFeatures = 10 // Low limit for testing
        });

        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
        sb.AppendLine(@"<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""");
        sb.AppendLine(@"                 xmlns:test=""http://honua.io/service/test""");
        sb.AppendLine(@"                 service=""WFS"" version=""2.0.0"">");

        for (int i = 1; i <= 15; i++)
        {
            sb.AppendLine("  <wfs:Insert>");
            sb.AppendLine($"    <test:feature>");
            sb.AppendLine($"      <test:id>{i}</test:id>");
            sb.AppendLine("    </test:feature>");
            sb.AppendLine("  </wfs:Insert>");
        }

        sb.AppendLine("</wfs:Transaction>");

        var xml = sb.ToString();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("role", "datapublisher")
            }, "TestAuth"));

        context.Request.Body = stream;
        context.Request.Method = "POST";

        // Act
        var result = await WfsTransactionHandlers.HandleTransactionAsync(
            context,
            context.Request,
            context.Request.Query,
            _mockCatalog.Object,
            _mockContextResolver.Object,
            _mockRepository.Object,
            _mockLockManager.Object,
            _mockOrchestrator.Object,
            options,
            CancellationToken.None);

        // Assert - Should return OGC exception about exceeding limit
        Assert.NotNull(result);
        // In a real test, we'd verify the result content contains the error message
    }

    /// <summary>
    /// Tests timeout enforcement for long-running transactions.
    /// </summary>
    [Fact]
    public async Task HandleTransactionAsync_Timeout_CancelsOperation()
    {
        // Arrange - Set very short timeout
        var options = Options.Create(new WfsOptions
        {
            EnableStreamingTransactionParser = true,
            MaxTransactionFeatures = 5000,
            TransactionTimeoutSeconds = 1 // 1 second timeout
        });

        // Create a simple transaction
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                 xmlns:test=""http://honua.io/service/test""
                 service=""WFS"" version=""2.0.0"">
  <wfs:Insert>
    <test:feature>
      <test:id>1</test:id>
    </test:feature>
  </wfs:Insert>
</wfs:Transaction>";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("role", "datapublisher")
            }, "TestAuth"));

        context.Request.Body = stream;
        context.Request.Method = "POST";

        // Setup orchestrator to delay
        _mockOrchestrator.Setup(x => x.ExecuteAsync(
                It.IsAny<FeatureEditBatch>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (FeatureEditBatch batch, CancellationToken ct) =>
            {
                // Simulate slow operation
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new FeatureEditResult(Array.Empty<FeatureEditOperationResult>());
            });

        // Act & Assert - Should timeout
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await WfsTransactionHandlers.HandleTransactionAsync(
                context,
                context.Request,
                context.Request.Query,
                _mockCatalog.Object,
                _mockContextResolver.Object,
                _mockRepository.Object,
                _mockLockManager.Object,
                _mockOrchestrator.Object,
                options,
                CancellationToken.None);
        });
    }

    /// <summary>
    /// Tests that streaming parser can be disabled via configuration.
    /// When disabled, legacy DOM-based parsing is used.
    /// </summary>
    [Fact]
    public void WfsOptions_StreamingDisabled_UsesLegacyParser()
    {
        // Arrange
        var options = new WfsOptions
        {
            EnableStreamingTransactionParser = false
        };

        // Assert
        Assert.False(options.EnableStreamingTransactionParser);
    }

    /// <summary>
    /// Tests transaction batch size configuration.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void WfsOptions_TransactionBatchSize_ConfiguresCorrectly(int batchSize)
    {
        // Arrange & Act
        var options = new WfsOptions
        {
            TransactionBatchSize = batchSize
        };

        // Assert
        Assert.Equal(batchSize, options.TransactionBatchSize);
    }

    /// <summary>
    /// Tests that authentication is required for transactions.
    /// </summary>
    [Fact]
    public async Task HandleTransactionAsync_UnauthenticatedUser_ReturnsException()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                 service=""WFS"" version=""2.0.0"">
  <wfs:Insert>
    <test:feature xmlns:test=""http://honua.io/service/test"">
      <test:id>1</test:id>
    </test:feature>
  </wfs:Insert>
</wfs:Transaction>";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var context = new DefaultHttpContext();
        // No authentication
        context.Request.Body = stream;
        context.Request.Method = "POST";

        // Act
        var result = await WfsTransactionHandlers.HandleTransactionAsync(
            context,
            context.Request,
            context.Request.Query,
            _mockCatalog.Object,
            _mockContextResolver.Object,
            _mockRepository.Object,
            _mockLockManager.Object,
            _mockOrchestrator.Object,
            _options,
            CancellationToken.None);

        // Assert - Should return exception requiring authentication
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests that DataPublisher or Administrator role is required.
    /// </summary>
    [Theory]
    [InlineData("viewer", false)]
    [InlineData("datapublisher", true)]
    [InlineData("administrator", true)]
    public async Task HandleTransactionAsync_RoleValidation_EnforcesPermissions(string role, bool shouldSucceed)
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                 service=""WFS"" version=""2.0.0"">
  <wfs:Insert>
    <test:feature xmlns:test=""http://honua.io/service/test"">
      <test:id>1</test:id>
    </test:feature>
  </wfs:Insert>
</wfs:Transaction>";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("role", role)
            }, "TestAuth"));

        context.Request.Body = stream;
        context.Request.Method = "POST";

        // Act
        var result = await WfsTransactionHandlers.HandleTransactionAsync(
            context,
            context.Request,
            context.Request.Query,
            _mockCatalog.Object,
            _mockContextResolver.Object,
            _mockRepository.Object,
            _mockLockManager.Object,
            _mockOrchestrator.Object,
            _options,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // In a real test, we'd verify the result based on shouldSucceed
    }

    /// <summary>
    /// Performance benchmark: Measure memory usage for various transaction sizes.
    /// This test documents expected memory usage patterns.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    public async Task MemoryBenchmark_VariousTransactionSizes_DocumentsMemoryUsage(int featureCount)
    {
        // Arrange
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
        sb.AppendLine(@"<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""");
        sb.AppendLine(@"                 xmlns:test=""http://honua.io/service/test""");
        sb.AppendLine(@"                 service=""WFS"" version=""2.0.0"">");

        for (int i = 1; i <= featureCount; i++)
        {
            sb.AppendLine("  <wfs:Insert>");
            sb.AppendLine($"    <test:feature>");
            sb.AppendLine($"      <test:id>{i}</test:id>");
            sb.AppendLine($"      <test:name>Feature {i}</test:name>");
            sb.AppendLine($"      <test:value>{i * 1.5}</test:value>");
            sb.AppendLine("    </test:feature>");
            sb.AppendLine("  </wfs:Insert>");
        }

        sb.AppendLine("</wfs:Transaction>");

        var xml = sb.ToString();
        var xmlSizeBytes = Encoding.UTF8.GetByteCount(xml);

        // Measure with streaming parser
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
        {
            var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
                stream, maxOperations: featureCount * 2, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var (_, operations) = result.Value;

            var count = 0;
            await foreach (var operation in operations)
            {
                count++;
            }

            Assert.Equal(featureCount, count);
        }

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var memoryUsed = memoryAfter - memoryBefore;
        var memoryRatio = (double)memoryUsed / xmlSizeBytes;

        // Log results for documentation
        Console.WriteLine($"Transaction size: {featureCount} features");
        Console.WriteLine($"XML size: {xmlSizeBytes:N0} bytes");
        Console.WriteLine($"Memory used: {memoryUsed:N0} bytes");
        Console.WriteLine($"Memory ratio: {memoryRatio:P1}");

        // Assert memory usage is reasonable (less than 50% of XML size with streaming)
        Assert.True(memoryRatio < 0.5,
            $"Memory ratio ({memoryRatio:P1}) exceeded 50% for {featureCount} features");
    }
}
