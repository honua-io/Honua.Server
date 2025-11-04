// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Expressions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query.Filter;

public static class CqlFilterParser
{
    public static QueryFilter Parse(string filterText, LayerDefinition layer)
    {
        Guard.NotNullOrWhiteSpace(filterText);
        Guard.NotNull(layer);

        var parser = new Parser(filterText, layer);
        var expression = parser.Parse();
        return new QueryFilter(expression);
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly LayerDefinition _layer;
        private int _position;
        private Token _current;

        public Parser(string text, LayerDefinition layer)
        {
            _text = text;
            _layer = layer;
            _position = 0;
            _current = ReadNextToken();
        }

        public QueryExpression Parse()
        {
            var expression = ParseOr();
            Expect(TokenKind.End);
            return expression;
        }

        private QueryExpression ParseOr()
        {
            var left = ParseAnd();
            while (_current.Kind == TokenKind.Or)
            {
                Advance();
                var right = ParseAnd();
                left = new QueryBinaryExpression(left, QueryBinaryOperator.Or, right);
            }

            return left;
        }

        private QueryExpression ParseAnd()
        {
            var left = ParseNot();
            while (_current.Kind == TokenKind.And)
            {
                Advance();
                var right = ParseNot();
                left = new QueryBinaryExpression(left, QueryBinaryOperator.And, right);
            }

            return left;
        }

        private QueryExpression ParseNot()
        {
            if (_current.Kind == TokenKind.Not)
            {
                Advance();
                var operand = ParsePrimary();
                return new QueryUnaryExpression(QueryUnaryOperator.Not, operand);
            }

            return ParsePrimary();
        }

        private QueryExpression ParsePrimary()
        {
            switch (_current.Kind)
            {
                case TokenKind.LParen:
                    Advance();
                    var expression = ParseOr();
                    Expect(TokenKind.RParen);
                    return expression;
                case TokenKind.Identifier:
                case TokenKind.String:
                    return ParseComparison();
                default:
                    throw new InvalidOperationException($"Invalid CQL filter near '{_current.Text ?? _current.Kind.ToString()}'.");
            }
        }

        private QueryExpression ParseComparison()
        {
            var fieldToken = _current;
            Advance();

            if (!IsComparisonOperator(_current.Kind) && _current.Kind is not TokenKind.In and not TokenKind.Like)
            {
                throw new InvalidOperationException($"Expected comparison operator after '{fieldToken.Text}'.");
            }

            var opToken = _current;
            Advance();

            var (fieldName, fieldType) = ResolveField(fieldToken);
            if (opToken.Kind == TokenKind.In)
            {
                return ParseInExpression(fieldName, fieldType);
            }

            if (opToken.Kind == TokenKind.Like)
            {
                return ParseLikeExpression(fieldName, fieldType);
            }

            var rightExpression = ParseLiteral(fieldType);

            var op = MapOperator(opToken.Kind);
            return new QueryBinaryExpression(new QueryFieldReference(fieldName), op, rightExpression);
        }

        private QueryExpression ParseInExpression(string fieldName, string? fieldType)
        {
            Expect(TokenKind.LParen);

            var expressions = new List<QueryExpression>();
            while (true)
            {
                var literal = ParseLiteral(fieldType);
                expressions.Add(literal);

                if (_current.Kind == TokenKind.Comma)
                {
                    Advance();
                    continue;
                }

                break;
            }

            Expect(TokenKind.RParen);

            if (expressions.Count == 0)
            {
                throw new InvalidOperationException("IN clause must contain at least one value.");
            }

            QueryExpression? combined = null;
            foreach (var expression in expressions)
            {
                var equality = new QueryBinaryExpression(new QueryFieldReference(fieldName), QueryBinaryOperator.Equal, expression);
                combined = combined is null
                    ? equality
                    : new QueryBinaryExpression(combined, QueryBinaryOperator.Or, equality);
            }

            return combined ?? new QueryFieldReference(fieldName);
        }

        private QueryExpression ParseLikeExpression(string fieldName, string? fieldType)
        {
            var pattern = ParseLiteral(fieldType);
            return new QueryFunctionExpression(
                "like",
                new List<QueryExpression>
                {
                    new QueryFieldReference(fieldName),
                    pattern
                });
        }

        private QueryExpression ParseLiteral(string? fieldType)
        {
            switch (_current.Kind)
            {
                case TokenKind.String:
                    {
                        var value = _current.Text ?? string.Empty;
                        Advance();
                        return new QueryConstant(CqlFilterParserUtils.ConvertToFieldValue(fieldType, value));
                    }
                case TokenKind.Number:
                    {
                        var numeric = _current.Text ?? string.Empty;
                        Advance();
                        return new QueryConstant(CqlFilterParserUtils.ConvertToFieldValue(fieldType, numeric));
                    }
                case TokenKind.Identifier:
                    {
                        var value = _current.Text ?? string.Empty;
                        Advance();

                        if (string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase))
                        {
                            return new QueryConstant(null);
                        }

                        if (bool.TryParse(value, out var boolValue))
                        {
                            return new QueryConstant(boolValue);
                        }

                        return new QueryConstant(CqlFilterParserUtils.ConvertToFieldValue(fieldType, value));
                    }
                default:
                    throw new InvalidOperationException($"Invalid literal near '{_current.Text ?? _current.Kind.ToString()}'.");
            }
        }

