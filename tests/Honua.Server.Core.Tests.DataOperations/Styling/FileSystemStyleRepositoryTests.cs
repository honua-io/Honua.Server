using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Styling;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Styling;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class FileSystemStyleRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly FileSystemStyleRepository _repository;

    public FileSystemStyleRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"honua-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _repository = new FileSystemStyleRepository(_tempDirectory, NullLogger<FileSystemStyleRepository>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task CreateAsync_WithValidStyle_CreatesStyleAndVersion()
    {
        // Arrange
        var style = CreateTestStyle("test-style-1");

        // Act
        var created = await _repository.CreateAsync(style, "test-user");

        // Assert
        Assert.NotNull(created);
        Assert.Equal(style.Id, created.Id);

        var exists = await _repository.ExistsAsync(style.Id);
        Assert.True(exists);

        var history = await _repository.GetVersionHistoryAsync(style.Id);
        Assert.Single(history);
        Assert.Equal(1, history[0].Version);
        Assert.Equal("test-user", history[0].CreatedBy);
    }

    [Fact]
    public async Task CreateAsync_WithExistingStyle_ThrowsException()
    {
        // Arrange
        var style = CreateTestStyle("duplicate-style");
        await _repository.CreateAsync(style);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.CreateAsync(style));
    }

    [Fact]
    public async Task GetAsync_WithExistingStyle_ReturnsStyle()
    {
        // Arrange
        var style = CreateTestStyle("get-test-style");
        await _repository.CreateAsync(style);

        // Act
        var retrieved = await _repository.GetAsync(style.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(style.Id, retrieved.Id);
        Assert.Equal(style.Title, retrieved.Title);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentStyle_ReturnsNull()
    {
        // Act
        var result = await _repository.GetAsync("non-existent-style");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleStyles_ReturnsAllStyles()
    {
        // Arrange
        var style1 = CreateTestStyle("style-1");
        var style2 = CreateTestStyle("style-2");
        var style3 = CreateTestStyle("style-3");

        await _repository.CreateAsync(style1);
        await _repository.CreateAsync(style2);
        await _repository.CreateAsync(style3);

        // Act
        var allStyles = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(3, allStyles.Count);
        Assert.Contains(allStyles, s => s.Id == "style-1");
        Assert.Contains(allStyles, s => s.Id == "style-2");
        Assert.Contains(allStyles, s => s.Id == "style-3");
    }

    [Fact]
    public async Task UpdateAsync_WithExistingStyle_UpdatesAndCreatesNewVersion()
    {
        // Arrange
        var style = CreateTestStyle("update-style");
        await _repository.CreateAsync(style, "creator");

        var updatedStyle = style with { Title = "Updated Title" };

        // Act
        var result = await _repository.UpdateAsync(style.Id, updatedStyle, "updater");

        // Assert
        Assert.Equal("Updated Title", result.Title);

        var retrieved = await _repository.GetAsync(style.Id);
        Assert.Equal("Updated Title", retrieved!.Title);

        var history = await _repository.GetVersionHistoryAsync(style.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal("updater", history[1].CreatedBy);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentStyle_CreatesStyle()
    {
        // Arrange
        var style = CreateTestStyle("new-via-update");

        // Act
        var result = await _repository.UpdateAsync(style.Id, style, "creator");

        // Assert
        Assert.NotNull(result);
        var exists = await _repository.ExistsAsync(style.Id);
        Assert.True(exists);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingStyle_DeletesAndKeepsHistory()
    {
        // Arrange
        var style = CreateTestStyle("delete-style");
        await _repository.CreateAsync(style, "creator");

        // Act
        var deleted = await _repository.DeleteAsync(style.Id, "deleter");

        // Assert
        Assert.True(deleted);

        var exists = await _repository.ExistsAsync(style.Id);
        Assert.False(exists);

        var history = await _repository.GetVersionHistoryAsync(style.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal("deleter", history[1].CreatedBy);
        Assert.Contains("Deleted", history[1].ChangeDescription!);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentStyle_ReturnsFalse()
    {
        // Act
        var deleted = await _repository.DeleteAsync("non-existent");

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_WithMultipleVersions_ReturnsOrderedHistory()
    {
        // Arrange
        var style = CreateTestStyle("versioned-style");
        await _repository.CreateAsync(style, "user1");
        await _repository.UpdateAsync(style.Id, style with { Title = "V2" }, "user2");
        await _repository.UpdateAsync(style.Id, style with { Title = "V3" }, "user3");

        // Act
        var history = await _repository.GetVersionHistoryAsync(style.Id);

        // Assert
        Assert.Equal(3, history.Count);
        Assert.Equal(1, history[0].Version);
        Assert.Equal(2, history[1].Version);
        Assert.Equal(3, history[2].Version);
        Assert.Equal("user1", history[0].CreatedBy);
        Assert.Equal("user2", history[1].CreatedBy);
        Assert.Equal("user3", history[2].CreatedBy);
    }

    [Fact]
    public async Task GetVersionAsync_WithExistingVersion_ReturnsVersion()
    {
        // Arrange
        var style = CreateTestStyle("version-test");
        await _repository.CreateAsync(style, "user1");
        await _repository.UpdateAsync(style.Id, style with { Title = "Version 2" }, "user2");

        // Act
        var version1 = await _repository.GetVersionAsync(style.Id, 1);
        var version2 = await _repository.GetVersionAsync(style.Id, 2);

        // Assert
        Assert.NotNull(version1);
        Assert.Equal(style.Title, version1.Title);

        Assert.NotNull(version2);
        Assert.Equal("Version 2", version2.Title);
    }

    [Fact]
    public async Task GetVersionAsync_WithNonExistentVersion_ReturnsNull()
    {
        // Arrange
        var style = CreateTestStyle("version-null-test");
        await _repository.CreateAsync(style);

        // Act
        var result = await _repository.GetVersionAsync(style.Id, 999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsAsync_WithExistingStyle_ReturnsTrue()
    {
        // Arrange
        var style = CreateTestStyle("exists-test");
        await _repository.CreateAsync(style);

        // Act
        var exists = await _repository.ExistsAsync(style.Id);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentStyle_ReturnsFalse()
    {
        // Act
        var exists = await _repository.ExistsAsync("non-existent");

        // Assert
        Assert.False(exists);
    }

    private static StyleDefinition CreateTestStyle(string id)
    {
        return new StyleDefinition
        {
            Id = id,
            Title = $"Test Style {id}",
            Format = "legacy",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                FillColor = "#4A90E2",
                StrokeColor = "#1F364D",
                StrokeWidth = 1.5
            }
        };
    }
}
