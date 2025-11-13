// <copyright file="StringValidationExtensions.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Honua.Server.AlertReceiver.Extensions;

/// <summary>
/// Extension methods for string validation to eliminate verbose null/empty checks.
/// </summary>
public static class StringValidationExtensions
{
    /// <summary>
    /// Indicates whether a string is null, empty, or consists only of white-space characters.
    /// </summary>
    
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Indicates whether a string is null or empty.
    /// </summary>
    
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Indicates whether a string has a value (not null, empty, or whitespace).
    /// Inverse of IsNullOrWhiteSpace for more readable positive checks.
    /// </summary>
    
    public static bool HasValue([NotNullWhen(true)] this string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }
}
