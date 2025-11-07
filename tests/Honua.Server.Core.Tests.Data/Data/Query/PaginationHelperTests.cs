using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Query;

[Trait("Category", "Unit")]
public class PaginationHelperTests
{
    [Fact]
    public void BuildOrderByClause_WithSortOrders_BuildsCorrectClause()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var sortOrders = new List<FeatureSortOrder>
        {
            new("name", FeatureSortDirection.Ascending),
            new("age", FeatureSortDirection.Descending)
        };

        // Act
        PaginationHelper.BuildOrderByClause(sql, sortOrders, "t", name => $"\"{name}\"", "id");

        // Assert
        sql.ToString().Should().Contain("order by t.\"name\" asc, t.\"age\" desc");
    }

    [Fact]
    public void BuildOrderByClause_WithoutSortOrders_UsesDefaultColumn()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");

        // Act
        PaginationHelper.BuildOrderByClause(sql, null, "t", name => $"\"{name}\"", "id");

        // Assert
        sql.ToString().Should().Contain("order by t.\"id\" asc");
    }

    [Fact]
    public void BuildOrderByClause_WithEmptySortOrders_UsesDefaultColumn()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var sortOrders = new List<FeatureSortOrder>();

        // Act
        PaginationHelper.BuildOrderByClause(sql, sortOrders, "t", name => $"[{name}]", "pk");

        // Assert
        sql.ToString().Should().Contain("order by t.[pk] asc");
    }

    [Fact]
    public void BuildOrderByClause_WithNullSql_ThrowsArgumentNullException()
    {
        // Act
        var act = () => PaginationHelper.BuildOrderByClause(
            null!, null, "t", name => name, "id");

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("sql");
    }

    [Fact]
    public void BuildOrderByClause_WithNullQuoteIdentifier_ThrowsArgumentNullException()
    {
        // Arrange
        var sql = new StringBuilder();

        // Act
        var act = () => PaginationHelper.BuildOrderByClause(
            sql, null, "t", null!, "id");

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("quoteIdentifier");
    }

    [Fact]
    public void BuildOrderByClause_WithNullDefaultColumn_ThrowsArgumentNullException()
    {
        // Arrange
        var sql = new StringBuilder();

        // Act
        var act = () => PaginationHelper.BuildOrderByClause(
            sql, null, "t", name => name, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("defaultSortColumn");
    }

    [Fact]
    public void GetDefaultOrderByColumn_WithIdField_ReturnsIdField()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "feature_id"
        };

        // Act
        var result = PaginationHelper.GetDefaultOrderByColumn(layer);

        // Assert
        result.Should().Be("feature_id");
    }

    [Fact]
    public void GetDefaultOrderByColumn_WithoutIdField_ReturnsPrimaryKey()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "", // Empty IdField to test fallback to PrimaryKey
            Storage = new LayerStorageDefinition
            {
                PrimaryKey = "pk_column"
            }
        };

        // Act
        var result = PaginationHelper.GetDefaultOrderByColumn(layer);

        // Assert
        result.Should().Be("pk_column");
    }

    [Fact]
    public void BuildOffsetLimitClause_PostgreSQL_WithLimitAndOffset_BuildsCorrectSyntax()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, parameters, PaginationHelper.DatabaseVendor.PostgreSQL);

        // Assert
        sql.ToString().Should().Contain("limit @limit offset @offset");
        parameters["limit"].Should().Be(20);
        parameters["offset"].Should().Be(10);
    }

    [Fact]
    public void BuildOffsetLimitClause_PostgreSQL_WithOnlyLimit_BuildsCorrectSyntax()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, null, 20, parameters, PaginationHelper.DatabaseVendor.PostgreSQL);

        // Assert
        sql.ToString().Should().Contain("limit @limit");
        sql.ToString().Should().NotContain("offset");
        parameters["limit"].Should().Be(20);
    }

    [Fact]
    public void BuildOffsetLimitClause_PostgreSQL_WithOffsetOnly_UsesLimitAll()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, null, parameters, PaginationHelper.DatabaseVendor.PostgreSQL);

        // Assert
        sql.ToString().Should().Contain("limit ALL offset @offset");
        parameters["offset"].Should().Be(10);
    }

    [Fact]
    public void BuildOffsetLimitClause_MySQL_WithOffsetOnly_UsesMaxLimit()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, null, parameters, PaginationHelper.DatabaseVendor.MySQL);

        // Assert
        sql.ToString().Should().Contain("limit 18446744073709551615 offset @offset");
        parameters["offset"].Should().Be(10);
    }

    [Fact]
    public void BuildOffsetLimitClause_MySQL_WithLimitAndOffset_BuildsCorrectSyntax()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, parameters, PaginationHelper.DatabaseVendor.MySQL);

        // Assert
        sql.ToString().Should().Contain("limit @limit offset @offset");
        parameters["limit"].Should().Be(20);
        parameters["offset"].Should().Be(10);
    }

    [Fact]
    public void BuildOffsetLimitClause_SQLite_WithOffsetOnly_UsesLimitMinusOne()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, null, parameters, PaginationHelper.DatabaseVendor.SQLite);

        // Assert
        sql.ToString().Should().Contain("limit -1 offset @offset");
        parameters["offset"].Should().Be(10);
    }

    [Fact]
    public void BuildOffsetLimitClause_SQLite_WithLimitAndOffset_BuildsCorrectSyntax()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, parameters, PaginationHelper.DatabaseVendor.SQLite);

        // Assert
        sql.ToString().Should().Contain("limit @limit offset @offset");
        parameters["limit"].Should().Be(20);
        parameters["offset"].Should().Be(10);
    }

    [Fact]
    public void BuildOffsetLimitClause_SqlServer_WithLimitAndOffset_UsesFetchRows()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, parameters, PaginationHelper.DatabaseVendor.SqlServer);

        // Assert
        sql.ToString().Should().Contain("offset @offset rows fetch next @limit rows only");
        parameters["offset"].Should().Be(10);
        parameters["limit"].Should().Be(20);
    }

    [Fact]
    public void BuildOffsetLimitClause_SqlServer_WithOffsetOnly_UsesOffsetRows()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, null, parameters, PaginationHelper.DatabaseVendor.SqlServer);

        // Assert
        sql.ToString().Should().Contain("offset @offset rows");
        sql.ToString().Should().NotContain("fetch");
        parameters["offset"].Should().Be(10);
    }

    [Fact]
    public void BuildOffsetLimitClause_SqlServer_WithOnlyLimit_UsesZeroOffset()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, null, 20, parameters, PaginationHelper.DatabaseVendor.SqlServer);

        // Assert
        sql.ToString().Should().Contain("offset @offset rows fetch next @limit rows only");
        parameters["offset"].Should().Be(0);
        parameters["limit"].Should().Be(20);
    }

    [Fact]
    public void BuildOffsetLimitClause_Oracle_WithLimitAndOffset_UsesFetchRows()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, parameters, PaginationHelper.DatabaseVendor.Oracle, ":");

        // Assert
        sql.ToString().Should().Contain("offset :offset rows fetch next :limit rows only");
        parameters["offset"].Should().Be(10);
        parameters["limit"].Should().Be(20);
    }

    [Fact]
    public void BuildOffsetLimitClause_Snowflake_WithLimitAndOffset_UsesStandardSyntax()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, parameters, PaginationHelper.DatabaseVendor.Snowflake, ":");

        // Assert
        sql.ToString().Should().Contain("LIMIT :limit OFFSET :offset");
        parameters["limit"].Should().Be(20);
        parameters["offset"].Should().Be(10);
    }

    [Fact]
    public void BuildOffsetLimitClause_BigQuery_WithLimitAndOffset_UsesStandardSyntax()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, parameters, PaginationHelper.DatabaseVendor.BigQuery);

        // Assert
        sql.ToString().Should().Contain("LIMIT @limit OFFSET @offset");
    }

    [Fact]
    public void BuildOffsetLimitClause_Redshift_WithLimitAndOffset_UsesStandardSyntax()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, parameters, PaginationHelper.DatabaseVendor.Redshift);

        // Assert
        sql.ToString().Should().Contain("LIMIT @limit OFFSET @offset");
    }

    [Fact]
    public void BuildOffsetLimitClause_WithNullSql_ThrowsArgumentNullException()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();

        // Act
        var act = () => PaginationHelper.BuildOffsetLimitClause(
            null!, 10, 20, parameters, PaginationHelper.DatabaseVendor.PostgreSQL);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("sql");
    }

    [Fact]
    public void BuildOffsetLimitClause_WithNullParameters_ThrowsArgumentNullException()
    {
        // Arrange
        var sql = new StringBuilder();

        // Act
        var act = () => PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, null!, PaginationHelper.DatabaseVendor.PostgreSQL);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("parameters");
    }

    [Fact]
    public void NormalizeOffset_WithPositiveValue_ReturnsValue()
    {
        // Act
        var result = PaginationHelper.NormalizeOffset(10);

        // Assert
        result.Should().Be(10);
    }

    [Fact]
    public void NormalizeOffset_WithZero_ReturnsNull()
    {
        // Act
        var result = PaginationHelper.NormalizeOffset(0);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeOffset_WithNegative_ReturnsNull()
    {
        // Act
        var result = PaginationHelper.NormalizeOffset(-5);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeOffset_WithNull_ReturnsNull()
    {
        // Act
        var result = PaginationHelper.NormalizeOffset(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeLimit_WithPositiveValue_ReturnsValue()
    {
        // Act
        var result = PaginationHelper.NormalizeLimit(50, 1000);

        // Assert
        result.Should().Be(50);
    }

    [Fact]
    public void NormalizeLimit_WithValueExceedingMax_ReturnsMaxValue()
    {
        // Act
        var result = PaginationHelper.NormalizeLimit(5000, 1000);

        // Assert
        result.Should().Be(1000);
    }

    [Fact]
    public void NormalizeLimit_WithZero_ReturnsNull()
    {
        // Act
        var result = PaginationHelper.NormalizeLimit(0, 1000);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeLimit_WithNegative_ReturnsNull()
    {
        // Act
        var result = PaginationHelper.NormalizeLimit(-10, 1000);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeLimit_WithNull_ReturnsNull()
    {
        // Act
        var result = PaginationHelper.NormalizeLimit(null, 1000);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(PaginationHelper.DatabaseVendor.PostgreSQL, "@")]
    [InlineData(PaginationHelper.DatabaseVendor.MySQL, "@")]
    [InlineData(PaginationHelper.DatabaseVendor.SQLite, "@")]
    [InlineData(PaginationHelper.DatabaseVendor.SqlServer, "@")]
    [InlineData(PaginationHelper.DatabaseVendor.Oracle, ":")]
    [InlineData(PaginationHelper.DatabaseVendor.Snowflake, ":")]
    [InlineData(PaginationHelper.DatabaseVendor.BigQuery, "@")]
    [InlineData(PaginationHelper.DatabaseVendor.Redshift, "@")]
    public void BuildOffsetLimitClause_AllVendors_HandlesParameterPrefix(
        PaginationHelper.DatabaseVendor vendor,
        string expectedPrefix)
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, 10, 20, parameters, vendor, expectedPrefix);

        // Assert
        sql.ToString().Should().Contain(expectedPrefix);
    }

    [Fact]
    public void BuildOffsetLimitClause_WithNoOffsetOrLimit_DoesNotAddClause()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var originalLength = sql.Length;
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, null, null, parameters, PaginationHelper.DatabaseVendor.PostgreSQL);

        // Assert - SQL should be unchanged
        sql.Length.Should().Be(originalLength);
    }

    [Fact]
    public void BuildOffsetLimitClause_SqlServer_WithNoOffsetOrLimit_DoesNotAddClause()
    {
        // Arrange
        var sql = new StringBuilder("select * from table");
        var originalLength = sql.Length;
        var parameters = new Dictionary<string, object?>();

        // Act
        PaginationHelper.BuildOffsetLimitClause(
            sql, null, null, parameters, PaginationHelper.DatabaseVendor.SqlServer);

        // Assert - SQL should be unchanged for SQL Server too
        sql.Length.Should().Be(originalLength);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void NormalizeOffset_WithVariousPositiveValues_ReturnsValue(int offset)
    {
        // Act
        var result = PaginationHelper.NormalizeOffset(offset);

        // Assert
        result.Should().Be(offset);
    }

    [Theory]
    [InlineData(1, 1000, 1)]
    [InlineData(500, 1000, 500)]
    [InlineData(1000, 1000, 1000)]
    [InlineData(1500, 1000, 1000)]
    [InlineData(10000, 1000, 1000)]
    public void NormalizeLimit_WithVariousValues_CapsAtMaxLimit(int limit, int maxLimit, int expected)
    {
        // Act
        var result = PaginationHelper.NormalizeLimit(limit, maxLimit);

        // Assert
        result.Should().Be(expected);
    }
}
