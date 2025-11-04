// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Utilities;

/// <summary>
/// Centralized exception handling utility that standardizes try-catch-log patterns.
/// Provides consistent exception transformation, logging, and error handling across the codebase.
///
/// <para>
/// This utility consolidates 27+ exception handling patterns found in health checks,
/// metadata providers, and services. It supports both synchronous and asynchronous operations
/// with optional exception transformation and logging.
/// </para>
///
/// <para>
/// Key features:
/// - Preserves stack traces when rethrowing exceptions
/// - Optional exception type transformation (e.g., mapping external service exceptions to domain exceptions)
/// - Contextual logging with operation names
/// - Support for both sync and async operations
/// - Thread-safe and allocation-efficient
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Basic usage with exception transformation
/// var result = ExceptionHandler.ExecuteWithMapping(
///     () => ExternalService.Call(),
///     ex => new InvalidOperationException("Service failed", ex),
///     logger,
///     "external service call");
///
/// // Async usage
/// var result = await ExceptionHandler.ExecuteWithMappingAsync(
///     async () => await repository.LoadAsync(),
///     ex => ex switch
///     {
///         RedisException rex => new InvalidOperationException("Redis connection failed", rex),
///         _ => ex
///     },
///     logger,
///     "metadata load");
/// </code>
/// </example>
public static class ExceptionHandler
{
    /// <summary>
    /// Executes a synchronous operation with optional exception transformation and logging.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="exceptionTransformer">
    /// Optional function to transform exceptions. If null, exceptions are rethrown as-is.
    /// The transformer receives the caught exception and returns a new exception to throw.
    /// Return the original exception to rethrow it unchanged.
    /// </param>
    /// <param name="logger">Optional logger for exception logging</param>
    /// <param name="operationName">Optional operation name for logging context</param>
    /// <returns>The result of the operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation is null</exception>
    /// <remarks>
    /// This method preserves stack traces using ExceptionDispatchInfo.
    /// If an exception transformer is provided, it will be invoked before logging.
    /// The original exception is always logged, even if transformed.
    /// </remarks>
    public static T ExecuteWithMapping<T>(
        Func<T> operation,
        Func<Exception, Exception>? exceptionTransformer = null,
        ILogger? logger = null,
        string? operationName = null)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            return HandleException<T>(ex, exceptionTransformer, logger, operationName);
        }
    }

    /// <summary>
    /// Executes an asynchronous operation with optional exception transformation and logging.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="exceptionTransformer">
    /// Optional function to transform exceptions. If null, exceptions are rethrown as-is.
    /// The transformer receives the caught exception and returns a new exception to throw.
    /// Return the original exception to rethrow it unchanged.
    /// </param>
    /// <param name="logger">Optional logger for exception logging</param>
    /// <param name="operationName">Optional operation name for logging context</param>
    /// <returns>A task representing the async operation result</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation is null</exception>
    /// <remarks>
    /// This method preserves stack traces using ExceptionDispatchInfo.
    /// If an exception transformer is provided, it will be invoked before logging.
    /// The original exception is always logged, even if transformed.
    /// </remarks>
    public static async Task<T> ExecuteWithMappingAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, Exception>? exceptionTransformer = null,
        ILogger? logger = null,
        string? operationName = null)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return HandleException<T>(ex, exceptionTransformer, logger, operationName);
        }
    }

    /// <summary>
    /// Executes a synchronous void operation with optional exception transformation and logging.
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="exceptionTransformer">
    /// Optional function to transform exceptions. If null, exceptions are rethrown as-is.
    /// </param>
    /// <param name="logger">Optional logger for exception logging</param>
    /// <param name="operationName">Optional operation name for logging context</param>
    /// <exception cref="ArgumentNullException">Thrown when operation is null</exception>
    public static void ExecuteWithMapping(
        Action operation,
        Func<Exception, Exception>? exceptionTransformer = null,
        ILogger? logger = null,
        string? operationName = null)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        try
        {
            operation();
        }
        catch (Exception ex)
        {
            HandleException<object>(ex, exceptionTransformer, logger, operationName);
        }
    }

    /// <summary>
    /// Executes an asynchronous void operation with optional exception transformation and logging.
    /// </summary>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="exceptionTransformer">
    /// Optional function to transform exceptions. If null, exceptions are rethrown as-is.
    /// </param>
    /// <param name="logger">Optional logger for exception logging</param>
    /// <param name="operationName">Optional operation name for logging context</param>
    /// <exception cref="ArgumentNullException">Thrown when operation is null</exception>
    public static async Task ExecuteWithMappingAsync(
        Func<Task> operation,
        Func<Exception, Exception>? exceptionTransformer = null,
        ILogger? logger = null,
        string? operationName = null)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            HandleException<object>(ex, exceptionTransformer, logger, operationName);
        }
    }

    /// <summary>
    /// Executes a synchronous operation and returns a result indicating success or failure,
    /// without throwing exceptions.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="logger">Optional logger for exception logging</param>
    /// <param name="operationName">Optional operation name for logging context</param>
    /// <returns>A result containing either the value or the exception</returns>
    public static OperationResult<T> ExecuteSafe<T>(
        Func<T> operation,
        ILogger? logger = null,
        string? operationName = null)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        try
        {
            var result = operation();
            return OperationResult<T>.Success(result);
        }
        catch (Exception ex)
        {
            LogException(ex, logger, operationName);
            return OperationResult<T>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an asynchronous operation and returns a result indicating success or failure,
    /// without throwing exceptions.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="logger">Optional logger for exception logging</param>
    /// <param name="operationName">Optional operation name for logging context</param>
    /// <returns>A task containing a result with either the value or the exception</returns>
    public static async Task<OperationResult<T>> ExecuteSafeAsync<T>(
        Func<Task<T>> operation,
        ILogger? logger = null,
        string? operationName = null)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        try
        {
            var result = await operation().ConfigureAwait(false);
            return OperationResult<T>.Success(result);
        }
        catch (Exception ex)
        {
            LogException(ex, logger, operationName);
            return OperationResult<T>.Failure(ex);
        }
    }

    private static T HandleException<T>(
        Exception exception,
        Func<Exception, Exception>? exceptionTransformer,
        ILogger? logger,
        string? operationName)
    {
        // Log the original exception
        LogException(exception, logger, operationName);

        // Transform exception if transformer provided
        var exceptionToThrow = exceptionTransformer?.Invoke(exception) ?? exception;

        // Preserve stack trace when rethrowing
        if (ReferenceEquals(exception, exceptionToThrow))
        {
            // Same exception - use ExceptionDispatchInfo to preserve stack trace
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        // Transformed exception - throw normally (new exception has its own stack trace)
        throw exceptionToThrow;
    }

    private static void LogException(Exception exception, ILogger? logger, string? operationName)
    {
        if (logger == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(operationName))
        {
            logger.LogError(exception, "Operation failed with exception");
        }
        else
        {
            logger.LogError(exception, "Operation '{OperationName}' failed with exception", operationName);
        }
    }
}

