// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Provides extension methods for collection operations.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Determines whether a collection is null or empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to check.</param>
    /// <returns>True if the collection is null or contains no elements; otherwise, false.</returns>
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection)
    {
        return collection == null || !collection.Any();
    }

    /// <summary>
    /// Determines whether a collection is null or empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to check.</param>
    /// <returns>True if the collection is null or contains no elements; otherwise, false.</returns>
    /// <remarks>
    /// This overload is optimized for ICollection{T} and uses the Count property instead of Any(),
    /// which can be more efficient for collections that maintain a count.
    /// </remarks>
    public static bool IsNullOrEmpty<T>(this ICollection<T>? collection)
    {
        return collection == null || collection.Count == 0;
    }

    /// <summary>
    /// Determines whether a read-only collection is null or empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to check.</param>
    /// <returns>True if the collection is null or contains no elements; otherwise, false.</returns>
    /// <remarks>
    /// This overload is optimized for IReadOnlyCollection{T} and uses the Count property instead of Any(),
    /// which can be more efficient for collections that maintain a count.
    /// </remarks>
    public static bool IsNullOrEmpty<T>(this IReadOnlyCollection<T>? collection)
    {
        return collection == null || collection.Count == 0;
    }

    /// <summary>
    /// Determines whether a list is null or empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to check.</param>
    /// <returns>True if the list is null or contains no elements; otherwise, false.</returns>
    /// <remarks>
    /// This overload is optimized for IList{T} and uses the Count property instead of Any(),
    /// which can be more efficient for lists.
    /// </remarks>
    public static bool IsNullOrEmpty<T>(this IList<T>? list)
    {
        return list == null || list.Count == 0;
    }

    /// <summary>
    /// Determines whether a read-only list is null or empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to check.</param>
    /// <returns>True if the list is null or contains no elements; otherwise, false.</returns>
    /// <remarks>
    /// This overload is optimized for IReadOnlyList{T} and uses the Count property instead of Any(),
    /// which can be more efficient for lists.
    /// </remarks>
    public static bool IsNullOrEmpty<T>(this IReadOnlyList<T>? list)
    {
        return list == null || list.Count == 0;
    }
}
