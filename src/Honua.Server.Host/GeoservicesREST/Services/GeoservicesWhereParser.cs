// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Parses WHERE clauses and objectIds filters for Geoservices REST API requests.
/// </summary>
internal static class GeoservicesWhereParser
{
    public static QueryFilter? BuildFilter(IQueryCollection query, LayerDefinition layer, Microsoft.AspNetCore.Http.HttpContext? httpContext = null, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        var where = query.TryGetValue("where", out var whereValues) ? whereValues[^1] : "1=1";

        // SECURITY: Validate WHERE clause length before parsing
        GeoservicesRESTInputValidator.ValidateWhereClauseLength(where, httpContext, logger);

        var expression = ParseWhere(where, layer);

        if (query.TryGetValue("objectIds", out var objectIdValues) && objectIdValues.Count > 0)
        {
            var rawIds = objectIdValues[^1];
            if (string.IsNullOrWhiteSpace(rawIds))
            {
                ThrowBadRequest("objectIds must contain at least one identifier.");
            }

            var ids = QueryParsingHelpers.ParseCsv(rawIds);

            // SECURITY: Validate objectIds count before processing
            GeoservicesRESTInputValidator.ValidateObjectIdCount(ids.Count, httpContext, logger);

            if (ids.Count > 0)
            {
                var idExpression = BuildObjectIdExpression(layer.IdField, ids);
                expression = expression is null
                    ? idExpression
                    : new QueryBinaryExpression(expression, QueryBinaryOperator.And, idExpression);
            }
        }

        return expression is null ? null : new QueryFilter(expression);
    }

    private static QueryExpression? ParseWhere(string? where, LayerDefinition layer)
    {
        if (string.IsNullOrWhiteSpace(where) || where.Trim().EqualsIgnoreCase("1=1"))
        {
            return null;
        }

        // FIX (Bug 37): Ensure parsing errors are properly propagated to caller
        // rather than being caught and returning null silently
        try
        {
            var parser = new WhereParser(where, layer);
            var result = parser.Parse();

            // Ensure we got a valid result
            if (result == null)
            {
                ThrowBadRequest("WHERE clause parsing produced null result.");
            }

            return result;
        }
        catch (GeoservicesRESTQueryException)
        {
            // Re-throw our own exceptions to preserve error messages
            throw;
        }
        catch (Exception ex)
        {
            // Wrap other exceptions with clear error message
            ThrowBadRequest($"Invalid WHERE clause: {ex.Message}");
            return null; // Never reached due to throw
        }
    }

    private static QueryExpression BuildObjectIdExpression(string fieldName, IReadOnlyList<string> ids)
    {
        QueryExpression? expression = null;
        foreach (var id in ids)
        {
            var constant = ParseConstant(id);
            var equality = new QueryBinaryExpression(new QueryFieldReference(fieldName), QueryBinaryOperator.Equal, constant);
            expression = expression is null
                ? equality
                : new QueryBinaryExpression(expression, QueryBinaryOperator.Or, equality);
        }

        return expression ?? new QueryFieldReference(fieldName);
    }

    private static QueryConstant ParseConstant(string value)
    {
        if (value.EqualsIgnoreCase("null"))
        {
            return new QueryConstant(null);
        }

        if (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal) && value.Length >= 2)
        {
            var inner = value.Substring(1, value.Length - 2).Replace("''", "'", StringComparison.Ordinal);
            return new QueryConstant(inner);
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return new QueryConstant(longValue);
        }

        if (value.TryParseDouble(out var doubleValue))
        {
            return new QueryConstant(doubleValue);
        }

