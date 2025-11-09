// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;

namespace Honua.Server.Core.Time;

/// <summary>
/// Production implementation of <see cref="ITimeProvider"/> that returns actual system time.
/// </summary>
public sealed class SystemTimeProvider : ITimeProvider
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime Now => DateTime.Now;
}
