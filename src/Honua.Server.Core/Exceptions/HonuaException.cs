// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for all Honua domain exceptions.
/// </summary>
public abstract class HonuaException : Exception
{
    protected HonuaException(string message) : base(message)
    {
    }

    protected HonuaException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
