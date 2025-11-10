// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;

namespace Honua.Server.Core.Time;

/// <summary>
/// Provides access to current time for application logic.
/// This abstraction enables time-dependent code to be tested by injecting a fake time provider.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current Coordinated Universal Time (UTC).
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current local time.
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets the current date in UTC.
    /// </summary>
    DateOnly UtcToday => DateOnly.FromDateTime(UtcNow);

    /// <summary>
    /// Gets the current local date.
    /// </summary>
    DateOnly Today => DateOnly.FromDateTime(Now);
}
