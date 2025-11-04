// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Microsoft.OData;

namespace Honua.Server.Core.Data.Query;

public sealed class SqlFilterTranslator
{
    private readonly QueryEntityDefinition _entity;
    private readonly IDictionary<string, object?> _parameters;
    private readonly Func<string, string> _quoteIdentifier;
    private readonly string _parameterPrefix;
    private readonly Func<QueryFunctionExpression, string, string?>? _functionTranslator;
    private readonly Func<QuerySpatialExpression, string, string?>? _spatialTranslator;

    private string _alias = string.Empty;
    private int _parameterIndex;

    public SqlFilterTranslator(
        QueryEntityDefinition entity,
        IDictionary<string, object?> parameters,
        Func<string, string> quoteIdentifier,
        string parameterPrefix = "filter",
        Func<QueryFunctionExpression, string, string?>? functionTranslator = null,
        Func<QuerySpatialExpression, string, string?>? spatialTranslator = null)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _quoteIdentifier = quoteIdentifier ?? throw new ArgumentNullException(nameof(quoteIdentifier));
        _parameterPrefix = parameterPrefix;
        _functionTranslator = functionTranslator;
        _spatialTranslator = spatialTranslator;
    }

    public string? Translate(QueryFilter? filter, string alias)
    {
        if (filter?.Expression is null)
        {
            return null;
        }

        _alias = alias ?? throw new ArgumentNullException(nameof(alias));
        _parameterIndex = 0;
        return TranslateExpression(filter.Expression);
    }

    private string TranslateExpression(QueryExpression expression)
    {
        return expression switch
        {
            QueryBinaryExpression binary => TranslateBinary(binary),
            QueryUnaryExpression unary => TranslateUnary(unary),
            QueryFieldReference field => FormatField(field.Name),
            QueryConstant constant => TranslateConstant(constant.Value),
            QueryFunctionExpression function => TranslateFunction(function),
            QuerySpatialExpression spatial => TranslateSpatial(spatial),
            _ => throw new NotSupportedException($"Query expression '{expression.GetType().Name}' is not supported.")
        };
    }

    private string TranslateBinary(QueryBinaryExpression expression)
    {
        // Handle logical operators (AND, OR)
        if (expression.Operator is QueryBinaryOperator.And or QueryBinaryOperator.Or)
        {
            var left = TranslateExpression(expression.Left);
            var right = TranslateExpression(expression.Right);
            var op = expression.Operator == QueryBinaryOperator.And ? "AND" : "OR";
            return $"({left}) {op} ({right})";
        }

        // Handle arithmetic operators (add, sub, mul, div, mod)
        if (expression.Operator is QueryBinaryOperator.Add or QueryBinaryOperator.Subtract or
            QueryBinaryOperator.Multiply or QueryBinaryOperator.Divide or QueryBinaryOperator.Modulo)
        {
            var left = TranslateExpression(expression.Left);
            var right = TranslateExpression(expression.Right);
            var op = expression.Operator switch
            {
                QueryBinaryOperator.Add => "+",
                QueryBinaryOperator.Subtract => "-",
                QueryBinaryOperator.Multiply => "*",
                QueryBinaryOperator.Divide => "/",
                QueryBinaryOperator.Modulo => "%",
                _ => throw new NotSupportedException($"Arithmetic operator '{expression.Operator}' is not supported.")
            };
            return $"({left} {op} {right})";
        }

        // Handle comparison operators (eq, ne, gt, ge, lt, le)
        return TranslateComparison(expression);
    }

    private string TranslateUnary(QueryUnaryExpression expression)
    {
        if (expression.Operator != QueryUnaryOperator.Not)
        {
            throw new NotSupportedException($"Unary operator '{expression.Operator}' is not supported.");
        }

        var operand = TranslateExpression(expression.Operand);
        return $"NOT ({operand})";
    }

    private string TranslateComparison(QueryBinaryExpression expression)
    {
        if (expression.Left is QueryFieldReference leftField && expression.Right is QueryFieldReference rightField)
        {
            var leftSql = FormatField(leftField.Name);
            var rightSql = FormatField(rightField.Name);
            return $"{leftSql} {MapOperator(expression.Operator)} {rightSql}";
        }

        if (expression.Left is QueryFieldReference field && expression.Right is QueryConstant constant)
        {
            return TranslateFieldComparison(field, expression.Operator, constant);
        }

        if (expression.Right is QueryFieldReference fieldRight && expression.Left is QueryConstant constantLeft)
        {
            var swappedOperator = SwapOperator(expression.Operator);
            return TranslateFieldComparison(fieldRight, swappedOperator, constantLeft);
        }

        throw new NotSupportedException("Only comparisons between fields and constants are currently supported.");
    }

    private string TranslateFieldComparison(QueryFieldReference fieldReference, QueryBinaryOperator op, QueryConstant constant)
    {
        var field = _entity.GetField(fieldReference.Name);
        var fieldSql = FormatField(fieldReference.Name);
        var value = NormalizeConstant(constant.Value);

        if (value is null)
        {
            return op switch
            {
                QueryBinaryOperator.Equal => $"{fieldSql} IS NULL",
                QueryBinaryOperator.NotEqual => $"{fieldSql} IS NOT NULL",
                _ => throw new NotSupportedException("Null comparisons only support 'eq' or 'ne'.")
            };
        }

        var convertedValue = ConvertToFieldType(field, value);
        var parameter = AddParameter(convertedValue);
        var operatorSql = MapOperator(op);
        return $"{fieldSql} {operatorSql} {parameter}";
    }

    private string MapOperator(QueryBinaryOperator op)
    {
        return op switch
        {
            QueryBinaryOperator.Equal => "=",
            QueryBinaryOperator.NotEqual => "<>",
            QueryBinaryOperator.GreaterThan => ">",
            QueryBinaryOperator.GreaterThanOrEqual => ">=",
            QueryBinaryOperator.LessThan => "<",
            QueryBinaryOperator.LessThanOrEqual => "<=",
            _ => throw new NotSupportedException($"Operator '{op}' is not supported.")
        };
    }

    private static QueryBinaryOperator SwapOperator(QueryBinaryOperator op)
    {
        return op switch
        {
            QueryBinaryOperator.GreaterThan => QueryBinaryOperator.LessThan,
            QueryBinaryOperator.GreaterThanOrEqual => QueryBinaryOperator.LessThanOrEqual,
            QueryBinaryOperator.LessThan => QueryBinaryOperator.GreaterThan,
            QueryBinaryOperator.LessThanOrEqual => QueryBinaryOperator.GreaterThanOrEqual,
            _ => op
        };
    }

    private string FormatField(string fieldName)
    {
        var quoted = _quoteIdentifier(fieldName);
        return string.IsNullOrWhiteSpace(_alias) ? quoted : $"{_alias}.{quoted}";
    }

    private string TranslateConstant(object? value)
    {
        var normalized = NormalizeConstant(value);
        var parameter = AddParameter(normalized);
        return parameter;
    }

    private static object? NormalizeConstant(object? value)
    {
        return value switch
        {
            null => null,
            ODataEnumValue enumValue => enumValue.Value,
            _ => value
        };
    }

    private object? ConvertToFieldType(QueryFieldDefinition field, object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return field.DataType switch
            {
                QueryDataType.String or QueryDataType.Json => Convert.ToString(value, CultureInfo.InvariantCulture),
                QueryDataType.Boolean => ConvertToBoolean(value),
                QueryDataType.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                QueryDataType.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                QueryDataType.Single => Convert.ToSingle(value, CultureInfo.InvariantCulture),
                QueryDataType.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                QueryDataType.Decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                QueryDataType.DateTimeOffset => ConvertToDateTimeOffset(value),
                QueryDataType.Guid => ConvertToGuid(value),
                QueryDataType.Binary => ConvertToBinary(value),
                QueryDataType.Geometry => throw new NotSupportedException("Geometry comparisons are not supported yet."),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidOperationException($"Value '{value}' could not be converted to field '{field.Name}' of type '{field.DataType}'.", ex);
        }
    }

    private static bool ConvertToBoolean(object value)
    {
        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i != 0,
            int i => i != 0,
            long l => l != 0,
            _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
        };
    }

    private static DateTimeOffset ConvertToDateTimeOffset(object value)
    {
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) => parsed,
            _ => DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
        };
    }

    private static Guid ConvertToGuid(object value)
    {
        return value switch
        {
            Guid g => g,
            string s when Guid.TryParse(s, out var parsed) => parsed,
            _ => Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!)
        };
    }

    private static byte[] ConvertToBinary(object value)
    {
        return value switch
        {
            byte[] bytes => bytes,
            ReadOnlyMemory<byte> memory => memory.ToArray(),
            string s => Convert.FromBase64String(s),
            _ => throw new InvalidOperationException("Unsupported binary constant value.")
        };
    }

    private string AddParameter(object? value)
    {
        var name = $"{_parameterPrefix}_{_parameterIndex++}";
        _parameters[name] = value;
        return $"@{name}";
    }

    private string TranslateFunction(QueryFunctionExpression expression)
    {
        if (string.Equals(expression.Name, "like", StringComparison.OrdinalIgnoreCase))
        {
            return TranslateLike(expression);
        }

        if (_functionTranslator is null)
        {
            throw new NotSupportedException($"Query function '{expression.Name}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(_alias))
        {
            throw new InvalidOperationException("Function translation requires an alias context.");
        }

        var sql = _functionTranslator(expression, _alias);
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new NotSupportedException($"Query function '{expression.Name}' is not supported.");
        }

        return sql;
    }

    private string TranslateLike(QueryFunctionExpression expression)
    {
        if (expression.Arguments.Count != 2)
        {
            throw new NotSupportedException("LIKE requires two arguments.");
        }

        if (expression.Arguments[0] is not QueryFieldReference field)
        {
            throw new NotSupportedException("LIKE requires the first argument to be a field reference.");
        }

        if (expression.Arguments[1] is not QueryConstant constant)
        {
            throw new NotSupportedException("LIKE requires the second argument to be a constant value.");
        }

        var fieldSql = FormatField(field.Name);
        var parameter = AddParameter(constant.Value is null ? string.Empty : Convert.ToString(constant.Value, CultureInfo.InvariantCulture));
        return $"{fieldSql} LIKE {parameter}";
    }

    private string TranslateSpatial(QuerySpatialExpression expression)
    {
        if (_spatialTranslator is null)
        {
            throw new NotSupportedException("Spatial expressions require database-specific translation. Use the database provider's feature query builder.");
        }

        if (string.IsNullOrWhiteSpace(_alias))
        {
            throw new InvalidOperationException("Spatial expression translation requires an alias context.");
        }

        var sql = _spatialTranslator(expression, _alias);
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new NotSupportedException($"Spatial predicate '{expression.Predicate}' is not supported.");
        }

        return sql;
    }
}
