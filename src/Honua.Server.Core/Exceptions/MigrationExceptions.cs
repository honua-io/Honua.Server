// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Exception thrown when a migration operation fails.
/// </summary>
public class MigrationException : HonuaException
{
    public MigrationException(string message) : base(message, ErrorCodes.MIGRATION_FAILED)
    {
    }

    public MigrationException(string message, Exception innerException) : base(message, ErrorCodes.MIGRATION_FAILED, innerException)
    {
    }

    public MigrationException(string message, string? errorCode) : base(message, errorCode)
    {
    }

    public MigrationException(string message, string? errorCode, Exception innerException) : base(message, errorCode, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when migration validation fails.
/// </summary>
public sealed class MigrationValidationException : MigrationException
{
    public MigrationValidationException(string message) : base(message, ErrorCodes.MIGRATION_VALIDATION_FAILED)
    {
    }

    public MigrationValidationException(string message, Exception innerException) : base(message, ErrorCodes.MIGRATION_VALIDATION_FAILED, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a migration source has unsupported features.
/// </summary>
public sealed class UnsupportedMigrationSourceException : MigrationException
{
    public string? SourceType { get; }

    public UnsupportedMigrationSourceException(string message) : base(message, ErrorCodes.UNSUPPORTED_MIGRATION_SOURCE)
    {
    }

    public UnsupportedMigrationSourceException(string sourceType, string message)
        : base(message, ErrorCodes.UNSUPPORTED_MIGRATION_SOURCE)
    {
        SourceType = sourceType;
    }
}
