// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Provides extension methods for string operations.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Determines whether two strings are equal using case-insensitive ordinal comparison.
    /// </summary>
    /// <param name="str">The first string to compare.</param>
    /// <param name="other">The second string to compare.</param>
    /// <returns>True if the strings are equal (ignoring case); otherwise, false.</returns>
    /// <remarks>
    /// This is a convenience method that wraps string.Equals with StringComparison.OrdinalIgnoreCase.
    /// It provides a more readable API than repeatedly specifying the comparison type.
    ///
    /// Examples:
    /// - "Test".EqualsIgnoreCase("test") returns true
    /// - "Test".EqualsIgnoreCase("TEST") returns true
    /// - "Test".EqualsIgnoreCase("different") returns false
    /// - null.EqualsIgnoreCase(null) returns true
    /// - "Test".EqualsIgnoreCase(null) returns false
    /// </remarks>
    public static bool EqualsIgnoreCase(this string? str, string? other)
    {
        return string.Equals(str, other, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a string contains a specified substring using case-insensitive ordinal comparison.
    /// </summary>
    /// <param name="str">The string to search in.</param>
    /// <param name="value">The substring to search for.</param>
    /// <returns>True if the value parameter occurs within this string (ignoring case); otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when str is null.</exception>
    /// <remarks>
    /// This is a convenience method that wraps string.Contains with StringComparison.OrdinalIgnoreCase.
    /// It provides a more readable API than repeatedly specifying the comparison type.
    ///
    /// Examples:
    /// - "Hello World".ContainsIgnoreCase("hello") returns true
    /// - "Hello World".ContainsIgnoreCase("WORLD") returns true
    /// - "Hello World".ContainsIgnoreCase("xyz") returns false
    /// </remarks>
    public static bool ContainsIgnoreCase(this string str, string value)
    {
        Guard.NotNull(str);

        return str.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the beginning of this string instance matches the specified string using case-insensitive ordinal comparison.
    /// </summary>
    /// <param name="str">The string to check.</param>
    /// <param name="value">The string to compare.</param>
    /// <returns>True if value matches the beginning of this string (ignoring case); otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when str is null.</exception>
    /// <remarks>
    /// This is a convenience method that wraps string.StartsWith with StringComparison.OrdinalIgnoreCase.
    ///
    /// Examples:
    /// - "Hello World".StartsWithIgnoreCase("hello") returns true
    /// - "Hello World".StartsWithIgnoreCase("HELLO") returns true
    /// - "Hello World".StartsWithIgnoreCase("World") returns false
    /// </remarks>
    public static bool StartsWithIgnoreCase(this string str, string value)
    {
        Guard.NotNull(str);

        return str.StartsWith(value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the end of this string instance matches the specified string using case-insensitive ordinal comparison.
    /// </summary>
    /// <param name="str">The string to check.</param>
    /// <param name="value">The string to compare.</param>
    /// <returns>True if value matches the end of this string (ignoring case); otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when str is null.</exception>
    /// <remarks>
    /// This is a convenience method that wraps string.EndsWith with StringComparison.OrdinalIgnoreCase.
    ///
    /// Examples:
    /// - "Hello World".EndsWithIgnoreCase("world") returns true
    /// - "Hello World".EndsWithIgnoreCase("WORLD") returns true
    /// - "Hello World".EndsWithIgnoreCase("Hello") returns false
    /// </remarks>
    public static bool EndsWithIgnoreCase(this string str, string value)
    {
        Guard.NotNull(str);

        return str.EndsWith(value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the zero-based index of the first occurrence of the specified string using case-insensitive ordinal comparison.
    /// </summary>
    /// <param name="str">The string to search in.</param>
    /// <param name="value">The string to search for.</param>
    /// <returns>The zero-based index position of value if that string is found, or -1 if it is not.</returns>
    /// <exception cref="ArgumentNullException">Thrown when str is null.</exception>
    /// <remarks>
    /// This is a convenience method that wraps string.IndexOf with StringComparison.OrdinalIgnoreCase.
    ///
    /// Examples:
    /// - "Hello World".IndexOfIgnoreCase("world") returns 6
    /// - "Hello World".IndexOfIgnoreCase("HELLO") returns 0
    /// - "Hello World".IndexOfIgnoreCase("xyz") returns -1
    /// </remarks>
    public static int IndexOfIgnoreCase(this string str, string value)
    {
        Guard.NotNull(str);

        return str.IndexOf(value, StringComparison.OrdinalIgnoreCase);
    }
}
