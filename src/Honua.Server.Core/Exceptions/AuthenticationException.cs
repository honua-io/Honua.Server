// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for authentication failures.
/// </summary>
public class AuthenticationException : HonuaException
{
    public AuthenticationException(string message) : base(message, "AUTHENTICATION_FAILED")
    {
    }

    public AuthenticationException(string message, Exception innerException) : base(message, "AUTHENTICATION_FAILED", innerException)
    {
    }

    public AuthenticationException(string message, string? errorCode) : base(message, errorCode)
    {
    }

    public AuthenticationException(string message, string? errorCode, Exception innerException) : base(message, errorCode, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when credentials are invalid.
/// This is NOT a transient error.
/// </summary>
public sealed class InvalidCredentialsException : AuthenticationException
{
    public string? Username { get; }

    public InvalidCredentialsException(string message)
        : base(message, "INVALID_CREDENTIALS")
    {
    }

    public InvalidCredentialsException(string username, string message)
        : base(message, "INVALID_CREDENTIALS")
    {
        Username = username;
    }
}

/// <summary>
/// Exception thrown when an authentication token is invalid or expired.
/// This is NOT a transient error.
/// </summary>
public sealed class InvalidTokenException : AuthenticationException
{
    public string? TokenType { get; }

    public InvalidTokenException(string message)
        : base(message, "INVALID_TOKEN")
    {
    }

    public InvalidTokenException(string tokenType, string message)
        : base(message, "INVALID_TOKEN")
    {
        TokenType = tokenType;
    }

    public InvalidTokenException(string message, Exception innerException)
        : base(message, "INVALID_TOKEN", innerException)
    {
    }
}

/// <summary>
/// Exception thrown when authentication service is unavailable.
/// This is a transient error that can be retried.
/// </summary>
public sealed class AuthenticationServiceUnavailableException : AuthenticationException, ITransientException
{
    public string? ServiceName { get; }
    public bool IsTransient => true;

    public AuthenticationServiceUnavailableException(string message)
        : base(message, "AUTH_SERVICE_UNAVAILABLE")
    {
    }

    public AuthenticationServiceUnavailableException(string serviceName, string message, Exception innerException)
        : base(message, "AUTH_SERVICE_UNAVAILABLE", innerException)
    {
        ServiceName = serviceName;
    }
}

/// <summary>
/// Exception thrown when multi-factor authentication fails.
/// This is NOT a transient error.
/// </summary>
public sealed class MfaFailedException : AuthenticationException
{
    public MfaFailedException(string message)
        : base(message, "MFA_FAILED")
    {
    }

    public MfaFailedException(string message, Exception innerException)
        : base(message, "MFA_FAILED", innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a user account is locked.
/// This is NOT a transient error.
/// </summary>
public sealed class AccountLockedException : AuthenticationException
{
    public string? Username { get; }
    public DateTime? LockoutEnd { get; }

    public AccountLockedException(string username, DateTime? lockoutEnd = null)
        : base($"Account '{username}' is locked{(lockoutEnd.HasValue ? $" until {lockoutEnd.Value:u}" : "")}", "ACCOUNT_LOCKED")
    {
        Username = username;
        LockoutEnd = lockoutEnd;
    }
}
