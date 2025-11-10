// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.RegularExpressions;

namespace Honua.Server.Enterprise.Sensors.Query;

/// <summary>
/// Parses OData-style query parameters into QueryOptions model.
/// Supports $filter, $expand, $select, $orderby, $top, $skip, $count.
///
/// Advanced filter support includes:
/// - Comparison operators: eq, ne, gt, ge, lt, le
/// - Logical operators: and, or, not
/// - String functions: contains, startswith, endswith, length, tolower, toupper, trim, concat, substring, indexof
/// - Math functions: round, floor, ceiling, add, sub, mul, div, mod
/// - Spatial functions: geo.distance, geo.intersects, geo.length
/// - Temporal functions: year, month, day, hour, minute, second
/// </summary>
public static class QueryOptionsParser
{
    public static QueryOptions Parse(
        string? filter,
        string? expand,
        string? select,
        string? orderby,
        int? top,
        int? skip,
        bool count)
    {
        return new QueryOptions
        {
            Filter = ParseFilter(filter),
            Expand = ParseExpand(expand),
            Select = ParseSelect(select),
            OrderBy = ParseOrderBy(orderby),
            Top = top,
            Skip = skip,
            Count = count
        };
    }

    private static FilterExpression? ParseFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        try
        {
            var parser = new FilterParser(filter);
            return parser.Parse();
        }
        catch
        {
            // If parsing fails, return null (invalid filter will be ignored)
            return null;
        }
    }

    private static ExpandOptions? ParseExpand(string? expand)
    {
        if (string.IsNullOrWhiteSpace(expand))
            return null;

        return ExpandOptions.Parse(expand);
    }

    private static IReadOnlyList<string>? ParseSelect(string? select)
    {
        if (string.IsNullOrWhiteSpace(select))
            return null;

        return select.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();
    }

    private static IReadOnlyList<OrderBy>? ParseOrderBy(string? orderby)
    {
        if (string.IsNullOrWhiteSpace(orderby))
            return null;

        var parts = orderby.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var orderByList = new List<OrderBy>();

        foreach (var part in parts)
        {
            var tokens = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var property = tokens[0];
            var direction = tokens.Length > 1 && tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? SortDirection.Descending
                : SortDirection.Ascending;

            orderByList.Add(new OrderBy
            {
                Property = property,
                Direction = direction
            });
        }

        return orderByList;
    }

    /// <summary>
    /// Internal parser for OData filter expressions.
    /// Uses recursive descent parsing to handle complex expressions.
    /// </summary>
    private class FilterParser
    {
        private readonly string _input;
        private int _position;

        public FilterParser(string input)
        {
            _input = input;
            _position = 0;
        }

        public FilterExpression Parse()
        {
            return ParseLogicalOr();
        }

        private FilterExpression ParseLogicalOr()
        {
            var left = ParseLogicalAnd();

            while (TryConsumeKeyword("or"))
            {
                var right = ParseLogicalAnd();
                left = new LogicalExpression
                {
                    Operator = "or",
                    Left = left,
                    Right = right
                };
            }

            return left;
        }

        private FilterExpression ParseLogicalAnd()
        {
            var left = ParseLogicalNot();

            while (TryConsumeKeyword("and"))
            {
                var right = ParseLogicalNot();
                left = new LogicalExpression
                {
                    Operator = "and",
                    Left = left,
                    Right = right
                };
            }

            return left;
        }

        private FilterExpression ParseLogicalNot()
        {
            if (TryConsumeKeyword("not"))
            {
                var operand = ParsePrimary();
                return new LogicalExpression
                {
                    Operator = "not",
                    Left = operand,
                    Right = null
                };
            }

            return ParsePrimary();
        }

        private FilterExpression ParsePrimary()
        {
            SkipWhitespace();

            // Handle parentheses
            if (TryConsume('('))
            {
                var expr = ParseLogicalOr();
                Consume(')');
                return expr;
            }

            // Try to parse function call
            if (TryParseFunction(out var function))
            {
                return function;
            }

            // Parse comparison expression
            return ParseComparison();
        }

        private FilterExpression ParseComparison()
        {
            var property = ParseIdentifier();
            SkipWhitespace();

            var @operator = ParseOperator();
            SkipWhitespace();

            var value = ParseValue();

            return new ComparisonExpression
            {
                Property = property,
                Operator = @operator,
                Value = value
            };
        }

        private bool TryParseFunction(out FunctionExpression function)
        {
            var startPos = _position;

            // Try to parse function name
            var functionName = ParseFunctionName();
            if (string.IsNullOrEmpty(functionName))
            {
                _position = startPos;
                function = null!;
                return false;
            }

            SkipWhitespace();
            if (!TryConsume('('))
            {
                _position = startPos;
                function = null!;
                return false;
            }

            // Parse arguments
            var arguments = new List<object>();
            while (!TryConsume(')'))
            {
                SkipWhitespace();
                if (arguments.Count > 0)
                {
                    Consume(',');
                    SkipWhitespace();
                }

                arguments.Add(ParseFunctionArgument());
            }

            function = new FunctionExpression
            {
                Name = functionName,
                Arguments = arguments
            };

            // Check if this is part of a comparison
            SkipWhitespace();
            if (IsOperatorAhead())
            {
                var @operator = ParseOperator();
                SkipWhitespace();
                var value = ParseValue();

                // Wrap in comparison
                function = new FunctionExpression
                {
                    Name = "comparison",
                    Arguments = new object[] { function, @operator.ToString(), value }
                };
            }

            return true;
        }

        private string ParseFunctionName()
        {
            var start = _position;
            while (_position < _input.Length &&
                   (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '.' || _input[_position] == '_'))
            {
                _position++;
            }

            return _input.Substring(start, _position - start);
        }

        private object ParseFunctionArgument()
        {
            SkipWhitespace();

            // Check if it's a nested function
            if (TryParseFunction(out var nestedFunction))
            {
                return nestedFunction;
            }

            // Check if it's a quoted string
            if (Peek() == '\'' || Peek() == '"')
            {
                return ParseValue();
            }

            // Check if it's a geometry literal
            if (TryConsumeKeyword("geometry"))
            {
                return ParseGeometryLiteral();
            }

            // Otherwise, it's a property name or number
            var token = ParseIdentifier();
            if (double.TryParse(token, out var number))
            {
                return number;
            }

            return token;
        }

        private string ParseGeometryLiteral()
        {
            Consume('\'');
            var start = _position;
            while (_position < _input.Length && _input[_position] != '\'')
            {
                _position++;
            }
            var geometry = _input.Substring(start, _position - start);
            Consume('\'');
            return $"geometry'{geometry}'";
        }

        private string ParseIdentifier()
        {
            SkipWhitespace();
            var start = _position;

            while (_position < _input.Length &&
                   (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_' || _input[_position] == '/'))
            {
                _position++;
            }

            return _input.Substring(start, _position - start);
        }

        private ComparisonOperator ParseOperator()
        {
            SkipWhitespace();
            var start = _position;

            while (_position < _input.Length && char.IsLetter(_input[_position]))
            {
                _position++;
            }

            if (_position == start)
            {
                throw new InvalidOperationException("Expected comparison operator in filter expression.");
            }

            var operatorStr = _input.Substring(start, _position - start).ToLowerInvariant();

            return operatorStr switch
            {
                "eq" => ComparisonOperator.Equals,
                "ne" => ComparisonOperator.NotEquals,
                "gt" => ComparisonOperator.GreaterThan,
                "ge" => ComparisonOperator.GreaterThanOrEqual,
                "lt" => ComparisonOperator.LessThan,
                "le" => ComparisonOperator.LessThanOrEqual,
                _ => ComparisonOperator.Equals
            };
        }

        private bool IsOperatorAhead()
        {
            var savedPos = _position;
            SkipWhitespace();

            var operators = new[] { "eq", "ne", "gt", "ge", "lt", "le" };
            foreach (var op in operators)
            {
                if (TryConsumeKeyword(op))
                {
                    _position = savedPos;
                    return true;
                }
            }

            _position = savedPos;
            return false;
        }

        private object ParseValue()
        {
            SkipWhitespace();

            // Handle quoted strings
            if (Peek() == '\'' || Peek() == '"')
            {
                var quote = Consume();
                var start = _position;
                while (_position < _input.Length && _input[_position] != quote)
                {
                    _position++;
                }
                var value = _input.Substring(start, _position - start);
                Consume(quote);
                return value;
            }

            // Handle numbers and unquoted values
            var tokenStart = _position;
            while (_position < _input.Length &&
                   !char.IsWhiteSpace(_input[_position]) &&
                   _input[_position] != ')' &&
                   _input[_position] != ',')
            {
                _position++;
            }

            if (_position == tokenStart)
            {
                throw new InvalidOperationException("Expected value in filter expression.");
            }

            var token = _input.Substring(tokenStart, _position - tokenStart);

            // Try to parse as number
            if (double.TryParse(token, out var number))
            {
                return number;
            }

            // Try to parse as boolean
            if (bool.TryParse(token, out var boolean))
            {
                return boolean;
            }

            // Try to parse as DateTime
            if (DateTime.TryParse(token, out var dateTime))
            {
                return dateTime;
            }

            return token;
        }

        private bool TryConsumeKeyword(string keyword)
        {
            SkipWhitespace();

            if (_position + keyword.Length > _input.Length)
                return false;

            var segment = _input.Substring(_position, keyword.Length);
            if (!segment.Equals(keyword, StringComparison.OrdinalIgnoreCase))
                return false;

            // Make sure it's followed by whitespace or end of string (not part of larger identifier)
            if (_position + keyword.Length < _input.Length)
            {
                var nextChar = _input[_position + keyword.Length];
                if (char.IsLetterOrDigit(nextChar) || nextChar == '_')
                    return false;
            }

            _position += keyword.Length;
            return true;
        }

        private bool TryConsume(char c)
        {
            SkipWhitespace();
            if (_position < _input.Length && _input[_position] == c)
            {
                _position++;
                return true;
            }
            return false;
        }

        private char Consume()
        {
            if (_position >= _input.Length)
                throw new InvalidOperationException("Unexpected end of input");

            return _input[_position++];
        }

        private void Consume(char expected)
        {
            var actual = Consume();
            if (actual != expected)
                throw new InvalidOperationException($"Expected '{expected}' but got '{actual}'");
        }

        private char Peek()
        {
            return _position < _input.Length ? _input[_position] : '\0';
        }

        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            {
                _position++;
            }
        }
    }
}
