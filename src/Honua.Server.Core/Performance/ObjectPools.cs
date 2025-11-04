// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.IO;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Honua.Server.Core.Performance;

/// <summary>
/// Centralized object pools for high-performance scenarios.
/// Reduces GC pressure by reusing commonly allocated objects.
/// </summary>
public static class ObjectPools
{
    /// <summary>
    /// Pool for StringBuilder instances used in SQL query building, JSON generation, etc.
    /// Default capacity: 256 characters, max retained capacity: 4096 characters.
    /// </summary>
    public static readonly ObjectPool<StringBuilder> StringBuilder =
        new DefaultObjectPoolProvider().CreateStringBuilderPool(256, 4096);

    /// <summary>
    /// Pool for MemoryStream instances used in buffering operations.
    /// Streams are reset before being returned to the pool.
    /// </summary>
    public static readonly ObjectPool<MemoryStream> MemoryStream =
        new DefaultObjectPoolProvider().Create(new MemoryStreamPooledObjectPolicy());

    /// <summary>
    /// Shared ArrayPool for byte arrays (default shared pool).
    /// Use for temporary buffers in I/O operations, serialization, etc.
    /// </summary>
    public static readonly ArrayPool<byte> ByteArrayPool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Shared ArrayPool for char arrays.
    /// Use for string manipulation, parsing operations.
    /// </summary>
    public static readonly ArrayPool<char> CharArrayPool = ArrayPool<char>.Shared;
}

/// <summary>
/// Policy for pooling MemoryStream instances.
/// Ensures streams are properly reset before being returned to the pool.
/// </summary>
internal sealed class MemoryStreamPooledObjectPolicy : IPooledObjectPolicy<MemoryStream>
{
    private const int DefaultCapacity = 4096;
    private const int MaxRetainedCapacity = 1024 * 1024; // 1MB

    public MemoryStream Create()
    {
        return new MemoryStream(DefaultCapacity);
    }

    public bool Return(MemoryStream obj)
    {
        if (obj == null)
        {
            return false;
        }

        // Don't pool streams that have grown too large
        if (obj.Capacity > MaxRetainedCapacity)
        {
            obj.Dispose();
            return false;
        }

        // Reset stream for reuse
        obj.Position = 0;
        obj.SetLength(0);

        return true;
    }
}
