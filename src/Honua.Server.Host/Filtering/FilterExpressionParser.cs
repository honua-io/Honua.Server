// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Host.Filtering;

/// <summary>
/// Parses OData-style filter query strings into structured FilterExpression trees.
/// </summary>
/// <remarks>
/// <para>
/// Implements a lightweight parser for common OData filter expressions without requiring
/// the full OData library. Supports the most frequently used operators and functions.
/// </para>
/// <para>
/// <b>Supported Operators:</b>
/// </para>
/// <list type="bullet">
/// <item><description><b>Comparison:</b> eq, ne, gt, ge, lt, le</description></item>
/// <item><description><b>Logical:</b> and, or, not</description></item>
/// <item><description><b>String Functions:</b> contains, startswith, endswith</description></item>
/// </list>
/// <para>
/// <b>Operator Precedence (highest to lowest):</b>
/// </para>
/// <list type="number">
/// <item><description>Parentheses ( )</description></item>
/// <item><description>String functions (contains, startswith, endswith)</description></item>
/// <item><description>Comparison operators (eq, ne, gt, ge, lt, le)</description></item>
/// <item><description>Logical NOT (not)</description></item>
/// <item><description>Logical AND (and)</description></item>
/// <item><description>Logical OR (or)</description></item>
/// </list>
/// <para>
/// <b>Examples:</b>
/// </para>
/// <code>
/// var parser = new FilterExpressionParser();
///
/// // Simple comparison
/// var expr1 = parser.Parse("status eq 'active'");
///
/// // Multiple conditions
/// var expr2 = parser.Parse("createdAt gt 2025-01-01 and status eq 'active'");
///
/// // String function
/// var expr3 = parser.Parse("name contains 'test'");
///
/// // Complex expression with precedence
/// var expr4 = parser.Parse("(status eq 'active' or status eq 'pending') and priority gt 5");
///
/// // Negation
/// var expr5 = parser.Parse("not (isDeleted eq true)");
/// </code>
/// <para>
/// <b>Security Features:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Validates syntax and prevents injection attacks</description></item>
/// <item><description>Limits expression complexity (max 50 tokens, max 10 nested levels)</description></item>
/// <item><description>Property names must be validated separately using FilterQueryAttribute</description></item>
/// <item><description>All string comparisons are parameterized in generated LINQ expressions</description></item>
/// </list>
/// </remarks>
public sealed class FilterExpressionParser
{
    /// <summary>
    /// Maximum number of tokens allowed in a filter expression to prevent DoS attacks.
    /// </summary>
    private const int MaxTokens = 50;

    /// <summary>
    /// Maximum nesting depth for parentheses to prevent stack overflow.
    /// </summary>
    private const int MaxNestingDepth = 10;

    /// <summary>
    /// Parses an OData filter string into a FilterExpression tree.
    /// </summary>
    /// <param name="filter">The filter string to parse (e.g., "status eq 'active'").</param>
    /// <returns>A FilterExpression tree representing the parsed filter.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filter is null or whitespace.</exception>
    /// <exception cref="FilterParseException">Thrown when the filter syntax is invalid.</exception>
    /// <remarks>
    /// <b>Example Usage:</b>
    /// <code>
    /// var parser = new FilterExpressionParser();
    /// try
    /// {
    ///     var expression = parser.Parse("createdAt gt 2025-01-01");
    ///     // Use expression with FilterQueryableExtensions.ApplyFilter()
    /// }
    /// catch (FilterParseException ex)
    /// {
    ///     // Return 400 Bad Request with error message
    ///     return BadRequest(ex.Message);
    /// }
    /// </code>
    /// </remarks>
    public FilterExpression Parse(string filter)
    {
        Guard.NotNullOrWhiteSpace(filter);

        var tokens = Tokenize(filter);

        if (tokens.Count == 0)
        {
            throw new FilterParseException("Filter expression cannot be empty");
        }

        if (tokens.Count > MaxTokens)
        {
            throw new FilterParseException(
                $"Filter expression too complex (max {MaxTokens} tokens allowed)");
        }

        var index = 0;
        var expression = ParseOrExpression(tokens, ref index, nestingDepth: 0);

        if (index < tokens.Count)
        {
            throw new FilterParseException(
                $"Unexpected token '{tokens[index]}' at position {index}");
        }

        return expression;
    }

