// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Marker interface for exceptions that represent transient failures that can be retried.
/// </summary>
public interface ITransientException
{
    /// <summary>
    /// Gets a value indicating whether this exception represents a transient failure.
    /// </summary>
    bool IsTransient { get; }
}
