// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Host.Filtering;

/// <summary>
/// Base class for filter expressions in OData-style query filtering.
/// Represents a parsed filter expression that can be applied to IQueryable data sources.
/// </summary>
/// <remarks>
/// Filter expressions form a tree structure supporting:
/// <list type="bullet">
/// <item><description>Comparison operations (eq, ne, gt, ge, lt, le)</description></item>
/// <item><description>Logical operations (and, or, not)</description></item>
/// <item><description>String functions (contains, startswith, endswith)</description></item>
/// </list>
/// <para>
/// <b>Example filter expressions:</b>
/// </para>
/// <code>
/// // Simple comparison
/// createdAt gt 2025-01-01
///
/// // Logical combination
/// createdAt gt 2025-01-01 and status eq 'active'
///
/// // String function
/// name contains 'test'
///
/// // Negation
/// not (isActive eq false)
/// </code>
/// </remarks>
public abstract record FilterExpression;

/// <summary>
/// Represents a comparison expression comparing a property to a constant value.
/// </summary>
/// <param name="Property">The property name to compare (e.g., "createdAt", "status").</param>
/// <param name="Operator">The comparison operator to apply.</param>
/// <param name="Value">The constant value to compare against (will be converted to property type).</param>
/// <remarks>
/// <b>Examples:</b>
/// <code>
/// // Equality
/// status eq 'active'  → ComparisonExpression("status", Eq, "active")
///
/// // Numeric comparison
/// age gt 18          → ComparisonExpression("age", Gt, 18)
///
/// // Date comparison
/// createdAt ge 2025-01-01 → ComparisonExpression("createdAt", Ge, "2025-01-01")
///
/// // Boolean comparison
/// isActive eq true   → ComparisonExpression("isActive", Eq, true)
/// </code>
/// </remarks>
public sealed record ComparisonExpression(
    string Property,
    ComparisonOperator Operator,
    object Value) : FilterExpression;

/// <summary>
/// Represents a logical expression combining two filter expressions.
/// </summary>
/// <param name="Left">The left-hand side filter expression.</param>
/// <param name="Operator">The logical operator (And or Or).</param>
/// <param name="Right">The right-hand side filter expression.</param>
/// <remarks>
/// <b>Examples:</b>
/// <code>
/// // AND combination
/// createdAt gt 2025-01-01 and status eq 'active'
/// → LogicalExpression(
///     ComparisonExpression("createdAt", Gt, "2025-01-01"),
///     And,
///     ComparisonExpression("status", Eq, "active")
///   )
///
/// // OR combination
/// status eq 'active' or status eq 'pending'
/// → LogicalExpression(
///     ComparisonExpression("status", Eq, "active"),
///     Or,
///     ComparisonExpression("status", Eq, "pending")
///   )
///
/// // Nested combinations
/// (status eq 'active' and priority gt 5) or isUrgent eq true
/// → LogicalExpression(
///     LogicalExpression(...),
///     Or,
///     ComparisonExpression(...)
///   )
/// </code>
/// </remarks>
public sealed record LogicalExpression(
    FilterExpression Left,
    LogicalOperator Operator,
    FilterExpression Right) : FilterExpression;

/// <summary>
/// Represents a negation expression that inverts the result of a filter expression.
/// </summary>
/// <param name="Expression">The filter expression to negate.</param>
/// <remarks>
/// <b>Examples:</b>
/// <code>
/// // Simple negation
/// not (isActive eq false)
/// → NotExpression(ComparisonExpression("isActive", Eq, false))
///
/// // Negating complex expressions
/// not (status eq 'active' and age gt 18)
/// → NotExpression(LogicalExpression(...))
/// </code>
/// </remarks>
public sealed record NotExpression(FilterExpression Expression) : FilterExpression;

