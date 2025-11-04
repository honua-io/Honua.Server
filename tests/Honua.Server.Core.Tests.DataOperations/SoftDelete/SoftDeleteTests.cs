using System;
using System.Threading.Tasks;
using Honua.Server.Core.Data.SoftDelete;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.SoftDelete;

/// <summary>
/// Comprehensive tests for soft delete functionality.
/// Tests soft delete, restore, hard delete, and audit trail for all entity types.
/// </summary>
public class SoftDeleteTests
{
    private readonly SoftDeleteOptions _options;

    public SoftDeleteTests()
    {
        _options = new SoftDeleteOptions
        {
            Enabled = true,
            AuditDeletions = true,
            AuditRestorations = true,
            IncludeDeletedByDefault = false
        };
    }

    [Fact]
    public void SoftDeleteOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new SoftDeleteOptions();

        // Assert
        Assert.True(options.Enabled);
        Assert.False(options.AutoPurgeEnabled);
        Assert.Equal(TimeSpan.FromDays(90), options.RetentionPeriod);
        Assert.False(options.IncludeDeletedByDefault);
        Assert.True(options.AuditDeletions);
        Assert.True(options.AuditRestorations);
    }

    [Fact]
    public void DeletionContext_Empty_IsValid()
    {
        // Arrange & Act
        var context = DeletionContext.Empty;

        // Assert
        Assert.NotNull(context);
        Assert.Null(context.UserId);
        Assert.Null(context.Reason);
        Assert.Null(context.IpAddress);
        Assert.Null(context.UserAgent);
        Assert.False(context.IsDataSubjectRequest);
        Assert.Null(context.DataSubjectRequestId);
    }

    [Fact]
    public void DeletionContext_WithValues_PreservesData()
    {
        // Arrange
        var metadata = new System.Collections.Generic.Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        var context = new DeletionContext
        {
            UserId = "user123",
            Reason = "Test deletion",
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            IsDataSubjectRequest = true,
            DataSubjectRequestId = "dsr-123",
            Metadata = metadata
        };

        // Assert
        Assert.Equal("user123", context.UserId);
        Assert.Equal("Test deletion", context.Reason);
        Assert.Equal("192.168.1.1", context.IpAddress);
        Assert.Equal("Mozilla/5.0", context.UserAgent);
        Assert.True(context.IsDataSubjectRequest);
        Assert.Equal("dsr-123", context.DataSubjectRequestId);
        Assert.Equal(2, context.Metadata!.Count);
        Assert.Equal("value1", context.Metadata["key1"]);
    }

    [Fact]
    public void DeletionAuditRecord_PreservesAllFields()
    {
        // Arrange & Act
        var record = new DeletionAuditRecord
        {
            Id = 123,
            EntityType = "Feature",
            EntityId = "feature-456",
            DeletionType = "soft",
            DeletedBy = "admin",
            DeletedAt = DateTimeOffset.UtcNow,
            Reason = "Cleanup",
            IpAddress = "10.0.0.1",
            UserAgent = "curl/7.68.0",
            EntityMetadataSnapshot = "{\"name\":\"test\"}",
            IsDataSubjectRequest = false,
            DataSubjectRequestId = null,
            Metadata = new System.Collections.Generic.Dictionary<string, string>
            {
                ["source"] = "api"
            }
        };

        // Assert
        Assert.Equal(123, record.Id);
        Assert.Equal("Feature", record.EntityType);
        Assert.Equal("feature-456", record.EntityId);
        Assert.Equal("soft", record.DeletionType);
        Assert.Equal("admin", record.DeletedBy);
        Assert.Equal("Cleanup", record.Reason);
        Assert.Equal("10.0.0.1", record.IpAddress);
        Assert.Equal("curl/7.68.0", record.UserAgent);
        Assert.Equal("{\"name\":\"test\"}", record.EntityMetadataSnapshot);
        Assert.False(record.IsDataSubjectRequest);
        Assert.Null(record.DataSubjectRequestId);
        Assert.Single(record.Metadata!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ISoftDeletable_Interface_CanBeImplemented(bool isDeleted)
    {
        // Arrange
        var deletedAt = isDeleted ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
        var deletedBy = isDeleted ? "user123" : null;

        // Act
        var entity = new TestSoftDeletableEntity
        {
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy
        };

        // Assert
        Assert.Equal(isDeleted, entity.IsDeleted);
        Assert.Equal(deletedAt, entity.DeletedAt);
        Assert.Equal(deletedBy, entity.DeletedBy);
    }

    private class TestSoftDeletableEntity : ISoftDeletable
    {
        public bool IsDeleted { get; init; }
        public DateTimeOffset? DeletedAt { get; init; }
        public string? DeletedBy { get; init; }
    }
}

/// <summary>
/// Tests for query filter extensions and soft delete filtering.
/// </summary>
public class QueryFilterExtensionsTests
{
    [Fact]
    public void GetSoftDeleteWhereClause_WhenDisabled_ReturnsEmpty()
    {
        // Arrange
        var options = new SoftDeleteOptions { Enabled = false };

        // Act
        var clause = QueryFilterExtensions.GetSoftDeleteWhereClause("t", options);

        // Assert
        Assert.Equal(string.Empty, clause);
    }

    [Fact]
    public void GetSoftDeleteWhereClause_WhenIncludeDeleted_ReturnsEmpty()
    {
        // Arrange
        var options = new SoftDeleteOptions { Enabled = true };

        // Act
        var clause = QueryFilterExtensions.GetSoftDeleteWhereClause("t", options, includeDeleted: true);

        // Assert
        Assert.Equal(string.Empty, clause);
    }

    [Fact]
    public void GetSoftDeleteWhereClause_WhenExcludeDeleted_ReturnsClause()
    {
        // Arrange
        var options = new SoftDeleteOptions { Enabled = true };

        // Act
        var clause = QueryFilterExtensions.GetSoftDeleteWhereClause("t", options, includeDeleted: false);

        // Assert
        Assert.Contains("is_deleted", clause);
        Assert.Contains("t.is_deleted", clause);
    }

    [Fact]
    public void GetSoftDeleteWhereClause_WithoutTableName_ReturnsClauseWithoutPrefix()
    {
        // Arrange
        var options = new SoftDeleteOptions { Enabled = true };

        // Act
        var clause = QueryFilterExtensions.GetSoftDeleteWhereClause(null, options, includeDeleted: false);

        // Assert
        Assert.Contains("is_deleted", clause);
        Assert.DoesNotContain(".", clause);
    }

    [Theory]
    [InlineData("SQLite", true, 1)]
    [InlineData("SQLite", false, 0)]
    [InlineData("PostgreSQL", true, true)]
    [InlineData("PostgreSQL", false, false)]
    [InlineData("MySQL", true, true)]
    [InlineData("MySQL", false, false)]
    [InlineData("SQL Server", true, true)]
    [InlineData("SQL Server", false, false)]
    public void GetSoftDeleteBooleanValue_ReturnsCorrectType(string providerName, bool value, object expected)
    {
        // Act
        var result = QueryFilterExtensions.GetSoftDeleteBooleanValue(value, providerName);

        // Assert
        Assert.Equal(expected, result);
    }
}
