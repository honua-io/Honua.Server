// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Honua.Server.Core.Performance;

/// <summary>
/// High-performance span-based utilities for parsing and string operations.
/// Reduces allocations by working directly on memory without creating intermediate strings.
/// </summary>
public static class SpanExtensions
{
    /// <summary>
    /// Parses a double from a span of characters with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDouble(ReadOnlySpan<char> span, out double result)
    {
#if NET7_0_OR_GREATER
        return double.TryParse(span, out result);
#else
        // Fallback for older frameworks - requires allocation
        return double.TryParse(span.ToString(), out result);
#endif
    }

    /// <summary>
    /// Parses an integer from a span of characters with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseInt32(ReadOnlySpan<char> span, out int result)
    {
#if NET7_0_OR_GREATER
        return int.TryParse(span, out result);
#else
        return int.TryParse(span.ToString(), out result);
#endif
    }

    /// <summary>
    /// Counts occurrences of a character in a span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count(this ReadOnlySpan<char> span, char value)
    {
        int count = 0;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == value)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Splits a span by a delimiter character with minimal allocations.
    /// Uses a pooled array for the results.
    /// </summary>
    public static void Split(ReadOnlySpan<char> span, char delimiter, Span<Range> destination, out int count)
    {
        count = 0;
        int start = 0;

        for (int i = 0; i < span.Length && count < destination.Length; i++)
        {
            if (span[i] == delimiter)
            {
                destination[count++] = new Range(start, i);
                start = i + 1;
            }
        }

        // Add the last segment if there's room
        if (count < destination.Length && start < span.Length)
        {
            destination[count++] = new Range(start, span.Length);
        }
    }

    /// <summary>
    /// Trims whitespace from both ends of a span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> TrimWhitespace(this ReadOnlySpan<char> span)
    {
        return span.Trim();
    }

    /// <summary>
    /// Checks if a span starts with a prefix (ordinal comparison).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartsWithOrdinal(this ReadOnlySpan<char> span, ReadOnlySpan<char> prefix)
    {
        return span.StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if a span ends with a suffix (ordinal comparison).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EndsWithOrdinal(this ReadOnlySpan<char> span, ReadOnlySpan<char> suffix)
    {
        return span.EndsWith(suffix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Case-insensitive equality comparison for spans.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsIgnoreCase(this ReadOnlySpan<char> span, ReadOnlySpan<char> other)
    {
        return span.Equals(other, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies coordinate data efficiently using Span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyCoordinates(ReadOnlySpan<double> source, Span<double> destination)
    {
        source.CopyTo(destination);
    }

    /// <summary>
    /// Reverses byte order in-place for endianness conversion.
    /// Optimized for common sizes (2, 4, 8 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReverseEndianness(Span<byte> bytes, int elementSize)
    {
        switch (elementSize)
        {
            case 2:
                for (int i = 0; i < bytes.Length; i += 2)
                {
                    (bytes[i], bytes[i + 1]) = (bytes[i + 1], bytes[i]);
                }
                break;

            case 4:
                for (int i = 0; i < bytes.Length; i += 4)
                {
                    (bytes[i], bytes[i + 3]) = (bytes[i + 3], bytes[i]);
                    (bytes[i + 1], bytes[i + 2]) = (bytes[i + 2], bytes[i + 1]);
                }
                break;

            case 8:
                for (int i = 0; i < bytes.Length; i += 8)
                {
                    (bytes[i], bytes[i + 7]) = (bytes[i + 7], bytes[i]);
                    (bytes[i + 1], bytes[i + 6]) = (bytes[i + 6], bytes[i + 1]);
                    (bytes[i + 2], bytes[i + 5]) = (bytes[i + 5], bytes[i + 2]);
                    (bytes[i + 3], bytes[i + 4]) = (bytes[i + 4], bytes[i + 3]);
                }
                break;

            default:
                // Generic reversal for other sizes
                for (int i = 0; i < bytes.Length; i += elementSize)
                {
                    var slice = bytes.Slice(i, elementSize);
                    slice.Reverse();
                }
                break;
        }
    }
}
