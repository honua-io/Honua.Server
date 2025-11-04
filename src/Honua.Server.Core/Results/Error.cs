// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Results;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new("none", string.Empty);

    public static Error NotFound(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new Error("not_found", message);
    }

    public static Error Invalid(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new Error("invalid", message);
    }

    public static Error Conflict(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new Error("conflict", message);
    }
}
