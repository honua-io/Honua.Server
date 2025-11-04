// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Query.Expressions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query.Filter;

/// <summary>
/// Calculates complexity scores for query filters to prevent expensive queries.
/// Scores are based on filter depth, logical operators, and comparison operations.
/// </summary>
public static class FilterComplexityScorer
{
    /// <summary>
    /// Cost per level of nesting depth in logical expressions.
    /// </summary>
    private const int DepthPenalty = 5;

    /// <summary>
    /// Cost for each OR operator (more expensive than AND).
    /// </summary>
    private const int OrOperatorCost = 3;

    /// <summary>
    /// Cost for each AND operator.
    /// </summary>
    private const int AndOperatorCost = 1;

    /// <summary>
    /// Cost for each comparison operator.
    /// </summary>
    private const int ComparisonCost = 1;

    /// <summary>
    /// Cost for each spatial predicate (reserved for future use).
    /// </summary>
    private const int SpatialPredicateCost = 10;

    /// <summary>
    /// Cost for each NOT operator.
    /// </summary>
    private const int NotOperatorCost = 2;

    /// <summary>
    /// Calculates the complexity score for a query filter.
    /// </summary>
    /// <param name="filter">The filter to score. Can be null.</param>
    /// <returns>The complexity score as an integer. Returns 0 for null filters.</returns>
    public static int CalculateComplexity(QueryFilter? filter)
    {
        if (filter?.Expression is null)
        {
            return 0;
        }

        return CalculateExpressionComplexity(filter.Expression, depth: 0);
    }

    /// <summary>
    /// Calculates the complexity score for a query expression.
    /// </summary>
    /// <param name="expression">The expression to score.</param>
    /// <param name="depth">Current nesting depth.</param>
    /// <returns>The complexity score.</returns>
    private static int CalculateExpressionComplexity(QueryExpression expression, int depth)
    {
        Guard.NotNull(expression);

        return expression switch
        {
            QueryBinaryExpression binary => CalculateBinaryComplexity(binary, depth),
            QueryUnaryExpression unary => CalculateUnaryComplexity(unary, depth),
            QueryFunctionExpression function => CalculateFunctionComplexity(function, depth),
            QueryFieldReference => ComparisonCost, // Field reference alone is minimal
            QueryConstant => 0, // Constants are free
            _ => 1 // Unknown expression types have minimal cost
        };
    }

    /// <summary>
    /// Calculates complexity for binary expressions (AND, OR, comparisons).
    /// </summary>
    private static int CalculateBinaryComplexity(QueryBinaryExpression binary, int depth)
    {
        var score = 0;

        // Check if this is a logical operator (AND/OR) or a comparison
        var isLogicalOperator = binary.Operator is QueryBinaryOperator.And or QueryBinaryOperator.Or;

        if (isLogicalOperator)
        {
            // Depth penalty for nested logical operations
            score += depth * DepthPenalty;

            // Operator-specific cost
            score += binary.Operator == QueryBinaryOperator.Or ? OrOperatorCost : AndOperatorCost;

            // Recursively calculate complexity of children with increased depth
            score += CalculateExpressionComplexity(binary.Left, depth + 1);
            score += CalculateExpressionComplexity(binary.Right, depth + 1);
        }
        else
        {
            // This is a comparison operator (=, !=, <, >, etc.)
            score += ComparisonCost;

            // Calculate complexity of operands without increasing depth
            score += CalculateExpressionComplexity(binary.Left, depth);
            score += CalculateExpressionComplexity(binary.Right, depth);
        }

        return score;
    }

    /// <summary>
    /// Calculates complexity for unary expressions (NOT).
    /// </summary>
    private static int CalculateUnaryComplexity(QueryUnaryExpression unary, int depth)
    {
        var score = NotOperatorCost;

        // NOT operations can increase complexity, especially when nested
        if (depth > 0)
        {
            score += depth; // Small penalty for nested NOT
        }

        // Calculate complexity of the operand
        score += CalculateExpressionComplexity(unary.Operand, depth);

        return score;
    }

    /// <summary>
    /// Calculates complexity for function expressions.
    /// </summary>
    private static int CalculateFunctionComplexity(QueryFunctionExpression function, int depth)
    {
        // Functions have a base cost
        var score = ComparisonCost * 2;

        // Add complexity for each argument
        foreach (var arg in function.Arguments)
        {
            score += CalculateExpressionComplexity(arg, depth);
        }

        return score;
    }

    /// <summary>
    /// Gets a human-readable description of what contributes to filter complexity.
    /// </summary>
    public static string GetComplexityDescription()
    {
        return $"Filter complexity scoring: " +
               $"Depth penalty: {DepthPenalty} per level, " +
               $"OR operators: {OrOperatorCost}, " +
               $"AND operators: {AndOperatorCost}, " +
               $"NOT operators: {NotOperatorCost}, " +
               $"Comparisons: {ComparisonCost}, " +
               $"Spatial predicates: {SpatialPredicateCost}";
    }
}