        private (string FieldName, string? FieldType) ResolveField(Token token)
        {
            return token.Kind switch
            {
                TokenKind.Identifier => CqlFilterParserUtils.ResolveField(_layer, token.Text ?? string.Empty),
                TokenKind.String => CqlFilterParserUtils.ResolveField(_layer, token.Text ?? string.Empty),
                _ => throw new InvalidOperationException($"Invalid field token '{token.Kind}'.")
            };
        }

        private static bool IsComparisonOperator(TokenKind kind)
        {
            return kind is TokenKind.Equals or TokenKind.NotEquals or TokenKind.GreaterThan or TokenKind.GreaterThanOrEqual or TokenKind.LessThan or TokenKind.LessThanOrEqual;
        }

        private static QueryBinaryOperator MapOperator(TokenKind kind)
        {
            return kind switch
            {
                TokenKind.Equals => QueryBinaryOperator.Equal,
                TokenKind.NotEquals => QueryBinaryOperator.NotEqual,
                TokenKind.GreaterThan => QueryBinaryOperator.GreaterThan,
                TokenKind.GreaterThanOrEqual => QueryBinaryOperator.GreaterThanOrEqual,
                TokenKind.LessThan => QueryBinaryOperator.LessThan,
                TokenKind.LessThanOrEqual => QueryBinaryOperator.LessThanOrEqual,
                _ => throw new InvalidOperationException($"Operator '{kind}' is not supported.")
            };
        }

        private void Advance()
        {
            _current = ReadNextToken();
        }

        private void Expect(TokenKind expected)
        {
            if (_current.Kind != expected)
            {
                throw new InvalidOperationException($"Invalid CQL filter. Expected token '{expected}' but found '{_current.Kind}'.");
            }

            Advance();
        }

        private Token ReadNextToken()
        {
            SkipWhitespace();

            if (_position >= _text.Length)
            {
                return new Token(TokenKind.End);
            }

            var current = _text[_position];

            if (current is '(')
            {
                _position++;
                return new Token(TokenKind.LParen);
            }

            if (current is ')')
            {
                _position++;
                return new Token(TokenKind.RParen);
            }

            if (current is ',')
            {
                _position++;
                return new Token(TokenKind.Comma);
            }

            if (current is '\'' or '"')
            {
                var text = ReadStringLiteral(current);
                return new Token(TokenKind.String, text);
            }

            if (IsNumberStart(current))
            {
                var number = ReadNumber();
                return new Token(TokenKind.Number, number);
            }

            if (current is '=' or '!' or '<' or '>')
            {
                return ReadOperator();
            }

            if (IsIdentifierStart(current))
            {
                var identifier = ReadIdentifier();
                return ClassifyIdentifier(identifier);
            }

            throw new InvalidOperationException($"Invalid character '{current}' in CQL filter.");
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }

