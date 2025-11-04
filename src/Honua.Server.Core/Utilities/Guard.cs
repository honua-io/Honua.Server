// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Utilities;

/// <summary>
/// Provides guard clauses for argument validation to eliminate boilerplate null checks.
/// Uses CallerArgumentExpression for automatic parameter name capture.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Throws ArgumentNullException if the value is null.
    /// </summary>
    /// <returns>The non-null value.</returns>
    [return: NotNull]
    public static T NotNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws ArgumentNullException if the string is null or whitespace.
    /// </summary>
    /// <returns>The non-null, non-whitespace string.</returns>
    [return: NotNull]
    public static string NotNullOrWhiteSpace(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value.IsNullOrWhiteSpace())
        {
            throw new ArgumentNullException(paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws ArgumentNullException if the string is null or empty.
    /// </summary>
    /// <returns>The non-null, non-empty string.</returns>
    [return: NotNull]
    public static string NotNullOrEmpty(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value.IsNullOrEmpty())
        {
            throw new ArgumentNullException(paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws ArgumentException if the condition is false.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="message">The error message to include in the exception.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    public static void Require(
        bool condition,
        string message,
        [CallerArgumentExpression(nameof(condition))] string? paramName = null)
    {
        if (!condition)
        {
            throw new ArgumentException(message, paramName);
        }
    }
}