        return new QueryConstant(value);
    }

    private static void ThrowBadRequest(string message)
    {
        throw new GeoservicesRESTQueryException(message);
    }

    private enum WhereTokenKind
    {
        End,
        Identifier,
        String,
        Number,
        And,
        Or,
        Not,
        In,
        Like,
        Between,
        Is,
        Null,
        OpenParen,
        CloseParen,
        Comma,
        Operator
    }

    private readonly struct WhereToken
    {
        public WhereToken(WhereTokenKind kind, string? value)
        {
            Kind = kind;
            Value = value;
        }

        public WhereTokenKind Kind { get; }
        public string? Value { get; }
    }

    private sealed class WhereParser
    {
        private readonly string text;
        private readonly LayerDefinition layer;
        private int _position;
        private WhereToken _current;

        public WhereParser(string text, LayerDefinition layer)
        {
            this.text = text ?? string.Empty;
            this.layer = Guard.NotNull(layer);
            this.position = 0;
            this.current = default;
        }

        public QueryExpression Parse()
        {
            this.current = NextToken();
            var expression = ParseOr();
            Expect(WhereTokenKind.End);
            return expression;
        }

        private QueryExpression ParseOr()
        {
            var left = ParseAnd();
            while (this.current.Kind == WhereTokenKind.Or)
            {
                Advance();
                var right = ParseAnd();
                left = new QueryBinaryExpression(left, QueryBinaryOperator.Or, right);
            }

            return left;
        }

        private QueryExpression ParseAnd()
        {
            var left = ParseFactor();
            while (this.current.Kind == WhereTokenKind.And)
            {
                Advance();
                var right = ParseFactor();
                left = new QueryBinaryExpression(left, QueryBinaryOperator.And, right);
            }

            return left;
        }

        private QueryExpression ParseFactor()
        {
            if (this.current.Kind == WhereTokenKind.Not)
            {
                Advance();
                var operand = ParseFactor();
                return new QueryUnaryExpression(QueryUnaryOperator.Not, operand);
            }

            if (this.current.Kind == WhereTokenKind.OpenParen)
            {
                Advance();
                var inner = ParseOr();
                Expect(WhereTokenKind.CloseParen);
                return inner;
            }

            return ParseComparison();
        }

        private QueryExpression ParseComparison()
        {
            if (this.current.Kind != WhereTokenKind.Identifier)
            {
                ThrowError($"Unexpected token '{this.current.Value}'. Field name expected.");
            }

            var fieldToken = _current;
            Advance();
            var fieldName = GeoservicesFieldResolver.NormalizeFieldName(fieldToken.Value!, _layer);
            var fieldReference = new QueryFieldReference(fieldName);

            if (this.current.Kind == WhereTokenKind.Is)
            {
                Advance();
                var negateNull = false;
                if (this.current.Kind == WhereTokenKind.Not)
                {
                    negateNull = true;
                    Advance();
                }

                Expect(WhereTokenKind.Null);
                var constant = new QueryConstant(null);
                return negateNull
                    ? new QueryBinaryExpression(fieldReference, QueryBinaryOperator.NotEqual, constant)
                    : new QueryBinaryExpression(fieldReference, QueryBinaryOperator.Equal, constant);
            }

            var negate = false;
            if (this.current.Kind == WhereTokenKind.Not)
            {
                negate = true;
                Advance();
            }

            if (this.current.Kind == WhereTokenKind.Between)
            {
                Advance();
                var lower = ParseValue();
                ExpectKeyword(WhereTokenKind.And);
                var upper = ParseValue();

                var lowerComparison = new QueryBinaryExpression(fieldReference, QueryBinaryOperator.GreaterThanOrEqual, lower);
                var upperComparison = new QueryBinaryExpression(fieldReference, QueryBinaryOperator.LessThanOrEqual, upper);
                var between = new QueryBinaryExpression(lowerComparison, QueryBinaryOperator.And, upperComparison);
                return negate ? new QueryUnaryExpression(QueryUnaryOperator.Not, between) : between;
            }

            if (this.current.Kind == WhereTokenKind.In)
            {
                Advance();
                Expect(WhereTokenKind.OpenParen);
                var values = new List<QueryExpression>();
                do
                {
                    values.Add(ParseValue());
                    if (this.current.Kind == WhereTokenKind.Comma)
                    {
                        Advance();
                        continue;
                    }

                    break;
                }
                while (true);
                Expect(WhereTokenKind.CloseParen);

                QueryExpression? inExpression = null;
                foreach (var value in values)
                {
                    var equality = new QueryBinaryExpression(fieldReference, QueryBinaryOperator.Equal, value as QueryConstant ?? new QueryConstant(value));
                    inExpression = inExpression is null
                        ? equality
                        : new QueryBinaryExpression(inExpression, QueryBinaryOperator.Or, equality);
                }

                var inResult = inExpression ?? throw new GeoservicesRESTQueryException("IN list cannot be empty.");
                return negate ? new QueryUnaryExpression(QueryUnaryOperator.Not, inResult) : inResult;
            }

            if (this.current.Kind == WhereTokenKind.Like)
            {
                Advance();
                var pattern = ParseValue();
                var like = new QueryFunctionExpression("like", new QueryExpression[] { fieldReference, pattern });
                return negate ? new QueryUnaryExpression(QueryUnaryOperator.Not, like) : like;
            }

            if (this.current.Kind == WhereTokenKind.Operator)
            {
                var op = this.current.Value ?? string.Empty;
                Advance();
                var comparisonValue = ParseValue();
                var opKind = MapOperator(op);
                var comparison = new QueryBinaryExpression(fieldReference, opKind, comparisonValue);
                return negate ? new QueryUnaryExpression(QueryUnaryOperator.Not, comparison) : comparison;
            }

            ThrowError($"Unexpected token '{this.current.Value}' in where clause.");
            return new QueryConstant(1);
        }

        private QueryConstant ParseValue()
        {
            if (this.current.Kind == WhereTokenKind.String)
            {
                var value = ParseConstant(this.current.Value ?? string.Empty);
                Advance();
                return value;
            }

            if (this.current.Kind == WhereTokenKind.Number)
            {
                var value = ParseConstant(this.current.Value ?? string.Empty);
                Advance();
                return value;
            }

            if (this.current.Kind == WhereTokenKind.Identifier)
            {
                var identifier = this.current.Value ?? string.Empty;
                Advance();
                return ParseConstant(identifier);
            }

            if (this.current.Kind == WhereTokenKind.Null)
            {
                Advance();
                return new QueryConstant(null);
            }

            ThrowError($"Unable to parse literal from token '{this.current.Value}'.");
            return new QueryConstant(string.Empty);
        }

        private void Expect(WhereTokenKind kind)
        {
            if (this.current.Kind != kind)
            {
                ThrowError($"Expected token '{kind}' but found '{this.current.Kind}'.");
            }

            Advance();
        }

        private void ExpectKeyword(WhereTokenKind kind)
        {
            if (this.current.Kind != kind)
            {
                ThrowError($"Expected keyword '{kind}'.");
            }

            Advance();
        }

        private void Advance()
        {
            this.current = NextToken();
        }

        private WhereToken NextToken()
        {
            SkipWhitespace();
            if (_position >= this.text.Length)
            {
                return new WhereToken(WhereTokenKind.End, null);
            }

            var ch = _text[_position];

            if (char.IsLetter(ch) || ch == '_')
            {
                var identifier = ReadIdentifier();
                return identifier switch
                {
                    "AND" => new WhereToken(WhereTokenKind.And, null),
                    "OR" => new WhereToken(WhereTokenKind.Or, null),
                    "NOT" => new WhereToken(WhereTokenKind.Not, null),
                    "IN" => new WhereToken(WhereTokenKind.In, null),
                    "LIKE" => new WhereToken(WhereTokenKind.Like, null),
                    "BETWEEN" => new WhereToken(WhereTokenKind.Between, null),
                    "IS" => new WhereToken(WhereTokenKind.Is, null),
                    "NULL" => new WhereToken(WhereTokenKind.Null, null),
                    _ => new WhereToken(WhereTokenKind.Identifier, identifier)
                };
            }

            if (char.IsDigit(ch) || ch == '-' || ch == '+')
            {
                var number = ReadNumber();
                return new WhereToken(WhereTokenKind.Number, number);
            }

            if (ch == '\'')
            {
                var str = ReadString();
                return new WhereToken(WhereTokenKind.String, str);
            }

            if (ch == '(')
            {
                _position++;
                return new WhereToken(WhereTokenKind.OpenParen, null);
            }

            if (ch == ')')
            {
                _position++;
                return new WhereToken(WhereTokenKind.CloseParen, null);
            }

            if (ch == ',')
            {
                _position++;
                return new WhereToken(WhereTokenKind.Comma, null);
            }

            var op = ReadOperator();
            return new WhereToken(WhereTokenKind.Operator, op);
        }

        private void SkipWhitespace()
        {
            while (_position < this.text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }

        private string ReadIdentifier()
        {
            var start = _position;
            while (_position < this.text.Length && (char.IsLetterOrDigit(_text[_position]) || _text[_position] is '_' or '.'))
            {
                _position++;
            }

            return _text[start.._position].ToUpperInvariant();
        }

        private string ReadNumber()
        {
            var start = _position;
            _position++;
            while (_position < this.text.Length && (char.IsDigit(_text[_position]) || _text[_position] is '.' or 'e' or 'E' or '+' or '-'))
            {
                _position++;
            }

            return _text[start.._position];
        }

        private string ReadString()
        {
            _position++; // Skip opening quote
            var start = _position;
            var builder = new System.Text.StringBuilder();

            while (_position < this.text.Length)
            {
                var ch = _text[_position++];
                if (ch == '\'' && _position < this.text.Length && _text[_position] == '\'')
                {
                    builder.Append(_text, start, _position - start - 1);
                    builder.Append('\'');
                    _position++;
                    start = _position;
                    continue;
                }

                if (ch == '\'')
                {
                    builder.Append(_text, start, _position - start - 1);
                    return builder.ToString();
                }
            }

            ThrowError("Unterminated string literal in where clause.");
            return string.Empty;
        }

        private string ReadOperator()
        {
            var remaining = _text[this.position..];
            if (remaining.StartsWith(">=", StringComparison.Ordinal))
            {
                _position += 2;
                return ">=";
            }

            if (remaining.StartsWith("<=", StringComparison.Ordinal))
            {
                _position += 2;
                return "<=";
            }

            if (remaining.StartsWith("<>", StringComparison.Ordinal))
            {
                _position += 2;
                return "<>";
            }

            if (remaining.StartsWith("!=", StringComparison.Ordinal))
            {
                _position += 2;
                return "<>";
            }

            var ch = _text[_position++];
            return ch.ToString();
        }

        private static void ThrowError(string message)
        {
            throw new GeoservicesRESTQueryException(message);
        }
    }

    private static QueryBinaryOperator MapOperator(string op)
    {
        return op switch
        {
            "=" => QueryBinaryOperator.Equal,
            "!=" or "<>" => QueryBinaryOperator.NotEqual,
            ">" => QueryBinaryOperator.GreaterThan,
            ">=" => QueryBinaryOperator.GreaterThanOrEqual,
            "<" => QueryBinaryOperator.LessThan,
            "<=" => QueryBinaryOperator.LessThanOrEqual,
            _ => QueryBinaryOperator.Equal
        };
    }
}
