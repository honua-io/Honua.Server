// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for cache-related errors.
/// </summary>
public class CacheException : HonuaException
{
    public CacheException(string message) : base(message, ErrorCodes.CACHE_ERROR)
    {
    }

    public CacheException(string message, Exception innerException) : base(message, ErrorCodes.CACHE_ERROR, innerException)
    {
    }

    public CacheException(string message, string? errorCode) : base(message, errorCode)
    {
    }

    public CacheException(string message, string? errorCode, Exception innerException) : base(message, errorCode, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when cache is unavailable but operation can continue with fallback.
/// This is a transient error.
/// </summary>
public sealed class CacheUnavailableException : CacheException, ITransientException
{
    public string CacheName { get; }
    public bool IsTransient => true;

    public CacheUnavailableException(string cacheName, string message)
        : base($"Cache '{cacheName}' is unavailable: {message}", ErrorCodes.CACHE_UNAVAILABLE)
    {
        CacheName = cacheName;
    }

    public CacheUnavailableException(string cacheName, string message, Exception innerException)
        : base($"Cache '{cacheName}' is unavailable: {message}", ErrorCodes.CACHE_UNAVAILABLE, innerException)
    {
        CacheName = cacheName;
    }
}

/// <summary>
/// Exception thrown when a cache key is not found.
/// This is NOT a transient error - the data is missing.
/// </summary>
public sealed class CacheKeyNotFoundException : CacheException
{
    public string Key { get; }

    public CacheKeyNotFoundException(string key)
        : base($"Cache key '{key}' was not found.", ErrorCodes.CACHE_KEY_NOT_FOUND)
    {
        Key = key;
    }
}

/// <summary>
/// Exception thrown when cache write operation fails.
/// This is a transient error.
/// </summary>
public sealed class CacheWriteException : CacheException, ITransientException
{
    public string CacheName { get; }
    public bool IsTransient => true;

    public CacheWriteException(string cacheName, string message, Exception innerException)
        : base($"Failed to write to cache '{cacheName}': {message}", ErrorCodes.CACHE_WRITE_FAILED, innerException)
    {
        CacheName = cacheName;
    }
}

/// <summary>
/// Exception thrown when cache invalidation operation fails.
/// This is critical as it can lead to cache-database inconsistency.
/// </summary>
public sealed class CacheInvalidationException : CacheException, ITransientException
{
    public string CacheName { get; }
    public string CacheKey { get; }
    public bool IsTransient => true;
    public int AttemptNumber { get; }

    public CacheInvalidationException(
        string cacheName,
        string cacheKey,
        string message,
        int attemptNumber = 1)
        : base($"Failed to invalidate cache '{cacheName}' key '{cacheKey}' (attempt {attemptNumber}): {message}", ErrorCodes.CACHE_INVALIDATION_FAILED)
    {
        CacheName = cacheName;
        CacheKey = cacheKey;
        AttemptNumber = attemptNumber;
    }

    public CacheInvalidationException(
        string cacheName,
        string cacheKey,
        string message,
        Exception innerException,
        int attemptNumber = 1)
        : base($"Failed to invalidate cache '{cacheName}' key '{cacheKey}' (attempt {attemptNumber}): {message}", ErrorCodes.CACHE_INVALIDATION_FAILED, innerException)
    {
        CacheName = cacheName;
        CacheKey = cacheKey;
        AttemptNumber = attemptNumber;
    }
}
