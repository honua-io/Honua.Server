// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Exception thrown when a data operation fails.
/// </summary>
public class DataException : HonuaException
{
    public DataException(string message) : base(message)
    {
    }

    public DataException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a feature is not found.
/// </summary>
public sealed class FeatureNotFoundException : DataException
{
    public string FeatureId { get; }
    public string? LayerId { get; }

    public FeatureNotFoundException(string featureId, string? layerId = null)
        : base(layerId is null
            ? $"Feature '{featureId}' was not found."
            : $"Feature '{featureId}' was not found in layer '{layerId}'.")
    {
        FeatureId = featureId;
        LayerId = layerId;
    }
}

/// <summary>
/// Exception thrown when feature validation fails.
/// </summary>
public sealed class FeatureValidationException : DataException
{
    public FeatureValidationException(string message) : base(message)
    {
    }

    public FeatureValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a data store provider is not found or cannot be created.
/// </summary>
public sealed class DataStoreProviderException : DataException
{
    public string? ProviderName { get; }

    public DataStoreProviderException(string message) : base(message)
    {
    }

    public DataStoreProviderException(string providerName, string message)
        : base(message)
    {
        ProviderName = providerName;
    }

    public DataStoreProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a connection string is invalid or missing.
/// </summary>
public sealed class ConnectionStringException : DataException
{
    public string? DataSourceId { get; }

    public ConnectionStringException(string message) : base(message)
    {
    }

    public ConnectionStringException(string dataSourceId, string message)
        : base(message)
    {
        DataSourceId = dataSourceId;
    }
}

/// <summary>
/// Exception thrown when a concurrent update conflict is detected.
/// This occurs when an optimistic locking check fails during an update operation.
/// </summary>
public sealed class ConcurrencyException : DataException
{
    /// <summary>
    /// Gets the identifier of the entity that experienced the concurrency conflict.
    /// </summary>
    public string? EntityId { get; }

    /// <summary>
    /// Gets the type of entity (e.g., "Feature", "Collection", "Metadata").
    /// </summary>
    public string? EntityType { get; }

    /// <summary>
    /// Gets the expected version that was provided in the update request.
    /// </summary>
    public object? ExpectedVersion { get; }

    /// <summary>
    /// Gets the actual current version in the database.
    /// </summary>
    public object? ActualVersion { get; }

    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ConcurrencyException(
        string entityId,
        string entityType,
        object? expectedVersion,
        object? actualVersion)
        : base($"Concurrency conflict for {entityType} '{entityId}'. The resource has been modified by another user. Expected version: {expectedVersion}, Actual version: {actualVersion}.")
    {
        EntityId = entityId;
        EntityType = entityType;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public ConcurrencyException(
        string entityId,
        string entityType,
        object? expectedVersion,
        object? actualVersion,
        Exception innerException)
        : base($"Concurrency conflict for {entityType} '{entityId}'. The resource has been modified by another user. Expected version: {expectedVersion}, Actual version: {actualVersion}.", innerException)
    {
        EntityId = entityId;
        EntityType = entityType;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}
