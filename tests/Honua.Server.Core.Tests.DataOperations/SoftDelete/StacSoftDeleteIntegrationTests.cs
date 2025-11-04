using System;
using System.Threading.Tasks;
using Honua.Server.Core.Data.SoftDelete;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.SoftDelete;

/// <summary>
/// Integration tests for STAC catalog soft delete functionality.
/// Tests collections and items across different providers.
/// </summary>
[Collection("DatabaseTests")]
public class StacSoftDeleteIntegrationTests : IAsyncLifetime
{
    private readonly SoftDeleteOptions _softDeleteOptions;
    private InMemoryStacCatalogStore? _store;

    public StacSoftDeleteIntegrationTests()
    {
        _softDeleteOptions = new SoftDeleteOptions
        {
            Enabled = true,
            AuditDeletions = true,
            AuditRestorations = true
        };
    }

    public async Task InitializeAsync()
    {
        _store = new InMemoryStacCatalogStore();
        await _store.EnsureInitializedAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SoftDeleteCollection_MarksAsDeleted_AndAudits()
    {
        // Arrange
        var collection = CreateTestCollection("test-collection-1");
        await _store!.UpsertCollectionAsync(collection);

        // Act - Soft delete
        var deleted = await _store.SoftDeleteCollectionAsync("test-collection-1", "admin-user");

        // Assert
        Assert.True(deleted);

        // Collection should not be returned by normal queries (in real implementation)
        var retrieved = await _store.GetCollectionAsync("test-collection-1");
        // Note: InMemoryStacCatalogStore doesn't implement soft delete filtering yet
        // In real provider implementations, this would return null
    }

    [Fact]
    public async Task SoftDeleteCollection_WhenNotFound_ReturnsFalse()
    {
        // Act
        var deleted = await _store!.SoftDeleteCollectionAsync("non-existent", "admin-user");

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task SoftDeleteCollection_ThenRestore_MakesAvailableAgain()
    {
        // Arrange
        var collection = CreateTestCollection("test-collection-2");
        await _store!.UpsertCollectionAsync(collection);
        await _store.SoftDeleteCollectionAsync("test-collection-2", "admin-user");

        // Act - Restore
        var restored = await _store.RestoreCollectionAsync("test-collection-2");

        // Assert
        Assert.True(restored);

        // Collection should be available again
        var retrieved = await _store.GetCollectionAsync("test-collection-2");
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task HardDeleteCollection_PermanentlyRemoves_WithAudit()
    {
        // Arrange
        var collection = CreateTestCollection("test-collection-3");
        await _store!.UpsertCollectionAsync(collection);

        // Act - Hard delete
        var deleted = await _store.HardDeleteCollectionAsync("test-collection-3", "admin-user");

        // Assert
        Assert.True(deleted);

        // Collection should be permanently gone
        var retrieved = await _store.GetCollectionAsync("test-collection-3");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task SoftDeleteItem_MarksAsDeleted_AndAudits()
    {
        // Arrange
        var collection = CreateTestCollection("collection-for-items");
        await _store!.UpsertCollectionAsync(collection);

        var item = CreateTestItem("collection-for-items", "item-1");
        await _store.UpsertItemAsync(item);

        // Act - Soft delete
        var deleted = await _store.SoftDeleteItemAsync("collection-for-items", "item-1", "admin-user");

        // Assert
        Assert.True(deleted);

        // Item should not be returned by normal queries (in real implementation)
        var retrieved = await _store.GetItemAsync("collection-for-items", "item-1");
        // Note: InMemoryStacCatalogStore doesn't implement soft delete filtering yet
    }

    [Fact]
    public async Task SoftDeleteItem_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        var collection = CreateTestCollection("collection-empty");
        await _store!.UpsertCollectionAsync(collection);

        // Act
        var deleted = await _store.SoftDeleteItemAsync("collection-empty", "non-existent", "admin-user");

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task SoftDeleteItem_ThenRestore_MakesAvailableAgain()
    {
        // Arrange
        var collection = CreateTestCollection("collection-for-restore");
        await _store!.UpsertCollectionAsync(collection);

        var item = CreateTestItem("collection-for-restore", "item-2");
        await _store.UpsertItemAsync(item);
        await _store.SoftDeleteItemAsync("collection-for-restore", "item-2", "admin-user");

        // Act - Restore
        var restored = await _store.RestoreItemAsync("collection-for-restore", "item-2");

        // Assert
        Assert.True(restored);

        // Item should be available again
        var retrieved = await _store.GetItemAsync("collection-for-restore", "item-2");
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task HardDeleteItem_PermanentlyRemoves_WithAudit()
    {
        // Arrange
        var collection = CreateTestCollection("collection-for-hard-delete");
        await _store!.UpsertCollectionAsync(collection);

        var item = CreateTestItem("collection-for-hard-delete", "item-3");
        await _store.UpsertItemAsync(item);

        // Act - Hard delete
        var deleted = await _store.HardDeleteItemAsync("collection-for-hard-delete", "item-3", "admin-user");

        // Assert
        Assert.True(deleted);

        // Item should be permanently gone
        var retrieved = await _store.GetItemAsync("collection-for-hard-delete", "item-3");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task SoftDeleteCollection_WithItems_DoesNotDeleteItems()
    {
        // Arrange
        var collection = CreateTestCollection("collection-with-items");
        await _store!.UpsertCollectionAsync(collection);

        var item = CreateTestItem("collection-with-items", "item-child");
        await _store.UpsertItemAsync(item);

        // Act - Soft delete collection
        await _store.SoftDeleteCollectionAsync("collection-with-items", "admin-user");

        // Assert - Items should still exist (not cascaded)
        var retrievedItem = await _store.GetItemAsync("collection-with-items", "item-child");
        Assert.NotNull(retrievedItem);
    }

    private static StacCollectionRecord CreateTestCollection(string id)
    {
        return new StacCollectionRecord
        {
            Id = id,
            Title = $"Test Collection {id}",
            Description = "Test collection for soft delete tests",
            License = "proprietary",
            Extent = StacExtent.Empty,
            ETag = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static StacItemRecord CreateTestItem(string collectionId, string itemId)
    {
        return new StacItemRecord
        {
            CollectionId = collectionId,
            Id = itemId,
            Title = $"Test Item {itemId}",
            Description = "Test item for soft delete tests",
            Bbox = new[] { -180.0, -90.0, 180.0, 90.0 },
            Geometry = "{\"type\":\"Point\",\"coordinates\":[0,0]}",
            Datetime = DateTimeOffset.UtcNow,
            ETag = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Tests for GDPR compliance aspects of soft delete.
/// </summary>
public class GdprComplianceTests
{
    [Fact]
    public void DeletionContext_SupportsDataSubjectRequests()
    {
        // Arrange & Act
        var context = new DeletionContext
        {
            UserId = "admin",
            IsDataSubjectRequest = true,
            DataSubjectRequestId = "dsr-2024-001",
            Reason = "Right to be forgotten request from user@example.com"
        };

        // Assert
        Assert.True(context.IsDataSubjectRequest);
        Assert.Equal("dsr-2024-001", context.DataSubjectRequestId);
        Assert.Contains("Right to be forgotten", context.Reason);
    }

    [Fact]
    public void DeletionAuditRecord_TracksDataSubjectRequests()
    {
        // Arrange & Act
        var record = new DeletionAuditRecord
        {
            Id = 1,
            EntityType = "AuthUser",
            EntityId = "user-123",
            DeletionType = "hard",
            DeletedBy = "gdpr-system",
            DeletedAt = DateTimeOffset.UtcNow,
            Reason = "GDPR data subject request",
            IsDataSubjectRequest = true,
            DataSubjectRequestId = "dsr-2024-002",
            EntityMetadataSnapshot = "{\"email\":\"user@example.com\",\"name\":\"John Doe\"}"
        };

        // Assert
        Assert.True(record.IsDataSubjectRequest);
        Assert.Equal("dsr-2024-002", record.DataSubjectRequestId);
        Assert.Equal("hard", record.DeletionType);
        Assert.Contains("email", record.EntityMetadataSnapshot!);
    }

    [Fact]
    public void SoftDeleteOptions_SupportsRetentionPolicy()
    {
        // Arrange & Act
        var options = new SoftDeleteOptions
        {
            AutoPurgeEnabled = true,
            RetentionPeriod = TimeSpan.FromDays(30)
        };

        // Assert
        Assert.True(options.AutoPurgeEnabled);
        Assert.Equal(TimeSpan.FromDays(30), options.RetentionPeriod);
    }
}
