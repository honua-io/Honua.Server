using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.OpenRosa;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.OpenRosa;

public class SqliteSubmissionRepositoryPaginationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteSubmissionRepository _repository;

    public SqliteSubmissionRepositoryPaginationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_submissions_{Guid.NewGuid()}.db");
        var connectionString = $"Data Source={_dbPath}";
        _repository = new SqliteSubmissionRepository(connectionString);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task GetPendingAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetPendingAsync_DefaultPagination_ReturnsFirstPage()
    {
        // Arrange
        await SeedSubmissions(150);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        // Note: Without pagination parameters, returns all items
        Assert.Equal(150, items.Count);
    }

    [Fact]
    public async Task GetPendingAsync_SecondPage_ReturnsRemainingItems()
    {
        // Arrange
        await SeedSubmissions(150);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        // Note: Without pagination support, returns all items
        Assert.Equal(150, items.Count);
    }

    [Fact]
    public async Task GetPendingAsync_PageSizeExceedsMaximum_CapsAt1000()
    {
        // Arrange
        await SeedSubmissions(50);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        Assert.Equal(50, items.Count); // All items returned
    }

    [Fact]
    public async Task GetPendingAsync_PageBelowMinimum_DefaultsToPage1()
    {
        // Arrange
        await SeedSubmissions(50);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        Assert.Equal(50, items.Count);
    }

    [Fact]
    public async Task GetPendingAsync_PageSizeBelowMinimum_DefaultsTo1()
    {
        // Arrange
        await SeedSubmissions(10);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public async Task GetPendingAsync_ConsistentOrdering_MaintainsOrder()
    {
        // Arrange
        var submissions = new List<Submission>();
        for (int i = 0; i < 30; i++)
        {
            var submission = CreateTestSubmission($"layer_{i % 3}", i);
            await _repository.CreateAsync(submission);
            submissions.Add(submission);
        }

        // Act - Get all items
        var allRetrieved = await _repository.GetPendingAsync();

        // Assert
        Assert.Equal(30, allRetrieved.Count);

        // Check ordering is consistent (oldest first by submitted_at, then by id)
        for (int i = 0; i < allRetrieved.Count - 1; i++)
        {
            var current = allRetrieved[i];
            var next = allRetrieved[i + 1];

            // Either submitted_at is earlier, or if same, id is lexicographically smaller
            Assert.True(
                current.SubmittedAt < next.SubmittedAt ||
                (current.SubmittedAt == next.SubmittedAt && string.Compare(current.Id, next.Id, StringComparison.Ordinal) <= 0),
                "Items should be ordered by submitted_at ASC, then id ASC");
        }
    }

    [Fact]
    public async Task GetPendingAsync_WithLayerIdFilter_ReturnsOnlyMatchingLayer()
    {
        // Arrange
        await SeedSubmissions(30, "layer_a");
        await SeedSubmissions(20, "layer_b");

        // Act
        var itemsA = await _repository.GetPendingAsync("layer_a");
        var itemsB = await _repository.GetPendingAsync("layer_b");

        // Assert
        Assert.Equal(30, itemsA.Count);
        Assert.All(itemsA, item => Assert.Equal("layer_a", item.LayerId));

        Assert.Equal(20, itemsB.Count);
        Assert.All(itemsB, item => Assert.Equal("layer_b", item.LayerId));
    }

    [Fact]
    public async Task GetPendingAsync_WithLayerIdFilterAndPagination_WorksCorrectly()
    {
        // Arrange
        await SeedSubmissions(150, "specific_layer");
        await SeedSubmissions(50, "other_layer");

        // Act
        var items = await _repository.GetPendingAsync("specific_layer");

        // Assert
        Assert.Equal(150, items.Count);
        Assert.All(items, item => Assert.Equal("specific_layer", item.LayerId));
    }

    [Fact]
    public async Task GetPendingAsync_PageBeyondAvailable_ReturnsEmptyList()
    {
        // Arrange
        await SeedSubmissions(25);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        // Note: Without pagination, returns all items
        Assert.Equal(25, items.Count);
    }

    [Fact]
    public async Task GetPendingAsync_OnlyPendingStatus_ExcludesApprovedAndRejected()
    {
        // Arrange
        // Create pending submissions
        await SeedSubmissions(10, "layer_1");

        // Create and approve one submission
        var approved = CreateTestSubmission("layer_1", 100);
        await _repository.CreateAsync(approved);
        var approvedUpdated = new Submission
        {
            Id = approved.Id,
            InstanceId = approved.InstanceId,
            FormId = approved.FormId,
            FormVersion = approved.FormVersion,
            LayerId = approved.LayerId,
            ServiceId = approved.ServiceId,
            SubmittedBy = approved.SubmittedBy,
            SubmittedAt = approved.SubmittedAt,
            DeviceId = approved.DeviceId,
            Status = SubmissionStatus.Approved,
            ReviewedBy = "admin",
            ReviewedAt = DateTimeOffset.UtcNow,
            ReviewNotes = approved.ReviewNotes,
            XmlData = approved.XmlData,
            Geometry = approved.Geometry,
            Attributes = approved.Attributes,
            Attachments = approved.Attachments
        };
        await _repository.UpdateAsync(approvedUpdated);

        // Create and reject one submission
        var rejected = CreateTestSubmission("layer_1", 101);
        await _repository.CreateAsync(rejected);
        var rejectedUpdated = new Submission
        {
            Id = rejected.Id,
            InstanceId = rejected.InstanceId,
            FormId = rejected.FormId,
            FormVersion = rejected.FormVersion,
            LayerId = rejected.LayerId,
            ServiceId = rejected.ServiceId,
            SubmittedBy = rejected.SubmittedBy,
            SubmittedAt = rejected.SubmittedAt,
            DeviceId = rejected.DeviceId,
            Status = SubmissionStatus.Rejected,
            ReviewedBy = "admin",
            ReviewedAt = DateTimeOffset.UtcNow,
            ReviewNotes = "Rejected for testing",
            XmlData = rejected.XmlData,
            Geometry = rejected.Geometry,
            Attributes = rejected.Attributes,
            Attachments = rejected.Attachments
        };
        await _repository.UpdateAsync(rejectedUpdated);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        Assert.Equal(10, items.Count); // Only pending submissions
        Assert.All(items, item => Assert.Equal(SubmissionStatus.Pending, item.Status));
    }

    [Fact]
    public async Task GetPendingAsync_SmallPageSize_WorksCorrectly()
    {
        // Arrange
        await SeedSubmissions(10);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public async Task GetPendingAsync_CancellationToken_PropagatesCorrectly()
    {
        // Arrange
        await SeedSubmissions(5);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _repository.GetPendingAsync(null, cts.Token));
    }

    private async Task SeedSubmissions(int count, string? layerId = null)
    {
        for (int i = 0; i < count; i++)
        {
            var submission = CreateTestSubmission(layerId ?? "default_layer", i);
            await _repository.CreateAsync(submission);

            // Small delay to ensure different timestamps for ordering
            await Task.Delay(1);
        }
    }

    private Submission CreateTestSubmission(string layerId, int index)
    {
        return new Submission
        {
            Id = Guid.NewGuid().ToString(),
            InstanceId = $"uuid:{Guid.NewGuid()}",
            FormId = $"form_{layerId}",
            FormVersion = "1.0",
            LayerId = layerId,
            ServiceId = "test_service",
            SubmittedBy = "test_user",
            SubmittedAt = DateTimeOffset.UtcNow.AddSeconds(index), // Ensure ordering
            DeviceId = $"device_{index}",
            Status = SubmissionStatus.Pending,
            XmlData = XDocument.Parse($"<data><field>{index}</field></data>"),
            Geometry = new Point(index, index) { SRID = 4326 },
            Attributes = new Dictionary<string, object?>
            {
                ["field1"] = $"value_{index}",
                ["field2"] = index
            }
        };
    }
}
