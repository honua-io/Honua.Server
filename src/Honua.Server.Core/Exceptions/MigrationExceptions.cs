// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Exception thrown when a migration operation fails.
/// </summary>
public class MigrationException : HonuaException
{
    public MigrationException(string message) : base(message)
    {
    }

    public MigrationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when migration validation fails.
/// </summary>
public sealed class MigrationValidationException : MigrationException
{
    public MigrationValidationException(string message) : base(message)
    {
    }

    public MigrationValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a migration source has unsupported features.
/// </summary>
public sealed class UnsupportedMigrationSourceException : MigrationException
{
    public string? SourceType { get; }

    public UnsupportedMigrationSourceException(string message) : base(message)
    {
    }

    public UnsupportedMigrationSourceException(string sourceType, string message)
        : base(message)
    {
        SourceType = sourceType;
    }
}
