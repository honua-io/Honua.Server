// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain;

/// <summary>
/// Base exception class for all domain-specific exceptions.
/// Domain exceptions represent violations of domain rules and invariants.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Gets the error code associated with this domain exception.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets additional context or metadata associated with this exception.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    public DomainException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DomainException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with a specified error message
    /// and error code.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="errorCode">The error code associated with this exception.</param>
    public DomainException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with a specified error message,
    /// error code, and additional context.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="errorCode">The error code associated with this exception.</param>
    /// <param name="context">Additional context or metadata for the exception.</param>
    public DomainException(string message, string errorCode, Dictionary<string, object> context)
        : base(message)
    {
        ErrorCode = errorCode;
        Context = context;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with a specified error message,
    /// error code, inner exception, and additional context.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="errorCode">The error code associated with this exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="context">Additional context or metadata for the exception.</param>
    public DomainException(
        string message,
        string errorCode,
        Exception innerException,
        Dictionary<string, object>? context = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Context = context;
    }
}
