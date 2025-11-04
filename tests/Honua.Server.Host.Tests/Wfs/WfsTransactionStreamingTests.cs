using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Host.Wfs;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

/// <summary>
/// Tests for WFS Transaction streaming XML parser.
/// Verifies memory-efficient parsing of large transaction payloads.
/// </summary>
public sealed class WfsTransactionStreamingTests
{
    /// <summary>
    /// Tests parsing a small transaction with a single Insert operation.
    /// Verifies backward compatibility with legacy DOM-based approach.
    /// </summary>
    [Fact]
    public async Task ParseTransactionStream_SmallInsertTransaction_ParsesSuccessfully()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                 xmlns:fes=""http://www.opengis.net/fes/2.0""
                 xmlns:test=""http://honua.io/service/test""
                 service=""WFS"" version=""2.0.0"">
  <wfs:Insert>
    <test:feature>
      <test:id>1</test:id>
      <test:name>Test Feature</test:name>
    </test:feature>
  </wfs:Insert>
</wfs:Transaction>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
            stream, maxOperations: 100, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var (metadata, operations) = result.Value;
        Assert.Null(metadata.LockId);
        Assert.Null(metadata.ReleaseAction);

        var operationsList = await operations.ToListAsync();
        Assert.Single(operationsList);
        Assert.Equal(WfsStreamingTransactionParser.WfsTransactionOperationType.Insert, operationsList[0].Type);
    }

    /// <summary>
    /// Tests parsing a transaction with multiple operation types (Insert, Update, Delete).
    /// Verifies that all operations are parsed correctly in order.
    /// </summary>
    [Fact]
    public async Task ParseTransactionStream_MixedOperations_ParsesAllOperations()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                 xmlns:fes=""http://www.opengis.net/fes/2.0""
                 xmlns:test=""http://honua.io/service/test""
                 service=""WFS"" version=""2.0.0"">
  <wfs:Insert>
    <test:feature>
      <test:id>1</test:id>
      <test:name>New Feature</test:name>
    </test:feature>
  </wfs:Insert>
  <wfs:Update typeName=""test:feature"">
    <wfs:Property>
      <wfs:Name>name</wfs:Name>
      <wfs:Value>Updated Name</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:ResourceId rid=""test:feature.1""/>
    </fes:Filter>
  </wfs:Update>
  <wfs:Delete typeName=""test:feature"">
    <fes:Filter>
      <fes:ResourceId rid=""test:feature.2""/>
    </fes:Filter>
  </wfs:Delete>
</wfs:Transaction>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
            stream, maxOperations: 100, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var (_, operations) = result.Value;

        var operationsList = await operations.ToListAsync();
        Assert.Equal(3, operationsList.Count);
        Assert.Equal(WfsStreamingTransactionParser.WfsTransactionOperationType.Insert, operationsList[0].Type);
        Assert.Equal(WfsStreamingTransactionParser.WfsTransactionOperationType.Update, operationsList[1].Type);
        Assert.Equal(WfsStreamingTransactionParser.WfsTransactionOperationType.Delete, operationsList[2].Type);
    }

    /// <summary>
    /// Tests that transaction metadata attributes (lockId, releaseAction) are parsed correctly.
    /// </summary>
    [Fact]
    public async Task ParseTransactionStream_WithLockIdAndReleaseAction_ExtractsMetadata()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                 xmlns:test=""http://honua.io/service/test""
                 service=""WFS"" version=""2.0.0""
                 lockId=""lock-12345""
                 releaseAction=""ALL""
                 handle=""transaction-abc"">
  <wfs:Insert>
    <test:feature>
      <test:id>1</test:id>
    </test:feature>
  </wfs:Insert>
