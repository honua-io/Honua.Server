// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for all Honua domain exceptions.
/// </summary>
public abstract class HonuaException : Exception
{
    /// <summary>
    /// Gets the error code for this exception.
    /// </summary>
    public string? ErrorCode { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether this exception represents a transient failure that can be retried.
    /// </summary>
    public virtual bool IsTransient => this is ITransientException transient && transient.IsTransient;

    protected HonuaException(string message) : base(message)
    {
    }

    protected HonuaException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected HonuaException(string message, string? errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    protected HonuaException(string message, string? errorCode, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
