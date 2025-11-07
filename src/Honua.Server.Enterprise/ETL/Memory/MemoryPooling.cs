// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.Memory;

/// <summary>
/// Memory pooling utilities for reducing allocations
/// </summary>
public static class MemoryPooling
{
    private static readonly ArrayPool<byte> ByteArrayPool = ArrayPool<byte>.Shared;
    private static readonly ArrayPool<double> DoubleArrayPool = ArrayPool<double>.Shared;

    /// <summary>
    /// Rents a byte array from the pool
    /// </summary>
    public static byte[] RentByteArray(int minimumLength)
    {
        return ByteArrayPool.Rent(minimumLength);
    }

    /// <summary>
    /// Returns a byte array to the pool
    /// </summary>
    public static void ReturnByteArray(byte[] array, bool clearArray = false)
    {
        ByteArrayPool.Return(array, clearArray);
    }

    /// <summary>
    /// Rents a double array from the pool
    /// </summary>
    public static double[] RentDoubleArray(int minimumLength)
    {
        return DoubleArrayPool.Rent(minimumLength);
    }

    /// <summary>
    /// Returns a double array to the pool
    /// </summary>
    public static void ReturnDoubleArray(double[] array, bool clearArray = false)
    {
        DoubleArrayPool.Return(array, clearArray);
    }

    /// <summary>
    /// Rents memory from the shared memory pool
    /// </summary>
    public static IMemoryOwner<T> RentMemory<T>(int minimumLength)
    {
        return MemoryPool<T>.Shared.Rent(minimumLength);
    }
}

/// <summary>
/// Object pooling for frequently allocated workflow objects
/// </summary>
public class WorkflowObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly ILogger? _logger;
    private readonly int _maxPoolSize;
    private int _currentSize;

    public WorkflowObjectPool(int maxPoolSize = 100, ILogger? logger = null)
    {
        _maxPoolSize = maxPoolSize;
        _logger = logger;
    }

    /// <summary>
    /// Gets an object from the pool or creates a new one
    /// </summary>
    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            _logger?.LogTrace("Rented object from pool, remaining: {Count}", _currentSize - 1);
            System.Threading.Interlocked.Decrement(ref _currentSize);
            return item;
        }

        _logger?.LogTrace("Created new pooled object");
        return new T();
    }

    /// <summary>
    /// Returns an object to the pool
    /// </summary>
    public void Return(T item)
    {
        if (_currentSize < _maxPoolSize)
        {
            _pool.Add(item);
            System.Threading.Interlocked.Increment(ref _currentSize);
            _logger?.LogTrace("Returned object to pool, total: {Count}", _currentSize);
        }
        else
        {
            _logger?.LogTrace("Pool full, discarding object");
        }
    }

    /// <summary>
    /// Clears the pool
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out _))
        {
            System.Threading.Interlocked.Decrement(ref _currentSize);
        }

        _logger?.LogInformation("Cleared object pool");
    }

    /// <summary>
    /// Gets the current pool size
    /// </summary>
    public int Count => _currentSize;
}

/// <summary>
/// Disposable wrapper for pooled objects that returns them automatically
/// </summary>
public class PooledObject<T> : IDisposable where T : class
{
    private readonly T _value;
    private readonly Action<T> _returnAction;
    private bool _disposed;

    public PooledObject(T value, Action<T> returnAction)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
        _returnAction = returnAction ?? throw new ArgumentNullException(nameof(returnAction));
    }

    public T Value => _value;

    public void Dispose()
    {
        if (!_disposed)
        {
            _returnAction(_value);
            _disposed = true;
        }
    }
}

/// <summary>
/// Memory monitoring and pressure management
/// </summary>
public class MemoryPressureManager
{
    private readonly ILogger<MemoryPressureManager> _logger;
    private readonly long _maxMemoryBytes;
    private long _currentMemoryBytes;

    public MemoryPressureManager(ILogger<MemoryPressureManager> logger, long maxMemoryMB = 512)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxMemoryBytes = maxMemoryMB * 1024 * 1024;
    }

    /// <summary>
    /// Checks if there's enough memory available
    /// </summary>
    public bool CanAllocate(long bytes)
    {
        var newTotal = System.Threading.Interlocked.Read(ref _currentMemoryBytes) + bytes;
        return newTotal <= _maxMemoryBytes;
    }

    /// <summary>
    /// Records memory allocation
    /// </summary>
    public void RecordAllocation(long bytes)
    {
        var newTotal = System.Threading.Interlocked.Add(ref _currentMemoryBytes, bytes);

        if (newTotal > _maxMemoryBytes * 0.9)
        {
            _logger.LogWarning(
                "Memory usage is high: {CurrentMB}MB / {MaxMB}MB",
                newTotal / 1024 / 1024,
                _maxMemoryBytes / 1024 / 1024);
        }
    }

    /// <summary>
    /// Records memory deallocation
    /// </summary>
    public void RecordDeallocation(long bytes)
    {
        System.Threading.Interlocked.Add(ref _currentMemoryBytes, -bytes);
    }

    /// <summary>
    /// Gets current memory usage in bytes
    /// </summary>
    public long CurrentMemoryBytes => System.Threading.Interlocked.Read(ref _currentMemoryBytes);

    /// <summary>
    /// Gets current memory usage percentage
    /// </summary>
    public double MemoryUsagePercentage => (double)CurrentMemoryBytes / _maxMemoryBytes * 100;

    /// <summary>
    /// Triggers garbage collection if memory pressure is high
    /// </summary>
    public void CollectIfNeeded()
    {
        if (MemoryUsagePercentage > 80)
        {
            _logger.LogInformation("Triggering GC due to high memory pressure ({Percentage:F1}%)", MemoryUsagePercentage);
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }
}
