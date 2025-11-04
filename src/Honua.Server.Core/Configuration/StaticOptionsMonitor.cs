// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// A simple IOptionsMonitor implementation that returns a static value.
/// Used for providing already-loaded configuration to the options pattern.
/// </summary>
internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    private readonly T _currentValue;

    public StaticOptionsMonitor(T currentValue)
    {
        _currentValue = currentValue ?? throw new ArgumentNullException(nameof(currentValue));
    }

    public T CurrentValue => _currentValue;

    public T Get(string? name) => _currentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
