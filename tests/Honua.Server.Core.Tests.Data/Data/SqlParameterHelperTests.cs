using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

[Trait("Category", "Unit")]
public class SqlParameterHelperTests
{
    [Fact]
    public void AddParameters_WithValidDictionary_AddsAllParameters()
    {
        // Arrange
        var command = new MockDbCommand();
        var parameters = new Dictionary<string, object?>
        {
            ["Id"] = 1,
            ["Name"] = "test",
            ["Active"] = true
        };

        // Act
        SqlParameterHelper.AddParameters(command, parameters);

        // Assert
        command.Parameters.Count.Should().Be(3);
        ((MockDbParameter)command.Parameters["Id"]!).Value.Should().Be(1);
        ((MockDbParameter)command.Parameters["Name"]!).Value.Should().Be("test");
        ((MockDbParameter)command.Parameters["Active"]!).Value.Should().Be(true);
    }

    [Fact]
    public void AddParameters_WithNullValue_AddsDBNull()
    {
        // Arrange
        var command = new MockDbCommand();
        var parameters = new Dictionary<string, object?>
        {
            ["NullField"] = null
        };

        // Act
        SqlParameterHelper.AddParameters(command, parameters);

        // Assert
        command.Parameters.Count.Should().Be(1);
        ((MockDbParameter)command.Parameters["NullField"]!).Value.Should().Be(DBNull.Value);
    }

    [Fact]
    public void AddParameters_WithExistingParameter_UpdatesValue()
    {
        // Arrange
        var command = new MockDbCommand();
        var param = command.CreateParameter();
        param.ParameterName = "Id";
        param.Value = 1;
        command.Parameters.Add(param);

        var parameters = new Dictionary<string, object?>
        {
            ["Id"] = 2
        };

        // Act
        SqlParameterHelper.AddParameters(command, parameters);

        // Assert
        command.Parameters.Count.Should().Be(1);
        ((MockDbParameter)command.Parameters["Id"]!).Value.Should().Be(2);
    }

    [Fact]
    public void AddParameters_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        IDbCommand? command = null;
        var parameters = new Dictionary<string, object?>();

        // Act
        var act = () => SqlParameterHelper.AddParameters(command!, parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("command");
    }