</wfs:Transaction>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
            stream, maxOperations: 100, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var (metadata, _) = result.Value;
        Assert.Equal("lock-12345", metadata.LockId);
        Assert.Equal("ALL", metadata.ReleaseAction);
        Assert.Equal("transaction-abc", metadata.Handle);
    }

    /// <summary>
    /// Tests that exceeding the maximum operation limit returns a failure.
    /// This prevents memory exhaustion from extremely large transactions.
    /// </summary>
    [Fact]
    public async Task ParseTransactionStream_ExceedsMaxOperations_ReturnsFailure()
    {
        // Arrange - Create transaction with 3 Insert operations
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                 xmlns:test=""http://honua.io/service/test""
                 service=""WFS"" version=""2.0.0"">
  <wfs:Insert>
    <test:feature><test:id>1</test:id></test:feature>
  </wfs:Insert>
  <wfs:Insert>
    <test:feature><test:id>2</test:id></test:feature>
  </wfs:Insert>
  <wfs:Insert>
    <test:feature><test:id>3</test:id></test:feature>
  </wfs:Insert>
</wfs:Transaction>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act & Assert - Set max to 2, expect failure when parsing 3rd operation
        var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
            stream, maxOperations: 2, CancellationToken.None);

        Assert.True(result.IsSuccess); // Initial parse succeeds
        var (_, operations) = result.Value;

        // The exception should be thrown when enumerating past the limit
        await Assert.ThrowsAsync<System.Xml.XmlException>(async () =>
        {
            await operations.ToListAsync();
        });
    }

    /// <summary>
    /// Tests parsing a large transaction with many features.
    /// This simulates the scenario that caused memory exhaustion in the original implementation.
    /// </summary>
    [Fact]
    public async Task ParseTransactionStream_LargeTransaction_ParsesIncrementally()
    {
        // Arrange - Create a transaction with 1000 Insert operations
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
        sb.AppendLine(@"<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""");
        sb.AppendLine(@"                 xmlns:test=""http://honua.io/service/test""");
        sb.AppendLine(@"                 service=""WFS"" version=""2.0.0"">");

        for (int i = 1; i <= 1000; i++)
        {
            sb.AppendLine("  <wfs:Insert>");
            sb.AppendLine($"    <test:feature>");
            sb.AppendLine($"      <test:id>{i}</test:id>");
            sb.AppendLine($"      <test:name>Feature {i}</test:name>");
            sb.AppendLine("    </test:feature>");
            sb.AppendLine("  </wfs:Insert>");
        }

        sb.AppendLine("</wfs:Transaction>");

        var xml = sb.ToString();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
            stream, maxOperations: 2000, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var (_, operations) = result.Value;

        var count = 0;
        await foreach (var operation in operations)
        {
            Assert.Equal(WfsStreamingTransactionParser.WfsTransactionOperationType.Insert, operation.Type);
            count++;
        }

        Assert.Equal(1000, count);
    }

    /// <summary>
    /// Tests that malformed XML returns a failure result.
    /// </summary>
    [Fact]
    public async Task ParseTransactionStream_MalformedXml_ReturnsFailure()
    {
        // Arrange - Invalid XML (unclosed tag)
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0"">
  <wfs:Insert>
    <test:feature>
      <test:id>1</test:id>
  </wfs:Insert>
</wfs:Transaction>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
            stream, maxOperations: 100, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("not valid XML", result.Error!.Message);
    }

    /// <summary>
    /// Tests that an invalid releaseAction value returns a failure.
    /// </summary>
    [Fact]
    public async Task ParseTransactionStream_InvalidReleaseAction_ReturnsFailure()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                 xmlns:test=""http://honua.io/service/test""
                 service=""WFS"" version=""2.0.0""
                 releaseAction=""INVALID"">
  <wfs:Insert>
    <test:feature><test:id>1</test:id></test:feature>
  </wfs:Insert>