/// <summary>
/// Represents a string function expression for pattern matching on string properties.
/// </summary>
/// <param name="Property">The string property to search.</param>
/// <param name="Function">The string function to apply (Contains, StartsWith, or EndsWith).</param>
/// <param name="Value">The search string value.</param>
/// <remarks>
/// <b>Examples:</b>
/// <code>
/// // Contains (case-insensitive by default)
/// name contains 'test'
/// → StringFunctionExpression("name", Contains, "test")
///
/// // Starts with
/// email startswith 'admin'
/// → StringFunctionExpression("email", StartsWith, "admin")
///
/// // Ends with
/// filename endswith '.pdf'
/// → StringFunctionExpression("filename", EndsWith, ".pdf")
/// </code>
/// <para>
/// <b>Performance note:</b> String functions may not use indexes efficiently.
/// Consider using full-text search for complex text queries.
/// </para>
/// </remarks>
public sealed record StringFunctionExpression(
    string Property,
    StringFunction Function,
    string Value) : FilterExpression;

/// <summary>
/// Comparison operators for comparing property values.
/// Maps to OData comparison operators.
/// </summary>
/// <remarks>
/// <b>OData Operator Mapping:</b>
/// <list type="table">
/// <listheader>
/// <term>Operator</term>
/// <description>OData Syntax</description>
/// <description>Example</description>
/// </listheader>
/// <item>
/// <term>Eq</term>
/// <description>eq</description>
/// <description>status eq 'active'</description>
/// </item>
/// <item>
/// <term>Ne</term>
/// <description>ne</description>
/// <description>status ne 'deleted'</description>
/// </item>
/// <item>
/// <term>Gt</term>
/// <description>gt</description>
/// <description>age gt 18</description>
/// </item>
/// <item>
/// <term>Ge</term>
/// <description>ge</description>
/// <description>age ge 18</description>
/// </item>
/// <item>
/// <term>Lt</term>
/// <description>lt</description>
/// <description>price lt 100</description>
/// </item>
/// <item>
/// <term>Le</term>
/// <description>le</description>
/// <description>price le 100</description>
/// </item>
/// </list>
/// </remarks>
public enum ComparisonOperator
{
    /// <summary>Equal to (eq)</summary>
    Eq,

    /// <summary>Not equal to (ne)</summary>
    Ne,

    /// <summary>Greater than (gt)</summary>
    Gt,

    /// <summary>Greater than or equal to (ge)</summary>
    Ge,

    /// <summary>Less than (lt)</summary>
    Lt,

    /// <summary>Less than or equal to (le)</summary>
    Le
}

/// <summary>
/// Logical operators for combining filter expressions.
/// </summary>
/// <remarks>
/// <b>Usage:</b>
/// <list type="bullet">
/// <item><description><b>And:</b> Both conditions must be true</description></item>
/// <item><description><b>Or:</b> At least one condition must be true</description></item>
/// </list>
/// <para>
/// <b>Operator precedence:</b> NOT &gt; AND &gt; OR
/// Use parentheses to override default precedence.
/// </para>
/// </remarks>
public enum LogicalOperator
{
    /// <summary>Logical AND - both conditions must be true</summary>
    And,

    /// <summary>Logical OR - at least one condition must be true</summary>
    Or
}

/// <summary>
/// String functions for pattern matching on string properties.
/// </summary>
/// <remarks>
/// All string comparisons are case-insensitive by default for consistent behavior
/// across different database providers (SQL Server, PostgreSQL, MySQL, SQLite).
/// <para>
/// <b>Examples:</b>
/// </para>
/// <code>
/// // Contains - matches substring anywhere
/// name contains 'test'     → matches "Test User", "testing", "latest"
///
/// // StartsWith - matches prefix
/// email startswith 'admin' → matches "admin@example.com", "administrator"
///
/// // EndsWith - matches suffix
/// filename endswith '.pdf' → matches "report.pdf", "document.PDF"
/// </code>
/// </remarks>
public enum StringFunction
{
    /// <summary>Contains - checks if property contains the specified substring</summary>
    Contains,

    /// <summary>StartsWith - checks if property starts with the specified prefix</summary>
    StartsWith,

    /// <summary>EndsWith - checks if property ends with the specified suffix</summary>
    EndsWith
}