        private string ReadStringLiteral(char quote)
        {
            _position++; // Skip opening quote
            var start = _position;
            var builder = new System.Text.StringBuilder();

            while (_position < _text.Length)
            {
                var current = _text[_position++];
                if (current == quote)
                {
                    if (_position < _text.Length && _text[_position] == quote)
                    {
                        builder.Append(_text, start, _position - start - 1);
                        builder.Append(quote);
                        _position++;
                        start = _position;
                        continue;
                    }

                    builder.Append(_text, start, _position - start - 1);
                    return builder.ToString();
                }
            }

            throw new InvalidOperationException("Unterminated string literal in CQL filter.");
        }

        private bool IsNumberStart(char c)
        {
            if (char.IsDigit(c))
            {
                return true;
            }

            if ((c == '+' || c == '-') && _position + 1 < _text.Length)
            {
                return char.IsDigit(_text[_position + 1]);
            }

            return false;
        }

        private string ReadNumber()
        {
            var start = _position;

            if (_text[_position] is '+' or '-')
            {
                _position++;
            }

            bool hasDecimal = false;
            bool hasExponent = false;

            while (_position < _text.Length)
            {
                var current = _text[_position];
                if (char.IsDigit(current))
                {
                    _position++;
                    continue;
                }

                if (current == '.' && !hasDecimal)
                {
                    hasDecimal = true;
                    _position++;
                    continue;
                }

                if ((current == 'e' || current == 'E') && !hasExponent)
                {
                    hasExponent = true;
                    _position++;
                    if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-'))
                    {
                        _position++;
                    }
                    continue;
                }

                break;
            }

            return _text[start.._position];
        }

        private Token ReadOperator()
        {
            var current = _text[_position];
            _position++;

            if (current == '=')
            {
                return new Token(TokenKind.Equals);
            }

            if (current == '!')
            {
                if (_position < _text.Length && _text[_position] == '=')
                {
                    _position++;
                    return new Token(TokenKind.NotEquals);
                }

                throw new InvalidOperationException("Invalid token '!' in CQL filter.");
            }

            if (current == '<')
            {
                if (_position < _text.Length)
                {
                    if (_text[_position] == '=')
                    {
                        _position++;
                        return new Token(TokenKind.LessThanOrEqual);
                    }

                    if (_text[_position] == '>')
                    {
                        _position++;
                        return new Token(TokenKind.NotEquals);
                    }
                }

                return new Token(TokenKind.LessThan);
            }

            if (current == '>')
            {
                if (_position < _text.Length && _text[_position] == '=')
                {
                    _position++;
                    return new Token(TokenKind.GreaterThanOrEqual);
                }

                return new Token(TokenKind.GreaterThan);
            }

            throw new InvalidOperationException("Invalid comparison operator in CQL filter.");
        }

        private bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c is '_' or ':';
        }

        private string ReadIdentifier()
        {
            var start = _position;
            _position++;

            while (_position < _text.Length)
            {
                var current = _text[_position];
                if (char.IsLetterOrDigit(current) || current is '_' or ':' or '.' or '/')
                {
                    _position++;
                    continue;
                }

                break;
            }

            return _text[start.._position];
        }

        private Token ClassifyIdentifier(string identifier)
        {
            if (string.Equals(identifier, "AND", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.And);
            }

            if (string.Equals(identifier, "OR", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Or);
            }

            if (string.Equals(identifier, "NOT", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Not);
            }

            if (string.Equals(identifier, "IN", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.In);
            }

            if (string.Equals(identifier, "LIKE", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Like);
            }

            return new Token(TokenKind.Identifier, identifier);
        }
    }

    private readonly struct Token
    {
        public Token(TokenKind kind, string? text = null)
        {
            Kind = kind;
            Text = text;
        }

        public TokenKind Kind { get; }
        public string? Text { get; }
    }

    private enum TokenKind
    {
        End,
        Identifier,
        String,
        Number,
        LParen,
        RParen,
        Comma,
        Equals,
        NotEquals,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        And,
        Or,
        Not,
        In,
        Like
    }
}
