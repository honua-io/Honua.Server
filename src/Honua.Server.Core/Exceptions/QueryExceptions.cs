// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Exception thrown when a query operation fails.
/// </summary>
public class QueryException : HonuaException
{
    public QueryException(string message) : base(message)
    {
    }

    public QueryException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when query filter parsing fails.
/// </summary>
public sealed class QueryFilterParseException : QueryException
{
    public QueryFilterParseException(string message) : base(message)
    {
    }

    public QueryFilterParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a field is not found in a query entity definition.
/// </summary>
public sealed class FieldNotFoundException : QueryException
{
    public string FieldName { get; }
    public string? EntityName { get; }

    public FieldNotFoundException(string fieldName, string? entityName = null)
        : base(entityName is null
            ? $"Field '{fieldName}' is not defined."
            : $"Field '{fieldName}' is not defined on entity '{entityName}'.")
    {
        FieldName = fieldName;
        EntityName = entityName;
    }
}

/// <summary>
/// Exception thrown when a query operation is not supported.
/// </summary>
public sealed class QueryOperationNotSupportedException : QueryException
{
    public string Operation { get; }

    public QueryOperationNotSupportedException(string operation, string message)
        : base(message)
    {
        Operation = operation;
    }
}
