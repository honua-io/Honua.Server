// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for metadata operation errors.
/// </summary>
public class MetadataException : HonuaException
{
    public MetadataException(string message) : base(message, "METADATA_ERROR")
    {
    }

    public MetadataException(string message, Exception innerException) : base(message, "METADATA_ERROR", innerException)
    {
    }

    public MetadataException(string message, string? errorCode) : base(message, errorCode)
    {
    }

    public MetadataException(string message, string? errorCode, Exception innerException) : base(message, errorCode, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when metadata is not found.
/// </summary>
public class MetadataNotFoundException : MetadataException
{
    public MetadataNotFoundException(string message) : base(message)
    {
    }

    public MetadataNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a service is not found in metadata.
/// </summary>
public sealed class ServiceNotFoundException : MetadataNotFoundException
{
    public string ServiceId { get; }

    public ServiceNotFoundException(string serviceId)
        : base($"Service '{serviceId}' was not found in metadata.")
    {
        ServiceId = serviceId;
    }
}

/// <summary>
/// Exception thrown when a layer is not found in metadata.
/// </summary>
public sealed class LayerNotFoundException : MetadataNotFoundException
{
    public string? ServiceId { get; }
    public string LayerId { get; }

    public LayerNotFoundException(string layerId)
        : base($"Layer '{layerId}' was not found in metadata.")
    {
        LayerId = layerId;
    }

    public LayerNotFoundException(string serviceId, string layerId)
        : base($"Layer '{layerId}' was not found for service '{serviceId}'.")
    {
        ServiceId = serviceId;
        LayerId = layerId;
    }
}

/// <summary>
/// Exception thrown when a style is not found in metadata.
/// </summary>
public sealed class StyleNotFoundException : MetadataNotFoundException
{
    public string StyleId { get; }

    public StyleNotFoundException(string styleId)
        : base($"Style '{styleId}' was not found in metadata.")
    {
        StyleId = styleId;
    }
}

/// <summary>
/// Exception thrown when a data source is not found in metadata.
/// </summary>
public sealed class DataSourceNotFoundException : MetadataNotFoundException
{
    public string DataSourceId { get; }

    public DataSourceNotFoundException(string dataSourceId)
        : base($"Data source '{dataSourceId}' does not exist in metadata.")
    {
        DataSourceId = dataSourceId;
    }
}

/// <summary>
/// Exception thrown when a folder is not found in metadata.
/// </summary>
public sealed class FolderNotFoundException : MetadataNotFoundException
{
    public string FolderId { get; }

    public FolderNotFoundException(string folderId)
        : base($"Folder '{folderId}' does not exist in metadata.")
    {
        FolderId = folderId;
    }
}

/// <summary>
/// Exception thrown when metadata validation fails.
/// </summary>
public sealed class MetadataValidationException : MetadataException
{
    public MetadataValidationException(string message) : base(message, "METADATA_VALIDATION_FAILED")
    {
    }

    public MetadataValidationException(string message, Exception innerException) : base(message, "METADATA_VALIDATION_FAILED", innerException)
    {
    }
}

/// <summary>
/// Exception thrown when metadata configuration is invalid or missing.
/// </summary>
public sealed class MetadataConfigurationException : MetadataException
{
    public MetadataConfigurationException(string message) : base(message, "METADATA_CONFIGURATION_ERROR")
    {
    }

    public MetadataConfigurationException(string message, Exception innerException) : base(message, "METADATA_CONFIGURATION_ERROR", innerException)
    {
    }
}
