// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for data store operation errors.
/// </summary>
public class DataStoreException : HonuaException
{
    public DataStoreException(string message) : base(message)
    {
    }

    public DataStoreException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public DataStoreException(string message, string? errorCode) : base(message, errorCode)
    {
    }

    public DataStoreException(string message, string? errorCode, Exception innerException) : base(message, errorCode, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a data store connection fails.
/// This is a transient error that can be retried.
/// </summary>
public sealed class DataStoreConnectionException : DataStoreException, ITransientException
{
    public string? DataSourceId { get; }
    public bool IsTransient => true;

    public DataStoreConnectionException(string message)
        : base(message, "DATA_STORE_CONNECTION_FAILED")
    {
    }

    public DataStoreConnectionException(string message, Exception innerException)
        : base(message, "DATA_STORE_CONNECTION_FAILED", innerException)
    {
    }

    public DataStoreConnectionException(string dataSourceId, string message, Exception innerException)
        : base($"Failed to connect to data source '{dataSourceId}': {message}", "DATA_STORE_CONNECTION_FAILED", innerException)
    {
        DataSourceId = dataSourceId;
    }
}

/// <summary>
/// Exception thrown when a data store operation times out.
/// This is a transient error that can be retried.
/// </summary>
public sealed class DataStoreTimeoutException : DataStoreException, ITransientException
{
    public string? Operation { get; }
    public bool IsTransient => true;

    public DataStoreTimeoutException(string message)
        : base(message, "DATA_STORE_TIMEOUT")
    {
    }

    public DataStoreTimeoutException(string operation, string message, Exception innerException)
        : base($"Data store operation '{operation}' timed out: {message}", "DATA_STORE_TIMEOUT", innerException)
    {
        Operation = operation;
    }
}

/// <summary>
/// Exception thrown when a data store is temporarily unavailable.
/// This is a transient error that can be retried.
/// </summary>
public sealed class DataStoreUnavailableException : DataStoreException, ITransientException
{
    public string? DataSourceId { get; }
    public bool IsTransient => true;

    public DataStoreUnavailableException(string message)
        : base(message, "DATA_STORE_UNAVAILABLE")
    {
    }

    public DataStoreUnavailableException(string dataSourceId, string message)
        : base($"Data source '{dataSourceId}' is unavailable: {message}", "DATA_STORE_UNAVAILABLE")
    {
        DataSourceId = dataSourceId;
    }

    public DataStoreUnavailableException(string dataSourceId, string message, Exception innerException)
        : base($"Data source '{dataSourceId}' is unavailable: {message}", "DATA_STORE_UNAVAILABLE", innerException)
    {
        DataSourceId = dataSourceId;
    }
}

/// <summary>
/// Exception thrown when a data store constraint violation occurs.
/// This is NOT a transient error.
/// </summary>
public sealed class DataStoreConstraintException : DataStoreException
{
    public string? ConstraintName { get; }

    public DataStoreConstraintException(string message)
        : base(message, "DATA_STORE_CONSTRAINT_VIOLATION")
    {
    }

    public DataStoreConstraintException(string constraintName, string message)
        : base(message, "DATA_STORE_CONSTRAINT_VIOLATION")
    {
        ConstraintName = constraintName;
    }

    public DataStoreConstraintException(string message, Exception innerException)
        : base(message, "DATA_STORE_CONSTRAINT_VIOLATION", innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a data store deadlock is detected.
/// This is a transient error that can be retried.
/// </summary>
public sealed class DataStoreDeadlockException : DataStoreException, ITransientException
{
    public bool IsTransient => true;

    public DataStoreDeadlockException(string message)
        : base(message, "DATA_STORE_DEADLOCK")
    {
    }

    public DataStoreDeadlockException(string message, Exception innerException)
        : base(message, "DATA_STORE_DEADLOCK", innerException)
    {
    }
}