/// <summary>
/// Represents the result of an operation that may succeed or fail.
/// Used by safe execution methods that don't throw exceptions.
/// </summary>
/// <typeparam name="T">The type of the result value</typeparam>
public readonly struct OperationResult<T> : IEquatable<OperationResult<T>>
{
    private readonly T? _value;
    private readonly Exception? _exception;

    private OperationResult(T? value, Exception? exception, bool isSuccess)
    {
        _value = value;
        _exception = exception;
        IsSuccess = isSuccess;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the result value. Only valid when IsSuccess is true.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result</exception>
    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException(
                    "Cannot access Value on a failed result. Check IsSuccess before accessing Value.");
            }
            return _value!;
        }
    }

    /// <summary>
    /// Gets the exception that caused the failure. Only valid when IsFailure is true.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Exception on a successful result</exception>
    public Exception Exception
    {
        get
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException(
                    "Cannot access Exception on a successful result. Check IsFailure before accessing Exception.");
            }
            return _exception!;
        }
    }

    /// <summary>
    /// Tries to get the value if the operation succeeded.
    /// </summary>
    /// <param name="value">The result value if successful, default otherwise</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    public bool TryGetValue(out T? value)
    {
        value = _value;
        return IsSuccess;
    }

    /// <summary>
    /// Tries to get the exception if the operation failed.
    /// </summary>
    /// <param name="exception">The exception if failed, null otherwise</param>
    /// <returns>True if the operation failed, false otherwise</returns>
    public bool TryGetException(out Exception? exception)
    {
        exception = _exception;
        return IsFailure;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static OperationResult<T> Success(T value) =>
        new(value, null, true);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static OperationResult<T> Failure(Exception exception)
    {
        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }
        return new(default, exception, false);
    }

    /// <summary>
    /// Executes a function on the value if the operation succeeded.
    /// </summary>
    public OperationResult<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        if (mapper == null)
        {
            throw new ArgumentNullException(nameof(mapper));
        }

        return IsSuccess
            ? OperationResult<TResult>.Success(mapper(_value!))
            : OperationResult<TResult>.Failure(_exception!);
    }

    /// <summary>
    /// Returns the value if successful, or a default value if failed.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) =>
        IsSuccess ? _value! : defaultValue;

    /// <summary>
    /// Returns the value if successful, or the result of the fallback function if failed.
    /// </summary>
    public T GetValueOrDefault(Func<Exception, T> fallback)
    {
        if (fallback == null)
        {
            throw new ArgumentNullException(nameof(fallback));
        }

        return IsSuccess ? _value! : fallback(_exception!);
    }

    /// <summary>
    /// Determines whether the specified <see cref="OperationResult{T}"/> is equal to the current instance.
    /// </summary>
    public bool Equals(OperationResult<T> other)
    {
        if (IsSuccess != other.IsSuccess)
        {
            return false;
        }

        if (IsSuccess)
        {
            return EqualityComparer<T>.Default.Equals(_value, other._value);
        }

        // Compare exceptions by reference for failed results
        return ReferenceEquals(_exception, other._exception);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is OperationResult<T> other && Equals(other);
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override int GetHashCode()
    {
        if (IsSuccess)
        {
            return HashCode.Combine(IsSuccess, _value);
        }

        return HashCode.Combine(IsSuccess, _exception);
    }

    /// <summary>
    /// Determines whether two <see cref="OperationResult{T}"/> instances are equal.
    /// </summary>
    public static bool operator ==(OperationResult<T> left, OperationResult<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="OperationResult{T}"/> instances are not equal.
    /// </summary>
    public static bool operator !=(OperationResult<T> left, OperationResult<T> right)
    {
        return !left.Equals(right);
    }
}
