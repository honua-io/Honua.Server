// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.CosmosDb;

internal static class CosmosDbFilterTranslator
{
    public static string? TryTranslate(
        QueryFilter? filter,
        Func<string, string> formatProperty,
        Func<object?, string> addParameter)
    {
        if (filter?.Expression is null)
        {
            return null;
        }

        var translator = new Translator(formatProperty, addParameter);
        return translator.Translate(filter.Expression);
    }

    private sealed class Translator
    {
        private readonly Func<string, string> _formatProperty;
        private readonly Func<object?, string> _addParameter;

        public Translator(Func<string, string> formatProperty, Func<object?, string> addParameter)
        {
            _formatProperty = formatProperty ?? throw new ArgumentNullException(nameof(formatProperty));
            _addParameter = addParameter ?? throw new ArgumentNullException(nameof(addParameter));
        }

        public string Translate(QueryExpression expression)
            => Visit(expression);

        private string Visit(QueryExpression expression)
        {
            return expression switch
            {
                QueryBinaryExpression binary => VisitBinary(binary),
                QueryUnaryExpression unary => VisitUnary(unary),
                QueryFunctionExpression => throw new NotSupportedException("Cosmos DB filter translation does not yet support function expressions."),
                QueryConstant => throw new NotSupportedException("Unexpected standalone constant in filter expression."),
                QueryFieldReference => throw new NotSupportedException("Unexpected standalone field reference in filter expression."),
                _ => throw new NotSupportedException($"Unsupported filter expression type '{expression.GetType().Name}'.")
            };
        }

        private string VisitUnary(QueryUnaryExpression unary)
        {
            if (unary.Operator != QueryUnaryOperator.Not)
            {
                throw new NotSupportedException($"Unary operator '{unary.Operator}' is not supported for Cosmos DB filters.");
            }

            var operand = Visit(unary.Operand);
            return $"(NOT ({operand}))";
        }

        private string VisitBinary(QueryBinaryExpression binary)
        {
            if (binary.Operator is QueryBinaryOperator.And or QueryBinaryOperator.Or)
            {
                var left = Visit(binary.Left);
                var right = Visit(binary.Right);
                var op = binary.Operator == QueryBinaryOperator.And ? "AND" : "OR";
                return $"(({left}) {op} ({right}))";
            }

            var (field, constant, reverse) = ExtractComparisonOperands(binary.Left, binary.Right);
            if (field is null || constant is null)
            {
                throw new NotSupportedException("Cosmos DB filters currently support comparisons between fields and constants only.");
            }

            var fieldSql = _formatProperty(field.Name);
            var parameter = _addParameter(constant.Value);

            var comparisonOperator = binary.Operator switch
            {
                QueryBinaryOperator.Equal => "=",
                QueryBinaryOperator.NotEqual => "!=",
                QueryBinaryOperator.GreaterThan => reverse ? "<" : ">",
                QueryBinaryOperator.GreaterThanOrEqual => reverse ? "<=" : ">=",
                QueryBinaryOperator.LessThan => reverse ? ">" : "<",
                QueryBinaryOperator.LessThanOrEqual => reverse ? ">=" : "<=",
                _ => throw new NotSupportedException($"Binary operator '{binary.Operator}' is not supported for Cosmos DB filters.")
            };

            if (reverse && binary.Operator is QueryBinaryOperator.GreaterThan or QueryBinaryOperator.GreaterThanOrEqual or QueryBinaryOperator.LessThan or QueryBinaryOperator.LessThanOrEqual)
            {
                // when the field is on the right side, we've swapped the comparison
                // operator already, so just emit as field OP value.
            }

            return $"{fieldSql} {comparisonOperator} {parameter}";
        }

        private static (QueryFieldReference? Field, QueryConstant? Constant, bool Reverse) ExtractComparisonOperands(
            QueryExpression left,
            QueryExpression right)
        {
            var leftField = left as QueryFieldReference;
            var rightField = right as QueryFieldReference;
            var leftConstant = left as QueryConstant;
            var rightConstant = right as QueryConstant;

            if (leftField is not null && rightConstant is not null)
            {
                return (leftField, rightConstant, false);
            }

            if (leftConstant is not null && rightField is not null)
            {
                return (rightField, leftConstant, true);
            }

            if (leftField is not null && rightField is null && rightConstant is null)
            {
                throw new NotSupportedException("Comparisons between a field and a non-constant expression are not supported for Cosmos DB filters.");
            }

            if (rightField is not null && leftField is null && leftConstant is null)
            {
                throw new NotSupportedException("Comparisons between a field and a non-constant expression are not supported for Cosmos DB filters.");
            }

            return (null, null, false);
        }
    }
}
