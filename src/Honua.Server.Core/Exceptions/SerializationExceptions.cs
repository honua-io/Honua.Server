// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Exception thrown when geometry serialization fails.
/// </summary>
public sealed class GeometrySerializationException : HonuaException
{
    public string? GeometryType { get; }

    public GeometrySerializationException(string message) : base(message, ErrorCodes.GEOMETRY_SERIALIZATION_FAILED)
    {
    }

    public GeometrySerializationException(string message, Exception innerException) : base(message, ErrorCodes.GEOMETRY_SERIALIZATION_FAILED, innerException)
    {
    }

    public GeometrySerializationException(string geometryType, string message)
        : base(message, ErrorCodes.GEOMETRY_SERIALIZATION_FAILED)
    {
        GeometryType = geometryType;
    }

    public GeometrySerializationException(string geometryType, string message, Exception innerException)
        : base(message, ErrorCodes.GEOMETRY_SERIALIZATION_FAILED, innerException)
    {
        GeometryType = geometryType;
    }
}

/// <summary>
/// Exception thrown when feature serialization fails.
/// </summary>
public sealed class FeatureSerializationException : HonuaException
{
    public string? Format { get; }

    public FeatureSerializationException(string message) : base(message, ErrorCodes.FEATURE_SERIALIZATION_FAILED)
    {
    }

    public FeatureSerializationException(string message, Exception innerException) : base(message, ErrorCodes.FEATURE_SERIALIZATION_FAILED, innerException)
    {
    }

    public FeatureSerializationException(string format, string message)
        : base(message, ErrorCodes.FEATURE_SERIALIZATION_FAILED)
    {
        Format = format;
    }

    public FeatureSerializationException(string format, string message, Exception innerException)
        : base(message, ErrorCodes.FEATURE_SERIALIZATION_FAILED, innerException)
    {
        Format = format;
    }
}