</wfs:Transaction>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
            stream, maxOperations: 100, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("releaseAction", result.Error!.Message);
    }

    /// <summary>
    /// Tests that cancellation is properly handled during parsing.
    /// </summary>
    [Fact]
    public async Task ParseTransactionStream_CancellationRequested_ThrowsOperationCancelledException()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                 xmlns:test=""http://honua.io/service/test""
                 service=""WFS"" version=""2.0.0"">
  <wfs:Insert>
    <test:feature><test:id>1</test:id></test:feature>
  </wfs:Insert>
</wfs:Transaction>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
                stream, maxOperations: 100, cts.Token);

            if (result.IsSuccess)
            {
                await result.Value.Operations.ToListAsync(cts.Token);
            }
        });
    }

    /// <summary>
    /// Tests extracting features from an Insert operation element.
    /// </summary>
    [Fact]
    public void ExtractInsertFeatures_MultipleFeatures_ExtractsAll()
    {
        // Arrange
        var insertXml = @"<wfs:Insert xmlns:wfs=""http://www.opengis.net/wfs/2.0""
                                    xmlns:test=""http://honua.io/service/test"">
  <test:feature>
    <test:id>1</test:id>
    <test:name>Feature 1</test:name>
  </test:feature>
  <test:feature>
    <test:id>2</test:id>
    <test:name>Feature 2</test:name>
  </test:feature>
</wfs:Insert>";

        var insertElement = XElement.Parse(insertXml);

        // Act
        var features = WfsStreamingTransactionParser.ExtractInsertFeatures(insertElement).ToList();

        // Assert
        Assert.Equal(2, features.Count);
        Assert.Equal("feature", features[0].Name.LocalName);
        Assert.Equal("feature", features[1].Name.LocalName);
    }

    /// <summary>
    /// Tests memory usage during parsing of large transactions.
    /// This is a benchmark test to verify streaming reduces memory pressure.
    /// </summary>
    [Fact]
    public async Task ParseTransactionStream_LargeTransaction_UsesBoundedMemory()
    {
        // Arrange - Create a transaction with 5000 features
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
        sb.AppendLine(@"<wfs:Transaction xmlns:wfs=""http://www.opengis.net/wfs/2.0""");
        sb.AppendLine(@"                 xmlns:test=""http://honua.io/service/test""");
        sb.AppendLine(@"                 service=""WFS"" version=""2.0.0"">");

        for (int i = 1; i <= 5000; i++)
        {
            sb.AppendLine("  <wfs:Insert>");
            sb.AppendLine($"    <test:feature>");
            sb.AppendLine($"      <test:id>{i}</test:id>");
            sb.AppendLine($"      <test:name>Feature with a reasonably long name {i}</test:name>");
            sb.AppendLine($"      <test:description>This is a longer description field that adds more data to each feature {i}</test:description>");
            sb.AppendLine("    </test:feature>");
            sb.AppendLine("  </wfs:Insert>");
        }

        sb.AppendLine("</wfs:Transaction>");

        var xml = sb.ToString();
        var xmlSizeBytes = Encoding.UTF8.GetByteCount(xml);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Measure memory before parsing
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        // Act - Parse and enumerate operations
        var result = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
            stream, maxOperations: 10000, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var (_, operations) = result.Value;

        var count = 0;
        await foreach (var operation in operations)
        {
            count++;
            // Process each operation individually without accumulating in memory
        }

        // Measure memory after parsing
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var memoryUsed = memoryAfter - memoryBefore;

        // Assert
        Assert.Equal(5000, count);

        // Memory used should be significantly less than the full XML size
        // Allow for some overhead but streaming should keep memory under 25% of XML size
        var maxExpectedMemory = xmlSizeBytes * 0.25;

        // Note: This assertion may be flaky depending on GC behavior
        // In production, use memory profiler tools for accurate measurements
        Assert.True(memoryUsed < maxExpectedMemory,
            $"Memory usage ({memoryUsed:N0} bytes) exceeded expected limit ({maxExpectedMemory:N0} bytes) for XML size ({xmlSizeBytes:N0} bytes)");
    }
}
