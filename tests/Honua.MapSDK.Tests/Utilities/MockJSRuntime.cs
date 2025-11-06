// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.JSInterop;
using System.Diagnostics.CodeAnalysis;

namespace Honua.MapSDK.Tests.Utilities;

/// <summary>
/// Mock IJSRuntime for testing JavaScript interop without a browser.
/// Tracks JS invocations and allows configuring return values.
/// </summary>
public class MockJSRuntime : IJSRuntime
{
    private readonly Dictionary<string, object?> _returnValues = new();
    private readonly List<JSInvocation> _invocations = new();

    public IReadOnlyList<JSInvocation> Invocations => _invocations.AsReadOnly();

    /// <summary>
    /// Configure a return value for a specific JS identifier
    /// </summary>
    public void SetupReturn<T>(string identifier, T value)
    {
        _returnValues[identifier] = value;
    }

    /// <summary>
    /// Get all invocations for a specific identifier
    /// </summary>
    public IEnumerable<JSInvocation> GetInvocations(string identifier)
    {
        return _invocations.Where(i => i.Identifier == identifier);
    }

    /// <summary>
    /// Clear all tracked invocations
    /// </summary>
    public void ClearInvocations()
    {
        _invocations.Clear();
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier,
        object?[]? args)
    {
        _invocations.Add(new JSInvocation
        {
            Identifier = identifier,
            Arguments = args ?? Array.Empty<object?>(),
            Timestamp = DateTime.UtcNow
        });

        if (_returnValues.TryGetValue(identifier, out var value) && value is TValue typedValue)
        {
            return ValueTask.FromResult(typedValue);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, args);
    }
}

/// <summary>
/// Mock IJSObjectReference for testing JS module imports
/// </summary>
public class MockJSObjectReference : IJSObjectReference, IJSInProcessObjectReference
{
    private readonly MockJSRuntime _mockRuntime;
    public string ModuleName { get; }

    public MockJSObjectReference(string moduleName)
    {
        ModuleName = moduleName;
        _mockRuntime = new MockJSRuntime();
    }

    public void SetupReturn<T>(string identifier, T value)
    {
        _mockRuntime.SetupReturn(identifier, value);
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier,
        object?[]? args)
    {
        return _mockRuntime.InvokeAsync<TValue>(identifier, args);
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        return _mockRuntime.InvokeAsync<TValue>(identifier, args);
    }

    public TValue Invoke<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier,
        params object?[]? args)
    {
        return _mockRuntime.InvokeAsync<TValue>(identifier, args).GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Record of a JavaScript invocation for testing verification
/// </summary>
public class JSInvocation
{
    public required string Identifier { get; init; }
    public required object?[] Arguments { get; init; }
    public DateTime Timestamp { get; init; }
}