    [Fact]
    public void AddParameters_WithNullDictionary_ThrowsArgumentNullException()
    {
        // Arrange
        var command = new MockDbCommand();
        IReadOnlyDictionary<string, object?>? parameters = null;

        // Act
        var act = () => SqlParameterHelper.AddParameters(command, parameters!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("parameters");
    }

    [Fact]
    public void AddParameters_WithSpecialCharacters_AddsParametersCorrectly()
    {
        // Arrange
        var command = new MockDbCommand();
        var parameters = new Dictionary<string, object?>
        {
            ["Field"] = "value with 'quotes' and \"double quotes\"",
            ["Unicode"] = "日本語テスト",
            ["Emoji"] = "😀🎉"
        };

        // Act
        SqlParameterHelper.AddParameters(command, parameters);

        // Assert
        command.Parameters.Count.Should().Be(3);
        ((MockDbParameter)command.Parameters["Field"]!).Value.Should().Be("value with 'quotes' and \"double quotes\"");
        ((MockDbParameter)command.Parameters["Unicode"]!).Value.Should().Be("日本語テスト");
        ((MockDbParameter)command.Parameters["Emoji"]!).Value.Should().Be("😀🎉");
    }

    [Fact]
    public void AddParameters_WithVeryLongString_AddsParameterCorrectly()
    {
        // Arrange
        var command = new MockDbCommand();
        var longString = new string('A', 100000);
        var parameters = new Dictionary<string, object?>
        {
            ["LongField"] = longString
        };

        // Act
        SqlParameterHelper.AddParameters(command, parameters);

        // Assert
        command.Parameters.Count.Should().Be(1);
        ((MockDbParameter)command.Parameters["LongField"]!).Value.Should().Be(longString);
    }

    [Fact]
    public void AddParameters_WithEnumerable_AddsAllParameters()
    {
        // Arrange
        var command = new MockDbCommand();
        var parameters = new List<KeyValuePair<string, object?>>
        {
            new("Id", 1),
            new("Name", "test")
        };

        // Act
        SqlParameterHelper.AddParameters(command, parameters);

        // Assert
        command.Parameters.Count.Should().Be(2);
    }

    [Fact]
    public void TryResolveKey_WithValidKey_ReturnsKeyValue()
    {
        // Arrange
        var attributes = new Dictionary<string, object?>
        {
            ["id"] = 123,
            ["name"] = "test"
        };

        // Act
        var result = SqlParameterHelper.TryResolveKey(attributes, "id");

        // Assert
        result.Should().Be("123");
    }

    [Fact]
    public void TryResolveKey_WithMissingKey_ReturnsNull()
    {
        // Arrange
        var attributes = new Dictionary<string, object?>
        {
            ["name"] = "test"
        };

        // Act
        var result = SqlParameterHelper.TryResolveKey(attributes, "id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryResolveKey_WithNullValue_ReturnsNull()
    {
        // Arrange
        var attributes = new Dictionary<string, object?>
        {
            ["id"] = null
        };

        // Act
        var result = SqlParameterHelper.TryResolveKey(attributes, "id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeParameterName_WithValidName_ReturnsNormalizedName()
    {
        // Arrange & Act
        var result = SqlParameterHelper.NormalizeParameterName("FieldName", 0);

        // Assert
        result.Should().Be("@fieldname");
    }

    [Fact]
    public void NormalizeParameterName_WithSpecialCharacters_RemovesSpecialChars()
    {
        // Arrange & Act
        var result = SqlParameterHelper.NormalizeParameterName("Field-Name_123", 0);

        // Assert
        result.Should().Be("@fieldname_123");
    }

    [Fact]
    public void NormalizeParameterName_WithEmptyString_ReturnsOrdinalName()
    {
        // Arrange & Act
        var result = SqlParameterHelper.NormalizeParameterName("", 5);

        // Assert
        result.Should().Be("@p5");
    }

    [Fact]
    public void NormalizeParameterName_WithStartingDigit_PrependsP()
    {
        // Arrange & Act
        var result = SqlParameterHelper.NormalizeParameterName("123Field", 0);

        // Assert
        result.Should().Be("@p123field");
    }

    [Fact]
    public void CreateUniqueParameterName_WithFirstOccurrence_ReturnsBaseName()
    {
        // Arrange
        var counters = new Dictionary<string, int>();

        // Act
        var result = SqlParameterHelper.CreateUniqueParameterName("Field", counters, 0);

        // Assert
        result.Should().Be("@field");
        counters["field"].Should().Be(0);
    }

    [Fact]
    public void CreateUniqueParameterName_WithDuplicates_AddsSuffix()
    {
        // Arrange
        var counters = new Dictionary<string, int>();

        // Act
        var result1 = SqlParameterHelper.CreateUniqueParameterName("Field", counters, 0);
        var result2 = SqlParameterHelper.CreateUniqueParameterName("Field", counters, 1);
        var result3 = SqlParameterHelper.CreateUniqueParameterName("Field", counters, 2);

        // Assert
        result1.Should().Be("@field");
        result2.Should().Be("@field_1");
        result3.Should().Be("@field_2");
    }

    [Fact]
    public void CreateUniqueParameterName_WithNullCounters_ThrowsArgumentNullException()
    {
        // Arrange
        IDictionary<string, int>? counters = null;

        // Act
        var act = () => SqlParameterHelper.CreateUniqueParameterName("Field", counters!, 0);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("counters");
    }

    [Fact]
    public void NormalizeKeyParameter_DelegatesToLayerMetadataHelper()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            ServiceId = "test-service",
            Id = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geom",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "int" }
            }
        };

        // Act
        var result = SqlParameterHelper.NormalizeKeyParameter(layer, "123");

        // Assert
        result.Should().BeOfType<int>().Which.Should().Be(123);
    }

    // Mock implementations for testing
    private class MockDbCommand : DbCommand
    {
        public MockDbCommand()
        {
            DbParameterCollection = new MockParameterCollection();
        }

        public override string? CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; }
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }

        protected override DbParameter CreateDbParameter()
        {
            return new MockDbParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            throw new NotImplementedException();
        }
    }

    private class MockDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string? ParameterName { get; set; } = string.Empty;
        public override int Size { get; set; }
        public override string? SourceColumn { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }

        public override void ResetDbType() { }
    }

    private class MockParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new();

        public override int Count => _parameters.Count;
        public override object SyncRoot => _parameters;

        public override int Add(object value)
        {
            _parameters.Add((DbParameter)value);
            return _parameters.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (DbParameter param in values)
            {
                _parameters.Add(param);
            }
        }

        public override void Clear() => _parameters.Clear();

        public override bool Contains(object value) =>
            _parameters.Contains((DbParameter)value);

        public override bool Contains(string value) =>
            _parameters.Exists(p => p.ParameterName == value);

        public override void CopyTo(Array array, int index) =>
            ((System.Collections.ICollection)_parameters).CopyTo(array, index);

        public override System.Collections.IEnumerator GetEnumerator() => _parameters.GetEnumerator();

        public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);

        public override int IndexOf(string parameterName) =>
            _parameters.FindIndex(p => p.ParameterName == parameterName);

        public override void Insert(int index, object value) =>
            _parameters.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _parameters.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _parameters.RemoveAt(index);

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0) _parameters.RemoveAt(index);
        }

        protected override DbParameter GetParameter(int index) => _parameters[index];

        protected override DbParameter GetParameter(string parameterName) =>
            _parameters.Find(p => p.ParameterName == parameterName)!;

        protected override void SetParameter(int index, DbParameter value) =>
            _parameters[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index >= 0) _parameters[index] = value;
        }
    }
}