    /// <summary>
    /// Tokenizes the filter string into a list of tokens.
    /// </summary>
    private static List<string> Tokenize(string filter)
    {
        var tokens = new List<string>();
        var currentToken = string.Empty;
        var inString = false;
        var stringDelimiter = '\0';

        for (int i = 0; i < filter.Length; i++)
        {
            var ch = filter[i];

            if (inString)
            {
                if (ch == stringDelimiter)
                {
                    // End of string literal
                    tokens.Add(currentToken);
                    currentToken = string.Empty;
                    inString = false;
                }
                else
                {
                    currentToken += ch;
                }
            }
            else if (ch == '\'' || ch == '"')
            {
                // Start of string literal
                if (!string.IsNullOrWhiteSpace(currentToken))
                {
                    tokens.Add(currentToken.Trim());
                    currentToken = string.Empty;
                }
                inString = true;
                stringDelimiter = ch;
            }
            else if (ch == '(' || ch == ')')
            {
                // Parentheses are separate tokens
                if (!string.IsNullOrWhiteSpace(currentToken))
                {
                    tokens.Add(currentToken.Trim());
                    currentToken = string.Empty;
                }
                tokens.Add(ch.ToString());
            }
            else if (char.IsWhiteSpace(ch))
            {
                // Whitespace separates tokens
                if (!string.IsNullOrWhiteSpace(currentToken))
                {
                    tokens.Add(currentToken.Trim());
                    currentToken = string.Empty;
                }
            }
            else
            {
                currentToken += ch;
            }
        }

        if (inString)
        {
            throw new FilterParseException("Unterminated string literal");
        }

        if (!string.IsNullOrWhiteSpace(currentToken))
        {
            tokens.Add(currentToken.Trim());
        }

        return tokens;
    }

    /// <summary>
    /// Parses an OR expression (lowest precedence).
    /// </summary>
    private FilterExpression ParseOrExpression(List<string> tokens, ref int index, int nestingDepth)
    {
        var left = ParseAndExpression(tokens, ref index, nestingDepth);

        while (index < tokens.Count && IsOperator(tokens[index], "or"))
        {
            index++; // consume 'or'
            var right = ParseAndExpression(tokens, ref index, nestingDepth);
            left = new LogicalExpression(left, LogicalOperator.Or, right);
        }

        return left;
    }

    /// <summary>
    /// Parses an AND expression (higher precedence than OR).
    /// </summary>
    private FilterExpression ParseAndExpression(List<string> tokens, ref int index, int nestingDepth)
    {
        var left = ParseNotExpression(tokens, ref index, nestingDepth);

        while (index < tokens.Count && IsOperator(tokens[index], "and"))
        {
            index++; // consume 'and'
            var right = ParseNotExpression(tokens, ref index, nestingDepth);
            left = new LogicalExpression(left, LogicalOperator.And, right);
        }

        return left;
    }

    /// <summary>
    /// Parses a NOT expression (higher precedence than AND).
    /// </summary>
    private FilterExpression ParseNotExpression(List<string> tokens, ref int index, int nestingDepth)
    {
        if (index < tokens.Count && IsOperator(tokens[index], "not"))
        {
            index++; // consume 'not'
            var expression = ParseNotExpression(tokens, ref index, nestingDepth);
            return new NotExpression(expression);
        }

        return ParsePrimaryExpression(tokens, ref index, nestingDepth);
    }

