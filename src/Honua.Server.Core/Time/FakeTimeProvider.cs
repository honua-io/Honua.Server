// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;

namespace Honua.Server.Core.Time;

/// <summary>
/// Test implementation of <see cref="ITimeProvider"/> that allows controlling time for testing.
/// </summary>
public sealed class FakeTimeProvider : ITimeProvider
{
    private DateTime _utcNow;
    private DateTime _now;

    /// <summary>
    /// Initializes a new instance of <see cref="FakeTimeProvider"/> with the current time.
    /// </summary>
    public FakeTimeProvider()
        : this(DateTime.UtcNow, DateTime.Now)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="FakeTimeProvider"/> with a specific time.
    /// </summary>
    /// <param name="utcNow">The UTC time to use.</param>
    /// <param name="now">The local time to use.</param>
    public FakeTimeProvider(DateTime utcNow, DateTime now)
    {
        _utcNow = utcNow;
        _now = now;
    }

    /// <inheritdoc />
    public DateTime UtcNow
    {
        get => _utcNow;
        set => _utcNow = value;
    }

    /// <inheritdoc />
    public DateTime Now
    {
        get => _now;
        set => _now = value;
    }

    /// <summary>
    /// Advances the current time by the specified duration.
    /// </summary>
    /// <param name="duration">The amount of time to advance.</param>
    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
        _now = _now.Add(duration);
    }

    /// <summary>
    /// Sets the current time to a specific value.
    /// </summary>
    /// <param name="utcNow">The UTC time to set.</param>
    public void SetUtcNow(DateTime utcNow)
    {
        _utcNow = utcNow;
    }

    /// <summary>
    /// Sets the current local time to a specific value.
    /// </summary>
    /// <param name="now">The local time to set.</param>
    public void SetNow(DateTime now)
    {
        _now = now;
    }
}
