// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for raster processing errors.
/// </summary>
public class RasterException : HonuaException
{
    public RasterException(string message) : base(message)
    {
    }

    public RasterException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when raster processing fails transiently.
/// </summary>
public sealed class RasterProcessingException : RasterException, ITransientException
{
    public bool IsTransient { get; }

    public RasterProcessingException(string message, bool isTransient = true)
        : base(message)
    {
        IsTransient = isTransient;
    }

    public RasterProcessingException(string message, Exception innerException, bool isTransient = true)
        : base(message, innerException)
    {
        IsTransient = isTransient;
    }
}

/// <summary>
/// Exception thrown when a raster source is not found.
/// This is NOT a transient error.
/// </summary>
public sealed class RasterSourceNotFoundException : RasterException
{
    public string SourcePath { get; }

    public RasterSourceNotFoundException(string sourcePath)
        : base($"Raster source '{sourcePath}' was not found.")
    {
        SourcePath = sourcePath;
    }
}

/// <summary>
/// Exception thrown when raster format is not supported.
/// This is NOT a transient error.
/// </summary>
public sealed class UnsupportedRasterFormatException : RasterException
{
    public string Format { get; }

    public UnsupportedRasterFormatException(string format)
        : base($"Raster format '{format}' is not supported.")
    {
        Format = format;
    }
}