    /// <summary>
    /// Parses a primary expression (comparison, string function, or parenthesized expression).
    /// </summary>
    private FilterExpression ParsePrimaryExpression(List<string> tokens, ref int index, int nestingDepth)
    {
        if (index >= tokens.Count)
        {
            throw new FilterParseException("Unexpected end of expression");
        }

        // Check for parenthesized expression
        if (tokens[index] == "(")
        {
            if (nestingDepth >= MaxNestingDepth)
            {
                throw new FilterParseException(
                    $"Maximum nesting depth ({MaxNestingDepth}) exceeded");
            }

            index++; // consume '('
            var expression = ParseOrExpression(tokens, ref index, nestingDepth + 1);

            if (index >= tokens.Count || tokens[index] != ")")
            {
                throw new FilterParseException("Expected closing parenthesis ')'");
            }

            index++; // consume ')'
            return expression;
        }

        // Parse property name
        var property = tokens[index++];

        if (index >= tokens.Count)
        {
            throw new FilterParseException($"Expected operator after property '{property}'");
        }

        var operatorToken = tokens[index++];

        // Check for string functions
        if (IsStringFunction(operatorToken, out var stringFunction))
        {
            if (index >= tokens.Count)
            {
                throw new FilterParseException(
                    $"Expected value after string function '{operatorToken}'");
            }

            var value = tokens[index++];
            return new StringFunctionExpression(property, stringFunction, value);
        }

        // Check for comparison operators
        if (IsComparisonOperator(operatorToken, out var comparisonOperator))
        {
            if (index >= tokens.Count)
            {
                throw new FilterParseException(
                    $"Expected value after operator '{operatorToken}'");
            }

            var value = ParseValue(tokens[index++]);
            return new ComparisonExpression(property, comparisonOperator, value);
        }

        throw new FilterParseException($"Unknown operator '{operatorToken}'");
    }

    /// <summary>
    /// Parses a value token into an appropriate object type.
    /// </summary>
    private static object ParseValue(string token)
    {
        // Boolean
        if (token.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (token.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Null
        if (token.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null!;
        }

        // Numeric - try int first, then double
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue;
        }

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        // DateTime - support ISO 8601 format
        if (DateTime.TryParse(token, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal,
            out var dateValue))
        {
            return dateValue;
        }

        // Default to string
        return token;
    }

    /// <summary>
    /// Checks if the token is a specific operator (case-insensitive).
    /// </summary>
    private static bool IsOperator(string token, string expectedOperator)
    {
        return token.Equals(expectedOperator, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the token is a comparison operator and returns the corresponding enum value.
    /// </summary>
    private static bool IsComparisonOperator(string token, out ComparisonOperator op)
    {
        switch (token.ToLowerInvariant())
        {
            case "eq":
                op = ComparisonOperator.Eq;
                return true;
            case "ne":
                op = ComparisonOperator.Ne;
                return true;
            case "gt":
                op = ComparisonOperator.Gt;
                return true;
            case "ge":
                op = ComparisonOperator.Ge;
                return true;
            case "lt":
                op = ComparisonOperator.Lt;
                return true;
            case "le":
                op = ComparisonOperator.Le;
                return true;
            default:
                op = default;
                return false;
        }
    }

    /// <summary>
    /// Checks if the token is a string function and returns the corresponding enum value.
    /// </summary>
    private static bool IsStringFunction(string token, out StringFunction func)
    {
        switch (token.ToLowerInvariant())
        {
            case "contains":
                func = StringFunction.Contains;
                return true;
            case "startswith":
                func = StringFunction.StartsWith;
                return true;
            case "endswith":
                func = StringFunction.EndsWith;
                return true;
            default:
                func = default;
                return false;
        }
    }
}

/// <summary>
/// Exception thrown when parsing a filter expression fails.
/// </summary>
/// <remarks>
/// This exception should be caught by the FilterQueryActionFilter and converted
/// to a 400 Bad Request response with a user-friendly error message.
/// <para>
/// <b>Example handling:</b>
/// </para>
/// <code>
/// try
/// {
///     var expression = parser.Parse(filter);
/// }
/// catch (FilterParseException ex)
/// {
///     return new BadRequestObjectResult(new ProblemDetails
///     {
///         Status = 400,
///         Title = "Invalid filter expression",
///         Detail = ex.Message
///     });
/// }
/// </code>
/// </remarks>
public sealed class FilterParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParseException"/> class.
    /// </summary>
    /// <param name="message">The error message describing the parse failure.</param>
    public FilterParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParseException"/> class.
    /// </summary>
    /// <param name="message">The error message describing the parse failure.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public FilterParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
